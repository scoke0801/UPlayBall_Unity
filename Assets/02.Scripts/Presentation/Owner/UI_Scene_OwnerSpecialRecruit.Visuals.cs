using System;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    public sealed partial class UI_Scene_OwnerSpecialRecruit
    {
        private static Color Ink => Baseball.Presentation.UI.CareerUiTheme.ReferenceText;

        private static RectTransform Rect(string name, Transform parent, float x0, float y0, float x1, float y1)
        {
            RectTransform rect = OwnerRuntimeUiFactory.CreateRect(name, parent);
            Place(rect, x0, y0, x1, y1);
            return rect;
        }

        private static void Place(RectTransform rect, float x0, float y0, float x1, float y1)
        {
            OwnerRuntimeUiFactory.SetAnchors(rect, new Vector2(x0, y0), new Vector2(x1, y1), Vector2.zero, Vector2.zero);
        }

        private static RectTransform Surface(string name, Transform parent, Color color, float x0, float y0, float x1, float y1)
        {
            Image image = OwnerRuntimeUiFactory.CreateImage(name, parent, color);
            Place(image.rectTransform, x0, y0, x1, y1);
            return image.rectTransform;
        }

        private static Text Label(string name, Transform parent, string value, int size, Color color,
            float x0, float y0, float x1, float y1, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            Text text = OwnerRuntimeUiFactory.CreateText(name, parent, value, size, FontStyle.Normal, alignment, color);
            Place(text.rectTransform, x0, y0, x1, y1);

            return text;
        }

        private static Button Button(string name, Transform parent, string label,
            float x0, float y0, float x1, float y1, Action clicked, bool interactable = true)
        {
            Button button = OwnerWorkspaceUiFactory.CreateButton(parent, name, label, null);
            Place((RectTransform)button.transform, x0, y0, x1, y1);
            button.interactable = interactable;
            if (clicked != null) button.onClick.AddListener(() => clicked());
            return button;
        }

    }
}
