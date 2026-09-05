using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Career;
using Baseball.Simulation.Random;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Simulation.Career
{
    /// <summary>
    /// 감독 AI의 기용 판단이 기량·보직·Seed에 따라 재현되는지 검증한다.
    /// </summary>
    public sealed class ManagerUsageAiTests
    {
        [Test]
        public void DecideRole_경쟁자보다충분히좋은타자는선발한다()
        {
            Player player = CreatePlayer(PlayerPosition.Shortstop, 75);
            var ai = new ManagerUsageAi(
                CareerSeasonBalance.CreateDefault(),
                PlayerEvaluationBalance.CreateDefault());

            PlayerGameRole role = ai.DecideRole(
                player,
                ExpectedRole.StartingCompetition,
                strongestCompetitorOverall: 45,
                condition: 90,
                managerEvaluation: 50,
                teamGameNumber: 1,
                new Pcg32Random(1UL));

            Assert.That(role, Is.EqualTo(PlayerGameRole.StartingBatter));
        }

        [Test]
        public void DecideRole_같은Seed와입력은같은역할을반환한다()
        {
            Player player = CreatePlayer(PlayerPosition.ReliefPitcher, 55);
            var ai = new ManagerUsageAi(
                CareerSeasonBalance.CreateDefault(),
                PlayerEvaluationBalance.CreateDefault());

            PlayerGameRole first = ai.DecideRole(
                player, ExpectedRole.RosterCompetition, 58, 90, 50, 3, new Pcg32Random(55UL));
            PlayerGameRole second = ai.DecideRole(
                player, ExpectedRole.RosterCompetition, 58, 90, 50, 3, new Pcg32Random(55UL));

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void Decide_같은Seed와입력은판단근거와점수까지같다()
        {
            Player player = CreatePlayer(PlayerPosition.Shortstop, 40);
            var ai = new ManagerUsageAi(
                CareerSeasonBalance.CreateDefault(),
                PlayerEvaluationBalance.CreateDefault());

            ManagerUsageDecision first = ai.Decide(
                player, ExpectedRole.BenchCompetition, 90, 69, 45, 3, new Pcg32Random(55UL));
            ManagerUsageDecision second = ai.Decide(
                player, ExpectedRole.BenchCompetition, 90, 69, 45, 3, new Pcg32Random(55UL));

            Assert.That(second.Role, Is.EqualTo(first.Role));
            Assert.That(second.Reason, Is.EqualTo(first.Reason));
            Assert.That(second.DecisionScore, Is.EqualTo(first.DecisionScore));
            Assert.That(second.RequiredScore, Is.EqualTo(first.RequiredScore));
            Assert.That(second.ConditionAdjustment, Is.EqualTo(first.ConditionAdjustment));
            Assert.That(second.ManagerEvaluationAdjustment, Is.EqualTo(first.ManagerEvaluationAdjustment));
        }

        [Test]
        public void DecideRole_기량열세여도계약역할별최소평가기회를부여한다()
        {
            Player batter = CreatePlayer(PlayerPosition.Shortstop, 20);
            var ai = new ManagerUsageAi(
                CareerSeasonBalance.CreateDefault(),
                PlayerEvaluationBalance.CreateDefault());

            int startingCompetitionStarts = CountAppearances(
                ai, batter, ExpectedRole.StartingCompetition, PlayerGameRole.StartingBatter);
            int rosterCompetitionStarts = CountAppearances(
                ai, batter, ExpectedRole.RosterCompetition, PlayerGameRole.StartingBatter);
            int benchCompetitionStarts = CountAppearances(
                ai, batter, ExpectedRole.BenchCompetition, PlayerGameRole.StartingBatter);

            Assert.That(startingCompetitionStarts, Is.EqualTo(16));
            Assert.That(rosterCompetitionStarts, Is.EqualTo(8));
            Assert.That(benchCompetitionStarts, Is.EqualTo(5));
        }

        [Test]
        public void DecideRole_SP평가기회는자신의로테이션차례에만부여한다()
        {
            Player pitcher = CreatePlayer(PlayerPosition.StartingPitcher, 20);
            var ai = new ManagerUsageAi(
                CareerSeasonBalance.CreateDefault(),
                PlayerEvaluationBalance.CreateDefault());

            int startingCompetitionStarts = CountAppearances(
                ai, pitcher, ExpectedRole.StartingCompetition, PlayerGameRole.StartingPitcher);
            int rosterCompetitionStarts = CountAppearances(
                ai, pitcher, ExpectedRole.RosterCompetition, PlayerGameRole.StartingPitcher);
            int benchCompetitionStarts = CountAppearances(
                ai, pitcher, ExpectedRole.BenchCompetition, PlayerGameRole.StartingPitcher);

            Assert.That(startingCompetitionStarts, Is.EqualTo(16));
            Assert.That(rosterCompetitionStarts, Is.EqualTo(8));
            Assert.That(benchCompetitionStarts, Is.EqualTo(6));
        }

        [Test]
        public void DecideRole_SP로테이션차례가아니면투수휴식을반환한다()
        {
            Player pitcher = CreatePlayer(PlayerPosition.StartingPitcher, 90);
            var ai = new ManagerUsageAi(
                CareerSeasonBalance.CreateDefault(),
                PlayerEvaluationBalance.CreateDefault());

            PlayerGameRole role = ai.DecideRole(
                pitcher,
                ExpectedRole.StartingCompetition,
                strongestCompetitorOverall: 20,
                condition: 90,
                managerEvaluation: 90,
                teamGameNumber: 1,
                new Pcg32Random(1UL));

            Assert.That(role, Is.EqualTo(PlayerGameRole.PitcherRest));
        }

        [Test]
        public void Decide_SP로테이션차례가아니면명시적인휴식근거를반환한다()
        {
            Player pitcher = CreatePlayer(PlayerPosition.StartingPitcher, 90);
            var ai = new ManagerUsageAi(
                CareerSeasonBalance.CreateDefault(),
                PlayerEvaluationBalance.CreateDefault());

            ManagerUsageDecision decision = ai.Decide(
                pitcher,
                ExpectedRole.StartingCompetition,
                strongestCompetitorOverall: 20,
                condition: 90,
                managerEvaluation: 90,
                teamGameNumber: 1,
                new Pcg32Random(1UL));

            Assert.That(decision.Role, Is.EqualTo(PlayerGameRole.PitcherRest));
            Assert.That(decision.Reason, Is.EqualTo(ManagerUsageDecisionReason.RotationRest));
        }

        [Test]
        public void DecideRole_RP등판계획이없으면투수휴식을반환한다()
        {
            Player pitcher = CreatePlayer(PlayerPosition.ReliefPitcher, 20);
            var ai = new ManagerUsageAi(
                CareerSeasonBalance.CreateDefault(),
                PlayerEvaluationBalance.CreateDefault());

            PlayerGameRole role = ai.DecideRole(
                pitcher,
                ExpectedRole.BenchCompetition,
                strongestCompetitorOverall: 100,
                condition: 69,
                managerEvaluation: 50,
                teamGameNumber: 1,
                new Pcg32Random(1UL));

            Assert.That(role, Is.EqualTo(PlayerGameRole.PitcherRest));
        }

        [Test]
        public void DecideRole_평가기회라도낮은컨디션이면강행하지않는다()
        {
            Player player = CreatePlayer(PlayerPosition.Shortstop, 20);
            var ai = new ManagerUsageAi(
                CareerSeasonBalance.CreateDefault(),
                PlayerEvaluationBalance.CreateDefault());

            PlayerGameRole role = ai.DecideRole(
                player,
                ExpectedRole.BenchCompetition,
                strongestCompetitorOverall: 100,
                condition: 69,
                managerEvaluation: 50,
                teamGameNumber: 12,
                new Pcg32Random(1UL));

            Assert.That(role, Is.EqualTo(PlayerGameRole.Bench));
        }

        [Test]
        public void Decide_낮은컨디션으로경쟁에서밀리면조정치와원인을반환한다()
        {
            Player player = CreatePlayer(PlayerPosition.Shortstop, 20);
            var ai = new ManagerUsageAi(
                CareerSeasonBalance.CreateDefault(),
                PlayerEvaluationBalance.CreateDefault());

            ManagerUsageDecision decision = ai.Decide(
                player,
                ExpectedRole.BenchCompetition,
                strongestCompetitorOverall: 100,
                condition: 69,
                managerEvaluation: 50,
                teamGameNumber: 12,
                new Pcg32Random(1UL));

            Assert.That(decision.Role, Is.EqualTo(PlayerGameRole.Bench));
            Assert.That(decision.Reason, Is.EqualTo(ManagerUsageDecisionReason.CompetitionLoss));
            Assert.That(decision.ConditionAdjustment, Is.LessThan(0d));
            Assert.That(decision.ScoreMargin, Is.LessThan(0d));
        }

        [Test]
        public void DecideRole_포스트시즌에는평가기회를강제하지않는다()
        {
            Player player = CreatePlayer(PlayerPosition.Shortstop, 20);
            var ai = new ManagerUsageAi(
                CareerSeasonBalance.CreateDefault(),
                PlayerEvaluationBalance.CreateDefault());

            PlayerGameRole role = ai.DecideRole(
                player,
                ExpectedRole.BenchCompetition,
                strongestCompetitorOverall: 100,
                condition: 90,
                managerEvaluation: 50,
                teamGameNumber: 12,
                allowEvaluationOpportunity: false,
                new Pcg32Random(1UL));

            Assert.That(role, Is.EqualTo(PlayerGameRole.Bench));
        }

        private static int CountAppearances(
            ManagerUsageAi ai,
            Player player,
            ExpectedRole expectedRole,
            PlayerGameRole appearanceRole)
        {
            int appearances = 0;
            for (int gameNumber = 1; gameNumber <= 80; gameNumber++)
            {
                PlayerGameRole role = ai.DecideRole(
                    player,
                    expectedRole,
                    strongestCompetitorOverall: 100,
                    condition: 90,
                    managerEvaluation: 50,
                    teamGameNumber: gameNumber,
                    new Pcg32Random((ulong)gameNumber));
                if (role == appearanceRole)
                    appearances++;
            }
            return appearances;
        }

        private static Player CreatePlayer(PlayerPosition position, int rating)
        {
            return new Player(
                1_000_001,
                "테스트 선수",
                position,
                Handedness.Right,
                Handedness.Right,
                new BatterAttributes(rating, rating, rating, rating, rating, rating),
                new PitcherAttributes(rating, rating, rating, rating, rating, rating));
        }
    }
}
