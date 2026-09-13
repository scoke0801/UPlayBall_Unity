using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Game.Historical;
using Baseball.Simulation.Random;
using Baseball.Simulation.Historical;
using Baseball.Tools.ManagerReportValidation;

// 에디터·NUnit 없이 실제 합성 추첨기와 저장 매퍼를 검증한다.
var jsonOptions = new JsonSerializerOptions { IncludeFields = true };
var balance = JsonSerializer.Deserialize<OwnerDevelopmentBalance>(
    File.ReadAllText("Assets/10.Datas/Resources/NewGame/OwnerDevelopment.json"), jsonOptions).fusion;
balance.Validate();
var definitions = GrowthSkillContent.CreateDefaultBlocks();
int checks = 0;
const int samples = 20000;
long totalRolls = 0;
double worstError = 0;
for (int firstGrade = 0; firstGrade < SkillBlockGradeCatalog.Count; firstGrade++)
for (int secondGrade = firstGrade; secondGrade < SkillBlockGradeCatalog.Count; secondGrade++)
for (int focusIndex = 0; focusIndex < 3; focusIndex++)
{
    var first = Block(firstGrade);
    var second = definitions.First(b => (int)b.Rarity == secondGrade && b.Category == first.Category && OwnerSkillResearchService.HaveSameShape(first, b));
    var focus = (SkillFusionFocus)focusIndex;
    var expected = OwnerSkillResearchService.GetGradeProbabilities(first, second, balance, focus, 0);
    var counts = new int[SkillBlockGradeCatalog.Count];
    int category = 0, shape = 0;
    var random = new Pcg32Random((ulong)(100 + firstGrade * 100 + secondGrade * 10 + focusIndex));
    for (int i = 0; i < samples; i++)
    {
        var result = OwnerSkillResearchService.RollFusion(definitions, first, second, balance, focus, 0, random);
        counts[(int)result.Rarity]++;
        if (result.Category == first.Category) category++;
        if (OwnerSkillResearchService.HaveSameShape(first, result)) shape++;
    }
    totalRolls += samples;
    for (int i = 0; i < SkillBlockGradeCatalog.Count; i++)
    {
        double error = Math.Abs(counts[i] / (double)samples - expected[i]);
        worstError = Math.Max(worstError, error);
        Check(error < .018, "등급 분포");
        if (i < secondGrade - 1) Check(counts[i] == 0, "한 단계 하락 한도");
    }
    double categoryExpected = Math.Min(1, balance.sameCategoryChance + (focus == SkillFusionFocus.Ability ? balance.categoryBonus : 0));
    double shapeExpected = Math.Min(1, balance.sameShapeChance + (focus == SkillFusionFocus.Shape ? balance.shapeBonus : 0));
    Check(Math.Abs(category / (double)samples - categoryExpected) < .018, "능력치 유지 분포");
    Check(Math.Abs(shape / (double)samples - shapeExpected) < .018, "모양 유지 분포");
    if (firstGrade == secondGrade)
        Console.WriteLine($"{first.Rarity}+동급/{focus}: 등급 [{string.Join(", ", counts.Select(c => (c * 100d / samples).ToString("0.00")))}] 능력치 {category * 100d / samples:0.00}% 모양 {shape * 100d / samples:0.00}%");
}
var inventory = new OwnerSkillBlockInventoryState();
for (int grade = 0; grade < SkillBlockGradeCatalog.Count - 1; grade++)
{
    for (int i = 0; i < balance.pityFailures; i++) inventory.RecordFusion((SkillBlockRarity)grade, (SkillBlockRarity)grade);
    Check(inventory.GetFusionFailures((SkillBlockRarity)grade) == balance.pityFailures, "등급별 천장 적립");
    var first = Block(grade);
    var before = OwnerSkillResearchService.GetGradeProbabilities(first, first, balance, SkillFusionFocus.Grade, balance.pityFailures - 1);
    Check(before.Take(grade + 1).Sum() > 0, "10번째 합성은 아직 확률형");
    for (int seed = 0; seed < 1000; seed++)
        Check(OwnerSkillResearchService.RollFusion(definitions, first, first, balance, SkillFusionFocus.Shape,
            inventory.GetFusionFailures(first.Rarity), new Pcg32Random((ulong)seed)).Rarity > first.Rarity, "11번째 승급 보장");
    totalRolls += 1000;
    inventory.RecordFusion(first.Rarity, first.Rarity + 1);
    Check(inventory.GetFusionFailures(first.Rarity) == 0, "승급 후 천장 초기화");
}
var shuffled = definitions.Reverse().ToArray();
for (int seed = 0; seed < 1000; seed++)
{
    var a = OwnerSkillResearchService.RollFusion(definitions, Block(0), Block(2), balance, SkillFusionFocus.Grade, 0, new Pcg32Random((ulong)seed));
    var b = OwnerSkillResearchService.RollFusion(shuffled, Block(2), Block(0), balance, SkillFusionFocus.Grade, 0, new Pcg32Random((ulong)seed));
    Check(a.BlockId == b.BlockId, "정의 순서·재료 순서와 무관한 결정론");
}
totalRolls += 2000;
var growth = new OwnerPlayerGrowthState();
growth.Inventory.RestoreFusion(inventory.FusionCount, inventory.FusionPoints, inventory.CopyFusionFailures());
growth.Inventory.RecordFusion(SkillBlockRarity.Rare, SkillBlockRarity.Normal);
var create = typeof(ManagerHistoricalSaveAdapter).GetMethod("CreatePlayerGrowth", BindingFlags.NonPublic | BindingFlags.Static);
var restore = typeof(ManagerHistoricalSaveAdapter).GetMethod("RestorePlayerGrowth", BindingFlags.NonPublic | BindingFlags.Static);
var save = (OwnerPlayerGrowthSaveData)create.Invoke(null, new object[] { growth });
var copy = JsonSerializer.Deserialize<OwnerPlayerGrowthSaveData>(JsonSerializer.Serialize(save, jsonOptions), jsonOptions);
var restored = (OwnerPlayerGrowthState)restore.Invoke(null, new object[] { copy });
Check(restored.Inventory.FusionCount == growth.Inventory.FusionCount && restored.Inventory.FusionPoints == growth.Inventory.FusionPoints &&
    restored.Inventory.CopyFusionFailures().SequenceEqual(growth.Inventory.CopyFusionFailures()), "실제 매퍼·JSON 천장·포인트 왕복");
restored.Inventory.ExchangeFusionPoints(balance.pointsPerCraft);
Check(restored.Inventory.FusionPoints == growth.Inventory.FusionPoints - balance.pointsPerCraft, "포인트 차감");
Check(growth.Inventory.FusionPoints == inventory.FusionPoints + 1, "저장 복사 독립성");
var fixture = RuntimeFixture.Fixture.Create(WorldRecordMode.SimulatedHistory);
var adapter = fixture.CreateAdapter();
var runtime = adapter.Restore(adapter.CreateSaveData(fixture.State));
var stock = runtime.PlayerGrowth.Inventory;
int firstId = stock.Add(Block(0).BlockId).InstanceId;
int secondId = stock.Add(Block(0).BlockId).InstanceId;
Reject(firstId, firstId, SkillFusionFocus.Grade, "중복 재료 차단");
Reject(firstId, int.MaxValue, SkillFusionFocus.Grade, "없는 재료 차단");
Reject(firstId, secondId, (SkillFusionFocus)99, "잘못된 보정 차단");
int pitcherId = stock.Add(definitions.First(b => b.Category == SkillBlockCategory.Control).BlockId).InstanceId;
Reject(firstId, pitcherId, SkillFusionFocus.Grade, "야수·투수 혼합 차단");
new OwnerSkillBoardService(GrowthBalanceTable.CreateDefault()).Place(stock, runtime.OwnedCards[0].SkillBoard, firstId, 0, 0, 0);
Reject(firstId, secondId, SkillFusionFocus.Grade, "장착 재료 보호");
new OwnerSkillBoardService(GrowthBalanceTable.CreateDefault()).Remove(runtime.OwnedCards[0].SkillBoard, firstId);
long money = runtime.Economy.Money;
int blockCount = stock.Blocks.Count;
var rolled = OwnerSkillResearchService.Fuse(runtime, definitions, firstId, secondId, balance, SkillFusionFocus.Grade, new Pcg32Random(100));
Check(stock.Blocks.Count == blockCount - 1 && !stock.Contains(firstId) && !stock.Contains(secondId) &&
    stock.Blocks.Any(b => b.DefinitionId == rolled.BlockId), "실제 2개 소모·1개 지급");
Check(runtime.Economy.Money == money - balance.cost && stock.FusionCount == 1 && stock.FusionPoints == 1, "PT와 횟수·포인트 동시 반영");
Reject(firstId, secondId, SkillFusionFocus.Grade, "중복 실행 차단");
runtime = adapter.Restore(JsonSerializer.Deserialize<ManagerHistoricalSaveData>(JsonSerializer.Serialize(adapter.CreateSaveData(runtime), jsonOptions), jsonOptions));
stock = runtime.PlayerGrowth.Inventory;
Check(stock.FusionCount == 1 && stock.FusionPoints == 1 && !stock.Contains(firstId), "전체 저장 JSON·런타임 복원");
firstId = stock.Add(Block(0).BlockId).InstanceId;
secondId = stock.Add(Block(0).BlockId).InstanceId;
var clone = adapter.CreateSimulationCopy(runtime);
var next = OwnerSkillResearchService.Fuse(runtime, definitions, firstId, secondId, balance, SkillFusionFocus.Ability,
    new Pcg32Random(runtime.WorldHistory.WorldHistorySeed, (ulong)stock.FusionCount + 2701UL));
var nextCopy = OwnerSkillResearchService.Fuse(clone, definitions, firstId, secondId, balance, SkillFusionFocus.Ability,
    new Pcg32Random(clone.WorldHistory.WorldHistorySeed, (ulong)clone.PlayerGrowth.Inventory.FusionCount + 2701UL));
Check(next.BlockId == nextCopy.BlockId && runtime.Economy.Money == clone.Economy.Money, "저장 복사본의 다음 추첨 일치");
firstId = stock.Add(Block(0).BlockId).InstanceId;
secondId = stock.Add(Block(0).BlockId).InstanceId;
runtime.Economy.TrySpendMoney(runtime.Economy.Money);
Reject(firstId, secondId, SkillFusionFocus.Grade, "PT 부족 시 무변경");
stock.RestoreFusion(balance.pointsPerCraft, balance.pointsPerCraft, new int[SkillBlockGradeCatalog.Count - 1]);
var craft = Block(3);
int craftCount = stock.Blocks.Count;
OwnerSkillResearchService.Craft(runtime, craft, balance);
Check(stock.FusionPoints == 0 && stock.Blocks.Count == craftCount + 1 && stock.Blocks.Last().DefinitionId == craft.BlockId, "포인트 지정 제작");
Console.WriteLine($"총 {totalRolls:N0}회 분포·결정론 추첨 / {checks:N0}건 검증 / 등급 최대 오차 {worstError * 100:0.000}%p");

void Reject(int first, int second, SkillFusionFocus focus, string label)
{
    string before = JsonSerializer.Serialize(adapter.CreateSaveData(runtime), jsonOptions);
    bool rejected = false;
    try { OwnerSkillResearchService.Fuse(runtime, definitions, first, second, balance, focus, new Pcg32Random(1)); }
    catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException) { rejected = true; }
    Check(rejected && before == JsonSerializer.Serialize(adapter.CreateSaveData(runtime), jsonOptions), label);
}

SkillBlockDefinition Block(int grade) => definitions.First(b => (int)b.Rarity == grade && b.Category == SkillBlockCategory.Contact);
void Check(bool passed, string label)
{
    if (!passed) throw new InvalidOperationException(label);
    checks++;
}
