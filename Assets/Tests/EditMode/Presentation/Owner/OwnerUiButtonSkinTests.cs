using Baseball.Presentation.Owner;
using Baseball.Presentation.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Baseball.Tests.EditMode.Presentation.Owner
{
    /// <summary>구단주 프레임의 재적용, 선택, 입력 영역과 모드 복귀를 검증한다.</summary>
    public sealed class OwnerUiButtonSkinTests
    {
        private GameObject _root;

        [SetUp]
        public void SetUp() => _root = new GameObject("SkinTest", typeof(RectTransform));

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        [TestCase(OwnerButtonRole.Navigation, "navigation")]
        [TestCase(OwnerButtonRole.Secondary, "secondary")]
        [TestCase(OwnerButtonRole.Primary, "primary")]
        public void Apply_역할별프레임이공용스킨재적용후에도유지된다(OwnerButtonRole role, string asset)
        {
            Button button = CreateButton("상대 분석");
            OwnerUiButtonSkin.Apply(button, role);
            CareerUiSkin.Apply(_root.transform);
            CareerUiSkin.Apply(_root.transform);
            Image frame = (Image)button.targetGraphic;
            Assert.That(frame.sprite, Is.Not.Null);
            Assert.That(frame.sprite.name, Is.EqualTo("OwnerButton_" + asset));
            Assert.That(frame.type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(frame.sprite.rect.xMax, Is.LessThanOrEqualTo(frame.sprite.texture.width));
            Assert.That(frame.sprite.rect.yMax, Is.LessThanOrEqualTo(frame.sprite.texture.height));
            Assert.That(frame.raycastTarget, Is.True);
            Assert.That(frame.rectTransform.rect.size, Is.EqualTo(((RectTransform)button.transform).rect.size));
            Assert.That(button.transform.childCount, Is.EqualTo(2));
        }

        [Test]
        public void Selection_포커스와비활성변경이현재탭선택을지우지않는다()
        {
            Button button = CreateButton("선수단");
            OwnerUiButtonSkin.Apply(button, OwnerButtonRole.Tab);
            OwnerUiButtonSkin.SetSelected(button, true);
            button.interactable = false;
            button.GetComponent<OwnerUiButtonSkin>().Refresh();
            Assert.That(((Image)button.targetGraphic).sprite.name, Is.EqualTo("OwnerButton_primary"));
            button.interactable = true;
            OwnerUiButtonSkin.SetSelected(button, false);
            Assert.That(((Image)button.targetGraphic).sprite.name, Is.EqualTo("OwnerButton_secondary"));
            Assert.That(button.transform.Find("Label").GetComponent<Text>().color.b, Is.LessThan(.5f));
        }

        [Test]
        public void Restore_선수모드복귀시원래Graphic을복원한다()
        {
            Button button = CreateButton("설정");
            Image original = button.GetComponent<Image>();
            OwnerUiButtonSkin.Apply(button);
            OwnerUiButtonSkin.Restore(button);
            Assert.That(button.targetGraphic, Is.SameAs(original));
            Assert.That(original.enabled, Is.True);
            Assert.That(button.transform.Find("OwnerButtonFrame").gameObject.activeSelf, Is.False);
            OwnerUiButtonSkin.Apply(button);
            Assert.That(button.transform.childCount, Is.EqualTo(2));
            Assert.That(button.targetGraphic, Is.Not.SameAs(original));
        }

        [Test]
        public void Apply_빈라벨의카드클릭영역은변경하지않는다()
        {
            Button button = CreateButton(string.Empty);
            Image original = button.GetComponent<Image>();
            OwnerUiButtonSkin.Apply(button);
            Assert.That(button.GetComponent<OwnerUiButtonSkin>(), Is.Null);
            Assert.That(original.enabled, Is.True);
        }

        [Test]
        public void Click_프레임입력이원래버튼에전달되고비활성상태에서는막힌다()
        {
            Button button = CreateButton("결정");
            OwnerUiButtonSkin.Apply(button, OwnerButtonRole.Primary);
            var eventObject = new GameObject("Events", typeof(EventSystem));
            eventObject.transform.SetParent(_root.transform);
            var pointer = new PointerEventData(eventObject.GetComponent<EventSystem>()) { button = PointerEventData.InputButton.Left };
            int invoked = 0;
            button.onClick.AddListener(() => invoked++);
            ExecuteEvents.ExecuteHierarchy(button.targetGraphic.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            button.interactable = false;
            ExecuteEvents.ExecuteHierarchy(button.targetGraphic.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            Assert.That(invoked, Is.EqualTo(1));
        }

        [Test]
        public void Refresh_방침의의미색상을보존하고라벨대비를갱신한다()
        {
            Button button = CreateButton("중립");
            OwnerUiButtonSkin.Apply(button, OwnerButtonRole.Detail);
            Image source = button.GetComponent<Image>();
            Color red = new Color(.70f, .20f, .22f, 1f);
            source.color = red;
            button.GetComponent<OwnerUiButtonSkin>().Refresh();
            Assert.That(source.color, Is.EqualTo(red));
            Assert.That(button.targetGraphic.color.r, Is.GreaterThan(button.targetGraphic.color.b));
            Assert.That(button.transform.Find("Label").GetComponent<Text>().color.r, Is.GreaterThan(.9f));
            source.color = Color.white;
            CareerUiSkin.Apply(_root.transform);
            Assert.That(button.transform.Find("Label").GetComponent<Text>().color.r, Is.LessThan(.3f));
        }

        private Button CreateButton(string caption)
        {
            var rect = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<RectTransform>();
            rect.SetParent(_root.transform, false);
            rect.sizeDelta = new Vector2(160f, 42f);
            Button button = rect.GetComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            var label = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            label.transform.SetParent(rect, false);
            label.text = caption;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            return button;
        }
    }
}
