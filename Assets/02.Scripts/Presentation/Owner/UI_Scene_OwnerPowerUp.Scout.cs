using System;
using System.Collections.Generic;
using Baseball.Core.Shop;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    public sealed partial class UI_Scene_OwnerPowerUp
    {
        private RectTransform _scoutCanvas;
        private RectTransform _scoutResultGrid;
        private RectTransform _scoutProbabilityOverlay;
        private RectTransform _scoutPolicyOverlay;
        private RectTransform _scoutPolicyOptions;
        private Text _scoutSummary;
        private Text _scoutCost;
        private Text _scoutDispatch;
        private Text _scoutGaugeLabel;
        private Text _scoutResultTitle;
        private Text _scoutPolicyPreview;
        private Image _scoutGaugeFill;
        private RawImage _scoutPortrait;
        private RawImage _scoutPolicyPortrait;
        private Text _scoutName;
        private Text _scoutPolicyName;
        private Text _scoutStaffEffect;
        private InputField _scoutPolicySearch;
        private CanvasGroup _scoutBaseInput;
        private Button _scoutPolicyButton;
        private int _scoutPortraitIndex;
        private readonly List<OwnerScoutProductSnapshot> _filteredScoutProducts = new List<OwnerScoutProductSnapshot>();
        private static readonly string[] ScoutPortraitKeys = { "young_male", "young_female", "senior_male", "senior_female" };
        private static readonly string[] ScoutStaffIds = { "economy", "economy", "precision", "precision" };
        private Button _scoutPolicyConfirmButton;
        private string _scoutPreviewProductId = string.Empty;
        // 한 화면에 들어가는 파견 범위·방침 선택지 개수만 생성한다.
        private const int ScoutMapPageSize = 4;
        private const int ScoutPolicyPageSize = 8;
        private int _scoutMapPage;
        private int _scoutPolicyPage;
        private static readonly Color ScoutInk = OwnerDashboardStyle.Ivory;
        private static readonly Color ScoutBlue = OwnerDashboardStyle.Gold;
        private static readonly Color ScoutSilver = OwnerDashboardStyle.TableSurface;

        private void BuildScoutReference()
        {
            _scoutCanvas = OwnerRuntimeUiFactory.CreateRect("ScoutReference", _scoutRoot);
            _scoutCanvas.anchorMin = _scoutCanvas.anchorMax = new Vector2(.5f, .5f);
            _scoutCanvas.sizeDelta = new Vector2(1000, 460);
            RectTransform board = ScoutPanel(_scoutCanvas, "ScoutBoard", 0, 0, 1000, 460, ScoutSilver).rectTransform;
            _scoutBaseInput = board.gameObject.AddComponent<CanvasGroup>();
            ScoutLabel(board, "Heading", "스카우트 센터", 20, 18, 4, 220, 38, Color.white);
            _scoutWallet = ScoutLabel(board, "Wallet", "", 12, 430, 8, 550, 30, Color.white);
            _scoutWallet.alignment = TextAnchor.MiddleRight;
            ScoutPanel(board, "Destinations", 12, 60, 268, 388, Color.white);
            ScoutLabel(board, "MapHeading", "01  파견 범위", 15, 24, 68, 240, 26, ScoutBlue);
            ScoutLabel(board, "MapGuide", "구단·연도로 범위를 좁혀 선택하세요.", 11, 24, 98, 240, 24);
            BuildScoutScopeFilters(board);
            _scoutList = OwnerRuntimeUiFactory.CreateRect("ScoutProductPins", board);
            PlaceReference(_scoutList, 24, 236, 244, 196);
            ScoutPanel(board, "ScoutColumn", 292, 60, 208, 388, Color.white);
            ScoutLabel(board, "ScoutHeading", "02  전담 스카우터", 15, 304, 68, 184, 26, ScoutBlue);
            ScoutSurface(board, "PortraitBackdrop", 304, 100, 184, 124, OwnerDashboardStyle.Raised);
            _scoutPortrait = ScoutArtwork(board, "ScoutPortrait", 334, 100, 124, 124);
            _scoutName = ScoutLabel(board, "ScoutName", "", 13, 330, 224, 132, 25);
            _scoutName.alignment = TextAnchor.MiddleCenter;
            ScoutButton(board, "PreviousScout", "‹", () => ChangeScoutPortrait(-1), 304, 224, 24, 24);
            ScoutButton(board, "NextScout", "›", () => ChangeScoutPortrait(1), 464, 224, 24, 24);
            _scoutStaffEffect = ScoutLabel(board, "StaffEffect", "", 11, 304, 250, 184, 36);
            _scoutPolicyButton = ScoutButton(board, "ScoutPolicy", "탐색 방침 변경", OpenScoutPolicy, 304, 292, 184, 30);
            _scoutDispatch = ScoutLabel(board, "DispatchValue", "파견 정보를 불러오는 중입니다.", 11, 304, 328, 184, 62);
            _scoutDispatch.alignment = TextAnchor.UpperLeft;
            ScoutLabel(board, "GaugeHeading", "보장 영입", 12, 304, 392, 90, 22, ScoutBlue);
            _scoutGaugeLabel = ScoutLabel(board, "GaugeValue", "", 11, 394, 392, 94, 22);
            _scoutGaugeLabel.alignment = TextAnchor.MiddleRight;
            ScoutSurface(board, "GaugeTrack", 304, 422, 184, 6, OwnerDashboardStyle.Line);
            _scoutGaugeFill = ScoutSurface(board, "GaugeFill", 304, 422, 0, 6, ScoutBlue);
            ScoutPanel(board, "PlayerArea", 512, 60, 476, 302, Color.white);
            _scoutResultTitle = ScoutLabel(board, "ResultTitle", "영입 결과", 15, 526, 68, 270, 26, ScoutBlue);
            ScoutButton(board, "ScoutInformationTab", "영입 확률·보장 안내", OpenScoutProbability, 825, 68, 150, 26);
            _scoutResultGrid = OwnerRuntimeUiFactory.CreateRect("ScoutCards", board);
            PlaceReference(_scoutResultGrid, 526, 102, 448, 250);
            BindScoutResults(Array.Empty<ShopGrantedItem>());
            _scoutCost = ScoutLabel(board, "ScoutCost", "파견 비용  —", 17, 526, 366, 448, 28, ScoutBlue);
            _scoutSummary = ScoutLabel(board, "ScoutSummary", "상품 정보를 불러오는 중입니다.", 11, 526, 393, 448, 24);
            _scoutPurchaseButton = ScoutButton(board, "ScoutPurchase", "스카우트 파견", RequestScoutPurchase, 750, 420, 224, 28, role: OwnerButtonRole.Primary);
            ScoutButton(board, "OpenWishlist", "위시리스트", () => WishlistRequested?.Invoke(), 526, 420, 212, 28);
            BuildScoutProbabilityWindow();
            BuildScoutPolicyWindow();
            ChangeScoutPortrait(0);
        }

        private void BuildScoutProbabilityWindow()
        {
            _scoutProbabilityOverlay = ScoutPanel(_scoutCanvas, "ProbabilityWindow", 505, 38, 487, 414, ScoutSilver, true).rectTransform;
            _scoutProbabilityOverlay.GetComponent<Image>().raycastTarget = true;
            ScoutLabel(_scoutProbabilityOverlay, "Heading", "스카우트 정보 · 실제 영입 확률", 15, 12, 5, 380, 28, ScoutBlue);
            ScoutButton(_scoutProbabilityOverlay, "CloseProbability", "닫기", CloseScoutProbability, 406, 6, 68, 26);
            RectTransform viewport = ScoutSurface(_scoutProbabilityOverlay, "ProbabilityViewport", 12, 42, 462, 356, OwnerDashboardStyle.TableSurface).rectTransform;
            viewport.GetComponent<Image>().raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            RectTransform content = OwnerRuntimeUiFactory.CreateRect("ProbabilityRows", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1);
            content.sizeDelta = Vector2.zero;
            _scoutDetails = ScoutLabel(content, "ScoutDetails", "", 13, 0, 0, 440, 0);
            _scoutDetails.alignment = TextAnchor.UpperLeft;
            _scoutDetails.rectTransform.anchorMin = new Vector2(0, 1);
            _scoutDetails.rectTransform.anchorMax = Vector2.one;
            _scoutDetails.rectTransform.sizeDelta = new Vector2(-12, 0);
            _scoutDetails.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24;
            _scoutProbabilityOverlay.gameObject.SetActive(false);
        }

        private void BuildScoutPolicyWindow()
        {
            _scoutPolicyOverlay = ScoutSurface(_scoutCanvas, "ScoutPolicyShade", 0, 0, 1000, 460,
                new Color(.03f, .06f, .1f, .75f)).rectTransform;
            _scoutPolicyOverlay.GetComponent<Image>().raycastTarget = true;
            RectTransform window = ScoutPanel(_scoutPolicyOverlay, "ScoutPolicyWindow", 70, 12, 860, 436, ScoutSilver, true).rectTransform;
            Image header = ScoutSurface(window, "PolicyHeader", 0, 0, 860, 42, ScoutBlue);
            header.enabled = false;
            ScoutLabel(header.transform, "PolicyHeading", "탐색 방침 선택", 18, 18, 5, 470, 32, OwnerDashboardStyle.Ivory);
            ScoutButton(window, "ClosePolicy", "×", CloseScoutPolicy, 816, 8, 28, 28);
            ScoutPanel(window, "ScoutCardBorder", 14, 54, 218, 324, Color.white);
            _scoutPolicyPortrait = ScoutArtwork(window, "PolicyScoutPortrait", 53, 64, 140, 140);
            _scoutPolicyName = ScoutLabel(window, "PolicyScoutName", "", 15, 25, 208, 196, 26);
            _scoutPolicyName.alignment = TextAnchor.MiddleCenter;
            _scoutPolicyPreview = ScoutLabel(window, "PolicyPreview", "방침을 선택하세요.", 11, 26, 240, 194, 132);
            _scoutPolicyPreview.alignment = TextAnchor.UpperLeft;
            ScoutLabel(window, "PolicyListHeading", "구단·연도·탐색 방침으로 검색", 12, 250, 52, 310, 24, ScoutBlue);
            Image search = ScoutSurface(window, "PolicySearch", 250, 80, 594, 30, Color.white);
            search.raycastTarget = true;
            _scoutPolicySearch = search.gameObject.AddComponent<InputField>();
            _scoutPolicySearch.targetGraphic = search;
            _scoutPolicySearch.textComponent = ScoutLabel(search.transform, "SearchText", "", 13, 10, 2, 574, 26);
            _scoutPolicySearch.placeholder = ScoutLabel(search.transform, "SearchPlaceholder", "예: 전국, 구단명, 2024", 12, 10, 2, 574, 26, OwnerDashboardStyle.Muted);
            OwnerDashboardStyle.SetDataInput(_scoutPolicySearch);
            _scoutPolicySearch.onValueChanged.AddListener(_ => { _scoutPolicyPage = 0; RebuildScoutPolicyOptions(); });
            _scoutPolicyScopeButton = ScoutButton(window, "PolicyScopeFilter", "선택 범위", ToggleScoutPolicyScope,
                704, 48, 140, 28, true, OwnerButtonRole.Secondary);
            _scoutPolicyOptions = OwnerRuntimeUiFactory.CreateRect("PolicyOptions", window);
            PlaceReference(_scoutPolicyOptions, 250, 120, 594, 266);
            _scoutPolicyConfirmButton = ScoutButton(window, "ConfirmPolicy", "방침 적용", ApplyScoutPolicy, 620, 396, 224, 28, role: OwnerButtonRole.Primary);
            ScoutButton(window, "CancelPolicy", "취소", CloseScoutPolicy, 488, 396, 120, 28);
            ScoutLabel(window, "PolicyHint", "정밀도가 높을수록 고코스트 선수 영입에 유리합니다.\n같은 타입의 남녀는 효과가 같습니다. 비용은 파견 시 사용합니다.", 11, 18, 392, 450, 36);
            _scoutPolicyOverlay.gameObject.SetActive(false);
        }

        private void OpenScoutPolicy()
        {
            _scoutPreviewProductId = _selectedScoutProductId;
            _hasScoutPolicyScopeFilter = true;
            _scoutPolicyScope = FindScoutProduct(_selectedScoutProductId)?.Scope ?? string.Empty;
            _scoutPolicyScopeButton.transform.Find("Label").GetComponent<Text>().text = "선택 범위 ▾";
            OwnerUiButtonSkin.SetSelected(_scoutPolicyScopeButton, true);
            _scoutPolicySearch.SetTextWithoutNotify(string.Empty);
            _scoutPolicyPage = 0;
            PreviewScoutPolicy(_scoutPreviewProductId);
            _scoutPolicyOverlay.gameObject.SetActive(true);
            _scoutPolicyOverlay.SetAsLastSibling();
            _scoutBaseInput.interactable = false;
            _scoutBaseInput.blocksRaycasts = false;
            _scoutPolicySearch.Select();
        }

        private void CloseScoutPolicy()
        {
            _scoutPolicyOverlay.gameObject.SetActive(false);
            _scoutBaseInput.interactable = true;
            _scoutBaseInput.blocksRaycasts = true;
            _scoutPolicyButton.Select();
        }

        private void RebuildScoutPolicyOptions()
        {
            OwnerRuntimeUiFactory.ClearChildren(_scoutPolicyOptions);
            _filteredScoutProducts.Clear();
            if (_snapshot == null) return;
            string query = _scoutPolicySearch.text.Trim();
            foreach (OwnerScoutProductSnapshot product in _snapshot.Scout.Products)
                if (MatchesScoutStaff(product) && (!_hasScoutPolicyScopeFilter || product.Scope == _scoutPolicyScope)
                    && MatchesScoutQuery(product, query))
                    _filteredScoutProducts.Add(product);
            int pageCount = Math.Max(1, (_filteredScoutProducts.Count + ScoutPolicyPageSize - 1) / ScoutPolicyPageSize);
            _scoutPolicyPage = Mathf.Clamp(_scoutPolicyPage, 0, pageCount - 1);
            int start = _scoutPolicyPage * ScoutPolicyPageSize;
            int end = Math.Min(_filteredScoutProducts.Count, start + ScoutPolicyPageSize);
            for (int index = start; index < end; index++)
            {
                OwnerScoutProductSnapshot product = _filteredScoutProducts[index];
                bool selected = product.ProductId == _scoutPreviewProductId;
                ScoutButton(_scoutPolicyOptions, "PolicyChoice" + index,
                    (selected ? "✓ " : "") + product.Scope + "\n" +
                    DescribeScoutPolicy(product) + "\n" + product.DrawCount + "회 · " + product.PriceText,
                    () => PreviewScoutPolicy(product.ProductId),
                    (index - start) % 2 * 302, (index - start) / 2 * 58, 292, 54, selected, OwnerButtonRole.ListItem);
            }
            if (_filteredScoutProducts.Count == 0)
                ScoutLabel(_scoutPolicyOptions, "NoPolicyResults", "검색 결과가 없습니다. 다른 구단명이나 연도를 입력하세요.",
                    13, 10, 40, 574, 80).alignment = TextAnchor.MiddleCenter;
            ScoutButton(_scoutPolicyOptions, "PreviousPolicyPage", "이전", () =>
            {
                _scoutPolicyPage--;
                RebuildScoutPolicyOptions();
                FocusScoutControl(_scoutPolicyOptions, "NextPolicyPage");
            }, 0, 236, 72, 26).interactable = _scoutPolicyPage > 0;
            ScoutLabel(_scoutPolicyOptions, "PolicyPage", $"{_scoutPolicyPage + 1} / {pageCount}  ·  {_filteredScoutProducts.Count}개 방침",
                12, 85, 236, 420, 26).alignment = TextAnchor.MiddleCenter;
            ScoutButton(_scoutPolicyOptions, "NextPolicyPage", "다음", () =>
            {
                _scoutPolicyPage++;
                RebuildScoutPolicyOptions();
                FocusScoutControl(_scoutPolicyOptions, "PreviousPolicyPage");
            }, 522, 236, 72, 26).interactable = _scoutPolicyPage + 1 < pageCount;
        }

        private void BindScoutMapPagination(int count)
        {
            int pageCount = Math.Max(1, (count + ScoutMapPageSize - 1) / ScoutMapPageSize);
            ScoutButton(_scoutList, "PreviousMapPage", "이전", () =>
            {
                _scoutMapPage--;
                BindScout();
                FocusScoutControl(_scoutList, "NextMapPage");
            }, 0, 168, 62, 26).interactable = _scoutMapPage > 0;
            ScoutLabel(_scoutList, "MapPage", $"{_scoutMapPage + 1} / {pageCount} · {count}곳", 11, 66, 168, 112, 26)
                .alignment = TextAnchor.MiddleCenter;
            ScoutButton(_scoutList, "NextMapPage", "다음", () =>
            {
                _scoutMapPage++;
                BindScout();
                FocusScoutControl(_scoutList, "PreviousMapPage");
            }, 182, 168, 62, 26).interactable = _scoutMapPage + 1 < pageCount;
        }

        private void PreviewScoutPolicy(string productId)
        {
            OwnerScoutProductSnapshot product = FindScoutProduct(productId);
            if (product == null)
            {
                _scoutPolicyPreview.text = "선택할 수 있는 방침이 없습니다.";
                _scoutPolicyConfirmButton.interactable = false;
                return;
            }
            _scoutPreviewProductId = product.ProductId;
            _scoutPolicyPreview.text = DescribeScoutStaffEffect(product) + "\n\n" + product.Scope + "\n" +
                                       DescribeScoutPolicy(product) + "\n" +
                                       product.DrawCount + "명 탐색 · " + product.PriceText + "\n" +
                                       "후보 " + product.CandidateCount.ToString("N0") + "장 · 위시 " +
                                       product.WishlistCandidateCount.ToString("N0") + "장 포함";
            _scoutPolicyConfirmButton.interactable = true;
            RebuildScoutPolicyOptions();
            if (_scoutPolicyOverlay.gameObject.activeSelf) _scoutPolicyConfirmButton.Select();
        }

        private void ApplyScoutPolicy()
        {
            OwnerScoutProductSnapshot selected = FindScoutProduct(_scoutPreviewProductId);
            if (selected == null) return;
            _selectedScoutProductId = _scoutPreviewProductId;
            RevealScoutScope(selected);
            CloseScoutPolicy();
            BindScout();
        }

        private void RefreshScoutReference(OwnerScoutProductSnapshot product)
        {
            _scoutCost.text = product.DrawCount + "명 파견  ·  " + product.PriceText;
            _scoutSummary.text = product.CanPurchase
                ? "후보 " + product.CandidateCount.ToString("N0") + "장 · 위시 " +
                  product.WishlistCandidateCount.ToString("N0") + "장 포함 · 확정 즉시 영입"
                : product.BlockedReason;
            _scoutSummary.color = product.CanPurchase ? OwnerDashboardStyle.Ivory : CareerUiTheme.Error;
            _scoutStaffEffect.text = DescribeScoutStaffEffect(product);
            _scoutName.text = product.Staff?.DisplayName ?? "스카우터";
            _scoutPolicyName.text = _scoutName.text;
            _scoutDispatch.text = product.Scope + "\n" + DescribeScoutPolicy(product) + "\n" +
                product.DrawCount + "명 탐색";
            _scoutGaugeLabel.text = product.PityGauge.ToString("N0") + " / " + product.PityThreshold.ToString("N0");
            float fill = product.PityThreshold > 0 ? Mathf.Clamp01((float)product.PityGauge / product.PityThreshold) : 0;
            _scoutGaugeFill.rectTransform.sizeDelta = new Vector2(184 * fill, 6);
        }

        private void BindScoutResults(ShopGrantedItem[] items)
        {
            OwnerRuntimeUiFactory.ClearChildren(_scoutResultGrid);
            _scoutResultTitle.text = items.Length == 0 ? "03  영입 리포트" : "영입 완료 · " + items.Length + "명";
            if (items.Length == 0)
            {
                Texture2D environment = Resources.Load<Texture2D>("UI/OwnerPowerUp/Scouts/scout_office_v1");
                if (environment != null)
                {
                    ScoutArtwork(_scoutResultGrid, "ScoutingOffice", 0, 0, 448, 250).texture = environment;
                    ScoutSurface(_scoutResultGrid, "OfficeReadability", 0, 0, 448, 250, new Color(.03f, .09f, .16f, .58f));
                }
                Color titleColor = environment != null ? Color.white : ScoutBlue;
                Color bodyColor = environment != null ? new Color32(231, 239, 247, 255) : ScoutInk;
                ScoutLabel(_scoutResultGrid, "EmptyHeading", "다음 주전과의 첫 만남", 22, 24, 60, 400, 40, titleColor)
                    .alignment = TextAnchor.MiddleCenter;
                ScoutLabel(_scoutResultGrid, "EmptyGuide",
                    "파견 범위와 탐색 방침을 선택하세요.\n영입한 선수는 이곳에서 바로 확인할 수 있습니다.",
                    13, 24, 106, 400, 56, bodyColor).alignment = TextAnchor.MiddleCenter;
                ScoutLabel(_scoutResultGrid, "EmptyHint", "원하는 선수는 위시리스트에 담아두세요.",
                    11, 24, 190, 400, 26, bodyColor).alignment = TextAnchor.MiddleCenter;
                return;
            }
            // 현재 상품은 최대 10명이지만 지급 개수가 늘어도 모든 결과를 조회할 수 있다.
            RectTransform viewport = ScoutSurface(_scoutResultGrid, "ResultViewport", 0, 0, 448, 250, Color.clear).rectTransform;
            viewport.GetComponent<Image>().raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            RectTransform content = OwnerRuntimeUiFactory.CreateRect("ResultRows", viewport);
            PlaceReference(content, 0, 0, 448, Math.Max(250, ((items.Length + 4) / 5) * 125));
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30;
            for (int index = 0; index < items.Length; index++)
            {
                RectTransform slot = ScoutSurface(content, "CardSlot" + index, index % 5 * 90, index / 5 * 125, 84, 120,
                    OwnerDashboardStyle.TableAlternate).rectTransform;
                ShopGrantedItem item = items[index];
                OwnerCardTrainingTargetSnapshot target = FindTrainingTarget(item.ItemId);
                if (target != null)
                {
                    PlayerMiniCardView card = PlayerMiniCardView.CreateRuntime(slot, "GrantedCard");
                    card.UseLineupSlotLayout();
                    PlaceReference(card.GetComponent<RectTransform>(), 2, 2, 80, 99);
                    OwnerCollectionCardSnapshot granted = _snapshot.ResolveCard(target.Card);
                    card.Bind(OwnerCollectionPresentationBuilder.CreateMiniCard(granted, false),
                        PlayerPortraitSprites.GetDefault(granted.Position));
                    card.DetailRequested += _ => UI_Popup_OwnerPlayerCard.Show(transform, granted);
                }
                else
                    ScoutLabel(slot, "GrantedName", item.DisplayName + "\n" + item.GradeLabel, 12, 4, 10, 76, 84)
                        .alignment = TextAnchor.MiddleCenter;
                ScoutLabel(slot, "GrantedStatus", item.WasWishlisted ? "위시 영입" : item.IsNew ? "신규 영입" : "중복 획득",
                    10, 2, 102, 80, 18, item.IsNew ? ScoutBlue : ScoutInk).alignment = TextAnchor.MiddleCenter;
            }
        }

        private void ResizeScoutReference()
        {
            if (_scoutCanvas == null || !_scoutRoot.gameObject.activeInHierarchy) return;
            Rect bounds = _scoutRoot.rect;
            _scoutCanvas.localScale = Vector3.one * Mathf.Max(.01f, Mathf.Min(bounds.width / 1000, (bounds.height - 32) / 460));
            // 하단 피드백 32px를 한쪽에 확보한다. 중앙 정렬만 하면 양쪽 16px로 나뉜다.
            _scoutCanvas.anchoredPosition = new Vector2(0, 16);
        }

        private static void CreateScoutReferencePin(Transform parent, string name, string label, Action action, bool selected, int index, int count)
        {
            string caption = label.Replace("선수 카드", string.Empty).Trim(' ', '·');
            ScoutButton(parent, name, (selected ? "✓  " : "") + caption, action, 0, index * 40, 244, 32, selected, OwnerButtonRole.ListItem);
        }

        private static string DescribeScoutPolicy(OwnerScoutProductSnapshot product)
        {
            return product.PolicyName;
        }

        private void ChangeScoutPortrait(int direction)
        {
            _scoutPortraitIndex = (_scoutPortraitIndex + direction + ScoutPortraitKeys.Length) % ScoutPortraitKeys.Length;
            RefreshScoutPortrait();
            OwnerScoutProductSnapshot current = FindScoutProduct(_selectedScoutProductId);
            if (current == null) return;
            foreach (OwnerScoutProductSnapshot product in _snapshot.Scout.Products)
            {
                if (!MatchesScoutStaff(product) || product.Scope != current.Scope ||
                    product.DrawCount != current.DrawCount || product.IsGuaranteed != current.IsGuaranteed) continue;
                _selectedScoutProductId = product.ProductId;
                BindScout();
                return;
            }
        }

        private bool MatchesScoutStaff(OwnerScoutProductSnapshot product)
        {
            return product.Staff == null || product.Staff.Id == ScoutStaffIds[_scoutPortraitIndex];
        }

        private static string DescribeScoutStaffEffect(OwnerScoutProductSnapshot product)
        {
            return product.IsGuaranteed ? "보장 영입 · 모든 스카우터 동일\n타입별 비용·정밀도 효과 제외"
                : product.Staff?.Description ?? string.Empty;
        }

        private void RefreshScoutPortrait()
        {
            Texture2D portrait = Resources.Load<Texture2D>("UI/OwnerPowerUp/Scouts/scout_" + ScoutPortraitKeys[_scoutPortraitIndex] + "_v1");
            _scoutPortrait.texture = portrait;
            _scoutPolicyPortrait.texture = portrait;
        }

        private void ApplyScoutChrome()
        {
            Transform panel = _root.Find("PowerUpPanel");
            panel.Find("ThinBorder").gameObject.SetActive(false);
            UIOwnerFrontOfficePanel.ApplyFramedSurface(panel.GetComponent<RectTransform>());
            Text heading = panel.GetComponent<CareerUiFrame>().HeaderRoot.GetComponent<Text>();
            heading.text = "선수 영입   /   다음 시즌의 전력을 준비하세요";
            heading.color = OwnerDashboardStyle.Ivory;
            OwnerDashboardStyle.SetTypography(heading, true);
            panel.Find("HeaderSurface").GetComponent<Image>().enabled = false;
            panel.Find("HeaderAccent").GetComponent<Image>().enabled = false;
        }

        private void OpenScoutProbability()
        {
            _scoutProbabilityOverlay.gameObject.SetActive(true);
            _scoutProbabilityOverlay.SetAsLastSibling();
            _scoutBaseInput.interactable = false;
            _scoutBaseInput.blocksRaycasts = false;
            FocusScoutControl(_scoutProbabilityOverlay, "CloseProbability");
        }

        private void CloseScoutProbability()
        {
            _scoutProbabilityOverlay.gameObject.SetActive(false);
            _scoutBaseInput.interactable = true;
            _scoutBaseInput.blocksRaycasts = true;
            FocusScoutControl(_scoutBaseInput.transform, "ScoutInformationTab");
        }

        private static void FocusScoutControl(Transform root, string name)
        {
            Transform target = root.Find(name);
            if (target != null && target.TryGetComponent(out Button button) && button.IsInteractable()) button.Select();
        }

        private static RawImage ScoutArtwork(Transform parent, string name, float x, float y, float width, float height)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            image.transform.SetParent(parent, false);
            PlaceReference(image.rectTransform, x, y, width, height);
            image.raycastTarget = false;
            return image;
        }

        private static Button ScoutButton(Transform parent, string name, string value, Action action,
            float x, float y, float width, float height, bool selected = false, OwnerButtonRole role = OwnerButtonRole.Quiet)
        {
            // DataImage는 공용 버튼 스킨의 제외 대상이므로 V2 컨트롤 팩토리를 사용한다.
            Button button = OwnerWorkspaceUiFactory.CreateButton(parent, name, value, action);
            PlaceReference(button.GetComponent<RectTransform>(), x, y, width, height);
            Text label = button.transform.Find("Label").GetComponent<Text>();
            PlaceReference(label.rectTransform, 8, 2, width - 16, height - 4);
            label.fontSize = 12;
            OwnerDashboardStyle.SetTypography(label, role == OwnerButtonRole.Primary);
            OwnerUiButtonSkin.Apply(button, role);
            OwnerUiButtonSkin.SetSelected(button, selected);
            return button;
        }

        private static Image ScoutPanel(Transform parent, string name, float x, float y, float width, float height, Color color, bool framed = false)
        {
            Image image = ScoutSurface(parent, name, x, y, width, height, color);
            if (framed) UIOwnerFrontOfficePanel.ApplyFramedSurface(image.rectTransform);
            else OwnerDashboardStyle.ApplyInset(image);
            return image;
        }

        private static Image ScoutSurface(Transform parent, string name, float x, float y, float width, float height, Color color)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            PlaceReference(image.rectTransform, x, y, width, height);
            image.color = color;
            image.raycastTarget = false;
            image.gameObject.AddComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.DataImage);
            return image;
        }

        private static Text ScoutLabel(Transform parent, string name, string value, int size, float x, float y, float width, float height, Color? color = null)
        {
            Text text = ReferenceText(parent, name, value, size, x, y, width, height);
            Color requested = color ?? ScoutInk;
            text.color = UIOwnerFrontOfficePanel.HasDarkSurface(parent)
                ? requested == ScoutInk ? OwnerDashboardStyle.Ivory
                    : requested == ScoutBlue ? OwnerDashboardStyle.Gold : requested
                : requested;
            text.alignment = TextAnchor.MiddleLeft;
            OwnerDashboardStyle.SetTypography(text, requested == ScoutBlue || size >= 15);
            if (text.GetComponent<CareerUiPreserveTextColor>() == null)
                text.gameObject.AddComponent<CareerUiPreserveTextColor>();
            text.raycastTarget = false;
            return text;
        }
    }
}
