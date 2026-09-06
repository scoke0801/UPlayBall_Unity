using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Rules;
using Baseball.Core.Teams;
using Baseball.Game.Career;
using Baseball.Simulation.Historical;
using Baseball.Simulation.Match;
using Baseball.Simulation.Random;

namespace Baseball.Game.Historical
{
    /// <summary>구단주 경기 한 건의 Simulation·상태 반영·홈 재무 결과를 함께 반환한다.</summary>
    public sealed class ManagerModeMatchResult
    {
        public ManagerModeMatchResult(
            MatchResult match,
            PreGamePlanSnapshot playerPlan,
            LineupChemistryResult playerLineupChemistry,
            HomeGameFinanceResult homeFinance,
            ManagerModeTransactionStatus homeFinanceStatus,
            string managerId,
            string headCoachId,
            ManagerTacticalProfile effectiveManagerProfile,
            string managerDisplayName,
            string headCoachDisplayName)
        {
            Match = match ?? throw new ArgumentNullException(nameof(match));
            PlayerPlan = playerPlan ?? throw new ArgumentNullException(nameof(playerPlan));
            PlayerLineupChemistry = playerLineupChemistry ??
                throw new ArgumentNullException(nameof(playerLineupChemistry));
            HomeFinance = homeFinance ?? throw new ArgumentNullException(nameof(homeFinance));
            HomeFinanceStatus = homeFinanceStatus;
            ManagerId = managerId ?? string.Empty;
            HeadCoachId = headCoachId ?? string.Empty;
            EffectiveManagerProfile = effectiveManagerProfile;
            ManagerDisplayName = managerDisplayName ?? string.Empty;
            HeadCoachDisplayName = headCoachDisplayName ?? string.Empty;
        }

        public MatchResult Match { get; }
        public PreGamePlanSnapshot PlayerPlan { get; }
        public LineupChemistryResult PlayerLineupChemistry { get; }
        public HomeGameFinanceResult HomeFinance { get; }
        public ManagerModeTransactionStatus HomeFinanceStatus { get; }
        public string ManagerId { get; }
        public string HeadCoachId { get; }
        public ManagerTacticalProfile EffectiveManagerProfile { get; }
        public string ManagerDisplayName { get; }
        public string HeadCoachDisplayName { get; }
    }

    /// <summary>남은 정규시즌을 기존 경기 경로로 완주한 경기 수와 최종 구단 성적을 반환한다.</summary>
    public sealed class ManagerRegularSeasonCompletionResult
    {
        public ManagerRegularSeasonCompletionResult(
            int playerGamesSimulated,
            int leagueGamesSimulated,
            int seasonWins,
            int seasonLosses,
            int seasonDraws,
            bool isCompleted)
        {
            if (playerGamesSimulated < 0 || leagueGamesSimulated < 0 ||
                seasonWins < 0 || seasonLosses < 0 || seasonDraws < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(playerGamesSimulated));
            }

            PlayerGamesSimulated = playerGamesSimulated;
            LeagueGamesSimulated = leagueGamesSimulated;
            SeasonWins = seasonWins;
            SeasonLosses = seasonLosses;
            SeasonDraws = seasonDraws;
            IsCompleted = isCompleted;
        }

        public int PlayerGamesSimulated { get; }
        public int LeagueGamesSimulated { get; }
        public int SeasonWins { get; }
        public int SeasonLosses { get; }
        public int SeasonDraws { get; }
        public bool IsCompleted { get; }
    }

    /// <summary>검증된 프리셋을 DetailedMatchEngine 한 경로로 실행하고 경기 후 원본 상태를 갱신한다.</summary>
    public sealed class ManagerModeMatchService
    {
        private const ulong AttendanceRandomStream = 0x415454454E44414EUL;

        private readonly HistoricalBakedContent _content;
        private readonly BalanceTable _balance;
        private readonly TeamColorDefinition[] _teamColors;
        private readonly TacticCardDefinition[] _tacticCards;
        private readonly Dictionary<string, TeamColorDefinition> _teamColorsById;
        private readonly Dictionary<string, TacticCardDefinition> _tacticCardsById;
        private readonly LineupPresetValidator _presetValidator;
        private readonly LineupChemistryResolver _lineupChemistryResolver;
        private readonly BatteryChemistryResolver _batteryChemistryResolver;
        private readonly ChemistryFamiliarityRecorder _familiarityRecorder;
        private readonly AiTacticSelectionResolver _aiTacticSelectionResolver;
        private readonly ManagerModeCoordinator _coordinator;
        private readonly OwnerCardAbilityResolver _ownerCardAbilityResolver;
        private readonly DugoutStaffCatalog _dugoutCatalog;
        private readonly DugoutTacticalProfileResolver _dugoutResolver;

        public ManagerModeMatchService(
            HistoricalBakedContent content,
            BalanceTable balance,
            IReadOnlyList<TeamColorDefinition> teamColors = null,
            IReadOnlyList<TacticCardDefinition> tacticCards = null)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            _teamColors = CopyDefinitions(teamColors);
            _tacticCards = CopyDefinitions(tacticCards);
            _teamColorsById = Index(_teamColors, item => item.TeamColorId, "TeamColorId");
            _tacticCardsById = Index(_tacticCards, item => item.CardId, "TacticCardId");
            _presetValidator = new LineupPresetValidator();
            _lineupChemistryResolver = new LineupChemistryResolver(balance.ConditionChemistry);
            _batteryChemistryResolver = new BatteryChemistryResolver(balance.ConditionChemistry);
            _familiarityRecorder = new ChemistryFamiliarityRecorder(balance.ConditionChemistry);
            _aiTacticSelectionResolver = new AiTacticSelectionResolver();
            _coordinator = new ManagerModeCoordinator(balance);
            _ownerCardAbilityResolver = new OwnerCardAbilityResolver(balance.Growth);
            _dugoutCatalog = DugoutStaffCatalog.CreateDefault();
            _dugoutResolver = new DugoutTacticalProfileResolver();
        }

        public ManagerModeMatchService(
            IHistoricalContentProvider contentProvider,
            BalanceTable balance,
            IReadOnlyList<TeamColorDefinition> teamColors = null,
            IReadOnlyList<TacticCardDefinition> tacticCards = null)
            : this(
                (contentProvider ?? throw new ArgumentNullException(nameof(contentProvider))).Load(),
                balance,
                teamColors,
                tacticCards)
        {
        }

        /// <summary>다음 경기를 현재 로스터와 availability로 다시 검증한 뒤 정확히 한 번 실행한다.</summary>
        public ManagerModeMatchResult PlayNextGame(
            ManagerHistoricalRuntimeState runtime,
            IMatchEventSink eventSink = null,
            MatchExecutionProfile? executionProfile = null)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (!runtime.HasManagerMode)
                throw new InvalidOperationException("ManagerMode v4 상태가 없는 Save는 경기 전에 migration해야 합니다.");

            ManagerModeRuntimeState mode = runtime.ManagerMode;
            ScheduledGameState game = mode.LiveSeason.NextPlayerGame ??
                throw new InvalidOperationException("플레이어 구단의 남은 경기가 없습니다.");
            string awayTeamKey = mode.LiveSeason.GetTeamSeasonKey(game.AwayTeamId);
            string homeTeamKey = mode.LiveSeason.GetTeamSeasonKey(game.HomeTeamId);
            string playerTeamKey = runtime.PlayerTeamSeasonKey;
            bool playerIsHome = string.Equals(playerTeamKey, homeTeamKey, StringComparison.Ordinal);

            LineupPresetState playerPreset = mode.GetSelectedLineupPresetForGame(game);
            // 다른 프리셋에서 이미 쓴 카드도 있으므로 UI의 준비 Snapshot을 신뢰하지 않는다.
            // AI 경기와 재무를 포함한 어떤 상태도 바꾸기 전에 전체 보유 수량을 검증한다.
            if (!runtime.TacticCollection.CanConsume(playerPreset.DefaultTacticCardIds))
                throw new InvalidOperationException("장착한 전술 카드의 보유 수량이 부족합니다.");
            LineupPresetValidationResult validation = _presetValidator.Validate(
                playerPreset,
                CreateValidationContext(runtime, playerTeamKey));
            if (!validation.CanStartGame)
                throw new InvalidOperationException("현재 로스터와 availability 검증을 통과하지 못한 LineupPreset입니다.");
            var playerPlan = new PreGamePlanSnapshot(game.GameId, playerTeamKey, playerPreset, validation);

            string opponentKey = playerIsHome ? awayTeamKey : homeTeamKey;
            PlayerIdMap playerIds = PlayerIdMap.Create(runtime.Rosters);

            // 상대 구단이 실제 일정만큼 소모된 컨디션·투수 피로로 나오도록,
            // 플레이어 경기가 열리는 라운드 이전의 AI 대진을 먼저 확정한다.
            SimulateAiGamesThrough(runtime, playerIds, game.Round - 1);

            LineupPresetState opponentPlan = CreateRosterRolePlan(runtime.GetRoster(opponentKey));
            TeamMatchBuild playerBuild = BuildTeam(runtime, playerTeamKey, playerPlan, game.Round, playerIds);
            TeamMatchBuild opponentBuild = BuildTeam(runtime, opponentKey, opponentPlan, game.Round, playerIds);

            MatchRosterSnapshot away = playerIsHome ? opponentBuild.Roster : playerBuild.Roster;
            MatchRosterSnapshot home = playerIsHome ? playerBuild.Roster : opponentBuild.Roster;
            TacticLoadoutState playerTactics = CreateConfirmedLoadout(playerPlan.TacticCardIds);
            TacticLoadoutState opponentTactics = CreateAiLoadout(runtime, game, playerIsHome ? game.AwayTeamId : game.HomeTeamId);
            var configuration = new HistoricalMatchConfiguration(
                _balance.HistoricalAssignment.CreateRule(),
                awayTacticLoadout: playerIsHome ? opponentTactics : playerTactics,
                homeTacticLoadout: playerIsHome ? playerTactics : opponentTactics);
            var input = new MatchInput(
                mode.LiveSeason.OriginYear,
                game.GameId,
                game.RandomSeed,
                away,
                home,
                MatchRules.CreateDefault(requiresWinner: false),
                SimulationRulesVersion.DetailedV2,
                SimulationVersionStamp.CreateCurrent(
                    _balance.Version,
                    _content.Manifest.ContentHash,
                    (int)SimulationRulesVersion.DetailedV2),
                configuration);
            MatchResult match = new MatchSimulator(_balance, MatchRandomStreams.Create(game.RandomSeed))
                .Simulate(
                    input,
                    eventSink ?? NullMatchEventSink.Instance,
                    executionProfile ?? MatchExecutionProfile.DetailedBackground);

            ManagerModeTransactionStatus financeStatus = ApplyHomeFinance(
                runtime,
                game,
                homeTeamKey,
                awayTeamKey,
                match,
                playerIsHome,
                out HomeGameFinanceResult finance);
            if (financeStatus == ManagerModeTransactionStatus.InsufficientMoney)
                throw new InvalidOperationException("홈 경기 운영비를 지불할 수 없어 경기 결과를 확정할 수 없습니다.");

            // 경제 적용이 거부된 경기를 완료 처리하면 Load 후 재시도할 수 없으므로,
            // 영수증 경계를 먼저 통과한 뒤 일정과 선수 상태를 확정한다.
            game.Complete(match.AwayBoxScore.Runs, match.HomeBoxScore.Runs);
            ApplyPostGameState(mode, playerBuild, opponentBuild, match);
            RecordStatistics(mode, game, match);
            ConsumePlayerTactics(runtime.TacticCollection, playerPlan.TacticCardIds);
            mode.ClearSelectedTactics();
            mode.Dugout.RecordMatchCompleted();

            // 같은 라운드의 나머지 대진까지 확정해야 순위표에서 플레이어 구단만 경기 수가 앞서가지 않는다.
            SimulateAiGamesThrough(runtime, playerIds, game.Round);
            // 홀수 구단 일정에서는 플레이어 구단이 마지막 라운드에 bye일 수 있어,
            // 플레이어 일정이 끝난 뒤 남은 AI 대진을 시즌 마감 전에 함께 소진한다.
            if (mode.LiveSeason.NextPlayerGame == null)
                SimulateAiGamesThrough(runtime, playerIds, GetMaximumRound(mode.LiveSeason.Schedule.Games));

            return new ManagerModeMatchResult(
                match,
                playerPlan,
                playerBuild.LineupChemistry,
                finance,
                financeStatus,
                mode.Dugout.ManagerId,
                mode.Dugout.HeadCoachId,
                playerBuild.Roster.ManagerProfile,
                _dugoutCatalog.GetManager(mode.Dugout.ManagerId).DisplayName,
                _dugoutCatalog.GetHeadCoach(mode.Dugout.HeadCoachId).DisplayName);
        }

        /// <summary>남은 플레이어·AI 대진을 단일 Detailed 경기 공식과 저장 Seed로 모두 완료한다.</summary>
        public ManagerRegularSeasonCompletionResult CompleteRegularSeason(
            ManagerHistoricalRuntimeState runtime,
            Action<ManagerModeMatchResult> playerGameCompleted = null)
        {
            var session = new ManagerRegularSeasonSimulationSession(runtime, this);
            while (!session.IsCompleted)
            {
                ManagerRegularSeasonSimulationStepResult step = session.AdvanceNextStep();
                if (step.MatchResult != null) playerGameCompleted?.Invoke(step.MatchResult);
            }
            return session.CreateCompletionResult();
        }

        /// <summary>플레이어 일정 뒤 남은 AI 대진을 동일 엔진과 Seed로 소진한다.</summary>
        internal void CompleteRemainingAiGames(ManagerHistoricalRuntimeState runtime)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            SimulateAiGamesThrough(
                runtime,
                PlayerIdMap.Create(runtime.Rosters),
                GetMaximumRound(runtime.ManagerMode.LiveSeason.Schedule.Games));
        }

        /// <summary>지정 라운드까지 남은 AI 구단 대진을 라운드·GameId 순서로 정확히 한 번 진행한다.</summary>
        private void SimulateAiGamesThrough(
            ManagerHistoricalRuntimeState runtime,
            PlayerIdMap playerIds,
            int throughRound)
        {
            if (throughRound <= 0) return;
            ManagerLiveSeasonState season = runtime.ManagerMode.LiveSeason;
            IReadOnlyList<ScheduledGameState> games = season.Schedule.Games;
            for (int index = 0; index < games.Count; index++)
            {
                ScheduledGameState game = games[index];
                if (game.IsCompleted || game.Round > throughRound) continue;
                // 플레이어 구단 경기는 라인업·전술 확정을 거쳐야 하므로 자동 진행 대상이 아니다.
                if (game.IncludesTeam(season.PlayerTeamId)) continue;
                SimulateAiGame(runtime, playerIds, game);
            }
        }

        /// <summary>AI 구단끼리의 한 경기를 플레이어 경기와 같은 엔진·Seed 계약으로 진행하고 상태에 반영한다.</summary>
        private void SimulateAiGame(
            ManagerHistoricalRuntimeState runtime,
            PlayerIdMap playerIds,
            ScheduledGameState game)
        {
            ManagerLiveSeasonState season = runtime.ManagerMode.LiveSeason;
            string awayTeamKey = season.GetTeamSeasonKey(game.AwayTeamId);
            string homeTeamKey = season.GetTeamSeasonKey(game.HomeTeamId);
            TeamMatchBuild awayBuild = BuildTeam(
                runtime,
                awayTeamKey,
                CreateRosterRolePlan(runtime.GetRoster(awayTeamKey)),
                game.Round,
                playerIds);
            TeamMatchBuild homeBuild = BuildTeam(
                runtime,
                homeTeamKey,
                CreateRosterRolePlan(runtime.GetRoster(homeTeamKey)),
                game.Round,
                playerIds);
            var configuration = new HistoricalMatchConfiguration(
                _balance.HistoricalAssignment.CreateRule(),
                awayTacticLoadout: CreateAiLoadout(runtime, game, game.AwayTeamId),
                homeTacticLoadout: CreateAiLoadout(runtime, game, game.HomeTeamId));
            var input = new MatchInput(
                season.OriginYear,
                game.GameId,
                game.RandomSeed,
                awayBuild.Roster,
                homeBuild.Roster,
                MatchRules.CreateDefault(requiresWinner: false),
                SimulationRulesVersion.DetailedV2,
                SimulationVersionStamp.CreateCurrent(
                    _balance.Version,
                    _content.Manifest.ContentHash,
                    (int)SimulationRulesVersion.DetailedV2),
                configuration);
            MatchResult match = new MatchSimulator(_balance, MatchRandomStreams.Create(game.RandomSeed))
                .Simulate(input, NullMatchEventSink.Instance, MatchExecutionProfile.DetailedBackground);

            game.Complete(match.AwayBoxScore.Runs, match.HomeBoxScore.Runs);
            ApplyPostGameState(runtime.ManagerMode, awayBuild, homeBuild, match);
            RecordStatistics(runtime.ManagerMode, game, match);
        }

        /// <summary>플레이어 경기와 AI 경기를 같은 집계 경로에 넣어 리그 기록이 한쪽으로 치우치지 않게 한다.</summary>
        private static void RecordStatistics(
            ManagerModeRuntimeState mode,
            ScheduledGameState game,
            MatchResult match)
        {
            new LeagueStatisticsService(mode.LiveSeason.Statistics).RecordMatch(
                match,
                CompetitionScope.RegularSeason,
                game.Round,
                isChampionship: false,
                isSeriesClinching: false);
        }

        /// <summary>다음 홈 경기의 실제 관중 입력과 동일한 Seed·Context로 경기 전 예상 관중을 계산한다.</summary>
        public AttendanceResult? PreviewNextHomeAttendance(ManagerHistoricalRuntimeState runtime)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            ScheduledGameState game = runtime.ManagerMode.LiveSeason.NextPlayerGame;
            if (game == null || game.HomeTeamId != runtime.ManagerMode.LiveSeason.PlayerTeamId)
                return null;

            string homeTeamKey = runtime.ManagerMode.LiveSeason.GetTeamSeasonKey(game.HomeTeamId);
            string awayTeamKey = runtime.ManagerMode.LiveSeason.GetTeamSeasonKey(game.AwayTeamId);
            HomeGameContext context = CreateHomeGameContext(
                runtime,
                game,
                homeTeamKey,
                awayTeamKey,
                true,
                HomeGameOutcome.Draw);
            return new AttendanceResolver(_balance.ClubOperation).Resolve(
                context,
                runtime.ManagerMode.ClubOperation,
                new Pcg32Random(DeterministicSeed.Derive(game.RandomSeed, AttendanceRandomStream)));
        }

        private LineupPresetValidationContext CreateValidationContext(
            ManagerHistoricalRuntimeState runtime,
            string teamSeasonKey)
        {
            CurrentRosterState roster = runtime.GetRoster(teamSeasonKey);
            TeamSeasonPlayerStatusState status = runtime.ManagerMode.GetPlayerStatus(teamSeasonKey);
            var players = new LineupPresetPlayerContext[roster.Entries.Count];
            for (int index = 0; index < roster.Entries.Count; index++)
            {
                ActiveRosterEntry entry = roster.Entries[index];
                PlayerCardDefinition card = GetCard(runtime, entry.CardId);
                PlayerSeasonDefinition season = runtime.WorldCardCatalog.GetPlayerSeason(card);
                bool isPitcher = season.PlayerType == PlayerType.Pitcher;
                bool available = status.GetRequiredPlayer(entry.PlayerPersonId).Availability !=
                                 PlayerAvailabilityStatus.Unavailable;
                players[index] = new LineupPresetPlayerContext(
                    entry.CardId,
                    season.Position,
                    isPitcher ? season.PitcherRole : (PitcherRole?)null,
                    isPitcher ? season.PitcherRoleConfidence : (PitcherRoleConfidence?)null,
                    available);
            }
            IReadOnlyList<TeamColorCandidate> activeColors = new TeamColorResolver().Resolve(
                roster,
                runtime.WorldCardCatalog,
                _teamColors);
            var activeColorIds = new string[activeColors.Count];
            for (int index = 0; index < activeColors.Count; index++)
                activeColorIds[index] = activeColors[index].Definition.TeamColorId;
            return new LineupPresetValidationContext(
                roster,
                players,
                _balance.HistoricalAssignment.CreateRule(),
                activeColorIds,
                GetOwnedTacticIds(runtime.TacticCollection));
        }

        private string[] GetOwnedTacticIds(TacticCollectionState collection)
        {
            var ids = new List<string>();
            for (int index = 0; index < _tacticCards.Length; index++)
                if (collection.Contains(_tacticCards[index].CardId)) ids.Add(_tacticCards[index].CardId);
            return ids.ToArray();
        }

        private TeamMatchBuild BuildTeam(
            ManagerHistoricalRuntimeState runtime,
            string teamSeasonKey,
            object planSource,
            int rotationIndex,
            PlayerIdMap playerIds)
        {
            CurrentRosterState activeRoster = runtime.GetRoster(teamSeasonKey);
            PreGamePlanSnapshot playerPlan = planSource as PreGamePlanSnapshot;
            LineupPresetState plan = playerPlan == null ? (LineupPresetState)planSource : null;
            IReadOnlyList<LineupPresetSlot> lineupSlots = playerPlan?.StartingLineupSlots ?? plan.StartingLineupSlots;
            IReadOnlyList<string> battingOrder = playerPlan?.BattingOrderCardIds ?? plan.BattingOrderCardIds;
            IReadOnlyList<string> benchPriority = playerPlan?.BenchPriorityCardIds ?? plan.BenchPriorityCardIds;
            IReadOnlyList<string> rotation = playerPlan?.StarterRotationCardIds ?? plan.StarterRotationCardIds;
            IReadOnlyList<string> bullpenCards = playerPlan?.BullpenAssignmentCardIds ?? plan.BullpenAssignmentCardIds;
            string setupCard = playerPlan?.SetupPitcherCardId ?? plan.SetupPitcherCardId;
            string closerCard = playerPlan?.CloserPitcherCardId ?? plan.CloserPitcherCardId;
            IReadOnlyList<string> equippedColors = playerPlan?.TeamColorIds ?? plan.TeamColorIds;

            PerCardBonusMap teamColorBonuses;
            if (runtime.HasOwnedEconomy(teamSeasonKey))
                teamColorBonuses = ResolveTeamColorBonuses(activeRoster, runtime.WorldCardCatalog, equippedColors);
            else
            {
                TeamColorDefinition[] selected = ResolveAiTeamColors(activeRoster, runtime.WorldCardCatalog, _balance.TeamColor);
                var definitions = new List<TeamColorDefinition>();
                foreach (TeamColorDefinition color in selected)
                    if (color != null) definitions.Add(color);
                teamColorBonuses = new TeamColorResolver().ApplyEquipped(
                    activeRoster, runtime.WorldCardCatalog, definitions, selected[0], selected[1]);
            }
            var playersByCard = new Dictionary<string, Player>(activeRoster.Entries.Count, StringComparer.Ordinal);
            var personByPlayerId = new Dictionary<int, string>(activeRoster.Entries.Count);
            for (int index = 0; index < activeRoster.Entries.Count; index++)
            {
                ActiveRosterEntry entry = activeRoster.Entries[index];
                Player player = CreatePlayer(
                    runtime,
                    teamSeasonKey,
                    entry,
                    teamColorBonuses,
                    playerIds.Get(teamSeasonKey, entry.PlayerSeasonId));
                playersByCard.Add(entry.CardId, player);
                personByPlayerId.Add(player.PlayerId, entry.PlayerPersonId);
            }

            var positionByCard = new Dictionary<string, PlayerPosition>(StringComparer.Ordinal);
            for (int index = 0; index < lineupSlots.Count; index++)
                positionByCard.Add(lineupSlots[index].CardId, lineupSlots[index].Position);
            var lineup = new LineupSlot[battingOrder.Count];
            var lineupPeople = new string[battingOrder.Count];
            var chemistryPlayers = new LineupChemistryPlayer[battingOrder.Count];
            for (int index = 0; index < battingOrder.Count; index++)
            {
                string cardId = battingOrder[index];
                Player player = playersByCard[cardId];
                lineup[index] = new LineupSlot(player, positionByCard[cardId]);
                lineupPeople[index] = personByPlayerId[player.PlayerId];
                chemistryPlayers[index] = new LineupChemistryPlayer(
                    lineupPeople[index],
                    player.BatterAttributes);
            }

            var bench = new Player[benchPriority.Count];
            for (int index = 0; index < bench.Length; index++) bench[index] = playersByCard[benchPriority[index]];
            int starterIndex = PositiveModulo(rotationIndex - 1, rotation.Count);
            PitcherRosterEntry starter = CreatePitcherEntry(
                runtime,
                teamSeasonKey,
                activeRoster,
                rotation[starterIndex],
                playersByCard,
                PitcherRole.Starter,
                null);
            var bullpen = new PitcherRosterEntry[bullpenCards.Count + 2];
            for (int index = 0; index < bullpenCards.Count; index++)
            {
                bullpen[index] = CreatePitcherEntry(
                    runtime,
                    teamSeasonKey,
                    activeRoster,
                    bullpenCards[index],
                    playersByCard,
                    PitcherRole.MiddleRelief,
                    (ActiveRosterRole)((int)ActiveRosterRole.Bullpen1 + index));
            }
            bullpen[bullpenCards.Count] = CreatePitcherEntry(
                runtime,
                teamSeasonKey,
                activeRoster,
                setupCard,
                playersByCard,
                PitcherRole.Setup,
                ActiveRosterRole.Setup);
            bullpen[bullpenCards.Count + 1] = CreatePitcherEntry(
                runtime,
                teamSeasonKey,
                activeRoster,
                closerCard,
                playersByCard,
                PitcherRole.Closer,
                ActiveRosterRole.Closer);

            TeamChemistryFamiliarityState familiarity = runtime.ManagerMode.GetFamiliarity(teamSeasonKey);
            LineupChemistryResult lineupChemistry = _lineupChemistryResolver.Resolve(
                teamSeasonKey,
                chemistryPlayers,
                familiarity);
            var matchPlayerIds = new HashSet<int>();
            for (int index = 0; index < lineup.Length; index++)
                matchPlayerIds.Add(lineup[index].Player.PlayerId);
            for (int index = 0; index < bench.Length; index++)
                matchPlayerIds.Add(bench[index].PlayerId);
            matchPlayerIds.Add(starter.Player.PlayerId);
            for (int index = 0; index < bullpen.Length; index++)
                matchPlayerIds.Add(bullpen[index].Player.PlayerId);
            MatchPlayerConditionEntry[] conditions = CreateConditionEntries(
                runtime.ManagerMode.GetPlayerStatus(teamSeasonKey),
                activeRoster,
                playersByCard,
                matchPlayerIds,
                lineupChemistry);
            MatchBatteryConditionEntry[] battery = CreateBatteryEntries(
                teamSeasonKey,
                activeRoster,
                playersByCard,
                personByPlayerId,
                starter,
                bullpen,
                familiarity);
            int teamId = runtime.ManagerMode.LiveSeason.Teams[
                FindTeamReferenceIndex(runtime.ManagerMode.LiveSeason.Teams, teamSeasonKey)].TeamId;
            DugoutManagementState dugout = string.Equals(
                    teamSeasonKey,
                    runtime.PlayerTeamSeasonKey,
                    StringComparison.Ordinal)
                ? runtime.ManagerMode.Dugout
                : _dugoutResolver.CreateAiState(teamId, _dugoutCatalog);
            ManagerTacticalProfile managerProfile = _dugoutResolver.Resolve(dugout, _dugoutCatalog);
            var roster = new MatchRosterSnapshot(
                teamId,
                GetTeamDisplayName(runtime, teamSeasonKey),
                new Lineup(lineup),
                starter,
                bullpen,
                bench,
                managerProfile,
                RunningApproach.Balanced,
                playerConditions: conditions,
                batteryConditions: battery);
            return new TeamMatchBuild(roster, lineupPeople, personByPlayerId, lineupChemistry);
        }

        private Player CreatePlayer(
            ManagerHistoricalRuntimeState runtime,
            string teamSeasonKey,
            ActiveRosterEntry entry,
            PerCardBonusMap teamColorBonuses,
            int playerId)
        {
            PlayerCardDefinition card = GetCard(runtime, entry.CardId);
            PlayerSeasonDefinition season = runtime.WorldCardCatalog.GetPlayerSeason(card);
            if (!_content.TryGetPlayerPerson(season.PlayerPersonId, out PlayerPersonDefinition person))
                throw new InvalidOperationException($"PlayerPerson {season.PlayerPersonId}를 찾을 수 없습니다.");
            AbilityRatings source = season.CreateBaseAttributes();
            runtime.TryGetOwnedCard(entry.CardId, out OwnedPlayerCardState owned);
            bool usesOwnedEconomy = runtime.HasOwnedEconomy(teamSeasonKey);
            int GetRawPermanent(PlayerAbility ability)
            {
                return _ownerCardAbilityResolver.ResolveRawPermanent(
                    season, card, usesOwnedEconomy ? owned : null, ability);
            }
            int GetPermanent(PlayerAbility ability) => Math.Max(1, Math.Min(100, GetRawPermanent(ability)));
            double GetRawEffective(PlayerAbility ability) => Math.Max(1d, Math.Min(_balance.MatchRatingCurve.Caps.HardCap,
                checked(GetRawPermanent(ability) + teamColorBonuses.Get(entry.CardId, ability))));
            int Get(PlayerAbility ability)
            {
                int raw = checked(GetRawPermanent(ability) + teamColorBonuses.Get(entry.CardId, ability));
                return MatchRatingCurve.ResolveMatchInput(raw, _balance.MatchRatingCurve);
            }
            var batter = new BatterAttributes(
                Get(PlayerAbility.Contact),
                Get(PlayerAbility.Power),
                Get(PlayerAbility.Speed),
                Get(PlayerAbility.Arm),
                Get(PlayerAbility.Defense),
                Get(PlayerAbility.BatterMental));
            var pitcher = new PitcherAttributes(
                Get(PlayerAbility.Stamina),
                Get(PlayerAbility.Velocity),
                Get(PlayerAbility.Stuff),
                Get(PlayerAbility.Breaking),
                Get(PlayerAbility.Control),
                Get(PlayerAbility.PitcherMental));
            return new Player(
                playerId,
                runtime.IdentityRegistry.GetPlayerDisplayName(person.PlayerPersonId),
                season.Position,
                person.Bats,
                person.Throws,
                batter,
                pitcher,
                nationality: season.RegistrationType == RegistrationType.Foreign ? "외국인" : string.Empty,
                pitchRepertoire: season.PitchRepertoire,
                isPositionEvidenceMissing: season.IsPositionEvidenceMissing,
                traitIds: usesOwnedEconomy
                    ? _ownerCardAbilityResolver.ResolveActiveTraitIds(owned)
                    : Array.Empty<string>(),
                bakedPitcherAttributes: source.ToPitcherAttributes(),
                permanentPitcherAttributes: new PitcherAttributes(
                    GetPermanent(PlayerAbility.Stamina), GetPermanent(PlayerAbility.Velocity),
                    GetPermanent(PlayerAbility.Stuff), GetPermanent(PlayerAbility.Breaking),
                    GetPermanent(PlayerAbility.Control), GetPermanent(PlayerAbility.PitcherMental)),
                hasResolvedMatchRatings: true,
                uncurvedPitcherAttributes: new PitcherRatingValues(
                    GetRawEffective(PlayerAbility.Stamina), GetRawEffective(PlayerAbility.Velocity),
                    GetRawEffective(PlayerAbility.Stuff), GetRawEffective(PlayerAbility.Breaking),
                    GetRawEffective(PlayerAbility.Control), GetRawEffective(PlayerAbility.PitcherMental)));
        }

        private PitcherRosterEntry CreatePitcherEntry(
            ManagerHistoricalRuntimeState runtime,
            string teamSeasonKey,
            CurrentRosterState roster,
            string cardId,
            IReadOnlyDictionary<string, Player> players,
            PitcherRole assignedRole,
            ActiveRosterRole? activeRosterRole)
        {
            ActiveRosterEntry entry = FindEntry(roster, cardId);
            PlayerCardDefinition card = GetCard(runtime, cardId);
            PlayerSeasonDefinition season = runtime.WorldCardCatalog.GetPlayerSeason(card);
            TeamSeasonPlayerStatus playerStatus = runtime.ManagerMode
                .GetPlayerStatus(teamSeasonKey)
                .GetRequiredPlayer(entry.PlayerPersonId);
            PitchingWorkloadState load = playerStatus.PitchingWorkload;
            return new PitcherRosterEntry(
                players[cardId],
                assignedRole,
                playerStatus.StoredBaseCondition,
                new RecentPitchingWorkload(
                    load.PreviousDayPitches,
                    load.TwoDaysAgoPitches,
                    load.ThreeDaysAgoPitches),
                naturalRole: season.PitcherRole,
                activeRosterRole: activeRosterRole,
                playerSeasonId: season.PlayerSeasonId,
                naturalRoleConfidence: season.PitcherRoleConfidence);
        }

        private MatchPlayerConditionEntry[] CreateConditionEntries(
            TeamSeasonPlayerStatusState status,
            CurrentRosterState roster,
            IReadOnlyDictionary<string, Player> players,
            ISet<int> matchPlayerIds,
            LineupChemistryResult lineupChemistry)
        {
            var result = new MatchPlayerConditionEntry[matchPlayerIds.Count];
            int resultIndex = 0;
            for (int index = 0; index < roster.Entries.Count; index++)
            {
                ActiveRosterEntry entry = roster.Entries[index];
                Player player = players[entry.CardId];
                if (!matchPlayerIds.Contains(player.PlayerId)) continue;
                int lineupModifier = lineupChemistry.GetConditionModifier(entry.PlayerPersonId);
                result[resultIndex++] = new MatchPlayerConditionEntry(
                    player.PlayerId,
                    new EffectiveMatchCondition(
                        status.GetRequiredPlayer(entry.PlayerPersonId).StoredBaseCondition,
                        assignmentModifier: 0,
                        lineupChemistryModifier: lineupModifier,
                        batteryChemistryModifier: 0,
                        temporaryModifier: 0));
            }
            return result;
        }

        private MatchBatteryConditionEntry[] CreateBatteryEntries(
            string teamSeasonKey,
            CurrentRosterState roster,
            IReadOnlyDictionary<string, Player> players,
            IReadOnlyDictionary<int, string> personByPlayerId,
            PitcherRosterEntry starter,
            IReadOnlyList<PitcherRosterEntry> bullpen,
            TeamChemistryFamiliarityState familiarity)
        {
            var pitchers = new Player[1 + bullpen.Count];
            pitchers[0] = starter.Player;
            for (int index = 0; index < bullpen.Count; index++) pitchers[index + 1] = bullpen[index].Player;
            var hitters = new List<ActiveRosterEntry>(ActiveRosterCompositionRule.HitterCount);
            for (int index = 0; index < roster.Entries.Count; index++)
            {
                ActiveRosterEntry entry = roster.Entries[index];
                if (entry.Role <= ActiveRosterRole.BenchHitter) hitters.Add(entry);
            }
            var result = new MatchBatteryConditionEntry[pitchers.Length * hitters.Count];
            int resultIndex = 0;
            for (int pitcherIndex = 0; pitcherIndex < pitchers.Length; pitcherIndex++)
            {
                Player pitcher = pitchers[pitcherIndex];
                string pitcherPersonId = personByPlayerId[pitcher.PlayerId];
                for (int hitterIndex = 0; hitterIndex < hitters.Count; hitterIndex++)
                {
                    ActiveRosterEntry catcherEntry = hitters[hitterIndex];
                    Player catcher = players[catcherEntry.CardId];
                    BatteryChemistryResult chemistry = _batteryChemistryResolver.Resolve(
                        teamSeasonKey,
                        pitcherPersonId,
                        pitcher.PitcherAttributes,
                        catcherEntry.PlayerPersonId,
                        catcher.BatterAttributes,
                        familiarity);
                    result[resultIndex++] = new MatchBatteryConditionEntry(
                        pitcher.PlayerId,
                        catcher.PlayerId,
                        chemistry.PitcherConditionModifier);
                }
            }
            return result;
        }

        private void ApplyPostGameState(
            ManagerModeRuntimeState mode,
            TeamMatchBuild first,
            TeamMatchBuild second,
            MatchResult match)
        {
            ApplyTeamPostGame(mode, first, match.PitcherUsage, match.BatteryUsage);
            ApplyTeamPostGame(mode, second, match.PitcherUsage, match.BatteryUsage);
        }

        private void ApplyTeamPostGame(
            ManagerModeRuntimeState mode,
            TeamMatchBuild team,
            IReadOnlyList<PitcherUsageReport> usage,
            IReadOnlyList<BatteryUsageReport> batteryUsage)
        {
            string teamKey = mode.LiveSeason.GetTeamSeasonKey(team.Roster.TeamId);
            TeamSeasonPlayerStatusState status = mode.GetPlayerStatus(teamKey);
            TeamChemistryFamiliarityState familiarity = mode.GetFamiliarity(teamKey);
            for (int index = 0; index < team.StartingLineupPersonIds.Length; index++)
            {
                status.GetRequiredPlayer(team.StartingLineupPersonIds[index]).ChangeCondition(
                    -_balance.ConditionChemistry.StartingHitterConditionCost);
            }
            _familiarityRecorder.RecordStartingLineup(familiarity, team.StartingLineupPersonIds);

            var pitchesByPlayerId = new Dictionary<int, PitcherUsageReport>();
            for (int index = 0; index < usage.Count; index++)
                if (team.PersonByPlayerId.ContainsKey(usage[index].PlayerId))
                    pitchesByPlayerId[usage[index].PlayerId] = usage[index];

            AdvancePitcher(team.Roster.StartingPitcher.Player.PlayerId);
            for (int index = 0; index < team.Roster.Bullpen.Count; index++)
                AdvancePitcher(team.Roster.Bullpen[index].Player.PlayerId);

            void AdvancePitcher(int playerId)
            {
                string personId = team.PersonByPlayerId[playerId];
                bool used = pitchesByPlayerId.TryGetValue(playerId, out PitcherUsageReport report);
                int pitchCount = used ? report.PitchCount : 0;
                TeamSeasonPlayerStatus player = status.GetRequiredPlayer(personId);
                player.AdvancePitchingWorkload(pitchCount);
                if (!used || pitchCount <= 0) return;
                int units = (pitchCount + 29) / 30;
                player.ChangeCondition(-checked(units * _balance.ConditionChemistry.PitcherConditionCostPerThirtyPitches));
            }

            for (int index = 0; index < batteryUsage.Count; index++)
            {
                BatteryUsageReport report = batteryUsage[index];
                if (report.TeamId != team.Roster.TeamId)
                    continue;
                if (!team.PersonByPlayerId.TryGetValue(report.PitcherPlayerId, out string pitcherPersonId) ||
                    !team.PersonByPlayerId.TryGetValue(report.CatcherPlayerId, out string catcherPersonId))
                {
                    throw new InvalidOperationException("Battery 사용 기록이 경기 로스터 PlayerId와 일치하지 않습니다.");
                }
                _familiarityRecorder.RecordBatteryOuts(
                    familiarity,
                    pitcherPersonId,
                    catcherPersonId,
                    report.DefensiveOuts);
            }
        }

        private ManagerModeTransactionStatus ApplyHomeFinance(
            ManagerHistoricalRuntimeState runtime,
            ScheduledGameState game,
            string homeTeamKey,
            string awayTeamKey,
            MatchResult match,
            bool playerIsHome,
            out HomeGameFinanceResult finance)
        {
            HomeGameOutcome outcome = match.HomeBoxScore.Runs > match.AwayBoxScore.Runs
                ? HomeGameOutcome.Win
                : match.HomeBoxScore.Runs < match.AwayBoxScore.Runs
                    ? HomeGameOutcome.Loss
                    : HomeGameOutcome.Draw;
            HomeGameContext context = CreateHomeGameContext(
                runtime,
                game,
                homeTeamKey,
                awayTeamKey,
                playerIsHome,
                outcome);
            return _coordinator.ApplyHomeGameFinance(
                runtime,
                context,
                new Pcg32Random(DeterministicSeed.Derive(game.RandomSeed, AttendanceRandomStream)),
                out finance);
        }

        private HomeGameContext CreateHomeGameContext(
            ManagerHistoricalRuntimeState runtime,
            ScheduledGameState game,
            string homeTeamKey,
            string awayTeamKey,
            bool playerIsHome,
            HomeGameOutcome outcome)
        {
            double recentPerformance = ResolveRecentPerformance(
                runtime.ManagerMode.LiveSeason.Schedule.Games,
                runtime.ManagerMode.LiveSeason.PlayerTeamId,
                game.GameId);
            double opponentAttraction = ResolveOpponentAttraction(playerIsHome ? awayTeamKey : homeTeamKey);
            int maximumRound = GetMaximumRound(runtime.ManagerMode.LiveSeason.Schedule.Games);
            double seasonImportance = maximumRound == 0 ? 0d : Clamp01(game.Round / (double)maximumRound);
            return new HomeGameContext(
                "game:" + game.GameId,
                runtime.ManagerMode.LiveSeason.SeasonId,
                runtime.ManagerMode.LiveSeason.CurrentWeekIndex,
                homeTeamKey,
                awayTeamKey,
                playerIsHome ? GameVenue.Home : GameVenue.Away,
                runtime.League.Grade,
                outcome,
                recentPerformance,
                opponentAttraction,
                seasonImportance,
                rivalryStoryStrength: 0d);
        }

        /// <summary>구단주 AI 경기와 공개 UI가 같은 현재 로스터·밸런스로 팀컬러를 조회한다.</summary>
        public static TeamColorDefinition[] ResolveAiTeamColors(CurrentRosterState roster,
            WorldCardCatalog catalog, TeamColorBalanceTable balance)
        {
            IReadOnlyList<TeamColorRosterCard> cards = TeamColorResolver.CreateRosterCards(roster, catalog);
            return new TeamColorResolver().SelectAutomatic(cards,
                InitialTeamColorDefinitionFactory.CreateForRoster(cards, balance));
        }

        private PerCardBonusMap ResolveTeamColorBonuses(
            CurrentRosterState roster,
            WorldCardCatalog catalog,
            IReadOnlyList<string> equippedIds)
        {
            TeamColorDefinition slot0 = GetOptionalTeamColor(equippedIds, 0);
            TeamColorDefinition slot1 = GetOptionalTeamColor(equippedIds, 1);
            return new TeamColorResolver().ApplyEquipped(roster, catalog, _teamColors, slot0, slot1);
        }

        private TeamColorDefinition GetOptionalTeamColor(IReadOnlyList<string> ids, int index)
        {
            if (ids == null || index >= ids.Count || string.IsNullOrWhiteSpace(ids[index])) return null;
            if (_teamColorsById.TryGetValue(ids[index], out TeamColorDefinition definition)) return definition;
            throw new InvalidOperationException($"TeamColor {ids[index]} Definition이 없습니다.");
        }

        private TacticLoadoutState CreateConfirmedLoadout(IReadOnlyList<string> ids)
        {
            var cards = new TacticCardDefinition[ids.Count];
            for (int index = 0; index < ids.Count; index++)
            {
                if (!_tacticCardsById.TryGetValue(ids[index], out cards[index]))
                    throw new InvalidOperationException($"TacticCard {ids[index]} Definition이 없습니다.");
            }
            var loadout = new TacticLoadoutState(cards);
            loadout.ConfirmGame();
            return loadout;
        }

        private TacticLoadoutState CreateAiLoadout(
            ManagerHistoricalRuntimeState runtime,
            ScheduledGameState game,
            int teamId)
        {
            TacticCardDefinition[] cards = _aiTacticSelectionResolver.Select(
                _tacticCards,
                runtime.League.Grade,
                game.RandomSeed,
                teamId);
            var loadout = new TacticLoadoutState(cards);
            loadout.ConfirmGame();
            return loadout;
        }

        private static void ConsumePlayerTactics(
            TacticCollectionState collection,
            IReadOnlyList<string> tacticCardIds)
        {
            if (!collection.TryConsumeAll(tacticCardIds))
                throw new InvalidOperationException("경기 전에 검증한 전술 카드 수량이 변경되었습니다.");
        }

        private PlayerCardDefinition GetCard(ManagerHistoricalRuntimeState runtime, string cardId)
        {
            if (runtime.WorldCardCatalog.TryGetCard(cardId, out PlayerCardDefinition card)) return card;
            throw new KeyNotFoundException($"CardId {cardId}를 찾을 수 없습니다.");
        }

        private double ResolveOpponentAttraction(string teamSeasonKey)
        {
            return _content.TryGetTeamSeason(teamSeasonKey, out TeamSeasonDefinition team)
                ? Clamp01(team.ReferenceStrength / 100d)
                : 0.5d;
        }

        private string GetTeamDisplayName(ManagerHistoricalRuntimeState runtime, string teamSeasonKey)
        {
            // 합성 참가팀은 Franchise TeamSeason 정의가 없으므로 Key에서 직접 이름을 만든다.
            if (SpecialCompositeTeamDefinition.TryCreateDisplayName(teamSeasonKey, out string compositeName))
                return compositeName;
            if (_content.TryGetTeamSeason(teamSeasonKey, out TeamSeasonDefinition team))
                return runtime.IdentityRegistry.GetFranchiseDisplayName(team.FranchiseId);
            return teamSeasonKey;
        }

        /// <summary>공개 등록 역할에서 경기와 구단 조회가 공유하는 AI 기본 라인업을 만든다. 상태와 난수는 변경하지 않는다.</summary>
        public static LineupPresetState CreateRosterRolePlan(CurrentRosterState roster)
        {
            var starting = new LineupPresetSlot[9];
            var batting = new string[9];
            var bench = new string[5];
            var rotation = new string[5];
            var bullpen = new string[4];
            string setup = null;
            string closer = null;
            int benchIndex = 0;
            for (int index = 0; index < roster.Entries.Count; index++)
            {
                ActiveRosterEntry entry = roster.Entries[index];
                if (entry.Role >= ActiveRosterRole.StartingCatcher &&
                    entry.Role <= ActiveRosterRole.StartingDesignatedHitter)
                {
                    int slot = (int)entry.Role - (int)ActiveRosterRole.StartingCatcher;
                    starting[slot] = new LineupPresetSlot(entry.CardId, (PlayerPosition)(slot + 1));
                    batting[slot] = entry.CardId;
                }
                else if (entry.Role == ActiveRosterRole.BenchHitter) bench[benchIndex++] = entry.CardId;
                else if (entry.Role >= ActiveRosterRole.StartingPitcher1 && entry.Role <= ActiveRosterRole.StartingPitcher5)
                    rotation[(int)entry.Role - (int)ActiveRosterRole.StartingPitcher1] = entry.CardId;
                else if (entry.Role >= ActiveRosterRole.Bullpen1 && entry.Role <= ActiveRosterRole.Bullpen4)
                    bullpen[(int)entry.Role - (int)ActiveRosterRole.Bullpen1] = entry.CardId;
                else if (entry.Role == ActiveRosterRole.Setup) setup = entry.CardId;
                else if (entry.Role == ActiveRosterRole.Closer) closer = entry.CardId;
            }
            return new LineupPresetState(
                "runtime:" + roster.TeamSeasonKey,
                "AI 기본 운용",
                starting,
                batting,
                bench,
                rotation,
                bullpen,
                setup,
                closer,
                new string[2],
                Array.Empty<string>());
        }

        private static ActiveRosterEntry FindEntry(CurrentRosterState roster, string cardId)
        {
            for (int index = 0; index < roster.Entries.Count; index++)
                if (string.Equals(roster.Entries[index].CardId, cardId, StringComparison.Ordinal))
                    return roster.Entries[index];
            throw new KeyNotFoundException($"CardId {cardId}가 ActiveRoster에 없습니다.");
        }

        private static int FindStartingCatcherIndex(MatchRosterSnapshot roster)
        {
            for (int index = 0; index < roster.StartingLineup.Count; index++)
                if (roster.StartingLineup[index].FieldingPosition == PlayerPosition.Catcher) return index;
            throw new InvalidOperationException("선발 포수가 없습니다.");
        }

        private static int FindTeamReferenceIndex(
            IReadOnlyList<ManagerTeamReference> teams,
            string teamSeasonKey)
        {
            for (int index = 0; index < teams.Count; index++)
                if (string.Equals(teams[index].TeamSeasonKey, teamSeasonKey, StringComparison.Ordinal)) return index;
            throw new KeyNotFoundException($"TeamSeasonKey {teamSeasonKey}의 TeamId가 없습니다.");
        }

        private static double ResolveRecentPerformance(
            IReadOnlyList<ScheduledGameState> games,
            int teamId,
            int excludedGameId)
        {
            int points = 0;
            int possible = 0;
            for (int index = games.Count - 1; index >= 0 && possible < 10; index--)
            {
                ScheduledGameState game = games[index];
                if (!game.IsCompleted || game.GameId == excludedGameId || !game.IncludesTeam(teamId)) continue;
                int own = game.AwayTeamId == teamId ? game.AwayRuns : game.HomeRuns;
                int other = game.AwayTeamId == teamId ? game.HomeRuns : game.AwayRuns;
                points += own > other ? 2 : own == other ? 1 : 0;
                possible += 2;
            }
            return possible == 0 ? 0.5d : points / (double)possible;
        }

        private static int GetMaximumRound(IReadOnlyList<ScheduledGameState> games)
        {
            int maximum = 0;
            for (int index = 0; index < games.Count; index++)
                if (games[index].Round > maximum) maximum = games[index].Round;
            return maximum;
        }

        private static double Clamp01(double value)
        {
            if (value < 0d) return 0d;
            return value > 1d ? 1d : value;
        }

        private static int PositiveModulo(int value, int modulus)
        {
            int result = value % modulus;
            return result < 0 ? result + modulus : result;
        }

        private static T[] CopyDefinitions<T>(IReadOnlyList<T> source) where T : class
        {
            if (source == null || source.Count == 0) return Array.Empty<T>();
            var result = new T[source.Count];
            for (int index = 0; index < source.Count; index++)
                result[index] = source[index] ?? throw new ArgumentException("null Definition이 있습니다.", nameof(source));
            return result;
        }

        private static Dictionary<string, T> Index<T>(
            IReadOnlyList<T> source,
            Func<T, string> getId,
            string idName)
        {
            var result = new Dictionary<string, T>(source.Count, StringComparer.Ordinal);
            for (int index = 0; index < source.Count; index++)
            {
                string id = getId(source[index]);
                if (!result.TryAdd(id, source[index]))
                    throw new ArgumentException($"{idName}는 중복될 수 없습니다.", nameof(source));
            }
            return result;
        }

        private sealed class TeamMatchBuild
        {
            public TeamMatchBuild(
                MatchRosterSnapshot roster,
                string[] startingLineupPersonIds,
                Dictionary<int, string> personByPlayerId,
                LineupChemistryResult lineupChemistry)
            {
                Roster = roster;
                StartingLineupPersonIds = startingLineupPersonIds;
                PersonByPlayerId = personByPlayerId;
                LineupChemistry = lineupChemistry;
            }

            public MatchRosterSnapshot Roster { get; }
            public string[] StartingLineupPersonIds { get; }
            public Dictionary<int, string> PersonByPlayerId { get; }
            public LineupChemistryResult LineupChemistry { get; }
        }

        private sealed class PlayerIdMap
        {
            private readonly Dictionary<string, int> _ids;

            private PlayerIdMap(Dictionary<string, int> ids)
            {
                _ids = ids;
            }

            public int Get(string teamSeasonKey, string playerSeasonId) =>
                _ids[CreateKey(teamSeasonKey, playerSeasonId)];

            public static PlayerIdMap Create(IReadOnlyList<CurrentRosterState> rosters)
            {
                var seasonIds = new List<string>();
                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (int teamIndex = 0; teamIndex < rosters.Count; teamIndex++)
                {
                    for (int playerIndex = 0; playerIndex < rosters[teamIndex].Entries.Count; playerIndex++)
                    {
                        string id = CreateKey(
                            rosters[teamIndex].TeamSeasonKey,
                            rosters[teamIndex].Entries[playerIndex].PlayerSeasonId);
                        if (seen.Add(id)) seasonIds.Add(id);
                    }
                }
                seasonIds.Sort(StringComparer.Ordinal);
                var ids = new Dictionary<string, int>(seasonIds.Count, StringComparer.Ordinal);
                for (int index = 0; index < seasonIds.Count; index++) ids.Add(seasonIds[index], index + 1);
                return new PlayerIdMap(ids);
            }

            private static string CreateKey(string teamSeasonKey, string playerSeasonId) =>
                string.Concat(teamSeasonKey, "|", playerSeasonId);
        }
    }
}
