using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>구단 업무 화면의 불투명 작업면, 이미지와 선택 상태를 공통으로 구성한다.</summary>
    internal static class UIClubOfficeStyle
    {
        internal static readonly Color Paper = new Color32(246, 247, 248, 255);
        internal static readonly Color Ink = new Color32(35, 39, 45, 255);
        internal static readonly Color Muted = new Color32(93, 101, 113, 255);
        internal static readonly Color Blue = new Color32(28, 83, 148, 255);
        private static readonly Sprite[] Artwork = new Sprite[9];

        /// <summary>ImageGen 아틀라스의 독립된 장면을 읽기 순서대로 재사용한다.</summary>
        internal static Sprite LoadArtwork(int index)
        {
            if (index < 0 || index >= Artwork.Length) return null;
            if (Artwork[index] != null) return Artwork[index];
            Texture2D texture = Resources.Load<Texture2D>("UI/Generated/club_office_artwork_v2");
            if (texture == null) return null;
            float width = texture.width / 3f;
            float height = texture.height / 3f;
            Artwork[index] = Sprite.Create(texture,
                new Rect(index % 3 * width + 2f, (2 - index / 3) * height + 2f, width - 4f, height - 4f),
                new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
            return Artwork[index];
        }

        /// <summary>전역 반투명 스킨이 구단 정보의 대비를 낮추지 않도록 명시한 표면을 유지한다.</summary>
        internal static Image Surface(string name, Transform parent, Color color)
        {
            Image image = OwnerRuntimeUiFactory.CreateImage(name, parent, color);
            image.gameObject.AddComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.DataImage);
            return image;
        }

        internal static void Place(RectTransform rect, float x, float y, float right, float top)
        {
            OwnerRuntimeUiFactory.SetAnchors(rect, new Vector2(x, y), new Vector2(right, top),
                Vector2.zero, Vector2.zero);
        }

        internal static Text Label(string name, Transform parent, string value, int size, bool bold = false)
        {
            Text text = OwnerRuntimeUiFactory.CreateText(name, parent, value, size,
                bold ? FontStyle.Bold : FontStyle.Normal, TextAnchor.MiddleLeft, bold ? Ink : Muted);
            text.gameObject.AddComponent<CareerUiPreserveTextColor>();
            return text;
        }

        internal static Image Illustration(Transform parent, string name, int index)
        {
            Image image = Surface(name, parent, Color.white);
            image.sprite = LoadArtwork(index);
            image.preserveAspect = false;
            return image;
        }

        internal static OwnerWorkspaceUiFactory.Panel CreatePanel(
            Transform parent, string name, string title, bool isHero = false)
        {
            Image panel = Surface(name, parent, Color.white);
            Image header = Surface("HeaderSurface", panel.transform, Paper);
            header.rectTransform.anchorMin = new Vector2(0f, 1f);
            header.rectTransform.offsetMin = new Vector2(0f, -44f);
            header.rectTransform.offsetMax = Vector2.zero;
            Text label = Label("HeaderSlot", header.transform, title, 16, true);
            OwnerRuntimeUiFactory.Stretch(label.rectTransform, new Vector2(16f, 0f), new Vector2(-16f, 0f));
            Image rule = Surface("HeaderAccent", header.transform, Blue);
            rule.rectTransform.anchorMax = new Vector2(1f, 0f);
            rule.rectTransform.offsetMin = Vector2.zero;
            rule.rectTransform.offsetMax = new Vector2(0f, 2f);
            RectTransform content = OwnerRuntimeUiFactory.CreateRect("ContentSafeRect", panel.transform);
            OwnerRuntimeUiFactory.Stretch(content, new Vector2(12f, 12f), new Vector2(-12f, -54f));
            return new OwnerWorkspaceUiFactory.Panel(panel.rectTransform, content);
        }

        internal static OwnerWorkspaceUiFactory.Panel CreatePanel(
            string name, Transform parent, string title, bool isHero = false)
            => CreatePanel(parent, name, title, isHero);

        /// <summary>색과 좌측 표시선으로 현재 선택을 드러낸다.</summary>
        internal static void Select(Button button, bool selected)
        {
            Image surface = button.GetComponent<Image>();
            surface.color = selected ? new Color32(225, 237, 249, 255) : Paper;
            Text label = button.GetComponentInChildren<Text>();
            if (label.GetComponent<CareerUiPreserveTextColor>() == null)
                label.gameObject.AddComponent<CareerUiPreserveTextColor>();
            label.color = selected ? Blue : Ink;
            label.rectTransform.offsetMin = new Vector2(16f, 5f);
            label.rectTransform.offsetMax = new Vector2(-12f, -5f);
            Transform marker = button.transform.Find("SelectionMark");
            if (marker == null)
            {
                Image line = Surface("SelectionMark", button.transform, Blue);
                line.rectTransform.anchorMax = new Vector2(0f, 1f);
                line.rectTransform.offsetMin = Vector2.zero;
                line.rectTransform.offsetMax = new Vector2(4f, 0f);
                marker = line.transform;
            }
            marker.gameObject.SetActive(selected);
            OwnerUiButtonSkin.SetSelected(button, selected);
        }

        /// <summary>가변 너비 안내문 옆에서도 실행 버튼의 클릭 영역을 확보한다.</summary>
        internal static void SizeAction(Button button, float width, bool primary = false)
        {
            LayoutElement layout = button.GetComponent<LayoutElement>();
            layout.minWidth = width;
            layout.preferredWidth = width;
            layout.flexibleWidth = 0f;
            Text label = button.GetComponentInChildren<Text>();
            label.rectTransform.offsetMin = new Vector2(10f, 4f);
            label.rectTransform.offsetMax = new Vector2(-10f, -4f);
            if (label.GetComponent<CareerUiPreserveTextColor>() == null)
                label.gameObject.AddComponent<CareerUiPreserveTextColor>();
            label.color = primary ? Color.white : Ink;
            button.GetComponent<Image>().color = primary ? Blue : Paper;
            OwnerUiButtonSkin.Apply(button, primary ? OwnerButtonRole.Primary : OwnerButtonRole.Secondary);
        }
    }
}
