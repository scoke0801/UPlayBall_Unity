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

            System.Console.WriteLine(
                $"Personal Age22 {personalAge22:F2} / Study Age22 {studyAge22:F2} / " +
                $"Study Age32 {studyAge32:F2} / Study Age36 {studyAge36:F2}");

            Assert.That(personalAge22, Is.GreaterThan(50d));
            Assert.That(studyAge22, Is.GreaterThan(personalAge22));
            Assert.That(studyAge32, Is.LessThanOrEqualTo(73d));
            Assert.That(studyAge36, Is.LessThanOrEqualTo(73d));
        }

        [Test]
        public void Simulate_일만오천표본에서훈련강도는시간과돈과컨디션의서로다른효율을만든다()
        {
            GrowthBalanceTable balance = GrowthBalanceTable.CreateDefault();
            IntensityTotals safe = SimulateIntensity(
                balance,
                TrainingIntensity.Safe,
                100000UL);
            IntensityTotals standard = SimulateIntensity(
                balance,
                TrainingIntensity.Standard,
                200000UL);
            IntensityTotals intensive = SimulateIntensity(
                balance,
                TrainingIntensity.Intensive,
                300000UL);

            System.Console.WriteLine(
                $"Safe {safe.AverageGain:F3}/{safe.AverageCondition:F2}, " +
                $"Standard {standard.AverageGain:F3}/{standard.AverageCondition:F2}, " +
                $"Intensive {intensive.AverageGain:F3}/{intensive.AverageCondition:F2}");

            Assert.That(safe.AverageGain, Is.LessThan(standard.AverageGain));
            Assert.That(standard.AverageGain, Is.LessThan(intensive.AverageGain));
            Assert.That(safe.GainPerWeek, Is.LessThan(standard.GainPerWeek));
            Assert.That(standard.GainPerWeek, Is.LessThan(intensive.GainPerWeek));
            Assert.That(safe.GainPerMoney, Is.GreaterThan(standard.GainPerMoney));
            Assert.That(standard.GainPerMoney, Is.GreaterThan(intensive.GainPerMoney));
            Assert.That(safe.AverageCondition, Is.GreaterThan(standard.AverageCondition));
            Assert.That(standard.AverageCondition, Is.GreaterThan(intensive.AverageCondition));
        }

        [Test]
        public void Simulate_일만번엘리트유학은개발한계구간에서무성장결과를만들지않는다()
        {
            const int SampleCount = 10000;
            GrowthBalanceTable balance = GrowthBalanceTable.CreateDefault();
            TrainingProgramDefinition program = balance.FindProgram("usa_elite_batting_academy");
            var resolver = new GrowthResolver(balance);
            int zeroGrowthCount = 0;
            int totalGrowth = 0;

            for (int sample = 0; sample < SampleCount; sample++)
            {
                var player = new PlayerGrowthState(
                    sample + 1,
                    22,
                    PlayerType.Batter,
                    new AbilityRatings(70),
                    new AbilityRatings(70),
                    WorkEthicGrade.Normal,
                    90,
                    0,
                    70);
                ulong seed = 1_700_000UL + (ulong)sample;
                GrowthResultRecord result = resolver.Resolve(
                    player,
                    program,
                    2028,
                    0,
                    TrainingFitGrade.Normal,
                    seed,
                    new Pcg32Random(seed));
                int gain = SumAbilityChanges(result);
                totalGrowth += gain;
                if (gain == 0)
                    zeroGrowthCount++;
            }

            double averageGrowth = totalGrowth / (double)SampleCount;
            System.Console.WriteLine(
                $"Elite plateau growth {averageGrowth:F3} / zero {zeroGrowthCount}/{SampleCount}");

            Assert.That(zeroGrowthCount, Is.EqualTo(0));
            Assert.That(averageGrowth, Is.InRange(1.00d, 1.25d));
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

        private static IntensityTotals SimulateIntensity(
            GrowthBalanceTable balance,
            TrainingIntensity intensity,
            ulong seedOffset)
        {
            TrainingProgramDefinition program = balance.GetProgram(
                "pitch_velocity_camp",
                intensity);
            var resolver = new GrowthResolver(balance);
            double totalGain = 0d;
            double totalCondition = 0d;
            for (int sample = 0; sample < CareersPerStrategy; sample++)
            {
                var player = new PlayerGrowthState(
                    sample + 1,
                    20,
                    PlayerType.Pitcher,
                    new AbilityRatings(58),
                    new AbilityRatings(72),
                    WorkEthicGrade.Normal,
                    90,
                    0,
                    70);
                ulong seed = seedOffset + (ulong)sample;
                GrowthResultRecord result = resolver.Resolve(
                    player,
                    program,
                    2028,
                    0,
                    TrainingFitGrade.Normal,
                    seed,
                    new Pcg32Random(seed));
                for (int index = 0; index < result.AbilityChanges.Length; index++)
                    totalGain += result.AbilityChanges[index].Amount;
                totalCondition += player.Condition;
            }

            double averageGain = totalGain / CareersPerStrategy;
            return new IntensityTotals(
                averageGain,
                totalCondition / CareersPerStrategy,
                averageGain / program.DurationWeeks,
                averageGain / program.MoneyCost);
        }

        private static int SumAbilityChanges(GrowthResultRecord result)
        {
            int total = 0;
            for (int index = 0; index < result.AbilityChanges.Length; index++)
                total += result.AbilityChanges[index].Amount;
            return total;
        }

        private sealed class CareerTotals
        {
            public double Age22;
            public double Age32;
            public double Age36;
        }

        private readonly struct IntensityTotals
        {
            public IntensityTotals(
                double averageGain,
                double averageCondition,
                double gainPerWeek,
                double gainPerMoney)
            {
                AverageGain = averageGain;
                AverageCondition = averageCondition;
                GainPerWeek = gainPerWeek;
                GainPerMoney = gainPerMoney;
            }

            public double AverageGain { get; }
            public double AverageCondition { get; }
            public double GainPerWeek { get; }
            public double GainPerMoney { get; }
        }
    }
}
