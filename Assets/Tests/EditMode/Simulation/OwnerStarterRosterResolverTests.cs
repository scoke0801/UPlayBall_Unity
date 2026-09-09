using System.Collections.Generic;
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
        public void CreateInitial_CostV16저가치구간은Cost2열장과Cost3다섯장이다()
        {
            OwnerStarterRosterRule rule = OwnerStarterRosterRule.CreateInitial();

            Assert.That(rule.FillerMinimumCost, Is.EqualTo(2));
            Assert.That(rule.FillerMaximumCost, Is.EqualTo(3));
            Assert.That(rule.GetFillerCount(2), Is.EqualTo(10));
            Assert.That(rule.GetFillerCount(3), Is.EqualTo(5));
        }

        [Test]
        public void Resolve_CostV16저가치Pool로유효한25인로스터를생성한다()
        {
            WorldCardCatalog catalog = CreateStarterCatalog(out TeamSeasonDefinition team, out string[] mainCardIds);
            var resolver = new OwnerStarterRosterResolver(OwnerStarterRosterRule.CreateInitial());

            OwnerStarterRosterResult result = resolver.Resolve(team, mainCardIds, catalog, 20260905UL, 0);

            Assert.That(result.Roster.Entries.Count, Is.EqualTo(ActiveRosterCompositionRule.ActiveRosterSize));
            Assert.That(new ActiveRosterValidator().Validate(result.Roster).IsValid, Is.True);
            var fillerCountByCost = new Dictionary<int, int>();
            for (int index = 0; index < result.FillerCardIds.Count; index++)
            {
                Assert.That(catalog.TryGetCard(result.FillerCardIds[index], out PlayerCardDefinition card), Is.True);
                int cost = catalog.GetPlayerSeason(card).Cost;
                fillerCountByCost.TryGetValue(cost, out int count);
                fillerCountByCost[cost] = count + 1;
            }
            Assert.That(fillerCountByCost[2], Is.EqualTo(10));
            Assert.That(fillerCountByCost[3], Is.EqualTo(5));
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

        private static WorldCardCatalog CreateStarterCatalog(
            out TeamSeasonDefinition team,
            out string[] mainCardIds)
        {
            const string FranchiseId = "FRANCHISE";
            const string TeamSeasonKey = "FRANCHISE_2024";
            var seasons = new List<PlayerSeasonDefinition>();
            var cards = new List<PlayerCardDefinition>();
            var rosterCardIds = new List<string>(25);
            mainCardIds = new string[10];

            for (int index = 0; index < 10; index++)
            {
                PlayerType playerType = index < 6 ? PlayerType.Batter : PlayerType.Pitcher;
                mainCardIds[index] = AddStarterCard(
                    seasons,
                    cards,
                    $"MAIN_{index:00}",
                    FranchiseId,
                    TeamSeasonKey,
                    playerType,
                    4);
                rosterCardIds.Add(mainCardIds[index]);
            }

            int fillerIndex = 0;
            for (int cost = 2; cost <= 3; cost++)
            {
                int cardCount = cost == 2 ? 10 : 5;
                int hitterCount = cost == 2 ? 5 : 3;
                int pitcherCount = cardCount - hitterCount;
                for (int index = 0; index < hitterCount; index++)
                {
                    rosterCardIds.Add(AddStarterCard(
                        seasons,
                        cards,
                        $"FILLER_H_{fillerIndex++:00}",
                        "FILLER_FRANCHISE",
                        "FILLER_2024",
                        PlayerType.Batter,
                        cost));
                }
                for (int index = 0; index < pitcherCount; index++)
                {
                    rosterCardIds.Add(AddStarterCard(
                        seasons,
                        cards,
                        $"FILLER_P_{fillerIndex++:00}",
                        "FILLER_FRANCHISE",
                        "FILLER_2024",
                        PlayerType.Pitcher,
                        cost));
                }
            }

            team = new TeamSeasonDefinition(
                TeamSeasonKey,
                FranchiseId,
                2024,
                rosterCardIds,
                rosterCardIds,
                50d);
            return new WorldCardCatalog(seasons, cards);
        }

        private static string AddStarterCard(
            List<PlayerSeasonDefinition> seasons,
            List<PlayerCardDefinition> cards,
            string id,
            string franchiseId,
            string teamSeasonKey,
            PlayerType playerType,
            int cost)
        {
            string seasonId = "SEASON_" + id;
            string cardId = PlayerCardDefinition.CreateStableCardId(seasonId, PlayerCardEdition.Normal);
            var ratings = new AbilityRatings(50);
            seasons.Add(new PlayerSeasonDefinition(
                seasonId,
                "PERSON_" + id,
                2024,
                franchiseId,
                teamSeasonKey,
                playerType == PlayerType.Pitcher ? PlayerPosition.StartingPitcher : PlayerPosition.Catcher,
                playerType == PlayerType.Pitcher ? PitcherRole.Starter : PitcherRole.MiddleRelief,
                playerType,
                RegistrationType.Domestic,
                ratings,
                cost,
                ratings));
            cards.Add(new PlayerCardDefinition(
                cardId,
                seasonId,
                PlayerCardEdition.Normal,
                new int[PlayerAbilityCatalog.AbilityCount]));
            return cardId;
        }
    }
}
