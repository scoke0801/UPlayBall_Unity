using System;
using System.Collections.Generic;
using System.Linq;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Historical;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game.Historical
{
    public sealed class EncyclopediaCatalogServiceTests
    {
        [Test]
        public void Query_전체Archive와WorldCatalog을Core25제한없이노출한다()
        {
            Fixture fixture = Fixture.Create();

            IReadOnlyList<EncyclopediaPlayerSeasonEntry> seasons = fixture.Service.QueryPlayerSeasons();
            IReadOnlyList<EncyclopediaCardEntry> cards = fixture.Service.QueryCards();
            IReadOnlyList<EncyclopediaPlayerSeasonEntry> firstYear = fixture.Service.QueryPlayerSeasons(
                new EncyclopediaFilter { FranchiseId = "FR-A", OriginYear = 2000 });

            Assert.That(seasons.Select(entry => entry.PlayerSeasonId),
                Is.EquivalentTo(fixture.Content.PlayerSeasons.Select(season => season.PlayerSeasonId)));
            Assert.That(cards.Select(entry => entry.CardId),
                Is.EquivalentTo(fixture.Catalog.Cards.Select(card => card.CardId)));
            Assert.That(firstYear.Count, Is.EqualTo(26), "Core25 외 선수도 도감에 노출되어야 한다.");
        }

        [Test]
        public void Query_표시이름검색과Origin필터를AND로적용한다()
        {
            Fixture fixture = Fixture.Create();
            var filter = new EncyclopediaFilter
            {
                SearchText = "공유",
                FranchiseId = "FR-B",
                OriginYear = 2010,
                PlayerType = PlayerType.Batter,
                Position = PlayerPosition.Shortstop,
                MinimumCost = 7,
                MaximumCost = 7,
                RegistrationType = RegistrationType.Domestic,
                Bats = Handedness.Left,
                Throws = Handedness.Right
            };

            IReadOnlyList<EncyclopediaCardEntry> result = fixture.Service.QueryCards(filter);

            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].PlayerSeasonId, Is.EqualTo("PS-SHARED-2010"));
            Assert.That(result[0].DisplayName, Is.EqualTo("공유선수"));
            Assert.That(result[0].OriginFranchiseId, Is.EqualTo("FR-B"));
        }

        [Test]
        public void Timeline_같은Person의다른연도와이적Franchise를시간순으로보존한다()
        {
            Fixture fixture = Fixture.Create();

            IReadOnlyList<EncyclopediaPlayerTimelineEntry> timeline =
                fixture.Service.GetPlayerTimeline("PP-SHARED");

            Assert.That(timeline.Select(entry => entry.OriginYear), Is.EqualTo(new[] { 2000, 2010 }));
            Assert.That(timeline.Select(entry => entry.OriginFranchiseId), Is.EqualTo(new[] { "FR-A", "FR-B" }));
            Assert.That(timeline.Select(entry => entry.Season.DisplayName),
                Is.All.EqualTo("공유선수"));
        }

        [Test]
        public void Collection_현재보유획득이력위시를CardId별로분리한다()
        {
            Fixture fixture = Fixture.Create();

            EncyclopediaCardEntry owned = fixture.Service.GetCardDetail(fixture.OwnedCardId).Entry;
            EncyclopediaCardEntry acquiredNotOwned = fixture.Service.GetCardDetail(fixture.AllStarCardId).Entry;
            EncyclopediaCardEntry wishlisted = fixture.Service.GetCardDetail(fixture.WishlistCardId).Entry;
            IReadOnlyList<EncyclopediaCardEntry> acquiredFilter = fixture.Service.QueryCards(
                new EncyclopediaFilter { Collection = EncyclopediaCollectionFilter.AcquiredButNotOwned });

            Assert.That(owned.IsCurrentlyOwned, Is.True);
            Assert.That(owned.OwnedCount, Is.EqualTo(3));
            Assert.That(owned.WasEverAcquired, Is.True);
            Assert.That(acquiredNotOwned.IsCurrentlyOwned, Is.False);
            Assert.That(acquiredNotOwned.WasEverAcquired, Is.True);
            Assert.That(wishlisted.IsWishlisted, Is.True);
            Assert.That(wishlisted.WishlistAddedSequence, Is.EqualTo(4));
            Assert.That(acquiredFilter.Select(entry => entry.CardId), Does.Contain(fixture.AllStarCardId));
        }

        [Test]
        public void Progress_빈TeamSeason셀을N_A로구분하고Edition분모를Catalog에서집계한다()
        {
            Fixture fixture = Fixture.Create();

            EncyclopediaCollectionProgress progress = fixture.Service.GetCollectionProgress();
            FranchiseYearCollectionProgress existing = progress.FranchiseYears.Single(
                cell => cell.FranchiseId == "FR-A" && cell.OriginYear == 2000);
            FranchiseYearCollectionProgress absent = progress.FranchiseYears.Single(
                cell => cell.FranchiseId == "FR-A" && cell.OriginYear == 2010);
            EditionCollectionProgress normal = progress.Editions.Single(
                edition => edition.Edition == PlayerCardEdition.Normal);
            EditionCollectionProgress allStar = progress.Editions.Single(
                edition => edition.Edition == PlayerCardEdition.AllStar);

            Assert.That(existing.HasTeamSeason, Is.True);
            Assert.That(existing.PlayerSeasonCount, Is.EqualTo(26));
            Assert.That(absent.HasTeamSeason, Is.False);
            Assert.That(absent.CollectibleCardCount, Is.Zero);
            Assert.That(normal.CollectibleCardCount, Is.EqualTo(51));
            Assert.That(allStar.CollectibleCardCount, Is.EqualTo(1));
            Assert.That(progress.CollectibleCardCount, Is.EqualTo(52));
            Assert.That(progress.EverAcquiredCardCount, Is.EqualTo(2));
            Assert.That(progress.OwnedCardCount, Is.EqualTo(1));
            Assert.That(progress.WishlistCardCount, Is.EqualTo(1));
        }

        [Test]
        public void Sort_동일조건을반복해도CardId순서가일치한다()
        {
            Fixture fixture = Fixture.Create();
            var filter = new EncyclopediaFilter { OriginYear = 2000 };

            string[] first = fixture.Service.QueryCards(filter, EncyclopediaSort.CostDescending)
                .Select(entry => entry.CardId).ToArray();
            string[] second = fixture.Service.QueryCards(filter, EncyclopediaSort.CostDescending)
                .Select(entry => entry.CardId).ToArray();

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void Detail_WorldHistory의다른경기단계를제외하고정규시즌전체기록을사용한다()
        {
            Fixture fixture = Fixture.CreateWithHistoryPhases();

            EncyclopediaCardDetail detail = fixture.Service.GetCardDetail(fixture.OwnedCardId);

            Assert.That(detail.WorldStatistics, Is.Not.Null);
            Assert.That(detail.WorldStatistics.IsFirstHalf, Is.False);
            Assert.That(detail.WorldStatistics.IsPostseason, Is.False);
            Assert.That(detail.WorldStatistics.IsAllStarGame, Is.False);
            Assert.That(detail.WorldStatistics.PlateAppearances, Is.EqualTo(500));
        }

        private sealed class Fixture
        {
            private Fixture(
                HistoricalBakedContent content,
                WorldCardCatalog catalog,
                EncyclopediaCatalogService service,
                string ownedCardId,
                string allStarCardId,
                string wishlistCardId)
            {
                Content = content;
                Catalog = catalog;
                Service = service;
                OwnedCardId = ownedCardId;
                AllStarCardId = allStarCardId;
                WishlistCardId = wishlistCardId;
            }

            public HistoricalBakedContent Content { get; }
            public WorldCardCatalog Catalog { get; }
            public EncyclopediaCatalogService Service { get; }
            public string OwnedCardId { get; }
            public string AllStarCardId { get; }
            public string WishlistCardId { get; }

            public static Fixture Create()
            {
                return Create(Array.Empty<SeasonStatistics>());
            }

            public static Fixture CreateWithHistoryPhases()
            {
                return Create(new[]
                {
                    CreateStatistics(180, isFirstHalf: true),
                    CreateStatistics(500),
                    CreateStatistics(25, isPostseason: true),
                    CreateStatistics(3, isAllStarGame: true)
                });
            }

            private static Fixture Create(IReadOnlyList<SeasonStatistics> statistics)
            {
                var persons = new List<PlayerPersonDefinition>();
                var personIds = new HashSet<string>(StringComparer.Ordinal);
                HistoricalYearContentDefinition year2000 = CreateYear(
                    2000, "FR-A", "TEAM-A-2000", 26, persons, personIds, true);
                HistoricalYearContentDefinition year2010 = CreateYear(
                    2010, "FR-B", "TEAM-B-2010", 25, persons, personIds, true);
                HistoricalBakedContent content = new HistoricalBakedContent(
                    CreateManifest(),
                    persons,
                    new[] { year2000, year2010 });

                string shared2000 = "PS-SHARED-2000";
                string ownedCardId = PlayerCardDefinition.CreateStableCardId(
                    shared2000, PlayerCardEdition.Normal);
                string allStarCardId = PlayerCardDefinition.CreateStableCardId(
                    shared2000, PlayerCardEdition.AllStar);
                string wishlistCardId = PlayerCardDefinition.CreateStableCardId(
                    "PS-SHARED-2010", PlayerCardEdition.Normal);
                var cards = new List<PlayerCardDefinition>(content.NormalCards);
                cards.Add(new PlayerCardDefinition(
                    allStarCardId,
                    shared2000,
                    PlayerCardEdition.AllStar,
                    new int[PlayerAbilityCatalog.AbilityCount]));
                var catalog = new WorldCardCatalog(content.PlayerSeasons, cards, content.PlayerPersons);
                WorldIdentityRegistry identities = CreateIdentities(content.PlayerPersons);
                var history = new CardCollectionHistoryState(new[] { ownedCardId, allStarCardId });
                var wishlist = new WishlistState(
                    new[] { new WishlistEntry(wishlistCardId, 4L) },
                    5L);
                var ownedCards = new[] { new OwnedPlayerCardState(ownedCardId, duplicateCount: 2) };
                var worldHistory = new WorldHistorySnapshot(
                    WorldRecordMode.SimulatedHistory,
                    77UL,
                    statistics,
                    new WorldAwardRecord(Array.Empty<WorldAwardEntry>()));
                var service = new EncyclopediaCatalogService(
                    content, catalog, identities, worldHistory, ownedCards, history, wishlist);
                return new Fixture(content, catalog, service, ownedCardId, allStarCardId, wishlistCardId);
            }

            private static SeasonStatistics CreateStatistics(
                int plateAppearances,
                bool isFirstHalf = false,
                bool isPostseason = false,
                bool isAllStarGame = false)
            {
                return new SeasonStatistics(
                    "PS-SHARED-2000",
                    "TEAM-A-2000",
                    2000,
                    PlayerPosition.Shortstop,
                    plateAppearances: plateAppearances,
                    isFirstHalf: isFirstHalf,
                    isPostseason: isPostseason,
                    isAllStarGame: isAllStarGame);
            }

            private static HistoricalYearContentDefinition CreateYear(
                int year,
                string franchiseId,
                string teamSeasonKey,
                int playerCount,
                List<PlayerPersonDefinition> persons,
                HashSet<string> personIds,
                bool includeSharedPerson)
            {
                var seasons = new PlayerSeasonDefinition[playerCount];
                var cards = new PlayerCardDefinition[playerCount];
                var allNormalCardIds = new string[playerCount];
                var core25CardIds = new string[25];
                for (int index = 0; index < playerCount; index++)
                {
                    bool isShared = includeSharedPerson && index == 0;
                    string personId = isShared ? "PP-SHARED" : $"PP-{year}-{index:D2}";
                    string seasonId = isShared ? $"PS-SHARED-{year}" : $"PS-{year}-{index:D2}";
                    PlayerPosition position = isShared ? PlayerPosition.Shortstop : PlayerPosition.FirstBase;
                    int cost = isShared ? 7 : 5;
                    if (personIds.Add(personId))
                    {
                        persons.Add(new PlayerPersonDefinition(
                            personId,
                            1980,
                            isShared ? Handedness.Left : Handedness.Right,
                            Handedness.Right,
                            position,
                            RegistrationType.Domestic,
                            2000,
                            2010,
                            new PersonPotentialTrait(new int[PlayerAbilityCatalog.AbilityCount])));
                    }
                    seasons[index] = new PlayerSeasonDefinition(
                        seasonId,
                        personId,
                        year,
                        franchiseId,
                        teamSeasonKey,
                        position,
                        PitcherRole.Starter,
                        PlayerType.Batter,
                        RegistrationType.Domestic,
                        new AbilityRatings(50),
                        cost,
                        new AbilityRatings(70));
                    string cardId = PlayerCardDefinition.CreateStableCardId(
                        seasonId, PlayerCardEdition.Normal);
                    cards[index] = new PlayerCardDefinition(
                        cardId,
                        seasonId,
                        PlayerCardEdition.Normal,
                        new int[PlayerAbilityCatalog.AbilityCount]);
                    allNormalCardIds[index] = cardId;
                    if (index < core25CardIds.Length) core25CardIds[index] = cardId;
                }
                var team = new TeamSeasonDefinition(
                    teamSeasonKey,
                    franchiseId,
                    year,
                    allNormalCardIds,
                    core25CardIds,
                    50d);
                return new HistoricalYearContentDefinition(
                    year,
                    seasons,
                    cards,
                    new[] { team },
                    Array.Empty<OriginalSeasonRecordDefinition>(),
                    Array.Empty<OriginalAwardRecordDefinition>());
            }

            private static HistoricalContentManifest CreateManifest()
            {
                return new HistoricalContentManifest(
                    1,
                    1,
                    "archive-hash",
                    new HistoricalSourceContentManifest(
                        "reference-v1",
                        "generator-v1",
                        "balance-v1",
                        1UL,
                        "content-hash"));
            }

            private static WorldIdentityRegistry CreateIdentities(
                IReadOnlyList<PlayerPersonDefinition> persons)
            {
                var players = new WorldPlayerIdentity[persons.Count];
                for (int index = 0; index < persons.Count; index++)
                {
                    string displayName = persons[index].PlayerPersonId == "PP-SHARED"
                        ? "공유선수"
                        : "선수" + CreateLetters(index);
                    players[index] = new WorldPlayerIdentity(persons[index].PlayerPersonId, displayName);
                }
                return new WorldIdentityRegistry(
                    "identity-v1",
                    77UL,
                    players,
                    new[]
                    {
                        new WorldFranchiseIdentity("FR-A", "혜성"),
                        new WorldFranchiseIdentity("FR-B", "늑대")
                    });
            }

            private static string CreateLetters(int value)
            {
                char first = (char)('A' + value / 26);
                char second = (char)('A' + value % 26);
                return new string(new[] { first, second });
            }
        }
    }
}
