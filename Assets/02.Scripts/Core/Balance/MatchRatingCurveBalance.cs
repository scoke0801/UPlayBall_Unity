using System;
using Baseball.Core.Historical;

namespace Baseball.Core.Balance
{
    /// <summary>표시 능력 분산과 경기 효과 분산을 독립적으로 저작한다.</summary>
    public sealed class MatchRatingCurveBalance
    {
        public MatchRatingCurveBalance(double center, double slope, EffectiveRatingCapTable caps = null,
            double inputOffset = 0d, double? pitcherSlope = null, double? pitcherInputOffset = null,
            double? upperSpreadStart = null, double? lowerSpreadEnd = null, double lowerSlope = .45d,
            double? pitcherUpperSpreadStart = null)
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
            if (upperSpreadStart.HasValue && (double.IsNaN(upperSpreadStart.Value) ||
                upperSpreadStart.Value <= Center || upperSpreadStart.Value >= Caps.SoftCap ||
                Center + (upperSpreadStart.Value - Center) * Slope + InputOffset >= 100d))
                throw new ArgumentOutOfRangeException(nameof(upperSpreadStart));
            UpperSpreadStart = upperSpreadStart;
            PitcherUpperSpreadStart = pitcherUpperSpreadStart ?? upperSpreadStart;
            if (PitcherUpperSpreadStart.HasValue && (double.IsNaN(PitcherUpperSpreadStart.Value) ||
                PitcherUpperSpreadStart.Value <= Center || PitcherUpperSpreadStart.Value >= Caps.SoftCap ||
                Center + (PitcherUpperSpreadStart.Value - Center) * PitcherSlope + PitcherInputOffset >= 100d))
                throw new ArgumentOutOfRangeException(nameof(pitcherUpperSpreadStart));
            if (lowerSpreadEnd.HasValue && (double.IsNaN(lowerSpreadEnd.Value) ||
                lowerSpreadEnd.Value <= Center || lowerSpreadEnd.Value >= Math.Min(
                    upperSpreadStart ?? Caps.SoftCap, PitcherUpperSpreadStart ?? Caps.SoftCap) ||
                double.IsNaN(lowerSlope) || lowerSlope <= 0d || lowerSlope > slope || lowerSlope > PitcherSlope))
                throw new ArgumentOutOfRangeException(nameof(lowerSpreadEnd));
            LowerSpreadEnd = lowerSpreadEnd;
            LowerSlope = lowerSlope;
        }
        public double Center { get; }
        public double Slope { get; }
        public EffectiveRatingCapTable Caps { get; }
        /// <summary>선수 간 격차와 독립적으로 리그의 평균 경기 입력을 보정한다.</summary>
        public double InputOffset { get; }
        public double PitcherSlope { get; }
        public double PitcherInputOffset { get; }
        /// <summary>고능력 구간은 경기 입력 상한까지 남은 성장 여유를 나누어 사용한다.</summary>
        public double? UpperSpreadStart { get; }
        public double? PitcherUpperSpreadStart { get; }
        public double? LowerSpreadEnd { get; }
        public double LowerSlope { get; }
        // 일반 카드 차이를 살리고 타자·투수의 상위 구간에는 강화·성장 여유를 보존한다.
        public static MatchRatingCurveBalance CreateDefault() => new MatchRatingCurveBalance(45d, 1.4d,
            inputOffset: -18.75d, pitcherSlope: 1.4d, pitcherInputOffset: -16.25d, upperSpreadStart: 80d,
            lowerSpreadEnd: 65d, lowerSlope: .45d, pitcherUpperSpreadStart: 85d);
    }
}
