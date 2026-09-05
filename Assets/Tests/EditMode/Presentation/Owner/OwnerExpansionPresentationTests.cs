using System;
using System.Collections.Generic;
using System.Linq;
using Baseball.Core.Historical;
using Baseball.Presentation.Owner;
using Baseball.Presentation.SharedUI;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Presentation.Owner
{
    /// <summary>경기 준비와 Staff Office가 Resolver 결과만 표현하는지 검증한다.</summary>
    public sealed class OwnerExpansionPresentationTests
    {
        [Test]
        public void PregameBuilder_Unknown정보를공백대신정보부족과확인불가로표시한다()
        {
            OwnerPregamePresentationModel model = OwnerPregamePresentationBuilder.Build(
                CreatePregameSnapshot(CreateValidPresetValidation()));

            Assert.That(model.IntelText, Is.EqualTo("정보 부족"));
            Assert.That(model.ProbableStarterText, Is.EqualTo("확인 불가"));
            Assert.That(model.ExpectedLineup, Is.EqualTo(new[] { "정보 부족" }));
            Assert.That(model.Bullpen, Is.EqualTo(new[] { "정보 부족" }));
            Assert.That(model.ManagerTendencyText, Is.EqualTo("정보 부족"));
            Assert.That(model.CanStartMatch, Is.True);
        }

        [Test]
        public void PregameBuilder_현재Validation오류는선수경고와경기시작불가사유가된다()
        {
            var issue = new LineupPresetValidationIssue(
                LineupPresetValidationIssueCode.CardUnavailable,
                LineupPresetIssueSeverity.Incomplete,
                LineupPresetAssignmentGroup.StartingLineup,
                0,
                "CARD_0",
                "현재 경기에 출전할 수 없습니다.");
            var validation = new LineupPresetValidationResult("preset:default", new[] { issue });

            OwnerPregamePresentationModel model = OwnerPregamePresentationBuilder.Build(
                CreatePregameSnapshot(validation));

            Assert.That(model.CanStartMatch, Is.False);
            Assert.That(model.MatchStartDisabledReason, Does.Contain("출전"));
            Assert.That(model.Lineup[0].WarningText, Does.Contain("출전"));
            Assert.That(model.Presets.Single().StatusText, Is.EqualTo("수정 필요"));
        }

        [Test]
        public void StaffOfficeBuilder_항상5역할을표시하고BaseStat버프가아닌운영효율을표현한다()
        {
            StaffCatalog catalog = CreateStaffCatalog();
            var contract = new StaffContractState("contract:hit", "STAFF_HIT", "TEAM_2026", 2026, 2, 120000L);
            var snapshot = new OwnerStaffOfficeSnapshot(
                UiContentStateModel.Ready,
                catalog,
                new[] { contract },
                new TeamStaffAssignmentState("TEAM_2026", hittingCoachStaffId: "STAFF_HIT"),
                new TeamStaffEffectProfile(1.08d, 1.05d, 1.04d, 1.03d, 0.02d),
                Array.Empty<OwnerStaffMarketOfferSnapshot>());

            OwnerStaffOfficePresentationModel model = OwnerStaffOfficePresentationBuilder.Build(snapshot);

            Assert.That(model.Slots.Count, Is.EqualTo(5));
            Assert.That(model.Slots.Select(slot => slot.Role).Distinct().ToArray(), Has.Length.EqualTo(5));
            Assert.That(model.Slots[0].EffectText, Is.EqualTo("타자 훈련 효율 +8%"));
            Assert.That(model.Slots[0].EffectText, Does.Not.Contain("Contact"));
            Assert.That(model.Slots[0].EffectText, Does.Not.Contain("Power"));
            Assert.That(model.Slots[0].SalaryText, Does.Contain("12만원"));
            Assert.That(model.Slots.Count(slot => slot.IsVacant), Is.EqualTo(4));
        }

        [Test]
        public void StaffOfficeBuilder_시장Service의계약가능상태와사유를재판정하지않는다()
        {
            StaffCatalog catalog = CreateStaffCatalog();
            var offer = new StaffMarketOffer(
                "offer:pitch",
                "STAFF_PITCH",
                "TEAM_2026",
                "2026:mid",
                StaffMarketKind.MidseasonReplacement,
                2,
                230000L,
                23000L);
            var offerSnapshot = new OwnerStaffMarketOfferSnapshot(
                offer,
                false,
                "현재 보유 Money로 교체 비용까지 지불할 수 없습니다.",
                "투수 훈련 효율 +6%");
            var snapshot = new OwnerStaffOfficeSnapshot(
                UiContentStateModel.Ready,
                catalog,
                Array.Empty<StaffContractState>(),
                new TeamStaffAssignmentState("TEAM_2026"),
                TeamStaffEffectProfile.Neutral,
                new[] { offerSnapshot });

            OwnerStaffMarketOfferModel model = OwnerStaffOfficePresentationBuilder.Build(snapshot).Offers.Single();

            Assert.That(model.CanSign, Is.False);
            Assert.That(model.DisabledReason, Does.Contain("교체 비용"));
            Assert.That(model.EffectText, Is.EqualTo("투수 훈련 효율 +6%"));
            Assert.That(model.SigningCostText, Does.Contain("2만 3,000원"));
        }

        [TestCase(IntelState.Confirmed, "확정")]
        [TestCase(IntelState.HighConfidence, "높은 신뢰")]
        [TestCase(IntelState.Estimated, "추정")]
        [TestCase(IntelState.LowConfidence, "낮은 신뢰")]
        [TestCase(IntelState.Unknown, "정보 부족")]
        public void PregameBuilder_IntelState를명시적인한국어상태로표시한다(IntelState state, string expected)
        {
            Assert.That(OwnerPregamePresentationBuilder.FormatIntelState(state), Is.EqualTo(expected));
        }

        private static OwnerPregameSnapshot CreatePregameSnapshot(LineupPresetValidationResult validation)
        {
            var players = new OwnerPregamePlayerSnapshot[9];
            for (int index = 0; index < players.Length; index++)
            {
                players[index] = new OwnerPregamePlayerSnapshot(
                    $"CARD_{index}",
                    $"가상 선수 {index + 1}",
                    $"P{index + 1}",
                    "좋음",
                    index == 0 ? "+1" : "0",
                    index == 0 ? "+1" : string.Empty,
                    "좋음");
            }
            return new OwnerPregameSnapshot(
                UiContentStateModel.Ready,
                "5월 3일 홈 경기",
                "부산 마리너스",
                CreateUnknownReport(),
                new[] { new OwnerPregamePresetSnapshot("preset:default", "기본 라인업", validation) },
                "preset:default",
                players,
                new[] { "기동력 야구", "철벽 수비" },
                new[] { "초반 승부", "불펜 총력전" },
                new Dictionary<string, string>(),
                true);
        }

        private static LineupPresetValidationResult CreateValidPresetValidation()
        {
            return new LineupPresetValidationResult("preset:default", Array.Empty<LineupPresetValidationIssue>());
        }

        private static OpponentScoutingReport CreateUnknownReport()
        {
            return new OpponentScoutingReport(
                101,
                "OPPONENT_2026",
                new DateTime(2026, 5, 3),
                new ReportConfidenceSummary(IntelState.Unknown, 0d, 0),
                ScoutedValue<ProbableStarterProjection>.Unknown(),
                Array.Empty<ScoutedValue<ExpectedLineupEntry>>(),
                Array.Empty<ScoutedValue<BullpenReadinessEntry>>(),
                ScoutedValue<OpponentRecentForm>.Unknown(),
                ScoutedValue<OpponentPerformanceProfile>.Unknown(),
                ScoutedValue<OpponentPerformanceProfile>.Unknown(),
                ScoutedValue<OpponentPerformanceProfile>.Unknown(),
                ScoutedValue<ManagerTendencyEstimate>.Unknown(),
                Array.Empty<ScoutedValue<RecentTacticPatternSummary>>(),
                Array.Empty<ScoutingReportNote>(),
                Array.Empty<ScoutingReportNote>(),
                Array.Empty<ScoutingReportNote>());
        }

        private static StaffCatalog CreateStaffCatalog()
        {
            return new StaffCatalog(new[]
            {
                CreateStaff("STAFF_HIT", "김하람", StaffRole.HittingCoach, StaffSpecialtyTag.ContactTraining),
                CreateStaff("STAFF_PITCH", "이도윤", StaffRole.PitchingCoach, StaffSpecialtyTag.PitchCommand),
                CreateStaff("STAFF_DEV", "박시온", StaffRole.DevelopmentCoach, StaffSpecialtyTag.ProspectDevelopment),
                CreateStaff("STAFF_COND", "최은호", StaffRole.ConditioningCoach, StaffSpecialtyTag.RecoveryPlanning),
                CreateStaff("STAFF_SCOUT", "정해원", StaffRole.ScoutingDirector, StaffSpecialtyTag.DataAnalysis)
            });
        }

        private static StaffDefinition CreateStaff(
            string id,
            string name,
            StaffRole role,
            StaffSpecialtyTag specialty)
        {
            return new StaffDefinition(
                id,
                name,
                role,
                3,
                StaffSalaryBand.Standard,
                StaffContractPreference.Balanced,
                new[] { specialty },
                new[] { StaffPhilosophyTag.PlayerCentered });
        }
    }
}
