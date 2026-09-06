using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Historical;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Simulation
{
    /// <summary>구단주 새 게임의 완성 선택과 선택 중 즉시 거부 규칙을 검증한다.</summary>
    public sealed class OwnerStarterRosterResolverTests
    {
        [Test]
        public void CreateInitial_메인카드비용상한은60이다()
        {
            Assert.That(OwnerStarterRosterRule.CreateInitial().MaximumMainCost, Is.EqualTo(60));
        }

        [Test]
        public void ValidateMainCards_비용60은통과하고61은거부한다()
        {
            int[] costsAtLimit = { 9, 9, 7, 7, 6, 6, 4, 4, 4, 4 };
            WorldCardCatalog catalogAtLimit = CreateCatalog(costsAtLimit, out string[] cardsAtLimit);
            var resolver = new OwnerStarterRosterResolver(OwnerStarterRosterRule.CreateInitial());

            OwnerMainCardSelectionStatus accepted = resolver.ValidateMainCards(
                "FRANCHISE", cardsAtLimit, catalogAtLimit);

            Assert.That(accepted.IsValid, Is.True, accepted.Message);
            Assert.That(accepted.TotalCost, Is.EqualTo(60));

            int[] costsOverLimit = { 9, 9, 7, 7, 6, 6, 5, 4, 4, 4 };
            WorldCardCatalog catalogOverLimit = CreateCatalog(costsOverLimit, out string[] cardsOverLimit);
            OwnerMainCardSelectionStatus rejected = resolver.ValidateMainCards(
                "FRANCHISE", cardsOverLimit, catalogOverLimit);

            Assert.That(rejected.IsValid, Is.False);
            Assert.That(rejected.ErrorCode, Is.EqualTo("TOTAL_COST"));
            Assert.That(rejected.TotalCost, Is.EqualTo(61));
        }

        [Test]
        public void ValidatePartialMainCards_같은선수의다른연도는선택즉시거부한다()
        {
            WorldCardCatalog catalog = CreateDuplicatePersonCatalog(out string[] cardIds);
            var resolver = new OwnerStarterRosterResolver(OwnerStarterRosterRule.CreateInitial());

            OwnerMainCardSelectionStatus result = resolver.ValidatePartialMainCards(
                "FRANCHISE", cardIds, catalog);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("DUPLICATE_PERSON"));
        }

        private static WorldCardCatalog CreateCatalog(int[] costs, out string[] cardIds)
        {
            var seasons = new PlayerSeasonDefinition[costs.Length];
            var cards = new PlayerCardDefinition[costs.Length];
            cardIds = new string[costs.Length];
            for (int index = 0; index < costs.Length; index++)
            {
                bool isPitcher = index >= 6;
                string seasonId = $"SEASON_{index:00}";
                string cardId = PlayerCardDefinition.CreateStableCardId(seasonId, PlayerCardEdition.Normal);
                var ratings = new AbilityRatings(50);
                seasons[index] = new PlayerSeasonDefinition(
                    seasonId,
                    $"PERSON_{index:00}",
                    2000 + index,
                    "FRANCHISE",
                    $"FRANCHISE_{2000 + index}",
                    isPitcher ? PlayerPosition.StartingPitcher : PlayerPosition.Catcher,
                    isPitcher ? PitcherRole.Starter : PitcherRole.MiddleRelief,
                    isPitcher ? PlayerType.Pitcher : PlayerType.Batter,
                    RegistrationType.Domestic,
                    ratings,
                    costs[index],
                    ratings);
                cards[index] = new PlayerCardDefinition(
                    cardId, seasonId, PlayerCardEdition.Normal,
                    new int[PlayerAbilityCatalog.AbilityCount]);
                cardIds[index] = cardId;
            }
            return new WorldCardCatalog(seasons, cards);
        }

        private static WorldCardCatalog CreateDuplicatePersonCatalog(out string[] cardIds)
        {
            var seasons = new PlayerSeasonDefinition[2];
            var cards = new PlayerCardDefinition[2];
            cardIds = new string[2];
            for (int index = 0; index < 2; index++)
            {
                string seasonId = $"DUPLICATE_SEASON_{index}";
                string cardId = PlayerCardDefinition.CreateStableCardId(seasonId, PlayerCardEdition.Normal);
                var ratings = new AbilityRatings(50);
                seasons[index] = new PlayerSeasonDefinition(
                    seasonId,
                    "SAME_PERSON",
                    2023 + index,
                    "FRANCHISE",
                    $"FRANCHISE_{2023 + index}",
                    PlayerPosition.Shortstop,
                    PitcherRole.MiddleRelief,
                    PlayerType.Batter,
                    RegistrationType.Domestic,
                    ratings,
                    4,
                    ratings);
                cards[index] = new PlayerCardDefinition(
                    cardId, seasonId, PlayerCardEdition.Normal,
                    new int[PlayerAbilityCatalog.AbilityCount]);
                cardIds[index] = cardId;
            }
            return new WorldCardCatalog(seasons, cards);
        }
    }
}
