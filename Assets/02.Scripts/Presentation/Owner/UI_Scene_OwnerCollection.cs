using System;
using System.Collections.Generic;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>실제 보유 카드를 검색·정렬하고 공용 Mini Card로 선택하는 구단주 Collection 화면이다.</summary>
    [DisallowMultipleComponent]
    public sealed class UI_Scene_OwnerCollection : MonoBehaviour
    {
        private readonly List<PlayerMiniCardView> _cardViews = new List<PlayerMiniCardView>();
        private RectTransform _workspaceRoot;
        private RectTransform _inspectorRoot;
        private RectTransform _actionRoot;
        private RectTransform _gridContent;
        private InputField _searchInput;
        private Text _countText;
        private Text _emptyText;
        private Text _inspectorText;
        private OwnerCollectionSnapshot _snapshot;
        private OwnerCollectionPresentationModel _model;
        private OwnerCollectionSort _sort = OwnerCollectionSort.Name;
        private string _selectedCardId = string.Empty;

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
            _selectedCardId = string.Empty;
            RefreshCards();
            ShowNoSelection();
        }

        public void SetVisible(bool visible)
        {
            if (_workspaceRoot != null) _workspaceRoot.gameObject.SetActive(visible);
            if (_inspectorRoot != null) _inspectorRoot.gameObject.SetActive(visible);
            if (_actionRoot != null) _actionRoot.gameObject.SetActive(visible);
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
            OwnerWorkspaceUiFactory.Stretch(_inspectorText.rectTransform);

            _actionRoot = OwnerWorkspaceUiFactory.CreateRoot(actionBarHost, "OwnerCollectionActionBar", false);
            HorizontalLayoutGroup actions = OwnerWorkspaceUiFactory.AddHorizontalLayout(_actionRoot, CareerUiTheme.Space3);
            actions.padding = new RectOffset(16, 16, 4, 4);
            Text help = OwnerWorkspaceUiFactory.CreateText(
                _actionRoot, "ReadOnlyHelp", "카드를 선택하면 현재 저장 데이터의 소유 상태를 확인할 수 있습니다.",
                14, FontStyle.Normal, TextAnchor.MiddleLeft, CareerUiTheme.TextSecondary);
            OwnerWorkspaceUiFactory.SetFlexible(help.rectTransform, 1f, 0f);
            CreateDisabledAction("EnhancementDisabled", "강화 미리보기·실행 미제공");
            CreateDisabledAction("SaleDisabled", "판매 미리보기·실행 미제공");
            CreateDisabledAction("ActiveRosterDisabled", "1군 등록 변경 미제공");
            CareerUiSkin.Apply(_workspaceRoot);
            CareerUiSkin.Apply(_inspectorRoot);
            CareerUiSkin.Apply(_actionRoot);
        }

        private void BuildFilterBar(RectTransform parent)
        {
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
                Vector2.zero, new Vector2(0f, -54f));
            ScrollRect scroll = surface.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;

            Image viewportImage = OwnerRuntimeUiFactory.CreateImage(
                "Viewport", scroll.transform, new Color(0f, 0f, 0f, 0.01f));
            RectTransform viewport = viewportImage.rectTransform;
            OwnerRuntimeUiFactory.Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            _gridContent = OwnerRuntimeUiFactory.CreateRect("Content", viewport);
            _gridContent.anchorMin = new Vector2(0f, 1f);
            _gridContent.anchorMax = Vector2.one;
            _gridContent.pivot = new Vector2(0.5f, 1f);
            _gridContent.offsetMin = Vector2.zero;
            _gridContent.offsetMax = Vector2.zero;
            GridLayoutGroup grid = _gridContent.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(PlayerMiniCardView.PreferredWidth, PlayerMiniCardView.PreferredHeight);
            grid.spacing = new Vector2(12f, 12f);
            grid.padding = new RectOffset(10, 10, 10, 10);
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            ContentSizeFitter fitter = _gridContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = _gridContent;

            _emptyText = OwnerWorkspaceUiFactory.CreateText(
                viewport, "EmptyState", "검색 결과가 없습니다.", 16, FontStyle.Bold,
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
            _model = OwnerCollectionPresentationBuilder.Build(_snapshot, _searchInput?.text, _sort);
            _countText.text = _model.CountText;
            _emptyText.gameObject.SetActive(_model.Cards.Count == 0);
            EnsureCardCapacity(_model.Cards.Count);
            bool selectedRemainsVisible = false;
            for (int index = 0; index < _cardViews.Count; index++)
            {
                bool isVisible = index < _model.Cards.Count;
                _cardViews[index].gameObject.SetActive(isVisible);
                if (!isVisible) continue;
                OwnerCollectionCardModel card = _model.Cards[index];
                bool isSelected = string.Equals(card.Snapshot.CardId, _selectedCardId, StringComparison.Ordinal);
                PlayerMiniCardView cardView = _cardViews[index];
                cardView.name = $"Card_{card.Snapshot.CardId}";
                cardView.Bind(OwnerCollectionPresentationBuilder.CreateMiniCard(card.Snapshot, isSelected));
                selectedRemainsVisible |= isSelected;
            }
            if (!selectedRemainsVisible && !string.IsNullOrEmpty(_selectedCardId))
            {
                _selectedCardId = string.Empty;
                ShowNoSelection();
            }
        }

        private void EnsureCardCapacity(int count)
        {
            while (_cardViews.Count < count)
            {
                PlayerMiniCardView cardView = PlayerMiniCardView.CreateRuntime(_gridContent);
                cardView.Selected += HandleCardSelected;
                _cardViews.Add(cardView);
            }
        }

        private void HandleCardSelected(PlayerMiniCardModel selected)
        {
            _selectedCardId = selected.PlayerId;
            OwnerCollectionCardSnapshot selectedSnapshot = null;
            for (int index = 0; index < _model.Cards.Count; index++)
            {
                OwnerCollectionCardSnapshot card = _model.Cards[index].Snapshot;
                bool isSelected = string.Equals(card.CardId, _selectedCardId, StringComparison.Ordinal);
                _cardViews[index].Bind(OwnerCollectionPresentationBuilder.CreateMiniCard(card, isSelected));
                if (isSelected) selectedSnapshot = card;
            }
            if (selectedSnapshot != null) ShowInspector(selectedSnapshot);
        }

        private void ShowInspector(OwnerCollectionCardSnapshot card)
        {
            _inspectorText.text =
                $"{card.DisplayName}\n\n" +
                $"연도  {card.OriginYear}\n" +
                $"포지션  {OwnerCollectionPresentationBuilder.FormatPosition(card.Position)}\n" +
                    $"비용  {card.Cost}\n" +
                $"카드 종류  {OwnerCollectionPresentationBuilder.FormatEdition(card.Edition)}\n\n" +
                $"강화  +{card.EnhancementLevel}\n" +
                $"중복  {card.DuplicateCount}장\n" +
                $"잠금  {(card.IsLocked ? "예" : "아니오")}\n" +
                $"즐겨찾기  {(card.IsFavorite ? "예" : "아니오")}\n\n" +
                $"CardId\n{card.CardId}";
        }

        private void ShowNoSelection()
        {
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
                if (Application.isPlaying) Destroy(cardView.gameObject);
                else DestroyImmediate(cardView.gameObject);
            }
            _cardViews.Clear();
        }

        private void OnDestroy()
        {
            if (_searchInput != null) _searchInput.onValueChanged.RemoveListener(HandleSearchChanged);
            DestroyCards();
            OwnerWorkspaceUiFactory.DestroyOwnedRoot(_workspaceRoot);
            OwnerWorkspaceUiFactory.DestroyOwnedRoot(_inspectorRoot);
            OwnerWorkspaceUiFactory.DestroyOwnedRoot(_actionRoot);
        }
    }
}
