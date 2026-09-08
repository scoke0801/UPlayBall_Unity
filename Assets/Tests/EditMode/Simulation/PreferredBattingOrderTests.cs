using System;
using System.Collections.Generic;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Historical;
using Baseball.Simulation.Random;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Simulation
{
    /// <summary>선호 타순의 발급 균형·결정론·컨디션 확률 계약을 검증한다.</summary>
    public sealed class PreferredBattingOrderTests
    {
        [TestCase(1, PreferredBattingOrder.Upper)]
        [TestCase(2, PreferredBattingOrder.Upper)]
        [TestCase(3, PreferredBattingOrder.Cleanup)]
        [TestCase(5, PreferredBattingOrder.Cleanup)]
        [TestCase(6, PreferredBattingOrder.Lower)]
        [TestCase(9, PreferredBattingOrder.Lower)]
        [TestCase(0, PreferredBattingOrder.None)]
        [TestCase(10, PreferredBattingOrder.None)]
        public void Group_타순경계를구분한다(int order, PreferredBattingOrder expected) =>
            Assert.That(PreferredBattingOrderRule.GetGroup(order), Is.EqualTo(expected));

        [Test]
        public void Issue_주전은234명이며입력순서에영향받지않는다()
        {
            CreateFixture(out var seasons, out var teams);
            var catalog = WorldCardCatalogBuilder.Build(seasons, null, CardEditionBalanceTable.CreateInitial(), teamSeasons: teams);
            var first = PreferredBattingOrderEvaluator.Evaluate(seasons, teams);
            seasons.Reverse();
            var repeated = PreferredBattingOrderEvaluator.Evaluate(seasons, teams);
            foreach (var pair in first) Assert.That(repeated[pair.Key], Is.EqualTo(pair.Value));
            var ids = new string[9];
            var counts = new int[4];
            for (int index = 0; index < 9; index++)
            {
                ids[index] = teams[0].Core25CardIds[index];
                Assert.That(catalog.TryGetCard(ids[index], out var card), Is.True);
                counts[(int)card.PreferredBattingOrder]++;
            }
            Assert.That(counts, Is.EqualTo(new[] { 0, 2, 3, 4 }));
            Assert.That(first["s00"], Is.EqualTo(PreferredBattingOrder.Upper));
            Assert.That(first["s01"], Is.EqualTo(PreferredBattingOrder.Upper));
            for (int index = 2; index < 5; index++) Assert.That(first["s0" + index], Is.EqualTo(PreferredBattingOrder.Cleanup));
            var order = PreferredBattingOrderEvaluator.CreateBattingOrder(ids, catalog);
            for (int index = 0; index < order.Length; index++)
            {
                catalog.TryGetCard(order[index], out var card);
                Assert.That(PreferredBattingOrderRule.IsMatch(card.PreferredBattingOrder, index + 1), Is.True);
            }
        }

        [Test]
        public void Condition_일만회추첨에서불일치하락률이높고재현된다()
        {
            var balance = ConditionChemistryBalanceTable.CreateDefault();
            var resolver = new ConditionFluctuationResolver();
            var preferred = new Pcg32Random(90801UL);
            var mismatch = new Pcg32Random(90801UL);
            var replay = new Pcg32Random(90801UL);
            int preferredDeclines = 0, mismatchDeclines = 0;
            long preferredTotal = 0, mismatchTotal = 0;
            int preferredCondition = 80, mismatchCondition = 80;
            for (int index = 0; index < 10000; index++)
            {
                int normal = resolver.ResolveNextCondition(80, balance, preferred, BattingOrderFit.Preferred);
                int penalty = resolver.ResolveNextCondition(80, balance, mismatch, BattingOrderFit.Mismatch);
                Assert.That(penalty, Is.EqualTo(resolver.ResolveNextCondition(80, balance, replay, BattingOrderFit.Mismatch)));
                if (normal < 80) preferredDeclines++;
                if (penalty < 80) mismatchDeclines++;
            }
            var preferredWalk = new Pcg32Random(90802UL);
            var mismatchWalk = new Pcg32Random(90802UL);
            for (int index = 0; index < 10000; index++)
            {
                preferredCondition = resolver.ResolveNextCondition(preferredCondition, balance, preferredWalk, BattingOrderFit.Preferred);
                mismatchCondition = resolver.ResolveNextCondition(mismatchCondition, balance, mismatchWalk, BattingOrderFit.Mismatch);
                Assert.That(preferredCondition, Is.InRange(balance.PreferredOrderConditionFloor, 100));
                preferredTotal += preferredCondition;
                mismatchTotal += mismatchCondition;
            }
            Assert.That(mismatchDeclines, Is.GreaterThan(preferredDeclines + 1500));
            Assert.That(mismatchTotal, Is.LessThan(preferredTotal));
            Assert.That(resolver.ResolveNextCondition(0, balance, preferred, BattingOrderFit.Preferred), Is.GreaterThanOrEqualTo(40));
            int modifier = ConditionFluctuationResolver.ResolvePreferredOrderModifier(0, -10, BattingOrderFit.Preferred, balance);
            Assert.That(new EffectiveMatchCondition(0, modifier, -10, 0, 0).Value, Is.EqualTo(40));
            TestContext.WriteLine($"10000회 중립 시작 하락: 일치={preferredDeclines}, 불일치={mismatchDeclines}; 장기 평균: 일치={preferredTotal / 10000d:F2}, 불일치={mismatchTotal / 10000d:F2}");
        }

        private static void CreateFixture(out List<PlayerSeasonDefinition> seasons, out TeamSeasonDefinition[] teams)
        {
            seasons = new List<PlayerSeasonDefinition>();
            var ids = new string[25];
            for (int index = 0; index < 25; index++)
            {
                var ratings = new AbilityRatings(50);
                if (index < 2) ratings.AddClamped(PlayerAbility.Speed, 45);
                if (index >= 2 && index < 5) ratings.AddClamped(PlayerAbility.Power, 45);
                string id = "s" + index.ToString("D2");
                seasons.Add(new PlayerSeasonDefinition(id, "p" + index, 2025, "f", "t",
                    index < 14 ? PlayerPosition.FirstBase : PlayerPosition.StartingPitcher,
                    PitcherRole.Starter, index < 14 ? PlayerType.Batter : PlayerType.Pitcher,
                    RegistrationType.Domestic, ratings, 5, new AbilityRatings(100)));
                ids[index] = PlayerCardDefinition.CreateStableCardId(id, PlayerCardEdition.Normal);
            }
            teams = new[] { new TeamSeasonDefinition("t", "f", 2025, ids, ids, 50) };
        }
    }
}
