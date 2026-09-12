using System;

namespace Baseball.Core.Players
{
    /// <summary>
    /// 투수가 보유하거나 훈련으로 습득할 수 있는 구종을 정의한다.
    /// </summary>
    public enum PitchType
    {
        FourSeamFastball = 0,
        TwoSeamFastball = 1,
        Cutter = 2,
        Slider = 3,
        Curveball = 4,
        Changeup = 5,
        Splitter = 6,
        Sinker = 7,
        Sweeper = 8,
        Slurve = 9,
        KnuckleCurve = 10,
        CircleChangeup = 11,
        Forkball = 12,
        Screwball = 13,
        Knuckleball = 14
    }

    /// <summary>관측 기록과 합성된 구종 데이터의 출처를 구분한다.</summary>
    public enum PitchDataSourceKind { Synthetic, Observed }

    /// <summary>
    /// 생성 시 확정한 구종 하나와 초기 숙련도를 보관한다.
    /// </summary>
    public readonly struct PitchRepertoireEntry
    {
        public PitchRepertoireEntry(PitchType pitchType, int proficiency, bool isPrimary,
            double developmentAffinity = 1d, double usagePreference = 1d, double velocityOffset = 0d)
        {
            if (!Enum.IsDefined(typeof(PitchType), pitchType))
                throw new ArgumentOutOfRangeException(nameof(pitchType));
            PitchType = pitchType;
            if (proficiency < 0 || proficiency > 100)
                throw new ArgumentOutOfRangeException(nameof(proficiency));
            Proficiency = proficiency;
            IsPrimary = isPrimary;
            if (double.IsNaN(developmentAffinity) || double.IsInfinity(developmentAffinity) || developmentAffinity <= 0d)
                throw new ArgumentOutOfRangeException(nameof(developmentAffinity));
            if (double.IsNaN(usagePreference) || double.IsInfinity(usagePreference) || usagePreference <= 0d)
                throw new ArgumentOutOfRangeException(nameof(usagePreference));
            if (double.IsNaN(velocityOffset) || double.IsInfinity(velocityOffset) || Math.Abs(velocityOffset) > 20d)
                throw new ArgumentOutOfRangeException(nameof(velocityOffset));
            DevelopmentAffinity = developmentAffinity;
            UsagePreference = usagePreference;
            VelocityOffset = velocityOffset;
        }

        public PitchType PitchType { get; }
        public int Proficiency { get; }
        public bool IsPrimary { get; }
        public int BaseMastery => Proficiency;
        public double DevelopmentAffinity { get; }
        public double UsagePreference { get; }
        public double VelocityOffset { get; }
    }
}
