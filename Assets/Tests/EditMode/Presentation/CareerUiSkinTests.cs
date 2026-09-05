using Baseball.Presentation.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Tests.EditMode.Presentation
{
    /// <summary>공통 UI 스킨이 ImageGen 리소스와 Unity UI 상태를 올바르게 연결하는지 검증한다.</summary>
    public sealed class CareerUiSkinTests
    {
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("CareerUiSkinTests_Root", typeof(RectTransform));
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                Object.DestroyImmediate(_root);
        }

        [Test]
        public void ApplyButton_네가지상태Sprite와핵심CTA연출을적용한다()
        {
            Button button = CreateButton("MatchProgress", new Vector2(420f, 86f));

            CareerUiSkin.ApplyButton(button);

            Image image = button.GetComponent<Image>();
            Assert.That(image.sprite, Is.Not.Null);
            Assert.That(image.type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(button.transition, Is.EqualTo(Selectable.Transition.SpriteSwap));
            Assert.That(button.spriteState.highlightedSprite, Is.Not.Null);
            Assert.That(button.spriteState.pressedSprite, Is.Not.Null);
            Assert.That(button.spriteState.selectedSprite, Is.Not.Null);
            Assert.That(button.GetComponent<CareerUiShine>(), Is.Not.Null);
            Assert.That(button.GetComponent<RectMask2D>(), Is.Not.Null);

            Color firstTint = image.color;
            CareerUiSkin.ApplyButton(button);
            Assert.That(image.color, Is.EqualTo(firstTint));
        }

        [Test]
        public void ApplyButton_선택Tint를FocusedFrame과ColorTint로구분한다()
        {
            Button normal = CreateButton(
                "NormalOption", new Vector2(420f, 86f), new Color(0.035f, 0.075f, 0.115f, 1f));
            Button selected = CreateButton(
                "SelectedOption", new Vector2(420f, 86f), new Color(0.035f, 0.3f, 0.48f, 1f));
            Text selectedLabel = CreateStretchLabel(selected.transform, 20);

            CareerUiSkin.ApplyButton(normal);
            CareerUiSkin.ApplyButton(selected);

            Assert.That(selected.GetComponent<Image>().sprite, Is.Not.EqualTo(normal.GetComponent<Image>().sprite));
            Assert.That(selected.transition, Is.EqualTo(Selectable.Transition.ColorTint));
            Assert.That(selected.transform.Find("SkinSelectedBadge"), Is.Null);
            Assert.That(selectedLabel.rectTransform.offsetMin.x,
                Is.EqualTo(Mathf.Abs(selectedLabel.rectTransform.offsetMax.x)));

            Sprite selectedSprite = selected.GetComponent<Image>().sprite;
            CareerUiSkin.ApplyButton(selected);
            Assert.That(selected.GetComponent<Image>().sprite, Is.EqualTo(selectedSprite));
            Assert.That(selected.transform.Find("SkinSelectedBadge"), Is.Null);
        }

        [Test]
        public void ApplyButton_라벨을안전영역안에서자동축소하고자른다()
        {
            Button button = CreateButton("LongOffer", new Vector2(420f, 86f));
            Text label = CreateStretchLabel(button.transform, 20);

            CareerUiSkin.ApplyButton(button);

            Assert.That(label.resizeTextForBestFit, Is.True);
            Assert.That(label.resizeTextMinSize, Is.LessThan(label.resizeTextMaxSize));
            Assert.That(label.horizontalOverflow, Is.EqualTo(HorizontalWrapMode.Wrap));
            Assert.That(label.verticalOverflow, Is.EqualTo(VerticalWrapMode.Truncate));
            Assert.That(label.rectTransform.offsetMin.x, Is.GreaterThanOrEqualTo(20f));
            Assert.That(label.rectTransform.offsetMax.x, Is.LessThanOrEqualTo(-20f));
        }

        [Test]
        public void ApplyButton_소형필터는장식Atlas대신평면규격을사용한다()
        {
            Button normal = CreateButton("Filter", new Vector2(140f, 38f));
            Button selected = CreateButton(
                "SelectedFilter", new Vector2(140f, 38f), new Color(0.035f, 0.3f, 0.48f, 1f));
            Text label = CreateStretchLabel(selected.transform, 16);

            CareerUiSkin.ApplyButton(normal);
            CareerUiSkin.ApplyButton(selected);

            Assert.That(normal.GetComponent<Image>().sprite, Is.Null);
            Assert.That(normal.GetComponent<Outline>(), Is.Not.Null);
            Assert.That(normal.transition, Is.EqualTo(Selectable.Transition.ColorTint));
            Assert.That(selected.transform.Find("SkinSelectedBadge"), Is.Null);
            Assert.That(label.rectTransform.offsetMin.x, Is.EqualTo(10f));
        }

        [Test]
        public void ApplyButton_너비만좁은탐색Button은표준Frame을유지한다()
        {
            Button button = CreateButton("Back", new Vector2(160f, 52f));

            CareerUiSkin.ApplyButton(button);

            Assert.That(button.GetComponent<Image>().sprite, Is.Not.Null);
            Assert.That(button.GetComponent<Image>().type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(button.GetComponent<Outline>(), Is.Null);
        }

        [Test]
        public void ApplyButton_220px선택Card도ButtonFrame규격을유지한다()
        {
            Button standard = CreateButton(
                "StandardOption", new Vector2(420f, 86f), new Color(0.035f, 0.3f, 0.48f, 1f));
            Button card = CreateButton(
                "CareerCard", new Vector2(580f, 220f), new Color(0.035f, 0.3f, 0.48f, 1f));

            CareerUiSkin.ApplyButton(standard);
            CareerUiSkin.ApplyButton(card);

            Assert.That(card.GetComponent<Image>().sprite, Is.EqualTo(standard.GetComponent<Image>().sprite));
            Assert.That(card.GetComponent<Image>().type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(card.transition, Is.EqualTo(Selectable.Transition.ColorTint));
            Assert.That(card.transform.Find("SkinSelectedBadge"), Is.Null);
        }

        [Test]
        public void ApplySlider_Track과Fill과Handle을각각연결한다()
        {
            Slider slider = CreateSlider();

            CareerUiSkin.ApplySlider(slider);

            Assert.That(slider.GetComponent<Image>().sprite, Is.Not.Null);
            Assert.That(slider.GetComponent<Image>().type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(slider.fillRect.GetComponent<Image>().sprite, Is.Not.Null);
            Assert.That(slider.handleRect.GetComponent<Image>().sprite, Is.Not.Null);
        }

        [Test]
        public void ApplyPanel_범용프레임을9Slice로연결한다()
        {
            var panelObject = new GameObject("SummaryPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panelObject.transform.SetParent(_root.transform, false);
            Image panel = panelObject.GetComponent<Image>();

            CareerUiSkin.ApplyPanel(panel, false);

            Assert.That(panel.sprite, Is.Not.Null);
            Assert.That(panel.type, Is.EqualTo(Image.Type.Sliced));
        }

        [Test]
        public void Apply_주요Panel은Surface만장식하고LegacyBackplate를제거한다()
        {
            Image panel = CreateSizedImage("SummaryPanel", _root.transform, new Vector2(720f, 420f));
            Image surface = CreateSizedImage("Surface", panel.transform, new Vector2(714f, 414f));

            CareerUiSkin.Apply(_root.transform);

            Assert.That(panel.sprite, Is.Null);
            Assert.That(panel.color.a, Is.EqualTo(0f));
            Assert.That(surface.sprite, Is.Not.Null);
            Assert.That(surface.type, Is.EqualTo(Image.Type.Sliced));
        }

        [Test]
        public void Apply_중첩Card는장식Frame을반복하지않고평면규격을사용한다()
        {
            Image panel = CreateSizedImage("SummaryPanel", _root.transform, new Vector2(720f, 420f));
            CreateSizedImage("Surface", panel.transform, new Vector2(714f, 414f));
            Image card = CreateSizedImage("DetailCard", panel.transform, new Vector2(420f, 180f));
            Image cardSurface = CreateSizedImage("Surface", card.transform, new Vector2(416f, 176f));

            CareerUiSkin.Apply(_root.transform);

            Assert.That(card.sprite, Is.Null);
            Assert.That(cardSurface.sprite, Is.Null);
            Assert.That(cardSurface.color.r, Is.LessThan(0.05f));
        }

        [Test]
        public void Apply_FramedCard는Button카드와같은Frame두께를쓴다()
        {
            Image card = CreateSizedImage("OwnerCareer", _root.transform, new Vector2(580f, 190f));
            card.color = new Color(0.04f, 0.05f, 0.065f, 0.88f);
            card.gameObject.AddComponent<CareerUiVisualElement>()
                .Initialize(CareerUiVisualRole.FramedCard);
            Button reference = CreateButton("PlayerCareer", new Vector2(580f, 220f));

            CareerUiSkin.Apply(_root.transform);

            Image referenceImage = reference.GetComponent<Image>();
            Assert.That(card.sprite, Is.Not.Null);
            Assert.That(card.type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(card.pixelsPerUnitMultiplier, Is.EqualTo(referenceImage.pixelsPerUnitMultiplier));
            Assert.That(card.raycastTarget, Is.False);

            Color firstTint = card.color;
            CareerUiSkin.Apply(_root.transform);
            Assert.That(card.color, Is.EqualTo(firstTint));
        }

        private Button CreateButton(string name, Vector2 size)
        {
            return CreateButton(name, size, Color.white);
        }

        private Button CreateButton(string name, Vector2 size, Color color)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(_root.transform, false);
            buttonObject.GetComponent<RectTransform>().sizeDelta = size;
            buttonObject.GetComponent<Image>().color = color;
            return buttonObject.GetComponent<Button>();
        }

        private static Text CreateStretchLabel(Transform parent, int fontSize)
        {
            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelObject.transform.SetParent(parent, false);
            Text label = labelObject.GetComponent<Text>();
            label.fontSize = fontSize;
            RectTransform rect = label.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return label;
        }

        private Slider CreateSlider()
        {
            var sliderObject = new GameObject("TestSlider", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Slider));
            sliderObject.transform.SetParent(_root.transform, false);
            Slider slider = sliderObject.GetComponent<Slider>();

            RectTransform fill = CreateImage("Fill", sliderObject.transform);
            RectTransform handle = CreateImage("Handle", sliderObject.transform);
            slider.fillRect = fill;
            slider.handleRect = handle;
            return slider;
        }

        private static RectTransform CreateImage(string name, Transform parent)
        {
            var imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            return imageObject.GetComponent<RectTransform>();
        }

        private static Image CreateSizedImage(string name, Transform parent, Vector2 size)
        {
            RectTransform rect = CreateImage(name, parent);
            rect.sizeDelta = size;
            return rect.GetComponent<Image>();
        }
    }
}
