using System.Text;
using Baseball.Core.Shop;
using Baseball.Presentation.Owner;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Shop
{
    public sealed partial class UI_Scene_Shop
    {
        private RectTransform _detailsRoot;
        private Text _detailsTitle;
        private Text _detailsBody;
        private Button _detailsPurchaseButton;
        private Button _detailsCloseButton;
        private RectTransform _confirmationRoot;
        private Text _confirmationTitle;
        private Text _confirmationBody;
        private Button _confirmationPurchaseButton;
        private Button _confirmationCancelButton;
        private Button _revealRepeatButton;
        private Button _revealInventoryButton;
        private Button _revealCloseButton;
        private ShopProductDetailsSnapshot _activeDetails;
        private string _lastPurchasedProductId = string.Empty;

        /// <summary>Simulation 확률과 현재 구매 조건을 상세 Overlay에 표시한다.</summary>
        public void ShowDetails(ShopProductDetailsSnapshot details)
        {
            if (details == null) return;
            _activeDetails = details;
            _detailsTitle.text = string.Concat(details.Title, " · ", details.Subtitle);
            _detailsBody.text = BuildDetailsBody(details);
            _detailsPurchaseButton.interactable = details.CanPurchase && !_isProcessing;
            _detailsPurchaseButton.transform.Find("Label").GetComponent<Text>().text =
                details.CanPurchase ? "구입 확인" : "구매 불가";
            _detailsRoot.gameObject.SetActive(true);
            _detailsRoot.SetAsLastSibling();
        }

        /// <summary>최신 Quote를 보여준 뒤 한 번 더 확인해야만 최종 Command를 보낸다.</summary>
        public void ShowPurchaseConfirmation(ShopProductDetailsSnapshot details)
        {
            if (details == null) return;
            _activeDetails = details;
            _detailsRoot.gameObject.SetActive(false);
            _confirmationTitle.text = "구매 확인";
            var body = new StringBuilder();
            body.Append(details.Title).Append(" · ").AppendLine(details.Subtitle)
                .Append(details.DrawCountText).Append(" / ").AppendLine(details.PriceText)
                .AppendLine(details.PurchaseLimitText);
            if (!details.CanPurchase)
                body.AppendLine().Append(details.BlockedReason);
            else
                body.AppendLine().Append("구입하면 재화가 즉시 차감되고 결과가 확정됩니다.");
            _confirmationBody.text = body.ToString();
            _confirmationPurchaseButton.interactable = details.CanPurchase && !_isProcessing;
            _confirmationPurchaseButton.transform.Find("Label").GetComponent<Text>().text =
                details.CanPurchase ? "구입" : "구매 불가";
            _confirmationRoot.gameObject.SetActive(true);
            _confirmationRoot.SetAsLastSibling();
        }

        /// <summary>동일 프레임의 중복 구매 입력을 막고 처리 상태를 한 곳에 반영한다.</summary>
        public void SetProcessing(bool isProcessing)
        {
            _isProcessing = isProcessing;
            for (int index = 0; index < _tabButtons.Count; index++)
                _tabButtons[index].interactable = !isProcessing;
            for (int index = 0; index < _tileActionButtons.Count; index++)
                _tileActionButtons[index].interactable = !isProcessing && _tileActionAvailability[index];

            if (_previewDetailsButton != null)
                _previewDetailsButton.interactable = !isProcessing && !string.IsNullOrEmpty(_selectedProductId);
            if (_previewPurchaseButton != null)
                _previewPurchaseButton.interactable = !isProcessing && _selectedProductCanPurchase;

            if (_confirmationPurchaseButton != null)
                _confirmationPurchaseButton.interactable = !isProcessing &&
                    _activeDetails != null && _activeDetails.CanPurchase;
            if (_confirmationCancelButton != null) _confirmationCancelButton.interactable = !isProcessing;
            if (_detailsCloseButton != null) _detailsCloseButton.interactable = !isProcessing;
            if (_detailsPurchaseButton != null)
                _detailsPurchaseButton.interactable = !isProcessing &&
                    _activeDetails != null && _activeDetails.CanPurchase;
            SetRevealButtonsInteractable(!isProcessing);

            if (isProcessing)
            {
                _confirmationPurchaseButton.transform.Find("Label").GetComponent<Text>().text = "처리 중";
                SetFeedback("구매 결과를 확정하는 중입니다.", false);
            }
            else if (_confirmationPurchaseButton != null)
            {
                _confirmationPurchaseButton.transform.Find("Label").GetComponent<Text>().text =
                    _activeDetails != null && _activeDetails.CanPurchase ? "구입" : "구매 불가";
            }
        }

        /// <summary>구매 실패 또는 Route 전환 때 남은 확인 Overlay를 닫는다.</summary>
        public void DismissPurchaseConfirmation()
        {
            if (_confirmationRoot != null) _confirmationRoot.gameObject.SetActive(false);
        }

        /// <summary>Back 입력은 Reveal, 구매 확인, 상세 순서로 가장 위 Overlay 하나를 닫는다.</summary>
        public bool TryCloseOverlay()
        {
            if (_isProcessing) return false;
            if (_revealRoot != null && _revealRoot.gameObject.activeSelf)
            {
                if (_isRevealPlaying)
                {
                    SkipReveal();
                    return true;
                }
                _revealRoot.gameObject.SetActive(false);
                return true;
            }
            if (_confirmationRoot != null && _confirmationRoot.gameObject.activeSelf)
            {
                _confirmationRoot.gameObject.SetActive(false);
                return true;
            }
            if (_detailsRoot != null && _detailsRoot.gameObject.activeSelf)
            {
                _detailsRoot.gameObject.SetActive(false);
                return true;
            }
            return false;
        }

        private void BuildDecisionOverlays()
        {
            BuildDetailsOverlay();
            BuildConfirmationOverlay();
        }

        private void BuildDetailsOverlay()
        {
            _detailsRoot = CreateOverlayRoot("ProductDetailsOverlay");
            RectTransform panel = CreateModalPanel(_detailsRoot, "ProductDetailsPanel", new Vector2(680f, 520f));
            VerticalLayoutGroup layout = OwnerWorkspaceUiFactory.AddVerticalLayout(panel, CareerUiTheme.Space2);
            layout.padding = new RectOffset(26, 26, 22, 22);

            _detailsTitle = OwnerRuntimeUiFactory.CreateText(
                "Title", panel, string.Empty, 22, FontStyle.Bold,
                TextAnchor.MiddleLeft, CareerUiTheme.ReferenceText);
            AddFixedHeight(_detailsTitle.rectTransform, 36f);
            _detailsBody = OwnerRuntimeUiFactory.CreateText(
                "Body", panel, string.Empty, 14, FontStyle.Normal,
                TextAnchor.UpperLeft, CareerUiTheme.ReferenceText);
            _detailsBody.verticalOverflow = VerticalWrapMode.Truncate;
            OwnerWorkspaceUiFactory.SetFlexible(_detailsBody.rectTransform, 1f);

            RectTransform actions = OwnerRuntimeUiFactory.CreateRect("Actions", panel);
            HorizontalLayoutGroup actionLayout = OwnerWorkspaceUiFactory.AddHorizontalLayout(actions, CareerUiTheme.Space2);
            actionLayout.childAlignment = TextAnchor.MiddleRight;
            AddFixedHeight(actions, 42f);
            _detailsCloseButton = OwnerWorkspaceUiFactory.CreateButton(
                actions, "Close", "닫기", () => _detailsRoot.gameObject.SetActive(false));
            _detailsPurchaseButton = OwnerWorkspaceUiFactory.CreateButton(
                actions, "Purchase", "구입 확인", () => ShowPurchaseConfirmation(_activeDetails));
            _detailsRoot.gameObject.SetActive(false);
        }

        private void BuildConfirmationOverlay()
        {
            _confirmationRoot = CreateOverlayRoot("PurchaseConfirmationOverlay");
            RectTransform panel = CreateModalPanel(
                _confirmationRoot, "PurchaseConfirmationPanel", new Vector2(520f, 310f));
            VerticalLayoutGroup layout = OwnerWorkspaceUiFactory.AddVerticalLayout(panel, CareerUiTheme.Space3);
            layout.padding = new RectOffset(28, 28, 24, 24);

            _confirmationTitle = OwnerRuntimeUiFactory.CreateText(
                "Title", panel, string.Empty, 23, FontStyle.Bold,
                TextAnchor.MiddleCenter, CareerUiTheme.ReferenceText);
            AddFixedHeight(_confirmationTitle.rectTransform, 38f);
            _confirmationBody = OwnerRuntimeUiFactory.CreateText(
                "Body", panel, string.Empty, 15, FontStyle.Normal,
                TextAnchor.MiddleCenter, CareerUiTheme.ReferenceText);
            OwnerWorkspaceUiFactory.SetFlexible(_confirmationBody.rectTransform, 1f);

            RectTransform actions = OwnerRuntimeUiFactory.CreateRect("Actions", panel);
            HorizontalLayoutGroup actionLayout = OwnerWorkspaceUiFactory.AddHorizontalLayout(actions, CareerUiTheme.Space2);
            actionLayout.childAlignment = TextAnchor.MiddleCenter;
            AddFixedHeight(actions, 42f);
            _confirmationCancelButton = OwnerWorkspaceUiFactory.CreateButton(
                actions, "Cancel", "취소", () => _confirmationRoot.gameObject.SetActive(false));
            _confirmationPurchaseButton = OwnerWorkspaceUiFactory.CreateButton(
                actions, "Confirm", "구입", HandlePurchaseConfirmed);
            _confirmationRoot.gameObject.SetActive(false);
        }

        private RectTransform CreateOverlayRoot(string name)
        {
            Image dim = OwnerRuntimeUiFactory.CreateImage(name, _root, new Color(0.02f, 0.03f, 0.05f, 0.82f));
            dim.raycastTarget = true;
            OwnerRuntimeUiFactory.Stretch(dim.rectTransform);
            return dim.rectTransform;
        }

        private static RectTransform CreateModalPanel(RectTransform parent, string name, Vector2 size)
        {
            Image surface = OwnerRuntimeUiFactory.CreateImage(name, parent, CareerUiTheme.ReferencePanel);
            RectTransform rect = surface.rectTransform;
            OwnerRuntimeUiFactory.SetAnchors(
                rect,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                size);
            rect.anchoredPosition = Vector2.zero;
            var outline = surface.gameObject.AddComponent<Outline>();
            outline.effectColor = CareerUiTheme.ReferenceBorder;
            outline.effectDistance = new Vector2(2f, -2f);
            return rect;
        }

        private static string BuildDetailsBody(ShopProductDetailsSnapshot details)
        {
            var body = new StringBuilder();
            body.Append(details.Summary).AppendLine().AppendLine()
                .Append(details.DrawCountText).Append(" / ").Append(details.PriceText)
                .Append(" / ").AppendLine(details.PurchaseLimitText)
                .AppendLine("획득 확률");
            for (int index = 0; index < details.ProbabilityLines.Count; index++)
                body.Append("  ").AppendLine(details.ProbabilityLines[index]);
            if (details.ProbabilityLines.Count == 0)
                body.AppendLine("  공개 가능한 결과군이 없습니다.");
            if (details.Notice.Length > 0)
                body.AppendLine().AppendLine(details.Notice);
            if (!details.CanPurchase)
                body.AppendLine().Append("현재 구매 불가: ").Append(details.BlockedReason);
            return body.ToString();
        }

        private void HandlePurchaseConfirmed()
        {
            if (_isProcessing || _activeDetails == null || !_activeDetails.CanPurchase) return;
            PurchaseRequested?.Invoke(_activeDetails.ProductId);
        }

        private void HandleRepurchaseClicked()
        {
            if (_isProcessing || string.IsNullOrEmpty(_lastPurchasedProductId)) return;
            _revealRoot.gameObject.SetActive(false);
            RepurchaseRequested?.Invoke(_lastPurchasedProductId);
        }

        private void HandleInventoryClicked()
        {
            if (_isProcessing || string.IsNullOrEmpty(_lastPurchasedProductId)) return;
            _revealRoot.gameObject.SetActive(false);
            InventoryRequested?.Invoke(_lastPurchasedProductId);
        }

        private void RefreshRevealActions()
        {
            bool hasProduct = _activeDetails != null && !string.IsNullOrEmpty(_lastPurchasedProductId);
            _revealRepeatButton.gameObject.SetActive(hasProduct);
            _revealInventoryButton.gameObject.SetActive(hasProduct);
            if (!hasProduct) return;
            string label = _activeDetails.Kind switch
            {
                ShopProductKind.PlayerCardPack => "보유선수",
                ShopProductKind.SkillBlockPack => "카드훈련",
                ShopProductKind.TacticCardPack => "작전 설정",
                _ => "보관함"
            };
            _revealInventoryButton.transform.Find("Label").GetComponent<Text>().text = label;
            SetRevealButtonsInteractable(!_isProcessing);
        }

        private void SetRevealButtonsInteractable(bool interactable)
        {
            if (_revealRepeatButton != null) _revealRepeatButton.interactable = interactable;
            if (_revealInventoryButton != null) _revealInventoryButton.interactable = interactable;
            if (_revealCloseButton != null) _revealCloseButton.interactable = interactable;
        }

        private void HideDecisionOverlays()
        {
            if (_detailsRoot != null) _detailsRoot.gameObject.SetActive(false);
            if (_confirmationRoot != null) _confirmationRoot.gameObject.SetActive(false);
        }

        private void HideAllOverlays()
        {
            StopRevealPlayback();
            HideDecisionOverlays();
            if (_revealRoot != null) _revealRoot.gameObject.SetActive(false);
        }
    }
}
