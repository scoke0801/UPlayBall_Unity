using System;

namespace Baseball.Core.Growth
{
    /// <summary>
    /// 시즌 자연 성장에 쓰는 활용량 비율과 실제 역할별 능력치 배분이다.
    /// </summary>
    public sealed class SeasonUsageSummary
    {
        public SeasonUsageSummary(double usageRatio, AbilityWeight[] developmentWeights)
        {
            if (usageRatio < 0d)
                throw new ArgumentOutOfRangeException(nameof(usageRatio));
            UsageRatio = usageRatio;
            DevelopmentWeights = developmentWeights ?? throw new ArgumentNullException(nameof(developmentWeights));
            double sum = 0d;
            for (int index = 0; index < DevelopmentWeights.Length; index++)
                sum += DevelopmentWeights[index].Weight;
            if (Math.Abs(sum - 1d) > 0.000001d)
                throw new ArgumentException("자연 성장 배분 가중치 합은 1이어야 합니다.", nameof(developmentWeights));
        }

        public double UsageRatio { get; }
        public AbilityWeight[] DevelopmentWeights { get; }
    }
}
