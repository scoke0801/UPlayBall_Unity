using System.Linq;
using Baseball.Presentation.Owner;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Tests.EditMode.Presentation.Owner
{
    /// <summary>대기실의 정보 표시·이동 요청·진행 차단·해상도별 영역 분리를 검증한다.</summary>
    public sealed class OwnerHomeRuntimePresentationTests
    {
        private GameObject _root;
        private SharedGameShellView _shell;
        private UI_Scene_OwnerHome _view;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("OwnerHomeTests", typeof(RectTransform));
            _root.GetComponent<RectTransform>().sizeDelta = new Vector2(1920f, 1080f);
            _shell = SharedGameShellView.CreateRuntime(_root.transform);
            _shell.BindProfile(OwnerModeUiProfileFactory.Create());
            _shell.SetInspectorVisible(false);
            _shell.SetActionBarVisible(false);
            _shell.BindContext(new ShellContextModel(OwnerNavigationRoutes.Home, "홈", "", ""));
            _view = UI_Scene_OwnerHome.CreateRuntime(_shell.MainWorkspaceHost, _shell.ContextActionBarHost);
        }

        [TearDown]
        public void TearDown()
        {
            if (_view != null) Object.DestroyImmediate(_view.gameObject);
            if (_root != null) Object.DestroyImmediate(_root);
        }

        [Test]
        public void Bind_구단정보와실제일정을표시하고없는순위를발명하지않는다()
        {
            _view.Bind(CreateModel(), true);
            Assert.That(FindText("NextMatchValue").text, Is.EqualTo("R3 · 부산 마리너스 · 홈"));
            Assert.That(FindText("TeamName").text, Is.EqualTo("서울 웨이브스"));
            Assert.That(_shell.MainWorkspaceHost.GetComponentsInChildren<Text>().Any(t => t.text == "시즌 성적 집계 전"), Is.True);
            Assert.That(FindButton("PlayNextGameButton").interactable, Is.True);
        }

        [Test]
        public void Actions_경기와바로가기를기존Coordinator에요청한다()
        {
            int requests = 0;
            string route = null;
            _view.OpponentAnalysisRequested += () => requests++;
            _view.MatchPreparationRequested += () => requests++;
            _view.PlayNextGameRequested += () => requests++;
            _view.SaveRequested += () => requests++;
            _view.NavigationRequested += value => route = value;
            _view.Bind(CreateModel(), true);
            foreach (string name in new[] { "OpponentAnalysisButton", "MatchPreparationButton", "PlayNextGameButton", "SaveButton" })
                FindButton(name).onClick.Invoke();
            Assert.That(requests, Is.EqualTo(4));
            FindButton("ScheduleButton").onClick.Invoke();
            Assert.That(route, Is.EqualTo(OwnerSharedInformationWorkspaceCoordinator.ScheduleRouteId));
            FindButton("ClubButton").onClick.Invoke();
            Assert.That(route, Is.EqualTo(OwnerNavigationRoutes.ClubInformation));
        }

        [Test]
        public void InvalidRoster_진행은막고수정과분석은열어둔다()
        {
            _view.Bind(CreateModel(false), true);
            Assert.That(FindButton("PlayNextGameButton").interactable, Is.False);
            Assert.That(FindButton("MatchPreparationButton").interactable, Is.True);
            Assert.That(FindButton("OpponentAnalysisButton").interactable, Is.True);
            Assert.That(FindText("Feedback").text, Is.EqualTo("투수 1명이 부족합니다."));
        }

        [Test]
        public void CompletedSchedule_경기행동은막고결과조회는유지한다()
        {
            _view.Bind(CreateModel(), false);
            Assert.That(FindButton("PlayNextGameButton").interactable, Is.False);
            Assert.That(FindButton("MatchPreparationButton").interactable, Is.False);
            Assert.That(FindButton("OpponentAnalysisButton").interactable, Is.False);
            Assert.That(FindButton("ScheduleButton").interactable, Is.True);
            Assert.That(FindText("NextMatchValue").text, Is.EqualTo("남은 일정 없음"));
        }

        [Test]
        public void Skin_재적용해도밝은정보창과텍스트대비를유지한다()
        {
            _view.Bind(CreateModel(), true);
            Color titleColor = FindText("TeamName").color;
            CareerUiSkin.Apply(_shell.MainWorkspaceHost);
            Assert.That(FindText("TeamName").color, Is.EqualTo(titleColor));
            Assert.That(FindButton("PlayNextGameButton").GetComponent<Image>().sprite, Is.Null);
        }

        [TestCase(1280, 720)]
        [TestCase(1920, 1080)]
        [TestCase(2560, 1440)]
        [TestCase(3440, 1440)]
        public void Layout_정보창과경기버튼이Workspace안에있고겹치지않는다(int width, int height)
        {
            _root.GetComponent<RectTransform>().sizeDelta = new Vector2(width, height);
            _view.Bind(CreateModel(), true);
            Canvas.ForceUpdateCanvases();
            RectTransform dock = _view.GuideDockTarget;
            var corners = new Vector3[4];
            dock.GetWorldCorners(corners);
            foreach (Vector3 corner in corners)
                Assert.That(_shell.MainWorkspaceHost.rect.Contains(_shell.MainWorkspaceHost.InverseTransformPoint(corner)), Is.True);
            Button[] buttons = _shell.MainWorkspaceHost.GetComponentsInChildren<Button>();
            for (int i = 0; i < buttons.Length; i++)
            {
                Rect first = Bounds((RectTransform)buttons[i].transform, dock);
                Assert.That(first.width, Is.GreaterThan(0f));
                for (int j = i + 1; j < buttons.Length; j++)
                    Assert.That(first.Overlaps(Bounds((RectTransform)buttons[j].transform, dock)), Is.False);
            }
        }

        [Test]
        public void Route_홈과세부화면왕복시배경과제목행을복원한다()
        {
            Assert.That(_shell.transform.Find("ContextHeader").gameObject.activeSelf, Is.False);
            _view.SetVisible(false);
            _shell.BindContext(new ShellContextModel(OwnerNavigationRoutes.RosterLineup, "선수단", "", ""));
            Assert.That(_shell.transform.Find("ContextHeader").gameObject.activeSelf, Is.True);
            Assert.That(_view.GuideDockTarget.gameObject.activeInHierarchy, Is.False);
            _shell.BindContext(new ShellContextModel(OwnerNavigationRoutes.Home, "홈", "", ""));
            _view.SetVisible(true);
            Assert.That(_shell.transform.Find("ContextHeader").gameObject.activeSelf, Is.False);
            Assert.That(_view.GuideDockTarget.gameObject.activeInHierarchy, Is.True);
        }

        private static Rect Bounds(RectTransform rect, Transform relativeTo)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector3 min = relativeTo.InverseTransformPoint(corners[0]);
            Vector3 max = relativeTo.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private Text FindText(string name) => _shell.MainWorkspaceHost.GetComponentsInChildren<Text>(true).First(t => t.name == name);
        private Button FindButton(string name) => _shell.MainWorkspaceHost.GetComponentsInChildren<Button>(true).First(b => b.name == name);

        private static OwnerHomePresentationModel CreateModel(bool isValid = true)
        {
            return OwnerHomePresentationBuilder.Build(new OwnerHomeSnapshot(
                "2028 시즌", "3주차", "루키 리그", "서울 웨이브스", string.Empty,
                "R3 · 부산 마리너스 · 홈", 1250000, 420, 185, 12, 25, 25, 14, 14, 11, 11, 3, 3, 61,
                isValid, isValid ? "" : "투수 1명이 부족합니다."));
        }
    }
}
