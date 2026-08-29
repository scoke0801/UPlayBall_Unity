using System.Linq;
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
    /// 이닝 종료, 득점, BoxScore, 이벤트 결정론을 검증한다.
    /// </summary>
    public sealed class MatchSimulatorTests
    {
        [Test]
        public void Simulate_세Out마다HalfInning을종료한다()
        {
            MatchInput input = CreateInput();
            var simulator = new MatchSimulator(
                BalanceTable.CreateDefault(),
                new SequenceRandom(0.5d),
                new ScriptedPlateAppearanceSimulator(PlateAppearanceResult.Strikeout));

            MatchResult result = simulator.Simulate(input);
            MatchEvent[] halfInningEvents = result.Events
                .Where(matchEvent => matchEvent.EventType == MatchEventType.HalfInningEnded)
                .ToArray();

            Assert.That(result.InningsPlayed, Is.EqualTo(12));
            Assert.That(halfInningEvents, Has.Length.EqualTo(24));
            Assert.That(halfInningEvents.All(matchEvent => matchEvent.Outs == 3), Is.True);
        }

        [Test]
        public void Simulate_만루HomeRun은네점을기록한다()
        {
            MatchInput input = CreateInput();
            var scriptedPlateAppearances = new ScriptedPlateAppearanceSimulator(
                PlateAppearanceResult.Strikeout,
                PlateAppearanceResult.Walk,
                PlateAppearanceResult.Walk,
                PlateAppearanceResult.Walk,
                PlateAppearanceResult.HomeRun);
            var simulator = new MatchSimulator(
                BalanceTable.CreateDefault(),
                new SequenceRandom(0.5d),
                scriptedPlateAppearances);

            MatchResult result = simulator.Simulate(input);
            PlayerBattingLine homeRunBatter = result.AwayBoxScore.BattingLines[3];

            Assert.That(result.AwayBoxScore.Runs, Is.EqualTo(4));
            Assert.That(result.AwayBoxScore.Hits, Is.EqualTo(1));
            Assert.That(homeRunBatter.HomeRuns, Is.EqualTo(1));
            Assert.That(homeRunBatter.RunsBattedIn, Is.EqualTo(4));
            Assert.That(result.HomeBoxScore.PitchingLine.RunsAllowed, Is.EqualTo(4));
        }

        [Test]
        public void Simulate_사구는타수없이출루하고만루에서밀어내기득점을만든다()
        {
            MatchInput input = CreateInput();
            var scriptedPlateAppearances = new ScriptedPlateAppearanceSimulator(
                PlateAppearanceResult.Strikeout,
                PlateAppearanceResult.HitByPitch,
                PlateAppearanceResult.HitByPitch,
                PlateAppearanceResult.HitByPitch,
                PlateAppearanceResult.HitByPitch);
            var simulator = new MatchSimulator(
                BalanceTable.CreateDefault(),
                new SequenceRandom(0.5d),
                scriptedPlateAppearances);

            MatchResult result = simulator.Simulate(input);
            PlayerBattingLine pushedRunBatter = result.AwayBoxScore.BattingLines[3];

            Assert.That(result.AwayBoxScore.Runs, Is.EqualTo(1));
            Assert.That(result.AwayBoxScore.Hits, Is.EqualTo(0));
            Assert.That(pushedRunBatter.HitByPitches, Is.EqualTo(1));
            Assert.That(pushedRunBatter.RunsBattedIn, Is.EqualTo(1));
            Assert.That(
                pushedRunBatter.AtBats,
                Is.EqualTo(pushedRunBatter.PlateAppearances - pushedRunBatter.HitByPitches),
                "사구는 타석에는 포함되지만 타수로는 기록하지 않는다.");
            Assert.That(result.HomeBoxScore.PitchingLine.HitBatters, Is.EqualTo(4));
            Assert.That(result.HomeBoxScore.PitchingLine.WalksAllowed, Is.EqualTo(0));
        }

        [Test]
        public void Simulate_끝내기Double은승리에필요한득점까지만기록한다()
        {
            MatchInput input = CreateInput();
            PlateAppearanceResult[] scriptedResults = Enumerable
                .Repeat(PlateAppearanceResult.Strikeout, 56)
                .ToArray();
            scriptedResults[0] = PlateAppearanceResult.HomeRun;
            scriptedResults[52] = PlateAppearanceResult.Walk;
            scriptedResults[53] = PlateAppearanceResult.Walk;
            scriptedResults[54] = PlateAppearanceResult.Walk;
            scriptedResults[55] = PlateAppearanceResult.Double;
            var simulator = new MatchSimulator(
                BalanceTable.CreateDefault(),
                new SequenceRandom(0d),
                new ScriptedPlateAppearanceSimulator(
                    PlateAppearanceResult.Strikeout,
                    scriptedResults));

            MatchResult result = simulator.Simulate(input);

            Assert.That(result.AwayBoxScore.Runs, Is.EqualTo(1));
            Assert.That(result.HomeBoxScore.Runs, Is.EqualTo(2));
            Assert.That(result.HomeBoxScore.BattingLines[0].Doubles, Is.EqualTo(1));
            Assert.That(result.HomeBoxScore.BattingLines[0].RunsBattedIn, Is.EqualTo(2));
        }

        [Test]
        public void Simulate_같은Seed와입력은이벤트가완전히같다()
        {
            MatchInput input = CreateInput();

            MatchResult first = new MatchSimulator(
                    BalanceTable.CreateDefault(),
                    new Pcg32Random(input.RandomSeed))
                .Simulate(input);
            MatchResult second = new MatchSimulator(
                    BalanceTable.CreateDefault(),
                    new Pcg32Random(input.RandomSeed))
                .Simulate(input);

            Assert.That(second.AwayBoxScore.Runs, Is.EqualTo(first.AwayBoxScore.Runs));
            Assert.That(second.HomeBoxScore.Runs, Is.EqualTo(first.HomeBoxScore.Runs));
            Assert.That(second.Events.Count, Is.EqualTo(first.Events.Count));
            for (int index = 0; index < first.Events.Count; index++)
                Assert.That(second.Events[index], Is.EqualTo(first.Events[index]), $"Event {index}");
        }

        [Test]
        public void Simulate_BoxScore팀합계와선수합계가일치한다()
        {
            MatchInput input = CreateInput();
            MatchResult result = new MatchSimulator(
                    BalanceTable.CreateDefault(),
                    new Pcg32Random(input.RandomSeed))
                .Simulate(input);

            AssertTeamTotals(result.AwayBoxScore);
            AssertTeamTotals(result.HomeBoxScore);
            Assert.That(
                result.AwayBoxScore.Runs,
                Is.EqualTo(result.HomeBoxScore.PitchingLine.RunsAllowed));
            Assert.That(
                result.HomeBoxScore.Runs,
                Is.EqualTo(result.AwayBoxScore.PitchingLine.RunsAllowed));
        }

        [Test]
        public void Simulate_피로기반교체후구원투수기록을별도로누적한다()
        {
            Team awayBase = SimulationTestFactory.CreateTeam(1, 50, 50);
            Team homeBase = SimulationTestFactory.CreateTeam(2, 50, 50);
            Team away = AddReliefPitcher(awayBase, 198);
            Team home = AddReliefPitcher(homeBase, 298);
            var input = new MatchInput(1, 1, 333UL, away, home);
            var simulator = new MatchSimulator(
                BalanceTable.CreateDefault(),
                new SequenceRandom(0.5d),
                new ScriptedPlateAppearanceSimulator(PlateAppearanceResult.Strikeout));

            MatchResult result = simulator.Simulate(input);

            Assert.That(result.AwayBoxScore.PitchingLines.Count, Is.EqualTo(2));
            Assert.That(result.AwayBoxScore.PitchingLines[0].OutsRecorded, Is.GreaterThan(0));
            Assert.That(result.AwayBoxScore.PitchingLines[1].OutsRecorded, Is.GreaterThan(0));
            Assert.That(result.HomeBoxScore.PitchingLines[0].OutsRecorded, Is.GreaterThan(0));
            Assert.That(result.HomeBoxScore.PitchingLines[1].OutsRecorded, Is.GreaterThan(0));
            Assert.That(result.AwayBoxScore.PitchingLines.Sum(line => line.OutsRecorded), Is.EqualTo(36));
            Assert.That(result.HomeBoxScore.PitchingLines.Sum(line => line.OutsRecorded), Is.EqualTo(36));
        }

        [Test]
        public void Simulate_승자필수경기는최대연장뒤에도무승부를남기지않는다()
        {
            MatchInput source = CreateInput();
            var input = new MatchInput(
                source.SeasonId,
                source.GameId,
                source.RandomSeed,
                source.AwayTeam,
                source.HomeTeam,
                requiresWinner: true);
            var simulator = new MatchSimulator(
                BalanceTable.CreateDefault(),
                new SequenceRandom(0.25d),
                new ScriptedPlateAppearanceSimulator(
                    PlateAppearanceResult.Strikeout,
                    PlateAppearanceResult.HomeRun));

            MatchResult result = simulator.Simulate(input);

            Assert.That(result.IsTie, Is.False);
            Assert.That(
                new[] { source.AwayTeam.TeamId, source.HomeTeam.TeamId },
                Does.Contain(result.WinnerTeamId));
        }

        [Test]
        public void Simulate_수비이닝과실제수비기회를BoxScore에남긴다()
        {
            MatchInput input = CreateInput();
            MatchResult result = new MatchSimulator(
                    BalanceTable.CreateDefault(),
                    new Pcg32Random(input.RandomSeed))
                .Simulate(input);

            Assert.That(result.AwayBoxScore.FieldingLines.Count, Is.EqualTo(9));
            Assert.That(result.HomeBoxScore.FieldingLines.Count, Is.EqualTo(9));
            Assert.That(result.AwayBoxScore.FieldingLines.Sum(line => line.DefensiveOuts), Is.GreaterThan(0));
            Assert.That(result.HomeBoxScore.FieldingLines.Sum(line => line.Opportunities), Is.GreaterThan(0));
        }

        [Test]
        public void SimulateUntilDecision_벤치선수가대타출전하면교체이벤트후입력을기다린다()
        {
            Team source = SimulationTestFactory.CreateTeam(1, 50, 50);
            LineupSlot targetSlot = source.Lineup[0];
            var substitute = new Player(
                999,
                "테스트 대타",
                targetSlot.FieldingPosition,
                Handedness.Left,
                Handedness.Right,
                    new BatterAttributes(100, 100, 75, 70, 50, 80),
                new PitcherAttributes(20, 20, 20, 20, 20, 20));
            var substitution = new PositionPlayerSubstitutionPlan(
                substitute,
                battingOrderIndex: 0,
                earliestInning: 1,
                maximumScoreDifference: 99);
            var away = new Team(
                source.TeamId,
                source.Name,
                source.Lineup,
                source.StartingPitcher,
                source.ReliefPitcher,
                source.ReliefStartInning,
                substitution);
            Team home = SimulationTestFactory.CreateTeam(2, 50, 50);
            var input = new MatchInput(1, 1, 778899UL, away, home);
            var simulator = new MatchSimulator(
                BalanceTable.CreateDefault(),
                new SequenceRandom(0.5d),
                new WaitingDecisionSource(substitute.PlayerId));

            MatchSimulationProgress progress = simulator.SimulateUntilDecision(input);

            Assert.That(progress.IsComplete, Is.False);
            Assert.That(progress.PendingDecision.HasValue, Is.True);
            Assert.That(progress.PendingDecision.Value.BatterId, Is.EqualTo(substitute.PlayerId));
            Assert.That(
                progress.Events.Any(matchEvent =>
                    matchEvent.EventType == MatchEventType.PinchHitterEntered &&
                    matchEvent.BatterId == substitute.PlayerId &&
                    matchEvent.PlayerId == targetSlot.Player.PlayerId),
                Is.True);
        }

        private static MatchInput CreateInput()
        {
            Team away = SimulationTestFactory.CreateTeam(1, 50, 50);
            Team home = SimulationTestFactory.CreateTeam(2, 50, 50);
            return new MatchInput(1, 1, 123456789UL, away, home);
        }

        private static Team AddReliefPitcher(Team source, int playerId)
        {
            var relief = new Baseball.Core.Players.Player(
                playerId,
                "테스트 구원",
                Baseball.Core.Players.PlayerPosition.ReliefPitcher,
                Baseball.Core.Players.Handedness.Right,
                Baseball.Core.Players.Handedness.Right,
                new Baseball.Core.Players.BatterAttributes(20, 20, 20, 20, 20, 20),
                new Baseball.Core.Players.PitcherAttributes(50, 50, 50, 50, 50, 50));
            return new Team(source.TeamId, source.Name, source.Lineup, source.StartingPitcher, relief, 7);
        }

        private static void AssertTeamTotals(TeamBoxScore boxScore)
        {
            int playerRuns = boxScore.BattingLines.Sum(line => line.Runs);
            int playerHits = boxScore.BattingLines.Sum(line => line.Hits);
            int inningRuns = boxScore.RunsByInning.Sum();

            Assert.That(playerRuns, Is.EqualTo(boxScore.Runs));
            Assert.That(playerHits, Is.EqualTo(boxScore.Hits));
            Assert.That(inningRuns, Is.EqualTo(boxScore.Runs));
        }

        private sealed class WaitingDecisionSource : IMatchDecisionSource
        {
            private readonly int _controlledPlayerId;

            public WaitingDecisionSource(int controlledPlayerId)
            {
                _controlledPlayerId = controlledPlayerId;
            }

            public bool RequiresBattingDecision(int batterId)
            {
                return batterId == _controlledPlayerId;
            }

            public bool TryGetBattingApproach(
                in MatchDecisionRequest request,
                out BattingApproach approach)
            {
                approach = BattingApproach.Balanced;
                return false;
            }
        }
    }
}
