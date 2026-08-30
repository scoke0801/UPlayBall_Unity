using System;

namespace Baseball.Core.Balance
{
    /// <summary>
    /// 스프링캠프 역할 평가의 여섯 근거와 역할 변경 임계값을 보관한다.
    /// </summary>
    public readonly struct ManagerEvaluationWeightTable
    {
        public ManagerEvaluationWeightTable(
            double currentAbility,
            double lastSeasonPerformance,
            double condition,
            double managerTrust,
            double roleFit,
            double growthOutlook,
            double starterMargin,
            double competitionMargin,
            double backupMargin)
        {
            double sum = currentAbility + lastSeasonPerformance + condition + managerTrust + roleFit + growthOutlook;
            if (Math.Abs(sum - 1d) > 0.000001d)
                throw new ArgumentException("역할 평가 가중치 합은 1이어야 합니다.");
            CurrentAbility = currentAbility;
            LastSeasonPerformance = lastSeasonPerformance;
            Condition = condition;
            ManagerTrust = managerTrust;
            RoleFit = roleFit;
            GrowthOutlook = growthOutlook;
            StarterMargin = starterMargin;
            CompetitionMargin = competitionMargin;
            BackupMargin = backupMargin;
        }

        public double CurrentAbility { get; }
        public double LastSeasonPerformance { get; }
        public double Condition { get; }
        public double ManagerTrust { get; }
        public double RoleFit { get; }
        public double GrowthOutlook { get; }
        public double StarterMargin { get; }
        public double CompetitionMargin { get; }
        public double BackupMargin { get; }

        public static ManagerEvaluationWeightTable CreateDefault()
        {
            return new ManagerEvaluationWeightTable(
                0.55d, 0.15d, 0.10d, 0.00d, 0.20d, 0.00d,
                starterMargin: 4d,
                competitionMargin: -3d,
                backupMargin: -9d);
        }
    }

    /// <summary>
    /// 최근 성과·현재 전력·나이·내구도·희소성·구단 필요를 계약 시장 가치로 묶는다.
    /// </summary>
    public readonly struct ContractMarketBalanceTable
    {
        public ContractMarketBalanceTable(
            double recentPerformance,
            double stableAbility,
            double ageAndOutlook,
            double durability,
            double positionScarcity,
            double teamNeed)
        {
            double sum = recentPerformance + stableAbility + ageAndOutlook + durability + positionScarcity + teamNeed;
            if (Math.Abs(sum - 1d) > 0.000001d)
                throw new ArgumentException("계약 시장 가중치 합은 1이어야 합니다.");
            RecentPerformance = recentPerformance;
            StableAbility = stableAbility;
            AgeAndOutlook = ageAndOutlook;
            Durability = durability;
            PositionScarcity = positionScarcity;
            TeamNeed = teamNeed;
        }

        public double RecentPerformance { get; }
        public double StableAbility { get; }
        public double AgeAndOutlook { get; }
        public double Durability { get; }
        public double PositionScarcity { get; }
        public double TeamNeed { get; }

        public static ContractMarketBalanceTable CreateDefault()
        {
            return new ContractMarketBalanceTable(0.30d, 0.25d, 0.15d, 0.10d, 0.10d, 0.10d);
        }
    }
}
