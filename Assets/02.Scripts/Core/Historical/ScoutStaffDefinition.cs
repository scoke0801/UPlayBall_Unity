using System;

namespace Baseball.Core.Historical
{
    /// <summary>스카우터의 비용과 코스트별 탐색 성향을 정의하는 읽기 전용 밸런스다.</summary>
    public sealed class ScoutStaffDefinition
    {
        public ScoutStaffDefinition(string id, string displayName, string description,
            double priceMultiplier, double costWeightStep)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("스카우터 식별자와 이름이 필요합니다.");
            if (double.IsNaN(priceMultiplier) || double.IsInfinity(priceMultiplier) || priceMultiplier <= 0)
                throw new ArgumentOutOfRangeException(nameof(priceMultiplier));
            if (double.IsNaN(costWeightStep) || double.IsInfinity(costWeightStep) || costWeightStep <= 0)
                throw new ArgumentOutOfRangeException(nameof(costWeightStep));
            Id = id;
            DisplayName = displayName;
            Description = description ?? string.Empty;
            PriceMultiplier = priceMultiplier;
            CostWeightStep = costWeightStep;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public double PriceMultiplier { get; }
        public double CostWeightStep { get; }
    }
}
