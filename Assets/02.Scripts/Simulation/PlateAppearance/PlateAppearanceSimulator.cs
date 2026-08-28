using System;
using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Core.Rules;
using Baseball.Simulation.Random;

namespace Baseball.Simulation.PlateAppearance
{
    /// <summary>
    /// 주입받은 밸런스와 RNG로 Pitch Count부터 타구 결과까지 계산한다.
    /// </summary>
    public sealed class PlateAppearanceSimulator : IPlateAppearanceSimulator
    {
        private readonly BalanceTable _balance;
        private readonly IRandomSource _random;

        /// <summary>
        /// 타석 시뮬레이터를 순수 데이터와 결정론적 RNG로 구성한다.
        /// </summary>
        public PlateAppearanceSimulator(BalanceTable balance, IRandomSource random)
        {
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        /// <summary>
        /// 이벤트 수집 없이 한 타석의 최종 결과만 계산한다.
        /// </summary>
        public PlateAppearanceOutcome Simulate(in PlateAppearanceMatchup matchup)
        {
            int balls = 0;
            int strikes = 0;
            int pitchCount = 0;

            while (true)
            {
                pitchCount++;
                PitchResult pitchResult = SimulatePitch(
                    matchup,
                    balls,
                    strikes,
                    pitchCount,
                    BattingApproach.Balanced);
                switch (pitchResult)
                {
                    case PitchResult.Ball:
                        balls++;
                        if (balls >= BaseballRules.BallsForWalk)
                            return new PlateAppearanceOutcome(PlateAppearanceResult.Walk, pitchCount, balls, strikes);
                        break;

                    case PitchResult.CalledStrike:
                    case PitchResult.SwingingStrike:
                        strikes++;
                        if (strikes >= BaseballRules.StrikesForStrikeout)
                            return new PlateAppearanceOutcome(PlateAppearanceResult.Strikeout, pitchCount, balls, strikes);
                        break;

                    case PitchResult.Foul:
                        if (strikes < BaseballRules.StrikesForStrikeout - 1)
                            strikes++;
                        break;

                    case PitchResult.HitByPitch:
                        return new PlateAppearanceOutcome(
                            PlateAppearanceResult.HitByPitch,
                            pitchCount,
                            balls,
                            strikes);

                    case PitchResult.InPlay:
                        return new PlateAppearanceOutcome(
                            ResolveBallInPlay(matchup, BattingApproach.Balanced),
                            pitchCount,
                            balls,
                            strikes);

                    default:
                        throw new InvalidOperationException("지원하지 않는 PitchResult입니다.");
                }
            }
        }

        /// <summary>
        /// 현재 Count에서 다음 투구 결과를 계산한다.
        /// </summary>
        public PitchResult SimulatePitch(
            in PlateAppearanceMatchup matchup,
            int balls,
            int strikes,
            int pitchNumber,
            BattingApproach approach)
        {
            PlateDisciplineBalance tuning = _balance.PlateDiscipline;
            BattingApproachModifier approachModifier = _balance.BattingApproach.GetModifier(approach);
            BatterAttributes batter = matchup.Batter.BatterAttributes;
            PitcherAttributes pitcher = matchup.Pitcher.PitcherAttributes;

            double mentalAdjustment = matchup.HasRunnerInScoringPosition
                ? (pitcher.Mental - 50d) * 0.15d
                : 0d;
            double effectiveControl = pitcher.Control + mentalAdjustment;
            double effectiveStuff = pitcher.Stuff + mentalAdjustment;
            double effectiveVelocity = pitcher.Velocity + mentalAdjustment;
            double strikeZoneProbability = ClampProbability(
                tuning.StrikeZoneProbability +
                (effectiveControl - 50d) * tuning.ControlStrikeZoneWeight);

            bool isStrike = _random.NextDouble() < strikeZoneProbability;
            if (!isStrike)
            {
                return ResolvePitchOutsideZone(
                    tuning,
                    batter,
                    effectiveControl,
                    effectiveStuff,
                    effectiveVelocity,
                    matchup,
                    pitchNumber,
                    approachModifier);
            }

            double swingProbability = ClampProbability(
                tuning.StrikeSwingProbability +
                (batter.Mental - 50d) * tuning.MentalStrikeSwingWeight +
                approachModifier.StrikeSwingAdjustment);
            if (_random.NextDouble() >= swingProbability)
                return PitchResult.CalledStrike;

            double contactProbability = CalculateContactProbability(
                tuning.StrikeContactProbability,
                tuning,
                matchup,
                effectiveStuff,
                effectiveVelocity,
                approachModifier.ContactAdjustment);
            return ResolveSwing(
                contactProbability,
                tuning.FairContactProbability + approachModifier.FairContactAdjustment,
                pitchNumber);
        }

        /// <summary>
        /// 공정 타구가 된 Contact의 최종 결과를 계산한다.
        /// </summary>
        public PlateAppearanceResult ResolveBallInPlay(
            in PlateAppearanceMatchup matchup,
            BattingApproach approach)
        {
            BattedBallBalance tuning = _balance.BattedBall;
            BattingApproachModifier approachModifier = _balance.BattingApproach.GetModifier(approach);
            BatterAttributes batter = matchup.Batter.BatterAttributes;
            PitcherAttributes pitcher = matchup.Pitcher.PitcherAttributes;

            double homeRunProbability = Clamp(
                tuning.HomeRunProbability +
                (batter.Power - 50d) * tuning.PowerHomeRunWeight -
                (pitcher.Breaking - 50d) * tuning.BreakingHomeRunWeight +
                approachModifier.HomeRunAdjustment,
                0.005d,
                0.16d);
            if (_random.NextDouble() < homeRunProbability)
                return PlateAppearanceResult.HomeRun;

            double hitProbability = Clamp(
                tuning.NonHomeRunHitProbability +
                (batter.Contact - 50d) * tuning.ContactHitWeight -
                (pitcher.Breaking - 50d) * tuning.BreakingHitWeight -
                (matchup.DefenseRating - 50d) * tuning.DefenseHitWeight +
                approachModifier.NonHomeRunHitAdjustment,
                0.12d,
                0.48d);
            if (_random.NextDouble() < hitProbability)
                return ResolveHitType(tuning, batter, pitcher, approachModifier);

            double groundOutProbability = ClampProbability(
                tuning.GroundOutShare +
                (pitcher.Breaking - 50d) * tuning.BreakingGroundOutWeight -
                (batter.Power - 50d) * tuning.PowerGroundOutWeight);
            return _random.NextDouble() < groundOutProbability
                ? PlateAppearanceResult.GroundOut
                : PlateAppearanceResult.FlyOut;
        }

        private PitchResult ResolvePitchOutsideZone(
            PlateDisciplineBalance tuning,
            BatterAttributes batter,
            double effectiveControl,
            double effectiveStuff,
            double effectiveVelocity,
            in PlateAppearanceMatchup matchup,
            int pitchNumber,
            BattingApproachModifier approachModifier)
        {
            // 사구는 타자가 피할 판단을 하기 전에 결정되므로 추격 판정보다 먼저 계산한다.
            double hitByPitchProbability = Clamp(
                tuning.HitByPitchProbability -
                (effectiveControl - 50d) * tuning.ControlHitByPitchWeight,
                0d,
                0.02d);
            if (_random.NextDouble() < hitByPitchProbability)
                return PitchResult.HitByPitch;

            double chaseProbability = ClampProbability(
                tuning.ChaseProbability -
                (batter.Mental - 50d) * tuning.MentalChaseWeight +
                (effectiveStuff - 50d) * tuning.StuffChaseWeight +
                (effectiveVelocity - 50d) * tuning.VelocityChaseWeight +
                approachModifier.ChaseAdjustment);
            if (_random.NextDouble() >= chaseProbability)
                return PitchResult.Ball;

            double contactProbability = CalculateContactProbability(
                tuning.ChaseContactProbability,
                tuning,
                matchup,
                effectiveStuff,
                effectiveVelocity,
                approachModifier.ContactAdjustment);
            return ResolveSwing(
                contactProbability,
                tuning.FairContactProbability + approachModifier.FairContactAdjustment,
                pitchNumber);
        }

        private PitchResult ResolveSwing(
            double contactProbability,
            double fairContactProbability,
            int pitchNumber)
        {
            if (_random.NextDouble() >= contactProbability)
                return PitchResult.SwingingStrike;

            bool isFair = _random.NextDouble() < fairContactProbability;
            if (isFair || pitchNumber >= BaseballRules.MaximumPitchesPerPlateAppearance)
                return PitchResult.InPlay;

            // 비정상 RNG가 무한 Foul을 만들지 못하게 하되, 정상 수열에서는 사실상 도달하지 않는 안전장치다.
            return PitchResult.Foul;
        }

        private PlateAppearanceResult ResolveHitType(
            BattedBallBalance tuning,
            BatterAttributes batter,
            PitcherAttributes pitcher,
            BattingApproachModifier approachModifier)
        {
            double tripleShare = Clamp(
                tuning.TripleShare + (batter.Speed - 50d) * tuning.SpeedTripleWeight,
                0.002d,
                0.08d);
            double doubleShare = Clamp(
                tuning.DoubleShare +
                (batter.Power - 50d) * tuning.PowerDoubleWeight -
                (pitcher.Breaking - 50d) * tuning.BreakingDoubleWeight +
                approachModifier.DoubleShareAdjustment,
                0.08d,
                0.38d);
            double hitTypeRoll = _random.NextDouble();

            if (hitTypeRoll < tripleShare)
                return PlateAppearanceResult.Triple;
            if (hitTypeRoll < tripleShare + doubleShare)
                return PlateAppearanceResult.Double;
            return PlateAppearanceResult.Single;
        }

        private static double CalculateContactProbability(
            double baseProbability,
            PlateDisciplineBalance tuning,
            in PlateAppearanceMatchup matchup,
            double effectiveStuff,
            double effectiveVelocity,
            double approachContactAdjustment)
        {
            double platoonAdjustment = GetPlatoonContactAdjustment(tuning, matchup);
            double contactDifference = matchup.Batter.BatterAttributes.Contact + platoonAdjustment - effectiveStuff;
            double velocityPenalty = (effectiveVelocity - 50d) * tuning.VelocityContactWeight;
            return ClampProbability(
                baseProbability +
                contactDifference * tuning.ContactMatchupWeight -
                velocityPenalty +
                approachContactAdjustment);
        }

        private static double GetPlatoonContactAdjustment(
            PlateDisciplineBalance tuning,
            in PlateAppearanceMatchup matchup)
        {
            Handedness battingHand = matchup.Batter.BattingHand;
            Handedness throwingHand = matchup.Pitcher.ThrowingHand;
            if (battingHand == Handedness.Switch || battingHand != throwingHand)
                return tuning.OppositeHandedContactBonus;

            return -tuning.SameHandedContactPenalty;
        }

        private static double ClampProbability(double value)
        {
            return Clamp(value, 0.01d, 0.99d);
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            if (value < minimum)
                return minimum;
            if (value > maximum)
                return maximum;
            return value;
        }
    }
}
