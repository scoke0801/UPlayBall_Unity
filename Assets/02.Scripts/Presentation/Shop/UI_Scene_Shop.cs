using System;
using System.Collections.Generic;
using Baseball.Core.Shop;
using Baseball.Presentation.Owner;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Shop
{
    /// <summary>
    /// 카드 상점 화면이다. 목록 카테고리 탭과 좌측 상품 진열, 우측 선택 상품 상세를 한 화면에 보여준다.
    /// 구매 가능 여부는 <see cref="ShopPresentationModel"/>이 준 스냅샷을 표시만 하고 다시 판정하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class UI_Scene_Shop : MonoBehaviour
    {
        private const float TileHeight = 132f;
        private const float ArtworkSize = 92f;
        private const int TileColumnCount = 2;

        private readonly List<Button> _tabButtons = new List<Button>();
        private readonly List<Image> _tabSurfaces = new List<Image>();
        private readonly List<Button> _tileActionButtons = new List<Button>();
        private readonly List<bool> _tileActionAvailability = new List<bool>();
        private readonly Dictionary<ShopTab, float> _scrollByTab = new Dictionary<ShopTab, float>();

        private RectTransform _root;
        private RectTransform _tabBar;
        private RectTransform _playerCardFilterBar;
        private RectTransform _gridContent;
        private GridLayoutGroup _gridLayout;
        private ScrollRect _scroll;
        private Text _lockText;
        private RectTransform _revealRoot;
        private Text _revealTitle;
        private RawImage _revealArtwork;
        private Text _revealBody;
        private ShopScreenSnapshot _snapshot;
        private int _selectedTabIndex;
        private bool _isProcessing;

        /// <summary>구매 확인부터 연속 뽑기·최종 결과 확인을 마칠 때까지 가이드 개입을 보류한다.</summary>
        public bool IsGuideSuppressed => _root != null && _root.gameObject.activeInHierarchy &&
            (_isProcessing ||
             (_confirmationRoot != null && _confirmationRoot.gameObject.activeInHierarchy) ||
             (_revealRoot != null && _revealRoot.gameObject.activeInHierarchy));

        /// <summary>구입 전 최신 Quote를 확인 Overlay로 열어 달라는 요청이다.</summary>
        public event Action<string> PurchasePreviewRequested;

        /// <summary>구입 버튼이 눌렸을 때 ProductId를 전달한다. 실제 구매는 Game 레이어가 수행한다.</summary>
        public event Action<string> PurchaseRequested;

        /// <summary>상세(확률·구성) 보기 요청이다.</summary>
        public event Action<string> DetailsRequested;

        /// <summary>Reveal에서 같은 상품을 확인 팝업 없이 즉시 다시 구매하는 요청이다.</summary>
        public event Action<string> RepurchaseRequested;

        /// <summary>Reveal에서 상품 종류에 맞는 보관 화면으로 이동하는 요청이다.</summary>
        public event Action<string> InventoryRequested;

        /// <summary>Reveal에서 공개가 끝난 선수 카드의 상세 보기 요청이다.</summary>
        public event Action<string> PlayerCardDetailsRequested;

        /// <summary>외부 SFX 시스템이 연출 단계에 맞는 소리를 선택할 수 있도록 Cue만 전달한다.</summary>
        public event Action<ShopRevealAudioCue> RevealAudioRequested;

        public static UI_Scene_Shop CreateRuntime(RectTransform host)
        {
            if (host == null)
                throw new ArgumentNullException(nameof(host));
            var view = new GameObject(nameof(UI_Scene_Shop)).AddComponent<UI_Scene_Shop>();
            view.Build(host);
            return view;
        }

        public void Bind(ShopScreenSnapshot snapshot)
        {
            RememberScrollPosition();
            ShopTab? selectedTab = GetSelectedTab();
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            _selectedTabIndex = FindTabIndex(selectedTab);
            RebuildTabs();
            RebuildPlayerCardFilters();
            RefreshTiles();
        }

        /// <summary>
        /// 구매 결과를 안내 줄에 표시한다. <see cref="Bind"/>가 이 줄을 잠금 사유로 되돌리므로
        /// 스냅샷을 다시 바인딩한 뒤에 호출해야 한다.
        /// </summary>
        public void SetFeedback(string message, bool isError)
        {
            if (_lockText == null)
                return;
            _lockText.text = message ?? string.Empty;
            _lockText.color = isError ? CareerUiTheme.Error : CareerUiTheme.ReferenceAccent;
        }

        /// <summary>이미 확정된 구매 결과를 전체 화면 카드 공개 패널로 보여준다.</summary>
        public void ShowReveal(ShopPurchaseResult result)
        {
            ShowReveal(result, _activeDetails);
        }

        /// <summary>확정 결과와 구매 상품 문맥을 함께 공개해 재구매·보관함 이동을 유지한다.</summary>
        public void ShowReveal(ShopPurchaseResult result, ShopProductDetailsSnapshot details,
            PlayerMiniCardModel[] playerCards = null,
            ShopSkillBlockRevealModel[] skillBlocks = null)
        {
            if (!result.IsSuccess || result.Items == null || result.Items.Length == 0 || _revealRoot == null)
                return;
            _revealPlayerCards = playerCards;
            _revealSkillBlocks = skillBlocks;
            StartRevealPlayback(result, details);
        }

        public void SetVisible(bool visible)
        {
            if (_root != null)
            {
                if (!visible)
                    HideAllOverlays();
                _root.gameObject.SetActive(visible);
            }
        }

        private void Build(RectTransform host)
        {
            _root = OwnerWorkspaceUiFactory.CreateRoot(host, "ShopWorkspace", true);
            OwnerWorkspaceUiFactory.Panel panel = OwnerWorkspaceUiFactory.CreatePanel(_root, "ShopPanel", "상점");
            OwnerRuntimeUiFactory.Stretch(panel.Root,
                new Vector2(CareerUiTheme.Space4, CareerUiTheme.Space4),
                new Vector2(-CareerUiTheme.Space4, -CareerUiTheme.Space4));
            OwnerWorkspaceUiFactory.AddVerticalLayout(panel.Content, CareerUiTheme.Space3);

            BuildReferenceStorefront(panel.Content);
            BuildRevealOverlay();
            BuildDecisionOverlays();
            SetFeedback("상품 정보를 불러오는 중입니다.", false);
        }

        private void BuildRevealOverlay()
        {
            Image dim = OwnerRuntimeUiFactory.CreateImage("PurchaseReveal", _root, new Color(0.02f, 0.03f, 0.05f, 0.94f));
            _revealRoot = (RectTransform)dim.transform;
            OwnerRuntimeUiFactory.Stretch(_revealRoot);
            dim.raycastTarget = true;
            RawImage revealBackground = ShopArtwork.Create(
                _revealRoot, "RevealBackground", ShopArtwork.RevealBackgroundKey, Color.white);
            OwnerRuntimeUiFactory.Stretch(revealBackground.rectTransform);

            Image card = OwnerRuntimeUiFactory.CreateImage("RevealCard", _revealRoot, new Color(0.025f, 0.045f, 0.08f, .9f));
            RectTransform cardRoot = (RectTransform)card.transform;
            OwnerRuntimeUiFactory.SetAnchors(
                cardRoot,
                new Vector2(.06f, .08f), new Vector2(.94f, .92f), Vector2.zero, Vector2.zero);
            _revealPanel = card;
            _revealTitle = OwnerRuntimeUiFactory.CreateText(
                "RevealTitle", cardRoot, string.Empty, 24, FontStyle.Bold,
                TextAnchor.MiddleCenter, Color.white);
            OwnerRuntimeUiFactory.SetAnchors(_revealTitle.rectTransform,
                new Vector2(.2f, .91f), new Vector2(.8f, 1f), Vector2.zero, Vector2.zero);
            RectTransform artworkFrame = OwnerRuntimeUiFactory.CreateRect("ArtworkFrame", cardRoot);
            OwnerRuntimeUiFactory.SetAnchors(artworkFrame,
                new Vector2(.3f, .38f), new Vector2(.7f, .86f), Vector2.zero, Vector2.zero);
            _revealArtwork = TacticCardArtwork.Create(
                artworkFrame, "RevealArtwork", TacticCardArtwork.CommonKey, Color.white);
            var artworkAspect = _revealArtwork.gameObject.AddComponent<AspectRatioFitter>();
            artworkAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            _revealArtwork.gameObject.SetActive(false);
            _revealBody = OwnerRuntimeUiFactory.CreateText(
                "RevealBody", cardRoot, string.Empty, 18, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Color32(211, 224, 241, 255));
            OwnerRuntimeUiFactory.SetAnchors(_revealBody.rectTransform,
                new Vector2(.12f, .12f), new Vector2(.88f, .85f), Vector2.zero, Vector2.zero);
            RectTransform actions = OwnerRuntimeUiFactory.CreateRect("RevealActions", cardRoot);
            HorizontalLayoutGroup actionLayout = OwnerWorkspaceUiFactory.AddHorizontalLayout(actions, CareerUiTheme.Space2);
            actionLayout.childAlignment = TextAnchor.MiddleCenter;
            OwnerRuntimeUiFactory.SetAnchors(actions,
                new Vector2(.24f, 0f), new Vector2(.76f, 0f), new Vector2(0f, 0f), new Vector2(0f, 42f));
            _revealRepeatButton = OwnerWorkspaceUiFactory.CreateButton(
                actions, "Repurchase", "다시 구입", HandleRepurchaseClicked);
            _revealInventoryButton = OwnerWorkspaceUiFactory.CreateButton(
                actions, "OpenInventory", "보관함", HandleInventoryClicked);
            _revealCloseButton = OwnerWorkspaceUiFactory.CreateButton(
                actions, "CloseReveal", "확인", () => _revealRoot.gameObject.SetActive(false));
            foreach (Button button in new[] { _revealRepeatButton, _revealInventoryButton, _revealCloseButton })
            {
                button.GetComponent<Image>().color = new Color32(29, 48, 72, 255);
                button.transform.Find("Label").GetComponent<Text>().color = Color.white;
            }
            BuildRevealPlaybackControls();
            _revealRoot.gameObject.SetActive(false);
        }

        private void BuildTabBar(RectTransform parent)
        {
            _tabBar = OwnerRuntimeUiFactory.CreateRect("TabBar", parent);
            HorizontalLayoutGroup layout = OwnerWorkspaceUiFactory.AddHorizontalLayout(_tabBar, CareerUiTheme.Space2);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childForceExpandWidth = true;
            AddFixedHeight(_tabBar, 36f);
        }

        private void BuildLockNotice(RectTransform parent)
        {
            _lockText = OwnerRuntimeUiFactory.CreateText(
                "LockNotice", parent, string.Empty, 14, FontStyle.Normal,
                TextAnchor.MiddleRight, CareerUiTheme.ReferenceTextSecondary);
            var layout = _lockText.gameObject.AddComponent<LayoutElement>();
            layout.flexibleWidth = 1f;
            layout.minHeight = 0f;
            layout.preferredHeight = 28f;
        }

        private void RebuildTabs()
        {
            ClearStorefrontChildren(_tabBar);
            _tabButtons.Clear();
            _tabSurfaces.Clear();

            for (int index = 0; index < _snapshot.Tabs.Count; index++)
            {
                ShopTabSnapshot tab = _snapshot.Tabs[index];
                int tabIndex = index;
                Button button = OwnerWorkspaceUiFactory.CreateButton(
                    _tabBar, "Tab_" + tab.Tab, tab.Title, () => SelectTab(tabIndex));
                var layout = button.GetComponent<LayoutElement>();
                layout.minWidth = 0f;
                layout.preferredWidth = 0f;
                layout.flexibleWidth = 1f;
                layout.minHeight = 36f;
                layout.preferredHeight = 36f;
                button.interactable = !_isProcessing;
                _tabButtons.Add(button);
                _tabSurfaces.Add(button.GetComponent<Image>());
            }
            ApplyTabSelection();
        }

        private void SelectTab(int tabIndex)
        {
            if (_isProcessing || _selectedTabIndex == tabIndex)
                return;
            RememberScrollPosition();
            _selectedTabIndex = tabIndex;
            ApplyTabSelection();
            RefreshTiles();
        }

        private void ApplyTabSelection()
        {
            for (int index = 0; index < _tabSurfaces.Count; index++)
            {
                bool isSelected = index == _selectedTabIndex;
                _tabSurfaces[index].color = isSelected
                    ? CareerUiTheme.ReferenceAccent
                    : CareerUiTheme.ReferenceButton;
                Text label = _tabButtons[index].transform.Find("Label").GetComponent<Text>();
                label.color = isSelected ? Color.white : CareerUiTheme.ReferenceText;
            }
        }

        private void RefreshTiles()
        {
            _scroll.StopMovement();
            ClearStorefrontChildren(_gridContent);
            _tileActionButtons.Clear();
            _tileActionAvailability.Clear();
            _productTileSurfaces.Clear();
            _productTileIds.Clear();
            ShopTabSnapshot tab = _snapshot.Tabs[_selectedTabIndex];
            if (_playerCardFilterBar != null)
                _playerCardFilterBar.gameObject.SetActive(tab.Tab == ShopTab.PlayerCard);
            _lockText.text = tab.IsUnlocked ? string.Empty : tab.LockDescription;
            _lockText.color = CareerUiTheme.ReferenceTextSecondary;

            int visibleTileCount = CountVisibleTiles(tab);
            if (visibleTileCount == 0)
            {
                BuildEmptyTile(tab.IsUnlocked
                    ? "선택한 구단·연도에 해당하는 상품이 없습니다."
                    : tab.LockDescription);
                ClearSelectedProduct();
            }
            else
            {
                EnsureSelectedProduct(tab);
                BuildFilteredTiles(tab);
                RefreshSelectedProduct(tab);
            }

            RefreshPityGauge(tab.Tab);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_root);
            Canvas.ForceUpdateCanvases();
            UpdateGridCellSize();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_gridContent);
            RestoreScrollPosition(tab.Tab);
        }

        private static void ClearStorefrontChildren(RectTransform parent)
        {
            // Play Mode의 Destroy는 프레임 끝에 실행된다. 이전 항목을 즉시 레이아웃에서
            // 제외해야 새 목록 높이와 스크롤 최상단이 이전 목록에 영향을 받지 않는다.
            for (int index = 0; index < parent.childCount; index++)
                parent.GetChild(index).gameObject.SetActive(false);
            OwnerRuntimeUiFactory.ClearChildren(parent);
        }

        private void BuildEmptyTile(string message)
        {
            Image surface = OwnerRuntimeUiFactory.CreateImage(
                "EmptyState", _gridContent, CareerUiTheme.ReferencePanel);
            Text label = OwnerRuntimeUiFactory.CreateText(
                "Label", surface.rectTransform, message, 14, FontStyle.Bold,
                TextAnchor.MiddleCenter, CareerUiTheme.ReferenceTextSecondary);
            OwnerRuntimeUiFactory.Stretch(label.rectTransform, new Vector2(16f, 16f), new Vector2(-16f, -16f));
        }

        private void RememberScrollPosition()
        {
            ShopTab? tab = GetSelectedTab();
            if (tab.HasValue && _scroll != null)
                _scrollByTab[tab.Value] = _scroll.verticalNormalizedPosition;
        }

        private void RestoreScrollPosition(ShopTab tab)
        {
            if (_scroll != null)
                _scroll.verticalNormalizedPosition = _scrollByTab.TryGetValue(tab, out float value) ? value : 1f;
        }

        private ShopTab? GetSelectedTab()
        {
            if (_snapshot == null || _snapshot.Tabs.Count == 0 ||
                _selectedTabIndex < 0 || _selectedTabIndex >= _snapshot.Tabs.Count)
                return null;
            return _snapshot.Tabs[_selectedTabIndex].Tab;
        }

        private int FindTabIndex(ShopTab? selectedTab)
        {
            if (!selectedTab.HasValue || _snapshot.Tabs.Count == 0) return 0;
            for (int index = 0; index < _snapshot.Tabs.Count; index++)
                if (_snapshot.Tabs[index].Tab == selectedTab.Value) return index;
            return 0;
        }

        private void OnRectTransformDimensionsChange()
        {
            if (_gridLayout != null) UpdateGridCellSize();
        }

        private void LateUpdate()
        {
            if (_revealRoot != null && _revealRoot.gameObject.activeInHierarchy) LayoutRevealCards();
            // 상위 Shell의 레이아웃이 확정된 뒤 실제 Viewport 너비로 두 열을 맞춘다.
            if (_gridLayout != null && _root != null && _root.gameObject.activeInHierarchy)
                UpdateGridCellSize();
        }

        private void UpdateGridCellSize()
        {
            float width = _scroll != null && _scroll.viewport != null
                ? _scroll.viewport.rect.width
                : 0f;
            if (width <= 1f && _scroll != null)
                width = ((RectTransform)_scroll.transform).rect.width;
            if (width <= 1f) return;
            float usable = width - _gridLayout.padding.horizontal -
                _gridLayout.spacing.x * (TileColumnCount - 1);
            var size = new Vector2(Mathf.Max(1f, usable / TileColumnCount), TileHeight);
            if (_gridLayout.cellSize != size) _gridLayout.cellSize = size;
        }

        private static void AddFixedHeight(RectTransform target, float height)
        {
            LayoutElement layout = target.GetComponent<LayoutElement>();
            if (layout == null)
                layout = target.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
            // LayoutGroup도 flexibleHeight를 제공하므로 미지정(-1)으로 두면 고정 높이가 늘어난다.
            layout.flexibleHeight = 0f;
        }
    }
}
