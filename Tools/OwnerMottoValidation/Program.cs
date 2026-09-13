using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Baseball.Core.Historical;
using Baseball.Game.Historical;
using Baseball.Presentation.Owner;
using Baseball.Tools.ManagerReportValidation;

// Unity·EditMode를 실행하지 않는 저장 왕복 및 안내 선택 계약 검증이다.
int checks = 0;
var options = new JsonSerializerOptions { IncludeFields = true };
var fixture = RuntimeFixture.Fixture.Create(WorldRecordMode.SimulatedHistory);
var adapter = fixture.CreateAdapter();
var profile = fixture.State.OwnerProfile;
Check(profile.Motto == OwnerProfileState.DefaultMotto, "새 게임 기본 한마디");
profile.ChangeMotto("  끝까지 함께하는 우리 구단  ");
Check(profile.Motto == "끝까지 함께하는 우리 구단", "앞뒤 공백 정리");
var save = adapter.CreateSaveData(fixture.State);
Check(save.saveVersion == 39 && save.ownerProfile.motto == profile.Motto, "저장 DTO 및 버전");
var serialized = JsonSerializer.Serialize(save, options);
var deserialized = JsonSerializer.Deserialize<ManagerHistoricalSaveData>(serialized, options);
Check(deserialized.ownerProfile.motto == profile.Motto, "JSON 한글 왕복");
Check(adapter.Restore(deserialized).OwnerProfile.Motto == profile.Motto, "실제 저장 어댑터 복원");
foreach (string invalid in new[] { "", "   ", new string('가', 41), "첫째\n둘째", "첫째\t둘째", "첫째\u2028둘째" })
{
    string previous = profile.Motto;
    try { profile.ChangeMotto(invalid); throw new Exception("잘못된 입력 허용"); }
    catch (ArgumentException) { Check(profile.Motto == previous, "입력 거절 시 기존 상태 보존"); }
}
profile.ChangeMotto(new string('가', 40));
Check(profile.Motto.Length == 40, "최대 길이 저장");
profile.ChangeMotto("<b>우리 구단</b>");
Check(profile.Motto == "<b>우리 구단</b>", "일반 텍스트 보존");
string json = File.ReadAllText("Assets/10.Datas/Resources/FrontManager/OwnerClubGuides.json");
var catalog = JsonSerializer.Deserialize<OwnerClubGuideCatalog>(json, options);
Check(catalog.entries.Length == 80, "안내 80개");
Check(catalog.entries.Select(e => e.id).Distinct().Count() == 80, "고유 ID");
Check(catalog.entries.Select(e => e.text).Distinct().Count() == 80, "고유 대사");
Check(catalog.entries.Select(e => e.expression).Distinct().Count() == 8, "표정 8종");
foreach (var entry in catalog.entries)
{
    Check(entry.text.Split('\n').Length == 3, "세 줄 안내");
    foreach (string manager in new[] { "01", "02", "03" })
        Check(File.Exists("Assets/10.Datas/Resources/FrontManager/FM_" + manager + "_" + entry.expression.Substring(3) + ".png"), "매니저별 실제 표정 자산");
}
var reached = new HashSet<string>();
foreach (var context in new[] {
    new OwnerClubInformationPresentationModel(),
    new OwnerClubInformationPresentationModel { Wins = 12, Losses = 3 },
    new OwnerClubInformationPresentationModel { Wins = 3, Losses = 12 },
    new OwnerClubInformationPresentationModel { Wins = 8, Losses = 8 },
    new OwnerClubInformationPresentationModel { Ties = 1 } })
{
    var recent = new Queue<string>();
    for (int visit = 0; visit < 500; visit++)
    {
        var guide = catalog.Select(context);
        Check(guide.IsEligible(context), "상황에 맞는 대사만 선택");
        Check(!recent.Contains(guide.id), "최근 여덟 대사 반복 금지");
        recent.Enqueue(guide.id);
        if (recent.Count > 8) recent.Dequeue();
        reached.Add(guide.id);
    }
}
Check(reached.Count == 80, "모든 대사 도달 가능");
Console.WriteLine($"PASS {checks:N0} checks; 80 guides, 8 expressions, 2,500 visits, save JSON round-trip.");
void Check(bool valid, string name) { if (!valid) throw new Exception(name); checks++; }
