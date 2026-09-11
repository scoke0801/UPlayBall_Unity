using System;
using System.Collections.Generic;
using System.Text;
using Baseball.Core.Shop;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using Baseball.Simulation.Historical;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>스카우트·카드훈련·강화·판매를 각각의 View State로 제공하는 전력보강 화면이다.</summary>
    [DisallowMultipleComponent]
    public sealed partial class UI_Scene_OwnerPowerUp : MonoBehaviour, IUiCancelHandler
    {
        private RectTransform _root;
        private RectTransform _scoutRoot;
        private RectTransform _trainingRoot;
        private RectTransform _enhancementRoot;
        private RectTransform _scoutList;
        private RectTransform _trainingCardList;
        private RectTransform _trainingProgramList;
        private RectTransform _enhancementCardList;
        private Text _scoutWallet;
        private Text _scoutDetails;
        private Text _trainingWallet;
        private Text _trainingDetails;
        private Text _enhancementWallet;
        private Text _enhancementDetails;
        private Text _feedback;
        private Button _scoutPurchaseButton;
        private Button _trainingExecuteButton;
        private Button _enhanceButton;
        private Button _sellButton;
        private RectTransform _trainingCard;
        private RectTransform _trainingCardFront;
        private PlayerMiniCardView _enhancementCard;
        private PlayerMiniCardView _enhancementMaterialCard;
        private Text _enhancementMaterialEmpty;
        private Text _trainingCardCount;
        private Text _enhancementCardCount;
        private RectTransform _confirmRoot;
        private Text _confirmTitle;
        private Text _confirmBody;
        private Action _confirmedAction;
        private RectTransform _revealRoot;
        private Text _revealBody;
        private OwnerPowerUpSnapshot _snapshot;
        private string _selectedScoutProductId = string.Empty;
        private string _selectedTrainingCardId = string.Empty;
        private string _selectedTrainingProgramId = string.Empty;
        private string _selectedEnhancementCardId = string.Empty;
        private int _saleCount = 1;

        public event Action<string> ScoutPurchaseRequested;
        public event Action<string, string> TrainingRequested;
        public event Action<string> EnhancementRequested;
        public event Action<string, int> DuplicateSaleRequested;
        public event Action WishlistRequested;

        /// <summary>공용 Shell Workspace 아래에 전력보강 화면을 런타임 생성한다.</summary>
        public static UI_Scene_OwnerPowerUp CreateRuntime(RectTransform workspaceHost)
        {
            if (workspaceHost == null) throw new ArgumentNullException(nameof(workspaceHost));
            var view = new GameObject(nameof(UI_Scene_OwnerPowerUp)).AddComponent<UI_Scene_OwnerPowerUp>();
            view.Build(workspaceHost);
            return view;
        }

        /// <summary>세 Route의 실제 Query와 Preview 결과를 다시 표시한다.</summary>
        public void Bind(OwnerPowerUpSnapshot snapshot)
        {
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            BindScout();
            BindTraining();
            BindEnhancementSale();
        }

        /// <summary>첫 Snapshot을 만들기 전 세 Route에 입력이 막힌 Loading 상태를 표시한다.</summary>
        public void ShowLoading()
        {
            _scoutWallet.text = string.Empty;
            _trainingWallet.text = string.Empty;
            _enhancementWallet.text = string.Empty;
            _scoutDetails.text = "스카우트 상품과 실제 확률을 불러오는 중입니다.";
            _trainingDetails.text = "보유 카드와 훈련 Preview를 불러오는 중입니다.";
            _enhancementDetails.text = "중복 카드와 강화·판매 Preview를 불러오는 중입니다.";
            _trainingCard.gameObject.SetActive(false);
            _enhancementCard.gameObject.SetActive(false);
            _enhancementMaterialCard.gameObject.SetActive(false);
            _enhancementMaterialEmpty.gameObject.SetActive(true);
            _scoutPurchaseButton.interactable = false;
            _trainingExecuteButton.interactable = false;
            _enhanceButton.interactable = false;
            _sellButton.interactable = false;
            _registerButton.interactable = false;
            _hasRegisteredEnhancement = false;
        }

        /// <summary>선택한 전력보강 Route의 View State만 표시한다.</summary>
        public void ShowRoute(string routeId)
        {
            bool isScout = string.Equals(routeId, OwnerNavigationRoutes.PowerUpScout, StringComparison.Ordinal);
            bool isTraining = string.Equals(routeId, OwnerNavigationRoutes.PowerUpTraining, StringComparison.Ordinal);
            bool isEnhancement = string.Equals(routeId, OwnerNavigationRoutes.PowerUpEnhancementSale, StringComparison.Ordinal);
            _scoutRoot.gameObject.SetActive(isScout);
            _trainingRoot.gameObject.SetActive(isTraining);
            _enhancementRoot.gameObject.SetActive(isEnhancement);
            if (!isScout)
            {
                if (_scoutProbabilityOverlay != null) _scoutProbabilityOverlay.gameObject.SetActive(false);
                if (_scoutPolicyOverlay != null) _scoutPolicyOverlay.gameObject.SetActive(false);
            }
            _salePanel.gameObject.SetActive(false);
        }

        /// <summary>전력보강 Workspace 전체의 표시 여부를 바꾼다.</summary>
        public void SetVisible(bool visible)
        {
            if (_root != null) _root.gameObject.SetActive(visible);
        }

        /// <summary>전력보강 화면에 겹쳐 열린 확인·결과·정책·판매 창을 최상단부터 하나 닫는다.</summary>
        public bool TryHandleCancel()
        {
            if (_confirmRoot != null && _confirmRoot.gameObject.activeSelf)
            {
                CloseConfirmation();
                return true;
            }
            if (_revealRoot != null && _revealRoot.gameObject.activeSelf)
            {
                _revealRoot.gameObject.SetActive(false);
                return true;
            }
            if (_scoutPolicyOverlay != null && _scoutPolicyOverlay.gameObject.activeSelf)
            {
                CloseScoutPolicy();
                return true;
            }
            if (_scoutProbabilityOverlay != null && _scoutProbabilityOverlay.gameObject.activeSelf)
            {
                _scoutProbabilityOverlay.gameObject.SetActive(false);
                return true;
            }
            if (_salePanel != null && _salePanel.gameObject.activeSelf)
            {
                _salePanel.gameObject.SetActive(false);
                return true;
            }
            if (_hasRegisteredEnhancement)
            {
                _hasRegisteredEnhancement = false;
                RefreshEnhancementTarget();
                return true;
            }
            return false;
        }

        /// <summary>현재 Route 하단에 Command 결과나 차단 사유를 표시한다.</summary>
        public void SetFeedback(string message, bool isError)
        {
            if (_feedback == null) return;
            _feedback.text = message ?? string.Empty;
            _feedback.color = isError ? CareerUiTheme.Error : new Color32(210, 225, 242, 255);
        }

        /// <summary>이미 확정된 Scout 결과를 신규·중복 상태와 함께 Reveal한다.</summary>
        public void ShowScoutReveal(ShopPurchaseResult result)
        {
            if (!result.IsSuccess || result.Items == null || result.Items.Length == 0) return;
            BindScoutResults(result.Items);
            SetFeedback("영입이 완료되었습니다. 영입 선수 탭에서 결과를 확인하세요.", false);
        }

        private void Build(RectTransform host)
        {
            _root = OwnerWorkspaceUiFactory.CreateRoot(host, "OwnerPowerUpWorkspace", true);
            OwnerWorkspaceUiFactory.Panel panel = OwnerWorkspaceUiFactory.CreatePanel(
                _root, "PowerUpPanel", "전력보강 센터");
            panel.Root.GetComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.TexturedPanel);
            OwnerRuntimeUiFactory.Stretch(panel.Root,
                new Vector2(CareerUiTheme.Space4, CareerUiTheme.Space4),
                new Vector2(-CareerUiTheme.Space4, -CareerUiTheme.Space4));
            _scoutRoot = CreateRouteRoot(panel.Content, "ScoutContent");
            _trainingRoot = CreateRouteRoot(panel.Content, "TrainingContent");
            _enhancementRoot = CreateRouteRoot(panel.Content, "EnhancementSaleContent");
            BuildScout();
            BuildTraining();
            BuildEnhancementSale();
            BuildFeedback(panel.Content);
            BuildConfirmOverlay();
            BuildRevealOverlay();
            ShowLoading();
            ShowRoute(OwnerNavigationRoutes.PowerUpScout);
            CareerUiSkin.Apply(_root);
        }

        private void BuildScout()
        {
            BuildScoutReference();
        }

        private void BuildTraining()
        {
            HorizontalLayoutGroup columns = OwnerWorkspaceUiFactory.AddHorizontalLayout(_trainingRoot, CareerUiTheme.Space2);
            columns.padding = new RectOffset(0, 0, 0, 38);
            _trainingRoot.gameObject.AddComponent<CareerUiPreserveTextColor>();
            RectTransform left = CreateTrainingColumn("TrainingTargets", 0.34f, "01  보유 선수 카드");
            OwnerWorkspaceUiFactory.AddVerticalLayout(left, CareerUiTheme.Space2);
            _trainingCardCount = CreateFixedText(left, "TrainingCardCount", 30f, 13, FontStyle.Bold, TextAnchor.MiddleRight);
            _trainingCardCount.color = new Color32(210, 220, 233, 255);
            _trainingCardList = CreateGridScrollContent(left, 3, new Vector2(102f, 153f));
            StyleTrainingScroll(_trainingCardList);
            RectTransform center = CreateTrainingColumn("TrainingCard", 0.30f, "02  선택한 선수");
            VerticalLayoutGroup centerLayout =
                OwnerWorkspaceUiFactory.AddVerticalLayout(center, CareerUiTheme.Space2);
            centerLayout.padding = new RectOffset(0, 0, (int)(CareerUiTheme.Space4 * 3f), 0);
            _trainingCard = OwnerRuntimeUiFactory.CreateRect("SelectedTrainingCard", center);
            SetPreferred(_trainingCard, 260f, 1f);
            _trainingCardFront = OwnerRuntimeUiFactory.CreateRect("CardFront", _trainingCard);
            OwnerRuntimeUiFactory.Stretch(_trainingCardFront);
            _trainingCardFront.gameObject.AddComponent<CareerUiPreserveTextColor>();
            var cardAspect = _trainingCardFront.gameObject.AddComponent<AspectRatioFitter>();
            cardAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            cardAspect.aspectRatio = 2f / 3f;
            _trainingDetails = CreateFixedText(center, "TrainingCardDetails", 140f, 15, FontStyle.Normal, TextAnchor.UpperLeft);
            _trainingDetails.color = Color.white;
            RectTransform right = CreateTrainingColumn("TrainingPrograms", 0.36f, "03  훈련 프로그램");
            OwnerWorkspaceUiFactory.AddVerticalLayout(right, CareerUiTheme.Space2);
            _trainingWallet = CreateFixedText(right, "TrainingWallet", 36f, 16, FontStyle.Bold, TextAnchor.MiddleRight);
            _trainingWallet.color = new Color32(241, 206, 132, 255);
            Text hint = CreateFixedText(right, "TrainingHint", 26f, 12, FontStyle.Normal, TextAnchor.MiddleLeft);
            hint.text = "성장 수치와 소모 포인트를 확인하고 훈련을 선택하세요.";
            hint.color = new Color32(210, 220, 233, 255);
            ScrollRect scroll = OwnerRuntimeUiFactory.CreateVerticalScroll(
                "ProgramScroll", right, out _trainingProgramList);
            StyleTrainingScroll(_trainingProgramList);
            SetPreferred(scroll.GetComponent<RectTransform>(), 230f, 1f);
            _trainingExecuteButton = OwnerWorkspaceUiFactory.CreateButton(
                right, "TrainingExecute", "선택 훈련 적용", RequestTraining);
            SetPreferred(_trainingExecuteButton.GetComponent<RectTransform>(), 54f);
            OwnerUiButtonSkin.Apply(_trainingExecuteButton, OwnerButtonRole.Primary);
        }

        private RectTransform CreateTrainingColumn(string name, float width, string title)
        {
            RectTransform content = CreateColumn(_trainingRoot, name, width, title);
            Transform panel = content.parent;
            panel.GetComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.TexturedPanel);
            panel.Find("HeaderSurface").gameObject.SetActive(false);
            panel.GetComponent<CareerUiFrame>().HeaderRoot.GetComponent<Text>().color = Color.white;
            return content;
        }

        private static void StyleTrainingScroll(RectTransform content)
        {
            Image surface = content.GetComponentInParent<ScrollRect>().GetComponent<Image>();
            surface.gameObject.AddComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.DataImage);
            surface.color = new Color32(15, 24, 37, 235);
        }

        private void CreateTrainingProgramButton(OwnerCardTrainingProgramSnapshot program, bool selected)
        {
            Button button = OwnerWorkspaceUiFactory.CreateButton(_trainingProgramList,
                "Program_" + program.ProgramId, program.Title, () => SelectTrainingProgram(program.ProgramId));
            RectTransform root = button.GetComponent<RectTransform>();
            SetPreferred(root, 94f);
            OwnerUiButtonSkin.SetSelected(button, selected);
            Text title = button.transform.Find("Label").GetComponent<Text>();
            OwnerRuntimeUiFactory.SetAnchors(title.rectTransform, new Vector2(0f, .61f), Vector2.one,
                new Vector2(16f, 0f), new Vector2(-16f, -6f));
            title.alignment = TextAnchor.MiddleLeft;
            title.fontSize = 15;
            title.text = (selected ? "●  " : string.Empty) + program.Title;
            Color ink = selected ? Color.white : new Color32(28, 43, 62, 255);
            Text preview = OwnerWorkspaceUiFactory.CreateText(root, "GrowthPreview",
                $"{program.Current} → {program.Current + program.GainedPoints}   /   상한 {program.Ceiling}",
                15, FontStyle.Bold, TextAnchor.MiddleLeft);
            preview.color = ink;
            OwnerRuntimeUiFactory.SetAnchors(preview.rectTransform, new Vector2(0f, .31f), new Vector2(1f, .62f),
                new Vector2(16f, 0f), new Vector2(-16f, 0f));
            Text cost = OwnerWorkspaceUiFactory.CreateText(root, "TrainingCost",
                program.CanTrain ? $"육성 포인트 {program.DpCost:N0} 소모" : program.BlockedReason,
                12, FontStyle.Normal, TextAnchor.MiddleLeft);
            cost.color = program.CanTrain ? ink : selected ? new Color32(255, 210, 153, 255) : new Color32(140, 63, 38, 255);
            OwnerRuntimeUiFactory.SetAnchors(cost.rectTransform, Vector2.zero, new Vector2(1f, .31f),
                new Vector2(16f, 5f), new Vector2(-16f, 0f));
        }

        private void BuildFeedback(RectTransform parent)
        {
            _feedback = OwnerWorkspaceUiFactory.CreateText(
                parent, "PowerUpFeedback", "대상과 결과를 확인한 뒤 실행하세요.", 13,
                FontStyle.Normal, TextAnchor.MiddleLeft, CareerUiTheme.ReferenceTextSecondary);
            _feedback.gameObject.AddComponent<CareerUiPreserveTextColor>();
            _feedback.color = new Color32(210, 225, 242, 255);
            OwnerRuntimeUiFactory.SetAnchors(
                _feedback.rectTransform, Vector2.zero, new Vector2(1f, 0f),
                Vector2.zero, new Vector2(0f, 32f));
        }

        private void BuildConfirmOverlay()
        {
            Image dim = OwnerRuntimeUiFactory.CreateImage("PowerUpConfirmation", _root, CareerUiTheme.InputBlocker);
            _confirmRoot = dim.rectTransform;
            OwnerRuntimeUiFactory.Stretch(_confirmRoot);
            RectTransform card = CreateCenteredCard(_confirmRoot, "ConfirmationCard", new Vector2(470f, 270f));
            OwnerWorkspaceUiFactory.AddVerticalLayout(card, CareerUiTheme.Space3).padding = new RectOffset(24, 24, 20, 20);
            _confirmTitle = CreateFixedText(card, "ConfirmationTitle", 38f, 20, FontStyle.Bold, TextAnchor.MiddleCenter);
            _confirmBody = CreateFlexibleText(card, "ConfirmationBody", TextAnchor.MiddleCenter);
            RectTransform actions = OwnerRuntimeUiFactory.CreateRect("ConfirmationActions", card);
            HorizontalLayoutGroup layout = OwnerWorkspaceUiFactory.AddHorizontalLayout(actions, CareerUiTheme.Space3);
            layout.childAlignment = TextAnchor.MiddleCenter;
            SetPreferred(actions, 42f);
            OwnerWorkspaceUiFactory.CreateButton(actions, "Cancel", "취소", CloseConfirmation);
            OwnerWorkspaceUiFactory.CreateButton(actions, "Confirm", "확정", ConfirmAction);
            _confirmRoot.gameObject.SetActive(false);
        }

        private void BuildRevealOverlay()
        {
            Image dim = OwnerRuntimeUiFactory.CreateImage("ScoutReveal", _root, new Color(0.02f, 0.03f, 0.05f, 0.94f));
            _revealRoot = dim.rectTransform;
            OwnerRuntimeUiFactory.Stretch(_revealRoot);
            RectTransform card = CreateCenteredCard(_revealRoot, "RevealCard", new Vector2(430f, 390f));
            OwnerWorkspaceUiFactory.AddVerticalLayout(card, CareerUiTheme.Space3).padding = new RectOffset(28, 28, 28, 28);
            CreateFixedText(card, "RevealTitle", 46f, 24, FontStyle.Bold, TextAnchor.MiddleCenter).text = "영입 결과";
            _revealBody = CreateFlexibleText(card, "RevealBody", TextAnchor.MiddleCenter);
            OwnerWorkspaceUiFactory.CreateButton(card, "RevealClose", "확인", () => _revealRoot.gameObject.SetActive(false));
            _revealRoot.gameObject.SetActive(false);
        }

        private void BindScout()
        {
            OwnerRuntimeUiFactory.ClearChildren(_scoutList);
            OwnerScoutScreenSnapshot scout = _snapshot.Scout;
            _scoutWallet.text = scout.WalletText;
            if (scout.State != OwnerPowerUpContentState.Ready)
            {
                OwnerRuntimeUiFactory.ClearChildren(_scoutPolicyOptions);
                _scoutPolicyOverlay.gameObject.SetActive(false);
                _scoutDetails.text = scout.State == OwnerPowerUpContentState.Empty
                    ? "현재 이용할 수 있는 스카우트 상품이 없습니다."
                    : scout.ErrorMessage;
                _scoutSummary.text = _scoutDetails.text;
                _scoutCost.text = "비 용   —";
                _scoutDispatch.text = "이용 가능한\n상품 없음";
                _scoutPurchaseButton.interactable = false;
                return;
            }
            if (FindScoutProduct(_selectedScoutProductId) == null)
                _selectedScoutProductId = scout.Products[0].ProductId;
            OwnerScoutProductSnapshot selectedScout = FindScoutProduct(_selectedScoutProductId);
            int markerIndex = 0;
            int markerCount = 0;
            for (int index = 0; index < scout.Products.Count; index++)
                if (scout.Products[index].DrawCount == 1) markerCount++;
            _scoutMapPage = Math.Min(_scoutMapPage, Math.Max(0, (markerCount - 1) / ScoutMapPageSize));
            for (int index = 0; index < scout.Products.Count; index++)
            {
                OwnerScoutProductSnapshot product = scout.Products[index];
                if (product.DrawCount != 1) continue;
                int position = markerIndex++ - _scoutMapPage * ScoutMapPageSize;
                if (position < 0 || position >= ScoutMapPageSize) continue;
                CreateScoutReferencePin(_scoutList, "Scout_" + product.ProductId,
                    product.Scope,
                    () => SelectScoutProduct(product.ProductId),
                    selectedScout != null && string.Equals(product.Scope, selectedScout.Scope, StringComparison.Ordinal),
                    position, markerCount);
            }
            BindScoutMapPagination(markerCount);
            RefreshScoutDetails();
        }

        private void BindTraining()
        {
            OwnerRuntimeUiFactory.ClearChildren(_trainingCardList);
            OwnerRuntimeUiFactory.ClearChildren(_trainingProgramList);
            OwnerCardTrainingScreenSnapshot training = _snapshot.Training;
            _trainingExecuteButton.transform.Find("Label").GetComponent<Text>().text = "선택 훈련 적용";
            _trainingWallet.text = $"보유 육성 포인트 {training.DevelopmentPoints:N0}";
            _trainingCardCount.text = $"전체 {training.Cards.Count:N0}장";
            if (training.State != OwnerPowerUpContentState.Ready)
            {
                _trainingCard.gameObject.SetActive(false);
                _trainingDetails.text = training.State == OwnerPowerUpContentState.Empty
                    ? "훈련할 보유 카드가 없습니다."
                    : training.ErrorMessage;
                _trainingExecuteButton.interactable = false;
                return;
            }
            if (FindTrainingTarget(_selectedTrainingCardId) == null)
                _selectedTrainingCardId = training.Cards[0].Card.CardId;
            _trainingCard.gameObject.SetActive(true);
            for (int index = 0; index < training.Cards.Count; index++)
            {
                OwnerCollectionCardSnapshot card = training.Cards[index].Card;
                CreateCompactCardButton(_trainingCardList, card, () => SelectTrainingCard(card.CardId),
                    null,
                    string.Equals(card.CardId, _selectedTrainingCardId, StringComparison.Ordinal));
            }
            RefreshTrainingTarget();
        }

        private void BindEnhancementSale()
        {
            if (_snapshot == null) return;
            OwnerRuntimeUiFactory.ClearChildren(_enhancementCardList);
            OwnerEnhancementSaleScreenSnapshot screen = _snapshot.EnhancementSale;
            _enhancementWallet.text = $"보유 스카우트 포인트 {screen.ScoutingPoints:N0}";
            _enhancementCardCount.text = $"보유선수  {screen.Cards.Count:N0}장";
            _registerButton.interactable = screen.State == OwnerPowerUpContentState.Ready;
            _hideLockedButton.transform.Find("Label").GetComponent<Text>().text =
                (_hideLockedCards ? "■" : "□") + " 잠금 선수 숨기기";
            if (screen.State != OwnerPowerUpContentState.Ready)
            {
                _enhancementCard.gameObject.SetActive(false);
                _enhancementMaterialCard.gameObject.SetActive(false);
                _enhancementMaterialEmpty.gameObject.SetActive(true);
                _enhancementDetails.text = screen.State == OwnerPowerUpContentState.Empty
                    ? "강화하거나 판매할 보유 카드가 없습니다."
                    : screen.ErrorMessage;
                _enhanceButton.interactable = false;
                _sellButton.interactable = false;
                return;
            }
            if (FindEnhancementTarget(_selectedEnhancementCardId) == null)
            {
                _hasRegisteredEnhancement = false;
                _selectedEnhancementCardId = screen.Cards[0].Card.CardId;
            }
            _enhancementCard.gameObject.SetActive(_hasRegisteredEnhancement);
            var sortedCards = new List<OwnerEnhancementSaleTargetSnapshot>(screen.Cards);
            sortedCards.Sort((left, right) =>
            {
                int cost = left.Card.Cost.CompareTo(right.Card.Cost);
                if (cost != 0) return _sortCostDescending ? -cost : cost;
                return string.CompareOrdinal(left.Card.CardId, right.Card.CardId);
            });
            for (int index = 0; index < sortedCards.Count; index++)
            {
                OwnerCollectionCardSnapshot card = sortedCards[index].Card;
                if (_hideLockedCards && card.IsLocked) continue;
                CreateCompactCardButton(_enhancementCardList, card, () => SelectAndRegisterEnhancementCard(card.CardId),
                    () => ShowEnhancementCardDetail(card.CardId),
                    string.Equals(card.CardId, _selectedEnhancementCardId, StringComparison.Ordinal));
            }
            RefreshEnhancementTarget();
        }

        /// <summary>도감이 제안한 실제 Scout 상품을 사용자의 명시적 이동 뒤 선택한다.</summary>
        public void SelectScoutProduct(string productId)
        {
            if (FindScoutProduct(productId) == null) return;
            _selectedScoutProductId = productId;
            BindScout();
        }

        private void RefreshScoutDetails()
        {
            OwnerScoutProductSnapshot product = FindScoutProduct(_selectedScoutProductId);
            if (product == null) return;
            var text = new StringBuilder()
                .Append(product.Title).AppendLine().AppendLine()
                .Append("대상 범위  ").Append(product.Scope).AppendLine()
                .Append("비용  ").Append(product.PriceText).AppendLine()
                .Append("획득 수  ").Append(product.DrawCount).AppendLine().AppendLine()
                .Append("집중 영입 Gauge  ").Append(product.PityGauge).Append(" / ").Append(product.PityThreshold).AppendLine()
                .Append("1회당 +").Append(product.PityGainPerDraw)
                .Append(" · 완성 시 비용 ").Append(product.GuaranteedMinimumCost).Append(" 이상 보장").AppendLine().AppendLine()
                .AppendLine("실제 후보 Bucket 확률");
            try
            {
                IReadOnlyList<OwnerScoutProbabilitySnapshot> probabilities = product.Probabilities;
                for (int index = 0; index < probabilities.Count; index++)
                    text.Append(probabilities[index].Label).Append("   ")
                        .Append(probabilities[index].ProbabilityText).AppendLine();
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                _scoutDetails.text = exception.Message;
                _scoutSummary.text = exception.Message;
                _scoutPurchaseButton.interactable = false;
                return;
            }
            if (!product.CanPurchase) text.AppendLine().Append(product.BlockedReason);
            _scoutDetails.text = text.ToString();
            _scoutPurchaseButton.interactable = product.CanPurchase;
            RefreshScoutReference(product);
        }

        private void RequestScoutPurchase()
        {
            OwnerScoutProductSnapshot product = FindScoutProduct(_selectedScoutProductId);
            if (product == null || !product.CanPurchase) return;
            OpenConfirmation("스카우트 파견",
                product.Scope + "에 전담 스카우터를 파견합니다.\n" + product.Title + "\n" +
                product.DrawCount + "명 탐색 · 비용 " + product.PriceText,
                () => ScoutPurchaseRequested?.Invoke(product.ProductId));
        }

        private void SelectTrainingCard(string cardId)
        {
            _selectedTrainingCardId = cardId;
            _selectedTrainingProgramId = string.Empty;
            BindTraining();
        }

        private void SelectTrainingProgram(string programId)
        {
            _selectedTrainingProgramId = programId;
            RefreshTrainingTarget();
        }

        private void RefreshTrainingTarget()
        {
            OwnerCardTrainingTargetSnapshot target = FindTrainingTarget(_selectedTrainingCardId);
            if (target == null) return;
            OwnerRuntimeUiFactory.ClearChildren(_trainingCardFront);
            UI_Popup_OwnerPlayerCard.BuildFrontCard(_trainingCardFront, target.Card);
            OwnerRuntimeUiFactory.ClearChildren(_trainingProgramList);
            if (FindTrainingProgram(target, _selectedTrainingProgramId) == null && target.Programs.Count > 0)
                _selectedTrainingProgramId = target.Programs[0].ProgramId;
            for (int index = 0; index < target.Programs.Count; index++)
            {
                OwnerCardTrainingProgramSnapshot program = target.Programs[index];
                CreateTrainingProgramButton(program,
                    string.Equals(program.ProgramId, _selectedTrainingProgramId, StringComparison.Ordinal));
            }
            OwnerCardTrainingProgramSnapshot selected = FindTrainingProgram(target, _selectedTrainingProgramId);
            if (selected == null)
            {
                _trainingDetails.text = target.Card.DisplayName + "\n적용 가능한 훈련이 없습니다.";
                _trainingExecuteButton.interactable = false;
                return;
            }
            _trainingDetails.text = target.Card.DisplayName + "\n" +
                OwnerCollectionPresentationBuilder.FormatPlayerRole(
                    target.Card.Position,
                    target.Card.PitcherRole, target.Card.IsPositionEvidenceMissing) + " · 비용 " + target.Card.Cost + "\n\n" +
                selected.Title + "\n" + selected.Current + " → " + (selected.Current + selected.GainedPoints) +
                " / 상한 " + selected.Ceiling + "\n육성 포인트 " + selected.DpCost.ToString("N0") +
                (selected.CanTrain ? string.Empty : "\n" + selected.BlockedReason);
            _trainingExecuteButton.interactable = selected.CanTrain;
            _trainingExecuteButton.transform.Find("Label").GetComponent<Text>().text =
                selected.CanTrain ? $"{selected.Title} 훈련 · {selected.DpCost:N0} 포인트 사용" : "훈련 불가 · 조건을 확인하세요";
        }

        private void RequestTraining()
        {
            OwnerCardTrainingTargetSnapshot target = FindTrainingTarget(_selectedTrainingCardId);
            OwnerCardTrainingProgramSnapshot program = FindTrainingProgram(target, _selectedTrainingProgramId);
            if (target == null || program == null || !program.CanTrain) return;
            OpenConfirmation("카드훈련 실행",
                target.Card.DisplayName + " · " + program.Title + "\n" +
                program.Current + " → " + (program.Current + program.GainedPoints) +
                " / 상한 " + program.Ceiling + "\n육성 포인트 " + program.DpCost.ToString("N0") + " 사용",
                () => TrainingRequested?.Invoke(target.Card.CardId, program.ProgramId));
        }

        private void SelectAndRegisterEnhancementCard(string cardId)
        {
            _selectedEnhancementCardId = cardId;
            _saleCount = 1;
            _hasRegisteredEnhancement = FindEnhancementTarget(cardId) != null;
            BindEnhancementSale();
        }

        private void ChangeSaleCount(int delta)
        {
            OwnerEnhancementSaleTargetSnapshot target = FindEnhancementTarget(_selectedEnhancementCardId);
            if (target == null) return;
            _saleCount = Mathf.Clamp(_saleCount + delta, 1, Math.Max(1, target.Card.DuplicateCount));
            RefreshEnhancementTarget();
        }

        private void RefreshEnhancementTarget()
        {
            OwnerEnhancementSaleTargetSnapshot target = FindEnhancementTarget(_selectedEnhancementCardId);
            if (target == null) return;
            _enhancementCard.Bind(
                OwnerCollectionPresentationBuilder.CreateMiniCard(target.Card, true),
                PlayerPortraitSprites.GetDefault(target.Card.Position));
            _enhancementCard.gameObject.SetActive(_hasRegisteredEnhancement);
            bool hasMaterial = _hasRegisteredEnhancement && target.Card.DuplicateCount > 0;
            _enhancementMaterialCard.gameObject.SetActive(hasMaterial);
            _enhancementMaterialEmpty.gameObject.SetActive(!hasMaterial);
            if (hasMaterial)
                _enhancementMaterialCard.Bind(
                    OwnerCollectionPresentationBuilder.CreateMiniCard(target.Card, false),
                    PlayerPortraitSprites.GetDefault(target.Card.Position));
            _saleCount = Mathf.Clamp(_saleCount, 1, Math.Max(1, target.Card.DuplicateCount));
            CardSalePreview sale = target.GetSalePreview(_saleCount);
            string enhancementReason = target.Enhancement.Result switch
            {
                CardEnhancementResult.NoDuplicate => "강화 재료가 없습니다.",
                CardEnhancementResult.MaximumLevel => "최대 강화 단계입니다.",
                _ => "전 능력치 +1"
            };
            _enhancementDetails.text = _hasRegisteredEnhancement
                ? target.Card.DisplayName + "  +" + target.Enhancement.CurrentLevel + " → +" +
                    target.Enhancement.NextLevel + "    " + enhancementReason + " · 중복 1장 사용"
                : target.Card.DisplayName + " 선택 · 등록하기를 눌러 보강할 선수를 등록하세요.";
            _sellButton.transform.Find("Label").GetComponent<Text>().text =
                _saleCount + "장 판매 · " + sale.TotalPriceSp.ToString("N0") + " SP";
            _enhanceButton.interactable = _hasRegisteredEnhancement && target.Enhancement.CanEnhance;
            _sellButton.interactable = sale.CanSell;
        }

        private void RequestEnhancement()
        {
            OwnerEnhancementSaleTargetSnapshot target = FindEnhancementTarget(_selectedEnhancementCardId);
            if (target == null || !_hasRegisteredEnhancement || !target.Enhancement.CanEnhance) return;
            OpenConfirmation("선수카드 합성",
                target.Card.DisplayName + "\n+" + target.Enhancement.CurrentLevel + " → +" +
                target.Enhancement.NextLevel + "\n동일 선수카드 1장을 합성 재료로 사용합니다.\n실패 확률은 없습니다.",
                () => EnhancementRequested?.Invoke(target.Card.CardId));
        }

        private void RequestSale()
        {
            OwnerEnhancementSaleTargetSnapshot target = FindEnhancementTarget(_selectedEnhancementCardId);
            if (target == null || target.Card.DuplicateCount < _saleCount) return;
            CardSalePreview sale = target.GetSalePreview(_saleCount);
            if (!sale.CanSell) return;
            OpenConfirmation("중복 카드 판매",
                target.Card.DisplayName + " 중복 " + _saleCount + "장을 판매하고\n스카우트 포인트 " + sale.TotalPriceSp.ToString("N0") + "을 획득합니다.",
                () => DuplicateSaleRequested?.Invoke(target.Card.CardId, _saleCount));
        }

        private void OpenConfirmation(string title, string body, Action action)
        {
            _confirmTitle.text = title;
            _confirmBody.text = body;
            _confirmedAction = action;
            _confirmRoot.gameObject.SetActive(true);
            _confirmRoot.SetAsLastSibling();
        }

        private void CloseConfirmation()
        {
            _confirmedAction = null;
            _confirmRoot.gameObject.SetActive(false);
        }

        private void ConfirmAction()
        {
            Action action = _confirmedAction;
            CloseConfirmation();
            action?.Invoke();
        }

        private OwnerScoutProductSnapshot FindScoutProduct(string productId)
        {
            if (_snapshot == null) return null;
            for (int index = 0; index < _snapshot.Scout.Products.Count; index++)
                if (string.Equals(_snapshot.Scout.Products[index].ProductId, productId, StringComparison.Ordinal))
                    return _snapshot.Scout.Products[index];
            return null;
        }

        private OwnerCardTrainingTargetSnapshot FindTrainingTarget(string cardId)
        {
            if (_snapshot == null) return null;
            for (int index = 0; index < _snapshot.Training.Cards.Count; index++)
                if (string.Equals(_snapshot.Training.Cards[index].Card.CardId, cardId, StringComparison.Ordinal))
                    return _snapshot.Training.Cards[index];
            return null;
        }

        private static OwnerCardTrainingProgramSnapshot FindTrainingProgram(
            OwnerCardTrainingTargetSnapshot target,
            string programId)
        {
            if (target == null) return null;
            for (int index = 0; index < target.Programs.Count; index++)
                if (string.Equals(target.Programs[index].ProgramId, programId, StringComparison.Ordinal))
                    return target.Programs[index];
            return null;
        }

        private OwnerEnhancementSaleTargetSnapshot FindEnhancementTarget(string cardId)
        {
            if (_snapshot == null) return null;
            for (int index = 0; index < _snapshot.EnhancementSale.Cards.Count; index++)
                if (string.Equals(_snapshot.EnhancementSale.Cards[index].Card.CardId, cardId, StringComparison.Ordinal))
                    return _snapshot.EnhancementSale.Cards[index];
            return null;
        }

        private void ShowEnhancementCardDetail(string cardId)
        {
            if (_snapshot == null) return;
            var cards = new List<OwnerCollectionCardSnapshot>(_snapshot.EnhancementSale.Cards.Count);
            for (int index = 0; index < _snapshot.EnhancementSale.Cards.Count; index++)
            {
                OwnerCollectionCardSnapshot card = _snapshot.EnhancementSale.Cards[index].Card;
                if (_hideLockedCards && card.IsLocked) continue;
                cards.Add(card);
            }
            cards.Sort((left, right) =>
            {
                int cost = left.Cost.CompareTo(right.Cost);
                if (cost != 0) return _sortCostDescending ? -cost : cost;
                return string.CompareOrdinal(left.CardId, right.CardId);
            });
            ShowCardDetail(cards, cardId);
        }

        private void ShowCardDetail(IReadOnlyList<OwnerCollectionCardSnapshot> cards, string cardId)
        {
            int selectedIndex = -1;
            for (int index = 0; index < cards.Count; index++)
            {
                if (string.Equals(cards[index].CardId, cardId, StringComparison.Ordinal))
                {
                    selectedIndex = index;
                    break;
                }
            }
            if (selectedIndex >= 0)
                UI_Popup_OwnerPlayerCard.Show(_root, cards, selectedIndex);
        }

        private static RectTransform CreateRouteRoot(RectTransform parent, string name)
        {
            RectTransform root = OwnerRuntimeUiFactory.CreateRect(name, parent);
            OwnerRuntimeUiFactory.Stretch(root);
            return root;
        }

        private static RectTransform CreateColumn(RectTransform parent, string name, float flex, string title)
        {
            OwnerWorkspaceUiFactory.Panel panel = OwnerWorkspaceUiFactory.CreatePanel(parent, name, title);
            SetFlexibleWidth(panel.Root, flex);
            return panel.Content;
        }

        private static RectTransform CreateScrollContent(RectTransform parent)
        {
            ScrollRect scroll = OwnerRuntimeUiFactory.CreateVerticalScroll("Scroll", parent, out RectTransform content);
            OwnerRuntimeUiFactory.Stretch(scroll.GetComponent<RectTransform>());
            return content;
        }

        private static RectTransform CreateGridScrollContent(
            RectTransform parent,
            int columns,
            Vector2 cellSize)
        {
            ScrollRect scroll = OwnerRuntimeUiFactory.CreateVerticalGridScroll(
                "CardScroll", parent, columns, cellSize, 8f, out RectTransform content);
            SetPreferred(scroll.GetComponent<RectTransform>(), 240f, 1f);
            return content;
        }

        private static Text CreateFixedText(
            Transform parent,
            string name,
            float height,
            int fontSize,
            FontStyle style,
            TextAnchor alignment)
        {
            Text text = OwnerWorkspaceUiFactory.CreateText(parent, name, string.Empty, fontSize, style, alignment,
                CareerUiTheme.ReferenceText);
            SetPreferred(text.rectTransform, height);
            return text;
        }

        private static Text CreateFlexibleText(
            Transform parent,
            string name,
            TextAnchor alignment = TextAnchor.UpperLeft)
        {
            Text text = OwnerWorkspaceUiFactory.CreateText(parent, name, string.Empty, 15, FontStyle.Normal, alignment,
                CareerUiTheme.ReferenceText);
            OwnerWorkspaceUiFactory.SetFlexible(text.rectTransform, 1f, 1f);
            return text;
        }

        private static Button CreateListButton(
            Transform parent,
            string name,
            string label,
            Action action,
            bool selected)
        {
            Button button = OwnerWorkspaceUiFactory.CreateButton(parent, name, label, action);
            SetPreferred(button.GetComponent<RectTransform>(), 60f);
            button.GetComponent<Image>().color = selected
                ? CareerUiTheme.ReferenceAccentLight
                : CareerUiTheme.ReferenceButton;
            Text text = button.transform.Find("Label").GetComponent<Text>();
            text.alignment = TextAnchor.MiddleLeft;
            text.color = selected ? Color.white : CareerUiTheme.ReferenceText;
            return button;
        }

        private static void CreateCompactCardButton(
            Transform parent,
            OwnerCollectionCardSnapshot card,
            Action action,
            Action detailAction,
            bool selected)
        {
            PlayerMiniCardView view = PlayerMiniCardView.CreateRuntime(parent, "Card_" + card.CardId);
            view.UseLineupSlotLayout();
            view.Bind(
                OwnerCollectionPresentationBuilder.CreateMiniCard(card, selected),
                PlayerPortraitSprites.GetDefault(card.Position));
            view.Selected += _ => action?.Invoke();
            view.DetailRequested += _ => detailAction?.Invoke();
            RectTransform rect = view.GetComponent<RectTransform>();
            LayoutElement element = rect.gameObject.AddComponent<LayoutElement>();
            element.minWidth = PlayerMiniCardView.LineupSlotWidth;
            element.preferredWidth = PlayerMiniCardView.LineupSlotWidth;
            element.minHeight = PlayerMiniCardView.LineupSlotHeight;
            element.preferredHeight = PlayerMiniCardView.LineupSlotHeight;
        }

        private static void CreateScoutPin(
            Transform parent,
            string name,
            string label,
            Action action,
            bool selected,
            int index,
            int count)
        {
            Vector2[] positions =
            {
                new Vector2(.20f, .67f), new Vector2(.47f, .69f), new Vector2(.72f, .60f),
                new Vector2(.58f, .38f), new Vector2(.82f, .31f), new Vector2(.31f, .35f)
            };
            Button button = OwnerWorkspaceUiFactory.CreateButton(parent, name, "●  " + label, action);
            RectTransform rect = button.GetComponent<RectTransform>();
            Vector2 position = positions[index % positions.Length];
            rect.anchorMin = position;
            rect.anchorMax = position;
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(count <= 3 ? 168f : 142f, 44f);
            rect.anchoredPosition = Vector2.zero;
            LayoutElement layout = button.GetComponent<LayoutElement>();
            if (layout != null) layout.ignoreLayout = true;
            Image image = button.GetComponent<Image>();
            image.color = selected
                ? new Color32(236, 248, 255, 255)
                : new Color32(15, 48, 87, 238);
            Text text = button.transform.Find("Label").GetComponent<Text>();
            text.fontSize = 12;
            text.color = selected ? new Color32(20, 80, 139, 255) : Color.white;
            Outline outline = image.GetComponent<Outline>() ?? image.gameObject.AddComponent<Outline>();
            outline.effectColor = selected ? new Color32(100, 205, 255, 255) : new Color32(156, 207, 242, 255);
            outline.effectDistance = selected ? new Vector2(2f, -2f) : new Vector2(1f, -1f);
        }

        private static void CreateCardListButton(
            Transform parent,
            OwnerCollectionCardSnapshot card,
            Action action,
            bool selected)
        {
            CreateListButton(parent, "Card_" + card.CardId,
                card.DisplayName + "   " + OwnerCollectionPresentationBuilder.FormatPlayerRole(
                    card.Position,
                    card.PitcherRole, card.IsPositionEvidenceMissing) + "\n" +
                card.OriginYear + " · 비용 " + card.Cost + " · +" + card.EnhancementLevel + " · 중복 " + card.DuplicateCount,
                action, selected);
        }

        private static Button CreateSizedButton(
            Transform parent,
            string name,
            string label,
            Action action,
            float width)
        {
            Button button = OwnerWorkspaceUiFactory.CreateButton(parent, name, label, action);
            LayoutElement element = button.GetComponent<LayoutElement>();
            element.minWidth = width;
            element.preferredWidth = width;
            element.flexibleWidth = 0f;
            return button;
        }

        private static RectTransform CreateCenteredCard(RectTransform parent, string name, Vector2 size)
        {
            Image image = OwnerRuntimeUiFactory.CreateImage(name, parent, CareerUiTheme.ReferencePanel);
            RectTransform card = image.rectTransform;
            OwnerRuntimeUiFactory.SetAnchors(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                -size * 0.5f, size * 0.5f);
            Outline outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = CareerUiTheme.ReferenceAccent;
            outline.effectDistance = new Vector2(2f, -2f);
            return card;
        }

        private static void SetFlexibleWidth(RectTransform target, float width)
        {
            LayoutElement element = target.GetComponent<LayoutElement>() ?? target.gameObject.AddComponent<LayoutElement>();
            element.flexibleWidth = width;
            element.minWidth = 220f;
        }

        private static void SetPreferred(RectTransform target, float height, float flexibleHeight = 0f)
        {
            LayoutElement element = target.GetComponent<LayoutElement>() ?? target.gameObject.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            element.flexibleHeight = flexibleHeight;
        }

        private void OnDestroy()
        {
            ScoutPurchaseRequested = null;
            TrainingRequested = null;
            EnhancementRequested = null;
            DuplicateSaleRequested = null;
            OwnerWorkspaceUiFactory.DestroyOwnedRoot(_root);
        }
    }

    /// <summary>스카우트 선택 화면에 저해상도 레퍼런스의 지도망과 탐색 지점을 벡터로 그린다.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class OwnerScoutMapGraphic : MaskableGraphic
    {
        private static readonly Vector2[] WorldPoints =
        {
            new Vector2(.10f,.66f), new Vector2(.14f,.70f), new Vector2(.18f,.67f), new Vector2(.21f,.61f),
            new Vector2(.16f,.57f), new Vector2(.20f,.51f), new Vector2(.25f,.48f), new Vector2(.29f,.42f),
            new Vector2(.32f,.34f), new Vector2(.28f,.28f), new Vector2(.23f,.35f), new Vector2(.13f,.52f),
            new Vector2(.38f,.69f), new Vector2(.42f,.72f), new Vector2(.47f,.68f), new Vector2(.50f,.61f),
            new Vector2(.47f,.55f), new Vector2(.45f,.48f), new Vector2(.48f,.39f), new Vector2(.53f,.29f),
            new Vector2(.58f,.24f), new Vector2(.59f,.36f), new Vector2(.57f,.49f), new Vector2(.62f,.61f),
            new Vector2(.68f,.67f), new Vector2(.74f,.64f), new Vector2(.78f,.57f), new Vector2(.73f,.50f),
            new Vector2(.79f,.44f), new Vector2(.84f,.36f), new Vector2(.89f,.30f), new Vector2(.86f,.24f),
            new Vector2(.77f,.29f), new Vector2(.69f,.40f), new Vector2(.64f,.50f), new Vector2(.55f,.58f)
        };

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            Rect bounds = GetPixelAdjustedRect();
            Color32 grid = new Color32(91, 159, 215, 75);
            Color32 land = new Color32(198, 230, 250, 220);
            for (int index = 1; index < 10; index++)
            {
                float x = bounds.xMin + bounds.width * index / 10f;
                AddQuad(vertexHelper, new Rect(x, bounds.yMin, 1f, bounds.height), grid);
            }
            for (int index = 1; index < 7; index++)
            {
                float y = bounds.yMin + bounds.height * index / 7f;
                AddQuad(vertexHelper, new Rect(bounds.xMin, y, bounds.width, 1f), grid);
            }
            float dotSize = Mathf.Clamp(Mathf.Min(bounds.width, bounds.height) * .014f, 3f, 7f);
            for (int index = 0; index < WorldPoints.Length; index++)
            {
                Vector2 point = WorldPoints[index];
                float x = bounds.xMin + point.x * bounds.width - dotSize * .5f;
                float y = bounds.yMin + point.y * bounds.height - dotSize * .5f;
                AddQuad(vertexHelper, new Rect(x, y, dotSize, dotSize), land);
            }
        }

        private static void AddQuad(VertexHelper helper, Rect rect, Color32 color)
        {
            int start = helper.currentVertCount;
            helper.AddVert(new Vector3(rect.xMin, rect.yMin), color, Vector2.zero);
            helper.AddVert(new Vector3(rect.xMin, rect.yMax), color, Vector2.up);
            helper.AddVert(new Vector3(rect.xMax, rect.yMax), color, Vector2.one);
            helper.AddVert(new Vector3(rect.xMax, rect.yMin), color, Vector2.right);
            helper.AddTriangle(start, start + 1, start + 2);
            helper.AddTriangle(start, start + 2, start + 3);
        }
    }
}
