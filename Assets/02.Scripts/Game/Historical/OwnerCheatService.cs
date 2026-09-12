#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using Baseball.Core.Growth;
using Baseball.Core.Historical;

namespace Baseball.Game.Historical
{
    /// <summary>개발 환경에서 지급한 카드·스킬 블록 수를 UI에 반환한다.</summary>
    public readonly struct OwnerCheatGrantResult
    {
        public OwnerCheatGrantResult(int definitionCount, int itemCount, int newCardCount = 0, int skippedCardCount = 0)
        {
            if (definitionCount < 0 || itemCount < 0 || newCardCount < 0 || newCardCount > itemCount || skippedCardCount < 0)
                throw new ArgumentOutOfRangeException(nameof(definitionCount));
            DefinitionCount = definitionCount;
            ItemCount = itemCount;
            NewCardCount = newCardCount;
            SkippedCardCount = skippedCardCount;
        }

        public int DefinitionCount { get; }
        public int ItemCount { get; }
        public int NewCardCount { get; }
        public int SkippedCardCount { get; }
        public int DuplicateCardCount => ItemCount - NewCardCount;
    }

    /// <summary>구단주 개발 치트를 실제 저장 Aggregate의 공개 변경 경로로 적용한다.</summary>
    public sealed class OwnerCheatService
    {
        /// <summary>지급 가능한 카드에서 선택 연도의 구단별 원본 시즌을 하나씩 반환한다.</summary>
        public static IReadOnlyList<PlayerSeasonDefinition> GetCardOrigins(WorldCardCatalog catalog, int originYear)
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));
            if (originYear <= 0)
                throw new ArgumentOutOfRangeException(nameof(originYear));

            var origins = new List<PlayerSeasonDefinition>();
            var franchiseIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < catalog.Cards.Count; index++)
            {
                PlayerSeasonDefinition season = catalog.GetPlayerSeason(catalog.Cards[index]);
                if (season.OriginYear == originYear && franchiseIds.Add(season.OriginFranchiseId))
                    origins.Add(season);
            }
            origins.Sort((left, right) => string.CompareOrdinal(left.OriginFranchiseId, right.OriginFranchiseId));
            return origins;
        }

        /// <summary>Money, SP, DP를 한 번에 검증한 뒤 증가시켜 일부만 적용되는 상태를 막는다.</summary>
        public void IncreaseResources(
            ManagerHistoricalRuntimeState runtime,
            long money,
            int scoutingPoints,
            int developmentPoints)
        {
            if (runtime == null)
                throw new ArgumentNullException(nameof(runtime));
            if (money < 0L)
                throw new ArgumentOutOfRangeException(nameof(money));
            if (scoutingPoints < 0)
                throw new ArgumentOutOfRangeException(nameof(scoutingPoints));
            if (developmentPoints < 0)
                throw new ArgumentOutOfRangeException(nameof(developmentPoints));
            if (money == 0L && scoutingPoints == 0 && developmentPoints == 0)
                throw new ArgumentException("하나 이상의 자원 증가량이 필요합니다.", nameof(money));

            ManagerEconomyState economy = runtime.Economy;
            if (money > long.MaxValue - economy.Money ||
                scoutingPoints > int.MaxValue - economy.ScoutingPoints ||
                developmentPoints > int.MaxValue - economy.DevelopmentPoints)
                throw new OverflowException("보유 자원이 저장 가능한 최대값을 넘습니다.");

            if (money > 0L) economy.AddMoney(money);
            if (scoutingPoints > 0) economy.AddScoutingPoints(scoutingPoints);
            if (developmentPoints > 0) economy.AddDevelopmentPoints(developmentPoints);
        }

        /// <summary>지정한 CardId 한 장을 보유 카드 또는 중복 재료로 지급한다.</summary>
        public OwnerCheatGrantResult AcquireCard(ManagerHistoricalRuntimeState runtime, string cardId)
        {
            RequireRuntime(runtime);
            if (string.IsNullOrWhiteSpace(cardId) || !runtime.WorldCardCatalog.TryGetCard(cardId.Trim(), out _))
                throw new ArgumentException("월드에 존재하는 CardId가 필요합니다.", nameof(cardId));
            string id = cardId.Trim();
            return AcquireMatchingCards(runtime, 1, (card, _) => string.Equals(card.CardId, id, StringComparison.Ordinal));
        }

        /// <summary>원 연도와 원 구단이 모두 일치하는 활성 카드 정의를 N장씩 지급한다.</summary>
        public OwnerCheatGrantResult AcquireCards(
            ManagerHistoricalRuntimeState runtime,
            int originYear,
            string originFranchiseId,
            int countPerCard)
        {
            RequireRuntime(runtime);
            if (originYear <= 0)
                throw new ArgumentOutOfRangeException(nameof(originYear));
            if (string.IsNullOrWhiteSpace(originFranchiseId))
                throw new ArgumentException("원 구단 ID가 필요합니다.", nameof(originFranchiseId));
            string franchiseId = originFranchiseId.Trim();
            return AcquireMatchingCards(
                runtime,
                countPerCard,
                (_, season) => season.OriginYear == originYear &&
                               string.Equals(season.OriginFranchiseId, franchiseId, StringComparison.Ordinal));
        }

        /// <summary>지정 Edition의 활성 카드 정의를 N장씩 지급한다. originYear가 null이면 모든 연도를 대상으로 한다.</summary>
        public OwnerCheatGrantResult AcquireCardsByEdition(
            ManagerHistoricalRuntimeState runtime,
            PlayerCardEdition edition,
            int? originYear,
            int countPerCard)
        {
            RequireRuntime(runtime);
            if (!Enum.IsDefined(typeof(PlayerCardEdition), edition))
                throw new ArgumentOutOfRangeException(nameof(edition));
            if (originYear.HasValue && originYear.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(originYear));
            return AcquireMatchingCards(
                runtime,
                countPerCard,
                (card, season) => card.Edition == edition &&
                                  (!originYear.HasValue || season.OriginYear == originYear.Value));
        }

        /// <summary>현재 월드에 활성화된 모든 카드 정의를 한 장씩 지급한다.</summary>
        public OwnerCheatGrantResult AcquireAllCards(ManagerHistoricalRuntimeState runtime)
        {
            RequireRuntime(runtime);
            return AcquireMatchingCards(runtime, 1, (_, __) => true);
        }

        /// <summary>지정한 정의의 스킬 블록 인스턴스 하나를 공유 인벤토리에 지급한다.</summary>
        public OwnerCheatGrantResult AcquireSkillBlock(
            ManagerHistoricalRuntimeState runtime,
            IReadOnlyList<SkillBlockDefinition> definitions,
            string definitionId)
        {
            RequireRuntime(runtime);
            SkillBlockDefinition definition = FindDefinition(definitions, definitionId);
            runtime.PlayerGrowth.Inventory.Add(definition.BlockId);
            return new OwnerCheatGrantResult(1, 1);
        }

        /// <summary>지정 등급의 모든 스킬 블록 정의를 N개씩 지급한다.</summary>
        public OwnerCheatGrantResult AcquireSkillBlocks(
            ManagerHistoricalRuntimeState runtime,
            IReadOnlyList<SkillBlockDefinition> definitions,
            SkillBlockRarity rarity,
            int countPerDefinition)
        {
            RequireRuntime(runtime);
            if (!Enum.IsDefined(typeof(SkillBlockRarity), rarity))
                throw new ArgumentOutOfRangeException(nameof(rarity));
            return AcquireMatchingSkillBlocks(
                runtime,
                definitions,
                countPerDefinition,
                definition => definition.Rarity == rarity);
        }

        /// <summary>모든 스킬 블록 정의를 N개씩 지급한다.</summary>
        public OwnerCheatGrantResult AcquireAllSkillBlocks(
            ManagerHistoricalRuntimeState runtime,
            IReadOnlyList<SkillBlockDefinition> definitions,
            int countPerDefinition)
        {
            RequireRuntime(runtime);
            return AcquireMatchingSkillBlocks(runtime, definitions, countPerDefinition, _ => true);
        }

        private static OwnerCheatGrantResult AcquireMatchingCards(
            ManagerHistoricalRuntimeState runtime,
            int countPerCard,
            Func<PlayerCardDefinition, PlayerSeasonDefinition, bool> predicate)
        {
            if (countPerCard <= 0)
                throw new ArgumentOutOfRangeException(nameof(countPerCard));

            var selected = new List<PlayerCardDefinition>();
            IReadOnlyList<PlayerCardDefinition> cards = runtime.WorldCardCatalog.Cards;
            for (int index = 0; index < cards.Count; index++)
            {
                PlayerCardDefinition card = cards[index];
                PlayerSeasonDefinition season = runtime.WorldCardCatalog.GetPlayerSeason(card);
                if (predicate(card, season))
                    selected.Add(card);
            }
            if (selected.Count == 0)
                return new OwnerCheatGrantResult(0, 0);

            int requestedCount = checked(selected.Count * countPerCard);
            int itemCount = 0;
            int definitionCount = 0;
            var grantCounts = new int[selected.Count];
            for (int index = 0; index < selected.Count; index++)
            {
                int grantCount = runtime.GetDevelopmentCardGrantCount(selected[index], countPerCard);
                grantCounts[index] = grantCount;
                itemCount += grantCount;
                if (grantCount == 0)
                    continue;
                definitionCount++;
                if (!runtime.TryGetOwnedCard(selected[index].CardId, out OwnedPlayerCardState owned))
                    continue;
                if (grantCount > int.MaxValue - owned.DuplicateCount)
                    throw new OverflowException("카드 중복 수가 저장 가능한 최대값을 넘습니다.");
            }

            int newCardCount = 0;
            for (int index = 0; index < selected.Count; index++)
            {
                for (int count = 0; count < grantCounts[index]; count++)
                {
                    if (runtime.AcquireCardForDevelopment(selected[index].CardId))
                        newCardCount++;
                }
            }
            return new OwnerCheatGrantResult(definitionCount, itemCount, newCardCount, requestedCount - itemCount);
        }

        private static OwnerCheatGrantResult AcquireMatchingSkillBlocks(
            ManagerHistoricalRuntimeState runtime,
            IReadOnlyList<SkillBlockDefinition> definitions,
            int countPerDefinition,
            Func<SkillBlockDefinition, bool> predicate)
        {
            RequireDefinitions(definitions);
            if (countPerDefinition <= 0)
                throw new ArgumentOutOfRangeException(nameof(countPerDefinition));

            int definitionCount = 0;
            for (int index = 0; index < definitions.Count; index++)
            {
                SkillBlockDefinition definition = definitions[index]
                    ?? throw new ArgumentException("null 스킬 블록 정의가 있습니다.", nameof(definitions));
                if (predicate(definition)) definitionCount++;
            }
            int itemCount = checked(definitionCount * countPerDefinition);
            if (itemCount > int.MaxValue - runtime.PlayerGrowth.Inventory.Blocks.Count)
                throw new OverflowException("스킬 블록 인벤토리가 저장 가능한 최대 크기를 넘습니다.");

            for (int index = 0; index < definitions.Count; index++)
            {
                SkillBlockDefinition definition = definitions[index];
                if (!predicate(definition)) continue;
                for (int count = 0; count < countPerDefinition; count++)
                    runtime.PlayerGrowth.Inventory.Add(definition.BlockId);
            }
            return new OwnerCheatGrantResult(definitionCount, itemCount);
        }

        private static SkillBlockDefinition FindDefinition(
            IReadOnlyList<SkillBlockDefinition> definitions,
            string definitionId)
        {
            RequireDefinitions(definitions);
            if (string.IsNullOrWhiteSpace(definitionId))
                throw new ArgumentException("스킬 블록 DefinitionId가 필요합니다.", nameof(definitionId));
            string id = definitionId.Trim();
            for (int index = 0; index < definitions.Count; index++)
            {
                SkillBlockDefinition definition = definitions[index]
                    ?? throw new ArgumentException("null 스킬 블록 정의가 있습니다.", nameof(definitions));
                if (string.Equals(definition.BlockId, id, StringComparison.Ordinal))
                    return definition;
            }
            throw new ArgumentException("존재하지 않는 스킬 블록 DefinitionId입니다.", nameof(definitionId));
        }

        private static void RequireDefinitions(IReadOnlyList<SkillBlockDefinition> definitions)
        {
            if (definitions == null)
                throw new ArgumentNullException(nameof(definitions));
        }

        private static void RequireRuntime(ManagerHistoricalRuntimeState runtime)
        {
            if (runtime == null)
                throw new ArgumentNullException(nameof(runtime));
        }
    }
}
#endif
