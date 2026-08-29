using System;
using Baseball.Core.Balance;
using Baseball.Core.Players;

namespace Baseball.Simulation.Match
{
    /// <summary>
    /// 도루·번트·고의사구·수비 정렬을 같은 기대 득점/승리 기대값 위에서 결정한다.
    /// </summary>
    public sealed class TacticalAiResolver
    {
        private readonly TacticalMatchBalance _balance;
        private readonly RunExpectancy24 _runExpectancy;

        public TacticalAiResolver(TacticalMatchBalance balance, RunExpectancy24 runExpectancy)
        {
            _balance = balance;
            _runExpectancy = runExpectancy ?? throw new ArgumentNullException(nameof(runExpectancy));
        }

        public DefensiveAlignment SelectAlignment(DecisionContext context)
        {
            if (context.Bases.HasRunnerOnThird && context.Outs < 2 &&
                context.Inning >= 7 && context.ScoreDifference <= 1)
            {
                return DefensiveAlignment.InfieldIn;
            }
            if (context.Bases.HasRunnerOnFirst && context.Outs < 2)
                return DefensiveAlignment.DoublePlayDepth;
            if (context.Inning >= 8 && context.ScoreDifference == 1)
                return DefensiveAlignment.GuardLines;

            BattingTendencyProfile tendency = BattingTendencyProfile.Derive(context.Batter);
            if (tendency.PullTendency >= 0.52d && tendency.GroundBallTendency >= 0.44d)
                return DefensiveAlignment.PullShift;
            return DefensiveAlignment.Standard;
        }

        public bool ShouldIntentionalWalk(DecisionContext context)
        {
            if (context.Bases.HasRunnerOnFirst ||
                context.Bases.HasRunnerOnFirst && context.Bases.HasRunnerOnSecond && context.Bases.HasRunnerOnThird)
            {
                return false;
            }
            double batterThreat = GetBatterThreat(context.Batter);
            double nextThreat = GetBatterThreat(context.OnDeckBatter);
            double baseCost = context.Bases.HasRunnerOnSecond || context.Bases.HasRunnerOnThird ? 8d : 14d;
            double leverageWeight = 1d + (int)context.Leverage * 0.35d;
            double utility = (batterThreat - nextThreat - baseCost) * leverageWeight / 100d;
            return context.Outs >= 1 && utility >= _balance.IntentionalWalkUtilityThreshold;
        }

        public bool ShouldAttemptSteal(
            DecisionContext context,
            Player runner,
            Player catcher)
        {
            if (!context.Bases.HasRunnerOnFirst || context.Bases.HasRunnerOnSecond || context.Outs >= 2)
                return false;
            double success = CalculateStealSuccess(runner, catcher, context.Pitcher);
            double current = _runExpectancy.Get(context.Outs, context.Bases.OccupancyMask);
            double successValue = _runExpectancy.Get(context.Outs, 2);
            double failureValue = context.Outs == 2 ? 0d : _runExpectancy.Get(context.Outs + 1, 0);
            double utility = success * successValue + (1d - success) * failureValue - current;
            utility += (context.ManagerProfile.RunningAggression - 50d) * 0.0015d;
            if (context.Inning >= 8 && context.ScoreDifference == 0)
                utility += 0.035d;
            return utility >= _balance.StealAttemptUtilityThreshold;
        }

        public bool ShouldSacrificeBunt(DecisionContext context)
        {
            if (context.Outs >= 2 || !context.Bases.HasRunnerOnFirst && !context.Bases.HasRunnerOnSecond)
                return false;
            if (context.Inning < 7 || context.ScoreDifference < -1 || context.ScoreDifference > 0)
                return false;
            if (GetBatterThreat(context.Batter) >= 62d)
                return false;
            int successMask = context.Bases.HasRunnerOnSecond ? 4 : 2;
            double current = _runExpectancy.Get(context.Outs, context.Bases.OccupancyMask);
            double oneRunValue = _runExpectancy.Get(context.Outs + 1, successMask);
            double utility = oneRunValue - current +
                             (context.ManagerProfile.SmallBallPreference - 50d) * 0.002d;
            if (context.Inning >= 8 && context.ScoreDifference == 0)
                utility += 0.24d;
            else if (context.Inning >= 7 && context.ScoreDifference == -1)
                utility += 0.12d;
            return utility >= _balance.BuntUtilityThreshold;
        }

        public double CalculateStealSuccess(Player runner, Player catcher, Player pitcher)
        {
            double pitcherHold = pitcher.PitcherAttributes.Control * 0.60d +
                                 pitcher.PitcherAttributes.Mental * 0.40d;
            double catcherArm = FieldingProfile.Derive(catcher, catcher.PrimaryPosition).Arm;
            return Clamp(
                _balance.StealBaseSuccess +
                (runner.BatterAttributes.Speed - 50d) * _balance.StealSpeedWeight +
                (runner.BatterAttributes.Mental - 50d) * _balance.StealMentalWeight -
                (catcherArm - 50d) * _balance.CatcherArmWeight -
                (pitcherHold - 50d) * _balance.PitcherHoldWeight,
                _balance.MinimumStealSuccess,
                _balance.MaximumStealSuccess);
        }

        private static double GetBatterThreat(Player player)
        {
            return player.BatterAttributes.Contact * 0.46d +
                   player.BatterAttributes.Power * 0.42d +
                   player.BatterAttributes.Mental * 0.12d;
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            if (value < minimum) return minimum;
            if (value > maximum) return maximum;
            return value;
        }
    }
}
