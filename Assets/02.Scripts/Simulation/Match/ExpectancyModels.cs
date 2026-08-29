using System;

namespace Baseball.Simulation.Match
{
    /// <summary>
    /// 3개 아웃 상태와 8개 주자 상태의 이닝 잔여 기대 득점을 제공한다.
    /// </summary>
    public sealed class RunExpectancy24
    {
        private readonly double[] _values;

        public RunExpectancy24(double[] values)
        {
            if (values == null || values.Length != 24)
                throw new ArgumentException("Run Expectancy는 정확히 24개 상태가 필요합니다.", nameof(values));
            _values = (double[])values.Clone();
        }

        public double Get(int outs, int occupancyMask)
        {
            if (outs < 0 || outs > 2) throw new ArgumentOutOfRangeException(nameof(outs));
            if (occupancyMask < 0 || occupancyMask > 7) throw new ArgumentOutOfRangeException(nameof(occupancyMask));
            return _values[outs * 8 + occupancyMask];
        }

        /// <summary>
        /// V2 대량 시뮬레이션을 처음 돌리기 전 의사결정 회귀를 위한 초기 테이블을 만든다.
        /// </summary>
        public static RunExpectancy24 CreateDefault()
        {
            return new RunExpectancy24(new[]
            {
                0.48d, 0.86d, 1.10d, 1.43d, 1.34d, 1.78d, 1.98d, 2.25d,
                0.25d, 0.51d, 0.66d, 0.91d, 0.92d, 1.16d, 1.37d, 1.55d,
                0.10d, 0.22d, 0.32d, 0.43d, 0.36d, 0.50d, 0.59d, 0.76d
            });
        }
    }

    /// <summary>
    /// 이닝·점수·주자·아웃 상태를 하나의 승리 기대값과 레버리지 단계로 변환한다.
    /// </summary>
    public sealed class WinExpectancyModel
    {
        private readonly RunExpectancy24 _runExpectancy;

        public WinExpectancyModel(RunExpectancy24 runExpectancy)
        {
            _runExpectancy = runExpectancy ?? throw new ArgumentNullException(nameof(runExpectancy));
        }

        public double GetWinExpectancy(
            int inning,
            InningHalf half,
            int offenseScoreDifference,
            int outs,
            int occupancyMask)
        {
            double expectedRuns = _runExpectancy.Get(outs, occupancyMask);
            double inningsRemaining = Math.Max(0.5d, 9.5d - inning - (half == InningHalf.Bottom ? 0.5d : 0d));
            double effectiveMargin = offenseScoreDifference + expectedRuns - 0.48d;
            double scale = 1.35d + Math.Sqrt(inningsRemaining) * 0.65d;
            return 1d / (1d + Math.Exp(-effectiveMargin / scale * 2.2d));
        }

        public LeverageTier GetLeverage(
            int inning,
            InningHalf half,
            int offenseScoreDifference,
            int outs,
            int occupancyMask)
        {
            double current = GetWinExpectancy(inning, half, offenseScoreDifference, outs, occupancyMask);
            double success = GetWinExpectancy(inning, half, offenseScoreDifference + 1, outs, occupancyMask);
            double failure = outs >= 2
                ? GetWinExpectancy(inning + (half == InningHalf.Bottom ? 1 : 0),
                    half == InningHalf.Top ? InningHalf.Bottom : InningHalf.Top,
                    -offenseScoreDifference,
                    0,
                    0)
                : GetWinExpectancy(inning, half, offenseScoreDifference, outs + 1, occupancyMask);
            double swing = Math.Abs(success - failure) + Math.Abs(current - 0.5d) * -0.12d;
            if (inning >= 8 && Math.Abs(offenseScoreDifference) <= 1 && swing >= 0.16d)
                return LeverageTier.Critical;
            if (swing >= 0.14d) return LeverageTier.High;
            if (swing >= 0.08d) return LeverageTier.Medium;
            return LeverageTier.Low;
        }
    }
}
