using System;
using System.Linq;
using System.Reflection;
using Baseball.Presentation.Player;
using Baseball.Presentation.SharedUI;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Presentation.Player
{
    /// <summary>
    /// 선수 모드 Navigation이 팀 편집과 카드 경제 권한을 노출하지 않는지 검증한다.
    /// </summary>
    public sealed class PlayerCareerUiProfileTests
    {
        [Test]
        public void Create_Player전용권한만제공한다()
        {
            GameModeUiProfile profile = PlayerCareerUiProfileFactory.Create();

            Assert.That(profile.Mode, Is.EqualTo(UiGameMode.PlayerCareer));
            Assert.That(profile.DisplayName, Is.EqualTo("선수 모드"));
            Assert.That(profile.Capabilities.Has(UiCapability.CanViewCareerPlayerGrowth), Is.True);
            Assert.That(profile.Capabilities.Has(UiCapability.CanPlayPlayerMiniGame), Is.True);
            Assert.That(profile.Capabilities.Has(UiCapability.CanViewManagerDecisionReason), Is.False);
            Assert.That(profile.Capabilities.Has(UiCapability.CanEditActiveRoster), Is.False);
            Assert.That(profile.Capabilities.Has(UiCapability.CanEditLineup), Is.False);
            Assert.That(profile.Capabilities.Has(UiCapability.CanEquipTeamColor), Is.False);
            Assert.That(profile.Capabilities.Has(UiCapability.CanEquipTacticCards), Is.False);
            Assert.That(profile.Capabilities.Has(UiCapability.CanUseScout), Is.False);
            Assert.That(profile.Capabilities.Has(UiCapability.CanTrainOwnedCards), Is.False);
            Assert.That(profile.Capabilities.Has(UiCapability.CanManageFinance), Is.False);
        }

        [Test]
        public void Create_여섯개업무영역과LocalNavigation을제공한다()
        {
            GameModeUiProfile profile = PlayerCareerUiProfileFactory.Create();

            Assert.That(
                profile.Navigation.Entries.Select(entry => entry.DisplayName),
                Is.EqualTo(new[] { "홈", "경기", "선수", "팀", "리그", "커리어" }));
            Assert.That(
                profile.Navigation.FindEntry(PlayerCareerRoutes.Game).Children
                    .Select(entry => entry.DisplayName),
                Is.EqualTo(new[] { "다음 경기", "일정", "경기 결과" }));
            Assert.That(
                profile.Navigation.FindEntry(PlayerCareerRoutes.Player).Children
                    .Select(entry => entry.DisplayName),
                Is.EqualTo(new[] { "선수 정보", "능력치", "성장", "스킬" }));
            Assert.That(
                profile.Navigation.FindEntry(PlayerCareerRoutes.CareerHub).Children
                    .Select(entry => entry.DisplayName),
                Is.EqualTo(new[] { "계약", "시즌 기록", "통산 기록", "수상·하이라이트" }));
        }

        [Test]
        public void Create_팀LocalNavigation은읽기전용이고OwnerRoute를노출하지않는다()
        {
            GameModeUiProfile profile = PlayerCareerUiProfileFactory.Create();
            NavigationEntry team = profile.Navigation.FindEntry(PlayerCareerRoutes.TeamHub);

            Assert.That(team, Is.Not.Null);
            Assert.That(team.RequiredCapability, Is.EqualTo(UiCapability.None));
            Assert.That(
                team.Children.Select(entry => entry.DisplayName),
                Is.EqualTo(new[] { "선수단", "선발 라인업", "투수진", "팀 정보" }));
            Assert.That(team.Children.All(entry => entry.RequiredCapability == UiCapability.None), Is.True);
            Assert.That(profile.Navigation.FindEntry("Owner.Roster"), Is.Null);
            Assert.That(profile.Navigation.FindEntry("Owner.Scout"), Is.Null);
        }

        [TestCase(PlayerCareerRoutes.Match, PlayerCareerRoutes.NextMatch)]
        [TestCase(PlayerCareerRoutes.MatchRole, PlayerCareerRoutes.NextMatch)]
        [TestCase(PlayerCareerRoutes.SeasonStatistics, PlayerCareerRoutes.Records)]
        [TestCase(PlayerCareerRoutes.ManagerDecision, PlayerCareerRoutes.TeamLineup)]
        [TestCase(PlayerCareerRoutes.Career, PlayerCareerRoutes.Contract)]
        public void Create_기존Route를CanonicalLeaf로변환한다(string oldRoute, string expectedRoute)
        {
            GameModeUiProfile profile = PlayerCareerUiProfileFactory.Create();

            Assert.That(profile.ResolveRouteId(oldRoute), Is.EqualTo(expectedRoute));
            Assert.That(profile.FindEntry(oldRoute).RouteId, Is.EqualTo(expectedRoute));
        }

        [Test]
        public void NavigationState_Primary진입시첫Local을열고마지막선택을복원한다()
        {
            GameModeUiProfile profile = PlayerCareerUiProfileFactory.Create();
            var navigation = new GameModeNavigationState(profile, PlayerCareerRoutes.Home);

            Assert.That(navigation.Navigate(PlayerCareerRoutes.Player), Is.EqualTo(PlayerCareerRoutes.Profile));
            Assert.That(navigation.Navigate(PlayerCareerRoutes.Growth), Is.EqualTo(PlayerCareerRoutes.Growth));
            Assert.That(navigation.Navigate(PlayerCareerRoutes.Home), Is.EqualTo(PlayerCareerRoutes.Home));
            Assert.That(navigation.Navigate(PlayerCareerRoutes.Player), Is.EqualTo(PlayerCareerRoutes.Growth));
        }

        [Test]
        public void PlayerPresentation공개Api_Owner전용State를노출하지않는다()
        {
            string[] forbiddenTerms =
            {
                "OwnedPlayerCardState",
                "ManagerHistoricalRuntimeState",
                "CardTraining",
                "Enhancement",
                "Scout",
                "TeamColor"
            };
            Type[] playerTypes = typeof(PlayerCareerUiProfileFactory).Assembly.GetTypes()
                .Where(type => type.Namespace == "Baseball.Presentation.Player")
                .ToArray();

            foreach (Type type in playerTypes)
            {
                MemberInfo[] publicMembers = type.GetMembers(
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public);
                foreach (MemberInfo member in publicMembers)
                {
                    Assert.That(
                        forbiddenTerms.Any(term => member.Name.Contains(term)),
                        Is.False,
                        $"{type.FullName}.{member.Name}");
                }

                Type[] exposedTypes = type.GetProperties()
                    .Select(property => property.PropertyType)
                    .Concat(type.GetFields().Select(field => field.FieldType))
                    .Concat(type.GetMethods().SelectMany(method => method.GetParameters())
                        .Select(parameter => parameter.ParameterType))
                    .ToArray();
                foreach (Type exposedType in exposedTypes)
                {
                    Assert.That(
                        forbiddenTerms.Any(term =>
                            (exposedType.FullName ?? exposedType.Name).Contains(term)),
                        Is.False,
                        $"{type.FullName} -> {exposedType.FullName}");
                }
            }
        }
    }
}
