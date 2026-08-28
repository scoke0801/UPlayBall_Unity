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
            int majorContractYears)
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
