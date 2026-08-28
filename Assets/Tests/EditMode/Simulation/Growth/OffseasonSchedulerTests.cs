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
            var economy = new CareerEconomyState(MoneyAmount.FromTenThousandWon(10000L));
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
            var economy = new CareerEconomyState(MoneyAmount.FromTenThousandWon(5400L));
            var study = new PlayerStudyState();
            PlayerGrowthState player = CreateBatter();
            PlannedOffseasonActivity activity = scheduler.PlanActivity(
                offseason, economy, player, "japan_batting_camp", 1);

            scheduler.StartActivity(offseason, economy, player, study, activity.ActivityId, 777UL);
            GrowthResultRecord result = scheduler.CompleteActivity(
                offseason, player, study, activity.ActivityId, new Pcg32Random(777UL));

            Assert.That(economy.Money, Is.EqualTo(MoneyAmount.FromTenThousandWon(2200L)));
            Assert.That(result.RandomSeed, Is.EqualTo(777UL));
            Assert.That(result.MoneySpent, Is.EqualTo(MoneyAmount.FromTenThousandWon(3200L)));
            Assert.That(result.AbilityChanges.Length, Is.GreaterThan(0));
            Assert.That(activity.Status, Is.EqualTo(OffseasonActivityStatus.Completed));
            Assert.That(offseason.CurrentWeek, Is.EqualTo(7));
            Assert.That(study.StudyUsedThisOffseason, Is.True);
        }

        [Test]
        public void PlanActivity_컨디션미달은계획과Money를변경하지않는다()
        {
            GrowthBalanceTable balance = GrowthBalanceTable.CreateDefault();
            var scheduler = new OffseasonScheduler(balance);
            var offseason = new OffseasonState(2028, 12, 30);
            var economy = new CareerEconomyState(MoneyAmount.FromTenThousandWon(5400L));
            var study = new PlayerStudyState();
            PlayerGrowthState player = CreateBatter();
            player.ChangeCondition(-60);
            Assert.Throws<InvalidOperationException>(() =>
                scheduler.PlanActivity(offseason, economy, player, "japan_batting_camp", 1));

            Assert.That(offseason.Activities, Is.Empty);
            Assert.That(offseason.CurrentWeek, Is.EqualTo(1));
            Assert.That(offseason.StudyUsed, Is.False);
            Assert.That(study.StudyUsedThisOffseason, Is.False);
            Assert.That(economy.Money, Is.EqualTo(MoneyAmount.FromTenThousandWon(5400L)));
        }

        [Test]
        public void PlanActivity_앞선훈련후컨디션이부족하면회복전까지다음계획을거부한다()
        {
            GrowthBalanceTable balance = GrowthBalanceTable.CreateDefault();
            var scheduler = new OffseasonScheduler(balance);
            var offseason = new OffseasonState(2028, 12, 55);
            var economy = new CareerEconomyState(MoneyAmount.FromTenThousandWon(10000L));
            PlayerGrowthState player = CreateBatter();
            player.ChangeCondition(-35);

            scheduler.PlanActivity(offseason, economy, player, "bat_power_camp", 1);

            Assert.Throws<InvalidOperationException>(() =>
                scheduler.PlanActivity(offseason, economy, player, "japan_batting_camp", 4));

            scheduler.PlanActivity(offseason, economy, player, "rehab_general", 4);
            PlannedOffseasonActivity study = scheduler.PlanActivity(
                offseason,
                economy,
                player,
                "japan_batting_camp",
                6);

            Assert.That(study.StartWeek, Is.EqualTo(6));
            Assert.That(study.EndWeek, Is.EqualTo(11));
        }

        [Test]
        public void CancelActivity_중간계획을삭제하면뒤활동을앞으로당긴다()
        {
            GrowthBalanceTable balance = GrowthBalanceTable.CreateDefault();
            var scheduler = new OffseasonScheduler(balance);
            var offseason = new OffseasonState(2028, 12, 90);
            var economy = new CareerEconomyState(MoneyAmount.FromTenThousandWon(10000L));
            PlayerGrowthState player = CreateBatter();
            PlannedOffseasonActivity training = scheduler.PlanActivity(
                offseason, economy, player, "personal_batting", 1);
            PlannedOffseasonActivity recovery = scheduler.PlanActivity(
                offseason, economy, player, "rehab_general", 4);
            PlannedOffseasonActivity study = scheduler.PlanActivity(
                offseason, economy, player, "japan_batting_camp", 6);

            scheduler.CancelActivity(offseason, recovery.ActivityId);

            Assert.That(training.StartWeek, Is.EqualTo(1));
            Assert.That(recovery.Status, Is.EqualTo(OffseasonActivityStatus.Cancelled));
            Assert.That(study.StartWeek, Is.EqualTo(4));
            Assert.That(study.EndWeek, Is.EqualTo(9));
        }

        [Test]
        public void StartActivity_이전유학사용상태는비용차감전에거부한다()
        {
            GrowthBalanceTable balance = GrowthBalanceTable.CreateDefault();
            var scheduler = new OffseasonScheduler(balance);
            var offseason = new OffseasonState(2028, 12, 90);
            var economy = new CareerEconomyState(MoneyAmount.FromTenThousandWon(5400L));
            var study = new PlayerStudyState();
            study.RecordVisit("japan_batting_camp", 2027);
            PlayerGrowthState player = CreateBatter();
            PlannedOffseasonActivity activity = scheduler.PlanActivity(
                offseason, economy, player, "japan_batting_camp", 1);

            Assert.Throws<InvalidOperationException>(() =>
                scheduler.StartActivity(offseason, economy, player, study, activity.ActivityId, 779UL));

            Assert.That(activity.Status, Is.EqualTo(OffseasonActivityStatus.Planned));
            Assert.That(offseason.CurrentWeek, Is.EqualTo(1));
            Assert.That(offseason.StudyUsed, Is.False);
            Assert.That(economy.Money, Is.EqualTo(MoneyAmount.FromTenThousandWon(5400L)));
            Assert.That(player.GrowthHistory, Is.Empty);
        }

        [Test]
        public void CompleteActivity_집중훈련의실행값과강도를계획부터결과까지보존한다()
        {
            GrowthBalanceTable balance = GrowthBalanceTable.CreateDefault();
            var scheduler = new OffseasonScheduler(balance);
            var offseason = new OffseasonState(2028, 12, 90);
            var economy = new CareerEconomyState(MoneyAmount.FromTenThousandWon(3000L));
            PlayerGrowthState player = CreatePitcher();
            PlannedOffseasonActivity activity = scheduler.PlanActivity(
                offseason,
                economy,
                player,
                "pitch_velocity_camp",
                startWeek: 1,
                intensity: TrainingIntensity.Intensive);

            scheduler.StartActivity(offseason, economy, player, null, activity.ActivityId, 880UL);
            GrowthResultRecord result = scheduler.CompleteActivity(
                offseason,
                player,
                null,
                activity.ActivityId,
                new Pcg32Random(880UL));

            Assert.That(activity.Intensity, Is.EqualTo(TrainingIntensity.Intensive));
            Assert.That(activity.DurationWeeks, Is.EqualTo(2));
            Assert.That(result.Intensity, Is.EqualTo(TrainingIntensity.Intensive));
            Assert.That(result.MoneySpent, Is.EqualTo(MoneyAmount.FromTenThousandWon(900L)));
            Assert.That(result.WeeksSpent, Is.EqualTo(2));
            Assert.That(economy.Money, Is.EqualTo(MoneyAmount.FromTenThousandWon(2100L)));
            Assert.That(offseason.CurrentWeek, Is.EqualTo(3));
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

        private static PlayerGrowthState CreatePitcher()
        {
            return new PlayerGrowthState(
                2,
                20,
                PlayerType.Pitcher,
                new AbilityRatings(58),
                new AbilityRatings(72),
                WorkEthicGrade.Diligent,
                90,
                0,
                70);
        }
    }
}
