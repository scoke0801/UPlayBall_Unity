using System;
using System.Collections.Generic;
using Baseball.Core.Growth;
using Baseball.Core.Balance;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Historical;
using Baseball.Simulation.Random;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Simulation
{
    /// <summary>특수 카드가 일반 획득 경로와 원본 시즌 계약을 침범하지 않는지 검증한다.</summary>
    public sealed class SpecialCardRulesTests
    {
        [Test]
        public void SpecialCards_SalaryAndSaleTablesSupportAllIssuedEditions()
        {
            var salary = OwnerPlayerMarketBalanceTable.CreateInitial();
            var sale = CardSaleBalanceTable.CreateInitial();
            foreach (PlayerCardEdition edition in Enum.GetValues(typeof(PlayerCardEdition)))
            {
                Assert.That(salary.GetAnnualSalary(5, edition, 1), Is.GreaterThan(0));
                Assert.That(sale.GetEditionMultiplier(edition), Is.GreaterThanOrEqualTo(0));
            }
            Assert.That(salary.GetAnnualSalary(5, PlayerCardEdition.Rare, 1),
                Is.EqualTo(salary.GetAnnualSalary(5, PlayerCardEdition.Normal, 1)));
            Assert.That(sale.GetEditionMultiplier(PlayerCardEdition.Rare), Is.EqualTo(1d));
        }
        [Test]
        public void ExCostGateRejectsNineWithoutChangingSource()
        {
            var season = Season(9);
            Assert.Throws<ArgumentException>(() => new WorldCardCatalog(new[] { season }, new[] {
                Card(PlayerCardEdition.Normal), Card(PlayerCardEdition.Ex) }));
            Assert.That(season.Cost, Is.EqualTo(9));
        }

        [Test]
        public void ScoutAndPityNeverSelectEx()
        {
            var catalog = new WorldCardCatalog(new[] { Season(10) }, new[] {
                Card(PlayerCardEdition.Normal), Card(PlayerCardEdition.Ex) });
            var pool = new ScoutPoolDefinition("general", ScoutType.General,
                ScoutPoolDefinition.CreateInitialCostWeights(), ScoutPoolDefinition.CreateStandardEditionWeights(), 0);
            var roller = new ScoutRoller();
            var random = new Pcg32Random(17);
            for (int index = 0; index < 1000; index++)
            {
                Assert.That(roller.Roll(pool, catalog, ScoutFeaturePolicy.FullWorldAwards, random).Edition,
                    Is.EqualTo(PlayerCardEdition.Normal));
                var economy = new ManagerEconomyState(0, 0, 0, 100);
                Assert.That(roller.RollFocused("franchise", null, catalog, ScoutFeaturePolicy.FullWorldAwards,
                    ScoutPityBalanceTable.CreateInitial(), economy, random).Edition, Is.EqualTo(PlayerCardEdition.Normal));
            }
        }

        [Test]
        public void WildcardMatchesLineageYearFranchiseButNotPureYearOrAwards()
        {
            var zero = new TeamColorStatBonus(new int[PlayerAbilityCatalog.AbilityCount]);
            var key = new TeamColorEligibilityKey(2000, "old-franchise", "old-team", PlayerCardEdition.Legend,
                new[] { "old-franchise", "new-franchise" });
            var lineageYear = new TeamColorDefinition("lineage-year", TeamColorFamily.YearFranchise, 1,
                zero, zero, originYear: 2024, originFranchiseId: "new-franchise", originTeamSeasonKey: "new-team");
            var pureYear = new TeamColorDefinition("year", TeamColorFamily.Year, 1, zero, zero, originYear: 2024);
            var other = new TeamColorDefinition("other", TeamColorFamily.Franchise, 1, zero, zero,
                originFranchiseId: "unrelated");
            var award = new TeamColorDefinition("award", TeamColorFamily.Mvp, 1, zero, zero,
                requiredEdition: PlayerCardEdition.Mvp);
            Assert.That(lineageYear.IsEligible(key), Is.True);
            Assert.That(pureYear.IsEligible(key), Is.False);
            Assert.That(other.IsEligible(key), Is.False);
            Assert.That(award.IsEligible(key), Is.False);
            Assert.That(key.OriginYear, Is.EqualTo(2000));
            Assert.That(key.OriginFranchiseId, Is.EqualTo("old-franchise"));
        }

        [Test]
        public void CombinationIgnoresMissingBucketsAndCatalogOrder()
        {
            var normal = Card(PlayerCardEdition.Normal);
            var ex = Card(PlayerCardEdition.Ex);
            var a = new WorldCardCatalog(new[] { Season(10) }, new[] { normal, ex });
            var b = new WorldCardCatalog(new[] { Season(10) }, new[] { ex, normal });
            var materials = new[] { normal.CardId, normal.CardId, normal.CardId, normal.CardId, normal.CardId };
            var outcomes = new[] { new CardCombinationOutcome(5, 4, PlayerCardEdition.Rare, 99),
                new CardCombinationOutcome(5, 10, PlayerCardEdition.Ex, 1) };
            Assert.That(CardCombinationResolver.Roll(a, materials, outcomes, new Pcg32Random(9)).CardId,
                Is.EqualTo(CardCombinationResolver.Roll(b, materials, outcomes, new Pcg32Random(9)).CardId));
            Assert.That(CardCombinationResolver.Roll(a, materials, outcomes, new Pcg32Random(9)).Edition,
                Is.EqualTo(PlayerCardEdition.Ex));
        }

        [Test]
        public void ExDoublesOnlySkillBlockContributionOnce()
        {
            var growth = BalanceTable.CreateDefault().Growth;
            var board = new OwnedCardSkillBoardState();
            board.Add(new PlacedSkillBlock(new SkillBlockInstance(1, growth.SkillBlocks[0].BlockId), 0, 0, 0));
            var owned = new OwnedPlayerCardState("season:Normal", enhancementLevel: 2, skillBoard: board);
            var resolver = new OwnerCardAbilityResolver(growth);
            int totalBlockBonus = 0;
            for (int index = 0; index < PlayerAbilityCatalog.AbilityCount; index++)
            {
                var ability = (PlayerAbility)index;
                var normal = resolver.ResolveContribution(Season(10), Card(PlayerCardEdition.Normal), owned, ability);
                var ex = resolver.ResolveContribution(Season(10), Card(PlayerCardEdition.Ex), owned, ability);
                Assert.That(ex.SkillBlock, Is.EqualTo(normal.SkillBlock * 2));
                Assert.That(ex.Enhancement, Is.EqualTo(normal.Enhancement));
                Assert.That(ex.Training, Is.EqualTo(normal.Training));
                Assert.That(ex.BaseCard, Is.EqualTo(normal.BaseCard));
                totalBlockBonus += normal.SkillBlock;
            }
            Assert.That(totalBlockBonus, Is.GreaterThan(0));
        }

        private static PlayerCardDefinition Card(PlayerCardEdition edition) => new PlayerCardDefinition(
            "season:" + edition, "season", edition, new int[PlayerAbilityCatalog.AbilityCount]);

        private static PlayerSeasonDefinition Season(int cost) => new PlayerSeasonDefinition(
            "season", "person", 2024, "franchise", "team", PlayerPosition.Catcher, PitcherRole.Starter,
            PlayerType.Batter, RegistrationType.Domestic, new AbilityRatings(50), cost, new AbilityRatings(70));
    }
}
