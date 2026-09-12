using System.Text.Json;
using Baseball.Core.Historical;
using Baseball.Game.Historical;
using Baseball.Game.Shop;
using Baseball.Simulation.Historical;
using Baseball.Simulation.Random;
using UnityEngine;

namespace Baseball.Tools.HistoricalSeasonDiagnostics;

/// <summary>
/// 실제 Scout 추첨 코드와 전체 역사 카드로, 한 연도 구단의 1군 25인을 정밀 Scout로 모으는 데
/// 걸리는 시즌 수를 모든 구단 연도에 대해 반복 측정한다. 플레이어는 SP를 모두 목표 구단 정밀 Scout에 쓰고,
/// 보장 영입 게이지가 차면 즉시 쓴다고 가정한다.
/// </summary>
internal static class ScoutCollection
{
    private const int RegularSeasonGames = 144;
    private const int LegacyInitialScoutingPoints = 10_000;
    private const int CurrentInitialScoutingPoints = 3_000;
    private const int MaximumSeasons = 200;

    private enum EconomyRule
    {
        Current,
        Legacy
    }

    public static int Run(string[] args)
    {
        if (args.Length < 3)
            throw new ArgumentException(
                "--scout-collection <Runtime 경로> <출력 JSON> [시행 수=30] [승률=0.5] [--legacy] [--facility]");
        int trials = args.Length > 3 && !args[3].StartsWith("--", StringComparison.Ordinal) ? int.Parse(args[3]) : 30;
        double winRate = args.Length > 4 && !args[4].StartsWith("--", StringComparison.Ordinal)
            ? double.Parse(args[4], System.Globalization.CultureInfo.InvariantCulture)
            : 0.5d;
        EconomyRule rule = args.Contains("--legacy") ? EconomyRule.Legacy : EconomyRule.Current;
        bool hasFacility = args.Contains("--facility") || rule == EconomyRule.Legacy;

        HistoricalBakedContent content = LoadContent(Path.GetFullPath(args[1]));
        WorldCardCatalog catalog = WorldCardCatalogBuilder.Build(content.PlayerSeasons, null,
            CardEditionBalanceTable.CreateInitial(), content.PlayerPersons, content.TeamSeasons, content.SpecialCards);
        ScoutFeaturePolicy policy = ScoutFeaturePolicy.FullWorldAwards;
        ScoutEconomyBalance economy = ScoutEconomyBalance.CreateDefault();
        var targets = new List<ScoutMarketTarget>();
        foreach (TeamSeasonDefinition team in content.TeamSeasons)
            targets.Add(new ScoutMarketTarget(team.FranchiseId, team.OriginYear));
        IReadOnlyList<ScoutPoolDefinition> pools = ShopDefaultPools.CreateScoutPools(policy, targets);

        var roller = new ScoutRoller();
        var allSeasons = new List<double>();
        var allDraws = new List<int>();
        var teams = new List<object>();
        foreach (TeamSeasonDefinition team in content.TeamSeasons)
        {
            ScoutPoolDefinition pool = FindPreciseScoutPool(pools, team, rule);
            HashSet<string> core = ResolveCoreSeasonIds(catalog, team);
            WorldCardCatalog teamCatalog = CreateTeamCatalog(catalog, team);
            var seasons = new List<double>(trials);
            var draws = new List<int>(trials);
            for (int trial = 0; trial < trials; trial++)
            {
                ulong seed = DeterministicSeed.Derive(StableHash(team.TeamSeasonKey), (ulong)trial);
                (double seasonCount, int drawCount) = SimulateCollection(
                    roller, pool, teamCatalog, policy, economy, core, rule, hasFacility, winRate, new Pcg32Random(seed));
                seasons.Add(seasonCount);
                draws.Add(drawCount);
            }
            allSeasons.AddRange(seasons);
            allDraws.AddRange(draws);
            seasons.Sort();
            teams.Add(new
            {
                team.TeamSeasonKey,
                team.OriginYear,
                meanSeasons = seasons.Average(),
                medianSeasons = Percentile(seasons, 0.5),
                p90Seasons = Percentile(seasons, 0.9),
                meanDraws = draws.Average()
            });
        }

        allSeasons.Sort();
        allDraws.Sort();
        var teamMeans = teams.Select(team => (double)team.GetType().GetProperty("meanSeasons")!.GetValue(team)!).ToList();
        var summary = new
        {
            rule = rule.ToString(),
            hasFacility,
            winRate,
            trials,
            teamCount = content.TeamSeasons.Count,
            seasons = new
            {
                p10 = Percentile(allSeasons, 0.1),
                median = Percentile(allSeasons, 0.5),
                p90 = Percentile(allSeasons, 0.9),
                max = allSeasons[^1],
                slowestTeamMean = teamMeans.Max(),
                fastestTeamMean = teamMeans.Min()
            },
            draws = new
            {
                p10 = Percentile(allDraws.Select(value => (double)value).ToList(), 0.1),
                median = Percentile(allDraws.Select(value => (double)value).ToList(), 0.5),
                p90 = Percentile(allDraws.Select(value => (double)value).ToList(), 0.9)
            },
            teams
        };
        string output = Path.GetFullPath(args[2]);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.WriteAllText(output, JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(
            $"{rule} facility={hasFacility} winRate={winRate:0.00} teams={content.TeamSeasons.Count} trials={trials} | " +
            $"시즌 p10 {summary.seasons.p10:0.00} 중앙 {summary.seasons.median:0.00} p90 {summary.seasons.p90:0.00} " +
            $"최대 {summary.seasons.max:0.00} 팀평균 {summary.seasons.fastestTeamMean:0.00}~{summary.seasons.slowestTeamMean:0.00} | " +
            $"정밀 Scout 횟수 중앙 {summary.draws.median:0} p90 {summary.draws.p90:0}");
        return 0;
    }

    /// <summary>SP가 경기마다 들어오고, 모일 때마다 정밀 Scout와 보장 영입을 쓴다.</summary>
    private static (double Seasons, int Draws) SimulateCollection(
        ScoutRoller roller,
        ScoutPoolDefinition pool,
        WorldCardCatalog catalog,
        ScoutFeaturePolicy policy,
        ScoutEconomyBalance economy,
        HashSet<string> core,
        EconomyRule rule,
        bool hasFacility,
        double winRate,
        IRandomSource random)
    {
        var owned = new HashSet<string>(StringComparer.Ordinal);
        int missing = core.Count;
        double scoutingPoints = rule == EconomyRule.Legacy ? LegacyInitialScoutingPoints : CurrentInitialScoutingPoints;
        int gauge = 0;
        int draws = 0;
        ScoutPityBalanceTable pity = economy.Pity;
        for (int game = 0; game <= MaximumSeasons * RegularSeasonGames; game++)
        {
            while (missing > 0 && scoutingPoints >= pool.PriceSp)
            {
                scoutingPoints -= pool.PriceSp;
                draws++;
                Acquire(catalog, roller.Roll(pool, catalog, policy, random), core, owned, ref missing);
                if (rule == EconomyRule.Legacy)
                    continue;
                gauge = Math.Min(pity.Threshold, gauge + pity.GetGaugeGain(pool));
                if (gauge < pity.Threshold || missing == 0)
                    continue;
                gauge -= pity.Threshold;
                Acquire(catalog, roller.RollGuaranteed(pool, catalog, policy, pity, owned.Contains, random),
                    core, owned, ref missing);
            }
            if (missing == 0)
                return (game / (double)RegularSeasonGames, draws);

            int season = game / RegularSeasonGames + 1;
            if (hasFacility)
                scoutingPoints += ResolveFacilityScoutingPointsPerSeason(season) / (double)RegularSeasonGames;
            if (rule == EconomyRule.Current)
                scoutingPoints += economy.GetMatchReward(random.NextDouble() < winRate);
        }
        return (MaximumSeasons, draws);
    }

    /// <summary>
    /// 스카우트 시설을 가장 빠르게 올리는 경우의 시즌당 SP다. 주 6경기 기준 24주로 환산했다.
    /// L1은 첫 시즌, L2는 리그 등급 2 도달 후(3시즌), L3는 등급 5 도달 후(6시즌)로 본다.
    /// </summary>
    private static int ResolveFacilityScoutingPointsPerSeason(int season)
    {
        if (season <= 2) return 25 * 24;
        if (season <= 5) return 40 * 24;
        return 60 * 24;
    }

    private static void Acquire(
        WorldCardCatalog catalog,
        PlayerCardDefinition card,
        HashSet<string> core,
        HashSet<string> owned,
        ref int missing)
    {
        string seasonId = catalog.GetPlayerSeason(card).PlayerSeasonId;
        if (owned.Add(seasonId) && core.Contains(seasonId))
            missing--;
    }

    private static ScoutPoolDefinition FindPreciseScoutPool(
        IReadOnlyList<ScoutPoolDefinition> pools,
        TeamSeasonDefinition team,
        EconomyRule rule)
    {
        foreach (ScoutPoolDefinition pool in pools)
        {
            if (pool.ScoutType != ScoutType.YearFranchise || pool.FranchiseFilter != team.FranchiseId ||
                pool.YearFilter != team.OriginYear)
                continue;
            if (rule == EconomyRule.Current)
                return pool;
            return new ScoutPoolDefinition(pool.ScoutPoolId, pool.ScoutType,
                ScoutPoolDefinition.CreateInitialCostWeights(), ScoutPoolDefinition.CreateStandardEditionWeights(),
                pool.PriceSp, pool.FranchiseFilter, pool.YearFilter);
        }
        throw new InvalidOperationException($"{team.TeamSeasonKey}의 정밀 Scout 풀이 없습니다.");
    }

    /// <summary>
    /// 정밀 Scout 후보는 원 구단·연도가 같은 카드뿐이므로, 그 카드만 담은 카탈로그로 추첨해도 버킷과 결과가 같다.
    /// 전체 카탈로그를 뽑기마다 순회하는 비용만 줄인다. 특수 영입 와일드카드는 Scout 대상이 아니라서 뺀다.
    /// </summary>
    private static WorldCardCatalog CreateTeamCatalog(WorldCardCatalog catalog, TeamSeasonDefinition team)
    {
        var seasons = new Dictionary<string, PlayerSeasonDefinition>(StringComparer.Ordinal);
        var cards = new List<PlayerCardDefinition>();
        foreach (PlayerCardDefinition card in catalog.Cards)
        {
            PlayerSeasonDefinition season = catalog.GetPlayerSeason(card);
            if (season.OriginFranchiseId != team.FranchiseId || season.OriginYear != team.OriginYear ||
                card.IsFranchiseWildcard)
                continue;
            seasons[season.PlayerSeasonId] = season;
            cards.Add(card);
        }
        var activeRoster = seasons.Keys.Where(catalog.IsActiveRosterSeason).ToList();
        return new WorldCardCatalog(seasons.Values.ToList(), cards, activeRosterPlayerSeasonIds: activeRoster);
    }

    private static HashSet<string> ResolveCoreSeasonIds(WorldCardCatalog catalog, TeamSeasonDefinition team)
    {
        var core = new HashSet<string>(StringComparer.Ordinal);
        foreach (string cardId in team.Core25CardIds)
            core.Add(catalog.GetRequiredCard(cardId).PlayerSeasonId);
        return core;
    }

    private static HistoricalBakedContent LoadContent(string root)
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "manifest.json")));
        HistoricalRuntimeContentFile ReadEntry(string path) =>
            new(path, new TextAsset(File.ReadAllText(Path.Combine(root, path))));
        var catalog = new HistoricalRuntimeContentCatalog();
        catalog.Configure(new TextAsset(manifest.RootElement.GetRawText()), ReadEntry("player_persons.json"),
            manifest.RootElement.GetProperty("years").EnumerateArray().Select(year =>
                new HistoricalRuntimeYearContentFile(year.GetProperty("year").GetInt32(),
                    ReadEntry(year.GetProperty("path").GetString()!))).ToArray());
        // 레어 카드가 정밀 Scout 확률에 들어가므로 특수 카드도 함께 싣는다.
        var specialCards = new TextAsset(File.ReadAllText(Path.Combine(root, "BakedSpecialCards.json")));
        catalog.ConfigureSpecialCards(specialCards,
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(specialCards.bytes)));
        return new UnityHistoricalContentProvider(catalog, HistoricalContentVerificationMode.Fast).Load();
    }

    private static double Percentile(List<double> sorted, double quantile)
    {
        if (sorted.Count == 0) return 0d;
        var copy = sorted.OrderBy(value => value).ToList();
        int index = Math.Min(copy.Count - 1, (int)Math.Floor(quantile * copy.Count));
        return copy[index];
    }

    private static ulong StableHash(string text)
    {
        ulong hash = 14695981039346656037UL;
        foreach (char character in text)
        {
            hash ^= character;
            hash *= 1099511628211UL;
        }
        return hash;
    }
}
