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
            AssertEnabled(profile, OwnerNavigationRoutes.RosterTacticCards);
            AssertEnabled(profile, OwnerNavigationRoutes.RosterSupportCards);
            AssertEnabled(profile, OwnerNavigationRoutes.RosterTeamColor);
            AssertEnabled(profile, OwnerNavigationRoutes.DugoutLineupNotes);
            AssertEnabled(profile, OwnerManagementRoutes.ClubFinance);
            AssertEnabled(profile, OwnerManagementRoutes.ClubFacility);
            AssertEnabled(profile, OwnerExpansionWorkspaceCoordinator.StaffOfficeRouteId);
        }

        [Test]
        public void Profile_Contract는권한과ProductionRoute로활성화되고Trade는노출하지않는다()
        {
            GameModeUiProfile profile = OwnerModeUiProfileFactory.Create();

            Assert.That(profile.Capabilities.Has(UiCapability.CanManagePlayerContracts), Is.True);
            AssertEnabled(profile, OwnerNavigationRoutes.ClubContract);
            Assert.That(profile.FindEntry("Owner.Club.Trade"), Is.Null);
        }

        [Test]
        public void Profile_전력보강의다섯업무와필요권한을모두연다()
        {
            GameModeUiProfile profile = OwnerModeUiProfileFactory.Create();
            NavigationEntry powerUp = profile.Navigation.FindEntry(OwnerNavigationRoutes.PowerUp);
            NavigationEntry scout = profile.Navigation.FindEntry(OwnerNavigationRoutes.PowerUpScout);

            Assert.That(profile.Capabilities.Has(UiCapability.CanUseScout), Is.True);
            Assert.That(profile.Capabilities.Has(UiCapability.CanTrainOwnedCards), Is.True);
            Assert.That(powerUp.IsEnabled, Is.True);
            Assert.That(powerUp.IsVisible(profile.Capabilities), Is.True);
            Assert.That(scout.IsEnabled, Is.True);
            Assert.That(scout.IsVisible(profile.Capabilities), Is.True);
            Assert.That(powerUp.Children[2].DisplayName, Is.EqualTo("카드 합성"));
            Assert.That(powerUp.Children.Count, Is.EqualTo(5));
            Assert.That(powerUp.Children[3].RouteId, Is.EqualTo(OwnerNavigationRoutes.PowerUpSkills));
            Assert.That(powerUp.Children[4].RouteId, Is.EqualTo(OwnerNavigationRoutes.PowerUpStudy));
            for (int index = 0; index < powerUp.Children.Count; index++)
            {
                Assert.That(powerUp.Children[index].IsEnabled, Is.True, powerUp.Children[index].RouteId);
                Assert.That(powerUp.Children[index].DisabledReason, Is.Empty, powerUp.Children[index].RouteId);
            }
        }

        [Test]
        public void Profile_선수단은레퍼런스의네SubTab을순서대로노출한다()
        {
            GameModeUiProfile profile = OwnerModeUiProfileFactory.Create();
            NavigationEntry roster = profile.Navigation.FindEntry(OwnerNavigationRoutes.Roster);

            Assert.That(profile.Capabilities.Has(UiCapability.CanEquipTeamColor), Is.True);
            Assert.That(profile.Capabilities.Has(UiCapability.CanEquipTacticCards), Is.True);
            Assert.That(roster.Children.Count, Is.EqualTo(4));
            Assert.That(roster.Children[0].DisplayName, Is.EqualTo("선수 오더"));
            Assert.That(roster.Children[1].DisplayName, Is.EqualTo("작전 카드"));
            Assert.That(roster.Children[2].DisplayName, Is.EqualTo("서포트 카드"));
            Assert.That(roster.Children[3].DisplayName, Is.EqualTo("팀 컬러"));
            for (int index = 0; index < roster.Children.Count; index++)
                Assert.That(roster.Children[index].IsEnabled, Is.True, roster.Children[index].RouteId);
        }

        [Test]
        public void Profile_OldRoute를새업무영역과ContextTarget으로이관한다()
        {
            GameModeUiProfile profile = OwnerModeUiProfileFactory.Create();
            Assert.That(profile.ResolveRouteId("Owner.Roster.Active"), Is.EqualTo(OwnerNavigationRoutes.RosterLineup));
            Assert.That(profile.ResolveRouteId("Owner.Scout.Award"), Is.EqualTo(OwnerNavigationRoutes.PowerUpScout));
            Assert.That(profile.ResolveRouteId("Owner.Development.Training"), Is.EqualTo(OwnerNavigationRoutes.PowerUpTraining));
            Assert.That(profile.ResolveRouteId(OwnerNavigationRoutes.RosterPitching),
                Is.EqualTo(OwnerNavigationRoutes.RosterLineup));
            Assert.That(profile.ResolveRouteId(OwnerNavigationRoutes.RosterCollection),
                Is.EqualTo(OwnerNavigationRoutes.RosterLineup));
            Assert.That(profile.ResolveRouteId("Owner.Tactic.Cards"),
                Is.EqualTo(OwnerNavigationRoutes.RosterTacticCards));
            Assert.That(profile.ResolveRouteId("Owner.Tactic.TeamColor"),
                Is.EqualTo(OwnerNavigationRoutes.RosterTeamColor));
            Assert.That(profile.ResolveRouteId(OwnerModeShellCoordinator.MatchRouteId),
                Is.EqualTo(OwnerNavigationRoutes.MatchCenterAnalysis));
            Assert.That(profile.ResolveRouteId(OwnerExpansionWorkspaceCoordinator.PregameRouteId),
                Is.EqualTo(OwnerNavigationRoutes.MatchCenterAnalysis));
        }

        [Test]
        public void NavigationState_MatchCenter종료시Home과LeagueTab진입점을복원한다()
        {
            GameModeUiProfile profile = OwnerModeUiProfileFactory.Create();
            var state = new GameModeNavigationState(profile, OwnerNavigationRoutes.Home);

            state.OpenContext(OwnerNavigationRoutes.MatchCenterAnalysis);
            state.NavigateContext(OwnerNavigationRoutes.MatchCenterLineup);
            Assert.That(state.TryBack(out string homeOrigin), Is.True);
            Assert.That(homeOrigin, Is.EqualTo(OwnerNavigationRoutes.Home));

            state.Navigate(OwnerNavigationRoutes.LeagueStandings);
            state.OpenContext(OwnerNavigationRoutes.MatchCenterAnalysis);
            state.NavigateContext(OwnerNavigationRoutes.MatchCenterCondition);
            Assert.That(state.TryBack(out string leagueOrigin), Is.True);
            Assert.That(leagueOrigin, Is.EqualTo(OwnerNavigationRoutes.LeagueStandings));
        }

        [Test]
        public void NavigationState_상위메뉴를누르면각메뉴의첫LocalRoute를연다()
        {
            GameModeUiProfile profile = OwnerModeUiProfileFactory.Create();
            var state = new GameModeNavigationState(profile, OwnerNavigationRoutes.Home);

            AssertPrimaryOpensFirstLocal(state, profile, OwnerNavigationRoutes.Roster);
            AssertPrimaryOpensFirstLocal(state, profile, OwnerNavigationRoutes.PowerUp);
            AssertPrimaryOpensFirstLocal(state, profile, OwnerNavigationRoutes.Dugout);
            AssertPrimaryOpensFirstLocal(state, profile, OwnerNavigationRoutes.Club);
            AssertPrimaryOpensFirstLocal(state, profile, OwnerNavigationRoutes.League);
            Assert.That(
                OwnerModeUiProfileFactory.ResolvePrimaryMenuRoute(profile, OwnerNavigationRoutes.Home),
                Is.EqualTo(OwnerNavigationRoutes.Home));
            Assert.That(
                OwnerModeUiProfileFactory.ResolvePrimaryMenuRoute(profile, OwnerNavigationRoutes.Shop),
                Is.EqualTo(OwnerNavigationRoutes.Shop));
        }

        private static void AssertPrimaryOpensFirstLocal(
            GameModeNavigationState state,
            GameModeUiProfile profile,
            string primaryRouteId)
        {
            NavigationEntry primary = profile.Navigation.FindEntry(primaryRouteId);
            state.Navigate(primary.Children[primary.Children.Count - 1].RouteId);

            string destination = OwnerModeUiProfileFactory.ResolvePrimaryMenuRoute(profile, primaryRouteId);
            Assert.That(state.Navigate(destination), Is.EqualTo(primary.Children[0].RouteId));
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
