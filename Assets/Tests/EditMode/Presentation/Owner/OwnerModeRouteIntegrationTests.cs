using Baseball.Presentation.Owner;
using Baseball.Presentation.SharedUI;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Presentation.Owner
{
    public sealed class OwnerModeRouteIntegrationTests
    {
        [Test]
        public void Profile_실제연결된업무영역과LocalRoute를노출한다()
        {
            GameModeUiProfile profile = OwnerModeUiProfileFactory.Create();

            Assert.That(profile.Capabilities.Has(UiCapability.CanManageFinance), Is.True);
            AssertEnabled(profile, OwnerNavigationRoutes.Home);
            AssertEnabled(profile, OwnerNavigationRoutes.Roster);
            AssertEnabled(profile, OwnerNavigationRoutes.PowerUp);
            AssertEnabled(profile, OwnerNavigationRoutes.Dugout);
            AssertEnabled(profile, OwnerNavigationRoutes.Club);
            AssertEnabled(profile, OwnerNavigationRoutes.League);
            AssertEnabled(profile, OwnerExpansionWorkspaceCoordinator.RosterLineupRouteId);
            AssertEnabled(profile, OwnerExpansionWorkspaceCoordinator.CollectionRouteId);
            AssertEnabled(profile, OwnerManagementRoutes.RosterCondition);
            AssertEnabled(profile, OwnerNavigationRoutes.DugoutLineupNotes);
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
        public void Profile_전력보강은Global업무영역이고미완성기능은Local에서잠근다()
        {
            GameModeUiProfile profile = OwnerModeUiProfileFactory.Create();
            NavigationEntry powerUp = profile.Navigation.FindEntry(OwnerNavigationRoutes.PowerUp);
            NavigationEntry scout = profile.Navigation.FindEntry(OwnerNavigationRoutes.PowerUpScout);

            Assert.That(profile.Capabilities.Has(UiCapability.CanUseScout), Is.False);
            Assert.That(powerUp.IsEnabled, Is.True);
            Assert.That(powerUp.IsVisible(profile.Capabilities), Is.True);
            Assert.That(scout.IsEnabled, Is.False);
            Assert.That(scout.IsVisible(profile.Capabilities), Is.True);
            Assert.That(scout.DisabledReason, Does.Contain("스카우트 후보군"));
            for (int index = 0; index < powerUp.Children.Count; index++)
            {
                Assert.That(powerUp.Children[index].IsEnabled, Is.False, powerUp.Children[index].RouteId);
                Assert.That(powerUp.Children[index].DisabledReason, Is.Not.Empty, powerUp.Children[index].RouteId);
            }
        }

        [Test]
        public void Profile_TeamColor와Tactic슬롯권한은있지만상세Route는비활성이다()
        {
            GameModeUiProfile profile = OwnerModeUiProfileFactory.Create();
            NavigationEntry dugout = profile.Navigation.FindEntry(OwnerNavigationRoutes.Dugout);

            Assert.That(profile.Capabilities.Has(UiCapability.CanEquipTeamColor), Is.True);
            Assert.That(profile.Capabilities.Has(UiCapability.CanEquipTacticCards), Is.True);
            Assert.That(dugout.IsEnabled, Is.True);
            Assert.That(profile.Navigation.FindEntry(OwnerNavigationRoutes.DugoutLineupNotes).IsEnabled, Is.True);
            Assert.That(profile.Navigation.FindEntry(OwnerNavigationRoutes.DugoutTeamColor).DisabledReason,
                Does.Contain("상세 화면"));
            Assert.That(profile.Navigation.FindEntry(OwnerNavigationRoutes.DugoutTactics).DisabledReason,
                Does.Contain("발동 조건"));
            Assert.That(profile.Navigation.FindEntry(OwnerNavigationRoutes.DugoutManagerPolicy).DisabledReason,
                Does.Contain("감독 방침"));
        }

        [Test]
        public void Profile_OldRoute를새업무영역과ContextTarget으로이관한다()
        {
            GameModeUiProfile profile = OwnerModeUiProfileFactory.Create();
            Assert.That(profile.ResolveRouteId("Owner.Roster.Active"), Is.EqualTo(OwnerNavigationRoutes.RosterLineup));
            Assert.That(profile.ResolveRouteId("Owner.Scout.Award"), Is.EqualTo(OwnerNavigationRoutes.PowerUpScout));
            Assert.That(profile.ResolveRouteId("Owner.Development.Training"), Is.EqualTo(OwnerNavigationRoutes.PowerUpTraining));
            Assert.That(profile.ResolveRouteId("Owner.Tactic.Cards"), Is.EqualTo(OwnerNavigationRoutes.DugoutTactics));
            Assert.That(profile.ResolveRouteId(OwnerModeShellCoordinator.MatchRouteId),
                Is.EqualTo(OwnerNavigationRoutes.MatchCenterAnalysis));
            Assert.That(profile.ResolveRouteId(OwnerExpansionWorkspaceCoordinator.PregameRouteId),
                Is.EqualTo(OwnerNavigationRoutes.MatchCenterAnalysis));
        }

        [Test]
        public void NavigationState_MatchCenter종료시Home과Schedule진입점을복원한다()
        {
            GameModeUiProfile profile = OwnerModeUiProfileFactory.Create();
            var state = new GameModeNavigationState(profile, OwnerNavigationRoutes.Home);

            state.OpenContext(OwnerNavigationRoutes.MatchCenterAnalysis);
            state.NavigateContext(OwnerNavigationRoutes.MatchCenterLineup);
            Assert.That(state.TryBack(out string homeOrigin), Is.True);
            Assert.That(homeOrigin, Is.EqualTo(OwnerNavigationRoutes.Home));

            state.Navigate(OwnerSharedInformationWorkspaceCoordinator.ScheduleRouteId);
            state.OpenContext(OwnerNavigationRoutes.MatchCenterAnalysis);
            state.NavigateContext(OwnerNavigationRoutes.MatchCenterCondition);
            Assert.That(state.TryBack(out string scheduleOrigin), Is.True);
            Assert.That(scheduleOrigin, Is.EqualTo(OwnerSharedInformationWorkspaceCoordinator.ScheduleRouteId));
        }

        private static void AssertEnabled(GameModeUiProfile profile, string routeId)
        {
            NavigationEntry entry = profile.FindEntry(routeId);
            Assert.That(entry, Is.Not.Null, routeId);
            Assert.That(entry.IsEnabled, Is.True, routeId);
            Assert.That(entry.IsVisible(profile.Capabilities), Is.True, routeId);
        }
    }
}
