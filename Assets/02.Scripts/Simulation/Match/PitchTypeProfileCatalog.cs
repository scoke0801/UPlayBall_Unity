using System;
using Baseball.Core.Players;

namespace Baseball.Simulation.Match
{
    /// <summary>판정과 대표 궤적이 공유하는 구종의 고정 특성이다.</summary>
    public readonly struct PitchTypeProfile
    {
        internal PitchTypeProfile(
            double baseVelocityMph,
            double horizontalBreak,
            double verticalBreak,
            double breakStartTime01,
            double commandDifficulty,
            double horizontalErrorScale,
            double verticalErrorScale,
            double errorRotationDegrees,
            double fatigueCost)
        {
            BaseVelocityMph = baseVelocityMph;
            HorizontalBreak = horizontalBreak;
            VerticalBreak = verticalBreak;
            BreakStartTime01 = breakStartTime01;
            CommandDifficulty = commandDifficulty;
            HorizontalErrorScale = horizontalErrorScale;
            VerticalErrorScale = verticalErrorScale;
            ErrorRotationDegrees = errorRotationDegrees;
            FatigueCost = fatigueCost;
        }

        public double BaseVelocityMph { get; }
        public double HorizontalBreak { get; }
        public double VerticalBreak { get; }
        public double BreakStartTime01 { get; }
        public double CommandDifficulty { get; }
        public double HorizontalErrorScale { get; }
        public double VerticalErrorScale { get; }
        public double ErrorRotationDegrees { get; }
        public double FatigueCost { get; }
    }

    /// <summary>지원 중인 구종의 판정 수치를 값 변경 없이 한곳에서 제공한다.</summary>
    public static class PitchTypeProfileCatalog
    {
        /// <summary>지정한 구종의 판정 프로필을 반환한다.</summary>
        public static PitchTypeProfile Get(PitchType pitchType)
        {
            return pitchType switch
            {
                PitchType.FourSeamFastball => new PitchTypeProfile(
                    91d, 0.03d, 0.10d, 0.72d, -0.012d, 0.92d, 1.05d, 0d, 1d),
                PitchType.TwoSeamFastball => new PitchTypeProfile(
                    89d, 0.16d, -0.04d, 0.62d, 0d, 1.15d, 0.95d, 18d, 0.95d),
                PitchType.Cutter => new PitchTypeProfile(
                    87d, -0.13d, 0.01d, 0.68d, 0.012d, 1.10d, 0.94d, -15d, 1.02d),
                PitchType.Slider => new PitchTypeProfile(
                    83d, -0.31d, -0.15d, 0.60d, 0.025d, 1.22d, 1.02d, -22d, 1.05d),
                PitchType.Curveball => new PitchTypeProfile(
                    76d, -0.14d, -0.42d, 0.52d, 0.035d, 1.02d, 1.28d, 8d, 1.08d),
                PitchType.Changeup => new PitchTypeProfile(
                    82d, 0.11d, -0.17d, 0.64d, 0.018d, 1.12d, 1.06d, 14d, 0.90d),
                PitchType.Splitter => new PitchTypeProfile(
                    84d, 0.04d, -0.36d, 0.66d, 0.032d, 1.03d, 1.30d, 4d, 1.10d),
                PitchType.Sinker => new PitchTypeProfile(
                    88d, 0.18d, -0.23d, 0.61d, 0.018d, 1.18d, 1.10d, 20d, 1.03d),
                _ => throw new ArgumentOutOfRangeException(nameof(pitchType))
            };
        }
    }
}
