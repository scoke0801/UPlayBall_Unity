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
    /// 서로 독립적인 3개 리그와 전역 선수 레지스트리를 결정론적으로 생성한다.
    /// </summary>
    public sealed class CareerWorldFactory
    {
        private const ulong RookieLeagueStream = 0x524F4F4B49454C47UL;
        private const ulong MinorLeagueStream = 0x4D494E4F524C4721UL;
        private const ulong MajorLeagueStream = 0x4D414A4F524C4721UL;
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
                teamNamePrefix: string.Empty);
            AddPlayerToTeam(rookieTeams, myPlayer.CurrentTeamId, myPlayer.PlayerId);

            TeamState[] minorTeams = GenerateBackgroundLeague(
                worldSeed,
                MinorLeagueStream,
                LeagueId.MinorMain,
                teamIdBase: 100,
                playerIdBase: 2_000_000,
                world.MinorOverallBonus,
                world.MinorTeamNamePrefix);
            TeamState[] majorTeams = GenerateBackgroundLeague(
                worldSeed,
                MajorLeagueStream,
                LeagueId.MajorMain,
                teamIdBase: 200,
                playerIdBase: 3_000_000,
                world.MajorOverallBonus,
                world.MajorTeamNamePrefix);

            LeagueState[] leagues =
            {
                CreateLeague(worldSeed, RookieLeagueStream, LeagueId.RookieMain, LeagueLevel.Rookie, 1, rookieTeams, 0),
                CreateLeague(worldSeed, MinorLeagueStream, LeagueId.MinorMain, LeagueLevel.Minor, 2, minorTeams, world.MinorOverallBonus),
                CreateLeague(worldSeed, MajorLeagueStream, LeagueId.MajorMain, LeagueLevel.Major, 3, majorTeams, world.MajorOverallBonus)
            };

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
                long baseSalary = league.LeagueLevel switch
                {
                    LeagueLevel.Rookie => lifecycle.RookieBaseSalary,
                    LeagueLevel.Minor => lifecycle.MinorBaseSalary,
                    LeagueLevel.Major => lifecycle.MajorBaseSalary,
                    _ => throw new ArgumentOutOfRangeException(nameof(league.LeagueLevel))
                };
                long annualSalary = checked(baseSalary * (75L + overall) / 125L);
                int contractYears = league.LeagueLevel switch
                {
                    LeagueLevel.Rookie => lifecycle.RookieContractYears,
                    LeagueLevel.Minor => lifecycle.MinorContractYears,
                    LeagueLevel.Major => lifecycle.MajorContractYears,
                    _ => throw new ArgumentOutOfRangeException(nameof(league.LeagueLevel))
                };
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
                teamNamePrefix);
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
            string teamNamePrefix)
        {
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
                            ClampRating(sourcePlayer.Overall + overallBonus)));
                    }
                }

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

        private PlayerState[] CreatePlayerRegistry(
            IReadOnlyList<LeagueState> leagues,
            PlayerState myPlayer,
            ulong playerSeed)
        {
            var result = new List<PlayerState>(1 + _configuration.TeamCount * 66);
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

        private static void AddPlayerToTeam(TeamState[] teams, int teamId, int playerId)
        {
            for (int index = 0; index < teams.Length; index++)
            {
                if (teams[index].TeamId != teamId)
                    continue;
                teams[index] = teams[index].WithRosteredPlayer(playerId);
                return;
            }
            throw new InvalidOperationException($"TeamId {teamId}를 Rookie 리그에서 찾을 수 없습니다.");
        }

        private static int ClampRating(int value)
        {
            if (value < 0) return 0;
            return value > 100 ? 100 : value;
        }
    }
}
