using System;
using Baseball.Core.Players;

namespace Baseball.Simulation.PlateAppearance
{
    /// <summary>
    /// 한 타석의 타자·투수·수비 입력을 불변 값으로 묶는다.
    /// </summary>
    public readonly struct PlateAppearanceMatchup
    {
        /// <summary>
        /// 타석 확률 계산에 필요한 입력을 생성한다.
        /// </summary>
        public PlateAppearanceMatchup(
            Player batter,
            Player pitcher,
            double defenseRating,
            bool hasRunnerInScoringPosition,
            int inning = 1)
            : this(
                batter,
                pitcher,
                defenseRating,
                hasRunnerInScoringPosition,
                pitcher?.PitcherAttributes.Velocity ?? 0d,
                pitcher?.PitcherAttributes.Stuff ?? 0d,
                pitcher?.PitcherAttributes.Breaking ?? 0d,
                pitcher?.PitcherAttributes.Control ?? 0d,
                pitcher?.PitcherAttributes.Mental ?? 0d,
                0d,
                0d,
                PitchingApproach.Balanced,
                inning)
        {
        }

        /// <summary>
        /// 피로·압박·타순 대면과 투구 방침이 반영된 현재 투구 능력치를 함께 고정한다.
        /// </summary>
        public PlateAppearanceMatchup(
            Player batter,
            Player pitcher,
            double defenseRating,
            bool hasRunnerInScoringPosition,
            double effectiveVelocity,
            double effectiveStuff,
            double effectiveBreaking,
            double effectiveControl,
            double effectiveMental,
            double batterContactAdjustment,
            double hardHitAdjustment,
            PitchingApproach pitchingApproach,
            int inning = 1)
        {
            Batter = batter ?? throw new ArgumentNullException(nameof(batter));
            Pitcher = pitcher ?? throw new ArgumentNullException(nameof(pitcher));
            DefenseRating = defenseRating;
            HasRunnerInScoringPosition = hasRunnerInScoringPosition;
            EffectiveVelocity = effectiveVelocity;
            EffectiveStuff = effectiveStuff;
            EffectiveBreaking = effectiveBreaking;
            EffectiveControl = effectiveControl;
            EffectiveMental = effectiveMental;
            BatterContactAdjustment = batterContactAdjustment;
            HardHitAdjustment = hardHitAdjustment;
            PitchingApproach = pitchingApproach;
            Inning = Math.Max(1, inning);
        }

        public Player Batter { get; }
        public Player Pitcher { get; }
        public double DefenseRating { get; }
        public bool HasRunnerInScoringPosition { get; }
        public double EffectiveVelocity { get; }
        public double EffectiveStuff { get; }
        public double EffectiveBreaking { get; }
        public double EffectiveControl { get; }
        public double EffectiveMental { get; }
        public double BatterContactAdjustment { get; }
        public double HardHitAdjustment { get; }
        public PitchingApproach PitchingApproach { get; }
        public int Inning { get; }
    }
}
