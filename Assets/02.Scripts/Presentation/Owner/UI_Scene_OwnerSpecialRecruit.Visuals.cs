using System;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    public sealed partial class UI_Scene_OwnerSpecialRecruit
    {
        private static readonly Color Ink = new Color32(47, 50, 56, 255);
        private static readonly Color Gold = new Color32(156, 120, 56, 255);

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
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Math.Max(10, size - 3);
            text.resizeTextMaxSize = size;
            return text;
        }

        private static Button Button(string name, Transform parent, string label,
            float x0, float y0, float x1, float y1, Action clicked, bool interactable = true)
        {
            Button button = OwnerRuntimeUiFactory.CreateReferenceButton(name, parent, label, 15);
            Place((RectTransform)button.transform, x0, y0, x1, y1);
            button.interactable = interactable;
            if (clicked != null) button.onClick.AddListener(() => clicked());
            return button;
        }

        private static void Border(RectTransform rect, Color color)
        {
            Outline outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(2, -2);
        }

        private static void SectionTitle(Transform parent, string text, float x0, float y0, float x1, float y1)
        {
            RectTransform title = Surface(text, parent, new Color32(251, 249, 239, 255), x0, y0, x1, y1);
            Border(title, Gold);
            Label("Label", title, text, 17, Ink, .02f, 0, .98f, 1);
        }

        private static void Frame(RectTransform parent, string edition, bool mini)
        {
            string resource = "UI/PlayerCards/PlayerCard_" + (mini ? "Mini_" : "Full_") + edition + "_v2";
            Sprite frame = Resources.Load<Sprite>(resource);
            Image image = OwnerRuntimeUiFactory.CreateImage("CardFrame", parent, frame == null ? new Color32(49, 53, 59, 255) : Color.white);
            Place(image.rectTransform, .045f, .04f, .955f, .96f);
            image.sprite = frame;
            image.preserveAspect = true;
            // 선수 정보가 없는 상태에서는 아트만 사용하고 이름·Cost 등의 임의 값을 채우지 않는다.
            Label("EmptyCard", parent, mini ? "미등록" : "영입 대상 없음", mini ? 11 : 18,
                new Color32(223, 222, 211, 255), .08f, .40f, .92f, .59f);
        }

        private static void MaterialSlot(RectTransform parent, int index,
            float x0, float y0, float x1, float y1, bool timeline)
        {
            RectTransform slot = Surface("MaterialSlot" + (index + 1), parent,
                timeline ? new Color32(31, 65, 110, 255) : new Color32(202, 169, 98, 255), x0, y0, x1, y1);
            Border(slot, timeline ? new Color32(133, 171, 214, 255) : Gold);
            Label("SlotTitle", slot, timeline ? "연도 —" : "재료 " + (index + 1), 12,
                timeline ? Color.white : Ink, .03f, .84f, .97f, .98f);
            RectTransform card = Rect("MiniCard", slot, .07f, .22f, .93f, .84f);
            Frame(card, "Normal", true);
            Button("Register", slot, "등록", .08f, .045f, .92f, .205f, null, false);
        }
    }
}
