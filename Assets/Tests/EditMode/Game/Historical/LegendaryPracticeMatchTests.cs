using System;
using System.Reflection;
using Baseball.Core.Balance;
using Baseball.Core.Historical;
using Baseball.Game.Historical;
using Baseball.Simulation.Match;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game.Historical
{
    public sealed class LegendaryPracticeMatchTests
    {
        [Test]
        public void 실제연습경기는정규진행을변경하지않고같은이벤트를재현한다()
        {
            object[] arguments = { null, null };
            typeof(ManagerModeMatchServiceTests).GetMethod("CreateRuntime", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, arguments);
            var runtime = (ManagerHistoricalRuntimeState)arguments[0]; var provider = (IHistoricalContentProvider)arguments[1];
            var balance = BalanceTable.CreateDefault();
            var adapter = new ManagerHistoricalSaveAdapter(provider, CardEditionBalanceTable.CreateInitial(), balance: balance);
            var copy = adapter.CreateSimulationCopy(runtime);
            var service = new ManagerModeMatchService(provider, balance);
            var next = service.PlayNextGame(copy).Match.Input;
            var opponent = next.AwayRoster.TeamId == runtime.ManagerMode.LiveSeason.PlayerTeamId ? next.HomeRoster : next.AwayRoster;
            var before = adapter.CreateSaveData(runtime);
            var first = new MatchEventBuffer(); var second = new MatchEventBuffer();
            var a = service.PlayPractice(runtime, opponent, 1, 93288UL, first);
            var b = service.PlayPractice(runtime, opponent, 1, 93288UL, second);
            Assert.That(a.Match.AwayBoxScore.Runs, Is.EqualTo(b.Match.AwayBoxScore.Runs));
            Assert.That(a.Match.HomeBoxScore.Runs, Is.EqualTo(b.Match.HomeBoxScore.Runs));
            CollectionAssert.AreEqual(first.ToArray(), second.ToArray());
            typeof(ManagerModeMatchServiceTests).GetMethod("AssertSaveFieldsEqual", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { before, adapter.CreateSaveData(runtime), "practice-save" });
            var catalog = LegendaryPracticeTests.Catalog();
            runtime.LegendaryPractice.Commit(catalog, "team100", 1, a.Match.HomeBoxScore.Runs, a.Match.AwayBoxScore.Runs, 1);
            var restored = adapter.Restore(adapter.CreateSaveData(runtime));
            Assert.That(restored.LegendaryPractice.Get("team100").attempts, Is.EqualTo(1));
        }
    }
}
