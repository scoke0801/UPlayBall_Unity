using System;
using System.Collections.Generic;
using Baseball.Core.Players;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    public sealed partial class UI_Scene_OwnerPowerUp
    {
        private readonly OwnerCardFilters _trainingOrigins = new OwnerCardFilters();
        private readonly List<OwnerCollectionCardSnapshot> _visibleTrainingCards = new List<OwnerCollectionCardSnapshot>();
        private RectTransform _trainingOriginRow;
        private InputField _trainingSearch;
        private Dropdown _trainingPosition;
        private Dropdown _trainingSort;
        private Button _trainingReset;
        private Button _trainingRosterPriority;
        private bool _isTrainingRosterPriority = true;
        private Text _trainingEmpty;
        private Text _trainingSelectionHint;
        private OwnerCardTrainingScreenSnapshot _trainingFilterSource;
        private static readonly PlayerPosition[] TrainingPositions =
        {
            PlayerPosition.Catcher, PlayerPosition.FirstBase, PlayerPosition.SecondBase,
            PlayerPosition.ThirdBase, PlayerPosition.Shortstop, PlayerPosition.LeftField,
            PlayerPosition.CenterField, PlayerPosition.RightField, PlayerPosition.DesignatedHitter,
            PlayerPosition.StartingPitcher, PlayerPosition.ReliefPitcher
        };

        private void BuildTrainingFilters(RectTransform parent)
        {
            RectTransform searchRow = CreateTrainingFilterRow(parent, "TrainingSearchRow");
            searchRow.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
            Image surface = OwnerRuntimeUiFactory.CreateImage("TrainingSearch", searchRow, CareerUiTheme.RosterBoard);
            SetTrainingSurface(surface, CareerUiTheme.RosterBoard);
            surface.raycastTarget = true;
            var size = surface.gameObject.AddComponent<LayoutElement>();
            size.minWidth = 100;
            size.flexibleWidth = 1;
            _trainingSearch = surface.gameObject.AddComponent<InputField>();
            _trainingSearch.targetGraphic = surface;
            _trainingSearch.characterLimit = 48;
            Text value = OwnerWorkspaceUiFactory.CreateText(surface.transform, "SearchText", "", 14,
                FontStyle.Normal, TextAnchor.MiddleLeft, CareerUiTheme.RosterText);
            Text placeholder = OwnerWorkspaceUiFactory.CreateText(surface.transform, "SearchPlaceholder", "선수 이름 검색", 14,
                FontStyle.Normal, TextAnchor.MiddleLeft, CareerUiTheme.RosterTextSecondary);
            OwnerRuntimeUiFactory.Stretch(value.rectTransform, new Vector2(12, 4), new Vector2(-12, -4));
            OwnerRuntimeUiFactory.Stretch(placeholder.rectTransform, new Vector2(12, 4), new Vector2(-12, -4));
            _trainingSearch.textComponent = value;
            _trainingSearch.placeholder = placeholder;
            OwnerDashboardStyle.SetDataInput(_trainingSearch);
            _trainingSearch.onValueChanged.AddListener(_ => ChangeTrainingFilters());
            _trainingReset = OwnerWorkspaceUiFactory.CreateButton(searchRow, "ResetTrainingFilters", "초기화", ResetTrainingFilters);
            var resetSize = _trainingReset.GetComponent<LayoutElement>();
            resetSize.minWidth = resetSize.preferredWidth = 72;
            resetSize.flexibleWidth = 0;
            SetPreferred(_trainingReset.GetComponent<RectTransform>(), 36);
            OwnerUiButtonSkin.SetBoardStyle(_trainingReset);

            RectTransform roleRow = CreateTrainingFilterRow(parent, "TrainingRoleRow");
            var labels = new List<string> { "전체 포지션", "타자 전체", "투수 전체" };
            foreach (PlayerPosition position in TrainingPositions)
                labels.Add(OwnerCollectionPresentationBuilder.FormatPlayerRole(position, null, false));
            _trainingPosition = OwnerCardFilters.CreateDropdown(roleRow, "TrainingPositionFilter", labels, 0);
            _trainingSort = OwnerCardFilters.CreateDropdown(roleRow, "TrainingSort",
                new List<string> { "코스트 높은 순", "코스트 낮은 순", "이름순", "최신 연도순" }, 0);
            StyleTrainingDropdown(_trainingPosition);
            StyleTrainingDropdown(_trainingSort);
            _trainingPosition.onValueChanged.AddListener(_ => ChangeTrainingFilters());
            _trainingSort.onValueChanged.AddListener(_ => ChangeTrainingFilters());
            _trainingOriginRow = CreateTrainingFilterRow(parent, "TrainingOriginFilters");
        }

        private void BuildTrainingListToolbar(RectTransform parent)
        {
            RectTransform row = CreateTrainingFilterRow(parent, "TrainingListToolbar");
            row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
            _trainingCardCount = TrainingText(row, "TrainingCardCount", 36, 12);
            LayoutElement countSize = _trainingCardCount.GetComponent<LayoutElement>();
            countSize.minWidth = 100;
            countSize.flexibleWidth = 1;
            _trainingRosterPriority = OwnerWorkspaceUiFactory.CreateButton(row,
                "TrainingRosterPriority", "배치 우선 켜짐", ToggleTrainingRosterPriority);
            LayoutElement buttonSize = _trainingRosterPriority.GetComponent<LayoutElement>();
            buttonSize.minWidth = buttonSize.preferredWidth = 112;
            buttonSize.flexibleWidth = 0;
            SetPreferred(_trainingRosterPriority.GetComponent<RectTransform>(), 36);
            _trainingRosterPriority.transform.Find("Label").GetComponent<Text>().fontSize = 12;
            OwnerUiButtonSkin.SetBoardStyle(_trainingRosterPriority);
            RefreshTrainingRosterPriority();
        }

        private void ToggleTrainingRosterPriority()
        {
            _isTrainingRosterPriority = !_isTrainingRosterPriority;
            ChangeTrainingFilters();
        }

        private void RefreshTrainingRosterPriority()
        {
            _trainingRosterPriority.transform.Find("Label").GetComponent<Text>().text =
                _isTrainingRosterPriority ? "배치 우선 켜짐" : "배치 우선 꺼짐";
            OwnerUiButtonSkin.SetSelected(_trainingRosterPriority, _isTrainingRosterPriority);
        }

        private static void StyleTrainingDropdown(Dropdown dropdown)
        {
            OwnerDashboardStyle.SetDataDropdown(dropdown);
            OwnerDashboardStyle.SetDataText(dropdown.transform.Find("DropdownArrow").GetComponent<Text>());
        }

        private static RectTransform CreateTrainingFilterRow(Transform parent, string name)
        {
            RectTransform row = OwnerRuntimeUiFactory.CreateRect(name, parent);
            SetPreferred(row, 36);
            OwnerWorkspaceUiFactory.AddHorizontalLayout(row, CareerUiTheme.Space2);
            return row;
        }

        private void BuildTrainingEmptyState()
        {
            var viewport = _trainingCardList.GetComponentInParent<ScrollRect>().viewport;
            _trainingEmpty = OwnerWorkspaceUiFactory.CreateText(viewport, "TrainingEmpty", "", 15,
                FontStyle.Normal, TextAnchor.MiddleCenter, CareerUiTheme.RosterTextSecondary);
            OwnerRuntimeUiFactory.Stretch(_trainingEmpty.rectTransform, new Vector2(16, 16), new Vector2(-16, -16));
            _trainingEmpty.gameObject.SetActive(false);
            _trainingSelectionHint = TrainingText(_trainingCardList.GetComponentInParent<ScrollRect>().transform.parent,
                "TrainingSelectionHint", 36, 12);
            _trainingSelectionHint.gameObject.SetActive(false);
        }

        private void BindTrainingFilterOptions(OwnerCardTrainingScreenSnapshot screen)
        {
            if (ReferenceEquals(_trainingFilterSource, screen)) return;
            _trainingFilterSource = screen;
            OwnerRuntimeUiFactory.ClearChildren(_trainingOriginRow);
            var cards = new List<OwnerCollectionCardSnapshot>(screen.Cards.Count);
            foreach (OwnerCardTrainingTargetSnapshot target in screen.Cards) cards.Add(target.Card);
            _trainingOrigins.Build(_trainingOriginRow, cards, ChangeTrainingFilters);
            foreach (Dropdown dropdown in _trainingOriginRow.GetComponentsInChildren<Dropdown>())
                StyleTrainingDropdown(dropdown);
        }

        private void ChangeTrainingFilters()
        {
            if (_snapshot == null) return;
            ScrollRect scroll = _trainingCardList.GetComponentInParent<ScrollRect>();
            scroll.StopMovement();
            _trainingCardList.anchoredPosition = Vector2.zero;
            RefreshTrainingCards();
        }

        private void ResetTrainingFilters()
        {
            _trainingSearch.SetTextWithoutNotify(string.Empty);
            _trainingPosition.SetValueWithoutNotify(0);
            _trainingSort.SetValueWithoutNotify(0);
            _isTrainingRosterPriority = true;
            _trainingOrigins.Reset();
            foreach (Dropdown dropdown in _trainingOriginRow.GetComponentsInChildren<Dropdown>())
                dropdown.SetValueWithoutNotify(0);
            ChangeTrainingFilters();
        }

        private void RefreshTrainingCards()
        {
            if (_snapshot == null) return;
            OwnerCardTrainingScreenSnapshot screen = _snapshot.Training;
            string query = _trainingSearch.text.Trim();
            _visibleTrainingCards.Clear();
            if (screen.State == OwnerPowerUpContentState.Ready)
                foreach (OwnerCardTrainingTargetSnapshot target in screen.Cards)
                {
                    OwnerCollectionCardSnapshot card = target.Card;
                    if (!_trainingOrigins.Matches(card) || !MatchesTrainingPosition(card)) continue;
                    if (query.Length > 0 && card.DisplayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    _visibleTrainingCards.Add(card);
                }
            // 요약만 정렬한다. 검색 때문에 전체 보유 카드의 훈련 Preview를 만들지 않는다.
            _visibleTrainingCards.Sort(CompareTrainingCards);
            _trainingGrid.Bind(_visibleTrainingCards, _selectedTrainingCardId, _snapshot.ResolveCard);
            _trainingEmpty.gameObject.SetActive(_visibleTrainingCards.Count == 0);
            _trainingEmpty.text = screen.State == OwnerPowerUpContentState.Ready
                ? (query.Length == 0 ? "조건에 맞는 선수가 없습니다." : $"‘{query}’ 검색 결과가 없습니다.") + "\n상단의 초기화로 전체 선수를 확인하세요."
                : screen.State == OwnerPowerUpContentState.Empty ? "아직 보유 선수가 없습니다.\n스카우트에서 선수를 영입하세요." : screen.ErrorMessage;
            bool hasFilter = query.Length > 0 || _trainingPosition.value != 0 || _trainingSort.value != 0
                || !_isTrainingRosterPriority;
            foreach (Dropdown dropdown in _trainingOriginRow.GetComponentsInChildren<Dropdown>()) hasFilter |= dropdown.value != 0;
            _trainingReset.interactable = hasFilter;
            OwnerUiButtonSkin.Apply(_trainingReset);
            RefreshTrainingRosterPriority();
            RefreshTrainingFilterSummary();
        }

        private bool MatchesTrainingPosition(OwnerCollectionCardSnapshot card)
        {
            int selected = _trainingPosition.value;
            if (selected == 0) return true;
            bool pitcher = card.Position == PlayerPosition.StartingPitcher || card.Position == PlayerPosition.ReliefPitcher;
            if (selected == 1) return !pitcher;
            if (selected == 2) return pitcher;
            return card.Position == TrainingPositions[selected - 3];
        }

        private int CompareTrainingCards(OwnerCollectionCardSnapshot left, OwnerCollectionCardSnapshot right)
        {
            // 배치 그룹 안에서도 사용자가 고른 정렬을 유지한다. 상세 조회는 필요하지 않다.
            if (_isTrainingRosterPriority && left.IsActiveRoster != right.IsActiveRoster)
                return left.IsActiveRoster ? -1 : 1;
            int comparison;
            switch (_trainingSort.value)
            {
                case 1: comparison = left.Cost.CompareTo(right.Cost); break;
                case 2: comparison = StringComparer.CurrentCulture.Compare(left.DisplayName, right.DisplayName); break;
                case 3: comparison = right.OriginYear.CompareTo(left.OriginYear); break;
                default: comparison = right.Cost.CompareTo(left.Cost); break;
            }
            if (comparison != 0) return comparison;
            comparison = StringComparer.CurrentCulture.Compare(left.DisplayName, right.DisplayName);
            return comparison != 0 ? comparison : StringComparer.Ordinal.Compare(left.CardId, right.CardId);
        }

        private void RefreshTrainingFilterSummary()
        {
            if (_snapshot == null) return;
            _trainingCardCount.text = $"표시 {_visibleTrainingCards.Count:N0} / 보유 {_snapshot.Training.Cards.Count:N0}장";
            bool selectedVisible = _visibleTrainingCards.Exists(card => card.CardId == _selectedTrainingCardId);
            bool hasSelection = _snapshot.Training.State == OwnerPowerUpContentState.Ready && !string.IsNullOrEmpty(_selectedTrainingCardId);
            _trainingSelectionHint.gameObject.SetActive(hasSelection && !selectedVisible);
            _trainingSelectionHint.text = "선택 선수는 필터 밖에 있습니다.\n중앙의 훈련 대상은 유지됩니다.";
        }
    }
}
