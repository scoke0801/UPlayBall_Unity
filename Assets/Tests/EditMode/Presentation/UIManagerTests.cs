using Baseball.Presentation.UI;
using NUnit.Framework;
using UnityEngine;

namespace Baseball.Tests.EditMode.Presentation
{
    /// <summary>
    /// UI 표시 스택과 Cancel 닫기 순서를 검증한다.
    /// </summary>
    public sealed class UIManagerTests
    {
        private GameObject _testRoot;
        private UIManager _uiManager;

        [SetUp]
        public void SetUp()
        {
            _testRoot = new GameObject("UIManagerTests_Root");
            var managerObject = new GameObject("UIManager_Test");
            managerObject.transform.SetParent(_testRoot.transform, false);
            _uiManager = managerObject.AddComponent<UIManager>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_testRoot != null)
                Object.DestroyImmediate(_testRoot);
        }

        [Test]
        public void CloseTopmost_가장나중에연UI부터닫는다()
        {
            TestFirstPopup first = CreateUi<TestFirstPopup>("UI_Popup_First");
            TestSecondPopup second = CreateUi<TestSecondPopup>("UI_Popup_Second");
            first.Show();
            second.Show();

            bool closed = _uiManager.CloseTopmost();

            Assert.That(closed, Is.True);
            Assert.That(second.IsVisible, Is.False);
            Assert.That(first.IsVisible, Is.True);
            Assert.That(_uiManager.VisibleCount, Is.EqualTo(1));
        }

        [Test]
        public void Show_동일UI를반복호출해도스택이중복되지않는다()
        {
            TestFirstPopup popup = CreateUi<TestFirstPopup>("UI_Popup_First");

            popup.Show();
            popup.Show();

            Assert.That(_uiManager.VisibleCount, Is.EqualTo(1));
        }

        private T CreateUi<T>(string objectName) where T : UIBase
        {
            var uiObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(T));
            uiObject.transform.SetParent(_testRoot.transform, false);
            T ui = uiObject.GetComponent<T>();
            _uiManager.Register(ui);
            return ui;
        }
    }

    public sealed class TestFirstPopup : UIPopupBase
    {
    }

    public sealed class TestSecondPopup : UIPopupBase
    {
    }
}
