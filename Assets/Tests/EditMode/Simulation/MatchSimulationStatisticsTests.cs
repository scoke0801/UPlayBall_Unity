using Baseball.Core.Balance;
using Baseball.Core.Teams;
using Baseball.Simulation.Match;
using Baseball.Simulation.Random;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Simulation
{
    /// <summary>
    /// 대량 경기에서 기본 야구 지표와 능력치 격차가 유효한지 검증한다.
    /// </summary>
    public sealed class MatchSimulationStatisticsTests
    {
        private const int EqualTeamGames = 5000;
        private const int StrengthComparisonGames = 5000;

        [Test]
        public void Simulate_만경기에서기준통계와전력차가나타난다()
        {
            BalanceTable balance = BalanceTable.CreateDefault();
            Team averageA = SimulationTestFactory.CreateTeam(1, 50, 50);
            Team averageB = SimulationTestFactory.CreateTeam(2, 50, 50);
            var totals = new LeagueBattingTotals();

            for (int game = 0; game < EqualTeamGames; game++)
            {
                Team away = game % 2 == 0 ? averageA : averageB;
                Team home = game % 2 == 0 ? averageB : averageA;
                ulong seed = (ulong)(100000 + game);
                var input = new MatchInput(1, game + 1, seed, away, home);
                MatchResult result = new MatchSimulator(balance, new Pcg32Random(seed))
                    .Simulate(input, NullMatchEventSink.Instance);
                totals.Add(result.AwayBoxScore);
                totals.Add(result.HomeBoxScore);
            }

            Team strongTeam = SimulationTestFactory.CreateTeam(3, 56, 56, 56);
            Team weakTeam = SimulationTestFactory.CreateTeam(4, 48, 48, 48);
            int strongWins = 0;
            int ties = 0;

            for (int game = 0; game < StrengthComparisonGames; game++)
            {
                bool strongIsAway = game % 2 == 0;
                Team away = strongIsAway ? strongTeam : weakTeam;
                Team home = strongIsAway ? weakTeam : strongTeam;
                ulong seed = (ulong)(200000 + game);
                var input = new MatchInput(1, EqualTeamGames + game + 1, seed, away, home);
                MatchResult result = new MatchSimulator(balance, new Pcg32Random(seed))
                    .Simulate(input, NullMatchEventSink.Instance);

                if (result.IsTie)
                    ties++;
                else if (result.WinnerTeamId == strongTeam.TeamId)
                    strongWins++;
            }

            double battingAverage = totals.AtBats == 0 ? 0d : (double)totals.Hits / totals.AtBats;
            double onBasePercentage = totals.OnBaseDenominator == 0
                ? 0d
                : (double)(totals.Hits + totals.Walks) / totals.OnBaseDenominator;
            double sluggingPercentage = totals.AtBats == 0
                ? 0d
                : (double)totals.TotalBases / totals.AtBats;
            double runsPerTeamGame = (double)totals.Runs / (EqualTeamGames * 2);
            double walkRate = totals.PlateAppearances == 0
                ? 0d
                : (double)totals.Walks / totals.PlateAppearances;
            double strikeoutRate = totals.PlateAppearances == 0
                ? 0d
                : (double)totals.Strikeouts / totals.PlateAppearances;
            double strongWinRate = (double)strongWins / StrengthComparisonGames;

            TestContext.WriteLine(
                $"AVG {battingAverage:F3} / OBP {onBasePercentage:F3} / SLG {sluggingPercentage:F3} / " +
                $"R/G {runsPerTeamGame:F2} / BB% {walkRate:P1} / SO% {strikeoutRate:P1} / " +
                $"Strong W% {strongWinRate:P1} / Ties {ties}");

            Assert.That(battingAverage, Is.InRange(0.220d, 0.300d));
            Assert.That(onBasePercentage, Is.InRange(0.290d, 0.380d));
            Assert.That(sluggingPercentage, Is.InRange(0.330d, 0.470d));
            Assert.That(runsPerTeamGame, Is.InRange(3.2d, 5.8d));
            Assert.That(walkRate, Is.InRange(0.065d, 0.120d));
            Assert.That(strikeoutRate, Is.InRange(0.170d, 0.270d));
            Assert.That(strongWinRate, Is.GreaterThan(0.58d));
        }

        private sealed class LeagueBattingTotals
        {
            public int PlateAppearances { get; private set; }
            public int AtBats { get; private set; }
            public int Runs { get; private set; }
            public int Hits { get; private set; }
            public int Walks { get; private set; }
            public int Strikeouts { get; private set; }
            public int SacrificeFlies { get; private set; }
            public int TotalBases { get; private set; }
            public int OnBaseDenominator => AtBats + Walks + SacrificeFlies;

            public void Add(TeamBoxScore boxScore)
            {
                Runs += boxScore.Runs;
                for (int index = 0; index < boxScore.BattingLines.Count; index++)
                {
                    PlayerBattingLine line = boxScore.BattingLines[index];
                    PlateAppearances += line.PlateAppearances;
                    AtBats += line.AtBats;
                    Hits += line.Hits;
                    Walks += line.Walks;
                    Strikeouts += line.Strikeouts;
                    SacrificeFlies += line.SacrificeFlies;
                    int singles = line.Hits - line.Doubles - line.Triples - line.HomeRuns;
                    TotalBases += singles + line.Doubles * 2 + line.Triples * 3 + line.HomeRuns * 4;
                }
            }
        }
    }
}
