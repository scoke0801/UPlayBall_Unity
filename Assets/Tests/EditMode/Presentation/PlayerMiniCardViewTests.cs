using Baseball.Presentation.SharedUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Tests.EditMode.Presentation
{
    /// <summary>
    /// 공용 Mini Card가 모드 State 대신 순수 표시 모델만 소비하는지 검증한다.
    /// </summary>
    public sealed class PlayerMiniCardViewTests
    {
        private GameObject _root;
        private PlayerMiniCardView _view;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("PlayerMiniCardViewTests_Root", typeof(RectTransform));
            _view = PlayerMiniCardView.CreateRuntime(_root.transform);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                Object.DestroyImmediate(_root);
        }

        [Test]
        public void Bind_선수표시값과읽기전용상태를그린다()
        {
            var model = new PlayerMiniCardModel(
                "player-17",
                "김하늘",
                "SS",
                "2028",
                "COST 8",
                "AllStar",
                "오늘 5번 선발",
                teamAccentHex: "#6C927B",
                visualState: PlayerMiniCardVisualState.Highlighted);

            _view.Bind(model);

            Assert.That(_view.Model, Is.SameAs(model));
            Assert.That(_view.transform.Find("Name").GetComponent<Text>().text, Is.EqualTo("김하늘"));
            Assert.That(_view.transform.Find("Position").GetComponent<Text>().text, Is.EqualTo("SS"));
            Assert.That(_view.transform.Find("Status").GetComponent<Text>().text, Is.EqualTo("오늘 5번 선발"));
            Assert.That(_view.transform.Find("Status").gameObject.activeSelf, Is.True);
        }

        [Test]
        public void Click_InteractableModel만선택Event를발생시킨다()
        {
            string selectedPlayerId = null;
            _view.Selected += model => selectedPlayerId = model.PlayerId;
            _view.Bind(new PlayerMiniCardModel("player-1", "이도윤", "CF", "2027", "COST 6", "Normal"));

            _view.GetComponent<Button>().onClick.Invoke();

            Assert.That(selectedPlayerId, Is.EqualTo("player-1"));

            selectedPlayerId = null;
            _view.Bind(new PlayerMiniCardModel(
                "player-2", "박지호", "SP", "2027", "COST 7", "Normal",
                visualState: PlayerMiniCardVisualState.Disabled));
            _view.GetComponent<Button>().onClick.Invoke();

            Assert.That(selectedPlayerId, Is.Null);
        }
    }
}
