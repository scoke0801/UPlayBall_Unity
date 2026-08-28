using Baseball.Core.Balance;
using Baseball.Simulation.Career;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Simulation.Career
{
    /// <summary>인접 리그 계약 평가가 기록 신뢰도·최소 전력·실제 경쟁자를 함께 반영하는지 검증한다.</summary>
    public sealed class LeagueMovementEvaluatorTests
    {
        [Test]
        public void Evaluate_표본이쌓일수록좋은성적의환산기여도가커진다()
        {
            var evaluator = new LeagueMovementEvaluator(LeagueMovementBalance.CreateDefault());

            LeagueMovementEvaluationResult smallSample = evaluator.Evaluate(CreateInput(sampleSize: 30));
            LeagueMovementEvaluationResult reliableSample = evaluator.Evaluate(CreateInput(sampleSize: 300));

            Assert.That(smallSample.Reliability, Is.EqualTo(0.1d).Within(0.0001d));
            Assert.That(reliableSample.Reliability, Is.EqualTo(1d));
            Assert.That(reliableSample.ProjectedOverall, Is.GreaterThan(smallSample.ProjectedOverall));
        }

        [Test]
        public void Evaluate_최소전력미만이면포지션수요가높아도승격자격이없다()
        {
            var evaluator = new LeagueMovementEvaluator(LeagueMovementBalance.CreateDefault());
            var input = new LeagueMovementEvaluationInput(
                playerOverall: 40,
                performanceRating: 50d,
                potentialRating: 50d,
                sampleSize: 300,
                reliableSampleSize: 300,
                levelPenalty: 2,
                minimumProjectedOverall: 47d,
                strongestCompetitorOverall: 55,
                weakestCompetitorOverall: 45,
                positionNeed: 95,
                teamBudget: 100,
                developmentRating: 100);

            LeagueMovementEvaluationResult result = evaluator.Evaluate(input);

            Assert.That(result.ProjectedOverall, Is.LessThan(47d));
            Assert.That(result.IsEligible, Is.False);
        }

        [Test]
        public void Evaluate_최소전력과경쟁자기준을통과하면정식오퍼후보가된다()
        {
            var evaluator = new LeagueMovementEvaluator(LeagueMovementBalance.CreateDefault());
            var input = new LeagueMovementEvaluationInput(
                playerOverall: 60,
                performanceRating: 60d,
                potentialRating: 60d,
                sampleSize: 300,
                reliableSampleSize: 300,
                levelPenalty: 2,
                minimumProjectedOverall: 47d,
                strongestCompetitorOverall: 80,
                weakestCompetitorOverall: 75,
                positionNeed: 65,
                teamBudget: 70,
                developmentRating: 75);

            LeagueMovementEvaluationResult result = evaluator.Evaluate(input);

            Assert.That(result.IsEligible, Is.True);
            Assert.That(result.InterestScore, Is.GreaterThan(LeagueMovementBalance.CreateDefault().InterestScoreThreshold));
        }

        private static LeagueMovementEvaluationInput CreateInput(int sampleSize)
        {
            return new LeagueMovementEvaluationInput(
                playerOverall: 50,
                performanceRating: 70d,
                potentialRating: 60d,
                sampleSize,
                reliableSampleSize: 300,
                levelPenalty: 2,
                minimumProjectedOverall: 47d,
                strongestCompetitorOverall: 65,
                weakestCompetitorOverall: 60,
                positionNeed: 70,
                teamBudget: 70,
                developmentRating: 70);
        }
    }
}
