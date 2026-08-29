using System;

namespace Baseball.Core.Balance
{
    /// <summary>플레이어의 인접 리그 승격·재기 계약 자격과 오퍼 수를 조정한다.</summary>
    public readonly struct LeagueMovementBalance
    {
        public LeagueMovementBalance(
            int upperLeagueOverallPenalty,
            double performanceWeight,
            double potentialWeight,
            int reliablePlateAppearances,
            int reliablePitchingOuts,
            double minorMinimumProjectedOverall,
            double majorMinimumProjectedOverall,
            double competitorMargin,
            int minimumTeamBudget,
            double interestScoreThreshold,
            int maximumPromotionOffers,
            int maximumRehabilitationOffers,
            int minorContractYears,
            int majorContractYears,
            double seasonPerformanceFitWeight = 0.30d,
            double targetAbilityFitWeight = 0.25d,
            double positionNeedFitWeight = 0.20d,
            double agePotentialFitWeight = 0.10d,
            double recentGrowthFitWeight = 0.05d,
            double durabilityFitWeight = 0.05d,
            double reputationFitWeight = 0.05d)
        {
            if (upperLeagueOverallPenalty < 0) throw new ArgumentOutOfRangeException(nameof(upperLeagueOverallPenalty));
            if (performanceWeight < 0d || potentialWeight < 0d)
                throw new ArgumentOutOfRangeException(nameof(performanceWeight));
            if (reliablePlateAppearances <= 0 || reliablePitchingOuts <= 0)
                throw new ArgumentOutOfRangeException(nameof(reliablePlateAppearances));
            if (minorMinimumProjectedOverall < 0d || majorMinimumProjectedOverall <= minorMinimumProjectedOverall)
                throw new ArgumentOutOfRangeException(nameof(majorMinimumProjectedOverall));
            if (competitorMargin < 0d || minimumTeamBudget < 0 || minimumTeamBudget > 100)
                throw new ArgumentOutOfRangeException(nameof(competitorMargin));
            if (interestScoreThreshold <= 0d || maximumPromotionOffers <= 0 || maximumRehabilitationOffers <= 0)
                throw new ArgumentOutOfRangeException(nameof(interestScoreThreshold));
            if (minorContractYears <= 0 || majorContractYears <= 0)
                throw new ArgumentOutOfRangeException(nameof(minorContractYears));
            double fitWeightSum = seasonPerformanceFitWeight + targetAbilityFitWeight +
                                  positionNeedFitWeight + agePotentialFitWeight +
                                  recentGrowthFitWeight + durabilityFitWeight + reputationFitWeight;
            if (seasonPerformanceFitWeight < 0d || targetAbilityFitWeight < 0d ||
                positionNeedFitWeight < 0d || agePotentialFitWeight < 0d ||
                recentGrowthFitWeight < 0d || durabilityFitWeight < 0d || reputationFitWeight < 0d ||
                Math.Abs(fitWeightSum - 1d) > 0.000001d)
                throw new ArgumentOutOfRangeException(nameof(seasonPerformanceFitWeight),
                    "상위 리그 적합도 가중치 합은 1이어야 합니다.");

            UpperLeagueOverallPenalty = upperLeagueOverallPenalty;
            PerformanceWeight = performanceWeight;
            PotentialWeight = potentialWeight;
            ReliablePlateAppearances = reliablePlateAppearances;
            ReliablePitchingOuts = reliablePitchingOuts;
            MinorMinimumProjectedOverall = minorMinimumProjectedOverall;
            MajorMinimumProjectedOverall = majorMinimumProjectedOverall;
            CompetitorMargin = competitorMargin;
            MinimumTeamBudget = minimumTeamBudget;
            InterestScoreThreshold = interestScoreThreshold;
            MaximumPromotionOffers = maximumPromotionOffers;
            MaximumRehabilitationOffers = maximumRehabilitationOffers;
            MinorContractYears = minorContractYears;
            MajorContractYears = majorContractYears;
            SeasonPerformanceFitWeight = seasonPerformanceFitWeight;
            TargetAbilityFitWeight = targetAbilityFitWeight;
            PositionNeedFitWeight = positionNeedFitWeight;
            AgePotentialFitWeight = agePotentialFitWeight;
            RecentGrowthFitWeight = recentGrowthFitWeight;
            DurabilityFitWeight = durabilityFitWeight;
            ReputationFitWeight = reputationFitWeight;
        }

        public int UpperLeagueOverallPenalty { get; }
        public double PerformanceWeight { get; }
        public double PotentialWeight { get; }
        public int ReliablePlateAppearances { get; }
        public int ReliablePitchingOuts { get; }
        public double MinorMinimumProjectedOverall { get; }
        public double MajorMinimumProjectedOverall { get; }
        public double CompetitorMargin { get; }
        public int MinimumTeamBudget { get; }
        public double InterestScoreThreshold { get; }
        public int MaximumPromotionOffers { get; }
        public int MaximumRehabilitationOffers { get; }
        public int MinorContractYears { get; }
        public int MajorContractYears { get; }
        public double SeasonPerformanceFitWeight { get; }
        public double TargetAbilityFitWeight { get; }
        public double PositionNeedFitWeight { get; }
        public double AgePotentialFitWeight { get; }
        public double RecentGrowthFitWeight { get; }
        public double DurabilityFitWeight { get; }
        public double ReputationFitWeight { get; }

        public static LeagueMovementBalance CreateDefault()
        {
            return new LeagueMovementBalance(
                upperLeagueOverallPenalty: 2,
                performanceWeight: 0.20d,
                potentialWeight: 0.08d,
                reliablePlateAppearances: 300,
                reliablePitchingOuts: 300,
                minorMinimumProjectedOverall: 47d,
                majorMinimumProjectedOverall: 60d,
                competitorMargin: 15d,
                minimumTeamBudget: 35,
                interestScoreThreshold: 0.95d,
                maximumPromotionOffers: 2,
                maximumRehabilitationOffers: 2,
                minorContractYears: 2,
                majorContractYears: 3);
        }
    }
}
