using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Rules;
using Baseball.Core.Teams;
using Baseball.Game.Career;
using Baseball.Game.Diagnostics;
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
            string headCoachDisplayName,
            int scoutingPointsEarned = 0)
        {
            if (scoutingPointsEarned < 0)
                throw new ArgumentOutOfRangeException(nameof(scoutingPointsEarned));
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
            ScoutingPointsEarned = scoutingPointsEarned;
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

        /// <summary>이 경기로 받은 SP다. 경기 완료와 같은 트랜잭션에서 지급된다.</summary>
        public int ScoutingPointsEarned { get; }
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
    public sealed partial class ManagerModeMatchService
    {
        private enum AiScheduleAdvanceMode
        {
            ThroughPlayerRound,
            Deferred
        }

        private const ulong AttendanceRandomStream = 0x415454454E44414EUL;
        private const ulong ConditionRandomStream = 0x434F4E444954494FUL;

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
        private readonly MatchExecutionProfile _offscreenExecutionProfile;
        private readonly Dictionary<string, AiRosterPreparation> _aiRosterPreparations =
            new Dictionary<string, AiRosterPreparation>(StringComparer.Ordinal);

        // 로스터는 교체로 갱신된다. 카드 정의까지 같은 경우에만 정적 준비 결과를 재사용한다.
        private AiRosterPreparation GetAiRosterPreparation(ManagerHistoricalRuntimeState runtime, string teamSeasonKey)
        {
            CurrentRosterState roster = runtime.GetRoster(teamSeasonKey);
            if (_aiRosterPreparations.TryGetValue(teamSeasonKey, out AiRosterPreparation preparation) &&
                ReferenceEquals(preparation.Runtime, runtime) &&
                ReferenceEquals(preparation.Roster, roster) &&
                ReferenceEquals(preparation.Catalog, runtime.WorldCardCatalog))
                return preparation;
            preparation = new AiRosterPreparation(runtime, roster, runtime.WorldCardCatalog, _balance.TeamColor);
            _aiRosterPreparations[teamSeasonKey] = preparation;
            return preparation;
        }

        private sealed class AiRosterPreparation
        {
            public AiRosterPreparation(ManagerHistoricalRuntimeState runtime, CurrentRosterState roster,
                WorldCardCatalog catalog, TeamColorBalanceTable balance)
            {
                Runtime = runtime;
                Roster = roster;
                Catalog = catalog;
                Plan = CreateRosterRolePlan(roster, catalog);
                Bonuses = ResolveAiTeamColorBonuses(roster, catalog, balance, out _);
                Players = new Player[roster.Entries.Count];
            }

            public ManagerHistoricalRuntimeState Runtime { get; }
            public CurrentRosterState Roster { get; }
            public WorldCardCatalog Catalog { get; }
            public LineupPresetState Plan { get; }
            public PerCardBonusMap Bonuses { get; }
            // Player는 불변 경기 입력이다. 피로·컨디션·호흡은 여기 저장하지 않는다.
            public Player[] Players { get; }
        }

        public ManagerModeMatchService(
            HistoricalBakedContent content,
            BalanceTable balance,
            IReadOnlyList<TeamColorDefinition> teamColors = null,
            IReadOnlyList<TacticCardDefinition> tacticCards = null,
            MatchExecutionProfile? offscreenExecutionProfile = null, DugoutStaffCatalog dugoutCatalog = null)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            _offscreenExecutionProfile = offscreenExecutionProfile ?? MatchExecutionProfile.AggregateBackground;
            if (!_offscreenExecutionProfile.Equals(MatchExecutionProfile.AggregateBackground) &&
                !_offscreenExecutionProfile.Equals(MatchExecutionProfile.DetailedBackground))
                throw new ArgumentException("다른 조에는 백그라운드 계산 프로필만 사용할 수 있습니다.", nameof(offscreenExecutionProfile));
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
            _dugoutCatalog = dugoutCatalog ?? DugoutStaffCatalog.CreateDefault();
            _dugoutResolver = new DugoutTacticalProfileResolver();
        }

        public ManagerModeMatchService(
            IHistoricalContentProvider contentProvider,
            BalanceTable balance,
            IReadOnlyList<TeamColorDefinition> teamColors = null,
            IReadOnlyList<TacticCardDefinition> tacticCards = null, DugoutStaffCatalog dugoutCatalog = null)
            : this(
                (contentProvider ?? throw new ArgumentNullException(nameof(contentProvider))).Load(),
                balance,
                teamColors,
                tacticCards, dugoutCatalog: dugoutCatalog)
        {
        }

        /// <summary>다음 경기를 현재 로스터와 availability로 다시 검증한 뒤 정확히 한 번 실행한다.</summary>
        public ManagerModeMatchResult PlayNextGame(
            ManagerHistoricalRuntimeState runtime,
            IMatchEventSink eventSink = null,
            MatchExecutionProfile? executionProfile = null)
        {
            return PlayNextGame(
                runtime,
                PlayerIdMap.Create(runtime),
                eventSink,
                executionProfile,
                AiScheduleAdvanceMode.ThroughPlayerRound);
        }

        /// <summary>시즌 진행 UI가 AI 대진을 프레임별로 나눌 수 있도록 플레이어 경기 한 건만 확정한다.</summary>
        internal ManagerModeMatchResult PlayNextPlayerGameForSeasonSimulation(
            ManagerHistoricalRuntimeState runtime,
            PlayerIdMap playerIds)
        {
            return PlayNextGame(
                runtime,
                playerIds,
                NullMatchEventSink.Instance,
                MatchExecutionProfile.DetailedBackground,
                AiScheduleAdvanceMode.Deferred);
        }

        private ManagerModeMatchResult PlayNextGame(
            ManagerHistoricalRuntimeState runtime,
            PlayerIdMap playerIds,
            IMatchEventSink eventSink,
            MatchExecutionProfile? executionProfile,
            AiScheduleAdvanceMode aiAdvanceMode)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (playerIds == null) throw new ArgumentNullException(nameof(playerIds));
            if (executionProfile.HasValue && executionProfile.Value.EngineKind != SimulationEngineKind.Detailed)
                throw new InvalidOperationException("플레이어 조의 경기는 상세 계산을 사용해야 합니다.");
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
            // 상대 구단이 실제 일정만큼 소모된 컨디션·투수 피로로 나오도록,
            // 플레이어 경기가 열리는 라운드 이전의 AI 대진을 먼저 확정한다.
            if (aiAdvanceMode == AiScheduleAdvanceMode.ThroughPlayerRound)
                SimulateAiGamesThrough(runtime, playerIds, game.Round - 1);

            LineupPresetState opponentPlan = GetAiRosterPreparation(runtime, opponentKey).Plan;
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
            int scoutingPointsEarned = GrantMatchScoutingPoints(runtime, match, playerIsHome);
            OwnerTraitTrainingService.GrantMatchReward(runtime, _balance.TraitTraining);
            ApplyPostGameState(mode, playerBuild, opponentBuild, match);
            RecordStatistics(mode, game, match);
            ConsumePlayerTactics(runtime.TacticCollection, playerPlan.TacticCardIds);
            mode.ClearSelectedTactics();
            mode.Dugout.RecordMatchCompleted();
            OwnerSupportService.CompleteMatch(runtime);

            // 같은 라운드의 나머지 대진까지 확정해야 순위표에서 플레이어 구단만 경기 수가 앞서가지 않는다.
            if (aiAdvanceMode == AiScheduleAdvanceMode.ThroughPlayerRound)
                SimulateAiGamesThrough(runtime, playerIds, game.Round);
            // 홀수 구단 일정에서는 플레이어 구단이 마지막 라운드에 bye일 수 있어,
            // 플레이어 일정이 끝난 뒤 남은 AI 대진을 시즌 마감 전에 함께 소진한다.
            if (aiAdvanceMode == AiScheduleAdvanceMode.ThroughPlayerRound &&
                mode.LiveSeason.NextPlayerGame == null)
                SimulateAiGamesThrough(runtime, playerIds, int.MaxValue);

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
                _dugoutCatalog.GetHeadCoach(mode.Dugout.HeadCoachId).DisplayName,
                scoutingPointsEarned);
        }

        /// <summary>남은 대진을 조별 경기 해상도와 저장 Seed로 모두 완료한다.</summary>
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

        /// <summary>조별 포스트시즌 한 경기를 정규시즌과 같은 로스터·상태·해상도로 확정한다.</summary>
        public MatchResult PlayPostseasonGame(
            ManagerHistoricalRuntimeState runtime,
            OwnerLeagueGroupState group,
            OwnerPostseasonSeriesState series,
            ScheduledGameState game,
            IMatchEventSink eventSink,
            MatchExecutionProfile? executionProfile,
            out ManagerModeMatchResult playerResult)
        {
            if (runtime == null || group == null || series == null || game == null)
                throw new ArgumentNullException();
            if (!ReferenceEquals(group.Postseason.CurrentSeries, series) || game.IsCompleted ||
                series.Games.Count == 0 || !ReferenceEquals(series.Games[series.Games.Count - 1], game))
                throw new InvalidOperationException("현재 포스트시즌 대진의 미완료 경기가 필요합니다.");

            ManagerLiveSeasonState season = group.Season;
            string awayTeamKey = season.GetTeamSeasonKey(game.AwayTeamId);
            string homeTeamKey = season.GetTeamSeasonKey(game.HomeTeamId);
            bool includesPlayer = game.IncludesTeam(season.PlayerTeamId) &&
                string.Equals(season.GetTeamSeasonKey(season.PlayerTeamId), runtime.PlayerTeamSeasonKey, StringComparison.Ordinal);
            if (includesPlayer && executionProfile.HasValue && executionProfile.Value.EngineKind != SimulationEngineKind.Detailed)
                throw new InvalidOperationException("플레이어 조의 포스트시즌은 상세 계산을 사용해야 합니다.");
            bool playerIsHome = includesPlayer && game.HomeTeamId == season.PlayerTeamId;
            PreGamePlanSnapshot playerPlan = null;
            TeamMatchBuild awayBuild;
            TeamMatchBuild homeBuild;
            TacticLoadoutState awayTactics;
            TacticLoadoutState homeTactics;
            int rotationIndex = GetMaximumRound(season.Schedule.Games) + CountPostseasonGames(group.Postseason);
            // 시리즈 사이와 홈구장 이동일에 하루 휴식을 반영한다.
            int gameNumber = series.Games.Count;
            int restRounds = gameNumber == 1 || series.Round != OwnerPostseasonRound.WildCard &&
                (gameNumber == 3 || gameNumber == (series.Round == OwnerPostseasonRound.Championship ? 6 : 5)) ? 1 : 0;
            PlayerIdMap playerIds = PlayerIdMap.Create(runtime);

            if (includesPlayer)
            {
                LineupPresetState preset = runtime.ManagerMode.GetSelectedLineupPresetForGame(game);
                if (!runtime.TacticCollection.CanConsume(preset.DefaultTacticCardIds))
                    throw new InvalidOperationException("장착한 전술 카드의 보유 수량이 부족합니다.");
                LineupPresetValidationResult validation = _presetValidator.Validate(
                    preset, CreateValidationContext(runtime, runtime.PlayerTeamSeasonKey));
                if (!validation.CanStartGame)
                    throw new InvalidOperationException("현재 선수단과 라인업으로 포스트시즌 경기를 시작할 수 없습니다.");
                playerPlan = new PreGamePlanSnapshot(game.GameId, runtime.PlayerTeamSeasonKey, preset, validation);
                string opponentKey = playerIsHome ? awayTeamKey : homeTeamKey;
                TeamMatchBuild playerBuild = BuildTeam(runtime, runtime.PlayerTeamSeasonKey, playerPlan,
                    rotationIndex, playerIds, restRounds);
                TeamMatchBuild opponentBuild = BuildTeam(runtime, opponentKey,
                    GetAiRosterPreparation(runtime, opponentKey).Plan,
                    rotationIndex, playerIds, restRounds);
                awayBuild = playerIsHome ? opponentBuild : playerBuild;
                homeBuild = playerIsHome ? playerBuild : opponentBuild;
                TacticLoadoutState playerTactics = CreateConfirmedLoadout(playerPlan.TacticCardIds);
                TacticLoadoutState opponentTactics = CreateAiLoadout(runtime, game,
                    playerIsHome ? game.AwayTeamId : game.HomeTeamId);
                awayTactics = playerIsHome ? opponentTactics : playerTactics;
                homeTactics = playerIsHome ? playerTactics : opponentTactics;
            }
            else
            {
                awayBuild = BuildTeam(runtime, awayTeamKey,
                    GetAiRosterPreparation(runtime, awayTeamKey).Plan,
                    rotationIndex, playerIds, restRounds);
                homeBuild = BuildTeam(runtime, homeTeamKey,
                    GetAiRosterPreparation(runtime, homeTeamKey).Plan,
                    rotationIndex, playerIds, restRounds);
                awayTactics = CreateAiLoadout(runtime, game, game.AwayTeamId);
                homeTactics = CreateAiLoadout(runtime, game, game.HomeTeamId);
            }

            var configuration = new HistoricalMatchConfiguration(
                _balance.HistoricalAssignment.CreateRule(),
                awayTacticLoadout: awayTactics,
                homeTacticLoadout: homeTactics);
            var input = new MatchInput(
                season.OriginYear,
                game.GameId,
                game.RandomSeed,
                awayBuild.Roster,
                homeBuild.Roster,
                new MatchRules(9, 6, ExtraInningPolicy.DrawAtLimit, 16, true, 0),
                SimulationRulesVersion.DetailedV2,
                SimulationVersionStamp.CreateCurrent(_balance.Version, _content.Manifest.ContentHash,
                    (int)SimulationRulesVersion.DetailedV2),
                configuration);
            MatchResult match = new MatchSimulator(_balance, MatchRandomStreams.Create(game.RandomSeed)).Simulate(
                input,
                includesPlayer ? eventSink ?? NullMatchEventSink.Instance : NullMatchEventSink.Instance,
                includesPlayer ? executionProfile ?? MatchExecutionProfile.DetailedBackground :
                    ResolveBackgroundExecutionProfile(runtime, season));

            HomeGameFinanceResult finance = null;
            ManagerModeTransactionStatus financeStatus = ManagerModeTransactionStatus.Rejected;
            if (includesPlayer)
            {
                financeStatus = ApplyHomeFinance(runtime, game, homeTeamKey, awayTeamKey, match, playerIsHome, out finance);
                if (financeStatus == ManagerModeTransactionStatus.InsufficientMoney)
                    throw new InvalidOperationException("홈 경기 운영비를 지불할 수 없어 포스트시즌 결과를 확정할 수 없습니다.");
            }
            game.Complete(match.AwayBoxScore.Runs, match.HomeBoxScore.Runs);
            ApplyPostGameState(runtime.ManagerMode, awayBuild, homeBuild, match);
            int winnerTeamId = match.AwayBoxScore.Runs > match.HomeBoxScore.Runs ? game.AwayTeamId : game.HomeTeamId;
            bool clinching = match.AwayBoxScore.Runs == match.HomeBoxScore.Runs
                ? series.Round == OwnerPostseasonRound.WildCard
                : winnerTeamId == series.HigherSeedTeamId
                ? series.HigherSeedWins + 1 >= series.HigherSeedWinsRequired
                : series.LowerSeedWins + 1 >= series.WinsRequired;
            new LeagueStatisticsService(season.Statistics).RecordMatch(match, CompetitionScope.Postseason,
                CountPostseasonGames(group.Postseason),
                series.Round == OwnerPostseasonRound.Championship,
                clinching);
            if (!includesPlayer)
            {
                playerResult = null;
                return match;
            }

            int scoutingPointsEarned = GrantMatchScoutingPoints(runtime, match, playerIsHome);
            ConsumePlayerTactics(runtime.TacticCollection, playerPlan.TacticCardIds);
            runtime.ManagerMode.ClearSelectedTactics();
            runtime.ManagerMode.Dugout.RecordMatchCompleted();
            OwnerSupportService.CompleteMatch(runtime);
            TeamMatchBuild ownedBuild = playerIsHome ? homeBuild : awayBuild;
            playerResult = new ManagerModeMatchResult(match, playerPlan, ownedBuild.LineupChemistry,
                finance, financeStatus, runtime.ManagerMode.Dugout.ManagerId, runtime.ManagerMode.Dugout.HeadCoachId,
                ownedBuild.Roster.ManagerProfile,
                _dugoutCatalog.GetManager(runtime.ManagerMode.Dugout.ManagerId).DisplayName,
                _dugoutCatalog.GetHeadCoach(runtime.ManagerMode.Dugout.HeadCoachId).DisplayName,
                scoutingPointsEarned);
            return match;
        }

        /// <summary>
        /// 플레이어 구단 경기 한 판의 SP를 지급한다. 주간 결산과 달리 경기 완료에 묶여 있어
        /// 반복 조작으로 늘릴 수 없고, 경기를 진행하는 만큼만 선수 수집이 앞으로 간다.
        /// </summary>
        private int GrantMatchScoutingPoints(ManagerHistoricalRuntimeState runtime, MatchResult match, bool playerIsHome)
        {
            int playerRuns = playerIsHome ? match.HomeBoxScore.Runs : match.AwayBoxScore.Runs;
            int opponentRuns = playerIsHome ? match.AwayBoxScore.Runs : match.HomeBoxScore.Runs;
            int reward = _balance.ScoutEconomy.GetMatchReward(playerRuns > opponentRuns);
            runtime.Economy.AddScoutingPoints(reward);
            return reward;
        }

        private static int CountPostseasonGames(OwnerPostseasonState postseason)
        {
            int count = 0;
            for (int index = 0; index < postseason.Series.Count; index++) count += postseason.Series[index].Games.Count;
            return count;
        }

        /// <summary>플레이어 일정 뒤 남은 AI 대진을 동일 엔진과 Seed로 소진한다.</summary>
        internal void CompleteRemainingAiGames(ManagerHistoricalRuntimeState runtime)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            SimulateAiGamesThrough(
                runtime,
                PlayerIdMap.Create(runtime),
                int.MaxValue);
        }

        /// <summary>지정 라운드까지 가장 앞선 AI 대진 한 건만 찾아 확정한다.</summary>
        internal bool TrySimulateNextAiGameThrough(
            ManagerHistoricalRuntimeState runtime,
            PlayerIdMap playerIds,
            AiScheduleCursor cursor,
            int throughRound,
            out int completedRound,
            out bool isPlayerLeagueGame)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (playerIds == null) throw new ArgumentNullException(nameof(playerIds));
            if (cursor == null) throw new ArgumentNullException(nameof(cursor));
            completedRound = 0;
            isPlayerLeagueGame = false;
            if (throughRound <= 0) return false;
            if (!cursor.TryTakeNext(runtime, throughRound, out ManagerLiveSeasonState season,
                    out ScheduledGameState game))
                return false;
            SimulateAiGame(runtime, playerIds, game, season);
            completedRound = game.Round;
            isPlayerLeagueGame = ReferenceEquals(season, runtime.ManagerMode.LiveSeason);
            return true;
        }

        private void SimulateAiGame(
            ManagerHistoricalRuntimeState runtime,
            PlayerIdMap playerIds,
            ScheduledGameState game,
            ManagerLiveSeasonState season)
        {
            string awayTeamKey = season.GetTeamSeasonKey(game.AwayTeamId);
            string homeTeamKey = season.GetTeamSeasonKey(game.HomeTeamId);
            TeamMatchBuild awayBuild = BuildTeam(
                runtime,
                awayTeamKey,
                GetAiRosterPreparation(runtime, awayTeamKey).Plan,
                game.Round,
                playerIds);
            TeamMatchBuild homeBuild = BuildTeam(
                runtime,
                homeTeamKey,
                GetAiRosterPreparation(runtime, homeTeamKey).Plan,
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
            MatchResult match;
            using (new ProfilerSection("OwnerSeason.MatchEngine").Auto())
                match = new MatchSimulator(_balance, MatchRandomStreams.Create(game.RandomSeed))
                    .Simulate(input, NullMatchEventSink.Instance, ResolveBackgroundExecutionProfile(runtime, season));

            game.Complete(match.AwayBoxScore.Runs, match.HomeBoxScore.Runs);
            using (new ProfilerSection("OwnerSeason.ApplyState").Auto())
                ApplyPostGameState(runtime.ManagerMode, awayBuild, homeBuild, match);
            using (new ProfilerSection("OwnerSeason.Statistics").Auto())
                new LeagueStatisticsService(season.Statistics).RecordMatch(match, CompetitionScope.RegularSeason,
                    game.Round, isChampionship: false, isSeriesClinching: false);
        }

        /// <summary>같은 순위표를 겨루는 조 전체에 같은 경기 해상도를 적용한다.</summary>
        internal MatchExecutionProfile ResolveBackgroundExecutionProfile(
            ManagerHistoricalRuntimeState runtime, ManagerLiveSeasonState season)
        {
            return string.Equals(runtime.ManagerMode.LiveSeason.SeasonId, season.SeasonId, StringComparison.Ordinal)
                ? MatchExecutionProfile.DetailedBackground
                : _offscreenExecutionProfile;
        }

        /// <summary>지정 라운드까지 남은 AI 구단 대진을 라운드·GameId 순서로 정확히 한 번 진행한다.</summary>
        private void SimulateAiGamesThrough(
            ManagerHistoricalRuntimeState runtime,
            PlayerIdMap playerIds,
            int throughRound)
        {
            if (throughRound <= 0) return;
            if (runtime.LeagueWorld == null)
            {
                SimulateGroupThrough(runtime, playerIds, runtime.ManagerMode.LiveSeason, throughRound);
                return;
            }
            foreach (var group in runtime.LeagueWorld.Groups)
                SimulateGroupThrough(runtime, playerIds, group.Season, throughRound);
        }

        private void SimulateGroupThrough(ManagerHistoricalRuntimeState runtime, PlayerIdMap playerIds,
            ManagerLiveSeasonState season, int throughRound)
        {
            IReadOnlyList<ScheduledGameState> games = season.Schedule.Games;
            for (int index = 0; index < games.Count; index++)
            {
                ScheduledGameState game = games[index];
                if (game.IsCompleted || game.Round > throughRound) continue;
                // 플레이어 구단 경기는 라인업·전술 확정을 거쳐야 하므로 자동 진행 대상이 아니다.
                if (ReferenceEquals(season, runtime.ManagerMode.LiveSeason) && game.IncludesTeam(season.PlayerTeamId)) continue;
                SimulateAiGame(runtime, playerIds, game, season);
            }
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
            PlayerIdMap playerIds,
            int? restRoundsOverride = null)
        {
            using var preparationScope = new ProfilerSection("OwnerSeason.BuildTeam").Auto();
            CurrentRosterState activeRoster = runtime.GetRoster(teamSeasonKey);
            int restRounds = restRoundsOverride ?? ResolveRestRounds(runtime, teamSeasonKey, rotationIndex);
            var pitcherIds = new List<int>(ActiveRosterCompositionRule.PitcherCount);
            int conditionBonus = ResolveHeadCoachConditionBonus(runtime, teamSeasonKey, _balance.ConditionChemistry,
                _dugoutCatalog, _dugoutResolver);
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
            AiRosterPreparation aiPreparation = null;
            if (runtime.HasOwnedEconomy(teamSeasonKey))
                teamColorBonuses = ResolveTeamColorBonuses(activeRoster, runtime.WorldCardCatalog, equippedColors);
            else
            {
                aiPreparation = GetAiRosterPreparation(runtime, teamSeasonKey);
                teamColorBonuses = aiPreparation.Bonuses;
            }
            var playersByCard = new Dictionary<string, Player>(activeRoster.Entries.Count, StringComparer.Ordinal);
            var personByPlayerId = new Dictionary<int, string>(activeRoster.Entries.Count);
            for (int index = 0; index < activeRoster.Entries.Count; index++)
            {
                ActiveRosterEntry entry = activeRoster.Entries[index];
                int playerId = playerIds.Get(teamSeasonKey, entry.PlayerSeasonId);
                Player player = aiPreparation?.Players[index];
                // 새 시즌·구단 편성이 ID 매핑을 바꿔도 이전 경기 입력이 섞이지 않는다.
                if (player == null || player.PlayerId != playerId)
                {
                    player = CreatePlayer(runtime, teamSeasonKey, entry, teamColorBonuses, playerId);
                    if (aiPreparation != null) aiPreparation.Players[index] = player;
                }
                playersByCard.Add(entry.CardId, player);
                personByPlayerId.Add(player.PlayerId, entry.PlayerPersonId);
                if (runtime.WorldCardCatalog.GetPlayerSeason(GetCard(runtime, entry.CardId)).PlayerType == PlayerType.Pitcher)
                    pitcherIds.Add(player.PlayerId);
            }

            var positionByCard = new Dictionary<string, PlayerPosition>(StringComparer.Ordinal);
            for (int index = 0; index < lineupSlots.Count; index++)
                positionByCard.Add(lineupSlots[index].CardId, lineupSlots[index].Position);
            var lineup = new LineupSlot[battingOrder.Count];
            var lineupPeople = new string[battingOrder.Count];
            var chemistryPlayers = new LineupChemistryPlayer[battingOrder.Count];
            var battingOrderFits = new Dictionary<int, BattingOrderFit>();
            for (int index = 0; index < battingOrder.Count; index++)
            {
                string cardId = battingOrder[index];
                Player player = playersByCard[cardId];
                lineup[index] = new LineupSlot(player, positionByCard[cardId]);
                battingOrderFits.Add(player.PlayerId,
                    PreferredBattingOrderRule.GetFit(GetCard(runtime, cardId).PreferredBattingOrder, index + 1));
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
                null,
                restRounds, conditionBonus);
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
                    (ActiveRosterRole)((int)ActiveRosterRole.Bullpen1 + index),
                    restRounds, conditionBonus);
            }
            bullpen[bullpenCards.Count] = CreatePitcherEntry(
                runtime,
                teamSeasonKey,
                activeRoster,
                setupCard,
                playersByCard,
                PitcherRole.Setup,
                ActiveRosterRole.Setup,
                restRounds, conditionBonus);
            bullpen[bullpenCards.Count + 1] = CreatePitcherEntry(
                runtime,
                teamSeasonKey,
                activeRoster,
                closerCard,
                playersByCard,
                PitcherRole.Closer,
                ActiveRosterRole.Closer,
                restRounds, conditionBonus);

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
                runtime, teamSeasonKey,
                runtime.ManagerMode.GetPlayerStatus(teamSeasonKey),
                activeRoster,
                playersByCard,
                matchPlayerIds,
                lineupChemistry,
                conditionBonus, battingOrderFits);
            MatchBatteryConditionEntry[] battery = CreateBatteryEntries(
                teamSeasonKey,
                activeRoster,
                playersByCard,
                personByPlayerId,
                starter,
                bullpen,
                familiarity);
            int teamId = runtime.LeagueWorld != null ? runtime.LeagueWorld.GetTeam(teamSeasonKey).TeamId :
                runtime.ManagerMode.LiveSeason.Teams[FindTeamReferenceIndex(runtime.ManagerMode.LiveSeason.Teams, teamSeasonKey)].TeamId;
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
            return new TeamMatchBuild(teamSeasonKey, roster, lineupPeople, personByPlayerId, lineupChemistry,
                pitcherIds.ToArray(), restRounds, battingOrderFits);
        }

        private static int ResolveRestRounds(ManagerHistoricalRuntimeState runtime, string teamSeasonKey, int round)
        {
            ManagerLiveSeasonState season = runtime.LeagueWorld?.GetGroup(teamSeasonKey).Season ?? runtime.ManagerMode.LiveSeason;
            int teamId = season.Teams[FindTeamReferenceIndex(season.Teams, teamSeasonKey)].TeamId;
            int lastRound = 0;
            foreach (ScheduledGameState game in season.Schedule.Games)
                if (game.IsCompleted && game.Round < round && game.IncludesTeam(teamId))
                    lastRound = Math.Max(lastRound, game.Round);
            // 최근 부하는 3일까지만 남는다. 일정에서 파생해 재시도·불러오기 때 중복 회복하지 않는다.
            return Math.Min(3, Math.Max(0, round - lastRound - 1));
        }

        /// <summary>수석코치의 선수단 컨디션 효과를 경기와 공개 조회에서 같은 값으로 계산한다.</summary>
        public static int ResolveHeadCoachConditionBonus(ManagerHistoricalRuntimeState runtime, string teamSeasonKey,
            ConditionChemistryBalanceTable balance)
        {
            return ResolveHeadCoachConditionBonus(runtime, teamSeasonKey, balance,
                DugoutStaffCatalog.CreateDefault(), new DugoutTacticalProfileResolver());
        }

        private static int ResolveHeadCoachConditionBonus(ManagerHistoricalRuntimeState runtime, string teamSeasonKey,
            ConditionChemistryBalanceTable balance, DugoutStaffCatalog catalog, DugoutTacticalProfileResolver resolver)
        {
            ManagerLiveSeasonState season = runtime.LeagueWorld?.GetGroup(teamSeasonKey).Season ?? runtime.ManagerMode.LiveSeason;
            int teamId = season.Teams[FindTeamReferenceIndex(season.Teams, teamSeasonKey)].TeamId;
            var dugout = runtime.HasOwnedEconomy(teamSeasonKey) ? runtime.ManagerMode.Dugout :
                resolver.CreateAiState(teamId, catalog);
            return catalog.GetHeadCoach(dugout.HeadCoachId).HasConditionSupport ? balance.HeadCoachConditionBonus : 0;
        }

        private static PitchingWorkloadState ApplyRestRounds(PitchingWorkloadState workload, int restRounds)
        {
            for (int index = 0; index < restRounds; index++) workload = workload.AdvanceDay(0);
            return workload;
        }

        private Player CreatePlayer(
            ManagerHistoricalRuntimeState runtime,
            string teamSeasonKey,
            ActiveRosterEntry entry,
            PerCardBonusMap teamColorBonuses,
            int playerId)
        {
            using var playerScope = new ProfilerSection("OwnerSeason.CreatePlayer").Auto();
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
            int GetPermanent(PlayerAbility ability) => Math.Max(1, Math.Min(AttributeRating.Maximum, GetRawPermanent(ability)));
            double GetRawEffective(PlayerAbility ability) => Math.Max(1d, Math.Min(_balance.MatchRatingCurve.Caps.HardCap,
                checked(_ownerCardAbilityResolver.ResolveContribution(season, card, usesOwnedEconomy ? owned : null, ability).Total
                    + teamColorBonuses.Get(entry.CardId, ability))));
            int Get(PlayerAbility ability)
            {
                int raw = checked(GetRawPermanent(ability) + teamColorBonuses.Get(entry.CardId, ability));
                return MatchRatingCurve.ResolveMatchInput(raw, ability, _balance.MatchRatingCurve);
            }
            var batter = new BatterAttributes(
                Get(PlayerAbility.Contact),
                Get(PlayerAbility.Power),
                Get(PlayerAbility.Speed),
                Get(PlayerAbility.Bunt),
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
                secondaryPositions: season.SecondaryPositions,
                  traitIds: usesOwnedEconomy
                      ? _ownerCardAbilityResolver.ResolveActiveTraitIds(owned)
                      : Array.Empty<string>(),
                  cardTrait: usesOwnedEconomy && owned != null && owned.Trait.trait != CardTraitKind.None
                      ? new CardTraitEffect(owned.Trait.trait, _balance.TraitTraining.Get(owned.Trait.trait).effect
                          * _balance.TraitTraining.multipliers[(int)owned.Trait.rank - 1]) : default,
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
            ActiveRosterRole? activeRosterRole,
            int restRounds,
            int conditionBonus)
        {
            ActiveRosterEntry entry = FindEntry(roster, cardId);
            PlayerCardDefinition card = GetCard(runtime, cardId);
            PlayerSeasonDefinition season = runtime.WorldCardCatalog.GetPlayerSeason(card);
            TeamSeasonPlayerStatus playerStatus = runtime.ManagerMode
                .GetPlayerStatus(teamSeasonKey)
                .GetRequiredPlayer(entry.PlayerPersonId);
            PitchingWorkloadState load = ApplyRestRounds(playerStatus.PitchingWorkload, restRounds);
            var usage = HistoricalPitcherUsageResolver.Resolve(season, assignedRole, _balance.HistoricalPitcherUsage);
            return new PitcherRosterEntry(
                players[cardId],
                assignedRole,
                Math.Min(100, playerStatus.StoredBaseCondition + conditionBonus +
                    (teamSeasonKey == runtime.PlayerTeamSeasonKey ? OwnerSupportService.GetConditionBonus(runtime, cardId) : 0)),
                new RecentPitchingWorkload(
                    load.PreviousDayPitches,
                    load.TwoDaysAgoPitches,
                    load.ThreeDaysAgoPitches),
                naturalRole: season.PitcherRole,
                activeRosterRole: activeRosterRole,
                playerSeasonId: season.PlayerSeasonId,
                naturalRoleConfidence: season.PitcherRoleConfidence,
                capacityMultiplier: usage.Capacity,
                recoveryMultiplier: usage.Recovery);
        }

        private MatchPlayerConditionEntry[] CreateConditionEntries(
            ManagerHistoricalRuntimeState runtime, string teamSeasonKey,
            TeamSeasonPlayerStatusState status,
            CurrentRosterState roster,
            IReadOnlyDictionary<string, Player> players,
            ISet<int> matchPlayerIds,
            LineupChemistryResult lineupChemistry,
            int conditionBonus, IReadOnlyDictionary<int, BattingOrderFit> battingOrderFits)
        {
            var result = new MatchPlayerConditionEntry[matchPlayerIds.Count];
            int resultIndex = 0;
            for (int index = 0; index < roster.Entries.Count; index++)
            {
                ActiveRosterEntry entry = roster.Entries[index];
                Player player = players[entry.CardId];
                if (!matchPlayerIds.Contains(player.PlayerId)) continue;
                int lineupModifier = lineupChemistry.GetConditionModifier(entry.PlayerPersonId);
                battingOrderFits.TryGetValue(player.PlayerId, out BattingOrderFit fit);
                int storedCondition = status.GetRequiredPlayer(entry.PlayerPersonId).StoredBaseCondition;
                int temporaryBonus = conditionBonus + (teamSeasonKey == runtime.PlayerTeamSeasonKey
                    ? OwnerSupportService.GetConditionBonus(runtime, entry.CardId) : 0);
                int preferenceModifier = ConditionFluctuationResolver.ResolvePreferredOrderModifier(
                    storedCondition, lineupModifier + temporaryBonus, fit, _balance.ConditionChemistry);
                result[resultIndex++] = new MatchPlayerConditionEntry(
                    player.PlayerId,
                    new EffectiveMatchCondition(
                        status.GetRequiredPlayer(entry.PlayerPersonId).StoredBaseCondition,
                        assignmentModifier: preferenceModifier,
                        lineupChemistryModifier: lineupModifier,
                        batteryChemistryModifier: 0,
                        temporaryModifier: temporaryBonus));
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
            ApplyTeamPostGame(mode, first, match.PitcherUsage, match.BatteryUsage, match.Input.RandomSeed);
            ApplyTeamPostGame(mode, second, match.PitcherUsage, match.BatteryUsage, match.Input.RandomSeed);
        }

        private void ApplyTeamPostGame(
            ManagerModeRuntimeState mode,
            TeamMatchBuild team,
            IReadOnlyList<PitcherUsageReport> usage,
            IReadOnlyList<BatteryUsageReport> batteryUsage,
            ulong gameSeed)
        {
            string teamKey = team.TeamSeasonKey;
            TeamSeasonPlayerStatusState status = mode.GetPlayerStatus(teamKey);
            TeamChemistryFamiliarityState familiarity = mode.GetFamiliarity(teamKey);
            var conditionResolver = new ConditionFluctuationResolver();
            ulong conditionSeed = DeterministicSeed.Derive(gameSeed, ConditionRandomStream);
            foreach (var entry in team.PersonByPlayerId)
            {
                TeamSeasonPlayerStatus player = status.GetRequiredPlayer(entry.Value);
                var random = new Pcg32Random(DeterministicSeed.Derive(conditionSeed, (ulong)entry.Key));
                team.BattingOrderFits.TryGetValue(entry.Key, out BattingOrderFit fit);
                player.SetCondition(conditionResolver.ResolveNextCondition(player.StoredBaseCondition, _balance.ConditionChemistry, random, fit));
            }
            _familiarityRecorder.RecordStartingLineup(familiarity, team.StartingLineupPersonIds);

            var pitchesByPlayerId = new Dictionary<int, PitcherUsageReport>();
            for (int index = 0; index < usage.Count; index++)
                if (team.PersonByPlayerId.ContainsKey(usage[index].PlayerId))
                    pitchesByPlayerId[usage[index].PlayerId] = usage[index];

            // 당일 엔트리 밖의 선발도 휴식일 0구를 기록해야 다음 등판에 과거 부하가 남지 않는다.
            for (int index = 0; index < team.PitcherIds.Length; index++)
                AdvancePitcher(team.PitcherIds[index]);

            void AdvancePitcher(int playerId)
            {
                string personId = team.PersonByPlayerId[playerId];
                bool used = pitchesByPlayerId.TryGetValue(playerId, out PitcherUsageReport report);
                int pitchCount = used ? report.PitchCount : 0;
                TeamSeasonPlayerStatus player = status.GetRequiredPlayer(personId);
                for (int index = 0; index < team.RestRounds; index++) player.AdvancePitchingWorkload(0);
                player.AdvancePitchingWorkload(pitchCount);
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

        /// <summary>AI의 공개 팀컬러 선택과 선수별 실제 경기 보너스를 한 번의 조회로 반환한다.</summary>
        public static PerCardBonusMap ResolveAiTeamColorBonuses(
            CurrentRosterState roster,
            WorldCardCatalog catalog,
            TeamColorBalanceTable balance,
            out TeamColorDefinition[] selected)
        {
            selected = ResolveAiTeamColors(roster, catalog, balance);
            var definitions = new List<TeamColorDefinition>(selected.Length);
            for (int index = 0; index < selected.Length; index++)
                if (selected[index] != null) definitions.Add(selected[index]);
            return new TeamColorResolver().ApplyEquipped(
                roster, catalog, definitions, selected[0], selected[1]);
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
                runtime.LeagueWorld?.GetGrade(teamId) ?? runtime.League.Grade,
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
            // 플레이어 구단은 중계·기록 표기 전부에서 구단주가 직접 지은 이름을 쓴다.
            if (runtime.TryGetPlayerClubName(teamSeasonKey, out string playerClubName))
                return playerClubName;
            // 합성 참가팀은 Franchise TeamSeason 정의가 없으므로 Key에서 직접 이름을 만든다.
            if (SpecialCompositeTeamDefinition.TryCreateDisplayName(teamSeasonKey, out string compositeName))
                return compositeName;
            // 임시 구단도 다른 참가 구단과 같은 형식으로 표시한다. CPU라는 사실은 UI에 드러내지 않는다.
            if (LeagueFillerTeamKey.TryParse(teamSeasonKey, out LeagueFillerDeckType deck, out string source))
                return deck == LeagueFillerDeckType.YearTeam
                    ? GetTeamDisplayName(runtime, source)
                    : LeagueFillerTeamKey.GetDeckTeamName(deck);
            if (_content.TryGetTeamSeason(teamSeasonKey, out TeamSeasonDefinition team))
                return runtime.IdentityRegistry.GetFranchiseDisplayName(team.FranchiseId);
            return teamSeasonKey;
        }

        /// <summary>공개 등록 역할에서 경기와 구단 조회가 공유하는 AI 기본 라인업을 만든다. 상태와 난수는 변경하지 않는다.</summary>
        public static LineupPresetState CreateRosterRolePlan(CurrentRosterState roster, WorldCardCatalog catalog = null)
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
                catalog == null ? batting : PreferredBattingOrderEvaluator.CreateBattingOrder(batting, catalog),
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
                string teamSeasonKey,
                MatchRosterSnapshot roster,
                string[] startingLineupPersonIds,
                Dictionary<int, string> personByPlayerId,
                LineupChemistryResult lineupChemistry,
                int[] pitcherIds,
                int restRounds, Dictionary<int, BattingOrderFit> battingOrderFits)
            {
                TeamSeasonKey = teamSeasonKey;
                Roster = roster;
                StartingLineupPersonIds = startingLineupPersonIds;
                PersonByPlayerId = personByPlayerId;
                LineupChemistry = lineupChemistry;
                PitcherIds = pitcherIds;
                RestRounds = restRounds;
                BattingOrderFits = battingOrderFits;
            }

            public string TeamSeasonKey { get; }
            public MatchRosterSnapshot Roster { get; }
            public string[] StartingLineupPersonIds { get; }
            public Dictionary<int, string> PersonByPlayerId { get; }
            public LineupChemistryResult LineupChemistry { get; }
            public int[] PitcherIds { get; }
            public int RestRounds { get; }
            public IReadOnlyDictionary<int, BattingOrderFit> BattingOrderFits { get; }
        }

        internal sealed class PlayerIdMap
        {
            private readonly Dictionary<string, int> _ids;
            internal IReadOnlyDictionary<string, int> Entries => _ids;

            private PlayerIdMap(Dictionary<string, int> ids)
            {
                _ids = ids;
            }

            public int Get(string teamSeasonKey, string playerSeasonId) =>
                _ids[CreateKey(teamSeasonKey, playerSeasonId)];

            public bool TryGet(string teamSeasonKey, string playerSeasonId, out int playerId) =>
                _ids.TryGetValue(CreateKey(teamSeasonKey, playerSeasonId), out playerId);

            /// <summary>조 편성·1군 등록 변경으로 기존 기록의 선수 ID가 바뀌지 않게 월드 원장을 사용한다.</summary>
            public static PlayerIdMap Create(ManagerHistoricalRuntimeState runtime)
            {
                if (runtime.LeagueWorld == null) return Create(runtime.Rosters);
                // 현재 조의 교체 결과가 월드 목록에 누락된 진행 중 상태도 경기 입력과 일치시킨다.
                foreach (CurrentRosterState roster in runtime.Rosters)
                    runtime.LeagueWorld.ReplaceRoster(roster);
                PlayerIdMap map = runtime.LeagueWorld.PlayerIds ?? Create(runtime.WorldRosters);
                map.EnsureRosters(runtime.WorldRosters);
                runtime.LeagueWorld.PlayerIds = map;
                return map;
            }

            internal static PlayerIdMap Restore(IReadOnlyDictionary<string, int> entries)
            {
                var ids = new Dictionary<string, int>(StringComparer.Ordinal);
                var numbers = new HashSet<int>();
                foreach (var entry in entries)
                {
                    if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value <= 0 || !numbers.Add(entry.Value))
                        throw new ArgumentException("월드 선수 ID 원장이 올바르지 않습니다.");
                    ids.Add(entry.Key, entry.Value);
                }
                return new PlayerIdMap(ids);
            }

            internal void EnsureRosters(IReadOnlyList<CurrentRosterState> rosters)
            {
                PlayerIdMap candidates = Create(rosters);
                var keys = new List<string>(candidates._ids.Keys);
                keys.Sort(StringComparer.Ordinal);
                int nextId = 1;
                foreach (int id in _ids.Values) nextId = Math.Max(nextId, checked(id + 1));
                foreach (string key in keys) if (!_ids.ContainsKey(key)) _ids.Add(key, nextId++);
            }

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

        /// <summary>긴 시즌에서 완료된 Schedule 앞부분을 매 Step 다시 훑지 않는 결정론적 AI 대진 Cursor다.</summary>
        internal sealed class AiScheduleCursor
        {
            private readonly ManagerLiveSeasonState[] _seasons;
            private readonly int[] _nextGameIndexes;

            private AiScheduleCursor(ManagerLiveSeasonState[] seasons)
            {
                _seasons = seasons;
                _nextGameIndexes = new int[seasons.Length];
            }

            public static AiScheduleCursor Create(ManagerHistoricalRuntimeState runtime)
            {
                if (runtime == null) throw new ArgumentNullException(nameof(runtime));
                if (runtime.LeagueWorld == null)
                    return new AiScheduleCursor(new[] { runtime.ManagerMode.LiveSeason });
                var seasons = new ManagerLiveSeasonState[runtime.LeagueWorld.Groups.Count];
                for (int index = 0; index < seasons.Length; index++)
                    seasons[index] = runtime.LeagueWorld.Groups[index].Season;
                return new AiScheduleCursor(seasons);
            }

            public bool TryTakeNext(
                ManagerHistoricalRuntimeState runtime,
                int throughRound,
                out ManagerLiveSeasonState season,
                out ScheduledGameState game)
            {
                for (int seasonIndex = 0; seasonIndex < _seasons.Length; seasonIndex++)
                {
                    season = _seasons[seasonIndex];
                    IReadOnlyList<ScheduledGameState> games = season.Schedule.Games;
                    int gameIndex = _nextGameIndexes[seasonIndex];
                    while (gameIndex < games.Count)
                    {
                        game = games[gameIndex];
                        if (game.Round > throughRound)
                        {
                            _nextGameIndexes[seasonIndex] = gameIndex;
                            break;
                        }
                        gameIndex++;
                        _nextGameIndexes[seasonIndex] = gameIndex;
                        if (game.IsCompleted) continue;
                        if (ReferenceEquals(season, runtime.ManagerMode.LiveSeason) &&
                            game.IncludesTeam(season.PlayerTeamId))
                            continue;
                        return true;
                    }
                }
                season = null;
                game = null;
                return false;
            }
        }
    }
}
