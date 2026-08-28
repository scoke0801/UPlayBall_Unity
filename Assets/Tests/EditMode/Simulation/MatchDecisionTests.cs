using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Match;
using Baseball.Simulation.PlateAppearance;
using Baseball.Simulation.Random;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Simulation
{
    /// <summary>
    /// 타격 선택 재생의 결정론과 접근 방식별 통계 방향을 검증한다.
    /// </summary>
    public sealed class MatchDecisionTests
    {
        [Test]
        public void SimulateUntilDecision_균형선택재생은일반경기와완전히같다()
        {
            MatchInput input = CreateInput(912345UL);
            int controlledPlayerId = input.AwayTeam.Lineup[0].Player.PlayerId;
            var decisions = new List<BattingApproach>();
            MatchSimulationProgress progress = null;

            for (int index = 0; index < 128; index++)
            {
                progress = new MatchSimulator(
                        BalanceTable.CreateDefault(),
                        new Pcg32Random(input.RandomSeed),
                        new RecordedMatchDecisionSource(controlledPlayerId, decisions))
                    .SimulateUntilDecision(input);
                if (progress.IsComplete)
                    break;
                decisions.Add(BattingApproach.Balanced);
            }

            MatchResult expected = new MatchSimulator(
                    BalanceTable.CreateDefault(),
                    new Pcg32Random(input.RandomSeed))
                .Simulate(input);

            Assert.That(progress, Is.Not.Null);
            Assert.That(progress.IsComplete, Is.True);
            Assert.That(progress.Result.AwayBoxScore.Runs, Is.EqualTo(expected.AwayBoxScore.Runs));
            Assert.That(progress.Result.HomeBoxScore.Runs, Is.EqualTo(expected.HomeBoxScore.Runs));
            Assert.That(progress.Events.Count, Is.EqualTo(expected.Events.Count));
            for (int index = 0; index < expected.Events.Count; index++)
                Assert.That(progress.Events[index], Is.EqualTo(expected.Events[index]), $"Event {index}");
        }

        [Test]
        public void SimulateUntilDecision_선택이없으면내선수첫투구전에멈춘다()
        {
            MatchInput input = CreateInput(777UL);
            int controlledPlayerId = input.AwayTeam.Lineup[0].Player.PlayerId;

            MatchSimulationProgress progress = new MatchSimulator(
                    BalanceTable.CreateDefault(),
                    new Pcg32Random(input.RandomSeed),
                    new RecordedMatchDecisionSource(controlledPlayerId, new BattingApproach[0]))
                .SimulateUntilDecision(input);

            Assert.That(progress.IsComplete, Is.False);
            Assert.That(progress.PendingDecision.HasValue, Is.True);
            Assert.That(progress.PendingDecision.Value.BatterId, Is.EqualTo(controlledPlayerId));
            Assert.That(progress.PendingDecision.Value.PitchNumber, Is.EqualTo(1));
            Assert.That(progress.PendingDecision.Value.Balls, Is.EqualTo(0));
            Assert.That(progress.PendingDecision.Value.Strikes, Is.EqualTo(0));
        }

        [Test]
        public void BattingApproach_대량타석에서의도한장단점이나타난다()
        {
            const int sampleCount = 20000;
            ApproachStatistics contact = SimulateApproach(BattingApproach.Contact, sampleCount);
            ApproachStatistics power = SimulateApproach(BattingApproach.Power, sampleCount);
            ApproachStatistics patient = SimulateApproach(BattingApproach.Patient, sampleCount);
            ApproachStatistics balanced = SimulateApproach(BattingApproach.Balanced, sampleCount);

            Assert.That(contact.Strikeouts, Is.LessThan(power.Strikeouts));
            Assert.That(power.HomeRuns, Is.GreaterThan(contact.HomeRuns));
            Assert.That(patient.Walks, Is.GreaterThan(balanced.Walks));
        }

        private static ApproachStatistics SimulateApproach(BattingApproach approach, int sampleCount)
        {
            PlateAppearanceMatchup matchup = CreateMatchup();
            var result = new ApproachStatistics();
            for (int index = 0; index < sampleCount; index++)
            {
                var simulator = new PlateAppearanceSimulator(
                    BalanceTable.CreateDefault(),
                    new Pcg32Random((ulong)(index + 1)));
                PlateAppearanceResult plateAppearanceResult = Simulate(simulator, matchup, approach);
                if (plateAppearanceResult == PlateAppearanceResult.Strikeout) result.Strikeouts++;
                else if (plateAppearanceResult == PlateAppearanceResult.Walk) result.Walks++;
                else if (plateAppearanceResult == PlateAppearanceResult.HomeRun) result.HomeRuns++;
            }
            return result;
        }

        private static PlateAppearanceResult Simulate(
            PlateAppearanceSimulator simulator,
            PlateAppearanceMatchup matchup,
            BattingApproach approach)
        {
            int balls = 0;
            int strikes = 0;
            for (int pitchNumber = 1; pitchNumber <= 32; pitchNumber++)
            {
                PitchResult pitch = simulator.SimulatePitch(matchup, balls, strikes, pitchNumber, approach);
                if (pitch == PitchResult.Ball && ++balls >= 4)
                    return PlateAppearanceResult.Walk;
                if ((pitch == PitchResult.CalledStrike || pitch == PitchResult.SwingingStrike) && ++strikes >= 3)
                    return PlateAppearanceResult.Strikeout;
                if (pitch == PitchResult.Foul && strikes < 2)
                    strikes++;
                if (pitch == PitchResult.InPlay)
                    return simulator.ResolveBallInPlay(matchup, approach);
            }
            Assert.Fail("타석이 안전 한도 안에 끝나지 않았습니다.");
            return PlateAppearanceResult.None;
        }

        private static MatchInput CreateInput(ulong seed)
        {
            Team away = SimulationTestFactory.CreateTeam(1, 50, 50);
            Team home = SimulationTestFactory.CreateTeam(2, 50, 50);
            return new MatchInput(1, 1, seed, away, home);
        }

        private static PlateAppearanceMatchup CreateMatchup()
        {
            var batter = new Player(
                1,
                "접근 테스트 타자",
                PlayerPosition.Shortstop,
                Handedness.Right,
                Handedness.Right,
                new BatterAttributes(55, 55, 50, 40, 50, 55),
                new PitcherAttributes(20, 20, 20, 20, 20, 20));
            var pitcher = new Player(
                2,
                "접근 테스트 투수",
                PlayerPosition.StartingPitcher,
                Handedness.Right,
                Handedness.Right,
                new BatterAttributes(20, 20, 20, 20, 20, 20),
                new PitcherAttributes(55, 55, 55, 55, 55, 55));
            return new PlateAppearanceMatchup(batter, pitcher, 50d, false);
        }

        private struct ApproachStatistics
        {
            public int Strikeouts;
            public int Walks;
            public int HomeRuns;
        }
    }
}
