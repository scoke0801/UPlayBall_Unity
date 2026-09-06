using System;
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

        private void BuildEnhancementSale()
        {
            _reinforcementCanvas = OwnerRuntimeUiFactory.CreateRect("ReferenceReinforcement", _enhancementRoot);
            _reinforcementCanvas.anchorMin = _reinforcementCanvas.anchorMax = new Vector2(.5f, .5f);
            _reinforcementCanvas.sizeDelta = new Vector2(1000f, 460f);
            var paper = new GameObject("ReinforcementPaper", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            paper.transform.SetParent(_reinforcementCanvas, false);
            OwnerRuntimeUiFactory.Stretch(paper.rectTransform);
            paper.texture = Resources.Load<Texture2D>("UI/OwnerPowerUp/reinforcement_paper_v1");
            paper.raycastTarget = false;

            ReferenceButton(_reinforcementCanvas, "PlayerTab", "선수 카드", () => { }, 8, 6, 100, 26);
            string[] tabs = { "서포트 카드", "작전 카드", "수석코치 카드" };
            for (int index = 0; index < tabs.Length; index++)
                ReferenceButton(_reinforcementCanvas, "Category" + index, tabs[index], null,
                    108 + index * 98, 6, 98, 26).interactable = false;
            ReferenceButton(_reinforcementCanvas, "CostSort", "코스트 ▼", () =>
            {
                _sortCostDescending = !_sortCostDescending;
                _reinforcementCanvas.Find("CostSort/Label").GetComponent<Text>().text =
                    _sortCostDescending ? "코스트 ▼" : "코스트 ▲";
                BindEnhancementSale();
            }, 402, 6, 82, 26);
            _enhancementCardCount = ReferenceText(_reinforcementCanvas, "EnhancementCardCount", "", 15, 730, 6, 250, 26);
            _enhancementCardCount.alignment = TextAnchor.MiddleRight;
            ScrollRect scroll = OwnerRuntimeUiFactory.CreateVerticalGridScroll(
                "EnhancementInventory", _reinforcementCanvas, 6, new Vector2(70f, 116f), 6f,
                out _enhancementCardList);
            PlaceReference(scroll.GetComponent<RectTransform>(), 8, 34, 478, 366);
            _enhancementCardList.GetComponent<GridLayoutGroup>().padding = new RectOffset(4, 4, 4, 4);
            AddInventoryScrollbar(scroll);

            Image frame = ReferenceSurface(_reinforcementCanvas, "RegistrationFrame", 536, 46, 410, 267,
                new Color32(244, 247, 250, 255), new Color32(72, 103, 145, 255));
            ReferenceSurface(frame.transform, "InsetFrame", 4, 4, 402, 259, Color.white,
                new Color32(170, 185, 203, 255));
            for (int index = 0; index < 10; index++)
            {
                Image slot = ReferenceSurface(frame.transform, "RegistrationSlot" + index,
                    12 + index % 5 * 78, 10 + index / 5 * 108, 74, 104,
                    new Color32(245, 245, 244, 255), new Color32(218, 221, 223, 255));
                ReferenceText(slot.transform, "EmptyCard", "○", 52, 2, 16, 70, 72).color =
                    new Color32(233, 234, 234, 255);
                if (index > 1) continue;
                PlayerMiniCardView card = PlayerMiniCardView.CreateRuntime(slot.transform,
                    index == 0 ? "TargetCard" : "MaterialCard");
                card.UseLineupSlotLayout();
                OwnerRuntimeUiFactory.Stretch(card.GetComponent<RectTransform>(), new Vector2(3, 3), new Vector2(-3, -3));
                if (index == 0) _enhancementCard = card;
                else _enhancementMaterialCard = card;
            }
            _enhancementMaterialEmpty = ReferenceText(frame.transform, "MaterialCardEmpty", "", 11, 90, 50, 74, 40);
            _registerButton = ReferenceButton(frame.transform, "Register", "등록하기", RegisterEnhancement, 10, 229, 128, 28);
            ReferenceButton(frame.transform, "Unregister", "등록취소", ClearEnhancementRegistration, 140, 229, 128, 28);
            ReferenceButton(frame.transform, "Reset", "초기화", ResetEnhancement, 270, 229, 128, 28);
            ReferenceSurface(_reinforcementCanvas, "ReinforceButtonFrame", 662, 350, 154, 48,
                new Color32(233, 239, 244, 255), new Color32(110, 143, 180, 255));
            _enhanceButton = ReferenceButton(_reinforcementCanvas, "Enhance", "보강", RequestEnhancement, 669, 356, 140, 34);
            _enhancementDetails = ReferenceText(_reinforcementCanvas, "EnhancementDetails", "", 12, 528, 316, 442, 32);
            _enhancementDetails.alignment = TextAnchor.UpperLeft;
            _enhancementWallet = ReferenceText(_reinforcementCanvas, "EnhancementWallet", "", 12, 518, 404, 292, 24);
            ReferenceButton(_reinforcementCanvas, "OpenSale", "선수방출", () =>
                _salePanel.gameObject.SetActive(true), 8, 412, 130, 28);
            _hideLockedButton = ReferenceButton(_reinforcementCanvas, "HideLocked", "□ 잠금 선수 숨기기", () =>
            {
                _hideLockedCards = !_hideLockedCards;
                BindEnhancementSale();
            }, 320, 412, 166, 28);
            ReferenceButton(_reinforcementCanvas, "PowerUpSettings", "전력보강 설정", () =>
                SetFeedback("선수 카드 선택 → 등록하기 → 보강. 동일 카드 1장을 사용하며 실패 확률은 없습니다.", false),
                838, 412, 142, 28);

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

        private void LateUpdate()
        {
            ResizeScoutReference();
            if (_reinforcementCanvas == null || !_enhancementRoot.gameObject.activeInHierarchy) return;
            Rect bounds = _enhancementRoot.rect;
            float scale = Mathf.Max(.01f, Mathf.Min(bounds.width / 1000f, (bounds.height - 32f) / 460f));
            _reinforcementCanvas.localScale = Vector3.one * scale;
        }

        private void RegisterEnhancement()
        {
            _hasRegisteredEnhancement = FindEnhancementTarget(_selectedEnhancementCardId) != null;
            RefreshEnhancementTarget();
        }

        private void ClearEnhancementRegistration()
        {
            _hasRegisteredEnhancement = false;
            _enhancementCard.gameObject.SetActive(false);
            _enhancementMaterialCard.gameObject.SetActive(false);
            _enhanceButton.interactable = false;
            _enhancementDetails.text = "선수 카드를 선택한 뒤 등록하기를 누르세요.";
        }

        private void ResetEnhancement()
        {
            _selectedEnhancementCardId = string.Empty;
            _saleCount = 1;
            ClearEnhancementRegistration();
            BindEnhancementSale();
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
            Image track = ReferenceSurface(scroll.transform, "InventoryScrollbar", 466, 0, 12, 366,
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
