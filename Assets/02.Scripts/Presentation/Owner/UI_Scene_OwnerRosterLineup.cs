using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>25인 상태와 타자·투수 역할 슬롯을 한 화면에서 편집하는 구단주 uGUI Workspace다.</summary>
    [DisallowMultipleComponent]
    public sealed class UI_Scene_OwnerRosterLineup : MonoBehaviour
    {
        private readonly List<Button> _slotButtons = new List<Button>();
        private RectTransform _workspaceRoot;
        private RectTransform _inspectorRoot;
        private RectTransform _actionRoot;
        private RectTransform _defenseContent;
        private RectTransform _battingContent;
        private RectTransform _pitchingContent;
        private Text _summaryText;
        private Text _validationText;
        private Text _presetStateText;
        private Button _activeRosterEditButton;
        private Button _previousPresetButton;
        private Button _nextPresetButton;
        private Button[] _teamColorButtons;
        private Button[] _tacticButtons;
        private Button _selectedButton;
        private OwnerRosterLineupPresentationModel _model;
        private int _presetIndex;
        private OwnerLineupSwapGroup? _selectedGroup;
        private int _selectedIndex = -1;

        public event Action<OwnerLineupSwapGroup, int, int> SwapRequested;
        public event Action<string> PresetSelected;
        public event Action<int> TeamColorSlotCycleRequested;
        public event Action<int> TacticSlotCycleRequested;

        public static UI_Scene_OwnerRosterLineup CreateRuntime(
            RectTransform workspaceHost,
            RectTransform inspectorHost,
            RectTransform actionBarHost)
        {
            if (workspaceHost == null) throw new ArgumentNullException(nameof(workspaceHost));
            if (inspectorHost == null) throw new ArgumentNullException(nameof(inspectorHost));
            if (actionBarHost == null) throw new ArgumentNullException(nameof(actionBarHost));
            var view = new GameObject(nameof(UI_Scene_OwnerRosterLineup)).AddComponent<UI_Scene_OwnerRosterLineup>();
            view.Build(workspaceHost, inspectorHost, actionBarHost);
            return view;
        }

        public void Bind(OwnerRosterLineupPresentationModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            _model = model;
            EnsureBuilt();
            ClearSelection();
            _slotButtons.Clear();
            OwnerRuntimeUiFactory.ClearChildren(_defenseContent);
            OwnerRuntimeUiFactory.ClearChildren(_battingContent);
            OwnerRuntimeUiFactory.ClearChildren(_pitchingContent);
            _summaryText.text = model.RosterSummaryText;
            _validationText.text = model.ValidationText;
            _validationText.color = CareerUiTheme.TextPrimary;
            RenderColumn(_defenseContent, "수비 위치", model.DefensiveLineup);
            RenderColumn(_defenseContent, "벤치 우선순위", model.Bench);
            RenderColumn(_battingContent, "타순", model.BattingOrder);
            RenderColumn(_pitchingContent, "선발 로테이션", model.StarterRotation);
            RenderColumn(_pitchingContent, "불펜 역할", model.ReliefPitching);
            _presetIndex = FindSelectedPreset(model);
            RenderPresetControls();
        }

        /// <summary>현재 Route에서 발생한 Command 실패를 Home으로 보내지 않고 Inspector에 표시한다.</summary>
        public void SetFeedback(string message, bool isError)
        {
            EnsureBuilt();
            _validationText.text = string.IsNullOrWhiteSpace(message) ? "작업 결과가 없습니다." : message;
            _validationText.color = isError ? CareerUiTheme.Error : CareerUiTheme.Success;
        }

        public void SetVisible(bool visible)
        {
            if (_workspaceRoot != null) _workspaceRoot.gameObject.SetActive(visible);
            if (_inspectorRoot != null) _inspectorRoot.gameObject.SetActive(visible);
            if (_actionRoot != null) _actionRoot.gameObject.SetActive(visible);
        }

        private void OnDestroy()
        {
            for (int index = 0; index < _slotButtons.Count; index++)
                if (_slotButtons[index] != null) _slotButtons[index].onClick.RemoveAllListeners();
            if (_activeRosterEditButton != null) _activeRosterEditButton.onClick.RemoveAllListeners();
            if (_previousPresetButton != null) _previousPresetButton.onClick.RemoveAllListeners();
            if (_nextPresetButton != null) _nextPresetButton.onClick.RemoveAllListeners();
            RemoveListeners(_teamColorButtons);
            RemoveListeners(_tacticButtons);
            OwnerWorkspaceUiFactory.DestroyOwnedRoot(_workspaceRoot);
            OwnerWorkspaceUiFactory.DestroyOwnedRoot(_inspectorRoot);
            OwnerWorkspaceUiFactory.DestroyOwnedRoot(_actionRoot);
        }

        private void Build(RectTransform workspaceHost, RectTransform inspectorHost, RectTransform actionBarHost)
        {
            _workspaceRoot = OwnerWorkspaceUiFactory.CreateRoot(workspaceHost, "OwnerRosterLineupWorkspace", true);
            RectTransform columns = OwnerWorkspaceUiFactory.CreateRoot(_workspaceRoot, "LineupColumns", false);
            columns.offsetMin = new Vector2(CareerUiTheme.Space4, CareerUiTheme.Space4);
            columns.offsetMax = new Vector2(-CareerUiTheme.Space4, -CareerUiTheme.Space4);
            OwnerWorkspaceUiFactory.AddHorizontalLayout(columns, CareerUiTheme.Space3);
            _defenseContent = CreateColumn(columns, "HitterRolePanel", "타자 역할", 1.05f);
            _battingContent = CreateColumn(columns, "BattingOrderPanel", "타순", 0.9f);
            _pitchingContent = CreateColumn(columns, "PitcherRolePanel", "투수 역할", 1.05f);

            _inspectorRoot = OwnerWorkspaceUiFactory.CreateRoot(inspectorHost, "OwnerRosterLineupInspector", false);
            OwnerWorkspaceUiFactory.Panel validation = OwnerWorkspaceUiFactory.CreatePanel(
                _inspectorRoot, "ValidationPanel", "Resolver 검증");
            OwnerWorkspaceUiFactory.Stretch(validation.Root);
            OwnerWorkspaceUiFactory.AddVerticalLayout(validation.Content, CareerUiTheme.Space3);
            _summaryText = AddText(validation.Content, "RosterSummary", 16, FontStyle.Bold, 96f);
            _validationText = AddText(validation.Content, "ValidationMessages", 14, FontStyle.Normal, 410f);

            _actionRoot = OwnerWorkspaceUiFactory.CreateRoot(actionBarHost, "OwnerRosterLineupActionBar", false);
            HorizontalLayoutGroup actions = OwnerWorkspaceUiFactory.AddHorizontalLayout(_actionRoot, CareerUiTheme.Space3);
            actions.padding = new RectOffset(16, 16, 8, 8);
            _presetStateText = OwnerWorkspaceUiFactory.CreateText(
                _actionRoot, "PresetState", string.Empty,
                13, FontStyle.Normal, TextAnchor.MiddleLeft, CareerUiTheme.TextSecondary);
            OwnerWorkspaceUiFactory.SetFlexible(_presetStateText.rectTransform, 1f, 0f);
            _previousPresetButton = OwnerWorkspaceUiFactory.CreateButton(
                _actionRoot, "PreviousPresetButton", "◀ 프리셋", () => SelectRelativePreset(-1));
            _nextPresetButton = OwnerWorkspaceUiFactory.CreateButton(
                _actionRoot, "NextPresetButton", "프리셋 ▶", () => SelectRelativePreset(1));
            _teamColorButtons = new Button[LineupPresetState.TeamColorSlotCount];
            for (int index = 0; index < _teamColorButtons.Length; index++)
            {
                int slotIndex = index;
                _teamColorButtons[index] = OwnerWorkspaceUiFactory.CreateButton(
                    _actionRoot,
                    $"TeamColorSlot{index}",
                    string.Empty,
                    () => TeamColorSlotCycleRequested?.Invoke(slotIndex));
            }
            _tacticButtons = new Button[LineupPresetState.MaximumTacticCardCount];
            for (int index = 0; index < _tacticButtons.Length; index++)
            {
                int slotIndex = index;
                _tacticButtons[index] = OwnerWorkspaceUiFactory.CreateButton(
                    _actionRoot,
                    $"TacticSlot{index}",
                    string.Empty,
                    () => TacticSlotCycleRequested?.Invoke(slotIndex));
            }
            _activeRosterEditButton = OwnerWorkspaceUiFactory.CreateButton(
                _actionRoot, "ActiveRosterEditDisabled", "1군 등록 변경 미제공", null);
            _activeRosterEditButton.interactable = false;
            CareerUiSkin.Apply(_workspaceRoot);
            CareerUiSkin.Apply(_inspectorRoot);
            CareerUiSkin.Apply(_actionRoot);
        }

        private void SelectRelativePreset(int delta)
        {
            if (_model == null || _model.Presets.Count < 2) return;
            _presetIndex = (_presetIndex + delta + _model.Presets.Count) % _model.Presets.Count;
            PresetSelected?.Invoke(_model.Presets[_presetIndex].PresetId);
        }

        private void RenderPresetControls()
        {
            bool hasMultiplePresets = _model != null && _model.Presets.Count > 1;
            OwnerRosterPresetChoiceModel preset = _model.Presets[_presetIndex];
            _presetStateText.text = $"{preset.Name} · {preset.StatusText}";
            _previousPresetButton.interactable = hasMultiplePresets;
            _nextPresetButton.interactable = hasMultiplePresets;
            for (int index = 0; index < _teamColorButtons.Length; index++)
            {
                _teamColorButtons[index].GetComponentInChildren<Text>().text = _model.TeamColorSlotText(index);
                _teamColorButtons[index].interactable = _model.Snapshot.TeamColorCandidates.Count > 0;
            }
            for (int index = 0; index < _tacticButtons.Length; index++)
            {
                _tacticButtons[index].GetComponentInChildren<Text>().text = _model.TacticSlotText(index);
                _tacticButtons[index].interactable =
                    _model.Snapshot.TacticCandidates.Count > 0 &&
                    index <= _model.Snapshot.Preset.DefaultTacticCardIds.Count;
            }
        }

        private static int FindSelectedPreset(OwnerRosterLineupPresentationModel model)
        {
            for (int index = 0; index < model.Presets.Count; index++)
                if (model.Presets[index].IsSelected) return index;
            return 0;
        }

        private static void RemoveListeners(IReadOnlyList<Button> buttons)
        {
            if (buttons == null) return;
            for (int index = 0; index < buttons.Count; index++)
                if (buttons[index] != null) buttons[index].onClick.RemoveAllListeners();
        }

        private static RectTransform CreateColumn(Transform parent, string name, string title, float flexibleWidth)
        {
            OwnerWorkspaceUiFactory.Panel panel = OwnerWorkspaceUiFactory.CreatePanel(parent, name, title);
            OwnerWorkspaceUiFactory.SetFlexible(panel.Root, flexibleWidth);
            ScrollRect scroll = OwnerRuntimeUiFactory.CreateVerticalScroll("RoleScroll", panel.Content, out RectTransform content);
            OwnerRuntimeUiFactory.Stretch(scroll.GetComponent<RectTransform>());
            return content;
        }

        private void RenderColumn(
            RectTransform content,
            string sectionTitle,
            IReadOnlyList<OwnerLineupSlotModel> slots)
        {
            AddSectionTitle(content, sectionTitle);
            for (int index = 0; index < slots.Count; index++)
                CreateSlotButton(content, slots[index]);
        }

        private void CreateSlotButton(Transform parent, OwnerLineupSlotModel slot)
        {
            string warning = slot.HasWarning ? $"\n[경고] {slot.WarningText}" : string.Empty;
            Button button = OwnerWorkspaceUiFactory.CreateButton(
                parent,
                $"{slot.Group}_{slot.Index}",
                $"{slot.Label}  {slot.PlayerText}{warning}",
                () => HandleSlotSelected(slot.Group, slot.Index));
            LayoutElement layout = button.GetComponent<LayoutElement>();
            layout.minHeight = slot.HasWarning ? 54f : 38f;
            layout.preferredHeight = layout.minHeight;
            Text label = button.GetComponentInChildren<Text>();
            label.alignment = TextAnchor.MiddleLeft;
            label.fontSize = slot.HasWarning ? 12 : 13;
            label.rectTransform.offsetMin = new Vector2(10f, 2f);
            _slotButtons.Add(button);
        }

        private void HandleSlotSelected(OwnerLineupSwapGroup group, int index)
        {
            Button clicked = FindButton(group, index);
            if (!_selectedGroup.HasValue || _selectedGroup.Value != group)
            {
                Select(clicked, group, index);
                return;
            }
            if (_selectedIndex == index)
            {
                ClearSelection();
                return;
            }

            int first = _selectedIndex;
            ClearSelection();
            SwapRequested?.Invoke(group, first, index);
        }

        private void Select(Button button, OwnerLineupSwapGroup group, int index)
        {
            ClearSelection();
            _selectedButton = button;
            _selectedGroup = group;
            _selectedIndex = index;
            if (_selectedButton != null) _selectedButton.image.color = CareerUiTheme.PrimaryAction;
        }

        private void ClearSelection()
        {
            if (_selectedButton != null) _selectedButton.image.color = CareerUiTheme.SecondaryAction;
            _selectedButton = null;
            _selectedGroup = null;
            _selectedIndex = -1;
        }

        private Button FindButton(OwnerLineupSwapGroup group, int index)
        {
            string name = $"{group}_{index}";
            for (int buttonIndex = 0; buttonIndex < _slotButtons.Count; buttonIndex++)
                if (_slotButtons[buttonIndex] != null && _slotButtons[buttonIndex].name == name)
                    return _slotButtons[buttonIndex];
            return null;
        }

        private static void AddSectionTitle(Transform parent, string title)
        {
            Text text = OwnerWorkspaceUiFactory.CreateText(parent, "SectionTitle", title, 14, FontStyle.Bold,
                TextAnchor.MiddleLeft, CareerUiTheme.AccentGold);
            text.gameObject.AddComponent<LayoutElement>().preferredHeight = 22f;
        }

        private static Text AddText(Transform parent, string name, int size, FontStyle style, float height)
        {
            Text text = OwnerWorkspaceUiFactory.CreateText(parent, name, string.Empty, size, style,
                TextAnchor.UpperLeft, CareerUiTheme.TextPrimary);
            text.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            return text;
        }

        private void EnsureBuilt()
        {
            if (_workspaceRoot == null) throw new InvalidOperationException("CreateRuntime으로 View를 생성해야 합니다.");
        }
    }
}
