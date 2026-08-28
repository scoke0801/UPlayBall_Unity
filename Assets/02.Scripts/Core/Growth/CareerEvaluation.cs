using System;

namespace Baseball.Core.Growth
{
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
            bool isPitcher)
        {
            CurrentAbility = Validate(currentAbility, nameof(currentAbility));
            LastSeasonPerformance = Validate(lastSeasonPerformance, nameof(lastSeasonPerformance));
            Condition = Validate(condition, nameof(condition));
            ManagerTrust = Validate(managerTrust, nameof(managerTrust));
            RoleFit = Validate(roleFit, nameof(roleFit));
            GrowthOutlook = Validate(growthOutlook, nameof(growthOutlook));
            IsPitcher = isPitcher;
        }

        public double CurrentAbility { get; }
        public double LastSeasonPerformance { get; }
        public double Condition { get; }
        public double ManagerTrust { get; }
        public double RoleFit { get; }
        public double GrowthOutlook { get; }
        public bool IsPitcher { get; }

        private static double Validate(double value, string parameterName)
        {
            if (value < 0d || value > 100d)
                throw new ArgumentOutOfRangeException(parameterName);
            return value;
        }
    }

    public readonly struct ManagerRoleEvaluationResult
    {
        public ManagerRoleEvaluationResult(double score, double strongestCompetitorScore, OpportunityRole role)
        {
            Score = score;
            StrongestCompetitorScore = strongestCompetitorScore;
            Role = role;
        }

        public double Score { get; }
        public double StrongestCompetitorScore { get; }
        public double Margin => Score - StrongestCompetitorScore;
        public OpportunityRole Role { get; }
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
}
