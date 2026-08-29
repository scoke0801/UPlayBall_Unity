using System;
using Baseball.Core.Balance;
using Baseball.Core.Teams;

namespace Baseball.Simulation.Career
{
    /// <summary>리그 수준 보정·기록 신뢰도·경쟁자 수준으로 인접 리그 계약 자격을 평가한다.</summary>
    public sealed class LeagueMovementEvaluator
    {
        private readonly LeagueMovementBalance _balance;

        public LeagueMovementEvaluator(LeagueMovementBalance balance)
        {
            _balance = balance;
        }

        public LeagueMovementEvaluationResult Evaluate(LeagueMovementEvaluationInput input)
        {
            double reliability = input.SampleSize / (double)input.ReliableSampleSize;
            if (reliability > 1d) reliability = 1d;
            double projectedOverall = input.PlayerOverall - input.LevelPenalty +
                (input.PerformanceRating - 50d) * _balance.PerformanceWeight * reliability +
                (input.PotentialRating - 50d) * _balance.PotentialWeight;
            projectedOverall = Clamp(projectedOverall, 0d, 100d);

            double rosterFit = projectedOverall + _balance.CompetitorMargin - input.WeakestCompetitorOverall;
            bool isEligible = projectedOverall >= input.MinimumProjectedOverall &&
                              rosterFit >= 0d &&
                              input.TeamBudget >= _balance.MinimumTeamBudget;
            double reliablePerformance = 50d + (input.PerformanceRating - 50d) * reliability;
            double targetAbilityFit = Clamp(50d + rosterFit * 5d, 0d, 100d);
            // 실제 구단 관심은 시즌 성과·로스터 적합도·포지션 수요가 75%를 차지한다.
            // 나머지 항목은 젊은 유망주와 건강한 베테랑을 구별하되 하드 게이트가 되지 않는다.
            double fitScore =
                reliablePerformance * _balance.SeasonPerformanceFitWeight +
                targetAbilityFit * _balance.TargetAbilityFitWeight +
                input.PositionNeed * _balance.PositionNeedFitWeight +
                input.PotentialRating * _balance.AgePotentialFitWeight +
                input.RecentGrowthRating * _balance.RecentGrowthFitWeight +
                input.DurabilityRating * _balance.DurabilityFitWeight +
                input.CareerReputation * _balance.ReputationFitWeight;
            double interestScore = Clamp(fitScore, 0d, 100d) / 50d;

            ExpectedRole role;
            if (projectedOverall >= input.StrongestCompetitorOverall + 2d)
                role = ExpectedRole.StartingCompetition;
            else if (projectedOverall + 2d >= input.WeakestCompetitorOverall)
                role = ExpectedRole.RosterCompetition;
            else
                role = ExpectedRole.BenchCompetition;
            double playingTime = role switch
            {
                ExpectedRole.StartingCompetition => 0.64d,
                ExpectedRole.RosterCompetition => 0.40d,
                _ => 0.18d
            };
            return new LeagueMovementEvaluationResult(
                isEligible,
                projectedOverall,
                reliability,
                interestScore,
                fitScore,
                role,
                playingTime);
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            if (value < minimum) return minimum;
            return value > maximum ? maximum : value;
        }
    }

    public readonly struct LeagueMovementEvaluationInput
    {
        public LeagueMovementEvaluationInput(
            int playerOverall,
            double performanceRating,
            double potentialRating,
            int sampleSize,
            int reliableSampleSize,
            int levelPenalty,
            double minimumProjectedOverall,
            int strongestCompetitorOverall,
            int weakestCompetitorOverall,
            int positionNeed,
            int teamBudget,
            int developmentRating,
            double recentGrowthRating = 50d,
            double durabilityRating = 75d,
            double careerReputation = 50d)
        {
            if (sampleSize < 0 || reliableSampleSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(sampleSize));
            PlayerOverall = playerOverall;
            PerformanceRating = performanceRating;
            PotentialRating = potentialRating;
            SampleSize = sampleSize;
            ReliableSampleSize = reliableSampleSize;
            LevelPenalty = levelPenalty;
            MinimumProjectedOverall = minimumProjectedOverall;
            StrongestCompetitorOverall = strongestCompetitorOverall;
            WeakestCompetitorOverall = weakestCompetitorOverall;
            PositionNeed = positionNeed;
            TeamBudget = teamBudget;
            DevelopmentRating = developmentRating;
            RecentGrowthRating = ClampRating(recentGrowthRating, nameof(recentGrowthRating));
            DurabilityRating = ClampRating(durabilityRating, nameof(durabilityRating));
            CareerReputation = ClampRating(careerReputation, nameof(careerReputation));
        }

        public int PlayerOverall { get; }
        public double PerformanceRating { get; }
        public double PotentialRating { get; }
        public int SampleSize { get; }
        public int ReliableSampleSize { get; }
        public int LevelPenalty { get; }
        public double MinimumProjectedOverall { get; }
        public int StrongestCompetitorOverall { get; }
        public int WeakestCompetitorOverall { get; }
        public int PositionNeed { get; }
        public int TeamBudget { get; }
        public int DevelopmentRating { get; }
        public double RecentGrowthRating { get; }
        public double DurabilityRating { get; }
        public double CareerReputation { get; }

        private static double ClampRating(double value, string parameterName)
        {
            if (value < 0d || value > 100d)
                throw new ArgumentOutOfRangeException(parameterName);
            return value;
        }
    }

    public readonly struct LeagueMovementEvaluationResult
    {
        public LeagueMovementEvaluationResult(
            bool isEligible,
            double projectedOverall,
            double reliability,
            double interestScore,
            double fitScore,
            ExpectedRole expectedRole,
            double estimatedPlayingTime)
        {
            IsEligible = isEligible;
            ProjectedOverall = projectedOverall;
            Reliability = reliability;
            InterestScore = interestScore;
            FitScore = fitScore;
            ExpectedRole = expectedRole;
            EstimatedPlayingTime = estimatedPlayingTime;
        }

        public bool IsEligible { get; }
        public double ProjectedOverall { get; }
        public double Reliability { get; }
        public double InterestScore { get; }
        public double FitScore { get; }
        public ExpectedRole ExpectedRole { get; }
        public double EstimatedPlayingTime { get; }
    }
}
