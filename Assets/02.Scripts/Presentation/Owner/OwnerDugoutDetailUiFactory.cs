using System;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>덕아웃 상세 화면 세 장의 조밀한 패널·목록·액션 모양을 통일한다.</summary>
    internal static class OwnerDugoutDetailUiFactory
    {
        public static RectTransform CreatePanel(Transform parent, string name, float left, float bottom, float right, float top)
        {
            RectTransform rect = CreateRect(parent, name, left, bottom, right, top);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = CareerUiTheme.ReferencePanel;
            image.raycastTarget = false;
            rect.gameObject.AddComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.FlatSurface);
            var outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = CareerUiTheme.ReferenceBorder;
            outline.effectDistance = new Vector2(1f, -1f);
            return rect;
        }

        public static Text CreateLabel(
            Transform parent,
            string name,
            string value,
            float left,
            float bottom,
            float right,
            float top,
            int size = 14,
            FontStyle style = FontStyle.Normal,
            TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            Text text = OwnerWorkspaceUiFactory.CreateText(parent, name, value, size, style, anchor,
                CareerUiTheme.ReferenceText);
            Place(text.rectTransform, left, bottom, right, top);
            return text;
        }

        public static Button CreateButton(
            Transform parent,
            string name,
            string label,
            float left,
            float bottom,
            float right,
            float top,
            Action action)
        {
            Button button = OwnerWorkspaceUiFactory.CreateButton(parent, name, label, action);
            Place((RectTransform)button.transform, left, bottom, right, top);
            return button;
        }

        public static RectTransform CreateScrollContent(
            Transform parent,
            string name,
            float left,
            float bottom,
            float right,
            float top,
            out ScrollRect scrollRect)
        {
            RectTransform root = CreateRect(parent, name, left, bottom, right, top);
            var image = root.gameObject.AddComponent<Image>();
            image.color = new Color(0.94f, 0.94f, 0.92f, 0.72f);
            var mask = root.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            scrollRect = root.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            RectTransform content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(root, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            scrollRect.viewport = root;
            scrollRect.content = content;
            return content;
        }

        public static void ClearChildren(RectTransform parent)
        {
            for (int index = parent.childCount - 1; index >= 0; index--)
            {
                GameObject child = parent.GetChild(index).gameObject;
                if (Application.isPlaying) UnityEngine.Object.Destroy(child);
                else UnityEngine.Object.DestroyImmediate(child);
            }
        }

        public static void Place(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static RectTransform CreateRect(Transform parent, string name, float left, float bottom, float right, float top)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Place(rect, left, bottom, right, top);
            return rect;
        }
    }
}
