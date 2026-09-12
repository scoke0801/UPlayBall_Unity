using System.Text.Json;
using Baseball.Game.Historical;

namespace Baseball.Tools.HistoricalSeasonDiagnostics;

/// <summary>Unity 베이크를 순수 런타임 코덱으로 복원해 정규 인코딩과 타율 분모를 검증한다.</summary>
internal static class WorldBakeValidation
{
    public static int Run(string directory)
    {
        string[] files = Directory.GetFiles(directory, "world_history_*.bytes");
        if (files.Length == 0) throw new InvalidDataException("역사 베이크 파일이 없습니다.");
        foreach (string file in files.OrderBy(path => path, StringComparer.Ordinal))
        {
            byte[] bytes = File.ReadAllBytes(file);
            var payload = WorldHistoryBakeCodec.Decode(bytes);
            if (!WorldHistoryBakeService.TryReadCompleted(file, payload.Key, out _))
                throw new InvalidDataException("복원·재인코딩 왕복 실패: " + file);
            int battingRows = 0;
            foreach (var row in payload.History.statistics)
            {
                if (row.hits > row.atBats || row.atBats > row.plateAppearances || row.atBats < 0)
                    throw new InvalidDataException("H/AB/PA 관계가 잘못됐습니다: " + row.playerSeasonId);
                if (row.atBats > 0) battingRows++;
            }
            if (battingRows == 0) throw new InvalidDataException("타수 기록이 없습니다: " + file);
            Console.WriteLine(JsonSerializer.Serialize(new { file = Path.GetFileName(file),
                payload.Key.WorldHistorySeed, payload.Key.ContentHash,
                rows = payload.History.statistics.Length, battingRows, roundTrip = true }));
        }
        return 0;
    }
}
