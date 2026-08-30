using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Career;
using Baseball.Simulation.Random;

namespace Baseball.Game.Career
{
    /// <summary>모든 리그의 승강 이후 은퇴·수요 충원·Rookie 신인 유입을 하나의 계획으로 계산한다.</summary>
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
        public WorldOffseasonMarketPlan CreatePlan(
            WorldState world,
            int myPlayerId,
            int nextYear,
            LeagueMovementPlan leagueMovementPlan = null)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (myPlayerId <= 0) throw new ArgumentOutOfRangeException(nameof(myPlayerId));
            if (nextYear <= 0) throw new ArgumentOutOfRangeException(nameof(nextYear));

            var rollover = new LeagueSeasonRolloverService(_balance);
            if (world.Leagues.Count == 1)
                return CreateSingleLeagueCompatibilityPlan(world, rollover);
            if (world.Leagues.Count != LeagueLevelRules.Count)
                throw new InvalidOperationException($"기본 월드 시장에는 {LeagueLevelRules.Count}개 리그가 모두 필요합니다.");

            leagueMovementPlan ??= new LeagueMovementPlanner().CreatePlan(world);
            TeamRosterBuilder[][] leagueBuilders = CreateLeagueBuilders(
                world,
                rollover,
                leagueMovementPlan);
            PlayerState myPlayer = world.GetPlayer(myPlayerId);
            for (int index = 0; index < leagueBuilders.Length; index++)
            {
                RebalanceLeagueDepth(
                    leagueBuilders[index],
                    myPlayerId,
                    myPlayer.PrimaryPosition);
            }

            var retiredPlayerIds = new List<int>();
            int topIndex = leagueBuilders.Length - 1;
            List<RosterVacancy> rookieVacancies = RetirePlayers(
                world,
                leagueBuilders[topIndex],
                world.Leagues[topIndex].LeagueId,
                nextYear,
                retiredPlayerIds);
            for (int targetIndex = topIndex; targetIndex > 0; targetIndex--)
            {
                rookieVacancies = FillVacancies(
                    rookieVacancies,
                    leagueBuilders[targetIndex],
                    leagueBuilders[targetIndex - 1]);
                rookieVacancies.AddRange(RetirePlayers(
                    world,
                    leagueBuilders[targetIndex - 1],
                    world.Leagues[targetIndex - 1].LeagueId,
                    nextYear,
                    retiredPlayerIds));
            }

            int nextPlayerId = GetNextPlayerId(world);
            var newPlayers = new List<PlayerState>(rookieVacancies.Count);
            RecruitRookies(
                world,
                leagueBuilders[0],
                rookieVacancies,
                nextYear,
                ref nextPlayerId,
                newPlayers);

            var rosters = new LeagueRosterPlan[world.Leagues.Count];
            for (int index = 0; index < rosters.Length; index++)
                rosters[index] = new LeagueRosterPlan(world.Leagues[index].LeagueId, BuildTeams(leagueBuilders[index]));
            AiMarketDecision[] decisions = BuildDecisions(
                world,
                rosters,
                retiredPlayerIds,
                newPlayers,
                nextYear,
                myPlayerId);
            return new WorldOffseasonMarketPlan(
                rosters,
                decisions,
                newPlayers.ToArray(),
                leagueMovementPlan);
        }

        private static TeamRosterBuilder[][] CreateLeagueBuilders(
            WorldState world,
            LeagueSeasonRolloverService rollover,
            LeagueMovementPlan movementPlan)
        {
            var teamsByLeague = new List<TeamState>[world.Leagues.Count];
            for (int index = 0; index < teamsByLeague.Length; index++)
                teamsByLeague[index] = new List<TeamState>(world.Leagues[index].Teams.Count);

            for (int leagueIndex = 0; leagueIndex < world.Leagues.Count; leagueIndex++)
            {
                LeagueState league = world.Leagues[leagueIndex];
                TeamState[] advanced = rollover.AdvanceRosters(
                    league,
                    world,
                    league.CurrentSeason.SeasonId + 1);
                for (int teamIndex = 0; teamIndex < advanced.Length; teamIndex++)
                {
                    TeamState team = advanced[teamIndex];
                    LeagueId targetLeagueId = movementPlan.GetTargetLeagueId(team.TeamId, team.LeagueId);
                    LeagueState targetLeague = world.GetLeague(targetLeagueId);
                    teamsByLeague[(int)targetLeague.LeagueLevel].Add(team.WithLeague(targetLeagueId));
                }
            }

            var result = new TeamRosterBuilder[teamsByLeague.Length][];
            for (int index = 0; index < result.Length; index++)
            {
                if (teamsByLeague[index].Count != world.Leagues[index].Teams.Count)
                    throw new InvalidOperationException($"{world.Leagues[index].LeagueId}의 승강 후 구단 수가 달라졌습니다.");
                result[index] = CreateBuilders(teamsByLeague[index].ToArray());
            }
            return result;
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
                        .ShouldRetire(BuildRetirementInput(
                            world,
                            player,
                            competitor,
                            leagueId,
                            nextYear));
                    if (!shouldRetire)
                        continue;

                    league[teamIndex].RemovePlayer(player.PlayerId);
                    retiredPlayerIds.Add(player.PlayerId);
                    vacancies.Add(new RosterVacancy(league[teamIndex].TeamId, competitor.Position));
                }
            }
            return vacancies;
        }

        private RetirementEvaluationInput BuildRetirementInput(
            WorldState world,
            PlayerState player,
            RosterCompetitorState competitor,
            LeagueId leagueId,
            int nextYear)
        {
            LeagueState league = world.GetLeague(leagueId);
            SeasonState season = league.CurrentSeason;
            PlayerCompetitionStatisticsState statistics =
                season.LeagueStatistics.RegularSeason.GetPlayer(player.PlayerId);
            double appearanceRate = statistics == null
                ? 0d
                : statistics.GamesPlayed / (double)Math.Max(1, _balance.CareerSeason.RegularSeasonGamesPerTeam);
            PlayerContractState contract = FindActiveContract(world, player.ActiveContractId);
            bool hasContractRemaining = contract != null && contract.EndYear >= nextYear;
            bool isFranchiseTeam = contract != null && contract.SigningTeamId == player.CurrentTeamId;
            bool isChampionshipContender = GetFinalRank(season, player.CurrentTeamId) <= 2;
            LeagueDefinition definition = WorldGenerationConfiguration.GetDefaultDefinition(league.LeagueLevel);
            bool hasVeteranDemand = competitor.Overall >=
                                    definition.TargetRosterOverall - definition.OverallSpread;
            return new RetirementEvaluationInput(
                player.Age + 1,
                competitor.Overall,
                player.RetirementPersonality,
                CalculateRecentAbilityDecline(player, nextYear - 1),
                appearanceRate,
                HasRecentLongTermInjury(player, nextYear - 1),
                hasContractRemaining,
                IsNearCareerMilestone(player),
                isChampionshipContender,
                isFranchiseTeam,
                hasVeteranDemand);
        }

        private static int GetFinalRank(SeasonState season, int teamId)
        {
            for (int index = 0; index < season.FinalStandingTeamIds.Count; index++)
            {
                if (season.FinalStandingTeamIds[index] == teamId)
                    return index + 1;
            }
            return int.MaxValue;
        }

        private static int CalculateRecentAbilityDecline(PlayerState player, int seasonYear)
        {
            if (player.GrowthState == null)
                return 0;
            int decline = 0;
            for (int recordIndex = 0; recordIndex < player.GrowthState.GrowthHistory.Count; recordIndex++)
            {
                GrowthResultRecord record = player.GrowthState.GrowthHistory[recordIndex];
                if (record.SeasonYear != seasonYear)
                    continue;
                for (int changeIndex = 0; changeIndex < record.AbilityChanges.Length; changeIndex++)
                {
                    if (record.AbilityChanges[changeIndex].Amount < 0)
                        decline -= record.AbilityChanges[changeIndex].Amount;
                }
            }
            return decline;
        }

        private static bool HasRecentLongTermInjury(PlayerState player, int seasonYear)
        {
            if (player.GrowthState == null)
                return false;
            for (int index = player.GrowthState.InjuryHistory.Count - 1; index >= 0; index--)
            {
                InjuryRecord injury = player.GrowthState.InjuryHistory[index];
                if (injury.SeasonYear < seasonYear)
                    break;
                if (injury.SeasonYear == seasonYear && injury.Severity >= InjurySeverity.Serious)
                    return true;
            }
            return false;
        }

        private static bool IsNearCareerMilestone(PlayerState player)
        {
            int careerWorkload = player.PrimaryPosition is
                PlayerPosition.StartingPitcher or PlayerPosition.ReliefPitcher
                    ? player.CareerPitchingOuts
                    : player.CareerPlateAppearances;
            int interval = player.PrimaryPosition is
                PlayerPosition.StartingPitcher or PlayerPosition.ReliefPitcher
                    ? 1500
                    : 1000;
            int remainder = careerWorkload % interval;
            return careerWorkload >= interval && remainder >= interval * 85 / 100;
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
                    teamGenerationBalance: _balance.TeamGeneration,
                    playerEvaluationBalance: _balance.PlayerEvaluation,
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
            int nextYear,
            int myPlayerId)
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
                        // 플레이어 계약은 CareerSeasonTransitionService가 선택 결과까지 소유한다.
                        if (competitor.PlayerId == myPlayerId)
                            continue;
                        PlayerState player = FindNewPlayer(newPlayers, competitor.PlayerId) ?? world.GetPlayer(competitor.PlayerId);
                        bool isNew = player.RegisteredSeasons == 0 && FindNewPlayer(newPlayers, player.PlayerId) != null;
                        bool moved = !isNew &&
                            (player.CurrentTeamId != team.TeamId || player.CurrentLeagueId != roster.LeagueId);
                        PlayerContractState activeContract = isNew ? null : FindActiveContract(world, player.ActiveContractId);
                        bool followedTeam = moved && player.CurrentTeamId == team.TeamId;
                        bool preservesContract = followedTeam && activeContract != null && activeContract.EndYear >= nextYear;
                        bool needsContract = isNew || moved && !preservesContract ||
                                             activeContract == null || activeContract.EndYear < nextYear;
                        if (!needsContract && !preservesContract)
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
                            movementType = level > world.GetLeague(player.CurrentLeagueId).LeagueLevel
                                ? followedTeam
                                    ? PlayerMovementType.TeamPromotion
                                    : PlayerMovementType.Promotion
                                : followedTeam
                                    ? PlayerMovementType.TeamRelegation
                                    : PlayerMovementType.Rehabilitation;
                        long salary = CalculateSalary(level, competitor.Overall);
                        decisions.Add(new AiMarketDecision(
                            player.PlayerId,
                            movementType,
                            isNew ? LeagueId.Unassigned : player.CurrentLeagueId,
                            isNew ? 0 : player.CurrentTeamId,
                            roster.LeagueId,
                            team.TeamId,
                            role,
                            preservesContract ? 0 : GetContractYears(level),
                            preservesContract ? 0L : salary,
                            movementType is PlayerMovementType.Promotion or PlayerMovementType.TeamPromotion
                                ? "상위 리그 로스터 수요에 따른 승격 계약"
                                : movementType == PlayerMovementType.TeamRelegation
                                    ? "구단 강등에 따른 계약 승계"
                                : movementType == PlayerMovementType.SameLeagueTransfer
                                    ? "포지션 로스터 깊이 보충 계약"
                                : movementType == PlayerMovementType.InitialSigning
                                    ? "Rookie 신규 선수 계약"
                                    : "기존 구단 재계약",
                            preservesContract));
                    }
                }
            }
            return decisions.ToArray();
        }

        private long CalculateSalary(LeagueLevel level, int overall)
        {
            PlayerLifecycleBalance lifecycle = _balance.PlayerLifecycle;
            LeagueDefinition definition = WorldGenerationConfiguration.GetDefaultDefinition(level);
            long baseSalary = checked((long)Math.Round(
                lifecycle.RookieBaseSalary * definition.SalaryMultiplier));
            return checked(baseSalary * (75L + overall) / 125L);
        }

        private int GetContractYears(LeagueLevel level)
        {
            PlayerLifecycleBalance lifecycle = _balance.PlayerLifecycle;
            if (level == LeagueLevel.Rookie)
                return lifecycle.RookieContractYears;
            if (level == LeagueLevel.Minor)
                return lifecycle.MinorContractYears;
            return lifecycle.MajorContractYears;
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

        private void RebalanceLeagueDepth(
            TeamRosterBuilder[] league,
            int myPlayerId,
            PlayerPosition myPlayerPosition)
        {
            int requiredDepth = _balance.TeamGeneration.CompetitorsPerPosition;
            for (int rawPosition = (int)PlayerPosition.Catcher;
                 rawPosition <= (int)PlayerPosition.ReliefPitcher;
                 rawPosition++)
            {
                var position = (PlayerPosition)rawPosition;
                for (int targetIndex = 0; targetIndex < league.Length; targetIndex++)
                {
                    while (league[targetIndex].CountPosition(
                               position,
                               myPlayerId,
                               myPlayerPosition) < requiredDepth)
                    {
                        int sourceIndex = -1;
                        RosterCompetitorState selected = default;
                        for (int candidateIndex = 0; candidateIndex < league.Length; candidateIndex++)
                        {
                            if (league[candidateIndex].CountPosition(
                                    position,
                                    myPlayerId,
                                    myPlayerPosition) <= requiredDepth)
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

            public int CountPosition(
                PlayerPosition position,
                int myPlayerId,
                PlayerPosition myPlayerPosition)
            {
                int count = 0;
                for (int index = 0; index < _competitors.Count; index++)
                {
                    if (_competitors[index].Position == position)
                        count++;
                }
                if (position == myPlayerPosition && _playerIds.Contains(myPlayerId))
                    count++;
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
