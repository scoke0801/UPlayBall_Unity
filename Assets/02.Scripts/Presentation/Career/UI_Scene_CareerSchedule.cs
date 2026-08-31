using System;
using Baseball.Game.Career;
using Baseball.Game.Manager;
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

        private static readonly Color BackgroundColor = new(0.006f, 0.02f, 0.034f, 1f);
        private static readonly Color TopBarColor = new(0.008f, 0.027f, 0.052f, 1f);
        private static readonly Color PanelColor = new(0.018f, 0.065f, 0.108f, 0.99f);
        private static readonly Color PanelDarkColor = new(0.009f, 0.035f, 0.061f, 0.99f);
        private static readonly Color CardColor = new(0.024f, 0.086f, 0.139f, 0.97f);
        private static readonly Color BorderColor = new(0.28f, 0.46f, 0.62f, 1f);
        private static readonly Color DividerColor = new(0.14f, 0.31f, 0.45f, 1f);
        private static readonly Color AccentColor = new(0.13f, 0.55f, 0.92f, 1f);
        private static readonly Color BrightAccentColor = new(0.12f, 0.67f, 1f, 1f);
        private static readonly Color GoldColor = new(0.95f, 0.69f, 0.22f, 1f);
        private static readonly Color WinColor = new(0.25f, 0.78f, 0.50f, 1f);
        private static readonly Color LossColor = new(0.95f, 0.32f, 0.38f, 1f);
        private static readonly Color TieColor = new(0.86f, 0.69f, 0.34f, 1f);
        private static readonly Color PrimaryTextColor = new(0.94f, 0.97f, 1f, 1f);
        private static readonly Color SecondaryTextColor = new(0.62f, 0.71f, 0.8f, 1f);
        private static readonly Color MutedColor = new(0.34f, 0.40f, 0.49f, 1f);
        private static readonly Vector4 ScheduleFramePadding = new(24f, 52f, 24f, 72f);

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
            _content = CreateRect("Content", root, new Vector2(1920f, 1080f), Vector2.zero);
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
            EnsureVisibleMonth(view);
            CareerScheduleMonthView month = view.BuildMonth(
                _visibleMonth.Year,
                _visibleMonth.Month,
                _scope);

            ClearChildren(_content);
            RenderBackgroundAccents();
            RenderTopBar(view);
            RenderScreenHeader();
            RenderViewControls(view);
            if (_layout == ScheduleLayout.Calendar)
                RenderCalendar(month);
            else if (_layout == ScheduleLayout.List)
                RenderList(month);
            else
                RenderSplit(month);
            RenderTeamSummary(view, month);
            RenderScopeAndLegend(view);
            CareerNavigationChrome.Create(_content, CareerMainTab.Schedule);
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
            CreateImage("TopGlow", _content, new Color(0.02f, 0.18f, 0.31f, 0.24f),
                new Vector2(1920f, 5f), new Vector2(0f, 456f));
            CreateImage("BottomGlow", _content, new Color(0.02f, 0.16f, 0.28f, 0.20f),
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
