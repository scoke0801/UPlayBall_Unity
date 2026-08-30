using System;

namespace Baseball.Simulation.Match
{
    /// <summary>Unity 좌표와 무관한 한 시점의 투구 궤적 위치다.</summary>
    public readonly struct PitchTrajectoryPoint
    {
        public PitchTrajectoryPoint(double x, double y, double depth01)
        {
            X = x;
            Y = y;
            Depth01 = depth01;
        }

        public double X { get; }
        public double Y { get; }
        public double Depth01 { get; }
    }

    /// <summary>투구마다 포인트 배열을 만들지 않고 Descriptor를 정규화 시간에서 평가한다.</summary>
    public static class PitchTrajectoryEvaluator
    {
        /// <summary>릴리스와 실제 PlatePoint를 정확히 보존하는 궤적 위치를 반환한다.</summary>
        public static PitchTrajectoryPoint Evaluate(in PitchFlightDescriptor pitch, double time01)
        {
            double time = Clamp01(time01);
            double breakStart = Clamp(pitch.BreakStartTime01, 0d, 0.98d);
            double breakProgress = time <= breakStart
                ? 0d
                : SmoothStep((time - breakStart) / (1d - breakStart));

            // 변화 전 투영점에서 실제 통과점으로 수렴시켜 늦은 변화를 표현하면서 끝점은 보존한다.
            double preBreakPlateX = pitch.PlatePoint.X - pitch.HorizontalBreak;
            double preBreakPlateY = pitch.PlatePoint.Y - pitch.VerticalBreak;
            double x = Lerp(pitch.ReleasePoint.X, preBreakPlateX, time) +
                       pitch.HorizontalBreak * breakProgress;
            double y = Lerp(pitch.ReleasePoint.Y, preBreakPlateY, time) +
                       pitch.VerticalBreak * breakProgress;
            return new PitchTrajectoryPoint(x, y, time);
        }

        private static double SmoothStep(double value)
        {
            double clamped = Clamp01(value);
            return clamped * clamped * (3d - 2d * clamped);
        }

        private static double Lerp(double from, double to, double time) => from + (to - from) * time;

        private static double Clamp01(double value) => Clamp(value, 0d, 1d);

        private static double Clamp(double value, double minimum, double maximum)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(nameof(value));
            if (value < minimum) return minimum;
            if (value > maximum) return maximum;
            return value;
        }
    }
}
