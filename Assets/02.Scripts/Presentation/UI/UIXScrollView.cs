using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.UI
{
    /// <summary>런타임 생성 UI에 마스크·스크롤바·콘텐츠 기준점을 일관되게 제공한다.</summary>
    public sealed class UIXScrollView
    {
        private const float DefaultScrollbarThickness = 10f;

        private UIXScrollView(
            RectTransform root,
            RectTransform viewport,
            RectTransform content,
            ScrollRect scrollRect)
        {
            Root = root;
            Viewport = viewport;
            Content = content;
            ScrollRect = scrollRect;
        }

        public RectTransform Root { get; }
        public RectTransform Viewport { get; }
        public RectTransform Content { get; }
        public ScrollRect ScrollRect { get; }

        /// <summary>지정 크기의 양방향 또는 단방향 스크롤 영역을 생성한다.</summary>
        public static UIXScrollView Create(
            Transform parent,
            string name,
            Vector2 size,
            Vector2 position,
            Vector2 contentSize,
            bool horizontal,
            bool vertical,
            Color backgroundColor,
            Color trackColor,
            Color handleColor,
            float scrollbarThickness = DefaultScrollbarThickness)
        {
            RectTransform root = CreateRect(name, parent, size, position);
            Image rootImage = root.gameObject.AddComponent<Image>();
            rootImage.color = Color.clear;

            float rightInset = vertical ? scrollbarThickness + 3f : 0f;
            float bottomInset = horizontal ? scrollbarThickness + 3f : 0f;
            RectTransform viewport = CreateRect("Viewport", root, Vector2.zero, Vector2.zero);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(0f, bottomInset);
            viewport.offsetMax = new Vector2(-rightInset, 0f);
            Image viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = backgroundColor;
            viewportImage.raycastTarget = true;
            Mask mask = viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            RectTransform content = CreateRect("Content", viewport, contentSize, Vector2.zero);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 1f);
            content.anchoredPosition = Vector2.zero;

            ScrollRect scrollRect = root.gameObject.AddComponent<ScrollRect>();
            scrollRect.viewport = viewport;
            scrollRect.content = content;
            scrollRect.horizontal = horizontal;
            scrollRect.vertical = vertical;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.inertia = true;
            scrollRect.decelerationRate = 0.12f;
            scrollRect.scrollSensitivity = 34f;

            if (horizontal)
            {
                Scrollbar scrollbar = CreateScrollbar(
                    root,
                    "HorizontalScrollbar",
                    Scrollbar.Direction.LeftToRight,
                    trackColor,
                    handleColor);
                RectTransform rect = (RectTransform)scrollbar.transform;
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = new Vector2(-rightInset, scrollbarThickness);
                scrollRect.horizontalScrollbar = scrollbar;
                scrollRect.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
                scrollRect.horizontalScrollbarSpacing = 3f;
            }

            if (vertical)
            {
                Scrollbar scrollbar = CreateScrollbar(
                    root,
                    "VerticalScrollbar",
                    Scrollbar.Direction.BottomToTop,
                    trackColor,
                    handleColor);
                RectTransform rect = (RectTransform)scrollbar.transform;
                rect.anchorMin = new Vector2(1f, 0f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 0.5f);
                rect.offsetMin = new Vector2(-scrollbarThickness, bottomInset);
                rect.offsetMax = Vector2.zero;
                scrollRect.verticalScrollbar = scrollbar;
                scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
                scrollRect.verticalScrollbarSpacing = 3f;
            }

            scrollRect.horizontalNormalizedPosition = 0f;
            scrollRect.verticalNormalizedPosition = 1f;
            return new UIXScrollView(root, viewport, content, scrollRect);
        }

        private static Scrollbar CreateScrollbar(
            Transform parent,
            string name,
            Scrollbar.Direction direction,
            Color trackColor,
            Color handleColor)
        {
            RectTransform track = CreateRect(name, parent, Vector2.zero, Vector2.zero);
            Image trackImage = track.gameObject.AddComponent<Image>();
            trackImage.color = trackColor;

            RectTransform slidingArea = CreateRect("SlidingArea", track, Vector2.zero, Vector2.zero);
            Stretch(slidingArea, 1f);
            RectTransform handle = CreateRect("Handle", slidingArea, Vector2.zero, Vector2.zero);
            Stretch(handle, 1f);
            Image handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = handleColor;

            Scrollbar scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            scrollbar.direction = direction;
            ColorBlock colors = scrollbar.colors;
            colors.normalColor = handleColor;
            colors.highlightedColor = Color.Lerp(handleColor, Color.white, 0.18f);
            colors.pressedColor = Color.Lerp(handleColor, Color.black, 0.15f);
            scrollbar.colors = colors;
            return scrollbar;
        }

        private static RectTransform CreateRect(
            string name,
            Transform parent,
            Vector2 size,
            Vector2 position)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }
    }
}
