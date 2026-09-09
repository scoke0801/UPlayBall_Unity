using System;
using Baseball.Core.Historical;

namespace Baseball.Core.Balance
{
    /// <summary>표시 능력 분산과 경기 효과 분산을 독립적으로 저작한다.</summary>
    public sealed class MatchRatingCurveBalance
    {
        public MatchRatingCurveBalance(double center, double slope, EffectiveRatingCapTable caps = null,
            double inputOffset = 0d, double? pitcherSlope = null, double? pitcherInputOffset = null)
        {
            if (double.IsNaN(center) || center < 1 || center > 100 ||
                double.IsNaN(slope) || slope <= 0 || slope > 2)
                throw new ArgumentOutOfRangeException(nameof(slope));
            Center = center; Slope = slope; Caps = caps ?? EffectiveRatingCapTable.CreateInitial();
            if (double.IsNaN(inputOffset) || double.IsInfinity(inputOffset) || inputOffset < -100d || inputOffset > 100d)
                throw new ArgumentOutOfRangeException(nameof(inputOffset));
            InputOffset = inputOffset;
            PitcherSlope = pitcherSlope ?? slope;
            PitcherInputOffset = pitcherInputOffset ?? inputOffset;
            if (double.IsNaN(PitcherSlope) || PitcherSlope <= 0d || PitcherSlope > 2d ||
                double.IsNaN(PitcherInputOffset) || double.IsInfinity(PitcherInputOffset) ||
                PitcherInputOffset < -100d || PitcherInputOffset > 100d)
                throw new ArgumentOutOfRangeException(nameof(pitcherSlope));
        }
        public double Center { get; }
        public double Slope { get; }
        public EffectiveRatingCapTable Caps { get; }
        /// <summary>선수 간 격차와 독립적으로 리그의 평균 경기 입력을 보정한다.</summary>
        public double InputOffset { get; }
        public double PitcherSlope { get; }
        public double PitcherInputOffset { get; }
        // 원점수 70의 경기 입력 56을 유지하면서 선수 간 격차의 과도한 압축을 완화한다.
        public static MatchRatingCurveBalance CreateDefault() => new MatchRatingCurveBalance(45d, 0.45d);
    }
}
