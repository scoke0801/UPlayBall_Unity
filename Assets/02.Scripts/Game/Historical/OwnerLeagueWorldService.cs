using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Historical;
using Baseball.Game.Career;
using Baseball.Simulation.Historical;
using Baseball.Simulation.Random;

namespace Baseball.Game.Historical
{
    /// <summary>전체 역사 구단의 월드 등록과 순위 승강 후 등급별 조 재추첨을 담당한다.</summary>
    public sealed class OwnerLeagueWorldService
    {
        private const ulong DrawStream = 0x4F574E4552445241UL;
        private readonly BalanceTable _balance;
        private readonly OwnerLeagueAllocationResolver _resolver = new OwnerLeagueAllocationResolver();

        public OwnerLeagueWorldService(BalanceTable balance)
        {
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
        }

        /// <summary>현재 진행 중인 조는 보존하고 나머지 모든 연도별 정규 구단을 Rookie 조에 등록한다.</summary>
        public void Initialize(ManagerHistoricalRuntimeState runtime, HistoricalBakedContent content)
        {
            if (runtime.LeagueWorld != null) return;
            var groups = new List<OwnerLeagueGroupState> { new OwnerLeagueGroupState(runtime.League, runtime.ManagerMode.LiveSeason) };
            var rosters = new List<CurrentRosterState>(runtime.Rosters);
            var references = new Dictionary<string, ManagerTeamReference>(StringComparer.Ordinal);
            int nextId = 1;
            foreach (var team in runtime.ManagerMode.LiveSeason.Teams)
            {
                references.Add(team.TeamSeasonKey, team);
                nextId = Math.Max(nextId, team.TeamId + 1);
            }
            var definitions = new List<TeamSeasonDefinition>(content.TeamSeasons);
            definitions.Sort((a, b) => string.CompareOrdinal(a.TeamSeasonKey, b.TeamSeasonKey));
            var added = new List<string>();
            foreach (var definition in definitions)
            {
                if (references.ContainsKey(definition.TeamSeasonKey)) continue;
                rosters.Add(ManagerHistoricalNewGameService.CreateRegularRoster(definition, runtime.WorldCardCatalog));
                references.Add(definition.TeamSeasonKey, new ManagerTeamReference(nextId++, definition.TeamSeasonKey));
                added.Add(definition.TeamSeasonKey);
            }
            if (added.Count == 1)
                throw new InvalidOperationException("현재 조를 보존하면서 추가할 구단이 한 개뿐이어서 월드를 편성할 수 없습니다.");
            string[][] drawn = _resolver.DrawGroups(added, _balance.LeaguePromotion.GroupTeamCount,
                new Pcg32Random(DeterministicSeed.Derive(runtime.WorldHistory.WorldHistorySeed, DrawStream)));
            for (int index = 0; index < drawn.Length; index++)
                groups.Add(CreateGroup(runtime, LeagueGrade.Rookie, index + 1, drawn[index], references,
                    new Dictionary<string, SpecialCompositeTeamRegistration>(), runtime.ManagerMode.LiveSeason.SeasonNumber));
            var world = new OwnerLeagueWorldState(groups, rosters);
            world.PlayerIds = ManagerModeMatchService.PlayerIdMap.Create(runtime.Rosters);
            world.PlayerIds.EnsureRosters(world.Rosters);
            runtime.ManagerMode.EnsureWorldTeamStates(world.Rosters, _balance.ConditionChemistry.NeutralMatchCondition);
            runtime.SetLeagueWorld(world);
        }

        /// <summary>완료한 모든 조의 순위를 먼저 확정하고 승강·재추첨된 다음 시즌 계획을 반환한다.</summary>
        public OwnerLeagueWorldState PlanNextSeason(ManagerHistoricalRuntimeState runtime)
        {
            OwnerLeagueWorldState world = runtime.LeagueWorld ?? new OwnerLeagueWorldState(
                new[] { new OwnerLeagueGroupState(runtime.League, runtime.ManagerMode.LiveSeason) }, runtime.Rosters);
            if (!world.IsCompleted) throw new InvalidOperationException("모든 조의 정규시즌 종료가 필요합니다.");
            var previous = new Dictionary<string, LeagueGrade>(StringComparer.Ordinal);
            var next = new Dictionary<string, LeagueGrade>(StringComparer.Ordinal);
            var references = new Dictionary<string, ManagerTeamReference>(StringComparer.Ordinal);
            var specials = new Dictionary<string, SpecialCompositeTeamRegistration>(StringComparer.Ordinal);
            var orderedKeys = new List<string>();
            foreach (var group in world.Groups)
            {
                foreach (var special in group.League.SpecialCompositeTeams) specials.Add(special.TeamSeasonKey, special);
                OwnerLeagueStanding[] ranking = Rank(group.Season);
                for (int index = 0; index < ranking.Length; index++)
                {
                    string key = ranking[index].TeamKey;
                    previous.Add(key, group.League.Grade);
                    next.Add(key, _resolver.ResolveGrade(group.League.Grade, index + 1, ranking.Length, _balance.LeaguePromotion));
                    orderedKeys.Add(key);
                }
                foreach (var team in group.Season.Teams) references.Add(team.TeamSeasonKey, team);
            }
            orderedKeys.Sort(StringComparer.Ordinal);
            // 새로 생긴 등급에 한 팀만 진입하면 상대가 없다. 해당 팀만 기존 등급에 잔류시킨다.
            // 각 조에 최소 두 팀을 남기므로 돌려보내는 원래 등급에는 항상 상대가 있다.
            for (int grade = 0; grade <= (int)LeagueGrade.Galaxy; grade++)
            {
                string single = null;
                int count = 0;
                foreach (string key in orderedKeys) if ((int)next[key] == grade) { single = key; count++; }
                if (count == 1) next[single] = previous[single];
            }
            int seasonNumber = checked(runtime.ManagerMode.LiveSeason.SeasonNumber + 1);
            var groups = new List<OwnerLeagueGroupState>();
            for (int grade = 0; grade <= (int)LeagueGrade.Galaxy; grade++)
            {
                var keys = new List<string>();
                foreach (string key in orderedKeys) if ((int)next[key] == grade) keys.Add(key);
                ulong seed = DeterministicSeed.Derive(DeterministicSeed.Derive(runtime.WorldHistory.WorldHistorySeed, DrawStream),
                    ((ulong)(uint)seasonNumber << 32) | (uint)grade);
                string[][] drawn = _resolver.DrawGroups(keys, _balance.LeaguePromotion.GroupTeamCount, new Pcg32Random(seed));
                for (int index = 0; index < drawn.Length; index++)
                    groups.Add(CreateGroup(runtime, (LeagueGrade)grade, index, drawn[index], references, specials, seasonNumber));
            }
            var history = new List<OwnerLeagueGroupState>(world.CompletedGroups);
            history.AddRange(world.Groups);
            return new OwnerLeagueWorldState(groups, world.Rosters, history) { PlayerIds = world.PlayerIds ?? ManagerModeMatchService.PlayerIdMap.Create(world.Rosters) };
        }

        /// <summary>화면과 승강 판정이 공유하는 정규시즌 최종 순위를 반환한다.</summary>
        public OwnerLeagueStanding[] Rank(ManagerLiveSeasonState season)
        {
            var records = new Dictionary<int, int[]>();
            foreach (var team in season.Teams) records.Add(team.TeamId, new int[3]);
            foreach (var game in season.Schedule.Games)
            {
                if (!game.IsCompleted) continue;
                int[] away = records[game.AwayTeamId];
                int[] home = records[game.HomeTeamId];
                if (game.AwayRuns > game.HomeRuns) { away[0]++; home[1]++; }
                else if (game.HomeRuns > game.AwayRuns) { home[0]++; away[1]++; }
                away[2] += game.AwayRuns - game.HomeRuns;
                home[2] += game.HomeRuns - game.AwayRuns;
            }
            var standings = new List<OwnerLeagueStanding>();
            foreach (var team in season.Teams)
            {
                int[] record = records[team.TeamId];
                standings.Add(new OwnerLeagueStanding(team.TeamSeasonKey, record[0], record[1], record[2]));
            }
            return _resolver.Rank(standings);
        }

        private OwnerLeagueGroupState CreateGroup(ManagerHistoricalRuntimeState runtime, LeagueGrade grade, int groupIndex,
            string[] keys, IReadOnlyDictionary<string, ManagerTeamReference> references,
            IReadOnlyDictionary<string, SpecialCompositeTeamRegistration> specials, int seasonNumber)
        {
            var regular = new List<string>();
            var composite = new List<SpecialCompositeTeamRegistration>();
            var teams = new List<ManagerTeamReference>();
            foreach (string key in keys)
            {
                teams.Add(references[key]);
                if (specials.TryGetValue(key, out var special)) composite.Add(special);
                else regular.Add(key);
            }
            teams.Sort((a, b) => a.TeamId.CompareTo(b.TeamId));
            string id = $"owner:{(int)grade:D2}:{groupIndex:D4}";
            var league = new LeagueInstance(id, grade, regular, composite, isPooledGroup: true);
            ManagerLiveSeasonState current = runtime.ManagerMode.LiveSeason;
            int focusTeam = references[runtime.PlayerTeamSeasonKey].TeamId;
            bool hasPlayer = Array.IndexOf(keys, runtime.PlayerTeamSeasonKey) >= 0;
            ulong seed = DeterministicSeed.Derive(runtime.WorldHistory.WorldHistorySeed, (ulong)(uint)teams[0].TeamId);
            SeasonScheduleState schedule = ManagerModeRuntimeFactory.CreateSchedule(teams, seed, seasonNumber,
                _balance.CareerSeason.RegularSeasonGamesPerTeam);
            var season = new ManagerLiveSeasonState($"manager:{current.OriginYear}:{seasonNumber}:{id}", seasonNumber,
                current.OriginYear, 0, hasPlayer ? focusTeam : teams[0].TeamId, teams, schedule);
            return new OwnerLeagueGroupState(league, season);
        }
    }
}
