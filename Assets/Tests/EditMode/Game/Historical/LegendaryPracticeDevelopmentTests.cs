using System;
using System.Linq;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Historical;
using Baseball.Simulation.Historical;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game.Historical
{
    /// <summary>연습경기 성장의 경계·역할·영구 능력치와 원본 격리를 검사한다.</summary>
    public sealed class LegendaryPracticeDevelopmentTests
    {
        private static LegendaryPracticeDevelopmentBalance Configuration() => new LegendaryPracticeDevelopmentBalance
        {
            tiers = new[] {
                new LegendaryPracticeDevelopmentTier { maximumRank = 20, blockCount = 4,
                    blockRarity = SkillBlockRarity.Unique, enhancement = 5, studyCompletions = 2,
                    studyRank = CardStudyRank.A, traitRank = CardTraitRank.S },
                new LegendaryPracticeDevelopmentTier { maximumRank = 100, blockCount = 2,
                    blockRarity = SkillBlockRarity.Normal, enhancement = 1, studyCompletions = 1,
                    studyRank = CardStudyRank.C, traitRank = CardTraitRank.C } }
        };

        [TestCase(PlayerType.Batter, PlayerPosition.Catcher)]
        [TestCase(PlayerType.Pitcher, PlayerPosition.StartingPitcher)]
        [TestCase(PlayerType.Pitcher, PlayerPosition.ReliefPitcher)]
        public void 상위등급은실제성장이증가하고원본과다른상대를오염시키지않는다(PlayerType type, PlayerPosition position)
        {
            var balance = BalanceTable.CreateDefault();
            var service = new LegendaryPracticeDevelopment(balance, Configuration());
            var season = new PlayerSeasonDefinition("season", "person", 2000, "franchise", "team", position,
                position == PlayerPosition.StartingPitcher ? PitcherRole.Starter : PitcherRole.MiddleRelief,
                type, RegistrationType.Domestic, new AbilityRatings(50), 5, new AbilityRatings(70));
            var card = new PlayerCardDefinition("card", "season", PlayerCardEdition.Normal, new int[12]);
            var low = service.Create(card, season, 100);
            var high = service.Create(card, season, 1);
            var resolver = new OwnerCardAbilityResolver(balance.Growth);
            int lowTotal = 0, highTotal = 0;
            for (int i = 0; i < 12; i++)
            {
                lowTotal += resolver.ResolveRawPermanent(season, card, low, (PlayerAbility)i);
                highTotal += resolver.ResolveRawPermanent(season, card, high, (PlayerAbility)i);
            }
            Assert.That(highTotal, Is.GreaterThan(lowTotal));
            Assert.That(high.SkillBoard.Placements.Count, Is.EqualTo(4));
            Assert.That(low.SkillBoard.Placements.Count, Is.EqualTo(2));
            Assert.That(high.Trait.rank, Is.EqualTo(CardTraitRank.S));
            Assert.That(high.Trait.trainingSeason, Is.GreaterThan(high.LastStudySeason));
            Assert.That(balance.TraitTraining.Get(high.Trait.trait).playerType, Is.EqualTo(type));
            Assert.That(resolver.ResolveActiveTraitIds(high).Length, Is.GreaterThan(0));
            var repeated = service.Create(card, season, 1);
            Assert.That(high.SkillBoard.Placements.SequenceEqual(repeated.SkillBoard.Placements), Is.True);
            high.Training.AddStudyBonus(PlayerAbility.Contact, 1, "검증");
            Assert.That(high.Training.GetStudyBonus(PlayerAbility.Contact), Is.Not.EqualTo(repeated.Training.GetStudyBonus(PlayerAbility.Contact)));
            Assert.That(season.CreateBaseAttributes().Get(PlayerAbility.Contact), Is.EqualTo(50));
        }

        [TestCase(0)]
        [TestCase(101)]
        public void 순위범위를벗어나면거부한다(int rank) =>
            Assert.Throws<ArgumentOutOfRangeException>(() => Configuration().GetTier(rank));

        [Test]
        public void 단계경계에서상위설정을소비한다()
        {
            var configuration = Configuration(); configuration.Validate();
            Assert.That(configuration.GetTier(20).enhancement, Is.EqualTo(5));
            Assert.That(configuration.GetTier(21).enhancement, Is.EqualTo(1));
        }

        [Test]
        public void 하위가더강하거나100위가누락된설정은거부한다()
        {
            var configuration = Configuration(); configuration.tiers[1].blockCount = 5;
            Assert.Throws<ArgumentException>(() => configuration.Validate());
            configuration = Configuration(); configuration.tiers[1].maximumRank = 99;
            Assert.Throws<ArgumentException>(() => configuration.Validate());
        }
    }
}
