using System;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Career;
using Baseball.Game.Manager;
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
        private static readonly Color BackgroundColor = new(0.004f, 0.015f, 0.028f, 1f);
        private static readonly Color TopBarColor = new(0.008f, 0.027f, 0.052f, 1f);
        private static readonly Color PanelColor = new(0.012f, 0.047f, 0.079f, 0.99f);
        private static readonly Color PanelDarkColor = new(0.006f, 0.028f, 0.049f, 1f);
        private static readonly Color CardColor = new(0.020f, 0.075f, 0.124f, 0.98f);
        private static readonly Color PortraitBackdropColor = new(0.78f, 0.86f, 0.94f, 1f);
        private static readonly Color BorderColor = new(0.28f, 0.46f, 0.62f, 1f);
        private static readonly Color DividerColor = new(0.11f, 0.27f, 0.40f, 1f);
        private static readonly Color AccentColor = new(0.08f, 0.52f, 0.92f, 1f);
        private static readonly Color BrightAccentColor = new(0.12f, 0.68f, 1f, 1f);
        private static readonly Color GoldColor = new(1f, 0.73f, 0.16f, 1f);
        private static readonly Color SuccessColor = new(0.27f, 0.82f, 0.36f, 1f);
        private static readonly Color WarningColor = new(0.98f, 0.62f, 0.10f, 1f);
        private static readonly Color ErrorColor = new(1f, 0.38f, 0.40f, 1f);
        private static readonly Color PrimaryTextColor = new(0.94f, 0.97f, 1f, 1f);
        private static readonly Color SecondaryTextColor = new(0.62f, 0.71f, 0.80f, 1f);
        private static readonly Color MutedColor = new(0.34f, 0.42f, 0.50f, 1f);

        private CareerManager _manager;
        private RectTransform _content;

        public override bool BlocksLowerInput => true;
        public CareerMainTab MainTab => CareerMainTab.Contract;

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
            _content = CreateRect("Content", root, new Vector2(1920f, 1080f), Vector2.zero);
        }

        private void HandleCareerChanged()
        {
            if (!_manager.HasActiveCareer)
            {
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
            RenderBackgroundAccents();
            RenderTopBar(view);
            RenderTitle(view);
            RenderPlayerPanel(view);
            if (view.NegotiationStatus is ContractNegotiationStatus.CurrentTeamOfferAvailable or
                ContractNegotiationStatus.OffersAvailable)
                RenderOfferMode(view);
            else
                RenderContractOverview(view);
            CareerTabBar.Create(_content, CareerMainTab.Contract);
        }

        private void RenderBackgroundAccents()
        {
            CreateImage("TopGlow", _content, new Color(0.02f, 0.18f, 0.31f, 0.22f),
                new Vector2(1920f, 5f), new Vector2(0f, 456f));
            CreateImage("BottomGlow", _content, new Color(0.02f, 0.16f, 0.28f, 0.18f),
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
                "LogoCaption", bar, "BASEBALL CAREER", 10, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(230f, 18f), new Vector2(-796f, -23f), AccentColor);
            CreateTopBarSegment(
                bar, "LEAGUE", $"{view.SeasonYear}  {GetLeagueLabel(view.LeagueLevel)} LEAGUE",
                new Vector2(-365f, 0f), new Vector2(420f, 64f));
            CreateTopBarSegment(
                bar, "SEASON", GetSeasonPhaseLabel(view.SeasonPhase),
                new Vector2(25f, 0f), new Vector2(300f, 64f));
            CreateTopBarSegment(
                bar, "MONEY", FormatMoney(view.AvailableMoney),
                new Vector2(390f, 0f), new Vector2(370f, 64f), GoldColor);
            CreateText(
                "Settings", bar, "설정", 14, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(100f, 44f), new Vector2(835f, 0f), SecondaryTextColor);
        }

        private static void CreateTopBarSegment(
            Transform parent,
            string eyebrow,
            string value,
            Vector2 position,
            Vector2 size,
            Color? valueColor = null)
        {
            RectTransform segment = CreateImage(
                eyebrow + "Segment", parent, new Color(0.02f, 0.07f, 0.12f, 0.76f), size, position);
            CreateText(
                "Eyebrow", segment, eyebrow, 9, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(size.x - 32f, 16f), new Vector2(0f, 16f), MutedColor);
            CreateText(
                "Value", segment, value, 18, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(size.x - 32f, 30f), new Vector2(0f, -7f), valueColor ?? PrimaryTextColor);
        }

        private void RenderTitle(CareerContractView view)
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
                "Title", _content, "계약", 30, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(70f, 48f), new Vector2(-910f, 440f), PrimaryTextColor);
            CreateText(
                "Subtitle", _content, subtitle,
                15, FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(subtitleWidth, 40f), new Vector2(-860f + subtitleWidth * 0.5f, 438f),
                SecondaryTextColor);
        }

        private void RenderPlayerPanel(CareerContractView view)
        {
            RectTransform panel = CreatePanel(
                "PlayerPanel", "MY PLAYER", "내 선수",
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
            RectTransform positionBadge = CreateImage(
                "PositionBadge", card, new Color(0.02f, 0.18f, 0.34f, 1f),
                new Vector2(78f, 48f), new Vector2(-113f, -94f));
            CreateText(
                "Position", positionBadge, GetPositionCode(view.Position), 20, FontStyle.Bold,
                TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, PrimaryTextColor, stretch: true);
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
                new Vector2(330f, 72f), new Vector2(0f, -238f),
                interactable ? new Color(0.025f, 0.31f, 0.61f, 1f) : new Color(0.05f, 0.10f, 0.15f, 1f),
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
                    new Vector2(200f, 34f), new Vector2(0f, -292f),
                    new Color(0.16f, 0.10f, 0.10f, 1f), out Text declineLabel);
                declineLabel.fontSize = 12;
                decline.onClick.AddListener(() => _manager.DeclineCurrentTeamExtension());
            }
            CreateText(
                "NegotiationGuide", panel, guide, 12, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(340f, 44f), new Vector2(0f, view.CanAcceptExtension ? -326f : -305f),
                interactable ? SecondaryTextColor : MutedColor);
        }

        private void RenderContractOverview(CareerContractView view)
        {
            RenderCurrentContract(view);
            RenderSalaryAndHistory(view);
            RenderBonusConditions(view);
            RenderMarketAndStatus(view);
        }

        private void RenderCurrentContract(CareerContractView view)
        {
            CurrentContractView contract = view.CurrentContract;
            RectTransform panel = CreatePanel(
                "CurrentContract", "CURRENT CONTRACT", "현재 계약",
                new Vector2(760f, 350f), new Vector2(-170f, 200f));
            CreateContractMetric(panel, "계약 기간",
                $"{contract.SignedYear} ~ {contract.EndYear}\n({contract.ContractYears}년)",
                new Vector2(-245f, 56f));
            CreateContractMetric(panel, "연봉", FormatMoney(contract.AnnualSalary),
                new Vector2(0f, 56f), GoldColor);
            CreateContractMetric(panel, "계약금", FormatMoney(contract.SigningBonus),
                new Vector2(245f, 56f));
            CreateContractMetric(panel, "총 보장 금액", FormatMoney(contract.GuaranteedValue),
                new Vector2(-245f, -82f));
            CreateContractMetric(panel, "이번 시즌 이후",
                $"{contract.RemainingSeasons}시즌", new Vector2(0f, -82f));
            CreateContractMetric(panel, "예상 역할", GetExpectedRoleLabel(contract.ExpectedRole),
                new Vector2(245f, -82f), SuccessColor);
        }

        private static void CreateContractMetric(
            Transform parent,
            string label,
            string value,
            Vector2 position,
            Color? valueColor = null)
        {
            RectTransform cell = CreateSection(
                "Metric_" + label, parent, new Vector2(236f, 126f), position, PanelDarkColor);
            CreateText(
                "Label", cell, label, 13, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(210f, 28f), new Vector2(0f, 37f), SecondaryTextColor);
            CreateText(
                "Value", cell, value, 23, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(216f, 82f), new Vector2(0f, -15f), valueColor ?? PrimaryTextColor);
        }

        private void RenderSalaryAndHistory(CareerContractView view)
        {
            RectTransform panel = CreatePanel(
                "SalaryHistory", "SALARY / HISTORY", "연봉 및 계약 이력",
                new Vector2(760f, 390f), new Vector2(-170f, -190f));
            CurrentContractView contract = view.CurrentContract;
            CreateText(
                "SalaryTitle", panel, "연도별 보장 연봉", 14, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Vector2(240f, 30f), new Vector2(-244f, 126f), SecondaryTextColor);
            int visibleSalaryYears = Math.Min(contract.ContractYears, 4);
            for (int index = 0; index < visibleSalaryYears; index++)
            {
                float y = 84f - index * 43f;
                int year = contract.SignedYear + index;
                RectTransform row = CreateImage(
                    "Salary_" + year, panel,
                    index % 2 == 0 ? PanelDarkColor : new Color(0.014f, 0.052f, 0.084f, 1f),
                    new Vector2(710f, 39f), new Vector2(0f, y));
                CreateText("Year", row, year.ToString(), 13, FontStyle.Bold, TextAnchor.MiddleCenter,
                    new Vector2(120f, 30f), new Vector2(-285f, 0f), PrimaryTextColor);
                CreateText("Salary", row, FormatMoney(contract.AnnualSalary), 14, FontStyle.Bold,
                    TextAnchor.MiddleRight, new Vector2(190f, 30f), new Vector2(-60f, 0f), GoldColor);
                CreateText("Note", row, index == 0 ? "계약 시작" : "보장",
                    12, FontStyle.Normal, TextAnchor.MiddleCenter,
                    new Vector2(150f, 30f), new Vector2(240f, 0f), SecondaryTextColor);
            }

            CreateText(
                "HistoryTitle", panel, "계약 히스토리", 14, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Vector2(240f, 30f), new Vector2(-244f, -100f), SecondaryTextColor);
            int visibleHistory = Math.Min(view.ContractHistory.Length, 2);
            for (int index = 0; index < visibleHistory; index++)
            {
                ContractHistoryView history = view.ContractHistory[index];
                float y = -137f - index * 39f;
                RectTransform row = CreateImage(
                    "History_" + index, panel,
                    history.IsCurrent ? new Color(0.025f, 0.12f, 0.20f, 1f) : PanelDarkColor,
                    new Vector2(710f, 37f), new Vector2(0f, y));
                CreateText("Term", row, $"{history.SignedYear}~{history.EndYear}", 12, FontStyle.Bold,
                    TextAnchor.MiddleCenter, new Vector2(120f, 30f), new Vector2(-290f, 0f), PrimaryTextColor);
                CreateText("Team", row, history.TeamName, 13, FontStyle.Bold, TextAnchor.MiddleLeft,
                    new Vector2(220f, 30f), new Vector2(-100f, 0f), PrimaryTextColor);
                CreateText("Salary", row, FormatMoney(history.AnnualSalary), 12, FontStyle.Normal,
                    TextAnchor.MiddleRight, new Vector2(160f, 30f), new Vector2(105f, 0f), SecondaryTextColor);
                CreateText("Role", row, GetExpectedRoleLabel(history.ExpectedRole), 12, FontStyle.Bold,
                    TextAnchor.MiddleCenter, new Vector2(130f, 30f), new Vector2(275f, 0f),
                    history.IsCurrent ? SuccessColor : SecondaryTextColor);
            }
        }

        private void RenderBonusConditions(CareerContractView view)
        {
            RectTransform panel = CreatePanel(
                "Bonus", "INCENTIVES", "상여 조건",
                new Vector2(650f, 440f), new Vector2(570f, 155f));
            int visibleCount = Math.Min(view.BonusProgress.Length, 6);
            for (int index = 0; index < visibleCount; index++)
            {
                ContractBonusProgressView bonus = view.BonusProgress[index];
                float y = 145f - index * 52f;
                RectTransform row = CreateImage(
                    "Bonus_" + bonus.ClauseId, panel,
                    index % 2 == 0 ? PanelDarkColor : new Color(0.014f, 0.052f, 0.084f, 1f),
                    new Vector2(610f, 47f), new Vector2(0f, y));
                CreateText(
                    "Condition", row, GetBonusLabel(bonus), 13, FontStyle.Bold, TextAnchor.MiddleLeft,
                    new Vector2(210f, 32f), new Vector2(-185f, 0f), PrimaryTextColor);
                CreateText(
                    "Reward", row, "+" + FormatMoney(bonus.Reward), 12, FontStyle.Bold,
                    TextAnchor.MiddleRight, new Vector2(120f, 30f), new Vector2(5f, 0f), GoldColor);
                CreateText(
                    "Progress", row, GetBonusProgressText(bonus), 11, FontStyle.Normal,
                    TextAnchor.MiddleRight, new Vector2(90f, 30f), new Vector2(145f, 0f),
                    bonus.IsCompleted ? SuccessColor : SecondaryTextColor);
                CreateProgressBar(
                    row, (float)bonus.NormalizedProgress, new Vector2(100f, 12f), new Vector2(250f, 0f),
                    bonus.IsCompleted ? SuccessColor :
                    bonus.NormalizedProgress >= 0.75d ? GoldColor : BrightAccentColor);
            }

            CreateImage("BonusFooter", panel, DividerColor, new Vector2(610f, 2f), new Vector2(0f, -154f));
            CreateText(
                "AchievedLabel", panel, "현재 달성 상여", 12, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(160f, 24f), new Vector2(-148f, -173f), SecondaryTextColor);
            CreateText(
                "Achieved", panel, FormatMoney(view.AchievedBonus), 20, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(190f, 32f), new Vector2(-148f, -201f), GoldColor);
            CreateText(
                "MaximumLabel", panel, "최대 수령 가능", 12, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(160f, 24f), new Vector2(148f, -173f), SecondaryTextColor);
            CreateText(
                "Maximum", panel, FormatMoney(view.MaximumBonus), 20, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(190f, 32f), new Vector2(148f, -201f), PrimaryTextColor);
        }

        private void RenderMarketAndStatus(CareerContractView view)
        {
            RectTransform panel = CreatePanel(
                "Market", "MARKET VALUE", "시장 가치 / 협상 정보",
                new Vector2(650f, 300f), new Vector2(570f, -245f));
            CreateMarketRow(panel, "FA 예상 연봉",
                view.MarketOfferCount > 0
                    ? $"{FormatMoney(view.MarketSalaryMinimum)} ~ {FormatMoney(view.MarketSalaryMaximum)}"
                    : "평가 자료 부족",
                82f, GoldColor);
            CreateMarketRow(panel, "예상 시장 역할", GetExpectedRoleLabel(view.MarketExpectedRole), 40f,
                SuccessColor);
            CreateMarketRow(panel, "현재 팀 포지션 필요도", $"{view.CurrentTeamPositionNeed} / 100", -2f);
            CreateMarketRow(panel, "예상 오퍼 구단", $"{view.MarketOfferCount}개 구단", -44f);

            GetStatusContent(view, out string statusTitle, out string statusDescription, out Color statusColor);
            RectTransform status = CreateSection(
                "Status", panel, new Vector2(610f, 80f), new Vector2(0f, -107f),
                new Color(0.01f, 0.06f, 0.10f, 1f));
            CreateImage("StatusAccent", status, statusColor, new Vector2(5f, 68f), new Vector2(-298f, 0f));
            CreateText("StatusTitle", status, statusTitle, 16, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(530f, 28f), new Vector2(12f, 16f), statusColor);
            CreateText("StatusDescription", status, statusDescription, 12, FontStyle.Normal,
                TextAnchor.MiddleLeft, new Vector2(530f, 36f), new Vector2(12f, -17f), SecondaryTextColor);
            if (!string.IsNullOrEmpty(view.LastError))
            {
                CreateText("Error", panel, view.LastError, 11, FontStyle.Normal, TextAnchor.MiddleCenter,
                    new Vector2(590f, 24f), new Vector2(0f, -164f), ErrorColor);
            }
        }

        private static void CreateMarketRow(
            Transform parent,
            string label,
            string value,
            float y,
            Color? valueColor = null)
        {
            RectTransform row = CreateImage(
                "Market_" + label, parent, PanelDarkColor, new Vector2(610f, 38f), new Vector2(0f, y));
            CreateText("Label", row, label, 12, FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(250f, 28f), new Vector2(-160f, 0f), SecondaryTextColor);
            CreateText("Value", row, value, 14, FontStyle.Bold, TextAnchor.MiddleRight,
                new Vector2(310f, 28f), new Vector2(130f, 0f), valueColor ?? PrimaryTextColor);
        }

        private void RenderOfferMode(CareerContractView view)
        {
            RectTransform panel = CreatePanel(
                "Offers", "CAREER MARKET",
                view.CanOpenMarket ? "기존 구단 우선 협상" : "계약 오퍼 비교",
                new Vector2(1410f, 700f), new Vector2(260f, 10f));
            CreateText(
                "Guide", panel,
                view.CanOpenMarket
                    ? "기존 구단의 제안을 먼저 검토하세요. 보류하면 오퍼가 철회될 위험을 감수하고 외부 시장을 확인합니다."
                    : "높은 연봉보다 실제 출장 기회가 더 좋은 계약일 수 있습니다. 예상 역할과 출장 비율을 함께 비교하세요.",
                14, FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(1320f, 38f), new Vector2(0f, 285f), SecondaryTextColor);

            int visibleCount = Math.Min(view.RenewalOffers.Length, 5);
            for (int index = 0; index < visibleCount; index++)
                RenderOfferRow(panel, view.RenewalOffers[index], index);

            float signX = view.CanOpenMarket ? -350f : 0f;
            Button signButton = CreateButton(
                "SignOffer", panel,
                view.CanSignSelectedOffer ? "선택한 구단과 계약" : "계약할 구단을 선택하세요",
                new Vector2(520f, 68f), new Vector2(signX, -302f),
                view.CanSignSelectedOffer
                    ? new Color(0.025f, 0.31f, 0.61f, 1f)
                    : new Color(0.05f, 0.10f, 0.15f, 1f),
                out Text label);
            label.fontSize = 22;
            signButton.interactable = view.CanSignSelectedOffer;
            if (view.CanSignSelectedOffer)
                signButton.onClick.AddListener(SignSelectedOffer);
            if (view.CanOpenMarket)
            {
                Button holdButton = CreateButton(
                    "HoldAndOpenMarket", panel, "보류하고 시장 보기",
                    new Vector2(330f, 68f), new Vector2(185f, -302f),
                    new Color(0.08f, 0.24f, 0.37f, 1f), out Text holdLabel);
                holdLabel.fontSize = 18;
                holdButton.onClick.AddListener(() => _manager.OpenContractMarket(true));
                Button declineButton = CreateButton(
                    "DeclineAndOpenMarket", panel, "거절하고 시장 보기",
                    new Vector2(330f, 68f), new Vector2(535f, -302f),
                    new Color(0.18f, 0.12f, 0.12f, 1f), out Text declineLabel);
                declineLabel.fontSize = 18;
                declineButton.onClick.AddListener(() => _manager.OpenContractMarket(false));
            }
            if (!string.IsNullOrEmpty(view.LastError))
            {
                CreateText("Error", panel, view.LastError, 12, FontStyle.Normal, TextAnchor.MiddleCenter,
                    new Vector2(900f, 24f), new Vector2(0f, -338f), ErrorColor);
            }
        }

        private void SignSelectedOffer()
        {
            if (_manager.SignSelectedContractOffer())
                CareerTabNavigation.Show(CareerMainTab.Home);
        }

        private void RenderOfferRow(Transform parent, RenewalContractOfferView offer, int index)
        {
            float y = 220f - index * 99f;
            Color teamColor = ToColor(offer.TeamColor);
            Color background = offer.IsSelected
                ? new Color(0.025f, 0.14f, 0.23f, 1f)
                : index % 2 == 0 ? PanelDarkColor : new Color(0.014f, 0.052f, 0.084f, 1f);
            RectTransform row = CreateImage(
                "Offer_" + offer.TeamId, parent, background,
                new Vector2(1360f, 88f), new Vector2(0f, y));
            Image rowImage = row.GetComponent<Image>();
            rowImage.raycastTarget = true;
            Button button = row.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => _manager.SelectContractOffer(offer.TeamId));
            CreateImage("TeamColor", row, teamColor, new Vector2(7f, 76f), new Vector2(-671f, 0f));
            CreateText("Team", row,
                $"{GetOfferChannelLabel(offer.Channel)} [{GetLeagueLevelLabel(offer.LeagueLevel)}]  {offer.TeamName}", 18,
                FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(260f, 38f), new Vector2(-520f, 13f), PrimaryTextColor);
            CreateText("Term", row, $"{offer.ContractYears}년 / 보장 {FormatMoney(offer.GuaranteedValue)}",
                11, FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(280f, 30f), new Vector2(-510f, -20f), SecondaryTextColor);
            CreateOfferMetric(row, "연봉", FormatMoney(offer.AnnualSalary), -230f, GoldColor);
            CreateOfferMetric(row, "계약금", FormatMoney(offer.SigningBonus), 5f, PrimaryTextColor);
            CreateOfferMetric(row, "예상 역할", GetExpectedRoleLabel(offer.ExpectedRole), 240f, SuccessColor);
            CreateOfferMetric(row, "예상 출장", $"{offer.EstimatedPlayingTime:P0}", 445f, PrimaryTextColor);
            CreateOfferMetric(row, "필요 / 육성", $"{offer.PositionNeed} / {offer.DevelopmentRating}", 600f,
                PrimaryTextColor);
            if (offer.IsSelected)
            {
                CreateImage("Selected", row, BrightAccentColor, new Vector2(4f, 76f), new Vector2(671f, 0f));
                CreateText("SelectedLabel", row, "선택", 10, FontStyle.Bold, TextAnchor.MiddleRight,
                    new Vector2(70f, 24f), new Vector2(630f, -29f), BrightAccentColor);
            }
        }

        private static void CreateOfferMetric(
            Transform parent,
            string label,
            string value,
            float x,
            Color valueColor)
        {
            CreateText(label + "Label", parent, label, 10, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(150f, 22f), new Vector2(x, 19f), MutedColor);
            CreateText(label + "Value", parent, value, 14, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(180f, 30f), new Vector2(x, -12f), valueColor);
        }

        private static void CreateInfoRow(
            Transform parent,
            string label,
            string value,
            float y,
            Color? valueColor = null)
        {
            RectTransform row = CreateImage(
                "Info_" + label, parent, PanelDarkColor, new Vector2(344f, 42f), new Vector2(0f, y));
            CreateText("Label", row, label, 12, FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(145f, 30f), new Vector2(-90f, 0f), SecondaryTextColor);
            CreateText("Value", row, value, 13, FontStyle.Bold, TextAnchor.MiddleRight,
                new Vector2(175f, 30f), new Vector2(75f, 0f), valueColor ?? PrimaryTextColor);
        }

        private RectTransform CreatePanel(
            string name,
            string eyebrow,
            string title,
            Vector2 size,
            Vector2 position)
        {
            CreateImage(
                name + "Shadow", _content, new Color(0f, 0f, 0f, 0.68f),
                size + new Vector2(8f, 8f), position + new Vector2(4f, -5f));
            RectTransform panel = CreateImage(name, _content, BorderColor, size, position);
            RectTransform surface = CreateImage(
                "Surface", panel, PanelColor, Vector2.zero, Vector2.zero, stretch: true);
            surface.offsetMin = new Vector2(3f, 3f);
            surface.offsetMax = new Vector2(-3f, -3f);
            RectTransform header = CreateImage(
                "Header", panel, new Color(0.024f, 0.11f, 0.19f, 1f),
                new Vector2(size.x - 8f, 50f), new Vector2(0f, size.y * 0.5f - 29f));
            CreateImage(
                "HeaderLine", header, AccentColor, new Vector2(size.x * 0.34f, 2f),
                new Vector2(-size.x * 0.29f, -23f));
            CreateText(
                "Eyebrow", header, eyebrow, 9, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(size.x * 0.3f, 18f), new Vector2(-size.x * 0.33f, 11f), AccentColor);
            CreateText(
                "Heading", header, title, 20, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(size.x * 0.62f, 36f), new Vector2(0f, -1f), PrimaryTextColor);
            return panel;
        }

        private static RectTransform CreateSection(
            string name,
            Transform parent,
            Vector2 size,
            Vector2 position,
            Color color)
        {
            RectTransform frame = CreateImage(name, parent, DividerColor, size, position);
            RectTransform surface = CreateImage(
                "Surface", frame, color, Vector2.zero, Vector2.zero, stretch: true);
            surface.offsetMin = new Vector2(2f, 2f);
            surface.offsetMax = new Vector2(-2f, -2f);
            return frame;
        }

        private static void CreateProgressBar(
            Transform parent,
            float normalizedValue,
            Vector2 size,
            Vector2 position,
            Color fillColor)
        {
            float clamped = Mathf.Clamp01(normalizedValue);
            RectTransform track = CreateImage(
                "Track", parent, new Color(0.09f, 0.14f, 0.18f, 1f), size, position);
            float fillWidth = Mathf.Max(2f, (size.x - 4f) * clamped);
            RectTransform fill = CreateImage(
                "Fill", track, fillColor, new Vector2(fillWidth, size.y - 4f), Vector2.zero);
            fill.anchorMin = fill.anchorMax = new Vector2(0f, 0.5f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.anchoredPosition = new Vector2(2f, 0f);
        }

        private static string GetBonusLabel(ContractBonusProgressView bonus)
        {
            return bonus.Metric switch
            {
                ContractBonusMetric.GamesPlayed => $"{bonus.TargetValue:0}경기 출장",
                ContractBonusMetric.HomeRuns => $"홈런 {bonus.TargetValue:0}개",
                ContractBonusMetric.RunsBattedIn => $"타점 {bonus.TargetValue:0}개",
                ContractBonusMetric.OnBasePlusSlugging => $"OPS {bonus.TargetValue:.000}",
                ContractBonusMetric.PitchingAppearances => $"{bonus.TargetValue:0}경기 등판",
                ContractBonusMetric.PitchingOuts => $"{FormatInnings((int)bonus.TargetValue)}이닝",
                ContractBonusMetric.PitchingStrikeouts => $"탈삼진 {bonus.TargetValue:0}개",
                ContractBonusMetric.EarnedRunAverage => $"ERA {bonus.TargetValue:0.00} 이하",
                ContractBonusMetric.IndividualAward => "개인상 수상",
                ContractBonusMetric.Championship => "리그 우승",
                _ => bonus.ClauseId
            };
        }

        private static string GetBonusProgressText(ContractBonusProgressView bonus)
        {
            if (bonus.IsCompleted)
                return "달성";
            return bonus.Metric switch
            {
                ContractBonusMetric.OnBasePlusSlugging => $"{bonus.CurrentValue:.000} / {bonus.TargetValue:.000}",
                ContractBonusMetric.EarnedRunAverage => bonus.HasSample
                    ? $"{bonus.CurrentValue:0.00} / {bonus.TargetValue:0.00}"
                    : "미등판",
                ContractBonusMetric.PitchingOuts =>
                    $"{FormatInnings((int)bonus.CurrentValue)} / {FormatInnings((int)bonus.TargetValue)}",
                ContractBonusMetric.IndividualAward or ContractBonusMetric.Championship => "미달성",
                _ => $"{bonus.CurrentValue:0} / {bonus.TargetValue:0}"
            };
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

        private static string GetOfferChannelLabel(ContractOfferChannel channel)
        {
            return channel switch
            {
                ContractOfferChannel.CurrentTeamRenewal => "[기존 구단]",
                ContractOfferChannel.CurrentTeamExtension => "[연장 계약]",
                ContractOfferChannel.Promotion => "[상위 리그]",
                ContractOfferChannel.Rehabilitation => "[재기 계약]",
                ContractOfferChannel.DevelopmentFallback => "[육성 계약]",
                _ => "[공개 시장]"
            };
        }

        private static string GetLeagueLevelLabel(LeagueLevel level)
        {
            return level switch
            {
                LeagueLevel.Rookie => "루키",
                LeagueLevel.Minor => "마이너",
                LeagueLevel.Major => "메이저",
                _ => level.ToString()
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
            return level switch
            {
                LeagueLevel.Rookie => "ROOKIE",
                LeagueLevel.Minor => "MINOR",
                LeagueLevel.Major => "MAJOR",
                _ => "ROOKIE"
            };
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

        private static Color ToColor(TeamColor color)
        {
            return new Color32(color.Red, color.Green, color.Blue, 255);
        }

        private static string FormatMoney(long amount)
        {
            return amount >= 100_000_000L
                ? $"{amount / 100_000_000d:0.##}억원"
                : $"{amount / 10_000d:N0}만원";
        }

        private static string FormatInnings(int outs)
        {
            return $"{outs / 3}.{outs % 3}";
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
