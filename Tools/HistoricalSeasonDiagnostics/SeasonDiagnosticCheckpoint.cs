using System.Text.Json;

namespace Baseball.Tools.HistoricalSeasonDiagnostics;

/// <summary>완료한 연도를 원자적으로 저장하고 큰 최종 보고서는 연도별로 읽어 스트리밍한다.</summary>
internal sealed class SeasonDiagnosticCheckpoint
{
    private readonly string _directory;
    private readonly string _contentHash;
    private readonly string _balanceHash;
    private readonly int _engineVersion;
    private readonly int _repeats;
    private readonly int _gamesPerTeam;
    private readonly JsonSerializerOptions _options;

    public SeasonDiagnosticCheckpoint(string output, string contentHash, string balanceHash,
        int engineVersion, int repeats, int gamesPerTeam, JsonSerializerOptions options)
    {
        _directory = output + ".parts";
        _contentHash = contentHash;
        _balanceHash = balanceHash;
        _engineVersion = engineVersion;
        _repeats = repeats;
        _gamesPerTeam = gamesPerTeam;
        _options = options;
        Directory.CreateDirectory(_directory);
    }

    /// <summary>입력이 같은 완료 연도만 재사용하며 첫 시드의 새 실행과 비교할 체크섬을 반환한다.</summary>
    public bool TryRead(int year, out long games, out string firstChecksum)
    {
        games = 0;
        firstChecksum = null;
        if (!File.Exists(GetPath(year))) return false;
        using JsonDocument document = OpenValidated(year);
        games = document.RootElement.GetProperty("games").GetInt64();
        firstChecksum = document.RootElement.GetProperty("rows")[0].GetProperty("checksum").GetString();
        return true;
    }

    /// <summary>경기·결정론 검사가 끝난 연도만 완료 파일로 전환한다.</summary>
    public void Write(int year, long games, List<object> rows)
    {
        WriteAtomically(GetPath(year), new { schemaVersion = 1, contentHash = _contentHash,
            balanceHash = _balanceHash, engineVersion = _engineVersion, year, repeats = _repeats,
            gamesPerTeam = _gamesPerTeam, games, rows }, _options);
    }

    /// <summary>전체 보고서를 메모리에 합치지 않고 한 연도의 문서 수명 안에서 행을 순서대로 제공한다.</summary>
    public IEnumerable<object> ReadRows(int year)
    {
        using JsonDocument document = OpenValidated(year);
        foreach (JsonElement row in document.RootElement.GetProperty("rows").EnumerateArray())
            yield return row;
    }

    /// <summary>UTF-16 전체 문자열을 만들지 않아 대규모 보고서의 문자열 크기 한도를 피한다.</summary>
    public static void WriteAtomically<T>(string path, T value, JsonSerializerOptions options)
    {
        string pending = path + ".writing";
        using (FileStream stream = File.Create(pending))
            JsonSerializer.Serialize(stream, value, options);
        File.Move(pending, path, true);
    }

    private string GetPath(int year) => Path.Combine(_directory, year + ".json");

    private JsonDocument OpenValidated(int year)
    {
        using FileStream stream = File.OpenRead(GetPath(year));
        JsonDocument document = JsonDocument.Parse(stream);
        try
        {
            JsonElement root = document.RootElement;
            if (root.GetProperty("schemaVersion").GetInt32() != 1 ||
                root.GetProperty("contentHash").GetString() != _contentHash ||
                root.GetProperty("balanceHash").GetString() != _balanceHash ||
                root.GetProperty("engineVersion").GetInt32() != _engineVersion ||
                root.GetProperty("year").GetInt32() != year ||
                root.GetProperty("repeats").GetInt32() != _repeats ||
                root.GetProperty("gamesPerTeam").GetInt32() != _gamesPerTeam ||
                root.GetProperty("games").GetInt64() <= 0 ||
                root.GetProperty("rows").GetArrayLength() != _repeats)
                throw new InvalidDataException($"중간 저장 입력 불일치: {year}. 다른 출력 경로를 사용하세요.");
            int run = 0;
            foreach (JsonElement row in root.GetProperty("rows").EnumerateArray())
            {
                if (row.GetProperty("year").GetInt32() != year ||
                    row.GetProperty("seed").GetUInt64() != 20260905UL + (ulong)run++ * 104729UL)
                    throw new InvalidDataException($"중간 저장 연도·시드 불일치: {year}");
            }
            return document;
        }
        catch { document.Dispose(); throw; }
    }
}
