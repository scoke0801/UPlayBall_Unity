using System;
using Baseball.Core.Balance;
using Baseball.Core.Teams;

namespace Baseball.Simulation.Match
{
    public enum PitcherChangeReason
    {
        Fatigue = 0,
        PitchLimit = 1,
        TimesThroughOrder = 2,
        Performance = 3,
        HighLeverage = 4,
        Matchup = 5,
        Injury = 6,
        ScheduledUsage = 7,
        DefensiveStrategy = 8,
        Emergency = 9
    }

    public readonly struct PitcherChangeDecision
    {
        public PitcherChangeDecision(
            bool shouldChange,
            PitcherChangeReason reason,
            double pullScore,
            double threshold)
        {
            ShouldChange = shouldChange;
            Reason = reason;
            PullScore = pullScore;
            Threshold = threshold;
        }

        public bool ShouldChange { get; }
        public PitcherChangeReason Reason { get; }
        public double PullScore { get; }
        public double Threshold { get; }
    }

    /// <summary>
    /// 피로·현재 위험·타순 순환·실적·레버리지와 불펜 보존을 합산해 교체를 결정한다.
    /// </summary>
    public sealed class PitcherManagementAi
    {
        private readonly BullpenManagementBalance _balance;

        public PitcherManagementAi(BullpenManagementBalance balance)
        {
            _balance = balance;
        }

        public PitcherChangeDecision Evaluate(
            in DecisionContext context,
            int availableRelieverCount,
            double bullpenFreshness)
        {
            PitcherGameState pitcher = context.PitcherState;
            if (availableRelieverCount <= 0)
                return new PitcherChangeDecision(false, PitcherChangeReason.Emergency, 0d, double.MaxValue);
            if (pitcher.RosterEntry.PitchLimit > 0 && pitcher.PitchCount >= pitcher.RosterEntry.PitchLimit)
                return new PitcherChangeDecision(true, PitcherChangeReason.PitchLimit, 100d, 0d);
            if (pitcher.FatigueRatio >= 1.05d)
                return new PitcherChangeDecision(true, PitcherChangeReason.Fatigue, 100d, 0d);

            double fatigueRisk = Clamp01((pitcher.FatigueRatio - 0.50d) / 0.55d) *
                                 _balance.MaximumFatigueRisk;
            int occupiedBases = CountOccupiedBases(context.Bases);
            double currentDanger = (occupiedBases * 3d + (2 - context.Outs) * 2d) / 10d *
                                   _balance.MaximumCurrentDanger;
            double ttoRisk = Clamp01((pitcher.TimesThroughOrder - 1d) / 3d) *
                             _balance.MaximumTimesThroughOrderRisk;
            double performanceDamage = Math.Min(
                _balance.MaximumPerformanceDamage,
                pitcher.RunsAllowed * 3d + pitcher.ConsecutiveBattersReached * 3.5d);
            double leverageMismatch = context.Leverage >= LeverageTier.High &&
                                      pitcher.Role is PitcherRole.Starter or PitcherRole.LongRelief or PitcherRole.MiddleRelief
                ? _balance.MaximumLeverageMismatch * ((int)context.Leverage - 1) / 2d
                : 0d;
            double starterTrust = pitcher.Role == PitcherRole.Starter
                ? context.ManagerProfile.StarTrust / 100d * _balance.MaximumStarterTrust
                : 0d;
            double bullpenConservation = (1d - Clamp01(bullpenFreshness)) *
                                         _balance.MaximumBullpenConservation;
            double pullScore = fatigueRisk + currentDanger + ttoRisk + performanceDamage + leverageMismatch -
                               starterTrust - bullpenConservation;
            double threshold = _balance.PullThreshold -
                               (context.ManagerProfile.HookSpeed - 50d) * 0.20d -
                               (context.ManagerProfile.BullpenAggression - 50d) *
                               (context.Leverage >= LeverageTier.High ? 0.12d : 0.04d);
            PitcherChangeReason reason = GetPrimaryReason(
                fatigueRisk,
                ttoRisk,
                performanceDamage,
                leverageMismatch);
            return new PitcherChangeDecision(pullScore >= threshold, reason, pullScore, threshold);
        }

        public double ScoreReliever(
            PitcherGameState candidate,
            LeverageTier leverage,
            int remainingInnings,
            ManagerTacticalProfile managerProfile)
        {
            double quality = (candidate.Player.PitcherAttributes.Stuff +
                              candidate.Player.PitcherAttributes.Control +
                              candidate.Player.PitcherAttributes.Breaking) / 3d;
            double freshness = Math.Max(0d, 24d - candidate.FatigueRatio * 24d);
            double roleFit = CalculateRoleFit(candidate.Role, leverage, remainingInnings);
            double rigidity = managerProfile.BullpenRoleRigidity / 100d;
            double recentLoad = candidate.RosterEntry.RecentWorkload.PreviousDayPitches +
                                candidate.RosterEntry.RecentWorkload.TwoDaysAgoPitches *
                                _balance.RecentLoadDayTwoWeight +
                                candidate.RosterEntry.RecentWorkload.ThreeDaysAgoPitches *
                                _balance.RecentLoadDayThreeWeight;
            double futureUsageCost = candidate.Role == PitcherRole.Closer && leverage < LeverageTier.High
                ? _balance.LowLeverageCloserPenalty
                : 0d;
            return quality + freshness + roleFit * (0.6d + rigidity * 0.8d) -
                   recentLoad * 0.35d - futureUsageCost;
        }

        private static double CalculateRoleFit(PitcherRole role, LeverageTier leverage, int remainingInnings)
        {
            return role switch
            {
                PitcherRole.Closer => leverage == LeverageTier.Critical ? 22d : leverage == LeverageTier.High ? 16d : -5d,
                PitcherRole.Setup => leverage >= LeverageTier.High ? 15d : 3d,
                PitcherRole.MiddleRelief => leverage <= LeverageTier.Medium ? 10d : 2d,
                PitcherRole.LongRelief => remainingInnings >= 3 ? 14d : 1d,
                PitcherRole.Swingman => remainingInnings >= 4 ? 12d : 0d,
                _ => -20d
            };
        }

        private static PitcherChangeReason GetPrimaryReason(
            double fatigue,
            double tto,
            double performance,
            double leverage)
        {
            double maximum = fatigue;
            PitcherChangeReason reason = PitcherChangeReason.Fatigue;
            if (tto > maximum) { maximum = tto; reason = PitcherChangeReason.TimesThroughOrder; }
            if (performance > maximum) { maximum = performance; reason = PitcherChangeReason.Performance; }
            if (leverage > maximum) reason = PitcherChangeReason.HighLeverage;
            return reason;
        }

        private static int CountOccupiedBases(in BaseStateSnapshot bases)
        {
            return (bases.HasRunnerOnFirst ? 1 : 0) +
                   (bases.HasRunnerOnSecond ? 1 : 0) +
                   (bases.HasRunnerOnThird ? 1 : 0);
        }

        private static double Clamp01(double value)
        {
            if (value < 0d) return 0d;
            if (value > 1d) return 1d;
            return value;
        }
    }
}
