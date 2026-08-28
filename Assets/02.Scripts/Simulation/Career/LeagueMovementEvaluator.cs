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
            double contributionFactor = projectedOverall / Math.Max(1d, input.MinimumProjectedOverall);
            double needFactor = input.PositionNeed / 50d;
            double budgetFactor = input.TeamBudget / 50d;
            double developmentFactor = 0.8d + input.DevelopmentRating / 250d;
            double interestScore = contributionFactor * needFactor * budgetFactor * developmentFactor;

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
            int developmentRating)
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
    }

    public readonly struct LeagueMovementEvaluationResult
    {
        public LeagueMovementEvaluationResult(
            bool isEligible,
            double projectedOverall,
            double reliability,
            double interestScore,
            ExpectedRole expectedRole,
            double estimatedPlayingTime)
        {
            IsEligible = isEligible;
            ProjectedOverall = projectedOverall;
            Reliability = reliability;
            InterestScore = interestScore;
            ExpectedRole = expectedRole;
            EstimatedPlayingTime = estimatedPlayingTime;
        }

        public bool IsEligible { get; }
        public double ProjectedOverall { get; }
        public double Reliability { get; }
        public double InterestScore { get; }
        public ExpectedRole ExpectedRole { get; }
        public double EstimatedPlayingTime { get; }
    }
}
