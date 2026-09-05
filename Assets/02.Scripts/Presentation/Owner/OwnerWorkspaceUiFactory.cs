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
            }
            return root;
        }

        public static Panel CreatePanel(Transform parent, string name, string title, bool isHero = false)
        {
            RectTransform root = CreateRect(name, parent);
            Image frameImage = root.gameObject.AddComponent<Image>();
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
            var text = rect.gameObject.AddComponent<Text>();
            text.font = Font;
            text.text = value ?? string.Empty;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = ResolveTextColor(color ?? CareerUiTheme.TextPrimary);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        public static Button CreateButton(Transform parent, string name, string label, Action onClick)
        {
            RectTransform rect = CreateRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = CareerUiTheme.ReferenceButton;
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
            CareerUiSkin.ApplyButton(button);
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = CareerUiTheme.ReferenceButtonHighlight;
            colors.pressedColor = new Color(0.70f, 0.72f, 0.72f, 1f);
            colors.disabledColor = new Color(0.72f, 0.73f, 0.72f, 0.55f);
            colors.colorMultiplier = 1f;
            button.colors = colors;
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

        private static Font Font => _font ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }
}
