using System;
using Baseball.Game.Career;
using Baseball.Game.Manager;
using Baseball.Presentation.SharedScreens;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Baseball.Presentation.Career
{
    /// <summary>월간 달력·목록·홈/원정 스플릿으로 전체 시즌 일정을 조회한다.</summary>
    public sealed partial class UI_Scene_CareerSchedule : UISceneBase, ICareerTabScreen
    {
        private enum ScheduleLayout
        {
            Calendar,
            List,
            Split
        }

        private static readonly Color BackgroundColor = CareerUiTheme.Background;
        private static readonly Color TopBarColor = CareerUiTheme.TopBar;
        private static readonly Color PanelColor = CareerUiTheme.Panel;
        private static readonly Color PanelDarkColor = CareerUiTheme.PanelDark;
        private static readonly Color CardColor = CareerUiTheme.Surface;
        private static readonly Color BorderColor = CareerUiTheme.Border;
        private static readonly Color DividerColor = CareerUiTheme.Divider;
        private static readonly Color AccentColor = CareerUiTheme.Primary;
        private static readonly Color BrightAccentColor = CareerUiTheme.PrimaryBright;
        private static readonly Color GoldColor = CareerUiTheme.AccentGold;
        private static readonly Color WinColor = CareerUiTheme.Success;
        private static readonly Color LossColor = CareerUiTheme.Loss;
        private static readonly Color TieColor = CareerUiTheme.TextSecondary;
        private static readonly Color PrimaryTextColor = CareerUiTheme.TextPrimary;
        private static readonly Color SecondaryTextColor = CareerUiTheme.TextSecondary;
        private static readonly Color MutedColor = CareerUiTheme.TextMuted;
        private static readonly Vector4 ScheduleFramePadding = new(24f, 52f, 24f, 72f);
        private static readonly Vector2 SharedShellWorkspaceOffset = new(
            0f,
            -(CareerUiTheme.SharedShellChromeHeight * 0.5f + CareerUiTheme.Space2));

        private CareerManager _manager;
        private RectTransform _content;
        private ScheduleLayout _layout = ScheduleLayout.Calendar;
        private CareerScheduleScope _scope = CareerScheduleScope.MyTeam;
        private DateTime _visibleMonth;
        private int _visibleSeasonYear;
        private bool _hasVisibleMonth;

        public override bool BlocksLowerInput => true;
        public CareerMainTab MainTab => CareerMainTab.Schedule;

        /// <summary>프리팹이 없는 프로토타입 환경에서 일정 화면을 런타임 생성한다.</summary>
        public static UI_Scene_CareerSchedule CreateRuntime(Transform parent)
        {
            var screenObject = new GameObject(
                nameof(UI_Scene_CareerSchedule),
                typeof(RectTransform),
                typeof(CanvasGroup));
            screenObject.transform.SetParent(parent, false);
            UI_Scene_CareerSchedule screen = screenObject.AddComponent<UI_Scene_CareerSchedule>();
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

        private void Update()
        {
            if (!IsVisible || Keyboard.current == null || _manager?.HasActiveCareer != true)
                return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard.leftArrowKey.wasPressedThisFrame)
                MoveMonth(-1);
            else if (keyboard.rightArrowKey.wasPressedThisFrame)
                MoveMonth(1);
            else if (keyboard.homeKey.wasPressedThisFrame)
                MoveToCurrentMonth();
        }

        private void BuildHierarchy()
        {
            RectTransform root = (RectTransform)transform;
            Stretch(root);
            CreateImage("Background", root, BackgroundColor, Vector2.zero, Vector2.zero, true);
            _content = CreateRect("Content", root, new Vector2(1920f, 1080f), SharedShellWorkspaceOffset);
        }

        private void HandleCareerChanged()
        {
            if (_manager?.HasActiveCareer != true)
            {
                Hide();
                return;
            }
            if (IsVisible)
                Render();
        }

        private void Render()
        {
            if (_content == null || _manager?.HasActiveCareer != true)
                return;

            CareerScheduleView view = _manager.Schedule;
            if (view == null)
                return;
            ScheduleScreenSnapshot snapshot = CareerScheduleSnapshotAdapter.Create(view);
            EnsureVisibleMonth(view);
            CareerScheduleMonthView month = view.BuildMonth(
                _visibleMonth.Year,
                _visibleMonth.Month,
                _scope);

            ClearChildren(_content);
            RenderBackgroundAccents();
            RenderViewControls(view);
            if (_layout == ScheduleLayout.Calendar)
                RenderCalendar(month);
            else if (_layout == ScheduleLayout.List)
            {
                if (_scope == CareerScheduleScope.MyTeam)
                    RenderSharedScheduleList(snapshot);
                else
                    RenderList(month);
            }
            else
                RenderSplit(month);
            RenderTeamSummary(view, month);
            RenderScopeAndLegend(view);
        }

        private void RenderSharedScheduleList(ScheduleScreenSnapshot snapshot)
        {
            RectTransform panel = CreateFrame(
                "ScheduleList", _content, new Vector2(1320f, 570f), new Vector2(-270f, -4f), PanelDarkColor);
            CreateText(
                "Title", panel, "내 구단 월간 일정", 18, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(600f, 34f), new Vector2(-330f, 200f), PrimaryTextColor);
            CreateText(
                "Description", panel, "완료 결과와 앞으로의 대진을 한 흐름으로 확인합니다.",
                12, FontStyle.Normal, TextAnchor.MiddleRight,
                new Vector2(600f, 30f), new Vector2(330f, 200f), SecondaryTextColor);
            CreateImage("HeaderLine", panel, DividerColor, new Vector2(1240f, 2f), new Vector2(0f, 178f));

            RecordTableModel tableModel = ScheduleRecordTableBuilder.CreateFocusedMonth(
                snapshot,
                _visibleMonth.Year,
                _visibleMonth.Month);
            RecordTableView table = RecordTableView.CreateRuntime(
                panel,
                new Vector2(1240f, 350f),
                new Vector2(0f, -18f),
                "SharedScheduleTable");
            UiContentStateModel state = tableModel.Rows.Count > 0
                ? UiContentStateModel.Ready
                : UiContentStateModel.CreateEmpty("일정 없음", "이 달에는 표시할 경기가 없습니다.");
            table.Bind(tableModel, state);
        }

        private void EnsureVisibleMonth(CareerScheduleView view)
        {
            if (!_hasVisibleMonth || _visibleSeasonYear != view.SeasonYear)
            {
                _visibleMonth = new DateTime(view.CurrentDate.Year, view.CurrentDate.Month, 1);
                _visibleSeasonYear = view.SeasonYear;
                _hasVisibleMonth = true;
            }

            DateTime firstMonth = new(view.SeasonStartDate.Year, view.SeasonStartDate.Month, 1);
            DateTime lastMonth = new(view.SeasonEndDate.Year, view.SeasonEndDate.Month, 1);
            if (_visibleMonth < firstMonth) _visibleMonth = firstMonth;
            if (_visibleMonth > lastMonth) _visibleMonth = lastMonth;
        }

        private void SetLayout(ScheduleLayout layout)
        {
            if (_layout == layout)
                return;
            _layout = layout;
            Render();
        }

        private void SetScope(CareerScheduleScope scope)
        {
            if (_scope == scope)
                return;
            _scope = scope;
            Render();
        }

        private void MoveMonth(int offset)
        {
            CareerScheduleView view = _manager?.Schedule;
            if (view == null)
                return;
            DateTime target = _visibleMonth.AddMonths(offset);
            DateTime firstMonth = new(view.SeasonStartDate.Year, view.SeasonStartDate.Month, 1);
            DateTime lastMonth = new(view.SeasonEndDate.Year, view.SeasonEndDate.Month, 1);
            if (target < firstMonth || target > lastMonth)
                return;
            _visibleMonth = target;
            Render();
        }

        private void MoveToCurrentMonth()
        {
            CareerScheduleView view = _manager?.Schedule;
            if (view == null)
                return;
            _visibleMonth = new DateTime(view.CurrentDate.Year, view.CurrentDate.Month, 1);
            Render();
        }

        private void ResetFilters()
        {
            _scope = CareerScheduleScope.MyTeam;
            _layout = ScheduleLayout.Calendar;
            MoveToCurrentMonth();
        }

        private void RenderBackgroundAccents()
        {
            CreateImage("TopGlow", _content, CareerUiTheme.TopGlow,
                new Vector2(1920f, 5f), new Vector2(0f, 456f));
            CreateImage("BottomGlow", _content, CareerUiTheme.BottomGlow,
                new Vector2(1920f, 4f), new Vector2(0f, -443f));
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
            MarkVisual(rect, CareerUiVisualRole.FramedControl);
            Button button = rect.gameObject.AddComponent<Button>();
            rect.GetComponent<Image>().raycastTarget = true;
            ColorBlock colors = button.colors;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.12f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = Color.Lerp(color, BackgroundColor, 0.55f);
            button.colors = colors;
            text = CreateText("Label", rect, label, 17, FontStyle.Bold, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.zero, PrimaryTextColor, true);
            CareerUiSkin.ApplyButton(button);
            return button;
        }

        private static RectTransform CreateFrame(
            string name,
            Transform parent,
            Vector2 size,
            Vector2 position,
            Color surfaceColor)
        {
            RectTransform root = CreateRect(name, parent, size, position);
            bool isTopLevelFrame = parent.name == "Content" && size.y >= 500f;
            if (!isTopLevelFrame)
            {
                RectTransform surface = CreateImage(
                    "FlatSurface", root, surfaceColor, Vector2.zero, Vector2.zero, true);
                MarkVisual(surface, CareerUiVisualRole.FlatSurface);
                return root;
            }

            RectTransform decorativeFrame = CreateImage(
                "DecorativeFrame", root, Color.white, Vector2.zero, Vector2.zero, true);
            MarkVisual(decorativeFrame, CareerUiVisualRole.DecorativeFrame);
            RectTransform content = CreateRect("ContentSafeArea", root, size, Vector2.zero);
            RectTransform header = CreateRect("HeaderRoot", root, size, Vector2.zero);
            RectTransform interaction = CreateRect("InteractionRoot", root, size, Vector2.zero);
            CareerUiFrame.ApplyContentPadding(content, size, ScheduleFramePadding);
            CareerUiFrame.ApplyContentPadding(interaction, size, ScheduleFramePadding);
            content.gameObject.AddComponent<RectMask2D>();
            CareerUiFrame frame = root.gameObject.AddComponent<CareerUiFrame>();
            frame.Initialize(
                decorativeFrame.GetComponent<Image>(), header, content, interaction,
                ScheduleFramePadding, false);
            return content;
        }

        private static RectTransform CreateFramedSurface(
            string name,
            Transform parent,
            Vector2 size,
            Vector2 position,
            Color surfaceColor)
        {
            RectTransform root = CreateRect(name, parent, size, position);
            RectTransform surface = CreateImage(
                "FramedSurface", root, surfaceColor, Vector2.zero, Vector2.zero, true);
            MarkVisual(surface, CareerUiVisualRole.FramedSurface);
            return root;
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
