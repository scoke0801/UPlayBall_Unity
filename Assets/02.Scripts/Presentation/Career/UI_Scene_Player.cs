using System;
using Baseball.Core.Teams;
using Baseball.Game.Career;
using Baseball.Game.Manager;
using Baseball.Presentation.SharedScreens;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Career
{
    /// <summary>내 선수의 현재 상태·능력·성장 근거·커리어를 읽는 선수 상세 화면이다.</summary>
    public sealed partial class UI_Scene_Player : UISceneBase, ICareerTabScreen
    {
        private static readonly Color BackgroundColor = CareerUiTheme.Background;
        private static readonly Color TopBarColor = CareerUiTheme.TopBar;
        private static readonly Color PanelColor = CareerUiTheme.Panel;
        private static readonly Color PanelDarkColor = CareerUiTheme.PanelDark;
        private static readonly Color CardColor = CareerUiTheme.Surface;
        private static readonly Color BorderColor = CareerUiTheme.Border;
        private static readonly Color DividerColor = CareerUiTheme.Divider;
        private static readonly Color AccentColor = CareerUiTheme.Primary;
        private static readonly Color BrightAccentColor = CareerUiTheme.PrimaryBright;
        private static readonly Color RoleColor = CareerUiTheme.Success;
        private static readonly Color GoldColor = CareerUiTheme.AccentGold;
        private static readonly Color WarningColor = CareerUiTheme.Warning;
        private static readonly Color PrimaryTextColor = CareerUiTheme.TextPrimary;
        private static readonly Color SecondaryTextColor = CareerUiTheme.TextSecondary;
        private static readonly Color MutedColor = CareerUiTheme.TextMuted;
        private static readonly Vector2 SharedShellWorkspaceOffset = new(
            0f,
            -(CareerUiTheme.SharedShellChromeHeight * 0.5f + CareerUiTheme.Space2));

        private readonly PlayerProfileViewBuilder _viewBuilder = new();
        private CareerManager _manager;
        private RectTransform _content;
        private PlayerDetailTab _selectedTab;

        public override bool BlocksLowerInput => true;
        public CareerMainTab MainTab => CareerMainTab.Player;

        /// <summary>현재 화면과 공용 선수 상세 화면이 함께 소비하는 읽기 전용 Snapshot이다.</summary>
        public PlayerDetailSnapshot CurrentDetailSnapshot { get; private set; }

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
            _content = CreateRect("Content", root, new Vector2(1920f, 1080f), SharedShellWorkspaceOffset);
        }

        private void HandleCareerChanged()
        {
            if (!_manager.HasActiveCareer)
            {
                CurrentDetailSnapshot = null;
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
            CurrentDetailSnapshot = CareerPlayerDetailSnapshotAdapter.Create(view);

            ClearChildren(_content);
            RenderBackgroundAccents();
            RenderPlayerCard(view);
            RenderSubTabs();
            RenderSelectedTab(view, CurrentDetailSnapshot);
            RenderSharedDetailContext(CurrentDetailSnapshot);
        }

        private void RenderBackgroundAccents()
        {
            CreateImage("TopGlow", _content, CareerUiTheme.TopGlow,
                new Vector2(1920f, 5f), new Vector2(0f, 456f));
            CreateImage("BottomGlow", _content, CareerUiTheme.BottomGlow,
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
            CreateText("LogoCaption", bar, "프로야구 선수 커리어", 10, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Vector2(230f, 18f), new Vector2(-796f, -23f), AccentColor);
            CreateTopBarSegment(bar, "리그", $"{view.SeasonYear}  {GetLeagueLabel(view.LeagueLevel)} 리그",
                new Vector2(-355f, 0f), new Vector2(430f, 64f));
            string date = view.NextGame.HasValue
                ? $"{view.NextGame.Value.Date:M월 d일} ({GetKoreanDayOfWeek(view.NextGame.Value.Date.DayOfWeek)})"
                : "시즌 일정 종료";
            CreateTopBarSegment(bar, "날짜", date, new Vector2(40f, 0f), new Vector2(310f, 64f));
            CreateTopBarSegment(bar, "보유 자금", FormatMoney(view.AvailableMoney),
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
                button.onClick.AddListener(() =>
                {
                    _selectedTab = tab;
                    Render();
                });
            }
        }

        private void RenderSelectedTab(PlayerProfileView view, PlayerDetailSnapshot detail)
        {
            switch (_selectedTab)
            {
                case PlayerDetailTab.Attributes:
                    RenderSharedAbilityPage(detail);
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

        /// <summary>공용 Snapshot의 동일한 식별·상태 값을 Career 전용 화면 상단에 표시한다.</summary>
        private void RenderSharedDetailContext(PlayerDetailSnapshot detail)
        {
            if (detail == null)
                return;

            RectTransform strip = CreateImage(
                "SharedPlayerDetailContext",
                _content,
                CareerUiTheme.SurfaceSubtle,
                new Vector2(1390f, 44f),
                new Vector2(205f, 421f));
            CreateImage(
                "Accent",
                strip,
                CareerUiTheme.Primary,
                new Vector2(5f, 44f),
                new Vector2(-692f, 0f));
            string identity =
                $"{detail.DisplayName}  ·  {detail.TeamName}  ·  {detail.PositionLabel}  ·  {detail.SeasonLabel} 시즌";
            CreateText(
                "Identity",
                strip,
                identity,
                17,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                new Vector2(760f, 38f),
                new Vector2(-285f, 0f),
                PrimaryTextColor);
            string condition = FindDetailValue(detail, "Condition");
            string fatigue = FindDetailValue(detail, "Fatigue");
            CreateText(
                "Availability",
                strip,
                $"컨디션 {condition}  ·  피로 {fatigue}",
                15,
                FontStyle.Bold,
                TextAnchor.MiddleRight,
                new Vector2(460f, 38f),
                new Vector2(430f, 0f),
                SecondaryTextColor);
        }

        /// <summary>모드 중립 능력치 표를 공용 RecordTable View로 표시한다.</summary>
        private void RenderSharedAbilityPage(PlayerDetailSnapshot detail)
        {
            RectTransform page = CreateRect(
                "SharedAbilityPage",
                _content,
                new Vector2(1390f, 720f),
                new Vector2(205f, -20f));
            RectTransform panel = CreatePanel(
                "SharedAbilityTablePanel",
                page,
                "능력치 · 현재 / 기본 / 성장 근거",
                new Vector2(1320f, 650f),
                Vector2.zero);
            RectTransform tableHost = CreateRect(
                "SharedAbilityTableHost",
                panel,
                new Vector2(1240f, 520f),
                new Vector2(0f, -30f));
            CompactRecordTableView table = CompactRecordTableView.CreateRuntime(
                tableHost,
                "PlayerAbilityRecordTable");
            table.Bind(detail.AbilityTable);
        }

        private static string FindDetailValue(PlayerDetailSnapshot detail, string valueId)
        {
            for (int index = 0; index < detail.SummaryValues.Count; index++)
            {
                DetailValueModel value = detail.SummaryValues[index];
                if (string.Equals(value.ValueId, valueId, StringComparison.Ordinal))
                    return value.Value;
            }
            return "-";
        }

        /// <summary>Shared Shell의 선수 Local Navigation이 선택할 기존 상세 영역이다.</summary>
        public enum PlayerDetailTab
        {
            Profile,
            Attributes,
            Board,
            Skills,
            Career
        }

        /// <summary>Shared Shell Route와 기존 선수 화면의 상세 영역을 동기화한다.</summary>
        public void SelectDetailTab(PlayerDetailTab tab)
        {
            if (_selectedTab == tab)
                return;

            _selectedTab = tab;
            if (IsVisible)
                Render();
        }
    }
}
