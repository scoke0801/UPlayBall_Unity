using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Simulation.Growth;
using Baseball.Simulation.Random;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Simulation.Growth
{
    /// <summary>
    /// 영구 성장·자연 성장·노쇠의 결정론과 상한을 검증한다.
    /// </summary>
    public sealed class GrowthResolverTests
    {
        [Test]
        public void Resolve_같은Seed와입력에서같은성장결과를만든다()
        {
            GrowthBalanceTable balance = GrowthBalanceTable.CreateDefault();
            TrainingProgramDefinition program = balance.FindProgram("japan_batting_camp");
            PlayerGrowthState first = CreateBatter(age: 20, baseRating: 58, potential: 72);
            PlayerGrowthState second = CreateBatter(age: 20, baseRating: 58, potential: 72);
            var resolver = new GrowthResolver(balance);

            GrowthResultRecord firstResult = resolver.Resolve(
                first, program, 2028, 0, TrainingFitGrade.High, 1234UL, new Pcg32Random(1234UL));
            GrowthResultRecord secondResult = resolver.Resolve(
                second, program, 2028, 0, TrainingFitGrade.High, 1234UL, new Pcg32Random(1234UL));

            Assert.That(secondResult.AbilityChanges.Length, Is.EqualTo(firstResult.AbilityChanges.Length));
            for (int index = 0; index < firstResult.AbilityChanges.Length; index++)
            {
                Assert.That(secondResult.AbilityChanges[index].Ability, Is.EqualTo(firstResult.AbilityChanges[index].Ability));
                Assert.That(secondResult.AbilityChanges[index].Amount, Is.EqualTo(firstResult.AbilityChanges[index].Amount));
            }
            Assert.That(second.Condition, Is.EqualTo(first.Condition));
        }

        [Test]
        public void Resolve_일반성장은Potential보다3을초과하지않는다()
        {
            GrowthBalanceTable balance = GrowthBalanceTable.CreateDefault();
            PlayerGrowthState player = CreateBatter(age: 18, baseRating: 70, potential: 70);
            var program = new TrainingProgramDefinition(
                "cap_test",
                OffseasonActivityType.PersonalTraining,
                TrainingCategory.Batting,
                PlayerType.Batter,
                1,
                0L,
                100d,
                new[] { new AbilityWeight(PlayerAbility.Contact, 1d) },
                0,
                0d,
                20,
                20,
                0);

            new GrowthResolver(balance).Resolve(
                player, program, 2028, 0, TrainingFitGrade.VeryHigh, 1UL, new Pcg32Random(1UL));

            Assert.That(player.BaseAbilities.Get(PlayerAbility.Contact), Is.EqualTo(73));
        }

        [Test]
        public void NaturalDevelopment와Aging은한결산에서원인을분리해기록한다()
        {
            GrowthBalanceTable balance = GrowthBalanceTable.CreateDefault();
            PlayerGrowthState player = CreateBatter(age: 36, baseRating: 65, potential: 75);
            var usage = new SeasonUsageSummary(
                1d,
                new[]
                {
                    new AbilityWeight(PlayerAbility.Contact, 0.5d),
                    new AbilityWeight(PlayerAbility.Defense, 0.5d)
                });

            GrowthResultRecord natural = new NaturalDevelopmentResolver(balance)
                .Resolve(player, usage, 2035, 11UL, new Pcg32Random(11UL));
            GrowthResultRecord aging = new AgingResolver(balance)
                .Resolve(player, 2035, 12UL, new Pcg32Random(12UL));

            Assert.That(natural.SourceType, Is.EqualTo(GrowthSourceType.NaturalDevelopment));
            Assert.That(natural.AbilityChanges, Is.Empty);
            Assert.That(aging.SourceType, Is.EqualTo(GrowthSourceType.Aging));
            Assert.That(aging.AbilityChanges.Length, Is.GreaterThan(0));
            Assert.That(player.GrowthHistory.Count, Is.EqualTo(2));
        }

        private static PlayerGrowthState CreateBatter(int age, int baseRating, int potential)
        {
            return new PlayerGrowthState(
                1,
                age,
                PlayerType.Batter,
                new AbilityRatings(baseRating),
                new AbilityRatings(potential),
                WorkEthicGrade.Diligent,
                90,
                0,
                70);
        }
    }
}
