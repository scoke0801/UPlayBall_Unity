#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Reflection;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Game.Historical;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game.Historical
{
    /// <summary>개발 치트가 실제 구단주 저장 Aggregate에 정확한 수량만 지급하는지 검증한다.</summary>
    public sealed class OwnerCheatServiceTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void AcquireCards_WithExAndUniqueEdition_CompletesBatchAndRepeatedGrants(bool isLegend)
        {
            ManagerHistoricalRuntimeState runtime = CreateSpecialRuntime(isLegend, out string target, out string[] materials);
            var service = new OwnerCheatService();
            runtime.Wishlist.Add(target);
            Assert.Throws<ArgumentException>(() => runtime.AcquireCard(target));
            Assert.Throws<ArgumentException>(() => runtime.AcquireCard("SPECIAL-0:Ex"));

            OwnerCheatGrantResult first = service.AcquireCards(runtime, 2000, "FRANCHISE-00", 3);
            Assert.That(first.ItemCount, Is.EqualTo(7));
            Assert.That(first.NewCardCount, Is.EqualTo(2));
            Assert.That(first.SkippedCardCount, Is.EqualTo(2));
            Assert.That(runtime.TryGetOwnedCard(target, out var unique), Is.True);
            Assert.That(unique.DuplicateCount, Is.Zero);
            Assert.That(unique.IsLocked, Is.True);
            Assert.That(runtime.Wishlist.Contains(target), Is.False);
            Assert.That(runtime.TryGetOwnedCard("SPECIAL-0:Ex", out var ex), Is.True);
            Assert.That(ex.DuplicateCount, Is.EqualTo(2));

            OwnerCheatGrantResult repeated = service.AcquireCards(runtime, 2000, "FRANCHISE-00", 3);
            Assert.That(repeated.ItemCount, Is.EqualTo(6));
            Assert.That(repeated.SkippedCardCount, Is.EqualTo(3));
            Assert.That(unique.DuplicateCount, Is.Zero);
            Assert.That(ex.DuplicateCount, Is.EqualTo(5));
            Assert.That(service.AcquireCard(runtime, target).SkippedCardCount, Is.EqualTo(1));
            Assert.That(service.AcquireCard(runtime, " SPECIAL-0:Ex ").ItemCount, Is.EqualTo(1));
            Assert.That(service.AcquireAllCards(runtime).ItemCount, Is.EqualTo(runtime.WorldCardCatalog.Cards.Count - 1));
            Assert.That(runtime.CreateSpecialCardTransactionsSave(), Is.Empty);
            Assert.Throws<ArgumentException>(() => runtime.AcquireCard(target));
        }

        [Test]
        public void AcquireAllCards_PendingRecruit_PreservesReservationAndCanCommitAfterGrant()
        {
            ManagerHistoricalRuntimeState runtime = CreateSpecialRuntime(false, out string target, out string[] materials);
            runtime.ReserveSpecialRecruit("pending", target, materials);
            OwnerCheatGrantResult result = new OwnerCheatService().AcquireAllCards(runtime);
            Assert.That(result.SkippedCardCount, Is.EqualTo(1));
            Assert.That(runtime.TryGetOwnedCard(target, out _), Is.False);
            Assert.That(runtime.IsCardReserved(materials[0]), Is.True);
            Assert.That(runtime.CommitSpecialRecruit("pending"), Is.EqualTo(target));
        }

        private static ManagerHistoricalRuntimeState CreateSpecialRuntime(bool isLegend, out string target, out string[] materials)
        {
            var args = new object[] { null, null, isLegend };
            var method = typeof(ManagerHistoricalSaveTests).GetMethod("CreateSpecialCardRuntime",
                BindingFlags.Static | BindingFlags.NonPublic);
            var runtime = (ManagerHistoricalRuntimeState)method.Invoke(null, args);
            target = (string)args[0];
            materials = (string[])args[1];
            return runtime;
        }

        [Test]
        public void GetCardOrigins_YearChange_SelectsThatYearsTeamSeasonAndExcludesAbsentTeams()
        {
            ManagerHistoricalRuntimeState runtime = CreateRuntime();
            PlayerSeasonDefinition template = runtime.WorldCardCatalog.GetPlayerSeason(runtime.WorldCardCatalog.Cards[0]);
            PlayerSeasonDefinition[] seasons =
            {
                CreateSeason("past", 1989, "franchise-a"),
                CreateSeason("current", 1994, "franchise-a"),
                CreateSeason("teammate", 1994, "franchise-a"),
                CreateSeason("past-only", 1989, "franchise-b")
            };
            var cards = new List<PlayerCardDefinition>();
            for (int index = 0; index < seasons.Length; index++)
                cards.Add(new PlayerCardDefinition(seasons[index].PlayerSeasonId + ":Normal",
                    seasons[index].PlayerSeasonId, PlayerCardEdition.Normal, new int[PlayerAbilityCatalog.AbilityCount]));
            var catalog = new WorldCardCatalog(seasons, cards);

            IReadOnlyList<PlayerSeasonDefinition> current = OwnerCheatService.GetCardOrigins(catalog, 1994);
            Assert.That(current.Count, Is.EqualTo(1));
            Assert.That(current[0].OriginFranchiseId, Is.EqualTo("franchise-a"));
            Assert.That(current[0].OriginTeamSeasonKey, Is.EqualTo("franchise-a_1994"));
            IReadOnlyList<PlayerSeasonDefinition> past = OwnerCheatService.GetCardOrigins(catalog, 1989);
            Assert.That(past.Count, Is.EqualTo(2));
            Assert.That(past[0].OriginTeamSeasonKey, Is.EqualTo("franchise-a_1989"));
            Assert.That(OwnerCheatService.GetCardOrigins(catalog, 1990), Is.Empty);

            PlayerSeasonDefinition CreateSeason(string id, int year, string franchiseId) =>
                new PlayerSeasonDefinition(id, template.PlayerPersonId, year, franchiseId,
                    franchiseId + "_" + year, template.Position, template.PitcherRole, template.PlayerType,
                    template.RegistrationType, template.CreateBaseAttributes(), template.Cost, template.CreateTrainingCeiling());
        }

        [Test]
        public void IncreaseResources_ValidAmounts_UpdatesCanonicalEconomy()
        {
            ManagerHistoricalRuntimeState runtime = CreateRuntime();
            var service = new OwnerCheatService();
            long moneyBefore = runtime.Economy.Money;
            int scoutingBefore = runtime.Economy.ScoutingPoints;
            int developmentBefore = runtime.Economy.DevelopmentPoints;

            service.IncreaseResources(runtime, 123_000_000L, 456, 789);

            Assert.That(runtime.Economy.Money, Is.EqualTo(moneyBefore + 123_000_000L));
            Assert.That(runtime.Economy.ScoutingPoints, Is.EqualTo(scoutingBefore + 456));
            Assert.That(runtime.Economy.DevelopmentPoints, Is.EqualTo(developmentBefore + 789));
        }

        [Test]
        public void AcquireCards_YearAndFranchise_GrantsEveryMatchingEditionExactCount()
        {
            ManagerHistoricalRuntimeState runtime = CreateRuntime();
            var service = new OwnerCheatService();
            WorldCardCatalog catalog = runtime.WorldCardCatalog;
            PlayerSeasonDefinition target = catalog.GetPlayerSeason(catalog.Cards[0]);
            var before = CaptureOwnedCounts(runtime);
            int expectedDefinitions = CountCards(catalog, target.OriginYear, target.OriginFranchiseId);

            OwnerCheatGrantResult result = service.AcquireCards(
                runtime,
                target.OriginYear,
                target.OriginFranchiseId,
                3);

            Assert.That(result.DefinitionCount, Is.EqualTo(expectedDefinitions));
            Assert.That(result.ItemCount, Is.EqualTo(expectedDefinitions * 3));
            for (int index = 0; index < catalog.Cards.Count; index++)
            {
                PlayerCardDefinition card = catalog.Cards[index];
                PlayerSeasonDefinition season = catalog.GetPlayerSeason(card);
                bool matches = season.OriginYear == target.OriginYear &&
                               string.Equals(
                                   season.OriginFranchiseId,
                                   target.OriginFranchiseId,
                                   StringComparison.Ordinal);
                AssertOwnedDelta(runtime, card.CardId, before[card.CardId], matches ? 3 : 0);
            }
        }

        [Test]
        public void AcquireCardsByEdition_EditionAndOptionalYear_GrantsOnlyMatchingCards()
        {
            ManagerHistoricalRuntimeState runtime = CreateRuntime();
            var service = new OwnerCheatService();
            WorldCardCatalog catalog = runtime.WorldCardCatalog;
            PlayerCardDefinition sample = catalog.Cards[0];
            int year = catalog.GetPlayerSeason(sample).OriginYear;

            var before = CaptureOwnedCounts(runtime);
            OwnerCheatGrantResult yearLimited = service.AcquireCardsByEdition(runtime, sample.Edition, year, 2);
            int expectedYearLimited = 0;
            for (int index = 0; index < catalog.Cards.Count; index++)
            {
                PlayerCardDefinition card = catalog.Cards[index];
                bool matches = card.Edition == sample.Edition && catalog.GetPlayerSeason(card).OriginYear == year;
                if (matches) expectedYearLimited++;
                AssertOwnedDelta(runtime, card.CardId, before[card.CardId], matches ? 2 : 0);
            }
            Assert.That(yearLimited.DefinitionCount, Is.EqualTo(expectedYearLimited));

            before = CaptureOwnedCounts(runtime);
            OwnerCheatGrantResult allYears = service.AcquireCardsByEdition(runtime, sample.Edition, null, 1);
            int expectedAllYears = 0;
            for (int index = 0; index < catalog.Cards.Count; index++)
            {
                PlayerCardDefinition card = catalog.Cards[index];
                bool matches = card.Edition == sample.Edition;
                if (matches) expectedAllYears++;
                AssertOwnedDelta(runtime, card.CardId, before[card.CardId], matches ? 1 : 0);
            }
            Assert.That(allYears.DefinitionCount, Is.EqualTo(expectedAllYears));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => service.AcquireCardsByEdition(runtime, sample.Edition, 0, 1));
        }

        [Test]
        public void AcquireCard_SelectedCard_GrantsExactlyOneCopy()
        {
            ManagerHistoricalRuntimeState runtime = CreateRuntime();
            var service = new OwnerCheatService();
            PlayerCardDefinition selected = runtime.WorldCardCatalog.Cards[
                runtime.WorldCardCatalog.Cards.Count - 1];
            int duplicateCountBefore = runtime.TryGetOwnedCard(
                selected.CardId,
                out OwnedPlayerCardState owned)
                ? owned.DuplicateCount
                : -1;

            OwnerCheatGrantResult result = service.AcquireCard(runtime, selected.CardId);

            Assert.That(result.DefinitionCount, Is.EqualTo(1));
            Assert.That(result.ItemCount, Is.EqualTo(1));
            AssertOwnedDelta(runtime, selected.CardId, duplicateCountBefore, 1);
        }

        [Test]
        public void AcquireAllCards_GrantsOneOfEveryActiveWorldCard()
        {
            ManagerHistoricalRuntimeState runtime = CreateRuntime();
            var service = new OwnerCheatService();
            WorldCardCatalog catalog = runtime.WorldCardCatalog;
            var before = CaptureOwnedCounts(runtime);

            OwnerCheatGrantResult result = service.AcquireAllCards(runtime);

            Assert.That(result.DefinitionCount, Is.EqualTo(catalog.Cards.Count));
            Assert.That(result.ItemCount, Is.EqualTo(catalog.Cards.Count));
            for (int index = 0; index < catalog.Cards.Count; index++)
                AssertOwnedDelta(runtime, catalog.Cards[index].CardId, before[catalog.Cards[index].CardId], 1);
        }

        [Test]
        public void AcquireSkillBlocks_SpecificRarityAndAll_GrantsIndependentInstances()
        {
            ManagerHistoricalRuntimeState runtime = CreateRuntime();
            var service = new OwnerCheatService();
            SkillBlockDefinition[] definitions = BalanceTable.CreateDefault().Growth.SkillBlocks;
            Assert.That(definitions, Is.Not.Empty);
            SkillBlockDefinition selected = definitions[0];
            int before = runtime.PlayerGrowth.Inventory.Blocks.Count;

            OwnerCheatGrantResult single = service.AcquireSkillBlock(
                runtime,
                definitions,
                selected.BlockId);
            int rarityDefinitionCount = CountSkillBlocks(definitions, selected.Rarity);
            OwnerCheatGrantResult rarity = service.AcquireSkillBlocks(
                runtime,
                definitions,
                selected.Rarity,
                3);
            OwnerCheatGrantResult all = service.AcquireAllSkillBlocks(runtime, definitions, 2);

            Assert.That(single.ItemCount, Is.EqualTo(1));
            Assert.That(rarity.DefinitionCount, Is.EqualTo(rarityDefinitionCount));
            Assert.That(rarity.ItemCount, Is.EqualTo(rarityDefinitionCount * 3));
            Assert.That(all.DefinitionCount, Is.EqualTo(definitions.Length));
            Assert.That(all.ItemCount, Is.EqualTo(definitions.Length * 2));
            Assert.That(
                runtime.PlayerGrowth.Inventory.Blocks.Count,
                Is.EqualTo(before + 1 + rarity.ItemCount + all.ItemCount));
            Assert.That(CountInstances(runtime.PlayerGrowth.Inventory.Blocks, selected.BlockId), Is.GreaterThanOrEqualTo(6));
        }

        private static Dictionary<string, int> CaptureOwnedCounts(ManagerHistoricalRuntimeState runtime)
        {
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            IReadOnlyList<PlayerCardDefinition> cards = runtime.WorldCardCatalog.Cards;
            for (int index = 0; index < cards.Count; index++)
            {
                result[cards[index].CardId] = runtime.TryGetOwnedCard(
                    cards[index].CardId,
                    out OwnedPlayerCardState owned)
                    ? owned.DuplicateCount
                    : -1;
            }
            return result;
        }

        private static void AssertOwnedDelta(
            ManagerHistoricalRuntimeState runtime,
            string cardId,
            int duplicateCountBefore,
            int acquiredCount)
        {
            bool isOwned = runtime.TryGetOwnedCard(cardId, out OwnedPlayerCardState owned);
            if (acquiredCount == 0)
            {
                Assert.That(isOwned, Is.EqualTo(duplicateCountBefore >= 0), cardId);
                if (isOwned) Assert.That(owned.DuplicateCount, Is.EqualTo(duplicateCountBefore), cardId);
                return;
            }

            Assert.That(isOwned, Is.True, cardId);
            int expected = duplicateCountBefore >= 0
                ? duplicateCountBefore + acquiredCount
                : acquiredCount - 1;
            Assert.That(owned.DuplicateCount, Is.EqualTo(expected), cardId);
        }

        private static int CountCards(WorldCardCatalog catalog, int originYear, string franchiseId)
        {
            int count = 0;
            for (int index = 0; index < catalog.Cards.Count; index++)
            {
                PlayerSeasonDefinition season = catalog.GetPlayerSeason(catalog.Cards[index]);
                if (season.OriginYear == originYear &&
                    string.Equals(season.OriginFranchiseId, franchiseId, StringComparison.Ordinal))
                    count++;
            }
            return count;
        }

        private static int CountSkillBlocks(
            IReadOnlyList<SkillBlockDefinition> definitions,
            SkillBlockRarity rarity)
        {
            int count = 0;
            for (int index = 0; index < definitions.Count; index++)
                if (definitions[index].Rarity == rarity) count++;
            return count;
        }

        private static int CountInstances(IReadOnlyList<SkillBlockInstance> instances, string definitionId)
        {
            int count = 0;
            for (int index = 0; index < instances.Count; index++)
                if (string.Equals(instances[index].DefinitionId, definitionId, StringComparison.Ordinal)) count++;
            return count;
        }

        private static ManagerHistoricalRuntimeState CreateRuntime()
        {
            Type fixtureType = typeof(ManagerHistoricalSaveTests).GetNestedType(
                "Fixture",
                BindingFlags.NonPublic);
            MethodInfo create = fixtureType.GetMethod("Create", BindingFlags.Static | BindingFlags.Public);
            object fixture = create.Invoke(null, new object[] { WorldRecordMode.SimulatedHistory, false });
            Type fixtureDataType = fixture.GetType();
            ManagerHistoricalRuntimeState original = (ManagerHistoricalRuntimeState)fixtureDataType
                .GetProperty("State", BindingFlags.Instance | BindingFlags.Public)
                .GetValue(fixture);
            ManagerHistoricalSaveAdapter adapter = (ManagerHistoricalSaveAdapter)fixtureDataType
                .GetMethod("CreateAdapter", BindingFlags.Instance | BindingFlags.Public)
                .Invoke(fixture, null);
            return adapter.Restore(adapter.CreateSaveData(original));
        }
    }
}
#endif
