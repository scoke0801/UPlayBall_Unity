using System;
using System.Collections.Generic;
using Baseball.Core.Growth;
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
    public sealed partial class UI_Scene_OwnerRosterLineup : MonoBehaviour, IUiCancelHandler
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
        private Button _placementEditButton;
        private Button _selectedButton;
        private PlayerMiniCardView _selectedOwnedCard;
        private OwnerRosterLineupPresentationModel _model;
        private OwnerRosterPitchingPresentationModel _pitchingModel;
        private int _presetIndex;
        private PlayerGroupTab _activePlayerGroup = PlayerGroupTab.Hitter;
        private OwnerRosterWorkspaceMode _workspaceMode = OwnerRosterWorkspaceMode.Lineup;
        private OwnerLineupSwapGroup? _selectedGroup;
        private int _selectedIndex = -1;
        private string _selectedOwnedCardId;
        private string _selectedOwnedPlayerName;
        private int _positionFilter;
        private bool _isPlacementEditMode;
        private bool _hasPreview;

        public event Action<OwnerLineupSwapGroup, int, int> SwapRequested;
        public event Action<OwnerLineupSwapGroup, int, string> AssignmentRequested;
        public event Action<string> PresetSelected;
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
            SetPreviewState(CreateDefaultInstruction(), false, false);
        }

        /// <summary>Navigation Route마다 타자·투수 선택과 필터를 독립적으로 유지한다.</summary>
        public void SetWorkspaceMode(OwnerRosterWorkspaceMode mode)
        {
            PlayerGroupTab target = mode == OwnerRosterWorkspaceMode.Pitching
                ? PlayerGroupTab.Pitcher
                : PlayerGroupTab.Hitter;
            if (_workspaceMode == mode && _activePlayerGroup == target)
            {
                return;
            }
            _workspaceMode = mode;
            _activePlayerGroup = target;
            _positionFilter = 0;
            RenderActivePlayerGroup();
        }

        /// <summary>저장 전 후보 배치를 화면에 표시하고 Validator가 허용한 경우에만 확정 CTA를 연다.</summary>
        public void BindPreview(
            OwnerRosterLineupPresentationModel preview,
            string message)
        {
            if (preview == null) throw new ArgumentNullException(nameof(preview));
            _model = preview;
            _pitchingModel = OwnerRosterPitchingPresentationBuilder.Build(preview);
            _summaryText.text = preview.RosterSummaryText;
            _evaluationText.text = preview.EvaluationText + "\n" + preview.EvaluationBasisText;
            _validationText.text = preview.ValidationText;
            _validationText.color = preview.CanSave ? InspectorMessage : CareerUiTheme.Warning;
            RenderActivePlayerGroup();
            _presetIndex = FindSelectedPreset(preview);
            RenderPresetControls();
            SetPreviewState(message, preview.CanSave, true);
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

        /// <summary>배치 Preview와 선택 상태를 저장값으로 되돌리고 편집 모드를 끝낸다.</summary>
        public bool TryHandleCancel()
        {
            if (_hasPreview)
            {
                _isPlacementEditMode = false;
                LineupChangeCancelled?.Invoke();
                return true;
            }
            if (_selectedGroup.HasValue || _isPlacementEditMode)
            {
                _isPlacementEditMode = false;
                if (_placementEditButton != null)
                    _placementEditButton.GetComponentInChildren<Text>().text = "배치 편집";
                ClearSelection();
                return true;
            }
            return false;
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
            if (_placementEditButton != null) _placementEditButton.onClick.RemoveAllListeners();
            if (_confirmPreviewButton != null) _confirmPreviewButton.onClick.RemoveAllListeners();
            if (_cancelPreviewButton != null) _cancelPreviewButton.onClick.RemoveAllListeners();
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
            _summaryText = CreateToolbarText(tabs, "RosterSummary", 280f, 12, FontStyle.Bold);
            _presetStateText = CreateToolbarText(tabs, "PresetState", 190f, 12, FontStyle.Bold);
            _evaluationText = CreateToolbarText(tabs, "RosterEvaluation", 0f, 10, FontStyle.Normal);
            _evaluationText.gameObject.SetActive(false);
            _placementEditButton = CreateToolbarButton(tabs, "PlacementEditMode", "배치 편집", 96f,
                TogglePlacementEditMode);
            _previousPresetButton = CreateToolbarButton(tabs, "PreviousPresetButton", "◀", 54f,
                () => SelectRelativePreset(-1));
            _nextPresetButton = CreateToolbarButton(tabs, "NextPresetButton", "▶", 54f,
                () => SelectRelativePreset(1));
            _cancelPreviewButton = CreateToolbarButton(tabs, "CancelLineupPreview", "취소", 72f,
                () => LineupChangeCancelled?.Invoke());
            _confirmPreviewButton = CreateToolbarButton(tabs, "ConfirmLineupPreview", "배치 저장", 104f,
                () => LineupChangeConfirmed?.Invoke());
            OwnerUiButtonSkin.Apply(_confirmPreviewButton, OwnerButtonRole.Primary);

            RectTransform statusStrip = OwnerWorkspaceUiFactory.CreateRoot(board, "PlayerOrderStatusStrip", false);
            OwnerRuntimeUiFactory.SetAnchors(
                statusStrip, new Vector2(0f, 0.895f), new Vector2(1f, 0.935f), Vector2.zero, Vector2.zero);
            HorizontalLayoutGroup statusLayout = OwnerWorkspaceUiFactory.AddHorizontalLayout(
                statusStrip, CareerUiTheme.Space2);
            statusLayout.childForceExpandWidth = false;
            _previewStateText = CreateToolbarText(statusStrip, "PreviewState", 620f, 12, FontStyle.Normal);
            _validationText = CreateToolbarText(statusStrip, "ValidationMessages", 620f, 12, FontStyle.Normal);

            _primaryAssignedContent = CreateColumn(
                board, "PrimaryAssignedPanel", "선발", out RectTransform primaryAssignedPanel);
            _secondaryAssignedContent = CreateColumn(
                board, "SecondaryAssignedPanel", "벤치", out RectTransform secondaryAssignedPanel);
            _ownedContent = CreateColumn(board, "OwnedPlayerPanel", "보유 선수", out RectTransform ownedPanel);
            _analysisContent = CreateColumn(
                board, "ConditionAnalysisPanel", "컨디션 분석", out RectTransform analysisPanel);
            _setupContent = CreateColumn(board, "SetupPanel", "셋업", out RectTransform setupPanel);
            _closerContent = CreateColumn(board, "CloserPanel", "마무리", out RectTransform closerPanel);
            SetUpperPanel(setupPanel, 0.81f, 0.90f);
            SetUpperPanel(closerPanel, 0.91f, 1f);
            OwnerRuntimeUiFactory.SetAnchors(
                primaryAssignedPanel, new Vector2(0f, 0.56f), new Vector2(0.66f, 0.93f), Vector2.zero, Vector2.zero);
            OwnerRuntimeUiFactory.SetAnchors(
                secondaryAssignedPanel, new Vector2(0.67f, 0.56f), new Vector2(1f, 0.93f), Vector2.zero, Vector2.zero);
            OwnerRuntimeUiFactory.SetAnchors(
                ownedPanel, Vector2.zero, new Vector2(0.66f, 0.55f), Vector2.zero, Vector2.zero);
            OwnerRuntimeUiFactory.SetAnchors(
                analysisPanel, new Vector2(0.67f, 0f), new Vector2(1f, 0.55f), Vector2.zero, Vector2.zero);

            CareerUiSkin.Apply(_workspaceRoot);
            ApplyRoleBoardPalette(_workspaceRoot.Find("PlayerOrderBoard/PrimaryAssignedPanel"));
            ApplyRoleBoardPalette(_workspaceRoot.Find("PlayerOrderBoard/SecondaryAssignedPanel"));
            ApplyRoleBoardPalette(_workspaceRoot.Find("PlayerOrderBoard/OwnedPlayerPanel"));
            ApplyRoleBoardPalette(_workspaceRoot.Find("PlayerOrderBoard/ConditionAnalysisPanel"));
            ApplyRoleBoardPalette(setupPanel);
            ApplyRoleBoardPalette(closerPanel);
            UpdatePlayerGroupTabs();
            foreach (Transform panel in board)
                if (panel.name.EndsWith("Panel", StringComparison.Ordinal)) CompactPanel((RectTransform)panel);
            OwnerRuntimeUiFactory.SetAnchors(ownedPanel, Vector2.zero, new Vector2(0.64f, 0.60f), Vector2.zero, Vector2.zero);
            OwnerRuntimeUiFactory.SetAnchors(analysisPanel, new Vector2(0.65f, 0f), new Vector2(1f, 0.60f), Vector2.zero, Vector2.zero);
        }

        private static Text CreateToolbarText(
            Transform parent,
            string name,
            float preferredWidth,
            int fontSize,
            FontStyle fontStyle)
        {
            Text text = OwnerWorkspaceUiFactory.CreateText(
                parent, name, string.Empty, fontSize, fontStyle,
                TextAnchor.MiddleLeft, CareerUiTheme.ReferenceText);
            var layout = text.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = preferredWidth;
            layout.preferredWidth = preferredWidth;
            layout.flexibleWidth = preferredWidth <= 0f ? 0f : 1f;
            return text;
        }

        private static Button CreateToolbarButton(
            Transform parent,
            string name,
            string label,
            float width,
            Action action)
        {
            Button button = OwnerWorkspaceUiFactory.CreateButton(parent, name, label, action);
            LayoutElement layout = button.GetComponent<LayoutElement>();
            layout.minWidth = width;
            layout.preferredWidth = width;
            layout.flexibleWidth = 0f;
            return button;
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
        }

        private static int FindSelectedPreset(OwnerRosterLineupPresentationModel model)
        {
            for (int index = 0; index < model.Presets.Count; index++)
                if (model.Presets[index].IsSelected) return index;
            return 0;
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
            _workspaceMode = playerGroup == PlayerGroupTab.Pitcher
                ? OwnerRosterWorkspaceMode.Pitching
                : OwnerRosterWorkspaceMode.Lineup;
            _positionFilter = 0;
            RenderActivePlayerGroup();
        }

        private void TogglePlacementEditMode()
        {
            _isPlacementEditMode = !_isPlacementEditMode;
            _placementEditButton.GetComponentInChildren<Text>().text =
                _isPlacementEditMode ? "편집 종료" : "배치 편집";
            ClearSelection();
            if (!_hasPreview) _previewStateText.text = CreateDefaultInstruction();
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
            SetUpperPanel((RectTransform)board.Find("PrimaryAssignedPanel"), 0f, isPitcher ? 0.44f : 0.64f);
            SetUpperPanel((RectTransform)board.Find("SecondaryAssignedPanel"), isPitcher ? 0.45f : 0.65f, isPitcher ? 0.80f : 1f);
            board.Find("SecondaryAssignedPanel/HeaderSlot").GetComponent<Text>().text = isPitcher ? "불펜" : "벤치";
            board.Find("PrimaryAssignedPanel/HeaderSlot").GetComponent<Text>().text = isPitcher ? "선발" : "타순";

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
            _hasPreview = hasPreview;
            _previewStateText.text = message ?? string.Empty;
            _previewStateText.color = hasPreview && !canConfirm
                ? CareerUiTheme.Warning
                : CareerUiTheme.ReferenceText;
            _confirmPreviewButton.interactable = canConfirm;
            _cancelPreviewButton.interactable = hasPreview;
        }

        private static void SetUpperPanel(RectTransform panel, float left, float right)
        {
            OwnerRuntimeUiFactory.SetAnchors(panel, new Vector2(left, 0.61f),
                new Vector2(right, 0.89f), Vector2.zero, Vector2.zero);
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
            List<OwnerCollectionCardSnapshot> cards = GetFilteredOwnedPlayers(isPitcher);
            int cardCount = cards.Count;

            AddSectionTitle(content, $"{(isPitcher ? "보유 투수" : "보유 야수")} {cardCount}장");
            int slotCount = Math.Max(columnCount * 2, ((cardCount + columnCount - 1) / columnCount) * columnCount);
            RectTransform gridRoot = CreateCardGrid(content, "OwnedGrid", slotCount, columnCount);
            for (int index = 0; index < cards.Count; index++)
            {
                CreateOwnedPlayerCard(gridRoot, cards[index], index);
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
                // 배치 영역의 높이와 전체 슬롯 수로 공통 크기를 정해 셋업 카드만 확대되지 않게 한다.
                RectTransform board = (RectTransform)_workspaceRoot.Find("PlayerOrderBoard");
                int orderCount = _activePlayerGroup == PlayerGroupTab.Pitcher ? 11 : 14;
                float orderWidth = (board.rect.width - 100f) / orderCount;
                float orderHeight = Mathf.Max(72f, board.rect.height * .28f - 38f);
                float width = Mathf.Max(1f, Mathf.Min(available / grid.constraintCount, orderWidth, orderHeight / 1.5f));
                float height = width * 1.5f;
                if (!Mathf.Approximately(grid.cellSize.x, width) ||
                    !Mathf.Approximately(grid.cellSize.y, height))
                    grid.cellSize = new Vector2(width, height);

                int rowCount = Mathf.Max(1, Mathf.CeilToInt(grid.transform.childCount / (float)grid.constraintCount));
                float gridHeight = rowCount * height + Mathf.Max(0, rowCount - 1) * grid.spacing.y +
                                   grid.padding.vertical;
                LayoutElement layout = grid.GetComponent<LayoutElement>();
                layout.minHeight = gridHeight;
                layout.preferredHeight = gridHeight;
            }
        }

        private void CreateSlotButton(Transform parent, OwnerLineupSlotModel slot)
        {
            OwnerRosterPlayerSnapshot player = slot.Player;
            string playerId = player?.CardId ?? $"empty:{slot.Group}:{slot.Index}";
            string displayName = player?.DisplayName ?? "미지정";
            string year = player == null ? string.Empty : FormatCompactYear(player.OriginYear);
            string cost = player == null ? string.Empty : $"★ {player.Cost}";
            string edition = player == null || player.Edition == PlayerCardEdition.Normal
                ? string.Empty : OwnerRosterLineupPresentationBuilder.FormatEdition(player.Edition);
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
                visualState: PlayerMiniCardVisualState.Normal, frameEdition: player?.Edition, cost: player?.Cost);
            PlayerMiniCardView card = PlayerMiniCardView.CreateRuntime(
                parent, $"{slot.Group}_{slot.Index}");
            card.UseLineupSlotLayout();
            card.Bind(cardModel, GetRosterPortrait());
            card.SetTeamIdentity(FindOwnedCard(player?.CardId)?.TeamDisplayName);
            card.Selected += selected => HandleAssignedCardSelected(selected, slot);
            card.DetailRequested += ShowCardDetail;

            Button button = card.GetComponent<Button>();
            _slotButtons.Add(button);
            _slotCards[button] = card;
        }

        private void HandleAssignedCardSelected(PlayerMiniCardModel selected, OwnerLineupSlotModel slot)
        {
            if (_isPlacementEditMode)
            {
                HandleSlotSelected(slot);
                return;
            }

            if (slot.Player != null) ShowCardDetail(selected);
        }

        private void CreateOwnedPlayerCard(
            Transform parent,
            OwnerCollectionCardSnapshot player,
            int sourceIndex)
        {
            var cardModel = new PlayerMiniCardModel(
                player.CardId,
                player.DisplayName,
                "미배치",
                FormatCompactYear(player.OriginYear),
                $"★ {player.Cost}",
                player.Edition == PlayerCardEdition.Normal ? string.Empty : OwnerRosterLineupPresentationBuilder.FormatEdition(player.Edition),
                FormatPositionName(player.Position), frameEdition: player.Edition, cost: player.Cost);
            PlayerMiniCardView card = PlayerMiniCardView.CreateRuntime(parent, $"Owned_{sourceIndex}");
            card.UseLineupSlotLayout();
            card.Bind(cardModel, GetRosterPortrait());
            card.SetTeamIdentity(player.TeamDisplayName);
            card.SetAssignmentBadge(FindOwnedCardAssignment(player.CardId));
            card.Selected += selected => HandleOwnedCardSelected(selected, card);
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
            List<OwnerCollectionCardSnapshot> cards = CreateDetailSequence(selected.PlayerId);
            for (int index = 0; index < cards.Count; index++)
                if (cards[index].CardId == selected.PlayerId)
                {
                    UI_Popup_OwnerPlayerCard.Show(_workspaceRoot, cards, index);
                    return;
                }
        }

        private List<OwnerCollectionCardSnapshot> CreateDetailSequence(string selectedCardId)
        {
            var assigned = new List<OwnerCollectionCardSnapshot>();
            AppendAssignedCards(assigned, _model.BattingOrder);
            AppendAssignedCards(assigned, _model.Bench);
            if (ContainsCard(assigned, selectedCardId)) return assigned;

            assigned.Clear();
            AppendAssignedCards(assigned, _model.StarterRotation);
            AppendAssignedCards(assigned, _model.ReliefPitching);
            if (ContainsCard(assigned, selectedCardId)) return assigned;

            return GetFilteredOwnedPlayers(_activePlayerGroup == PlayerGroupTab.Pitcher);
        }

        private void AppendAssignedCards(
            ICollection<OwnerCollectionCardSnapshot> destination,
            IReadOnlyList<OwnerLineupSlotModel> slots)
        {
            for (int index = 0; index < slots.Count; index++)
            {
                OwnerCollectionCardSnapshot card = FindOwnedCard(slots[index].Player?.CardId);
                if (card != null) destination.Add(card);
            }
        }

        private OwnerCollectionCardSnapshot FindOwnedCard(string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return null;
            for (int index = 0; index < _model.Snapshot.OwnedPlayers.Count; index++)
            {
                OwnerCollectionCardSnapshot card = _model.Snapshot.OwnedPlayers[index];
                if (string.Equals(card.CardId, cardId, StringComparison.Ordinal)) return card;
            }
            return null;
        }

        private static bool ContainsCard(IReadOnlyList<OwnerCollectionCardSnapshot> cards, string cardId)
        {
            for (int index = 0; index < cards.Count; index++)
                if (string.Equals(cards[index].CardId, cardId, StringComparison.Ordinal)) return true;
            return false;
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
            OwnerUiButtonSkin.SetSelected(button, isSelected);
        }

        private void HandleSlotSelected(OwnerLineupSlotModel slot)
        {
            Button clicked = FindButton(slot.Group, slot.Index);
            if (!string.IsNullOrEmpty(_selectedOwnedCardId))
            {
                string incomingCardId = _selectedOwnedCardId;
                ClearSelection();
                AssignmentRequested?.Invoke(slot.Group, slot.Index, incomingCardId);
                return;
            }
            if (!_selectedGroup.HasValue)
            {
                SelectAssigned(clicked, slot);
                ShowPitchingDetail(slot.Group, slot.Index);
                return;
            }
            if (_selectedGroup.Value == slot.Group && _selectedIndex == slot.Index)
            {
                ClearSelection();
                _previewStateText.text = CreateDefaultInstruction();
                return;
            }

            OwnerLineupSwapGroup firstGroup = _selectedGroup.Value;
            int first = _selectedIndex;
            ClearSelection();
            if (firstGroup == slot.Group)
            {
                SwapRequested?.Invoke(slot.Group, first, slot.Index);
                return;
            }
            if (slot.Player != null)
                AssignmentRequested?.Invoke(firstGroup, first, slot.Player.CardId);
        }

        private void HandleOwnedCardSelected(PlayerMiniCardModel selected, PlayerMiniCardView card)
        {
            if (!_isPlacementEditMode)
            {
                ShowCardDetail(selected);
                return;
            }
            if (_selectedGroup.HasValue)
            {
                OwnerLineupSwapGroup group = _selectedGroup.Value;
                int index = _selectedIndex;
                ClearSelection();
                AssignmentRequested?.Invoke(group, index, selected.PlayerId);
                return;
            }
            if (string.Equals(_selectedOwnedCardId, selected.PlayerId, StringComparison.Ordinal))
            {
                ClearSelection();
                _previewStateText.text = CreateDefaultInstruction();
                return;
            }

            ClearSelection();
            _selectedOwnedCard = card;
            _selectedOwnedCardId = selected.PlayerId;
            _selectedOwnedPlayerName = selected.DisplayName;
            _selectedOwnedCard.SetVisualState(PlayerMiniCardVisualState.Selected);
            _previewStateText.text = $"{_selectedOwnedPlayerName} 선택 · 배치할 슬롯을 선택하세요.";
            _previewStateText.color = CareerUiTheme.ReferenceAccent;
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

        private void SelectAssigned(Button button, OwnerLineupSlotModel slot)
        {
            ClearSelection();
            _selectedButton = button;
            _selectedGroup = slot.Group;
            _selectedIndex = slot.Index;
            SetSelectionVisual(_selectedButton, true);
            _previewStateText.text = $"{slot.Label} 선택 · 교체할 보유 선수를 선택하세요.";
            _previewStateText.color = CareerUiTheme.ReferenceAccent;
        }

        private void ClearSelection()
        {
            SetSelectionVisual(_selectedButton, false);
            if (_selectedOwnedCard != null)
                _selectedOwnedCard.SetVisualState(PlayerMiniCardVisualState.Normal);
            _selectedButton = null;
            _selectedOwnedCard = null;
            _selectedGroup = null;
            _selectedIndex = -1;
            _selectedOwnedCardId = null;
            _selectedOwnedPlayerName = null;
        }

        private string CreateDefaultInstruction()
        {
            return _isPlacementEditMode
                ? "배치 슬롯과 보유 선수를 차례로 선택하세요. 배치된 선수끼리는 역할을 맞바꿉니다."
                : "카드를 누르면 상세 정보를 확인할 수 있습니다. 교체하려면 배치 편집을 누르세요.";
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
