using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    public sealed partial class UI_Scene_OwnerRosterLineup
    {
        private readonly OwnerCardFilters _cardFilters = new OwnerCardFilters();
        private enum CostSortOrder { Default, Descending, Ascending }
        private CostSortOrder _costSortOrder = CostSortOrder.Descending;
        private PlayerCardEdition? _editionFilter;
        private string _playerSearch = string.Empty;
        private bool? _areFiltersExpanded;
        private Button _filterToggleButton;

        private void RefreshFilterVisibility()
        {
            if (_ownedHeader == null || _filterToggleButton == null) return;
            bool expanded = _areFiltersExpanded ?? _workspaceRoot.rect.height >= 650f;
            _ownedHeader.Find("OriginFilters").gameObject.SetActive(expanded);
            _ownedHeader.Find("PositionFilters").gameObject.SetActive(expanded);
            Text label = _filterToggleButton.GetComponentInChildren<Text>();
            string title = expanded ? "필터 접기" : "검색·필터";
            if (label.text != title) label.text = title;
        }
        private readonly List<Button> _positionButtons = new List<Button>();
        private int _positionSourceIndex = -1;
        private int _positionTargetIndex = -1;
        private Text _positionChangeText;
        private Button _applyPositionButton;
        private RectTransform _positionActions;

        private void RenderPositionButtons(RectTransform content)
        {
            RectTransform row = OwnerRuntimeUiFactory.CreateRect("DefensivePositions", content);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 26f;
            row.GetComponent<LayoutElement>().minWidth = 0f;
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(2, 2, 0, 0);
            layout.spacing = 6f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            foreach (OwnerLineupSlotModel slot in _model.BattingOrder)
            {
                int battingOrderIndex = slot.Index;
                Button button = OwnerWorkspaceUiFactory.CreateButton(row, "Position_" + slot.Index,
                    FormatPositionButton(FindAssignedPosition(slot.Player)) + " ▾",
                    () => OpenPositionEditorAtBattingOrderIndex(battingOrderIndex));
                button.interactable = slot.Player != null;
                var sizing = button.GetComponent<LayoutElement>();
                sizing.minWidth = sizing.preferredWidth = PlayerMiniCardView.LineupSlotWidth;
                sizing.minHeight = sizing.preferredHeight = 26f;
                sizing.flexibleWidth = 0f;
                Text label = button.GetComponentInChildren<Text>();
                label.fontSize = 11;
                _positionButtons.Add(button);
            }
        }

        private void RefreshPositionButtons()
        {
            if (_activePlayerGroup != PlayerGroupTab.Hitter) return;
            if (_positionButtons.Count != _model.BattingOrder.Count) return;
            for (int index = 0; index < _positionButtons.Count; index++)
            {
                Button button = _positionButtons[index];
                OwnerLineupSlotModel slot = _model.BattingOrder[index];
                button.interactable = slot.Player != null;
                button.GetComponentInChildren<Text>().text =
                    FormatPositionButton(FindAssignedPosition(slot.Player)) + " ▾";
            }
        }

        private void OpenPositionEditorAtBattingOrderIndex(int battingOrderIndex)
        {
            if (battingOrderIndex < 0 || battingOrderIndex >= _model.BattingOrder.Count) return;
            OpenPositionEditor(_model.BattingOrder[battingOrderIndex].Player?.CardId);
        }

        private void OpenPositionEditor(string cardId)
        {
            int sourceIndex = -1;
            for (int index = 0; index < _model.DefensiveLineup.Count; index++)
                if (_model.DefensiveLineup[index].Player?.CardId == cardId && cardId != null)
                    sourceIndex = index;
            if (sourceIndex < 0) return;
            SelectAnalysisTab(false);
            ResetPositionEditorLayout();
            ClearSelection();
            _positionSourceIndex = sourceIndex;
            _positionTargetIndex = -1;
            OwnerRuntimeUiFactory.ClearChildren(_analysisContent);
            SetAnalysisTitle("수비 위치 변경");
            OwnerLineupSlotModel source = _model.DefensiveLineup[sourceIndex];
            AddPositionExplanation(source.Player.DisplayName + " · 이동할 포지션 선택", 22f).fontStyle = FontStyle.Bold;
            AddPositionExplanation("타순을 유지하고 두 선수의 수비 위치를 맞바꿉니다.", 26f);

            // 타순 인덱스와 수비 슬롯 인덱스는 독립적이므로 실제 프리셋의 수비 슬롯을 사용한다.
            for (int start = 0; start < _model.DefensiveLineup.Count; start += 3)
            {
                RectTransform row = OwnerRuntimeUiFactory.CreateRect("PositionChoices", _analysisContent);
                row.gameObject.AddComponent<LayoutElement>().preferredHeight = 36f;
                var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = 4f;
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                for (int index = start; index < Math.Min(start + 3, _model.DefensiveLineup.Count); index++)
                {
                    int targetIndex = index;
                    OwnerLineupSlotModel target = _model.DefensiveLineup[index];
                    string position = FormatPositionName(_model.Snapshot.Preset.StartingLineupSlots[index].Position);
                    Button choice = OwnerWorkspaceUiFactory.CreateButton(row, "ChoosePosition_" + index,
                        position + (index == sourceIndex ? " · 현재" : string.Empty) + "\n" +
                        (target.Player?.DisplayName ?? "선수 정보 없음"), () => SelectPositionTarget(targetIndex));
                    choice.GetComponent<LayoutElement>().minWidth = 0f;
                    choice.GetComponent<LayoutElement>().flexibleWidth = 1f;
                    choice.GetComponent<LayoutElement>().minHeight = 36f;
                    choice.GetComponent<LayoutElement>().preferredHeight = 36f;
                    choice.GetComponentInChildren<Text>().fontSize = 12;
                    choice.interactable = index != sourceIndex && target.Player != null;
                    SetPlayerGroupTabVisual(choice, index == sourceIndex);
                }
            }
            _positionChangeText = AddPositionExplanation("변경할 포지션을 선택하세요.", 54f);
            // 좁은 해상도에서도 선택 목록을 스크롤하며 적용·닫기 버튼에 항상 접근할 수 있게 한다.
            RectTransform viewport = (RectTransform)_analysisContent.parent;
            viewport.offsetMin = new Vector2(viewport.offsetMin.x, 44f);
            _positionActions = CreateInspectorControlRow(viewport.parent, "PositionActions");
            OwnerRuntimeUiFactory.SetAnchors(_positionActions, Vector2.zero, new Vector2(1f, 0f),
                Vector2.zero, new Vector2(0f, 40f));
            _applyPositionButton = OwnerWorkspaceUiFactory.CreateButton(_positionActions, "ApplyPosition", "변경안에 적용", ApplyPositionChange);
            _applyPositionButton.interactable = false;
            OwnerWorkspaceUiFactory.CreateButton(_positionActions, "ClosePosition", "닫기", ClosePositionEditor);
            foreach (Button button in _analysisContent.GetComponentsInChildren<Button>())
                OwnerUiButtonSkin.SetBoardStyle(button);
            foreach (Button button in _positionActions.GetComponentsInChildren<Button>())
                OwnerUiButtonSkin.SetBoardStyle(button);
        }

        private Text AddPositionExplanation(string message, float height)
        {
            Text text = OwnerWorkspaceUiFactory.CreateText(_analysisContent, "PositionExplanation", message,
                14, FontStyle.Normal, TextAnchor.UpperLeft, CareerUiTheme.RosterText);
            text.gameObject.AddComponent<CareerUiPreserveTextColor>();
            text.gameObject.AddComponent<LayoutElement>().minHeight = height;
            return text;
        }

        private static string FormatPositionButton(string position)
        {
            return position.Replace("1루수", "1루").Replace("2루수", "2루").Replace("3루수", "3루")
                .Replace("유격수", "유격").Replace("좌익수", "좌익").Replace("중견수", "중견")
                .Replace("우익수", "우익").Replace("지명타자", "지명");
        }

        private void SelectPositionTarget(int targetIndex)
        {
            if (_positionSourceIndex < 0 || targetIndex == _positionSourceIndex) return;
            _positionTargetIndex = targetIndex;
            OwnerLineupSlotModel source = _model.DefensiveLineup[_positionSourceIndex];
            OwnerLineupSlotModel target = _model.DefensiveLineup[targetIndex];
            var slots = _model.Snapshot.Preset.StartingLineupSlots;
            string from = FormatPositionName(slots[_positionSourceIndex].Position);
            string to = FormatPositionName(slots[targetIndex].Position);
            _positionChangeText.text =
                $"{source.Player.DisplayName}  {from} → {to}  (주 포지션: {OwnerCollectionPresentationBuilder.FormatPosition(source.Player.NaturalPosition, source.Player.IsPositionEvidenceMissing)})\n" +
                $"{target.Player.DisplayName}  {to} → {from}  (주 포지션: {OwnerCollectionPresentationBuilder.FormatPosition(target.Player.NaturalPosition, target.Player.IsPositionEvidenceMissing)})\n" +
                "적용 후 수비 배치 경고를 확인하고 하단 ‘배치 저장’으로 확정하세요.";
            foreach (Button button in _analysisContent.GetComponentsInChildren<Button>())
                if (button.name.StartsWith("ChoosePosition_", StringComparison.Ordinal))
                    SetPlayerGroupTabVisual(button, button.name == "ChoosePosition_" + targetIndex);
            _applyPositionButton.interactable = true;
        }

        private void ApplyPositionChange()
        {
            if (_positionSourceIndex < 0 || _positionTargetIndex < 0) return;
            int source = _positionSourceIndex;
            int target = _positionTargetIndex;
            ClosePositionEditor();
            SwapRequested?.Invoke(OwnerLineupSwapGroup.DefensiveLineup, source, target);
        }

        private void ClosePositionEditor()
        {
            _positionSourceIndex = _positionTargetIndex = -1;
            _positionChangeText = null;
            _applyPositionButton = null;
            ResetPositionEditorLayout();
            OwnerRuntimeUiFactory.ClearChildren(_analysisContent);
            SetAnalysisTitle("편성 분석");
            RenderRosterChart(_analysisContent, _model.BattingOrder, false);
            RenderDefensiveWarnings();
        }

        private void RenderDefensiveWarnings()
        {
            int warningCount = 0;
            foreach (OwnerLineupSlotModel slot in _model.DefensiveLineup)
                if (slot.HasWarning) warningCount++;
            AddSectionTitle(_analysisContent, warningCount > 0 ? $"수비 배치 주의 · {warningCount}명" : "수비 배치 · 경고 없음");
            foreach (OwnerLineupSlotModel slot in _model.DefensiveLineup)
                if (slot.HasWarning)
                {
                    string position = FormatPositionName(_model.Snapshot.Preset.StartingLineupSlots[slot.Index].Position);
                    Text text = AddPositionExplanation($"{slot.Player?.DisplayName} · {position}\n{slot.WarningText}", 64f);
                    text.color = CareerUiTheme.Warning;
                    string cardId = slot.Player?.CardId;
                    Button action = OwnerWorkspaceUiFactory.CreateButton(_analysisContent,
                        "ReviewPosition_" + slot.Index, "수비 위치 확인", () => OpenPositionEditor(cardId));
                    action.interactable = cardId != null;
                    OwnerUiButtonSkin.SetBoardStyle(action);
                }
        }

        private void SetAnalysisTitle(string title)
        {
            if (_isComparisonTab) title = "교체 전후 비교";
            _workspaceRoot.Find("PlayerOrderBoard/ConditionAnalysisPanel/HeaderSlot").GetComponent<Text>().text = title;
        }

        private void ResetPositionEditorLayout()
        {
            if (_positionActions != null)
            {
                _positionActions.gameObject.SetActive(false);
                OwnerWorkspaceUiFactory.DestroyOwnedRoot(_positionActions);
                _positionActions = null;
            }
            RectTransform viewport = (RectTransform)_analysisContent.parent;
            viewport.offsetMin = new Vector2(viewport.offsetMin.x, 0f);
            // 숨겨진 Workspace도 Bind에서 먼저 갱신하므로 비활성 부모의 ScrollRect까지 찾는다.
            viewport.GetComponentInParent<ScrollRect>(true).verticalNormalizedPosition = 1f;
        }
        private static readonly PlayerCardEdition?[] EditionFilters =
        {
            null, PlayerCardEdition.GoldenGlove, PlayerCardEdition.Normal,
            PlayerCardEdition.Mvp, PlayerCardEdition.AllStar, PlayerCardEdition.Rare,
            PlayerCardEdition.Ex, PlayerCardEdition.CareerHigh, PlayerCardEdition.Legend
        };

        private static void CompactPanel(RectTransform panel)
        {
            RectTransform accent = (RectTransform)panel.Find("HeaderAccent");
            accent.gameObject.SetActive(true);
            accent.offsetMin = new Vector2(1f, -27f);
            accent.offsetMax = new Vector2(-1f, -25f);
            RectTransform header = (RectTransform)panel.Find("HeaderSlot");
            header.offsetMin = new Vector2(10f, -24f);
            header.offsetMax = new Vector2(-6f, -2f);
            header.GetComponent<Text>().fontSize = 14;
            RectTransform surface = (RectTransform)panel.Find("HeaderSurface");
            surface.offsetMin = new Vector2(1f, -26f);
            RectTransform safe = (RectTransform)panel.Find("ContentSafeRect");
            safe.offsetMin = new Vector2(3f, 3f);
            safe.offsetMax = new Vector2(-3f, -27f);
            var layout = safe.GetComponentInChildren<VerticalLayoutGroup>();
            if (layout != null) { layout.padding = new RectOffset(3, 3, 3, 3); layout.spacing = 3f; }
        }

        private void RenderPositionFilters(RectTransform content, bool pitcher)
        {
            RectTransform origins = OwnerRuntimeUiFactory.CreateRect("OriginFilters", content);
            origins.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;
            var originLayout = origins.gameObject.AddComponent<HorizontalLayoutGroup>();
            originLayout.spacing = 6;
            originLayout.childControlWidth = true;
            originLayout.childControlHeight = true;
            BuildPlayerSearch(origins);
            _cardFilters.Build(origins, _model.Snapshot.OwnedPlayers, HandleOwnedPlayerFilterChanged);
            var editionLabels = new List<string> { "전체 종류" };
            for (int index = 1; index < EditionFilters.Length; index++)
                editionLabels.Add(OwnerCollectionPresentationBuilder.FormatEdition(EditionFilters[index].Value));
            Dropdown editionDropdown = OwnerCardFilters.CreateDropdown(origins, "EditionFilter", editionLabels,
                Array.IndexOf(EditionFilters, _editionFilter));
            editionDropdown.onValueChanged.AddListener(index =>
            {
                _editionFilter = EditionFilters[index];
                HandleOwnedPlayerFilterChanged();
            });
            Dropdown costDropdown = OwnerCardFilters.CreateDropdown(origins, "CostSort",
                new List<string> { "기본 순서", "코스트 높은 순", "코스트 낮은 순" }, (int)_costSortOrder);
            costDropdown.onValueChanged.AddListener(index =>
            {
                _costSortOrder = (CostSortOrder)index;
                HandleOwnedPlayerFilterChanged();
            });
            string[] labels = pitcher ? new[] { "전체", "선발", "불펜", "셋업", "마무리" } :
                new[] { "전체", "포수", "1루수", "2루수", "3루수", "유격수", "외야수" };
            RectTransform row = OwnerRuntimeUiFactory.CreateRect("PositionFilters", content);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 26;
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            for (int index = 0; index < labels.Length; index++)
            {
                int filter = index;
                Button button = OwnerWorkspaceUiFactory.CreateButton(row, "Filter" + index, labels[index], () =>
                {
                    _positionFilter = filter;
                    HandleOwnedPlayerFilterChanged();
                });
                var sizing = button.GetComponent<LayoutElement>();
                sizing.minWidth = 0; sizing.preferredWidth = 60; sizing.flexibleWidth = 1;
                sizing.minHeight = sizing.preferredHeight = 32f;
                sizing.flexibleHeight = 0f;
                Text text = button.GetComponentInChildren<Text>();
                text.fontSize = 11;
                SetPlayerGroupTabVisual(button, filter == _positionFilter);
            }
        }

        private void HandleOwnedPlayerFilterChanged()
        {
            _ownedPageIndex = 0;
            RenderActivePlayerGroup();
        }

        private bool MatchesFilter(OwnerCollectionCardSnapshot card, bool pitcher)
        {
            if (IsPitcher(card) != pitcher || !_cardFilters.Matches(card)) return false;
            if (_playerSearch.Length > 0 && card.DisplayName.IndexOf(_playerSearch, StringComparison.CurrentCultureIgnoreCase) < 0) return false;
            if (_editionFilter.HasValue && card.Edition != _editionFilter.Value) return false;
            if (_positionFilter == 0) return true;
            if (pitcher)
            {
                if (_positionFilter == 1) return card.Position == PlayerPosition.StartingPitcher;
                // 보유 카드의 보직으로 검색해야 미배치 셋업·마무리도 찾을 수 있다.
                if (_positionFilter == 3) return card.PitcherRole == PitcherRole.Setup;
                if (_positionFilter == 4) return card.PitcherRole == PitcherRole.Closer;
                return card.Position == PlayerPosition.ReliefPitcher;
            }
            if (card.IsPositionEvidenceMissing) return false;
            return _positionFilter switch
            {
                1 => card.Position == PlayerPosition.Catcher,
                2 => card.Position == PlayerPosition.FirstBase,
                3 => card.Position == PlayerPosition.SecondBase,
                4 => card.Position == PlayerPosition.ThirdBase,
                5 => card.Position == PlayerPosition.Shortstop,
                6 => card.Position == PlayerPosition.LeftField || card.Position == PlayerPosition.CenterField || card.Position == PlayerPosition.RightField,
                _ => true
            };
        }

        private List<OwnerCollectionCardSnapshot> GetFilteredOwnedPlayers(bool pitcher)
        {
            var cards = new List<OwnerCollectionCardSnapshot>();
            foreach (OwnerCollectionCardSnapshot card in _model.Snapshot.OwnedPlayers)
                if (MatchesFilter(card, pitcher)) cards.Add(card);
            if (_costSortOrder != CostSortOrder.Default)
                cards.Sort((left, right) =>
                {
                    int costComparison = _costSortOrder == CostSortOrder.Ascending
                        ? left.Cost.CompareTo(right.Cost) : right.Cost.CompareTo(left.Cost);
                    return costComparison != 0 ? costComparison : string.CompareOrdinal(left.CardId, right.CardId);
                });
            return cards;
        }

        private void BuildPlayerSearch(Transform parent)
        {
            GameObject root = DefaultControls.CreateInputField(new DefaultControls.Resources());
            root.name = "PlayerSearch";
            root.transform.SetParent(parent, false);
            root.AddComponent<CareerUiPreserveTextColor>();
            var sizing = root.AddComponent<LayoutElement>();
            sizing.minWidth = 100f;
            sizing.preferredWidth = 140f;
            sizing.flexibleWidth = 1f;
            sizing.minHeight = 28f;
            InputField input = root.GetComponent<InputField>();
            foreach (Text text in root.GetComponentsInChildren<Text>())
            {
                text.font = Baseball.Presentation.UI.UIProjectFonts.Default;
                text.fontSize = 12;
                text.color = CareerUiTheme.ReferenceText;
            }
            ((Text)input.placeholder).text = "선수 이름 검색";
            input.SetTextWithoutNotify(_playerSearch);
            // 입력 중 계층을 다시 만들면 한글 조합과 포커스를 잃으므로 제출 시 필터를 적용한다.
            input.onEndEdit.AddListener(value =>
            {
                string query = value.Trim();
                if (_playerSearch == query) return;
                _playerSearch = query;
                HandleOwnedPlayerFilterChanged();
            });
        }

        private static void RenderRosterChart(RectTransform content, IReadOnlyList<OwnerLineupSlotModel> slots, bool pitcher)
        {
            AddSectionTitle(content, pitcher ? "투수 컨디션 · 역할별 비교" : "타선 컨디션 · 타순별 비교");
            RectTransform chart = OwnerRuntimeUiFactory.CreateRect("RosterChart", content);
            LayoutElement sizing = chart.gameObject.AddComponent<LayoutElement>();
            sizing.minHeight = sizing.preferredHeight = 250f;
            RectTransform plot = OwnerRuntimeUiFactory.CreateRect("Plot", chart);
            OwnerRuntimeUiFactory.SetAnchors(plot, Vector2.zero, Vector2.one,
                new Vector2(32f, 64f), new Vector2(-8f, -32f));
            var graphic = plot.gameObject.AddComponent<UIRosterConditionPlot>();
            var values = new float[slots.Count];
            var valid = new bool[slots.Count];
            var levels = new int[slots.Count];
            for (int index = 0; index < slots.Count; index++)
            {
                values[index] = slots[index].Player?.Condition ?? 0;
                valid[index] = slots[index].Player != null;
                levels[index] = slots[index].Player?.ConditionLevel ?? 0;
                float left = (float)index / slots.Count;
                float right = (float)(index + 1) / slots.Count;
                Text label = CreateConditionChartText(plot, "Order" + index,
                    FormatCompactRole(slots[index].Label), 11, CareerUiTheme.RosterText);
                OwnerRuntimeUiFactory.SetAnchors(label.rectTransform, new Vector2(left, 0f), new Vector2(right, 0f),
                    new Vector2(0f, -28f), new Vector2(0f, -8f));
                Text state = CreateConditionChartText(plot, "Status" + index,
                    valid[index] ? PlayerCardConditionSprites.GetLabel(levels[index]) : "미배치",
                    10, PlayerCardConditionSprites.GetColor(levels[index]));
                OwnerRuntimeUiFactory.SetAnchors(state.rectTransform, new Vector2(left, 0f), new Vector2(right, 0f),
                    new Vector2(0f, -52f), new Vector2(0f, -28f));
                Text value = CreateConditionChartText(plot, "Value" + index,
                    valid[index] ? values[index].ToString("0") : "—", 13, CareerUiTheme.RosterText);
                OwnerRuntimeUiFactory.SetAnchors(value.rectTransform, new Vector2(left, 1f), new Vector2(right, 1f),
                    new Vector2(0f, 4f), new Vector2(0f, 24f));
            }
            graphic.Bind(values, valid, levels);
            for (int tick = 0; tick <= 100; tick += 50)
            {
                Text axis = CreateConditionChartText(plot, "Tick" + tick, tick.ToString(),
                    10, CareerUiTheme.RosterTextSecondary);
                OwnerRuntimeUiFactory.SetAnchors(axis.rectTransform, new Vector2(0f, tick / 100f),
                    new Vector2(0f, tick / 100f), new Vector2(-32f, -8f), new Vector2(-4f, 8f));
            }
            Text caption = CreateConditionChartText(content, "ConditionCaption",
                "경기 컨디션은 수비 위치와 선수 궁합에 따라 변동됩니다.",
                11, CareerUiTheme.RosterTextSecondary);
            caption.alignment = TextAnchor.MiddleLeft;
            caption.horizontalOverflow = HorizontalWrapMode.Wrap;
            caption.gameObject.AddComponent<LayoutElement>().minHeight = 36f;
        }

        private static Text CreateConditionChartText(Transform parent, string name, string value, int size, Color color)
        {
            Text text = CreateAnalysisText(parent, name, value, size, FontStyle.Normal, TextAnchor.MiddleCenter);
            text.resizeTextForBestFit = false;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }
    }
}
