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
        private CardGrid _trainingGrid;
        private CardGrid _enhancementGrid;
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
        private Text _confirmActionLabel;
        private Action _confirmedAction;
        private RectTransform _revealRoot;
        private Text _revealBody;
        private OwnerPowerUpSnapshot _snapshot;
        private string _activeRouteId = OwnerNavigationRoutes.PowerUpScout;
        private readonly HashSet<string> _boundRoutes = new HashSet<string>(StringComparer.Ordinal);
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

        /// <summary>새 조회 묶음으로 교체하고 현재 탭만 갱신한다.</summary>
        public void Bind(OwnerPowerUpSnapshot snapshot, string routeId = null)
        {
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            _boundRoutes.Clear();
            if (routeId != null) _activeRouteId = routeId;
            BindActiveRoute();
        }

        private void BindActiveRoute()
        {
            if (_snapshot == null || _boundRoutes.Contains(_activeRouteId)) return;
            switch (_activeRouteId)
            {
                case OwnerNavigationRoutes.PowerUpScout: BindScout(); break;
                case OwnerNavigationRoutes.PowerUpTraining: BindTraining(); break;
                case OwnerNavigationRoutes.PowerUpEnhancementSale: BindEnhancementSale(); break;
                default: return;
            }
            _boundRoutes.Add(_activeRouteId);
        }

        /// <summary>첫 Snapshot을 만들기 전 세 Route에 입력이 막힌 Loading 상태를 표시한다.</summary>
        public void ShowLoading()
        {
            _scoutWallet.text = string.Empty;
            _trainingWallet.text = string.Empty;
            _enhancementWallet.text = string.Empty;
            _scoutDetails.text = "스카우트 상품과 실제 확률을 불러오는 중입니다.";
            _scoutSummary.text = "스카우트 정보를 불러오는 중입니다.";
            _scoutCost.text = "파견 비용  —";
            _scoutDispatch.text = "파견 정보를 불러오는 중입니다.";
            _scoutPolicyButton.interactable = false;
            _trainingDetails.text = "보유 선수와 훈련 정보를 불러오는 중입니다.";
            _enhancementDetails.text = "보유 카드와 합성 정보를 불러오는 중입니다.";
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
            _activeRouteId = routeId;
            BindActiveRoute();
            bool isScout = string.Equals(routeId, OwnerNavigationRoutes.PowerUpScout, StringComparison.Ordinal);
            bool isTraining = string.Equals(routeId, OwnerNavigationRoutes.PowerUpTraining, StringComparison.Ordinal);
            bool isEnhancement = string.Equals(routeId, OwnerNavigationRoutes.PowerUpEnhancementSale, StringComparison.Ordinal);
            _scoutRoot.gameObject.SetActive(isScout);
            _trainingRoot.gameObject.SetActive(isTraining);
            ApplyTrainingChrome(isTraining || isEnhancement);
            if (isScout) ApplyScoutChrome();
            if (isEnhancement)
                _root.Find("PowerUpPanel").GetComponent<CareerUiFrame>().HeaderRoot.GetComponent<Text>().text = "카드 합성   /   중복 카드로 선수의 전력을 높이세요";
            _enhancementRoot.gameObject.SetActive(isEnhancement);
            if (isTraining || isEnhancement)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_root);
                _trainingGrid?.Refresh();
                _enhancementGrid?.Refresh();
            }
            if (!isScout)
            {
                if (_scoutProbabilityOverlay != null) _scoutProbabilityOverlay.gameObject.SetActive(false);
                if (_scoutPolicyOverlay != null) _scoutPolicyOverlay.gameObject.SetActive(false);
                _scoutBaseInput.interactable = true;
                _scoutBaseInput.blocksRaycasts = true;
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
                CloseScoutProbability();
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
            _feedback.color = isError ? OwnerDashboardStyle.Danger : OwnerDashboardStyle.Success;
        }

        /// <summary>이미 확정된 Scout 결과를 신규·중복 상태와 함께 Reveal한다.</summary>
        public void ShowScoutReveal(ShopPurchaseResult result)
        {
            if (!result.IsSuccess || result.Items == null || result.Items.Length == 0) return;
            BindScoutResults(result.Items);
            if (_scoutProbabilityOverlay.gameObject.activeSelf) CloseScoutProbability();
            SetFeedback("영입이 완료되었습니다. 영입 리포트에서 선수를 확인하세요.", false);
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
            RectTransform card = CreateCenteredCard(_confirmRoot, "ConfirmationCard", new Vector2(520f, 320f));
            UIOwnerFrontOfficePanel.ApplyFramedSurface(card);
            OwnerWorkspaceUiFactory.AddVerticalLayout(card, CareerUiTheme.Space3).padding = new RectOffset(28, 28, 24, 24);
            _confirmTitle = CreateFixedText(card, "ConfirmationTitle", 38f, 20, FontStyle.Bold, TextAnchor.MiddleCenter);
            _confirmBody = CreateFlexibleText(card, "ConfirmationBody", TextAnchor.MiddleLeft);
            OwnerDashboardStyle.SetDataText(_confirmTitle, true);
            OwnerDashboardStyle.SetDataText(_confirmBody);
            _confirmBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            RectTransform actions = OwnerRuntimeUiFactory.CreateRect("ConfirmationActions", card);
            HorizontalLayoutGroup layout = OwnerWorkspaceUiFactory.AddHorizontalLayout(actions, CareerUiTheme.Space3);
            layout.childAlignment = TextAnchor.MiddleCenter;
            SetPreferred(actions, 42f);
            Button cancel = OwnerWorkspaceUiFactory.CreateButton(actions, "Cancel", "취소", CloseConfirmation);
            Button confirm = OwnerWorkspaceUiFactory.CreateButton(actions, "Confirm", "확정", ConfirmAction);
            _confirmActionLabel = confirm.GetComponentInChildren<Text>();
            OwnerUiButtonSkin.Apply(cancel, OwnerButtonRole.Secondary);
            OwnerUiButtonSkin.Apply(confirm, OwnerButtonRole.Primary);
            cancel.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = confirm, selectOnDown = confirm };
            confirm.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = cancel, selectOnUp = cancel };
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
            _scoutPolicyButton.interactable = scout.State == OwnerPowerUpContentState.Ready;
            if (scout.State != OwnerPowerUpContentState.Ready)
            {
                OwnerRuntimeUiFactory.ClearChildren(_scoutPolicyOptions);
                CloseScoutPolicy();
                _scoutDetails.text = scout.State == OwnerPowerUpContentState.Empty
                    ? "현재 이용할 수 있는 스카우트 상품이 없습니다."
                    : scout.ErrorMessage;
                _scoutSummary.text = _scoutDetails.text;
                _scoutCost.text = "비 용   —";
                _scoutDispatch.text = "이용 가능한\n상품 없음";
                _scoutGaugeLabel.text = "—";
                _scoutGaugeFill.rectTransform.sizeDelta = new Vector2(0, 6);
                _scoutSummary.color = scout.State == OwnerPowerUpContentState.Error ? CareerUiTheme.Error : ScoutInk;
                _scoutPurchaseButton.interactable = false;
                return;
            }
            if (FindScoutProduct(_selectedScoutProductId) == null)
                _selectedScoutProductId = scout.Products[0].ProductId;
            OwnerScoutProductSnapshot selectedStaffProduct = FindScoutProduct(_selectedScoutProductId);
            if (!MatchesScoutStaff(selectedStaffProduct))
            {
                int portraitIndex = Array.IndexOf(ScoutStaffIds, selectedStaffProduct.Staff.Id);
                if (portraitIndex >= 0) _scoutPortraitIndex = portraitIndex;
                RefreshScoutPortrait();
            }
            BindScoutScopes(scout);
            RefreshScoutDetails();
        }

        private void BindTraining()
        {
            ClearTrainingPrograms();
            OwnerCardTrainingScreenSnapshot training = _snapshot.Training;
            _trainingExecuteButton.transform.Find("Label").GetComponent<Text>().text = "선택 훈련 적용";
            _trainingWallet.text = $"육성 포인트  {training.DevelopmentPoints:N0}";
            BindTrainingFilterOptions(training);
            if (training.State != OwnerPowerUpContentState.Ready)
            {
                _trainingGrid.Bind(Array.Empty<OwnerCollectionCardSnapshot>(), string.Empty, _snapshot.ResolveCard);
                _trainingCard.gameObject.SetActive(false);
                _trainingDetails.text = training.State == OwnerPowerUpContentState.Empty
                    ? "훈련할 보유 카드가 없습니다."
                    : training.ErrorMessage;
                _trainingExecuteButton.interactable = false;
                RefreshTrainingCards();
                return;
            }
            bool needsSelection = FindTrainingTarget(_selectedTrainingCardId) == null;
            _trainingCard.gameObject.SetActive(true);
            RefreshTrainingCards();
            if (needsSelection)
            {
                _selectedTrainingCardId = _visibleTrainingCards.Count > 0
                    ? _visibleTrainingCards[0].CardId : training.Cards[0].Card.CardId;
                _trainingGrid.Select(_selectedTrainingCardId);
                RefreshTrainingFilterSummary();
            }
            RefreshTrainingTarget();
        }

        private void BindEnhancementSale()
        {
            if (_snapshot == null) return;
            OwnerEnhancementSaleScreenSnapshot screen = _snapshot.EnhancementSale;
            _enhancementWallet.text = $"보유 스카우트 포인트 {screen.ScoutingPoints:N0}";
            BindEnhancementFilterOptions(screen);
            RefreshEnhancementFilterLabels();
            _visibleEnhancementCards.Clear();
            _enhancementCardCount.text = $"보유 선수 {screen.Cards.Count:N0}종";
            _registerButton.interactable = screen.State == OwnerPowerUpContentState.Ready;
            _enhancementEmptyResults.gameObject.SetActive(false);
            if (screen.State != OwnerPowerUpContentState.Ready)
            {
                _hasRegisteredEnhancement = false;
                _enhancementTargetEmpty.gameObject.SetActive(true);
                _enhancementMaterialEmpty.text = "동일 카드 필요";
                _enhancementGrid.Bind(Array.Empty<OwnerCollectionCardSnapshot>(), string.Empty, _snapshot.ResolveCard);
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
                if (!MatchesEnhancementFilters(card)) continue;
                _visibleEnhancementCards.Add(card);
            }
            _enhancementCardCount.text = $"표시 {_visibleEnhancementCards.Count:N0} / 보유 {screen.Cards.Count:N0}종";
            _enhancementEmptyResults.gameObject.SetActive(_visibleEnhancementCards.Count == 0);
            _enhancementGrid.Bind(_visibleEnhancementCards, _hasRegisteredEnhancement ? _selectedEnhancementCardId : string.Empty, _snapshot.ResolveCard);
            RefreshEnhancementTarget();
        }

        /// <summary>도감이 제안한 실제 Scout 상품을 사용자의 명시적 이동 뒤 선택한다.</summary>
        public void SelectScoutProduct(string productId)
        {
            if (FindScoutProduct(productId) == null) return;
            _selectedScoutProductId = productId;
            RevealScoutScope(FindScoutProduct(productId));
            BindScout();
            FocusSelectedScoutScope();
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
                .Append("보장 영입 게이지  ").Append(product.PityGauge.ToString("N0")).Append(" / ")
                .Append(product.PityThreshold.ToString("N0")).AppendLine()
                .Append(product.PityGainPerDraw > 0
                    ? "1회당 +" + product.PityGainPerDraw.ToString("N0") + " · "
                    : string.Empty)
                .Append("가득 차면 구단·연도 정밀 스카우트에서 1군 미보유 선수를 확정 영입합니다 (비용 ")
                .Append(product.GuaranteedMinimumCost).Append(" 이상 우선)").AppendLine().AppendLine()
                .AppendLine("등급별 영입 확률");
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
                "탐색 지역   " + product.Scope + "\n" +
                  "탐색 방침   " + product.Title + "\n" +
                  DescribeScoutStaffEffect(product) + "\n" +
                "탐색 인원   " + product.DrawCount.ToString("N0") + "명\n\n" +
                "파견 비용   " + product.PriceText.Replace("SP", "스카우트 포인트"),
                () => ScoutPurchaseRequested?.Invoke(product.ProductId), "파견하기");
            _scoutBaseInput.interactable = false;
            _scoutBaseInput.blocksRaycasts = false;
            _confirmRoot.GetComponentsInChildren<Button>()[0].Select();
        }

        private void SelectTrainingCard(string cardId)
        {
            _selectedTrainingCardId = cardId;
            _selectedTrainingProgramId = string.Empty;
            _trainingGrid.Select(cardId);
            RefreshTrainingFilterSummary();
            RefreshTrainingTarget();
        }

        private void SelectTrainingProgram(string programId)
        {
            _selectedTrainingProgramId = programId;
            RefreshTrainingSelection();
        }

        private void RefreshTrainingTarget()
        {
            OwnerCardTrainingTargetSnapshot target = FindTrainingTarget(_selectedTrainingCardId);
            if (target == null) return;
            OwnerRuntimeUiFactory.ClearChildren(_trainingCardFront);
            UI_Popup_OwnerPlayerCard.BuildFrontCard(_trainingCardFront, _snapshot.ResolveCard(target.Card));
            ClearTrainingPrograms();
            if (FindTrainingProgram(target, _selectedTrainingProgramId) == null && target.Programs.Count > 0)
                _selectedTrainingProgramId = target.Programs[0].ProgramId;
            for (int index = 0; index < target.Programs.Count; index++)
            {
                OwnerCardTrainingProgramSnapshot program = target.Programs[index];
                CreateTrainingProgramButton(program);
            }
            RefreshTrainingSelection();
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
            _enhancementGrid.Select(cardId);
            RefreshEnhancementTarget();
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
            _enhancementTargetEmpty.gameObject.SetActive(target == null || !_hasRegisteredEnhancement);
            _registerButton.interactable = target != null && _hasRegisteredEnhancement;
            if (target == null) return;
            _enhancementCard.Bind(
                OwnerCollectionPresentationBuilder.CreateMiniCard(_snapshot.ResolveCard(target.Card), false),
                PlayerPortraitSprites.GetDefault(target.Card.Position));
            _enhancementCard.gameObject.SetActive(_hasRegisteredEnhancement);
            bool hasMaterial = _hasRegisteredEnhancement && target.Enhancement.DuplicateCount > 0;
            _enhancementMaterialCard.gameObject.SetActive(hasMaterial);
            _enhancementMaterialEmpty.gameObject.SetActive(!hasMaterial);
            if (hasMaterial)
                _enhancementMaterialCard.Bind(
                    OwnerCollectionPresentationBuilder.CreateMiniCard(_snapshot.ResolveCard(target.Card), false),
                    PlayerPortraitSprites.GetDefault(target.Card.Position));
            _saleCount = Mathf.Clamp(_saleCount, 1, Math.Max(1, target.Card.DuplicateCount));
            CardSalePreview sale = target.GetSalePreview(_saleCount);
            string enhancementReason = target.Enhancement.Result switch
            {
                CardEnhancementResult.NoDuplicate => "강화 재료가 없습니다.",
                CardEnhancementResult.MaximumLevel => "최대 강화 단계입니다.",
                _ => "전 능력치 +1"
            };
            _enhancementMaterialEmpty.text = _hasRegisteredEnhancement
                ? "재료 부족\n\n동일 선수 카드를 추가로 획득하세요"
                : "동일 카드 필요\n\n보유 중복 카드가 자동 등록됩니다";
            _enhancementDetails.text = !_hasRegisteredEnhancement
                ? "왼쪽에서 합성할 선수를 선택하세요."
                : target.Enhancement.CanEnhance
                    ? target.Card.DisplayName + "   +" + target.Enhancement.CurrentLevel + " → +" + target.Enhancement.NextLevel +
                        "\n전 능력치 +1"
                    : enhancementReason + "\n" + (target.Enhancement.Result == CardEnhancementResult.MaximumLevel
                        ? "다른 선수를 선택하세요." : "동일 선수 카드를 추가로 획득하세요.");
            _enhanceButton.transform.Find("Label").GetComponent<Text>().text =
                _hasRegisteredEnhancement && target.Enhancement.CanEnhance ? "+" + target.Enhancement.NextLevel + " 단계로 합성" : "카드 합성";
            _sellButton.transform.Find("Label").GetComponent<Text>().text =
                _saleCount + "장 판매 · " + sale.TotalPriceSp.ToString("N0") + " SP";
            _enhanceButton.interactable = _hasRegisteredEnhancement && target.Enhancement.CanEnhance;
            OwnerUiButtonSkin.Apply(_enhanceButton, OwnerButtonRole.Primary);
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

        private void OpenConfirmation(string title, string body, Action action, string actionLabel = "확정")
        {
            _confirmTitle.text = title;
            _confirmActionLabel.text = actionLabel;
            _confirmBody.text = body;
            _confirmedAction = action;
            _confirmRoot.gameObject.SetActive(true);
            _confirmRoot.SetAsLastSibling();
            FocusTrainingConfirmation();
        }

        private void CloseConfirmation()
        {
            _confirmedAction = null;
            _confirmRoot.gameObject.SetActive(false);
            RestoreTrainingConfirmationFocus();
            if (_activeRouteId == OwnerNavigationRoutes.PowerUpScout)
            {
                _scoutBaseInput.interactable = true;
                _scoutBaseInput.blocksRaycasts = true;
                _scoutPurchaseButton.Select();
            }
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
            foreach (OwnerCollectionCardSnapshot card in _visibleEnhancementCards)
                if (card.CardId == cardId)
                {
                    ShowCardDetail(_visibleEnhancementCards, cardId);
                    return;
                }
            // 필터로 목록에서 숨겨도 작업 패널에 등록한 선수의 상세는 열 수 있다.
            OwnerEnhancementSaleTargetSnapshot target = FindEnhancementTarget(cardId);
            if (target != null) ShowCardDetail(new[] { target.Card }, cardId);
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
                UI_Popup_OwnerPlayerCard.Show(_root, cards, selectedIndex, _snapshot.ResolveCard);
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
