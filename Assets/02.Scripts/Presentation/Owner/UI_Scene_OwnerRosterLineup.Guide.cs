using System;
using Baseball.Core.Historical;
using Baseball.Game.Guide;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    public sealed partial class UI_Scene_OwnerRosterLineup
    {
        /// <summary>실제 편집 대상을 선택하지만 배정·교환 명령은 발행하지 않는다.</summary>
        public bool TrySelectGuideTarget(GuideGoal goal, out RectTransform target)
        {
            target = null;
            if (goal == null || _model == null || _workspaceRoot == null || !_workspaceRoot.gameObject.activeInHierarchy || _hasPreview)
                return false;
            if (goal.Target == GuideTargetKind.Roster)
            {
                // 1군 편집을 통합한 현재 배치 편집 진입점으로 안내한다.
                if (_placementEditButton == null || !_placementEditButton.gameObject.activeInHierarchy) return false;
                target = (RectTransform)_placementEditButton.transform;
                return true;
            }
            if (goal.Target == GuideTargetKind.Condition)
            {
                SetWorkspaceMode(OwnerRosterWorkspaceMode.Pitching);
                target = _primaryAssignedContent;
                return true;
            }
            if (!string.Equals(_model.Snapshot.Preset.PresetId, goal.PresetId, StringComparison.Ordinal)) return false;
            int index = goal.SlotIndex;
            OwnerLineupSwapGroup group;
            switch (goal.Group)
            {
                case LineupPresetAssignmentGroup.StarterRotation: group = OwnerLineupSwapGroup.StarterRotation; break;
                case LineupPresetAssignmentGroup.Bullpen: group = OwnerLineupSwapGroup.ReliefPitching; break;
                case LineupPresetAssignmentGroup.Setup:
                    group = OwnerLineupSwapGroup.ReliefPitching; index = ActiveRosterCompositionRule.BullpenPitcherCount; break;
                case LineupPresetAssignmentGroup.Closer:
                    group = OwnerLineupSwapGroup.ReliefPitching; index = ActiveRosterCompositionRule.BullpenPitcherCount + 1; break;
                case LineupPresetAssignmentGroup.Bench: group = OwnerLineupSwapGroup.Bench; break;
                case LineupPresetAssignmentGroup.StartingLineup:
                    group = OwnerLineupSwapGroup.BattingOrder;
                    index = -1;
                    for (int slot = 0; slot < _model.BattingOrder.Count; slot++)
                        if (!string.IsNullOrEmpty(goal.CardId) && _model.BattingOrder[slot].Player?.CardId == goal.CardId) index = slot;
                    break;
                default: group = OwnerLineupSwapGroup.BattingOrder; break;
            }
            SetWorkspaceMode(group == OwnerLineupSwapGroup.StarterRotation || group == OwnerLineupSwapGroup.ReliefPitching
                ? OwnerRosterWorkspaceMode.Pitching : OwnerRosterWorkspaceMode.Lineup);
            if (index < 0)
            {
                if (goal.SlotIndex >= 0) return false;
                target = _presetStateText.rectTransform;
                return true;
            }
            OwnerLineupSlotModel selected = FindCurrentSlot(group, index);
            if (selected == null || (!string.IsNullOrEmpty(goal.CardId) && selected.Player?.CardId != goal.CardId)) return false;
            if (!_isPlacementEditMode) TogglePlacementEditMode();
            ClearSelection();
            Button button = FindButton(group, index);
            if (button == null || !button.gameObject.activeInHierarchy) return false;
            SelectAssigned(button, selected);
            ShowPitchingDetail(group, index);
            target = (RectTransform)button.transform;
            return true;
        }
    }
}
