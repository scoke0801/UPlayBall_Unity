using System;
using Baseball.Core.Balance;
using Baseball.Simulation.Random;

namespace Baseball.Simulation.Career
{
    /// <summary>나이와 현재 경쟁력을 사용해 AI 선수의 은퇴 여부를 결정론적으로 판정한다.</summary>
    public sealed class PlayerRetirementResolver
    {
        private readonly PlayerLifecycleBalance _balance;
        private readonly IRandomSource _random;

        public PlayerRetirementResolver(PlayerLifecycleBalance balance, IRandomSource random)
        {
            _balance = balance;
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public bool ShouldRetire(int nextSeasonAge, int overall)
        {
            if (nextSeasonAge >= _balance.GuaranteedRetirementAge)
                return true;
            if (nextSeasonAge < _balance.RetirementMinimumAge)
                return false;

            double probability = _balance.RetirementBaseProbability +
                (nextSeasonAge - _balance.RetirementMinimumAge) * _balance.RetirementAgeWeight;
            if (overall < _balance.LowAbilityThreshold)
                probability += (_balance.LowAbilityThreshold - overall) * _balance.LowAbilityWeight;
            if (probability > 1d)
                probability = 1d;
            return _random.NextDouble() < probability;
        }
    }
}
