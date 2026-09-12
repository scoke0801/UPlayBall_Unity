using System.Text.Json;
using Baseball.Core.Historical;
using Baseball.Core.Rules;
using Baseball.Game.Historical;
using Baseball.Simulation.Historical;
using Baseball.Simulation.Match;
using Baseball.Simulation.Random;
using UnityEngine;

namespace Baseball.Tools.HistoricalSeasonDiagnostics;

/// <summary>모든 역사 후보가 같은 상대·홈원정·선발 주기를 거치는 상세 경기 순위 Bake다.</summary>
internal static class LegendaryPracticeBake
{
    public static int Run(string[] args)
        => RunCore(args[1], args[2], Baseball.Tools.CommonMatchBalanceInput.DefaultPath,
            Baseball.Tools.CommonMatchBalanceInput.RatingCurvePath, false, true, args.Length == 4 ? args[3] : null);

    /// <summary>정규 카드와 보강 편성을 분리 측정하며 게임용 카탈로그는 생성하지 않는다.</summary>
    public static int RunDiagnostic(string[] args)
    {
        if (args.Length != 6 || (args[5] != "normal" && args[5] != "reinforced"))
            throw new ArgumentException("--practice-ranking-diagnostic <Runtime> <출력> <경기 JSON> <곡선 JSON> <normal|reinforced>");
        return RunCore(args[1], args[2], args[3], args[4], true, args[5] == "reinforced");
    }

    /// <summary>정규화 원기록을 안정적인 구단 키로 연결하며 누락을 임의 승률로 대체하지 않는다.</summary>
    private static void ApplyHistoricalRanking(List<LegendaryPracticeTeam> rows, LegendaryPracticeCatalog catalog)
    {
        using var settings = JsonDocument.Parse(File.ReadAllText("Assets/10.Datas/Resources/NewGame/LegendaryPracticeRewards.json"));
        catalog.historicalWinRateWeight = settings.RootElement.GetProperty("historicalWinRateWeight").GetDouble();
        foreach (var group in rows.GroupBy(t => t.year))
        {
            using var source = JsonDocument.Parse(File.ReadAllText($"Tools/KBOImporter/.cache/KBOImport/Normalized/{group.Key}.json"));
            var records = new Dictionary<string, (int wins, int losses)>(StringComparer.Ordinal);
            foreach (var team in source.RootElement.GetProperty("teams").EnumerateArray())
            {
                string franchise = team.TryGetProperty("sourceFranchiseId", out var id) && !string.IsNullOrEmpty(id.GetString())
                    ? id.GetString()! : "source-team-id:" + team.GetProperty("sourceTeamId").GetString();
                string key = "FRANCHISE_" + LegendaryPracticeCatalog.Hash("source-franchise-identity-v1\0" + franchise).Substring(0, 20)
                    + "_" + group.Key;
                var stats = team.GetProperty("rankStats");
                records.Add(key, (stats.GetProperty("wins").GetInt32(), stats.GetProperty("losses").GetInt32()));
            }
            foreach (var row in group)
            {
                var record = records[row.teamSeasonKey];
                if (record.wins < 0 || record.losses < 0 || record.wins + record.losses <= 0)
                    throw new InvalidOperationException("역사 승패 기록이 올바르지 않습니다: " + row.teamSeasonKey);
                row.historicalWins = record.wins; row.historicalLosses = record.losses;
            }
        }
    }
    private static int RunCore(string runtimePath, string outputPath, string balancePath,
        string curvePath, bool diagnostic, bool reinforced, string cachedPath = null)
    {
        string root = Path.GetFullPath(runtimePath);
        string output = Path.GetFullPath(outputPath);
        var json = new JsonSerializerOptions { IncludeFields = true, WriteIndented = true };
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "manifest.json")));
        HistoricalRuntimeContentFile Read(string path) => new(path, new TextAsset(File.ReadAllText(Path.Combine(root, path))));
        var source = new HistoricalRuntimeContentCatalog();
        source.Configure(new TextAsset(manifest.RootElement.GetRawText()), Read("player_persons.json"),
            manifest.RootElement.GetProperty("years").EnumerateArray().Select(y => new HistoricalRuntimeYearContentFile(
                y.GetProperty("year").GetInt32(), Read(y.GetProperty("path").GetString()))).ToArray());
        string specialPath = Path.Combine(root, "BakedSpecialCards.json");
        if (diagnostic && reinforced && !File.Exists(specialPath))
            throw new FileNotFoundException("보강 편성 진단에는 BakedSpecialCards.json이 필요합니다.", specialPath);
        if (reinforced && File.Exists(specialPath)) source.ConfigureSpecialCards(new TextAsset(File.ReadAllText(specialPath)),
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(specialPath))));
        var content = new UnityHistoricalContentProvider(source, HistoricalContentVerificationMode.Full).Load();
        var balance = Baseball.Tools.CommonMatchBalanceInput.Load(balancePath, curvePath);
        var identities = new WorldIdentityGenerator().Generate(content.PlayerPersons, content.TeamSeasons, content.IdentityNameCatalog, 20260912);
        var builder = new LegendaryPracticeRosterBuilder(content, balance);
        var candidates = content.TeamSeasons.OrderBy(t => t.TeamSeasonKey, StringComparer.Ordinal).ToArray();
        var snapshots = new List<MatchRosterSnapshot[]>();
        var rows = new List<LegendaryPracticeTeam>();
        foreach (var team in candidates)
        {
            try
            {
                var roster = builder.Build(team, identities, rows.Count + 1, (rows.Count + 1) * 100, out _);
                rows.Add(new LegendaryPracticeTeam { challengeTeamId = "historic:" + team.TeamSeasonKey,
                    teamSeasonKey = team.TeamSeasonKey, year = team.OriginYear, rosterHash = builder.GetRosterHash(team) });
                snapshots.Add(roster);
            }
            catch (InvalidOperationException error)
            {
                throw new InvalidOperationException("전체 구단 Bake를 중단합니다. 편성 실패: " + team.TeamSeasonKey, error);
            }
        }
        if (rows.Count < 100) throw new InvalidOperationException("유효한 역사 팀이 100개보다 적습니다.");
        int cycles = (int)Math.Ceiling(1000d / (2 * (rows.Count - 1)));
        var catalog = new LegendaryPracticeCatalog { seed = 20260912,
            simulationVersion = LegendaryPracticeCatalog.CreateSimulationVersion(
                File.ReadAllText(balancePath), File.ReadAllText(curvePath)),
            contentHash = content.Manifest.ContentHash, candidateCount = rows.Count,
            candidateTeamSeasonKeys = candidates.Select(t => t.TeamSeasonKey).ToArray(),
            gamesPerCandidate = cycles * 2 * (rows.Count - 1) };
        long games = 0;
        if (cachedPath != null)
        {
            var previous = JsonSerializer.Deserialize<LegendaryPracticeCatalog>(File.ReadAllText(cachedPath), json)!;
            var cached = JsonSerializer.Deserialize<LegendaryPracticeTeam[]>(File.ReadAllText(cachedPath + ".candidates.json"), json)!;
            if (previous.simulationVersion != catalog.simulationVersion || previous.contentHash != catalog.contentHash ||
                previous.seed != catalog.seed || previous.gamesPerCandidate != catalog.gamesPerCandidate ||
                previous.candidateCount != rows.Count || cached.Length != rows.Count ||
                !previous.candidateTeamSeasonKeys.SequenceEqual(catalog.candidateTeamSeasonKeys))
                throw new InvalidOperationException("재사용할 전체 경기 결과의 입력이 현재 Bake와 다릅니다.");
            var byKey = cached.ToDictionary(t => t.teamSeasonKey, StringComparer.Ordinal);
            foreach (var row in rows)
            {
                var old = byKey[row.teamSeasonKey];
                if (old.rosterHash != row.rosterHash || old.challengeTeamId != row.challengeTeamId ||
                    old.Games != catalog.gamesPerCandidate || old.wins < 0 || old.losses < 0 || old.draws < 0 ||
                    old.runsScored < 0 || old.runsAllowed < 0)
                    throw new InvalidOperationException("재사용할 팀의 로스터 또는 경기 표본이 다릅니다.");
                row.wins = old.wins; row.losses = old.losses; row.draws = old.draws;
                row.runsScored = old.runsScored; row.runsAllowed = old.runsAllowed;
            }
            games = (long)rows.Count * catalog.gamesPerCandidate / 2;
        }
        for (int first = 0; cachedPath == null && first < rows.Count; first++)
        {
            for (int second = first + 1; second < rows.Count; second++)
                for (int cycle = 0; cycle < cycles; cycle++)
                    for (int home = 0; home < 2; home++)
                    {
                        int a = home == 0 ? first : second, h = home == 0 ? second : first;
                        ulong seed = DeterministicSeed.Derive(catalog.seed, (ulong)++games);
                        var input = new MatchInput(1, checked((int)games), seed,
                            snapshots[a][(first + second + cycle) % 5], snapshots[h][(first + second + cycle) % 5],
                            new MatchRules(9, 0, ExtraInningPolicy.DrawAtLimit, 10, true, 0),
                            historicalConfiguration: new HistoricalMatchConfiguration(balance.HistoricalAssignment.CreateRule()));
                        var result = new MatchSimulator(balance, MatchRandomStreams.Create(seed)).Simulate(input,
                            NullMatchEventSink.Instance, MatchExecutionProfile.DetailedBackground);
                        int ar = result.AwayBoxScore.Runs, hr = result.HomeBoxScore.Runs;
                        rows[a].runsScored += ar; rows[a].runsAllowed += hr;
                        rows[h].runsScored += hr; rows[h].runsAllowed += ar;
                        if (ar > hr) { rows[a].wins++; rows[h].losses++; }
                        else if (hr > ar) { rows[h].wins++; rows[a].losses++; }
                        else { rows[a].draws++; rows[h].draws++; }
                        if (games <= 10)
                        {
                            var repeat = new MatchSimulator(balance, MatchRandomStreams.Create(seed)).Simulate(input,
                                NullMatchEventSink.Instance, MatchExecutionProfile.DetailedBackground);
                            if (repeat.AwayBoxScore.Runs != ar || repeat.HomeBoxScore.Runs != hr)
                                throw new InvalidOperationException("동일 Seed 경기 재현 실패");
                        }
                    }
            Console.WriteLine($"후보 {first + 1}/{rows.Count}, 누적 {games:N0}경기");
        }
        if (!diagnostic) ApplyHistoricalRanking(rows, catalog);
        rows.Sort(catalog.Compare);
        for (int i = 0; i < rows.Count; i++)
        {
            rows[i].rank = i + 1;
            if (rows[i].Games != catalog.gamesPerCandidate)
                throw new InvalidOperationException("전체 후보의 경기 표본이 일치하지 않습니다.");
        }
        if (rows.Sum(t => t.wins) != rows.Sum(t => t.losses) || rows.Sum(t => t.runsScored) != rows.Sum(t => t.runsAllowed))
            throw new InvalidOperationException("전체 후보의 승패 또는 득실점 집계가 일치하지 않습니다.");
        if (diagnostic)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            File.WriteAllText(output, JsonSerializer.Serialize(new {
                diagnosticOnly = true, reinforced, games, balanceHash = balance.ContentHash,
                engineVersion = SimulationVersionStamp.CurrentEngineVersion,
                contentHash = content.Manifest.ContentHash, rows }, json));
            return 0;
        }
        catalog.teams = rows.Take(100).ToArray();
        using var rewards = JsonDocument.Parse(File.ReadAllText("Assets/10.Datas/Resources/NewGame/LegendaryPracticeRewards.json"));
        for (int i = 0; i < 100; i++)
        {
            var row = catalog.teams[i]; row.rank = i + 1;
            var band = rewards.RootElement.GetProperty("bands").EnumerateArray().Single(b =>
                b.GetProperty("minRank").GetInt32() <= row.rank && b.GetProperty("maxRank").GetInt32() >= row.rank);
            row.rewardMoney = band.GetProperty("money").GetInt64();
            row.rewardDevelopment = band.GetProperty("development").GetInt32(); row.rewardScouting = band.GetProperty("scouting").GetInt32();
        }
        catalog.dataHash = catalog.CalculateHash(); catalog.ValidateCoverage(content.TeamSeasons);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        Console.WriteLine($"완료: {games:N0}경기, 팀당 {catalog.gamesPerCandidate}, SHA256 {catalog.dataHash}");
        var state = new LegendaryPracticeState(); var wallet = new ManagerEconomyState(); int campaignGames = 0;
        content.TryGetTeamSeason(catalog.teams[0].teamSeasonKey, out var strongest);
        var player = builder.Build(strongest, identities, 900000, 9000000, out _);
        for (int index = 99; index >= 0; index--)
        {
            var challenge = catalog.teams[index]; content.TryGetTeamSeason(challenge.teamSeasonKey, out var team);
            var opponent = builder.Build(team, identities, 800000, 8000000, out _);
            while (state.Get(challenge.challengeTeamId).wins < 3)
            {
                if (++campaignGames > 10000) throw new InvalidOperationException("도전 완주 검증 한도 초과");
                var progress = state.Get(challenge.challengeTeamId);
                ulong seed = DeterministicSeed.Derive(20260913, (ulong)campaignGames);
                var input = new MatchInput(1, campaignGames, seed, opponent[progress.NextStarterIndex], player[campaignGames % 5],
                    new MatchRules(9, 0, ExtraInningPolicy.DrawAtLimit, 10, true, 0),
                    historicalConfiguration: new HistoricalMatchConfiguration(balance.HistoricalAssignment.CreateRule()));
                var match = new MatchSimulator(balance, MatchRandomStreams.Create(seed)).Simulate(input,
                    NullMatchEventSink.Instance, MatchExecutionProfile.DetailedBackground);
                state.Commit(catalog, challenge.challengeTeamId, progress.attempts + 1, match.HomeBoxScore.Runs, match.AwayBoxScore.Runs, 1);
                state = LegendaryPracticeState.Restore(JsonSerializer.Deserialize<LegendaryPracticeProgress[]>(JsonSerializer.Serialize(state.Capture(), json), json));
            }
            if (!state.Claim(catalog, challenge.challengeTeamId, wallet) || state.Claim(catalog, challenge.challengeTeamId, wallet))
                throw new InvalidOperationException("최초 보상 중복 지급 검증 실패");
        }
        Directory.CreateDirectory("output");
        File.WriteAllText(Path.Combine("output", Path.GetFileName(output) + ".validation.json"), JsonSerializer.Serialize(new { games, catalog.candidateCount,
            catalog.gamesPerCandidate, catalog.dataHash, campaignGames, wins = state.Capture().Sum(p => p.wins),
            wallet.Money, wallet.DevelopmentPoints, wallet.ScoutingPoints }, json));
        Console.WriteLine($"실제 경기 완주: {campaignGames}경기 / 300승, 보상 {wallet.Money} / {wallet.DevelopmentPoints} / {wallet.ScoutingPoints}");
        // 전체 경기·완주 검증이 끝난 산출물만 게시 후보로 내보낸다.
        File.WriteAllText(output, JsonSerializer.Serialize(catalog, json));
        File.WriteAllText(output + ".candidates.json", JsonSerializer.Serialize(rows, json));
        return 0;
    }
}
