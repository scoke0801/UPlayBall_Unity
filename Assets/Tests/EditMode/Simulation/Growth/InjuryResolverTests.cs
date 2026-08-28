using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Simulation.Growth;
using Baseball.Simulation.Random;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Simulation.Growth
{
    /// <summary>
    /// 설명 가능한 부상 위험과 치료 비용 규칙을 검증한다.
    /// </summary>
    public sealed class InjuryResolverTests
    {
        [Test]
        public void CalculateRisk_피로와과부하가높으면위험이상승한다()
        {
            var resolver = new InjuryResolver(InjuryBalanceTable.CreateDefault());
            var safe = new InjuryRiskInput(22, 10, 0.8d, 0d, false, 90);
            var overloaded = new InjuryRiskInput(36, 100, 2d, 1d, true, 30);

            Assert.That(resolver.CalculateRisk(overloaded), Is.GreaterThan(resolver.CalculateRisk(safe)));
        }

        [Test]
        public void Resolve와ChooseTreatment_부상Seed와전문치료비용을기록한다()
        {
            InjuryBalanceTable balance = InjuryBalanceTable.CreateDefault();
            var resolver = new InjuryResolver(balance);
            PlayerGrowthState player = CreatePlayer();
            var economy = new CareerEconomyState(MoneyAmount.FromTenThousandWon(1_000L));
            var input = new InjuryRiskInput(36, 100, 2d, 1d, true, 0);

            InjuryRecord injury = resolver.Resolve(
                player, input, 2036, "game_80", 99UL, new FixedRandom());
            resolver.ChooseTreatment(
                injury, InjuryTreatmentChoice.SpecialistTreatment, economy, 2036);

            Assert.That(injury, Is.Not.Null);
            Assert.That(injury.RandomSeed, Is.EqualTo(99UL));
            Assert.That(injury.TreatmentChoice, Is.EqualTo(InjuryTreatmentChoice.SpecialistTreatment));
            Assert.That(economy.Money, Is.EqualTo(MoneyAmount.FromTenThousandWon(500L)));
            Assert.That(player.InjuryHistory.Count, Is.EqualTo(1));
        }

        private static PlayerGrowthState CreatePlayer()
        {
            return new PlayerGrowthState(
                1, 36, PlayerType.Batter,
                new AbilityRatings(60), new AbilityRatings(70),
                WorkEthicGrade.Normal, 80, 50, 50);
        }

        private sealed class FixedRandom : IRandomSource
        {
            public double NextDouble() => 0d;
        }
    }
}
