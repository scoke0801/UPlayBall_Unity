using System;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Career
{
    /// <summary>선수 커리어 화면의 고정된 하단 8개 탭을 식별한다.</summary>
    public enum CareerMainTab
    {
        Home,
        Player,
        Growth,
        Schedule,
        League,
        Team,
        Records,
        Contract
    }

    /// <summary>각 메뉴 화면이 중앙 라우터에 자신이 담당하는 탭을 알리는 계약이다.</summary>
    public interface ICareerTabScreen
    {
        CareerMainTab MainTab { get; }
    }

    /// <summary>모든 커리어 메뉴에 하단 탭과 공통 설정 진입점을 같은 위치로 배치한다.</summary>
    public static class CareerNavigationChrome
    {
        /// <summary>현재 메뉴 탭바와 전역 설정 버튼을 함께 생성한다.</summary>
        public static void Create(Transform parent, CareerMainTab activeTab)
        {
            CareerTabBar.Create(parent, activeTab);
            CreateSettingsButton(parent);
        }

        private static void CreateSettingsButton(Transform parent)
        {
            var buttonObject = new GameObject("CareerSettings", typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(100f, 44f);
            rect.anchoredPosition = new Vector2(890f, 500f);

            Image image = buttonObject.GetComponent<Image>();
            image.color = CareerUiTheme.PanelDark;
            MarkVisual(rect, CareerUiVisualRole.InteractiveControl);
            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.highlightedColor = CareerUiTheme.SurfaceSelected;
            colors.pressedColor = CareerUiTheme.Background;
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            button.onClick.AddListener(() => UI_Popup_CareerSettings.ShowRuntime());

            Text label = CreateText(
                "Label", rect, "설정", 14, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(90f, 36f), Vector2.zero, CareerUiTheme.TextSecondary);
            label.raycastTarget = false;
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
            Color color)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            Text text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private static void MarkVisual(RectTransform target, CareerUiVisualRole role)
        {
            CareerUiVisualElement visual = target.gameObject.AddComponent<CareerUiVisualElement>();
            visual.Initialize(role);
        }
    }

    /// <summary>등록된 커리어 화면을 찾아 현재 탭만 보이게 전환한다.</summary>
    public static class CareerTabNavigation
    {
        public static bool Show(CareerMainTab tab)
        {
            UIBase[] screens = UnityEngine.Object.FindObjectsByType<UIBase>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            UIBase target = null;
            for (int index = 0; index < screens.Length; index++)
            {
                if (screens[index] is ICareerTabScreen careerScreen && careerScreen.MainTab == tab)
                {
                    target = screens[index];
                    break;
                }
            }
            if (target == null)
                return false;

            for (int index = 0; index < screens.Length; index++)
            {
                if (screens[index] != target && screens[index] is ICareerTabScreen)
                    screens[index].Hide();
            }
            target.Show();
            return true;
        }
    }

    /// <summary>모든 커리어 메뉴가 재사용하는 하단 탭바를 런타임 생성한다.</summary>
    public static class CareerTabBar
    {
        private static readonly Color BarColor = CareerUiTheme.TopBar;
        private static readonly Color BorderColor = CareerUiTheme.Border;
        private static readonly Color ActiveColor = CareerUiTheme.SurfaceSelected;
        private static readonly Color InactiveColor = CareerUiTheme.PanelDark;
        private static readonly Color AccentColor = CareerUiTheme.PrimaryBright;
        private static readonly Color PrimaryTextColor = CareerUiTheme.TextPrimary;
        private static readonly Color SecondaryTextColor = CareerUiTheme.TextSecondary;
        private static readonly Color MutedColor = CareerUiTheme.TextMuted;

        private static readonly string[] Labels =
            { "홈", "선수", "성장", "일정", "리그", "구단", "기록", "계약" };

        private static readonly string[] Icons =
            { "HOME", "PLAYER", "GROW", "DATE", "LEAGUE", "TEAM", "RECORD", "DEAL" };

        public static void Create(Transform parent, CareerMainTab activeTab)
        {
            RectTransform bar = CreateImage(
                "Tabs", parent, BarColor, new Vector2(1920f, 94f), new Vector2(0f, -493f));
            MarkVisual(bar, CareerUiVisualRole.FlatSurface);
            RectTransform topDivider = CreateImage(
                "TabsTop", bar, BorderColor, new Vector2(1920f, 2f), new Vector2(0f, 46f));
            MarkVisual(topDivider, CareerUiVisualRole.Divider);
            const float tabWidth = 240f;
            for (int index = 0; index < Labels.Length; index++)
            {
                var tabId = (CareerMainTab)index;
                bool isActive = tabId == activeTab;
                float x = -840f + index * tabWidth;
                RectTransform tab = CreateImage(
                    "Tab_" + Labels[index],
                    bar,
                    isActive ? ActiveColor : InactiveColor,
                    new Vector2(tabWidth - 2f, 86f),
                    new Vector2(x, -2f));
                MarkVisual(tab, CareerUiVisualRole.InteractiveControl);
                if (isActive)
                {
                    RectTransform activeIndicator = CreateImage(
                        "ActiveGlow", tab, AccentColor, new Vector2(tabWidth - 18f, 4f),
                        new Vector2(0f, 41f));
                    MarkVisual(activeIndicator, CareerUiVisualRole.Divider);
                }

                Text icon = CreateText(
                    "Icon", tab, Icons[index], 10, FontStyle.Bold, TextAnchor.MiddleCenter,
                    new Vector2(110f, 20f), new Vector2(0f, 16f), isActive ? AccentColor : MutedColor);
                Text label = CreateText(
                    "Label", tab, Labels[index], 18, FontStyle.Bold, TextAnchor.MiddleCenter,
                    new Vector2(150f, 32f), new Vector2(0f, -15f),
                    isActive ? PrimaryTextColor : SecondaryTextColor);
                icon.raycastTarget = false;
                label.raycastTarget = false;

                Button button = tab.gameObject.AddComponent<Button>();
                tab.GetComponent<Image>().raycastTarget = true;
                ColorBlock colors = button.colors;
                colors.highlightedColor = Color.Lerp(isActive ? ActiveColor : InactiveColor, Color.white, 0.12f);
                colors.pressedColor = Color.Lerp(isActive ? ActiveColor : InactiveColor, Color.black, 0.18f);
                colors.selectedColor = colors.highlightedColor;
                button.colors = colors;
                button.onClick.AddListener(() => CareerTabNavigation.Show(tabId));
            }
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

        private static void MarkVisual(RectTransform target, CareerUiVisualRole role)
        {
            CareerUiVisualElement visual = target.gameObject.AddComponent<CareerUiVisualElement>();
            visual.Initialize(role);
        }

        private static RectTransform CreateImage(
            string name,
            Transform parent,
            Color color,
            Vector2 size,
            Vector2 position)
        {
            RectTransform rect = CreateRect(name, parent, size, position);
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
            Color color)
        {
            RectTransform rect = CreateRect(name, parent, size, position);
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
    }
}
