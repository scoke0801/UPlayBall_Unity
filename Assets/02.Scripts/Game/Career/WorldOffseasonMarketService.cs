using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Career;
using Baseball.Simulation.Random;

namespace Baseball.Game.Career
{
    /// <summary>세 리그의 은퇴·승격 충원·Rookie 신인 유입을 하나의 결정론적 시장 계획으로 계산한다.</summary>
    public sealed class WorldOffseasonMarketService
    {
        private const ulong RetirementStream = 0x5245544952454D54UL;
        private const ulong RookieEntryStream = 0x524F4F4B49454E54UL;

        private readonly BalanceTable _balance;
        private readonly PlayerValueEvaluator _playerValueEvaluator;

        public WorldOffseasonMarketService(BalanceTable balance)
        {
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            _playerValueEvaluator = new PlayerValueEvaluator(balance.PlayerEvaluation);
        }

        /// <summary>현재 월드를 변경하지 않고 다음 시즌의 전체 AI 시장 결과를 만든다.</summary>
        public WorldOffseasonMarketPlan CreatePlan(WorldState world, int myPlayerId, int nextYear)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (myPlayerId <= 0) throw new ArgumentOutOfRangeException(nameof(myPlayerId));
            if (nextYear <= 0) throw new ArgumentOutOfRangeException(nameof(nextYear));

            var rollover = new LeagueSeasonRolloverService(_balance);
            if (world.Leagues.Count == 1)
                return CreateSingleLeagueCompatibilityPlan(world, rollover);
            if (world.Leagues.Count != 3)
                throw new InvalidOperationException("기본 월드 시장에는 Rookie, Minor, Major 세 리그가 모두 필요합니다.");

            LeagueState rookieLeague = world.GetLeague(LeagueId.RookieMain);
            LeagueState minorLeague = world.GetLeague(LeagueId.MinorMain);
            LeagueState majorLeague = world.GetLeague(LeagueId.MajorMain);
            TeamRosterBuilder[] rookie = CreateBuilders(
                rollover.AdvanceRosters(rookieLeague, world, rookieLeague.CurrentSeason.SeasonId + 1));
            TeamRosterBuilder[] minor = CreateBuilders(
                rollover.AdvanceRosters(minorLeague, world, minorLeague.CurrentSeason.SeasonId + 1));
            TeamRosterBuilder[] major = CreateBuilders(
                rollover.AdvanceRosters(majorLeague, world, majorLeague.CurrentSeason.SeasonId + 1));

            RebalanceLeagueDepth(rookie);
            RebalanceLeagueDepth(minor);
            RebalanceLeagueDepth(major);

            var retiredPlayerIds = new List<int>();
            List<RosterVacancy> majorVacancies = RetirePlayers(
                world,
                major,
                LeagueId.MajorMain,
                nextYear,
                retiredPlayerIds);
            List<RosterVacancy> minorVacancies = FillVacancies(majorVacancies, major, minor);
            minorVacancies.AddRange(RetirePlayers(
                world,
                minor,
                LeagueId.MinorMain,
                nextYear,
                retiredPlayerIds));
            List<RosterVacancy> rookieVacancies = FillVacancies(minorVacancies, minor, rookie);
            rookieVacancies.AddRange(RetirePlayers(
                world,
                rookie,
                LeagueId.RookieMain,
                nextYear,
                retiredPlayerIds));

            int nextPlayerId = GetNextPlayerId(world);
            var newPlayers = new List<PlayerState>(rookieVacancies.Count);
            RecruitRookies(
                world,
                rookie,
                rookieVacancies,
                nextYear,
                ref nextPlayerId,
                newPlayers);

            LeagueRosterPlan[] rosters =
            {
                new LeagueRosterPlan(LeagueId.RookieMain, BuildTeams(rookie)),
                new LeagueRosterPlan(LeagueId.MinorMain, BuildTeams(minor)),
                new LeagueRosterPlan(LeagueId.MajorMain, BuildTeams(major))
            };
            AiMarketDecision[] decisions = BuildDecisions(
                world,
                rosters,
                retiredPlayerIds,
                newPlayers,
                nextYear);
            return new WorldOffseasonMarketPlan(rosters, decisions, newPlayers.ToArray());
        }

        /// <summary>v7 마이그레이션 전 상태와 단일 리그 테스트 픽스처는 기존 로스터 전환 경로를 보존한다.</summary>
        private static WorldOffseasonMarketPlan CreateSingleLeagueCompatibilityPlan(
            WorldState world,
            LeagueSeasonRolloverService rollover)
        {
            LeagueState league = world.Leagues[0];
            TeamState[] teams = rollover.AdvanceRosters(
                league,
                world,
                league.CurrentSeason.SeasonId + 1);
            return new WorldOffseasonMarketPlan(
                new[] { new LeagueRosterPlan(league.LeagueId, teams) },
                Array.Empty<AiMarketDecision>(),
                Array.Empty<PlayerState>());
        }

        private List<RosterVacancy> RetirePlayers(
            WorldState world,
            TeamRosterBuilder[] league,
            LeagueId leagueId,
            int nextYear,
            List<int> retiredPlayerIds)
        {
            var vacancies = new List<RosterVacancy>();
            for (int teamIndex = 0; teamIndex < league.Length; teamIndex++)
            {
                RosterCompetitorState[] snapshot = league[teamIndex].CopyCompetitors();
                for (int playerIndex = 0; playerIndex < snapshot.Length; playerIndex++)
                {
                    RosterCompetitorState competitor = snapshot[playerIndex];
                    PlayerState player = world.GetPlayer(competitor.PlayerId);
                    ulong eventSeed = DeterministicSeed.Derive(
                        world.WorldSeed,
                        RetirementStream ^
                        ((ulong)(uint)nextYear << 32) ^
                        (uint)player.PlayerId);
                    bool shouldRetire = new PlayerRetirementResolver(
                            _balance.PlayerLifecycle,
                            new Pcg32Random(eventSeed))
                        .ShouldRetire(player.Age + 1, competitor.Overall);
                    if (!shouldRetire)
                        continue;

                    league[teamIndex].RemovePlayer(player.PlayerId);
                    retiredPlayerIds.Add(player.PlayerId);
                    vacancies.Add(new RosterVacancy(league[teamIndex].TeamId, competitor.Position));
                }
            }
            return vacancies;
        }

        private static List<RosterVacancy> FillVacancies(
            List<RosterVacancy> targetVacancies,
            TeamRosterBuilder[] targetLeague,
            TeamRosterBuilder[] sourceLeague)
        {
            var sourceVacancies = new List<RosterVacancy>(targetVacancies.Count);
            for (int vacancyIndex = 0; vacancyIndex < targetVacancies.Count; vacancyIndex++)
            {
                RosterVacancy vacancy = targetVacancies[vacancyIndex];
                int sourceTeamIndex = -1;
                RosterCompetitorState selected = default;
                bool found = false;
                for (int teamIndex = 0; teamIndex < sourceLeague.Length; teamIndex++)
                {
                    RosterCompetitorState candidate;
                    if (!sourceLeague[teamIndex].TryGetStrongest(vacancy.Position, out candidate))
                        continue;
                    if (!found || candidate.Overall > selected.Overall ||
                        candidate.Overall == selected.Overall && candidate.PlayerId < selected.PlayerId)
                    {
                        sourceTeamIndex = teamIndex;
                        selected = candidate;
                        found = true;
                    }
                }
                if (!found)
                    throw new InvalidOperationException($"{vacancy.Position} 승격 후보가 없어 상위 리그 로스터를 채울 수 없습니다.");

                sourceLeague[sourceTeamIndex].RemovePlayer(selected.PlayerId);
                GetBuilder(targetLeague, vacancy.TeamId).AddPlayer(selected);
                sourceVacancies.Add(new RosterVacancy(sourceLeague[sourceTeamIndex].TeamId, selected.Position));
            }
            return sourceVacancies;
        }

        private void RecruitRookies(
            WorldState world,
            TeamRosterBuilder[] rookieLeague,
            List<RosterVacancy> vacancies,
            int nextYear,
            ref int nextPlayerId,
            List<PlayerState> newPlayers)
        {
            PlayerLifecycleBalance lifecycle = _balance.PlayerLifecycle;
            for (int index = 0; index < vacancies.Count; index++)
            {
                RosterVacancy vacancy = vacancies[index];
                int playerId = nextPlayerId++;
                ulong seed = DeterministicSeed.Derive(
                    world.WorldSeed,
                    RookieEntryStream ^
                    ((ulong)(uint)nextYear << 32) ^
                    (uint)playerId);
                var random = new Pcg32Random(seed);
                int overallRange = lifecycle.RookieEntryMaximumOverall - lifecycle.RookieEntryMinimumOverall + 1;
                int overall = lifecycle.RookieEntryMinimumOverall +
                    Math.Min((int)(random.NextDouble() * overallRange), overallRange - 1);
                string name = SelectRookieName(world, random);
                var competitor = new RosterCompetitorState(playerId, name, vacancy.Position, overall);
                PlayerState player = CareerWorldFactory.CreateRosterPlayerState(
                    LeagueId.RookieMain,
                    LeagueLevel.Rookie,
                    vacancy.TeamId,
                    competitor,
                    seed,
                    _balance.Growth,
                    worldGeneration: null,
                    minimumAgeOverride: lifecycle.RookieEntryMinimumAge,
                    maximumAgeOverride: lifecycle.RookieEntryMaximumAge);
                newPlayers.Add(player);
                GetBuilder(rookieLeague, vacancy.TeamId).AddPlayer(competitor);
            }
        }

        private AiMarketDecision[] BuildDecisions(
            WorldState world,
            LeagueRosterPlan[] rosters,
            List<int> retiredPlayerIds,
            List<PlayerState> newPlayers,
            int nextYear)
        {
            var decisions = new List<AiMarketDecision>(retiredPlayerIds.Count + newPlayers.Count + 64);
            retiredPlayerIds.Sort();
            for (int index = 0; index < retiredPlayerIds.Count; index++)
            {
                PlayerState player = world.GetPlayer(retiredPlayerIds[index]);
                decisions.Add(new AiMarketDecision(
                    player.PlayerId,
                    PlayerMovementType.Retirement,
                    player.CurrentLeagueId,
                    player.CurrentTeamId,
                    LeagueId.Unassigned,
                    0,
                    ExpectedRole.BenchCompetition,
                    0,
                    0L,
                    "현역 은퇴"));
            }

            for (int leagueIndex = 0; leagueIndex < rosters.Length; leagueIndex++)
            {
                LeagueRosterPlan roster = rosters[leagueIndex];
                LeagueLevel level = world.GetLeague(roster.LeagueId).LeagueLevel;
                for (int teamIndex = 0; teamIndex < roster.Teams.Count; teamIndex++)
                {
                    TeamState team = roster.Teams[teamIndex];
                    for (int playerIndex = 0; playerIndex < team.RosterCompetitors.Count; playerIndex++)
                    {
                        RosterCompetitorState competitor = team.RosterCompetitors[playerIndex];
                        PlayerState player = FindNewPlayer(newPlayers, competitor.PlayerId) ?? world.GetPlayer(competitor.PlayerId);
                        bool isNew = player.RegisteredSeasons == 0 && FindNewPlayer(newPlayers, player.PlayerId) != null;
                        bool moved = !isNew &&
                            (player.CurrentTeamId != team.TeamId || player.CurrentLeagueId != roster.LeagueId);
                        PlayerContractState activeContract = isNew ? null : FindActiveContract(world, player.ActiveContractId);
                        bool needsContract = isNew || moved || activeContract == null || activeContract.EndYear < nextYear;
                        if (!needsContract)
                            continue;

                        ExpectedRole role = GetExpectedRole(team, competitor);
                        PlayerMovementType movementType;
                        if (isNew)
                            movementType = PlayerMovementType.InitialSigning;
                        else if (!moved)
                            movementType = PlayerMovementType.CurrentTeamRenewal;
                        else if (player.CurrentLeagueId == roster.LeagueId)
                            movementType = PlayerMovementType.SameLeagueTransfer;
                        else
                            movementType = PlayerMovementType.Promotion;
                        long salary = CalculateSalary(level, competitor.Overall);
                        decisions.Add(new AiMarketDecision(
                            player.PlayerId,
                            movementType,
                            isNew ? LeagueId.Unassigned : player.CurrentLeagueId,
                            isNew ? 0 : player.CurrentTeamId,
                            roster.LeagueId,
                            team.TeamId,
                            role,
                            GetContractYears(level),
                            salary,
                            movementType == PlayerMovementType.Promotion
                                ? "상위 리그 로스터 수요에 따른 승격 계약"
                                : movementType == PlayerMovementType.SameLeagueTransfer
                                    ? "포지션 로스터 깊이 보충 계약"
                                : movementType == PlayerMovementType.InitialSigning
                                    ? "Rookie 신규 선수 계약"
                                    : "기존 구단 재계약"));
                    }
                }
            }
            return decisions.ToArray();
        }

        private long CalculateSalary(LeagueLevel level, int overall)
        {
            PlayerLifecycleBalance lifecycle = _balance.PlayerLifecycle;
            long baseSalary = level switch
            {
                LeagueLevel.Rookie => lifecycle.RookieBaseSalary,
                LeagueLevel.Minor => lifecycle.MinorBaseSalary,
                LeagueLevel.Major => lifecycle.MajorBaseSalary,
                _ => throw new ArgumentOutOfRangeException(nameof(level))
            };
            return checked(baseSalary * (75L + overall) / 125L);
        }

        private int GetContractYears(LeagueLevel level)
        {
            PlayerLifecycleBalance lifecycle = _balance.PlayerLifecycle;
            return level switch
            {
                LeagueLevel.Rookie => lifecycle.RookieContractYears,
                LeagueLevel.Minor => lifecycle.MinorContractYears,
                LeagueLevel.Major => lifecycle.MajorContractYears,
                _ => throw new ArgumentOutOfRangeException(nameof(level))
            };
        }

        private static ExpectedRole GetExpectedRole(TeamState team, RosterCompetitorState player)
        {
            for (int index = 0; index < team.RosterCompetitors.Count; index++)
            {
                RosterCompetitorState candidate = team.RosterCompetitors[index];
                if (candidate.Position != player.Position || candidate.PlayerId == player.PlayerId)
                    continue;
                if (candidate.Overall > player.Overall ||
                    candidate.Overall == player.Overall && candidate.PlayerId < player.PlayerId)
                {
                    return ExpectedRole.RosterCompetition;
                }
            }
            return ExpectedRole.StartingCompetition;
        }

        private static PlayerContractState FindActiveContract(WorldState world, int contractId)
        {
            if (contractId <= 0)
                return null;
            for (int index = 0; index < world.Contracts.Count; index++)
            {
                PlayerContractState contract = world.Contracts[index];
                if (contract.ContractId == contractId && contract.IsActive)
                    return contract;
            }
            return null;
        }

        private static PlayerState FindNewPlayer(List<PlayerState> players, int playerId)
        {
            for (int index = 0; index < players.Count; index++)
            {
                if (players[index].PlayerId == playerId)
                    return players[index];
            }
            return null;
        }

        private static string SelectRookieName(WorldState world, IRandomSource random)
        {
            LeagueState rookie = world.GetLeague(LeagueId.RookieMain);
            int count = 0;
            for (int teamIndex = 0; teamIndex < rookie.Teams.Count; teamIndex++)
                count += rookie.Teams[teamIndex].RosterCompetitors.Count;
            int selectedIndex = Math.Min((int)(random.NextDouble() * count), count - 1);
            for (int teamIndex = 0; teamIndex < rookie.Teams.Count; teamIndex++)
            {
                IReadOnlyList<RosterCompetitorState> competitors = rookie.Teams[teamIndex].RosterCompetitors;
                if (selectedIndex < competitors.Count)
                    return competitors[selectedIndex].Name;
                selectedIndex -= competitors.Count;
            }
            throw new InvalidOperationException("Rookie 이름 풀을 찾을 수 없습니다.");
        }

        private static int GetNextPlayerId(WorldState world)
        {
            int maximum = 0;
            for (int index = 0; index < world.Players.Count; index++)
                maximum = Math.Max(maximum, world.Players[index].PlayerId);
            return checked(maximum + 1);
        }

        private static TeamRosterBuilder[] CreateBuilders(TeamState[] teams)
        {
            var result = new TeamRosterBuilder[teams.Length];
            for (int index = 0; index < teams.Length; index++)
                result[index] = new TeamRosterBuilder(teams[index]);
            Array.Sort(result, (left, right) => left.TeamId.CompareTo(right.TeamId));
            return result;
        }

        private void RebalanceLeagueDepth(TeamRosterBuilder[] league)
        {
            int requiredDepth = _balance.TeamGeneration.CompetitorsPerPosition;
            for (int rawPosition = (int)PlayerPosition.Catcher;
                 rawPosition <= (int)PlayerPosition.ReliefPitcher;
                 rawPosition++)
            {
                var position = (PlayerPosition)rawPosition;
                for (int targetIndex = 0; targetIndex < league.Length; targetIndex++)
                {
                    while (league[targetIndex].CountPosition(position) < requiredDepth)
                    {
                        int sourceIndex = -1;
                        RosterCompetitorState selected = default;
                        for (int candidateIndex = 0; candidateIndex < league.Length; candidateIndex++)
                        {
                            if (league[candidateIndex].CountPosition(position) <= requiredDepth)
                                continue;
                            RosterCompetitorState candidate;
                            if (!league[candidateIndex].TryGetWeakest(position, out candidate))
                                continue;
                            if (sourceIndex < 0 || candidate.Overall < selected.Overall ||
                                candidate.Overall == selected.Overall && candidate.PlayerId < selected.PlayerId)
                            {
                                sourceIndex = candidateIndex;
                                selected = candidate;
                            }
                        }
                        if (sourceIndex < 0)
                            throw new InvalidOperationException($"{position} 로스터 깊이 부족을 같은 리그에서 복구할 수 없습니다.");
                        league[sourceIndex].RemovePlayer(selected.PlayerId);
                        league[targetIndex].AddPlayer(selected);
                    }
                }
            }
        }

        private static TeamState[] BuildTeams(TeamRosterBuilder[] builders)
        {
            var result = new TeamState[builders.Length];
            for (int index = 0; index < builders.Length; index++)
                result[index] = builders[index].Build();
            return result;
        }

        private static TeamRosterBuilder GetBuilder(TeamRosterBuilder[] builders, int teamId)
        {
            for (int index = 0; index < builders.Length; index++)
            {
                if (builders[index].TeamId == teamId)
                    return builders[index];
            }
            throw new InvalidOperationException($"TeamId {teamId}의 로스터 계획이 없습니다.");
        }

        private readonly struct RosterVacancy
        {
            public RosterVacancy(int teamId, PlayerPosition position)
            {
                TeamId = teamId;
                Position = position;
            }

            public int TeamId { get; }
            public PlayerPosition Position { get; }
        }

        private sealed class TeamRosterBuilder
        {
            private readonly TeamState _source;
            private readonly List<RosterCompetitorState> _competitors;
            private readonly List<int> _playerIds;

            public TeamRosterBuilder(TeamState source)
            {
                _source = source;
                _competitors = new List<RosterCompetitorState>(source.RosterCompetitors.Count);
                _playerIds = new List<int>(source.RosterPlayerIds.Count);
                for (int index = 0; index < source.RosterCompetitors.Count; index++)
                    _competitors.Add(source.RosterCompetitors[index]);
                for (int index = 0; index < source.RosterPlayerIds.Count; index++)
                    _playerIds.Add(source.RosterPlayerIds[index]);
            }

            public int TeamId => _source.TeamId;

            public RosterCompetitorState[] CopyCompetitors() => _competitors.ToArray();

            public bool TryGetStrongest(PlayerPosition position, out RosterCompetitorState strongest)
            {
                strongest = default;
                bool found = false;
                for (int index = 0; index < _competitors.Count; index++)
                {
                    RosterCompetitorState candidate = _competitors[index];
                    if (candidate.Position != position)
                        continue;
                    if (!found || candidate.Overall > strongest.Overall ||
                        candidate.Overall == strongest.Overall && candidate.PlayerId < strongest.PlayerId)
                    {
                        strongest = candidate;
                        found = true;
                    }
                }
                return found;
            }

            public bool TryGetWeakest(PlayerPosition position, out RosterCompetitorState weakest)
            {
                weakest = default;
                bool found = false;
                for (int index = 0; index < _competitors.Count; index++)
                {
                    RosterCompetitorState candidate = _competitors[index];
                    if (candidate.Position != position)
                        continue;
                    if (!found || candidate.Overall < weakest.Overall ||
                        candidate.Overall == weakest.Overall && candidate.PlayerId < weakest.PlayerId)
                    {
                        weakest = candidate;
                        found = true;
                    }
                }
                return found;
            }

            public int CountPosition(PlayerPosition position)
            {
                int count = 0;
                for (int index = 0; index < _competitors.Count; index++)
                {
                    if (_competitors[index].Position == position)
                        count++;
                }
                return count;
            }

            public void RemovePlayer(int playerId)
            {
                for (int index = 0; index < _competitors.Count; index++)
                {
                    if (_competitors[index].PlayerId == playerId)
                    {
                        _competitors.RemoveAt(index);
                        break;
                    }
                }
                if (!_playerIds.Remove(playerId))
                    throw new InvalidOperationException($"TeamId {TeamId}에 PlayerId {playerId}가 없습니다.");
            }

            public void AddPlayer(RosterCompetitorState player)
            {
                _competitors.Add(player);
                _playerIds.Add(player.PlayerId);
            }

            public TeamState Build()
            {
                _competitors.Sort((left, right) =>
                {
                    int position = left.Position.CompareTo(right.Position);
                    return position != 0 ? position : left.PlayerId.CompareTo(right.PlayerId);
                });
                _playerIds.Sort();
                return _source.WithRosterAndPlayerIds(_competitors.ToArray(), _playerIds.ToArray());
            }
        }
    }
}
