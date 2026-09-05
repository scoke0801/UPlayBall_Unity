using System;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Career;
using Baseball.Game.Manager;
using Baseball.Presentation.Player;
using Baseball.Presentation.SharedScreens;
using Baseball.Presentation.UI;
using Baseball.Simulation.Career;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Career
{
    /// <summary>
    /// 현재 계약·상여 진행·시장 가치와 만료 후 오퍼 선택을 제공하는 계약 화면이다.
    /// </summary>
    public sealed class UI_Scene_Contract : UISceneBase, ICareerTabScreen
    {
        private static readonly Color BackgroundColor = CareerUiTheme.Background;
        private static readonly Color TopBarColor = CareerUiTheme.TopBar;
        private static readonly Color PanelColor = CareerUiTheme.Panel;
        private static readonly Color PanelDarkColor = CareerUiTheme.PanelDark;
        private static readonly Color CardColor = CareerUiTheme.Surface;
        private static readonly Color PortraitBackdropColor = CareerUiTheme.PortraitBackdrop;
        private static readonly Color BorderColor = CareerUiTheme.Border;
        private static readonly Color DividerColor = CareerUiTheme.Divider;
        private static readonly Color AccentColor = CareerUiTheme.Primary;
        private static readonly Color GoldColor = CareerUiTheme.AccentGold;
        private static readonly Color SuccessColor = CareerUiTheme.Success;
        private static readonly Color WarningColor = CareerUiTheme.Warning;
        private static readonly Color ErrorColor = CareerUiTheme.Error;
        private static readonly Color PrimaryTextColor = CareerUiTheme.TextPrimary;
        private static readonly Color SecondaryTextColor = CareerUiTheme.TextSecondary;
        private static readonly Color MutedColor = CareerUiTheme.TextMuted;
        private static readonly Vector2 SharedShellWorkspaceOffset = new(
            0f,
            -(CareerUiTheme.SharedShellChromeHeight * 0.5f + CareerUiTheme.Space2));

        private CareerManager _manager;
        private readonly PlayerContractPresentationModelBuilder _presentationBuilder = new();
        private RectTransform _content;
        private bool _isRetirementConfirming;

        public override bool BlocksLowerInput => true;
        public CareerMainTab MainTab => CareerMainTab.Contract;

        /// <summary>공용 표와 상태 View가 현재 표시 중인 선수 계약 모델이다.</summary>
        public PlayerContractPresentationModel CurrentPresentationModel { get; private set; }

        /// <summary>
        /// 프리팹이 없는 프로토타입 환경에서 계약 화면을 런타임 생성한다.
        /// </summary>
        public static UI_Scene_Contract CreateRuntime(Transform parent)
        {
            var screenObject = new GameObject(
                nameof(UI_Scene_Contract),
                typeof(RectTransform),
                typeof(CanvasGroup));
            screenObject.transform.SetParent(parent, false);
            UI_Scene_Contract screen = screenObject.AddComponent<UI_Scene_Contract>();
            Stretch(screenObject.GetComponent<RectTransform>());
            return screen;
        }

        protected override void OnInitialize()
        {
            _manager = GameManager.EnsureExists().EnsureManager<CareerManager>("CareerManager");
            _manager.CareerChanged += HandleCareerChanged;
            BuildHierarchy();
        }

        protected override void OnShow()
        {
            _isRetirementConfirming = false;
            Render();
        }

        protected override void OnDestroy()
        {
            if (_manager != null)
                _manager.CareerChanged -= HandleCareerChanged;
            base.OnDestroy();
        }

        private void BuildHierarchy()
        {
            RectTransform root = (RectTransform)transform;
            Stretch(root);
            CreateImage("Background", root, BackgroundColor, Vector2.zero, Vector2.zero, stretch: true);
            _content = CreateRect("Content", root, new Vector2(1920f, 1080f), SharedShellWorkspaceOffset);
        }

        private void HandleCareerChanged()
        {
            if (!_manager.HasActiveCareer)
            {
                CurrentPresentationModel = null;
                Hide();
                return;
            }
            if (IsVisible)
                Render();
        }

        private void Render()
        {
            if (_content == null || _manager == null || !_manager.HasActiveCareer)
                return;

            ClearChildren(_content);
            CareerContractView view = _manager.Contract;
            CurrentPresentationModel = _presentationBuilder.Build(view);
            RenderBackgroundAccents();
            RenderContextGuide(view);
            RenderPlayerPanel(view);
            if (view.NegotiationStatus is ContractNegotiationStatus.CurrentTeamOfferAvailable or
                ContractNegotiationStatus.OffersAvailable)
                RenderOfferMode(view, CurrentPresentationModel);
            else
                RenderContractOverview(view, CurrentPresentationModel);
            if (_isRetirementConfirming && view.CanRetireInsteadOfSigning)
                RenderRetirementConfirm(view);
        }

        private void RenderBackgroundAccents()
        {
            CreateImage("TopGlow", _content, CareerUiTheme.TopGlow,
                new Vector2(1920f, 5f), new Vector2(0f, 456f));
            CreateImage("BottomGlow", _content, CareerUiTheme.BottomGlow,
                new Vector2(1920f, 4f), new Vector2(0f, -443f));
        }

        private void RenderTopBar(CareerContractView view)
        {
            RectTransform bar = CreateImage(
                "TopBar", _content, TopBarColor, new Vector2(1920f, 80f), new Vector2(0f, 500f));
            CreateImage("TopBarBottom", bar, BorderColor, new Vector2(1920f, 2f), new Vector2(0f, -39f));
            Text logo = CreateText(
                "Logo", bar, "UPlayBall", 34, FontStyle.BoldAndItalic, TextAnchor.MiddleLeft,
                new Vector2(310f, 50f), new Vector2(-800f, 5f), PrimaryTextColor);
            AddTextOutline(logo, new Color(0.05f, 0.34f, 0.62f, 0.9f), 1.5f);
            CreateText(
                "LogoCaption", bar, "프로야구 선수 커리어", 10, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(230f, 18f), new Vector2(-796f, -23f), AccentColor);
            CreateTopBarSegment(
                bar, "리그", $"{view.SeasonYear}  {GetLeagueLabel(view.LeagueLevel)} 리그",
                new Vector2(-365f, 0f), new Vector2(420f, 64f));
            CreateTopBarSegment(
                bar, "시즌", GetSeasonPhaseLabel(view.SeasonPhase),
                new Vector2(25f, 0f), new Vector2(300f, 64f));
            CreateTopBarSegment(
                bar, "보유 자금", FormatMoney(view.AvailableMoney),
                new Vector2(390f, 0f), new Vector2(370f, 64f), GoldColor);
        }

        private static void CreateTopBarSegment(
            Transform parent,
            string eyebrow,
            string value,
            Vector2 position,
            Vector2 size,
            Color? valueColor = null)
        {
            RectTransform segment = CreateRect(eyebrow + "Segment", parent, size, position);
            CreateImage("LeftDivider", segment, DividerColor, new Vector2(2f, size.y - 10f),
                new Vector2(-size.x * 0.5f + 1f, 0f));
            CreateText(
                "Eyebrow", segment, eyebrow, 9, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(size.x - 32f, 16f), new Vector2(0f, 16f), MutedColor);
            CreateText(
                "Value", segment, value, 18, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(size.x - 32f, 30f), new Vector2(0f, -7f), valueColor ?? PrimaryTextColor);
        }

        private void RenderContextGuide(CareerContractView view)
        {
            bool isOfferMode = view.NegotiationStatus is ContractNegotiationStatus.CurrentTeamOfferAvailable or
                ContractNegotiationStatus.OffersAvailable;
            string subtitle = isOfferMode
                ? "제안 금액뿐 아니라 예상 역할과 육성 환경을 비교해 다음 커리어를 결정하세요."
                : "현재 보장 조건과 상여 진행, 계약 만료 뒤의 시장 가치를 확인합니다.";
            float subtitleWidth = isOfferMode
                ? 900f
                : 640f;
            CreateText(
                "ContextGuide", _content, subtitle,
                15, FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(subtitleWidth, 40f), new Vector2(-860f + subtitleWidth * 0.5f, 438f),
                SecondaryTextColor);
        }

        private void RenderPlayerPanel(CareerContractView view)
        {
            RectTransform panel = CreatePanel(
                "PlayerPanel", "내 선수",
                new Vector2(390f, 790f), new Vector2(-750f, -20f));

            RectTransform card = CreateSection(
                "PlayerCard", panel, new Vector2(346f, 310f), new Vector2(0f, 150f), CardColor);
            CreateImage(
                "CardStripe", card, AccentColor, new Vector2(6f, 292f), new Vector2(-167f, 0f));
            CreateText(
                "Overall", card, view.Overall.ToString(), 52, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(110f, 70f), new Vector2(-105f, 98f), PrimaryTextColor);
            CreateText(
                "OverallLabel", card, "OVR", 12, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(90f, 22f), new Vector2(-105f, 58f), SecondaryTextColor);
            CreateImage(
                "PortraitBackdrop", card, PortraitBackdropColor,
                new Vector2(180f, 200f), new Vector2(62f, 28f));
            RectTransform portrait = CreateImage(
                "Portrait", card, Color.white, new Vector2(180f, 200f), new Vector2(62f, 28f));
            Image portraitImage = portrait.GetComponent<Image>();
            portraitImage.sprite = PlayerPortraitSprites.GetDefault(view.Position);
            portraitImage.preserveAspect = true;
            CreateText(
                "Position", card, GetPositionCode(view.Position), 20, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(78f, 48f), new Vector2(-113f, -94f),
                PrimaryTextColor);
            CreateText(
                "Name", card, view.PlayerName, 27, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(320f, 46f), new Vector2(0f, -128f), PrimaryTextColor);

            CreateInfoRow(panel, "소속 구단", view.CurrentContract.TeamName, -30f);
            CreateInfoRow(panel, "나이", $"{view.Age}세", -78f);
            CreateInfoRow(panel, "계약 역할", GetExpectedRoleLabel(view.CurrentContract.ExpectedRole), -126f,
                SuccessColor);
            CreateInfoRow(panel, "계약 만료", $"{view.CurrentContract.EndYear} 시즌 종료", -174f,
                view.NegotiationStatus == ContractNegotiationStatus.Active ? PrimaryTextColor : WarningColor);

            RenderNegotiationAction(panel, view);
        }

        private void RenderNegotiationAction(RectTransform panel, CareerContractView view)
        {
            string buttonLabel;
            string guide;
            bool interactable = view.CanBeginNegotiation || view.CanAcceptExtension;
            switch (view.NegotiationStatus)
            {
                case ContractNegotiationStatus.NegotiationAvailable:
                    buttonLabel = "계약 오퍼 확인";
                    guide = "오프시즌을 마감하고 FA 시장을 엽니다.";
                    break;
                case ContractNegotiationStatus.ExpiringThisSeason:
                    buttonLabel = "시즌 종료 후 협상";
                    guide = "이번 시즌 결산 뒤 새 구단의 제안을 확인할 수 있습니다.";
                    break;
                case ContractNegotiationStatus.OffersAvailable:
                    buttonLabel = "오퍼 비교 중";
                    guide = "금액과 예상 출장 기회를 함께 비교하세요.";
                    break;
                case ContractNegotiationStatus.CurrentTeamOfferAvailable:
                    buttonLabel = "기존 구단 우선 협상";
                    guide = "수락하거나 보류·거절한 뒤 공개 시장을 확인할 수 있습니다.";
                    break;
                case ContractNegotiationStatus.ExtensionOfferAvailable:
                    RenewalContractOfferView extension = view.ExtensionOffer.Value;
                    buttonLabel = "연장 계약 수락";
                    guide = $"{extension.ContractYears}년 연장 · 연봉 {FormatMoney(extension.AnnualSalary)} · " +
                            $"{GetExpectedRoleLabel(extension.ExpectedRole)}";
                    break;
                default:
                    buttonLabel = "현재 계약 유지";
                    guide = $"다음 협상: {view.CurrentContract.EndYear} 시즌 종료 후";
                    break;
            }

            Button button = CreateButton(
                "Negotiation", panel, buttonLabel,
                new Vector2(330f, 58f), new Vector2(0f, -226f),
                interactable ? CareerUiTheme.SurfaceSelected : CareerUiTheme.SurfaceSubtle,
                out Text label);
            label.fontSize = 22;
            button.interactable = interactable;
            if (interactable)
            {
                if (view.CanAcceptExtension)
                    button.onClick.AddListener(() => _manager.AcceptCurrentTeamExtension());
                else
                    button.onClick.AddListener(() => _manager.BeginContractNegotiation());
            }
            if (view.CanAcceptExtension)
            {
                Button decline = CreateButton(
                    "DeclineExtension", panel, "이번 연장 제안 거절",
                    new Vector2(200f, 30f), new Vector2(0f, -270f),
                    Color.Lerp(CareerUiTheme.PanelDark, ErrorColor, 0.28f), out Text declineLabel);
                declineLabel.fontSize = 12;
                decline.onClick.AddListener(() => _manager.DeclineCurrentTeamExtension());
            }
            CreateText(
                "NegotiationGuide", panel, guide, 12, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(340f, 36f), new Vector2(0f, view.CanAcceptExtension ? -304f : -276f),
                interactable ? SecondaryTextColor : MutedColor);
        }

        private void RenderContractOverview(
            CareerContractView view,
            PlayerContractPresentationModel presentation)
        {
            RenderCurrentContract(view);
            RenderContractHistory(presentation);
            RenderBonusConditions(view, presentation);
            RenderMarketAndStatus(view);
        }

        private void RenderCurrentContract(CareerContractView view)
        {
            CurrentContractView contract = view.CurrentContract;
            RectTransform panel = CreatePanel(
                "CurrentContract", "현재 계약",
                new Vector2(760f, 350f), new Vector2(-170f, 200f));
            CreateContractMetric(panel, "계약 기간",
                $"{contract.SignedYear} ~ {contract.EndYear}\n({contract.ContractYears}년)",
                new Vector2(-238f, 52f));
            CreateContractMetric(panel, "연봉", FormatMoney(contract.AnnualSalary),
                new Vector2(0f, 52f), GoldColor);
            CreateContractMetric(panel, "계약금", FormatMoney(contract.SigningBonus),
                new Vector2(238f, 52f));
            CreateContractMetric(panel, "총 보장 금액", FormatMoney(contract.GuaranteedValue),
                new Vector2(-238f, -48f));
            CreateContractMetric(panel, "이번 시즌 이후",
                $"{contract.RemainingSeasons}시즌", new Vector2(0f, -48f));
            CreateContractMetric(panel, "예상 역할", GetExpectedRoleLabel(contract.ExpectedRole),
                new Vector2(238f, -48f), SuccessColor);
        }

        private static void CreateContractMetric(
            Transform parent,
            string label,
            string value,
            Vector2 position,
            Color? valueColor = null)
        {
            RectTransform cell = CreateFramedSurface(
                "Metric_" + label, parent, PanelDarkColor, new Vector2(224f, 96f), position);
            CreateText(
                "Label", cell, label, 13, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(205f, 24f), new Vector2(0f, 28f), SecondaryTextColor);
            CreateText(
                "Value", cell, value, 23, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(210f, 62f), new Vector2(0f, -13f), valueColor ?? PrimaryTextColor);
        }

        private void RenderContractHistory(PlayerContractPresentationModel presentation)
        {
            RectTransform panel = CreatePanel(
                "ContractHistory", "계약 이력 · 최근 12건",
                new Vector2(760f, 390f), new Vector2(-170f, -190f));
            RectTransform tableHost = CreateRect(
                "ContractHistoryTableHost", panel, new Vector2(710f, 300f), Vector2.zero);
            CompactRecordTableView table = CompactRecordTableView.CreateRuntime(
                tableHost,
                "ContractHistoryTable");
            table.Bind(presentation.ContractHistory, presentation.ContractHistoryState);
        }

        private void RenderBonusConditions(
            CareerContractView view,
            PlayerContractPresentationModel presentation)
        {
            RectTransform panel = CreatePanel(
                "Bonus", "상여 조건",
                new Vector2(650f, 440f), new Vector2(570f, 155f));
            RectTransform tableHost = CreateRect(
                "BonusProgressTableHost", panel, new Vector2(610f, 230f), new Vector2(0f, 40f));
            CompactRecordTableView table = CompactRecordTableView.CreateRuntime(
                tableHost,
                "BonusProgressTable");
            table.Bind(presentation.BonusProgress, presentation.BonusProgressState);

            CreateImage("BonusFooter", panel, DividerColor, new Vector2(610f, 2f), new Vector2(0f, -91f));
            CreateText(
                "AchievedLabel", panel, "현재 달성 상여", 12, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(160f, 16f), new Vector2(-148f, -108f), SecondaryTextColor);
            CreateText(
                "Achieved", panel, FormatMoney(view.AchievedBonus), 20, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(190f, 24f), new Vector2(-148f, -132f), GoldColor);
            CreateText(
                "MaximumLabel", panel, "최대 수령 가능", 12, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(160f, 16f), new Vector2(148f, -108f), SecondaryTextColor);
            CreateText(
                "Maximum", panel, FormatMoney(view.MaximumBonus), 20, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(190f, 24f), new Vector2(148f, -132f), PrimaryTextColor);
        }

        private void RenderMarketAndStatus(CareerContractView view)
        {
            RectTransform panel = CreatePanel(
                "Market", "시장 가치 / 협상 정보",
                new Vector2(650f, 300f), new Vector2(570f, -245f));
            CreateMarketRow(panel, "FA 예상 연봉",
                view.MarketOfferCount > 0
                    ? $"{FormatMoney(view.MarketSalaryMinimum)} ~ {FormatMoney(view.MarketSalaryMaximum)}"
                    : "평가 자료 부족",
                68f, GoldColor);
            CreateMarketRow(panel, "예상 시장 역할", GetExpectedRoleLabel(view.MarketExpectedRole), 42f,
                SuccessColor);
            CreateMarketRow(panel, "현재 팀 포지션 필요도", $"{view.CurrentTeamPositionNeed} / 100", 16f);
            CreateMarketRow(panel, "예상 오퍼 구단", $"{view.MarketOfferCount}개 구단", -10f);

            GetStatusContent(view, out string statusTitle, out string statusDescription, out Color statusColor);
            RectTransform status = CreateFramedSurface(
                "Status", panel, CareerUiTheme.SurfaceSubtle,
                new Vector2(590f, 40f), new Vector2(0f, -48f));
            CreateText("StatusTitle", status, statusTitle, 16, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(530f, 20f), new Vector2(12f, 8f), statusColor);
            CreateText("StatusDescription", status, statusDescription, 12, FontStyle.Normal,
                TextAnchor.MiddleLeft, new Vector2(530f, 18f), new Vector2(12f, -9f), SecondaryTextColor);
            if (!string.IsNullOrEmpty(view.LastError))
            {
                CreateText("Error", panel, view.LastError, 11, FontStyle.Normal, TextAnchor.MiddleCenter,
                    new Vector2(590f, 18f), new Vector2(0f, -82f), ErrorColor);
            }
        }

        private static void CreateMarketRow(
            Transform parent,
            string label,
            string value,
            float y,
            Color? valueColor = null)
        {
            RectTransform row = CreateFramedSurface(
                "Market_" + label, parent, PanelDarkColor, new Vector2(610f, 26f), new Vector2(0f, y));
            CreateText("Label", row, label, 12, FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(250f, 24f), new Vector2(-160f, 0f), SecondaryTextColor);
            CreateText("Value", row, value, 14, FontStyle.Bold, TextAnchor.MiddleRight,
                new Vector2(310f, 24f), new Vector2(130f, 0f), valueColor ?? PrimaryTextColor);
        }

        private void RenderOfferMode(
            CareerContractView view,
            PlayerContractPresentationModel presentation)
        {
            RectTransform panel = CreatePanel(
                "Offers",
                view.CanOpenMarket ? "기존 구단 우선 협상" : "계약 오퍼 비교",
                new Vector2(1410f, 700f), new Vector2(260f, 10f));
            CreateText(
                "Guide", panel,
                view.CanOpenMarket
                    ? "기존 구단의 제안을 먼저 검토하세요. 보류하면 오퍼가 철회될 위험을 감수하고 외부 시장을 확인합니다."
                    : "높은 연봉보다 실제 출장 기회가 더 좋은 계약일 수 있습니다. 예상 역할과 출장 비율을 함께 비교하세요.",
                14, FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(1320f, 38f), new Vector2(0f, 285f), SecondaryTextColor);

            if (view.IsNextSeasonForcedFinal)
            {
                CreateText(
                    "ForcedFinalNotice", panel,
                    $"{view.GuaranteedRetirementAge}세 규정에 따라, 어떤 계약을 택하든 다음 시즌이 마지막 시즌으로 선언됩니다.",
                    14, FontStyle.Bold, TextAnchor.MiddleLeft,
                    new Vector2(1320f, 30f), new Vector2(0f, 258f), WarningColor);
            }

            RectTransform tableHost = CreateRect(
                "ContractOfferTableHost", panel, new Vector2(1320f, 470f), new Vector2(0f, -13f));
            CompactRecordTableView table = CompactRecordTableView.CreateRuntime(
                tableHost,
                "ContractOfferTable");
            table.Bind(presentation.Offers, presentation.OffersState);
            table.RowSelected += HandleOfferRowSelected;

            RenderOfferActions(panel, view);
        }

        private void HandleOfferRowSelected(string rowId)
        {
            if (PlayerContractPresentationModel.TryGetOfferTeamId(rowId, out int teamId))
                _manager.SelectContractOffer(teamId);
        }

        /// <summary>계약 확정·시장 확인·현역 은퇴를 한 줄에 배치한다. 은퇴는 계약 버튼과 색으로 구분한다.</summary>
        private void RenderOfferActions(RectTransform panel, CareerContractView view)
        {
            bool canRetire = view.CanRetireInsteadOfSigning;
            float signX = view.CanOpenMarket ? canRetire ? -450f : -350f : canRetire ? -200f : 0f;
            float signWidth = view.CanOpenMarket && canRetire ? 440f : 520f;
            Button signButton = CreateButton(
                "SignOffer", panel,
                view.IsUnsignedRetirementRequired
                    ? "Rookie 테스트 입단 연속 실패"
                    : view.CanSignSelectedOffer ? "선택한 구단과 계약" : "계약할 구단을 선택하세요",
                new Vector2(signWidth, 68f), new Vector2(signX, -302f),
                view.CanSignSelectedOffer
                    ? CareerUiTheme.SurfaceSelected
                    : CareerUiTheme.SurfaceSubtle,
                out Text label);
            label.fontSize = 22;
            signButton.interactable = view.CanSignSelectedOffer;
            if (view.CanSignSelectedOffer)
                signButton.onClick.AddListener(SignSelectedOffer);

            if (view.CanOpenMarket)
            {
                float holdX = canRetire ? -60f : 185f;
                float declineX = canRetire ? 250f : 535f;
                Button holdButton = CreateButton(
                    "HoldAndOpenMarket", panel, "보류하고 시장 보기",
                    new Vector2(300f, 68f), new Vector2(holdX, -302f),
                    CareerUiTheme.SurfaceSelected, out Text holdLabel);
                holdLabel.fontSize = 18;
                holdButton.onClick.AddListener(() => _manager.OpenContractMarket(true));
                Button declineButton = CreateButton(
                    "DeclineAndOpenMarket", panel, "거절하고 시장 보기",
                    new Vector2(300f, 68f), new Vector2(declineX, -302f),
                    Color.Lerp(CareerUiTheme.PanelDark, ErrorColor, 0.28f), out Text declineLabel);
                declineLabel.fontSize = 18;
                declineButton.onClick.AddListener(() => _manager.OpenContractMarket(false));
            }

            if (canRetire)
            {
                float retireX = view.CanOpenMarket ? 570f : 330f;
                float retireWidth = view.CanOpenMarket ? 260f : 300f;
                Button retireButton = CreateButton(
                    "RetireInstead", panel,
                    view.IsUnsignedRetirementRequired ? "커리어 종료 확인" : "여기서 은퇴하기",
                    new Vector2(retireWidth, 68f), new Vector2(retireX, -302f),
                    new Color(0.10f, 0.09f, 0.07f, 1f), out Text retireLabel);
                retireLabel.fontSize = 18;
                retireLabel.color = GoldColor;
                retireButton.onClick.AddListener(() =>
                {
                    _isRetirementConfirming = true;
                    Render();
                });
            }

            if (!string.IsNullOrEmpty(view.LastError))
            {
                CreateText("Error", panel, view.LastError, 12, FontStyle.Normal, TextAnchor.MiddleCenter,
                    new Vector2(900f, 24f), new Vector2(0f, -338f), ErrorColor);
            }
        }

        /// <summary>제안을 눈앞에 두고 은퇴를 확정하기 전 되돌릴 수 없음을 한 번 더 확인시킨다.</summary>
        private void RenderRetirementConfirm(CareerContractView view)
        {
            RectTransform backdrop = CreateImage(
                "RetirementBackdrop", _content, new Color(0.002f, 0.006f, 0.011f, 0.88f),
                Vector2.zero, Vector2.zero, stretch: true);
            backdrop.GetComponent<Image>().raycastTarget = true;

            RectTransform panel = CreateSection(
                "RetirementConfirm", backdrop, new Vector2(880f, 420f), Vector2.zero, CardColor);
            CreateText("Eyebrow", panel, "한 선수의 기록", 14, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(600f, 26f), new Vector2(0f, 160f), GoldColor);
            string retirementTitle = view.IsUnsignedRetirementRequired
                ? "모든 계약과 Rookie 테스트 입단이 끝났습니다."
                : $"{view.Age}세, 여기서 선수 생활을 마칩니까?";
            CreateText("Title", panel, retirementTitle, 30, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(800f, 50f), new Vector2(0f, 105f), PrimaryTextColor);

            string offerSummary = view.RenewalOffers.Length > 0
                ? $"테이블 위에는 {view.RenewalOffers.Length}개의 제안이 남아 있습니다. " +
                  $"최고 연봉 {FormatMoney(GetBestOfferSalary(view.RenewalOffers))}."
                : "지금 테이블 위에 남은 제안은 없습니다.";
            CreateText("Summary", panel, offerSummary, 17, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(800f, 30f), new Vector2(0f, 45f), SecondaryTextColor);
            string retirementMessage = view.IsUnsignedRetirementRequired
                ? "두 구단의 테스트 입단에도 실패해 현역 등록 경로가 없습니다.\n" +
                  "이 시점의 커리어로 회고가 만들어집니다."
                : "은퇴를 확정하면 남은 제안은 모두 거절한 것으로 기록되고,\n" +
                  "이 시점의 커리어로 회고가 만들어집니다. 되돌릴 수 없습니다.";
            CreateText("Message", panel, retirementMessage,
                17, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(800f, 70f), new Vector2(0f, -15f), SecondaryTextColor);

            Button cancel = CreateButton("KeepPlaying", panel,
                view.IsUnsignedRetirementRequired ? "회고 전 화면으로" : "한 시즌 더 뛴다",
                new Vector2(330f, 66f), new Vector2(-180f, -125f),
                CareerUiTheme.SurfaceSelected, out Text cancelLabel);
            cancelLabel.fontSize = 19;
            cancel.onClick.AddListener(() =>
            {
                _isRetirementConfirming = false;
                Render();
            });
            Button confirm = CreateButton("ConfirmRetirement", panel,
                view.IsUnsignedRetirementRequired ? "커리어 종료" : "은퇴 확정",
                new Vector2(330f, 66f), new Vector2(180f, -125f),
                Color.Lerp(CareerUiTheme.PanelDark, GoldColor, 0.3f), out Text confirmLabel);
            confirmLabel.fontSize = 19;
            confirmLabel.color = GoldColor;
            confirm.onClick.AddListener(() =>
            {
                _isRetirementConfirming = false;
                if (!_manager.RetireFromContractOffers())
                    Render();
            });
        }

        private static long GetBestOfferSalary(RenewalContractOfferView[] offers)
        {
            long best = 0L;
            for (int index = 0; index < offers.Length; index++)
            {
                if (offers[index].AnnualSalary > best)
                    best = offers[index].AnnualSalary;
            }
            return best;
        }

        private void SignSelectedOffer()
        {
            if (_manager.SignSelectedContractOffer())
                CareerTabNavigation.Show(CareerMainTab.Home);
        }

        private static void CreateInfoRow(
            Transform parent,
            string label,
            string value,
            float y,
            Color? valueColor = null)
        {
            RectTransform row = CreateFramedSurface(
                "Info_" + label, parent, PanelDarkColor, new Vector2(344f, 42f), new Vector2(0f, y));
            CreateText("Label", row, label, 12, FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(145f, 30f), new Vector2(-90f, 0f), SecondaryTextColor);
            CreateText("Value", row, value, 13, FontStyle.Bold, TextAnchor.MiddleRight,
                new Vector2(175f, 30f), new Vector2(75f, 0f), valueColor ?? PrimaryTextColor);
        }

        private RectTransform CreatePanel(
            string name,
            string title,
            Vector2 size,
            Vector2 position)
        {
            RectTransform panel = CreateRect(name, _content, size, position);
            RectTransform decorativeFrame = CreateImage(
                "DecorativeFrame", panel, Color.white, Vector2.zero, Vector2.zero, stretch: true);
            MarkVisual(decorativeFrame, CareerUiVisualRole.DecorativeFrame);
            RectTransform content = CreateRect("ContentSafeArea", panel, size, Vector2.zero);
            RectTransform interaction = CreateRect("InteractionRoot", panel, size, Vector2.zero);
            Vector4 padding = size.y <= 320f
                ? new Vector4(20f, 20f, 20f, 60f)
                : CareerUiTheme.DenseFramePadding;
            CareerUiFrame.ApplyContentPadding(content, size, padding);
            CareerUiFrame.ApplyContentPadding(interaction, size, padding);
            RectTransform header = CreateRect(
                "HeaderRoot", panel, new Vector2(size.x - 72f, 48f),
                new Vector2(0f, size.y * 0.5f - 54f));
            CreateText(
                "Heading", header, title, 20, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(size.x * 0.62f, 32f), new Vector2(0f, -7f), PrimaryTextColor);
            CareerUiFrame frame = panel.gameObject.AddComponent<CareerUiFrame>();
            frame.Initialize(
                decorativeFrame.GetComponent<Image>(), header, content, interaction,
                padding, false);
            return content;
        }

        private static RectTransform CreateSection(
            string name,
            Transform parent,
            Vector2 size,
            Vector2 position,
            Color color)
        {
            RectTransform section = CreateRect(name, parent, size, position);
            RectTransform surface = CreateImage(
                "FlatSurface", section, color, Vector2.zero, Vector2.zero, stretch: true);
            MarkVisual(surface, CareerUiVisualRole.FlatSurface);
            return section;
        }

        private static RectTransform CreateFramedSurface(
            string name,
            Transform parent,
            Color color,
            Vector2 size,
            Vector2 position)
        {
            RectTransform surface = CreateImage(name, parent, color, size, position);
            MarkVisual(surface, CareerUiVisualRole.FramedSurface);
            return surface;
        }

        private static void GetStatusContent(
            CareerContractView view,
            out string title,
            out string description,
            out Color color)
        {
            switch (view.NegotiationStatus)
            {
                case ContractNegotiationStatus.NegotiationAvailable:
                    title = "계약 협상이 가능합니다.";
                    description = "오프시즌을 마감하면 실제 구단 오퍼를 비교할 수 있습니다.";
                    color = WarningColor;
                    return;
                case ContractNegotiationStatus.ExpiringThisSeason:
                    title = "이번 시즌 종료 후 계약이 만료됩니다.";
                    description = "남은 경기의 성적과 수상 결과가 다음 오퍼에 반영됩니다.";
                    color = WarningColor;
                    return;
                case ContractNegotiationStatus.CurrentTeamOfferAvailable:
                    title = "기존 구단이 우선 재계약을 제안했습니다.";
                    description = "안정성을 택하거나 오퍼 철회 위험을 감수하고 공개 시장을 확인할 수 있습니다.";
                    color = SuccessColor;
                    return;
                case ContractNegotiationStatus.ExtensionOfferAvailable:
                    title = "기존 구단이 시즌 중 연장 계약을 제안했습니다.";
                    description = "현재 역할의 안정성을 택하거나 시즌 종료 후 시장 가치 상승을 노릴 수 있습니다.";
                    color = SuccessColor;
                    return;
                default:
                    title = "현재 계약이 유효합니다.";
                    description = $"다음 협상은 {view.CurrentContract.EndYear} 시즌 결산 뒤 시작됩니다.";
                    color = SuccessColor;
                    return;
            }
        }

        private static string GetExpectedRoleLabel(ExpectedRole role)
        {
            return role switch
            {
                ExpectedRole.StartingCompetition => "주전 경쟁",
                ExpectedRole.RosterCompetition => "로스터 경쟁",
                _ => "백업 경쟁"
            };
        }

        private static string GetPositionCode(PlayerPosition position)
        {
            return position switch
            {
                PlayerPosition.Catcher => "C",
                PlayerPosition.FirstBase => "1B",
                PlayerPosition.SecondBase => "2B",
                PlayerPosition.ThirdBase => "3B",
                PlayerPosition.Shortstop => "SS",
                PlayerPosition.LeftField => "LF",
                PlayerPosition.CenterField => "CF",
                PlayerPosition.RightField => "RF",
                PlayerPosition.DesignatedHitter => "DH",
                PlayerPosition.StartingPitcher => "SP",
                PlayerPosition.ReliefPitcher => "RP",
                _ => "-"
            };
        }

        private static string GetLeagueLabel(LeagueLevel level)
        {
            return WorldGenerationConfiguration.GetDefaultDefinition(level).UiDisplayName;
        }

        private static string GetSeasonPhaseLabel(SeasonPhase phase)
        {
            return phase switch
            {
                SeasonPhase.Preseason => "프리시즌",
                SeasonPhase.RegularSeason => "정규 시즌",
                SeasonPhase.Postseason => "포스트시즌",
                SeasonPhase.SeasonReview => "시즌 결산",
                SeasonPhase.Offseason => "오프시즌",
                _ => "시즌 완료"
            };
        }

        private static string FormatMoney(long amount)
        {
            return amount >= 100_000_000L
                ? $"{amount / 100_000_000d:0.##}억원"
                : $"{amount / 10_000d:N0}만원";
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            var rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private static RectTransform CreateImage(
            string name,
            Transform parent,
            Color color,
            Vector2 size,
            Vector2 position,
            bool stretch = false)
        {
            RectTransform rect = CreateRect(name, parent, size, position);
            if (stretch)
                Stretch(rect);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private static Text CreateText(
            string name,
            Transform parent,
            string value,
            int fontSize,
            FontStyle style,
            TextAnchor alignment,
            Vector2 size,
            Vector2 position,
            Color color,
            bool stretch = false)
        {
            RectTransform rect = CreateRect(name, parent, size, position);
            if (stretch)
                Stretch(rect);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(
            string name,
            Transform parent,
            string label,
            Vector2 size,
            Vector2 position,
            Color color,
            out Text text)
        {
            RectTransform rect = CreateImage(name, parent, color, size, position);
            MarkVisual(rect, CareerUiVisualRole.InteractiveControl);
            Button button = rect.gameObject.AddComponent<Button>();
            rect.GetComponent<Image>().raycastTarget = true;
            ColorBlock colors = button.colors;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.12f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            text = CreateText(
                "Label", rect, label, 19, FontStyle.Bold, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.zero, PrimaryTextColor, stretch: true);
            return button;
        }

        private static void MarkVisual(
            RectTransform target,
            CareerUiVisualRole role,
            bool isHeroFrame = false)
        {
            CareerUiVisualElement visual = target.GetComponent<CareerUiVisualElement>();
            if (visual == null)
                visual = target.gameObject.AddComponent<CareerUiVisualElement>();
            visual.Initialize(role, isHeroFrame);
        }

        private static void AddTextOutline(Text text, Color color, float distance)
        {
            Outline outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(distance, -distance);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void ClearChildren(Transform parent)
        {
            for (int index = parent.childCount - 1; index >= 0; index--)
            {
                GameObject child = parent.GetChild(index).gameObject;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(child);
                else
#endif
                    Destroy(child);
            }
        }
    }
}
