using Baseball.Presentation.SharedUI;
using Baseball.Core.Historical;
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
        public void AssignmentBadge_상단에서초상을보존하고중복라벨을숨긴다()
        {
            _view.UseLineupSlotLayout();
            _view.Bind(new PlayerMiniCardModel("p", "최준욱", "유격수", "24", "★ 4", "", "유격수"));
            _view.SetAssignmentBadge("1번");
            Assert.That(_view.transform.Find("Position").gameObject.activeSelf, Is.False);
            Transform badge = _view.transform.Find("AssignmentBadge");
            Assert.That(badge.gameObject.activeSelf, Is.True);
            Assert.That(((RectTransform)badge).anchorMin, Is.EqualTo(new Vector2(.03f, .89f)));
            Assert.That(((RectTransform)badge).anchorMax, Is.EqualTo(new Vector2(.97f, 1f)));
            Assert.That(_view.transform.Find("AssignmentBadge/AssignmentLabel").GetComponent<Text>().text,
                Is.EqualTo("배치 중 · 1번"));
            _view.SetAssignmentBadge(null);
            Assert.That(_view.transform.Find("Position").gameObject.activeSelf, Is.True);
            Assert.That(_view.transform.Find("AssignmentBadge").gameObject.activeSelf, Is.False);
        }

        [Test]
        public void Bind_선수표시값과읽기전용상태를그린다()
        {
            var model = new PlayerMiniCardModel(
                "player-17",
                "김하늘",
                "SS",
                "2028",
                "비용 8",
                "올스타",
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

        [TestCase(PlayerCardEdition.Normal)]
        [TestCase(PlayerCardEdition.CareerHigh)]
        [TestCase(PlayerCardEdition.Legend)]
        public void Roster_원화명찰과장식을보존하고선택을독립테두리로표시한다(PlayerCardEdition edition)
        {
            _view.UseLineupSlotLayout();
            var model = new PlayerMiniCardModel("p", "송은범", "", "08", "★ 7", "", "선발", frameEdition: edition, cost: 7);
            _view.Bind(model);
            var name = _view.transform.Find("Name").GetComponent<RectTransform>();
            Vector2 originalMin = name.anchorMin;
            Vector2 originalMax = name.anchorMax;
            _view.UseRosterPresentation();
            _view.Bind(model);
            Assert.That(_view.transform.Find("NameBand").gameObject.activeSelf, Is.False);
            Assert.That(name.anchorMin, Is.EqualTo(originalMin));
            Assert.That(name.anchorMax, Is.EqualTo(originalMax));
            _view.SetVisualState(PlayerMiniCardVisualState.Selected);
            Transform selection = _view.transform.Find("SelectionOverlay");
            Assert.That(selection.gameObject.activeSelf, Is.True);
            Assert.That(selection.Find("Header/Label").GetComponent<Text>().text, Is.EqualTo("선택됨"));
            Assert.That(selection.Find("Left").GetComponent<Image>().raycastTarget, Is.False);
            Assert.That(selection.Find("Right").GetComponent<RectTransform>().rect.width, Is.EqualTo(3f));
            Assert.That(_view.transform.Find("NameBand").gameObject.activeSelf, Is.False);
            _view.SetVisualState(PlayerMiniCardVisualState.Normal);
            Assert.That(selection.gameObject.activeSelf, Is.False);
            Assert.That(name.anchorMin, Is.EqualTo(originalMin));
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

        [Test]
        public void UseLineupSlotLayout_NameBand의CanvasRenderer를유지한다()
        {
            Transform nameBand = _view.transform.Find("NameBand");

            Assert.That(nameBand, Is.Not.Null);
            Assert.That(nameBand.GetComponent<PlayerCardSurface>(), Is.Not.Null);
            Assert.That(nameBand.GetComponent<CanvasRenderer>(), Is.Not.Null);

            Assert.DoesNotThrow(() => _view.UseLineupSlotLayout());
            Assert.That(nameBand.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void UseLineupSlotLayout_전용미니아트와식별정보를표시하고능력막대는숨긴다()
        {
            _view.UseLineupSlotLayout();
            _view.Bind(new PlayerMiniCardModel(
                "player-stats", "최강타", "1번", "26", "비용 8", "유격수",
                stats: new[]
                {
                    new PlayerMiniCardStatModel("교타", 75, 100),
                    new PlayerMiniCardStatModel("장타", 60, 100)
                }));

            Image frame = _view.transform.Find("LineupSubFrame").GetComponent<Image>();
            Assert.That(frame.gameObject.activeSelf, Is.True);
            Assert.That(frame.sprite, Is.Not.Null);
            Assert.That(frame.sprite.name, Is.EqualTo("PlayerCard_Mini_Reference"));
            Assert.That(_view.transform.Find("Name").GetComponent<Text>().text, Is.EqualTo("최강타"));
            Assert.That(_view.transform.Find("StatLabel0"), Is.Null);
            Assert.That(_view.transform.Find("StatTrack0"), Is.Null);
            Assert.That(_view.transform.Find("StatLabel2"), Is.Null);
        }
    }
}
