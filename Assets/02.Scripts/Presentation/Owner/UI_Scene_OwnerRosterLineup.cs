using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>같은 프리셋을 타자 Lineup과 투수 역할 화면에서 서로 독립된 View State로 연다.</summary>
    public enum OwnerRosterWorkspaceMode
    {
        Lineup,
        Pitching
    }

    /// <summary>25인 상태와 타자·투수 역할 슬롯을 한 화면에서 편집하는 구단주 uGUI Workspace다.</summary>
    [DisallowMultipleComponent]
    public sealed partial class UI_Scene_OwnerRosterLineup : MonoBehaviour
    {
        private enum PlayerGroupTab
        {
            Hitter,
            Pitcher
        }

        private static readonly Color RoleBoardSurface = new Color(0.92f, 0.93f, 0.93f, 1f);
        private static readonly Color RoleBoardBorder = new Color(0.31f, 0.38f, 0.44f, 1f);
        private static readonly Color InspectorMessage = new Color(0.94f, 0.95f, 0.93f, 1f);

        private readonly List<Button> _slotButtons = new List<Button>();
        private readonly List<GridLayoutGroup> _responsiveGrids = new List<GridLayoutGroup>();
        private readonly Dictionary<Button, PlayerMiniCardView> _slotCards =
            new Dictionary<Button, PlayerMiniCardView>();
        private RectTransform _workspaceRoot;
        private RectTransform _inspectorRoot;
        private RectTransform _actionRoot;
        private RectTransform _primaryAssignedContent;
        private RectTransform _secondaryAssignedContent;
        private RectTransform _ownedContent;
        private RectTransform _analysisContent;
        private RectTransform _setupContent;
        private RectTransform _closerContent;
        private Text _summaryText;
        private Text _evaluationText;
        private Text _validationText;
        private Text _presetStateText;
        private Text _previewStateText;
        private Button _activeRosterEditButton;
        private Button _confirmPreviewButton;
        private Button _cancelPreviewButton;
        private Button _previousPresetButton;
        private Button _nextPresetButton;
        private Button _hitterTabButton;
        private Button _pitcherTabButton;
        private Button[] _teamColorButtons;
        private Button[] _tacticButtons;
        private Button _selectedButton;
        private OwnerRosterLineupPresentationModel _model;
        private OwnerRosterPitchingPresentationModel _pitchingModel;
        private int _presetIndex;
        private PlayerGroupTab _activePlayerGroup = PlayerGroupTab.Hitter;
        private OwnerRosterWorkspaceMode _workspaceMode = OwnerRosterWorkspaceMode.Lineup;
        private OwnerLineupSwapGroup? _selectedGroup;
        private int _selectedIndex = -1;
        private int _positionFilter;

        public event Action<OwnerLineupSwapGroup, int, int> SwapRequested;
        public event Action<string> PresetSelected;
        public event Action<int> TeamColorSlotCycleRequested;
        public event Action<int> TacticSlotCycleRequested;
        public event Action LineupChangeConfirmed;
        public event Action LineupChangeCancelled;

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
            _pitchingModel = OwnerRosterPitchingPresentationBuilder.Build(model);
            EnsureBuilt();
            ClearSelection();
            _summaryText.text = model.RosterSummaryText;
            _evaluationText.text = model.EvaluationText + "\n" + model.EvaluationBasisText;
            _validationText.text = string.Empty;
            _validationText.color = InspectorMessage;
            RenderActivePlayerGroup();
            _presetIndex = FindSelectedPreset(model);
            RenderPresetControls();
            SetPreviewState("슬롯 두 개를 선택하면 변경안을 먼저 검증합니다.", false, false);
        }

        /// <summary>Navigation Route마다 타자·투수 선택과 필터를 독립적으로 유지한다.</summary>
        public void SetWorkspaceMode(OwnerRosterWorkspaceMode mode)
        {
            PlayerGroupTab target = mode == OwnerRosterWorkspaceMode.Pitching
                ? PlayerGroupTab.Pitcher
                : PlayerGroupTab.Hitter;
            if (_workspaceMode == mode && _activePlayerGroup == target)
            {
                if (_hitterTabButton != null) _hitterTabButton.transform.parent.gameObject.SetActive(false);
                return;
            }
            _workspaceMode = mode;
            _activePlayerGroup = target;
            _positionFilter = 0;
            if (_hitterTabButton != null) _hitterTabButton.transform.parent.gameObject.SetActive(false);
            RenderActivePlayerGroup();
        }

        /// <summary>저장 전 후보 배치를 화면에 표시하고 Validator가 허용한 경우에만 확정 CTA를 연다.</summary>
        public void BindPreview(
            OwnerRosterLineupPresentationModel preview,
            string message,
            bool canConfirm)
        {
            if (preview == null) throw new ArgumentNullException(nameof(preview));
            _model = preview;
            _pitchingModel = OwnerRosterPitchingPresentationBuilder.Build(preview);
            _summaryText.text = preview.RosterSummaryText;
            _evaluationText.text = preview.EvaluationText + "\n" + preview.EvaluationBasisText;
            RenderActivePlayerGroup();
            SetPreviewState(message, canConfirm, true);
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
            if (_hitterTabButton != null) _hitterTabButton.onClick.RemoveAllListeners();
            if (_pitcherTabButton != null) _pitcherTabButton.onClick.RemoveAllListeners();
            if (_confirmPreviewButton != null) _confirmPreviewButton.onClick.RemoveAllListeners();
            if (_cancelPreviewButton != null) _cancelPreviewButton.onClick.RemoveAllListeners();
            RemoveListeners(_teamColorButtons);
            RemoveListeners(_tacticButtons);
            OwnerWorkspaceUiFactory.DestroyOwnedRoot(_workspaceRoot);
            OwnerWorkspaceUiFactory.DestroyOwnedRoot(_inspectorRoot);
            OwnerWorkspaceUiFactory.DestroyOwnedRoot(_actionRoot);
        }

        private void Build(RectTransform workspaceHost, RectTransform inspectorHost, RectTransform actionBarHost)
        {
            _workspaceRoot = OwnerWorkspaceUiFactory.CreateRoot(workspaceHost, "OwnerRosterLineupWorkspace", false);
            RectTransform board = OwnerWorkspaceUiFactory.CreateRoot(_workspaceRoot, "PlayerOrderBoard", false);
            board.offsetMin = new Vector2(CareerUiTheme.Space4, CareerUiTheme.Space4);
            board.offsetMax = new Vector2(-CareerUiTheme.Space4, -CareerUiTheme.Space4);

            RectTransform tabs = OwnerWorkspaceUiFactory.CreateRoot(board, "PlayerGroupTabs", false);
            OwnerRuntimeUiFactory.SetAnchors(
                tabs, new Vector2(0f, 0.94f), Vector2.one, Vector2.zero, Vector2.zero);
            HorizontalLayoutGroup tabLayout = OwnerWorkspaceUiFactory.AddHorizontalLayout(tabs, CareerUiTheme.Space1);
            tabLayout.childForceExpandWidth = false;
            _hitterTabButton = CreatePlayerGroupTab(tabs, "HitterTab", "타자", PlayerGroupTab.Hitter);
            _pitcherTabButton = CreatePlayerGroupTab(tabs, "PitcherTab", "투수", PlayerGroupTab.Pitcher);

            _primaryAssignedContent = CreateColumn(
                board, "PrimaryAssignedPanel", "선발", out RectTransform primaryAssignedPanel);
            _secondaryAssignedContent = CreateColumn(
                board, "SecondaryAssignedPanel", "벤치", out RectTransform secondaryAssignedPanel);
            _ownedContent = CreateColumn(board, "OwnedPlayerPanel", "보유 선수", out RectTransform ownedPanel);
            _analysisContent = CreateColumn(
                board, "ConditionAnalysisPanel", "컨디션 분석", out RectTransform analysisPanel);
            _setupContent = CreateColumn(board, "SetupPanel", "셋업", out RectTransform setupPanel);
            _closerContent = CreateColumn(board, "CloserPanel", "마무리", out RectTransform closerPanel);
            SetUpperPanel(setupPanel, 0.79f, 0.89f);
            SetUpperPanel(closerPanel, 0.90f, 1f);
            OwnerRuntimeUiFactory.SetAnchors(
                primaryAssignedPanel, new Vector2(0f, 0.56f), new Vector2(0.66f, 0.93f), Vector2.zero, Vector2.zero);
            OwnerRuntimeUiFactory.SetAnchors(
                secondaryAssignedPanel, new Vector2(0.67f, 0.56f), new Vector2(1f, 0.93f), Vector2.zero, Vector2.zero);
            OwnerRuntimeUiFactory.SetAnchors(
                ownedPanel, Vector2.zero, new Vector2(0.66f, 0.55f), Vector2.zero, Vector2.zero);
            OwnerRuntimeUiFactory.SetAnchors(
                analysisPanel, new Vector2(0.67f, 0f), new Vector2(1f, 0.55f), Vector2.zero, Vector2.zero);

            _inspectorRoot = OwnerWorkspaceUiFactory.CreateRoot(inspectorHost, "OwnerRosterLineupInspector", false);
            OwnerWorkspaceUiFactory.Panel validation = OwnerWorkspaceUiFactory.CreatePanel(
                _inspectorRoot, "ValidationPanel", "선수단 검증");
            OwnerWorkspaceUiFactory.Stretch(validation.Root);
            OwnerWorkspaceUiFactory.AddVerticalLayout(validation.Content, CareerUiTheme.Space3);
            _summaryText = AddText(validation.Content, "RosterSummary", 15, FontStyle.Bold, 54f);
            _evaluationText = AddText(validation.Content, "RosterEvaluation", 13, FontStyle.Normal, 112f);
            _evaluationText.GetComponent<LayoutElement>().minHeight = 112f;
            _validationText = AddText(validation.Content, "ValidationMessages", 13, FontStyle.Normal, 160f);
            _presetStateText = OwnerWorkspaceUiFactory.CreateText(
                validation.Content, "PresetState", string.Empty,
                13, FontStyle.Bold, TextAnchor.MiddleLeft, CareerUiTheme.TextSecondary);
            _presetStateText.gameObject.AddComponent<LayoutElement>().preferredHeight = 26f;
            RectTransform presetControls = CreateInspectorControlRow(validation.Content, "PresetControls");
            RectTransform teamColorControls = CreateInspectorControlRow(validation.Content, "TeamColorControls");
            RectTransform tacticControls = CreateInspectorControlRow(validation.Content, "TacticControls");
            RectTransform rosterControls = CreateInspectorControlRow(validation.Content, "RosterControls");

            _actionRoot = OwnerWorkspaceUiFactory.CreateRoot(actionBarHost, "OwnerRosterLineupActionBar", false);
            HorizontalLayoutGroup actions = OwnerWorkspaceUiFactory.AddHorizontalLayout(_actionRoot, CareerUiTheme.Space3);
            actions.padding = new RectOffset(16, 16, 4, 4);
            _previewStateText = OwnerWorkspaceUiFactory.CreateText(
                _actionRoot, "PreviewState", string.Empty,
                13, FontStyle.Normal, TextAnchor.MiddleLeft, CareerUiTheme.TextSecondary);
            OwnerWorkspaceUiFactory.SetFlexible(_previewStateText.rectTransform, 1f, 0f);
            _previousPresetButton = OwnerWorkspaceUiFactory.CreateButton(
                presetControls, "PreviousPresetButton", "◀ 이전", () => SelectRelativePreset(-1));
            _nextPresetButton = OwnerWorkspaceUiFactory.CreateButton(
                presetControls, "NextPresetButton", "다음 ▶", () => SelectRelativePreset(1));
            _teamColorButtons = new Button[LineupPresetState.TeamColorSlotCount];
            for (int index = 0; index < _teamColorButtons.Length; index++)
            {
                int slotIndex = index;
                _teamColorButtons[index] = OwnerWorkspaceUiFactory.CreateButton(
                    teamColorControls,
                    $"TeamColorSlot{index}",
                    string.Empty,
                    () => TeamColorSlotCycleRequested?.Invoke(slotIndex));
            }
            _tacticButtons = new Button[LineupPresetState.MaximumTacticCardCount];
            for (int index = 0; index < _tacticButtons.Length; index++)
            {
                int slotIndex = index;
                _tacticButtons[index] = OwnerWorkspaceUiFactory.CreateButton(
                    tacticControls,
                    $"TacticSlot{index}",
                    string.Empty,
                    () => TacticSlotCycleRequested?.Invoke(slotIndex));
                LayoutElement tacticLayout = _tacticButtons[index].GetComponent<LayoutElement>();
                tacticLayout.minWidth = 0f;
                tacticLayout.preferredWidth = 120f;
                RawImage artwork = TacticCardArtwork.Create(
                    _tacticButtons[index].transform,
                    "TacticArtwork",
                    TacticCardArtwork.CommonKey,
                    new Color(1f, 1f, 1f, 0.32f));
                OwnerRuntimeUiFactory.Stretch(artwork.rectTransform);
                artwork.uvRect = new Rect(0f, 0.34f, 1f, 0.28f);
                artwork.transform.SetAsFirstSibling();
            }
            _activeRosterEditButton = OwnerWorkspaceUiFactory.CreateButton(
                rosterControls, "ActiveRosterEditDisabled", "1군 등록 변경 미제공", null);
            _activeRosterEditButton.interactable = false;
            _cancelPreviewButton = OwnerWorkspaceUiFactory.CreateButton(
                _actionRoot, "CancelLineupPreview", "변경 취소", () => LineupChangeCancelled?.Invoke());
            _confirmPreviewButton = OwnerWorkspaceUiFactory.CreateButton(
                _actionRoot, "ConfirmLineupPreview", "검증된 배치 저장", () => LineupChangeConfirmed?.Invoke());
            CareerUiSkin.Apply(_workspaceRoot);
            CareerUiSkin.Apply(_inspectorRoot);
            CareerUiSkin.Apply(_actionRoot);
            ApplyRoleBoardPalette(_workspaceRoot.Find("PlayerOrderBoard/PrimaryAssignedPanel"));
            ApplyRoleBoardPalette(_workspaceRoot.Find("PlayerOrderBoard/SecondaryAssignedPanel"));
            ApplyRoleBoardPalette(_workspaceRoot.Find("PlayerOrderBoard/OwnedPlayerPanel"));
            ApplyRoleBoardPalette(_workspaceRoot.Find("PlayerOrderBoard/ConditionAnalysisPanel"));
            ApplyRoleBoardPalette(setupPanel);
            ApplyRoleBoardPalette(closerPanel);
            ApplyInspectorPalette(_inspectorRoot.Find("ValidationPanel"));
            UpdatePlayerGroupTabs();
            tabs.gameObject.SetActive(false);
            foreach (Transform panel in board)
                if (panel.name.EndsWith("Panel", StringComparison.Ordinal)) CompactPanel((RectTransform)panel);
            OwnerRuntimeUiFactory.SetAnchors(ownedPanel, Vector2.zero, new Vector2(0.64f, 0.69f), Vector2.zero, Vector2.zero);
            OwnerRuntimeUiFactory.SetAnchors(analysisPanel, new Vector2(0.65f, 0f), new Vector2(1f, 0.69f), Vector2.zero, Vector2.zero);
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
                BindTacticArtwork(_tacticButtons[index], index);
                _tacticButtons[index].interactable =
                    _model.Snapshot.TacticCandidates.Count > 0 &&
                    index <= _model.Snapshot.Preset.DefaultTacticCardIds.Count;
            }
        }

        private void BindTacticArtwork(Button button, int slotIndex)
        {
            RawImage artwork = button.transform.Find("TacticArtwork")?.GetComponent<RawImage>();
            if (artwork == null) return;
            string selectedId = slotIndex < _model.Snapshot.Preset.DefaultTacticCardIds.Count
                ? _model.Snapshot.Preset.DefaultTacticCardIds[slotIndex]
                : string.Empty;
            OwnerLoadoutCandidateSnapshot selected = null;
            for (int index = 0; index < _model.Snapshot.TacticCandidates.Count; index++)
            {
                OwnerLoadoutCandidateSnapshot candidate = _model.Snapshot.TacticCandidates[index];
                if (!string.Equals(candidate.Id, selectedId, StringComparison.Ordinal)) continue;
                selected = candidate;
                break;
            }
            artwork.texture = selected == null ? null : TacticCardArtwork.Load(selected.ArtworkKey);
            artwork.gameObject.SetActive(artwork.texture != null);
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

        private static RectTransform CreateColumn(
            Transform parent,
            string name,
            string title,
            out RectTransform panelRoot)
        {
            OwnerWorkspaceUiFactory.Panel panel = OwnerWorkspaceUiFactory.CreatePanel(parent, name, title);
            panelRoot = panel.Root;
            ScrollRect scroll = OwnerRuntimeUiFactory.CreateVerticalScroll("RoleScroll", panel.Content, out RectTransform content);
            OwnerRuntimeUiFactory.Stretch(scroll.GetComponent<RectTransform>());
            return content;
        }

        private static RectTransform CreateInspectorControlRow(Transform parent, string name)
        {
            RectTransform row = OwnerRuntimeUiFactory.CreateRect(name, parent);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 38f;
            HorizontalLayoutGroup layout = OwnerWorkspaceUiFactory.AddHorizontalLayout(row, CareerUiTheme.Space1);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            return row;
        }

        private Button CreatePlayerGroupTab(
            Transform parent,
            string name,
            string label,
            PlayerGroupTab playerGroup)
        {
            Button button = OwnerWorkspaceUiFactory.CreateButton(
                parent, name, label, () => HandlePlayerGroupSelected(playerGroup));
            LayoutElement layout = button.GetComponent<LayoutElement>();
            layout.minWidth = 112f;
            layout.preferredWidth = 132f;
            layout.flexibleWidth = 0f;
            return button;
        }

        private void HandlePlayerGroupSelected(PlayerGroupTab playerGroup)
        {
            if (_activePlayerGroup == playerGroup) return;
            _activePlayerGroup = playerGroup;
            _positionFilter = 0;
            RenderActivePlayerGroup();
        }

        private void RenderActivePlayerGroup()
        {
            if (_model == null || _primaryAssignedContent == null || _secondaryAssignedContent == null ||
                _ownedContent == null || _analysisContent == null) return;
            ClearSelection();
            _slotButtons.Clear();
            _slotCards.Clear();
            _responsiveGrids.Clear();
            OwnerRuntimeUiFactory.ClearChildren(_primaryAssignedContent);
            OwnerRuntimeUiFactory.ClearChildren(_secondaryAssignedContent);
            OwnerRuntimeUiFactory.ClearChildren(_ownedContent);
            OwnerRuntimeUiFactory.ClearChildren(_analysisContent);
            OwnerRuntimeUiFactory.ClearChildren(_setupContent);
            OwnerRuntimeUiFactory.ClearChildren(_closerContent);
            bool isPitcher = _activePlayerGroup == PlayerGroupTab.Pitcher;
            Transform board = _workspaceRoot.Find("PlayerOrderBoard");
            board.Find("SetupPanel").gameObject.SetActive(isPitcher);
            board.Find("CloserPanel").gameObject.SetActive(isPitcher);
            SetUpperPanel((RectTransform)board.Find("PrimaryAssignedPanel"), 0f, isPitcher ? 0.43f : 0.64f);
            SetUpperPanel((RectTransform)board.Find("SecondaryAssignedPanel"), isPitcher ? 0.44f : 0.65f, isPitcher ? 0.78f : 1f);
            board.Find("SecondaryAssignedPanel/HeaderSlot").GetComponent<Text>().text = isPitcher ? "불펜" : "벤치";

            if (_activePlayerGroup == PlayerGroupTab.Hitter)
            {
                RenderSlotGroup(_primaryAssignedContent, "선발 타순 9명", _model.BattingOrder, 9);
                RenderSlotGroup(_secondaryAssignedContent, "벤치 5명", _model.Bench, 5);
                RenderOwnedPlayers(_ownedContent, isPitcher: false, 9);
                RenderRosterChart(_analysisContent, _model.BattingOrder, false);
            }
            else
            {
                RenderSlotGroup(_primaryAssignedContent, "선발 로테이션 5명", _model.StarterRotation, 5);
                RenderSlotGroup(_secondaryAssignedContent, "불펜 4명", SliceSlots(_model.ReliefPitching, 0, 4), 4);
                RenderSlotGroup(_setupContent, "1명", SliceSlots(_model.ReliefPitching, 4, 1), 1);
                RenderSlotGroup(_closerContent, "1명", SliceSlots(_model.ReliefPitching, 5, 1), 1);
                RenderOwnedPlayers(_ownedContent, isPitcher: true, 9);
                var pitchers = new List<OwnerLineupSlotModel>(_model.StarterRotation);
                pitchers.AddRange(_model.ReliefPitching);
                RenderRosterChart(_analysisContent, pitchers, true);
            }
            UpdatePlayerGroupTabs();
        }

        private void SetPreviewState(string message, bool canConfirm, bool hasPreview)
        {
            if (_previewStateText == null) return;
            _previewStateText.text = message ?? string.Empty;
            _previewStateText.color = hasPreview && !canConfirm
                ? CareerUiTheme.Warning
                : CareerUiTheme.TextSecondary;
            _confirmPreviewButton.interactable = canConfirm;
            _cancelPreviewButton.interactable = hasPreview;
        }

        private static void SetUpperPanel(RectTransform panel, float left, float right)
        {
            OwnerRuntimeUiFactory.SetAnchors(panel, new Vector2(left, 0.70f),
                new Vector2(right, 0.99f), Vector2.zero, Vector2.zero);
        }

        private static OwnerLineupSlotModel[] SliceSlots(IReadOnlyList<OwnerLineupSlotModel> slots, int start, int count)
        {
            var result = new OwnerLineupSlotModel[Math.Min(count, Math.Max(0, slots.Count - start))];
            for (int index = 0; index < result.Length; index++) result[index] = slots[start + index];
            return result;
        }

        private void RenderSlotGroup(
            RectTransform content,
            string title,
            IReadOnlyList<OwnerLineupSlotModel> slots,
            int columnCount)
        {
            RectTransform gridRoot = CreateCardGrid(content, "AssignedGrid", slots.Count, columnCount);
            for (int index = 0; index < slots.Count; index++)
                CreateSlotButton(gridRoot, slots[index]);
        }

        private void RenderOwnedPlayers(RectTransform content, bool isPitcher, int columnCount)
        {
            RenderPositionFilters(content, isPitcher);
            int cardCount = 0;
            for (int index = 0; index < _model.Snapshot.OwnedPlayers.Count; index++)
                if (MatchesFilter(_model.Snapshot.OwnedPlayers[index], isPitcher)) cardCount++;

            AddSectionTitle(content, $"{(isPitcher ? "보유 투수" : "보유 야수")} {cardCount}장");
            int slotCount = Math.Max(columnCount * 2, ((cardCount + columnCount - 1) / columnCount) * columnCount);
            RectTransform gridRoot = CreateCardGrid(content, "OwnedGrid", slotCount, columnCount);
            for (int index = 0; index < _model.Snapshot.OwnedPlayers.Count; index++)
            {
                OwnerCollectionCardSnapshot player = _model.Snapshot.OwnedPlayers[index];
                if (!MatchesFilter(player, isPitcher)) continue;
                CreateOwnedPlayerCard(gridRoot, player, index);
            }
            for (int index = cardCount; index < slotCount; index++)
            {
                RectTransform empty = CreateAnalysisSurface(gridRoot, "EmptySlot", CareerUiTheme.RosterEmptySlot);
                var outline = empty.gameObject.AddComponent<Outline>();
                outline.effectColor = CareerUiTheme.RosterBorder;
                outline.effectDistance = Vector2.one;
            }
        }


        private static RectTransform CreateAnalysisSurface(Transform parent, string name, Color color)
        {
            RectTransform rect = OwnerRuntimeUiFactory.CreateRect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private static Text CreateAnalysisText(
            Transform parent,
            string name,
            string value,
            int fontSize,
            FontStyle style,
            TextAnchor alignment)
        {
            Text text = OwnerWorkspaceUiFactory.CreateText(
                parent, name, value, fontSize, style, alignment, CareerUiTheme.ReferenceText);
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 6;
            text.resizeTextMaxSize = fontSize;
            return text;
        }


        private RectTransform CreateCardGrid(
            Transform content,
            string name,
            int cardCount,
            int columnCount)
        {
            RectTransform gridRoot = OwnerRuntimeUiFactory.CreateRect(name, content);
            var grid = gridRoot.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(PlayerMiniCardView.LineupSlotWidth, PlayerMiniCardView.LineupSlotHeight);
            grid.spacing = new Vector2(6f, 6f);
            grid.padding = new RectOffset(2, 2, 2, 2);
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columnCount;
            _responsiveGrids.Add(grid);
            int rowCount = Mathf.Max(1, Mathf.CeilToInt(cardCount / (float)columnCount));
            var gridLayout = gridRoot.gameObject.AddComponent<LayoutElement>();
            gridLayout.minHeight = rowCount * PlayerMiniCardView.LineupSlotHeight +
                                   Mathf.Max(0, rowCount - 1) * grid.spacing.y + 4f;
            gridLayout.preferredHeight = gridLayout.minHeight;
            return gridRoot;
        }

        private void LateUpdate()
        {
            // 실제 Canvas 폭을 기준으로 계산해 좁은 셋업·마무리 구역에서도 카드가 잘리지 않게 한다.
            foreach (GridLayoutGroup grid in _responsiveGrids)
            {
                if (grid == null) continue;
                float available = ((RectTransform)grid.transform).rect.width - grid.padding.horizontal -
                                  grid.spacing.x * (grid.constraintCount - 1);
                float width = Mathf.Max(1f, available / grid.constraintCount);
                if (!Mathf.Approximately(grid.cellSize.x, width))
                    grid.cellSize = new Vector2(width, PlayerMiniCardView.LineupSlotHeight);
            }
        }

        private void CreateSlotButton(Transform parent, OwnerLineupSlotModel slot)
        {
            OwnerRosterPlayerSnapshot player = slot.Player;
            string playerId = player?.CardId ?? $"empty:{slot.Group}:{slot.Index}";
            string displayName = player?.DisplayName ?? "미지정";
            string year = player == null ? string.Empty : FormatCompactYear(player.OriginYear);
            string cost = player == null ? string.Empty : $"비용 {player.Cost}";
            string edition = player == null ? string.Empty : FormatPositionName(player.NaturalPosition);
            string status = slot.Group == OwnerLineupSwapGroup.BattingOrder
                ? FindAssignedPosition(player) : slot.Group == OwnerLineupSwapGroup.Bench ? "벤치" :
                slot.Group == OwnerLineupSwapGroup.StarterRotation ? "선발" : slot.Index < 4 ? "불펜" : slot.Index == 4 ? "셋업" : "마무리";
            var cardModel = new PlayerMiniCardModel(
                playerId,
                displayName,
                FormatCompactRole(slot.Label),
                year,
                cost,
                edition,
                status,
                visualState: PlayerMiniCardVisualState.Normal);
            PlayerMiniCardView card = PlayerMiniCardView.CreateRuntime(
                parent, $"{slot.Group}_{slot.Index}");
            card.UseLineupSlotLayout();
            card.Bind(cardModel, GetRosterPortrait());
            card.Selected += _ => HandleSlotSelected(slot.Group, slot.Index);
            card.DetailRequested += ShowCardDetail;

            Button button = card.GetComponent<Button>();
            _slotButtons.Add(button);
            _slotCards[button] = card;
        }

        private void CreateOwnedPlayerCard(
            Transform parent,
            OwnerCollectionCardSnapshot player,
            int sourceIndex)
        {
            var cardModel = new PlayerMiniCardModel(
                player.CardId,
                player.DisplayName,
                FormatPositionName(player.Position),
                FormatCompactYear(player.OriginYear),
                $"비용 {player.Cost}",
                FormatPositionName(player.Position),
                OwnerRosterLineupPresentationBuilder.FormatEdition(player.Edition));
            PlayerMiniCardView card = PlayerMiniCardView.CreateRuntime(parent, $"Owned_{sourceIndex}");
            card.UseLineupSlotLayout();
            card.Bind(cardModel, GetRosterPortrait());
            card.SetAssignmentBadge(FindOwnedCardAssignment(player.CardId));
            card.DetailRequested += ShowCardDetail;
        }

        private string FindOwnedCardAssignment(string cardId)
        {
            // 이름이나 본래 포지션이 아닌 현재 프리셋의 카드 ID로 배치 여부를 판정한다.
            return FindAssignedSlotLabel(_model.BattingOrder, cardId)
                ?? FindAssignedSlotLabel(_model.Bench, cardId)
                ?? FindAssignedSlotLabel(_model.StarterRotation, cardId)
                ?? FindAssignedSlotLabel(_model.ReliefPitching, cardId);
        }

        private static string FindAssignedSlotLabel(IReadOnlyList<OwnerLineupSlotModel> slots, string cardId)
        {
            foreach (OwnerLineupSlotModel slot in slots)
            {
                if (slot.Player == null || slot.Player.CardId != cardId) continue;
                if (slot.Group == OwnerLineupSwapGroup.Bench) return $"벤치 {slot.Index + 1}";
                if (slot.Group == OwnerLineupSwapGroup.StarterRotation) return $"선발 {slot.Index + 1}";
                return slot.Label;
            }
            return null;
        }

        private void ShowCardDetail(PlayerMiniCardModel selected)
        {
            foreach (var card in _model.Snapshot.OwnedPlayers)
                if (card.CardId == selected.PlayerId)
                {
                    UI_Popup_OwnerPlayerCard.Show(_workspaceRoot, card);
                    return;
                }
        }

        private static bool IsPitcher(OwnerCollectionCardSnapshot player)
        {
            return player.Position == PlayerPosition.StartingPitcher ||
                   player.Position == PlayerPosition.ReliefPitcher;
        }

        private string FindAssignedPosition(OwnerRosterPlayerSnapshot player)
        {
            if (player == null) return "미지정";
            foreach (var slot in _model.Snapshot.Preset.StartingLineupSlots)
                if (slot.CardId == player.CardId) return FormatPositionName(slot.Position);
            return FormatPositionName(player.NaturalPosition);
        }

        private static string FormatCompactYear(int year)
        {
            return Mathf.Abs(year % 100).ToString("00");
        }

        private static string FormatCompactRole(string role)
        {
            if (string.IsNullOrEmpty(role)) return string.Empty;
            if (role.EndsWith("선발", StringComparison.Ordinal))
                return "선" + role.Substring(0, role.Length - 2);
            if (role.StartsWith("불펜 ", StringComparison.Ordinal))
                return "불" + role.Substring(3);
            if (role.EndsWith("순위", StringComparison.Ordinal))
                return "벤" + role.Substring(0, role.Length - 2);
            if (string.Equals(role, "셋업", StringComparison.Ordinal)) return "셋";
            if (string.Equals(role, "마무리", StringComparison.Ordinal)) return "마";
            return role;
        }

        private static string FormatPositionName(PlayerPosition position)
        {
            return position switch
            {
                PlayerPosition.Catcher => "포수",
                PlayerPosition.FirstBase => "1루수",
                PlayerPosition.SecondBase => "2루수",
                PlayerPosition.ThirdBase => "3루수",
                PlayerPosition.Shortstop => "유격수",
                PlayerPosition.LeftField => "좌익수",
                PlayerPosition.CenterField => "중견수",
                PlayerPosition.RightField => "우익수",
                PlayerPosition.DesignatedHitter => "지명타자",
                PlayerPosition.StartingPitcher => "선발",
                PlayerPosition.ReliefPitcher => "불펜",
                _ => "미정"
            };
        }

        private void UpdatePlayerGroupTabs()
        {
            SetPlayerGroupTabVisual(_hitterTabButton, _activePlayerGroup == PlayerGroupTab.Hitter);
            SetPlayerGroupTabVisual(_pitcherTabButton, _activePlayerGroup == PlayerGroupTab.Pitcher);
        }

        private static void SetPlayerGroupTabVisual(Button button, bool isSelected)
        {
            if (button == null) return;
            if (button.GetComponent<CareerUiPreserveTextColor>() == null)
                button.gameObject.AddComponent<CareerUiPreserveTextColor>();
            button.image.color = isSelected
                ? CareerUiTheme.ReferenceAccent
                : CareerUiTheme.RosterEmptySlot;
            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
                label.color = isSelected ? Color.white : CareerUiTheme.ReferenceText;
        }

        private void HandleSlotSelected(OwnerLineupSwapGroup group, int index)
        {
            Button clicked = FindButton(group, index);
            if (!_selectedGroup.HasValue || _selectedGroup.Value != group)
            {
                Select(clicked, group, index);
                ShowPitchingDetail(group, index);
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

        private void ShowPitchingDetail(OwnerLineupSwapGroup group, int index)
        {
            if (_workspaceMode != OwnerRosterWorkspaceMode.Pitching || _pitchingModel == null) return;
            IReadOnlyList<OwnerLineupSlotModel> slots = group == OwnerLineupSwapGroup.StarterRotation
                ? _model.StarterRotation
                : group == OwnerLineupSwapGroup.ReliefPitching ? _model.ReliefPitching : null;
            if (slots == null || index < 0 || index >= slots.Count || slots[index].Player == null) return;
            OwnerPitchingPlayerPresentationModel pitcher = _pitchingModel.Find(slots[index].Player.CardId);
            if (pitcher == null) return;
            _validationText.text =
                $"{pitcher.Slot.Player.DisplayName} · {pitcher.Slot.Label}\n\n" +
                $"{pitcher.ConditionText}\n{pitcher.WorkloadText}\n\n" +
                $"{pitcher.PitchesText}\n\n{pitcher.RecentRecordText}";
            _validationText.color = InspectorMessage;
        }

        private void Select(Button button, OwnerLineupSwapGroup group, int index)
        {
            ClearSelection();
            _selectedButton = button;
            _selectedGroup = group;
            _selectedIndex = index;
            SetSelectionVisual(_selectedButton, true);
        }

        private void ClearSelection()
        {
            SetSelectionVisual(_selectedButton, false);
            _selectedButton = null;
            _selectedGroup = null;
            _selectedIndex = -1;
        }

        private void SetSelectionVisual(Button button, bool isSelected)
        {
            if (button == null) return;
            if (!_slotCards.TryGetValue(button, out PlayerMiniCardView card)) return;
            PlayerMiniCardVisualState defaultState = PlayerMiniCardVisualState.Normal;
            card.SetVisualState(isSelected ? PlayerMiniCardVisualState.Selected : defaultState);
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
                TextAnchor.MiddleLeft, CareerUiTheme.ReferenceAccent);
            LayoutElement layout = text.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 26f;
            layout.preferredHeight = 26f;
        }

        private static void ApplyRoleBoardPalette(Transform panel)
        {
            if (panel == null) return;
            SetImageColor(panel, CareerUiTheme.RosterPanel);
            SetImageColor(panel.Find("HeaderSurface"), CareerUiTheme.RosterHeader);
            SetImageColor(panel.Find("HeaderAccent"), CareerUiTheme.ShellGold);
            Transform title = panel.Find("HeaderSlot");
            if (title.GetComponent<CareerUiPreserveTextColor>() == null)
                title.gameObject.AddComponent<CareerUiPreserveTextColor>();
            SetTextColor(title, Color.white);
            // 불투명 표면으로 유지해 배경 구장 색이 본문 대비를 흐리지 않게 한다.
            panel.GetComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.DataImage);
            panel.Find("HeaderSurface").GetComponent<CareerUiVisualElement>()
                .Initialize(CareerUiVisualRole.DataImage);
            SetImageColor(panel.Find("ContentSafeRect/RoleScroll"), CareerUiTheme.RosterPanel);
            Outline border = panel.Find("ThinBorder").GetComponent<Outline>();
            border.effectColor = CareerUiTheme.RosterBorder;
        }

        private static void ApplyInspectorPalette(Transform panel)
        {
            if (panel == null) return;
            SetImageColor(panel, RoleBoardSurface);
            SetImageColor(panel.Find("HeaderSurface"), RoleBoardSurface);
            SetImageColor(panel.Find("HeaderAccent"), RoleBoardBorder);
            SetTextColor(panel.Find("HeaderSlot"), CareerUiTheme.ReferenceText);
        }

        private static void SetImageColor(Transform target, Color color)
        {
            if (target == null) return;
            Image image = target.GetComponent<Image>();
            if (image != null) image.color = color;
        }

        private static void SetTextColor(Transform target, Color color)
        {
            if (target == null) return;
            Text text = target.GetComponent<Text>();
            if (text != null) text.color = color;
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
