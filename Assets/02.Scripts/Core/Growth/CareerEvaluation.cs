using System;

namespace Baseball.Core.Growth
{
    public enum DecisionType
    {
        ManagerRole,
        Growth,
        Injury,
        Contract,
        Retirement,
        ContentAccess
    }

    public enum DecisionReasonCode
    {
        None,
        CurrentAbility,
        PositionFit,
        RecentPerformance,
        Condition,
        ManagerTrust,
        GrowthOutlook,
        IncumbentBonus,
        CompetitorScore,
        AgeCurve,
        PotentialGap,
        WorkEthic,
        TrainingFit,
        RepetitionPenalty,
        UsageExposure,
        CatchUpSupport,
        StableAbility,
        Durability,
        PositionScarcity,
        TeamNeed,
        BaseRisk,
        Fatigue,
        Workload,
        TrainingIntensity,
        ExistingInjury,
        AbilityDecline,
        PlayingTime,
        LongTermInjury,
        ContractRemaining,
        MilestonePursuit,
        ChampionshipWindow,
        FranchiseLoyalty,
        VeteranDemand,
        Personality,
        RecoveryProtection
    }

    public enum DecisionDirection
    {
        Negative = -1,
        Neutral = 0,
        Positive = 1
    }

    public enum RecommendedActionCode
    {
        ImproveCoreAbility,
        ImprovePositionFit,
        RestoreCondition,
        EarnPlayingTime,
        ReduceTrainingRepetition,
        ChooseRecovery,
        ReduceWorkload,
        SeekTreatment,
        PursueContract,
        PursueMilestone
    }

    public readonly struct DecisionFactor
    {
        public DecisionFactor(
            DecisionReasonCode reasonCode,
            double rawValue,
            double normalizedValue,
            double weight,
            double contribution,
            DecisionDirection direction,
            int priority)
        {
            ReasonCode = reasonCode;
            RawValue = rawValue;
            NormalizedValue = normalizedValue;
            Weight = weight;
            Contribution = contribution;
            Direction = direction;
            Priority = priority;
        }

        public DecisionReasonCode ReasonCode { get; }
        public double RawValue { get; }
        public double NormalizedValue { get; }
        public double Weight { get; }
        public double Contribution { get; }
        public DecisionDirection Direction { get; }
        public int Priority { get; }
    }

    /// <summary>Simulation이 문장 대신 반환하는 판정 근거·임계값·추천 행동 묶음이다.</summary>
    public sealed class DecisionExplanation
    {
        public DecisionExplanation(
            DecisionType decisionType,
            DecisionReasonCode summaryReasonCode,
            DecisionFactor[] factors,
            double[] thresholds,
            RecommendedActionCode[] recommendedActions,
            int rulesVersion)
        {
            DecisionType = decisionType;
            SummaryReasonCode = summaryReasonCode;
            Factors = factors ?? Array.Empty<DecisionFactor>();
            Thresholds = thresholds ?? Array.Empty<double>();
            RecommendedActions = recommendedActions ?? Array.Empty<RecommendedActionCode>();
            RulesVersion = rulesVersion;
        }

        public DecisionType DecisionType { get; }
        public DecisionReasonCode SummaryReasonCode { get; }
        public DecisionFactor[] Factors { get; }
        public double[] Thresholds { get; }
        public RecommendedActionCode[] RecommendedActions { get; }
        public int RulesVersion { get; }
    }

    public enum ManagerDevelopmentStyle
    {
        Balanced,
        VeteranPreference,
        Development,
        DataDriven,
        DefenseFirst
    }

    public enum OpportunityRole
    {
        KeyStarter,
        Starter,
        Platoon,
        Backup,
        PinchHitter,
        PinchRunner,
        StartingRotation,
        HighLeverageRelief,
        LowLeverageRelief,
        MinorLeague
    }

    /// <summary>
    /// 스프링캠프에서 한 선수를 평가하는 설명 가능한 여섯 점수다.
    /// </summary>
    public readonly struct ManagerRoleEvaluationInput
    {
        public ManagerRoleEvaluationInput(
            double currentAbility,
            double lastSeasonPerformance,
            double condition,
            double managerTrust,
            double roleFit,
            double growthOutlook,
            bool isPitcher,
            double incumbentBonus = 0d)
        {
            CurrentAbility = Validate(currentAbility, nameof(currentAbility));
            LastSeasonPerformance = Validate(lastSeasonPerformance, nameof(lastSeasonPerformance));
            Condition = Validate(condition, nameof(condition));
            ManagerTrust = Validate(managerTrust, nameof(managerTrust));
            RoleFit = Validate(roleFit, nameof(roleFit));
            GrowthOutlook = Validate(growthOutlook, nameof(growthOutlook));
            if (incumbentBonus < 0d || incumbentBonus > 2d)
                throw new ArgumentOutOfRangeException(nameof(incumbentBonus));
            IncumbentBonus = incumbentBonus;
            IsPitcher = isPitcher;
        }

        public double CurrentAbility { get; }
        public double LastSeasonPerformance { get; }
        public double Condition { get; }
        public double ManagerTrust { get; }
        public double RoleFit { get; }
        public double GrowthOutlook { get; }
        public bool IsPitcher { get; }
        public double IncumbentBonus { get; }

        private static double Validate(double value, string parameterName)
        {
            if (value < 0d || value > 100d)
                throw new ArgumentOutOfRangeException(parameterName);
            return value;
        }
    }

    public readonly struct ManagerRoleEvaluationResult
    {
        public ManagerRoleEvaluationResult(
            double score,
            double strongestCompetitorScore,
            OpportunityRole role,
            DecisionExplanation explanation = null)
        {
            Score = score;
            StrongestCompetitorScore = strongestCompetitorScore;
            Role = role;
            Explanation = explanation;
        }

        public double Score { get; }
        public double StrongestCompetitorScore { get; }
        public double Margin => Score - StrongestCompetitorScore;
        public OpportunityRole Role { get; }
        public DecisionExplanation Explanation { get; }
    }

    public readonly struct ContractMarketInput
    {
        public ContractMarketInput(
            double recentPerformance,
            double stableAbility,
            double ageAndGrowthOutlook,
            double durability,
            double positionScarcity,
            double teamNeed)
        {
            RecentPerformance = recentPerformance;
            StableAbility = stableAbility;
            AgeAndGrowthOutlook = ageAndGrowthOutlook;
            Durability = durability;
            PositionScarcity = positionScarcity;
            TeamNeed = teamNeed;
        }

        public double RecentPerformance { get; }
        public double StableAbility { get; }
        public double AgeAndGrowthOutlook { get; }
        public double Durability { get; }
        public double PositionScarcity { get; }
        public double TeamNeed { get; }
    }

    public readonly struct ContractMarketEvaluationResult
    {
        public ContractMarketEvaluationResult(double score, DecisionExplanation explanation)
        {
            Score = score;
            Explanation = explanation;
        }

        public double Score { get; }
        public DecisionExplanation Explanation { get; }
    }

    public readonly struct InjuryRiskEvaluationResult
    {
        public InjuryRiskEvaluationResult(double risk, DecisionExplanation explanation)
        {
            Risk = risk;
            Explanation = explanation;
        }

        public double Risk { get; }
        public DecisionExplanation Explanation { get; }
    }
}
