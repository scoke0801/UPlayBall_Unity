using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Shop;

namespace Baseball.Game.Shop
{
    /// <summary>
    /// 기존 밸런스(스킬 뽑기·Scout 풀·전술 연구 풀)를 상점 상품 목록으로 옮긴다.
    /// 가격과 확률의 원본은 언제나 각 밸런스이며, 상점은 값을 새로 만들지 않는다.
    /// </summary>
    public static class ShopCatalogBuilder
    {
        /// <summary>단품과 5회·10회 묶음이 붙어 보이도록 상품 계열별 정렬 구간을 둔다.</summary>
        private const int PlayerCardSortBase = 100;
        private const int SkillBlockSortBase = 200;
        private const int TacticSortBase = 300;

        public static ShopCatalog Build(
            SkillGachaBalanceTable skillGacha,
            IReadOnlyList<ScoutPoolDefinition> scoutPools,
            IReadOnlyList<TacticResearchPoolDefinition> tacticResearchPools,
            Func<string, string> franchiseDisplayNameResolver = null)
        {
            var products = new List<ShopProductDefinition>();
            AppendPlayerCardProducts(products, scoutPools, franchiseDisplayNameResolver);
            AppendSkillBlockProducts(products, skillGacha);
            AppendTacticProducts(products, tacticResearchPools);
            return new ShopCatalog(products);
        }

        private static void AppendPlayerCardProducts(
            List<ShopProductDefinition> products,
            IReadOnlyList<ScoutPoolDefinition> scoutPools,
            Func<string, string> franchiseDisplayNameResolver)
        {
            if (scoutPools == null)
                return;

            for (int index = 0; index < scoutPools.Count; index++)
            {
                ScoutPoolDefinition pool = scoutPools[index]
                    ?? throw new ArgumentException("null Scout 풀이 있습니다.", nameof(scoutPools));
                string franchiseName = ResolveFranchiseName(pool, franchiseDisplayNameResolver);
                products.Add(new ShopProductDefinition(
                    productId: "shop.player." + pool.ScoutPoolId,
                    kind: ShopProductKind.PlayerCardPack,
                    sourceId: pool.ScoutPoolId,
                    displayName: "선수 카드",
                    scopeLabel: DescribeScoutScope(pool, franchiseDisplayNameResolver),
                    gradeLabel: DescribeScoutPolicy(pool),
                    currency: ShopCurrency.ScoutingPoint,
                    price: pool.PriceSp,
                    badge: pool.ScoutType == ScoutType.General ? ShopProductBadge.None : ShopProductBadge.New,
                    isFeatured: pool.ScoutType == ScoutType.General,
                    sortOrder: PlayerCardSortBase + index * 2,
                    targetFranchiseId: pool.FranchiseFilter,
                    targetFranchiseName: franchiseName,
                    targetYear: pool.YearFilter));

                // 선수 Scout는 묶음에서도 같은 Pool을 순서대로 열어 확률과 Pity 계약을 그대로 유지한다.
                products.Add(new ShopProductDefinition(
                    productId: "shop.player." + pool.ScoutPoolId + ".x10",
                    kind: ShopProductKind.PlayerCardPack,
                    sourceId: pool.ScoutPoolId,
                    displayName: "선수 카드 10회",
                    scopeLabel: DescribeScoutScope(pool, franchiseDisplayNameResolver),
                    gradeLabel: DescribeScoutPolicy(pool),
                    currency: ShopCurrency.ScoutingPoint,
                    price: checked(pool.PriceSp * 10L),
                    drawCount: 10,
                    badge: ShopProductBadge.Best,
                    isFeatured: pool.ScoutType == ScoutType.General,
                    sortOrder: PlayerCardSortBase + index * 2 + 1,
                    targetFranchiseId: pool.FranchiseFilter,
                    targetFranchiseName: franchiseName,
                    targetYear: pool.YearFilter));
            }
        }

        private static void AppendSkillBlockProducts(
            List<ShopProductDefinition> products,
            SkillGachaBalanceTable skillGacha)
        {
            for (int tierIndex = 0; tierIndex <= (int)SkillGachaPurchaseTier.Legendary; tierIndex++)
            {
                var tier = (SkillGachaPurchaseTier)tierIndex;
                SkillGachaOfferBalance offer = skillGacha.GetOffer(tier);
                string tierName = DescribeSkillTier(tier);

                products.Add(new ShopProductDefinition(
                    productId: "shop.skill." + tier,
                    kind: ShopProductKind.SkillBlockPack,
                    sourceId: tier.ToString(),
                    displayName: "스킬 블록",
                    scopeLabel: "스킬블록 전체",
                    gradeLabel: tierName,
                    currency: ShopCurrency.Money,
                    price: offer.Price,
                    badge: ShopProductBadge.None,
                    isFeatured: tier == SkillGachaPurchaseTier.Normal,
                    maxPurchasesPerPeriod: offer.MaxPurchasesPerOffseason,
                    sortOrder: SkillBlockSortBase + tierIndex * 3));

                if (!offer.SupportsFivePull)
                    continue;

                // 5회 묶음은 실제로 할인가가 존재하므로 SALE 배지를 붙인다. 표시용 과장이 아니다.
                products.Add(new ShopProductDefinition(
                    productId: "shop.skill." + tier + ".x5",
                    kind: ShopProductKind.SkillBlockPack,
                    sourceId: tier.ToString(),
                    displayName: "스킬 블록 묶음",
                    scopeLabel: "스킬블록 " + tierName,
                    gradeLabel: "5회",
                    currency: ShopCurrency.Money,
                    price: skillGacha.GetFivePullPrice(tier),
                    drawCount: 5,
                    badge: ShopProductBadge.Sale,
                    isFeatured: tier == SkillGachaPurchaseTier.Normal,
                    maxPurchasesPerPeriod: offer.MaxPurchasesPerOffseason == 0
                        ? 0
                        : offer.MaxPurchasesPerOffseason / 5,
                    sortOrder: SkillBlockSortBase + tierIndex * 3 + 1));

                if (!offer.SupportsTenPull)
                    continue;

                // 10회 가격은 이미 검증된 5회 묶음 두 세트와 같아 별도 할인 수치를 만들지 않는다.
                products.Add(new ShopProductDefinition(
                    productId: "shop.skill." + tier + ".x10",
                    kind: ShopProductKind.SkillBlockPack,
                    sourceId: tier.ToString(),
                    displayName: "스킬 블록 10회",
                    scopeLabel: "스킬블록 " + tierName,
                    gradeLabel: "10회",
                    currency: ShopCurrency.Money,
                    price: checked(skillGacha.GetFivePullPrice(tier) * 2L),
                    drawCount: 10,
                    badge: ShopProductBadge.Best,
                    isFeatured: tier == SkillGachaPurchaseTier.Normal,
                    maxPurchasesPerPeriod: offer.MaxPurchasesPerOffseason == 0
                        ? 0
                        : offer.MaxPurchasesPerOffseason / 10,
                    sortOrder: SkillBlockSortBase + tierIndex * 3 + 2));
            }
        }

        private static void AppendTacticProducts(
            List<ShopProductDefinition> products,
            IReadOnlyList<TacticResearchPoolDefinition> tacticResearchPools)
        {
            if (tacticResearchPools == null)
                return;

            for (int index = 0; index < tacticResearchPools.Count; index++)
            {
                TacticResearchPoolDefinition pool = tacticResearchPools[index]
                    ?? throw new ArgumentException("null 전술 연구 풀이 있습니다.", nameof(tacticResearchPools));
                products.Add(new ShopProductDefinition(
                    productId: "shop.tactic." + pool.ResearchPoolId,
                    kind: ShopProductKind.TacticCardPack,
                    sourceId: pool.ResearchPoolId,
                    displayName: "작전 카드",
                    scopeLabel: DescribeTacticScope(pool),
                    gradeLabel: "연구",
                    currency: ShopCurrency.Money,
                    price: pool.PriceMoney,
                    badge: ShopProductBadge.None,
                    isFeatured: !pool.CategoryFilter.HasValue,
                    sortOrder: TacticSortBase + index * 2));

                products.Add(new ShopProductDefinition(
                    productId: "shop.tactic." + pool.ResearchPoolId + ".x10",
                    kind: ShopProductKind.TacticCardPack,
                    sourceId: pool.ResearchPoolId,
                    displayName: "작전 카드 10회",
                    scopeLabel: DescribeTacticScope(pool),
                    gradeLabel: "10회 연구",
                    currency: ShopCurrency.Money,
                    price: checked(pool.PriceMoney * 10L),
                    drawCount: 10,
                    badge: ShopProductBadge.Best,
                    isFeatured: !pool.CategoryFilter.HasValue,
                    sortOrder: TacticSortBase + index * 2 + 1));
            }
        }

        private static string DescribeScoutPolicy(ScoutPoolDefinition pool)
        {
            switch (pool.ScoutType)
            {
                case ScoutType.General: return "전 연도 균형";
                case ScoutType.Franchise: return "구단 집중";
                case ScoutType.Year: return "연도 집중";
                case ScoutType.YearFranchise: return "구단·연도 정밀";
                case ScoutType.Award: return "수상 경력 집중";
                default: throw new ArgumentOutOfRangeException(nameof(pool));
            }
        }

        private static string DescribeScoutScope(
            ScoutPoolDefinition pool,
            Func<string, string> franchiseDisplayNameResolver)
        {
            string franchise = ResolveFranchiseName(pool, franchiseDisplayNameResolver);
            switch (pool.ScoutType)
            {
                case ScoutType.General: return "전국";
                case ScoutType.Franchise: return franchise + " 연고 지역";
                case ScoutType.Year: return pool.YearFilter.Value + "년 전국";
                case ScoutType.YearFranchise: return franchise + " · " + pool.YearFilter.Value + "년";
                case ScoutType.Award: return "전국 수상 선수";
                default: throw new ArgumentOutOfRangeException(nameof(pool));
            }
        }

        private static string ResolveFranchiseName(
            ScoutPoolDefinition pool,
            Func<string, string> franchiseDisplayNameResolver)
        {
            if (string.IsNullOrWhiteSpace(pool.FranchiseFilter))
                return string.Empty;
            return franchiseDisplayNameResolver?.Invoke(pool.FranchiseFilter) ?? "연고 구단";
        }

        private static string DescribeSkillTier(SkillGachaPurchaseTier tier)
        {
            switch (tier)
            {
                case SkillGachaPurchaseTier.Normal: return "일반";
                case SkillGachaPurchaseTier.Rare: return "희귀";
                case SkillGachaPurchaseTier.Elite: return "정예";
                case SkillGachaPurchaseTier.Unique: return "고유";
                case SkillGachaPurchaseTier.Legendary: return "전설";
                default: throw new ArgumentOutOfRangeException(nameof(tier));
            }
        }

        private static string DescribeTacticScope(TacticResearchPoolDefinition pool)
        {
            if (!pool.CategoryFilter.HasValue)
                return "작전 카드 전체";
            switch (pool.CategoryFilter.Value)
            {
                case TacticCardCategory.Batting: return "공격 작전";
                case TacticCardCategory.Pitching: return "투수 작전";
                case TacticCardCategory.Analysis: return "분석 작전";
                case TacticCardCategory.Common: return "공용 작전";
                default: throw new ArgumentOutOfRangeException(nameof(pool));
            }
        }
    }
}
