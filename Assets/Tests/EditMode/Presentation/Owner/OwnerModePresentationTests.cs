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
        public void Profile_여섯업무영역과ContextMatchCenter를분리한다()
        {
            GameModeUiProfile profile = OwnerModeUiProfileFactory.Create();

            Assert.That(profile.Navigation.Entries.Count, Is.EqualTo(6));
            Assert.That(profile.Navigation.Entries[0].RouteId, Is.EqualTo(OwnerNavigationRoutes.Home));
            Assert.That(profile.Navigation.Entries[1].RouteId, Is.EqualTo(OwnerNavigationRoutes.Roster));
            Assert.That(profile.Navigation.Entries[2].RouteId, Is.EqualTo(OwnerNavigationRoutes.PowerUp));
            Assert.That(profile.Navigation.Entries[3].RouteId, Is.EqualTo(OwnerNavigationRoutes.Dugout));
            Assert.That(profile.Navigation.Entries[4].RouteId, Is.EqualTo(OwnerNavigationRoutes.Club));
            Assert.That(profile.Navigation.Entries[5].RouteId, Is.EqualTo(OwnerNavigationRoutes.League));
            Assert.That(profile.Navigation.FindEntry("Owner.Scout"), Is.Null);
            Assert.That(profile.Navigation.FindEntry("Owner.Development"), Is.Null);
            Assert.That(profile.Navigation.FindEntry("Owner.Tactic"), Is.Null);
            Assert.That(profile.Navigation.FindEntry(OwnerModeShellCoordinator.MatchRouteId), Is.Null);
            Assert.That(profile.ContextNavigation.FindEntry(OwnerNavigationRoutes.MatchCenterAnalysis), Is.Not.Null);
            NavigationEntry spectator = profile.ContextNavigation.FindEntry(OwnerNavigationRoutes.MatchSpectator);
            Assert.That(spectator, Is.Not.Null);
            Assert.That(spectator.Children, Is.Empty);
            Assert.That(profile.BackgroundResourcePath, Is.EqualTo(OwnerUiAssetIds.HomeBackgroundResourcePath));
        }

        [TestCase("COMPOSITE", "상대 구단", "상대 구단")]
        [TestCase("KBO_COMPOSITE_1982", "내 구단", "내 구단")]
        [TestCase("서울 웨이브스", "상대 구단", "서울 웨이브스")]
        public void TeamDisplayName_내부Id를사용자용이름으로보정한다(
            string teamName,
            string fallback,
            string expected)
        {
            Assert.That(
                OwnerModeRuntimeSnapshotFactory.FormatTeamDisplayName(teamName, fallback),
                Is.EqualTo(expected));
        }

        [Test]
        public void Profile_백엔드없는기능은구체적인잠금사유를제공한다()
        {
            GameModeUiProfile profile = OwnerModeUiProfileFactory.Create();

            NavigationEntry contract = profile.FindEntry(OwnerNavigationRoutes.ClubContract);
            NavigationEntry awardScout = profile.FindEntry("Owner.Scout.Award");

            Assert.That(contract, Is.Not.Null);
            Assert.That(contract.IsEnabled, Is.False);
            Assert.That(contract.DisabledReason, Does.Contain("계약 조회"));
            Assert.That(awardScout.IsEnabled, Is.False);
            Assert.That(awardScout.DisabledReason, Does.Contain("스카우트 후보군"));
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
