using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Baseball.Core.Balance;
using Baseball.Core.Historical;
using Baseball.Game.Historical;
using Baseball.Simulation.Historical;
using UnityEngine;

namespace Baseball.Tools.HistoricalSeasonDiagnostics;

/// <summary>실제 역사 시즌 경로의 반복 통계와 동일 입력 재실행 해시를 기록한다.</summary>
internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new() { IncludeFields = true };

    private static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--bake-performance")
            return BakePerformance.Run(args);
        if ((args.Length != 4 && args.Length != 6) || !int.TryParse(args[2], out int repeats) || repeats < 1)
        {
            Console.Error.WriteLine("사용법: <Runtime 경로> <출력 JSON> <반복 수> <연도,연도> [Rating center slope]");
            return 1;
        }

        string root = Path.GetFullPath(args[0]);
        string output = Path.GetFullPath(args[1]);
        int[] years = args[3].Split(',').Select(int.Parse).Distinct().Order().ToArray();
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "manifest.json")));
        var catalog = new HistoricalRuntimeContentCatalog();
        HistoricalRuntimeContentFile ReadEntry(string path) =>
            new(path, new TextAsset(File.ReadAllText(Path.Combine(root, path))));
        catalog.Configure(new TextAsset(manifest.RootElement.GetRawText()), ReadEntry("player_persons.json"),
            manifest.RootElement.GetProperty("years").EnumerateArray().Select(year =>
                new HistoricalRuntimeYearContentFile(year.GetProperty("year").GetInt32(),
                    ReadEntry(year.GetProperty("path").GetString()))).ToArray());

        var content = new UnityHistoricalContentProvider(catalog, HistoricalContentVerificationMode.Full).Load();
        var balance = BalanceTable.CreateDefault();
        if (args.Length == 6)
        {
            balance = new BalanceTable(balance.Version, balance.PlateDiscipline, balance.BattedBall,
                balance.BaseRunning, balance.ContractOffer, balance.TeamGeneration, balance.PlayerEvaluation,
                balance.CareerSeason, contentHash: "diagnostic-rating-curve",
                matchRatingCurve: new MatchRatingCurveBalance(double.Parse(args[4], CultureInfo.InvariantCulture),
                    double.Parse(args[5], CultureInfo.InvariantCulture)));
        }

        var identities = new WorldIdentityGenerator().Generate(content.PlayerPersons, content.TeamSeasons,
            content.IdentityNameCatalog, 20260905);
        var source = new BakedHistoricalDetailedSeasonSource(content, balance, identities);
        MethodInfo aggregate = typeof(DetailedMatchHistoricalSeasonAdapter).GetMethod("Aggregate",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException("시즌 기록 집계 함수가 변경되어 진단 도구 갱신이 필요합니다.");
        var rows = new List<object>();
        long games = 0;
        foreach (int year in years)
        {
            var teams = content.GetYear(year).TeamSeasons;
            for (int run = 0; run < repeats; run++)
            {
                // 공통 시드를 사용하는 전후 비교다. 실제 경기 Seed는 Production 경로에서 파생한다.
                ulong seed = 20260905UL + (ulong)run * 104729UL;
                var result = source.RunSeason(seed, teams);
                games += source.LastRunMetrics.TotalGameCount;
                var rotations = ValidateRotation(result, teams);
                var statistics = aggregate.Invoke(null, new object[] { result, teams });
                string checksum = HashMatches(result);
                if (run == 0 && HashMatches(source.RunSeason(seed, teams)) != checksum)
                    throw new InvalidOperationException($"시즌 결정론 실패: {year}");
                rows.Add(new { year, seed, checksum, teams = result.TeamStatistics,
                    standings = result.Standings, statistics, rotations });
            }
            Console.WriteLine($"{year}: {repeats}시즌 완료, 누적 {games}경기");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(output));
        File.WriteAllText(output, JsonSerializer.Serialize(new
        {
            contentHash = content.Manifest.ContentHash, balanceHash = balance.ContentHash,
            center = balance.MatchRatingCurve.Center, slope = balance.MatchRatingCurve.Slope,
            regularSeasonGamesPerTeam = balance.CareerSeason.RegularSeasonGamesPerTeam,
            repeatCount = repeats, games, determinismChecks = years.Length, rotationPolicy = "FixedFive", rows
        }, JsonOptions));
        return 0;
    }

    private static string HashMatches(HistoricalDetailedSeasonOutput result)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var match in result.Matches)
            hash.AppendData(JsonSerializer.SerializeToUtf8Bytes(match, JsonOptions));
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static object[] ValidateRotation(HistoricalDetailedSeasonOutput result, IReadOnlyList<TeamSeasonDefinition> teams)
    {
        var players = result.Players.ToDictionary(p => p.PlayerId);
        var byKey = teams.ToDictionary(t => t.TeamSeasonKey);
        var games = teams.ToDictionary(t => t.TeamSeasonKey, _ => 0);
        var starts = teams.ToDictionary(t => t.TeamSeasonKey, _ => new int[5]);
        foreach (var match in result.Matches)
        {
            if (match.Stage == HistoricalMatchStage.AllStarGame) continue;
            foreach (var roster in new[] { match.Result.Input.AwayRoster, match.Result.Input.HomeRoster })
            {
                var starter = players[roster.StartingPitcher.Player.PlayerId];
                int index = games[starter.TeamSeasonKey]++ % 5;
                if (starter.PlayerSeasonId + ":Normal" != byKey[starter.TeamSeasonKey].Core25CardIds[14 + index])
                    throw new InvalidOperationException($"1~5선발 순환 위반: {starter.TeamSeasonKey}");
                if (match.Stage != HistoricalMatchStage.Postseason) starts[starter.TeamSeasonKey][index]++;
            }
        }
        return teams.Select(t => (object)new { teamSeasonKey = t.TeamSeasonKey,
            regularStarts = starts[t.TeamSeasonKey] }).ToArray();
    }
}
