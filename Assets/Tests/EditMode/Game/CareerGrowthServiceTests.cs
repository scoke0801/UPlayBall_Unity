using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Game.Career;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game
{
    /// <summary>
    /// 시즌 결산과 오프시즌 활동이 세이브 루트에서 한 번만 결정론적으로 진행되는지 검증한다.
    /// </summary>
    public sealed class CareerGrowthServiceTests
    {
        [Test]
        public void SettleSeason_성장노쇠수입과12주오프시즌을연결한다()
        {
            NewGameFlow flow = CreateRegularSeasonCareer(424242UL);
            flow.Career.League.CurrentSeason.CompleteRegularSeason();
            long moneyBefore = flow.Career.AvailableMoney;
            long salary = flow.Career.CurrentContract.AnnualSalary;
            var service = new CareerGrowthService(flow.Career, NewGameConfiguration.CreateDefault().Balance);

            SeasonGrowthSettlementResult settlement = service.SettleSeasonAndBeginOffseason(
                CreateBatterUsage(),
                bonusIncome: 300L);

            Assert.That(flow.Career.SaveVersion, Is.EqualTo(5));
            Assert.That(flow.Career.League.CurrentSeason.Phase, Is.EqualTo(SeasonPhase.Offseason));
            Assert.That(flow.Career.CurrentOffseason, Is.SameAs(settlement.Offseason));
            Assert.That(settlement.Offseason.TotalWeeks, Is.EqualTo(12));
            Assert.That(flow.Career.AvailableMoney, Is.EqualTo(moneyBefore + salary + 300L));
            Assert.That(settlement.NaturalDevelopment.SourceType, Is.EqualTo(GrowthSourceType.NaturalDevelopment));
            Assert.That(settlement.Aging.SourceType, Is.EqualTo(GrowthSourceType.Aging));
            Assert.That(flow.Career.MyPlayer.GrowthState.GrowthHistory.Count, Is.EqualTo(2));
            Assert.Throws<System.InvalidOperationException>(() =>
                service.SettleSeasonAndBeginOffseason(CreateBatterUsage()));
        }

        [Test]
        public void OffseasonActivity_커리어Seed로결과를확정하고상태를동기화한다()
        {
            NewGameFlow flow = CreateRegularSeasonCareer(777UL);
            flow.Career.League.CurrentSeason.CompleteRegularSeason();
            var service = new CareerGrowthService(flow.Career, NewGameConfiguration.CreateDefault().Balance);
            service.SettleSeasonAndBeginOffseason(CreateBatterUsage());
            long moneyBefore = flow.Career.AvailableMoney;

            PlannedOffseasonActivity activity = service.PlanActivity("personal_batting", startWeek: 1);
            service.StartActivity(activity.ActivityId);
            GrowthResultRecord result = service.CompleteActivity(activity.ActivityId);

            Assert.That(activity.Status, Is.EqualTo(OffseasonActivityStatus.Completed));
            Assert.That(activity.RandomSeed, Is.Not.EqualTo(0UL));
            Assert.That(result.RandomSeed, Is.EqualTo(activity.RandomSeed));
            Assert.That(flow.Career.AvailableMoney, Is.EqualTo(moneyBefore - 300L));
            Assert.That(flow.Career.CurrentOffseason.CurrentWeek, Is.EqualTo(4));
            Assert.That(flow.Career.MyPlayer.Condition, Is.EqualTo(flow.Career.MyPlayer.GrowthState.Condition));
        }

        [Test]
        public void SettleSeason_같은입력과Seed면완전히같은결과를낸다()
        {
            NewGameFlow first = CreateRegularSeasonCareer(991UL);
            NewGameFlow second = CreateRegularSeasonCareer(991UL);
            first.Career.League.CurrentSeason.CompleteRegularSeason();
            second.Career.League.CurrentSeason.CompleteRegularSeason();

            var firstService = new CareerGrowthService(first.Career, NewGameConfiguration.CreateDefault().Balance);
            var secondService = new CareerGrowthService(second.Career, NewGameConfiguration.CreateDefault().Balance);
            SeasonGrowthSettlementResult firstResult = firstService.SettleSeasonAndBeginOffseason(CreateBatterUsage());
            SeasonGrowthSettlementResult secondResult = secondService.SettleSeasonAndBeginOffseason(CreateBatterUsage());

            Assert.That(secondResult.NaturalDevelopment.RandomSeed,
                Is.EqualTo(firstResult.NaturalDevelopment.RandomSeed));
            Assert.That(secondResult.Aging.RandomSeed, Is.EqualTo(firstResult.Aging.RandomSeed));
            Assert.That(second.Career.MyPlayer.GrowthState.BaseAbilities.ToArray(),
                Is.EqualTo(first.Career.MyPlayer.GrowthState.BaseAbilities.ToArray()));
            Assert.That(second.Career.AvailableMoney, Is.EqualTo(first.Career.AvailableMoney));
        }

        private static NewGameFlow CreateRegularSeasonCareer(ulong seed)
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            var flow = new NewGameFlow(configuration, seed);
            flow.SubmitIdentity("성장 테스트", "대한민국");
            flow.SelectPlayerType(PlayerType.Batter);
            flow.SelectPosition(PlayerPosition.Shortstop);
            flow.SelectHandedness(Handedness.Left, Handedness.Right);
            flow.SubmitBatterAttributes(new BatterAttributes(55, 50, 52, 43, 60, 52));
            flow.GenerateOffers();
            flow.SelectOffer(flow.State.SetupResult.Offers[0].Team.TeamId);
            flow.SignSelectedOffer();
            flow.StartRookieSeason();
            return flow;
        }

        private static SeasonUsageSummary CreateBatterUsage()
        {
            return new SeasonUsageSummary(
                1d,
                new[]
                {
                    new AbilityWeight(PlayerAbility.Contact, 0.5d),
                    new AbilityWeight(PlayerAbility.Defense, 0.3d),
                    new AbilityWeight(PlayerAbility.BatterMental, 0.2d)
                });
        }
    }
}
