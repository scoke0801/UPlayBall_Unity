using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Simulation.Random;

namespace Baseball.Simulation.Historical
{
    /// <summary>
    /// 작전 카드 연구 결과를 결정론적으로 뽑는다. 상점·연출은 이 결과를 표시만 한다.
    /// </summary>
    public sealed class TacticResearchRoller
    {
        private sealed class TierBucket
        {
            public TacticTier Tier;
            public double Weight;
            public readonly List<TacticCardDefinition> Cards = new List<TacticCardDefinition>();
        }

        /// <summary>
        /// 후보가 하나도 없는 등급을 제외하고 재정규화한 실제 확률을 돌려준다.
        /// UI는 이 값을 그대로 표시하고 원래 가중치를 다시 계산하지 않는다.
        /// </summary>
        public IReadOnlyList<TacticResearchTierProbability> GetProbabilities(
            TacticResearchPoolDefinition pool,
            IReadOnlyList<TacticCardDefinition> catalog)
        {
            List<TierBucket> buckets = BuildBuckets(pool, catalog);
            double totalWeight = 0d;
            for (int index = 0; index < buckets.Count; index++)
                totalWeight += buckets[index].Weight;

            var result = new TacticResearchTierProbability[buckets.Count];
            for (int index = 0; index < buckets.Count; index++)
            {
                TierBucket bucket = buckets[index];
                result[index] = new TacticResearchTierProbability(
                    bucket.Tier,
                    bucket.Weight / totalWeight,
                    bucket.Cards.Count);
            }
            return result;
        }

        public TacticCardDefinition Roll(
            TacticResearchPoolDefinition pool,
            IReadOnlyList<TacticCardDefinition> catalog,
            IRandomSource random)
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random));

            List<TierBucket> buckets = BuildBuckets(pool, catalog);
            double totalWeight = 0d;
            for (int index = 0; index < buckets.Count; index++)
                totalWeight += buckets[index].Weight;

            double roll = random.NextDouble() * totalWeight;
            TierBucket selected = buckets[buckets.Count - 1];
            double accumulated = 0d;
            for (int index = 0; index < buckets.Count; index++)
            {
                accumulated += buckets[index].Weight;
                if (roll < accumulated)
                {
                    selected = buckets[index];
                    break;
                }
            }

            int cardIndex = selected.Cards.Count == 1
                ? 0
                : Math.Min((int)(random.NextDouble() * selected.Cards.Count), selected.Cards.Count - 1);
            return selected.Cards[cardIndex];
        }

        private static List<TierBucket> BuildBuckets(
            TacticResearchPoolDefinition pool,
            IReadOnlyList<TacticCardDefinition> catalog)
        {
            if (pool == null)
                throw new ArgumentNullException(nameof(pool));
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));

            var buckets = new List<TierBucket>(TacticResearchPoolDefinition.ResearchableTierCount);
            // 등급 순서를 enum 값 순으로 고정해, 카탈로그 순회 순서가 결과에 영향을 주지 않게 한다.
            for (int tierIndex = 0; tierIndex < TacticResearchPoolDefinition.ResearchableTierCount; tierIndex++)
            {
                var tier = (TacticTier)tierIndex;
                double weight = pool.GetTierWeight(tier);
                if (weight <= 0d)
                    continue;
                buckets.Add(new TierBucket { Tier = tier, Weight = weight });
            }

            for (int index = 0; index < catalog.Count; index++)
            {
                TacticCardDefinition card = catalog[index]
                    ?? throw new ArgumentException("null 전술 카드가 있습니다.", nameof(catalog));
                if (!pool.Accepts(card))
                    continue;
                for (int bucketIndex = 0; bucketIndex < buckets.Count; bucketIndex++)
                {
                    if (buckets[bucketIndex].Tier != card.TacticTier)
                        continue;
                    buckets[bucketIndex].Cards.Add(card);
                    break;
                }
            }

            for (int index = buckets.Count - 1; index >= 0; index--)
            {
                if (buckets[index].Cards.Count == 0)
                    buckets.RemoveAt(index);
            }

            if (buckets.Count == 0)
                throw new InvalidOperationException("연구 풀에 뽑을 수 있는 전술 카드가 없습니다.");
            return buckets;
        }
    }
}
