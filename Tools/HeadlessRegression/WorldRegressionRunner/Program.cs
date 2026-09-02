using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime;
using System.Runtime.InteropServices;
using Baseball.Game.Diagnostics;

namespace Baseball.Tools.WorldRegression
{
    /// <summary>
    /// Unity 에디터 없이 실제 Career/World 진행을 Release로 실행하는 Headless 회귀 진입점이다.
    /// </summary>
    public static class Program
    {
        private const ulong DefaultWorldSeed = 8261021UL;
        private const int DefaultSeasonCount = 10;

        public static int Main(string[] args)
        {
            ulong worldSeed = DefaultWorldSeed;
            int seasonCount = DefaultSeasonCount;
            int runCount = 2;
            bool showStages = true;

            for (int index = 0; index < args.Length; index++)
            {
                string argument = args[index];
                if (argument == "--seed" && index + 1 < args.Length)
                    worldSeed = ulong.Parse(args[++index], CultureInfo.InvariantCulture);
                else if (argument == "--seasons" && index + 1 < args.Length)
                    seasonCount = int.Parse(args[++index], CultureInfo.InvariantCulture);
                else if (argument == "--runs" && index + 1 < args.Length)
                    runCount = int.Parse(args[++index], CultureInfo.InvariantCulture);
                else if (argument == "--no-stages")
                    showStages = false;
                else if (argument == "--help" || argument == "-h")
                {
                    Console.WriteLine("사용법: WorldRegressionRunner [--seed <ulong>] [--seasons <int>] [--runs <int>] [--no-stages]");
                    return 0;
                }
            }

            var sink = new StageTimingSink();
            ProfilerSectionSink.Current = sink;

            WriteEnvironment(worldSeed, seasonCount, runCount);

            var runs = new List<WorldRegressionRun>(runCount);
            for (int index = 0; index < runCount; index++)
            {
                WorldRegressionRun run = WorldRegressionScenario.Run(worldSeed, seasonCount, sink);
                runs.Add(run);
                Console.WriteLine();
                Console.WriteLine($"### Run {index + 1}");
                WriteRun(run);
                if (showStages)
                    WriteStages(sink);
            }

            Console.WriteLine();
            Console.WriteLine("### Determinism");
            bool deterministic = true;
            for (int index = 1; index < runs.Count; index++)
            {
                if (runs[index].FinalWorldChecksum != runs[0].FinalWorldChecksum)
                    deterministic = false;
                for (int season = 0; season < runs[index].SeasonChecksums.Count; season++)
                {
                    if (runs[index].SeasonChecksums[season] != runs[0].SeasonChecksums[season])
                    {
                        Console.WriteLine($"SeasonChecksumMismatch=Season{season + 1}");
                        deterministic = false;
                    }
                }
            }
            Console.WriteLine($"Runs={runs.Count}");
            Console.WriteLine($"Deterministic={deterministic}");
            Console.WriteLine($"FinalWorldChecksum={runs[0].FinalWorldChecksum}");
            return deterministic ? 0 : 1;
        }

        private static void WriteEnvironment(ulong worldSeed, int seasonCount, int runCount)
        {
            Console.WriteLine("### Configuration");
            Console.WriteLine($"Runtime={RuntimeInformation.FrameworkDescription}");
            Console.WriteLine($"OS={RuntimeInformation.OSDescription}");
            Console.WriteLine($"Architecture={RuntimeInformation.ProcessArchitecture}");
            Console.WriteLine($"ProcessorCount={Environment.ProcessorCount}");
            Console.WriteLine($"CPU={Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "unknown"}");
#if DEBUG
            Console.WriteLine("BuildConfiguration=Debug");
#else
            Console.WriteLine("BuildConfiguration=Release");
#endif
            Console.WriteLine($"ServerGC={GCSettings.IsServerGC}");
            Console.WriteLine($"WorldSeed={worldSeed}");
            Console.WriteLine($"SeasonCount={seasonCount}");
            Console.WriteLine($"RunCount={runCount}");
        }

        private static void WriteRun(WorldRegressionRun run)
        {
            Console.WriteLine($"LeagueCount={run.LeagueCount}");
            Console.WriteLine($"TeamsPerLeague={run.TeamsPerLeague}");
            Console.WriteLine($"WorldCreateMs={run.WorldCreateMs:F1}");
            Console.WriteLine($"RegularSeasonMs={run.RegularSeasonMs:F1}");
            Console.WriteLine($"PostseasonMs={run.PostseasonMs:F1}");
            Console.WriteLine($"AutoCompletionMs={run.AutoCompletionMs:F1}");
            Console.WriteLine($"GrowthMs={run.GrowthMs:F1}");
            Console.WriteLine($"TransitionMs={run.TransitionMs:F1}");
            Console.WriteLine($"TotalSeconds={run.TotalSeconds:F2}");
            Console.WriteLine($"ChecksumMs={run.ChecksumMs:F1} (계측 전용, TotalSeconds에 미포함)");
            Console.WriteLine($"RegularGames={run.RegularGames}");
            Console.WriteLine($"PostseasonGames={run.PostseasonGames}");
            Console.WriteLine($"TotalGames={run.TotalGames}");
            Console.WriteLine($"AllocatedMB={run.AllocatedBytes / (1024d * 1024d):F1}");
            Console.WriteLine($"Gen0={run.Gen0Collections} Gen1={run.Gen1Collections} Gen2={run.Gen2Collections}");
            Console.WriteLine($"FinalWorldChecksum={run.FinalWorldChecksum}");
        }

        /// <summary>구간은 중첩될 수 있으므로 상위 구간 시간에 하위 구간이 포함된다.</summary>
        private static void WriteStages(StageTimingSink sink)
        {
            Console.WriteLine("-- Stage timings (nested totals) --");
            IReadOnlyList<(string Name, double Milliseconds, int Calls)> stages = sink.Snapshot();
            for (int index = 0; index < stages.Count; index++)
            {
                (string name, double milliseconds, int calls) = stages[index];
                Console.WriteLine($"{name}={milliseconds:F1}ms calls={calls}");
            }
        }
    }
}
