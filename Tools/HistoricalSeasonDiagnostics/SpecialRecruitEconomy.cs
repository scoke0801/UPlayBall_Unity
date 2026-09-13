using System.Text.Json;
using Baseball.Core.Historical;
using Baseball.Game.Historical;
using Baseball.Game.Shop;
using Baseball.Simulation.Historical;
using Baseball.Simulation.Random;

namespace Baseball.Tools.HistoricalSeasonDiagnostics;

/// <summary>실제 정밀 추첨으로 특수 카드의 정상 재료 8장을 모으는 비용을 측정한다. 연습 보상은 없다.</summary>
internal static class SpecialRecruitEconomy
{
    public static int Run(HistoricalBakedContent content, WorldCardCatalog catalog, string output, int trials)
    {
        var markets = content.TeamSeasons.ToDictionary(t => (t.FranchiseId, t.OriginYear), t => (
            Catalog: ScoutCollection.CreateTeamCatalog(catalog, t),
            Pool: ShopDefaultPools.CreateScoutPools(ScoutFeaturePolicy.FullWorldAwards, t.FranchiseId, t.OriginYear)
                .Single(p => p.ScoutType == ScoutType.YearFranchise)));
        var roller = new ScoutRoller();
        var pity = ScoutPityBalanceTable.CreateInitial();
        var results = new List<object>();
        var costs = new List<int>();
        var unsupportedTargets = new List<string>();
        foreach (var recipe in content.SpecialCards.Recipes.OrderBy(r => r.TargetCardId, StringComparer.Ordinal))
        {
            // 슬롯별로 정밀 풀에 존재하는 최저 Cost 재료를 목표로 한다. 다른 후보를 우연히 얻으면 그것도 쓴다.
            var candidates = recipe.MaterialGroups.Select(g => g.CandidateCardIds
                .Select(catalog.GetRequiredCard)
                .Where(c => catalog.IsActiveRosterSeason(c.PlayerSeasonId))
                .OrderBy(c => catalog.GetPlayerSeason(c).Cost).ThenBy(c => c.CardId, StringComparer.Ordinal)
                .ToArray()).ToArray();
            var targets = new PlayerCardDefinition[SpecialRecruitRecipe.RequiredMaterialCount];
            bool careerHigh = catalog.GetRequiredCard(recipe.TargetCardId).Edition == PlayerCardEdition.CareerHigh;
            if (!Plan(0, new HashSet<int>())) { unsupportedTargets.Add(recipe.TargetCardId); continue; }
            bool Plan(int slot, HashSet<int> years)
            {
                if (slot == targets.Length) return true;
                foreach (var candidate in candidates[slot])
                {
                    int year = catalog.GetPlayerSeason(candidate).OriginYear;
                    if (careerHigh && !years.Add(year)) continue;
                    targets[slot] = candidate;
                    if (Plan(slot + 1, years)) return true;
                    if (careerHigh) years.Remove(year);
                }
                return false;
            }
            var samples = new List<int>();
            for (int trial = 0; trial < trials; trial++)
            {
                var random = new Pcg32Random((ulong)(costs.Count + trial + 1));
                var inventory = new Dictionary<string, int>(StringComparer.Ordinal);
                var ownedSeasons = new HashSet<string>(StringComparer.Ordinal);
                var materialYears = new HashSet<int>();
                int spent = 0, gauge = 0;
                foreach (var slot in Enumerable.Range(0, targets.Length))
                {
                    var target = catalog.GetPlayerSeason(targets[slot]);
                    var market = markets[(target.OriginFranchiseId, target.OriginYear)];
                    string available;
                    while ((available = recipe.MaterialGroups[slot].CandidateCardIds.FirstOrDefault(id => inventory.GetValueOrDefault(id) > 0 &&
                        (!careerHigh || catalog.GetPlayerSeason(catalog.GetRequiredCard(id)).OriginYear == target.OriginYear))) == null)
                    {
                        if (spent >= 2_400_000) throw new InvalidOperationException("재료 수집이 10,000회 추첨 한도를 넘었습니다.");
                        Acquire(roller.Roll(market.Pool, market.Catalog, ScoutFeaturePolicy.FullWorldAwards, random));
                        spent += market.Pool.PriceSp;
                        gauge += pity.GetGaugeGain(market.Pool);
                        if (gauge >= pity.Threshold)
                        {
                            gauge -= pity.Threshold;
                            Acquire(roller.RollGuaranteed(market.Pool, market.Catalog, ScoutFeaturePolicy.FullWorldAwards,
                                pity, ownedSeasons.Contains, random));
                        }
                    }
                    inventory[available]--;
                    if (catalog.GetRequiredCard(recipe.TargetCardId).Edition == PlayerCardEdition.CareerHigh &&
                        !materialYears.Add(catalog.GetPlayerSeason(catalog.GetRequiredCard(available)).OriginYear))
                        throw new InvalidOperationException("커리어하이 재료의 연도가 중복되었습니다.");
                    if (inventory.Where(p => p.Value > 0).All(p => catalog.GetRequiredCard(p.Key).PlayerSeasonId != catalog.GetRequiredCard(available).PlayerSeasonId))
                        ownedSeasons.Remove(catalog.GetRequiredCard(available).PlayerSeasonId);
                }
                samples.Add(spent);
                void Acquire(PlayerCardDefinition card)
                {
                    inventory[card.CardId] = inventory.GetValueOrDefault(card.CardId) + 1;
                    ownedSeasons.Add(card.PlayerSeasonId);
                }
            }
            samples.Sort(); costs.AddRange(samples);
            results.Add(new { recipe.TargetCardId, median = samples[samples.Count / 2], p90 = samples[(int)(samples.Count * .9)] });
        }
        costs.Sort();
        var result = new { strategy = "재료 슬롯별 최저 Cost 정밀 Scout, 보장 즉시 사용, 빈 보관함 시작; 최적 정책의 하한이 아님",
            trials, recipes = results.Count, unsupported = unsupportedTargets.Count, unsupportedTargets,
            median = costs[costs.Count / 2], p90 = costs[(int)(costs.Count * .9)],
            results };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllText(output, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"특수 재료 1장분: {result.recipes}레시피 × {trials}회, 정밀 지원 불가 {result.unsupported}, SP 중앙 {result.median} p90 {result.p90}");
        return 0;
    }
}
