using System.Diagnostics;
using System.Text.Json;
using Baseball.Core.Balance;
using Baseball.Core.Historical;
using Baseball.Game.Historical;
using Baseball.Simulation.Match;
using Baseball.Game.Diagnostics;
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
        var balance = Baseball.Tools.CommonMatchBalanceInput.Load();
        var newGame = new ManagerHistoricalNewGameService(provider, new HistoricalWorldRuntimeBuilder(balance), balance);
        bool verifyWorker = args.Length > 2 && args[2] == "verify-worker";
        bool inspectCopy = args.Length > 2 && args[2] == "inspect-copy";
        bool benchmark = args.Length > 2 && args[2] == "benchmark";
        int runIndex = 0;
        JsonElement? expectedState = null;
        var profiles = benchmark
            ? Enumerable.Repeat(MatchExecutionProfile.AggregateBackground, 4).ToArray()
            : verifyWorker
            ? new[] { MatchExecutionProfile.AggregateBackground, MatchExecutionProfile.AggregateBackground }
            : args.Length > 2 && args[2] == "compare"
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
            var stages = args.Length > 2 && args[2] == "profile" ? new StageSink() : null;
            ProfilerSectionSink.Current = stages;
            var timer = Stopwatch.StartNew();
            bool workerMode = inspectCopy || args.Length > 2 && args[2] == "worker" || verifyWorker && expectedState.HasValue ||
                benchmark && (runIndex == 1 || runIndex == 2);
            bool framedMode = args.Length > 2 && args[2] == "frame60" || benchmark && !workerMode;
            Console.WriteLine($"Run={++runIndex} Mode={(workerMode ? "Worker" : framedMode ? "Frame60" : "Sequential")}");
            if (workerMode)
            {
                var adapter = new ManagerHistoricalSaveAdapter(provider, CardEditionBalanceTable.CreateInitial(), balance: balance);
                var beforeCopy = verifyWorker || inspectCopy ? adapter.CreateSaveData(runtime) : null;
                var beforeRuntime = runtime;
                runtime = adapter.CreateSimulationCopy(runtime);
                Console.WriteLine($"CloneSeconds={timer.Elapsed.TotalSeconds:F3}");
                if (verifyWorker || inspectCopy)
                {
                    var options = new JsonSerializerOptions { IncludeFields = true };
                    Console.WriteLine($"CloneDifferences={CompareState(JsonSerializer.SerializeToElement(beforeCopy, options), JsonSerializer.SerializeToElement(adapter.CreateSaveData(runtime), options), "copy", 0)}");
                    int inputsChanged = 0;
                    foreach (var roster in beforeRuntime.WorldRosters)
                        foreach (var entry in roster.Entries)
                        {
                            var left = beforeRuntime.WorldCardCatalog.GetRequiredCard(entry.CardId);
                            var right = runtime.WorldCardCatalog.GetRequiredCard(entry.CardId);
                            inputsChanged = CompareState(JsonSerializer.SerializeToElement(left), JsonSerializer.SerializeToElement(right), entry.CardId, inputsChanged);
                            inputsChanged = CompareState(JsonSerializer.SerializeToElement(beforeRuntime.WorldCardCatalog.GetPlayerSeason(left)),
                                JsonSerializer.SerializeToElement(runtime.WorldCardCatalog.GetPlayerSeason(right)), entry.PlayerSeasonId, inputsChanged);
                        }
                    Console.WriteLine($"InputDifferences={inputsChanged}");
                    if (inspectCopy)
                    {
                        var leftService = new ManagerModeMatchService(content, balance);
                        var rightService = new ManagerModeMatchService(content, balance);
                        int rosterDifferences = 0;
                        foreach (var roster in beforeRuntime.WorldRosters.ToArray())
                        {
                            if (beforeRuntime.HasOwnedEconomy(roster.TeamSeasonKey)) continue;
                            rosterDifferences = CompareState(CaptureTeamInput(leftService, beforeRuntime, roster.TeamSeasonKey),
                                CaptureTeamInput(rightService, runtime, roster.TeamSeasonKey), "input." + roster.TeamSeasonKey, rosterDifferences);
                        }
                        Console.WriteLine($"RosterInputDifferences={rosterDifferences}");
                        var leftSession = new ManagerRegularSeasonSimulationSession(beforeRuntime, new ManagerModeMatchService(content, balance));
                        var rightSession = new ManagerRegularSeasonSimulationSession(runtime, new ManagerModeMatchService(content, balance));
                        for (int index = 0; index < 200; index++)
                        {
                            leftSession.AdvanceNextStep();
                            rightSession.AdvanceNextStep();
                        }
                        int stepDifferences = CompareState(JsonSerializer.SerializeToElement(adapter.CreateSaveData(beforeRuntime), options),
                            JsonSerializer.SerializeToElement(adapter.CreateSaveData(runtime), options), "step200", 0);
                        Console.WriteLine($"SequentialCopyDifferences={stepDifferences}");
                        return stepDifferences == 0 ? 0 : 1;
                    }
                }
            }
            var session = new ManagerRegularSeasonSimulationSession(runtime,
                new ManagerModeMatchService(content, balance, offscreenExecutionProfile: profile));
            int steps = 0;
            if (workerMode)
            {
                var worker = new ManagerRegularSeasonSimulationWorker(session);
                while (!worker.IsCompleted) Thread.Sleep(10);
                if (worker.Fault != null) throw worker.Fault;
                steps = worker.ReadProgress().LeagueGamesSimulated + 1;
            }
            else
            {
                while (!session.IsCompleted)
                {
                    long frameStart = Stopwatch.GetTimestamp();
                    int frameSteps = 0;
                    do
                    {
                        session.AdvanceNextStep();
                        frameSteps++;
                        if (++steps % 5000 == 0) Console.WriteLine($"Steps={steps} Seconds={timer.Elapsed.TotalSeconds:F2}");
                    } while (framedMode && !session.IsCompleted && frameSteps < 256 &&
                        Stopwatch.GetElapsedTime(frameStart).TotalMilliseconds < 8);
                    if (framedMode && !session.IsCompleted)
                    {
                        // Windows Sleep의 약 15ms 타이머 양자화가 60fps 대기를 부풀리지 않게 한다.
                        // 진단용 프레임 모사일 뿐 Production Worker는 대기하지 않는다.
                        while (Stopwatch.GetElapsedTime(frameStart).TotalMilliseconds < 1000d / 60)
                            Thread.SpinWait(64);
                    }
                }
            }
            timer.Stop();
            ProfilerSectionSink.Current = null;
            stages?.Print();
            Console.WriteLine($"Engine={profile.EngineKind} Completed={runtime.LeagueWorld.IsRegularSeasonCompleted} Seconds={timer.Elapsed.TotalSeconds:F3} " +
                $"AllocatedMB={(GC.GetTotalAllocatedBytes(true) - allocated) / 1048576.0:F1} Steps={steps}");
            // 계측 밖에서 저장 가능한 전체 상태를 비교해 피로·경제·개인 기록 누락도 검출한다.
            var save = new ManagerHistoricalSaveAdapter(provider, CardEditionBalanceTable.CreateInitial(), balance: balance)
                .CreateSaveData(runtime);
            byte[] stateBytes = JsonSerializer.SerializeToUtf8Bytes(save, new JsonSerializerOptions { IncludeFields = true });
            Console.WriteLine($"StateSHA256={Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stateBytes))}");
            if (verifyWorker || benchmark)
            {
                var actualState = JsonDocument.Parse(stateBytes).RootElement.Clone();
                if (expectedState.HasValue)
                {
                    int differences = CompareState(expectedState.Value, actualState, "$", 0);
                    Console.WriteLine($"StateDifferences={differences}");
                    if (differences != 0) return 1;
                }
                else expectedState = actualState;
            }
            if (!runtime.LeagueWorld.IsRegularSeasonCompleted) return 1;
            var playerGroup = runtime.LeagueWorld.GetGroup(runtime.PlayerTeamSeasonKey);
            foreach (var group in runtime.LeagueWorld.Groups)
            {
                var players = group.Season.Statistics.RegularSeason.Players.Values;
                var hitters = players.Where(p => p.TeamGames > 0 && p.Batting.PlateAppearances >= p.TeamGames * 3.1).ToArray();
                var pitchers = players.Where(p => p.TeamGames > 0 && p.Pitching.OutsRecorded >= p.TeamGames * 3).ToArray();
                long atBats = players.Sum(p => (long)p.Batting.AtBats);
                long outs = players.Sum(p => (long)p.Pitching.OutsRecorded);
                Console.WriteLine($"Group={group.League.LeagueInstanceId} PlayerGroup={ReferenceEquals(group, playerGroup)} " +
                    $"AVG={(atBats == 0 ? 0 : players.Sum(p => (long)p.Batting.Hits) / (double)atBats):F3} " +
                    $"ERA={(outs == 0 ? 0 : 27d * players.Sum(p => (long)p.Pitching.EarnedRuns) / outs):F3} " +
                    $"QualifiedHitters={hitters.Length} AVG400={hitters.Count(p => p.Batting.BattingAverage >= .4)} " +
                    $"QualifiedPitchers={pitchers.Length} ERABelow3={pitchers.Count(p => p.Pitching.EarnedRunAverage < 3)}");
            }
        }
        return 0;
    }

    private sealed class StageSink : IProfilerSectionSink
    {
        private readonly Dictionary<string, (long Start, long Ticks, long BytesStart, long Bytes, int Count)> _stages = new();
        public void Begin(string name)
        {
            _stages.TryGetValue(name, out var value);
            _stages[name] = (Stopwatch.GetTimestamp(), value.Ticks, GC.GetAllocatedBytesForCurrentThread(), value.Bytes, value.Count);
        }
        public void End(string name)
        {
            var value = _stages[name];
            _stages[name] = (0, value.Ticks + Stopwatch.GetTimestamp() - value.Start, 0,
                value.Bytes + GC.GetAllocatedBytesForCurrentThread() - value.BytesStart, value.Count + 1);
        }
        public void Print()
        {
            foreach (var entry in _stages)
                Console.WriteLine($"Stage={entry.Key} Seconds={entry.Value.Ticks / (double)Stopwatch.Frequency:F3} " +
                    $"AllocatedMB={entry.Value.Bytes / 1048576d:F1} Calls={entry.Value.Count}");
        }
    }

    private static int CompareState(JsonElement expected, JsonElement actual, string path, int differences)
    {
        if (expected.ValueKind == JsonValueKind.Object && actual.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in expected.EnumerateObject())
                differences = actual.TryGetProperty(property.Name, out var value)
                    ? CompareState(property.Value, value, path + "." + property.Name, differences)
                    : differences + 1;
            return differences;
        }
        if (expected.ValueKind == JsonValueKind.Array && actual.ValueKind == JsonValueKind.Array &&
            expected.GetArrayLength() == actual.GetArrayLength())
        {
            for (int index = 0; index < expected.GetArrayLength(); index++)
                differences = CompareState(expected[index], actual[index], $"{path}[{index}]", differences);
            return differences;
        }
        if (expected.GetRawText() == actual.GetRawText()) return differences;
        if (differences < 20) Console.WriteLine($"Difference={path} Expected={expected.ToString()[..Math.Min(100, expected.ToString().Length)]} Actual={actual.ToString()[..Math.Min(100, actual.ToString().Length)]}");
        return differences + 1;
    }

    private static JsonElement CaptureTeamInput(ManagerModeMatchService service, ManagerHistoricalRuntimeState runtime, string teamKey)
    {
        var type = typeof(ManagerModeMatchService);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        object preparation = type.GetMethod("GetAiRosterPreparation", flags).Invoke(service, new object[] { runtime, teamKey });
        object plan = preparation.GetType().GetProperty("Plan").GetValue(preparation);
        var mapType = type.GetNestedType("PlayerIdMap", System.Reflection.BindingFlags.NonPublic);
        object map = mapType.GetMethod("Create", new[] { typeof(ManagerHistoricalRuntimeState) }).Invoke(null, new object[] { runtime });
        object build = type.GetMethod("BuildTeam", flags).Invoke(service, new[] { runtime, (object)teamKey, plan, 1, map, null });
        return JsonSerializer.SerializeToElement(build, build.GetType(), new JsonSerializerOptions { IncludeFields = true });
    }
}
