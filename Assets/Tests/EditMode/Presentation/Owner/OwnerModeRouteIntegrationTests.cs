using Baseball.Presentation.Owner;
using Baseball.Presentation.SharedUI;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Presentation.Owner
{
    public sealed class OwnerModeRouteIntegrationTests
    {
        [Test]
        public void Profile_실제연결된확장Route와Finance권한을노출한다()
        {
            GameModeUiProfile profile = OwnerModeUiProfileFactory.Create();

            Assert.That(profile.Capabilities.Has(UiCapability.CanManageFinance), Is.True);
            AssertEnabled(profile, "Owner.Roster");
            AssertEnabled(profile, "Owner.Club");
            AssertEnabled(profile, OwnerModeShellCoordinator.MatchRouteId);
            AssertEnabled(profile, OwnerExpansionWorkspaceCoordinator.RosterLineupRouteId);
            AssertEnabled(profile, OwnerExpansionWorkspaceCoordinator.CollectionRouteId);
            AssertEnabled(profile, OwnerManagementRoutes.RosterCondition);
            AssertEnabled(profile, OwnerExpansionWorkspaceCoordinator.PregameRouteId);
            AssertEnabled(profile, OwnerManagementRoutes.ClubFinance);
            AssertEnabled(profile, OwnerManagementRoutes.ClubFacility);
            AssertEnabled(profile, OwnerExpansionWorkspaceCoordinator.StaffOfficeRouteId);
        }

        [Test]
        public void Profile_Contract와Trade는계속비활성이다()
        {
            GameModeUiProfile profile = OwnerModeUiProfileFactory.Create();

            Assert.That(profile.Navigation.FindEntry("Owner.Club.Contract").IsEnabled, Is.False);
            Assert.That(profile.Navigation.FindEntry("Owner.Club.Trade").IsEnabled, Is.False);
        }

        [Test]
        public void Profile_ScoutGame계약이없으므로권한과모든Route를노출하지않는다()
        {
            GameModeUiProfile profile = OwnerModeUiProfileFactory.Create();
            NavigationEntry scout = profile.Navigation.FindEntry("Owner.Scout");

            Assert.That(profile.Capabilities.Has(UiCapability.CanUseScout), Is.False);
            Assert.That(scout.IsEnabled, Is.False);
            Assert.That(scout.IsVisible(profile.Capabilities), Is.False);
            Assert.That(scout.DisabledReason, Does.Contain("스카우트 후보군"));
            for (int index = 0; index < scout.Children.Count; index++)
            {
                Assert.That(scout.Children[index].IsEnabled, Is.False, scout.Children[index].RouteId);
                Assert.That(scout.Children[index].DisabledReason, Is.Not.Empty, scout.Children[index].RouteId);
            }
        }

        [Test]
        public void Profile_TeamColor와Tactic슬롯권한은있지만상세Route는비활성이다()
        {
            GameModeUiProfile profile = OwnerModeUiProfileFactory.Create();
            NavigationEntry tactic = profile.Navigation.FindEntry("Owner.Tactic");

            Assert.That(profile.Capabilities.Has(UiCapability.CanEquipTeamColor), Is.True);
            Assert.That(profile.Capabilities.Has(UiCapability.CanEquipTacticCards), Is.True);
            Assert.That(tactic.IsEnabled, Is.False);
            Assert.That(tactic.IsVisible(profile.Capabilities), Is.True);
            Assert.That(profile.Navigation.FindEntry("Owner.Tactic.TeamColor").DisabledReason,
                Does.Contain("상세 화면"));
            Assert.That(profile.Navigation.FindEntry("Owner.Tactic.Cards").DisabledReason,
                Does.Contain("발동 조건"));
            Assert.That(profile.Navigation.FindEntry("Owner.Tactic.ManagerPolicy").DisabledReason,
                Does.Contain("감독 방침"));
            for (int index = 0; index < tactic.Children.Count; index++)
                Assert.That(tactic.Children[index].IsEnabled, Is.False, tactic.Children[index].RouteId);
        }

        [Test]
        public void Profile_Card경제QueryPreviewCommand가불완전하므로육성권한과Route를노출하지않는다()
        {
            GameModeUiProfile profile = OwnerModeUiProfileFactory.Create();
            NavigationEntry development = profile.Navigation.FindEntry("Owner.Development");

            Assert.That(profile.Capabilities.Has(UiCapability.CanTrainOwnedCards), Is.False);
            Assert.That(development.IsEnabled, Is.False);
            Assert.That(development.IsVisible(profile.Capabilities), Is.False);
            Assert.That(profile.Navigation.FindEntry("Owner.Development.Training").DisabledReason,
                Does.Contain("훈련 목록"));
            Assert.That(profile.Navigation.FindEntry("Owner.Development.Enhancement").DisabledReason,
                Does.Contain("미리보기"));
            Assert.That(profile.Navigation.FindEntry("Owner.Development.Sale").DisabledReason,
                Does.Contain("가격 미리보기"));
            for (int index = 0; index < development.Children.Count; index++)
                Assert.That(development.Children[index].IsEnabled, Is.False, development.Children[index].RouteId);
        }

        private static void AssertEnabled(GameModeUiProfile profile, string routeId)
        {
            NavigationEntry entry = profile.Navigation.FindEntry(routeId);
            Assert.That(entry, Is.Not.Null, routeId);
            Assert.That(entry.IsEnabled, Is.True, routeId);
            Assert.That(entry.IsVisible(profile.Capabilities), Is.True, routeId);
        }
    }
}
