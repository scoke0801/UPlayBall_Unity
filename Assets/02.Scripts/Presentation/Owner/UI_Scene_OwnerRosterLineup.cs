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
        private const int OwnedCardsPerPage = 18;
        private static readonly string[] AssignedPanelNames =
            { "PrimaryAssignedPanel", "SecondaryAssignedPanel", "SetupPanel", "CloserPanel" };
        private static readonly string[] LowerPanelNames = { "OwnedPlayerPanel", "ConditionAnalysisPanel" };

        private readonly List<Button> _slotButtons = new List<Button>();
        private readonly List<GridLayoutGroup> _responsiveGrids = new List<GridLayoutGroup>();
        private readonly Dictionary<Button, PlayerMiniCardView> _slotCards =
            new Dictionary<Button, PlayerMiniCardView>();
        private readonly Dictionary<string, PlayerMiniCardView> _assignedCardViews =
            new Dictionary<string, PlayerMiniCardView>(StringComparer.Ordinal);
        private readonly List<PlayerMiniCardView> _ownedCardViews = new List<PlayerMiniCardView>();
        private bool _hasRenderedPlayerGroup;
        private PlayerGroupTab _renderedPlayerGroup;
        private int _renderedOwnedCount;
        private int _renderedOwnedPageIndex;
        private RectTransform _workspaceRoot;
        private RectTransform _inspectorRoot;
        private RectTransform _actionRoot;
        private RectTransform _primaryAssignedContent;
        private RectTransform _secondaryAssignedContent;
        private RectTransform _ownedContent;
        private RectTransform _ownedHeader;
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
        private UI_Popup_OwnerAutoLineup _autoLineupPopup;
        private Func<int, string, System.Threading.CancellationToken, System.Threading.Tasks.Task<string>> _autoLineupHandler;
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
        private int _ownedPageIndex;
        private bool _isPlacementEditMode;
        private bool _hasPreview;
        private Func<IReadOnlyList<string>, IReadOnlyList<OwnerCollectionCardSnapshot>> _cardDetailResolver;

        public event Action<OwnerLineupSwapGroup, int, int> SwapRequested;
        public event Action<OwnerLineupSwapGroup, int, string> AssignmentRequested;
        public event Action<string> PresetSelected;
        public event Action LineupChangeConfirmed;
        public event Action LineupChangeCancelled;

        /// <summary>자동 편성 계산과 검증은 Game 경계를 거쳐 기존 배치 미리보기로 전달한다.</summary>
        public void SetAutoLineupHandler(Func<int, string, System.Threading.CancellationToken,
            System.Threading.Tasks.Task<string>> handler) => _autoLineupHandler = handler;

        private void OpenAutoLineup()
        {
            if (_autoLineupPopup != null || _model == null) return;
            if (_positionSourceIndex >= 0) ClosePositionEditor();
            _autoLineupPopup = UI_Popup_OwnerAutoLineup.Show(_workspaceRoot, _model.Snapshot.OwnedPlayers,
                _autoLineupHandler, message => _previewStateText.text = message);
        }

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
            bool isFirstBind = _model == null;
            _model = model;
            _pitchingModel = OwnerRosterPitchingPresentationBuilder.Build(model);
            EnsureBuilt();
            if (isFirstBind) _ownedPageIndex = 0;
            ClearSelection();
            _summaryText.text = model.RosterSummaryText;
            ClearComparison();
            _evaluationText.text = model.EvaluationText + "\n" + model.EvaluationBasisText;
            _validationText.text = string.Empty;
            _validationText.color = InspectorMessage;
            RefreshOrRenderActivePlayerGroup();
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
            ClearComparison();
            _activePlayerGroup = target;
            _positionFilter = 0;
            _ownedPageIndex = 0;
            RefreshOrRenderActivePlayerGroup();
        }

        /// <summary>목록용 요약 카드의 상세 정보는 팝업을 열 때만 계산하도록 Resolver를 연결한다.</summary>
        public void SetCardDetailResolver(
            Func<IReadOnlyList<string>, IReadOnlyList<OwnerCollectionCardSnapshot>> resolver)
        {
            _cardDetailResolver = resolver;
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
            RefreshOrRenderActivePlayerGroup();
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
            if (!visible && _autoLineupPopup != null) _autoLineupPopup.Close();
            if (!visible && _positionSourceIndex >= 0) ClosePositionEditor();
            if (_workspaceRoot != null) _workspaceRoot.gameObject.SetActive(visible);
            if (_inspectorRoot != null) _inspectorRoot.gameObject.SetActive(visible);
            if (_actionRoot != null) _actionRoot.gameObject.SetActive(visible);
        }

        /// <summary>배치 Preview와 선택 상태를 저장값으로 되돌리고 편집 모드를 끝낸다.</summary>
        public bool TryHandleCancel()
        {
            if (_autoLineupPopup != null) return _autoLineupPopup.TryHandleCancel();
            if (_positionSourceIndex >= 0)
            {
                ClosePositionEditor();
                return true;
            }
            if (_hasPreview)
            {
                _isPlacementEditMode = false;
                LineupChangeCancelled?.Invoke();
                return true;
            }
            if (_selectedGroup.HasValue || _selectedOwnedCard != null || _isPlacementEditMode)
            {
                _isPlacementEditMode = false;
                if (_placementEditButton != null)
                    _placementEditButton.GetComponentInChildren<Text>().text = "배치 편집";
                ClearSelection();
                return true;
            }
            if (_isComparisonTab)
            {
                SelectAnalysisTab(false);
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
            Image backdrop = _workspaceRoot.gameObject.AddComponent<Image>();
            backdrop.color = CareerUiTheme.RosterBoard;
            backdrop.raycastTarget = false;
            _workspaceRoot.gameObject.AddComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.DataImage);
            RectTransform board = OwnerWorkspaceUiFactory.CreateRoot(_workspaceRoot, "PlayerOrderBoard", false);
            board.offsetMin = new Vector2(CareerUiTheme.Space4, CareerUiTheme.RosterActionHeight + CareerUiTheme.Space2);
            board.offsetMax = new Vector2(-CareerUiTheme.Space4, -CareerUiTheme.Space4);

            RectTransform tabs = OwnerWorkspaceUiFactory.CreateRoot(board, "PlayerGroupTabs", false);
            OwnerRuntimeUiFactory.SetAnchors(
                tabs, Vector2.up, Vector2.one, new Vector2(0f, -44f), Vector2.zero);
            HorizontalLayoutGroup tabLayout = OwnerWorkspaceUiFactory.AddHorizontalLayout(tabs, CareerUiTheme.Space1);
            tabLayout.childForceExpandWidth = false;
            _hitterTabButton = CreatePlayerGroupTab(tabs, "HitterTab", "타자", PlayerGroupTab.Hitter);
            _pitcherTabButton = CreatePlayerGroupTab(tabs, "PitcherTab", "투수", PlayerGroupTab.Pitcher);
            _summaryText = CreateToolbarText(tabs, "RosterSummary", 160f, 13, FontStyle.Bold);
            _presetStateText = CreateToolbarText(tabs, "RosterRuleSummary", 160f, 12, FontStyle.Normal);
            _evaluationText = CreateToolbarText(tabs, "RosterEvaluation", 0f, 10, FontStyle.Normal);
            _evaluationText.gameObject.SetActive(false);
            CreateToolbarButton(tabs, "AutoLineup", "자동 배치", 96f, OpenAutoLineup);
            _placementEditButton = CreateToolbarButton(tabs, "PlacementEditMode", "배치 편집", 96f,
                TogglePlacementEditMode);
            _previousPresetButton = CreateToolbarButton(tabs, "PreviousPresetButton", "이전 편성", 88f,
                () => SelectRelativePreset(-1));
            _nextPresetButton = CreateToolbarButton(tabs, "NextPresetButton", "다음 편성", 88f,
                () => SelectRelativePreset(1));
            RectTransform actions = OwnerWorkspaceUiFactory.CreateRoot(_workspaceRoot, "RosterActions", false);
            OwnerRuntimeUiFactory.SetAnchors(actions, Vector2.zero, new Vector2(1f, 0f),
                new Vector2(CareerUiTheme.Space4, CareerUiTheme.Space2),
                new Vector2(-CareerUiTheme.Space4, CareerUiTheme.RosterActionHeight));
            var actionLayout = OwnerWorkspaceUiFactory.AddHorizontalLayout(actions, CareerUiTheme.Space2);
            actionLayout.childForceExpandWidth = false;
            _validationText = CreateToolbarText(actions, "ValidationMessages", 0f, 13, FontStyle.Normal);
            _validationText.GetComponent<LayoutElement>().flexibleWidth = 1f;
            _cancelPreviewButton = CreateToolbarButton(actions, "CancelLineupPreview", "변경 취소", 104f,
                () => LineupChangeCancelled?.Invoke());
            _confirmPreviewButton = CreateToolbarButton(actions, "ConfirmLineupPreview", "배치 저장", 128f,
                () => LineupChangeConfirmed?.Invoke());
            OwnerUiButtonSkin.Apply(_confirmPreviewButton, OwnerButtonRole.Primary);

            RectTransform statusStrip = OwnerWorkspaceUiFactory.CreateRoot(board, "PlayerOrderStatusStrip", false);
            OwnerRuntimeUiFactory.SetAnchors(
                statusStrip, Vector2.up, Vector2.one, new Vector2(0f, -76f), new Vector2(0f, -44f));
            HorizontalLayoutGroup statusLayout = OwnerWorkspaceUiFactory.AddHorizontalLayout(
                statusStrip, CareerUiTheme.Space2);
            statusLayout.childForceExpandWidth = false;
            _previewStateText = CreateToolbarText(statusStrip, "PreviewState", 0f, 13, FontStyle.Normal);
            _previewStateText.GetComponent<LayoutElement>().flexibleWidth = 1f;

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
            BuildOwnedPlayerHeader(ownedPanel);
            BuildAnalysisTabs(analysisPanel);
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
                TextAnchor.MiddleLeft, CareerUiTheme.RosterText);
            text.gameObject.AddComponent<CareerUiPreserveTextColor>();
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
            _presetStateText.text = _model.RosterCardAndTeamColorSummaryText;
            _previousPresetButton.interactable = hasMultiplePresets;
            _nextPresetButton.interactable = hasMultiplePresets;
            _previousPresetButton.gameObject.SetActive(hasMultiplePresets);
            _nextPresetButton.gameObject.SetActive(hasMultiplePresets);
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

        private void BuildOwnedPlayerHeader(RectTransform panel)
        {
            var safe = (RectTransform)panel.Find("ContentSafeRect");
            var layout = safe.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = CareerUiTheme.Space1;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            _ownedHeader = CreateAnalysisSurface(safe, "OwnedPlayerHeader", CareerUiTheme.RosterSurface);
            _ownedHeader.SetAsFirstSibling();
            // 필터 행의 LayoutGroup이 보고하는 flexibleHeight가 고정 헤더를 늘리지 않게 한다.
            _ownedHeader.gameObject.AddComponent<LayoutElement>().flexibleHeight = 0f;
            var headerLayout = _ownedHeader.gameObject.AddComponent<VerticalLayoutGroup>();
            headerLayout.spacing = CareerUiTheme.Space1;
            headerLayout.childControlWidth = headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = true;
            headerLayout.childForceExpandHeight = false;
            var sizing = safe.Find("RoleScroll").gameObject.AddComponent<LayoutElement>();
            sizing.minHeight = sizing.preferredHeight = 0f;
            sizing.flexibleHeight = 1f;
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
            ClearComparison();
            _activePlayerGroup = playerGroup;
            _workspaceMode = playerGroup == PlayerGroupTab.Pitcher
                ? OwnerRosterWorkspaceMode.Pitching
                : OwnerRosterWorkspaceMode.Lineup;
            _positionFilter = 0;
            _ownedPageIndex = 0;
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
            _positionSourceIndex = -1;
            _positionButtons.Clear();
            ResetPositionEditorLayout();
            SetAnalysisTitle("편성 분석");
            _slotButtons.Clear();
            _slotCards.Clear();
            _assignedCardViews.Clear();
            _ownedCardViews.Clear();
            _responsiveGrids.Clear();
            OwnerRuntimeUiFactory.ClearChildren(_primaryAssignedContent);
            OwnerRuntimeUiFactory.ClearChildren(_secondaryAssignedContent);
            OwnerRuntimeUiFactory.ClearChildren(_ownedContent);
            OwnerRuntimeUiFactory.ClearChildren(_ownedHeader);
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
                RenderPositionButtons(_primaryAssignedContent);
                RenderSlotGroup(_secondaryAssignedContent, "벤치 5명", _model.Bench, 5);
                RenderOwnedPlayers(_ownedContent, isPitcher: false, 9);
                RenderRosterChart(_analysisContent, _model.BattingOrder, false);
                RenderDefensiveWarnings();
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
            _renderedPlayerGroup = _activePlayerGroup;
            foreach (Button button in _workspaceRoot.GetComponentsInChildren<Button>(true))
                OwnerUiButtonSkin.SetBoardStyle(button);
            _renderedOwnedCount = GetFilteredOwnedPlayers(isPitcher).Count;
            _renderedOwnedPageIndex = _ownedPageIndex;
            _hasRenderedPlayerGroup = true;
            ScrollRect scroll = _ownedContent.GetComponentInParent<ScrollRect>(true);
            scroll.StopMovement();
            _ownedContent.anchoredPosition = Vector2.zero;
        }

        /// <summary>Preview 갱신에서는 카드 계층을 재생성하지 않고 데이터와 분석만 다시 바인딩한다.</summary>
        private void RefreshOrRenderActivePlayerGroup()
        {
            bool canPreserveScroll = _hasRenderedPlayerGroup && _renderedPlayerGroup == _activePlayerGroup &&
                _renderedOwnedPageIndex == _ownedPageIndex;
            Vector2 position = _ownedContent == null ? Vector2.zero : _ownedContent.anchoredPosition;
            if (!TryRefreshActivePlayerGroup())
            {
                RenderActivePlayerGroup();
                if (canPreserveScroll)
                {
                    Canvas.ForceUpdateCanvases();
                    _ownedContent.anchoredPosition = position;
                }
            }
        }

        private bool TryRefreshActivePlayerGroup()
        {
            if (!_hasRenderedPlayerGroup || _renderedPlayerGroup != _activePlayerGroup)
                return false;

            ClearSelection();
            _positionSourceIndex = -1;
            ResetPositionEditorLayout();
            SetAnalysisTitle("편성 분석");

            bool isPitcher = _activePlayerGroup == PlayerGroupTab.Pitcher;
            int assignedCount = isPitcher
                ? _model.StarterRotation.Count + _model.ReliefPitching.Count
                : _model.BattingOrder.Count + _model.Bench.Count;
            List<OwnerCollectionCardSnapshot> owned = GetFilteredOwnedPlayers(isPitcher);
            if (owned.Count != _renderedOwnedCount || _ownedPageIndex != _renderedOwnedPageIndex)
                return false;
            int firstOwnedIndex = _ownedPageIndex * OwnedCardsPerPage;
            int visibleOwnedCount = Math.Min(OwnedCardsPerPage, Math.Max(0, owned.Count - firstOwnedIndex));
            if (_assignedCardViews.Count != assignedCount || _ownedCardViews.Count != visibleOwnedCount)
                return false;

            if (isPitcher)
            {
                if (!TryBindAssignedCards(_model.StarterRotation) ||
                    !TryBindAssignedCards(_model.ReliefPitching))
                    return false;
            }
            else if (!TryBindAssignedCards(_model.BattingOrder) || !TryBindAssignedCards(_model.Bench))
            {
                return false;
            }

            for (int index = 0; index < visibleOwnedCount; index++)
                BindOwnedPlayerCard(_ownedCardViews[index], owned[firstOwnedIndex + index]);

            RefreshPositionButtons();
            OwnerRuntimeUiFactory.ClearChildren(_analysisContent);
            if (isPitcher)
            {
                var pitchers = new List<OwnerLineupSlotModel>(_model.StarterRotation);
                pitchers.AddRange(_model.ReliefPitching);
                RenderRosterChart(_analysisContent, pitchers, true);
            }
            else
            {
                RenderRosterChart(_analysisContent, _model.BattingOrder, false);
                RenderDefensiveWarnings();
            }
            UpdatePlayerGroupTabs();
            return true;
        }

        private bool TryBindAssignedCards(IReadOnlyList<OwnerLineupSlotModel> slots)
        {
            for (int index = 0; index < slots.Count; index++)
            {
                OwnerLineupSlotModel slot = slots[index];
                if (!_assignedCardViews.TryGetValue(
                    CreateSlotKey(slot.Group, slot.Index), out PlayerMiniCardView card))
                    return false;
                BindSlotCard(card, slot);
            }
            return true;
        }

        private void SetPreviewState(string message, bool canConfirm, bool hasPreview)
        {
            if (_previewStateText == null) return;
            _hasPreview = hasPreview;
            _previewStateText.text = message ?? string.Empty;
            _previewStateText.color = hasPreview && !canConfirm
                ? CareerUiTheme.Warning
                : CareerUiTheme.RosterTextSecondary;
            _confirmPreviewButton.interactable = canConfirm;
            _cancelPreviewButton.interactable = hasPreview;
            if (!hasPreview && string.IsNullOrEmpty(_validationText.text))
                _validationText.text = "저장된 편성입니다 · 변경 후 배치 저장으로 확정하세요.";
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
            RenderPositionFilters(_ownedHeader, isPitcher);
            List<OwnerCollectionCardSnapshot> cards = GetFilteredOwnedPlayers(isPitcher);
            int cardCount = cards.Count;
            int pageCount = Math.Max(1, (cardCount + OwnedCardsPerPage - 1) / OwnedCardsPerPage);
            _ownedPageIndex = Math.Min(_ownedPageIndex, pageCount - 1);
            int firstCardIndex = _ownedPageIndex * OwnedCardsPerPage;
            int visibleCardCount = Math.Min(OwnedCardsPerPage, cardCount - firstCardIndex);
            string summary = $"{(isPitcher ? "보유 투수" : "보유 야수")} {cardCount}장";
            RenderOwnedPageControls(_ownedHeader, pageCount, summary);

            if (cardCount == 0)
            {
                RectTransform empty = OwnerRuntimeUiFactory.CreateRect("OwnedGrid", content);
                OwnerWorkspaceUiFactory.AddVerticalLayout(empty);
                AddSectionTitle(empty, "조건에 맞는 선수가 없습니다.");
                Button reset = OwnerWorkspaceUiFactory.CreateButton(empty, "ResetPlayerFilters", "검색·필터 초기화", () =>
                {
                    _playerSearch = string.Empty;
                    _positionFilter = 0;
                    _editionFilter = null;
                    _cardFilters.Reset();
                    HandleOwnedPlayerFilterChanged();
                });
                OwnerUiButtonSkin.SetBoardStyle(reset);
                return;
            }

            int slotCount = columnCount * 2;
            RectTransform gridRoot = CreateCardGrid(content, "OwnedGrid", slotCount, columnCount);
            for (int index = 0; index < visibleCardCount; index++)
            {
                int sourceIndex = firstCardIndex + index;
                CreateOwnedPlayerCard(gridRoot, cards[sourceIndex], sourceIndex);
            }
            for (int index = visibleCardCount; index < slotCount; index++)
            {
                RectTransform empty = CreateAnalysisSurface(gridRoot, "EmptySlot", CareerUiTheme.RosterBoard);
                var outline = empty.gameObject.AddComponent<Outline>();
                outline.effectColor = CareerUiTheme.RosterDivider;
                outline.effectDistance = Vector2.one;
            }
        }

        private void RenderOwnedPageControls(Transform content, int pageCount, string summary)
        {
            RectTransform row = OwnerRuntimeUiFactory.CreateRect("OwnedPageControls", content);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 32f;
            HorizontalLayoutGroup layout = OwnerWorkspaceUiFactory.AddHorizontalLayout(row, CareerUiTheme.Space1);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;

            Text count = CreateToolbarText(row, "OwnedPlayerCount", 100f, 12, FontStyle.Bold);
            count.text = summary;
            count.GetComponent<LayoutElement>().flexibleWidth = 1f;
            _filterToggleButton = CreateToolbarButton(row, "TogglePlayerFilters", "검색·필터", 96f, () =>
            {
                _areFiltersExpanded = !(_areFiltersExpanded ?? _workspaceRoot.rect.height >= 650f);
                RefreshFilterVisibility();
            });

            Button previous = CreateToolbarButton(row, "PreviousOwnedPage", "이전", 72f, () => ChangeOwnedPage(-1));
            previous.interactable = _ownedPageIndex > 0;
            Text page = CreateToolbarText(
                row, "OwnedPageState", 44f, 11, FontStyle.Bold);
            page.alignment = TextAnchor.MiddleCenter;
            page.text = $"{_ownedPageIndex + 1} / {pageCount}";
            Button next = CreateToolbarButton(row, "NextOwnedPage", "다음", 72f, () => ChangeOwnedPage(1));
            next.interactable = _ownedPageIndex + 1 < pageCount;
            foreach (Button button in new[] { previous, next, _filterToggleButton })
            {
                LayoutElement sizing = button.GetComponent<LayoutElement>();
                sizing.minHeight = sizing.preferredHeight = 32f;
                sizing.flexibleHeight = 0f;
            }
        }

        private void ChangeOwnedPage(int delta)
        {
            _ownedPageIndex = Math.Max(0, _ownedPageIndex + delta);
            RenderActivePlayerGroup();
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
                parent, name, value, fontSize, style, alignment, CareerUiTheme.RosterText);
            text.gameObject.AddComponent<CareerUiPreserveTextColor>();
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
            // 셀의 이전 너비가 부모의 최소 너비로 역전파되면 창을 줄여도 Grid가 줄어들지 않는다.
            gridLayout.minWidth = gridLayout.preferredWidth = 0f;
            gridLayout.flexibleWidth = 1f;
            gridLayout.minHeight = rowCount * PlayerMiniCardView.LineupSlotHeight +
                                   Mathf.Max(0, rowCount - 1) * grid.spacing.y + 4f;
            gridLayout.preferredHeight = gridLayout.minHeight;
            return gridRoot;
        }

        private void LateUpdate()
        {
            if (_workspaceRoot == null || !_workspaceRoot.gameObject.activeInHierarchy) return;
            RefreshFilterVisibility();
            var boardRect = (RectTransform)_workspaceRoot.Find("PlayerOrderBoard");
            bool compact = _workspaceRoot.rect.height < 650f;
            float inset = Mathf.Max(CareerUiTheme.Space4, (_workspaceRoot.rect.width - CareerUiTheme.RosterMaxWidth) * .5f);
            boardRect.offsetMin = new Vector2(inset, boardRect.offsetMin.y);
            boardRect.offsetMax = new Vector2(-inset, compact ? -CareerUiTheme.Space2 : -CareerUiTheme.Space4);
            var status = (RectTransform)boardRect.Find("PlayerOrderStatusStrip");
            status.offsetMin = new Vector2(0f, compact ? -68f : -76f);
            float orderWidth = FitAssignedPanelsToCards();
            // 실제 Canvas 폭을 기준으로 계산해 좁은 셋업·마무리 구역에서도 카드가 잘리지 않게 한다.
            foreach (GridLayoutGroup grid in _responsiveGrids)
            {
                if (grid == null) continue;
                float innerWidth = GetGridInnerWidth(grid);
                float available = innerWidth - CareerUiTheme.Space1 * (grid.constraintCount - 1);
                float width = Mathf.Max(1f, Mathf.Min(available / grid.constraintCount, orderWidth));
                float height = width * 1.5f;
                bool isAssignedGrid = grid.transform.parent != _ownedContent;
                float spacing = isAssignedGrid && grid.constraintCount > 1
                    ? Mathf.Max(CareerUiTheme.Space1, (innerWidth - width * grid.constraintCount) / (grid.constraintCount - 1))
                    : CareerUiTheme.Space1;
                grid.spacing = new Vector2(spacing, grid.spacing.y);
                grid.childAlignment = grid.constraintCount == 1 ? TextAnchor.UpperCenter : TextAnchor.UpperLeft;
                if (!Mathf.Approximately(grid.cellSize.x, width) ||
                    !Mathf.Approximately(grid.cellSize.y, height))
                    grid.cellSize = new Vector2(width, height);

                int rowCount = Mathf.Max(1, Mathf.CeilToInt(grid.transform.childCount / (float)grid.constraintCount));
                float gridHeight = rowCount * height + Mathf.Max(0, rowCount - 1) * grid.spacing.y +
                                   grid.padding.vertical;
                LayoutElement layout = grid.GetComponent<LayoutElement>();
                layout.minHeight = gridHeight;
                layout.preferredHeight = gridHeight;
                if (grid.transform.parent == _primaryAssignedContent)
                {
                    Transform positions = _primaryAssignedContent.Find("DefensivePositions");
                    if (positions != null) positions.GetComponent<HorizontalLayoutGroup>().spacing = spacing;
                    foreach (Button positionButton in _positionButtons)
                    {
                        if (positionButton == null) continue;
                        LayoutElement sizing = positionButton.GetComponent<LayoutElement>();
                        sizing.minWidth = width;
                        sizing.preferredWidth = width;
                    }
                }
            }
        }

        private float FitAssignedPanelsToCards()
        {
            if (_workspaceRoot == null || _responsiveGrids.Count == 0) return PlayerMiniCardView.LineupSlotWidth;
            float width = float.MaxValue;
            foreach (GridLayoutGroup grid in _responsiveGrids)
            {
                if (grid == null || grid.transform.parent == _ownedContent) continue;
                float available = GetGridInnerWidth(grid) -
                    CareerUiTheme.Space1 * (grid.constraintCount - 1);
                if (available > 0f) width = Mathf.Min(width, available / grid.constraintCount);
            }
            if (width == float.MaxValue) return PlayerMiniCardView.LineupSlotWidth;
            bool compact = _workspaceRoot.rect.height < 650f;
            if (compact) width = Mathf.Min(width, PlayerMiniCardView.LineupSlotWidth);
            var board = (RectTransform)_workspaceRoot.Find("PlayerOrderBoard");
            if (board.rect.height <= 0f) return width;
            // 카드 폭에서 패널 높이를 계산해 카드 비율과 포지션 버튼 공간을 함께 확보한다.
            float chromeHeight = 40f + (_activePlayerGroup == PlayerGroupTab.Hitter ? 29f : 0f);
            float toolbarHeight = compact ? CareerUiTheme.RosterCompactToolbarHeight : CareerUiTheme.RosterToolbarHeight;
            float top = 1f - toolbarHeight / board.rect.height;
            float bottom = top - (width * 1.5f + chromeHeight) / board.rect.height;
            foreach (string name in AssignedPanelNames)
            {
                var panel = (RectTransform)board.Find(name);
                panel.anchorMin = new Vector2(panel.anchorMin.x, bottom);
                panel.anchorMax = new Vector2(panel.anchorMax.x, top);
            }
            foreach (string name in LowerPanelNames)
            {
                var panel = (RectTransform)board.Find(name);
                panel.anchorMax = new Vector2(panel.anchorMax.x, bottom - .01f);
            }
            return width;
        }

        private static float GetGridInnerWidth(GridLayoutGroup grid)
        {
            var content = (RectTransform)grid.transform.parent;
            var viewport = (RectTransform)content.parent;
            var layout = content.GetComponent<VerticalLayoutGroup>();
            return viewport.rect.width - (layout != null ? layout.padding.horizontal : 0f) - grid.padding.horizontal;
        }

        private void CreateSlotButton(Transform parent, OwnerLineupSlotModel slot)
        {
            PlayerMiniCardView card = PlayerMiniCardView.CreateRuntime(
                parent, $"{slot.Group}_{slot.Index}");
            card.UseLineupSlotLayout();
            card.UseRosterPresentation();
            BindSlotCard(card, slot);
            OwnerLineupSwapGroup group = slot.Group;
            int slotIndex = slot.Index;
            card.Selected += selected => HandleAssignedCardSelected(selected, FindCurrentSlot(group, slotIndex));
            card.DetailRequested += ShowCardDetail;

            Button button = card.GetComponent<Button>();
            _slotButtons.Add(button);
            _slotCards[button] = card;
            _assignedCardViews[CreateSlotKey(slot.Group, slot.Index)] = card;
        }

        private void BindSlotCard(PlayerMiniCardView card, OwnerLineupSlotModel slot)
        {
            OwnerRosterPlayerSnapshot player = slot.Player;
            string status = slot.Group == OwnerLineupSwapGroup.BattingOrder
                ? FindAssignedPosition(player) : slot.Group == OwnerLineupSwapGroup.Bench ? "벤치" :
                slot.Group == OwnerLineupSwapGroup.StarterRotation ? "선발" : slot.Index < 4 ? "불펜" :
                slot.Index == 4 ? "셋업" : "마무리";
            var model = new PlayerMiniCardModel(
                player?.CardId ?? $"empty:{slot.Group}:{slot.Index}",
                player?.DisplayName ?? "미지정",
                FormatCompactRole(slot.Label),
                player == null ? string.Empty : FormatCompactYear(player.OriginYear),
                player == null ? string.Empty : $"★ {player.Cost}",
                player == null || player.Edition == PlayerCardEdition.Normal
                    ? string.Empty
                    : OwnerRosterLineupPresentationBuilder.FormatEdition(player.Edition),
                status,
                visualState: PlayerMiniCardVisualState.Normal,
                frameEdition: player?.Edition,
                cost: player?.Cost, conditionLevel: player?.ConditionLevel,
                growthBadges: FindOwnedCard(player?.CardId)?.GrowthBadges);
            card.Bind(model, player == null ? null : PlayerPortraitSprites.GetDefault(player.NaturalPosition));
            card.SetTeamIdentity(FindOwnedCard(player?.CardId)?.TeamDisplayName);
        }

        private OwnerLineupSlotModel FindCurrentSlot(OwnerLineupSwapGroup group, int index)
        {
            IReadOnlyList<OwnerLineupSlotModel> slots = group switch
            {
                OwnerLineupSwapGroup.BattingOrder => _model.BattingOrder,
                OwnerLineupSwapGroup.Bench => _model.Bench,
                OwnerLineupSwapGroup.StarterRotation => _model.StarterRotation,
                OwnerLineupSwapGroup.ReliefPitching => _model.ReliefPitching,
                _ => null
            };
            return slots != null && index >= 0 && index < slots.Count ? slots[index] : null;
        }

        private static string CreateSlotKey(OwnerLineupSwapGroup group, int index) => $"{group}:{index}";

        private void HandleAssignedCardSelected(PlayerMiniCardModel selected, OwnerLineupSlotModel slot)
        {
            if (slot == null) return;
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
            PlayerMiniCardView card = PlayerMiniCardView.CreateRuntime(parent, $"Owned_{sourceIndex}");
            card.UseLineupSlotLayout();
            card.UseRosterPresentation();
            BindOwnedPlayerCard(card, player);
            card.Selected += selected => HandleOwnedCardSelected(selected, card);
            card.DetailRequested += ShowCardDetail;
            _ownedCardViews.Add(card);
        }

        private void BindOwnedPlayerCard(PlayerMiniCardView card, OwnerCollectionCardSnapshot player)
        {
            string assignment = FindOwnedCardAssignment(player.CardId);
            var model = new PlayerMiniCardModel(
                player.CardId,
                player.DisplayName,
                assignment ?? string.Empty,
                FormatCompactYear(player.OriginYear),
                $"★ {player.Cost}",
                player.Edition == PlayerCardEdition.Normal
                    ? string.Empty
                    : OwnerRosterLineupPresentationBuilder.FormatEdition(player.Edition),
                OwnerCollectionPresentationBuilder.FormatPlayerRole(player.Position, player.PitcherRole, player.IsPositionEvidenceMissing),
                frameEdition: player.Edition,
                cost: player.Cost, conditionLevel: player.ConditionLevel, growthBadges: player.GrowthBadges);
            card.Bind(model, PlayerPortraitSprites.GetDefault(player.Position));
            card.SetTeamIdentity(player.TeamDisplayName);
            card.SetAssignmentBadge(assignment, assignment == null && IsOtherCardAssigned(player));
        }

        private bool IsOtherCardAssigned(OwnerCollectionCardSnapshot player)
        {
            if (player == null) return false;
            // 미리보기 프리셋을 기준으로 연도·종류가 다른 카드도 동일 인물로 구분한다.
            foreach (OwnerCollectionCardSnapshot other in _model.Snapshot.OwnedPlayers)
                if (other.CardId != player.CardId &&
                    string.Equals(other.PlayerPersonId, player.PlayerPersonId, StringComparison.Ordinal) &&
                    FindOwnedCardAssignment(other.CardId) != null)
                    return true;
            return false;
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
            IReadOnlyList<OwnerCollectionCardSnapshot> details = ResolveCardDetails(cards);
            for (int index = 0; index < details.Count; index++)
                if (details[index].CardId == selected.PlayerId)
                {
                    UI_Popup_OwnerPlayerCard.Show(_workspaceRoot, details, index);
                    return;
                }
        }

        private IReadOnlyList<OwnerCollectionCardSnapshot> ResolveCardDetails(
            IReadOnlyList<OwnerCollectionCardSnapshot> cards)
        {
            if (_cardDetailResolver == null || cards.Count == 0) return cards;
            var cardIds = new string[cards.Count];
            for (int index = 0; index < cardIds.Length; index++) cardIds[index] = cards[index].CardId;
            return _cardDetailResolver(cardIds);
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

            List<OwnerCollectionCardSnapshot> filtered =
                GetFilteredOwnedPlayers(_activePlayerGroup == PlayerGroupTab.Pitcher);
            int firstCardIndex = _ownedPageIndex * OwnedCardsPerPage;
            int visibleCardCount = Math.Min(OwnedCardsPerPage, Math.Max(0, filtered.Count - firstCardIndex));
            return filtered.GetRange(firstCardIndex, visibleCardCount);
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
            return OwnerCollectionPresentationBuilder.FormatPosition(player.NaturalPosition, player.IsPositionEvidenceMissing);
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
            if (_positionSourceIndex >= 0) ClosePositionEditor();
            Button clicked = FindButton(slot.Group, slot.Index);
            if (!string.IsNullOrEmpty(_selectedOwnedCardId))
            {
                string incomingCardId = _selectedOwnedCardId;
                ClearSelection();
                RequestAssignment(slot.Group, slot.Index, incomingCardId);
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
                OwnerCollectionCardSnapshot before = FindOwnedCard(FindCurrentSlot(firstGroup, first)?.Player?.CardId);
                OwnerCollectionCardSnapshot after = FindOwnedCard(slot.Player?.CardId);
                SwapRequested?.Invoke(slot.Group, first, slot.Index);
                if (_hasPreview && before != null && after != null &&
                    FindCurrentSlot(firstGroup, first)?.Player?.CardId == after.CardId)
                    ShowComparison(before, after);
                return;
            }
            if (slot.Player != null)
                RequestAssignment(firstGroup, first, slot.Player.CardId);
        }

        private void HandleOwnedCardSelected(PlayerMiniCardModel selected, PlayerMiniCardView card)
        {
            if (_positionSourceIndex >= 0) ClosePositionEditor();
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
                RequestAssignment(group, index, selected.PlayerId);
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
            _previewStateText.text = IsOtherCardAssigned(FindOwnedCard(selected.PlayerId))
                ? $"{_selectedOwnedPlayerName} 선택 · 배치된 동일 선수의 슬롯을 선택해 교체하세요."
                : $"{_selectedOwnedPlayerName} 선택 · 배치할 슬롯을 선택하세요.";
            _previewStateText.color = CareerUiTheme.RosterAccent;
        }

        private void RequestAssignment(OwnerLineupSwapGroup group, int index, string incomingCardId)
        {
            OwnerCollectionCardSnapshot outgoing = FindOwnedCard(FindCurrentSlot(group, index)?.Player?.CardId);
            OwnerCollectionCardSnapshot incoming = FindOwnedCard(incomingCardId);
            AssignmentRequested?.Invoke(group, index, incomingCardId);
            if (!_hasPreview || outgoing == null || incoming == null ||
                FindCurrentSlot(group, index)?.Player?.CardId != incomingCardId) return;
            ShowComparison(outgoing, incoming);
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
            OwnerRuntimeUiFactory.ClearChildren(_analysisContent);
            SetAnalysisTitle("투수 상태");
            AddPositionExplanation(
                $"{pitcher.Slot.Player.DisplayName} · {pitcher.Slot.Label}\n\n" +
                $"{pitcher.ConditionText}\n{pitcher.WorkloadText}\n\n" +
                $"{pitcher.PitchesText}\n\n{pitcher.RecentRecordText}", 240f);
        }

        private void SelectAssigned(Button button, OwnerLineupSlotModel slot)
        {
            ClearSelection();
            _selectedButton = button;
            _selectedGroup = slot.Group;
            _selectedIndex = slot.Index;
            SetSelectionVisual(_selectedButton, true);
            _previewStateText.text = $"{slot.Label} 선택 · 교체할 보유 선수를 선택하세요.";
            _previewStateText.color = CareerUiTheme.RosterAccent;
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
                ? "① 교체할 자리 선택  →  ② 보유 선수 선택  →  ③ 배치 저장 · 포지션 버튼으로 수비 위치 변경"
                : "카드 선택으로 선수 상세 확인 · 선수 교체는 배치 편집 · 수비 변경은 카드 아래 포지션 선택";
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
                TextAnchor.MiddleLeft, CareerUiTheme.RosterAccent);
            text.gameObject.AddComponent<CareerUiPreserveTextColor>();
            LayoutElement layout = text.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 26f;
            layout.preferredHeight = 26f;
        }

        private static void ApplyRoleBoardPalette(Transform panel)
        {
            if (panel == null) return;
            SetImageColor(panel, CareerUiTheme.RosterSurface);
            SetImageColor(panel.Find("HeaderSurface"), CareerUiTheme.RosterSurfaceRaised);
            SetImageColor(panel.Find("HeaderAccent"), CareerUiTheme.RosterDivider);
            Transform title = panel.Find("HeaderSlot");
            if (title.GetComponent<CareerUiPreserveTextColor>() == null)
                title.gameObject.AddComponent<CareerUiPreserveTextColor>();
            SetTextColor(title, Color.white);
            // 불투명 표면으로 유지해 배경 구장 색이 본문 대비를 흐리지 않게 한다.
            panel.GetComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.DataImage);
            panel.Find("HeaderSurface").GetComponent<CareerUiVisualElement>()
                .Initialize(CareerUiVisualRole.DataImage);
            SetImageColor(panel.Find("ContentSafeRect/RoleScroll"), CareerUiTheme.RosterSurface);
            Outline border = panel.Find("ThinBorder").GetComponent<Outline>();
            border.effectColor = CareerUiTheme.RosterDivider;
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
