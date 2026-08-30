using Baseball.Core.Balance;
using Baseball.Game.Data;
using UnityEngine;

namespace Baseball.Editor.Tools
{
    /// <summary>실제 NewGameDefinition 변환 결과와 코드 기본 성장 콘텐츠를 한 경로로 검증한다.</summary>
    public static class GrowthContentValidationTool
    {
        [BaseballEditorTool(
            "밸런스",
            "성장 콘텐츠 정합성 검증",
            "프로그램 ID·기간·티어, 블록 수치·Trait, 등급별 뽑기 풀과 판매가를 검사합니다.",
            order: 10,
            impact: ToolImpact.ReadOnly)]
        public static void Validate()
        {
            GrowthBalanceTable balance = NewGameDefinition.LoadConfiguration().Balance.Growth;
            ContentValidationIssue[] issues = new GrowthContentValidator().Validate(balance);
            if (issues.Length == 0)
            {
                Debug.Log("[GrowthContentValidation] 오류 없음");
                return;
            }

            for (int index = 0; index < issues.Length; index++)
            {
                ContentValidationIssue issue = issues[index];
                string message = $"[GrowthContentValidation/{issue.Code}] {issue.ContentId}: {issue.Message}";
                if (issue.Severity == ContentValidationSeverity.Error)
                    Debug.LogError(message);
                else
                    Debug.LogWarning(message);
            }
        }
    }
}
