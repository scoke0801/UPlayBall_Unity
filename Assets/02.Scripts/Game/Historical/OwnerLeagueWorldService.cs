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
            int seasonNumber = runtime.ManagerMode.LiveSeason.SeasonNumber;
            var fillers = new OwnerLeagueFillerFactory(_balance.LeaguePromotion, runtime.WorldCardCatalog,
                runtime.WorldHistory.WorldHistorySeed, rosters, references, nextId);
            string[][] drawn = _resolver.DrawGroups(added, _balance.LeaguePromotion.GroupTeamCount,
                new Pcg32Random(DeterministicSeed.Derive(runtime.WorldHistory.WorldHistorySeed, DrawStream)));
            for (int index = 0; index < drawn.Length; index++)
                groups.Add(CreateGroup(runtime, LeagueGrade.Rookie, index + 1,
                    fillers.Fill(LeagueGrade.Rookie, index + 1, seasonNumber, drawn[index]), references,
                    new Dictionary<string, SpecialCompositeTeamRegistration>(), seasonNumber));
            var world = new OwnerLeagueWorldState(groups, rosters);
            world.PlayerIds = ManagerModeMatchService.PlayerIdMap.Create(runtime.Rosters);
            world.PlayerIds.EnsureRosters(world.Rosters);
            runtime.ManagerMode.SyncWorldTeamStates(world.Rosters, _balance.ConditionChemistry.NeutralMatchCondition);
            runtime.SetLeagueWorld(world);
        }

        /// <summary>
        /// 완료한 모든 조의 순위를 먼저 확정하고 승강·재추첨된 다음 시즌 계획을 반환한다. 런타임 상태는 바꾸지 않는다.
        /// 특수 합성팀과 CPU 임시 구단은 이번 시즌으로 끝나며, 다음 시즌 조의 빈 자리는 새 CPU 임시 구단이 채운다.
        /// </summary>
        public OwnerLeagueWorldState PlanNextSeason(ManagerHistoricalRuntimeState runtime)
        {
            OwnerLeagueWorldState world = RequireCompletedWorld(runtime);
            NextSeasonAllocation allocation = ResolveAllocation(world);
            int seasonNumber = checked(runtime.ManagerMode.LiveSeason.SeasonNumber + 1);

            var rosters = new List<CurrentRosterState>();
            foreach (CurrentRosterState roster in world.Rosters)
                if (allocation.NextGrades.ContainsKey(roster.TeamSeasonKey)) rosters.Add(roster);
            var fillers = new OwnerLeagueFillerFactory(_balance.LeaguePromotion, runtime.WorldCardCatalog,
                runtime.WorldHistory.WorldHistorySeed, rosters, allocation.References, GetNextTeamId(world));

            var groups = new List<OwnerLeagueGroupState>();
            var noSpecials = new Dictionary<string, SpecialCompositeTeamRegistration>();
            for (int grade = 0; grade <= (int)LeagueGrade.Galaxy; grade++)
            {
                var keys = new List<string>();
                foreach (string key in allocation.OrderedKeys) if ((int)allocation.NextGrades[key] == grade) keys.Add(key);
                ulong seed = DeterministicSeed.Derive(DeterministicSeed.Derive(runtime.WorldHistory.WorldHistorySeed, DrawStream),
                    ((ulong)(uint)seasonNumber << 32) | (uint)grade);
                string[][] drawn = _resolver.DrawGroups(keys, _balance.LeaguePromotion.GroupTeamCount, new Pcg32Random(seed),
                    allocation.PreviousGroups, _balance.LeaguePromotion.GroupRepeatAvoidanceChance);
                for (int index = 0; index < drawn.Length; index++)
                    groups.Add(CreateGroup(runtime, (LeagueGrade)grade, index,
                        fillers.Fill((LeagueGrade)grade, index, seasonNumber, drawn[index]), allocation.References,
                        noSpecials, seasonNumber));
            }
            var history = new List<OwnerLeagueGroupState>(world.CompletedGroups);
            history.AddRange(world.Groups);
            return new OwnerLeagueWorldState(groups, rosters, history) { PlayerIds = world.PlayerIds ?? ManagerModeMatchService.PlayerIdMap.Create(world.Rosters) };
        }

        /// <summary>시즌 결산 화면용으로 CPU 임시 구단 생성 없이 한 구단의 다음 시즌 등급만 계산한다.</summary>
        public LeagueGrade ResolveNextGrade(ManagerHistoricalRuntimeState runtime, string teamSeasonKey)
        {
            NextSeasonAllocation allocation = ResolveAllocation(RequireCompletedWorld(runtime));
            return allocation.NextGrades.TryGetValue(teamSeasonKey, out LeagueGrade grade)
                ? grade
                : throw new KeyNotFoundException(teamSeasonKey);
        }

        /// <summary>
        /// 계획한 다음 시즌 월드를 확정한다. 사라진 임시 구단의 컨디션·친밀도는 버리고 새 임시 구단 상태를 만든다.
        /// 선수 ID 원장은 지난 시즌 기록이 계속 참조하므로 지우지 않는다.
        /// </summary>
        public void CommitNextSeason(ManagerHistoricalRuntimeState runtime, OwnerLeagueWorldState nextWorld)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (nextWorld == null) throw new ArgumentNullException(nameof(nextWorld));
            runtime.ManagerMode.SyncWorldTeamStates(nextWorld.Rosters, _balance.ConditionChemistry.NeutralMatchCondition);
            runtime.SetLeagueWorld(nextWorld);
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

        private static OwnerLeagueWorldState RequireCompletedWorld(ManagerHistoricalRuntimeState runtime)
        {
            OwnerLeagueWorldState world = runtime.LeagueWorld ?? new OwnerLeagueWorldState(
                new[] { new OwnerLeagueGroupState(runtime.League, runtime.ManagerMode.LiveSeason) }, runtime.Rosters);
            if (!world.IsCompleted) throw new InvalidOperationException("모든 조의 정규시즌과 포스트시즌 종료가 필요합니다.");
            return world;
        }

        /// <summary>
        /// 순위표에 보이는 순위 그대로 승강을 판정한다. CPU 임시 구단도 순위를 차지하므로 CPU가 승격 순위에 들면
        /// 그 승격 자리는 사라지고, 실제 구단은 자기 순위가 가리키는 결과만 받는다.
        /// </summary>
        private NextSeasonAllocation ResolveAllocation(OwnerLeagueWorldState world)
        {
            var allocation = new NextSeasonAllocation();
            for (int groupIndex = 0; groupIndex < world.Groups.Count; groupIndex++)
            {
                OwnerLeagueGroupState group = world.Groups[groupIndex];
                OwnerLeagueStanding[] ranking = Rank(group.Season);
                for (int index = 0; index < ranking.Length; index++)
                {
                    string key = ranking[index].TeamKey;
                    if (!group.League.IsPermanentParticipant(key)) continue;
                    allocation.PreviousGroups.Add(key, groupIndex);
                    allocation.NextGrades.Add(key, _resolver.ResolveGrade(group.League.Grade, index + 1, ranking.Length,
                        _balance.LeaguePromotion));
                    allocation.OrderedKeys.Add(key);
                }
                foreach (var team in group.Season.Teams)
                    if (group.League.IsPermanentParticipant(team.TeamSeasonKey))
                        allocation.References.Add(team.TeamSeasonKey, team);
            }
            allocation.OrderedKeys.Sort(StringComparer.Ordinal);
            return allocation;
        }

        /// <summary>임시 구단 TeamId가 지난 시즌 기록의 구단과 겹치지 않도록 이력 전체의 최댓값 다음부터 발급한다.</summary>
        private static int GetNextTeamId(OwnerLeagueWorldState world)
        {
            int maximum = 0;
            foreach (var group in world.Groups)
                foreach (var team in group.Season.Teams) maximum = Math.Max(maximum, team.TeamId);
            foreach (var group in world.CompletedGroups)
                foreach (var team in group.Season.Teams) maximum = Math.Max(maximum, team.TeamId);
            return checked(maximum + 1);
        }

        private OwnerLeagueGroupState CreateGroup(ManagerHistoricalRuntimeState runtime, LeagueGrade grade, int groupIndex,
            string[] keys, IReadOnlyDictionary<string, ManagerTeamReference> references,
            IReadOnlyDictionary<string, SpecialCompositeTeamRegistration> specials, int seasonNumber)
        {
            var regular = new List<string>();
            var composite = new List<SpecialCompositeTeamRegistration>();
            var fillers = new List<string>();
            var teams = new List<ManagerTeamReference>();
            foreach (string key in keys)
            {
                teams.Add(references[key]);
                if (LeagueFillerTeamKey.IsFillerKey(key)) fillers.Add(key);
                else if (specials.TryGetValue(key, out var special)) composite.Add(special);
                else regular.Add(key);
            }
            teams.Sort((a, b) => a.TeamId.CompareTo(b.TeamId));
            string id = $"owner:{(int)grade:D2}:{groupIndex:D4}";
            var league = new LeagueInstance(id, grade, regular, composite, isPooledGroup: true, fillerTeamSeasonKeys: fillers);
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

        private sealed class NextSeasonAllocation
        {
            public readonly Dictionary<string, LeagueGrade> NextGrades = new Dictionary<string, LeagueGrade>(StringComparer.Ordinal);
            public readonly Dictionary<string, int> PreviousGroups = new Dictionary<string, int>(StringComparer.Ordinal);
            public readonly Dictionary<string, ManagerTeamReference> References =
                new Dictionary<string, ManagerTeamReference>(StringComparer.Ordinal);
            public readonly List<string> OrderedKeys = new List<string>();
        }
    }
}
