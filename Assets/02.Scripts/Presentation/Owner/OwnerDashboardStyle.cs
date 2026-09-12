using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>홈과 매니저 리포트가 공유하는 재질·타이포그래피 규격이다.</summary>
    public static class OwnerDashboardStyle
    {
        public static readonly Color Ink = new Color32(18, 27, 34, 255);
        public static readonly Color Surface = new Color32(23, 33, 42, 248);
        public static readonly Color Raised = new Color32(42, 57, 69, 255);
        public static readonly Color Gold = new Color32(218, 187, 123, 255);
        public static readonly Color Ivory = new Color32(239, 235, 222, 255);
        public static readonly Color Muted = new Color32(155, 168, 175, 255);
        public static readonly Color Line = new Color32(111, 130, 144, 65);

        /// <summary>네이티브 면과 가는 상단 반사선으로 패널 깊이를 만든다.</summary>
        public static void ApplySurface(RectTransform root, bool accent = false)
        {
            root.GetComponent<Image>().color = Surface;
            if (root.GetComponent<UIOwnerSurfaceGradient>() == null)
                root.gameObject.AddComponent<UIOwnerSurfaceGradient>();
            var outline = root.GetComponent<Outline>();
            if (outline != null) outline.effectColor = Line;
            UIOwnerPanelFrame.Attach(root, accent);
        }

        /// <summary>제목에는 Medium, 설명에는 Light 실제 폰트를 사용한다.</summary>
        public static void SetTypography(Text text, bool title = false)
        {
            text.font = title ? UIProjectFonts.Default : UIProjectFonts.Body;
            text.fontStyle = FontStyle.Normal;
            text.lineSpacing = 1.12f;
        }

        /// <summary>화면 배율에 따라 선명하게 그려지는 장식선이다.</summary>
        public static void Rule(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 lower, Vector2 upper, Color color)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.color = color; image.raycastTarget = false;
            image.gameObject.AddComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.FlatSurface);
            var rect = image.rectTransform;
            rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.offsetMin = lower; rect.offsetMax = upper;
        }
    }
}
