using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Historical;
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
        private const ulong PlayerAttributeStream = 0x504C415941545452UL;

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
            AssignTeamEmblems(leagues, worldSeed, _configuration.TeamEmblemCount);

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

        /// <summary>
        /// 공통 Baked Person/Season/Card/Core25만 소비해 커리어 월드를 만들며 Runtime 생성기는 호출하지 않는다.
        /// </summary>
        public WorldState CreateNewWorld(
            ulong worldSeed,
            CareerBakedContent bakedContent,
            PlayerState myPlayer,
            PlayerContractState currentContract)
        {
            if (bakedContent == null)
                throw new ArgumentNullException(nameof(bakedContent));
            if (myPlayer == null)
                throw new ArgumentNullException(nameof(myPlayer));
            if (currentContract == null)
                throw new ArgumentNullException(nameof(currentContract));

            WorldGenerationConfiguration world = _configuration.WorldGeneration;
            var leagues = new LeagueState[world.LeagueDefinitions.Count];
            for (int definitionIndex = 0; definitionIndex < world.LeagueDefinitions.Count; definitionIndex++)
            {
                LeagueDefinition definition = world.LeagueDefinitions[definitionIndex];
                LeagueGrade grade = (LeagueGrade)(int)definition.Tier;
                LeagueId leagueId = LeagueId.FromLevel(definition.Tier);
                TeamState[] teams = CreateBakedTeamStates(bakedContent, grade, leagueId);
                if (definition.Tier == LeagueLevel.Rookie)
                    ReplaceCompetitorWithMyPlayer(teams, myPlayer);
                leagues[definitionIndex] = CreateLeague(
                    worldSeed,
                    GetLeagueStream(definition.Tier),
                    leagueId,
                    definition.Tier,
                    definition.SortOrder + 1,
                    teams,
                    world.GetCompetitionOverallBonus(definition.Tier));
            }

            myPlayer.AssignLeague(LeagueId.RookieMain);
            if (currentContract.ContractId <= 0)
                currentContract.AttachIdentity(1, myPlayer.PlayerId, LeagueId.RookieMain);
            myPlayer.AttachContract(currentContract.ContractId, LeagueId.RookieMain);

            PlayerState[] players = CreateBakedPlayerRegistry(bakedContent, leagues, myPlayer);
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

        private TeamState[] CreateBakedTeamStates(
            CareerBakedContent content,
            LeagueGrade grade,
            LeagueId leagueId)
        {
            IReadOnlyList<CareerBakedTeamRuntimeDefinition> definitions = content.GetTeams(grade);
            GeneratedTeam[] generated = CareerBakedContentAdapter.CreateGeneratedTeams(
                content,
                grade,
                _configuration.Balance.PlayerEvaluation);
            var result = new TeamState[definitions.Count];
            for (int teamIndex = 0; teamIndex < definitions.Count; teamIndex++)
            {
                CareerBakedTeamRuntimeDefinition definition = definitions[teamIndex];
                GeneratedTeam source = generated[teamIndex];
                var positionNeeds = new int[(int)PlayerPosition.ReliefPitcher + 1];
                var competitors = new List<RosterCompetitorState>(
                    ActiveRosterCompositionRule.ActiveRosterSize);
                for (int rawPosition = (int)PlayerPosition.Catcher;
                     rawPosition <= (int)PlayerPosition.ReliefPitcher;
                     rawPosition++)
                {
                    var position = (PlayerPosition)rawPosition;
                    positionNeeds[rawPosition] = source.GetPositionNeed(position);
                    IReadOnlyList<RosterCompetitor> sourcePlayers = source.GetPositionCompetitors(position);
                    for (int playerIndex = 0; playerIndex < sourcePlayers.Count; playerIndex++)
                    {
                        RosterCompetitor player = sourcePlayers[playerIndex];
                        competitors.Add(new RosterCompetitorState(
                            player.PlayerId,
                            player.Name,
                            player.Position,
                            player.Overall));
                    }
                }

                if (competitors.Count != ActiveRosterCompositionRule.ActiveRosterSize)
                    throw new InvalidOperationException("Baked ActiveRoster 변환 후 인원이 25명이 아닙니다.");
                result[teamIndex] = new TeamState(
                    NewGameFlow.CurrentSaveVersion,
                    definition.TeamId,
                    leagueId,
                    source.Name,
                    source.Archetype,
                    source.PrimaryColor,
                    positionNeeds,
                    competitors.ToArray(),
                    source.EmblemId);
            }
            return result;
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
                    competitors.ToArray(),
                    source.EmblemId);
            }
            return result;
        }

        /// <summary>
        /// 리그·구단 배열의 고정 순서에 독립 난수 덱을 매핑해 월드 전체 엠블럼 중복을 막는다.
        /// </summary>
        internal static void AssignTeamEmblems(
            IReadOnlyList<LeagueState> leagues,
            ulong worldSeed,
            int emblemCount)
        {
            if (leagues == null)
                throw new ArgumentNullException(nameof(leagues));

            int teamCount = 0;
            for (int leagueIndex = 0; leagueIndex < leagues.Count; leagueIndex++)
                teamCount += leagues[leagueIndex].Teams.Count;
            if (teamCount > emblemCount)
            {
                throw new InvalidOperationException(
                    $"구단 {teamCount}개에 중복 없이 배정하려면 엠블럼이 {teamCount}개 이상 필요합니다.");
            }

            int[] emblemIds = TeamEmblemSelector.CreateShuffledIds(emblemCount, worldSeed);
            int emblemIndex = 0;
            for (int leagueIndex = 0; leagueIndex < leagues.Count; leagueIndex++)
            {
                LeagueState league = leagues[leagueIndex];
                for (int teamIndex = 0; teamIndex < league.Teams.Count; teamIndex++)
                {
                    TeamState team = league.Teams[teamIndex];
                    int emblemId = emblemIds[emblemIndex++];
                    if (team.EmblemId > 0 && team.EmblemId != emblemId)
                    {
                        throw new InvalidOperationException(
                            $"TeamId {team.TeamId}의 사전 배정 엠블럼이 월드 덱과 다릅니다.");
                    }
                    league.ReplaceTeam(team.WithEmblemId(emblemId));
                }
            }
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
                            _configuration.WorldGeneration,
                            _configuration.Balance.TeamGeneration,
                            _configuration.Balance.PlayerEvaluation));
                    }
                }
            }
            result.Add(myPlayer);
            return result.ToArray();
        }

        private PlayerState[] CreateBakedPlayerRegistry(
            CareerBakedContent content,
            IReadOnlyList<LeagueState> leagues,
            PlayerState myPlayer)
        {
            var result = new List<PlayerState>(
                content.Teams.Count * ActiveRosterCompositionRule.ActiveRosterSize + 1);
            for (int leagueIndex = 0; leagueIndex < leagues.Count; leagueIndex++)
            {
                LeagueState league = leagues[leagueIndex];
                LeagueGrade grade = (LeagueGrade)(int)league.LeagueLevel;
                IReadOnlyList<CareerBakedTeamRuntimeDefinition> definitions = content.GetTeams(grade);
                for (int teamIndex = 0; teamIndex < definitions.Count; teamIndex++)
                {
                    CareerBakedTeamRuntimeDefinition definition = definitions[teamIndex];
                    TeamState team = FindLeagueTeam(league, definition.TeamId);
                    for (int playerIndex = 0; playerIndex < definition.ActiveRoster.Entries.Count; playerIndex++)
                    {
                        ActiveRosterEntry entry = definition.ActiveRoster.Entries[playerIndex];
                        int playerId = CareerBakedContentAdapter.CreateStablePlayerInstanceId(
                            definition.TeamSeason.TeamSeasonKey,
                            entry.PlayerSeasonId);
                        if (!ContainsPlayerId(team.RosterPlayerIds, playerId))
                            continue;
                        result.Add(CareerBakedContentAdapter.CreatePlayerState(
                            content,
                            definition,
                            entry,
                            league.LeagueId,
                            _configuration.FirstSeasonYear));
                    }
                }
            }
            result.Add(myPlayer);
            return result.ToArray();
        }

        private static bool ContainsPlayerId(IReadOnlyList<int> playerIds, int playerId)
        {
            for (int index = 0; index < playerIds.Count; index++)
                if (playerIds[index] == playerId) return true;
            return false;
        }

        private static TeamState FindLeagueTeam(LeagueState league, int teamId)
        {
            for (int index = 0; index < league.Teams.Count; index++)
                if (league.Teams[index].TeamId == teamId) return league.Teams[index];
            throw new InvalidOperationException($"League에서 TeamId {teamId}를 찾을 수 없습니다.");
        }

        internal static PlayerState CreateRosterPlayerState(
            LeagueId leagueId,
            LeagueLevel leagueLevel,
            int teamId,
            RosterCompetitorState competitor,
            ulong playerSeed,
            GrowthBalanceTable growthBalance = null,
            WorldGenerationConfiguration worldGeneration = null,
            TeamGenerationBalance? teamGenerationBalance = null,
            PlayerEvaluationBalance? playerEvaluationBalance = null,
            int? minimumAgeOverride = null,
            int? maximumAgeOverride = null)
        {
            growthBalance ??= GrowthBalanceTable.CreateDefault();
            worldGeneration ??= WorldGenerationConfiguration.CreateDefault();
            TeamGenerationBalance generationBalance =
                teamGenerationBalance ?? TeamGenerationBalance.CreateDefault();
            PlayerEvaluationBalance evaluationBalance =
                playerEvaluationBalance ?? PlayerEvaluationBalance.CreateDefault();
            bool isPitcher = competitor.Position is
                PlayerPosition.StartingPitcher or PlayerPosition.ReliefPitcher;
            ulong attributeSeed = DeterministicSeed.Derive(
                playerSeed,
                PlayerAttributeStream ^ (uint)competitor.PlayerId);
            var attributeGenerator = new RosterPlayerAttributeGenerator(
                generationBalance,
                evaluationBalance,
                new Pcg32Random(attributeSeed));
            BatterAttributes batterAttributes = isPitcher
                ? new BatterAttributes(20, 20, 20, 20, 20, 20)
                : attributeGenerator.GenerateBatter(competitor.Position, competitor.Overall);
            PitcherAttributes pitcherAttributes = isPitcher
                ? attributeGenerator.GeneratePitcher(competitor.Position, competitor.Overall)
                : new PitcherAttributes(20, 20, 20, 20, 20, 20);
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
                batterAttributes,
                pitcherAttributes,
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
