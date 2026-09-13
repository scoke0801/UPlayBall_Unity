using System;
using System.Text.Json;
using Baseball.Core.Historical;
using Baseball.Game.Historical;

namespace Baseball.Tools.LegendaryPracticeValidation
{
    /// <summary>에디터 없이 실제 도전 원장의 재시작·보상·저장 계약을 검증한다.</summary>
    internal static class Program
    {
        private static int _checks;

        private static void Main()
        {
            var lower = new LegendaryPracticeTeam { challengeTeamId = "lower", rank = 2,
                rewardMoney = 10000, rewardDevelopment = 100, rewardScouting = 10 };
            var upper = new LegendaryPracticeTeam { challengeTeamId = "upper", rank = 1 };
            var catalog = new LegendaryPracticeCatalog { teams = new[] { upper, lower } };
            var state = new LegendaryPracticeState();
            var economy = new ManagerEconomyState();
            Check(!state.Restart(catalog, "lower"), "미도전 재시작 차단");
            Check(!state.Restart(catalog, "upper"), "잠긴 구단 재시작 차단");
            state.Commit(catalog, "lower", 1, 0, 1, 1);
            Check(state.Restart(catalog, "lower") && state.Get("lower").NextStarterIndex == 0,
                "1패 후 1선발 재시작");
            state.Commit(catalog, "lower", 2, 2, 0, 2);
            Check(state.Restart(catalog, "lower") && state.Get("lower").wins == 0,
                "진행 중 승수 초기화");
            for (int attempt = 3; attempt <= 5; attempt++) state.Commit(catalog, "lower", attempt, 2, 0, attempt);
            Check(state.Get("lower").HasCleared && state.CanPlay(catalog, upper), "3승 격파와 해금");
            state.Restart(catalog, "lower");
            Check(state.Get("lower").wins == 0 && state.Get("lower").HasCleared && state.CanPlay(catalog, upper),
                "재도전 후 격파와 해금 보존");
            Check(state.Claim(catalog, "lower", economy), "재도전 후 미수령 보상 수령");
            Check(!state.Commit(catalog, "lower", 5, 2, 0, 5), "재도전 후 과거 결과 중복 차단");
            var options = new JsonSerializerOptions { IncludeFields = true };
            state = LegendaryPracticeState.Restore(JsonSerializer.Deserialize<LegendaryPracticeProgress[]>(
                JsonSerializer.Serialize(state.Capture(), options), options));
            Check(state.Get("lower").NextStarterIndex == 0 && state.Get("lower").rewardClaimed &&
                state.Get("lower").attempts == 5 && state.Get("lower").losses == 1, "저장 복원과 통산 기록 보존");
            for (int attempt = 6; attempt <= 8; attempt++) state.Commit(catalog, "lower", attempt, 2, 0, attempt);
            Check(!state.Claim(catalog, "lower", economy) && economy.Money == 10000 &&
                economy.DevelopmentPoints == 100 && economy.ScoutingPoints == 10, "재격파 보상 중복 차단");
            state.Restart(catalog, "lower");
            Check(state.Get("lower").NextStarterIndex == 0 && state.Get("lower").wins == 0,
                "클리어 구단 반복 재시작");
            for (int attempt = 9; attempt <= 13; attempt++) state.Commit(catalog, "lower", attempt, 0, 0, attempt);
            Check(state.Get("lower").NextStarterIndex == 0, "재시작 기준 5선발 순환");
            Console.WriteLine($"재도전 콘솔 검증 {_checks}건 통과");
        }

        private static void Check(bool passed, string label)
        {
            if (!passed) throw new InvalidOperationException(label);
            _checks++;
        }
    }
}
