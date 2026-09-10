using System;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Simulation.Random;

namespace Baseball.Simulation.Match
{
    public enum ExtraBaseOutcome
    {
        Hold = 0,
        Safe = 1,
        Out = 2
    }

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
        private readonly SkillTraitBalance _skillTraits;
        private readonly IRandomSource _random;

        public BaserunningResolver(
            BaseRunningBalance balance,
            IRandomSource random,
            SkillTraitBalance? skillTraits = null)
        {
            _balance = balance;
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _skillTraits = skillTraits ?? SkillTraitBalance.CreateDefault();
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
            if (runner.HasTrait(SkillTraitIds.AggressiveBaserunning))
                threshold -= _skillTraits.AggressiveRunningThresholdReduction;
            return new BaserunningDecision(successChance >= threshold, successChance);
        }

        public bool Resolve(in BaserunningDecision decision)
        {
            return decision.ShouldAttempt && _random.NextDouble() < decision.SuccessChance;
        }

        /// <summary>목표 진루율과 송구 발생 시 세이프율을 분리해 보류·진루·주루사를 판정한다.</summary>
        public ExtraBaseOutcome ResolveExtraBase(
            double baseAdvanceProbability,
            Player runner,
            int fielderArm,
            int outs,
            int inning,
            int scoreDifference,
            RunningApproach approach)
        {
            double advanceProbability = Clamp(
                baseAdvanceProbability +
                (runner.BatterAttributes.Speed - 50d) * _balance.RunnerSpeedWeight +
                (runner.BatterAttributes.Mental - 50d) * 0.0012d -
                (fielderArm - 50d) * _balance.DefenseWeight,
                0.02d,
                0.95d);
            double safeProbability = Clamp(
                _balance.ExtraBaseSafeProbability +
                (runner.BatterAttributes.Speed - 50d) * _balance.ExtraBaseSafeSpeedWeight -
                (fielderArm - 50d) * _balance.ExtraBaseSafeDefenseWeight,
                0.82d,
                0.985d);
            double attemptProbability = advanceProbability / safeProbability;
            if (approach == RunningApproach.Conservative)
                attemptProbability *= _balance.ConservativeAttemptMultiplier;
            else if (approach == RunningApproach.Aggressive)
                attemptProbability *= _balance.AggressiveAttemptMultiplier;
            if (outs == 2) attemptProbability *= 1.08d;
            if (inning >= 8 && scoreDifference == 0) attemptProbability *= 1.05d;
            if (runner.HasTrait(SkillTraitIds.AggressiveBaserunning))
                attemptProbability *= 1d + _skillTraits.AggressiveRunningThresholdReduction;
            attemptProbability = Clamp(attemptProbability, 0.01d, 1d);

            if (_random.NextDouble() >= attemptProbability)
                return ExtraBaseOutcome.Hold;
            return _random.NextDouble() < safeProbability
                ? ExtraBaseOutcome.Safe
                : ExtraBaseOutcome.Out;
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            if (value < minimum) return minimum;
            if (value > maximum) return maximum;
            return value;
        }
    }
}
