using System;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Career;
using Baseball.Game.Career.News;
using Baseball.Game.Manager;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Baseball.Presentation.Career
{
    /// <summary>
    /// 다음 경기와 내 선수의 역할·기록을 중심으로 정규 시즌을 진행하는 메인 화면이다.
    /// </summary>
    public sealed class UI_Scene_CareerDashboard : UISceneBase, ICareerTabScreen
    {
        private static readonly Color BackgroundColor = new(0.006f, 0.02f, 0.034f, 1f);
        private static readonly Color TopBarColor = new(0.008f, 0.027f, 0.052f, 1f);
        private static readonly Color PanelColor = new(0.018f, 0.065f, 0.108f, 0.99f);
        private static readonly Color PanelDarkColor = new(0.009f, 0.035f, 0.061f, 0.99f);
        private static readonly Color CardColor = new(0.024f, 0.086f, 0.139f, 0.97f);
        private static readonly Color PortraitBackdropColor = new(0.78f, 0.86f, 0.94f, 1f);
        private static readonly Color BorderColor = new(0.28f, 0.46f, 0.62f, 1f);
        private static readonly Color DividerColor = new(0.14f, 0.31f, 0.45f, 1f);
        private static readonly Color AccentColor = new(0.13f, 0.55f, 0.92f, 1f);
        private static readonly Color BrightAccentColor = new(0.12f, 0.67f, 1f, 1f);
        private static readonly Color RoleColor = new(0.27f, 0.77f, 0.47f, 1f);
        private static readonly Color GoldColor = new(0.95f, 0.69f, 0.22f, 1f);
        private static readonly Color WarningColor = new(0.94f, 0.56f, 0.16f, 1f);
        private static readonly Color LossColor = new(0.82f, 0.27f, 0.31f, 1f);
        private static readonly Color PrimaryTextColor = new(0.94f, 0.97f, 1f, 1f);
        private static readonly Color SecondaryTextColor = new(0.62f, 0.71f, 0.8f, 1f);
        private static readonly Color MutedColor = new(0.34f, 0.40f, 0.49f, 1f);
        private static readonly Color ErrorColor = new(1f, 0.42f, 0.42f, 1f);

        private CareerManager _manager;
        private RectTransform _content;
        private bool _isSeasonAutoCompletionConfirmationVisible;

        public override bool BlocksLowerInput => true;
        public CareerMainTab MainTab => CareerMainTab.Home;

        /// <summary>
        /// 프리팹이 없는 프로토타입 환경에서 대시보드를 런타임 생성한다.
        /// </summary>
        public static UI_Scene_CareerDashboard CreateRuntime(Transform parent)
        {
            var screenObject = new GameObject(
                nameof(UI_Scene_CareerDashboard),
                typeof(RectTransform),
                typeof(CanvasGroup));
            screenObject.transform.SetParent(parent, false);
            UI_Scene_CareerDashboard screen = screenObject.AddComponent<UI_Scene_CareerDashboard>();
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
            _isSeasonAutoCompletionConfirmationVisible = false;
            Render();
        }

        private void Update()
        {
            if (!IsVisible || _manager == null || !_manager.HasActiveCareer || Keyboard.current == null)
                return;

            Keyboard keyboard = Keyboard.current;
            if (_isSeasonAutoCompletionConfirmationVisible)
            {
                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    _isSeasonAutoCompletionConfirmationVisible = false;
                    Render();
                }
                else if (IsConfirmKeyPressed(keyboard))
                {
                    ConfirmSeasonAutoCompletion();
                }
                return;
            }

            if (_manager.HasActiveMatch)
                return;
            if (keyboard.sKey.wasPressedThisFrame && CanAutoCompleteCurrentPhase(_manager.Dashboard))
                ShowSeasonAutoCompletionConfirmation();
            else if (IsConfirmKeyPressed(keyboard))
                AdvancePrimaryAction(_manager.Dashboard);
        }

        private void AdvancePrimaryAction(CareerDashboardView view)
        {
            if (view.NextGame.HasValue)
            {
                _manager.PrepareNextGame();
                return;
            }

            switch (view.SeasonPhase)
            {
                case SeasonPhase.Postseason:
                    if (view.SeasonProgress.CanPlayNextPostseasonGame)
                        _manager.PrepareNextGame();
                    else
                        ShowSeasonAutoCompletionConfirmation();
                    break;
                case SeasonPhase.SeasonReview:
                    _manager.SettleSeasonAndBeginOffseason();
                    break;
                case SeasonPhase.Offseason:
                    CareerTabNavigation.Show(
                        view.SeasonProgress.RequiresContractDecision
                            ? CareerMainTab.Contract
                            : CareerMainTab.Growth);
                    break;
            }
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

            UI_Scene_NewGame newGameScreen = FindFirstObjectByType<UI_Scene_NewGame>(FindObjectsInactive.Include);
            if (newGameScreen != null && newGameScreen.IsVisible)
            {
                newGameScreen.Hide();
                CareerTabNavigation.Show(CareerMainTab.Home);
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
            CareerDashboardView view = _manager.Dashboard;
            RenderBackgroundAccents();
            RenderTopBar(view);
            RenderPlayerPanel(view);
            RenderNextGame(view);
            RenderSeasonPanel(view);
            RenderCompetition(view);
            RenderEventFeed(view);
            RenderUpcoming(view);
            RenderTabs();
            if (_isSeasonAutoCompletionConfirmationVisible)
                RenderSeasonAutoCompletionConfirmation(view);
        }

        private void RenderBackgroundAccents()
        {
            CreateImage("TopGlow", _content, new Color(0.02f, 0.18f, 0.31f, 0.24f),
                new Vector2(1920f, 5f), new Vector2(0f, 456f));
            CreateImage("BottomGlow", _content, new Color(0.02f, 0.16f, 0.28f, 0.2f),
                new Vector2(1920f, 4f), new Vector2(0f, -443f));
        }

        private void RenderTopBar(CareerDashboardView view)
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
            string dateText = GetSeasonDateText(view);
            CreateTopBarSegment(bar, "DATE", dateText, new Vector2(25f, 0f), new Vector2(300f, 64f));
            CreateTopBarSegment(
                bar, "MONEY", FormatMoney(view.AvailableMoney), new Vector2(390f, 0f), new Vector2(370f, 64f));
            CreateText(
                "Mail", bar, "MAIL", 12, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(80f, 44f), new Vector2(755f, 0f), SecondaryTextColor);
            CreateText(
                "Settings", bar, "설정", 14, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(80f, 44f), new Vector2(855f, 0f), SecondaryTextColor);
        }

        private static void CreateTopBarSegment(
            Transform parent,
            string eyebrow,
            string value,
            Vector2 position,
            Vector2 size)
        {
            RectTransform segment = CreateImage(
                eyebrow + "Segment", parent, new Color(0.02f, 0.07f, 0.12f, 0.76f), size, position);
            CreateImage(
                "LeftDivider", segment, DividerColor, new Vector2(2f, size.y - 10f),
                new Vector2(-size.x * 0.5f + 1f, 0f));
            CreateText(
                "Eyebrow", segment, eyebrow, 10, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(size.x - 42f, 18f), new Vector2(14f, 15f), AccentColor);
            CreateText(
                "Value", segment, value, 20, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(size.x - 42f, 31f), new Vector2(14f, -8f), PrimaryTextColor);
        }

        private void RenderPlayerPanel(CareerDashboardView view)
        {
            RectTransform panel = CreatePanel(
                "PlayerPanel", "MY PLAYER", "내 선수", new Vector2(480f, 612f), new Vector2(-700f, 143f));
            RenderPlayerCard(panel, view);
            RenderPlayerAttributes(panel, view);
            RenderPlayerStatus(panel, view);
        }

        private static void RenderPlayerCard(RectTransform panel, CareerDashboardView view)
        {
            RectTransform card = CreateSection(
                "PlayerCard", panel, new Vector2(446f, 238f), new Vector2(0f, 105f),
                new Color(0.025f, 0.16f, 0.27f, 1f));
            CreateImage("CardStripe", card, AccentColor, new Vector2(8f, 224f), new Vector2(-215f, 0f));
            CreateImage(
                "CardGlow", card, new Color(0.08f, 0.34f, 0.58f, 0.42f),
                new Vector2(76f, 142f), new Vector2(-174f, 25f));
            CreateText(
                "OverallLabel", card, "OVR", 13, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(64f, 22f), new Vector2(-174f, 86f), SecondaryTextColor);
            Text overall = CreateText(
                "Overall", card, view.Overall.ToString(), 46, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(74f, 58f), new Vector2(-174f, 52f), PrimaryTextColor);
            AddTextOutline(overall, AccentColor, 1.2f);

            CreateImage(
                "PortraitBackdrop", card, PortraitBackdropColor,
                new Vector2(210f, 142f), new Vector2(-30f, 25f));
            RectTransform portrait = CreateImage(
                "PlayerPortrait", card, Color.white, new Vector2(210f, 142f), new Vector2(-30f, 25f));
            Image portraitImage = portrait.GetComponent<Image>();
            portraitImage.sprite = PlayerPortraitSprites.GetDefault(view.Position);
            portraitImage.preserveAspect = true;
            CreateTeamBadge(card, view.TeamName, new Vector2(150f, 65f));

            CreateImage(
                "NameStrip", card, new Color(0.004f, 0.025f, 0.048f, 0.94f),
                new Vector2(434f, 53f), new Vector2(0f, -89f));
            CreateText(
                "Position", card, GetPositionCode(view.Position), 20, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(66f, 38f), new Vector2(-175f, -89f), PrimaryTextColor);
            CreateText(
                "PlayerName", card, view.PlayerName, 26, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(250f, 42f), new Vector2(15f, -89f), PrimaryTextColor);

            CreateText(
                "TeamName", panel, view.TeamName, 15, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(230f, 24f), new Vector2(-105f, -29f), SecondaryTextColor);
            CreateText(
                "Profile", panel, $"{GetPositionCode(view.Position)}  ·  {view.Age}세  ·  {GetHandsLabel(view)}",
                15, FontStyle.Normal, TextAnchor.MiddleRight,
                new Vector2(220f, 24f), new Vector2(105f, -29f), SecondaryTextColor);
        }

        private static void RenderPlayerAttributes(RectTransform panel, CareerDashboardView view)
        {
            bool isPitcher = view.Position is PlayerPosition.StartingPitcher or PlayerPosition.ReliefPitcher;
            string[] labels;
            int[] values;
            if (isPitcher)
            {
                labels = new[] { "체력", "구속", "구위", "변화구", "제구력", "정신력" };
                values = new[]
                {
                    view.PitcherAttributes.Stamina,
                    view.PitcherAttributes.Velocity,
                    view.PitcherAttributes.Stuff,
                    view.PitcherAttributes.Breaking,
                    view.PitcherAttributes.Control,
                    view.PitcherAttributes.Mental
                };
            }
            else
            {
                labels = new[] { "교타력", "장타력", "주력", "번트", "수비력", "정신력" };
                values = new[]
                {
                    view.BatterAttributes.Contact,
                    view.BatterAttributes.Power,
                    view.BatterAttributes.Speed,
                    view.BatterAttributes.Bunt,
                    view.BatterAttributes.Defense,
                    view.BatterAttributes.Mental
                };
            }

            for (int index = 0; index < values.Length; index++)
                CreateAttributeBar(panel, labels[index], values[index], new Vector2(0f, -72f - index * 30f));
        }

        private static void RenderPlayerStatus(RectTransform panel, CareerDashboardView view)
        {
            RectTransform condition = CreateSection(
                "Condition", panel, new Vector2(215f, 62f), new Vector2(-112f, -270f), PanelDarkColor);
            CreateText(
                "Label", condition, "컨디션", 12, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(86f, 21f), new Vector2(-54f, 15f), SecondaryTextColor);
            CreateText(
                "Value", condition, view.Condition.ToString(), 25, FontStyle.Bold, TextAnchor.MiddleRight,
                new Vector2(70f, 34f), new Vector2(59f, -3f), GetRatingColor(view.Condition));
            CreateProgressBar(
                condition, view.Condition / 100f, new Vector2(112f, 8f), new Vector2(-28f, -17f),
                GetRatingColor(view.Condition));

            RectTransform evaluation = CreateSection(
                "Evaluation", panel, new Vector2(215f, 62f), new Vector2(112f, -270f), PanelDarkColor);
            CreateText(
                "Label", evaluation, "감독 평가", 12, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(96f, 21f), new Vector2(-49f, 15f), SecondaryTextColor);
            CreateText(
                "Value", evaluation, view.ManagerEvaluation.ToString(), 25, FontStyle.Bold,
                TextAnchor.MiddleRight, new Vector2(70f, 34f), new Vector2(59f, -3f),
                GetRatingColor(view.ManagerEvaluation));
            CreateProgressBar(
                evaluation, view.ManagerEvaluation / 100f, new Vector2(112f, 8f),
                new Vector2(-28f, -17f), GetRatingColor(view.ManagerEvaluation));
        }

        private static void CreateAttributeBar(Transform parent, string label, int value, Vector2 position)
        {
            CreateText(
                label, parent, label, 14, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(72f, 24f), new Vector2(-174f, position.y), SecondaryTextColor);
            CreateProgressBar(
                parent, value / 100f, new Vector2(258f, 11f), new Vector2(12f, position.y),
                GetRatingColor(value));
            CreateText(
                label + "Value", parent, value.ToString(), 15, FontStyle.Bold, TextAnchor.MiddleRight,
                new Vector2(42f, 24f), new Vector2(194f, position.y), GetRatingColor(value));
        }

        private void RenderNextGame(CareerDashboardView view)
        {
            RectTransform panel = CreatePanel(
                "NextGamePanel", "NEXT GAME", "다음 경기", new Vector2(760f, 560f), new Vector2(-64f, 168f));
            if (!view.NextGame.HasValue)
            {
                RenderSeasonTransition(panel, view);
                return;
            }

            NextCareerGameView game = view.NextGame.Value;
            CreateTeamBadge(panel, game.AwayTeamName, new Vector2(-220f, 112f), 128f);
            CreateTeamBadge(panel, game.HomeTeamName, new Vector2(220f, 112f), 128f);
            CreateText(
                "AwayTeam", panel, game.AwayTeamName, 21, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(260f, 35f), new Vector2(-220f, 28f), PrimaryTextColor);
            CreateText(
                "HomeTeam", panel, game.HomeTeamName, 21, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(260f, 35f), new Vector2(220f, 28f), PrimaryTextColor);
            Text versus = CreateText(
                "Versus", panel, "VS", 48, FontStyle.BoldAndItalic, TextAnchor.MiddleCenter,
                new Vector2(110f, 72f), new Vector2(0f, 107f), PrimaryTextColor);
            AddTextOutline(versus, AccentColor, 1.6f);
            CreateText(
                "VenueType", panel, game.IsHome ? "HOME" : "AWAY", 13, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(100f, 25f), new Vector2(0f, 58f), AccentColor);

            CreateInfoChip(panel, "경기일", game.Date.ToString("M월 d일"), new Vector2(-218f, -25f));
            CreateInfoChip(panel, "구분", game.IsHome ? "홈 경기" : "원정 경기", new Vector2(0f, -25f));
            CreateInfoChip(
                panel, "시즌", $"{view.Statistics.TeamGames + 1}번째 경기", new Vector2(218f, -25f));

            RectTransform role = CreateSection(
                "PlannedRoleBand", panel, new Vector2(650f, 64f), new Vector2(0f, -99f),
                new Color(0.025f, 0.13f, 0.2f, 1f));
            CreateText(
                "RoleLabel", role, "예상 역할", 14, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(130f, 30f), new Vector2(-225f, 0f), SecondaryTextColor);
            CreateText(
                "PlannedRole", role, GetRoleLabel(game.PlannedRole, view.Position), 25,
                FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(285f, 42f), Vector2.zero, RoleColor);
            CreateText(
                "RoleGuide", role, GetRoleGuide(game.PlannedRole, view.Position), 13, FontStyle.Normal,
                TextAnchor.MiddleRight, new Vector2(180f, 30f), new Vector2(218f, 0f), SecondaryTextColor);

            RectTransform buttonFrame = CreateImage(
                "AdvanceFrame", panel, BorderColor, new Vector2(442f, 86f), new Vector2(-105f, -205f));
            Button advance = CreateButton(
                "AdvanceGame", buttonFrame, $"{GetAdvanceButtonLabel(game.PlannedRole)}   SPACE",
                new Vector2(432f, 76f), Vector2.zero, new Color(0.025f, 0.31f, 0.61f, 1f), out Text label);
            label.fontSize = 27;
            AddTextOutline(label, new Color(0.02f, 0.16f, 0.34f, 1f), 1.5f);
            CreateImage(
                "ButtonGlow", buttonFrame, BrightAccentColor, new Vector2(330f, 3f), new Vector2(0f, -38f));
            advance.onClick.AddListener(() =>
            {
                advance.interactable = false;
                _manager.PrepareNextGame();
            });
            Button autoSeason = CreateButton(
                "AutoCompleteRegularSeason", panel, "정규시즌 자동 완료\nS",
                new Vector2(190f, 82f), new Vector2(235f, -205f),
                new Color(0.12f, 0.16f, 0.2f, 1f), out Text autoSeasonLabel);
            autoSeasonLabel.fontSize = 17;
            autoSeasonLabel.color = GoldColor;
            autoSeason.onClick.AddListener(ShowSeasonAutoCompletionConfirmation);
            if (!string.IsNullOrEmpty(_manager.LastError))
            {
                CreateText(
                    "Error", panel, _manager.LastError, 15, FontStyle.Normal, TextAnchor.MiddleCenter,
                    new Vector2(680f, 25f), new Vector2(0f, -257f), ErrorColor);
            }
        }

        private void RenderSeasonTransition(RectTransform panel, CareerDashboardView view)
        {
            switch (view.SeasonPhase)
            {
                case SeasonPhase.Postseason:
                    RenderPostseasonTransition(panel, view);
                    return;
                case SeasonPhase.SeasonReview:
                    RenderSeasonReviewTransition(panel, view);
                    return;
                case SeasonPhase.Offseason:
                    RenderOffseasonTransition(panel, view);
                    return;
                default:
                    RenderUnavailableSeasonTransition(panel);
                    return;
            }
        }

        private void RenderPostseasonTransition(RectTransform panel, CareerDashboardView view)
        {
            bool qualified = view.SeasonProgress.IsPlayerTeamPostseasonQualified;
            bool canPlay = view.SeasonProgress.CanPlayNextPostseasonGame;
            RenderSeasonTransitionHeading(
                panel,
                "POST-SEASON",
                canPlay ? "포스트시즌 진출" : qualified ? "포스트시즌 탈락" : "포스트시즌 관전",
                canPlay
                    ? $"{view.TeamName}의 우승 도전이 진행 중입니다.\n내 구단 경기는 직접 진행하며 포스트시즌 기록으로 별도 집계됩니다."
                    : qualified
                        ? $"{view.TeamName}의 도전은 끝났습니다.\n남은 대진을 진행해 우승 구단을 확인하세요."
                        : $"{view.TeamName}은 진출하지 못했습니다.\n상위 4개 구단의 우승 경쟁 결과를 확인하세요.",
                canPlay ? GoldColor : SecondaryTextColor);

            if (!canPlay)
            {
                Button complete = CreateSeasonTransitionButton(
                    panel,
                    "AutoCompletePostseason",
                    "포스트시즌 자동 완료",
                    new Color(0.42f, 0.25f, 0.04f, 1f));
                complete.onClick.AddListener(ShowSeasonAutoCompletionConfirmation);
                return;
            }

            Button advance = CreateButton(
                "AdvancePostseason", panel, "다음 포스트시즌 경기   ENTER",
                new Vector2(430f, 68f), new Vector2(-105f, -122f),
                new Color(0.42f, 0.25f, 0.04f, 1f), out Text advanceLabel);
            advanceLabel.fontSize = 21;
            advance.onClick.AddListener(() =>
            {
                advance.interactable = false;
                _manager.PrepareNextGame();
            });

            Button autoComplete = CreateButton(
                "AutoCompletePostseason", panel, "포스트시즌\n자동 완료   S",
                new Vector2(190f, 68f), new Vector2(235f, -122f),
                new Color(0.12f, 0.16f, 0.2f, 1f), out Text autoCompleteLabel);
            autoCompleteLabel.fontSize = 16;
            autoCompleteLabel.color = GoldColor;
            autoComplete.onClick.AddListener(ShowSeasonAutoCompletionConfirmation);
        }

        private void RenderSeasonReviewTransition(RectTransform panel, CareerDashboardView view)
        {
            string champion = string.IsNullOrEmpty(view.SeasonProgress.ChampionTeamName)
                ? "우승 구단 집계 완료"
                : $"우승 · {view.SeasonProgress.ChampionTeamName}";
            string result = GetPostseasonResultLabel(view.SeasonProgress.PlayerTeamPostseasonResult);
            string autoSummary = view.LastSeasonAutoCompletion.HasValue &&
                                 view.LastSeasonAutoCompletion.Value.CompletedPhase == SeasonPhase.Postseason
                ? $"포스트시즌 {view.LastSeasonAutoCompletion.Value.PostseasonGames}경기 자동 진행 · "
                : string.Empty;
            RenderSeasonTransitionHeading(
                panel,
                "SEASON REVIEW",
                "시즌 결산",
                $"{champion}\n{autoSummary}{result} · 개인 수상 {view.SeasonProgress.PlayerAwardCount}개",
                GoldColor);

            Button settle = CreateSeasonTransitionButton(
                panel,
                "BeginOffseason",
                "성장·수입 결산",
                new Color(0.08f, 0.34f, 0.28f, 1f));
            settle.onClick.AddListener(() =>
            {
                settle.interactable = false;
                _manager.SettleSeasonAndBeginOffseason();
            });
        }

        private void RenderOffseasonTransition(RectTransform panel, CareerDashboardView view)
        {
            CareerSeasonProgressView progress = view.SeasonProgress;
            string income = $"연봉 +{FormatMoney(progress.SalaryIncome)} · 상여 +{FormatMoney(progress.BonusIncome)}";
            string description = progress.RequiresContractDecision
                ? $"{income}\n다음 시즌을 시작하려면 새 계약을 선택해야 합니다."
                : $"{income}\n남은 {progress.OffseasonRemainingWeeks}주 동안 성장 방향을 결정하세요.";
            RenderSeasonTransitionHeading(
                panel,
                "OFF-SEASON",
                progress.RequiresContractDecision ? "계약 결정 필요" : "오프시즌 시작",
                description,
                progress.RequiresContractDecision ? WarningColor : RoleColor);

            string buttonName = progress.RequiresContractDecision ? "OpenContract" : "OpenGrowth";
            string buttonLabel = progress.RequiresContractDecision ? "계약 오퍼 확인" : "성장 계획 열기";
            Button open = CreateSeasonTransitionButton(
                panel,
                buttonName,
                buttonLabel,
                new Color(0.025f, 0.31f, 0.61f, 1f));
            open.onClick.AddListener(() => CareerTabNavigation.Show(
                progress.RequiresContractDecision ? CareerMainTab.Contract : CareerMainTab.Growth));
        }

        private static void RenderUnavailableSeasonTransition(RectTransform panel)
        {
            RenderSeasonTransitionHeading(
                panel,
                "SEASON COMPLETE",
                "시즌 일정 완료",
                "현재 시즌 상태를 확인하고 다시 시도하세요.",
                SecondaryTextColor);
        }

        private static void RenderSeasonTransitionHeading(
            Transform panel,
            string eyebrow,
            string title,
            string description,
            Color accent)
        {
            CreateText(
                "SeasonTransitionLabel", panel, eyebrow, 15, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(440f, 30f), new Vector2(0f, 128f), AccentColor);
            Text complete = CreateText(
                "SeasonTransitionTitle", panel, title, 38, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(620f, 62f), new Vector2(0f, 70f), PrimaryTextColor);
            AddTextOutline(complete, accent, 1.4f);
            CreateText(
                "SeasonTransitionDescription", panel, description,
                19, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(600f, 90f), new Vector2(0f, -20f), SecondaryTextColor);
        }

        private static Button CreateSeasonTransitionButton(
            Transform panel,
            string name,
            string label,
            Color color)
        {
            Button button = CreateButton(
                name, panel, label, new Vector2(430f, 68f), new Vector2(0f, -122f),
                color, out Text buttonLabel);
            buttonLabel.fontSize = 23;
            return button;
        }

        private void ShowSeasonAutoCompletionConfirmation()
        {
            if (!CanAutoCompleteCurrentPhase(_manager?.Dashboard))
                return;
            _isSeasonAutoCompletionConfirmationVisible = true;
            Render();
        }

        private void ConfirmSeasonAutoCompletion()
        {
            _isSeasonAutoCompletionConfirmationVisible = false;
            if (!_manager.AutoCompleteCurrentSeasonPhase())
            {
                _isSeasonAutoCompletionConfirmationVisible = true;
                Render();
            }
        }

        private void RenderSeasonAutoCompletionConfirmation(CareerDashboardView view)
        {
            bool isRegularSeason = view.SeasonPhase == SeasonPhase.RegularSeason;
            string title = isRegularSeason ? "정규시즌 자동 완료" : "포스트시즌 자동 완료";
            string warning = isRegularSeason
                ? $"남은 정규시즌 {view.RemainingRegularSeasonGames}경기를\n결과만 보기로 자동 진행합니다."
                : "남은 포스트시즌 경기를\n결과만 보기로 자동 진행합니다.";
            string guide = isRegularSeason
                ? "개별 경기 개입은 건너뜁니다.\n포스트시즌 진입 화면에서 멈추며, 포스트시즌은 별도로 진행합니다."
                : "개별 경기 개입은 건너뜁니다.\n우승 구단이 확정된 뒤 시즌 결산 화면에서 멈춥니다.";
            RectTransform blocker = CreateImage(
                "SeasonAutoCompletionBlocker", _content, new Color(0f, 0.01f, 0.02f, 0.82f),
                Vector2.zero, Vector2.zero, stretch: true);
            blocker.GetComponent<Image>().raycastTarget = true;
            RectTransform modal = CreatePanel(
                "SeasonAutoCompletionModal", "FAST FORWARD", title,
                new Vector2(790f, 440f), Vector2.zero);
            CreateText(
                "Warning", modal, warning,
                25, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(700f, 80f), new Vector2(0f, 74f), PrimaryTextColor);
            CreateText(
                "Guide", modal, guide,
                17, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(680f, 70f), new Vector2(0f, -8f), SecondaryTextColor);
            Button cancel = CreateButton(
                "Cancel", modal, "취소   ESC", new Vector2(260f, 62f), new Vector2(-155f, -132f),
                PanelDarkColor, out Text cancelLabel);
            cancelLabel.color = SecondaryTextColor;
            cancel.onClick.AddListener(() =>
            {
                _isSeasonAutoCompletionConfirmationVisible = false;
                Render();
            });
            Button confirm = CreateButton(
                "Confirm", modal, "자동 완료   ENTER", new Vector2(300f, 62f), new Vector2(155f, -132f),
                new Color(0.42f, 0.25f, 0.04f, 1f), out _);
            confirm.onClick.AddListener(ConfirmSeasonAutoCompletion);
        }

        private static bool CanAutoCompleteCurrentPhase(CareerDashboardView view)
        {
            if (view == null)
                return false;
            return view.SeasonPhase == SeasonPhase.RegularSeason
                ? view.RemainingRegularSeasonGames > 0
                : view.SeasonPhase == SeasonPhase.Postseason;
        }

        private static bool IsConfirmKeyPressed(Keyboard keyboard)
        {
            return keyboard.spaceKey.wasPressedThisFrame ||
                   keyboard.enterKey.wasPressedThisFrame ||
                   keyboard.numpadEnterKey.wasPressedThisFrame;
        }

        private static string GetPostseasonResultLabel(PlayerTeamPostseasonResult result)
        {
            return result switch
            {
                PlayerTeamPostseasonResult.Champion => "내 구단 우승",
                PlayerTeamPostseasonResult.RunnerUp => "내 구단 준우승",
                PlayerTeamPostseasonResult.SemifinalElimination => "내 구단 준결승 탈락",
                _ => "내 구단 포스트시즌 미진출"
            };
        }

        private static void CreateInfoChip(Transform parent, string label, string value, Vector2 position)
        {
            RectTransform chip = CreateSection(
                "Info_" + label, parent, new Vector2(204f, 58f), position, PanelDarkColor);
            CreateText(
                "Label", chip, label, 11, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(56f, 22f), new Vector2(-67f, 0f), MutedColor);
            CreateText(
                "Value", chip, value, 16, FontStyle.Bold, TextAnchor.MiddleRight,
                new Vector2(126f, 28f), new Vector2(28f, 0f), PrimaryTextColor);
        }

        private void RenderSeasonPanel(CareerDashboardView view)
        {
            RectTransform panel = CreatePanel(
                "SeasonPanel", "SEASON", $"{view.SeasonYear} 시즌 요약",
                new Vector2(628f, 560f), new Vector2(646f, 168f));

            RectTransform rank = CreateSection(
                "RankTile", panel, new Vector2(286f, 116f), new Vector2(-151f, 145f), PanelDarkColor);
            CreateText(
                "Label", rank, "팀 순위", 14, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(104f, 28f), new Vector2(-78f, 34f), SecondaryTextColor);
            Text rankValue = CreateText(
                "Value", rank, view.TeamRank.ToString(), 52, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(105f, 69f), new Vector2(-34f, -12f), AccentColor);
            AddTextOutline(rankValue, new Color(0.04f, 0.25f, 0.5f, 1f), 1.4f);
            CreateText(
                "Suffix", rank, "위", 24, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(55f, 40f), new Vector2(48f, -17f), PrimaryTextColor);
            CreateText(
                "LeagueCount", rank, "8개 구단", 12, FontStyle.Normal, TextAnchor.MiddleRight,
                new Vector2(92f, 24f), new Vector2(86f, 34f), MutedColor);

            RectTransform record = CreateSection(
                "RecordTile", panel, new Vector2(286f, 116f), new Vector2(151f, 145f), PanelDarkColor);
            CreateText(
                "Label", record, "팀 성적", 14, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(104f, 28f), new Vector2(-78f, 34f), SecondaryTextColor);
            CreateText(
                "Value", record, $"{view.TeamWins}승  {view.TeamLosses}패", 28, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(245f, 43f), Vector2.zero, PrimaryTextColor);
            double winningPercentage = CalculateWinningPercentage(view);
            CreateText(
                "Percentage", record, $"승률 {winningPercentage:.000}", 13, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Vector2(94f, 23f), new Vector2(-73f, -37f), AccentColor);
            CreateProgressBar(
                record, (float)winningPercentage, new Vector2(132f, 10f), new Vector2(60f, -37f), AccentColor);

            RectTransform statistics = CreateSection(
                "StatisticsSection", panel, new Vector2(588f, 160f), Vector2.zero, CardColor);
            CreateText(
                "Heading", statistics,
                view.Statistics.IsPitcher ? "선수 시즌 성적 · 투수" : "선수 시즌 성적 · 타자",
                15, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(530f, 28f), new Vector2(0f, 55f), PrimaryTextColor);
            CreateImage(
                "HeadingLine", statistics, DividerColor, new Vector2(548f, 1f), new Vector2(0f, 38f));
            RenderSeasonStatistics(statistics, view.Statistics);

            RectTransform recent = CreateSection(
                "RecentSection", panel, new Vector2(588f, 120f), new Vector2(0f, -151f), PanelDarkColor);
            CreateText(
                "Heading", recent, "최근 5경기", 14, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(160f, 26f), new Vector2(-191f, 36f), PrimaryTextColor);
            CreateText(
                "Summary", recent, BuildRecentPerformance(view), 14, FontStyle.Bold,
                TextAnchor.MiddleRight, new Vector2(310f, 26f), new Vector2(120f, 36f), SecondaryTextColor);
            RenderRecentFormChips(recent, view);
        }

        private static void RenderSeasonStatistics(Transform parent, PlayerSeasonStatisticsView statistics)
        {
            string[] labels;
            string[] values;
            if (statistics.IsPitcher)
            {
                labels = new[] { "ERA", "APP", "SO", "WHIP" };
                values = new[]
                {
                    statistics.EarnedRunAverage.ToString("0.00"),
                    statistics.PitchingAppearances.ToString(),
                    statistics.PitchingStrikeouts.ToString(),
                    statistics.WalksHitsPerInningPitched.ToString("0.00")
                };
            }
            else
            {
                labels = new[] { "AVG", "HR", "RBI", "OPS" };
                values = new[]
                {
                    statistics.BattingAverage.ToString(".000"),
                    statistics.HomeRuns.ToString(),
                    statistics.RunsBattedIn.ToString(),
                    statistics.OnBasePlusSlugging.ToString("0.000")
                };
            }

            for (int index = 0; index < labels.Length; index++)
            {
                float x = -207f + index * 138f;
                if (index > 0)
                {
                    CreateImage(
                        "Divider_" + index, parent, DividerColor, new Vector2(1f, 70f),
                        new Vector2(x - 69f, -10f));
                }
                CreateText(
                    "Label_" + index, parent, labels[index], 13, FontStyle.Bold,
                    TextAnchor.MiddleCenter, new Vector2(110f, 24f), new Vector2(x, 16f), SecondaryTextColor);
                CreateText(
                    "Value_" + index, parent, values[index], 27, FontStyle.Bold,
                    TextAnchor.MiddleCenter, new Vector2(125f, 39f), new Vector2(x, -21f), PrimaryTextColor);
            }
        }

        private static void RenderRecentFormChips(Transform parent, CareerDashboardView view)
        {
            const int capacity = 5;
            for (int index = 0; index < capacity; index++)
            {
                bool hasGame = index < view.RecentGames.Length;
                PlayerGameLogState game = hasGame ? view.RecentGames[index] : default;
                string value = hasGame ? GetOutcomeLabel(game) : "-";
                Color color = hasGame ? GetOutcomeColor(game) : MutedColor;
                RectTransform chip = CreateImage(
                    "Form_" + index, parent,
                    new Color(color.r, color.g, color.b, hasGame ? 0.32f : 0.16f),
                    new Vector2(98f, 40f), new Vector2(-216f + index * 108f, -23f));
                CreateImage("Top", chip, color, new Vector2(98f, 3f), new Vector2(0f, 18f));
                CreateText(
                    "Label", chip, value, 15, FontStyle.Bold, TextAnchor.MiddleCenter,
                    Vector2.zero, Vector2.zero, hasGame ? PrimaryTextColor : MutedColor, stretch: true);
            }
        }

        private void RenderCompetition(CareerDashboardView view)
        {
            RectTransform panel = CreatePanel(
                "CompetitionPanel", "POSITION DEPTH", $"{GetPositionCode(view.Position)} 포지션 경쟁",
                new Vector2(480f, 264f), new Vector2(-700f, -302f));
            string role = GetExpectedRoleLabel(view.ExpectedRole);
            RectTransform roleBadge = CreateSection(
                "RoleBadge", panel, new Vector2(430f, 55f), new Vector2(0f, 65f),
                new Color(0.18f, 0.14f, 0.055f, 1f));
            CreateText(
                "RoleLabel", roleBadge, "현재 역할", 13, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(105f, 29f), new Vector2(-134f, 0f), SecondaryTextColor);
            CreateText(
                "Role", roleBadge, role, 23, FontStyle.Bold, TextAnchor.MiddleRight,
                new Vector2(250f, 38f), new Vector2(68f, 0f), GoldColor);

            int visibleCount = Math.Min(view.Competition.Length, 3);
            for (int index = 0; index < visibleCount; index++)
            {
                PositionCompetitionView competitor = view.Competition[index];
                float y = 13f - index * 42f;
                Color color = competitor.IsMyPlayer ? AccentColor : SecondaryTextColor;
                CreateText(
                    "Marker_" + index, panel, competitor.IsMyPlayer ? "●" : "○", 16,
                    FontStyle.Bold, TextAnchor.MiddleCenter,
                    new Vector2(28f, 30f), new Vector2(-198f, y), color);
                CreateText(
                    "Name_" + index, panel, competitor.Name, 16,
                    competitor.IsMyPlayer ? FontStyle.Bold : FontStyle.Normal, TextAnchor.MiddleLeft,
                    new Vector2(230f, 30f), new Vector2(-57f, y), PrimaryTextColor);
                CreateText(
                    "Overall_" + index, panel, $"OVR  {competitor.Overall}", 15, FontStyle.Bold,
                    TextAnchor.MiddleRight, new Vector2(105f, 30f), new Vector2(158f, y), color);
                if (index < visibleCount - 1)
                {
                    CreateImage(
                        "RowLine_" + index, panel, DividerColor, new Vector2(414f, 1f),
                        new Vector2(0f, y - 21f));
                }
            }
        }

        private void RenderEventFeed(CareerDashboardView view)
        {
            RectTransform panel = CreatePanel(
                "EventPanel", "NEWS", "커리어 뉴스",
                new Vector2(760f, 310f), new Vector2(-64f, -279f));
            Button more = CreateButton(
                "MoreNews",
                panel,
                "전체 뉴스",
                new Vector2(116f, 30f),
                new Vector2(287f, 112f),
                PanelDarkColor,
                out Text moreLabel);
            moreLabel.fontSize = 12;
            moreLabel.color = AccentColor;
            more.onClick.AddListener(() => UIManager.Instance?.Show<UI_Popup_CareerNews>());

            CareerNewsFeedView feed = _manager.BuildNewsFeed(NewsFeedCategory.Latest, 3);
            if (feed.Articles.Length == 0)
            {
                RenderFeedRow(
                    panel, "OPEN", "정규 시즌 첫 뉴스가 아직 발행되지 않았습니다.",
                    "시즌 개막", 68f, AccentColor);
                return;
            }

            for (int index = 0; index < feed.Articles.Length; index++)
            {
                NewsArticleView article = feed.Articles[index];
                RenderFeedRow(
                    panel,
                    index == 0 ? "TOP" : GetNewsCategoryTag(article.Category),
                    article.Headline,
                    index == 0 && view.LastGame.HasValue
                        ? $"최근 경기 · {article.PublishedAt:M.d}"
                        : article.PublishedAt.ToString("M.d"),
                    68f - index * 60f,
                    GetNewsColor(article));
            }
        }

        private static string GetNewsCategoryTag(NewsCategory category)
        {
            return category switch
            {
                NewsCategory.MyPlayer => "PLAYER",
                NewsCategory.Club => "CLUB",
                NewsCategory.League => "LEAGUE",
                NewsCategory.Injury => "INJURY",
                NewsCategory.TransferContract => "DEAL",
                NewsCategory.Postseason => "POST",
                NewsCategory.RecordsAwards => "RECORD",
                NewsCategory.Offseason => "OFF",
                _ => "GAME"
            };
        }

        private static Color GetNewsColor(NewsArticleView article)
        {
            if (article.Importance is NewsImportance.S or NewsImportance.A)
                return GoldColor;
            return article.Category switch
            {
                NewsCategory.MyPlayer => BrightAccentColor,
                NewsCategory.Injury => WarningColor,
                NewsCategory.TransferContract => RoleColor,
                NewsCategory.Postseason => GoldColor,
                NewsCategory.RecordsAwards => GoldColor,
                _ => AccentColor
            };
        }

        private static void RenderFeedRow(
            Transform parent,
            string tag,
            string message,
            string meta,
            float y,
            Color accent)
        {
            RectTransform row = CreateImage(
                "Feed_" + tag, parent, new Color(0.01f, 0.045f, 0.078f, 0.92f),
                new Vector2(708f, 52f), new Vector2(0f, y));
            CreateImage("Accent", row, accent, new Vector2(4f, 42f), new Vector2(-350f, 0f));
            CreateText(
                "Tag", row, tag, 10, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(58f, 24f), new Vector2(-309f, 0f), accent);
            CreateText(
                "Message", row, message, 15, FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(475f, 35f), new Vector2(-34f, 0f), PrimaryTextColor);
            CreateText(
                "Meta", row, meta, 12, FontStyle.Normal, TextAnchor.MiddleRight,
                new Vector2(110f, 28f), new Vector2(286f, 0f), MutedColor);
        }

        private void RenderUpcoming(CareerDashboardView view)
        {
            RectTransform panel = CreatePanel(
                "UpcomingPanel", "UPCOMING", "예정 경기",
                new Vector2(628f, 310f), new Vector2(646f, -279f));
            if (view.UpcomingGames.Length == 0)
            {
                CreateText(
                    "Schedule", panel, GetEmptyScheduleText(view), 17, FontStyle.Normal,
                    TextAnchor.MiddleCenter, new Vector2(520f, 90f), Vector2.zero, SecondaryTextColor);
                return;
            }

            int visibleCount = Math.Min(view.UpcomingGames.Length, 4);
            for (int index = 0; index < visibleCount; index++)
                RenderUpcomingRow(panel, view.UpcomingGames[index], index);
            CreateText(
                "More", panel, $"전체 일정 · 다음 {view.UpcomingGames.Length}경기", 12,
                FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(430f, 24f), new Vector2(0f, -126f), MutedColor);
        }

        private static void RenderUpcomingRow(Transform parent, UpcomingGameView game, int index)
        {
            float y = 75f - index * 55f;
            Color accent = game.IsCurrent ? AccentColor : DividerColor;
            RectTransform row = CreateImage(
                "Upcoming_" + index, parent,
                game.IsCurrent ? new Color(0.035f, 0.13f, 0.22f, 1f) : PanelDarkColor,
                new Vector2(584f, 48f), new Vector2(0f, y));
            CreateImage("Accent", row, accent, new Vector2(4f, 40f), new Vector2(-288f, 0f));
            CreateText(
                "Date", row, game.Date.ToString("MM/dd"), 15, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(88f, 31f), new Vector2(-232f, 0f),
                game.IsCurrent ? PrimaryTextColor : SecondaryTextColor);
            CreateText(
                "Day", row, $"({GetKoreanDayOfWeek(game.Date.DayOfWeek)})", 12, FontStyle.Normal,
                TextAnchor.MiddleCenter, new Vector2(45f, 28f), new Vector2(-169f, 0f), MutedColor);
            CreateText(
                "Opponent", row, game.OpponentName, 16, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(280f, 31f), Vector2.zero, PrimaryTextColor);
            CreateText(
                "Venue", row, game.IsHome ? "HOME" : "AWAY", 13, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(78f, 30f), new Vector2(235f, 0f),
                game.IsHome ? AccentColor : WarningColor);
        }

        private void RenderTabs()
        {
            CareerTabBar.Create(_content, CareerMainTab.Home);
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
                "Eyebrow", header, eyebrow, 10, FontStyle.Bold, TextAnchor.MiddleLeft,
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

        private static RectTransform CreateTeamBadge(
            Transform parent,
            string teamName,
            Vector2 position,
            float size = 100f)
        {
            RectTransform outer = CreateImage(
                "TeamBadge_" + teamName, parent, BorderColor, new Vector2(size, size), position);
            RectTransform middle = CreateImage(
                "Middle", outer, new Color(0.015f, 0.12f, 0.2f, 1f),
                new Vector2(size - 8f, size - 8f), Vector2.zero);
            CreateImage(
                "Inset", middle, AccentColor, new Vector2(size - 20f, 3f),
                new Vector2(0f, size * 0.5f - 10f));
            CreateText(
                "Monogram", middle, GetTeamMonogram(teamName), Math.Max(24, (int)(size * 0.34f)),
                FontStyle.Bold, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero,
                PrimaryTextColor, stretch: true);
            return outer;
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
                "Track", parent, new Color(0.11f, 0.16f, 0.2f, 1f), size, position);
            float fillWidth = Mathf.Max(2f, (size.x - 4f) * clamped);
            RectTransform fill = CreateImage(
                "Fill", track, fillColor, new Vector2(fillWidth, size.y - 4f), Vector2.zero);
            fill.anchorMin = fill.anchorMax = new Vector2(0f, 0.5f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.anchoredPosition = new Vector2(2f, 0f);
        }

        private static string GetPersonalGameSummary(
            CareerGameAdvanceResult result,
            PlayerPosition position)
        {
            if (CareerGameRoleFormatter.IsPitcherRest(result.Role, position))
                return $"{CareerGameRoleFormatter.GetPitcherRestLabel(position)} · 등판 없음";

            return result.Role switch
            {
                PlayerGameRole.StartingBatter =>
                    $"{result.AtBats}타수 {result.Hits}안타 · HR {result.HomeRuns} · RBI {result.RunsBattedIn}" +
                    $" · BB {result.Walks}",
                PlayerGameRole.StartingPitcher or PlayerGameRole.ReliefPitcher =>
                    $"{FormatInnings(result.OutsRecorded)}이닝 · ER {result.EarnedRuns} · SO {result.Strikeouts}" +
                    $" · BB {result.WalksAllowed}",
                _ => "벤치 대기 · 출장 없음"
            };
        }

        private static string BuildRecentPerformance(CareerDashboardView view)
        {
            if (view.RecentGames.Length == 0)
                return "기록 없음";

            if (view.Statistics.IsPitcher)
            {
                int outs = 0;
                int earnedRuns = 0;
                int strikeouts = 0;
                int walksAllowed = 0;
                for (int index = 0; index < view.RecentGames.Length; index++)
                {
                    PlayerGameLogState game = view.RecentGames[index];
                    outs += game.OutsRecorded;
                    earnedRuns += game.EarnedRuns;
                    strikeouts += game.Strikeouts;
                    walksAllowed += game.WalksAllowed;
                }
                return $"{FormatInnings(outs)} IP / {earnedRuns} ER / {strikeouts} SO / {walksAllowed} BB";
            }

            int atBats = 0;
            int hits = 0;
            int homeRuns = 0;
            int runsBattedIn = 0;
            int walks = 0;
            for (int index = 0; index < view.RecentGames.Length; index++)
            {
                PlayerGameLogState game = view.RecentGames[index];
                atBats += game.AtBats;
                hits += game.Hits;
                homeRuns += game.HomeRuns;
                runsBattedIn += game.RunsBattedIn;
                walks += game.Walks;
            }
            double average = atBats == 0 ? 0d : hits / (double)atBats;
            return $"{average:.000} / {homeRuns} HR / {runsBattedIn} RBI / {walks} BB";
        }

        private static string GetAdvanceButtonLabel(PlayerGameRole role)
        {
            return role is PlayerGameRole.Bench or PlayerGameRole.PitcherRest ? "경기 관전" : "경기 진행";
        }

        private static string GetRoleGuide(PlayerGameRole role, PlayerPosition position)
        {
            if (CareerGameRoleFormatter.IsPitcherRest(role, position))
                return "오늘 등판 없음";

            return role switch
            {
                PlayerGameRole.StartingBatter => "선발 라인업 확정",
                PlayerGameRole.StartingPitcher => "선발 등판 확정",
                PlayerGameRole.ReliefPitcher => "불펜 대기",
                PlayerGameRole.Bench => "대기 명단 포함",
                _ => "감독 판단 대기"
            };
        }

        private static string GetRoleLabel(PlayerGameRole role, PlayerPosition position)
        {
            if (CareerGameRoleFormatter.IsPitcherRest(role, position))
                return CareerGameRoleFormatter.GetPitcherRestLabel(position);

            return role switch
            {
                PlayerGameRole.StartingBatter => $"선발 {GetPositionCode(position)}",
                PlayerGameRole.StartingPitcher => "선발 등판",
                PlayerGameRole.ReliefPitcher => "구원 등판 예정",
                PlayerGameRole.Bench => "벤치",
                _ => "미정"
            };
        }

        private static string GetExpectedRoleLabel(ExpectedRole role)
        {
            return role switch
            {
                ExpectedRole.StartingCompetition => "주전 경쟁",
                ExpectedRole.RosterCompetition => "로스터 경쟁",
                _ => "벤치 경쟁"
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

        private static string GetHandsLabel(CareerDashboardView view)
        {
            string throwing = view.ThrowingHand == Handedness.Left ? "좌투" : "우투";
            string batting = view.BattingHand switch
            {
                Handedness.Left => "좌타",
                Handedness.Switch => "양타",
                _ => "우타"
            };
            return throwing + batting;
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

        private static string GetSeasonDateText(CareerDashboardView view)
        {
            if (view.NextGame.HasValue)
            {
                return $"{view.NextGame.Value.Date:M월 d일} " +
                       $"({GetKoreanDayOfWeek(view.NextGame.Value.Date.DayOfWeek)})";
            }

            return view.SeasonPhase switch
            {
                SeasonPhase.Postseason => "포스트시즌",
                SeasonPhase.SeasonReview => "시즌 결산",
                SeasonPhase.Offseason => $"오프시즌 {view.SeasonProgress.OffseasonRemainingWeeks}주",
                SeasonPhase.Completed => "시즌 종료",
                _ => "정규 시즌 종료"
            };
        }

        private static string GetSeasonPhaseFeedText(CareerDashboardView view)
        {
            return view.SeasonPhase switch
            {
                SeasonPhase.Postseason => view.SeasonProgress.IsPlayerTeamPostseasonQualified
                    ? view.SeasonProgress.CanPlayNextPostseasonGame
                        ? "포스트시즌 진출 · 다음 내 구단 경기를 진행하세요."
                        : "포스트시즌 탈락 · 남은 우승 경쟁 결과를 확인하세요."
                    : "포스트시즌 미진출 · 리그 우승 결과를 확인하세요.",
                SeasonPhase.SeasonReview =>
                    $"{view.SeasonProgress.ChampionTeamName} 우승 · 시즌 결산을 진행하세요.",
                SeasonPhase.Offseason =>
                    $"오프시즌 {view.SeasonProgress.OffseasonRemainingWeeks}주 · 성장 방향을 선택하세요.",
                _ => "정규 시즌 일정을 모두 마쳤습니다."
            };
        }

        private static string GetSeasonPhaseMeta(CareerDashboardView view)
        {
            return view.SeasonPhase switch
            {
                SeasonPhase.Postseason => "포스트시즌",
                SeasonPhase.SeasonReview => "시즌 결산",
                SeasonPhase.Offseason => "오프시즌",
                _ => "시즌 종료"
            };
        }

        private static string GetEmptyScheduleText(CareerDashboardView view)
        {
            return view.SeasonPhase switch
            {
                SeasonPhase.Postseason => view.SeasonProgress.CanPlayNextPostseasonGame
                    ? "내 구단 포스트시즌 경기는 중앙 진행 카드에서 시작할 수 있습니다."
                    : "남은 포스트시즌 결과는 중앙 진행 카드에서 확인할 수 있습니다.",
                SeasonPhase.SeasonReview => "시즌 결산 후 오프시즌 일정이 시작됩니다.",
                SeasonPhase.Offseason =>
                    $"다음 시즌 전까지 성장에 사용할 수 있는 시간이 {view.SeasonProgress.OffseasonRemainingWeeks}주 남았습니다.",
                _ => "남은 정규 시즌 경기가 없습니다."
            };
        }

        private static string GetKoreanDayOfWeek(DayOfWeek day)
        {
            return day switch
            {
                DayOfWeek.Monday => "월",
                DayOfWeek.Tuesday => "화",
                DayOfWeek.Wednesday => "수",
                DayOfWeek.Thursday => "목",
                DayOfWeek.Friday => "금",
                DayOfWeek.Saturday => "토",
                _ => "일"
            };
        }

        private static string GetTeamMonogram(string teamName)
        {
            if (string.IsNullOrWhiteSpace(teamName))
                return "UP";
            string compact = teamName.Replace(" ", string.Empty);
            return compact.Length == 1 ? compact : compact.Substring(0, 2);
        }

        private static string GetOutcomeLabel(PlayerGameLogState game)
        {
            return game.TeamRuns > game.OpponentRuns
                ? "승"
                : game.TeamRuns < game.OpponentRuns ? "패" : "무";
        }

        private static Color GetOutcomeColor(PlayerGameLogState game)
        {
            return game.TeamRuns > game.OpponentRuns
                ? RoleColor
                : game.TeamRuns < game.OpponentRuns ? LossColor : GoldColor;
        }

        private static Color GetResultColor(CareerGameAdvanceResult result)
        {
            return result.TeamRuns > result.OpponentRuns
                ? RoleColor
                : result.TeamRuns < result.OpponentRuns ? LossColor : GoldColor;
        }

        private static Color GetRatingColor(int rating)
        {
            if (rating >= 80) return RoleColor;
            if (rating >= 65) return AccentColor;
            if (rating >= 50) return new Color(0.38f, 0.67f, 0.86f, 1f);
            return WarningColor;
        }

        private static double CalculateWinningPercentage(CareerDashboardView view)
        {
            int decisions = view.TeamWins + view.TeamLosses;
            return decisions == 0 ? 0d : view.TeamWins / (double)decisions;
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
            string name, Transform parent, Color color, Vector2 size, Vector2 position, bool stretch = false)
        {
            RectTransform rect = CreateRect(name, parent, size, position);
            if (stretch) Stretch(rect);
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
            if (stretch) Stretch(rect);
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
