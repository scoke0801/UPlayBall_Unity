using System.Text.Json;
using Baseball.Game.Guide;

namespace Baseball.Tools.GuideValidation;

/// <summary>에디터 없이 실제 가이드 JSON과 스킬 배치 알림의 수신·반복 제어를 검증한다.</summary>
internal static class Program
{
    private static void Main(string[] args)
    {
        string path = args.Length > 0 ? args[0] : "Assets/10.Datas/FrontManager/front_manager_guide_dataset_v1.json";
        var data = JsonSerializer.Deserialize<GuideDatasetData>(File.ReadAllText(path),
            new JsonSerializerOptions { IncludeFields = true });
        if (!GuideDatasetFactory.TryCreate(data, out var catalog, out var issues))
            throw new InvalidOperationException(string.Join("\n", issues.Select(issue => issue.ToString())));

        foreach (string operation in new[] { "manual-placement", "automatic-placement" })
        {
            var guide = new FrontManagerGuide(catalog);
            var payload = new Dictionary<string, string> { ["cardId"] = "card", ["instanceId"] = "1" };
            var fact = new GuideFact(GuideModeScope.Owner, "SkillBlockPlaced",
                new GuideFactIdentity(1, operation, "save"), payload);
            var result = guide.Enqueue(fact);
            Require(result.IsAccepted && result.EnqueuedCount == 1, "스킬 배치 Fact 수신: " + result.Error);
            Require(guide.TryDequeue(new GuideDisplayContext(Array.Empty<string>(), false, false), out var message), "안내 표시");
            Require(message.CueId == "SKILL_BLOCK_PLACED", "스킬 배치 안내 선택");
            var restored = new FrontManagerGuide(catalog);
            restored.RepeatState.Restore(guide.RepeatState.Capture());
            var repeated = restored.Enqueue(new GuideFact(GuideModeScope.Owner, "SkillBlockPlaced",
                new GuideFactIdentity(1, operation + "-again", "save"), payload));
            Require(repeated.IsAccepted && repeated.EnqueuedCount == 0, "저장 복원 후 중복 안내 억제");
            var invalid = guide.Enqueue(new GuideFact(GuideModeScope.Owner, "UnknownFact",
                new GuideFactIdentity(1, operation + "-invalid", "other-save")));
            Require(!invalid.IsAccepted, "미등록 Fact 검증 유지");
        }
        Console.WriteLine($"가이드 카탈로그 검증 통과: {data.cueDefinitions.Length} cues / {data.factTypeIndex.Length} facts");
        Console.WriteLine("수동·자동 배치 Fact 수신, 표시, 저장 복원 후 중복 억제, 미등록 Fact 거부: 10개 검증 통과");
    }

    private static void Require(bool passed, string description)
    {
        if (!passed) throw new InvalidOperationException(description);
    }
}
