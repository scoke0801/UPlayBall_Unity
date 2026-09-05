using System;
using Baseball.Presentation.Owner;
using Baseball.Presentation.SharedUI;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Presentation.Owner
{
    /// <summary>구단주 UI 권한과 실제 Snapshot 기반 Header 표현을 검증한다.</summary>
    public sealed class OwnerModePresentationTests
    {
        [Test]
        public void Profile_구단주권한만제공하고선수직접입력권한은노출하지않는다()
        {
            GameModeUiProfile profile = OwnerModeUiProfileFactory.Create();

            Assert.That(profile.Mode, Is.EqualTo(UiGameMode.OwnerCareer));
            Assert.That(profile.DisplayName, Is.EqualTo("구단주 모드"));
            Assert.That(profile.Capabilities.Has(UiCapability.CanEditActiveRoster), Is.False);
            Assert.That(profile.Capabilities.Has(UiCapability.CanEditLineup), Is.True);
            Assert.That(profile.Capabilities.Has(UiCapability.CanUseScout), Is.False);
            Assert.That(profile.Capabilities.Has(UiCapability.CanEquipTeamColor), Is.True);
            Assert.That(profile.Capabilities.Has(UiCapability.CanEquipTacticCards), Is.True);
            Assert.That(profile.Capabilities.Has(UiCapability.CanTrainOwnedCards), Is.False);
            Assert.That(profile.Capabilities.Has(UiCapability.CanManageFinance), Is.True);
            Assert.That(profile.Capabilities.Has(UiCapability.CanPlayPlayerMiniGame), Is.False);
            Assert.That(profile.Capabilities.Has(UiCapability.CanViewCareerPlayerGrowth), Is.False);
        }

        [Test]
        public void Profile_ProductionAdapter가연결된상위화면만선택가능하다()
        {
            GameModeUiProfile profile = OwnerModeUiProfileFactory.Create();

            Assert.That(profile.Navigation.FindEntry(OwnerModeShellCoordinator.HomeRouteId).IsEnabled, Is.True);
            Assert.That(profile.Navigation.FindEntry("Owner.Roster").IsEnabled, Is.True);
            Assert.That(profile.Navigation.FindEntry("Owner.Club").IsEnabled, Is.True);
            Assert.That(profile.Navigation.FindEntry("Owner.Match").IsEnabled, Is.True);
            Assert.That(profile.Navigation.FindEntry("Owner.Scout").IsEnabled, Is.False);
            Assert.That(profile.Navigation.FindEntry("Owner.Development").IsEnabled, Is.False);
            Assert.That(profile.Navigation.FindEntry("Owner.Tactic").IsEnabled, Is.False);
            Assert.That(profile.Navigation.FindEntry("Shared.League").IsEnabled, Is.False);
            Assert.That(profile.Navigation.FindEntry("Owner.Roster.Active").IsEnabled, Is.False);
            Assert.That(profile.Navigation.FindEntry(OwnerManagementRoutes.RosterCondition).IsEnabled, Is.True);
        }

        [Test]
        public void Profile_백엔드없는기능은구체적인잠금사유를제공한다()
        {
            GameModeUiProfile profile = OwnerModeUiProfileFactory.Create();

            NavigationEntry contract = profile.Navigation.FindEntry("Owner.Club.Contract");
            NavigationEntry awardScout = profile.Navigation.FindEntry("Owner.Scout.Award");

            Assert.That(contract, Is.Not.Null);
            Assert.That(contract.IsEnabled, Is.False);
            Assert.That(contract.DisabledReason, Does.Contain("Runtime State"));
            Assert.That(awardScout.IsEnabled, Is.False);
            Assert.That(awardScout.DisabledReason, Does.Contain("Runtime"));
        }

        [Test]
        public void HomeBuilder_MoneySpDp와Resolver로확정된Roster상태를Header에표시한다()
        {
            OwnerHomeSnapshot snapshot = CreateSnapshot(isRosterValid: true, validationMessage: string.Empty);

            OwnerHomePresentationModel model = OwnerHomePresentationBuilder.Build(snapshot);

            Assert.That(model.ShellStatus.TeamName, Is.EqualTo("서울 웨이브스"));
            Assert.That(model.ShellStatus.ModeSlots.Count, Is.EqualTo(4));
            Assert.That(model.ShellStatus.ModeSlots[0].SlotId, Is.EqualTo("Money"));
            Assert.That(model.ShellStatus.ModeSlots[0].Value, Is.EqualTo("125만원"));
            Assert.That(model.ShellStatus.ModeSlots[1].SlotId, Is.EqualTo("SP"));
            Assert.That(model.ShellStatus.ModeSlots[2].SlotId, Is.EqualTo("DP"));
            Assert.That(model.RosterCountText, Is.EqualTo("현재 1군 25/25"));
            Assert.That(model.RosterCompositionText, Is.EqualTo("야수 14/14 · 투수 11/11"));
            Assert.That(model.ForeignPlayerText, Is.EqualTo("외국인 3/3"));
            Assert.That(model.RosterEmphasis, Is.EqualTo(ShellStatusEmphasis.Positive));
        }

        [Test]
        public void Snapshot_유효하지않은Roster에는Resolver사유를요구한다()
        {
            Assert.Throws<ArgumentException>(() => CreateSnapshot(
                isRosterValid: false,
                validationMessage: string.Empty));

            OwnerHomePresentationModel model = OwnerHomePresentationBuilder.Build(CreateSnapshot(
                isRosterValid: false,
                validationMessage: "외국인 등록은 최대 3명입니다."));

            Assert.That(model.RosterEmphasis, Is.EqualTo(ShellStatusEmphasis.Critical));
            Assert.That(model.ShellStatus.ModeSlots[3].Tooltip, Does.Contain("최대 3명"));
        }

        [TestCase(0L, "0원")]
        [TestCase(9_999L, "9,999원")]
        [TestCase(12_345L, "1만 2,345원")]
        [TestCase(125_000_000L, "1억 2,500만원")]
        [TestCase(-120_000L, "-12만원")]
        public void MoneyFormatter_원단위정수를억만원표기로변환한다(long money, string expected)
        {
            Assert.That(OwnerMoneyFormatter.Format(money), Is.EqualTo(expected));
        }

        private static OwnerHomeSnapshot CreateSnapshot(bool isRosterValid, string validationMessage)
        {
            return new OwnerHomeSnapshot(
                "2028 시즌",
                "4월 3주",
                "Rookie",
                "서울 웨이브스",
                "3위",
                "다음 경기 D-1",
                1_250_000,
                420,
                185,
                30,
                25,
                25,
                14,
                14,
                11,
                11,
                3,
                3,
                61,
                isRosterValid,
                validationMessage);
        }
    }
}
