using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Simulation.Match;
using Baseball.Simulation.Random;

namespace Baseball.Tools.SimulationDiagnostics
{
    internal static partial class Program
    {
        private static int RunAggregateHistoricalComparison(string[] args)
        {
            int gamesPerPair = ParseCount(args, 2, 200);
            var all = new List<StrengthTeam>();
            foreach (string path in Directory.GetFiles(Path.Combine(args[1], "Years"), "*.json").OrderBy(p => p, StringComparer.Ordinal))
                all.AddRange(ReadStrengthTeams(args[1], int.Parse(Path.GetFileNameWithoutExtension(path))).Values);
            // 특정 구단을 유리하게 선택하지 않고 모든 시대의 전력 구간에서 균등 추출한다.
            var ordered = all.OrderBy(t => Enumerable.Range(0, 9).Average(i =>
                t.Rotations[0].StartingLineup[i].Player.BatterAttributes.Contact +
                t.Rotations[0].StartingLineup[i].Player.BatterAttributes.Power) +
                t.Rotations.Average(r => r.StartingPitcher.Player.PitcherAttributes.Stuff +
                    r.StartingPitcher.Player.PitcherAttributes.Control)).ThenBy(t => t.Key, StringComparer.Ordinal).ToArray();
            var teams = Enumerable.Range(0, 8).Select(i => ordered[i * (ordered.Length - 1) / 7]).ToArray();
            var rates = new List<double[]>();
            AggregateStatistics detailedStats = null;
            foreach (var profile in new[] { MatchExecutionProfile.DetailedBackground, MatchExecutionProfile.AggregateBackground })
            {
                var stats = new AggregateStatistics();
                int[] wins = new int[teams.Length], losses = new int[teams.Length];
                int gameId = 0;
                var timer = Stopwatch.StartNew();
                var balance = BalanceTable.CreateDefault();
                for (int left = 0; left < teams.Length; left++)
                for (int right = left + 1; right < teams.Length; right++)
                for (int sample = 0; sample < gamesPerPair; sample++)
                {
                    int rotation = sample / 2 % 25;
                    bool leftHome = sample % 2 == 0;
                    var leftRoster = teams[left].Rotations[rotation % 5];
                    var rightRoster = teams[right].Rotations[rotation / 5];
                    ulong seed = DeterministicSeed.Derive(0xA661157UL, (ulong)++gameId);
                    var input = new MatchInput(1, gameId, seed, leftHome ? rightRoster : leftRoster,
                        leftHome ? leftRoster : rightRoster, Baseball.Core.Rules.MatchRules.CreateDefault(false));
                    var result = new MatchSimulator(balance, MatchRandomStreams.Create(seed))
                        .Simulate(input, NullMatchEventSink.Instance, profile);
                    stats.Add(result);
                    int difference = result.HomeBoxScore.Runs - result.AwayBoxScore.Runs;
                    if (!leftHome) difference = -difference;
                    if (difference > 0) { wins[left]++; losses[right]++; }
                    if (difference < 0) { wins[right]++; losses[left]++; }
                }
                timer.Stop();
                if (detailedStats == null) detailedStats = stats;
                else stats.ValidateAggregateAgainst(detailedStats, gameId);
                rates.Add(Enumerable.Range(0, teams.Length).Select(i => (double)wins[i] / (wins[i] + losses[i])).ToArray());
                Console.WriteLine($"Engine={profile.EngineKind} ms/game={timer.Elapsed.TotalMilliseconds / gameId:F3}");
                Console.WriteLine(stats.Format(gameId));
                for (int i = 0; i < teams.Length; i++) Console.WriteLine($"{teams[i].Key} W={wins[i]} L={losses[i]} Win%={rates.Last()[i] * 100:F2}");
            }
            double maxDifference = rates[0].Zip(rates[1], (a, b) => Math.Abs(a - b)).Max();
            int[] detailedOrder = Enumerable.Range(0, teams.Length).OrderByDescending(i => rates[0][i]).ToArray();
            int[] aggregateOrder = Enumerable.Range(0, teams.Length).OrderByDescending(i => rates[1][i]).ToArray();
            double squaredRanks = Enumerable.Range(0, teams.Length).Sum(i => Math.Pow(
                Array.IndexOf(detailedOrder, i) - Array.IndexOf(aggregateOrder, i), 2));
            double correlation = 1 - 6 * squaredRanks / (teams.Length * (teams.Length * teams.Length - 1d));
            Console.WriteLine($"MaximumWinRateDifference={maxDifference:F4} RankCorrelation={correlation:F4}");
            return maxDifference <= 0.08 && correlation >= 0.8 ? 0 : 1;
        }

        private static int RunAggregateMatchComparison(string[] args)
        {
            int count = ParseCount(args, 1, 1000);
            foreach (int strength in new[] { 50, 55, 65 })
            {
                var away = CreateRoster(1, strength, strength, strength);
                var home = CreateRoster(2, 50, 50, 50);
                foreach (var profile in new[] { MatchExecutionProfile.DetailedBackground, MatchExecutionProfile.AggregateBackground })
                {
                    var balance = BalanceTable.CreateDefault();
                    var stats = new AggregateStatistics();
                    int wins = 0, draws = 0;
                    var timer = Stopwatch.StartNew();
                    for (int index = 0; index < count; index++)
                    {
                        ulong seed = DeterministicSeed.Derive(0xA661UL, (ulong)index);
                        var input = new MatchInput(1, index + 1, seed, away, home,
                            Baseball.Core.Rules.MatchRules.CreateDefault(false));
                        var result = new MatchSimulator(balance, MatchRandomStreams.Create(seed))
                            .Simulate(input, NullMatchEventSink.Instance, profile);
                        stats.Add(result);
                        if (result.AwayBoxScore.Runs > result.HomeBoxScore.Runs) wins++;
                        if (result.IsTie) draws++;
                    }
                    timer.Stop();
                    Console.WriteLine($"Engine={profile.EngineKind} Strength={strength}/50 Win%={(double)wins / (count - draws) * 100:F2} ms/game={timer.Elapsed.TotalMilliseconds / count:F3}");
                    Console.WriteLine(stats.Format(count));
                }
            }
            return 0;
        }
    }
}
