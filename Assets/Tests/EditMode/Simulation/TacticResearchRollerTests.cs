using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Core.Growth;
using Baseball.Simulation.Historical;
using Baseball.Simulation.Random;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Simulation
{
    public sealed class TacticResearchRollerTests
    {
        [Test]
        public void Signature_전술은_연구_결과로_나오지_않는다()
        {
            var roller = new TacticResearchRoller();
            TacticResearchPoolDefinition pool = CreatePool();
            IReadOnlyList<TacticCardDefinition> catalog = CreateCatalog();

            for (ulong seed = 1; seed <= 200; seed++)
            {
                TacticCardDefinition card = roller.Roll(pool, catalog, new Pcg32Random(seed));
                Assert.AreNotEqual(TacticTier.Signature, card.TacticTier);
            }
        }

        [Test]
        public void 같은_Seed로_두_번_돌리면_같은_전술이_나온다()
        {
            var roller = new TacticResearchRoller();
            TacticResearchPoolDefinition pool = CreatePool();
            IReadOnlyList<TacticCardDefinition> catalog = CreateCatalog();

            TacticCardDefinition first = roller.Roll(pool, catalog, new Pcg32Random(4242UL));
            TacticCardDefinition second = roller.Roll(pool, catalog, new Pcg32Random(4242UL));

            Assert.AreEqual(first.CardId, second.CardId);
        }

        [Test]
        public void 계열_필터가_있으면_그_계열_카드만_나온다()
        {
            var roller = new TacticResearchRoller();
            var pool = new TacticResearchPoolDefinition(
                "pitching",
                TacticResearchPoolDefinition.CreateInitialTierWeights(),
                3_500L,
                TacticCardCategory.Pitching);
            IReadOnlyList<TacticCardDefinition> catalog = CreateCatalog();

            for (ulong seed = 1; seed <= 50; seed++)
            {
                TacticCardDefinition card = roller.Roll(pool, catalog, new Pcg32Random(seed));
                Assert.AreEqual(TacticCardCategory.Pitching, card.Category);
            }
        }

        [Test]
        public void 후보가_없는_등급이_빠지면_확률이_재정규화된다()
        {
            var roller = new TacticResearchRoller();
            // Special 등급 카드가 없는 카탈로그라, 표시 확률에 Special이 남으면 안 된다.
            var catalog = new List<TacticCardDefinition>
            {
                CreateCard("n1", TacticCardCategory.Common, TacticTier.Normal),
                CreateCard("r1", TacticCardCategory.Common, TacticTier.Rare)
            };

            IReadOnlyList<TacticResearchTierProbability> probabilities =
                roller.GetProbabilities(CreatePool(), catalog);

            double total = 0d;
            for (int index = 0; index < probabilities.Count; index++)
            {
                Assert.AreNotEqual(TacticTier.Special, probabilities[index].Tier);
                total += probabilities[index].Probability;
            }
            Assert.AreEqual(1d, total, 0.000001d);
        }

        private static TacticResearchPoolDefinition CreatePool()
        {
            return new TacticResearchPoolDefinition(
                "general",
                TacticResearchPoolDefinition.CreateInitialTierWeights(),
                3_500L);
        }

        private static IReadOnlyList<TacticCardDefinition> CreateCatalog()
        {
            return new List<TacticCardDefinition>
            {
                CreateCard("bat_normal", TacticCardCategory.Batting, TacticTier.Normal),
                CreateCard("pit_normal", TacticCardCategory.Pitching, TacticTier.Normal),
                CreateCard("pit_rare", TacticCardCategory.Pitching, TacticTier.Rare),
                CreateCard("ana_special", TacticCardCategory.Analysis, TacticTier.Special),
                CreateCard("com_signature", TacticCardCategory.Common, TacticTier.Signature)
            };
        }

        private static TacticCardDefinition CreateCard(
            string cardId,
            TacticCardCategory category,
            TacticTier tier)
        {
            return new TacticCardDefinition(
                cardId,
                cardId,
                category,
                tier,
                "테스트 Reference",
                "테스트 Balance",
                Array.Empty<TacticTriggerCondition>(),
                TacticTargetRule.BattingTeam,
                new[] { new TacticStatModifier(PlayerAbility.Contact, 1) },
                Array.Empty<TacticBehaviorModifier>(),
                TacticDurationRule.UntilInningEnd,
                Array.Empty<string>(),
                false);
        }
    }
}
