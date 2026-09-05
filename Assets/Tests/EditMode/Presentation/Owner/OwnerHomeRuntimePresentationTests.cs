using Baseball.Presentation.Owner;
using Baseball.Presentation.SharedUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Tests.EditMode.Presentation.Owner
{
    /// <summary>구단주 Home이 실제 Snapshot 표시와 Command 요청만 담당하는지 검증한다.</summary>
    public sealed class OwnerHomeRuntimePresentationTests
    {
        private GameObject _root;
        private SharedGameShellView _shell;
        private UI_Scene_OwnerHome _view;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("OwnerHomeRuntimePresentationTests_Root", typeof(RectTransform));
            _shell = SharedGameShellView.CreateRuntime(_root.transform);
            _view = UI_Scene_OwnerHome.CreateRuntime(
                _shell.MainWorkspaceHost,
                _shell.ContextActionBarHost);
        }

        [TearDown]
        public void TearDown()
        {
            if (_view != null)
                Object.DestroyImmediate(_view.gameObject);
            if (_root != null)
                Object.DestroyImmediate(_root);
        }

        [Test]
        public void Bind_다음경기로스터구단자원을NativeUi에표시한다()
        {
            OwnerHomePresentationModel model = CreateModel();

            _view.Bind(model, true);

            Assert.That(FindText("MainWorkspaceHost/OwnerHomeWorkspace/DashboardColumns/NextMatchPanel/ContentSafeRect/NextMatchValue").text,
                Is.EqualTo("R3 · 부산 마리너스 · 홈"));
            Assert.That(FindText("MainWorkspaceHost/OwnerHomeWorkspace/DashboardColumns/RosterPanel/ContentSafeRect/RosterValue").text,
                Does.Contain("현재 1군 25/25"));
            Assert.That(FindText("MainWorkspaceHost/OwnerHomeWorkspace/DashboardColumns/ResourcePanel/ContentSafeRect/ResourceValue").text,
                Does.Contain("SP  420"));
            Assert.That(FindButton("ContextActionBar/OwnerHomeActionBar/PlayNextGameButton").interactable, Is.True);
        }

        [Test]
        public void Actions_게임상태를바꾸지않고Coordinator에Command를요청한다()
        {
            bool playRequested = false;
            bool saveRequested = false;
            bool titleRequested = false;
            _view.PlayNextGameRequested += () => playRequested = true;
            _view.SaveRequested += () => saveRequested = true;
            _view.TitleRequested += () => titleRequested = true;
            _view.Bind(CreateModel(), true);

            FindButton("ContextActionBar/OwnerHomeActionBar/PlayNextGameButton").onClick.Invoke();
            FindButton("ContextActionBar/OwnerHomeActionBar/SaveButton").onClick.Invoke();
            FindButton("ContextActionBar/OwnerHomeActionBar/TitleButton").onClick.Invoke();

            Assert.That(playRequested, Is.True);
            Assert.That(saveRequested, Is.True);
            Assert.That(titleRequested, Is.True);
        }

        private Text FindText(string path) => _shell.transform.Find(path).GetComponent<Text>();
        private Button FindButton(string path) => _shell.transform.Find(path).GetComponent<Button>();

        private static OwnerHomePresentationModel CreateModel()
        {
            return OwnerHomePresentationBuilder.Build(new OwnerHomeSnapshot(
                "2028 시즌",
                "3주차",
                "Rookie League",
                "서울 웨이브스",
                string.Empty,
                "R3 · 부산 마리너스 · 홈",
                1_250_000,
                420,
                185,
                12,
                25,
                25,
                14,
                14,
                11,
                11,
                3,
                3,
                61,
                true,
                string.Empty));
        }
    }
}
