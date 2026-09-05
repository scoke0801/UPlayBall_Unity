using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Career;
using Baseball.Simulation.Historical;

namespace Baseball.Game.Historical
{
    /// <summary>다음 경기 분석, 최신 프리셋 검증, 궁합 Preview를 한 번 계산한 결과다.</summary>
    public sealed class ManagerPregamePreparation
    {
        public ManagerPregamePreparation(
            ScheduledGameState scheduledGame,
            string opponentTeamSeasonKey,
            ScoutingConfidenceContext confidenceContext,
            OpponentScoutingReportEvidence reportEvidence,
            OpponentScoutingReport scoutingReport,
            LineupPresetValidationResult presetValidation,
            PreGamePlanSnapshot planSnapshot,
            LineupChemistryResult lineupChemistry,
            BatteryChemistryResult? batteryChemistry)
        {
            ScheduledGame = scheduledGame ?? throw new ArgumentNullException(nameof(scheduledGame));
            if (string.IsNullOrWhiteSpace(opponentTeamSeasonKey))
                throw new ArgumentException("상대 TeamSeasonKey가 필요합니다.", nameof(opponentTeamSeasonKey));
            OpponentTeamSeasonKey = opponentTeamSeasonKey.Trim();
            ConfidenceContext = confidenceContext;
            ReportEvidence = reportEvidence ?? throw new ArgumentNullException(nameof(reportEvidence));
            ScoutingReport = scoutingReport ?? throw new ArgumentNullException(nameof(scoutingReport));
            PresetValidation = presetValidation ?? throw new ArgumentNullException(nameof(presetValidation));
            if (presetValidation.CanStartGame != (planSnapshot != null))
                throw new ArgumentException("Valid 프리셋만 PreGamePlanSnapshot을 가질 수 있습니다.");
            if (planSnapshot == null && (lineupChemistry != null || batteryChemistry.HasValue))
                throw new ArgumentException("Invalid 프리셋은 Chemistry Preview를 가질 수 없습니다.");
            PlanSnapshot = planSnapshot;
            LineupChemistry = lineupChemistry;
            BatteryChemistry = batteryChemistry;
        }

        public ScheduledGameState ScheduledGame { get; }
        public string OpponentTeamSeasonKey { get; }
        public ScoutingConfidenceContext ConfidenceContext { get; }
        public OpponentScoutingReportEvidence ReportEvidence { get; }
        public OpponentScoutingReport ScoutingReport { get; }
        public LineupPresetValidationResult PresetValidation { get; }
        public PreGamePlanSnapshot PlanSnapshot { get; }
        public LineupChemistryResult LineupChemistry { get; }
        public BatteryChemistryResult? BatteryChemistry { get; }
        public bool CanStartGame => PlanSnapshot != null;
    }

    /// <summary>공개 로스터와 workload만으로 상대 분석 및 사용자 경기 계획을 준비한다.</summary>
    public sealed class ManagerPregameService
    {
        private static readonly string[] PublicRosterEvidenceTags = { "public-active-roster" };
        private static readonly string[] PublicWorkloadEvidenceTags = { "public-active-roster", "public-workload" };

        private readonly BalanceTable _balance;
        private readonly HistoricalBakedContent _content;
        private readonly Dictionary<string, PlayerPersonDefinition> _peopleById;
        private readonly ManagerModeCoordinator _coordinator;
        private readonly OpponentScoutingReportBuilder _reportBuilder;
        private readonly LineupPresetValidator _presetValidator;
        private readonly LineupChemistryResolver _lineupChemistryResolver;
        private readonly BatteryChemistryResolver _batteryChemistryResolver;

        public ManagerPregameService(
            BalanceTable balance,
            IHistoricalContentProvider contentProvider)
        {
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            if (contentProvider == null) throw new ArgumentNullException(nameof(contentProvider));
            _content = contentProvider.Load()
                ?? throw new InvalidOperationException("Runtime Historical Content Provider가 null을 반환했습니다.");
            _peopleById = IndexPeople(_content.PlayerPersons);
            _coordinator = new ManagerModeCoordinator(balance);
            var confidenceResolver = new ScoutingConfidenceResolver(balance.ScoutingConfidence);
            _reportBuilder = new OpponentScoutingReportBuilder(
                confidenceResolver,
                new ProbableStarterResolver(confidenceResolver),
                new ExpectedLineupEstimator(confidenceResolver),
                new BullpenReadinessResolver(
                    new BullpenReadinessDefinition(
                        balance.ScoutingConfidence.BullpenFreshMaximumRecentPitches,
                        balance.ScoutingConfidence.BullpenTiredMinimumRecentPitches,
                        balance.ScoutingConfidence.BullpenVeryTiredMinimumRecentPitches,
                        balance.ScoutingConfidence.BullpenFreshMinimumRestDays),
                    confidenceResolver));
            _presetValidator = new LineupPresetValidator();
            _lineupChemistryResolver = new LineupChemistryResolver(balance.ConditionChemistry);
            _batteryChemistryResolver = new BatteryChemistryResolver(balance.ConditionChemistry);
        }

        public ManagerPregamePreparation PrepareNextGame(
            ManagerHistoricalRuntimeState runtime,
            IReadOnlyList<string> availableTeamColorIds,
            IReadOnlyList<string> availableTacticCardIds)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (!runtime.HasManagerMode)
                throw new InvalidOperationException("Manager Mode 상태가 없는 Save는 경기 준비를 할 수 없습니다.");
            if (availableTeamColorIds == null) throw new ArgumentNullException(nameof(availableTeamColorIds));
            if (availableTacticCardIds == null) throw new ArgumentNullException(nameof(availableTacticCardIds));
            runtime.ContentReference.EnsureMatches(_content.Manifest);

            ManagerModeRuntimeState mode = runtime.ManagerMode;
            ScheduledGameState game = mode.LiveSeason.NextPlayerGame
                ?? throw new InvalidOperationException("플레이어 구단의 다음 경기가 없습니다.");
            int opponentTeamId = game.AwayTeamId == mode.LiveSeason.PlayerTeamId
                ? game.HomeTeamId
                : game.AwayTeamId;
            string opponentTeamSeasonKey = mode.LiveSeason.GetTeamSeasonKey(opponentTeamId);
            CurrentRosterState opponentRoster = runtime.GetRoster(opponentTeamSeasonKey);
            TeamSeasonPlayerStatusState opponentStatus = mode.GetPlayerStatus(opponentTeamSeasonKey);

            OpponentScoutingReportEvidence evidence = CreateReportEvidence(
                runtime,
                mode.LiveSeason,
                game,
                opponentRoster,
                opponentStatus);
            ScoutingConfidenceContext confidenceContext = _coordinator.CreateScoutingConfidenceContext(mode);
            OpponentScoutingReport report = _reportBuilder.Build(
                evidence,
                confidenceContext.CombinedMultiplier);

            LineupPresetState preset = mode.GetSelectedLineupPreset();
            LineupPresetValidationResult validation = ValidateLineupPreset(
                runtime,
                preset,
                availableTeamColorIds,
                availableTacticCardIds);

            if (!validation.CanStartGame)
            {
                return new ManagerPregamePreparation(
                    game,
                    opponentTeamSeasonKey,
                    confidenceContext,
                    evidence,
                    report,
                    validation,
                    null,
                    null,
                    null);
            }

            var snapshot = new PreGamePlanSnapshot(
                game.GameId,
                runtime.PlayerTeamSeasonKey,
                preset,
                validation);
            LineupChemistryResult lineupChemistry = CreateLineupChemistry(runtime, mode, preset);
            BatteryChemistryResult batteryChemistry = CreateBatteryChemistry(
                runtime,
                mode,
                preset,
                game.Round);
            return new ManagerPregamePreparation(
                game,
                opponentTeamSeasonKey,
                confidenceContext,
                evidence,
                report,
                validation,
                snapshot,
                lineupChemistry,
                batteryChemistry);
        }

        /// <summary>저장된 임의 프리셋을 현재 ActiveRoster와 availability 및 실제 장착 후보로 재검증한다.</summary>
        public LineupPresetValidationResult ValidateLineupPreset(
            ManagerHistoricalRuntimeState runtime,
            LineupPresetState preset,
            IReadOnlyList<string> availableTeamColorIds,
            IReadOnlyList<string> availableTacticCardIds)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            if (availableTeamColorIds == null) throw new ArgumentNullException(nameof(availableTeamColorIds));
            if (availableTacticCardIds == null) throw new ArgumentNullException(nameof(availableTacticCardIds));
            runtime.ContentReference.EnsureMatches(_content.Manifest);

            CurrentRosterState playerRoster = runtime.GetRoster(runtime.PlayerTeamSeasonKey);
            return _presetValidator.Validate(
                preset,
                new LineupPresetValidationContext(
                    playerRoster,
                    CreatePlayerContexts(
                        runtime,
                        playerRoster,
                        runtime.ManagerMode.GetPlayerStatus(runtime.PlayerTeamSeasonKey)),
                    _balance.HistoricalAssignment.CreateRule(),
                    availableTeamColorIds,
                    availableTacticCardIds));
        }

        private OpponentScoutingReportEvidence CreateReportEvidence(
            ManagerHistoricalRuntimeState runtime,
            ManagerLiveSeasonState season,
            ScheduledGameState game,
            CurrentRosterState opponentRoster,
            TeamSeasonPlayerStatusState opponentStatus)
        {
            ScoutingConfidenceDefinition balance = _balance.ScoutingConfidence;
            var strength = new ScoutingEvidenceStrength(
                true,
                false,
                balance.PublicRosterEvidenceQuality,
                balance.PublicRosterRecencyFactor,
                balance.PublicRosterSampleFactor);
            var starters = new List<ProbableStarterCandidateEvidence>(
                ActiveRosterCompositionRule.StartingPitcherCount);
            var hitters = new List<ExpectedLineupCandidateEvidence>(
                ActiveRosterCompositionRule.HitterCount);
            var bullpen = new List<BullpenReadinessEvidence>(
                ActiveRosterCompositionRule.BullpenPitcherCount +
                ActiveRosterCompositionRule.SetupPitcherCount +
                ActiveRosterCompositionRule.CloserPitcherCount);
            int expectedRotationIndex = (game.Round - 1) % ActiveRosterCompositionRule.StartingPitcherCount;

            for (int index = 0; index < opponentRoster.Entries.Count; index++)
            {
                ActiveRosterEntry entry = opponentRoster.Entries[index];
                PlayerSeasonDefinition playerSeason = GetPlayerSeason(runtime, entry.CardId);
                TeamSeasonPlayerStatus status = opponentStatus.GetRequiredPlayer(entry.PlayerPersonId);
                bool isAvailable = status.Availability != PlayerAvailabilityStatus.Unavailable;
                if (ActiveRosterCompositionRule.Standard.IsStartingPitcherRole(entry.Role))
                {
                    int rotationIndex = (int)entry.Role - (int)ActiveRosterRole.StartingPitcher1;
                    PlayerPersonDefinition person = GetPerson(entry.PlayerPersonId);
                    starters.Add(new ProbableStarterCandidateEvidence(
                        entry.CardId,
                        entry.PlayerPersonId,
                        person.Throws,
                        (rotationIndex - expectedRotationIndex + ActiveRosterCompositionRule.StartingPitcherCount) %
                            ActiveRosterCompositionRule.StartingPitcherCount,
                        ResolveDaysSinceLastAppearance(status.PitchingWorkload),
                        CountRecentAppearances(status.PitchingWorkload),
                        isAvailable,
                        strength,
                        PublicWorkloadEvidenceTags));
                }
                else if (ActiveRosterCompositionRule.Standard.IsHitterRole(entry.Role))
                {
                    bool isStarting = ActiveRosterCompositionRule.Standard.IsStartingHitterRole(entry.Role);
                    PlayerPosition position = isStarting
                        ? ActiveRosterCompositionRule.Standard.GetAssignedPosition(entry.Role)
                        : playerSeason.Position;
                    hitters.Add(new ExpectedLineupCandidateEvidence(
                        entry.CardId,
                        entry.PlayerPersonId,
                        position,
                        isStarting ? 1 : 0,
                        0,
                        0,
                        isStarting ? 0 : 1,
                        isStarting ? (int)entry.Role + 1d : ActiveRosterCompositionRule.StartingHitterCount,
                        isAvailable,
                        strength,
                        PublicRosterEvidenceTags));
                }
                else if (ActiveRosterCompositionRule.Standard.IsBullpenRole(entry.Role) ||
                         entry.Role == ActiveRosterRole.Setup ||
                         entry.Role == ActiveRosterRole.Closer)
                {
                    bullpen.Add(new BullpenReadinessEvidence(
                        entry.CardId,
                        entry.PlayerPersonId,
                        entry.Role,
                        SumRecentPitches(status.PitchingWorkload),
                        ResolveRestDays(status.PitchingWorkload),
                        isAvailable,
                        strength,
                        PublicWorkloadEvidenceTags));
                }
            }

            DateTime generatedDate = new DateTime(season.OriginYear, 3, 1).AddDays(game.Round - 1);
            return new OpponentScoutingReportEvidence(
                game.GameId,
                opponentRoster.TeamSeasonKey,
                generatedDate,
                starters,
                hitters,
                bullpen,
                ObservedScoutingValue<OpponentRecentForm>.Unknown(),
                ObservedScoutingValue<OpponentPerformanceProfile>.Unknown(),
                ObservedScoutingValue<OpponentPerformanceProfile>.Unknown(),
                ObservedScoutingValue<OpponentPerformanceProfile>.Unknown(),
                ObservedScoutingValue<ManagerTendencyEstimate>.Unknown(),
                Array.Empty<ObservedScoutingValue<RecentTacticPatternSummary>>(),
                Array.Empty<ScoutingReportNote>(),
                Array.Empty<ScoutingReportNote>(),
                Array.Empty<ScoutingReportNote>());
        }

        private LineupPresetPlayerContext[] CreatePlayerContexts(
            ManagerHistoricalRuntimeState runtime,
            CurrentRosterState roster,
            TeamSeasonPlayerStatusState status)
        {
            var result = new LineupPresetPlayerContext[roster.Entries.Count];
            for (int index = 0; index < result.Length; index++)
            {
                ActiveRosterEntry entry = roster.Entries[index];
                PlayerSeasonDefinition season = GetPlayerSeason(runtime, entry.CardId);
                bool isPitcher = season.Position == PlayerPosition.StartingPitcher ||
                                 season.Position == PlayerPosition.ReliefPitcher;
                result[index] = new LineupPresetPlayerContext(
                    entry.CardId,
                    season.Position,
                    isPitcher ? season.PitcherRole : (PitcherRole?)null,
                    isPitcher ? season.PitcherRoleConfidence : (PitcherRoleConfidence?)null,
                    status.GetRequiredPlayer(entry.PlayerPersonId).Availability != PlayerAvailabilityStatus.Unavailable);
            }
            return result;
        }

        private LineupChemistryResult CreateLineupChemistry(
            ManagerHistoricalRuntimeState runtime,
            ManagerModeRuntimeState mode,
            LineupPresetState preset)
        {
            var battingOrder = new LineupChemistryPlayer[preset.BattingOrderCardIds.Count];
            for (int index = 0; index < battingOrder.Length; index++)
            {
                string cardId = preset.BattingOrderCardIds[index];
                PlayerSeasonDefinition season = GetPlayerSeason(runtime, cardId);
                battingOrder[index] = new LineupChemistryPlayer(
                    season.PlayerPersonId,
                    CreateEffectiveAbilities(runtime, cardId).ToBatterAttributes());
            }
            return _lineupChemistryResolver.Resolve(
                runtime.PlayerTeamSeasonKey,
                battingOrder,
                mode.GetFamiliarity(runtime.PlayerTeamSeasonKey));
        }

        private BatteryChemistryResult CreateBatteryChemistry(
            ManagerHistoricalRuntimeState runtime,
            ManagerModeRuntimeState mode,
            LineupPresetState preset,
            int round)
        {
            int starterIndex = (round - 1) % preset.StarterRotationCardIds.Count;
            string pitcherCardId = preset.StarterRotationCardIds[starterIndex];
            string catcherCardId = null;
            for (int index = 0; index < preset.StartingLineupSlots.Count; index++)
            {
                if (preset.StartingLineupSlots[index].Position == PlayerPosition.Catcher)
                {
                    catcherCardId = preset.StartingLineupSlots[index].CardId;
                    break;
                }
            }
            if (catcherCardId == null)
                throw new InvalidOperationException("Valid 프리셋에 Catcher가 없습니다.");
            PlayerSeasonDefinition pitcher = GetPlayerSeason(runtime, pitcherCardId);
            PlayerSeasonDefinition catcher = GetPlayerSeason(runtime, catcherCardId);
            return _batteryChemistryResolver.Resolve(
                runtime.PlayerTeamSeasonKey,
                pitcher.PlayerPersonId,
                CreateEffectiveAbilities(runtime, pitcherCardId).ToPitcherAttributes(),
                catcher.PlayerPersonId,
                CreateEffectiveAbilities(runtime, catcherCardId).ToBatterAttributes(),
                mode.GetFamiliarity(runtime.PlayerTeamSeasonKey));
        }

        private AbilityRatings CreateEffectiveAbilities(ManagerHistoricalRuntimeState runtime, string cardId)
        {
            PlayerCardDefinition card = GetCard(runtime, cardId);
            AbilityRatings ratings = runtime.WorldCardCatalog.GetPlayerSeason(card).CreateBaseAttributes();
            runtime.TryGetOwnedCard(cardId, out OwnedPlayerCardState owned);
            for (int index = 0; index < PlayerAbilityCatalog.AbilityCount; index++)
            {
                var ability = (PlayerAbility)index;
                int training = owned == null ? 0 : owned.Training.GetBonus(ability);
                ratings.AddClamped(ability, card.GetModifier(ability) + training);
            }
            return ratings;
        }

        private PlayerSeasonDefinition GetPlayerSeason(ManagerHistoricalRuntimeState runtime, string cardId)
        {
            return runtime.WorldCardCatalog.GetPlayerSeason(GetCard(runtime, cardId));
        }

        private static PlayerCardDefinition GetCard(ManagerHistoricalRuntimeState runtime, string cardId)
        {
            if (!runtime.WorldCardCatalog.TryGetCard(cardId, out PlayerCardDefinition card))
                throw new InvalidOperationException($"WorldCardCatalog에 CardId {cardId}가 없습니다.");
            return card;
        }

        private PlayerPersonDefinition GetPerson(string playerPersonId)
        {
            if (!_peopleById.TryGetValue(playerPersonId, out PlayerPersonDefinition person))
                throw new InvalidOperationException($"공개 PlayerPerson 원본에 {playerPersonId}가 없습니다.");
            return person;
        }

        private static Dictionary<string, PlayerPersonDefinition> IndexPeople(
            IReadOnlyList<PlayerPersonDefinition> source)
        {
            var result = new Dictionary<string, PlayerPersonDefinition>(source.Count, StringComparer.Ordinal);
            for (int index = 0; index < source.Count; index++)
            {
                PlayerPersonDefinition person = source[index]
                    ?? throw new ArgumentException("null PlayerPerson 원본이 있습니다.", nameof(source));
                if (!result.TryAdd(person.PlayerPersonId, person))
                    throw new ArgumentException("PlayerPersonId 원본이 중복되었습니다.", nameof(source));
            }
            return result;
        }

        private static int SumRecentPitches(PitchingWorkloadState workload)
        {
            return checked(workload.PreviousDayPitches + workload.TwoDaysAgoPitches + workload.ThreeDaysAgoPitches);
        }

        private static int CountRecentAppearances(PitchingWorkloadState workload)
        {
            int count = workload.PreviousDayPitches > 0 ? 1 : 0;
            if (workload.TwoDaysAgoPitches > 0) count++;
            if (workload.ThreeDaysAgoPitches > 0) count++;
            return count;
        }

        private static int ResolveDaysSinceLastAppearance(PitchingWorkloadState workload)
        {
            if (workload.PreviousDayPitches > 0) return 1;
            if (workload.TwoDaysAgoPitches > 0) return 2;
            if (workload.ThreeDaysAgoPitches > 0) return 3;
            return 4;
        }

        private static int ResolveRestDays(PitchingWorkloadState workload)
        {
            if (workload.PreviousDayPitches > 0) return 0;
            if (workload.TwoDaysAgoPitches > 0) return 1;
            if (workload.ThreeDaysAgoPitches > 0) return 2;
            return 3;
        }
    }
}
