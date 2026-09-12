using Baseball.Game.Historical;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game.Historical
{
    public sealed class OwnerPostseasonStateTests
    {
        [TestCase(4, 10)]
        [TestCase(5, 12)]
        public void 상위시드가직행하고최하위시드도전승으로우승할수있다(int seeds, int expectedGames)
        {
            var ids = new int[seeds];
            for (int i = 0; i < seeds; i++) ids[i] = (i + 1) * 11;
            var postseason = new OwnerPostseasonState("KBO", ids);
            int games = 0;
            while (!postseason.IsCompleted)
            {
                var series = postseason.EnsureCurrentSeries();
                Assert.That(series.HigherSeedTeamId, Is.EqualTo((seeds - postseason.Series.Count) * 11));
                Assert.That(series.LowerSeedTeamId, Is.EqualTo(seeds * 11));
                Assert.That(postseason.HasRemainingGames(11), Is.True);
                while (!series.IsCompleted)
                {
                    Complete(series, series.LowerSeedTeamId, ++games);
                    // 실제 저장과 동일하게 경기·승수만으로 매 경기 복원한다.
                    var copy = new OwnerPostseasonSeriesState(series.SeriesId, series.Round,
                        series.HigherSeedTeamId, series.LowerSeedTeamId, series.SeriesGames,
                        series.Games, series.HigherSeedWins, series.LowerSeedWins);
                    Assert.That(copy.WinnerTeamId, Is.EqualTo(series.WinnerTeamId));
                }
                var restored = new OwnerPostseasonState("KBO", ids, postseason.Series);
                Assert.That(restored.ChampionTeamId, Is.EqualTo(postseason.ChampionTeamId));
            }
            Assert.That(games, Is.EqualTo(expectedGames));
            Assert.That(postseason.GetTeamResult(seeds * 11), Is.EqualTo(OwnerTeamPostseasonResult.Champion));
            Assert.That(postseason.GetTeamResult(11), Is.EqualTo(OwnerTeamPostseasonResult.RunnerUp));
            Assert.That(postseason.GetTeamResult(22), Is.EqualTo(OwnerTeamPostseasonResult.PlayoffElimination));
            Assert.That(postseason.GetTeamResult(33), Is.EqualTo(OwnerTeamPostseasonResult.SemiPlayoffElimination));
            Assert.That(postseason.HasRemainingGames(seeds * 11), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void 와일드카드4위는첫승또는무승부로진출하며실제승수를보존한다(bool draw)
        {
            var postseason = new OwnerPostseasonState("WC", new[] { 1, 2, 3, 4, 5 });
            var series = postseason.EnsureCurrentSeries();
            Complete(series, draw ? 0 : 4, 1);
            Assert.That(series.WinnerTeamId, Is.EqualTo(4));
            Assert.That(series.HigherSeedWins, Is.EqualTo(draw ? 0 : 1));
            Assert.That(series.Draws, Is.EqualTo(draw ? 1 : 0));
            Assert.That(series.AppendNextGame(2, 2), Is.Null);
            Assert.That(postseason.HasRemainingGames(5), Is.False);
            var copy = new OwnerPostseasonSeriesState(series.SeriesId, series.Round, 4, 5, 2,
                series.Games, series.HigherSeedWins, series.LowerSeedWins);
            Assert.That(copy.WinnerTeamId, Is.EqualTo(4));
            Assert.That(postseason.EnsureCurrentSeries().HigherSeedTeamId, Is.EqualTo(3));
        }

        [TestCase(OwnerPostseasonRound.WildCard, 2, "HH")]
        [TestCase(OwnerPostseasonRound.SemiPlayoff, 5, "HHAAH")]
        [TestCase(OwnerPostseasonRound.Playoff, 5, "HHAAH")]
        [TestCase(OwnerPostseasonRound.Championship, 7, "HHAAAHH")]
        public void 홈구장순서를KBO규정과일치시킨다(OwnerPostseasonRound round, int length, string homes)
        {
            var series = new OwnerPostseasonSeriesState("HOME", round, 1, 2, length);
            for (int i = 0; i < homes.Length; i++)
            {
                var game = series.AppendNextGame(i + 1, (ulong)i + 1);
                Assert.That(game.HomeTeamId, Is.EqualTo(homes[i] == 'H' ? 1 : 2));
                int winner = i % 2 == 0 ? 2 : 1;
                game.Complete(game.AwayTeamId == winner ? 1 : 0, game.HomeTeamId == winner ? 1 : 0);
                series.RecordCompletedGame(game);
            }
        }

        [Test]
        public void 일반시리즈무승부는승수없이추가경기로이어진다()
        {
            var series = new OwnerPostseasonSeriesState("DRAW", OwnerPostseasonRound.Playoff, 1, 2, 5);
            Complete(series, 0, 1);
            Assert.That(series.IsCompleted, Is.False);
            Assert.That(series.Draws, Is.EqualTo(1));
            for (int i = 0; i < 3; i++) Complete(series, 1, i + 2);
            Assert.That(series.WinnerTeamId, Is.EqualTo(1));
            Assert.That(series.Games.Count, Is.EqualTo(4));
        }

        [Test]
        public void 무승부재경기는예정최종전뒤원래홈구장에서진행한다()
        {
            var series = new OwnerPostseasonSeriesState("REPLAY", OwnerPostseasonRound.Playoff, 1, 2, 5);
            Complete(series, 1, 1);
            Complete(series, 2, 2);
            Complete(series, 0, 3);
            Complete(series, 1, 4);
            Complete(series, 2, 5);
            var replay = series.AppendNextGame(6, 6);
            Assert.That(replay.HomeTeamId, Is.EqualTo(2));
        }

        [Test]
        public void 동일입력10000개대진이같은승자와경기수를낸다()
        {
            for (int seed = 0; seed < 10000; seed++)
            {
                Assert.That(RunBracket(seed), Is.EqualTo(RunBracket(seed)));
            }
        }

        private static (int champion, int games) RunBracket(int seed)
        {
            var random = new System.Random(seed);
            var postseason = new OwnerPostseasonState("MASS", new[] { 1, 2, 3, 4, 5 });
            int games = 0;
            while (!postseason.IsCompleted)
            {
                var series = postseason.EnsureCurrentSeries();
                while (!series.IsCompleted)
                    Complete(series, random.Next(2) == 0 ? series.HigherSeedTeamId : series.LowerSeedTeamId, ++games);
            }
            Assert.That(games, Is.InRange(11, 19));
            return (postseason.ChampionTeamId, games);
        }

        private static void Complete(OwnerPostseasonSeriesState series, int winner, int id)
        {
            var game = series.AppendNextGame(id, (ulong)id);
            game.Complete(game.AwayTeamId == winner ? 1 : 0, game.HomeTeamId == winner ? 1 : 0);
            series.RecordCompletedGame(game);
        }
    }
}
