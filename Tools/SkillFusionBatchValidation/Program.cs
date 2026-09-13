using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Game.Historical;
using Baseball.Simulation.Historical;
using Baseball.Simulation.Random;
using Baseball.Tools.ManagerReportValidation;

// 에디터·NUnit을 실행하지 않고 실제 명령과 저장 복사 계약을 확인한다.
var options = new JsonSerializerOptions { IncludeFields = true };
var balance = JsonSerializer.Deserialize<OwnerDevelopmentBalance>(
    File.ReadAllText("Assets/10.Datas/Resources/NewGame/OwnerDevelopment.json"), options).fusion;
var definitions = GrowthSkillContent.CreateDefaultBlocks();
var fixture = RuntimeFixture.Fixture.Create(WorldRecordMode.SimulatedHistory);
var adapter = fixture.CreateAdapter();
var source = adapter.CreateSimulationCopy(fixture.State);
var block = definitions.First(b => b.Category == SkillBlockCategory.Contact && (int)b.Rarity == 0);
var ids = Enumerable.Range(0, 6).Select(_ => source.PlayerGrowth.Inventory.Add(block.BlockId).InstanceId).ToArray();
int checks = 0;
Check(OwnerScheduleGateService.GetPhase(source) == OwnerSeasonPhase.RegularSeason, "시즌 중 검증 환경");
Check(OwnerScheduleGateService.Evaluate(source, OwnerGrowthAction.SkillBlock).IsAllowed, "시즌 중 스킬 허용");
Check(!OwnerScheduleGateService.Evaluate(source, OwnerGrowthAction.Correction).IsAllowed, "능력치 교정 시즌 제한 유지");
for (int run = 0; run < 30; run++)
for (int focusIndex = 0; focusIndex < 3; focusIndex++)
{
    var batch = adapter.CreateSimulationCopy(source);
    var single = adapter.CreateSimulationCopy(source);
    // 승급 보장 직전과 직후를 포함해 앞 슬롯이 뒤 슬롯에 미치는 영향을 비교한다.
    int failures = run % (balance.pityFailures + 1);
    for (int i = 0; i < failures; i++)
    {
        batch.PlayerGrowth.Inventory.RecordFusion(block.Rarity, block.Rarity);
        single.PlayerGrowth.Inventory.RecordFusion(block.Rarity, block.Rarity);
    }
    var focus = (SkillFusionFocus)focusIndex;
    var results = OwnerSkillResearchService.FuseBatch(batch, definitions, ids, balance, focus,
        count => new Pcg32Random((ulong)run, (ulong)count + 2701UL));
    for (int i = 0; i < 3; i++)
    {
        var result = OwnerSkillResearchService.Fuse(single, definitions, ids[i * 2], ids[i * 2 + 1], balance, focus,
            new Pcg32Random((ulong)run, (ulong)single.PlayerGrowth.Inventory.FusionCount + 2701UL));
        Check(result.BlockId == results[i].BlockId, "슬롯 순서·개별 추첨 결과 일치");
    }
    Check(Snapshot(batch) == Snapshot(single), "돈·포인트·천장·보유 블록·저장 상태 일치");
    Check(batch.Economy.Money == source.Economy.Money - 3 * balance.cost, "전체 비용");
    Check(batch.PlayerGrowth.Inventory.Blocks.Count == source.PlayerGrowth.Inventory.Blocks.Count - 3, "6개 소모·3개 지급");
    Check(Snapshot(adapter.Restore(adapter.CreateSaveData(batch))) == Snapshot(batch), "저장 복원");
}
Reject(new[] { ids[0], ids[1], ids[0], ids[2] }, "중복 재료");
Reject(new[] { ids[0], ids[1], ids[2] }, "미완성 슬롯");
Reject(new[] { ids[0], ids[1], ids[2], int.MaxValue }, "뒤 슬롯의 없는 재료");
Reject(ids, "잘못된 보정", (SkillFusionFocus)99);
var pitcher = definitions.First(b => b.Category == SkillBlockCategory.Control);
int pitcherId = source.PlayerGrowth.Inventory.Add(pitcher.BlockId).InstanceId;
Reject(new[] { ids[0], ids[1], ids[2], pitcherId }, "뒤 슬롯 유형 불일치");
new OwnerSkillBoardService(GrowthBalanceTable.CreateDefault()).Place(source.PlayerGrowth.Inventory,
    source.OwnedCards[0].SkillBoard, ids[4], 0, 0, 0);
Reject(ids, "장착 블록 보호");
new OwnerSkillBoardService(GrowthBalanceTable.CreateDefault()).Remove(source.OwnedCards[0].SkillBoard, ids[4]);
string before = Snapshot(source);
var candidate = adapter.CreateSimulationCopy(source);
int calls = 0;
try
{
    OwnerSkillResearchService.FuseBatch(candidate, definitions, ids, balance, SkillFusionFocus.Grade,
        count => ++calls == 2 ? throw new InvalidOperationException("중간 실패 주입") : new Pcg32Random(1));
    throw new Exception("실패 주입이 적용되지 않았습니다.");
}
catch (InvalidOperationException) { Check(Snapshot(source) == before, "중간 실패 시 실제 상태 보존"); }
// 월드 복사 없는 트랜잭션도 저장 실패·중간 실패에 정확히 같은 상태로 돌아와야 한다.
for (int failure = 0; failure < 3; failure++)
{
    string original = Snapshot(source);
    int rolls = 0;
    try
    {
        OwnerSkillTransaction.Execute(source, () =>
        {
            if (failure == 2)
                new OwnerSkillBoardService(GrowthBalanceTable.CreateDefault()).Place(source.PlayerGrowth.Inventory,
                    source.OwnedCards[0].SkillBoard, ids[4], 0, 0, 0);
            else OwnerSkillResearchService.FuseBatch(source, definitions, ids, balance, SkillFusionFocus.Grade,
                count => failure == 1 && ++rolls == 2 ? throw new InvalidOperationException("중간 실패") : new Pcg32Random(1));
            return true;
        }, () => throw new IOException("저장 실패"));
        throw new Exception("실패 주입 누락");
    }
    catch (Exception e) when (e is IOException || e is InvalidOperationException)
    { Check(Snapshot(source) == original, "합성·배치 실패 전체 저장 DTO 복구 " + failure); }
}
var expected = adapter.CreateSimulationCopy(source);
OwnerSkillResearchService.FuseBatch(expected, definitions, ids, balance, SkillFusionFocus.Shape, count => new Pcg32Random(1, (ulong)count));
var actual = adapter.CreateSimulationCopy(source);
int saves = 0;
OwnerSkillTransaction.Execute(actual, () =>
{
    OwnerSkillResearchService.FuseBatch(actual, definitions, ids, balance, SkillFusionFocus.Shape, count => new Pcg32Random(1, (ulong)count));
    return true;
}, () => { saves++; Check(Snapshot(actual) == Snapshot(expected), "저장 시 확정 상태 일치"); });
Check(saves == 1 && Snapshot(actual) == Snapshot(expected), "기존 복사 경로와 결과·저장 횟수 일치");
Check(!OwnerSkillTransaction.Execute(source, () => false, () => throw new Exception("불필요한 저장")), "무변경 시 저장 생략");

// 동일한 저장 DTO 생성을 포함해 월드 복사 제거 효과만 비교한다. 디스크와 Unity UI 시간은 제외한다.
const int iterations = 30;
var timer = System.Diagnostics.Stopwatch.StartNew();
for (int i = 0; i < iterations; i++) adapter.CreateSaveData(adapter.CreateSimulationCopy(source));
double oldMs = timer.Elapsed.TotalMilliseconds / iterations;
timer.Restart();
for (int i = 0; i < iterations; i++)
    OwnerSkillTransaction.Execute(source, () => true, () => adapter.CreateSaveData(source));
double newMs = timer.Elapsed.TotalMilliseconds / iterations;
Console.WriteLine($"커밋 준비 평균 ({iterations}회): 월드 복사 {oldMs:F3} ms → 스킬 체크포인트 {newMs:F3} ms (디스크·UI 제외)");
source.Economy.TrySpendMoney(source.Economy.Money - balance.cost);
Reject(ids, "전체 비용 부족");
Console.WriteLine($"일괄 합성 콘솔 검증 {checks}건 통과 · 에디터 테스트 미실행");

string Snapshot(ManagerHistoricalRuntimeState state) => JsonSerializer.Serialize(adapter.CreateSaveData(state), options);
void Check(bool success, string label)
{
    if (!success) throw new InvalidOperationException(label);
    checks++;
}
void Reject(int[] materials, string label, SkillFusionFocus focus = SkillFusionFocus.Grade)
{
    string previous = Snapshot(source);
    bool rejected = false;
    try { OwnerSkillResearchService.FuseBatch(source, definitions, materials, balance, focus, _ => new Pcg32Random(1)); }
    catch (Exception e) when (e is InvalidOperationException || e is ArgumentException) { rejected = true; }
    Check(rejected && Snapshot(source) == previous, label + " 무변경");
}
