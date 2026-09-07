#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Reflection;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Game.Historical;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game.Historical
{
    /// <summary>개발 치트가 실제 구단주 저장 Aggregate에 정확한 수량만 지급하는지 검증한다.</summary>
    public sealed class OwnerCheatServiceTests
    {
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
