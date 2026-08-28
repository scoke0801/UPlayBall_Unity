using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Simulation.Growth;
using Baseball.Simulation.Random;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Simulation.Growth
{
    /// <summary>
    /// 수만 커리어 표본에서 나이 곡선과 투자 전략의 분포가 의도대로 나타나는지 검증한다.
    /// </summary>
    public sealed class GrowthSimulationStatisticsTests
    {
        private const int CareersPerStrategy = 5000;

        [Test]
        public void Simulate_만커리어에서성장기체감과장기상한이나타난다()
        {
            GrowthBalanceTable balance = GrowthBalanceTable.CreateDefault();
            CareerTotals personal = SimulateStrategy(balance, useStudy: false, seedOffset: 100000UL);
            CareerTotals study = SimulateStrategy(balance, useStudy: true, seedOffset: 900000UL);

            double personalAge22 = personal.Age22 / CareersPerStrategy;
            double studyAge22 = study.Age22 / CareersPerStrategy;
            double studyAge32 = study.Age32 / CareersPerStrategy;
            double studyAge36 = study.Age36 / CareersPerStrategy;

            TestContext.WriteLine(
                $"Personal Age22 {personalAge22:F2} / Study Age22 {studyAge22:F2} / " +
                $"Study Age32 {studyAge32:F2} / Study Age36 {studyAge36:F2}");

            Assert.That(personalAge22, Is.GreaterThan(50d));
            Assert.That(studyAge22, Is.GreaterThan(personalAge22));
            Assert.That(studyAge32, Is.LessThanOrEqualTo(73d));
            Assert.That(studyAge36, Is.LessThanOrEqualTo(73d));
        }

        private static CareerTotals SimulateStrategy(
            GrowthBalanceTable balance,
            bool useStudy,
            ulong seedOffset)
        {
            var totals = new CareerTotals();
            var growthResolver = new GrowthResolver(balance);
            var naturalResolver = new NaturalDevelopmentResolver(balance);
            var agingResolver = new AgingResolver(balance);
            TrainingProgramDefinition program = balance.FindProgram(
                useStudy ? "japan_batting_camp" : "personal_batting");
            var usage = new SeasonUsageSummary(
                1d,
                new[]
                {
                    new AbilityWeight(PlayerAbility.Contact, 0.50d),
                    new AbilityWeight(PlayerAbility.Defense, 0.25d),
                    new AbilityWeight(PlayerAbility.BatterMental, 0.25d)
                });

            for (int career = 0; career < CareersPerStrategy; career++)
            {
                var player = new PlayerGrowthState(
                    career + 1,
                    18,
                    PlayerType.Batter,
                    new AbilityRatings(50),
                    new AbilityRatings(70),
                    WorkEthicGrade.Normal,
                    90,
                    0,
                    70);

                for (int season = 0; season < 20; season++)
                {
                    int year = 2028 + season;
                    ulong baseSeed = seedOffset + (ulong)(career * 100 + season * 3);
                    naturalResolver.Resolve(player, usage, year, baseSeed, new Pcg32Random(baseSeed));
                    agingResolver.Resolve(player, year, baseSeed + 1UL, new Pcg32Random(baseSeed + 1UL));
                    player.ChangeCondition(100);
                    growthResolver.Resolve(
                        player,
                        program,
                        year,
                        useStudy ? season : 0,
                        TrainingFitGrade.Normal,
                        baseSeed + 2UL,
                        new Pcg32Random(baseSeed + 2UL));

                    double current = CalculateBatterAverage(player);
                    if (player.Age == 22) totals.Age22 += current;
                    if (player.Age == 32) totals.Age32 += current;
                    if (player.Age == 36) totals.Age36 += current;
                    player.AdvanceAge();
                }
            }
            return totals;
        }

        private static double CalculateBatterAverage(PlayerGrowthState player)
        {
            int sum = 0;
            for (int index = (int)PlayerAbility.Contact; index <= (int)PlayerAbility.BatterMental; index++)
                sum += player.BaseAbilities.Get((PlayerAbility)index);
            return sum / 6d;
        }

        private sealed class CareerTotals
        {
            public double Age22;
            public double Age32;
            public double Age36;
        }
    }
}
