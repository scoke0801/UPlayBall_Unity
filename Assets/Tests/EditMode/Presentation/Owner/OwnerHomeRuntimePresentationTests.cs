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
                _shell.MainWorkspaceHost);
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
        public void Bind_다음경기와세가지행동만표시한다()
        {
            OwnerHomePresentationModel model = CreateModel();

            _view.Bind(model, true);

            Assert.That(_shell.MainWorkspaceHost.GetComponentsInChildren<Button>(true).Length, Is.EqualTo(3));
            Assert.That(_shell.transform.Find("MainWorkspaceHost/OwnerHomeWorkspace/DashboardColumns/RosterPanel"), Is.Null);
            Assert.That(_shell.transform.Find("MainWorkspaceHost/OwnerHomeWorkspace/DashboardColumns/ResourcePanel"), Is.Null);

            Assert.That(FindText("MainWorkspaceHost/OwnerHomeWorkspace/DashboardColumns/NextMatchPanel/ContentSafeRect/NextMatchValue").text,
                Is.EqualTo("R3 · 부산 마리너스 · 홈"));
            Assert.That(FindButton("MainWorkspaceHost/OwnerHomeWorkspace/DashboardColumns/NextMatchPanel/ContentSafeRect/NextMatchActions/OpponentAnalysisButton").interactable,
                Is.True);
            Assert.That(FindButton("MainWorkspaceHost/OwnerHomeWorkspace/DashboardColumns/NextMatchPanel/ContentSafeRect/NextMatchActions/MatchPreparationButton").interactable,
                Is.True);
            Assert.That(FindButton("MainWorkspaceHost/OwnerHomeWorkspace/DashboardColumns/NextMatchPanel/ContentSafeRect/NextMatchActions/PlayNextGameButton").interactable, Is.True);
        }

        [Test]
        public void Actions_게임상태를바꾸지않고Coordinator에Command를요청한다()
        {
            bool playRequested = false;
            bool analysisRequested = false;
            bool preparationRequested = false;
            _view.OpponentAnalysisRequested += () => analysisRequested = true;
            _view.MatchPreparationRequested += () => preparationRequested = true;
            _view.PlayNextGameRequested += () => playRequested = true;
            _view.Bind(CreateModel(), true);

            FindButton("MainWorkspaceHost/OwnerHomeWorkspace/DashboardColumns/NextMatchPanel/ContentSafeRect/NextMatchActions/OpponentAnalysisButton").onClick.Invoke();
            FindButton("MainWorkspaceHost/OwnerHomeWorkspace/DashboardColumns/NextMatchPanel/ContentSafeRect/NextMatchActions/MatchPreparationButton").onClick.Invoke();
            FindButton("MainWorkspaceHost/OwnerHomeWorkspace/DashboardColumns/NextMatchPanel/ContentSafeRect/NextMatchActions/PlayNextGameButton").onClick.Invoke();

            Assert.That(analysisRequested, Is.True);
            Assert.That(preparationRequested, Is.True);
            Assert.That(playRequested, Is.True);
        }

        [Test]
        public void Skin_재적용해도버튼프레임과진행강조를유지한다()
        {
            _view.Bind(CreateModel(), true);
            Button play = FindButton("MainWorkspaceHost/OwnerHomeWorkspace/DashboardColumns/NextMatchPanel/ContentSafeRect/NextMatchActions/PlayNextGameButton");
            Button save = FindButton("MainWorkspaceHost/OwnerHomeWorkspace/DashboardColumns/NextMatchPanel/ContentSafeRect/NextMatchActions/OpponentAnalysisButton");
            Sprite playSprite = play.GetComponent<Image>().sprite;
            Sprite saveSprite = save.GetComponent<Image>().sprite;
            Assert.That(playSprite, Is.Not.Null);
            Assert.That(saveSprite, Is.Not.Null.And.Not.EqualTo(playSprite));

            Baseball.Presentation.UI.CareerUiSkin.Apply(_shell.MainWorkspaceHost);
            _view.Bind(CreateModel(), false);

            Assert.That(play.GetComponent<Image>().sprite, Is.SameAs(playSprite));
            Assert.That(save.GetComponent<Image>().sprite, Is.SameAs(saveSprite));
            Assert.That(play.GetComponent<Image>().type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(play.interactable, Is.False);
            Assert.That(save.interactable, Is.False);
            Text saveLabel = save.transform.Find("Label").GetComponent<Text>();
            Assert.That(saveLabel.horizontalOverflow, Is.EqualTo(HorizontalWrapMode.Overflow));
            Assert.That(saveLabel.rectTransform.offsetMin.x, Is.EqualTo(18f));
            Assert.That(saveLabel.rectTransform.offsetMax.x, Is.EqualTo(-18f));
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
