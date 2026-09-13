using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Shop;
using Baseball.Game.Historical;
using Baseball.Game.Shop;
using Baseball.Simulation.Growth;
using Baseball.Simulation.Historical;
using Baseball.Simulation.Random;
using Baseball.Tools.ManagerReportValidation;

// 에디터 없이 상품별 대량 지급과 실제 구매·저장 경로를 검증한다.
int checks = 0;
var growth = GrowthBalanceTable.CreateDefault();
var definitions = growth.SkillBlocks.ToDictionary(block => block.BlockId);
var resolver = new OwnerSkillGachaResolver(growth);
var catalog = ShopCatalogBuilder.Build(growth.SkillGacha, null, null);
var random = new Pcg32Random(9132026);
foreach (SkillGachaPurchaseTier tier in Enum.GetValues<SkillGachaPurchaseTier>())
{
    Check(catalog.TryGetProduct("shop.skill." + tier, out var product), tier + " 단품");
    var counts = new int[SkillBlockGradeCatalog.Count];
    var offer = growth.SkillGacha.GetOffer(tier);
    var seen = new HashSet<string>();
    for (int draw = 0; draw < 100000; draw++)
    {
        var result = resolver.Draw(new OwnerSkillBlockInventoryState(), tier, 1, random)[0];
        counts[(int)definitions[result.DefinitionId].Rarity]++;
        seen.Add(result.DefinitionId);
    }
    for (int grade = 0; grade < counts.Length; grade++)
    {
        double probability = offer.GetProbability((SkillBlockRarity)grade);
        Check(probability == 0 ? counts[grade] == 0 : Math.Abs(counts[grade] / 100000d - probability) < .006, tier + " 확률 " + grade);
    }
    if (tier == SkillGachaPurchaseTier.Mythic) Check(seen.Count == 84, "SSS 84종 실제 지급");
    Check(offer.GetProbability(SkillGachaService.SelectRarity(offer, Math.BitDecrement(1d))) > 0, tier + " 경계값");
    Console.WriteLine($"{tier}: {string.Join(", ", counts)}");
}
Check(catalog.TryGetProduct("shop.skill.Mythic", out var sss), "SSS 상품");
Check(sss.Price == 625000000 && sss.MaxPurchasesPerPeriod == 1 && sss.DrawCount == 1, "가격·한도");
Check(!catalog.Products.Any(p => p.SourceId == "Mythic" && p.DrawCount != 1), "한도 초과 묶음 없음");
var fixture = RuntimeFixture.Fixture.Create(WorldRecordMode.SimulatedHistory);
var adapter = fixture.CreateAdapter();
var runtime = adapter.Restore(adapter.CreateSaveData(fixture.State));
runtime.Economy.AddMoney(sss.Price * 2);
var permission = new OwnerSchedulePermission(true, "");
ShopService CreateShop()
{
    var wallet = new ManagerEconomyShopWallet(runtime.Economy);
    var fulfillment = new OwnerSkillBlockPackFulfillment(resolver, growth.SkillBlocks, wallet,
        () => runtime.PlayerGrowth.Inventory, () => random, () => permission);
    return new ShopService(catalog, ShopAvailabilityFactory.CreateForOwner(permission), wallet,
        new IShopProductFulfillment[] { fulfillment }, runtime.ShopPurchaseHistory);
}
var shop = CreateShop();
long money = runtime.Economy.Money;
int blocks = runtime.PlayerGrowth.Inventory.Blocks.Count;
Check(shop.Purchase(sss.ProductId).IsSuccess, "실제 구매");
Check(runtime.Economy.Money == money - sss.Price, "정확한 결제");
Check(runtime.PlayerGrowth.Inventory.Blocks.Count == blocks + 1 &&
    definitions[runtime.PlayerGrowth.Inventory.Blocks.Last().DefinitionId].Rarity == SkillBlockRarity.Mythic, "SSS 지급");
Check(!shop.Purchase(sss.ProductId).IsSuccess && runtime.Economy.Money == money - sss.Price &&
    runtime.PlayerGrowth.Inventory.Blocks.Count == blocks + 1, "중복 구매 무변경 거부");
var jsonOptions = new JsonSerializerOptions { IncludeFields = true };
runtime = adapter.Restore(JsonSerializer.Deserialize<ManagerHistoricalSaveData>(
    JsonSerializer.Serialize(adapter.CreateSaveData(runtime), jsonOptions), jsonOptions));
shop = CreateShop();
Check(!shop.GetQuote(sss).CanPurchase && runtime.ShopPurchaseHistory.GetPurchaseCount(sss.ProductId) == 1, "구매 한도 저장 복원");
Check(definitions[runtime.PlayerGrowth.Inventory.Blocks.Last().DefinitionId].Rarity == SkillBlockRarity.Mythic, "지급 저장 복원");
runtime.ShopPurchaseHistory.ResetPeriod();
Check(shop.Purchase(sss.ProductId).IsSuccess, "다음 주기 재구매");
runtime.ShopPurchaseHistory.ResetPeriod();
runtime.Economy.TrySpendMoney(runtime.Economy.Money);
blocks = runtime.PlayerGrowth.Inventory.Blocks.Count;
Check(!shop.Purchase(sss.ProductId).IsSuccess && runtime.PlayerGrowth.Inventory.Blocks.Count == blocks &&
    runtime.ShopPurchaseHistory.GetPurchaseCount(sss.ProductId) == 0, "잔액 부족 무변경 거부");
var player = new SkillGachaService(growth.SkillGacha, growth.SkillBlocks);
var board = new SkillBoardState("validation");
var economy = new CareerEconomyState(sss.Price * 2);
var acquired = player.PullSingle(economy, board, SkillBlockCategory.Contact, SkillGachaPurchaseTier.Mythic, 2026, random);
Check(definitions[acquired.DefinitionId].Rarity == SkillBlockRarity.Mythic &&
    board.GetLimitedPurchaseCount(SkillGachaPurchaseTier.Mythic, 2026) == 1 &&
    board.GetLimitedPurchaseCount(SkillGachaPurchaseTier.Legendary, 2026) == 0, "선수 SSS 전용 구매 한도");
bool rejected = false;
try { player.PullSingle(economy, board, SkillBlockCategory.Contact, SkillGachaPurchaseTier.Mythic, 2026, random); }
catch (InvalidOperationException) { rejected = true; }
Check(rejected && economy.Money == sss.Price, "선수 중복 결제 차단");
player.PullSingle(economy, board, SkillBlockCategory.Contact, SkillGachaPurchaseTier.Mythic, 2027, random);
Check(economy.Money == 0 && board.GetLimitedPurchaseCount(SkillGachaPurchaseTier.Mythic, 2027) == 1, "선수 시즌 한도 초기화");
var serializerType = typeof(Baseball.Game.Career.NewGameFlow).Assembly.GetType("Baseball.Game.Career.Persistence.CareerSaveGraphSerializer", true);
var serializer = Activator.CreateInstance(serializerType, true);
var graph = serializerType.GetMethod("Capture").Invoke(serializer, new object[] { board });
var graphCopy = JsonSerializer.Deserialize(JsonSerializer.Serialize(graph, graph.GetType(), jsonOptions), graph.GetType(), jsonOptions);
var boardCopy = (SkillBoardState)serializerType.GetMethod("Restore").MakeGenericMethod(typeof(SkillBoardState)).Invoke(serializer, new[] { graphCopy });
Check(boardCopy.GetLimitedPurchaseCount(SkillGachaPurchaseTier.Mythic, 2027) == 1 &&
    boardCopy.OwnedBlocks.Count == 2, "선수 그래프 JSON 저장 복원");
var entries = System.Text.RegularExpressions.Regex.Matches(System.IO.File.ReadAllText(
    "Assets/10.Datas/Resources/NewGame/Growth/SkillGachaOfferCatalog.asset"), @"(?ms)^  - _tier: .*?(?=^  - _tier:|^  _fivePullDiscountRate:)");
Check(entries.Count == 6, "저작 상품 6종");
foreach (System.Text.RegularExpressions.Match entry in entries)
{
    double Field(string name) {
        var match = System.Text.RegularExpressions.Regex.Match(entry.Value, @"(?m)^\s*(?:- )?" + name + @":\s*([^\r\n]*)");
        return match.Success ? double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : 0;
    }
    var offer = growth.SkillGacha.GetOffer((SkillGachaPurchaseTier)(int)Field("_tier"));
    Check(Field("_price") == offer.Price && Field("_maxPurchasesPerOffseason") == offer.MaxPurchasesPerOffseason, "저작 가격·한도 일치");
    string[] names = { "_normal", "_rare", "_elite", "_unique", "_legendary", "_mythic" };
    for (int grade = 0; grade < 6; grade++) Check(Field(names[grade]) == offer.GetProbability((SkillBlockRarity)grade), "저작 확률 일치");
}
Console.WriteLine($"600,000회 지급 · {checks}건 검증 통과");
void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); checks++; }
