using System;
using System.Collections.Generic;

namespace Baseball.Core.Historical
{
    /// <summary>
    /// 작전 카드 연구(상점 작전 카드 상품)의 계열 필터·등급 가중치·가격을 정의한다.
    /// <para>
    /// Signature 전술은 업적 해금 전용이라 연구 풀에 절대 포함하지 않는다. 그래서 가중치 배열은
    /// Normal/Rare/Special 세 칸만 받고, 코드가 아니라 구조로 그 규칙을 강제한다.
    /// </para>
    /// </summary>
    public sealed class TacticResearchPoolDefinition
    {
        /// <summary>연구로 얻을 수 있는 최고 등급이다. Signature는 여기에 포함되지 않는다.</summary>
        public const int ResearchableTierCount = 3;

        private readonly double[] _tierWeights;

        public TacticResearchPoolDefinition(
            string researchPoolId,
            IReadOnlyList<double> tierWeights,
            long priceMoney,
            TacticCardCategory? categoryFilter = null)
        {
            if (string.IsNullOrWhiteSpace(researchPoolId))
                throw new ArgumentException("ResearchPoolId는 비어 있을 수 없습니다.", nameof(researchPoolId));
            if (priceMoney < 0L)
                throw new ArgumentOutOfRangeException(nameof(priceMoney));

            ResearchPoolId = researchPoolId.Trim();
            PriceMoney = priceMoney;
            CategoryFilter = categoryFilter;
            _tierWeights = CopyTierWeights(tierWeights);
        }

        public string ResearchPoolId { get; }
        public long PriceMoney { get; }

        /// <summary>null이면 전 계열을 연구 대상으로 삼는다.</summary>
        public TacticCardCategory? CategoryFilter { get; }

        public double GetTierWeight(TacticTier tier)
        {
            if (tier == TacticTier.Signature)
                return 0d;
            return _tierWeights[(int)tier];
        }

        public bool Accepts(TacticCardDefinition card)
        {
            if (card == null)
                throw new ArgumentNullException(nameof(card));
            if (card.TacticTier == TacticTier.Signature)
                return false;
            return !CategoryFilter.HasValue || card.Category == CategoryFilter.Value;
        }

        /// <summary>초기 밸런스. 상위 등급일수록 급격히 줄여 Special 연구가 사건처럼 느껴지게 한다.</summary>
        public static double[] CreateInitialTierWeights()
        {
            return new[] { 72d, 23d, 5d };
        }

        private static double[] CopyTierWeights(IReadOnlyList<double> weights)
        {
            if (weights == null || weights.Count != ResearchableTierCount)
            {
                throw new ArgumentException(
                    "Normal/Rare/Special 세 등급의 가중치가 필요합니다.", nameof(weights));
            }

            var copy = new double[ResearchableTierCount];
            double sum = 0d;
            for (int index = 0; index < ResearchableTierCount; index++)
            {
                double value = weights[index];
                if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
                    throw new ArgumentOutOfRangeException(nameof(weights));
                copy[index] = value;
                sum += value;
            }
            if (sum <= 0d)
                throw new ArgumentException("하나 이상의 양수 가중치가 필요합니다.", nameof(weights));
            return copy;
        }
    }

    /// <summary>연구 결과 등급 하나의 최종 확률이다. 필터로 비는 등급이 생기면 재정규화된 값이 담긴다.</summary>
    public readonly struct TacticResearchTierProbability
    {
        public TacticResearchTierProbability(TacticTier tier, double probability, int candidateCount)
        {
            Tier = tier;
            Probability = probability;
            CandidateCount = candidateCount;
        }

        public TacticTier Tier { get; }
        public double Probability { get; }
        public int CandidateCount { get; }
    }

    /// <summary>연구로 확보한 작전 카드 보유 상태다. 중복은 수량으로만 쌓인다.</summary>
    public sealed class TacticCollectionState
    {
        private readonly List<string> _cardIds;
        private readonly Dictionary<string, int> _countsByCardId;

        public TacticCollectionState(IReadOnlyList<string> cardIds = null)
        {
            _cardIds = new List<string>();
            _countsByCardId = new Dictionary<string, int>(StringComparer.Ordinal);
            if (cardIds == null)
                return;
            for (int index = 0; index < cardIds.Count; index++)
                Acquire(cardIds[index]);
        }

        /// <summary>보유 카드 Id다. 획득 순서를 유지해 표시 순서가 결정적으로 남는다.</summary>
        public IReadOnlyList<string> CardIds => _cardIds;

        public int GetCount(string cardId)
        {
            if (string.IsNullOrWhiteSpace(cardId))
                return 0;
            return _countsByCardId.TryGetValue(cardId.Trim(), out int count) ? count : 0;
        }

        public bool Contains(string cardId) => GetCount(cardId) > 0;

        /// <summary>중복 지정 수량까지 합산해 전체 전술을 소모할 수 있는지 상태 변경 없이 확인한다.</summary>
        public bool CanConsume(IReadOnlyList<string> cardIds)
        {
            if (cardIds == null) throw new ArgumentNullException(nameof(cardIds));
            for (int index = 0; index < cardIds.Count; index++)
            {
                if (string.IsNullOrWhiteSpace(cardIds[index])) return false;
                string id = cardIds[index].Trim();
                int requiredCount = 1;
                for (int previous = 0; previous < index; previous++)
                    if (string.Equals(cardIds[previous].Trim(), id, StringComparison.Ordinal)) requiredCount++;
                if (GetCount(id) < requiredCount) return false;
            }
            return true;
        }

        /// <summary>전체 수량이 충분할 때만 지정 전술을 일괄 소모해 일부 슬롯만 차감되지 않게 한다.</summary>
        public bool TryConsumeAll(IReadOnlyList<string> cardIds)
        {
            if (!CanConsume(cardIds)) return false;
            for (int index = 0; index < cardIds.Count; index++) TryConsume(cardIds[index]);
            return true;
        }

        /// <returns>처음 확보한 카드면 true다.</returns>
        public bool Acquire(string cardId)
        {
            if (string.IsNullOrWhiteSpace(cardId))
                throw new ArgumentException("CardId는 비어 있을 수 없습니다.", nameof(cardId));
            string id = cardId.Trim();
            if (_countsByCardId.TryGetValue(id, out int count))
            {
                _countsByCardId[id] = count + 1;
                return false;
            }
            _countsByCardId.Add(id, 1);
            _cardIds.Add(id);
            return true;
        }

        /// <summary>경기 확정에 사용한 카드 한 장을 보유 수량에서 제거한다.</summary>
        public bool TryConsume(string cardId)
        {
            if (string.IsNullOrWhiteSpace(cardId))
                return false;
            string id = cardId.Trim();
            if (!_countsByCardId.TryGetValue(id, out int count) || count <= 0)
                return false;
            if (count > 1)
            {
                _countsByCardId[id] = count - 1;
                return true;
            }

            _countsByCardId.Remove(id);
            _cardIds.Remove(id);
            return true;
        }
    }
}
