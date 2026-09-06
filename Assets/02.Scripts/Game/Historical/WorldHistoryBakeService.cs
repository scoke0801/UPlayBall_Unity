using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Baseball.Core.Balance;
using Baseball.Core.Historical;

namespace Baseball.Game.Historical
{
    /// <summary>경기 규칙과 무관한 역사 실행의 동시 작업 수와 진행 통지다.</summary>
    public sealed class HistoricalWorldExecutionOptions
    {
        public HistoricalWorldExecutionOptions(int maxDegreeOfParallelism = 1,
            Action<HistoricalWorldBuildProgress> progress = null)
        {
            if (maxDegreeOfParallelism < 1) throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism));
            MaxDegreeOfParallelism = maxDegreeOfParallelism;
            Progress = progress;
        }
        public int MaxDegreeOfParallelism { get; }
        /// <summary>작업 스레드에서 직렬 호출한다. Unity API는 호출자가 메인 스레드로 전달한다.</summary>
        public Action<HistoricalWorldBuildProgress> Progress { get; }
    }

    /// <summary>가장 최근 완료한 연도와 전체 완료량을 전달한다.</summary>
    public sealed class HistoricalWorldBuildProgress
    {
        public HistoricalWorldBuildProgress(int seasonYear, int completedYears, int totalYears, int completedGames)
        {
            SeasonYear = seasonYear;
            CompletedYears = completedYears;
            TotalYears = totalYears;
            CompletedGames = completedGames;
        }
        public int SeasonYear { get; }
        public int CompletedYears { get; }
        public int TotalYears { get; }
        public int CompletedGames { get; }
    }
    /// <summary>한 Seed의 Bake 결과와, 그것을 만드는 데 실제로 돌린 경기 수를 함께 돌려준다.</summary>
    public sealed class WorldHistoryBakeResult
    {
        public WorldHistoryBakeResult(BakedWorldHistoryPayload payload, int totalGameCount, int statisticsRowCount,
            HistoricalWorldBuildMetrics metrics = null, double mappingMilliseconds = 0)
        {
            Payload = payload ?? throw new ArgumentNullException(nameof(payload));
            TotalGameCount = totalGameCount;
            StatisticsRowCount = statisticsRowCount;
            Metrics = metrics;
            MappingMilliseconds = mappingMilliseconds;
        }

        public BakedWorldHistoryPayload Payload { get; }
        public int TotalGameCount { get; }
        public int StatisticsRowCount { get; }
        public HistoricalWorldBuildMetrics Metrics { get; }
        public double MappingMilliseconds { get; }
    }

    /// <summary>
    /// 44시즌을 실제로 시뮬레이션해 Bake 산출물을 만든다.
    /// Editor 저작 도구가 Simulation 어셈블리를 직접 참조하지 않도록 Game 경계에 두는 진입점이다.
    /// </summary>
    public static class WorldHistoryBakeService
    {
        public static WorldHistoryBakeResult Create(
            HistoricalBakedContent content,
            BalanceTable balance,
            ulong worldHistorySeed,
            HistoricalWorldExecutionOptions executionOptions = null,
            CancellationToken cancellationToken = default)
        {
            if (content == null)
                throw new ArgumentNullException(nameof(content));
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));

            // Bake Source를 주지 않으므로 기존 산출물을 재사용하지 않고 반드시 새로 시뮬레이션한다.
            var builder = new HistoricalWorldRuntimeBuilder(balance, executionOptions: executionOptions);
            HistoricalWorldRuntimeContent world = builder.Build(
                content,
                WorldRecordMode.SimulatedHistory,
                worldHistorySeed, cancellationToken);
            WorldHistorySnapshot history = world.WorldHistory;
            cancellationToken.ThrowIfCancellationRequested();
            var timer = Stopwatch.StartNew();
            var payload = new BakedWorldHistoryPayload(
                builder.CreateBakeKey(content, worldHistorySeed),
                new WorldHistorySaveMapper().CreateSaveData(history));
            return new WorldHistoryBakeResult(payload, world.Metrics.TotalGameCount, history.Statistics.Count,
                world.Metrics, timer.Elapsed.TotalMilliseconds);
        }

        public static byte[] Encode(BakedWorldHistoryPayload payload)
        {
            return WorldHistoryBakeCodec.Encode(payload);
        }

        /// <summary>Key 일치와 본문 복원·정규형을 확인한 완성 파일만 재사용한다.</summary>
        public static bool TryReadCompleted(string path, BakedWorldHistoryKey expectedKey, out byte[] bytes)
        {
            bytes = null;
            if (!File.Exists(path)) return false;
            try
            {
                byte[] candidate = File.ReadAllBytes(path);
                if (!WorldHistoryBakeCodec.TryPeekKey(candidate, out BakedWorldHistoryKey key) || !key.Equals(expectedKey))
                    return false;
                BakedWorldHistoryPayload payload = WorldHistoryBakeCodec.Decode(candidate);
                new WorldHistorySaveMapper().Restore(payload.History);
                byte[] canonical = Encode(payload);
                if (canonical.Length != candidate.Length) return false;
                for (int index = 0; index < canonical.Length; index++)
                    if (canonical[index] != candidate[index]) return false;
                bytes = candidate;
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is InvalidOperationException ||
                                               exception is ArgumentException || exception is OverflowException)
            {
                return false;
            }
        }

        /// <summary>완성된 바이트만 같은 볼륨에서 교체해 실패·취소 시 이전 산출물을 보존한다.</summary>
        public static void WriteAtomically(string path, byte[] bytes, CancellationToken cancellationToken = default)
        {
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp~";
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                File.WriteAllBytes(temporary, bytes);
                cancellationToken.ThrowIfCancellationRequested();
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }
}
