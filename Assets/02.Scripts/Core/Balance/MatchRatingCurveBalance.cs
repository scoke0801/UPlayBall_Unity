using System;
using Baseball.Core.Historical;

namespace Baseball.Core.Balance
{
    /// <summary>표시 능력 분산과 경기 효과 분산을 독립적으로 저작한다.</summary>
    public sealed class MatchRatingCurveBalance
    {
        public MatchRatingCurveBalance(double center, double slope, EffectiveRatingCapTable caps = null)
        {
            if (double.IsNaN(center) || center < 1 || center > 100 ||
                double.IsNaN(slope) || slope <= 0 || slope > 1)
                throw new ArgumentOutOfRangeException(nameof(slope));
            Center = center; Slope = slope; Caps = caps ?? EffectiveRatingCapTable.CreateInitial();
        }
        public double Center { get; }
        public double Slope { get; }
        public EffectiveRatingCapTable Caps { get; }
        public static MatchRatingCurveBalance CreateDefault() => new MatchRatingCurveBalance(50d, 0.3d);
    }
}
