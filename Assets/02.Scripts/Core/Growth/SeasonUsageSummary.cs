using System;

namespace Baseball.Core.Growth
{
    /// <summary>
    /// 시즌 자연 성장에 쓰는 활용량 비율과 실제 역할별 능력치 배분이다.
    /// </summary>
    public sealed class SeasonUsageSummary
    {
        public SeasonUsageSummary(
            double usageRatio,
            AbilityWeight[] developmentWeights,
            bool isStarter = true,
            double competitorGap = 0d)
        {
            if (usageRatio < 0d)
                throw new ArgumentOutOfRangeException(nameof(usageRatio));
            UsageRatio = usageRatio;
            IsStarter = isStarter;
            CompetitorGap = Math.Max(0d, competitorGap);
            DevelopmentWeights = developmentWeights ?? throw new ArgumentNullException(nameof(developmentWeights));
            double sum = 0d;
            for (int index = 0; index < DevelopmentWeights.Length; index++)
                sum += DevelopmentWeights[index].Weight;
            if (Math.Abs(sum - 1d) > 0.000001d)
                throw new ArgumentException("자연 성장 배분 가중치 합은 1이어야 합니다.", nameof(developmentWeights));
        }

        public double UsageRatio { get; }
        public AbilityWeight[] DevelopmentWeights { get; }
        public bool IsStarter { get; }
        public double CompetitorGap { get; }

        public double GetCatchUpMultiplier(int age)
        {
            if (IsStarter || age > 30 || CompetitorGap <= 0d || CompetitorGap > 8d)
                return 1d;
            return 1d + Math.Min(0.20d, CompetitorGap * 0.025d);
        }

        /// <summary>포지션 일반 가중치보다 큰 핵심·보조 능력인지 판정한다.</summary>
        public bool IsCatchUpTarget(PlayerAbility ability)
        {
            double minimumWeight = double.MaxValue;
            double targetWeight = 0d;
            for (int index = 0; index < DevelopmentWeights.Length; index++)
            {
                minimumWeight = Math.Min(minimumWeight, DevelopmentWeights[index].Weight);
                if (DevelopmentWeights[index].Ability == ability)
                    targetWeight = DevelopmentWeights[index].Weight;
            }
            return targetWeight > minimumWeight + 0.000001d;
        }
    }
}
