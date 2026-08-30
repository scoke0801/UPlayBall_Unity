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
    public sealed partial class UI_Scene_CareerDashboard : UISceneBase, ICareerTabScreen
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
        private static readonly Color BrightAccentColor = CareerUiTheme.PrimaryBright;
        private static readonly Color RoleColor = CareerUiTheme.Success;
        private static readonly Color GoldColor = CareerUiTheme.AccentGold;
        private static readonly Color WarningColor = CareerUiTheme.Warning;
        private static readonly Color LossColor = CareerUiTheme.Loss;
        private static readonly Color PrimaryTextColor = CareerUiTheme.TextPrimary;
        private static readonly Color SecondaryTextColor = CareerUiTheme.TextSecondary;
        private static readonly Color MutedColor = CareerUiTheme.TextMuted;
        private static readonly Color ErrorColor = CareerUiTheme.Error;

        private CareerManager _manager;
        private RectTransform _content;
        private RectTransform _topRow;
        private RectTransform _bottomRow;
        private bool _isSeasonAutoCompletionConfirmationVisible;
        private bool _isSeasonReviewSkipConfirmationVisible;

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
            _isSeasonReviewSkipConfirmationVisible = false;
            Render();
        }

        private void Update()
        {
            if (!IsVisible || _manager == null || !_manager.HasActiveCareer || Keyboard.current == null)
                return;
            if (UI_CareerPresentation.IsPlaying)
                return;

            Keyboard keyboard = Keyboard.current;
            CareerDashboardView dashboard = _manager.Dashboard;
            if (dashboard.PendingReaction != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame)
                    ResolveCareerReaction(0);
                else if (keyboard.digit2Key.wasPressedThisFrame)
                    ResolveCareerReaction(1);
                else if (keyboard.digit3Key.wasPressedThisFrame)
                    ResolveCareerReaction(2);
                return;
            }
            if (IsSeasonReviewOverlayVisible(dashboard))
            {
                if (_isSeasonReviewSkipConfirmationVisible)
                {
                    if (keyboard.escapeKey.wasPressedThisFrame)
                    {
                        _isSeasonReviewSkipConfirmationVisible = false;
                        Render();
                    }
                    else if (IsConfirmKeyPressed(keyboard))
                    {
                        _isSeasonReviewSkipConfirmationVisible = false;
                        _manager.SkipSeasonReview();
                    }
                    return;
                }

                if (keyboard.escapeKey.wasPressedThisFrame && CanSkipSeasonReview(dashboard))
                {
                    _isSeasonReviewSkipConfirmationVisible = true;
                    Render();
                }
                else if (IsConfirmKeyPressed(keyboard))
                {
                    _manager.AdvanceSeasonReview();
                }
                return;
            }

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
                    _manager.AdvanceSeasonReview();
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
            if (view.PendingReaction != null)
            {
                RenderCareerReactionOverlay(view);
                return;
            }
            if (IsSeasonReviewOverlayVisible(view))
            {
                RenderSeasonReviewOverlay(view);
                if (_isSeasonReviewSkipConfirmationVisible)
                    RenderSeasonReviewSkipConfirmation(view);
                return;
            }
            CreateDashboardLayout();
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

        private void CreateDashboardLayout()
        {
            RectTransform safeArea = CreateRect("DashboardContentSafeArea", _content, Vector2.zero, Vector2.zero);
            Stretch(safeArea);
            safeArea.offsetMin = new Vector2(CareerUiTheme.Space4, 102f);
            safeArea.offsetMax = new Vector2(-CareerUiTheme.Space4, -92f);

            VerticalLayoutGroup columns = safeArea.gameObject.AddComponent<VerticalLayoutGroup>();
            columns.childAlignment = TextAnchor.MiddleCenter;
            columns.spacing = CareerUiTheme.Space4;
            columns.childControlWidth = true;
            columns.childControlHeight = true;
            columns.childForceExpandWidth = true;
            columns.childForceExpandHeight = true;

            _topRow = CreateDashboardRow("TopRow", safeArea, 64f);
            _bottomRow = CreateDashboardRow("BottomRow", safeArea, 36f);
        }

        private static RectTransform CreateDashboardRow(string name, Transform parent, float flexibleHeight)
        {
            RectTransform row = CreateRect(name, parent, Vector2.zero, Vector2.zero);
            LayoutElement layout = row.gameObject.AddComponent<LayoutElement>();
            layout.flexibleHeight = flexibleHeight;
            layout.flexibleWidth = 1f;

            HorizontalLayoutGroup panels = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            panels.childAlignment = TextAnchor.MiddleCenter;
            panels.spacing = CareerUiTheme.Space4;
            panels.childControlWidth = true;
            panels.childControlHeight = true;
            panels.childForceExpandWidth = true;
            panels.childForceExpandHeight = true;
            return row;
        }

        private void RenderBackgroundAccents()
        {
            CreateImage("TopGlow", _content, CareerUiTheme.TopGlow,
                new Vector2(1920f, 5f), new Vector2(0f, 456f));
            CreateImage("BottomGlow", _content, CareerUiTheme.BottomGlow,
                new Vector2(1920f, 4f), new Vector2(0f, -443f));
        }

        private void RenderTopBar(CareerDashboardView view)
        {
            RectTransform bar = CreateImage(
                "TopBar", _content, TopBarColor, new Vector2(1920f, 80f), new Vector2(0f, 500f));
            MarkVisual(bar, CareerUiVisualRole.FlatSurface);
            CreateDivider("TopBarBottom", bar, BorderColor, new Vector2(1920f, 2f), new Vector2(0f, -39f));

            Text logo = CreateText(
                "Logo", bar, "UPlayBall", 34, FontStyle.BoldAndItalic, TextAnchor.MiddleLeft,
                new Vector2(310f, 50f), new Vector2(-800f, 5f), PrimaryTextColor);
            AddTextOutline(logo, CareerUiTheme.PrimaryOutline, 1.5f);
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
        }

        private static void CreateTopBarSegment(
            Transform parent,
            string eyebrow,
            string value,
            Vector2 position,
            Vector2 size)
        {
            RectTransform segment = CreateRect(eyebrow + "Segment", parent, size, position);
            CreateDivider(
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
            DashboardPanel panel = CreateDashboardPanel(
                "PlayerPanel", "MY PLAYER", "내 선수", _topRow, 26f, false);
            RenderPlayerCard(panel.ContentSafeArea, view);
            RenderPlayerAttributes(panel.ContentSafeArea, view);
            RenderPlayerStatus(panel.ContentSafeArea, view);
        }

        private static void RenderPlayerCard(RectTransform panel, CareerDashboardView view)
        {
            RectTransform card = CreateSection(
                "PlayerCard", panel, new Vector2(400f, 144f), new Vector2(0f, 144f),
                CareerUiTheme.SurfaceSubtle);
            CreateDivider("CardStripe", card, AccentColor, new Vector2(4f, 128f), new Vector2(-192f, 0f));
            CreateText(
                "OverallLabel", card, "OVR", 13, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(60f, 20f), new Vector2(-160f, 32f), SecondaryTextColor);
            Text overall = CreateText(
                "Overall", card, view.Overall.ToString(), 39, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(68f, 52f), new Vector2(-160f, 0f), PrimaryTextColor);
            AddTextOutline(overall, AccentColor, 1.2f);

            CreateImage(
                "PortraitBackdrop", card, PortraitBackdropColor,
                new Vector2(176f, 104f), new Vector2(-36f, 12f));
            RectTransform portrait = CreateImage(
                "PlayerPortrait", card, Color.white, new Vector2(176f, 104f), new Vector2(-36f, 12f));
            Image portraitImage = portrait.GetComponent<Image>();
            portraitImage.sprite = PlayerPortraitSprites.GetDefault(view.Position);
            portraitImage.preserveAspect = true;
            MarkVisual(portrait, CareerUiVisualRole.DataImage);
            CreateTeamBadge(card, view.TeamName, new Vector2(144f, 20f), 72f);

            CreateText(
                "Position", card, GetPositionCode(view.Position), 20, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(60f, 32f), new Vector2(-160f, -48f), PrimaryTextColor);
            CreateText(
                "PlayerName", card, view.PlayerName, 22, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(220f, 36f), new Vector2(-12f, -48f), PrimaryTextColor);

            CreateText(
                "TeamName", panel, view.TeamName, 15, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(192f, 24f), new Vector2(-100f, 60f), SecondaryTextColor);
            CreateText(
                "Profile", panel, $"{GetPositionCode(view.Position)}  ·  {view.Age}세  ·  {GetHandsLabel(view)}",
                15, FontStyle.Normal, TextAnchor.MiddleRight,
                new Vector2(200f, 24f), new Vector2(100f, 60f), SecondaryTextColor);
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
                labels = new[] { "교타력", "장타력", "주력", "송구력", "수비력", "정신력" };
                values = new[]
                {
                    view.BatterAttributes.Contact,
                    view.BatterAttributes.Power,
                    view.BatterAttributes.Speed,
                    view.BatterAttributes.Arm,
                    view.BatterAttributes.Defense,
                    view.BatterAttributes.Mental
                };
            }

            for (int index = 0; index < values.Length; index++)
                CreateAttributeBar(panel, labels[index], values[index], new Vector2(0f, 24f - index * 27f));
        }

        private static void RenderPlayerStatus(RectTransform panel, CareerDashboardView view)
        {
            RectTransform summary = CreateSection(
                "PlayerStatusSummary", panel, new Vector2(400f, 52f), new Vector2(0f, -192f),
                CareerUiTheme.SurfaceSubtle);
            CreateDivider("StatusDivider", summary, DividerColor, new Vector2(1f, 36f), Vector2.zero);

            RectTransform condition = CreateRect(
                "Condition", summary, new Vector2(196f, 44f), new Vector2(-100f, 0f));
            CreateText(
                "Label", condition, "컨디션", 12, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(92f, 20f), new Vector2(-44f, 12f), SecondaryTextColor);
            CreateText(
                "Value", condition, view.Condition.ToString(), 22, FontStyle.Bold, TextAnchor.MiddleRight,
                new Vector2(52f, 28f), new Vector2(68f, 12f), GetRatingColor(view.Condition));
            CreateProgressBar(
                condition, view.Condition / 100f, new Vector2(168f, 8f), new Vector2(0f, -16f),
                GetRatingColor(view.Condition));

            RectTransform evaluation = CreateRect(
                "Evaluation", summary, new Vector2(196f, 44f), new Vector2(100f, 0f));
            CreateText(
                "Label", evaluation, "감독 평가", 12, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(92f, 20f), new Vector2(-44f, 12f), SecondaryTextColor);
            CreateText(
                "Value", evaluation, view.ManagerEvaluation.ToString(), 22, FontStyle.Bold,
                TextAnchor.MiddleRight, new Vector2(52f, 28f), new Vector2(68f, 12f),
                GetRatingColor(view.ManagerEvaluation));
            CreateProgressBar(
                evaluation, view.ManagerEvaluation / 100f, new Vector2(168f, 8f),
                new Vector2(0f, -16f), GetRatingColor(view.ManagerEvaluation));
        }

        private static void CreateAttributeBar(Transform parent, string label, int value, Vector2 position)
        {
            CreateText(
                label, parent, label, 14, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(64f, 24f), new Vector2(-164f, position.y), SecondaryTextColor);
            CreateProgressBar(
                parent, value / 100f, new Vector2(224f, 8f), new Vector2(4f, position.y),
                GetRatingColor(value));
            CreateText(
                label + "Value", parent, value.ToString(), 15, FontStyle.Bold, TextAnchor.MiddleRight,
                new Vector2(44f, 24f), new Vector2(176f, position.y), GetRatingColor(value));
        }

        private void RenderNextGame(CareerDashboardView view)
        {
            DashboardPanel roots = CreateDashboardPanel(
                "NextGamePanel", "NEXT GAME", "다음 경기", _topRow, 43f, true);
            RectTransform panel = roots.ContentSafeArea;
            if (!view.NextGame.HasValue)
            {
                RenderSeasonTransition(panel, view);
                return;
            }

            NextCareerGameView game = view.NextGame.Value;
            CreateTeamBadge(panel, game.AwayTeamName, new Vector2(-220f, 140f), 96f);
            CreateTeamBadge(panel, game.HomeTeamName, new Vector2(220f, 140f), 96f);
            CreateText(
                "AwayTeam", panel, game.AwayTeamName, 21, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(248f, 32f), new Vector2(-220f, 72f), PrimaryTextColor);
            CreateText(
                "HomeTeam", panel, game.HomeTeamName, 21, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(248f, 32f), new Vector2(220f, 72f), PrimaryTextColor);
            Text versus = CreateText(
                "Versus", panel, "VS", 42, FontStyle.BoldAndItalic, TextAnchor.MiddleCenter,
                new Vector2(100f, 60f), new Vector2(0f, 140f), PrimaryTextColor);
            AddTextOutline(versus, AccentColor, 1.6f);
            CreateText(
                "VenueType", panel, game.IsHome ? "HOME" : "AWAY", 13, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(100f, 24f), new Vector2(0f, 100f), AccentColor);

            CreateMetadataRow(
                panel,
                new[] { "경기일", "구분", "시즌" },
                new[]
                {
                    game.Date.ToString("M월 d일"),
                    game.IsHome ? "홈 경기" : "원정 경기",
                    $"{view.Statistics.TeamGames + 1}번째 경기"
                },
                new Vector2(648f, 44f),
                new Vector2(0f, 24f));

            RectTransform role = CreateSection(
                "PlannedRoleBand", panel, new Vector2(648f, 48f), new Vector2(0f, -40f),
                CareerUiTheme.RoleBand);
            CreateText(
                "RoleLabel", role, "예상 역할", 14, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(112f, 28f), new Vector2(-256f, 0f), SecondaryTextColor);
            CreateText(
                "PlannedRole", role, GetRoleLabel(game.PlannedRole, view.Position, game.BattingOrder), 23,
                FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(328f, 40f), new Vector2(-16f, 0f), RoleColor);
            CreateText(
                "RoleGuide", role, GetRoleGuide(game.PlannedRole, view.Position), 13, FontStyle.Normal,
                TextAnchor.MiddleRight, new Vector2(152f, 28f), new Vector2(244f, 0f), SecondaryTextColor);

            Button advance = CreateButtonWithKeyPrompt(
                "AdvanceGame", roots.InteractionRoot, GetAdvanceButtonLabel(game.PlannedRole), "SPACE",
                new Vector2(392f, 60f), new Vector2(-129f, -116f),
                CareerUiTheme.PrimaryAction, out Text label);
            label.fontSize = 27;
            AddTextOutline(label, CareerUiTheme.StrongOutline, 1.5f);
            advance.onClick.AddListener(() =>
            {
                advance.interactable = false;
                _manager.PrepareNextGame();
            });
            Button autoSeason = CreateButtonWithKeyPrompt(
                "AutoCompleteRegularSeason", roots.InteractionRoot, "정규시즌 자동 완료", "S",
                new Vector2(242f, 60f), new Vector2(204f, -116f),
                CareerUiTheme.SecondaryAction, out Text autoSeasonLabel);
            autoSeasonLabel.fontSize = 16;
            autoSeasonLabel.color = PrimaryTextColor;
            autoSeason.onClick.AddListener(ShowSeasonAutoCompletionConfirmation);
            if (!string.IsNullOrEmpty(_manager.LastError))
            {
                CreateText(
                    "Error", panel, _manager.LastError, 15, FontStyle.Normal, TextAnchor.MiddleCenter,
                    new Vector2(648f, 24f), new Vector2(0f, -192f), ErrorColor);
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
                    CareerUiTheme.SpecialAction);
                complete.onClick.AddListener(ShowSeasonAutoCompletionConfirmation);
                return;
            }

            Button advance = CreateButtonWithKeyPrompt(
                "AdvancePostseason", panel, "다음 포스트시즌 경기", "ENTER",
                new Vector2(410f, 68f), new Vector2(-114f, -122f),
                CareerUiTheme.SpecialAction, out Text advanceLabel);
            advanceLabel.fontSize = 21;
            advance.onClick.AddListener(() =>
            {
                advance.interactable = false;
                _manager.PrepareNextGame();
            });

            Button autoComplete = CreateButtonWithKeyPrompt(
                "AutoCompletePostseason", panel, "포스트시즌 자동 완료", "S",
                new Vector2(210f, 68f), new Vector2(214f, -122f),
                CareerUiTheme.SecondaryAction, out Text autoCompleteLabel);
            autoCompleteLabel.fontSize = 16;
            autoCompleteLabel.color = PrimaryTextColor;
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
                CareerUiTheme.SuccessAction);
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
                CareerUiTheme.PrimaryAction);
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
            Button button = CreateButtonWithKeyPrompt(
                name, panel, label, "ENTER", new Vector2(430f, 68f), new Vector2(0f, -122f),
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
                "SeasonAutoCompletionBlocker", _content, CareerUiTheme.InputBlocker,
                Vector2.zero, Vector2.zero, stretch: true);
            blocker.GetComponent<Image>().raycastTarget = true;
            MarkVisual(blocker, CareerUiVisualRole.InputBlocker);
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
            Button cancel = CreateButtonWithKeyPrompt(
                "Cancel", modal, "취소", "ESC", new Vector2(260f, 62f), new Vector2(-155f, -132f),
                PanelDarkColor, out Text cancelLabel);
            cancelLabel.color = SecondaryTextColor;
            cancel.onClick.AddListener(() =>
            {
                _isSeasonAutoCompletionConfirmationVisible = false;
                Render();
            });
            Button confirm = CreateButtonWithKeyPrompt(
                "Confirm", modal, "자동 완료", "ENTER", new Vector2(300f, 62f), new Vector2(155f, -132f),
                CareerUiTheme.SpecialAction, out _);
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

        private static void CreateMetadataRow(
            Transform parent,
            string[] labels,
            string[] values,
            Vector2 size,
            Vector2 position)
        {
            RectTransform row = CreateSection("MetadataRow", parent, size, position, CareerUiTheme.SurfaceSubtle);
            float cellWidth = size.x / labels.Length;
            for (int index = 0; index < labels.Length; index++)
            {
                float x = -size.x * 0.5f + cellWidth * (index + 0.5f);
                if (index > 0)
                {
                    CreateDivider(
                        "Divider_" + index, row, DividerColor, new Vector2(1f, size.y - 16f),
                        new Vector2(x - cellWidth * 0.5f, 0f));
                }
                CreateText(
                    "Label_" + index, row, labels[index], 11, FontStyle.Bold, TextAnchor.MiddleLeft,
                    new Vector2(cellWidth * 0.36f, 22f), new Vector2(x - cellWidth * 0.27f, 0f),
                    SecondaryTextColor);
                CreateText(
                    "Value_" + index, row, values[index], 15, FontStyle.Bold, TextAnchor.MiddleRight,
                    new Vector2(cellWidth * 0.58f, 27f), new Vector2(x + cellWidth * 0.15f, 0f),
                    PrimaryTextColor);
            }
        }

        private void RenderSeasonPanel(CareerDashboardView view)
        {
            DashboardPanel roots = CreateDashboardPanel(
                "SeasonPanel", "SEASON", $"{view.SeasonYear} 시즌 요약", _topRow, 31f, false);
            RectTransform panel = roots.ContentSafeArea;

            RectTransform rank = CreateSection(
                "RankTile", panel, new Vector2(236f, 96f), new Vector2(-124f, 164f),
                CareerUiTheme.SurfaceSubtle);
            CreateText(
                "Label", rank, "팀 순위", 14, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(96f, 24f), new Vector2(-64f, 28f), SecondaryTextColor);
            Text rankValue = CreateText(
                "Value", rank, view.TeamRank.ToString(), 44, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(84f, 60f), new Vector2(-24f, -12f), AccentColor);
            AddTextOutline(rankValue, CareerUiTheme.MetricOutline, 1.4f);
            CreateText(
                "Suffix", rank, "위", 24, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(48f, 36f), new Vector2(36f, -12f), PrimaryTextColor);
            CreateText(
                "LeagueCount", rank, "8개 구단", 12, FontStyle.Normal, TextAnchor.MiddleRight,
                new Vector2(80f, 24f), new Vector2(72f, 28f), MutedColor);

            RectTransform record = CreateSection(
                "RecordTile", panel, new Vector2(236f, 96f), new Vector2(124f, 164f),
                CareerUiTheme.SurfaceSubtle);
            CreateText(
                "Label", record, "팀 성적", 14, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(96f, 24f), new Vector2(-64f, 28f), SecondaryTextColor);
            CreateText(
                "Value", record, $"{view.TeamWins}승  {view.TeamLosses}패", 25, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(220f, 40f), Vector2.zero, PrimaryTextColor);
            double winningPercentage = CalculateWinningPercentage(view);
            CreateText(
                "Percentage", record, $"승률 {winningPercentage:.000}", 13, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Vector2(88f, 24f), new Vector2(-64f, -32f), AccentColor);
            CreateProgressBar(
                record, (float)winningPercentage, new Vector2(112f, 8f), new Vector2(60f, -32f), AccentColor);

            RectTransform statistics = CreateSection(
                "StatisticsSection", panel, new Vector2(484f, 184f), new Vector2(0f, 40f), CardColor);
            CreateText(
                "Heading", statistics,
                view.Statistics.IsPitcher ? "선수 시즌 성적 · 투수" : "선수 시즌 성적 · 타자",
                15, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(252f, 28f), new Vector2(-104f, 68f), PrimaryTextColor);
            CreateText(
                "SampleSize", statistics, BuildSeasonSampleSize(view.Statistics),
                11, FontStyle.Bold, TextAnchor.MiddleRight,
                new Vector2(208f, 24f), new Vector2(128f, 68f), SecondaryTextColor);
            CreateDivider(
                "HeadingLine", statistics, DividerColor, new Vector2(464f, 1f), new Vector2(0f, 51f));
            RenderSeasonStatistics(statistics, view.Statistics);

            RectTransform recent = CreateSection(
                "RecentSection", panel, new Vector2(484f, 88f), new Vector2(0f, -152f),
                CareerUiTheme.SurfaceSubtle);
            CreateText(
                "Heading", recent, "최근 5경기", 14, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(128f, 24f), new Vector2(-168f, 28f), PrimaryTextColor);
            CreateText(
                "Summary", recent, BuildRecentPerformance(view), 14, FontStyle.Bold,
                TextAnchor.MiddleRight, new Vector2(320f, 24f), new Vector2(72f, 28f), SecondaryTextColor);
            RenderRecentFormChips(recent, view);
        }

        private static void RenderSeasonStatistics(Transform parent, PlayerSeasonStatisticsView statistics)
        {
            string[] primaryLabels;
            string[] primaryValues;
            string[] detailLabels;
            string[] detailValues;
            if (statistics.IsPitcher)
            {
                primaryLabels = new[] { "평균자책", "WHIP", "승-패", "탈삼진" };
                primaryValues = new[]
                {
                    statistics.EarnedRunAverage.ToString("0.00"),
                    statistics.WalksHitsPerInningPitched.ToString("0.00"),
                    $"{statistics.Wins}-{statistics.Losses}",
                    statistics.PitchingStrikeouts.ToString()
                };
                detailLabels = new[] { "이닝", "피안타", "볼넷", "피홈런" };
                detailValues = new[]
                {
                    FormatInnings(statistics.OutsRecorded),
                    statistics.HitsAllowed.ToString(),
                    statistics.WalksAllowed.ToString(),
                    statistics.HomeRunsAllowed.ToString()
                };
            }
            else
            {
                primaryLabels = new[] { "타율", "OPS", "홈런", "타점" };
                primaryValues = new[]
                {
                    statistics.BattingAverage.ToString(".000"),
                    statistics.OnBasePlusSlugging.ToString("0.000"),
                    statistics.HomeRuns.ToString(),
                    statistics.RunsBattedIn.ToString()
                };
                detailLabels = new[] { "볼넷", "삼진", "도루 / 실패", "실책" };
                detailValues = new[]
                {
                    statistics.Walks.ToString(),
                    statistics.BattingStrikeouts.ToString(),
                    $"{statistics.StolenBases} / {statistics.CaughtStealing}",
                    statistics.FieldingErrors.ToString()
                };
            }

            RenderSeasonStatisticsRow(parent, "Primary", primaryLabels, primaryValues, 24f, 0f, 21);
            RenderSeasonStatisticsRow(parent, "Detail", detailLabels, detailValues, -39f, -62f, 19);
        }

        private static void RenderSeasonStatisticsRow(
            Transform parent,
            string rowName,
            string[] labels,
            string[] values,
            float labelY,
            float valueY,
            int valueFontSize)
        {
            for (int index = 0; index < labels.Length; index++)
            {
                float x = -180f + index * 120f;
                if (index > 0)
                {
                    CreateDivider(
                        $"{rowName}Divider_{index}", parent, DividerColor, new Vector2(1f, 48f),
                        new Vector2(x - 60f, valueY + 11f));
                }
                CreateText(
                    $"{rowName}Label_{index}", parent, labels[index], 12, FontStyle.Bold,
                    TextAnchor.MiddleCenter, new Vector2(104f, 20f), new Vector2(x, labelY), SecondaryTextColor);
                CreateText(
                    $"{rowName}Value_{index}", parent, values[index], valueFontSize, FontStyle.Bold,
                    TextAnchor.MiddleCenter, new Vector2(108f, 28f), new Vector2(x, valueY), PrimaryTextColor);
            }
        }

        private static string BuildSeasonSampleSize(PlayerSeasonStatisticsView statistics)
        {
            return statistics.IsPitcher
                ? $"{statistics.PitchingAppearances}경기  ·  선발 {statistics.PitchingStarts}  ·  {FormatInnings(statistics.OutsRecorded)}이닝"
                : $"{statistics.GamesPlayed}경기  ·  {statistics.PlateAppearances}타석  ·  {statistics.AtBats}타수";
        }

        private static void RenderRecentFormChips(Transform parent, CareerDashboardView view)
        {
            if (view.RecentGames.Length == 0)
            {
                CreateText(
                    "EmptyTitle", parent, "아직 완료한 경기가 없습니다.", 14, FontStyle.Bold,
                    TextAnchor.MiddleCenter, new Vector2(440f, 24f), new Vector2(0f, -8f),
                    SecondaryTextColor);
                CreateText(
                    "EmptyDescription", parent, "첫 경기를 진행하면 최근 경기 결과가 표시됩니다.", 12,
                    FontStyle.Normal, TextAnchor.MiddleCenter, new Vector2(440f, 20f),
                    new Vector2(0f, -28f), MutedColor);
                return;
            }

            const int capacity = 5;
            int visibleCount = Math.Min(view.RecentGames.Length, capacity);
            for (int index = 0; index < visibleCount; index++)
            {
                PlayerGameLogState game = view.RecentGames[index];
                string value = GetOutcomeLabel(game);
                Color color = GetOutcomeColor(game);
                RectTransform chip = CreateImage(
                    "Form_" + index, parent,
                    new Color(color.r, color.g, color.b, 0.28f),
                    new Vector2(80f, 32f), new Vector2(-176f + index * 88f, -20f));
                MarkVisual(chip, CareerUiVisualRole.FlatSurface);
                CreateDivider("Top", chip, color, new Vector2(80f, 3f), new Vector2(0f, 16f));
                CreateText(
                    "Label", chip, value, 15, FontStyle.Bold, TextAnchor.MiddleCenter,
                    Vector2.zero, Vector2.zero, PrimaryTextColor, stretch: true);
            }
        }

        private void RenderCompetition(CareerDashboardView view)
        {
            DashboardPanel roots = CreateDashboardPanel(
                "CompetitionPanel", "POSITION DEPTH", $"{GetPositionCode(view.Position)} 포지션 경쟁",
                _bottomRow, 26f, false);
            RectTransform panel = roots.ContentSafeArea;
            string role = GetExpectedRoleLabel(view.ExpectedRole);
            RectTransform roleBadge = CreateSection(
                "RoleBadge", panel, new Vector2(400f, 40f), new Vector2(0f, 76f),
                CareerUiTheme.SurfaceSelected);
            CreateText(
                "RoleLabel", roleBadge, "현재 역할", 13, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(100f, 24f), new Vector2(-128f, 0f), SecondaryTextColor);
            CreateText(
                "Role", roleBadge, role, 19, FontStyle.Bold, TextAnchor.MiddleRight,
                new Vector2(240f, 32f), new Vector2(72f, 0f), PrimaryTextColor);

            float contentHeight = Math.Max(128f, view.Competition.Length * 40f);
            RectTransform list = CreateVerticalScrollArea(
                "CompetitionList", panel, new Vector2(400f, 128f), new Vector2(0f, -24f), contentHeight);
            for (int index = 0; index < view.Competition.Length; index++)
            {
                PositionCompetitionView competitor = view.Competition[index];
                RectTransform row = CreateTopAnchoredRow(
                    "Competitor_" + index, list, new Vector2(400f, 36f), index * 40f);
                if (competitor.IsMyPlayer)
                {
                    RectTransform selected = CreateImage(
                        "SelectedSurface", row, CareerUiTheme.SurfaceSelected,
                        Vector2.zero, Vector2.zero, stretch: true);
                    MarkVisual(selected, CareerUiVisualRole.FlatSurface);
                    CreateDivider(
                        "SelectedIndicator", row, AccentColor, new Vector2(4f, 28f),
                        new Vector2(-196f, 0f));
                }
                Color color = competitor.IsMyPlayer ? AccentColor : SecondaryTextColor;
                CreateText(
                    "Marker", row, competitor.IsMyPlayer ? "●" : "○", 15,
                    FontStyle.Bold, TextAnchor.MiddleCenter,
                    new Vector2(28f, 28f), new Vector2(-172f, 0f), color);
                CreateText(
                    "Name", row, competitor.Name, 15,
                    competitor.IsMyPlayer ? FontStyle.Bold : FontStyle.Normal, TextAnchor.MiddleLeft,
                    new Vector2(228f, 28f), new Vector2(-40f, 0f), PrimaryTextColor);
                CreateText(
                    "Overall", row, $"OVR  {competitor.Overall}", 14, FontStyle.Bold,
                    TextAnchor.MiddleRight, new Vector2(96f, 28f), new Vector2(144f, 0f), color);
                if (index < view.Competition.Length - 1)
                    CreateDivider(
                        "RowLine", row, DividerColor, new Vector2(384f, 1f),
                        new Vector2(0f, -18f));
            }
        }

        private void RenderEventFeed(CareerDashboardView view)
        {
            DashboardPanel roots = CreateDashboardPanel(
                "EventPanel", "NEWS", "커리어 뉴스", _bottomRow, 43f, false);
            RectTransform panel = roots.ContentSafeArea;
            Button more = CreateButton(
                "MoreNews",
                roots.HeaderRoot,
                "전체 뉴스",
                new Vector2(108f, 28f),
                Vector2.zero,
                PanelDarkColor,
                out Text moreLabel);
            RectTransform moreRect = (RectTransform)more.transform;
            moreRect.anchorMin = moreRect.anchorMax = new Vector2(1f, 0.5f);
            moreRect.anchoredPosition = new Vector2(-56f, -12f);
            moreLabel.fontSize = 12;
            moreLabel.color = AccentColor;
            more.onClick.AddListener(() => UIManager.Instance?.Show<UI_Popup_CareerNews>());

            CareerNewsFeedView feed = _manager.BuildNewsFeed(NewsFeedCategory.Latest, 3);
            if (feed.Articles.Length == 0)
            {
                CreateText(
                    "EmptyTitle", panel, "아직 등록된 커리어 뉴스가 없습니다.", 17,
                    FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(620f, 28f),
                    new Vector2(0f, 17f), SecondaryTextColor);
                CreateText(
                    "EmptyDescription", panel, "첫 경기와 시즌 이벤트가 발생하면 뉴스가 추가됩니다.",
                    13, FontStyle.Normal, TextAnchor.MiddleCenter, new Vector2(620f, 24f),
                    new Vector2(0f, -17f), MutedColor);
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
                    56f - index * 56f,
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
                "Feed_" + tag, parent, CareerUiTheme.FeedSurface,
                new Vector2(648f, 48f), new Vector2(0f, y));
            MarkVisual(row, CareerUiVisualRole.FlatSurface);
            CreateDivider("Accent", row, accent, new Vector2(4f, 40f), new Vector2(-320f, 0f));
            CreateText(
                "Tag", row, tag, 10, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(56f, 24f), new Vector2(-280f, 0f), accent);
            CreateText(
                "Message", row, message, 15, FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(424f, 32f), new Vector2(-32f, 0f), PrimaryTextColor);
            CreateText(
                "Meta", row, meta, 12, FontStyle.Normal, TextAnchor.MiddleRight,
                new Vector2(104f, 28f), new Vector2(264f, 0f), MutedColor);
        }

        private void RenderUpcoming(CareerDashboardView view)
        {
            DashboardPanel roots = CreateDashboardPanel(
                "UpcomingPanel", "UPCOMING", "예정 경기", _bottomRow, 31f, false);
            RectTransform panel = roots.ContentSafeArea;
            if (view.UpcomingGames.Length == 0)
            {
                CreateText(
                    "Schedule", panel, GetEmptyScheduleText(view), 17, FontStyle.Normal,
                    TextAnchor.MiddleCenter, new Vector2(520f, 90f), Vector2.zero, SecondaryTextColor);
                return;
            }

            float contentHeight = Math.Max(148f, view.UpcomingGames.Length * 40f);
            RectTransform list = CreateVerticalScrollArea(
                "UpcomingList", panel, new Vector2(484f, 148f), new Vector2(0f, 12f), contentHeight);
            for (int index = 0; index < view.UpcomingGames.Length; index++)
                RenderUpcomingRow(list, view.UpcomingGames[index], index);
            CreateText(
                "More", panel, $"전체 일정 · 다음 {view.UpcomingGames.Length}경기", 12,
                FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(430f, 22f), new Vector2(0f, -62f), MutedColor);
        }

        private static void RenderUpcomingRow(Transform parent, UpcomingGameView game, int index)
        {
            Color accent = game.IsCurrent ? AccentColor : DividerColor;
            RectTransform row = CreateTopAnchoredRow(
                "Upcoming_" + index, parent, new Vector2(484f, 36f), index * 40f);
            RectTransform surface = CreateImage(
                "RowSurface", row,
                game.IsCurrent ? CareerUiTheme.CurrentRow : CareerUiTheme.SurfaceSubtle,
                Vector2.zero, Vector2.zero, stretch: true);
            MarkVisual(surface, CareerUiVisualRole.FlatSurface);
            CreateDivider("Accent", row, accent, new Vector2(4f, 28f), new Vector2(-236f, 0f));
            CreateText(
                "Date", row, game.Date.ToString("MM/dd"), 14, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(80f, 28f), new Vector2(-188f, 0f),
                game.IsCurrent ? PrimaryTextColor : SecondaryTextColor);
            CreateText(
                "Day", row, $"({GetKoreanDayOfWeek(game.Date.DayOfWeek)})", 11, FontStyle.Normal,
                TextAnchor.MiddleCenter, new Vector2(40f, 24f), new Vector2(-132f, 0f), MutedColor);
            CreateText(
                "Opponent", row, game.OpponentName, 15, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(220f, 28f), new Vector2(4f, 0f), PrimaryTextColor);
            CreateText(
                "Venue", row, game.IsHome ? "HOME" : "AWAY", 12, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(72f, 28f), new Vector2(200f, 0f),
                game.IsHome ? AccentColor : WarningColor);
        }

        private void RenderTabs()
        {
            CareerNavigationChrome.Create(_content, CareerMainTab.Home);
        }

        private readonly struct DashboardPanel
        {
            public DashboardPanel(
                RectTransform root,
                RectTransform headerRoot,
                RectTransform contentSafeArea,
                RectTransform interactionRoot)
            {
                Root = root;
                HeaderRoot = headerRoot;
                ContentSafeArea = contentSafeArea;
                InteractionRoot = interactionRoot;
            }

            public RectTransform Root { get; }
            public RectTransform HeaderRoot { get; }
            public RectTransform ContentSafeArea { get; }
            public RectTransform InteractionRoot { get; }
        }

        private static DashboardPanel CreateDashboardPanel(
            string name,
            string eyebrow,
            string title,
            Transform row,
            float flexibleWidth,
            bool isHero)
        {
            RectTransform root = CreateRect(name, row, Vector2.zero, Vector2.zero);
            LayoutElement layout = root.gameObject.AddComponent<LayoutElement>();
            layout.flexibleWidth = flexibleWidth;
            layout.flexibleHeight = 1f;

            RectTransform decorativeFrame = CreateImage(
                "DecorativeFrame", root, Color.white, Vector2.zero, Vector2.zero, stretch: true);
            Image frameImage = decorativeFrame.GetComponent<Image>();
            CareerUiVisualElement frameVisual = decorativeFrame.gameObject.AddComponent<CareerUiVisualElement>();
            frameVisual.Initialize(CareerUiVisualRole.DecorativeFrame, isHero);

            Vector4 padding = isHero
                ? CareerUiTheme.HeroFramePadding
                : CareerUiTheme.UniversalFramePadding;
            RectTransform headerRoot = CreateRect("HeaderRoot", root, Vector2.zero, Vector2.zero);
            headerRoot.anchorMin = new Vector2(0f, 1f);
            headerRoot.anchorMax = Vector2.one;
            headerRoot.pivot = new Vector2(0.5f, 1f);
            headerRoot.offsetMin = new Vector2(padding.x, -padding.w - CareerUiTheme.Space2);
            headerRoot.offsetMax = new Vector2(-padding.z, -CareerUiTheme.Space5 - CareerUiTheme.Space1);

            CreateText(
                "Eyebrow", headerRoot, eyebrow, 9, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(360f, 16f), new Vector2(0f, 14f), AccentColor);
            CreateText(
                "Heading", headerRoot, title, 20, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(400f, 32f), new Vector2(0f, -10f), PrimaryTextColor);

            RectTransform contentSafeArea = CreateRect("ContentSafeArea", root, Vector2.zero, Vector2.zero);
            Stretch(contentSafeArea);
            contentSafeArea.offsetMin = new Vector2(padding.x, padding.y);
            contentSafeArea.offsetMax = new Vector2(-padding.z, -padding.w);

            RectTransform interactionRoot = CreateRect("InteractionRoot", root, Vector2.zero, Vector2.zero);
            Stretch(interactionRoot);
            interactionRoot.offsetMin = contentSafeArea.offsetMin;
            interactionRoot.offsetMax = contentSafeArea.offsetMax;
            interactionRoot.SetAsLastSibling();

            CareerUiFrame frame = root.gameObject.AddComponent<CareerUiFrame>();
            frame.Initialize(frameImage, headerRoot, contentSafeArea, interactionRoot, padding, isHero);
            return new DashboardPanel(root, headerRoot, contentSafeArea, interactionRoot);
        }

        private RectTransform CreatePanel(
            string name,
            string eyebrow,
            string title,
            Vector2 size,
            Vector2 position)
        {
            RectTransform panel = CreateRect(name, _content, size, position);
            RectTransform frame = CreateImage(
                "DecorativeFrame", panel, Color.white, Vector2.zero, Vector2.zero, stretch: true);
            MarkVisual(frame, CareerUiVisualRole.DecorativeFrame, true);

            RectTransform header = CreateRect(
                "Header", panel, new Vector2(size.x - 120f, 58f),
                new Vector2(0f, size.y * 0.5f - 48f));
            CreateText(
                "Eyebrow", header, eyebrow, 9, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(size.x - 150f, 16f), new Vector2(0f, 14f), AccentColor);
            CreateText(
                "Heading", header, title, 20, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(size.x - 150f, 34f), new Vector2(0f, -10f), PrimaryTextColor);
            return panel;
        }

        private static RectTransform CreateSection(
            string name,
            Transform parent,
            Vector2 size,
            Vector2 position,
            Color color)
        {
            RectTransform section = CreateRect(name, parent, size, position);
            if (color.a <= 0f)
                return section;

            RectTransform surface = CreateImage(
                "FlatSurface", section, color, Vector2.zero, Vector2.zero, stretch: true);
            CareerUiVisualElement visual = surface.gameObject.AddComponent<CareerUiVisualElement>();
            visual.Initialize(CareerUiVisualRole.FlatSurface);
            return section;
        }

        private static RectTransform CreateTeamBadge(
            Transform parent,
            string teamName,
            Vector2 position,
            float size = 100f)
        {
            RectTransform outer = CreateRect(
                "TeamBadge_" + teamName, parent, new Vector2(size, size), position);
            RectTransform middle = CreateImage(
                "FlatSurface", outer, CareerUiTheme.TeamBadgeSurface,
                Vector2.zero, Vector2.zero, stretch: true);
            CareerUiVisualElement surfaceVisual = middle.gameObject.AddComponent<CareerUiVisualElement>();
            surfaceVisual.Initialize(CareerUiVisualRole.FlatSurface);
            RectTransform inset = CreateImage(
                "Inset", middle, AccentColor, new Vector2(size - 20f, 3f),
                new Vector2(0f, size * 0.5f - 10f));
            CareerUiVisualElement insetVisual = inset.gameObject.AddComponent<CareerUiVisualElement>();
            insetVisual.Initialize(CareerUiVisualRole.Divider);
            CreateText(
                "Monogram", middle, CareerTeamNameFormatter.GetMonogram(teamName), Math.Max(24, (int)(size * 0.34f)),
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
                "Track", parent, CareerUiTheme.ProgressTrack, size, position);
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
                    $"{result.AtBats}타수 {result.Hits}안타 · 홈런 {result.HomeRuns} · 타점 {result.RunsBattedIn}" +
                    $" · 볼넷 {result.Walks}",
                PlayerGameRole.StartingPitcher or PlayerGameRole.ReliefPitcher =>
                    $"{FormatInnings(result.OutsRecorded)}이닝 · 자책 {result.EarnedRuns} · 탈삼진 {result.Strikeouts}" +
                    $" · 볼넷 {result.WalksAllowed}",
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
                return $"{FormatInnings(outs)}이닝 / 자책 {earnedRuns} / 탈삼진 {strikeouts} / 볼넷 {walksAllowed}";
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
            return $"타율 {average:.000} / 홈런 {homeRuns} / 타점 {runsBattedIn} / 볼넷 {walks}";
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

        private static string GetRoleLabel(PlayerGameRole role, PlayerPosition position, int battingOrder)
        {
            if (CareerGameRoleFormatter.IsPitcherRest(role, position))
                return CareerGameRoleFormatter.GetPitcherRestLabel(position);

            return role switch
            {
                PlayerGameRole.StartingBatter when battingOrder > 0 =>
                    $"선발 {GetPositionCode(position)} · {battingOrder}번 타자",
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
            return WorldGenerationConfiguration.GetDefaultDefinition(level).UiDisplayName;
        }

        private static string GetSeasonDateText(CareerDashboardView view)
        {
            if (view.SeasonReviewStep is SeasonReviewStep.RegularSeasonIntro or
                SeasonReviewStep.RegularSeasonResult or
                SeasonReviewStep.PostseasonEntry)
            {
                return "정규시즌 종료";
            }
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
            if (rating >= 50) return CareerUiTheme.RatingMid;
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

        private static Button CreateButtonWithKeyPrompt(
            string name,
            Transform parent,
            string label,
            string keyPrompt,
            Vector2 size,
            Vector2 position,
            Color color,
            out Text labelText)
        {
            Button button = CreateButton(name, parent, label, size, position, color, out labelText);
            RectTransform labelRect = labelText.rectTransform;
            labelRect.offsetMin = new Vector2(24f, 6f);
            labelRect.offsetMax = new Vector2(-72f, -6f);

            CreateDivider(
                "KeyDivider", button.transform, DividerColor, new Vector2(1f, size.y - 24f),
                new Vector2(size.x * 0.5f - 68f, 0f));
            Text prompt = CreateText(
                "KeyPrompt", button.transform, keyPrompt, 12, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(52f, 28f), Vector2.zero, SecondaryTextColor);
            RectTransform promptRect = prompt.rectTransform;
            promptRect.anchorMin = promptRect.anchorMax = new Vector2(1f, 0.5f);
            promptRect.anchoredPosition = new Vector2(-39f, 0f);
            return button;
        }

        private static RectTransform CreateVerticalScrollArea(
            string name,
            Transform parent,
            Vector2 size,
            Vector2 position,
            float contentHeight)
        {
            RectTransform root = CreateRect(name, parent, size, position);
            ScrollRect scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;

            RectTransform viewport = CreateRect("Viewport", root, Vector2.zero, Vector2.zero);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform content = CreateRect("Content", viewport, Vector2.zero, Vector2.zero);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, contentHeight);

            scroll.viewport = viewport;
            scroll.content = content;
            return content;
        }

        private static RectTransform CreateTopAnchoredRow(
            string name,
            Transform parent,
            Vector2 size,
            float topOffset)
        {
            RectTransform row = CreateRect(name, parent, size, Vector2.zero);
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.anchoredPosition = new Vector2(0f, -topOffset);
            return row;
        }

        private static RectTransform CreateDivider(
            string name,
            Transform parent,
            Color color,
            Vector2 size,
            Vector2 position)
        {
            RectTransform divider = CreateImage(name, parent, color, size, position);
            MarkVisual(divider, CareerUiVisualRole.Divider);
            return divider;
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
            MarkVisual(rect, CareerUiVisualRole.InteractiveControl);
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
