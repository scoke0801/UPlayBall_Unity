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

        [Test]
        public void ProcessCancel_Popup을먼저닫고화면뒤로가기는요청하지않는다()
        {
            TestFirstPopup popup = CreateUi<TestFirstPopup>("UI_Popup_First");
            int navigationRequestCount = 0;
            _uiManager.NavigationBackRequested += () => navigationRequestCount++;
            popup.Show();

            _uiManager.ProcessCancel();

            Assert.That(popup.IsVisible, Is.False);
            Assert.That(navigationRequestCount, Is.Zero);
        }

        [Test]
        public void ProcessCancel_닫을Popup이없으면화면뒤로가기를요청한다()
        {
            int navigationRequestCount = 0;
            _uiManager.NavigationBackRequested += () => navigationRequestCount++;

            _uiManager.ProcessCancel();

            Assert.That(navigationRequestCount, Is.EqualTo(1));
        }

        [Test]
        public void ProcessCancel_닫을수없는Modal뒤의화면으로입력을통과시키지않는다()
        {
            TestFirstPopup lowerPopup = CreateUi<TestFirstPopup>("UI_Popup_Lower");
            TestBlockingPopup blockingPopup = CreateUi<TestBlockingPopup>("UI_Popup_Blocking");
            int navigationRequestCount = 0;
            _uiManager.NavigationBackRequested += () => navigationRequestCount++;
            lowerPopup.Show();
            blockingPopup.Show();

            _uiManager.ProcessCancel();

            Assert.That(blockingPopup.IsVisible, Is.True);
            Assert.That(lowerPopup.IsVisible, Is.True);
            Assert.That(navigationRequestCount, Is.Zero);
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

    public sealed class TestBlockingPopup : UIPopupBase
    {
        public override bool CanCloseWithCancel => false;
    }
}
