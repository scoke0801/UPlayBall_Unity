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
            ScoutPityBalanceTable pityBalance,
            Func<string, bool> isPlayerSeasonOwned = null)
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
                    case ShopProductKind.StudyReset:
                        return new ShopProductDetails(product.ProductId, "구매할 때 유학을 초기화할 선수를 지정합니다.",
                            Array.Empty<ShopProbabilityEntry>(), "완료한 모든 유학의 누적 능력치와 시즌 참가 제한을 초기화합니다. 일반 훈련·강화는 유지됩니다. 진행 중인 유학에는 사용할 수 없으며 이전 유학 비용은 반환하지 않습니다.");
                    case ShopProductKind.ConditionItem:
                        return new ShopProductDetails(product.ProductId, product.ScopeLabel + " · " + product.GradeLabel,
                            Array.Empty<ShopProbabilityEntry>(), "구매 즉시 등록 선수 전원에게 적용됩니다. 최대 100이며 전원이 100이면 결제하지 않습니다.");
                    case ShopProductKind.PlayerCardPack when product.Currency == ShopCurrency.ScoutPity:
                        return BuildGuaranteedDetails(
                            product,
                            FindScoutPool(scoutPools, product.SourceId),
                            scoutFeaturePolicy,
                            worldCardCatalog,
                            pityBalance,
                            isPlayerSeasonOwned ?? (_ => false));
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
                    pool.RosterScope == ScoutRosterScope.ActiveRoster ? "후보는 그 해 1군 25인입니다. " : string.Empty,
                    "스카우트에 쓴 SP만큼 보장 영입 게이지가 차며, ",
                    pity.Threshold.ToString("N0"), "에 도달하면 정밀 스카우트에서 보장 영입을 쓸 수 있습니다."));
        }

        private static ShopProductDetails BuildGuaranteedDetails(
            ShopProductDefinition product,
            ScoutPoolDefinition pool,
            ScoutFeaturePolicy featurePolicy,
            WorldCardCatalog catalog,
            ScoutPityBalanceTable pity,
            Func<string, bool> isPlayerSeasonOwned)
        {
            List<PlayerCardDefinition> candidates = ScoutRoller.CollectGuaranteedCandidates(
                pool, catalog, featurePolicy, pity, isPlayerSeasonOwned);
            string candidateLabel = DescribeGuaranteedStage(candidates, catalog, pity, isPlayerSeasonOwned);
            return new ShopProductDetails(
                product.ProductId,
                "보장 영입 게이지를 모두 써서 이 팀 1군 선수 1명을 확정 영입합니다.",
                new[] { new ShopProbabilityEntry(candidateLabel, 1d, candidates.Count) },
                string.Concat(
                    "아직 없는 Cost ", pity.GuaranteedMinimumCost.ToString(), " 이상 선수를 먼저 고르고, ",
                    "모두 가졌다면 아직 없는 1군 선수, 그것도 모두 가졌다면 Cost ",
                    pity.GuaranteedMinimumCost.ToString(), " 이상 선수 중에서 균등하게 뽑습니다."));
        }

        private static string DescribeGuaranteedStage(
            List<PlayerCardDefinition> candidates,
            WorldCardCatalog catalog,
            ScoutPityBalanceTable pity,
            Func<string, bool> isPlayerSeasonOwned)
        {
            if (candidates.Count == 0)
                return "후보 없음";
            PlayerSeasonDefinition first = catalog.GetPlayerSeason(candidates[0]);
            if (isPlayerSeasonOwned(first.PlayerSeasonId))
                return "1군 25인 모두 보유 · 중복 영입";
            return first.Cost >= pity.GuaranteedMinimumCost
                ? string.Concat("미보유 Cost ", pity.GuaranteedMinimumCost.ToString(), "+ 선수")
                : "미보유 1군 선수";
        }

        private static ShopProductDetails BuildSkillDetails(
            ShopProductDefinition product,
            SkillGachaBalanceTable skillGacha)
        {
            if (!Enum.TryParse(product.SourceId, out SkillGachaPurchaseTier tier))
                throw new InvalidOperationException("스킬 블록 상품의 SourceId가 올바르지 않습니다.");
            SkillGachaOfferBalance offer = skillGacha.GetOffer(tier);
            var probabilities = new ShopProbabilityEntry[SkillBlockGradeCatalog.Count];
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

        private static string DescribeRarity(SkillBlockRarity rarity) => SkillBlockGradeCatalog.GetLabel(rarity);

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
