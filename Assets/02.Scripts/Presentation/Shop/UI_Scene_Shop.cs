using System;
using System.Collections.Generic;
using System.Text;
using Baseball.Core.Shop;
using Baseball.Presentation.Owner;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Shop
{
    /// <summary>
    /// 카드 상점 화면이다. 상단 탭으로 계열을 고르고 2열 격자로 상품 타일을 보여준다.
    /// 구매 가능 여부는 <see cref="ShopPresentationModel"/>이 준 스냅샷을 표시만 하고 다시 판정하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UI_Scene_Shop : MonoBehaviour
    {
        private const float TileHeight = 128f;
        private const float ArtworkSize = 96f;
        private const int TileColumnCount = 2;

        private readonly List<Button> _tabButtons = new List<Button>();
        private readonly List<Image> _tabSurfaces = new List<Image>();

        private RectTransform _root;
        private RectTransform _tabBar;
        private RectTransform _gridContent;
        private Text _walletText;
        private Text _lockText;
        private RectTransform _revealRoot;
        private Text _revealTitle;
        private RawImage _revealArtwork;
        private Text _revealBody;
        private ShopScreenSnapshot _snapshot;
        private int _selectedTabIndex;

        /// <summary>구입 버튼이 눌렸을 때 ProductId를 전달한다. 실제 구매는 Game 레이어가 수행한다.</summary>
        public event Action<string> PurchaseRequested;

        /// <summary>상세(확률·구성) 보기 요청이다.</summary>
        public event Action<string> DetailsRequested;

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
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            if (_selectedTabIndex >= _snapshot.Tabs.Count)
                _selectedTabIndex = 0;
            _walletText.text = _snapshot.WalletSummary;
            RebuildTabs();
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
            if (!result.IsSuccess || result.Items == null || result.Items.Length == 0 || _revealRoot == null)
                return;
            _revealTitle.text = result.Items.Length == 1 ? "획득 카드" : $"획득 카드 {result.Items.Length}장";
            Texture2D revealTexture = TacticCardArtwork.Load(result.Items[0].ArtworkKey);
            _revealArtwork.texture = revealTexture;
            _revealArtwork.gameObject.SetActive(revealTexture != null);
            var body = new StringBuilder();
            for (int index = 0; index < result.Items.Length; index++)
            {
                ShopGrantedItem item = result.Items[index];
                if (index > 0) body.AppendLine().AppendLine();
                body.Append(string.IsNullOrEmpty(item.GradeLabel) ? "CARD" : item.GradeLabel)
                    .AppendLine()
                    .Append(item.DisplayName)
                    .AppendLine()
                    .Append(item.IsNew ? "NEW · 첫 획득" : "DUPLICATE · 중복 재료");
            }
            _revealBody.text = body.ToString();
            _revealRoot.gameObject.SetActive(true);
            _revealRoot.SetAsLastSibling();
        }

        public void SetVisible(bool visible)
        {
            if (_root != null)
                _root.gameObject.SetActive(visible);
        }

        private void Build(RectTransform host)
        {
            _root = OwnerWorkspaceUiFactory.CreateRoot(host, "ShopWorkspace", true);
            OwnerWorkspaceUiFactory.Panel panel = OwnerWorkspaceUiFactory.CreatePanel(_root, "ShopPanel", "상점");
            OwnerRuntimeUiFactory.Stretch(panel.Root,
                new Vector2(CareerUiTheme.Space4, CareerUiTheme.Space4),
                new Vector2(-CareerUiTheme.Space4, -CareerUiTheme.Space4));
            OwnerWorkspaceUiFactory.AddVerticalLayout(panel.Content, CareerUiTheme.Space3);

            BuildWalletBar(panel.Content);
            BuildTabBar(panel.Content);
            BuildLockNotice(panel.Content);
            BuildGrid(panel.Content);
            BuildRevealOverlay();
        }

        private void BuildRevealOverlay()
        {
            Image dim = OwnerRuntimeUiFactory.CreateImage("PurchaseReveal", _root, new Color(0.02f, 0.03f, 0.05f, 0.94f));
            _revealRoot = (RectTransform)dim.transform;
            OwnerRuntimeUiFactory.Stretch(_revealRoot);

            Image card = OwnerRuntimeUiFactory.CreateImage("RevealCard", _revealRoot, CareerUiTheme.ReferencePanel);
            RectTransform cardRoot = (RectTransform)card.transform;
            OwnerRuntimeUiFactory.SetAnchors(
                cardRoot,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(440f, 420f));
            cardRoot.anchoredPosition = Vector2.zero;
            var outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = CareerUiTheme.ReferenceAccent;
            outline.effectDistance = new Vector2(2f, -2f);

            VerticalLayoutGroup layout = OwnerWorkspaceUiFactory.AddVerticalLayout(cardRoot, CareerUiTheme.Space3);
            layout.padding = new RectOffset(28, 28, 28, 28);
            _revealTitle = OwnerRuntimeUiFactory.CreateText(
                "RevealTitle", cardRoot, string.Empty, 24, FontStyle.Bold,
                TextAnchor.MiddleCenter, CareerUiTheme.ReferenceAccent);
            AddFixedHeight(_revealTitle.rectTransform, 40f);
            _revealArtwork = TacticCardArtwork.Create(
                cardRoot, "RevealArtwork", TacticCardArtwork.CommonKey, Color.white);
            AddFixedHeight(_revealArtwork.rectTransform, 176f);
            _revealArtwork.gameObject.SetActive(false);
            _revealBody = OwnerRuntimeUiFactory.CreateText(
                "RevealBody", cardRoot, string.Empty, 18, FontStyle.Bold,
                TextAnchor.MiddleCenter, CareerUiTheme.ReferenceText);
            OwnerWorkspaceUiFactory.SetFlexible(_revealBody.rectTransform, 1f);
            Button close = OwnerWorkspaceUiFactory.CreateButton(
                cardRoot, "CloseReveal", "확인", () => _revealRoot.gameObject.SetActive(false));
            AddFixedHeight((RectTransform)close.transform, 42f);
            _revealRoot.gameObject.SetActive(false);
        }

        private void BuildWalletBar(RectTransform parent)
        {
            _walletText = OwnerRuntimeUiFactory.CreateText(
                "WalletSummary", parent, string.Empty, 16, FontStyle.Bold,
                TextAnchor.MiddleRight, CareerUiTheme.ReferenceText);
            AddFixedHeight(_walletText.rectTransform, 24f);
        }

        private void BuildTabBar(RectTransform parent)
        {
            _tabBar = OwnerRuntimeUiFactory.CreateRect("TabBar", parent);
            HorizontalLayoutGroup layout = OwnerWorkspaceUiFactory.AddHorizontalLayout(_tabBar, CareerUiTheme.Space2);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false;
            AddFixedHeight(_tabBar, 40f);
        }

        private void BuildLockNotice(RectTransform parent)
        {
            _lockText = OwnerRuntimeUiFactory.CreateText(
                "LockNotice", parent, string.Empty, 14, FontStyle.Normal,
                TextAnchor.MiddleCenter, CareerUiTheme.ReferenceTextSecondary);
            AddFixedHeight(_lockText.rectTransform, 22f);
        }

        private void BuildGrid(RectTransform parent)
        {
            ScrollRect scroll = OwnerRuntimeUiFactory.CreateVerticalScroll(
                "ShopScroll", parent, out RectTransform scrollContent);
            OwnerWorkspaceUiFactory.SetFlexible((RectTransform)scroll.transform, 1f);

            // 스크롤 Content에는 이미 세로 Layout이 붙어 있으므로, 격자는 그 아래 별도 자식에 만든다.
            _gridContent = OwnerRuntimeUiFactory.CreateRect("Grid", scrollContent);
            var grid = _gridContent.gameObject.AddComponent<GridLayoutGroup>();
            grid.padding = new RectOffset(
                (int)CareerUiTheme.Space3, (int)CareerUiTheme.Space3,
                (int)CareerUiTheme.Space3, (int)CareerUiTheme.Space3);
            grid.spacing = new Vector2(CareerUiTheme.Space3, CareerUiTheme.Space3);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = TileColumnCount;
            grid.cellSize = new Vector2(320f, TileHeight);

            var fitter = _gridContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private void RebuildTabs()
        {
            OwnerRuntimeUiFactory.ClearChildren(_tabBar);
            _tabButtons.Clear();
            _tabSurfaces.Clear();

            for (int index = 0; index < _snapshot.Tabs.Count; index++)
            {
                ShopTabSnapshot tab = _snapshot.Tabs[index];
                int tabIndex = index;
                Button button = OwnerWorkspaceUiFactory.CreateButton(
                    _tabBar, "Tab_" + tab.Tab, tab.Title, () => SelectTab(tabIndex));
                var layout = button.GetComponent<LayoutElement>();
                layout.minWidth = 116f;
                layout.preferredWidth = 116f;
                _tabButtons.Add(button);
                _tabSurfaces.Add(button.GetComponent<Image>());
            }
            ApplyTabSelection();
        }

        private void SelectTab(int tabIndex)
        {
            if (_selectedTabIndex == tabIndex)
                return;
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
            OwnerRuntimeUiFactory.ClearChildren(_gridContent);
            ShopTabSnapshot tab = _snapshot.Tabs[_selectedTabIndex];
            _lockText.text = tab.IsUnlocked ? string.Empty : tab.LockDescription;
            _lockText.color = CareerUiTheme.ReferenceTextSecondary;

            for (int index = 0; index < tab.Tiles.Count; index++)
                BuildTile(tab.Tiles[index]);
        }

        private void BuildTile(ShopProductTileSnapshot tile)
        {
            Image surface = OwnerRuntimeUiFactory.CreateImage(
                "Tile_" + tile.ProductId, _gridContent, CareerUiTheme.ReferencePanel);
            surface.raycastTarget = true;
            Outline outline = surface.gameObject.AddComponent<Outline>();
            outline.effectColor = tile.CanPurchase
                ? CareerUiTheme.ReferenceBorder
                : CareerUiTheme.ReferenceTextSecondary;
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = false;

            var body = (RectTransform)surface.transform;
            HorizontalLayoutGroup layout = OwnerWorkspaceUiFactory.AddHorizontalLayout(body, CareerUiTheme.Space2);
            layout.padding = new RectOffset(
                (int)CareerUiTheme.Space2, (int)CareerUiTheme.Space2,
                (int)CareerUiTheme.Space2, (int)CareerUiTheme.Space2);
            layout.childForceExpandWidth = false;

            BuildArtwork(body, tile);
            BuildTileDetails(body, tile);
        }

        private void BuildArtwork(RectTransform parent, ShopProductTileSnapshot tile)
        {
            Image artwork = OwnerRuntimeUiFactory.CreateImage(
                "Artwork", parent, CareerUiTheme.ReferencePanelHeader);
            var artworkLayout = artwork.gameObject.AddComponent<LayoutElement>();
            artworkLayout.minWidth = ArtworkSize;
            artworkLayout.preferredWidth = ArtworkSize;

            Texture2D tacticTexture = TacticCardArtwork.Load(tile.ArtworkKey);
            if (tacticTexture != null)
            {
                RawImage tacticArtwork = TacticCardArtwork.Create(
                    artwork.transform, "TacticArtwork", tile.ArtworkKey, Color.white);
                OwnerRuntimeUiFactory.Stretch(tacticArtwork.rectTransform);
            }

            Text countBadge = OwnerRuntimeUiFactory.CreateText(
                "CountBadge", (RectTransform)artwork.transform, tile.CountBadgeText, 14, FontStyle.Bold,
                TextAnchor.LowerCenter, tacticTexture == null ? CareerUiTheme.ReferenceTextSecondary : Color.white);
            OwnerRuntimeUiFactory.Stretch(countBadge.rectTransform, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            if (tacticTexture != null)
            {
                var shadow = countBadge.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
                shadow.effectDistance = new Vector2(1f, -1f);
            }

            if (tile.BadgeText.Length == 0)
                return;

            Image badgeSurface = OwnerRuntimeUiFactory.CreateImage(
                "Badge", (RectTransform)artwork.transform, CareerUiTheme.Warning);
            OwnerRuntimeUiFactory.SetAnchors(
                badgeSurface.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                Vector2.zero,
                new Vector2(44f, 18f));
            badgeSurface.rectTransform.pivot = new Vector2(0f, 1f);
            badgeSurface.rectTransform.anchoredPosition = Vector2.zero;
            badgeSurface.rectTransform.sizeDelta = new Vector2(44f, 18f);
            Text badgeText = OwnerRuntimeUiFactory.CreateText(
                "Label", badgeSurface.rectTransform, tile.BadgeText, 11, FontStyle.Bold,
                TextAnchor.MiddleCenter, Color.white);
            OwnerRuntimeUiFactory.Stretch(badgeText.rectTransform);
        }

        private void BuildTileDetails(RectTransform parent, ShopProductTileSnapshot tile)
        {
            RectTransform details = OwnerRuntimeUiFactory.CreateRect("Details", parent);
            OwnerWorkspaceUiFactory.AddVerticalLayout(details, CareerUiTheme.Space1);
            OwnerWorkspaceUiFactory.SetFlexible(details, 1f);

            Image titleBar = OwnerRuntimeUiFactory.CreateImage(
                "TitleBar", details, CareerUiTheme.ReferencePanelHeader);
            AddFixedHeight((RectTransform)titleBar.transform, 24f);
            Text title = OwnerRuntimeUiFactory.CreateText(
                "Title", (RectTransform)titleBar.transform, tile.Title, 16, FontStyle.Bold,
                TextAnchor.MiddleCenter, CareerUiTheme.ReferenceText);
            OwnerRuntimeUiFactory.Stretch(title.rectTransform);

            Image subtitleBar = OwnerRuntimeUiFactory.CreateImage(
                "SubtitleBar", details, CareerUiTheme.ReferenceAccent);
            AddFixedHeight((RectTransform)subtitleBar.transform, 20f);
            Text subtitle = OwnerRuntimeUiFactory.CreateText(
                "Subtitle", (RectTransform)subtitleBar.transform, tile.Subtitle, 12, FontStyle.Normal,
                TextAnchor.MiddleCenter, Color.white);
            OwnerRuntimeUiFactory.Stretch(subtitle.rectTransform);

            Text price = OwnerRuntimeUiFactory.CreateText(
                "Price", details, tile.PriceText, 20, FontStyle.Bold,
                TextAnchor.MiddleRight, CareerUiTheme.ReferenceText);
            AddFixedHeight(price.rectTransform, 28f);

            BuildActionRow(details, tile);
        }

        private void BuildActionRow(RectTransform parent, ShopProductTileSnapshot tile)
        {
            RectTransform actions = OwnerRuntimeUiFactory.CreateRect("Actions", parent);
            HorizontalLayoutGroup layout = OwnerWorkspaceUiFactory.AddHorizontalLayout(actions, CareerUiTheme.Space2);
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childForceExpandWidth = false;
            AddFixedHeight(actions, 30f);

            string productId = tile.ProductId;

            // 원작의 "선물" 자리는 싱글 플레이에서 의미가 없어 확률·구성을 여는 "상세"로 대체한다.
            Button details = OwnerWorkspaceUiFactory.CreateButton(
                actions, "Details", "상세", () => DetailsRequested?.Invoke(productId));
            SetActionButtonSize(details);

            Button purchase = OwnerWorkspaceUiFactory.CreateButton(
                actions, "Purchase", "구입", () => PurchaseRequested?.Invoke(productId));
            SetActionButtonSize(purchase);
            purchase.interactable = tile.CanPurchase;
            purchase.GetComponent<Image>().color = tile.CanPurchase
                ? CareerUiTheme.ReferenceAccentLight
                : CareerUiTheme.ReferenceButton;
            Text purchaseLabel = purchase.transform.Find("Label").GetComponent<Text>();
            purchaseLabel.color = tile.CanPurchase ? Color.white : CareerUiTheme.ReferenceTextSecondary;
            if (!tile.CanPurchase && tile.BlockedReason.Length > 0)
                purchaseLabel.text = tile.BlockedReason;
        }

        private static void SetActionButtonSize(Button button)
        {
            var layout = button.GetComponent<LayoutElement>();
            layout.minHeight = 26f;
            layout.preferredHeight = 26f;
            layout.minWidth = 72f;
            layout.preferredWidth = 72f;
            button.transform.Find("Label").GetComponent<Text>().fontSize = 12;
        }

        private static void AddFixedHeight(RectTransform target, float height)
        {
            LayoutElement layout = target.GetComponent<LayoutElement>();
            if (layout == null)
                layout = target.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
        }
    }
}
