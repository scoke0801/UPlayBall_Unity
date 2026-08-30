using System;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
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

    public readonly struct RetirementEvaluationResult
    {
        public RetirementEvaluationResult(
            bool shouldRetire,
            double probability,
            double randomRoll,
            DecisionExplanation explanation)
        {
            ShouldRetire = shouldRetire;
            Probability = probability;
            RandomRoll = randomRoll;
            Explanation = explanation;
        }

        public bool ShouldRetire { get; }
        public double Probability { get; }
        public double RandomRoll { get; }
        public DecisionExplanation Explanation { get; }
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
            return Evaluate(input).ShouldRetire;
        }

        public RetirementEvaluationResult Evaluate(RetirementEvaluationInput input)
        {
            if (input.NextSeasonAge >= _balance.GuaranteedRetirementAge)
                return CreateBoundaryResult(input, true, 1d);
            if (input.NextSeasonAge < _balance.RetirementMinimumAge)
                return CreateBoundaryResult(input, false, 0d);

            double ageContribution = _balance.RetirementBaseProbability +
                                     (input.NextSeasonAge - _balance.RetirementMinimumAge) * _balance.RetirementAgeWeight;
            double abilityContribution = input.Overall < _balance.LowAbilityThreshold
                ? (_balance.LowAbilityThreshold - input.Overall) * _balance.LowAbilityWeight
                : 0d;
            double declineContribution = Math.Min(0.15d, input.RecentAbilityDecline * 0.012d);
            double playingTimeContribution = input.RecentAppearanceRate < 0.35d
                ? (0.35d - input.RecentAppearanceRate) * 0.30d
                : 0d;
            double injuryContribution = input.HasLongTermInjury ? 0.12d : 0d;
            double contractContribution = input.HasContractRemaining ? -0.08d : 0d;
            double milestoneContribution = input.IsMilestonePursuit ? -0.07d : 0d;
            double contenderContribution = input.IsChampionshipContender ? -0.05d : 0d;
            double franchiseContribution = input.IsFranchiseTeam ? -0.04d : 0d;
            double demandContribution = input.HasVeteranDemand ? -0.06d : 0d;
            double personalityContribution = input.Personality switch
            {
                RetirementPersonality.Ambitious => input.HasVeteranDemand ? 0d : 0.08d,
                RetirementPersonality.PlayingObsessed => -0.16d,
                RetirementPersonality.FranchiseLoyal => input.IsFranchiseTeam ? -0.09d : 0.05d,
                RetirementPersonality.ChampionshipSeeker => input.IsChampionshipContender ? -0.09d : 0.05d,
                _ => 0d
            };
            double probability = ageContribution + abilityContribution + declineContribution +
                playingTimeContribution + injuryContribution + contractContribution + milestoneContribution +
                contenderContribution + franchiseContribution + demandContribution + personalityContribution;
            if (probability < 0d) probability = 0d;
            if (probability > 1d) probability = 1d;
            double roll = _random.NextDouble();
            var factors = new[]
            {
                CreateFactor(DecisionReasonCode.AgeCurve, input.NextSeasonAge, ageContribution, 1),
                CreateFactor(DecisionReasonCode.StableAbility, input.Overall, abilityContribution, 2),
                CreateFactor(DecisionReasonCode.AbilityDecline, input.RecentAbilityDecline, declineContribution, 3),
                CreateFactor(DecisionReasonCode.PlayingTime, input.RecentAppearanceRate, playingTimeContribution, 4),
                CreateFactor(DecisionReasonCode.LongTermInjury, input.HasLongTermInjury ? 1d : 0d, injuryContribution, 5),
                CreateFactor(DecisionReasonCode.ContractRemaining, input.HasContractRemaining ? 1d : 0d, contractContribution, 6),
                CreateFactor(DecisionReasonCode.MilestonePursuit, input.IsMilestonePursuit ? 1d : 0d, milestoneContribution, 7),
                CreateFactor(DecisionReasonCode.ChampionshipWindow, input.IsChampionshipContender ? 1d : 0d, contenderContribution, 8),
                CreateFactor(DecisionReasonCode.FranchiseLoyalty, input.IsFranchiseTeam ? 1d : 0d, franchiseContribution, 9),
                CreateFactor(DecisionReasonCode.VeteranDemand, input.HasVeteranDemand ? 1d : 0d, demandContribution, 10),
                CreateFactor(DecisionReasonCode.Personality, (int)input.Personality, personalityContribution, 11)
            };
            DecisionReasonCode summary = DecisionReasonCode.AgeCurve;
            double strongest = Math.Abs(ageContribution);
            for (int index = 1; index < factors.Length; index++)
            {
                double strength = Math.Abs(factors[index].Contribution);
                if (strength <= strongest)
                    continue;
                strongest = strength;
                summary = factors[index].ReasonCode;
            }
            var actions = input.HasContractRemaining
                ? Array.Empty<RecommendedActionCode>()
                : input.IsMilestonePursuit
                    ? new[] { RecommendedActionCode.PursueMilestone }
                    : new[] { RecommendedActionCode.PursueContract };
            return new RetirementEvaluationResult(
                roll < probability,
                probability,
                roll,
                new DecisionExplanation(
                    DecisionType.Retirement,
                    summary,
                    factors,
                    new[] { probability, roll },
                    actions,
                    rulesVersion: 1));
        }

        private RetirementEvaluationResult CreateBoundaryResult(
            RetirementEvaluationInput input,
            bool shouldRetire,
            double probability)
        {
            DecisionFactor age = CreateFactor(
                DecisionReasonCode.AgeCurve,
                input.NextSeasonAge,
                probability,
                1);
            return new RetirementEvaluationResult(
                shouldRetire,
                probability,
                -1d,
                new DecisionExplanation(
                    DecisionType.Retirement,
                    DecisionReasonCode.AgeCurve,
                    new[] { age },
                    new[] { (double)_balance.RetirementMinimumAge, _balance.GuaranteedRetirementAge },
                    Array.Empty<RecommendedActionCode>(),
                    rulesVersion: 1));
        }

        private static DecisionFactor CreateFactor(
            DecisionReasonCode code,
            double rawValue,
            double contribution,
            int priority)
        {
            return new DecisionFactor(
                code,
                rawValue,
                rawValue,
                1d,
                contribution,
                contribution > 0d
                    ? DecisionDirection.Positive
                    : contribution < 0d
                        ? DecisionDirection.Negative
                        : DecisionDirection.Neutral,
                priority);
        }
    }
}
