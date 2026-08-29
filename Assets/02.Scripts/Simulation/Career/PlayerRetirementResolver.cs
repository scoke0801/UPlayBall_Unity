using System;
using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Simulation.Random;

namespace Baseball.Simulation.Career
{
    /// <summary>설명 가능한 AI 은퇴 의사 계산에 필요한 시즌 종료 스냅샷이다.</summary>
    public readonly struct RetirementEvaluationInput
    {
        public RetirementEvaluationInput(
            int nextSeasonAge,
            int overall,
            RetirementPersonality personality,
            int recentAbilityDecline = 0,
            double recentAppearanceRate = 1d,
            bool hasLongTermInjury = false,
            bool hasContractRemaining = false,
            bool isMilestonePursuit = false,
            bool isChampionshipContender = false,
            bool isFranchiseTeam = false,
            bool hasVeteranDemand = false)
        {
            if (recentAbilityDecline < 0)
                throw new ArgumentOutOfRangeException(nameof(recentAbilityDecline));
            if (recentAppearanceRate < 0d || recentAppearanceRate > 1d)
                throw new ArgumentOutOfRangeException(nameof(recentAppearanceRate));
            NextSeasonAge = nextSeasonAge;
            Overall = overall;
            Personality = personality;
            RecentAbilityDecline = recentAbilityDecline;
            RecentAppearanceRate = recentAppearanceRate;
            HasLongTermInjury = hasLongTermInjury;
            HasContractRemaining = hasContractRemaining;
            IsMilestonePursuit = isMilestonePursuit;
            IsChampionshipContender = isChampionshipContender;
            IsFranchiseTeam = isFranchiseTeam;
            HasVeteranDemand = hasVeteranDemand;
        }

        public int NextSeasonAge { get; }
        public int Overall { get; }
        public RetirementPersonality Personality { get; }
        public int RecentAbilityDecline { get; }
        public double RecentAppearanceRate { get; }
        public bool HasLongTermInjury { get; }
        public bool HasContractRemaining { get; }
        public bool IsMilestonePursuit { get; }
        public bool IsChampionshipContender { get; }
        public bool IsFranchiseTeam { get; }
        public bool HasVeteranDemand { get; }
    }

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
            return ShouldRetire(new RetirementEvaluationInput(
                nextSeasonAge,
                overall,
                RetirementPersonality.Ambitious));
        }

        /// <summary>나이 압박에 출전 감소·계약·마일스톤·구단 관계와 성향을 더해 은퇴를 판정한다.</summary>
        public bool ShouldRetire(RetirementEvaluationInput input)
        {
            if (input.NextSeasonAge >= _balance.GuaranteedRetirementAge)
                return true;
            if (input.NextSeasonAge < _balance.RetirementMinimumAge)
                return false;

            double probability = _balance.RetirementBaseProbability +
                (input.NextSeasonAge - _balance.RetirementMinimumAge) * _balance.RetirementAgeWeight;
            if (input.Overall < _balance.LowAbilityThreshold)
                probability += (_balance.LowAbilityThreshold - input.Overall) * _balance.LowAbilityWeight;
            probability += Math.Min(0.15d, input.RecentAbilityDecline * 0.012d);
            if (input.RecentAppearanceRate < 0.35d)
                probability += (0.35d - input.RecentAppearanceRate) * 0.30d;
            if (input.HasLongTermInjury) probability += 0.12d;
            if (input.HasContractRemaining) probability -= 0.08d;
            if (input.IsMilestonePursuit) probability -= 0.07d;
            if (input.IsChampionshipContender) probability -= 0.05d;
            if (input.IsFranchiseTeam) probability -= 0.04d;
            if (input.HasVeteranDemand) probability -= 0.06d;

            probability += input.Personality switch
            {
                RetirementPersonality.Ambitious => input.HasVeteranDemand ? 0d : 0.08d,
                RetirementPersonality.PlayingObsessed => -0.16d,
                RetirementPersonality.FranchiseLoyal => input.IsFranchiseTeam ? -0.09d : 0.05d,
                RetirementPersonality.ChampionshipSeeker => input.IsChampionshipContender ? -0.09d : 0.05d,
                _ => 0d
            };
            if (probability < 0d) probability = 0d;
            if (probability > 1d) probability = 1d;
            return _random.NextDouble() < probability;
        }
    }
}
