using System.Security.Cryptography;
using System.Text.Json;
using Baseball.Core.Balance;
using Baseball.Core.Historical;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Core.Shop;
using Baseball.Game.Historical;
using Baseball.Game.Shop;
using Baseball.Simulation.Historical;
using Baseball.Simulation.Random;
using UnityEngine;

// 에디터·NUnit 없이 실제 풀 구성, 확률 공개, 추첨, SP 차감·게이지 규칙을 검증한다.
using var config = JsonDocument.Parse(File.ReadAllText("Assets/10.Datas/Resources/NewGame/ScoutStaff.json"));
var staff = config.RootElement.GetProperty("staff").EnumerateArray().Select(entry => new ScoutStaffDefinition(
    entry.GetProperty("id").GetString(), entry.GetProperty("displayName").GetString(),
    entry.GetProperty("description").GetString(), entry.GetProperty("priceMultiplier").GetDouble(),
    entry.GetProperty("costWeightStep").GetDouble())).ToArray();
var roller = new ScoutRoller();
var policy = ScoutFeaturePolicy.Phase4NormalOnly;
var pity = ScoutPityBalanceTable.CreateInitial();
var rows = new List<object>();
int checks = 0;
void Check(bool valid, string message)
{
    if (!valid) throw new InvalidOperationException(message);
    checks++;
}

var seasons = Enumerable.Range(1, 10).Select(cost => new PlayerSeasonDefinition("season" + cost,
    "person" + cost, 1994, "original", "original_1994", PlayerPosition.Catcher,
    PitcherRole.MiddleRelief, PlayerType.Batter, RegistrationType.Domestic,
    new AbilityRatings(50), cost, new AbilityRatings(60))).ToArray();
var synthetic = WorldCardCatalogBuilder.Build(seasons, null, CardEditionBalanceTable.CreateInitial());
var source = new ScoutPoolDefinition("target", ScoutType.YearFranchise,
    ScoutPoolDefinition.CreateInitialCostWeights(), ScoutPoolDefinition.CreateNormalOnlyEditionWeights(),
    240, "original", 1994, rosterScope: ScoutRosterScope.ActiveRoster);
var variants = ScoutStaffPoolFactory.Create(new[] { source }, staff);
var pools = new[] { source }.Concat(variants).ToArray();
double[] means = new double[3];
double[] highRates = new double[3];
for (int index = 0; index < pools.Length; index++)
{
    ScoutPoolDefinition pool = pools[index];
    var probabilities = roller.GetProbabilities(pool, synthetic, policy);
    Check(Math.Abs(probabilities.Sum(bucket => bucket.Probability) - 1) < 1e-12, "확률 합계");
    double expectedHigh = probabilities.Where(bucket => bucket.Cost >= 7).Sum(bucket => bucket.Probability);
    double expectedMean = probabilities.Sum(bucket => bucket.Cost * bucket.Probability);
    var random = new Pcg32Random(20260913);
    var replay = new Pcg32Random(20260913);
    int high = 0, sum = 0;
    const int draws = 100_000;
    for (int draw = 0; draw < draws; draw++)
    {
        var card = roller.Roll(pool, synthetic, policy, random);
        if (draw < 1000) Check(card.CardId == roller.Roll(pool, synthetic, policy, replay).CardId, "동일 입력 재현");
        int cost = synthetic.GetPlayerSeason(card).Cost;
        sum += cost;
        if (cost >= 7) high++;
    }
    means[index] = (double)sum / draws;
    highRates[index] = (double)high / draws;
    Check(Math.Abs(highRates[index] - expectedHigh) < .006, "공개 확률·실제 추첨 일치");
    var economy = new ManagerEconomyState(scoutingPoints: pool.PriceSp * 10);
    for (int draw = 0; draw < 10; draw++)
        roller.RollAndSpend(pool, synthetic, policy, pity, economy, random);
    Check(economy.ScoutingPoints == 0 && economy.PityGauge == Math.Min(pity.Threshold, pool.PriceSp * 10), "SP·게이지 일치");
    rows.Add(new { scope = "모든 코스트가 존재하는 통제 풀", staff = pool.Staff?.DisplayName ?? "기존 기준",
        draws, price = pool.PriceSp, expectedMean, mean = means[index], expectedHigh, highRate = highRates[index] });
}
Check(means[1] < means[0] && means[0] < means[2], "정밀도별 평균 코스트 순서");
Check(highRates[1] < highRates[0] && highRates[0] < highRates[2], "정밀도별 고코스트 확률 순서");
for (ulong seed = 0; seed < 1000; seed++)
{
    string first = roller.RollGuaranteed(variants[0], synthetic, policy, pity, _ => false, new Pcg32Random(seed)).CardId;
    string second = roller.RollGuaranteed(variants[1], synthetic, policy, pity, _ => false, new Pcg32Random(seed)).CardId;
    Check(first == second, "보장 영입은 타입과 무관");
}
var balance = BalanceTable.CreateDefault();
var products = ShopCatalogBuilder.Build(balance.Growth.SkillGacha, variants, Array.Empty<TacticResearchPoolDefinition>(),
    _ => "원본 구단", scoutPity: pity, franchiseYearDisplayNameResolver: (_, year) => "원본 구단 · " + year + "년");
foreach (var pool in variants)
{
    Check(products.TryGetProduct("shop.player." + pool.ScoutPoolId, out var single) && single.Price == pool.PriceSp, "단품 가격");
    Check(products.TryGetProduct("shop.player." + pool.ScoutPoolId + ".x10", out var bundle) && bundle.Price == pool.PriceSp * 10, "묶음 가격");
    Check(products.TryGetProduct(ShopCatalogBuilder.GetGuaranteedProductId(pool.ScoutPoolId), out var guarantee)
        && guarantee.Price == pity.Threshold && guarantee.Currency == ShopCurrency.ScoutPity, "보장 상품 비용");
    Check(single.ScopeLabel == "원본 구단 · 1994년", "원본 구단·연도 표시");
    var resolver = OwnerShopDetailsBuilder.CreateResolver(balance.Growth.SkillGacha, variants, policy, synthetic,
        Array.Empty<TacticResearchPoolDefinition>(), Array.Empty<TacticCardDefinition>(), pity);
    var published = resolver(single).Probabilities;
    var actual = roller.GetProbabilities(pool, synthetic, policy);
    Check(published.Count == actual.Count && published.Select(p => p.Probability).SequenceEqual(actual.Select(p => p.Probability)), "상점 공개 확률 일치");
}

// 실제 역사 전체의 1군 후보 분포에서도 정밀도 역전과 구단·연도 범위 누출을 확인한다.
string root = "Assets/10.Datas/HistoricalSimulation/1982-2025";
using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "manifest.json")));
HistoricalRuntimeContentFile ReadEntry(string path) => new(path, new TextAsset(File.ReadAllText(Path.Combine(root, path))));
var input = new HistoricalRuntimeContentCatalog();
input.Configure(new TextAsset(manifest.RootElement.GetRawText()), ReadEntry("player_persons.json"),
    manifest.RootElement.GetProperty("years").EnumerateArray().Select(year => new HistoricalRuntimeYearContentFile(
        year.GetProperty("year").GetInt32(), ReadEntry(year.GetProperty("path").GetString()))).ToArray());
var special = new TextAsset(File.ReadAllText(Path.Combine(root, "BakedSpecialCards.json")));
input.ConfigureSpecialCards(special, Convert.ToHexString(SHA256.HashData(special.bytes)));
var content = new UnityHistoricalContentProvider(input, HistoricalContentVerificationMode.Fast).Load();
var world = WorldCardCatalogBuilder.Build(content.PlayerSeasons, null, CardEditionBalanceTable.CreateInitial(),
    content.PlayerPersons, content.TeamSeasons, content.SpecialCards);
var fullPolicy = ScoutFeaturePolicy.FullWorldAwards;
var totals = new long[3];
var highs = new long[3];
int teamCount = 0;
foreach (var team in content.TeamSeasons.OrderBy(team => team.TeamSeasonKey, StringComparer.Ordinal))
{
    var members = content.PlayerSeasons.Where(season => season.OriginTeamSeasonKey == team.TeamSeasonKey).ToArray();
    var memberIds = members.Select(season => season.PlayerSeasonId).ToHashSet(StringComparer.Ordinal);
    var cards = world.Cards.Where(card => memberIds.Contains(card.PlayerSeasonId) && !card.IsFranchiseWildcard).ToArray();
    var local = new WorldCardCatalog(members, cards, activeRosterPlayerSeasonIds:
        members.Where(season => world.IsActiveRosterSeason(season.PlayerSeasonId)).Select(season => season.PlayerSeasonId).ToArray());
    var basis = new ScoutPoolDefinition("real", ScoutType.YearFranchise, ScoutPoolDefinition.CreateInitialCostWeights(),
        ScoutPoolDefinition.CreateStandardEditionWeights(), 240, team.FranchiseId, team.OriginYear,
        rosterScope: ScoutRosterScope.ActiveRoster);
    var choices = new[] { basis }.Concat(ScoutStaffPoolFactory.Create(new[] { basis }, staff)).ToArray();
    var expected = choices.Select(pool => roller.GetProbabilities(pool, local, fullPolicy).Sum(bucket => bucket.Cost * bucket.Probability)).ToArray();
    Check(expected[1] <= expected[0] + 1e-12 && expected[0] <= expected[2] + 1e-12, "실제 구단별 정밀도 순서");
    for (int type = 0; type < choices.Length; type++)
    {
        var random = new Pcg32Random((ulong)(20260913 + teamCount));
        for (int draw = 0; draw < 1000; draw++)
        {
            var card = roller.Roll(choices[type], local, fullPolicy, random);
            var season = local.GetPlayerSeason(card);
            Check(season.OriginTeamSeasonKey == team.TeamSeasonKey && world.IsActiveRosterSeason(season.PlayerSeasonId), "원본 범위와 1군 유지");
            totals[type] += season.Cost;
            if (season.Cost >= 7) highs[type]++;
        }
    }
    teamCount++;
    if (teamCount % 100 == 0) Console.WriteLine($"실제 구단·연도 {teamCount}개 검증 중");
}
for (int type = 0; type < pools.Length; type++)
    rows.Add(new { scope = "실제 역사 1군", staff = pools[type].Staff?.DisplayName ?? "기존 기준",
        draws = teamCount * 1000, price = pools[type].PriceSp,
        mean = (double)totals[type] / (teamCount * 1000), highRate = (double)highs[type] / (teamCount * 1000) });
var result = new { checks, teamCount, totalDraws = 300000 + teamCount * 3000, rows };
Directory.CreateDirectory("output/scout-staff-validation");
string json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
File.WriteAllText("output/scout-staff-validation/results.json", json);
Console.WriteLine(json);
