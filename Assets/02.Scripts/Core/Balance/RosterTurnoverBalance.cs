using System;

namespace Baseball.Core.Balance
{
    /// <summary>
    /// 시즌 전환 시 다른 구단 로스터의 Overall이 포지션 필요도 기준선으로 얼마나 회귀하고,
    /// 얼마나 무작위로 흔들리는지를 정하는 계수를 보관한다.
    /// </summary>
    public readonly struct RosterTurnoverBalance
    {
        public RosterTurnoverBalance(double meanReversionWeight, double seasonDriftVariance)
        {
            if (meanReversionWeight < 0d || meanReversionWeight > 1d)
                throw new ArgumentOutOfRangeException(nameof(meanReversionWeight));
            if (seasonDriftVariance < 0d)
                throw new ArgumentOutOfRangeException(nameof(seasonDriftVariance));

            MeanReversionWeight = meanReversionWeight;
            SeasonDriftVariance = seasonDriftVariance;
        }

        public double MeanReversionWeight { get; }
        public double SeasonDriftVariance { get; }

        /// <summary>
        /// 한 시즌 사이 로스터 전력이 완만하게만 흔들리도록 맞춘 최초 검증용 값을 만든다.
        /// </summary>
        public static RosterTurnoverBalance CreateDefault()
        {
            return new RosterTurnoverBalance(meanReversionWeight: 0.15d, seasonDriftVariance: 6d);
        }
    }
}
