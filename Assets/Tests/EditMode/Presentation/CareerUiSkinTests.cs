using System.Collections.Generic;
using System.Reflection;
using Baseball.Presentation.Career;
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

        [TestCase(TitleButtonRole.Mode, "TitleFrame_mode")]
        [TestCase(TitleButtonRole.Secondary, "TitleButton_secondary")]
        [TestCase(TitleButtonRole.Primary, "TitleButton_primary")]
        [TestCase(TitleButtonRole.Danger, "TitleButton_secondary")]
        public void TitleSkin_역할별전용프레임이공용스킨재적용후에도유지된다(
            TitleButtonRole role,
            string spriteName)
        {
            Button button = CreateButton("TitleAction", new Vector2(580f, 190f));
            CreateStretchLabel(button.transform, 18).text = role.ToString();

            TitleUiButtonSkin.Apply(button, role);
            CareerUiSkin.Apply(_root.transform);
            CareerUiSkin.Apply(_root.transform);

            Image frame = (Image)button.targetGraphic;
            Assert.That(frame.sprite, Is.Not.Null);
            Assert.That(frame.sprite.name, Is.EqualTo(spriteName));
            Assert.That(frame.type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(frame.raycastTarget, Is.True);
            Assert.That(button.transform.Find("TitleButtonFrame"), Is.Not.Null);
        }

        [Test]
        public void TitleSkin_빈라벨모드카드도전체클릭프레임을사용한다()
        {
            Button button = CreateButton("OwnerCareer", new Vector2(580f, 190f));
            CreateStretchLabel(button.transform, 18).text = string.Empty;

            TitleUiButtonSkin.Apply(button, TitleButtonRole.Mode);

            Assert.That(button.GetComponent<TitleUiButtonSkin>(), Is.Not.Null);
            Assert.That(button.GetComponent<Image>().enabled, Is.False);
            Assert.That(button.targetGraphic.gameObject.name, Is.EqualTo("TitleButtonFrame"));
            Assert.That(button.targetGraphic.raycastTarget, Is.True);
        }

        [Test]
        public void TitleSkin_적용후생성된모드문구도밝은카드용대비색을사용한다()
        {
            Button button = CreateButton("PlayerCareer", new Vector2(580f, 220f));
            TitleUiButtonSkin.Apply(button, TitleButtonRole.Mode);
            Text mode = CreateStretchLabel(button.transform, 30);
            mode.gameObject.name = "Mode";
            mode.color = CareerUiTheme.TextPrimary;
            Text description = CreateStretchLabel(button.transform, 17);
            description.gameObject.name = "Description";
            description.color = CareerUiTheme.TextSecondary;
            Text action = CreateStretchLabel(button.transform, 16);
            action.gameObject.name = "Action";
            action.color = CareerUiTheme.PrimaryBright;

            CareerUiSkin.Apply(_root.transform);

            Assert.That(CalculateLuminance(mode.color), Is.LessThan(0.20f));
            Assert.That(CalculateLuminance(description.color), Is.LessThan(0.40f));
            Assert.That(CalculateLuminance(action.color), Is.LessThan(0.40f));
            Assert.That(action.color.g, Is.GreaterThan(action.color.r));
        }

        [Test]
        public void TitleSkin_패널프레임을DataImage로보존한다()
        {
            Image panel = CreateSizedImage("ModePanel", _root.transform, new Vector2(720f, 1080f));

            TitleUiButtonSkin.ApplyPanel(panel);
            CareerUiSkin.Apply(_root.transform);

            Assert.That(panel.sprite, Is.Not.Null);
            Assert.That(panel.sprite.name, Is.EqualTo("TitleFrame_mode"));
            Assert.That(panel.type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(panel.raycastTarget, Is.False);
            Assert.That(panel.GetComponent<CareerUiVisualElement>().Role,
                Is.EqualTo(CareerUiVisualRole.DataImage));
        }

        [Test]
        public void OwnerFilterDropdown_비활성Template의항목배경에도어두운Palette를적용한다()
        {
            MethodInfo createDropdown = typeof(UI_Scene_NewGame).GetMethod(
                "CreateOwnerFilterDropdown",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(createDropdown, Is.Not.Null);
            Dropdown dropdown = (Dropdown)createDropdown.Invoke(null, new object[]
            {
                "OwnerCardYearFilter",
                _root.transform,
                new List<string> { "연도 전체", "2024" },
                0,
                new Vector2(205f, 42f),
                Vector2.zero
            });

            Assert.That(dropdown.template.gameObject.activeSelf, Is.False);
            Toggle itemToggle = dropdown.itemText.GetComponentInParent<Toggle>(true);
            Assert.That(itemToggle, Is.Not.Null);
            Assert.That(itemToggle.targetGraphic, Is.TypeOf<Image>());

            Image itemBackground = (Image)itemToggle.targetGraphic;
            float textLuminance = CalculateLuminance(dropdown.itemText.color);
            float backgroundLuminance = CalculateLuminance(itemBackground.color);
            Assert.That(backgroundLuminance, Is.LessThan(0.2f));
            Assert.That(textLuminance - backgroundLuminance, Is.GreaterThan(0.65f));
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

        private static float CalculateLuminance(Color color)
        {
            return color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f;
        }
    }
}
