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
        private Button _activeRosterEditButton;
        private Button _previousPresetButton;
        private Button _nextPresetButton;
        private Button _hitterTabButton;
        private Button _pitcherTabButton;
        private Button[] _teamColorButtons;
        private Button[] _tacticButtons;
        private Button _selectedButton;
        private OwnerRosterLineupPresentationModel _model;
        private int _presetIndex;
        private PlayerGroupTab _activePlayerGroup = PlayerGroupTab.Hitter;
        private OwnerLineupSwapGroup? _selectedGroup;
        private int _selectedIndex = -1;
        private int _positionFilter;

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
            _summaryText.text = model.RosterSummaryText;
            _evaluationText.text = model.EvaluationText + "\n" + model.EvaluationBasisText;
            _validationText.text = string.Empty;
            _validationText.color = InspectorMessage;
            RenderActivePlayerGroup();
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
            if (_hitterTabButton != null) _hitterTabButton.onClick.RemoveAllListeners();
            if (_pitcherTabButton != null) _pitcherTabButton.onClick.RemoveAllListeners();
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
            _summaryText = AddText(validation.Content, "RosterSummary", 16, FontStyle.Bold, 64f);
            _evaluationText = AddText(validation.Content, "RosterEvaluation", 14, FontStyle.Normal, 144f);
            _evaluationText.GetComponent<LayoutElement>().minHeight = 144f;
            _validationText = AddText(validation.Content, "ValidationMessages", 14, FontStyle.Normal, 410f);

            _actionRoot = OwnerWorkspaceUiFactory.CreateRoot(actionBarHost, "OwnerRosterLineupActionBar", false);
            HorizontalLayoutGroup actions = OwnerWorkspaceUiFactory.AddHorizontalLayout(_actionRoot, CareerUiTheme.Space3);
            actions.padding = new RectOffset(16, 16, 4, 4);
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
            ApplyRoleBoardPalette(_workspaceRoot.Find("PlayerOrderBoard/PrimaryAssignedPanel"));
            ApplyRoleBoardPalette(_workspaceRoot.Find("PlayerOrderBoard/SecondaryAssignedPanel"));
            ApplyRoleBoardPalette(_workspaceRoot.Find("PlayerOrderBoard/OwnedPlayerPanel"));
            ApplyRoleBoardPalette(_workspaceRoot.Find("PlayerOrderBoard/ConditionAnalysisPanel"));
            ApplyRoleBoardPalette(setupPanel);
            ApplyRoleBoardPalette(closerPanel);
            ApplyInspectorPalette(_inspectorRoot.Find("ValidationPanel"));
            UpdatePlayerGroupTabs();
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

        private static void SetUpperPanel(RectTransform panel, float left, float right)
        {
            OwnerRuntimeUiFactory.SetAnchors(panel, new Vector2(left, 0.70f),
                new Vector2(right, 0.93f), Vector2.zero, Vector2.zero);
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
                RectTransform empty = CreateAnalysisSurface(gridRoot, "EmptySlot", new Color(0.78f, 0.79f, 0.80f));
                var outline = empty.gameObject.AddComponent<Outline>();
                outline.effectColor = Color.white;
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
            card.DetailRequested += ShowCardDetail;
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
            button.image.color = isSelected
                ? CareerUiTheme.ReferenceAccent
                : CareerUiTheme.ReferenceButton;
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
            SetImageColor(panel, RoleBoardSurface);
            SetImageColor(panel.Find("HeaderSurface"), RoleBoardSurface);
            SetImageColor(panel.Find("HeaderAccent"), RoleBoardBorder);
            SetTextColor(panel.Find("HeaderSlot"), CareerUiTheme.ReferenceText);
            SetImageColor(panel.Find("ContentSafeRect/RoleScroll"), CareerUiTheme.ReferencePanel);
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
