using System;
using System.Collections.Generic;
using Baseball.Core.Shop;
using Baseball.Presentation.Owner;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Shop
{
    public sealed partial class UI_Scene_Shop
    {
        private static Color ArtworkBackground => UIOwnerFrontOfficeSkin.IsOwnerContext
            ? OwnerDashboardStyle.InsetSurface : new Color32(232, 236, 233, 255);
        private readonly List<Image> _productTileSurfaces = new List<Image>();
        private readonly List<string> _productTileIds = new List<string>();

        private RectTransform _previewRoot;
        private RawImage _previewArtwork;
        private Text _previewTitle;
        private Text _previewSubtitle;
        private Text _previewCount;
        private Text _previewPrice;
        private Text _previewStatus;
        private Button _previewDetailsButton;
        private Button _previewPurchaseButton;
        private RectTransform _pityRoot;
        private RectTransform _pityFill;
        private Text _pityLabel;
        private string _selectedProductId = string.Empty;
        private bool _selectedProductCanPurchase;
        private string _playerCardFranchiseFilter = string.Empty;
        private int? _playerCardYearFilter;

        /// <summary>좌측 목록만 세로 스크롤되고 우측 선택 상품 정보는 고정되는 두 영역을 만든다.</summary>
        private void BuildReferenceStorefront(RectTransform parent)
        {
            RectTransform storefront = OwnerRuntimeUiFactory.CreateRect("Storefront", parent);
            HorizontalLayoutGroup layout = OwnerWorkspaceUiFactory.AddHorizontalLayout(
                storefront, CareerUiTheme.Space3);
            layout.childAlignment = TextAnchor.UpperLeft;
            OwnerWorkspaceUiFactory.SetFlexible(storefront, 1f, 1f);

            BuildCatalogColumn(storefront);
            BuildPreviewColumn(storefront);
        }

        private void BuildCatalogColumn(RectTransform parent)
        {
            Image surface = OwnerRuntimeUiFactory.CreateImage(
                "Catalog", parent, new Color32(231, 233, 230, 255));
            RectTransform root = surface.rectTransform;
            if (UIOwnerFrontOfficeSkin.IsOwnerContext) UIOwnerFrontOfficePanel.Apply(root, "ManagerReport");
            VerticalLayoutGroup layout = OwnerWorkspaceUiFactory.AddVerticalLayout(root, CareerUiTheme.Space2);
            layout.padding = new RectOffset(10, 10, 10, 10);
            OwnerWorkspaceUiFactory.SetFlexible(root, 1.25f, 1f);
            root.GetComponent<LayoutElement>().minWidth = 430f;

            RectTransform header = OwnerRuntimeUiFactory.CreateRect("CatalogHeader", root);
            HorizontalLayoutGroup headerLayout = OwnerWorkspaceUiFactory.AddHorizontalLayout(
                header, CareerUiTheme.Space2);
            headerLayout.childAlignment = TextAnchor.MiddleLeft;
            AddFixedHeight(header, 28f);
            Text title = OwnerRuntimeUiFactory.CreateText(
                "Title", header, "상품 목록", 16, FontStyle.Bold,
                TextAnchor.MiddleLeft, CareerUiTheme.ReferenceText);
            var titleLayout = title.gameObject.AddComponent<LayoutElement>();
            titleLayout.minWidth = 88f;
            titleLayout.preferredWidth = 88f;
            BuildLockNotice(header);

            BuildTabBar(root);
            BuildPlayerCardFilterBar(root);

            _scroll = OwnerRuntimeUiFactory.CreateVerticalGridScroll(
                "CatalogScroll", root, TileColumnCount, new Vector2(250f, TileHeight),
                CareerUiTheme.Space2, out _gridContent);
            _scroll.horizontal = false;
            _scroll.vertical = true;
            OwnerWorkspaceUiFactory.SetFlexible((RectTransform)_scroll.transform, 1f, 1f);

            _gridContent.name = "ProductGrid";
            _gridLayout = _gridContent.GetComponent<GridLayoutGroup>();
            _gridLayout.padding = new RectOffset(4, 4, 4, 4);
            _gridLayout.spacing = new Vector2(CareerUiTheme.Space2, CareerUiTheme.Space2);
            _gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            _gridLayout.constraintCount = TileColumnCount;
            _gridLayout.cellSize = new Vector2(250f, TileHeight);
        }

        private void BuildPlayerCardFilterBar(RectTransform parent)
        {
            _playerCardFilterBar = OwnerRuntimeUiFactory.CreateRect("PlayerCardFilters", parent);
            HorizontalLayoutGroup layout = OwnerWorkspaceUiFactory.AddHorizontalLayout(
                _playerCardFilterBar, CareerUiTheme.Space2);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childForceExpandWidth = false;
            AddFixedHeight(_playerCardFilterBar, 32f);
            _playerCardFilterBar.gameObject.SetActive(false);
        }

        /// <summary>월드 카드에서 만든 실제 구단·연도만 선택지로 노출하고 현재 선택을 재바인딩 뒤에도 보존한다.</summary>
        private void RebuildPlayerCardFilters()
        {
            if (_playerCardFilterBar == null || _snapshot == null)
                return;
            ClearStorefrontChildren(_playerCardFilterBar);

            ShopTabSnapshot playerTab = FindTab(ShopTab.PlayerCard);
            var franchiseNames = new Dictionary<string, string>(StringComparer.Ordinal);
            var years = new List<int>();
            if (playerTab != null)
            {
                for (int index = 0; index < playerTab.Tiles.Count; index++)
                {
                    ShopProductTileSnapshot tile = playerTab.Tiles[index];
                    if (tile.TargetFranchiseId.Length > 0 &&
                        !franchiseNames.ContainsKey(tile.TargetFranchiseId))
                    {
                        franchiseNames.Add(tile.TargetFranchiseId, tile.TargetFranchiseName);
                    }
                    if (tile.TargetYear.HasValue && !years.Contains(tile.TargetYear.Value))
                        years.Add(tile.TargetYear.Value);
                }
            }

            var franchises = new List<KeyValuePair<string, string>>(franchiseNames);
            franchises.Sort((left, right) =>
            {
                int byName = string.Compare(left.Value, right.Value, StringComparison.CurrentCulture);
                return byName != 0 ? byName : string.CompareOrdinal(left.Key, right.Key);
            });
            years.Sort((left, right) => right.CompareTo(left));
            if (!franchiseNames.ContainsKey(_playerCardFranchiseFilter))
                _playerCardFranchiseFilter = string.Empty;
            if (_playerCardYearFilter.HasValue && !years.Contains(_playerCardYearFilter.Value))
                _playerCardYearFilter = null;

            Text label = OwnerRuntimeUiFactory.CreateText(
                "FilterLabel", _playerCardFilterBar, "대상", 12, FontStyle.Bold,
                TextAnchor.MiddleLeft, CareerUiTheme.ReferenceTextSecondary);
            var labelLayout = label.gameObject.AddComponent<LayoutElement>();
            labelLayout.minWidth = 36f;
            labelLayout.preferredWidth = 36f;

            var franchiseLabels = new List<string>(franchises.Count + 1) { "전체 구단" };
            for (int index = 0; index < franchises.Count; index++)
                franchiseLabels.Add(franchises[index].Value);
            var yearLabels = new List<string>(years.Count + 1) { "전체 연도" };
            for (int index = 0; index < years.Count; index++)
                yearLabels.Add(years[index] + "년");

            int franchiseIndex = 0;
            for (int index = 0; index < franchises.Count; index++)
                if (string.Equals(franchises[index].Key, _playerCardFranchiseFilter, StringComparison.Ordinal))
                    franchiseIndex = index + 1;
            int yearIndex = _playerCardYearFilter.HasValue
                ? years.IndexOf(_playerCardYearFilter.Value) + 1
                : 0;
            Dropdown franchiseDropdown = CreatePlayerCardFilterDropdown(
                "FranchiseFilter", franchiseLabels, franchiseIndex, 190f);
            Dropdown yearDropdown = CreatePlayerCardFilterDropdown(
                "YearFilter", yearLabels, yearIndex, 130f);
            franchiseDropdown.onValueChanged.AddListener(index =>
            {
                _playerCardFranchiseFilter = index == 0 ? string.Empty : franchises[index - 1].Key;
                _selectedProductId = string.Empty;
                _scrollByTab[ShopTab.PlayerCard] = 1f;
                RefreshTiles();
            });
            yearDropdown.onValueChanged.AddListener(index =>
            {
                _playerCardYearFilter = index == 0 ? (int?)null : years[index - 1];
                _selectedProductId = string.Empty;
                _scrollByTab[ShopTab.PlayerCard] = 1f;
                RefreshTiles();
            });
        }

        private Dropdown CreatePlayerCardFilterDropdown(
            string name,
            List<string> options,
            int selectedIndex,
            float width)
        {
            GameObject root = DefaultControls.CreateDropdown(new DefaultControls.Resources());
            root.name = name;
            root.transform.SetParent(_playerCardFilterBar, false);
            root.AddComponent<CareerUiPreserveTextColor>();
            root.GetComponent<Image>().color = new Color32(228, 233, 237, 255);
            var layout = root.AddComponent<LayoutElement>();
            layout.minWidth = width;
            layout.preferredWidth = width;
            layout.minHeight = 28f;
            Dropdown dropdown = root.GetComponent<Dropdown>();
            dropdown.ClearOptions();
            dropdown.AddOptions(options);
            dropdown.SetValueWithoutNotify(selectedIndex);
            foreach (Text text in root.GetComponentsInChildren<Text>(true))
            {
                text.font = Baseball.Presentation.UI.UIProjectFonts.Default;
                text.fontSize = 12;
                text.color = CareerUiTheme.ReferenceText;
                text.alignment = TextAnchor.MiddleLeft;
                text.raycastTarget = false;
            }
            dropdown.captionText.rectTransform.offsetMin = new Vector2(10f, 1f);
            dropdown.captionText.rectTransform.offsetMax = new Vector2(-26f, -1f);
            root.transform.Find("Arrow").gameObject.SetActive(false);
            Text arrow = OwnerWorkspaceUiFactory.CreateText(
                root.transform, "DropdownArrow", "▾", 14, FontStyle.Bold,
                TextAnchor.MiddleCenter, CareerUiTheme.ReferenceText);
            OwnerRuntimeUiFactory.SetAnchors(
                arrow.rectTransform, new Vector2(1f, 0f), Vector2.one,
                new Vector2(-24f, 0f), Vector2.zero);
            dropdown.template.GetComponent<Image>().color = new Color32(240, 243, 246, 255);
            dropdown.template.sizeDelta = new Vector2(0f, Mathf.Min(7, options.Count) * 28f + 8f);
            dropdown.itemText.transform.parent.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 28f);
            Toggle toggle = dropdown.itemText.GetComponentInParent<Toggle>(true);
            toggle.targetGraphic.color = new Color32(215, 225, 235, 255);
            toggle.graphic.color = CareerUiTheme.ReferenceAccent;
            if (UIOwnerFrontOfficeSkin.IsOwnerContext)
            {
                OwnerDashboardStyle.SetDataDropdown(dropdown);
                OwnerDashboardStyle.SetDataText(arrow);
                toggle.graphic.color = OwnerDashboardStyle.Gold;
            }
            return dropdown;
        }

        private ShopTabSnapshot FindTab(ShopTab tab)
        {
            for (int index = 0; index < _snapshot.Tabs.Count; index++)
                if (_snapshot.Tabs[index].Tab == tab) return _snapshot.Tabs[index];
            return null;
        }

        private void BuildPreviewColumn(RectTransform parent)
        {
            Image surface = OwnerRuntimeUiFactory.CreateImage(
                "SelectedProduct", parent, new Color32(241, 242, 238, 255));
            _previewRoot = surface.rectTransform;
            if (UIOwnerFrontOfficeSkin.IsOwnerContext) UIOwnerFrontOfficePanel.Apply(_previewRoot, "ManagerReport");
            VerticalLayoutGroup layout = OwnerWorkspaceUiFactory.AddVerticalLayout(
                _previewRoot, CareerUiTheme.Space2);
            layout.padding = new RectOffset(16, 16, 14, 14);
            OwnerWorkspaceUiFactory.SetFlexible(_previewRoot, .75f, 1f);
            _previewRoot.GetComponent<LayoutElement>().minWidth = 340f;

            Text header = OwnerRuntimeUiFactory.CreateText(
                "Header", _previewRoot, "선택 상품", 16, FontStyle.Bold,
                TextAnchor.MiddleLeft, CareerUiTheme.ReferenceText);
            AddFixedHeight(header.rectTransform, 28f);

            Image artworkFrame = OwnerRuntimeUiFactory.CreateImage(
                "ArtworkFrame", _previewRoot, ArtworkBackground);
            if (UIOwnerFrontOfficeSkin.IsOwnerContext) OwnerDashboardStyle.ApplyInset(artworkFrame);
            var artworkLayout = artworkFrame.gameObject.AddComponent<LayoutElement>();
            artworkLayout.minHeight = 120f;
            artworkLayout.preferredHeight = 210f;
            artworkLayout.flexibleHeight = 1f;
            _previewArtwork = ShopArtwork.Create(
                artworkFrame.transform, "Artwork", ShopArtwork.PlayerPackKey, Color.white);
            OwnerRuntimeUiFactory.Stretch(
                _previewArtwork.rectTransform, new Vector2(12f, 8f), new Vector2(-12f, -8f));
            var aspect = _previewArtwork.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspect.aspectRatio = 1f;

            _previewTitle = CreatePreviewText("Title", 22, FontStyle.Bold, TextAnchor.MiddleCenter, 32f);
            _previewSubtitle = CreatePreviewText(
                "Subtitle", 13, FontStyle.Normal, TextAnchor.MiddleCenter, 22f);
            _previewCount = CreatePreviewText("Count", 14, FontStyle.Bold, TextAnchor.MiddleCenter, 24f);
            _previewPrice = CreatePreviewText("Price", 22, FontStyle.Bold, TextAnchor.MiddleRight, 32f);
            _previewStatus = CreatePreviewText("Status", 12, FontStyle.Normal, TextAnchor.MiddleRight, 22f);

            BuildPityGauge(_previewRoot);

            RectTransform actions = OwnerRuntimeUiFactory.CreateRect("Actions", _previewRoot);
            HorizontalLayoutGroup actionLayout = OwnerWorkspaceUiFactory.AddHorizontalLayout(
                actions, CareerUiTheme.Space2);
            actionLayout.childAlignment = TextAnchor.MiddleRight;
            actionLayout.childForceExpandWidth = true;
            AddFixedHeight(actions, 38f);
            _previewDetailsButton = OwnerWorkspaceUiFactory.CreateButton(
                actions, "Details", "확률 상세", RequestSelectedDetails);
            _previewPurchaseButton = OwnerWorkspaceUiFactory.CreateButton(
                actions, "Purchase", "구입", RequestSelectedPurchase);
            AddFixedHeight((RectTransform)_previewDetailsButton.transform, 38f);
            AddFixedHeight((RectTransform)_previewPurchaseButton.transform, 38f);
            _previewPurchaseButton.GetComponent<Image>().color = CareerUiTheme.ReferenceAccentLight;
            _previewPurchaseButton.transform.Find("Label").GetComponent<Text>().color = Color.white;
        }

        private Text CreatePreviewText(
            string name,
            int fontSize,
            FontStyle fontStyle,
            TextAnchor anchor,
            float height)
        {
            Text text = OwnerRuntimeUiFactory.CreateText(
                name, _previewRoot, string.Empty, fontSize, fontStyle,
                anchor, CareerUiTheme.ReferenceText);
            if (UIOwnerFrontOfficeSkin.IsOwnerContext)
                OwnerDashboardStyle.SetDataText(text, fontStyle == FontStyle.Bold);
            AddFixedHeight(text.rectTransform, height);
            return text;
        }

        private void BuildPityGauge(RectTransform parent)
        {
            _pityRoot = OwnerRuntimeUiFactory.CreateRect("ScoutPity", parent);
            VerticalLayoutGroup layout = OwnerWorkspaceUiFactory.AddVerticalLayout(_pityRoot, 3f);
            AddFixedHeight(_pityRoot, 42f);
            _pityLabel = OwnerRuntimeUiFactory.CreateText(
                "Label", _pityRoot, string.Empty, 11, FontStyle.Bold,
                TextAnchor.MiddleLeft, CareerUiTheme.ReferenceTextSecondary);
            AddFixedHeight(_pityLabel.rectTransform, 18f);
            Image track = OwnerRuntimeUiFactory.CreateImage(
                "Track", _pityRoot, CareerUiTheme.ReferenceButton);
            AddFixedHeight(track.rectTransform, 10f);
            Image fill = OwnerRuntimeUiFactory.CreateImage(
                "Fill", track.transform, CareerUiTheme.ReferenceAccent);
            _pityFill = fill.rectTransform;
            OwnerRuntimeUiFactory.SetAnchors(
                _pityFill, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
        }

        private void BuildReferenceTile(ShopProductTileSnapshot tile)
        {
            Image surface = OwnerRuntimeUiFactory.CreateImage(
                "Product_" + tile.ProductId, _gridContent, CareerUiTheme.ReferencePanel);
            // 공용 Image는 장식용 기본값으로 Raycast를 끈다. 상품 타일은 이 표면 자체가
            // Button의 입력 영역이므로 명시적으로 켜야 포인터 클릭이 선택 처리까지 도달한다.
            surface.raycastTarget = true;
            if (UIOwnerFrontOfficeSkin.IsOwnerContext) OwnerDashboardStyle.ApplyInset(surface, true);
            var outline = surface.gameObject.AddComponent<Outline>();
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = false;

            var button = surface.gameObject.AddComponent<Button>();
            button.targetGraphic = surface;
            button.transition = Selectable.Transition.ColorTint;
            string productId = tile.ProductId;
            button.onClick.AddListener(() => SelectProduct(productId));
            button.interactable = !_isProcessing;
            _tileActionButtons.Add(button);
            _tileActionAvailability.Add(true);
            _productTileSurfaces.Add(surface);
            _productTileIds.Add(tile.ProductId);

            HorizontalLayoutGroup layout = OwnerWorkspaceUiFactory.AddHorizontalLayout(
                surface.rectTransform, CareerUiTheme.Space2);
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childForceExpandWidth = false;

            BuildReferenceTileArtwork(surface.rectTransform, tile);
            BuildReferenceTileText(surface.rectTransform, tile);
            ApplyProductSelection(surface, outline, tile.ProductId);
        }

        private void BuildReferenceTileArtwork(RectTransform parent, ShopProductTileSnapshot tile)
        {
            Image frame = OwnerRuntimeUiFactory.CreateImage(
                "ArtworkFrame", parent, ArtworkBackground);
            if (UIOwnerFrontOfficeSkin.IsOwnerContext) OwnerDashboardStyle.ApplyInset(frame);
            var size = frame.gameObject.AddComponent<LayoutElement>();
            size.minWidth = ArtworkSize;
            size.preferredWidth = ArtworkSize;
            // 원화와 수량 설명의 영역을 분리해 밝은 패키지에서도 글자가 묻히지 않게 한다.
            RectTransform artworkSlot = OwnerRuntimeUiFactory.CreateRect("ArtworkSlot", frame.transform);
            OwnerRuntimeUiFactory.Stretch(artworkSlot,
                new Vector2(0f, TileBadgeHeight + CareerUiTheme.Space1), Vector2.zero);
            Texture2D texture = ShopArtwork.Load(tile.ArtworkKey);
            if (texture != null)
            {
                RawImage art = ShopArtwork.Create(artworkSlot, "Artwork", tile.ArtworkKey, Color.white);
                OwnerRuntimeUiFactory.Stretch(art.rectTransform, new Vector2(3f, 3f), new Vector2(-3f, -3f));
                var aspect = art.gameObject.AddComponent<AspectRatioFitter>();
                aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                aspect.aspectRatio = texture.width / (float)texture.height;
            }

            Image count = CreateTileBadge(frame.transform, "Count", tile.CountBadgeText, false);
            OwnerRuntimeUiFactory.SetAnchors(count.rectTransform, Vector2.zero, Vector2.right,
                Vector2.zero, new Vector2(0f, TileBadgeHeight));

            if (tile.BadgeText.Length == 0) return;
            Image badge = CreateTileBadge(artworkSlot, "Badge", tile.BadgeText, true);
            OwnerRuntimeUiFactory.SetAnchors(badge.rectTransform, Vector2.up, Vector2.up,
                new Vector2(0f, -TileBadgeHeight), new Vector2(52f, 0f));
        }

        /// <summary>상품 배지는 불투명 배경과 실제 Medium 서체로 스킨 재적용 후에도 대비를 유지한다.</summary>
        private static Image CreateTileBadge(Transform parent, string name, string value, bool highlighted)
        {
            Color background = highlighted ? OwnerDashboardStyle.Gold : OwnerDashboardStyle.Raised;
            Image badge = OwnerRuntimeUiFactory.CreateImage(name, parent, background);
            OwnerDashboardStyle.SetDataSurface(badge, background);
            Text label = OwnerRuntimeUiFactory.CreateText("Label", badge.transform, value,
                TileBadgeFontSize, FontStyle.Normal, TextAnchor.MiddleCenter, OwnerDashboardStyle.Ivory);
            OwnerDashboardStyle.SetDataText(label, true);
            label.color = highlighted ? OwnerDashboardStyle.Ink : OwnerDashboardStyle.Ivory;
            OwnerRuntimeUiFactory.Stretch(label.rectTransform,
                new Vector2(CareerUiTheme.Space1, 0f), new Vector2(-CareerUiTheme.Space1, 0f));
            return badge;
        }

        private static void BuildReferenceTileText(RectTransform parent, ShopProductTileSnapshot tile)
        {
            RectTransform body = OwnerRuntimeUiFactory.CreateRect("Summary", parent);
            OwnerWorkspaceUiFactory.AddVerticalLayout(body, 2f);
            OwnerWorkspaceUiFactory.SetFlexible(body, 1f, 1f);

            Text title = OwnerRuntimeUiFactory.CreateText(
                "Title", body, tile.Title, 15, FontStyle.Bold,
                TextAnchor.MiddleLeft, CareerUiTheme.ReferenceText);
            AddFixedHeight(title.rectTransform, 24f);
            Text subtitle = OwnerRuntimeUiFactory.CreateText(
                "Subtitle", body, tile.Subtitle, 11, FontStyle.Normal,
                TextAnchor.MiddleLeft, CareerUiTheme.ReferenceAccent);
            AddFixedHeight(subtitle.rectTransform, 20f);
            Text price = OwnerRuntimeUiFactory.CreateText(
                "Price", body, tile.PriceText, 16, FontStyle.Bold,
                TextAnchor.MiddleRight, CareerUiTheme.ReferenceText);
            AddFixedHeight(price.rectTransform, 44f);
            Text status = OwnerRuntimeUiFactory.CreateText(
                "Status", body, tile.CanPurchase ? "구매 가능" : tile.BlockedReason,
                10, FontStyle.Normal, TextAnchor.MiddleRight,
                tile.CanPurchase ? CareerUiTheme.ReferenceAccent : CareerUiTheme.Error);
            AddFixedHeight(status.rectTransform, 18f);
            if (UIOwnerFrontOfficeSkin.IsOwnerContext)
            {
                OwnerDashboardStyle.SetDataText(title, true);
                OwnerDashboardStyle.SetDataText(subtitle);
                OwnerDashboardStyle.SetDataText(price, true);
                price.color = OwnerDashboardStyle.Gold;
                OwnerDashboardStyle.SetDataText(status);
                status.color = tile.CanPurchase ? OwnerDashboardStyle.Success : OwnerDashboardStyle.Danger;
            }
        }

        private void EnsureSelectedProduct(ShopTabSnapshot tab)
        {
            for (int index = 0; index < tab.Tiles.Count; index++)
            {
                ShopProductTileSnapshot tile = tab.Tiles[index];
                if (MatchesPlayerCardFilters(tab, tile) &&
                    string.Equals(tile.ProductId, _selectedProductId, StringComparison.Ordinal)) return;
            }
            int bestIndex = -1;
            int bestSpecificity = -1;
            for (int index = 0; index < tab.Tiles.Count; index++)
            {
                ShopProductTileSnapshot tile = tab.Tiles[index];
                if (!MatchesPlayerCardFilters(tab, tile)) continue;
                int specificity = GetPlayerCardFilterSpecificity(tab, tile);
                if (specificity <= bestSpecificity) continue;
                bestIndex = index;
                bestSpecificity = specificity;
            }
            _selectedProductId = bestIndex >= 0 ? tab.Tiles[bestIndex].ProductId : string.Empty;
        }

        private int GetPlayerCardFilterSpecificity(ShopTabSnapshot tab, ShopProductTileSnapshot tile)
        {
            if (tab.Tab != ShopTab.PlayerCard)
                return 0;
            int specificity = 0;
            if (_playerCardFranchiseFilter.Length > 0 &&
                string.Equals(tile.TargetFranchiseId, _playerCardFranchiseFilter, StringComparison.Ordinal))
                specificity++;
            if (_playerCardYearFilter.HasValue && tile.TargetYear == _playerCardYearFilter)
                specificity++;
            return specificity;
        }

        private int CountVisibleTiles(ShopTabSnapshot tab)
        {
            int count = 0;
            for (int index = 0; index < tab.Tiles.Count; index++)
                if (MatchesPlayerCardFilters(tab, tab.Tiles[index])) count++;
            return count;
        }

        private void BuildFilteredTiles(ShopTabSnapshot tab)
        {
            int selectedConditionCount = tab.Tab == ShopTab.PlayerCard
                ? (_playerCardFranchiseFilter.Length > 0 ? 1 : 0) + (_playerCardYearFilter.HasValue ? 1 : 0)
                : 0;
            // 정밀 일치 → 단일 조건 일치 → 공통 상품. 같은 단계는 원래 순서를 유지해
            // 단품과 10회 묶음이 항상 나란히 표시되도록 한다.
            for (int specificity = selectedConditionCount; specificity >= 0; specificity--)
            {
                for (int index = 0; index < tab.Tiles.Count; index++)
                {
                    ShopProductTileSnapshot tile = tab.Tiles[index];
                    if (MatchesPlayerCardFilters(tab, tile) &&
                        GetPlayerCardFilterSpecificity(tab, tile) == specificity)
                        BuildReferenceTile(tile);
                }
            }
        }

        /// <summary>
        /// 전체 선택에서는 구단별·연도별 상품을 탐색하고, 두 필터가 모두 선택됐을 때만
        /// 해당 조합의 정밀 Scout까지 보여 목록 폭증을 막는다.
        /// </summary>
        private bool MatchesPlayerCardFilters(ShopTabSnapshot tab, ShopProductTileSnapshot tile)
        {
            if (tab.Tab != ShopTab.PlayerCard)
                return true;

            bool targetsFranchise = tile.TargetFranchiseId.Length > 0;
            bool targetsYear = tile.TargetYear.HasValue;
            if (!targetsFranchise && !targetsYear)
                return true;
            if (targetsFranchise && targetsYear)
            {
                return _playerCardFranchiseFilter.Length > 0 &&
                    _playerCardYearFilter.HasValue &&
                    string.Equals(
                        tile.TargetFranchiseId,
                        _playerCardFranchiseFilter,
                        StringComparison.Ordinal) &&
                    tile.TargetYear.Value == _playerCardYearFilter.Value;
            }
            if (targetsFranchise)
            {
                if (_playerCardYearFilter.HasValue && _playerCardFranchiseFilter.Length == 0)
                    return false;
                return _playerCardFranchiseFilter.Length == 0 ||
                    string.Equals(
                        tile.TargetFranchiseId,
                        _playerCardFranchiseFilter,
                        StringComparison.Ordinal);
            }

            if (_playerCardFranchiseFilter.Length > 0 && !_playerCardYearFilter.HasValue)
                return false;
            return !_playerCardYearFilter.HasValue || tile.TargetYear.Value == _playerCardYearFilter.Value;
        }

        private void SelectProduct(string productId)
        {
            if (_isProcessing || string.Equals(_selectedProductId, productId, StringComparison.Ordinal)) return;
            _selectedProductId = productId;
            ShopTabSnapshot tab = _snapshot.Tabs[_selectedTabIndex];
            RefreshSelectedProduct(tab);
            for (int index = 0; index < _productTileSurfaces.Count; index++)
            {
                Outline outline = _productTileSurfaces[index].GetComponent<Outline>();
                ApplyProductSelection(_productTileSurfaces[index], outline, _productTileIds[index]);
            }
        }

        private void RefreshSelectedProduct(ShopTabSnapshot tab)
        {
            for (int index = 0; index < tab.Tiles.Count; index++)
            {
                ShopProductTileSnapshot tile = tab.Tiles[index];
                if (!MatchesPlayerCardFilters(tab, tile) ||
                    !string.Equals(tile.ProductId, _selectedProductId, StringComparison.Ordinal)) continue;
                Texture2D texture = ShopArtwork.Load(tile.ArtworkKey);
                _previewArtwork.texture = texture;
                _previewArtwork.gameObject.SetActive(texture != null);
                if (texture != null)
                    _previewArtwork.GetComponent<AspectRatioFitter>().aspectRatio = texture.width / (float)texture.height;
                _previewTitle.text = tile.Title;
                _previewSubtitle.text = tile.Subtitle;
                _previewCount.text = tile.CountBadgeText;
                _previewPrice.text = tile.PriceText;
                _previewStatus.text = tile.CanPurchase ? "구매 가능" : tile.BlockedReason;
                _previewStatus.color = tile.CanPurchase
                    ? UIOwnerFrontOfficePanel.HasDarkSurface(_previewRoot) ? OwnerDashboardStyle.Gold : CareerUiTheme.ReferenceAccent
                    : UIOwnerFrontOfficeSkin.IsOwnerContext ? OwnerDashboardStyle.Danger : CareerUiTheme.Error;
                _selectedProductCanPurchase = tile.CanPurchase;
                _previewDetailsButton.interactable = !_isProcessing;
                _previewPurchaseButton.interactable = tile.CanPurchase && !_isProcessing;
                _previewPurchaseButton.transform.Find("Label").GetComponent<Text>().text =
                    tile.CanPurchase ? "구입" : "구매 불가";
                return;
            }
            ClearSelectedProduct();
        }

        private void ClearSelectedProduct()
        {
            _selectedProductId = string.Empty;
            _selectedProductCanPurchase = false;
            if (_previewArtwork != null) _previewArtwork.gameObject.SetActive(false);
            if (_previewTitle != null) _previewTitle.text = "선택 가능한 상품이 없습니다.";
            if (_previewSubtitle != null) _previewSubtitle.text = string.Empty;
            if (_previewCount != null) _previewCount.text = string.Empty;
            if (_previewPrice != null) _previewPrice.text = string.Empty;
            if (_previewStatus != null) _previewStatus.text = string.Empty;
            if (_previewDetailsButton != null) _previewDetailsButton.interactable = false;
            if (_previewPurchaseButton != null) _previewPurchaseButton.interactable = false;
        }

        private void ApplyProductSelection(Image surface, Outline outline, string productId)
        {
            bool selected = string.Equals(productId, _selectedProductId, StringComparison.Ordinal);
            if (UIOwnerFrontOfficeSkin.IsOwnerContext)
            {
                OwnerDashboardStyle.SetDataRow(surface.GetComponent<Button>(), selected, OwnerDashboardStyle.TableSurface);
                return;
            }
            surface.color = selected ? new Color32(221, 229, 235, 255) : CareerUiTheme.ReferencePanel;
            outline.effectColor = selected ? CareerUiTheme.ReferenceAccent : CareerUiTheme.ReferenceBorder;
            outline.effectDistance = selected ? new Vector2(2f, -2f) : new Vector2(1f, -1f);
        }

        private void RefreshPityGauge(ShopTab tab)
        {
            bool visible = tab == ShopTab.PlayerCard && _snapshot != null;
            _pityRoot.gameObject.SetActive(visible);
            if (!visible) return;
            int gauge = _snapshot.ScoutPityGauge;
            int threshold = _snapshot.ScoutPityThreshold;
            _pityLabel.text = _snapshot.IsFocusedScoutReady
                ? string.Concat("집중 스카우트 준비 완료  ", gauge.ToString(), " / ", threshold.ToString())
                : string.Concat("집중 스카우트 게이지  ", gauge.ToString(), " / ", threshold.ToString());
            float ratio = threshold == 0 ? 0f : Mathf.Clamp01(gauge / (float)threshold);
            _pityFill.anchorMax = new Vector2(ratio, 1f);
            _pityFill.offsetMin = Vector2.zero;
            _pityFill.offsetMax = Vector2.zero;
        }

        private void RequestSelectedDetails()
        {
            if (_isProcessing || string.IsNullOrEmpty(_selectedProductId)) return;
            DetailsRequested?.Invoke(_selectedProductId);
        }

        private void RequestSelectedPurchase()
        {
            if (_isProcessing || !_selectedProductCanPurchase || string.IsNullOrEmpty(_selectedProductId)) return;
            PurchasePreviewRequested?.Invoke(_selectedProductId);
        }
    }
}
