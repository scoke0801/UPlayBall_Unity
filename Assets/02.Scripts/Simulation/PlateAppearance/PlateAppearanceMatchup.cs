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
            int inning = 1,
            int buntAbilityBonus = 0)
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
            BuntAbility = Math.Max(0, Math.Min(AttributeRating.Maximum, batter.BatterAttributes.Bunt + buntAbilityBonus));
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
        /// <summary>컨디션·전술·반복 대면 보정을 상세와 간이 판정에 동일하게 전달한다.</summary>
        public double EffectiveContact => Math.Max(0d, Math.Min(AttributeRating.Maximum, Batter.BatterAttributes.Contact + BatterContactAdjustment));
        public int BuntAbility { get; }
        /// <summary>Power 능력치 점수가 아닌 강한 타구 확률의 가산량이다.</summary>
        public double HardHitAdjustment { get; }
        public PitchingApproach PitchingApproach { get; }
        public int Inning { get; }
    }
}
