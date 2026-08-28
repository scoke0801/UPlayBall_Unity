using System;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Simulation.Growth;
using Baseball.Simulation.Random;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Simulation.Growth
{
    /// <summary>
    /// 12주 시간 예산과 Money·유학 제약을 검증한다.
    /// </summary>
    public sealed class OffseasonSchedulerTests
    {
        [Test]
        public void PlanActivity_겹치는활동과두번째유학을거부한다()
        {
            GrowthBalanceTable balance = GrowthBalanceTable.CreateDefault();
            var scheduler = new OffseasonScheduler(balance);
            var offseason = new OffseasonState(2028, 12, 90);
            var economy = new CareerEconomyState(10000L);
            PlayerGrowthState player = CreateBatter();

            scheduler.PlanActivity(offseason, economy, player, "japan_batting_camp", 1);

            Assert.Throws<InvalidOperationException>(() =>
                scheduler.PlanActivity(offseason, economy, player, "personal_batting", 5));
            Assert.Throws<InvalidOperationException>(() =>
                scheduler.PlanActivity(offseason, economy, player, "usa_power_center", 7));
        }

        [Test]
        public void CompleteActivity_시작할때비용과Seed를확정하고완료결과를기록한다()
        {
            GrowthBalanceTable balance = GrowthBalanceTable.CreateDefault();
            var scheduler = new OffseasonScheduler(balance);
            var offseason = new OffseasonState(2028, 12, 90);
            var economy = new CareerEconomyState(5400L);
            var study = new PlayerStudyState();
            PlayerGrowthState player = CreateBatter();
            PlannedOffseasonActivity activity = scheduler.PlanActivity(
                offseason, economy, player, "japan_batting_camp", 1);

            scheduler.StartActivity(offseason, economy, activity.ActivityId, 777UL);
            GrowthResultRecord result = scheduler.CompleteActivity(
                offseason, player, study, activity.ActivityId, new Pcg32Random(777UL));

            Assert.That(economy.Money, Is.EqualTo(2200L));
            Assert.That(result.RandomSeed, Is.EqualTo(777UL));
            Assert.That(result.MoneySpent, Is.EqualTo(3200L));
            Assert.That(result.AbilityChanges.Length, Is.GreaterThan(0));
            Assert.That(activity.Status, Is.EqualTo(OffseasonActivityStatus.Completed));
            Assert.That(offseason.CurrentWeek, Is.EqualTo(7));
            Assert.That(study.StudyUsedThisOffseason, Is.True);
        }

        private static PlayerGrowthState CreateBatter()
        {
            return new PlayerGrowthState(
                1,
                20,
                PlayerType.Batter,
                new AbilityRatings(58),
                new AbilityRatings(72),
                WorkEthicGrade.Diligent,
                90,
                0,
                70);
        }
    }
}
