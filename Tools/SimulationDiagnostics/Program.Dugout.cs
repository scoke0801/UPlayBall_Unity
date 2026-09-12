using System;
using System.IO;
using System.Text.Json;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Rules;
using Baseball.Core.Teams;
using Baseball.Simulation.Historical;
using Baseball.Simulation.Match;
using Baseball.Simulation.Random;

namespace Baseball.Tools.SimulationDiagnostics
{
    internal static partial class Program
    {
        /// <summary>전체 코칭 조합을 같은 선수·시드·상대로 비교해 경기 지표와 운영 편향을 측정한다.</summary>
        private static int RunDugoutComparison(string[] args)
        {
            int count = ParseCount(args, 1, 500);
            var data = JsonSerializer.Deserialize<DugoutStaffBalanceData>(File.ReadAllText(
                "Assets/10.Datas/Resources/NewGame/DugoutStaffBalance.json"), new JsonSerializerOptions { IncludeFields = true });
            var catalog = data.BuildCatalog();
            var resolver = new DugoutTacticalProfileResolver();
            var balance = Baseball.Tools.CommonMatchBalanceInput.Load();
            var roster = CreateRoster(1, 50, 50, 50);
            var opponent = CreateRoster(2, 50, 50, 50);
            var combined = new AggregateStatistics();
            int combinations = catalog.Managers.Count * catalog.HeadCoaches.Count;
            for (int variant = -1; variant < combinations; variant++)
            {
                var state = variant < 0 ? DugoutManagementState.CreateDefault() : new DugoutManagementState(
                    catalog.Managers[variant / catalog.HeadCoaches.Count].ManagerId,
                    catalog.HeadCoaches[variant % catalog.HeadCoaches.Count].HeadCoachId, DugoutPolicySettings.Neutral);
                var profile = resolver.Resolve(state, variant < 0 ? DugoutStaffCatalog.CreateDefault() : catalog);
                var team = new MatchRosterSnapshot(1, "덕아웃 검증팀", roster.StartingLineup,
                    roster.StartingPitcher, roster.Bullpen, roster.Bench, profile, RunningApproach.Balanced);
                var totals = new AggregateStatistics();
                int wins = 0;
                int games = variant < 0 ? count * 10 : count;
                for (int i = 0; i < games; i++)
                {
                    ulong seed = DeterministicSeed.Derive(0xD06007UL, (ulong)i);
                    var input = new MatchInput(1, i + 1, seed, team, opponent, MatchRules.CreateDefault(false));
                    var result = new MatchSimulator(balance, MatchRandomStreams.Create(seed))
                        .Simulate(input, NullMatchEventSink.Instance, MatchExecutionProfile.DetailedBackground);
                    totals.Add(result);
                    if (variant >= 0) combined.Add(result);
                    if (result.AwayBoxScore.Runs > result.HomeBoxScore.Runs) wins++;
                }
                Console.WriteLine($"{(variant < 0 ? "BASELINE" : state.ManagerId + "/" + state.HeadCoachId)} Games={games} Wins={wins}");
                Console.WriteLine(totals.Format(games));
            }
            Console.WriteLine("ALL_COMBINATIONS");
            Console.WriteLine(combined.Format(combinations * count));
            return 0;
        }
    }
}
