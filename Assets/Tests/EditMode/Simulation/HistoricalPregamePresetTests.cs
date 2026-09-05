using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Historical;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Simulation
{
    /// <summary>경기 전 상대 분석과 프리셋 경계를 구현 Agent와 독립적으로 공격 검증한다.</summary>
    public sealed class HistoricalPregamePresetTests
    {
        private const string TeamSeasonKey = "COMETS_2026";

        [Test]
        public void ScoutingConfidenceResolver_IntelState경계는포함하한으로동작한다()
        {
            ScoutingConfidenceResolver resolver = CreateConfidenceResolver();

            Assert.That(resolver.ResolveState(0.199999d), Is.EqualTo(IntelState.Unknown));
            Assert.That(resolver.ResolveState(0.20d), Is.EqualTo(IntelState.LowConfidence));
            Assert.That(resolver.ResolveState(0.499999d), Is.EqualTo(IntelState.LowConfidence));
            Assert.That(resolver.ResolveState(0.50d), Is.EqualTo(IntelState.Estimated));
            Assert.That(resolver.ResolveState(0.799999d), Is.EqualTo(IntelState.Estimated));
            Assert.That(resolver.ResolveState(0.80d), Is.EqualTo(IntelState.HighConfidence));
            Assert.That(resolver.ResolveState(0d, isConfirmed: true), Is.EqualTo(IntelState.Confirmed));
        }

        [Test]
        public void ScoutingConfidenceResolver_추정값은Modifier상한과Inferred상한을넘지않는다()
        {
            ScoutingConfidenceResolver resolver = CreateConfidenceResolver();
            var strength = new ScoutingEvidenceStrength(true, false, 1d, 1d, 1d);

            double confidence = resolver.CalculateConfidence(strength, combinedModifier: 100d);
            ScoutedValue<string> value = resolver.Resolve(
                new ObservedScoutingValue<string>("관측값", strength, new[] { "completed-game" }),
                combinedModifier: 100d);

            Assert.That(confidence, Is.EqualTo(0.90d));
            Assert.That(value.State, Is.EqualTo(IntelState.HighConfidence));
            Assert.That(value.State, Is.Not.EqualTo(IntelState.Confirmed));
            Assert.That(() => resolver.CalculateConfidence(strength, 0d), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void ScoutingConfidenceResolver_근거가없으면값과식별자를노출하지않는다()
        {
            ScoutingConfidenceResolver resolver = CreateConfidenceResolver();
            ScoutedValue<string> result = resolver.Resolve(
                ObservedScoutingValue<string>.Unknown(new[] { "no-completed-game" }),
                combinedModifier: 1d);

            Assert.That(result.HasValue, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.State, Is.EqualTo(IntelState.Unknown));
            Assert.That(result.Confidence01, Is.Zero);
            Assert.That(result.EvidenceTags, Is.EqualTo(new[] { "no-completed-game" }));
        }

        [Test]
        public void ProbableStarterResolver_입력순서와무관하게StableId로동률을해소한다()
        {
            var resolver = new ProbableStarterResolver(CreateConfidenceResolver());
            ProbableStarterCandidateEvidence cardB = CreateStarterEvidence("CARD_B", 0, 5, 3);
            ProbableStarterCandidateEvidence cardA = CreateStarterEvidence("CARD_A", 0, 5, 3);
            ProbableStarterCandidateEvidence unavailable = CreateStarterEvidence(
                "CARD_0_HIDDEN",
                0,
                99,
                99,
                isPubliclyAvailable: false);

            ScoutedValue<ProbableStarterProjection> first = resolver.Resolve(
                new[] { cardB, unavailable, cardA },
                1d);
            ScoutedValue<ProbableStarterProjection> second = resolver.Resolve(
                new[] { cardA, cardB, unavailable },
                1d);

            Assert.That(first.HasValue, Is.True);
            Assert.That(first.Value.Player.CardId, Is.EqualTo("CARD_A"));
            Assert.That(second.Value.Player.CardId, Is.EqualTo(first.Value.Player.CardId));
            Assert.That(first.Value.Player.CardId, Is.Not.EqualTo(unavailable.CardId));
        }

        [Test]
        public void ProbableStarterResolver_공개불가또는근거없는후보만있으면Unknown이다()
        {
            var resolver = new ProbableStarterResolver(CreateConfidenceResolver());
            ProbableStarterCandidateEvidence unavailable = CreateStarterEvidence(
                "SECRET_STARTER",
                0,
                10,
                10,
                isPubliclyAvailable: false);
            var noEvidence = new ProbableStarterCandidateEvidence(
                "NO_EVIDENCE",
                "PERSON_NO_EVIDENCE",
                Handedness.Right,
                0,
                10,
                10,
                true,
                ScoutingEvidenceStrength.None,
                new[] { "not-observed" });

            ScoutedValue<ProbableStarterProjection> result = resolver.Resolve(
                new[] { unavailable, noEvidence },
                1d);

            Assert.That(result.HasValue, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.State, Is.EqualTo(IntelState.Unknown));
        }

        [Test]
        public void ExpectedLineupEstimator_입력순서와무관하게9명을같은타순으로선택한다()
        {
            var estimator = new ExpectedLineupEstimator(CreateConfidenceResolver());
            ExpectedLineupCandidateEvidence[] candidates = CreateLineupEvidence(11);

            IReadOnlyList<ScoutedValue<ExpectedLineupEntry>> first = estimator.Estimate(
                candidates,
                Handedness.Left,
                1d);
            IReadOnlyList<ScoutedValue<ExpectedLineupEntry>> second = estimator.Estimate(
                candidates.Reverse().ToArray(),
                Handedness.Left,
                1d);

            Assert.That(first.Count, Is.EqualTo(ActiveRosterCompositionRule.StartingHitterCount));
            Assert.That(second.Count, Is.EqualTo(first.Count));
            for (int index = 0; index < first.Count; index++)
            {
                Assert.That(second[index].Value.Player.CardId, Is.EqualTo(first[index].Value.Player.CardId));
                Assert.That(second[index].Value.BattingOrder, Is.EqualTo(index + 1));
            }
        }

        [Test]
        public void OpponentScoutingReport_Unknown근거는빈값이고출력계약에HiddenState가없다()
        {
            OpponentScoutingReport report = CreateReportBuilder().Build(
                CreateUnknownReportEvidence(),
                combinedConfidenceModifier: 1d);

            Assert.That(report.ProbableStarter.HasValue, Is.False);
            Assert.That(report.ExpectedLineup, Is.Empty);
            Assert.That(report.BullpenReadiness, Is.Empty);
            Assert.That(report.ReportConfidenceSummary.State, Is.EqualTo(IntelState.Unknown));
            Assert.That(report.ReportConfidenceSummary.ObservedItemCount, Is.Zero);

            Assert.That(typeof(OpponentScoutingReport).GetProperty("ActualLineup"), Is.Null);
            Assert.That(typeof(OpponentScoutingReport).GetProperty("ActualTacticLoadout"), Is.Null);
            Assert.That(typeof(OpponentScoutingReport).GetProperty("RandomSeed"), Is.Null);
            Assert.That(typeof(OpponentScoutingReport).GetProperty("UtilityScore"), Is.Null);
            Assert.That(typeof(BullpenReadinessEntry).GetProperty("Stamina"), Is.Null);
            Assert.That(typeof(BullpenReadinessEntry).GetProperty("RecentPitchCount"), Is.Null);
            Assert.That(
                typeof(OpponentScoutingReportBuilder)
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .SelectMany(method => method.GetParameters())
                    .Any(parameter => parameter.ParameterType.Name.Contains("Random")),
                Is.False,
                "Report Builder가 미래 RNG를 입력받아서는 안 됩니다.");
        }

        [Test]
        public void BullpenReadinessResolver_정확한투구수대신경계등급만반환한다()
        {
            var resolver = new BullpenReadinessResolver(
                new BullpenReadinessDefinition(10, 30, 60, 2),
                CreateConfidenceResolver());
            var evidence = new[]
            {
                CreateBullpenEvidence("BULLPEN_A", ActiveRosterRole.Bullpen1, 10, 2, true),
                CreateBullpenEvidence("BULLPEN_B", ActiveRosterRole.Bullpen2, 30, 1, true),
                CreateBullpenEvidence("BULLPEN_C", ActiveRosterRole.Setup, 60, 1, true),
                CreateBullpenEvidence("BULLPEN_D", ActiveRosterRole.Closer, 0, 5, false)
            };

            IReadOnlyList<ScoutedValue<BullpenReadinessEntry>> result = resolver.Resolve(evidence, 1d);

            Assert.That(result.Select(value => value.Value.Readiness), Is.EqualTo(new[]
            {
                BullpenReadiness.Fresh,
                BullpenReadiness.Tired,
                BullpenReadiness.VeryTired,
                BullpenReadiness.Unavailable
            }));
            Assert.That(typeof(BullpenReadinessEntry).GetProperties().Select(property => property.Name),
                Does.Not.Contain("RecentPitchCount"));
        }

        [Test]
        public void LineupPreset_정본슬롯수와Tactic2장상한을강제한다()
        {
            Assert.That(
                () => CreateValidPreset(teamColorIds: new[] { "COLOR_A" }),
                Throws.TypeOf<ArgumentException>());
            Assert.That(
                () => CreateValidPreset(tacticIds: new[] { "TACTIC_A", "TACTIC_B", "TACTIC_C" }),
                Throws.TypeOf<ArgumentException>());
            Assert.That(
                () => CreateValidPreset(tacticIds: new[] { "TACTIC_A", "TACTIC_A" }),
                Throws.TypeOf<ArgumentException>());

            LineupPresetState preset = CreateValidPreset();
            Assert.That(preset.TeamColorIds.Count, Is.EqualTo(LineupPresetState.TeamColorSlotCount));
            Assert.That(preset.DefaultTacticCardIds.Count, Is.EqualTo(LineupPresetState.MaximumTacticCardCount));
        }

        [Test]
        public void LineupPresetValidator_현재25인과가용Loadout이면ValidSnapshot으로동결된다()
        {
            CurrentRosterState roster = CreateValidRoster("CARD");
            LineupPresetState preset = CreateValidPreset();
            LineupPresetValidationResult validation = CreateValidator().Validate(
                preset,
                CreateValidationContext(roster));

            Assert.That(validation.Status, Is.EqualTo(LineupPresetValidationStatus.Valid));
            Assert.That(validation.CanStartGame, Is.True);
            var snapshot = new PreGamePlanSnapshot(77, TeamSeasonKey, preset, validation);
            Assert.That(snapshot.StartingLineupSlots.Count, Is.EqualTo(9));
            Assert.That(snapshot.StarterRotationCardIds.Count, Is.EqualTo(5));
            Assert.That(snapshot.BullpenAssignmentCardIds.Count, Is.EqualTo(4));
            Assert.That(snapshot.TeamColorIds, Is.EqualTo(new[] { "COLOR_A", "COLOR_B" }));
            Assert.That(snapshot.TacticCardIds, Is.EqualTo(new[] { "TACTIC_A", "TACTIC_B" }));
        }

        [Test]
        public void LineupPresetValidator_ActiveRoster밖의오래된카드는PartiallyValid이며자동등록하지않는다()
        {
            CurrentRosterState roster = CreateValidRoster("CARD");
            LineupPresetState preset = CreatePresetWithStaleStartingCard();

            LineupPresetValidationResult result = CreateValidator().Validate(
                preset,
                CreateValidationContext(roster));

            Assert.That(result.Status, Is.EqualTo(LineupPresetValidationStatus.PartiallyValid));
            Assert.That(result.CanStartGame, Is.False);
            Assert.That(HasIssue(result, LineupPresetValidationIssueCode.CardNotOnActiveRoster), Is.True);
            Assert.That(roster.Entries.Count, Is.EqualTo(ActiveRosterCompositionRule.ActiveRosterSize));
            Assert.That(roster.Entries.Any(entry => entry.CardId == "STALE_CARD"), Is.False);
            Assert.That(
                () => new PreGamePlanSnapshot(77, TeamSeasonKey, preset, result),
                Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void LineupPresetValidator_현재Unavailable선수는PartiallyValid로경기시작을막는다()
        {
            CurrentRosterState roster = CreateValidRoster("CARD");
            LineupPresetState preset = CreateValidPreset();
            LineupPresetValidationResult result = CreateValidator().Validate(
                preset,
                CreateValidationContext(roster, unavailableCardId: "CARD_00"));

            Assert.That(result.Status, Is.EqualTo(LineupPresetValidationStatus.PartiallyValid));
            Assert.That(result.CanStartGame, Is.False);
            Assert.That(HasIssue(result, LineupPresetValidationIssueCode.CardUnavailable, "CARD_00"), Is.True);
        }

        [Test]
        public void LineupPresetValidator_비주포지션은ValidWarning과기존Penalty를반환한다()
        {
            CurrentRosterState roster = CreateValidRoster("CARD");
            LineupPresetSlot[] swappedPositions = CreateStartingSlots("CARD");
            swappedPositions[0] = new LineupPresetSlot("CARD_00", PlayerPosition.Shortstop);
            swappedPositions[4] = new LineupPresetSlot("CARD_04", PlayerPosition.Catcher);
            LineupPresetState preset = CreateValidPreset(startingSlots: swappedPositions);

            LineupPresetValidationResult result = CreateValidator().Validate(
                preset,
                CreateValidationContext(roster));
            LineupPresetValidationIssue catcherIssue = result.Issues.First(issue =>
                issue.Code == LineupPresetValidationIssueCode.OffPositionAssignment &&
                issue.CardId == "CARD_00");

            Assert.That(result.Status, Is.EqualTo(LineupPresetValidationStatus.Valid));
            Assert.That(catcherIssue.Severity, Is.EqualTo(LineupPresetIssueSeverity.Warning));
            Assert.That(catcherIssue.ConditionPenalty, Is.EqualTo(7));
            Assert.That(catcherIssue.FieldingErrorProbabilityMultiplier, Is.EqualTo(1.8d));
        }

        [Test]
        public void LineupPresetValidator_TeamColor와Tactic은현재가용목록으로다시검증한다()
        {
            CurrentRosterState roster = CreateValidRoster("CARD");
            LineupPresetState preset = CreateValidPreset(
                teamColorIds: new[] { "COLOR_A", "REMOVED_COLOR" },
                tacticIds: new[] { "TACTIC_A", "REMOVED_TACTIC" });

            LineupPresetValidationResult result = CreateValidator().Validate(
                preset,
                CreateValidationContext(roster));

            Assert.That(result.Status, Is.EqualTo(LineupPresetValidationStatus.PartiallyValid));
            Assert.That(HasIssue(result, LineupPresetValidationIssueCode.TeamColorUnavailable), Is.True);
            Assert.That(HasIssue(result, LineupPresetValidationIssueCode.TacticCardUnavailable), Is.True);

            LineupPresetState duplicateColor = CreateValidPreset(
                teamColorIds: new[] { "COLOR_A", "COLOR_A" });
            LineupPresetValidationResult duplicateResult = CreateValidator().Validate(
                duplicateColor,
                CreateValidationContext(roster));
            Assert.That(duplicateResult.Status, Is.EqualTo(LineupPresetValidationStatus.Invalid));
            Assert.That(HasIssue(duplicateResult, LineupPresetValidationIssueCode.DuplicateCard), Is.True);
        }

        [Test]
        public void LineupPresetValidator_저장후Roster변경은새Context로재검증해야한다()
        {
            LineupPresetValidator validator = CreateValidator();
            LineupPresetState savedPreset = CreateValidPreset();
            CurrentRosterState originalRoster = CreateValidRoster("CARD");
            LineupPresetValidationResult original = validator.Validate(
                savedPreset,
                CreateValidationContext(originalRoster));
            Assert.That(original.Status, Is.EqualTo(LineupPresetValidationStatus.Valid));

            CurrentRosterState changedRoster = CreateValidRoster("NEW_CARD");
            LineupPresetValidationResult revalidated = validator.Validate(
                savedPreset,
                CreateValidationContext(changedRoster));

            Assert.That(revalidated.Status, Is.EqualTo(LineupPresetValidationStatus.PartiallyValid));
            Assert.That(HasIssue(revalidated, LineupPresetValidationIssueCode.CardNotOnActiveRoster), Is.True);
            Assert.That(
                () => new PreGamePlanSnapshot(78, TeamSeasonKey, savedPreset, revalidated),
                Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void LineupPresetValidator_수비포지션중복과누락은Invalid이다()
        {
            CurrentRosterState roster = CreateValidRoster("CARD");
            LineupPresetSlot[] duplicatePosition = CreateStartingSlots("CARD");
            duplicatePosition[0] = new LineupPresetSlot("CARD_00", PlayerPosition.FirstBase);
            LineupPresetState preset = CreateValidPreset(startingSlots: duplicatePosition);

            LineupPresetValidationResult result = CreateValidator().Validate(
                preset,
                CreateValidationContext(roster));

            Assert.That(
                result.Status,
                Is.EqualTo(LineupPresetValidationStatus.Invalid),
                "Catcher 누락과 FirstBase 중복인 라인업은 비주포지션 Warning만으로 경기 시작할 수 없어야 합니다.");
            Assert.That(
                HasIssue(result, LineupPresetValidationIssueCode.DuplicateDefensivePosition),
                Is.True);
            Assert.That(
                HasIssue(result, LineupPresetValidationIssueCode.MissingDefensivePosition),
                Is.True);
        }

        [Test]
        public void PreGamePlanSnapshot_임의Valid결과로수비포지션검증을우회할수없다()
        {
            LineupPresetSlot[] duplicatePosition = CreateStartingSlots("CARD");
            duplicatePosition[0] = new LineupPresetSlot("CARD_00", PlayerPosition.FirstBase);
            LineupPresetState invalidPreset = CreateValidPreset(startingSlots: duplicatePosition);
            var forgedValidation = new LineupPresetValidationResult(
                invalidPreset.PresetId,
                Array.Empty<LineupPresetValidationIssue>());

            Assert.That(forgedValidation.CanStartGame, Is.True, "Validator를 거치지 않은 임의 결과임을 확인합니다.");
            Assert.That(
                () => new PreGamePlanSnapshot(79, TeamSeasonKey, invalidPreset, forgedValidation),
                Throws.TypeOf<InvalidOperationException>(),
                "Snapshot은 호출자가 만든 Valid 결과만 신뢰하지 않고 최종 수비 포지션 집합을 방어해야 합니다.");
        }

        private static ScoutingConfidenceResolver CreateConfidenceResolver()
        {
            return new ScoutingConfidenceResolver(
                new ScoutingConfidenceDefinition(0.20d, 0.50d, 0.80d, 0.90d, 1.50d));
        }

        private static OpponentScoutingReportBuilder CreateReportBuilder()
        {
            ScoutingConfidenceResolver confidence = CreateConfidenceResolver();
            return new OpponentScoutingReportBuilder(
                confidence,
                new ProbableStarterResolver(confidence),
                new ExpectedLineupEstimator(confidence),
                new BullpenReadinessResolver(
                    new BullpenReadinessDefinition(10, 30, 60, 2),
                    confidence));
        }

        private static OpponentScoutingReportEvidence CreateUnknownReportEvidence()
        {
            return new OpponentScoutingReportEvidence(
                77,
                "WOLVES_2026",
                new DateTime(2026, 5, 1),
                Array.Empty<ProbableStarterCandidateEvidence>(),
                Array.Empty<ExpectedLineupCandidateEvidence>(),
                Array.Empty<BullpenReadinessEvidence>(),
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

        private static ProbableStarterCandidateEvidence CreateStarterEvidence(
            string cardId,
            int turnDistance,
            int daysSinceLastStart,
            int recentStartCount,
            bool isPubliclyAvailable = true)
        {
            return new ProbableStarterCandidateEvidence(
                cardId,
                string.Concat("PERSON_", cardId),
                Handedness.Right,
                turnDistance,
                daysSinceLastStart,
                recentStartCount,
                isPubliclyAvailable,
                new ScoutingEvidenceStrength(true, false, 1d, 0.9d, 0.9d),
                new[] { "completed-rotation" });
        }

        private static ExpectedLineupCandidateEvidence[] CreateLineupEvidence(int count)
        {
            var result = new ExpectedLineupCandidateEvidence[count];
            for (int index = 0; index < count; index++)
            {
                int recentStarts = 20 - index;
                result[index] = new ExpectedLineupCandidateEvidence(
                    $"HITTER_{index:D2}",
                    $"PERSON_HITTER_{index:D2}",
                    (PlayerPosition)(index % ActiveRosterCompositionRule.StartingHitterCount + 1),
                    recentStarts,
                    Math.Max(0, recentStarts - index % 3),
                    Math.Max(0, recentStarts - (index + 1) % 3),
                    index % 4,
                    index % ActiveRosterCompositionRule.StartingHitterCount + 1d,
                    true,
                    new ScoutingEvidenceStrength(true, false, 1d, 0.9d, 0.9d),
                    new[] { "completed-lineup" });
            }
            return result;
        }

        private static BullpenReadinessEvidence CreateBullpenEvidence(
            string cardId,
            ActiveRosterRole role,
            int recentPitchCount,
            int restDays,
            bool isPubliclyAvailable)
        {
            return new BullpenReadinessEvidence(
                cardId,
                string.Concat("PERSON_", cardId),
                role,
                recentPitchCount,
                restDays,
                isPubliclyAvailable,
                new ScoutingEvidenceStrength(true, false, 1d, 0.9d, 0.9d),
                new[] { "completed-workload" });
        }

        private static LineupPresetValidator CreateValidator()
        {
            return new LineupPresetValidator();
        }

        private static LineupPresetValidationContext CreateValidationContext(
            CurrentRosterState roster,
            string unavailableCardId = null)
        {
            return new LineupPresetValidationContext(
                roster,
                CreatePlayerContexts(roster, unavailableCardId),
                new PositionAssignmentRule(
                    new OffPositionPenaltyDefinition(7, 1.8d),
                    new PitcherRoleMismatchPenaltyDefinition(9)),
                new[] { "COLOR_A", "COLOR_B" },
                new[] { "TACTIC_A", "TACTIC_B" });
        }

        private static CurrentRosterState CreateValidRoster(string cardPrefix)
        {
            ActiveRosterRole[] roles = CreateRosterRoles();
            var entries = new ActiveRosterEntry[roles.Length];
            for (int index = 0; index < roles.Length; index++)
            {
                entries[index] = new ActiveRosterEntry(
                    $"{cardPrefix}_{index:D2}",
                    $"SEASON_{cardPrefix}_{index:D2}",
                    $"PERSON_{cardPrefix}_{index:D2}",
                    RegistrationType.Domestic,
                    roles[index]);
            }
            return new CurrentRosterState(TeamSeasonKey, entries);
        }

        private static ActiveRosterRole[] CreateRosterRoles()
        {
            return new[]
            {
                ActiveRosterRole.StartingCatcher,
                ActiveRosterRole.StartingFirstBase,
                ActiveRosterRole.StartingSecondBase,
                ActiveRosterRole.StartingThirdBase,
                ActiveRosterRole.StartingShortstop,
                ActiveRosterRole.StartingLeftField,
                ActiveRosterRole.StartingCenterField,
                ActiveRosterRole.StartingRightField,
                ActiveRosterRole.StartingDesignatedHitter,
                ActiveRosterRole.BenchHitter,
                ActiveRosterRole.BenchHitter,
                ActiveRosterRole.BenchHitter,
                ActiveRosterRole.BenchHitter,
                ActiveRosterRole.BenchHitter,
                ActiveRosterRole.StartingPitcher1,
                ActiveRosterRole.StartingPitcher2,
                ActiveRosterRole.StartingPitcher3,
                ActiveRosterRole.StartingPitcher4,
                ActiveRosterRole.StartingPitcher5,
                ActiveRosterRole.Bullpen1,
                ActiveRosterRole.Bullpen2,
                ActiveRosterRole.Bullpen3,
                ActiveRosterRole.Bullpen4,
                ActiveRosterRole.Setup,
                ActiveRosterRole.Closer
            };
        }

        private static LineupPresetPlayerContext[] CreatePlayerContexts(
            CurrentRosterState roster,
            string unavailableCardId)
        {
            var result = new LineupPresetPlayerContext[roster.Entries.Count];
            for (int index = 0; index < roster.Entries.Count; index++)
            {
                ActiveRosterEntry entry = roster.Entries[index];
                PlayerPosition position = ResolveNaturalPosition(entry.Role);
                PitcherRole? pitcherRole = ResolveNaturalPitcherRole(entry.Role);
                result[index] = new LineupPresetPlayerContext(
                    entry.CardId,
                    position,
                    pitcherRole,
                    pitcherRole.HasValue ? PitcherRoleConfidence.High : (PitcherRoleConfidence?)null,
                    !string.Equals(entry.CardId, unavailableCardId, StringComparison.Ordinal));
            }
            return result;
        }

        private static PlayerPosition ResolveNaturalPosition(ActiveRosterRole role)
        {
            if (role >= ActiveRosterRole.StartingCatcher && role <= ActiveRosterRole.StartingDesignatedHitter)
                return (PlayerPosition)((int)role + 1);
            if (role == ActiveRosterRole.BenchHitter)
                return PlayerPosition.FirstBase;
            if (role >= ActiveRosterRole.StartingPitcher1 && role <= ActiveRosterRole.StartingPitcher5)
                return PlayerPosition.StartingPitcher;
            return PlayerPosition.ReliefPitcher;
        }

        private static PitcherRole? ResolveNaturalPitcherRole(ActiveRosterRole role)
        {
            if (role >= ActiveRosterRole.StartingPitcher1 && role <= ActiveRosterRole.StartingPitcher5)
                return PitcherRole.Starter;
            if (role >= ActiveRosterRole.Bullpen1 && role <= ActiveRosterRole.Bullpen4)
                return PitcherRole.MiddleRelief;
            if (role == ActiveRosterRole.Setup)
                return PitcherRole.Setup;
            if (role == ActiveRosterRole.Closer)
                return PitcherRole.Closer;
            return null;
        }

        private static LineupPresetState CreateValidPreset(
            IReadOnlyList<LineupPresetSlot> startingSlots = null,
            IReadOnlyList<string> teamColorIds = null,
            IReadOnlyList<string> tacticIds = null)
        {
            return new LineupPresetState(
                "PRESET_DEFAULT",
                "기본",
                startingSlots ?? CreateStartingSlots("CARD"),
                CreateIds("CARD", 0, 9),
                CreateIds("CARD", 9, 5),
                CreateIds("CARD", 14, 5),
                CreateIds("CARD", 19, 4),
                "CARD_23",
                "CARD_24",
                teamColorIds ?? new[] { "COLOR_A", "COLOR_B" },
                tacticIds ?? new[] { "TACTIC_A", "TACTIC_B" });
        }

        private static LineupPresetState CreatePresetWithStaleStartingCard()
        {
            LineupPresetSlot[] slots = CreateStartingSlots("CARD");
            slots[0] = new LineupPresetSlot("STALE_CARD", PlayerPosition.Catcher);
            string[] batting = CreateIds("CARD", 0, 9);
            batting[0] = "STALE_CARD";
            return new LineupPresetState(
                "PRESET_STALE",
                "오래된 프리셋",
                slots,
                batting,
                CreateIds("CARD", 9, 5),
                CreateIds("CARD", 14, 5),
                CreateIds("CARD", 19, 4),
                "CARD_23",
                "CARD_24",
                new[] { "COLOR_A", "COLOR_B" },
                new[] { "TACTIC_A", "TACTIC_B" });
        }

        private static LineupPresetSlot[] CreateStartingSlots(string cardPrefix)
        {
            var result = new LineupPresetSlot[ActiveRosterCompositionRule.StartingHitterCount];
            for (int index = 0; index < result.Length; index++)
                result[index] = new LineupPresetSlot($"{cardPrefix}_{index:D2}", (PlayerPosition)(index + 1));
            return result;
        }

        private static string[] CreateIds(string prefix, int startIndex, int count)
        {
            var result = new string[count];
            for (int index = 0; index < count; index++)
                result[index] = $"{prefix}_{startIndex + index:D2}";
            return result;
        }

        private static bool HasIssue(
            LineupPresetValidationResult result,
            LineupPresetValidationIssueCode code,
            string cardId = null)
        {
            return result.Issues.Any(issue =>
                issue.Code == code &&
                (cardId == null || string.Equals(issue.CardId, cardId, StringComparison.Ordinal)));
        }
    }
}
