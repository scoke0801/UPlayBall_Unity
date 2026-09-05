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
        private static Color TopBar => CareerUiTheme.ShellHeader;
        private static Color NavigationSurface => CareerUiTheme.ShellNavigation;
        private static Color ContextSurface => CareerUiTheme.ReferenceCanvas;
        private static Color WorkspaceSurface => CareerUiTheme.ReferenceCanvas;
        private static Color InspectorSurface => CareerUiTheme.ReferenceCanvas;
        private static Color ActionSurface => CareerUiTheme.ReferencePanelHeader;
        private static Color NavigationColor => CareerUiTheme.ShellTab;
        private static Color NavigationSelectedColor => CareerUiTheme.ShellTabSelected;
        private static Color SubTabColor => CareerUiTheme.ReferencePanelHeader;
        private static Color StatusSurface => CareerUiTheme.ShellNavigation;
        private static Color Border => CareerUiTheme.ShellBorder;
        private static Color Divider => CareerUiTheme.ShellDivider;
        private static Color GoldAccent => CareerUiTheme.ShellGold;
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

            BuildModeBackground(root);
            BuildGlobalTopBar(root);
            BuildPrimaryNavigation(root);
            BuildContextHeader(root);
            BuildWorkspace(root);
            BuildContextActionBar(root);
            BuildOverlaySlots(root);
            UpdateWorkspaceOffsets();
        }

        private void BuildModeBackground(RectTransform root)
        {
            RectTransform backgroundRect = CreateAnchoredImage(
                "ModeBackground", root, Color.clear, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero);
            backgroundRect.SetAsFirstSibling();
            _modeBackground = backgroundRect.GetComponent<Image>();
            _modeBackground.preserveAspect = false;
        }

        private void BuildGlobalTopBar(RectTransform root)
        {
            _globalTopBar = CreateAnchoredImage(
                "GlobalTopBar", root, TopBar, new Vector2(0f, 1f), Vector2.one,
                new Vector2(0f, -TopBarHeight), Vector2.zero);
            _globalTopBar.GetComponent<Image>().raycastTarget = true;
            AddBottomBorder(_globalTopBar, GoldAccent, 2f);

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

            CreateVerticalDivider("BrandDivider", _globalTopBar, 260f);

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

            CreateVerticalDivider("TeamDivider", _globalTopBar, 804f);

            _nextMatchText = CreateText(
                "NextMatch", _globalTopBar, string.Empty, 14, FontStyle.Bold,
                TextAnchor.MiddleRight, TextSecondary);
            SetAnchors(_nextMatchText.rectTransform, new Vector2(0.41f, 0f), new Vector2(0.62f, 1f),
                new Vector2(8f, 0f), new Vector2(-8f, 0f));

            RectTransform nextMatchAccent = CreateAnchoredImage(
                "NextMatchAccent", _globalTopBar, CareerUiTheme.ShellField,
                new Vector2(0.41f, 0f), new Vector2(0.41f, 1f),
                new Vector2(0f, 13f), new Vector2(2f, -13f));
            nextMatchAccent.SetAsFirstSibling();

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
                new Vector2(18f, 5f), new Vector2(-18f, -5f));
            var layout = _navigationEntryHost.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 5f;
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

            RectTransform backRect = CreateAnchoredImage(
                "Back", _contextHeader, NavigationSurface,
                new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(16f, 7f), new Vector2(92f, -7f));
            _backButton = backRect.gameObject.AddComponent<Button>();
            _backButton.targetGraphic = backRect.GetComponent<Image>();
            _backButton.colors = CreateButtonColors(true);
            _backButton.onClick.AddListener(() => BackRequested?.Invoke());
            _backButtonLabel = CreateText(
                "Label", backRect, "이전", 13, FontStyle.Bold,
                TextAnchor.MiddleCenter, TextPrimary);
            Stretch(_backButtonLabel.rectTransform);
            _backButton.gameObject.SetActive(false);

            _contextTitleText = CreateText(
                "Title", _contextHeader, string.Empty, 19, FontStyle.Bold,
                TextAnchor.MiddleLeft, DarkText);
            SetAnchors(_contextTitleText.rectTransform, new Vector2(0f, 0f), new Vector2(0.28f, 1f),
                new Vector2(104f, 0f), new Vector2(-8f, 0f));

            _contextSummaryText = CreateText(
                "Summary", _contextHeader, string.Empty, 14, FontStyle.Normal,
                TextAnchor.MiddleLeft, Color.Lerp(DarkText, ContextSurface, 0.22f));
            SetAnchors(_contextSummaryText.rectTransform, new Vector2(0.28f, 0f), new Vector2(0.52f, 1f),
                new Vector2(0f, 0f), new Vector2(-10f, 0f));

            _subTabHost = CreateRect("SubTabs", _contextHeader);
            SetAnchors(_subTabHost, new Vector2(0.52f, 0f), Vector2.one,
                new Vector2(0f, 5f), new Vector2(-14f, -5f));
            var layout = _subTabHost.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 4f;
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
            AddTopBorder(_mainWorkspaceHost, GoldAccent, 2f);

            _rightInspectorHost = CreateAnchoredImage(
                "OptionalRightInspector", root, InspectorSurface, new Vector2(1f, 0f), Vector2.one,
                new Vector2(-(InspectorWidth + 16f), ActionBarHeight + 12f),
                new Vector2(-16f, -(TopBarHeight + NavigationHeight + ContextHeaderHeight + 12f)));
            AddOutline(_rightInspectorHost.GetComponent<Image>());
            AddTopBorder(_rightInspectorHost, CareerUiTheme.ShellField, 2f);
        }

        private void BuildContextActionBar(RectTransform root)
        {
            _contextActionBarHost = CreateAnchoredImage(
                "ContextActionBar", root, ActionSurface, Vector2.zero, new Vector2(1f, 0f),
                new Vector2(16f, 8f), new Vector2(-16f, ActionBarHeight));
            AddOutline(_contextActionBarHost.GetComponent<Image>());
            AddTopBorder(_contextActionBarHost, GoldAccent, 2f);
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

            // 홈은 사무실 전체를 보여주고, 세부 화면으로 이동하면 기존 작업 프레임을 복원한다.
            bool isOwnerHome = _activeRouteId == Baseball.Presentation.Owner.OwnerNavigationRoutes.Home;
            bool showContextHeader = !isOwnerHome && _isContextHeaderVisible;
            _contextHeader.gameObject.SetActive(showContextHeader);
            Image workspaceImage = _mainWorkspaceHost.GetComponent<Image>();
            workspaceImage.color = isOwnerHome ? Color.clear : WorkspaceSurface;
            _mainWorkspaceHost.GetComponent<Outline>().enabled = !isOwnerHome;
            _mainWorkspaceHost.Find("TopBorder").gameObject.SetActive(!isOwnerHome);
            SetAnchors(_contextActionBarHost, new Vector2(isOwnerHome ? 0.56f : 0f, 0f), new Vector2(1f, 0f),
                new Vector2(16f, 8f), new Vector2(-16f, ActionBarHeight));
            _contextActionBarHost.GetComponent<Image>().color = isOwnerHome
                ? new Color(0.025f, 0.045f, 0.075f, 0.94f) : ActionSurface;
            _primaryNavigation.GetComponent<Image>().color = isOwnerHome
                ? new Color(0.055f, 0.075f, 0.10f, 0.82f) : NavigationSurface;
            if (_modeBackground != null && _modeBackground.sprite != null)
                _modeBackground.color = isOwnerHome ? Color.white : CareerUiTheme.ShellBackdropTint;

            float bottom = _isActionBarVisible ? ActionBarHeight + 12f : 12f;
            float right = _isInspectorVisible ? InspectorWidth + WorkspaceGap + 16f : 16f;
            _mainWorkspaceHost.offsetMin = new Vector2(16f, bottom);
            _mainWorkspaceHost.offsetMax = new Vector2(-right,
                -(TopBarHeight + NavigationHeight + (showContextHeader ? ContextHeaderHeight : 0f) + 12f));

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
            AddBottomBorder(parent, Border, 1f);
        }

        private static void AddBottomBorder(RectTransform parent, Color color, float height)
        {
            CreateAnchoredImage(
                "BottomBorder", parent, color, Vector2.zero, new Vector2(1f, 0f),
                Vector2.zero, new Vector2(0f, height));
        }

        private static void AddTopBorder(RectTransform parent, Color color, float height)
        {
            CreateAnchoredImage(
                "TopBorder", parent, color, new Vector2(0f, 1f), Vector2.one,
                new Vector2(0f, -height), Vector2.zero);
        }

        private static void CreateVerticalDivider(string name, RectTransform parent, float x)
        {
            CreateAnchoredImage(
                name, parent, Divider, new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(x, 13f), new Vector2(x + 1f, -13f));
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
