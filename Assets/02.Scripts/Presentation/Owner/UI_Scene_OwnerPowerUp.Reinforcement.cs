using System;
using System.Collections.Generic;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    public sealed partial class UI_Scene_OwnerPowerUp
    {
        private RectTransform _reinforcementCanvas;
        private RectTransform _salePanel;
        private bool _hasRegisteredEnhancement;
        private bool _hideLockedCards;
        private bool _sortCostDescending = true;
        private Button _registerButton;
        private Button _hideLockedButton;
        private Button _enhancementCostSort;
        private InputField _enhancementSearch;
        private Dropdown _enhancementRoleFilter;
        private RectTransform _enhancementOriginFilters;
        private readonly OwnerCardFilters _enhancementOrigins = new OwnerCardFilters();
        private OwnerEnhancementSaleScreenSnapshot _enhancementFilterSource;
        private readonly List<string> _enhancementRoles = new List<string>();
        private readonly List<OwnerCollectionCardSnapshot> _visibleEnhancementCards = new List<OwnerCollectionCardSnapshot>();
        private bool _hasDuplicateFilter;
        private Button _duplicateFilterButton;
        private Text _enhancementEmptyResults;

        private void BuildEnhancementSale()
        {
            _reinforcementCanvas = OwnerRuntimeUiFactory.CreateRect("ReferenceReinforcement", _enhancementRoot);
            _reinforcementCanvas.anchorMin = _reinforcementCanvas.anchorMax = new Vector2(.5f, .5f);
            _reinforcementCanvas.sizeDelta = new Vector2(1000f, 460f);
            _reinforcementCanvas.gameObject.AddComponent<CareerUiPreserveTextColor>();
            Image inventory = EnhancementSurface(_reinforcementCanvas, "InventoryBoard", 0, 0, 496, 460);
            UIOwnerFrontOfficePanel.Apply(inventory.rectTransform, "ManagerReport");
            EnhancementText(_reinforcementCanvas, "InventoryTitle", "보유 선수", 20, 16, 12, 200, 28);
            _enhancementCostSort = ReferenceButton(_reinforcementCanvas, "CostSort", "코스트 높은순", () =>
            {
                _sortCostDescending = !_sortCostDescending;
                _enhancementCostSort.transform.Find("Label").GetComponent<Text>().text =
                    _sortCostDescending ? "코스트 높은순" : "코스트 낮은순";
                RefreshEnhancementFilters();
            }, 356, 12, 124, 28);
            _enhancementCardCount = EnhancementText(_reinforcementCanvas, "EnhancementCardCount", "", 12, 16, 44, 180, 24);
            ScrollRect scroll = OwnerRuntimeUiFactory.CreateVerticalGridScroll(
                "EnhancementInventory", _reinforcementCanvas, 6, new Vector2(70f, 100f), 6f,
                out _enhancementCardList);
            PlaceReference(scroll.GetComponent<RectTransform>(), 8, 148, 478, 248);
            StyleTrainingScroll(_enhancementCardList);
            _enhancementCardList.GetComponent<GridLayoutGroup>().padding = new RectOffset(4, 4, 4, 4);
            AddInventoryScrollbar(scroll);
            RectTransform scrollbar = scroll.verticalScrollbar.GetComponent<RectTransform>();
            scrollbar.anchorMin = new Vector2(1, 0);
            scrollbar.anchorMax = Vector2.one;
            scrollbar.pivot = new Vector2(1, 1);
            scrollbar.offsetMin = new Vector2(-12, 0);
            scrollbar.offsetMax = Vector2.zero;
            _enhancementGrid = new CardGrid(_enhancementCardList, SelectAndRegisterEnhancementCard,
                ShowEnhancementCardDetail);
            BuildEnhancementFilters();

            Image frame = EnhancementSurface(_reinforcementCanvas, "RegistrationFrame", 508, 0, 492, 460);
            UIOwnerFrontOfficePanel.Apply(frame.rectTransform, "ManagerReport");
            RectTransform content = OwnerRuntimeUiFactory.CreateRect("ContentSafeRect", frame.transform);
            PlaceReference(content, 20, 16, 452, 428);
            EnhancementText(content, "FusionTitle", "선수 카드 합성", 22, 0, 0, 300, 32);
            EnhancementText(content, "FusionHint", "같은 카드 한 장으로, 한 단계 더 강하게", 13, 0, 36, 452, 24).color = CareerUiTheme.RosterTextSecondary;
            EnhancementText(content, "TargetLabel", "강화할 선수", 14, 32, 72, 156, 24);
            EnhancementText(content, "MaterialLabel", "소모할 중복 카드", 14, 264, 72, 164, 24);
            SetTrainingSurface(EnhancementSurface(content, "TargetWell", 28, 100, 164, 208), CareerUiTheme.RosterBoard);
            SetTrainingSurface(EnhancementSurface(content, "MaterialWell", 260, 100, 164, 208), CareerUiTheme.RosterBoard);
            _enhancementTargetEmpty = EnhancementText(content, "TargetEmpty", "선수 선택\n\n왼쪽에서 카드를 선택하세요", 14, 32, 108, 156, 180);
            _enhancementMaterialEmpty = EnhancementText(content, "MaterialCardEmpty", "동일 카드 필요\n\n보유 중복 카드가 자동 등록됩니다", 14, 264, 108, 156, 180);
            _enhancementTargetEmpty.alignment = _enhancementMaterialEmpty.alignment = TextAnchor.MiddleCenter;
            _enhancementCard = CreateEnhancementPreview(content, "TargetCard", 36);
            _enhancementMaterialCard = CreateEnhancementPreview(content, "MaterialCard", 268);
            EnhancementText(content, "FusionOperator", "+", 30, 208, 172, 36, 48).alignment = TextAnchor.MiddleCenter;
            _enhancementDetails = EnhancementText(content, "EnhancementDetails", "", 14, 0, 308, 452, 64);
            _registerButton = ReferenceButton(content, "Register", "선택 해제", ClearEnhancementRegistration, 0, 384, 144, 44);
            _enhanceButton = ReferenceButton(content, "Enhance", "카드 합성", RequestEnhancement, 156, 384, 296, 44);
            OwnerUiButtonSkin.Apply(_enhanceButton, OwnerButtonRole.Primary);
            _enhancementWallet = EnhancementText(_reinforcementCanvas, "EnhancementWallet", "", 12, 196, 44, 284, 24);
            _enhancementWallet.alignment = TextAnchor.MiddleRight;
            ReferenceButton(_reinforcementCanvas, "OpenSale", "중복 카드 판매", () =>
                _salePanel.gameObject.SetActive(true), 16, 412, 144, 32);
            ReferenceButton(_reinforcementCanvas, "ResetEnhancementFilters", "필터 초기화", ResetEnhancementFilters,
                376, 412, 104, 32);
            GroupEnhancementInventoryContent();

            _salePanel = CreateCenteredCard(_root, "DuplicateSalePanel", new Vector2(420, 180));
            OwnerWorkspaceUiFactory.AddVerticalLayout(_salePanel, 10).padding = new RectOffset(20, 20, 16, 16);
            CreateFixedText(_salePanel, "SaleTitle", 30, 16, FontStyle.Bold, TextAnchor.MiddleCenter).text = "중복 카드 방출";
            RectTransform quantity = OwnerRuntimeUiFactory.CreateRect("SaleQuantity", _salePanel);
            OwnerWorkspaceUiFactory.AddHorizontalLayout(quantity, 8);
            SetPreferred(quantity, 32);
            CreateSizedButton(quantity, "SaleMinus", "−", () => ChangeSaleCount(-1), 56);
            CreateSizedButton(quantity, "SalePlus", "+", () => ChangeSaleCount(1), 56);
            _sellButton = OwnerWorkspaceUiFactory.CreateButton(quantity, "Sell", "선택 수량 판매", RequestSale);
            OwnerWorkspaceUiFactory.CreateButton(_salePanel, "CloseSale", "닫기", () => _salePanel.gameObject.SetActive(false));
            _salePanel.gameObject.SetActive(false);
        }

        private Text _enhancementTargetEmpty;

        private void GroupEnhancementInventoryContent()
        {
            RectTransform safe = OwnerRuntimeUiFactory.CreateRect("ContentSafeRect", _reinforcementCanvas.Find("InventoryBoard"));
            PlaceReference(safe, 8, 8, 480, 444);
            string[] names = { "InventoryTitle", "CostSort", "EnhancementCardCount", "EnhancementInventory",
                "EnhancementSearch", "EnhancementOriginFilters", "EnhancementRoleFilter", "DuplicateFilter",
                "HideLocked", "EnhancementEmptyResults", "EnhancementWallet", "OpenSale", "ResetEnhancementFilters" };
            foreach (string name in names)
            {
                RectTransform child = (RectTransform)_reinforcementCanvas.Find(name);
                child.SetParent(safe, false);
                child.anchoredPosition += new Vector2(-8, 8);
            }
        }

        private void BuildEnhancementFilters()
        {
            Image search = EnhancementSurface(_reinforcementCanvas, "EnhancementSearch", 16, 76, 192, 28);
            search.raycastTarget = true;
            _enhancementSearch = search.gameObject.AddComponent<InputField>();
            _enhancementSearch.targetGraphic = search;
            _enhancementSearch.textComponent = EnhancementText(search.transform, "SearchText", "", 12, 8, 2, 176, 24);
            Text placeholder = EnhancementText(search.transform, "SearchPlaceholder", "선수 이름 검색", 12, 8, 2, 176, 24);
            // InputField는 기본 pixelsPerUnit으로 캐럿·선택·클릭 위치를 계산하므로 글자 생성 배율도 맞춘다.
            ((UIProjectText)_enhancementSearch.textComponent).UsePanelRasterDensity = false;
            ((UIProjectText)placeholder).UsePanelRasterDensity = false;
            placeholder.color = CareerUiTheme.RosterTextSecondary;
            _enhancementSearch.placeholder = placeholder;
            _enhancementSearch.onValueChanged.AddListener(_ => RefreshEnhancementFilters());
            _enhancementOriginFilters = OwnerRuntimeUiFactory.CreateRect("EnhancementOriginFilters", _reinforcementCanvas);
            PlaceReference(_enhancementOriginFilters, 216, 76, 264, 28);
            _enhancementRoleFilter = OwnerCardFilters.CreateDropdown(_reinforcementCanvas, "EnhancementRoleFilter",
                new List<string> { "전체 포지션" }, 0);
            PlaceReference(_enhancementRoleFilter.GetComponent<RectTransform>(), 16, 112, 144, 28);
            _enhancementRoleFilter.onValueChanged.AddListener(_ => RefreshEnhancementFilters());
            _duplicateFilterButton = ReferenceButton(_reinforcementCanvas, "DuplicateFilter", "중복 보유: 전체", () =>
            {
                _hasDuplicateFilter = !_hasDuplicateFilter;
                RefreshEnhancementFilters();
            }, 168, 112, 148, 28);
            _hideLockedButton = ReferenceButton(_reinforcementCanvas, "HideLocked", "잠금 선수: 표시", () =>
            {
                _hideLockedCards = !_hideLockedCards;
                RefreshEnhancementFilters();
            }, 324, 112, 156, 28);
            _enhancementEmptyResults = EnhancementText(_reinforcementCanvas, "EnhancementEmptyResults",
                "조건에 맞는 선수가 없습니다.\n검색어나 필터를 변경해 주세요.", 14, 24, 224, 440, 80);
            _enhancementEmptyResults.alignment = TextAnchor.MiddleCenter;
            _enhancementEmptyResults.raycastTarget = false;
            _enhancementEmptyResults.gameObject.SetActive(false);
        }

        private void BindEnhancementFilterOptions(OwnerEnhancementSaleScreenSnapshot screen)
        {
            if (ReferenceEquals(_enhancementFilterSource, screen)) return;
            string previousRole = _enhancementRoleFilter.value == 0 ? string.Empty : _enhancementRoles[_enhancementRoleFilter.value - 1];
            _enhancementFilterSource = screen;
            var cards = new List<OwnerCollectionCardSnapshot>(screen.Cards.Count);
            var roles = new SortedSet<string>(StringComparer.Ordinal);
            foreach (OwnerEnhancementSaleTargetSnapshot target in screen.Cards)
            {
                cards.Add(target.Card);
                roles.Add(GetEnhancementRole(target.Card));
            }
            _enhancementRoles.Clear();
            _enhancementRoles.AddRange(roles);
            var labels = new List<string> { "전체 포지션" };
            labels.AddRange(_enhancementRoles);
            _enhancementRoleFilter.ClearOptions();
            _enhancementRoleFilter.AddOptions(labels);
            _enhancementRoleFilter.SetValueWithoutNotify(_enhancementRoles.IndexOf(previousRole) + 1);
            _enhancementRoleFilter.RefreshShownValue();
            _enhancementRoleFilter.template.sizeDelta = new Vector2(0, Math.Min(7, labels.Count) * 28 + 8);
            OwnerRuntimeUiFactory.ClearChildren(_enhancementOriginFilters);
            _enhancementOrigins.Build(_enhancementOriginFilters, cards, RefreshEnhancementFilters);
            PlaceReference((RectTransform)_enhancementOriginFilters.Find("YearFilter"), 0, 0, 112, 28);
            PlaceReference((RectTransform)_enhancementOriginFilters.Find("TeamFilter"), 120, 0, 144, 28);
        }

        private static string GetEnhancementRole(OwnerCollectionCardSnapshot card) =>
            OwnerCollectionPresentationBuilder.FormatPlayerRole(card.Position, card.PitcherRole, card.IsPositionEvidenceMissing);

        private bool MatchesEnhancementFilters(OwnerCollectionCardSnapshot card) =>
            (!_hideLockedCards || !card.IsLocked) && (!_hasDuplicateFilter || card.DuplicateCount > 0) &&
            _enhancementOrigins.Matches(card) &&
            (_enhancementRoleFilter.value == 0 || GetEnhancementRole(card) == _enhancementRoles[_enhancementRoleFilter.value - 1]) &&
            card.DisplayName.IndexOf(_enhancementSearch.text.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;

        private void RefreshEnhancementFilters()
        {
            if (_snapshot == null) return;
            ScrollRect scroll = _enhancementCardList.GetComponentInParent<ScrollRect>();
            scroll.StopMovement();
            _enhancementCardList.anchoredPosition = Vector2.zero;
            BindEnhancementSale();
        }

        private void ResetEnhancementFilters()
        {
            _enhancementSearch.SetTextWithoutNotify(string.Empty);
            _enhancementRoleFilter.SetValueWithoutNotify(0);
            _enhancementOrigins.Reset();
            foreach (Dropdown dropdown in _enhancementOriginFilters.GetComponentsInChildren<Dropdown>())
                dropdown.SetValueWithoutNotify(0);
            _hasDuplicateFilter = false;
            _hideLockedCards = false;
            RefreshEnhancementFilters();
        }

        private void RefreshEnhancementFilterLabels()
        {
            _hideLockedButton.transform.Find("Label").GetComponent<Text>().text =
                _hideLockedCards ? "잠금 선수: 숨김" : "잠금 선수: 표시";
            _duplicateFilterButton.transform.Find("Label").GetComponent<Text>().text =
                _hasDuplicateFilter ? "중복 보유만" : "중복 보유: 전체";
            OwnerUiButtonSkin.Apply(_hideLockedButton, OwnerButtonRole.Tab);
            OwnerUiButtonSkin.SetSelected(_hideLockedButton, _hideLockedCards);
            OwnerUiButtonSkin.Apply(_duplicateFilterButton, OwnerButtonRole.Tab);
            OwnerUiButtonSkin.SetSelected(_duplicateFilterButton, _hasDuplicateFilter);
        }

        private static Image EnhancementSurface(Transform parent, string name, float x, float y, float width, float height)
        {
            Image surface = ReferenceSurface(parent, name, x, y, width, height,
                CareerUiTheme.RosterSurface, CareerUiTheme.RosterDivider);
            SetTrainingSurface(surface, CareerUiTheme.RosterSurface);
            return surface;
        }

        private static Text EnhancementText(Transform parent, string name, string value, int size,
            float x, float y, float width, float height)
        {
            Text text = ReferenceText(parent, name, value, size, x, y, width, height);
            text.gameObject.AddComponent<CareerUiPreserveTextColor>();
            text.color = CareerUiTheme.RosterText;
            text.alignment = TextAnchor.MiddleLeft;
            return text;
        }

        private PlayerMiniCardView CreateEnhancementPreview(Transform parent, string name, float x)
        {
            PlayerMiniCardView card = PlayerMiniCardView.CreateRuntime(parent, name);
            // 합성에는 편성용 포지션 제목이 필요 없으므로 기본 카드가 미리보기 높이 전체를 사용한다.
            PlaceReference(card.GetComponent<RectTransform>(), x, 100, 148, 208);
            card.DetailRequested += _ => ShowEnhancementCardDetail(_selectedEnhancementCardId);
            return card;
        }

        private void LateUpdate()
        {
            ResizeScoutReference();
            ResizeTrainingPrograms();
            _trainingGrid?.Refresh();
            _enhancementGrid?.Refresh();
            if (_reinforcementCanvas == null || !_enhancementRoot.gameObject.activeInHierarchy) return;
            Rect bounds = _enhancementRoot.rect;
            float scale = Mathf.Max(.01f, Mathf.Min(bounds.width / 1000f, (bounds.height - 32f) / 460f));
            _reinforcementCanvas.localScale = Vector3.one * scale;
        }

        private void ClearEnhancementRegistration()
        {
            _hasRegisteredEnhancement = false;
            _enhancementGrid.Select(string.Empty);
            RefreshEnhancementTarget();
        }

        private static void PlaceReference(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static Image ReferenceSurface(Transform parent, string name, float x, float y,
            float width, float height, Color color, Color border)
        {
            Image image = OwnerRuntimeUiFactory.CreateImage(name, parent, color);
            PlaceReference(image.rectTransform, x, y, width, height);
            Outline outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = border;
            outline.effectDistance = new Vector2(1, -1);
            return image;
        }

        private static Text ReferenceText(Transform parent, string name, string value, int size,
            float x, float y, float width, float height)
        {
            Text text = OwnerWorkspaceUiFactory.CreateText(parent, name, value, size, FontStyle.Normal,
                TextAnchor.MiddleCenter, CareerUiTheme.ReferenceDataAccent);
            PlaceReference(text.rectTransform, x, y, width, height);
            return text;
        }

        private static Button ReferenceButton(Transform parent, string name, string label, Action action,
            float x, float y, float width, float height)
        {
            Button button = OwnerWorkspaceUiFactory.CreateButton(parent, name, label, action);
            PlaceReference(button.GetComponent<RectTransform>(), x, y, width, height);
            button.transform.Find("Label").GetComponent<Text>().fontSize = 14;
            return button;
        }

        private static void AddInventoryScrollbar(ScrollRect scroll)
        {
            Image track = ReferenceSurface(scroll.transform, "InventoryScrollbar", 466, 0, 12, 320,
                new Color32(234, 235, 235, 255), new Color32(181, 184, 187, 255));
            Scrollbar bar = track.gameObject.AddComponent<Scrollbar>();
            Image handle = OwnerRuntimeUiFactory.CreateImage("Handle", track.transform, new Color32(175, 183, 191, 255));
            bar.handleRect = handle.rectTransform;
            bar.targetGraphic = handle;
            bar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = bar;
            scroll.viewport.offsetMax = new Vector2(-14, 0);
        }
    }
}
