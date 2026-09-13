using System;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>Owner Workspace가 공용 Skin과 ContentSafeRect를 같은 방식으로 구성하도록 돕는다.</summary>
    internal static class OwnerWorkspaceUiFactory
    {
        private static Font _font;
        private static RectTransform _surfacePrefab;
        private static RectTransform _controlPrefab;

        internal readonly struct Panel
        {
            public Panel(RectTransform root, RectTransform content)
            {
                Root = root;
                Content = content;
            }

            public RectTransform Root { get; }
            public RectTransform Content { get; }
        }

        public static RectTransform CreateRoot(Transform parent, string name, bool showOwnerBackground)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            var root = CreateRect(name, parent);
            Stretch(root);
            if (showOwnerBackground)
            {
                Image background = root.gameObject.AddComponent<Image>();
                background.sprite = Resources.Load<Sprite>(OwnerUiAssetIds.HomeBackgroundResourcePath);
                background.color = background.sprite == null ? CareerUiTheme.Background : Color.white;
                background.preserveAspect = false;
                background.raycastTarget = false;
                if (UIOwnerFrontOfficeSkin.IsOwnerContext) OwnerDashboardStyle.ApplyBackdrop(background, true);
            }
            return root;
        }

        public static Panel CreatePanel(Transform parent, string name, string title, bool isHero = false)
        {
            RectTransform root = CreateSkinRoot(name, parent, false);
            Image frameImage = root.GetComponent<Image>() ?? root.gameObject.AddComponent<Image>();
            frameImage.color = CareerUiTheme.ReferencePanel;
            frameImage.raycastTarget = false;
            CareerUiVisualElement visual = root.gameObject.AddComponent<CareerUiVisualElement>();
            visual.Initialize(CareerUiVisualRole.FlatSurface);

            RectTransform headerSurface = CreateRect("HeaderSurface", root);
            headerSurface.anchorMin = new Vector2(0f, 1f);
            headerSurface.anchorMax = Vector2.one;
            headerSurface.offsetMin = new Vector2(1f, -46f);
            headerSurface.offsetMax = new Vector2(-1f, -1f);
            Image headerImage = headerSurface.gameObject.AddComponent<Image>();
            headerImage.color = CareerUiTheme.ReferencePanelHeader;
            headerImage.raycastTarget = false;
            headerSurface.gameObject.AddComponent<CareerUiVisualElement>()
                .Initialize(CareerUiVisualRole.FlatSurface);

            RectTransform accent = CreateRect("HeaderAccent", root);
            accent.anchorMin = new Vector2(0f, 1f);
            accent.anchorMax = Vector2.one;
            accent.offsetMin = new Vector2(1f, -46f);
            accent.offsetMax = new Vector2(-1f, -43f);
            Image accentImage = accent.gameObject.AddComponent<Image>();
            accentImage.color = isHero
                ? CareerUiTheme.ReferenceAccentLight
                : CareerUiTheme.ReferenceAccent;
            accentImage.raycastTarget = false;

            RectTransform border = CreateRect("ThinBorder", root);
            Stretch(border);
            Image borderImage = border.gameObject.AddComponent<Image>();
            borderImage.color = new Color(1f, 1f, 1f, 0.01f);
            borderImage.raycastTarget = false;
            var outline = border.gameObject.AddComponent<Outline>();
            outline.effectColor = CareerUiTheme.ReferenceBorder;
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = false;

            Text header = CreateText(root, "HeaderSlot", title, 16, FontStyle.Bold, TextAnchor.MiddleLeft,
                CareerUiTheme.ReferenceText);
            header.rectTransform.anchorMin = new Vector2(0f, 1f);
            header.rectTransform.anchorMax = Vector2.one;
            header.rectTransform.offsetMin = new Vector2(CareerUiTheme.Space3, -43f);
            header.rectTransform.offsetMax = new Vector2(-CareerUiTheme.Space3, -CareerUiTheme.Space1);

            RectTransform content = CreateRect("ContentSafeRect", root);
            content.anchorMin = Vector2.zero;
            content.anchorMax = Vector2.one;
            content.offsetMin = new Vector2(CareerUiTheme.Space3, CareerUiTheme.Space3);
            content.offsetMax = new Vector2(-CareerUiTheme.Space3, -54f);

            CareerUiFrame frame = root.gameObject.AddComponent<CareerUiFrame>();
            frame.Initialize(frameImage, header.rectTransform, content, content, CareerUiTheme.WideFramePadding, isHero);
            CareerUiSkin.ApplyVisualElement(frameImage);
            // 기존 Outline은 면 전체가 불투명해지는 스킨 충돌이 있어 전용 메시 테두리로 대체한다.
            borderImage.enabled = false;
            border.gameObject.AddComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.FlatSurface);
            if (UIOwnerFrontOfficeSkin.IsOwnerContext)
            {
                // 범용 패널의 기존 44px 제목 영역에는 제목선이 없는 상세 프레임을 사용한다.
                UIOwnerFrontOfficePanel.Apply(root, "ManagerReport");
                headerImage.enabled = false;
                accentImage.enabled = false;
                header.color = OwnerDashboardStyle.Ivory;
                header.gameObject.AddComponent<CareerUiPreserveTextColor>();
            }
            else UIOwnerPanelFrame.Attach(root, isHero);
            return new Panel(root, content);
        }

        public static Text CreateText(
            Transform parent,
            string name,
            string value,
            int fontSize = 16,
            FontStyle style = FontStyle.Normal,
            TextAnchor alignment = TextAnchor.UpperLeft,
            Color? color = null)
        {
            RectTransform rect = CreateRect(name, parent);
            var text = rect.gameObject.AddComponent<Baseball.Presentation.UI.UIProjectText>();
            text.font = Font;
            text.text = value ?? string.Empty;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            Color requested = color ?? CareerUiTheme.TextPrimary;
            text.color = UIOwnerFrontOfficeSkin.IsOwnerContext && UIOwnerFrontOfficePanel.HasDarkSurface(parent)
                ? UIOwnerFrontOfficePanel.ResolveTextColor(requested) : ResolveTextColor(requested);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            if (UIOwnerFrontOfficeSkin.IsOwnerContext) OwnerDashboardStyle.SetTypography(text, style == FontStyle.Bold);
            return text;
        }

        public static Button CreateButton(Transform parent, string name, string label, Action onClick)
        {
            RectTransform rect = CreateSkinRoot(name, parent, true);
            var image = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();
            image.color = CareerUiTheme.ReferenceButton;
            image.raycastTarget = true;
            image.gameObject.AddComponent<CareerUiVisualElement>()
                .Initialize(CareerUiVisualRole.FlatSurface);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            if (onClick != null) button.onClick.AddListener(() => onClick());
            Text text = CreateText(rect, "Label", label, 14, FontStyle.Bold, TextAnchor.MiddleCenter,
                CareerUiTheme.ReferenceText);
            Stretch(text.rectTransform);
            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 42f;
            layout.preferredHeight = 42f;
            OwnerUiButtonSkin.Apply(button);
            return button;
        }

        public static VerticalLayoutGroup AddVerticalLayout(RectTransform target, float spacing = CareerUiTheme.Space3)
        {
            var layout = target.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        public static HorizontalLayoutGroup AddHorizontalLayout(RectTransform target, float spacing = CareerUiTheme.Space4)
        {
            var layout = target.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = true;
            return layout;
        }

        public static void SetFlexible(RectTransform target, float flexibleWidth, float flexibleHeight = 1f)
        {
            LayoutElement layout = target.GetComponent<LayoutElement>() ?? target.gameObject.AddComponent<LayoutElement>();
            layout.flexibleWidth = flexibleWidth;
            layout.flexibleHeight = flexibleHeight;
            layout.minWidth = 160f;
            layout.minHeight = 80f;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static void DestroyOwnedRoot(RectTransform root)
        {
            if (root == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(root.gameObject);
            else UnityEngine.Object.DestroyImmediate(root.gameObject);
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var value = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            value.SetParent(parent, false);
            return value;
        }

        private static RectTransform CreateSkinRoot(string name, Transform parent, bool control)
        {
            if (!UIOwnerFrontOfficeSkin.IsOwnerContext) return CreateRect(name, parent);
            if (control)
                _controlPrefab ??= Resources.Load<RectTransform>(UIOwnerFrontOfficeSkin.ResourceRoot + "Prefabs/UI_Control");
            else
                _surfacePrefab ??= Resources.Load<RectTransform>(UIOwnerFrontOfficeSkin.ResourceRoot + "Prefabs/UI_Surface");
            var prefab = control ? _controlPrefab : _surfacePrefab;
            if (prefab == null) return CreateRect(name, parent);
            var root = UnityEngine.Object.Instantiate(prefab, parent, false);
            root.name = name;
            return root;
        }

        private static Color ResolveTextColor(Color requested)
        {
            if (requested == CareerUiTheme.TextPrimary)
                return CareerUiTheme.ReferenceText;
            if (requested == CareerUiTheme.TextSecondary || requested == CareerUiTheme.TextMuted)
                return CareerUiTheme.ReferenceTextSecondary;
            if (requested == CareerUiTheme.AccentGold)
                return CareerUiTheme.ReferenceAccent;
            return requested;
        }

        private static Font Font => _font ??= Baseball.Presentation.UI.UIProjectFonts.Default;
    }
}
