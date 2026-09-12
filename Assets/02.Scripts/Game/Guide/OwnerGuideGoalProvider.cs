using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Simulation.Historical;

namespace Baseball.Game.Guide
{
    /// <summary>원본 검증 결과의 문제를 그대로 목표로 투영하며 경기 규칙을 다시 계산하지 않는다.</summary>
    public static class OwnerGuideGoalProvider
    {
        public static IReadOnlyList<GuideGoal> Create(RosterValidationResult roster,
            LineupPresetValidationResult preset, bool hasNextGame, string publishedMatchKey)
        {
            var goals = new List<GuideGoal>();
            var keys = new HashSet<string>(StringComparer.Ordinal);
            if (roster != null)
            {
                foreach (var issue in roster.Issues)
                {
                    string key = "roster:" + issue.Code + ":" + issue.Context;
                    if (keys.Add(key)) goals.Add(new GuideGoal(key, GuideGoalKind.RosterIssue,
                        GuideTargetKind.Roster, true, issue.Code.ToString(), actual: issue.Actual, expected: issue.Expected));
                }
            }
            if (preset != null)
            {
                foreach (var issue in preset.Issues)
                {
                    if (issue.Code == LineupPresetValidationIssueCode.ActiveRosterInvalid) continue;
                    string key = "preset:" + preset.PresetId + ":" + issue.Code + ":" + issue.Group + ":" + issue.SlotIndex;
                    GuideTargetKind target = issue.Group == LineupPresetAssignmentGroup.TeamColor ? GuideTargetKind.TeamColor :
                        issue.Group == LineupPresetAssignmentGroup.Tactic ? GuideTargetKind.Tactic : GuideTargetKind.PresetSlot;
                    if (keys.Add(key)) goals.Add(new GuideGoal(key, GuideGoalKind.PresetIssue, target,
                        issue.Severity != LineupPresetIssueSeverity.Warning, issue.Code.ToString(),
                        issue.Group, issue.SlotIndex, issue.CardId, preset.PresetId));
                }
            }
            // 경기 일정과 점수만으로는 기용 변경의 근거가 되지 않는다.
            // 준비·결과는 홈의 경기 카드가 담당하고 매니저는 실제 검증 문제만 알린다.
            return goals;
        }
    }
}
