using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>Owner 화면들이 SharedGameShell 안에서 같은 uGUI 문법을 사용하게 하는 생성 도우미다.</summary>
    internal static class OwnerRuntimeUiFactory
    {
        public static RectTransform CreateRect(string name, Transform parent)
        {
            return OwnerWorkspaceUiFactory.CreateRoot(parent, name, false);
        }

        public static Image CreateImage(string name, Transform parent, Color color)
        {
            RectTransform rect = CreateRect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static OwnerWorkspaceUiFactory.Panel CreatePanel(
            string name,
            Transform parent,
            string title,
            bool isHero = false)
        {
            return OwnerWorkspaceUiFactory.CreatePanel(parent, name, title, isHero);
        }

        public static Text CreateText(
            string name,
            Transform parent,
            string value,
            int fontSize,
            FontStyle style,
            TextAnchor alignment,
            Color color)
        {
            return OwnerWorkspaceUiFactory.CreateText(
                parent, name, value, fontSize, style, alignment, color);
        }

        public static Button CreateButton(
            string name,
            Transform parent,
            string label,
            Color color,
            int fontSize = 15)
        {
            Button button = OwnerWorkspaceUiFactory.CreateButton(parent, name, label, null);
            button.GetComponent<Image>().color = color;
            button.transform.Find("Label").GetComponent<Text>().fontSize = fontSize;
            return button;
        }

        public static ScrollRect CreateVerticalScroll(string name, Transform parent, out RectTransform content)
        {
            Image scrollSurface = CreateImage(name, parent, CareerUiTheme.ReferencePanel);
            Outline outline = scrollSurface.gameObject.AddComponent<Outline>();
            outline.effectColor = CareerUiTheme.ReferenceBorder;
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = false;
            ScrollRect scroll = scrollSurface.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;

            Image viewportImage = CreateImage("Viewport", scroll.transform, new Color(0f, 0f, 0f, 0.01f));
            RectTransform viewport = viewportImage.rectTransform;
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            content = CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = CareerUiTheme.Space2;
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            return scroll;
        }

        public static void SetAnchors(
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

        public static void Stretch(RectTransform rect)
        {
            OwnerWorkspaceUiFactory.Stretch(rect);
        }

        public static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            SetAnchors(rect, Vector2.zero, Vector2.one, offsetMin, offsetMax);
        }

        public static void ClearChildren(Transform parent)
        {
            for (int index = parent.childCount - 1; index >= 0; index--)
            {
                GameObject child = parent.GetChild(index).gameObject;
                if (Application.isPlaying)
                    Object.Destroy(child);
                else
                    Object.DestroyImmediate(child);
            }
        }

    }
}
