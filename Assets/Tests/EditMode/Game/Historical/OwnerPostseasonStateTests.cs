using Baseball.Game.Historical;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game.Historical
{
    public sealed class OwnerPostseasonStateTests
    {
        [Test]
        public void EnsureCurrentSeries_4강두경기뒤시드순서로결승을만든다()
        {
            var postseason = new OwnerPostseasonState("OWNER-S01", new[] { 11, 22, 33, 44 });

            OwnerPostseasonSeriesState first = postseason.EnsureCurrentSeries(3, 5);

            Assert.That(first.SeriesId, Is.EqualTo("semifinal-a"));
            Assert.That(first.HigherSeedTeamId, Is.EqualTo(11));
            Assert.That(first.LowerSeedTeamId, Is.EqualTo(44));
            Assert.That(postseason.Series.Count, Is.EqualTo(2));
            WinSeries(first, first.HigherSeedTeamId, 100);

            OwnerPostseasonSeriesState second = postseason.EnsureCurrentSeries(3, 5);
            Assert.That(second.SeriesId, Is.EqualTo("semifinal-b"));
            WinSeries(second, second.LowerSeedTeamId, 200);

            OwnerPostseasonSeriesState championship = postseason.EnsureCurrentSeries(3, 5);

            Assert.That(championship.Round, Is.EqualTo(OwnerPostseasonRound.Championship));
            Assert.That(championship.HigherSeedTeamId, Is.EqualTo(11));
            Assert.That(championship.LowerSeedTeamId, Is.EqualTo(33));
            Assert.That(championship.WinsRequired, Is.EqualTo(3));
        }

        [Test]
        public void RecordCompletedGame_우승확정뒤결과를구단별로판정한다()
        {
            var postseason = new OwnerPostseasonState("OWNER-S02", new[] { 11, 22 });
            OwnerPostseasonSeriesState championship = postseason.EnsureCurrentSeries(3, 3);

            WinSeries(championship, championship.LowerSeedTeamId, 300);

            Assert.That(postseason.IsCompleted, Is.True);
            Assert.That(postseason.ChampionTeamId, Is.EqualTo(22));
            Assert.That(postseason.GetTeamResult(22), Is.EqualTo(OwnerTeamPostseasonResult.Champion));
            Assert.That(postseason.GetTeamResult(11), Is.EqualTo(OwnerTeamPostseasonResult.RunnerUp));
            Assert.That(postseason.GetTeamResult(99), Is.EqualTo(OwnerTeamPostseasonResult.DidNotQualify));
            Assert.That(championship.AppendNextGame(999, 999UL), Is.Null);
        }

        private static void WinSeries(OwnerPostseasonSeriesState series, int winnerTeamId, int gameIdBase)
        {
            for (int index = 0; index < series.WinsRequired; index++)
            {
                var game = series.AppendNextGame(gameIdBase + index, (ulong)(gameIdBase + index));
                bool winnerIsHome = game.HomeTeamId == winnerTeamId;
                game.Complete(winnerIsHome ? 0 : 1, winnerIsHome ? 1 : 0);
                series.RecordCompletedGame(game);
            }
        }
    }
}
