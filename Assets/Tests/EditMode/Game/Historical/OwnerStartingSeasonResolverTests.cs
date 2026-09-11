using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Historical;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game.Historical
{
    /// <summary>구단주 새 게임의 첫 시즌이 메인 카드 선택에서 결정되는 규칙을 검증한다.</summary>
    public sealed class OwnerStartingSeasonResolverTests
    {
        [Test]
        public void Resolve_최고Cost카드의원본연도를선택한다()
        {
            WorldCardCatalog catalog = CreateCatalog(
                new[] { 2024, 1998, 2018 },
                new[] { 4, 10, 9 },
                out string[] cardIds);

            PlayerSeasonDefinition result = OwnerStartingSeasonResolver.Resolve(cardIds, catalog);

            Assert.That(result.Cost, Is.EqualTo(10));
            Assert.That(result.OriginYear, Is.EqualTo(1998));
            Assert.That(result.OriginTeamSeasonKey, Is.EqualTo("FRANCHISE_1998"));
        }

        [Test]
        public void Resolve_같은최고Cost면더최신연도를선택한다()
        {
            WorldCardCatalog catalog = CreateCatalog(
                new[] { 2024, 1998, 2018 },
                new[] { 4, 9, 9 },
                out string[] cardIds);

            PlayerSeasonDefinition result = OwnerStartingSeasonResolver.Resolve(cardIds, catalog);

            Assert.That(result.Cost, Is.EqualTo(9));
            Assert.That(result.OriginYear, Is.EqualTo(2018));
            Assert.That(result.OriginTeamSeasonKey, Is.EqualTo("FRANCHISE_2018"));
        }

        private static WorldCardCatalog CreateCatalog(
            int[] years,
            int[] costs,
            out string[] cardIds)
        {
            var seasons = new PlayerSeasonDefinition[years.Length];
            var cards = new PlayerCardDefinition[years.Length];
            cardIds = new string[years.Length];
            for (int index = 0; index < years.Length; index++)
            {
                string seasonId = $"STARTING_SEASON_{index}";
                string cardId = PlayerCardDefinition.CreateStableCardId(
                    seasonId,
                    PlayerCardEdition.Normal);
                var ratings = new AbilityRatings(50);
                seasons[index] = new PlayerSeasonDefinition(
                    seasonId,
                    $"STARTING_PERSON_{index}",
                    years[index],
                    "FRANCHISE",
                    $"FRANCHISE_{years[index]}",
                    PlayerPosition.Shortstop,
                    PitcherRole.MiddleRelief,
                    PlayerType.Batter,
                    RegistrationType.Domestic,
                    ratings,
                    costs[index],
                    ratings);
                cards[index] = new PlayerCardDefinition(
                    cardId,
                    seasonId,
                    PlayerCardEdition.Normal,
                    new int[PlayerAbilityCatalog.AbilityCount]);
                cardIds[index] = cardId;
            }
            return new WorldCardCatalog(seasons, cards);
        }
    }
}
