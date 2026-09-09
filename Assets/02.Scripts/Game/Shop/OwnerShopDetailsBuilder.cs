using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Shop;
using Baseball.Simulation.Historical;

namespace Baseball.Game.Shop
{
    /// <summary>구단주 상점의 실제 후보군과 재정규화 확률을 상품 상세 조회로 만든다.</summary>
    public static class OwnerShopDetailsBuilder
    {
        public static IReadOnlyList<ShopProductDetails> Build(
            ShopCatalog catalog,
            SkillGachaBalanceTable skillGacha,
            IReadOnlyList<ScoutPoolDefinition> scoutPools,
            ScoutFeaturePolicy scoutFeaturePolicy,
            WorldCardCatalog worldCardCatalog,
            IReadOnlyList<TacticResearchPoolDefinition> tacticPools,
            IReadOnlyList<TacticCardDefinition> tacticCatalog,
            ScoutPityBalanceTable pityBalance)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            Func<ShopProductDefinition, ShopProductDetails> resolve = CreateResolver(
                skillGacha, scoutPools, scoutFeaturePolicy, worldCardCatalog,
                tacticPools, tacticCatalog, pityBalance);
            var details = new List<ShopProductDetails>(catalog.Products.Count);
            for (int index = 0; index < catalog.Products.Count; index++)
                details.Add(resolve(catalog.Products[index]));
            return details;
        }

        /// <summary>요청된 상품의 공개 확률만 계산하는 상점 수명 범위의 조회 함수를 만든다.</summary>
        public static Func<ShopProductDefinition, ShopProductDetails> CreateResolver(
            SkillGachaBalanceTable skillGacha,
            IReadOnlyList<ScoutPoolDefinition> scoutPools,
            ScoutFeaturePolicy scoutFeaturePolicy,
            WorldCardCatalog worldCardCatalog,
            IReadOnlyList<TacticResearchPoolDefinition> tacticPools,
            IReadOnlyList<TacticCardDefinition> tacticCatalog,
            ScoutPityBalanceTable pityBalance)
        {
            if (scoutPools == null) throw new ArgumentNullException(nameof(scoutPools));
            if (scoutFeaturePolicy == null) throw new ArgumentNullException(nameof(scoutFeaturePolicy));
            if (worldCardCatalog == null) throw new ArgumentNullException(nameof(worldCardCatalog));
            if (tacticPools == null) throw new ArgumentNullException(nameof(tacticPools));
            if (tacticCatalog == null) throw new ArgumentNullException(nameof(tacticCatalog));
            if (pityBalance == null) throw new ArgumentNullException(nameof(pityBalance));

            var scoutRoller = new ScoutRoller();
            var tacticRoller = new TacticResearchRoller();
            return product =>
            {
                if (product == null) throw new ArgumentNullException(nameof(product));
                switch (product.Kind)
                {
                    case ShopProductKind.ConditionItem:
                        return new ShopProductDetails(product.ProductId, product.ScopeLabel + " · " + product.GradeLabel,
                            Array.Empty<ShopProbabilityEntry>(), "구매 즉시 등록 선수 전원에게 적용됩니다. 최대 100이며 전원이 100이면 결제하지 않습니다.");
                    case ShopProductKind.PlayerCardPack:
                        return BuildScoutDetails(
                            product,
                            FindScoutPool(scoutPools, product.SourceId),
                            scoutFeaturePolicy,
                            worldCardCatalog,
                            scoutRoller,
                            pityBalance);
                    case ShopProductKind.SkillBlockPack:
                        return BuildSkillDetails(product, skillGacha);
                    case ShopProductKind.TacticCardPack:
                        return BuildTacticDetails(
                            product,
                            FindTacticPool(tacticPools, product.SourceId),
                            tacticCatalog,
                            tacticRoller);
                    default:
                        throw new ArgumentOutOfRangeException(nameof(product.Kind));
                }
            };
        }

        private static ShopProductDetails BuildScoutDetails(
            ShopProductDefinition product,
            ScoutPoolDefinition pool,
            ScoutFeaturePolicy featurePolicy,
            WorldCardCatalog catalog,
            ScoutRoller roller,
            ScoutPityBalanceTable pity)
        {
            IReadOnlyList<ScoutBucketProbability> source = roller.GetProbabilities(pool, catalog, featurePolicy);
            var probabilities = new ShopProbabilityEntry[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                ScoutBucketProbability entry = source[index];
                probabilities[index] = new ShopProbabilityEntry(
                    string.Concat("Cost ", entry.Cost.ToString(), " · ", DescribeEdition(entry.Edition)),
                    entry.Probability,
                    entry.CandidateCount);
            }
            return new ShopProductDetails(
                product.ProductId,
                string.Concat(
                    "현재 월드 카드 후보에서 Cost와 Edition을 함께 판정해 선수 카드 ",
                    product.DrawCount.ToString(),
                    "장을 영입합니다."),
                probabilities,
                string.Concat(
                    "각 결합 확률은 후보가 실제로 존재하는 Bucket만 재정규화한 값입니다. ",
                    "스카우트마다 Pity가 ", pity.GaugeGainPerScout.ToString(), " 증가하며 ",
                    pity.Threshold.ToString(), "에서 Cost ", pity.GuaranteedMinimumCost.ToString(), " 이상을 보장합니다."));
        }

        private static ShopProductDetails BuildSkillDetails(
            ShopProductDefinition product,
            SkillGachaBalanceTable skillGacha)
        {
            if (!Enum.TryParse(product.SourceId, out SkillGachaPurchaseTier tier))
                throw new InvalidOperationException("스킬 블록 상품의 SourceId가 올바르지 않습니다.");
            SkillGachaOfferBalance offer = skillGacha.GetOffer(tier);
            var probabilities = new ShopProbabilityEntry[5];
            for (int rarityIndex = 0; rarityIndex < probabilities.Length; rarityIndex++)
            {
                var rarity = (SkillBlockRarity)rarityIndex;
                probabilities[rarityIndex] = new ShopProbabilityEntry(
                    DescribeRarity(rarity), offer.GetProbability(rarity), 0);
            }
            return new ShopProductDetails(
                product.ProductId,
                product.DrawCount == 1
                    ? "선택한 보장 등급을 기준으로 스킬 블록 1개를 획득합니다."
                    : string.Concat(
                        "같은 공개 확률로 스킬 블록 ",
                        product.DrawCount.ToString(),
                        "개를 한 번에 획득합니다."),
                probabilities,
                "표시 확률은 각 뽑기의 등급 확률입니다. 획득한 블록은 카드훈련의 스킬 보드 인벤토리에 보관됩니다.");
        }

        private static ShopProductDetails BuildTacticDetails(
            ShopProductDefinition product,
            TacticResearchPoolDefinition pool,
            IReadOnlyList<TacticCardDefinition> catalog,
            TacticResearchRoller roller)
        {
            IReadOnlyList<TacticResearchTierProbability> source = roller.GetProbabilities(pool, catalog);
            var probabilities = new ShopProbabilityEntry[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                TacticResearchTierProbability entry = source[index];
                probabilities[index] = new ShopProbabilityEntry(
                    DescribeTacticTier(entry.Tier), entry.Probability, entry.CandidateCount);
            }
            return new ShopProductDetails(
                product.ProductId,
                pool.CategoryFilter.HasValue
                    ? string.Concat(
                        "선택한 계열에서 경기 작전에 사용할 카드 ",
                        product.DrawCount.ToString(),
                        "장을 연구합니다.")
                    : string.Concat(
                        "공격·투수·분석·공용 계열에서 작전 카드 ",
                        product.DrawCount.ToString(),
                        "장을 연구합니다."),
                probabilities,
                "후보가 없는 등급을 제외한 실제 재정규화 확률입니다. Signature 작전은 업적 해금 전용이라 상점에서 나오지 않습니다.");
        }

        private static ScoutPoolDefinition FindScoutPool(
            IReadOnlyList<ScoutPoolDefinition> pools,
            string sourceId)
        {
            for (int index = 0; index < pools.Count; index++)
                if (string.Equals(pools[index].ScoutPoolId, sourceId, StringComparison.Ordinal)) return pools[index];
            throw new InvalidOperationException("상점 상품에 연결된 Scout 풀을 찾을 수 없습니다.");
        }

        private static TacticResearchPoolDefinition FindTacticPool(
            IReadOnlyList<TacticResearchPoolDefinition> pools,
            string sourceId)
        {
            for (int index = 0; index < pools.Count; index++)
                if (string.Equals(pools[index].ResearchPoolId, sourceId, StringComparison.Ordinal)) return pools[index];
            throw new InvalidOperationException("상점 상품에 연결된 작전 연구 풀을 찾을 수 없습니다.");
        }

        private static string DescribeEdition(PlayerCardEdition edition)
        {
            return Baseball.Game.Historical.PlayerCardEditionText.Get(edition);
        }

        private static string DescribeRarity(SkillBlockRarity rarity)
        {
            return rarity switch
            {
                SkillBlockRarity.Normal => "일반",
                SkillBlockRarity.Rare => "희귀",
                SkillBlockRarity.Elite => "정예",
                SkillBlockRarity.Unique => "고유",
                SkillBlockRarity.Legendary => "전설",
                _ => throw new ArgumentOutOfRangeException(nameof(rarity))
            };
        }

        private static string DescribeTacticTier(TacticTier tier)
        {
            return tier switch
            {
                TacticTier.Normal => "일반",
                TacticTier.Rare => "희귀",
                TacticTier.Special => "특수",
                TacticTier.Signature => "시그니처",
                _ => throw new ArgumentOutOfRangeException(nameof(tier))
            };
        }
    }
}
