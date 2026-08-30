using System.Collections;
using Baseball.Presentation.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Baseball.Tests.PlayMode.Presentation
{
    /// <summary>런타임에 뒤늦게 생성된 UI가 공통 스킨을 자동으로 이어받는지 검증한다.</summary>
    public sealed class CareerUiSkinPlayModeTests
    {
        [UnityTest]
        public IEnumerator DynamicContent_다음Frame에공통ButtonSkin을적용한다()
        {
            var root = new GameObject("CareerUiSkinPlayModeTests_Root", typeof(RectTransform));
            var contentObject = new GameObject("DynamicContent", typeof(RectTransform));
            contentObject.transform.SetParent(root.transform, false);
            CareerUiSkin.Apply(root.transform);

            var buttonObject = new GameObject(
                "MatchProgress",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(contentObject.transform, false);
            buttonObject.GetComponent<RectTransform>().sizeDelta = new Vector2(420f, 86f);
            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelObject.transform.SetParent(buttonObject.transform, false);
            Text label = labelObject.GetComponent<Text>();
            label.fontSize = 20;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;

            yield return null;

            Button button = buttonObject.GetComponent<Button>();
            Assert.That(button.transition, Is.EqualTo(Selectable.Transition.SpriteSwap));
            Assert.That(button.GetComponent<Image>().sprite, Is.Not.Null);
            Assert.That(button.GetComponent<CareerUiShine>(), Is.Not.Null);
            Assert.That(button.GetComponent<RectMask2D>(), Is.Not.Null);
            Assert.That(label.resizeTextForBestFit, Is.True);

            Object.Destroy(root);
        }
    }
}
