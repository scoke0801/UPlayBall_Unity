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
        private RawImage _scoutGaugeFill;
        private Button _scoutPolicyConfirmButton;
        private string _scoutPreviewProductId = string.Empty;
        // 고정 크기 지도와 방침 창에 실제로 들어가는 버튼 수만 생성한다.
        private const int ScoutMapPageSize = 6;
        private const int ScoutPolicyPageSize = 10;
        private int _scoutMapPage;
        private int _scoutPolicyPage;
        private static readonly Color ScoutInk = new Color32(36, 43, 51, 255);
        private static readonly Color ScoutBlue = new Color32(34, 72, 128, 255);
        private static readonly Color ScoutSilver = new Color32(239, 240, 239, 255);

        private void BuildScoutReference()
        {
            _scoutCanvas = OwnerRuntimeUiFactory.CreateRect("ScoutReference", _scoutRoot);
            _scoutCanvas.anchorMin = _scoutCanvas.anchorMax = new Vector2(.5f, .5f);
            _scoutCanvas.sizeDelta = new Vector2(1000, 460);
            ScoutSurface(_scoutCanvas, "Paper", 0, 0, 1000, 460, ScoutSilver);
            ScoutLabel(_scoutCanvas, "MapHeading", "▸ 파견지 선택", 15, 12, 4, 320, 26);
            ScoutSurface(_scoutCanvas, "InformationStrip", 368, 4, 620, 32, new Color32(48, 51, 52, 255));
            ScoutLabel(_scoutCanvas, "Information", "선택한 상품의 영입 정보를 확인하세요.", 12, 378, 5, 300, 30, Color.white);
            _scoutWallet = ScoutLabel(_scoutCanvas, "Wallet", "", 11, 682, 5, 295, 30, Color.white);
            _scoutWallet.alignment = TextAnchor.MiddleRight;
            ScoutSurface(_scoutCanvas, "MapBorder", 10, 34, 350, 414, new Color32(157, 169, 153, 255));
            RawImage map = ScoutSurface(_scoutCanvas, "KoreaMap", 14, 38, 342, 406, Color.white);
            map.texture = Resources.Load<Texture2D>("UI/OwnerPowerUp/scout_korea_map_v1");
            _scoutList = OwnerRuntimeUiFactory.CreateRect("ScoutProductPins", map.transform);
            OwnerRuntimeUiFactory.Stretch(_scoutList);
            ScoutSurface(map.transform, "Speech", 22, 14, 296, 63, new Color32(250, 251, 245, 248));
            ScoutLabel(map.transform, "MapGuide", "선수를 찾아보겠습니다.\n마커를 선택하면 영입 비용과\n획득 정보를 확인할 수 있습니다.", 13, 32, 19, 276, 53);

            ScoutSurface(_scoutCanvas, "ScoutColumn", 368, 40, 128, 408, Color.white);
            ScoutLabel(_scoutCanvas, "ScoutHeading", "스카우터", 14, 378, 42, 108, 24, ScoutBlue).alignment = TextAnchor.MiddleCenter;
            RawImage portrait = ScoutSurface(_scoutCanvas, "ScoutPortrait", 405, 73, 55, 82, Color.white);
            portrait.texture = Resources.Load<Texture2D>("UI/OwnerPowerUp/OwnerScout_Silhouette_V1");
            ScoutLabel(_scoutCanvas, "ScoutName", "전담 스카우터", 12, 374, 157, 116, 23).alignment = TextAnchor.MiddleCenter;
            ReferenceButton(_scoutCanvas, "ScoutPolicy", "스카우트 방침", OpenScoutPolicy, 376, 181, 112, 28);
            ScoutLabel(_scoutCanvas, "GaugeHeading", "보장 영입", 12, 376, 221, 110, 22, ScoutBlue);
            ScoutSurface(_scoutCanvas, "GaugeTrack", 376, 245, 112, 16, new Color32(37, 49, 35, 255));
            _scoutGaugeFill = ScoutSurface(_scoutCanvas, "GaugeFill", 378, 247, 0, 12, new Color32(55, 191, 57, 255));
            _scoutGaugeLabel = ScoutLabel(_scoutCanvas, "GaugeValue", "0 / 100", 13, 376, 263, 112, 22);
            ScoutSurface(_scoutCanvas, "DispatchHeading", 374, 294, 116, 24, ScoutSilver);
            ScoutLabel(_scoutCanvas, "DispatchTitle", "파견 정보", 13, 380, 295, 104, 22, ScoutBlue);
            _scoutDispatch = ScoutLabel(_scoutCanvas, "DispatchValue", "정보 확인 중", 12, 378, 323, 108, 113);
            _scoutDispatch.alignment = TextAnchor.UpperLeft;

            ReferenceButton(_scoutCanvas, "ScoutInformationTab", "스카우트 정보", () => _scoutProbabilityOverlay.gameObject.SetActive(true), 507, 40, 138, 27);
            ReferenceButton(_scoutCanvas, "ScoutResultsTab", "영입대기 선수", () => _scoutProbabilityOverlay.gameObject.SetActive(false), 646, 40, 128, 27);
            ScoutSurface(_scoutCanvas, "PlayerAreaBorder", 508, 74, 480, 286, new Color32(174, 181, 189, 255));
            ScoutSurface(_scoutCanvas, "PlayerArea", 510, 76, 476, 282, Color.white);
            _scoutResultTitle = ScoutLabel(_scoutCanvas, "ResultTitle", "영입대기 선수 · 파견 후 결과가 표시됩니다", 13, 520, 79, 450, 24, ScoutBlue);
            _scoutResultGrid = OwnerRuntimeUiFactory.CreateRect("ScoutCards", _scoutCanvas);
            PlaceReference(_scoutResultGrid, 526, 108, 442, 242);
            BindScoutResults(Array.Empty<ShopGrantedItem>());
            ScoutSurface(_scoutCanvas, "CostStrip", 510, 366, 476, 25, new Color32(220, 222, 221, 255));
            _scoutCost = ScoutLabel(_scoutCanvas, "ScoutCost", "비 용", 14, 522, 366, 450, 25);
            _scoutCost.alignment = TextAnchor.MiddleCenter;
            _scoutSummary = ScoutLabel(_scoutCanvas, "ScoutSummary", "상품 정보를 불러오는 중입니다.", 12, 520, 394, 456, 28);
            _scoutSummary.alignment = TextAnchor.MiddleCenter;
            _scoutPurchaseButton = ReferenceButton(_scoutCanvas, "ScoutPurchase", "스카우트 파견", RequestScoutPurchase, 678, 425, 138, 26);
            OwnerUiButtonSkin.Apply(_scoutPurchaseButton, OwnerButtonRole.Primary);
            Button wishlist = ReferenceButton(
                _scoutCanvas, "OpenWishlist", "위시리스트", () => WishlistRequested?.Invoke(), 828, 425, 145, 26);
            OwnerUiButtonSkin.Apply(wishlist, OwnerButtonRole.Secondary);
            BuildScoutProbabilityWindow();
            BuildScoutPolicyWindow();
        }

        private void BuildScoutProbabilityWindow()
        {
            _scoutProbabilityOverlay = ScoutSurface(_scoutCanvas, "ProbabilityWindow", 505, 38, 487, 414, ScoutSilver).rectTransform;
            _scoutProbabilityOverlay.GetComponent<RawImage>().raycastTarget = true;
            ScoutLabel(_scoutProbabilityOverlay, "Heading", "스카우트 정보 · 실제 영입 확률", 15, 12, 5, 380, 28, ScoutBlue);
            ReferenceButton(_scoutProbabilityOverlay, "CloseProbability", "닫기", () => _scoutProbabilityOverlay.gameObject.SetActive(false), 406, 6, 68, 26);
            RectTransform viewport = ScoutSurface(_scoutProbabilityOverlay, "ProbabilityViewport", 12, 42, 462, 356, Color.white).rectTransform;
            viewport.GetComponent<RawImage>().raycastTarget = true;
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
            _scoutPolicyOverlay = ScoutSurface(
                _scoutCanvas, "ScoutPolicyShade", 0, 0, 1000, 460, new Color(0, 0, 0, .55f)).rectTransform;
            _scoutPolicyOverlay.GetComponent<RawImage>().raycastTarget = true;
            RectTransform window = ScoutSurface(
                _scoutPolicyOverlay, "ScoutPolicyWindow", 190, 20, 620, 420, ScoutSilver).rectTransform;
            ScoutLabel(window, "PolicyHeading", "스카우트 방침 선택", 16, 12, 4, 470, 28, ScoutBlue);
            ReferenceButton(window, "ClosePolicy", "×", CloseScoutPolicy, 578, 4, 30, 26);

            ScoutSurface(window, "ScoutCardBorder", 14, 40, 224, 324, new Color32(165, 168, 165, 255));
            RawImage portrait = ScoutSurface(window, "PolicyScoutPortrait", 62, 52, 128, 192, Color.white);
            portrait.texture = Resources.Load<Texture2D>("UI/OwnerPowerUp/OwnerScout_Silhouette_V1");
            ScoutLabel(window, "PolicyScoutName", "전담 스카우터", 17, 24, 247, 204, 28).alignment = TextAnchor.MiddleCenter;
            _scoutPolicyPreview = ScoutLabel(window, "PolicyPreview", "방침을 선택하세요.", 12, 25, 278, 202, 74);
            _scoutPolicyPreview.alignment = TextAnchor.UpperCenter;

            ScoutSurface(window, "PolicyListBorder", 250, 40, 356, 324, new Color32(174, 181, 189, 255));
            ScoutLabel(window, "PolicyListHeading", "파견 범위 · 탐색 방침 · 비용", 13, 262, 43, 330, 25, ScoutBlue);
            _scoutPolicyOptions = OwnerRuntimeUiFactory.CreateRect("PolicyOptions", window);
            PlaceReference(_scoutPolicyOptions, 258, 72, 340, 280);
            _scoutPolicyConfirmButton = ReferenceButton(
                window, "ConfirmPolicy", "결정", ApplyScoutPolicy, 158, 376, 142, 28);
            ReferenceButton(window, "CancelPolicy", "취소", CloseScoutPolicy, 320, 376, 142, 28);
            _scoutPolicyOverlay.gameObject.SetActive(false);
        }

        private void OpenScoutPolicy()
        {
            _scoutPreviewProductId = _selectedScoutProductId;
            _scoutPolicyPage = 0;
            if (_snapshot != null)
                for (int index = 0; index < _snapshot.Scout.Products.Count; index++)
                    if (_snapshot.Scout.Products[index].ProductId == _scoutPreviewProductId)
                        _scoutPolicyPage = index / ScoutPolicyPageSize;
            PreviewScoutPolicy(_scoutPreviewProductId);
            _scoutPolicyOverlay.gameObject.SetActive(true);
            _scoutPolicyOverlay.SetAsLastSibling();
        }

        private void CloseScoutPolicy() => _scoutPolicyOverlay.gameObject.SetActive(false);

        private void RebuildScoutPolicyOptions()
        {
            OwnerRuntimeUiFactory.ClearChildren(_scoutPolicyOptions);
            if (_snapshot == null) return;
            IReadOnlyList<OwnerScoutProductSnapshot> products = _snapshot.Scout.Products;
            int pageCount = Math.Max(1, (products.Count + ScoutPolicyPageSize - 1) / ScoutPolicyPageSize);
            _scoutPolicyPage = Math.Min(_scoutPolicyPage, pageCount - 1);
            int start = _scoutPolicyPage * ScoutPolicyPageSize;
            int end = Math.Min(products.Count, start + ScoutPolicyPageSize);
            for (int index = start; index < end; index++)
            {
                OwnerScoutProductSnapshot product = products[index];
                int capturedIndex = index;
                float x = (index - start) % 2 * 172;
                float y = (index - start) / 2 * 55;
                Button button = ReferenceButton(
                    _scoutPolicyOptions,
                    "PolicyChoice" + index,
                    product.Scope + "\n" + product.DrawCount + "회 · " + product.PriceText,
                    () => PreviewScoutPolicy(products[capturedIndex].ProductId),
                    x,
                    y,
                    166,
                    49);
                Text label = button.transform.Find("Label").GetComponent<Text>();
                label.fontSize = 11;
                label.alignment = TextAnchor.MiddleCenter;
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 9;
                button.interactable = !string.Equals(
                    product.ProductId, _scoutPreviewProductId, StringComparison.Ordinal);
            }
            ReferenceButton(_scoutPolicyOptions, "PreviousPolicyPage", "이전", () =>
            {
                _scoutPolicyPage--;
                RebuildScoutPolicyOptions();
            }, 0, 280, 70, 22).interactable = _scoutPolicyPage > 0;
            ScoutLabel(_scoutPolicyOptions, "PolicyPage", $"{_scoutPolicyPage + 1} / {pageCount}",
                12, 125, 280, 100, 22);
            ReferenceButton(_scoutPolicyOptions, "NextPolicyPage", "다음", () =>
            {
                _scoutPolicyPage++;
                RebuildScoutPolicyOptions();
            }, 270, 280, 70, 22).interactable = _scoutPolicyPage + 1 < pageCount;
        }

        private void BindScoutMapPagination(int count)
        {
            int pageCount = Math.Max(1, (count + ScoutMapPageSize - 1) / ScoutMapPageSize);
            ReferenceButton(_scoutList, "PreviousMapPage", "이전", () =>
            {
                _scoutMapPage--;
                BindScout();
            }, 20, 365, 60, 26).interactable = _scoutMapPage > 0;
            ScoutLabel(_scoutList, "MapPage", $"{_scoutMapPage + 1}/{pageCount}", 11, 85, 365, 64, 26);
            ReferenceButton(_scoutList, "NextMapPage", "다음", () =>
            {
                _scoutMapPage++;
                BindScout();
            }, 150, 365, 60, 26).interactable = _scoutMapPage + 1 < pageCount;
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
            _scoutPolicyPreview.text = product.Title + "\n" + product.Scope + "\n" +
                                       product.DrawCount + "명 탐색 · " + product.PriceText + "\n" +
                                       "후보 " + product.CandidateCount.ToString("N0") + "장 · 위시 " +
                                       product.WishlistCandidateCount.ToString("N0") + "장 포함";
            _scoutPolicyConfirmButton.interactable = true;
            RebuildScoutPolicyOptions();
        }

        private void ApplyScoutPolicy()
        {
            if (FindScoutProduct(_scoutPreviewProductId) == null) return;
            _selectedScoutProductId = _scoutPreviewProductId;
            CloseScoutPolicy();
            BindScout();
        }

        private void RefreshScoutReference(OwnerScoutProductSnapshot product)
        {
            _scoutCost.text = "비 용   " + product.PriceText;
            _scoutSummary.text = product.CanPurchase
                ? product.Title + " · 후보 " + product.CandidateCount.ToString("N0") + "장 중 위시 " +
                  product.WishlistCandidateCount.ToString("N0") + "장 · 확정 시 즉시 지급됩니다."
                : product.BlockedReason;
            _scoutDispatch.text = "파견 범위\n" + product.Scope + "\n\n탐색 방침\n" +
                                  DescribeScoutPolicy(product) + "\n\n탐색 인원  " + product.DrawCount + "명\n" +
                                  "위시 포함  " + product.WishlistCandidateCount.ToString("N0") + "장";
            _scoutGaugeLabel.text = product.PityGauge + " / " + product.PityThreshold;
            float fill = product.PityThreshold > 0 ? Mathf.Clamp01((float)product.PityGauge / product.PityThreshold) : 0;
            _scoutGaugeFill.rectTransform.sizeDelta = new Vector2(108 * fill, 12);
        }

        private void BindScoutResults(ShopGrantedItem[] items)
        {
            OwnerRuntimeUiFactory.ClearChildren(_scoutResultGrid);
            _scoutResultTitle.text = items.Length == 0
                ? "영입대기 선수 · 파견 후 결과가 표시됩니다"
                : "영입 완료 · " + items.Length + "명";
            for (int index = 0; index < Math.Max(10, items.Length); index++)
            {
                RectTransform slot = ScoutSurface(_scoutResultGrid, "CardSlot" + index, index % 5 * 88, index / 5 * 120, 82, 114,
                    new Color32(222, 224, 222, 255)).rectTransform;
                if (index >= items.Length)
                {
                    ScoutLabel(slot, "Empty", "◇", 40, 8, 22, 66, 60, new Color32(196, 200, 197, 255));
                    continue;
                }
                ShopGrantedItem item = items[index];
                OwnerCardTrainingTargetSnapshot target = FindTrainingTarget(item.ItemId);
                if (target != null)
                {
                    PlayerMiniCardView card = PlayerMiniCardView.CreateRuntime(slot, "GrantedCard");
                    card.UseLineupSlotLayout();
                    OwnerRuntimeUiFactory.Stretch(card.GetComponent<RectTransform>(), new Vector2(2, 2), new Vector2(-2, -2));
                    card.Bind(OwnerCollectionPresentationBuilder.CreateMiniCard(target.Card, false));
                    OwnerCollectionCardSnapshot granted = target.Card;
                    card.DetailRequested += _ => UI_Popup_OwnerPlayerCard.Show(transform, granted);
                }
                else
                    ScoutLabel(slot, "GrantedName", item.DisplayName + "\n" + item.GradeLabel, 12, 4, 15, 74, 90).alignment = TextAnchor.MiddleCenter;
                ScoutLabel(slot, "NewStatus", item.WasWishlisted ? "★ 위시 성공" : item.IsNew ? "신규" : "중복",
                    10, 3, 0, 76, 16, ScoutBlue);
            }
        }

        private void ResizeScoutReference()
        {
            if (_scoutCanvas == null || !_scoutRoot.gameObject.activeInHierarchy) return;
            Rect bounds = _scoutRoot.rect;
            _scoutCanvas.localScale = Vector3.one * Mathf.Max(.01f, Mathf.Min(bounds.width / 1000, (bounds.height - 32) / 460));
        }

        private static void CreateScoutReferencePin(Transform parent, string name, string label, Action action, bool selected, int index, int count)
        {
            float x = index % 2 == 0 ? 34 : 167;
            float y = 104 + index / 2 * 89 + index % 2 * 35;
            string caption = label.Replace("선수 카드", string.Empty).Trim(' ', '·');
            Button pin = ReferenceButton(parent, name, (selected ? "◉ " : "◎ ") + caption, action, x, y, 142, 30);
            pin.transform.Find("Label").GetComponent<Text>().fontSize = 12;
        }

        private static string DescribeScoutPolicy(OwnerScoutProductSnapshot product)
        {
            int separator = product.Title.LastIndexOf('·');
            return separator < 0 ? product.Title : product.Title.Substring(separator + 1).Trim();
        }

        private static RawImage ScoutSurface(Transform parent, string name, float x, float y, float width, float height, Color color)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            image.transform.SetParent(parent, false);
            PlaceReference(image.rectTransform, x, y, width, height);
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text ScoutLabel(Transform parent, string name, string value, int size, float x, float y, float width, float height, Color? color = null)
        {
            Text text = ReferenceText(parent, name, value, size, x, y, width, height);
            text.color = color ?? ScoutInk;
            text.alignment = TextAnchor.MiddleLeft;
            text.gameObject.AddComponent<CareerUiPreserveTextColor>();
            text.raycastTarget = false;
            return text;
        }
    }
}
