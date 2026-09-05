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
        public void Create_선수단은읽기전용화면이고미연결SubRoute를노출하지않는다()
        {
            GameModeUiProfile profile = PlayerCareerUiProfileFactory.Create();

            NavigationEntry team = profile.Navigation.FindEntry(PlayerCareerRoutes.Team);
            NavigationEntry match = profile.Navigation.FindEntry(PlayerCareerRoutes.Match);

            Assert.That(team, Is.Not.Null);
            Assert.That(team.RequiredCapability, Is.EqualTo(UiCapability.None));
            Assert.That(team.Children, Is.Empty);
            Assert.That(match.Children, Is.Empty);
            Assert.That(profile.Navigation.FindEntry(PlayerCareerRoutes.ManagerDecision), Is.Null);
            Assert.That(profile.Navigation.FindEntry("Owner.Roster"), Is.Null);
            Assert.That(profile.Navigation.FindEntry("Owner.Scout"), Is.Null);
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
