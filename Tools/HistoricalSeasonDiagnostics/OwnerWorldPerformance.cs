using System.Diagnostics;
using System.Text.Json;
using Baseball.Core.Balance;
using Baseball.Core.Historical;
using Baseball.Game.Historical;
using Baseball.Simulation.Match;
using UnityEngine;

namespace Baseball.Tools.HistoricalSeasonDiagnostics;

/// <summary>역사 구단 전체 월드의 실제 로스터 구성·피로·팀컬러·기록 집계까지 포함한 시즌 비용을 측정한다.</summary>
internal static class OwnerWorldPerformance
{
    public static int Run(string[] args)
    {
        string root = Path.GetFullPath(args[1]);
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "manifest.json")));
        HistoricalRuntimeContentFile ReadEntry(string path) =>
            new(path, new TextAsset(File.ReadAllText(Path.Combine(root, path))));
        var catalog = new HistoricalRuntimeContentCatalog();
        catalog.Configure(new TextAsset(manifest.RootElement.GetRawText()), ReadEntry("player_persons.json"),
            manifest.RootElement.GetProperty("years").EnumerateArray().Select(year =>
                new HistoricalRuntimeYearContentFile(year.GetProperty("year").GetInt32(),
                    ReadEntry(year.GetProperty("path").GetString()))).ToArray());
        var provider = new UnityHistoricalContentProvider(catalog, HistoricalContentVerificationMode.Full);
        var content = provider.Load();
        var balance = BalanceTable.CreateDefault();
        var newGame = new ManagerHistoricalNewGameService(provider, new HistoricalWorldRuntimeBuilder(balance), balance);
        var profiles = args.Length > 2 && args[2] == "compare"
            ? new[] { MatchExecutionProfile.DetailedBackground, MatchExecutionProfile.AggregateBackground }
            : new[] { args.Length > 2 && args[2] == "detailed" ? MatchExecutionProfile.DetailedBackground : MatchExecutionProfile.AggregateBackground };
        foreach (var profile in profiles)
        {
            var runtime = newGame
                .Create(new ManagerHistoricalNewGameRequest(WorldRecordMode.SimulatedHistory, 20260909, 2024,
                    "검증-플레이어조", content.GetYear(2024).TeamSeasons[0].TeamSeasonKey,
                    new ManagerEconomyState(100000000, 100, 100, 100)));
            int total = runtime.LeagueWorld.Groups.Sum(g => g.Season.Schedule.Games.Count);
            Console.WriteLine($"Groups={runtime.LeagueWorld.Groups.Count} Teams={runtime.LeagueWorld.Rosters.Count} Games={total}");
            long allocated = GC.GetTotalAllocatedBytes(true);
            var timer = Stopwatch.StartNew();
            var session = new ManagerRegularSeasonSimulationSession(runtime,
                new ManagerModeMatchService(content, balance, offscreenExecutionProfile: profile));
            int steps = 0;
            while (!session.IsCompleted)
            {
                session.AdvanceNextStep();
                if (++steps % 5000 == 0) Console.WriteLine($"Steps={steps} Seconds={timer.Elapsed.TotalSeconds:F2}");
            }
            timer.Stop();
            Console.WriteLine($"Engine={profile.EngineKind} Completed={runtime.LeagueWorld.IsRegularSeasonCompleted} Seconds={timer.Elapsed.TotalSeconds:F3} " +
                $"AllocatedMB={(GC.GetTotalAllocatedBytes(true) - allocated) / 1048576.0:F1} Steps={steps}");
            if (!runtime.LeagueWorld.IsRegularSeasonCompleted) return 1;
        }
        return 0;
    }
}
