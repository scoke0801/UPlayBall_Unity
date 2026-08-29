using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Career;
using Baseball.Simulation.Growth;
using Baseball.Simulation.Random;

namespace Baseball.Game.Career
{
    /// <summary>
    /// Rookie부터 Galaxy까지 영속 리그와 전역 선수 레지스트리를 결정론적으로 생성한다.
    /// </summary>
    public sealed class CareerWorldFactory
    {
        private const ulong RookieLeagueStream = 0x524F4F4B49454C47UL;
        private const ulong MinorLeagueStream = 0x4D494E4F524C4721UL;
        private const ulong MajorLeagueStream = 0x4D414A4F524C4721UL;
        private const ulong UpperLeagueStream = 0x55505045524C4721UL;
        private const ulong PlayerAgeStream = 0x504C415945524147UL;

        private readonly NewGameConfiguration _configuration;

        public CareerWorldFactory(NewGameConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public WorldState CreateNewWorld(
            ulong worldSeed,
            GeneratedTeam[] generatedRookieTeams,
            PlayerState myPlayer,
            PlayerContractState currentContract)
        {
            if (generatedRookieTeams == null)
                throw new ArgumentNullException(nameof(generatedRookieTeams));
            if (myPlayer == null)
                throw new ArgumentNullException(nameof(myPlayer));
            if (currentContract == null)
                throw new ArgumentNullException(nameof(currentContract));

            WorldGenerationConfiguration world = _configuration.WorldGeneration;
            TeamState[] rookieTeams = CreateTeamStates(
                generatedRookieTeams,
                LeagueId.RookieMain,
                teamIdBase: 0,
                playerIdBase: 0,
                overallBonus: 0,
                teamNamePrefix: string.Empty,
                rosterSize: world.RosterSize);
            ReplaceCompetitorWithMyPlayer(rookieTeams, myPlayer);

            var leagues = new LeagueState[world.LeagueDefinitions.Count];
            for (int definitionIndex = 0; definitionIndex < world.LeagueDefinitions.Count; definitionIndex++)
            {
                LeagueDefinition definition = world.LeagueDefinitions[definitionIndex];
                ulong stream = GetLeagueStream(definition.Tier);
                TeamState[] teams = definition.Tier == LeagueLevel.Rookie
                    ? rookieTeams
                    : GenerateBackgroundLeague(
                        worldSeed,
                        stream,
                        LeagueId.FromLevel(definition.Tier),
                        teamIdBase: definition.SortOrder * 100,
                        playerIdBase: (definition.SortOrder + 1) * 1_000_000,
                        world.GetCompetitionOverallBonus(definition.Tier),
                        definition.TeamNamePrefix);
                leagues[definitionIndex] = CreateLeague(
                    worldSeed,
                    stream,
                    LeagueId.FromLevel(definition.Tier),
                    definition.Tier,
                    definition.SortOrder + 1,
                    teams,
                    world.GetCompetitionOverallBonus(definition.Tier));
            }

            myPlayer.AssignLeague(LeagueId.RookieMain);
            if (currentContract.ContractId <= 0)
                currentContract.AttachIdentity(1, myPlayer.PlayerId, LeagueId.RookieMain);
            myPlayer.AttachContract(currentContract.ContractId, LeagueId.RookieMain);

            PlayerState[] players = CreatePlayerRegistry(leagues, myPlayer, worldSeed);
            PlayerContractState[] contracts = CreateInitialContracts(
                leagues,
                players,
                myPlayer.PlayerId,
                currentContract);
            var result = new WorldState(
                worldSeed,
                new GlobalCalendarState(new DateTime(_configuration.FirstSeasonYear, 1, 1)),
                leagues,
                players,
                contracts,
                _configuration.FirstSeasonYear);
            result.MovementLedger.Record(new PlayerMovementRecord(
                result.Calendar.CurrentDate,
                1,
                myPlayer.PlayerId,
                PlayerMovementType.InitialSigning,
                LeagueId.Unassigned,
                0,
                LeagueId.RookieMain,
                myPlayer.CurrentTeamId,
                currentContract.PromisedRole,
                currentContract.PromisedRole,
                currentContract.PromisedRole,
                currentContract.ContractId,
                "신규 프로 계약"));
            return result;
        }

        private PlayerContractState[] CreateInitialContracts(
            IReadOnlyList<LeagueState> leagues,
            IReadOnlyList<PlayerState> players,
            int myPlayerId,
            PlayerContractState myContract)
        {
            var result = new List<PlayerContractState>(players.Count) { myContract };
            int nextContractId = myContract.ContractId + 1;
            var evaluator = new PlayerValueEvaluator(_configuration.Balance.PlayerEvaluation);
            for (int index = 0; index < players.Count; index++)
            {
                PlayerState player = players[index];
                if (player.PlayerId == myPlayerId)
                    continue;
                LeagueState league = GetLeague(leagues, player.CurrentLeagueId);
                int overall = evaluator.CalculatePositionValue(player.ToPlayer());
                PlayerLifecycleBalance lifecycle = _configuration.Balance.PlayerLifecycle;
                LeagueDefinition definition = _configuration.WorldGeneration.GetDefinition(league.LeagueLevel);
                long baseSalary = checked((long)Math.Round(
                    lifecycle.RookieBaseSalary * definition.SalaryMultiplier));
                long annualSalary = checked(baseSalary * (75L + overall) / 125L);
                int contractYears = GetContractYears(lifecycle, league.LeagueLevel);
                var contract = new PlayerContractState(
                    NewGameFlow.CurrentSaveVersion,
                    nextContractId++,
                    player.PlayerId,
                    player.CurrentTeamId,
                    player.CurrentLeagueId,
                    _configuration.FirstSeasonYear,
                    contractYears,
                    0L,
                    annualSalary,
                    ExpectedRole.RosterCompetition);
                player.AttachContract(contract.ContractId, player.CurrentLeagueId);
                result.Add(contract);
            }
            return result.ToArray();
        }

        private static LeagueState GetLeague(IReadOnlyList<LeagueState> leagues, LeagueId leagueId)
        {
            for (int index = 0; index < leagues.Count; index++)
            {
                if (leagues[index].LeagueId == leagueId)
                    return leagues[index];
            }
            throw new InvalidOperationException($"{leagueId}를 찾을 수 없습니다.");
        }

        /// <summary>
        /// v7의 실제 리그를 보존하고 누락된 두 단계 리그만 별도 Migration Seed로 생성한다.
        /// </summary>
        public WorldState CreateMigratedWorld(
            ulong worldSeed,
            ulong migrationSeed,
            LeagueState legacyLeague,
            PlayerState myPlayer,
            IReadOnlyList<PlayerContractState> contracts)
        {
            if (legacyLeague == null) throw new ArgumentNullException(nameof(legacyLeague));
            if (myPlayer == null) throw new ArgumentNullException(nameof(myPlayer));
            if (contracts == null) throw new ArgumentNullException(nameof(contracts));

            var leagues = new List<LeagueState>(3);
            AddMigratedLeague(
                leagues,
                migrationSeed,
                legacyLeague,
                LeagueId.RookieMain,
                LeagueLevel.Rookie,
                RookieLeagueStream,
                teamIdBase: 10_000,
                playerIdBase: 10_000_000,
                overallBonus: 0,
                teamNamePrefix: string.Empty);
            AddMigratedLeague(
                leagues,
                migrationSeed,
                legacyLeague,
                LeagueId.MinorMain,
                LeagueLevel.Minor,
                MinorLeagueStream,
                teamIdBase: 20_000,
                playerIdBase: 20_000_000,
                _configuration.WorldGeneration.MinorOverallBonus,
                _configuration.WorldGeneration.MinorTeamNamePrefix);
            AddMigratedLeague(
                leagues,
                migrationSeed,
                legacyLeague,
                LeagueId.MajorMain,
                LeagueLevel.Major,
                MajorLeagueStream,
                teamIdBase: 30_000,
                playerIdBase: 30_000_000,
                _configuration.WorldGeneration.MajorOverallBonus,
                _configuration.WorldGeneration.MajorTeamNamePrefix);

            PlayerState[] players = CreatePlayerRegistry(leagues, myPlayer, migrationSeed);
            return new WorldState(
                worldSeed,
                new GlobalCalendarState(new DateTime(legacyLeague.LeagueYear, 1, 1)),
                leagues,
                players,
                contracts,
                legacyLeague.LeagueYear);
        }

        /// <summary>v12의 세 리그와 모든 역사 객체를 보존하고 World~Galaxy 인구를 추가한다.</summary>
        public WorldState ExpandV12World(WorldState source, ulong migrationSeed)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (source.Leagues.Count == LeagueLevelRules.Count)
                return source;
            if (source.Leagues.Count != 3)
                throw new InvalidOperationException("v12 월드는 Rookie·Minor·Major 세 리그여야 합니다.");

            var leagues = new List<LeagueState>(source.Leagues.Count + 7);
            var players = new List<PlayerState>(source.Players.Count + 7 * 8 * 25);
            var contracts = new List<PlayerContractState>(source.Contracts.Count + 7 * 8 * 25);
            leagues.AddRange(source.Leagues);
            players.AddRange(source.Players);
            contracts.AddRange(source.Contracts);

            int nextTeamBase = GetNextMigrationTeamIdBase(source.Teams);
            int nextPlayerBase = GetNextMigrationPlayerIdBase(source.Players);
            int nextContractId = 1;
            for (int index = 0; index < source.Contracts.Count; index++)
                nextContractId = Math.Max(nextContractId, source.Contracts[index].ContractId + 1);

            SeasonState referenceSeason = source.Leagues[0].CurrentSeason;
            bool startsRegularSeason = referenceSeason.Phase != SeasonPhase.Preseason;
            var rollover = new LeagueSeasonRolloverService(_configuration.Balance);
            var evaluator = new PlayerValueEvaluator(_configuration.Balance.PlayerEvaluation);
            PlayerLifecycleBalance lifecycle = _configuration.Balance.PlayerLifecycle;
            for (int definitionIndex = 3;
                 definitionIndex < _configuration.WorldGeneration.LeagueDefinitions.Count;
                 definitionIndex++)
            {
                LeagueDefinition definition = _configuration.WorldGeneration.LeagueDefinitions[definitionIndex];
                LeagueId leagueId = LeagueId.FromLevel(definition.Tier);
                int overallBonus = _configuration.WorldGeneration.GetCompetitionOverallBonus(definition.Tier);
                TeamState[] teams = GenerateBackgroundLeague(
                    migrationSeed,
                    GetLeagueStream(definition.Tier),
                    leagueId,
                    nextTeamBase + definitionIndex * 100,
                    nextPlayerBase + definitionIndex * 1_000_000,
                    overallBonus,
                    definition.TeamNamePrefix);
                ulong leagueSeed = DeterministicSeed.Derive(migrationSeed, GetLeagueStream(definition.Tier));
                int seasonId = referenceSeason.SeasonId + definitionIndex;
                var preseasonLeague = new LeagueState(
                    NewGameFlow.CurrentSaveVersion,
                    leagueId,
                    definition.Tier,
                    "Standard.80Games",
                    referenceSeason.Year,
                    leagueSeed,
                    teams,
                    new SeasonState(NewGameFlow.CurrentSaveVersion, seasonId, referenceSeason.Year, definition.Tier),
                    completedSeasonSummaries: null,
                    competitionOverallBonus: overallBonus);
                SeasonState season = startsRegularSeason
                    ? rollover.BuildNextRegularSeason(preseasonLeague, teams, seasonId, referenceSeason.Year)
                    : preseasonLeague.CurrentSeason;
                var league = new LeagueState(
                    NewGameFlow.CurrentSaveVersion,
                    leagueId,
                    definition.Tier,
                    preseasonLeague.LeagueRulesetId,
                    referenceSeason.Year,
                    leagueSeed,
                    teams,
                    season,
                    completedSeasonSummaries: null,
                    competitionOverallBonus: overallBonus);
                leagues.Add(league);

                for (int teamIndex = 0; teamIndex < teams.Length; teamIndex++)
                {
                    TeamState team = teams[teamIndex];
                    for (int playerIndex = 0; playerIndex < team.RosterCompetitors.Count; playerIndex++)
                    {
                        RosterCompetitorState competitor = team.RosterCompetitors[playerIndex];
                        PlayerState player = CreateRosterPlayerState(
                            leagueId,
                            definition.Tier,
                            team.TeamId,
                            competitor,
                            migrationSeed,
                            _configuration.Balance.Growth,
                            _configuration.WorldGeneration);
                        int overall = evaluator.CalculatePositionValue(player.ToPlayer());
                        long baseSalary = checked((long)Math.Round(
                            lifecycle.RookieBaseSalary * definition.SalaryMultiplier));
                        var contract = new PlayerContractState(
                            NewGameFlow.CurrentSaveVersion,
                            nextContractId++,
                            player.PlayerId,
                            team.TeamId,
                            leagueId,
                            referenceSeason.Year,
                            GetContractYears(lifecycle, definition.Tier),
                            0L,
                            checked(baseSalary * (75L + overall) / 125L),
                            ExpectedRole.RosterCompetition);
                        player.AttachContract(contract.ContractId, leagueId);
                        players.Add(player);
                        contracts.Add(contract);
                    }
                }
            }

            return new WorldState(
                source.WorldSeed,
                source.Calendar,
                leagues,
                players,
                contracts,
                source.HistoryStartYear,
                source.MovementLedger,
                source.TeamMovementLedger,
                source.Records,
                source.DomainEvents);
        }

        private static int GetNextMigrationTeamIdBase(IReadOnlyList<TeamState> source)
        {
            int maximum = 0;
            for (int index = 0; index < source.Count; index++)
                maximum = Math.Max(maximum, source[index].TeamId);
            return checked(((maximum / 10_000) + 1) * 10_000);
        }

        private static int GetNextMigrationPlayerIdBase(IReadOnlyList<PlayerState> source)
        {
            int maximum = 0;
            for (int index = 0; index < source.Count; index++)
                maximum = Math.Max(maximum, source[index].PlayerId);
            return checked(((maximum / 10_000_000) + 1) * 10_000_000);
        }

        private void AddMigratedLeague(
            List<LeagueState> result,
            ulong migrationSeed,
            LeagueState legacyLeague,
            LeagueId leagueId,
            LeagueLevel leagueLevel,
            ulong stream,
            int teamIdBase,
            int playerIdBase,
            int overallBonus,
            string teamNamePrefix)
        {
            if (legacyLeague.LeagueLevel == leagueLevel)
            {
                result.Add(legacyLeague.CompetitionOverallBonus == overallBonus
                    ? legacyLeague
                    : new LeagueState(
                        legacyLeague.SaveVersion,
                        leagueId,
                        leagueLevel,
                        legacyLeague.LeagueRulesetId,
                        legacyLeague.LeagueYear,
                        legacyLeague.RandomSeed,
                        legacyLeague.Teams,
                        legacyLeague.CurrentSeason,
                        legacyLeague.CompletedSeasonSummaries,
                        overallBonus));
                return;
            }

            TeamState[] teams = GenerateBackgroundLeague(
                migrationSeed,
                stream,
                leagueId,
                teamIdBase,
                playerIdBase,
                overallBonus,
                teamNamePrefix);
            ulong leagueSeed = DeterministicSeed.Derive(migrationSeed, stream);
            result.Add(new LeagueState(
                NewGameFlow.CurrentSaveVersion,
                leagueId,
                leagueLevel,
                "Standard.80Games",
                legacyLeague.LeagueYear,
                leagueSeed,
                teams,
                new SeasonState(
                    NewGameFlow.CurrentSaveVersion,
                    1_000 + (int)leagueLevel,
                    legacyLeague.LeagueYear,
                    leagueLevel),
                completedSeasonSummaries: null,
                competitionOverallBonus: overallBonus));
        }

        private TeamState[] GenerateBackgroundLeague(
            ulong worldSeed,
            ulong stream,
            LeagueId leagueId,
            int teamIdBase,
            int playerIdBase,
            int overallBonus,
            string teamNamePrefix)
        {
            ulong generationSeed = DeterministicSeed.Derive(worldSeed, stream);
            var generator = new TeamGenerator(
                _configuration.Balance.TeamGeneration,
                new Pcg32Random(generationSeed));
            GeneratedTeam[] generated = generator.GenerateLeague(
                _configuration.TeamCount,
                _configuration.Archetypes,
                _configuration.TeamIdentities,
                _configuration.PlayerNamePool);
            return CreateTeamStates(
                generated,
                leagueId,
                teamIdBase,
                playerIdBase,
                overallBonus,
                teamNamePrefix,
                _configuration.WorldGeneration.RosterSize);
        }

        private LeagueState CreateLeague(
            ulong worldSeed,
            ulong stream,
            LeagueId leagueId,
            LeagueLevel leagueLevel,
            int seasonId,
            TeamState[] teams,
            int overallBonus)
        {
            ulong leagueSeed = DeterministicSeed.Derive(worldSeed, stream);
            return new LeagueState(
                NewGameFlow.CurrentSaveVersion,
                leagueId,
                leagueLevel,
                "Standard.80Games",
                _configuration.FirstSeasonYear,
                leagueSeed,
                teams,
                new SeasonState(
                    NewGameFlow.CurrentSaveVersion,
                    seasonId,
                    _configuration.FirstSeasonYear,
                    leagueLevel),
                completedSeasonSummaries: null,
                competitionOverallBonus: overallBonus);
        }

        private static TeamState[] CreateTeamStates(
            GeneratedTeam[] generatedTeams,
            LeagueId leagueId,
            int teamIdBase,
            int playerIdBase,
            int overallBonus,
            string teamNamePrefix,
            int rosterSize)
        {
            int targetOverall = WorldGenerationConfiguration.RookieTargetOverall + overallBonus;
            int calibratedOverallBonus = targetOverall - CalculateGeneratedRosterAverage(generatedTeams);
            var result = new TeamState[generatedTeams.Length];
            int nextPlayerOffset = 1;
            for (int teamIndex = 0; teamIndex < generatedTeams.Length; teamIndex++)
            {
                GeneratedTeam source = generatedTeams[teamIndex];
                int teamId = teamIdBase + source.TeamId;
                var positionNeeds = new int[(int)PlayerPosition.ReliefPitcher + 1];
                var competitors = new List<RosterCompetitorState>();
                for (int rawPosition = (int)PlayerPosition.Catcher;
                     rawPosition <= (int)PlayerPosition.ReliefPitcher;
                     rawPosition++)
                {
                    var position = (PlayerPosition)rawPosition;
                    positionNeeds[rawPosition] = source.GetPositionNeed(position);
                    IReadOnlyList<RosterCompetitor> sourcePlayers = source.GetPositionCompetitors(position);
                    for (int playerIndex = 0; playerIndex < sourcePlayers.Count; playerIndex++)
                    {
                        RosterCompetitor sourcePlayer = sourcePlayers[playerIndex];
                        int playerId = playerIdBase == 0 ? sourcePlayer.PlayerId : playerIdBase + nextPlayerOffset;
                        nextPlayerOffset++;
                        competitors.Add(new RosterCompetitorState(
                            playerId,
                            sourcePlayer.Name,
                            sourcePlayer.Position,
                            ClampRating(sourcePlayer.Overall + calibratedOverallBonus)));
                    }
                }

                AddDepthPlayers(
                    competitors,
                    source.TeamId,
                    playerIdBase,
                    ref nextPlayerOffset,
                    rosterSize);

                result[teamIndex] = new TeamState(
                    NewGameFlow.CurrentSaveVersion,
                    teamId,
                    leagueId,
                    teamNamePrefix + source.Name,
                    source.Archetype,
                    source.PrimaryColor,
                    positionNeeds,
                    competitors.ToArray());
            }
            return result;
        }

        private static int CalculateGeneratedRosterAverage(IReadOnlyList<GeneratedTeam> generatedTeams)
        {
            long total = 0L;
            int count = 0;
            for (int teamIndex = 0; teamIndex < generatedTeams.Count; teamIndex++)
            {
                GeneratedTeam team = generatedTeams[teamIndex];
                for (int rawPosition = (int)PlayerPosition.Catcher;
                     rawPosition <= (int)PlayerPosition.ReliefPitcher;
                     rawPosition++)
                {
                    IReadOnlyList<RosterCompetitor> competitors =
                        team.GetPositionCompetitors((PlayerPosition)rawPosition);
                    for (int playerIndex = 0; playerIndex < competitors.Count; playerIndex++)
                    {
                        total += competitors[playerIndex].Overall;
                        count++;
                    }
                }
            }

            if (count == 0)
                throw new InvalidOperationException("리그 전력 기준을 계산할 생성 선수가 없습니다.");
            return (int)Math.Round(total / (double)count);
        }

        private static void AddDepthPlayers(
            List<RosterCompetitorState> competitors,
            int sourceTeamId,
            int playerIdBase,
            ref int nextPlayerOffset,
            int rosterSize)
        {
            PlayerPosition[] depthPositions =
            {
                PlayerPosition.StartingPitcher,
                PlayerPosition.ReliefPitcher,
                PlayerPosition.ReliefPitcher
            };
            int depthIndex = 0;
            while (competitors.Count < rosterSize)
            {
                PlayerPosition position = depthPositions[depthIndex % depthPositions.Length];
                RosterCompetitorState template = default;
                bool found = false;
                for (int index = 0; index < competitors.Count; index++)
                {
                    if (competitors[index].Position != position)
                        continue;
                    if (!found || competitors[index].Overall < template.Overall)
                    {
                        template = competitors[index];
                        found = true;
                    }
                }
                if (!found)
                    throw new InvalidOperationException($"{position} 뎁스 선수의 기준 선수가 없습니다.");

                int playerId = playerIdBase == 0
                    ? sourceTeamId * 1000 + competitors.Count + 1
                    : playerIdBase + nextPlayerOffset;
                nextPlayerOffset++;
                competitors.Add(new RosterCompetitorState(
                    playerId,
                    template.Name,
                    position,
                    template.Overall));
                depthIndex++;
            }
        }

        private PlayerState[] CreatePlayerRegistry(
            IReadOnlyList<LeagueState> leagues,
            PlayerState myPlayer,
            ulong playerSeed)
        {
            var result = new List<PlayerState>(
                _configuration.TeamCount * _configuration.WorldGeneration.RosterSize *
                _configuration.WorldGeneration.LeagueDefinitions.Count);
            for (int leagueIndex = 0; leagueIndex < leagues.Count; leagueIndex++)
            {
                LeagueState league = leagues[leagueIndex];
                for (int teamIndex = 0; teamIndex < league.Teams.Count; teamIndex++)
                {
                    TeamState team = league.Teams[teamIndex];
                    for (int playerIndex = 0; playerIndex < team.RosterCompetitors.Count; playerIndex++)
                    {
                        RosterCompetitorState competitor = team.RosterCompetitors[playerIndex];
                        result.Add(CreateRosterPlayerState(
                            league.LeagueId,
                            league.LeagueLevel,
                            team.TeamId,
                            competitor,
                            playerSeed,
                            _configuration.Balance.Growth,
                            _configuration.WorldGeneration));
                    }
                }
            }
            result.Add(myPlayer);
            return result.ToArray();
        }

        internal static PlayerState CreateRosterPlayerState(
            LeagueId leagueId,
            LeagueLevel leagueLevel,
            int teamId,
            RosterCompetitorState competitor,
            ulong playerSeed,
            GrowthBalanceTable growthBalance = null,
            WorldGenerationConfiguration worldGeneration = null,
            int? minimumAgeOverride = null,
            int? maximumAgeOverride = null)
        {
            growthBalance ??= GrowthBalanceTable.CreateDefault();
            worldGeneration ??= WorldGenerationConfiguration.CreateDefault();
            bool isPitcher = competitor.Position is
                PlayerPosition.StartingPitcher or PlayerPosition.ReliefPitcher;
            int batterRating = isPitcher ? 20 : competitor.Overall;
            int pitcherRating = isPitcher ? competitor.Overall : 20;
            Handedness battingHand = competitor.PlayerId % 3 == 0
                ? Handedness.Switch
                : competitor.PlayerId % 2 == 0 ? Handedness.Left : Handedness.Right;
            Handedness throwingHand = competitor.PlayerId % 4 == 0
                ? Handedness.Left
                : Handedness.Right;
            int minimumAge = minimumAgeOverride ?? worldGeneration.GetMinimumAge(leagueLevel);
            int maximumAge = maximumAgeOverride ?? worldGeneration.GetMaximumAge(leagueLevel);
            if (minimumAge < 16 || maximumAge < minimumAge)
                throw new ArgumentOutOfRangeException(nameof(minimumAgeOverride));
            ulong ageSeed = DeterministicSeed.Derive(
                playerSeed,
                PlayerAgeStream ^ (uint)competitor.PlayerId);
            int age = minimumAge + Math.Min(
                (int)(new Pcg32Random(ageSeed).NextDouble() * (maximumAge - minimumAge + 1)),
                maximumAge - minimumAge);
            var player = new PlayerState(
                NewGameFlow.CurrentSaveVersion,
                competitor.PlayerId,
                competitor.Name,
                string.Empty,
                age,
                competitor.Position,
                battingHand,
                throwingHand,
                new BatterAttributes(
                    batterRating,
                    batterRating,
                    batterRating,
                    batterRating,
                    batterRating,
                    batterRating),
                new PitcherAttributes(
                    pitcherRating,
                    pitcherRating,
                    pitcherRating,
                    pitcherRating,
                    pitcherRating,
                    pitcherRating),
                teamId,
                leagueId);
            player.AttachGrowthState(new PlayerGrowthFactory(growthBalance).Create(
                player.ToPlayer(),
                age,
                initialCondition: 90));
            player.InitializeAiCareerHistory(
                competitor.CareerPlateAppearances,
                competitor.CareerPitchingOuts,
                competitor.RegisteredSeasons);
            player.InitializeSeasonStatus(condition: 90, managerEvaluation: 50);
            return player;
        }

        private static void ReplaceCompetitorWithMyPlayer(TeamState[] teams, PlayerState myPlayer)
        {
            for (int index = 0; index < teams.Length; index++)
            {
                if (teams[index].TeamId != myPlayer.CurrentTeamId)
                    continue;
                TeamState team = teams[index];
                // 내 선수는 별도 런타임 상태로 라인업에 합류하므로, 포지션 최소 뎁스를 훼손하지
                // 않도록 생성 시 추가한 ReliefPitcher 한 명을 25인 엔트리 자리에서 교체한다.
                RosterCompetitorState replacement = team.GetStrongestCompetitor(PlayerPosition.ReliefPitcher);
                for (int rosterIndex = 0; rosterIndex < team.RosterCompetitors.Count; rosterIndex++)
                {
                    RosterCompetitorState candidate = team.RosterCompetitors[rosterIndex];
                    if (candidate.Position == PlayerPosition.ReliefPitcher && candidate.Overall < replacement.Overall)
                        replacement = candidate;
                }
                teams[index] = team.WithoutRosteredPlayer(replacement.PlayerId)
                    .WithRosteredPlayer(myPlayer.PlayerId);
                return;
            }
            throw new InvalidOperationException($"TeamId {myPlayer.CurrentTeamId}를 Rookie 리그에서 찾을 수 없습니다.");
        }

        private static ulong GetLeagueStream(LeagueLevel leagueLevel)
        {
            return leagueLevel switch
            {
                LeagueLevel.Rookie => RookieLeagueStream,
                LeagueLevel.Minor => MinorLeagueStream,
                LeagueLevel.Major => MajorLeagueStream,
                _ => DeterministicSeed.Derive(UpperLeagueStream, (ulong)(uint)leagueLevel)
            };
        }

        private static int GetContractYears(PlayerLifecycleBalance lifecycle, LeagueLevel leagueLevel)
        {
            if (leagueLevel == LeagueLevel.Rookie)
                return lifecycle.RookieContractYears;
            if (leagueLevel == LeagueLevel.Minor)
                return lifecycle.MinorContractYears;
            return lifecycle.MajorContractYears;
        }

        private static int ClampRating(int value)
        {
            if (value < 0) return 0;
            return value > 100 ? 100 : value;
        }
    }
}
