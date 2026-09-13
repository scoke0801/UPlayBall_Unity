using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Baseball.Core;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Game.Historical;
using Baseball.Simulation.Historical;
using Baseball.Simulation.Random;
using Baseball.Tools.ManagerReportValidation;

// 에디터 실행 없이 신규 등급의 실제 획득·배치·저장과 리소스 누락을 검증한다.
int checks = 0;
var growth = GrowthBalanceTable.CreateDefault();
var definitions = growth.SkillBlocks;
var options = new JsonSerializerOptions { IncludeFields = true };
var fusion = JsonSerializer.Deserialize<OwnerDevelopmentBalance>(File.ReadAllText("Assets/10.Datas/Resources/NewGame/OwnerDevelopment.json"), options).fusion;
Check(definitions.Length == 12 * SkillBlockGradeCatalog.Count * 7, "504종 카탈로그");
Check(new GrowthContentValidator().Validate(growth).All(issue => issue.Severity != ContentValidationSeverity.Error), "성장 콘텐츠 정합성");
string authored = File.ReadAllText("Assets/10.Datas/Resources/NewGame/Growth/SkillBlockCatalog.asset");
if (!args.Contains("--export-sss"))
{
    var entries = Regex.Matches(authored, @"(?ms)^  - _blockId: .*?(?=^  - _blockId:|\z)");
    Check(entries.Count == 84, "SSS 저작 자산 84종");
    foreach (Match entry in entries)
    {
        string Field(string name) => Regex.Match(entry.Value, @"(?m)^\s*(?:- )?" + name + @":\s*([^\r\n]*)").Groups[1].Value.Trim();
        var definition = definitions.Single(block => block.BlockId == Field("_blockId"));
        Check(int.Parse(Field("_rarity")) == (int)definition.Rarity, "저작 등급");
        Check(int.Parse(Field("_category")) == (int)definition.Category, "저작 계열");
        Check(int.Parse(Field("_ability")) == (int)definition.AbilityBonuses[0].Ability && int.Parse(Field("_amount")) == definition.AbilityBonuses[0].Amount, "저작 능력치");
        Check(long.Parse(Field("_sellValue")) == definition.SellValue, "저작 판매가");
        Check(SameCells(definition.ShapeCells, TetrominoShapeCatalog.CreateCells((TetrominoShape)int.Parse(Field("_shape")))), "저작 모양");
    }
}
foreach (SkillBlockRarity rarity in Enum.GetValues<SkillBlockRarity>())
{
    string label = SkillBlockGradeCatalog.GetLabel(rarity);
    Check(definitions.Count(block => block.Rarity == rarity) == 84, label + " 84종");
    foreach (SkillBlockCategory category in Enum.GetValues<SkillBlockCategory>())
        Check(definitions.Count(block => block.Rarity == rarity && block.Category == category) == 7, label + " 계열별 7모양");
    for (int mask = 0; mask < 15; mask++)
    {
        string path = "Assets/10.Datas/Resources/UI/OwnerPowerUp/SkillGrades/skill_" + label + "_" + mask + ".png";
        Check(File.Exists(path) && File.Exists(path + ".meta"), label + " 방향 " + mask);
    }
    string badge = "Assets/10.Datas/Resources/UI/PlayerGrowthBadges/SkillGrade_" + label + ".png";
    Check(File.Exists(badge) && File.Exists(badge + ".meta"), label + " 배지 연결");
}
var fixture = RuntimeFixture.Fixture.Create(WorldRecordMode.SimulatedHistory);
var adapter = fixture.CreateAdapter();
var runtime = adapter.Restore(adapter.CreateSaveData(fixture.State));
var stock = runtime.PlayerGrowth.Inventory;
var material = definitions.First(block => block.Rarity == SkillBlockRarity.Legendary && block.Category == SkillBlockCategory.Contact);
int first = stock.Add(material.BlockId).InstanceId;
int second = stock.Add(material.BlockId).InstanceId;
var failures = new int[SkillBlockGradeCatalog.Count - 1];
failures[(int)SkillBlockRarity.Legendary] = fusion.pityFailures;
stock.RestoreFusion(fusion.pityFailures, fusion.pityFailures, failures);
var result = OwnerSkillResearchService.Fuse(runtime, definitions, first, second, fusion, SkillFusionFocus.Grade, new Pcg32Random(6273));
Check(result.Rarity == SkillBlockRarity.Mythic, "SS 천장 실제 명령 SSS 지급");
Check(result.AbilityBonuses[0].Amount == 6, "SSS 능력치 +6");
Check(stock.GetFusionFailures(SkillBlockRarity.Legendary) == 0, "SS 천장 초기화");
var placed = stock.Blocks.Last();
var boardService = new OwnerSkillBoardService(growth);
Check(boardService.TryPlaceFirstAvailable(stock, runtime.OwnedCards[0].SkillBoard, placed.InstanceId), "SSS 실제 배치");
var restored = adapter.Restore(JsonSerializer.Deserialize<ManagerHistoricalSaveData>(JsonSerializer.Serialize(adapter.CreateSaveData(runtime), options), options));
Check(restored.OwnedCards[0].SkillBoard.Placements.Any(block => block.Instance.DefinitionId == result.BlockId), "SSS 배치 저장 왕복");
Check(restored.PlayerGrowth.Inventory.CopyFusionFailures().Length == 5, "5단계 승급 천장 저장");
Check(restored.PlayerGrowth.Inventory.GetFusionFailures(SkillBlockRarity.Mythic) == 0, "SSS 천장 미적립");
var effects = new Baseball.Simulation.Growth.SkillBoardService(growth.SkillBoard, definitions);
Check(effects.GetAbilityBonus(restored.OwnedCards[0].SkillBoard.Placements, result.AbilityBonuses[0].Ability) == 6, "복원 후 실제 +6");
Console.WriteLine($"총 {checks}건: 카탈로그·90개 방향 타일·6개 배지·SSS 천장 지급·배치·저장 검증 통과");

// 기본 콘텐츠도 저작 자산에서 수치를 조정할 수 있게 신규 SSS 정의를 내보낸다.
if (args.Contains("--export-sss"))
{
    string path = "Assets/10.Datas/Resources/NewGame/Growth/SkillBlockCatalog.asset";
    string asset = File.ReadAllText(path);
    if (!asset.Contains("  _blocks: []")) throw new InvalidOperationException("기존 저작 블록을 덮어쓸 수 없습니다.");
    var yaml = new StringBuilder("  _blocks:\n");
    foreach (var block in definitions.Where(block => block.Rarity == SkillBlockRarity.Mythic))
    {
        var shape = Enum.GetValues<TetrominoShape>().First(value => SameCells(block.ShapeCells, TetrominoShapeCatalog.CreateCells(value)));
        yaml.AppendLine("  - _blockId: " + block.BlockId);
        yaml.AppendLine("    _rarity: " + (int)block.Rarity);
        yaml.AppendLine("    _category: " + (int)block.Category);
        yaml.AppendLine("    _shape: " + (int)shape);
        yaml.AppendLine("    _canRotate: " + (block.CanRotate ? "1" : "0"));
        yaml.AppendLine("    _abilityBonuses:");
        foreach (var bonus in block.AbilityBonuses) { yaml.AppendLine("    - _ability: " + (int)bonus.Ability); yaml.AppendLine("      _amount: " + bonus.Amount); }
        yaml.AppendLine("    _sellValue: " + block.SellValue);
        yaml.AppendLine(("    _traitId: " + block.TraitId).TrimEnd());
        yaml.AppendLine("    _isUniqueReward: 0");
    }
    File.WriteAllText(path, asset.Replace("  _blocks: []", yaml.ToString().TrimEnd()), new UTF8Encoding(false));
    Console.WriteLine("SSS 84종 저작 자산 작성");
}

bool SameCells(BoardCell[] firstCells, BoardCell[] secondCells) => firstCells.All(cell => secondCells.Any(other => cell.X == other.X && cell.Y == other.Y));
void Check(bool passed, string name) { if (!passed) throw new InvalidOperationException(name); checks++; }
