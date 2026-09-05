using System;
using System.Reflection;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Career;
using Baseball.Presentation.Player;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Presentation.Player
{
    /// <summary>
    /// 실제 Career View가 선수 Home 모델로 손실 없이 투영되고 미제공 값은 꾸며지지 않는지 검증한다.
    /// </summary>
    public sealed class PlayerHomePresentationModelBuilderTests
    {
        [Test]
        public void Build_Dashboard성장계약의실제값을투영한다()
        {
            CareerDashboardView dashboard = CreateDashboard();
            CareerGrowthView growth = CreateGrowth();
            CareerContractView contract = CreateContract();

            PlayerHomePresentationModel model = new PlayerHomePresentationModelBuilder()
                .Build(dashboard, growth, contract);

            Assert.That(model.Identity.PlayerName, Is.EqualTo("김가람"));
            Assert.That(model.Identity.TeamName, Is.EqualTo("서울 웨이브"));
            Assert.That(model.Identity.Position, Is.EqualTo(PlayerPosition.Shortstop));
            Assert.That(model.Identity.SeasonYear, Is.EqualTo(2028));
            Assert.That(model.Condition, Is.EqualTo(76));
            Assert.That(model.Fatigue, Is.Null, "Dashboard에 없는 피로 수치를 UI가 만들어서는 안 됩니다.");
            Assert.That(model.AvailableMoney, Is.EqualTo(123_000_000L));
            Assert.That(model.Usage.ExpectedRole, Is.EqualTo(ExpectedRole.StartingCompetition));
            Assert.That(model.Usage.PlannedGameRole, Is.EqualTo(PlayerGameRole.StartingBatter));
            Assert.That(model.Usage.BattingOrder, Is.EqualTo(5));
            Assert.That(model.Usage.DecisionReasonCode, Is.EqualTo(DecisionReasonCode.RecentPerformance));
            Assert.That(model.NextMatch.OpponentName, Is.EqualTo("부산 앵커스"));
            Assert.That(model.RecentGames.Count, Is.EqualTo(1));
            Assert.That(model.RecentGames[0].Hits, Is.EqualTo(2));
            Assert.That(model.Growth.IsAvailable, Is.True);
            Assert.That(model.Growth.ActiveProgramId, Is.EqualTo("training_contact"));
            Assert.That(model.Contract.IsAvailable, Is.True);
            Assert.That(model.Contract.RemainingSeasons, Is.EqualTo(2));
            Assert.That(model.Contract.CanBeginNegotiation, Is.True);
        }

        [Test]
        public void Build_선택View가없으면Growth와Contract를가용하다고표시하지않는다()
        {
            PlayerHomePresentationModel model = new PlayerHomePresentationModelBuilder()
                .Build(CreateDashboard());

            Assert.That(model.Growth.IsAvailable, Is.False);
            Assert.That(model.Contract.IsAvailable, Is.False);
            Assert.That(model.Usage.HasManagerDecisionReason, Is.False);
        }

        private static CareerDashboardView CreateDashboard()
        {
            var dashboard = new CareerDashboardView();
            Set(dashboard, nameof(CareerDashboardView.PlayerName), "김가람");
            Set(dashboard, nameof(CareerDashboardView.Age), 23);
            Set(dashboard, nameof(CareerDashboardView.Position), PlayerPosition.Shortstop);
            Set(dashboard, nameof(CareerDashboardView.Overall), 72);
            Set(dashboard, nameof(CareerDashboardView.Condition), 76);
            Set(dashboard, nameof(CareerDashboardView.ManagerEvaluation), 81);
            Set(dashboard, nameof(CareerDashboardView.ExpectedRole), ExpectedRole.StartingCompetition);
            Set(dashboard, nameof(CareerDashboardView.TeamName), "서울 웨이브");
            Set(dashboard, nameof(CareerDashboardView.TeamEmblemId), 12);
            Set(dashboard, nameof(CareerDashboardView.SeasonYear), 2028);
            Set(dashboard, nameof(CareerDashboardView.LeagueLevel), LeagueLevel.Major);
            Set(dashboard, nameof(CareerDashboardView.SeasonPhase), SeasonPhase.RegularSeason);
            Set(dashboard, nameof(CareerDashboardView.AvailableMoney), 123_000_000L);
            Set(dashboard, nameof(CareerDashboardView.TeamRank), 3);
            Set(dashboard, nameof(CareerDashboardView.TeamWins), 18);
            Set(dashboard, nameof(CareerDashboardView.TeamLosses), 12);
            Set(dashboard, nameof(CareerDashboardView.TeamTies), 1);
            Set(
                dashboard,
                nameof(CareerDashboardView.NextGame),
                (NextCareerGameView?)new NextCareerGameView(
                    44,
                    new DateTime(2028, 5, 14),
                    "부산 앵커스",
                    "서울 웨이브",
                    "부산 앵커스",
                    true,
                    PlayerGameRole.StartingBatter,
                    5));
            Set(
                dashboard,
                nameof(CareerDashboardView.RecentGames),
                new[]
                {
                    new PlayerGameLogState(
                        43,
                        2,
                        true,
                        true,
                        5,
                        3,
                        PlayerGameRole.StartingBatter,
                        4,
                        2,
                        1,
                        3,
                        0,
                        0,
                        0,
                        0,
                        1,
                        0,
                        0)
                });
            return dashboard;
        }

        private static CareerGrowthView CreateGrowth()
        {
            var growth = new CareerGrowthView();
            Set(growth, nameof(CareerGrowthView.IsOffseason), false);
            Set(growth, nameof(CareerGrowthView.CanEditBoard), false);
            Set(growth, nameof(CareerGrowthView.IsActivityInProgress), true);
            Set(growth, nameof(CareerGrowthView.ActiveProgramId), "training_contact");
            Set(growth, nameof(CareerGrowthView.CurrentWeek), 3);
            Set(growth, nameof(CareerGrowthView.TotalWeeks), 8);
            Set(growth, nameof(CareerGrowthView.RemainingWeeks), 5);
            Set(growth, nameof(CareerGrowthView.CurrentRole), ExpectedRole.StartingCompetition);
            Set(growth, nameof(CareerGrowthView.RoleScore), 74d);
            Set(growth, nameof(CareerGrowthView.CompetitorRoleScore), 69d);
            Set(
                growth,
                nameof(CareerGrowthView.RoleExplanation),
                new DecisionExplanation(
                    DecisionType.ManagerRole,
                    DecisionReasonCode.RecentPerformance,
                    Array.Empty<DecisionFactor>(),
                    Array.Empty<double>(),
                    Array.Empty<RecommendedActionCode>(),
                    1));
            return growth;
        }

        private static CareerContractView CreateContract()
        {
            var contract = new CareerContractView();
            Set(
                contract,
                nameof(CareerContractView.CurrentContract),
                new CurrentContractView(
                    "서울 웨이브",
                    2027,
                    2029,
                    3,
                    2,
                    20_000_000L,
                    50_000_000L,
                    170_000_000L,
                    ExpectedRole.StartingCompetition));
            Set(
                contract,
                nameof(CareerContractView.NegotiationStatus),
                ContractNegotiationStatus.NegotiationAvailable);
            Set(contract, nameof(CareerContractView.CanBeginNegotiation), true);
            return contract;
        }

        private static void Set<T>(object target, string propertyName, T value)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, propertyName);
            MethodInfo setter = property.GetSetMethod(true);
            Assert.That(setter, Is.Not.Null, propertyName);
            setter.Invoke(target, new object[] { value });
        }
    }
}
