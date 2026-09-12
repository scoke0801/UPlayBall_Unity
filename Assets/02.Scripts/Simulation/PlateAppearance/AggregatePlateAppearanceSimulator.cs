using System;
using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Simulation.Random;

namespace Baseball.Simulation.PlateAppearance
{
    /// <summary>투구 선택·궤적·스윙 루프 없이 타석의 종료 유형과 투구 소모를 직접 추첨한다.</summary>
    public sealed class AggregatePlateAppearanceSimulator
    {
        private readonly BalanceTable _balance;
        private readonly IRandomSource _random;

        public AggregatePlateAppearanceSimulator(BalanceTable balance, IRandomSource random)
        {
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        /// <summary>None은 공정 타구이며 공통 타구·수비·주루 경로에서 최종 결과를 정한다.</summary>
        public PlateAppearanceOutcome Simulate(in PlateAppearanceMatchup matchup, BattingApproach approach)
        {
            AggregateMatchBalance tuning = _balance.AggregateMatch;
            BatterAttributes batter = matchup.Batter.BatterAttributes;
            BattingApproachModifier modifier = _balance.BattingApproach.GetModifier(approach);
            double contactAbility = approach == BattingApproach.Bunt
                ? matchup.BuntAbility : matchup.EffectiveContact;
            double platoon = matchup.Batter.BattingHand == Handedness.Switch ||
                             matchup.Batter.BattingHand != matchup.Pitcher.ThrowingHand
                ? _balance.PlateDiscipline.OppositeHandedContactBonus
                : -_balance.PlateDiscipline.SameHandedContactPenalty;
            // 50점 기준 오즈를 조정하고 함께 정규화해 극단 능력치에서도 확률 합계를 보장한다.
            double walk = tuning.WalkRate * Math.Exp(
                (50 - matchup.EffectiveControl) * tuning.ControlWalkWeight +
                (batter.Mental - 50) * tuning.MentalWalkWeight);
            double strikeout = tuning.StrikeoutRate * Math.Exp(
                (50 - contactAbility - platoon) * tuning.ContactStrikeoutWeight +
                (matchup.EffectiveStuff - 50) * tuning.StuffStrikeoutWeight +
                (matchup.EffectiveVelocity - 50) * tuning.VelocityStrikeoutWeight -
                modifier.ContactAdjustment);
            double hitByPitch = Math.Min(tuning.MaximumHitByPitchRate, tuning.HitByPitchRate * Math.Exp(
                (50 - matchup.EffectiveControl) * tuning.ControlHitByPitchWeight));
            double inPlay = 1 - tuning.WalkRate - tuning.StrikeoutRate - tuning.HitByPitchRate;
            double roll = _random.NextDouble() * (walk + strikeout + hitByPitch + inPlay);
            if (roll < walk)
                return new PlateAppearanceOutcome(PlateAppearanceResult.Walk, SamplePitchCount(4, tuning.WalkPitchMean), 4, 0);
            if (roll < walk + strikeout)
                return new PlateAppearanceOutcome(PlateAppearanceResult.Strikeout, SamplePitchCount(3, tuning.StrikeoutPitchMean), 0, 3);
            if (roll < walk + strikeout + hitByPitch)
                return new PlateAppearanceOutcome(PlateAppearanceResult.HitByPitch, SamplePitchCount(1, tuning.InPlayPitchMean), 0, 0);
            return new PlateAppearanceOutcome(PlateAppearanceResult.None, SamplePitchCount(1, tuning.InPlayPitchMean), 0, 0);
        }

        private int SamplePitchCount(int minimum, double mean)
        {
            // 종료 유형별 최소 투구를 지키면서 소모량의 분산을 남긴다. 경기 중 투구를 재생하지 않는다.
            double count = Math.Max(minimum, mean + (_random.NextDouble() - 0.5) * 2);
            int whole = (int)count;
            return whole + (_random.NextDouble() < count - whole ? 1 : 0);
        }
    }
}
