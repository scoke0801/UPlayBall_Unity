using System;
using System.Collections.Generic;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using Baseball.Core.Growth;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>실제 보유 카드를 검색·정렬하고 공용 Mini Card로 선택하는 구단주 Collection 화면이다.</summary>
    [DisallowMultipleComponent]
    public sealed class UI_Scene_OwnerCollection : MonoBehaviour, IUiCancelHandler
    {
        private readonly List<PlayerMiniCardView> _cardViews = new List<PlayerMiniCardView>();
        private readonly OwnerCardFilters _cardFilters = new OwnerCardFilters();
        private RectTransform _originFilters;
        private RectTransform _workspaceRoot;
        private RectTransform _inspectorRoot;
        private RectTransform _actionRoot;
        private RectTransform _gridContent;
        private RectTransform _gridViewport;
        private ScrollRect _gridScroll;
        private InputField _searchInput;
        private Text _countText;
        private Text _emptyText;
        private Text _inspectorText;
        private PlayerMiniCardView _inspectorCard;
        private Text _feedbackText;
        private OwnerCollectionSnapshot _snapshot;
        private OwnerCollectionPresentationModel _model;
        private OwnerCollectionSort _sort = OwnerCollectionSort.Name;
        private string _selectedCardId = string.Empty;
        private int _trainingIndex;
        private int _studyIndex;
        private string _pendingEnhancementCardId = string.Empty;
        private int _gridColumns = 4;
        private int _firstVisibleIndex = -1;
        private Vector2 _lastViewportSize;

        private static readonly PlayerAbility[] BatterTrainingAbilities =
            { PlayerAbility.Contact, PlayerAbility.Power, PlayerAbility.Speed, PlayerAbility.Bunt, PlayerAbility.Defense, PlayerAbility.BatterMental };
        private static readonly PlayerAbility[] PitcherTrainingAbilities =
            { PlayerAbility.Stamina, PlayerAbility.Velocity, PlayerAbility.Stuff, PlayerAbility.Breaking, PlayerAbility.Control, PlayerAbility.PitcherMental };
        private static readonly string[] BatterStudyPrograms =
            { "study_contact", "study_power", "study_defense", "study_batter_allround" };
        private static readonly string[] PitcherStudyPrograms =
            { "study_velocity", "study_command", "study_breaking", "study_stamina" };

        public event Action<string> EnhancementRequested;
        public event Action<string> DuplicateSaleRequested;
        public event Action<string, string> TrainingRequested;
        public event Action<string, string> StudyRequested;
        public event Action<string> SkillBlockAutoPlaceRequested;
        public event Action<string> SkillBlockRemoveRequested;

        public static UI_Scene_OwnerCollection CreateRuntime(
            RectTransform workspaceHost,
            RectTransform inspectorHost,
            RectTransform actionBarHost)
        {
            if (workspaceHost == null) throw new ArgumentNullException(nameof(workspaceHost));
            if (inspectorHost == null) throw new ArgumentNullException(nameof(inspectorHost));
            if (actionBarHost == null) throw new ArgumentNullException(nameof(actionBarHost));
            var view = new GameObject(nameof(UI_Scene_OwnerCollection)).AddComponent<UI_Scene_OwnerCollection>();
            view.Build(workspaceHost, inspectorHost, actionBarHost);
            return view;
        }

        public void Bind(OwnerCollectionSnapshot snapshot)
        {
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            OwnerRuntimeUiFactory.ClearChildren(_originFilters);
            _cardFilters.Build(_originFilters, snapshot.Cards, RefreshCards);
            _pendingEnhancementCardId = string.Empty;
            RefreshCards();
            OwnerCollectionCardSnapshot selected = GetSelectedCard();
            if (selected == null)
            {
                _selectedCardId = string.Empty;
                ShowNoSelection();
            }
            else
            {
                ShowInspector(selected);
            }
        }

        public void SetVisible(bool visible)
        {
            if (_workspaceRoot != null) _workspaceRoot.gameObject.SetActive(visible);
            if (_inspectorRoot != null) _inspectorRoot.gameObject.SetActive(visible);
            if (_actionRoot != null) _actionRoot.gameObject.SetActive(visible);
        }

        /// <summary>두 번 누르기 방식의 강화 확인 단계가 있으면 해당 작업만 취소한다.</summary>
        public bool TryHandleCancel()
        {
            if (string.IsNullOrEmpty(_pendingEnhancementCardId))
                return false;
            _pendingEnhancementCardId = string.Empty;
            SetFeedback("강화 확인을 취소했습니다.", false);
            return true;
        }

        public void SetFeedback(string message, bool isError)
        {
            if (_feedbackText == null) return;
            _feedbackText.text = message ?? string.Empty;
            _feedbackText.color = isError ? CareerUiTheme.Loss : CareerUiTheme.Success;
        }

        private void Build(RectTransform workspaceHost, RectTransform inspectorHost, RectTransform actionBarHost)
        {
            _workspaceRoot = OwnerWorkspaceUiFactory.CreateRoot(workspaceHost, "OwnerCollectionWorkspace", true);
            OwnerWorkspaceUiFactory.Panel collection = OwnerWorkspaceUiFactory.CreatePanel(
                _workspaceRoot, "CollectionPanel", "보유 선수 컬렉션");
            OwnerRuntimeUiFactory.Stretch(collection.Root,
                new Vector2(CareerUiTheme.Space4, CareerUiTheme.Space4),
                new Vector2(-CareerUiTheme.Space4, -CareerUiTheme.Space4));
            BuildFilterBar(collection.Content);
            BuildCardGrid(collection.Content);

            _inspectorRoot = OwnerWorkspaceUiFactory.CreateRoot(inspectorHost, "OwnerCollectionInspector", false);
            OwnerWorkspaceUiFactory.Panel inspector = OwnerWorkspaceUiFactory.CreatePanel(
                _inspectorRoot, "SelectedCardPanel", "선택 선수");
            OwnerWorkspaceUiFactory.Stretch(inspector.Root);
            _inspectorText = OwnerWorkspaceUiFactory.CreateText(
                inspector.Content, "SelectedCardDetails", string.Empty, 15, FontStyle.Normal,
                TextAnchor.UpperLeft, CareerUiTheme.TextPrimary);
            OwnerRuntimeUiFactory.SetAnchors(_inspectorText.rectTransform, Vector2.zero, new Vector2(1f, 0.50f),
                new Vector2(8f, 4f), new Vector2(-8f, -4f));
            _inspectorCard = PlayerMiniCardView.CreateRuntime(inspector.Content, "SelectedCardPreview");
            OwnerRuntimeUiFactory.SetAnchors(_inspectorCard.GetComponent<RectTransform>(),
                new Vector2(0.16f, 0.52f), new Vector2(0.84f, 0.98f), Vector2.zero, Vector2.zero);
            _inspectorCard.DetailRequested += ShowCardDetail;
            _inspectorCard.gameObject.SetActive(false);

            _actionRoot = OwnerWorkspaceUiFactory.CreateRoot(actionBarHost, "OwnerCollectionActionBar", false);
            HorizontalLayoutGroup actions = OwnerWorkspaceUiFactory.AddHorizontalLayout(_actionRoot, CareerUiTheme.Space3);
            actions.padding = new RectOffset(16, 16, 4, 4);
            _feedbackText = OwnerWorkspaceUiFactory.CreateText(
                _actionRoot, "ActionFeedback", "카드를 선택해 중복 강화 또는 판매를 결정합니다.",
                14, FontStyle.Normal, TextAnchor.MiddleLeft, CareerUiTheme.TextSecondary);
            OwnerWorkspaceUiFactory.SetFlexible(_feedbackText.rectTransform, 1f, 0f);
            CreateAction("Enhancement", "중복 1장 강화", () =>
            {
                if (string.IsNullOrEmpty(_selectedCardId)) return;
                if (!string.Equals(_pendingEnhancementCardId, _selectedCardId, StringComparison.Ordinal))
                {
                    _pendingEnhancementCardId = _selectedCardId;
                    SetFeedback("강화 결과는 전 능력치 +1, 재료는 중복 1장입니다. 같은 버튼을 다시 누르면 확정합니다.", false);
                    return;
                }
                _pendingEnhancementCardId = string.Empty;
                EnhancementRequested?.Invoke(_selectedCardId);
            });
            CreateAction("Sale", "중복 1장 판매", () =>
            {
                if (!string.IsNullOrEmpty(_selectedCardId)) DuplicateSaleRequested?.Invoke(_selectedCardId);
            });
            CreateAction("TrainingCycle", "훈련 선택", CycleTraining);
            CreateAction("TrainingApply", "훈련 실행", RequestTraining);
            CreateAction("StudyCycle", "유학 선택", CycleStudy);
            CreateAction("StudyStart", "유학 출발", RequestStudy);
            CreateAction("SkillPlace", "블록 장착", () =>
            {
                if (!string.IsNullOrEmpty(_selectedCardId)) SkillBlockAutoPlaceRequested?.Invoke(_selectedCardId);
            });
            CreateAction("SkillRemove", "블록 해제", () =>
            {
                if (!string.IsNullOrEmpty(_selectedCardId)) SkillBlockRemoveRequested?.Invoke(_selectedCardId);
            });
            CareerUiSkin.Apply(_workspaceRoot);
            CareerUiSkin.Apply(_inspectorRoot);
            CareerUiSkin.Apply(_actionRoot);
        }

        private void BuildFilterBar(RectTransform parent)
        {
            _originFilters = OwnerRuntimeUiFactory.CreateRect("OriginFilters", parent);
            OwnerRuntimeUiFactory.SetAnchors(_originFilters, new Vector2(0, 1), Vector2.one,
                new Vector2(0, -82), new Vector2(0, -50));
            OwnerWorkspaceUiFactory.AddHorizontalLayout(_originFilters, 6);
            RectTransform filter = OwnerRuntimeUiFactory.CreateRect("FilterBar", parent);
            OwnerRuntimeUiFactory.SetAnchors(filter, new Vector2(0f, 1f), Vector2.one,
                new Vector2(0f, -46f), Vector2.zero);
            HorizontalLayoutGroup layout = OwnerWorkspaceUiFactory.AddHorizontalLayout(filter, CareerUiTheme.Space2);
            layout.childForceExpandWidth = false;
            _searchInput = CreateSearchInput(filter);
            _searchInput.onValueChanged.AddListener(HandleSearchChanged);
            CreateSortButton(filter, "SortName", "이름", OwnerCollectionSort.Name);
            CreateSortButton(filter, "SortPosition", "포지션", OwnerCollectionSort.Position);
            CreateSortButton(filter, "SortCost", "비용", OwnerCollectionSort.Cost);
            CreateSortButton(filter, "SortEdition", "카드 종류", OwnerCollectionSort.Edition);
            _countText = OwnerWorkspaceUiFactory.CreateText(
                filter, "Count", string.Empty, 13, FontStyle.Bold, TextAnchor.MiddleRight,
                CareerUiTheme.TextSecondary);
            LayoutElement countLayout = _countText.gameObject.AddComponent<LayoutElement>();
            countLayout.minWidth = 130f;
            countLayout.flexibleWidth = 1f;
        }

        private void BuildCardGrid(RectTransform parent)
        {
            Image surface = OwnerRuntimeUiFactory.CreateImage("CardScroll", parent, CareerUiTheme.PanelDark);
            OwnerRuntimeUiFactory.SetAnchors(surface.rectTransform, Vector2.zero, Vector2.one,
                Vector2.zero, new Vector2(0f, -90f));
            _gridScroll = surface.gameObject.AddComponent<ScrollRect>();
            _gridScroll.horizontal = false;
            _gridScroll.vertical = true;
            _gridScroll.movementType = ScrollRect.MovementType.Clamped;
            _gridScroll.scrollSensitivity = 28f;

            Image viewportImage = OwnerRuntimeUiFactory.CreateImage(
                "Viewport", _gridScroll.transform, new Color(0f, 0f, 0f, 0.01f));
            _gridViewport = viewportImage.rectTransform;
            OwnerRuntimeUiFactory.Stretch(_gridViewport);
            _gridViewport.gameObject.AddComponent<RectMask2D>();
            _gridContent = OwnerRuntimeUiFactory.CreateRect("Content", _gridViewport);
            _gridContent.anchorMin = new Vector2(0f, 1f);
            _gridContent.anchorMax = Vector2.one;
            _gridContent.pivot = new Vector2(0.5f, 1f);
            _gridContent.offsetMin = Vector2.zero;
            _gridContent.offsetMax = Vector2.zero;
            _gridScroll.viewport = _gridViewport;
            _gridScroll.content = _gridContent;
            _gridScroll.onValueChanged.AddListener(HandleGridScrolled);

            _emptyText = OwnerWorkspaceUiFactory.CreateText(
                _gridViewport, "EmptyState", "검색 결과가 없습니다. 필터를 바꾸거나 검색어를 지워 주세요.", 16, FontStyle.Bold,
                TextAnchor.MiddleCenter, CareerUiTheme.TextMuted);
            OwnerRuntimeUiFactory.Stretch(_emptyText.rectTransform);
            _emptyText.gameObject.SetActive(false);
        }

        private InputField CreateSearchInput(Transform parent)
        {
            Image surface = OwnerRuntimeUiFactory.CreateImage("SearchField", parent, CareerUiTheme.Surface);
            surface.raycastTarget = true;
            LayoutElement size = surface.gameObject.AddComponent<LayoutElement>();
            size.minWidth = 210f;
            size.preferredWidth = 280f;
            size.minHeight = 40f;
            InputField input = surface.gameObject.AddComponent<InputField>();
            Text value = OwnerWorkspaceUiFactory.CreateText(
                surface.transform, "Text", string.Empty, 15, FontStyle.Normal,
                TextAnchor.MiddleLeft, CareerUiTheme.TextPrimary);
            OwnerRuntimeUiFactory.Stretch(value.rectTransform, new Vector2(12f, 4f), new Vector2(-12f, -4f));
            Text placeholder = OwnerWorkspaceUiFactory.CreateText(
                surface.transform, "Placeholder", "이름·포지션·비용·카드 종류 검색", 14, FontStyle.Italic,
                TextAnchor.MiddleLeft, CareerUiTheme.TextMuted);
            OwnerRuntimeUiFactory.Stretch(placeholder.rectTransform, new Vector2(12f, 4f), new Vector2(-12f, -4f));
            input.textComponent = value;
            input.placeholder = placeholder;
            input.targetGraphic = surface;
            return input;
        }

        private void CreateSortButton(
            Transform parent,
            string name,
            string label,
            OwnerCollectionSort sort)
        {
            Button button = OwnerWorkspaceUiFactory.CreateButton(parent, name, label, () => HandleSortChanged(sort));
            LayoutElement size = button.GetComponent<LayoutElement>();
            size.minWidth = 76f;
            size.preferredWidth = 88f;
            size.flexibleWidth = 0f;
        }

        private void CreateDisabledAction(string name, string label)
        {
            Button button = OwnerWorkspaceUiFactory.CreateButton(_actionRoot, name, label, null);
            button.interactable = false;
        }

        private void CreateAction(string name, string label, Action action)
        {
            OwnerWorkspaceUiFactory.CreateButton(_actionRoot, name, label, action);
        }

        private void HandleSearchChanged(string query)
        {
            if (_snapshot == null) return;
            RefreshCards();
        }

        private void HandleSortChanged(OwnerCollectionSort sort)
        {
            if (_snapshot == null) return;
            _sort = sort;
            RefreshCards();
        }

        private void RefreshCards()
        {
            var visible = new List<OwnerCollectionCardSnapshot>();
            foreach (OwnerCollectionCardSnapshot card in _snapshot.Cards)
                if (_cardFilters.Matches(card)) visible.Add(card);
            _model = OwnerCollectionPresentationBuilder.Build(new OwnerCollectionSnapshot(visible), _searchInput?.text, _sort);
            _countText.text = $"표시 {_model.Cards.Count} / 보유 {_snapshot.Cards.Count}장";
            _emptyText.gameObject.SetActive(_model.Cards.Count == 0);
            bool selectedRemainsVisible = false;
            for (int index = 0; index < _model.Cards.Count; index++)
            {
                OwnerCollectionCardModel card = _model.Cards[index];
                selectedRemainsVisible |= string.Equals(
                    card.Snapshot.CardId, _selectedCardId, StringComparison.Ordinal);
            }
            if (!selectedRemainsVisible && !string.IsNullOrEmpty(_selectedCardId))
            {
                _selectedCardId = string.Empty;
                ShowNoSelection();
            }
            UpdateVirtualContentHeight();
            RefreshVirtualizedGrid(true);
        }

        private void EnsureCardCapacity(int count)
        {
            while (_cardViews.Count < count)
            {
                PlayerMiniCardView cardView = PlayerMiniCardView.CreateRuntime(_gridContent);
                cardView.Selected += HandleCardSelected;
                cardView.DetailRequested += ShowCardDetail;
                _cardViews.Add(cardView);
            }
        }

        private void LateUpdate()
        {
            if (_gridViewport == null || _model == null || !gameObject.activeInHierarchy) return;
            Vector2 size = _gridViewport.rect.size;
            if (size == _lastViewportSize) return;
            _lastViewportSize = size;
            float stride = PlayerMiniCardView.PreferredWidth + 12f;
            _gridColumns = Mathf.Max(1, Mathf.FloorToInt(Mathf.Max(1f, size.x - 20f) / stride));
            UpdateVirtualContentHeight();
            RefreshVirtualizedGrid(true);
        }

        private void UpdateVirtualContentHeight()
        {
            if (_gridContent == null || _model == null) return;
            int rows = Mathf.CeilToInt(_model.Cards.Count / (float)Mathf.Max(1, _gridColumns));
            float height = 20f + rows * (PlayerMiniCardView.PreferredHeight + 12f);
            _gridContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(height, _gridViewport?.rect.height ?? 0f));
        }

        private void RefreshVirtualizedGrid(bool force)
        {
            if (_gridViewport == null || _gridContent == null || _model == null) return;
            float rowHeight = PlayerMiniCardView.PreferredHeight + 12f;
            int firstRow = Mathf.Max(0, Mathf.FloorToInt(Mathf.Max(0f, _gridContent.anchoredPosition.y - 10f) / rowHeight));
            int visibleRows = Mathf.Max(1, Mathf.CeilToInt(_gridViewport.rect.height / rowHeight) + 2);
            int firstIndex = firstRow * _gridColumns;
            int poolSize = visibleRows * _gridColumns;
            EnsureCardCapacity(poolSize);
            if (!force && firstIndex == _firstVisibleIndex) return;
            _firstVisibleIndex = firstIndex;

            float availableWidth = Mathf.Max(1f, _gridViewport.rect.width - 20f - 12f * (_gridColumns - 1));
            float cardWidth = availableWidth / _gridColumns;
            for (int poolIndex = 0; poolIndex < _cardViews.Count; poolIndex++)
            {
                int dataIndex = firstIndex + poolIndex;
                PlayerMiniCardView cardView = _cardViews[poolIndex];
                bool isVisible = poolIndex < poolSize && dataIndex < _model.Cards.Count;
                cardView.gameObject.SetActive(isVisible);
                if (!isVisible) continue;
                OwnerCollectionCardSnapshot card = _model.Cards[dataIndex].Snapshot;
                RectTransform rect = cardView.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                int row = dataIndex / _gridColumns;
                int column = dataIndex % _gridColumns;
                rect.anchoredPosition = new Vector2(10f + column * (cardWidth + 12f), -10f - row * rowHeight);
                rect.sizeDelta = new Vector2(cardWidth, PlayerMiniCardView.PreferredHeight);
                cardView.name = $"Card_{card.CardId}";
                bool selected = string.Equals(card.CardId, _selectedCardId, StringComparison.Ordinal);
                cardView.Bind(OwnerCollectionPresentationBuilder.CreateMiniCard(card, selected));
                cardView.SetTeamIdentity(card.TeamDisplayName);
            }
        }

        private void HandleGridScrolled(Vector2 _) => RefreshVirtualizedGrid(false);

        private void HandleCardSelected(PlayerMiniCardModel selected)
        {
            _selectedCardId = selected.PlayerId;
            _trainingIndex = 0;
            _studyIndex = 0;
            _pendingEnhancementCardId = string.Empty;
            OwnerCollectionCardSnapshot selectedSnapshot = GetSelectedCard();
            RefreshVirtualizedGrid(true);
            if (selectedSnapshot != null) ShowInspector(selectedSnapshot);
        }

        private void ShowCardDetail(PlayerMiniCardModel selected)
        {
            for (int index = 0; index < _snapshot.Cards.Count; index++)
                if (_snapshot.Cards[index].CardId == selected.PlayerId)
                {
                    UI_Popup_OwnerPlayerCard.Show(_workspaceRoot, _snapshot.Cards, index);
                    return;
                }
        }

        private void ShowInspector(OwnerCollectionCardSnapshot card)
        {
            _inspectorCard.gameObject.SetActive(true);
            _inspectorCard.Bind(OwnerCollectionPresentationBuilder.CreateMiniCard(card, true));
            _inspectorCard.SetTeamIdentity(card.TeamDisplayName);
            _inspectorText.text =
                $"{card.DisplayName}\n\n" +
                $"연도  {card.OriginYear}\n" +
                $"포지션  {OwnerCollectionPresentationBuilder.FormatPlayerRole(card.Position, card.PitcherRole, card.IsPositionEvidenceMissing)}\n" +
                    $"비용  {card.Cost}\n" +
                $"카드 종류  {OwnerCollectionPresentationBuilder.FormatEdition(card.Edition)}\n\n" +
                $"강화  +{card.EnhancementLevel}\n" +
                $"중복  {card.DuplicateCount}장\n" +
                $"잠금  {(card.IsLocked ? "예" : "아니오")}\n" +
                $"즐겨찾기  {(card.IsFavorite ? "예" : "아니오")}\n\n" +
                $"훈련 누적  +{card.TrainingBonusTotal}\n" +
                $"성장판  {card.PlacedSkillBlockCount}개 장착\n" +
                $"미장착 블록  {card.AvailableSkillBlockCount}개\n" +
                $"1군 상태  {(card.IsActiveRoster ? "등록" : "미등록")}\n" +
                $"유학  {(string.IsNullOrEmpty(card.StudyStatus) ? "대기" : card.StudyStatus)}\n\n" +
                $"CardId\n{card.CardId}";
        }

        private void CycleTraining()
        {
            OwnerCollectionCardSnapshot card = GetSelectedCard();
            if (card == null) return;
            PlayerAbility[] abilities = IsPitcher(card) ? PitcherTrainingAbilities : BatterTrainingAbilities;
            _trainingIndex = (_trainingIndex + 1) % abilities.Length;
            SetFeedback($"훈련 선택: {DescribeAbility(abilities[_trainingIndex])} · 실행 전 DP와 상한을 다시 검증합니다.", false);
        }

        private void RequestTraining()
        {
            OwnerCollectionCardSnapshot card = GetSelectedCard();
            if (card == null) return;
            PlayerAbility[] abilities = IsPitcher(card) ? PitcherTrainingAbilities : BatterTrainingAbilities;
            string programId = "card_training_" + abilities[_trainingIndex].ToString().ToLowerInvariant();
            TrainingRequested?.Invoke(card.CardId, programId);
        }

        private void CycleStudy()
        {
            OwnerCollectionCardSnapshot card = GetSelectedCard();
            if (card == null) return;
            string[] programs = IsPitcher(card) ? PitcherStudyPrograms : BatterStudyPrograms;
            _studyIndex = (_studyIndex + 1) % programs.Length;
            SetFeedback($"유학 선택: {DescribeStudy(programs[_studyIndex])} · 4주/육성 포인트 100", false);
        }

        private void RequestStudy()
        {
            OwnerCollectionCardSnapshot card = GetSelectedCard();
            if (card == null) return;
            string[] programs = IsPitcher(card) ? PitcherStudyPrograms : BatterStudyPrograms;
            StudyRequested?.Invoke(card.CardId, programs[_studyIndex]);
        }

        private OwnerCollectionCardSnapshot GetSelectedCard()
        {
            if (_snapshot == null || string.IsNullOrEmpty(_selectedCardId)) return null;
            for (int index = 0; index < _snapshot.Cards.Count; index++)
                if (string.Equals(_snapshot.Cards[index].CardId, _selectedCardId, StringComparison.Ordinal)) return _snapshot.Cards[index];
            return null;
        }

        private static bool IsPitcher(OwnerCollectionCardSnapshot card) =>
            card.Position == Baseball.Core.Players.PlayerPosition.StartingPitcher ||
            card.Position == Baseball.Core.Players.PlayerPosition.ReliefPitcher;

        private static string DescribeAbility(PlayerAbility ability) => ability switch
        {
            PlayerAbility.Contact => "교타력", PlayerAbility.Power => "장타력", PlayerAbility.Speed => "주력",
            PlayerAbility.Bunt => "번트력", PlayerAbility.Defense => "수비력", PlayerAbility.BatterMental => "타자 정신력",
            PlayerAbility.Stamina => "체력", PlayerAbility.Velocity => "구속", PlayerAbility.Stuff => "구위",
            PlayerAbility.Breaking => "변화구", PlayerAbility.Control => "제구력", _ => "투수 정신력"
        };

        private static string DescribeStudy(string id) => id switch
        {
            "study_contact" => "정교 타격 아카데미", "study_power" => "장타 강화 캠프",
            "study_defense" => "수비 전문 학교", "study_batter_allround" => "야수 실전 리그",
            "study_velocity" => "구속 연구소", "study_command" => "제구 아카데미",
            "study_breaking" => "변화구 디자인 랩", _ => "선발 체력 리그"
        };

        private void ShowNoSelection()
        {
            if (_inspectorCard != null) _inspectorCard.gameObject.SetActive(false);
            if (_inspectorText != null)
                _inspectorText.text = "보유 선수 카드를 선택하면\n현재 저장 데이터의 카드 상태를 확인할 수 있습니다.";
        }

        private void DestroyCards()
        {
            for (int index = 0; index < _cardViews.Count; index++)
            {
                PlayerMiniCardView cardView = _cardViews[index];
                if (cardView == null) continue;
                cardView.Selected -= HandleCardSelected;
                cardView.DetailRequested -= ShowCardDetail;
                if (Application.isPlaying) Destroy(cardView.gameObject);
                else DestroyImmediate(cardView.gameObject);
            }
            _cardViews.Clear();
        }

        private void OnDestroy()
        {
            if (_searchInput != null) _searchInput.onValueChanged.RemoveListener(HandleSearchChanged);
            if (_gridScroll != null) _gridScroll.onValueChanged.RemoveListener(HandleGridScrolled);
            if (_inspectorCard != null) _inspectorCard.DetailRequested -= ShowCardDetail;
            DestroyCards();
            EnhancementRequested = null;
            DuplicateSaleRequested = null;
            TrainingRequested = null;
            StudyRequested = null;
            SkillBlockAutoPlaceRequested = null;
            SkillBlockRemoveRequested = null;
            OwnerWorkspaceUiFactory.DestroyOwnedRoot(_workspaceRoot);
            OwnerWorkspaceUiFactory.DestroyOwnedRoot(_inspectorRoot);
            OwnerWorkspaceUiFactory.DestroyOwnedRoot(_actionRoot);
        }
    }
}
