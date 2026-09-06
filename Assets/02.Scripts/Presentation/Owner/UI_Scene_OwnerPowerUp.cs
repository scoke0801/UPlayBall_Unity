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
    public sealed class UI_Scene_OwnerPowerUp : MonoBehaviour
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
        private PlayerMiniCardView _trainingCard;
        private PlayerMiniCardView _enhancementCard;
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
            _scoutPurchaseButton.interactable = false;
            _trainingExecuteButton.interactable = false;
            _enhanceButton.interactable = false;
            _sellButton.interactable = false;
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
        }

        /// <summary>전력보강 Workspace 전체의 표시 여부를 바꾼다.</summary>
        public void SetVisible(bool visible)
        {
            if (_root != null) _root.gameObject.SetActive(visible);
        }

        /// <summary>현재 Route 하단에 Command 결과나 차단 사유를 표시한다.</summary>
        public void SetFeedback(string message, bool isError)
        {
            if (_feedback == null) return;
            _feedback.text = message ?? string.Empty;
            _feedback.color = isError ? CareerUiTheme.Error : CareerUiTheme.ReferenceAccent;
        }

        /// <summary>이미 확정된 Scout 결과를 신규·중복 상태와 함께 Reveal한다.</summary>
        public void ShowScoutReveal(ShopPurchaseResult result)
        {
            if (!result.IsSuccess || result.Items == null || result.Items.Length == 0) return;
            var body = new StringBuilder();
            for (int index = 0; index < result.Items.Length; index++)
            {
                if (index > 0) body.AppendLine().AppendLine();
                ShopGrantedItem item = result.Items[index];
                body.Append(item.IsNew ? "신규" : "중복")
                    .Append(" · ").Append(item.GradeLabel).AppendLine()
                    .Append(item.DisplayName);
            }
            _revealBody.text = body.ToString();
            _revealRoot.gameObject.SetActive(true);
            _revealRoot.SetAsLastSibling();
        }

        private void Build(RectTransform host)
        {
            _root = OwnerWorkspaceUiFactory.CreateRoot(host, "OwnerPowerUpWorkspace", true);
            OwnerWorkspaceUiFactory.Panel panel = OwnerWorkspaceUiFactory.CreatePanel(
                _root, "PowerUpPanel", "전력보강");
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
            HorizontalLayoutGroup columns = OwnerWorkspaceUiFactory.AddHorizontalLayout(_scoutRoot, CareerUiTheme.Space3);
            columns.padding = new RectOffset(0, 0, 0, 38);
            RectTransform left = CreateColumn(_scoutRoot, "ScoutProducts", 0.36f, "스카우트 상품");
            _scoutList = CreateScrollContent(left);
            RectTransform right = CreateColumn(_scoutRoot, "ScoutPreview", 0.64f, "영입 조건과 확률");
            OwnerWorkspaceUiFactory.AddVerticalLayout(right, CareerUiTheme.Space2);
            _scoutWallet = CreateFixedText(right, "ScoutWallet", 24f, 15, FontStyle.Bold, TextAnchor.MiddleRight);
            _scoutDetails = CreateFlexibleText(right, "ScoutDetails");
            _scoutPurchaseButton = OwnerWorkspaceUiFactory.CreateButton(
                right, "ScoutPurchase", "선택 상품 영입", RequestScoutPurchase);
        }

        private void BuildTraining()
        {
            HorizontalLayoutGroup columns = OwnerWorkspaceUiFactory.AddHorizontalLayout(_trainingRoot, CareerUiTheme.Space3);
            columns.padding = new RectOffset(0, 0, 0, 38);
            RectTransform left = CreateColumn(_trainingRoot, "TrainingTargets", 0.34f, "훈련 가능 카드");
            _trainingCardList = CreateScrollContent(left);
            RectTransform center = CreateColumn(_trainingRoot, "TrainingCard", 0.27f, "대상 선수");
            OwnerWorkspaceUiFactory.AddVerticalLayout(center, CareerUiTheme.Space2);
            _trainingCard = PlayerMiniCardView.CreateRuntime(center, "SelectedTrainingCard");
            SetPreferred(_trainingCard.GetComponent<RectTransform>(), PlayerMiniCardView.PreferredHeight);
            _trainingDetails = CreateFlexibleText(center, "TrainingCardDetails");
            RectTransform right = CreateColumn(_trainingRoot, "TrainingPrograms", 0.39f, "훈련 프로그램 / 미리보기");
            OwnerWorkspaceUiFactory.AddVerticalLayout(right, CareerUiTheme.Space2);
            _trainingWallet = CreateFixedText(right, "TrainingWallet", 24f, 15, FontStyle.Bold, TextAnchor.MiddleRight);
            ScrollRect scroll = OwnerRuntimeUiFactory.CreateVerticalScroll("ProgramScroll", right, out _trainingProgramList);
            SetPreferred(scroll.GetComponent<RectTransform>(), 200f, 1f);
            _trainingExecuteButton = OwnerWorkspaceUiFactory.CreateButton(
                right, "TrainingExecute", "훈련 실행", RequestTraining);
        }

        private void BuildEnhancementSale()
        {
            HorizontalLayoutGroup columns = OwnerWorkspaceUiFactory.AddHorizontalLayout(_enhancementRoot, CareerUiTheme.Space3);
            columns.padding = new RectOffset(0, 0, 0, 38);
            RectTransform left = CreateColumn(_enhancementRoot, "EnhancementTargets", 0.34f, "보유 카드와 중복");
            _enhancementCardList = CreateScrollContent(left);
            RectTransform center = CreateColumn(_enhancementRoot, "EnhancementCard", 0.27f, "선택 카드");
            OwnerWorkspaceUiFactory.AddVerticalLayout(center, CareerUiTheme.Space2);
            _enhancementCard = PlayerMiniCardView.CreateRuntime(center, "SelectedEnhancementCard");
            SetPreferred(_enhancementCard.GetComponent<RectTransform>(), PlayerMiniCardView.PreferredHeight);
            CreateFlexibleText(center, "EnhancementCardHint", TextAnchor.UpperCenter).text =
                "강화는 중복 카드 1장을 사용해 모든 능력치를 올립니다.";
            RectTransform right = CreateColumn(_enhancementRoot, "EnhancementPreview", 0.39f, "강화 / 판매 Preview");
            OwnerWorkspaceUiFactory.AddVerticalLayout(right, CareerUiTheme.Space2);
            _enhancementWallet = CreateFixedText(right, "EnhancementWallet", 24f, 15, FontStyle.Bold, TextAnchor.MiddleRight);
            _enhancementDetails = CreateFlexibleText(right, "EnhancementDetails");
            RectTransform quantity = OwnerRuntimeUiFactory.CreateRect("SaleQuantity", right);
            HorizontalLayoutGroup quantityLayout = OwnerWorkspaceUiFactory.AddHorizontalLayout(quantity, CareerUiTheme.Space2);
            quantityLayout.childForceExpandWidth = false;
            SetPreferred(quantity, 36f);
            CreateSizedButton(quantity, "SaleMinus", "−", () => ChangeSaleCount(-1), 56f);
            CreateSizedButton(quantity, "SalePlus", "+", () => ChangeSaleCount(1), 56f);
            _enhanceButton = OwnerWorkspaceUiFactory.CreateButton(right, "Enhance", "중복 1장으로 강화", RequestEnhancement);
            _sellButton = OwnerWorkspaceUiFactory.CreateButton(right, "Sell", "선택 수량 판매", RequestSale);
        }

        private void BuildFeedback(RectTransform parent)
        {
            _feedback = OwnerWorkspaceUiFactory.CreateText(
                parent, "PowerUpFeedback", "대상과 결과를 확인한 뒤 실행하세요.", 13,
                FontStyle.Normal, TextAnchor.MiddleLeft, CareerUiTheme.ReferenceTextSecondary);
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
                _scoutDetails.text = scout.State == OwnerPowerUpContentState.Empty
                    ? "현재 이용할 수 있는 스카우트 상품이 없습니다."
                    : scout.ErrorMessage;
                _scoutPurchaseButton.interactable = false;
                return;
            }
            if (FindScoutProduct(_selectedScoutProductId) == null)
                _selectedScoutProductId = scout.Products[0].ProductId;
            for (int index = 0; index < scout.Products.Count; index++)
            {
                OwnerScoutProductSnapshot product = scout.Products[index];
                CreateListButton(_scoutList, "Scout_" + product.ProductId,
                    product.Title + "\n" + product.Scope + "   " + product.PriceText,
                    () => SelectScoutProduct(product.ProductId),
                    string.Equals(product.ProductId, _selectedScoutProductId, StringComparison.Ordinal));
            }
            RefreshScoutDetails();
        }

        private void BindTraining()
        {
            OwnerRuntimeUiFactory.ClearChildren(_trainingCardList);
            OwnerCardTrainingScreenSnapshot training = _snapshot.Training;
            _trainingWallet.text = $"보유 육성 포인트 {training.DevelopmentPoints:N0}";
            if (training.State != OwnerPowerUpContentState.Ready)
            {
                _trainingDetails.text = training.State == OwnerPowerUpContentState.Empty
                    ? "훈련할 보유 카드가 없습니다."
                    : training.ErrorMessage;
                _trainingExecuteButton.interactable = false;
                return;
            }
            if (FindTrainingTarget(_selectedTrainingCardId) == null)
                _selectedTrainingCardId = training.Cards[0].Card.CardId;
            for (int index = 0; index < training.Cards.Count; index++)
            {
                OwnerCollectionCardSnapshot card = training.Cards[index].Card;
                CreateCardListButton(_trainingCardList, card, () => SelectTrainingCard(card.CardId),
                    string.Equals(card.CardId, _selectedTrainingCardId, StringComparison.Ordinal));
            }
            RefreshTrainingTarget();
        }

        private void BindEnhancementSale()
        {
            OwnerRuntimeUiFactory.ClearChildren(_enhancementCardList);
            OwnerEnhancementSaleScreenSnapshot screen = _snapshot.EnhancementSale;
            _enhancementWallet.text = $"보유 스카우트 포인트 {screen.ScoutingPoints:N0}";
            if (screen.State != OwnerPowerUpContentState.Ready)
            {
                _enhancementDetails.text = screen.State == OwnerPowerUpContentState.Empty
                    ? "강화하거나 판매할 보유 카드가 없습니다."
                    : screen.ErrorMessage;
                _enhanceButton.interactable = false;
                _sellButton.interactable = false;
                return;
            }
            if (FindEnhancementTarget(_selectedEnhancementCardId) == null)
                _selectedEnhancementCardId = screen.Cards[0].Card.CardId;
            for (int index = 0; index < screen.Cards.Count; index++)
            {
                OwnerCollectionCardSnapshot card = screen.Cards[index].Card;
                CreateCardListButton(_enhancementCardList, card, () => SelectEnhancementCard(card.CardId),
                    string.Equals(card.CardId, _selectedEnhancementCardId, StringComparison.Ordinal));
            }
            RefreshEnhancementTarget();
        }

        private void SelectScoutProduct(string productId)
        {
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
            for (int index = 0; index < product.Probabilities.Count; index++)
                text.Append(product.Probabilities[index].Label).Append("   ")
                    .Append(product.Probabilities[index].ProbabilityText).AppendLine();
            if (!product.CanPurchase) text.AppendLine().Append(product.BlockedReason);
            _scoutDetails.text = text.ToString();
            _scoutPurchaseButton.interactable = product.CanPurchase;
        }

        private void RequestScoutPurchase()
        {
            OwnerScoutProductSnapshot product = FindScoutProduct(_selectedScoutProductId);
            if (product == null || !product.CanPurchase) return;
            OpenConfirmation("스카우트 실행",
                product.Title + "\n" + product.Scope + "\n" + product.PriceText + "를 사용합니다.",
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
            _trainingCard.Bind(OwnerCollectionPresentationBuilder.CreateMiniCard(target.Card, true));
            OwnerRuntimeUiFactory.ClearChildren(_trainingProgramList);
            if (FindTrainingProgram(target, _selectedTrainingProgramId) == null && target.Programs.Count > 0)
                _selectedTrainingProgramId = target.Programs[0].ProgramId;
            for (int index = 0; index < target.Programs.Count; index++)
            {
                OwnerCardTrainingProgramSnapshot program = target.Programs[index];
                CreateListButton(_trainingProgramList, "Program_" + program.ProgramId,
                    program.Title + $"   {program.Current} → {program.Current + program.GainedPoints} / {program.Ceiling}",
                    () => SelectTrainingProgram(program.ProgramId),
                    string.Equals(program.ProgramId, _selectedTrainingProgramId, StringComparison.Ordinal));
            }
            OwnerCardTrainingProgramSnapshot selected = FindTrainingProgram(target, _selectedTrainingProgramId);
            if (selected == null)
            {
                _trainingDetails.text = target.Card.DisplayName + "\n적용 가능한 훈련 Program이 없습니다.";
                _trainingExecuteButton.interactable = false;
                return;
            }
            _trainingDetails.text = target.Card.DisplayName + "\n" +
                OwnerCollectionPresentationBuilder.FormatPosition(target.Card.Position) + " · 비용 " + target.Card.Cost + "\n\n" +
                selected.Title + "\n" + selected.Current + " → " + (selected.Current + selected.GainedPoints) +
                " / 상한 " + selected.Ceiling + "\n육성 포인트 " + selected.DpCost.ToString("N0") +
                (selected.CanTrain ? string.Empty : "\n" + selected.BlockedReason);
            _trainingExecuteButton.interactable = selected.CanTrain;
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

        private void SelectEnhancementCard(string cardId)
        {
            _selectedEnhancementCardId = cardId;
            _saleCount = 1;
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
            _enhancementCard.Bind(OwnerCollectionPresentationBuilder.CreateMiniCard(target.Card, true));
            _saleCount = Mathf.Clamp(_saleCount, 1, Math.Max(1, target.Card.DuplicateCount));
            CardSalePreview sale = target.GetSalePreview(_saleCount);
            string enhancementReason = target.Enhancement.Result switch
            {
                CardEnhancementResult.NoDuplicate => "강화 재료가 없습니다.",
                CardEnhancementResult.MaximumLevel => "최대 강화 단계입니다.",
                _ => "전 능력치 +1"
            };
            _enhancementDetails.text = target.Card.DisplayName + "\n" +
                OwnerCollectionPresentationBuilder.FormatPosition(target.Card.Position) + " · 비용 " + target.Card.Cost + "\n\n" +
                "강화 Preview\n+" + target.Enhancement.CurrentLevel + " → +" + target.Enhancement.NextLevel +
                " · " + enhancementReason + "\n재료  중복 1장 / 보유 " + target.Card.DuplicateCount + "장\n\n" +
                "판매 Preview\n수량  " + _saleCount + " / " + target.Card.DuplicateCount + "장\n" +
                "단가  스카우트 포인트 " + sale.UnitPriceSp.ToString("N0") +
                "\n획득  스카우트 포인트 " + sale.TotalPriceSp.ToString("N0");
            _enhanceButton.interactable = target.Enhancement.CanEnhance;
            _sellButton.interactable = sale.CanSell;
        }

        private void RequestEnhancement()
        {
            OwnerEnhancementSaleTargetSnapshot target = FindEnhancementTarget(_selectedEnhancementCardId);
            if (target == null || !target.Enhancement.CanEnhance) return;
            OpenConfirmation("카드 강화",
                target.Card.DisplayName + "\n+" + target.Enhancement.CurrentLevel + " → +" +
                target.Enhancement.NextLevel + "\n중복 카드 1장을 사용합니다.",
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

        private static void CreateCardListButton(
            Transform parent,
            OwnerCollectionCardSnapshot card,
            Action action,
            bool selected)
        {
            CreateListButton(parent, "Card_" + card.CardId,
                card.DisplayName + "   " + OwnerCollectionPresentationBuilder.FormatPosition(card.Position) + "\n" +
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
}
