using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Core.Rules;
using Baseball.Core.Teams;
using Baseball.Simulation.Match;
using Baseball.Simulation.Random;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Simulation
{
    /// <summary>간이 경기의 결정론·기록 합계·종료 규칙과 상세 경로의 격리를 검증한다.</summary>
    public sealed class AggregateMatchSimulationTests
    {
        [Test]
        public void 같은Seed는전체기록과투수소모를재현한다()
        {
            for (ulong seed = 1; seed <= 100; seed++)
            {
                MatchResult first = Simulate(seed);
                MatchResult second = Simulate(seed);
                AssertValuesEqual(first.AwayBoxScore, second.AwayBoxScore);
                AssertValuesEqual(first.HomeBoxScore, second.HomeBoxScore);
                AssertValuesEqual(first.PitcherUsage, second.PitcherUsage);
                AssertValuesEqual(first.BatteryUsage, second.BatteryUsage);
                Assert.That(first.Events, Is.Empty);
                Assert.That(first.DecisionTrace, Is.Empty);
                Assert.That(first.Input.RulesVersion, Is.EqualTo(SimulationRulesVersion.AggregateV1));
                Assert.That(first.Input.VersionStamp.RulesVersion, Is.EqualTo((int)SimulationRulesVersion.AggregateV1));
            }
        }

        [Test]
        public void 천경기에서팀과선수의득점안타투수기록이일치한다()
        {
            for (ulong seed = 1; seed <= 1000; seed++)
            {
                MatchResult result = Simulate(seed);
                AssertTotals(result.AwayBoxScore, result.HomeBoxScore);
                AssertTotals(result.HomeBoxScore, result.AwayBoxScore);
                Assert.That(result.InningsPlayed, Is.InRange(9, 12));
            }
        }

        [Test]
        public void 포스트시즌에는반드시승자가있다()
        {
            for (ulong seed = 1; seed <= 100; seed++) Assert.That(Simulate(seed, true).IsTie, Is.False);
        }

        [Test]
        public void 간이경기에외부입력과중계를요청할수없다()
        {
            var invalid = new MatchExecutionProfile(SimulationEngineKind.AggregatePlateAppearance,
                MatchDecisionMode.ExternalInputAllowed, MatchEventMode.Full,
                MatchDecisionTraceMode.Full, MatchStatisticsMode.FullBoxScore);
            Assert.Throws<InvalidOperationException>(() => new MatchSimulator(BalanceTable.CreateDefault(), new Pcg32Random(1))
                .Simulate(CreateInput(1, false), NullMatchEventSink.Instance, invalid));
            Assert.Throws<InvalidOperationException>(() => new MatchSimulator(BalanceTable.CreateDefault(), new Pcg32Random(1))
                .Simulate(Simulate(1).Input, NullMatchEventSink.Instance, MatchExecutionProfile.DetailedBackground));
        }

        private static MatchResult Simulate(ulong seed, bool requiresWinner = false) =>
            new MatchSimulator(BalanceTable.CreateDefault(), MatchRandomStreams.Create(seed))
                .Simulate(CreateInput(seed, requiresWinner), NullMatchEventSink.Instance, MatchExecutionProfile.AggregateBackground);

        private static MatchInput CreateInput(ulong seed, bool requiresWinner) =>
            new MatchInput(1, (int)seed, seed, CreateTeam(1), CreateTeam(2), requiresWinner);

        private static Team CreateTeam(int id)
        {
            var slots = new LineupSlot[9];
            for (int index = 0; index < slots.Length; index++)
            {
                var position = (PlayerPosition)(index + 1);
                slots[index] = new LineupSlot(CreatePlayer(id * 100 + index, position), position);
            }
            return new Team(id, "검증 구단", new Lineup(slots),
                CreatePlayer(id * 100 + 90, PlayerPosition.StartingPitcher),
                CreatePlayer(id * 100 + 91, PlayerPosition.ReliefPitcher), 7);
        }

        private static Player CreatePlayer(int id, PlayerPosition position) => new Player(id, "검증 선수", position,
            Handedness.Right, Handedness.Right, new BatterAttributes(60, 60, 60, 60, 60, 60),
            new PitcherAttributes(60, 60, 60, 60, 60, 60));

        private static void AssertTotals(TeamBoxScore offense, TeamBoxScore defense)
        {
            Assert.That(offense.BattingLines.Sum(p => p.Runs), Is.EqualTo(offense.Runs));
            Assert.That(offense.RunsByInning.Sum(), Is.EqualTo(offense.Runs));
            Assert.That(offense.BattingLines.Sum(p => p.Hits), Is.EqualTo(offense.Hits));
            Assert.That(defense.PitchingLines.Sum(p => p.RunsAllowed), Is.EqualTo(offense.Runs));
            Assert.That(defense.PitchingLines.Sum(p => p.BattersFaced), Is.EqualTo(offense.BattingLines.Sum(p => p.PlateAppearances)));
            Assert.That(defense.PitchingLines.Sum(p => p.HitsAllowed), Is.EqualTo(offense.Hits));
            foreach (var pitcher in defense.PitchingLines)
            {
                Assert.That(pitcher.EarnedRuns, Is.LessThanOrEqualTo(pitcher.RunsAllowed));
                Assert.That(pitcher.PitchesThrown, Is.GreaterThanOrEqualTo(pitcher.Strikeouts * 3));
            }
        }

        private static void AssertValuesEqual(object first, object second)
        {
            if (first is IEnumerable collection && !(first is string))
            {
                object[] left = collection.Cast<object>().ToArray(), right = ((IEnumerable)second).Cast<object>().ToArray();
                Assert.That(right.Length, Is.EqualTo(left.Length));
                for (int index = 0; index < left.Length; index++) AssertValuesEqual(left[index], right[index]);
                return;
            }
            if (first.GetType().IsPrimitive || first is string || first.GetType().IsEnum)
            {
                Assert.That(second, Is.EqualTo(first));
                return;
            }
            foreach (PropertyInfo property in first.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                if (property.GetIndexParameters().Length == 0) AssertValuesEqual(property.GetValue(first), property.GetValue(second));
        }
    }
}
