using Baseball.Core.Teams;
using Baseball.Game.Career;
using Baseball.Game.Manager;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Career
{
    /// <summary>내 선수의 현재 상태·능력·성장 근거·커리어를 읽는 선수 상세 화면이다.</summary>
    public sealed partial class UI_Scene_Player : UISceneBase, ICareerTabScreen
    {
        private static readonly Color BackgroundColor = new(0.006f, 0.02f, 0.034f, 1f);
        private static readonly Color TopBarColor = new(0.008f, 0.027f, 0.052f, 1f);
        private static readonly Color PanelColor = new(0.018f, 0.065f, 0.108f, 0.99f);
        private static readonly Color PanelDarkColor = new(0.009f, 0.035f, 0.061f, 0.99f);
        private static readonly Color CardColor = new(0.024f, 0.086f, 0.139f, 0.97f);
        private static readonly Color BorderColor = new(0.28f, 0.46f, 0.62f, 1f);
        private static readonly Color DividerColor = new(0.14f, 0.31f, 0.45f, 1f);
        private static readonly Color AccentColor = new(0.13f, 0.55f, 0.92f, 1f);
        private static readonly Color BrightAccentColor = new(0.12f, 0.67f, 1f, 1f);
        private static readonly Color RoleColor = new(0.27f, 0.77f, 0.47f, 1f);
        private static readonly Color GoldColor = new(0.95f, 0.69f, 0.22f, 1f);
        private static readonly Color WarningColor = new(0.94f, 0.56f, 0.16f, 1f);
        private static readonly Color PrimaryTextColor = new(0.94f, 0.97f, 1f, 1f);
        private static readonly Color SecondaryTextColor = new(0.62f, 0.71f, 0.8f, 1f);
        private static readonly Color MutedColor = new(0.34f, 0.40f, 0.49f, 1f);

        private readonly PlayerProfileViewBuilder _viewBuilder = new();
        private CareerManager _manager;
        private RectTransform _content;
        private PlayerDetailTab _selectedTab;

        public override bool BlocksLowerInput => true;
        public CareerMainTab MainTab => CareerMainTab.Player;

        /// <summary>프리팹이 없는 프로토타입 환경에서 선수 화면을 런타임 생성한다.</summary>
        public static UI_Scene_Player CreateRuntime(Transform parent)
        {
            var screenObject = new GameObject(nameof(UI_Scene_Player), typeof(RectTransform), typeof(CanvasGroup));
            screenObject.transform.SetParent(parent, false);
            UI_Scene_Player screen = screenObject.AddComponent<UI_Scene_Player>();
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

            CareerDashboardView dashboard = _manager.Dashboard;
            CareerGrowthView growth = _manager.GrowthDashboard;
            if (dashboard == null)
                return;
            PlayerGameRole plannedRole = dashboard.NextGame?.PlannedRole ?? PlayerGameRole.Inactive;
            PlayerProfileView view = _viewBuilder.Build(
                _manager.CurrentCareer,
                dashboard.Overall,
                plannedRole,
                growth);

            ClearChildren(_content);
            RenderBackgroundAccents();
            RenderTopBar(dashboard);
            RenderPageHeader();
            RenderPlayerCard(view);
            RenderSubTabs();
            RenderSelectedTab(view);
            CareerNavigationChrome.Create(_content, CareerMainTab.Player);
        }

        private void RenderBackgroundAccents()
        {
            CreateImage("TopGlow", _content, new Color(0.02f, 0.18f, 0.31f, 0.24f),
                new Vector2(1920f, 5f), new Vector2(0f, 456f));
            CreateImage("BottomGlow", _content, new Color(0.02f, 0.16f, 0.28f, 0.2f),
                new Vector2(1920f, 4f), new Vector2(0f, -443f));
        }

        private void RenderPageHeader()
        {
            CreateText("PageTitle", _content, "선수", 31, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(170f, 48f), new Vector2(-855f, 421f), PrimaryTextColor);
            CreateText("PageDescription", _content, "현재 상태와 능력, 성장 근거를 한눈에 확인합니다.", 15,
                FontStyle.Normal, TextAnchor.MiddleLeft, new Vector2(530f, 36f),
                new Vector2(-590f, 421f), SecondaryTextColor);
        }

        private void RenderTopBar(CareerDashboardView view)
        {
            RectTransform bar = CreateImage("TopBar", _content, TopBarColor,
                new Vector2(1920f, 80f), new Vector2(0f, 500f));
            CreateImage("TopBarBottom", bar, BorderColor, new Vector2(1920f, 2f), new Vector2(0f, -39f));
            Text logo = CreateText("Logo", bar, "UPlayBall", 34, FontStyle.BoldAndItalic,
                TextAnchor.MiddleLeft, new Vector2(310f, 50f), new Vector2(-800f, 5f), PrimaryTextColor);
            AddTextOutline(logo, new Color(0.05f, 0.34f, 0.62f, 0.9f), 1.5f);
            CreateText("LogoCaption", bar, "BASEBALL CAREER", 10, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Vector2(230f, 18f), new Vector2(-796f, -23f), AccentColor);
            CreateTopBarSegment(bar, "LEAGUE", $"{view.SeasonYear}  {GetLeagueLabel(view.LeagueLevel)} LEAGUE",
                new Vector2(-355f, 0f), new Vector2(430f, 64f));
            string date = view.NextGame.HasValue
                ? $"{view.NextGame.Value.Date:M월 d일} ({GetKoreanDayOfWeek(view.NextGame.Value.Date.DayOfWeek)})"
                : "시즌 일정 종료";
            CreateTopBarSegment(bar, "DATE", date, new Vector2(40f, 0f), new Vector2(310f, 64f));
            CreateTopBarSegment(bar, "MONEY", FormatMoney(view.AvailableMoney),
                new Vector2(405f, 0f), new Vector2(360f, 64f));
            CreateText("Status", bar, GetSeasonPhaseLabel(view.SeasonPhase), 15, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(170f, 42f), new Vector2(720f, 0f), RoleColor);
        }

        private static void CreateTopBarSegment(
            Transform parent, string eyebrow, string value, Vector2 position, Vector2 size)
        {
            RectTransform segment = CreateImage(eyebrow + "Segment", parent,
                new Color(0.02f, 0.07f, 0.12f, 0.76f), size, position);
            CreateImage("LeftDivider", segment, DividerColor, new Vector2(2f, size.y - 10f),
                new Vector2(-size.x * 0.5f + 1f, 0f));
            CreateText("Eyebrow", segment, eyebrow, 10, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(size.x - 42f, 18f), new Vector2(14f, 15f), AccentColor);
            CreateText("Value", segment, value, 20, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(size.x - 42f, 31f), new Vector2(14f, -8f), PrimaryTextColor);
        }

        private void RenderSubTabs()
        {
            string[] labels = { "선수 정보", "능력치", "성장판", "스킬", "경력" };
            const float width = 274f;
            const float detailContentCenterX = 205f;
            for (int index = 0; index < labels.Length; index++)
            {
                var tab = (PlayerDetailTab)index;
                bool selected = tab == _selectedTab;
                float x = detailContentCenterX + (index - (labels.Length - 1) * 0.5f) * width;
                Button button = CreateButton("DetailTab_" + tab, _content, labels[index],
                    new Vector2(width - 2f, 50f), new Vector2(x, 365f),
                    selected ? new Color(0.025f, 0.25f, 0.49f, 1f) : PanelDarkColor, out Text label);
                label.fontSize = 17;
                label.color = selected ? PrimaryTextColor : SecondaryTextColor;
                if (selected)
                    CreateImage("Active", button.transform, BrightAccentColor,
                        new Vector2(width - 18f, 3f), new Vector2(0f, 23f));
                button.onClick.AddListener(() =>
                {
                    _selectedTab = tab;
                    Render();
                });
            }
        }

        private void RenderSelectedTab(PlayerProfileView view)
        {
            switch (_selectedTab)
            {
                case PlayerDetailTab.Attributes:
                    RenderAttributesPage(view);
                    break;
                case PlayerDetailTab.Board:
                    RenderBoardPage(view);
                    break;
                case PlayerDetailTab.Skills:
                    RenderSkillsPage(view);
                    break;
                case PlayerDetailTab.Career:
                    RenderCareerPage(view);
                    break;
                default:
                    RenderProfilePage(view);
                    break;
            }
        }

        private enum PlayerDetailTab
        {
            Profile,
            Attributes,
            Board,
            Skills,
            Career
        }
    }
}
