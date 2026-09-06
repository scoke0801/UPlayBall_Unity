using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Baseball.Core.Balance;
using Baseball.Game.Historical;
using UnityEngine;

namespace Baseball.Tools.HistoricalSeasonDiagnostics;

/// <summary>동일 입력의 전체 역사 베이크 시간·할당량·산출물 해시를 측정한다.</summary>
internal static class BakePerformance
{
    public static int Run(string[] args)
    {
        if (args.Length != 3 && args.Length != 4)
            throw new ArgumentException("--bake-performance <Runtime 경로> <출력 JSON> [동시 연도 수]");
        int workers = args.Length == 4 ? int.Parse(args[3]) : 1;
        string root = Path.GetFullPath(args[1]);
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "manifest.json")));
        HistoricalRuntimeContentFile ReadEntry(string path) =>
            new(path, new TextAsset(File.ReadAllText(Path.Combine(root, path))));
        var catalog = new HistoricalRuntimeContentCatalog();
        catalog.Configure(new TextAsset(manifest.RootElement.GetRawText()), ReadEntry("player_persons.json"),
            manifest.RootElement.GetProperty("years").EnumerateArray().Select(year =>
                new HistoricalRuntimeYearContentFile(year.GetProperty("year").GetInt32(),
                    ReadEntry(year.GetProperty("path").GetString()))).ToArray());
        var content = new UnityHistoricalContentProvider(catalog, HistoricalContentVerificationMode.Full).Load();
        var balance = BalanceTable.CreateDefault();
        long allocated = GC.GetTotalAllocatedBytes(true);
        var timer = Stopwatch.StartNew();
        int progressCount = 0;
        var result = WorldHistoryBakeService.Create(content, balance, 20260905,
            new HistoricalWorldExecutionOptions(workers, progress =>
            {
                if (progress.CompletedYears != ++progressCount)
                    throw new InvalidOperationException("진행률 완료 순서가 역행했습니다.");
            }));
        double simulateMs = timer.Elapsed.TotalMilliseconds;
        timer.Restart();
        byte[] bytes = WorldHistoryBakeService.Encode(result.Payload);
        double encodeMs = timer.Elapsed.TotalMilliseconds;
        long allocatedBytes = GC.GetTotalAllocatedBytes(true) - allocated;
        long peakWorkingSetBytes = Process.GetCurrentProcess().PeakWorkingSet64;
        string output = Path.GetFullPath(args[2]);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        string payloadPath = Path.ChangeExtension(output, ".bytes");
        WorldHistoryBakeService.WriteAtomically(payloadPath, bytes);
        timer.Restart();
        if (!WorldHistoryBakeService.TryReadCompleted(payloadPath, result.Payload.Key, out var restored) ||
            !restored.AsSpan().SequenceEqual(bytes))
            throw new InvalidOperationException("완료 파일 재사용 검증에 실패했습니다.");
        double reuseMs = timer.Elapsed.TotalMilliseconds;
        var report = new
        {
            contentHash = content.Manifest.ContentHash, balanceHash = balance.ContentHash,
            games = result.TotalGameCount, rows = result.StatisticsRowCount, workers, progressCount,
            simulateMs, encodeMs, reuseMs, allocatedBytes, peakWorkingSetBytes,
            sha256 = Convert.ToHexString(SHA256.HashData(bytes)), bytes = bytes.Length
        };
        File.WriteAllText(output, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(File.ReadAllText(output));
        return 0;
    }
}
