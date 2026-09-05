using Baseball.Presentation.SharedUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Tests.EditMode.Presentation
{
    /// <summary>
    /// 공용 셸이 고정 Chrome과 화면별 합성 슬롯을 올바르게 생성하는지 검증한다.
    /// </summary>
    public sealed class SharedGameShellViewTests
    {
        private GameObject _root;
        private SharedGameShellView _view;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("SharedGameShellViewTests_Root", typeof(RectTransform));
            _view = SharedGameShellView.CreateRuntime(_root.transform);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                Object.DestroyImmediate(_root);
        }

        [Test]
        public void CreateRuntime_공용Chrome과모든합성슬롯을생성한다()
        {
            Assert.That(_view.transform.Find("GlobalTopBar"), Is.Not.Null);
            Assert.That(_view.transform.Find("PrimaryNavigation"), Is.Not.Null);
            Assert.That(_view.transform.Find("ContextHeader"), Is.Not.Null);
            Assert.That(_view.MainWorkspaceHost.name, Is.EqualTo("MainWorkspaceHost"));
            Assert.That(_view.RightInspectorHost.name, Is.EqualTo("OptionalRightInspector"));
            Assert.That(_view.ContextActionBarHost.name, Is.EqualTo("ContextActionBar"));
            Assert.That(_view.PopupHost.name, Is.EqualTo("PopupHost"));
            Assert.That(_view.ToastHost.name, Is.EqualTo("ToastHost"));
            Assert.That(_view.TooltipHost.name, Is.EqualTo("TooltipHost"));
        }

        [Test]
        public void BindProfile_Capability가없는메뉴를생성하지않는다()
        {
            _view.BindProfile(CreatePlayerProfile());

            Transform entries = _view.transform.Find("PrimaryNavigation/Entries");
            Assert.That(entries.Find("Player.Home"), Is.Not.Null);
            Assert.That(entries.Find("Shared.League"), Is.Not.Null);
            Assert.That(entries.Find("Owner.Scout"), Is.Null);
        }

        [Test]
        public void BindContext_선택한Primary와SubTab을표시하고Route요청을전달한다()
        {
            _view.BindProfile(CreatePlayerProfile());
            _view.BindContext(new ShellContextModel("Shared.League.Standings", "순위", "현재 3위", "리그"));
            string requestedRoute = null;
            _view.NavigationRequested += routeId => requestedRoute = routeId;

            Transform subTabs = _view.transform.Find("ContextHeader/SubTabs");
            Button schedule = subTabs.Find("Shared.League.Schedule").GetComponent<Button>();
            schedule.onClick.Invoke();

            Assert.That(requestedRoute, Is.EqualTo("Shared.League.Schedule"));
            Assert.That(_view.transform.Find("ContextHeader/Title").GetComponent<Text>().text, Is.EqualTo("리그 / 순위"));
        }

        [Test]
        public void BindStatus_Provider가준비한모드전용Slot만표시한다()
        {
            _view.BindStatus(new ShellStatusModel(
                "2028 시즌",
                "4월 3주",
                "Premier",
                "서울 웨이브스",
                "3위",
                "다음 경기 D-1",
                new[]
                {
                    new ShellStatusSlotModel("Condition", "컨디션", "좋음", ShellStatusEmphasis.Positive),
                    new ShellStatusSlotModel("Fatigue", "피로", "낮음")
                }));

            Transform slots = _view.transform.Find("GlobalTopBar/ModeStatusSlots");
            Assert.That(slots.childCount, Is.EqualTo(2));
            Assert.That(slots.Find("Condition/Value").GetComponent<Text>().text, Is.EqualTo("좋음"));
            Assert.That(slots.Find("Fatigue/Value").GetComponent<Text>().text, Is.EqualTo("낮음"));
        }

        [Test]
        public void OptionalSlot_숨기면Workspace가남는영역을사용한다()
        {
            float rightWithInspector = _view.MainWorkspaceHost.offsetMax.x;
            float bottomWithActionBar = _view.MainWorkspaceHost.offsetMin.y;

            _view.SetInspectorVisible(false);
            _view.SetActionBarVisible(false);

            Assert.That(_view.RightInspectorHost.gameObject.activeSelf, Is.False);
            Assert.That(_view.ContextActionBarHost.gameObject.activeSelf, Is.False);
            Assert.That(_view.MainWorkspaceHost.offsetMax.x, Is.GreaterThan(rightWithInspector));
            Assert.That(_view.MainWorkspaceHost.offsetMin.y, Is.LessThan(bottomWithActionBar));
        }

        [Test]
        public void ChromeOverlayMode_LegacyWorkspace위에Header와Navigation만남긴다()
        {
            _view.SetChromeOverlayMode(true);

            _view.SetInspectorVisible(true);
            _view.SetActionBarVisible(true);

            Assert.That(_view.GetComponent<Image>().color.a, Is.Zero);
            Assert.That(_view.transform.Find("GlobalTopBar").gameObject.activeSelf, Is.True);
            Assert.That(_view.transform.Find("PrimaryNavigation").gameObject.activeSelf, Is.True);
            Assert.That(_view.transform.Find("ContextHeader").gameObject.activeSelf, Is.True);
            Assert.That(_view.transform.Find("GlobalTopBar").GetComponent<Image>().raycastTarget, Is.True);
            Assert.That(_view.transform.Find("PrimaryNavigation").GetComponent<Image>().raycastTarget, Is.True);
            Assert.That(_view.transform.Find("ContextHeader").GetComponent<Image>().raycastTarget, Is.True);
            Assert.That(_view.MainWorkspaceHost.gameObject.activeSelf, Is.False);
            Assert.That(_view.RightInspectorHost.gameObject.activeSelf, Is.False);
            Assert.That(_view.ContextActionBarHost.gameObject.activeSelf, Is.False);
            Assert.That(_view.PopupHost.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void GlobalSettings_현재모드Coordinator에요청을전달한다()
        {
            bool wasRequested = false;
            _view.SettingsRequested += () => wasRequested = true;

            _view.transform.Find("GlobalTopBar/GlobalSettings").GetComponent<Button>().onClick.Invoke();

            Assert.That(wasRequested, Is.True);
        }

        private static GameModeUiProfile CreatePlayerProfile()
        {
            var leagueChildren = new[]
            {
                new NavigationEntry("Shared.League.Standings", "순위"),
                new NavigationEntry("Shared.League.Schedule", "일정")
            };
            var manifest = new NavigationManifest(new[]
            {
                new NavigationEntry("Player.Home", "홈"),
                new NavigationEntry(
                    "Shared.League",
                    "리그",
                    UiCapability.CanViewLeagueInformation,
                    children: leagueChildren),
                new NavigationEntry("Owner.Scout", "스카우트", UiCapability.CanUseScout)
            });
            return new GameModeUiProfile(
                UiGameMode.PlayerCareer,
                "선수 모드",
                manifest,
                new UiCapabilitySet(UiCapability.CanViewLeagueInformation));
        }
    }
}
