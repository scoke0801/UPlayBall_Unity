using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.SharedUI
{
    public sealed partial class SharedGameShellView
    {
        private const float TopBarHeight = 68f;
        private const float NavigationHeight = 48f;
        private const float ContextHeaderHeight = 48f;
        private const float ActionBarHeight = 60f;
        private const float InspectorWidth = 364f;
        private const float WorkspaceGap = 12f;

        private static Color Background => CareerUiTheme.Background;
        private static Color TopBar => CareerUiTheme.TopBar;
        private static Color NavigationSurface => CareerUiTheme.PanelDark;
        private static Color ContextSurface => CareerUiTheme.ContextSurface;
        private static Color WorkspaceSurface => CareerUiTheme.Panel;
        private static Color InspectorSurface => CareerUiTheme.SurfaceSubtle;
        private static Color ActionSurface => CareerUiTheme.PanelDark;
        private static Color NavigationColor => CareerUiTheme.SurfaceSubtle;
        private static Color NavigationSelectedColor => CareerUiTheme.SurfaceSelected;
        private static Color SubTabColor => CareerUiTheme.Surface;
        private static Color StatusSurface => CareerUiTheme.PanelDark;
        private static Color Border => CareerUiTheme.Border;
        private static Color TextPrimary => CareerUiTheme.TextPrimary;
        private static Color TextSecondary => CareerUiTheme.TextSecondary;
        private static Color TextMuted => CareerUiTheme.TextMuted;
        private static Color DarkText => CareerUiTheme.TextOnLight;
        private static Color Accent => CareerUiTheme.Primary;
        private static Color AccentLight => CareerUiTheme.PrimaryBright;
        private static Color Positive => CareerUiTheme.Success;
        private static Color Warning => CareerUiTheme.Warning;
        private static Color Critical => CareerUiTheme.Error;

        private static Font _defaultFont;

        private void EnsureHierarchy()
        {
            if (_mainWorkspaceHost != null)
                return;

            RectTransform root = GetComponent<RectTransform>();
            Stretch(root);
            _rootBackground = GetComponent<Image>();
            if (_rootBackground == null)
                _rootBackground = gameObject.AddComponent<Image>();
            _rootBackground.color = _isChromeOverlayMode ? Color.clear : Background;
            _rootBackground.raycastTarget = false;

            BuildGlobalTopBar(root);
            BuildPrimaryNavigation(root);
            BuildContextHeader(root);
            BuildWorkspace(root);
            BuildContextActionBar(root);
            BuildOverlaySlots(root);
            UpdateWorkspaceOffsets();
        }

        private void BuildGlobalTopBar(RectTransform root)
        {
            _globalTopBar = CreateAnchoredImage(
                "GlobalTopBar", root, TopBar, new Vector2(0f, 1f), Vector2.one,
                new Vector2(0f, -TopBarHeight), Vector2.zero);
            _globalTopBar.GetComponent<Image>().raycastTarget = true;
            AddBottomBorder(_globalTopBar);

            RectTransform brand = CreateRect("Brand", _globalTopBar);
            SetAnchors(brand, new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(20f, 0f), new Vector2(250f, 0f));
            Text logo = CreateText(
                "GameName", brand, "UPlayBall", 25, FontStyle.Bold,
                TextAnchor.MiddleLeft, TextPrimary);
            SetAnchors(logo.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, -20f));
            _modeNameText = CreateText(
                "ModeName", brand, string.Empty, 12, FontStyle.Bold,
                TextAnchor.LowerLeft, AccentLight);
            SetAnchors(_modeNameText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, 6f));

            RectTransform teamStatus = CreateRect("TeamStatus", _globalTopBar);
            SetAnchors(teamStatus, new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(270f, 0f), new Vector2(790f, 0f));
            _teamNameText = CreateText(
                "TeamName", teamStatus, string.Empty, 20, FontStyle.Bold,
                TextAnchor.MiddleLeft, TextPrimary);
            SetAnchors(_teamNameText.rectTransform, Vector2.zero, Vector2.one,
                Vector2.zero, new Vector2(0f, -22f));
            _commonStatusText = CreateText(
                "CommonStatus", teamStatus, string.Empty, 13, FontStyle.Normal,
                TextAnchor.LowerLeft, TextSecondary);
            SetAnchors(_commonStatusText.rectTransform, Vector2.zero, Vector2.one,
                Vector2.zero, new Vector2(0f, 6f));

            _nextMatchText = CreateText(
                "NextMatch", _globalTopBar, string.Empty, 14, FontStyle.Bold,
                TextAnchor.MiddleRight, TextSecondary);
            SetAnchors(_nextMatchText.rectTransform, new Vector2(0.41f, 0f), new Vector2(0.62f, 1f),
                new Vector2(8f, 0f), new Vector2(-8f, 0f));

            _statusSlotHost = CreateRect("ModeStatusSlots", _globalTopBar);
            SetAnchors(_statusSlotHost, new Vector2(0.63f, 0f), Vector2.one,
                new Vector2(0f, 7f), new Vector2(-78f, -7f));
            var layout = _statusSlotHost.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            RectTransform settingsRect = CreateAnchoredImage(
                "GlobalSettings", _globalTopBar, StatusSurface, new Vector2(1f, 0f), Vector2.one,
                new Vector2(-66f, 9f), new Vector2(-14f, -9f));
            Button settingsButton = settingsRect.gameObject.AddComponent<Button>();
            settingsButton.targetGraphic = settingsRect.GetComponent<Image>();
            settingsButton.colors = CreateButtonColors(false);
            settingsButton.onClick.AddListener(HandleSettingsRequested);
            Text settingsLabel = CreateText(
                "Label", settingsRect, "설정", 12, FontStyle.Bold, TextAnchor.MiddleCenter, TextSecondary);
            Stretch(settingsLabel.rectTransform);
        }

        private void BuildPrimaryNavigation(RectTransform root)
        {
            _primaryNavigation = CreateAnchoredImage(
                "PrimaryNavigation", root, NavigationSurface, new Vector2(0f, 1f), Vector2.one,
                new Vector2(0f, -(TopBarHeight + NavigationHeight)), new Vector2(0f, -TopBarHeight));
            _primaryNavigation.GetComponent<Image>().raycastTarget = true;
            AddBottomBorder(_primaryNavigation);

            _navigationEntryHost = CreateRect("Entries", _primaryNavigation);
            SetAnchors(_navigationEntryHost, Vector2.zero, Vector2.one,
                new Vector2(20f, 5f), new Vector2(-20f, -5f));
            var layout = _navigationEntryHost.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
        }

        private void BuildContextHeader(RectTransform root)
        {
            float topOffset = TopBarHeight + NavigationHeight;
            _contextHeader = CreateAnchoredImage(
                "ContextHeader", root, ContextSurface, new Vector2(0f, 1f), Vector2.one,
                new Vector2(0f, -(topOffset + ContextHeaderHeight)), new Vector2(0f, -topOffset));
            _contextHeader.GetComponent<Image>().raycastTarget = true;
            AddBottomBorder(_contextHeader);

            _contextTitleText = CreateText(
                "Title", _contextHeader, string.Empty, 19, FontStyle.Bold,
                TextAnchor.MiddleLeft, DarkText);
            SetAnchors(_contextTitleText.rectTransform, new Vector2(0f, 0f), new Vector2(0.31f, 1f),
                new Vector2(20f, 0f), new Vector2(-8f, 0f));

            _contextSummaryText = CreateText(
                "Summary", _contextHeader, string.Empty, 14, FontStyle.Normal,
                TextAnchor.MiddleLeft, Color.Lerp(DarkText, ContextSurface, 0.22f));
            SetAnchors(_contextSummaryText.rectTransform, new Vector2(0.31f, 0f), new Vector2(0.62f, 1f),
                new Vector2(0f, 0f), new Vector2(-10f, 0f));

            _subTabHost = CreateRect("SubTabs", _contextHeader);
            SetAnchors(_subTabHost, new Vector2(0.62f, 0f), Vector2.one,
                new Vector2(0f, 5f), new Vector2(-14f, -5f));
            var layout = _subTabHost.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 3f;
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
        }

        private void BuildWorkspace(RectTransform root)
        {
            _mainWorkspaceHost = CreateAnchoredImage(
                "MainWorkspaceHost", root, WorkspaceSurface, Vector2.zero, Vector2.one,
                new Vector2(16f, ActionBarHeight + 12f),
                new Vector2(-(InspectorWidth + WorkspaceGap + 16f), -(TopBarHeight + NavigationHeight + ContextHeaderHeight + 12f)));
            AddOutline(_mainWorkspaceHost.GetComponent<Image>());

            _rightInspectorHost = CreateAnchoredImage(
                "OptionalRightInspector", root, InspectorSurface, new Vector2(1f, 0f), Vector2.one,
                new Vector2(-(InspectorWidth + 16f), ActionBarHeight + 12f),
                new Vector2(-16f, -(TopBarHeight + NavigationHeight + ContextHeaderHeight + 12f)));
            AddOutline(_rightInspectorHost.GetComponent<Image>());
        }

        private void BuildContextActionBar(RectTransform root)
        {
            _contextActionBarHost = CreateAnchoredImage(
                "ContextActionBar", root, ActionSurface, Vector2.zero, new Vector2(1f, 0f),
                new Vector2(16f, 8f), new Vector2(-16f, ActionBarHeight));
            AddOutline(_contextActionBarHost.GetComponent<Image>());
        }

        private void BuildOverlaySlots(RectTransform root)
        {
            RectTransform overlayRoot = CreateRect("OverlaySlots", root);
            Stretch(overlayRoot);
            overlayRoot.SetAsLastSibling();

            _popupHost = CreateRect("PopupHost", overlayRoot);
            Stretch(_popupHost);
            _toastHost = CreateRect("ToastHost", overlayRoot);
            Stretch(_toastHost);
            _tooltipHost = CreateRect("TooltipHost", overlayRoot);
            Stretch(_tooltipHost);
        }

        private void UpdateWorkspaceOffsets()
        {
            if (_mainWorkspaceHost == null)
                return;

            float bottom = _isActionBarVisible ? ActionBarHeight + 12f : 12f;
            float right = _isInspectorVisible ? InspectorWidth + WorkspaceGap + 16f : 16f;
            _mainWorkspaceHost.offsetMin = new Vector2(16f, bottom);
            _mainWorkspaceHost.offsetMax = new Vector2(-right, -(TopBarHeight + NavigationHeight + ContextHeaderHeight + 12f));

            if (_rightInspectorHost != null)
            {
                _rightInspectorHost.offsetMin = new Vector2(-(InspectorWidth + 16f), bottom);
                _rightInspectorHost.offsetMax = new Vector2(-16f, -(TopBarHeight + NavigationHeight + ContextHeaderHeight + 12f));
            }
        }

        private static RectTransform CreateAnchoredImage(
            string name,
            Transform parent,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            RectTransform rect = CreateRect(name, parent);
            SetAnchors(rect, anchorMin, anchorMax, offsetMin, offsetMax);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return child.GetComponent<RectTransform>();
        }

        private static Text CreateText(
            string name,
            Transform parent,
            string value,
            int fontSize,
            FontStyle fontStyle,
            TextAnchor alignment,
            Color color)
        {
            RectTransform rect = CreateRect(name, parent);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = DefaultFont;
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static Font DefaultFont =>
            _defaultFont ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        private static void SetAnchors(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void AddBottomBorder(RectTransform parent)
        {
            CreateAnchoredImage(
                "BottomBorder", parent, Border, Vector2.zero, new Vector2(1f, 0f),
                Vector2.zero, new Vector2(0f, 1f));
        }

        private static void AddOutline(Image image)
        {
            var outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = Border;
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = false;
        }

        private static ColorBlock CreateButtonColors(bool isSubTab)
        {
            return new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f),
                pressedColor = new Color(0.82f, 0.86f, 0.84f, 1f),
                selectedColor = Accent,
                disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.75f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                GameObject child = parent.GetChild(i).gameObject;
                if (Application.isPlaying)
                    Destroy(child);
                else
                    DestroyImmediate(child);
            }
        }
    }
}
