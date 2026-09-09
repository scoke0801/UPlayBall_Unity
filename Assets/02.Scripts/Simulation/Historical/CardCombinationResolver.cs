using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Simulation.Random;

namespace Baseball.Simulation.Historical
{
    /// <summary>기획 데이터가 재료 품질 구간별 결과 Cost·Edition 확률을 지정한다.</summary>
    public sealed class CardCombinationOutcome
    {
        public CardCombinationOutcome(int minimumMaterialCostSum, int cost, PlayerCardEdition edition, double weight)
        {
            if (minimumMaterialCostSum < 5 || minimumMaterialCostSum > 50 || cost < 1 || cost > 10)
                throw new ArgumentOutOfRangeException(nameof(cost));
            if (edition != PlayerCardEdition.Normal && edition != PlayerCardEdition.Rare && edition != PlayerCardEdition.Ex)
                throw new ArgumentException("선수 합성 결과는 Normal·Rare·EX만 허용합니다.");
            if (double.IsNaN(weight) || double.IsInfinity(weight) || weight <= 0d)
                throw new ArgumentOutOfRangeException(nameof(weight));
            MinimumMaterialCostSum = minimumMaterialCostSum;
            Cost = cost;
            Edition = edition;
            Weight = weight;
        }
        public int MinimumMaterialCostSum { get; }
        public int Cost { get; }
        public PlayerCardEdition Edition { get; }
        public double Weight { get; }
    }

    /// <summary>실재하는 사전 Bake 카드만 대상으로 재료 5장의 합성 결과를 결정한다.</summary>
    public static class CardCombinationResolver
    {
        public const int RequiredMaterialCount = 5;

        public static PlayerCardDefinition Roll(WorldCardCatalog catalog, IReadOnlyList<string> materials,
            IReadOnlyList<CardCombinationOutcome> outcomes, IRandomSource random)
        {
            if (catalog == null || outcomes == null || random == null) throw new ArgumentNullException();
            if (materials == null || materials.Count != RequiredMaterialCount)
                throw new ArgumentException("선수 합성 재료는 정확히 5장입니다.");
            int score = 0;
            foreach (string id in materials)
            {
                if (!catalog.TryGetCard(id, out PlayerCardDefinition card) ||
                    card.Edition == PlayerCardEdition.Ex || card.IsUniqueOwnedCard)
                    throw new ArgumentException("특수 카드는 일반 합성 재료로 사용할 수 없습니다.");
                score += catalog.GetPlayerSeason(card).Cost;
            }
            var ordered = new List<CardCombinationOutcome>(outcomes);
            foreach (var outcome in ordered)
                if (outcome == null) throw new ArgumentException("빈 합성 확률 행입니다.");
            ordered.Sort((a, b) => a.Cost != b.Cost ? a.Cost.CompareTo(b.Cost) :
                a.Edition != b.Edition ? a.Edition.CompareTo(b.Edition) :
                a.MinimumMaterialCostSum.CompareTo(b.MinimumMaterialCostSum));
            var buckets = new List<(CardCombinationOutcome outcome, List<PlayerCardDefinition> cards)>();
            double total = 0;
            foreach (var outcome in ordered)
            {
                if (score < outcome.MinimumMaterialCostSum) continue;
                var candidates = new List<PlayerCardDefinition>();
                foreach (var card in catalog.Cards)
                    if (card.Edition == outcome.Edition && catalog.GetPlayerSeason(card).Cost == outcome.Cost)
                        candidates.Add(card);
                if (candidates.Count == 0) continue;
                candidates.Sort((a, b) => string.CompareOrdinal(a.CardId, b.CardId));
                buckets.Add((outcome, candidates));
                total += outcome.Weight;
            }
            if (buckets.Count == 0 || double.IsInfinity(total))
                throw new InvalidOperationException("발급 가능한 합성 결과 확률이 없습니다.");
            double roll = NextUnit(random) * total;
            var selected = buckets[buckets.Count - 1];
            foreach (var bucket in buckets)
            {
                roll -= bucket.outcome.Weight;
                if (roll < 0) { selected = bucket; break; }
            }
            return selected.cards[(int)(NextUnit(random) * selected.cards.Count)];
        }

        private static double NextUnit(IRandomSource random)
        {
            double value = random.NextDouble();
            if (double.IsNaN(value) || value < 0 || value >= 1) throw new InvalidOperationException("난수 범위가 잘못되었습니다.");
            return value;
        }
    }
}
