using System;
using System.Collections.Generic;

namespace Baseball.Core.Shop
{
    /// <summary>상품 상세 화면에 공개할 실제 결과군 하나의 확률과 후보 수다.</summary>
    public readonly struct ShopProbabilityEntry
    {
        public ShopProbabilityEntry(string label, double probability, int candidateCount)
        {
            if (string.IsNullOrWhiteSpace(label))
                throw new ArgumentException("확률 항목 이름은 비어 있을 수 없습니다.", nameof(label));
            if (double.IsNaN(probability) || double.IsInfinity(probability) || probability < 0d || probability > 1d)
                throw new ArgumentOutOfRangeException(nameof(probability));
            if (candidateCount < 0)
                throw new ArgumentOutOfRangeException(nameof(candidateCount));

            Label = label.Trim();
            Probability = probability;
            CandidateCount = candidateCount;
        }

        public string Label { get; }
        public double Probability { get; }
        public int CandidateCount { get; }
    }

    /// <summary>
    /// Simulation이 확정한 확률을 상점 상품과 연결한 조회 전용 상세다.
    /// Presentation은 이 값을 표시만 하고 원본 가중치를 다시 계산하지 않는다.
    /// </summary>
    public sealed class ShopProductDetails
    {
        private readonly ShopProbabilityEntry[] _probabilities;

        public ShopProductDetails(
            string productId,
            string summary,
            IReadOnlyList<ShopProbabilityEntry> probabilities,
            string notice)
        {
            if (string.IsNullOrWhiteSpace(productId))
                throw new ArgumentException("ProductId는 비어 있을 수 없습니다.", nameof(productId));
            if (string.IsNullOrWhiteSpace(summary))
                throw new ArgumentException("상품 설명은 비어 있을 수 없습니다.", nameof(summary));
            if (probabilities == null)
                throw new ArgumentNullException(nameof(probabilities));

            ProductId = productId.Trim();
            Summary = summary.Trim();
            Notice = notice == null ? string.Empty : notice.Trim();
            _probabilities = new ShopProbabilityEntry[probabilities.Count];
            for (int index = 0; index < probabilities.Count; index++)
                _probabilities[index] = probabilities[index];
        }

        public string ProductId { get; }
        public string Summary { get; }
        public IReadOnlyList<ShopProbabilityEntry> Probabilities => _probabilities;
        public string Notice { get; }
    }
}
