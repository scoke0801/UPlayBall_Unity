using System;
using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Simulation.Random;

namespace Baseball.Simulation.Match
{
    public readonly struct BaserunningDecision
    {
        public BaserunningDecision(bool shouldAttempt, double successChance)
        {
            ShouldAttempt = shouldAttempt;
            SuccessChance = successChance;
        }

        public bool ShouldAttempt { get; }
        public double SuccessChance { get; }
    }

    /// <summary>
    /// 진루 시도 여부는 기대값으로, 실제 세이프·아웃은 주입된 주루 RNG로 판정한다.
    /// </summary>
    public sealed class BaserunningResolver
    {
        private readonly BaseRunningBalance _balance;
        private readonly IRandomSource _random;

        public BaserunningResolver(BaseRunningBalance balance, IRandomSource random)
        {
            _balance = balance;
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public BaserunningDecision DecideExtraBase(
            double baseProbability,
            Player runner,
            int fielderArm,
            int outs,
            int inning,
            int scoreDifference,
            RunningApproach approach)
        {
            double successChance = Clamp(
                baseProbability +
                (runner.BatterAttributes.Speed - 50d) * _balance.RunnerSpeedWeight +
                (runner.BatterAttributes.Mental - 50d) * 0.0012d -
                (fielderArm - 50d) * _balance.DefenseWeight,
                0.05d,
                0.95d);
            double threshold = approach switch
            {
                RunningApproach.Conservative => 0.78d,
                RunningApproach.Aggressive => 0.58d,
                _ => 0.68d
            };
            if (outs == 2) threshold -= 0.05d;
            if (inning >= 8 && scoreDifference == 0) threshold -= 0.03d;
            return new BaserunningDecision(successChance >= threshold, successChance);
        }

        public bool Resolve(in BaserunningDecision decision)
        {
            return decision.ShouldAttempt && _random.NextDouble() < decision.SuccessChance;
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            if (value < minimum) return minimum;
            if (value > maximum) return maximum;
            return value;
        }
    }
}
