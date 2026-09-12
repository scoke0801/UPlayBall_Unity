using System.IO;
using System.Linq;
using System.Reflection;
using Baseball.Game.Guide;
using Baseball.Presentation.Guide;
using Baseball.Presentation.Owner;
using Baseball.Presentation.SharedUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Tests.EditMode.Presentation.Owner
{
    /// <summary>실제 Canvas 배율로 대시보드 세 상태와 경기 CTA·문자·안전영역을 렌더링한다.</summary>
    public sealed class OwnerMainDashboardRevisionTests
    {
        [Test]
        public void 처리결과는리포트를보존하고확인하면안내영역만닫힌다()
        {
            var root = new GameObject("ManagerFeedbackTest", typeof(RectTransform));
            try
            {
                var shell = SharedGameShellView.CreateRuntime(root.transform);
                var view = UI_System_OwnerGuide.Create(shell, OwnerGuidePresentationData.Load());
                var progress = new GuideProgressState();
                view.Bind(progress, "");
                Button Find(string name) => view.GetComponentsInChildren<Button>(true).Single(b => b.name == name);
                Find("Review").onClick.Invoke();
                var report = view.GetComponentsInChildren<Text>(true).Single(t => t.name == "ReportDetail");
                string previous = report.text;
                float height = 0;
                view.FeedbackHeightChanged += value => height = value;
                view.SetFeedback("구단주 진행 데이터를 저장했습니다.");
                view.Bind(progress, "");
                Assert.That(report.text, Is.EqualTo(previous));
                Assert.That(view.GetComponentsInChildren<Text>().Any(t => t.text.Contains("진행 데이터를 저장")), Is.True);
                Assert.That(height, Is.GreaterThan(0));
                Find("DismissResult").onClick.Invoke();
                Assert.That(height, Is.Zero);
                Assert.That(view.IsOpen, Is.True);
                Assert.That(report.text, Is.EqualTo(previous));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void 리포트는문제별제목과빈보관함을표시하고지난제안의이동을막는다()
        {
            var root = new GameObject("ReportFlow", typeof(RectTransform));
            try
            {
                var shell = SharedGameShellView.CreateRuntime(root.transform);
                var copy = OwnerGuidePresentationData.Load();
                var view = UI_System_OwnerGuide.Create(shell, copy);
                var progress = new GuideProgressState();
                progress.Reconcile("match", 0, new[] {
                    new GuideGoal("empty", GuideGoalKind.PresetIssue, GuideTargetKind.PresetSlot, true, "MissingAssignment"),
                    new GuideGoal("pitchers", GuideGoalKind.RosterIssue, GuideTargetKind.Roster, true, "StartingPitcherCount", actual: 4, expected: 5)
                });
                view.Bind(progress, "");
                view.ReadRequested += id => { progress.MarkReportRead(id); view.Bind(progress, ""); };
                view.SetOpen(true);
                Button Find(string name) => view.GetComponentsInChildren<Button>(true).Single(b => b.name == name);
                Find("Review").onClick.Invoke();
                string first = Find("ReportRow0").GetComponentInChildren<Text>().text;
                string second = Find("ReportRow1").GetComponentInChildren<Text>().text;
                Assert.That(first, Is.Not.EqualTo(second));
                Assert.That(first, Does.Not.Contain(copy.rosterAction));
                Find("ReportRow0").onClick.Invoke();
                Assert.That(progress.GetReports().Count(r => r.isRead), Is.EqualTo(1));
                Assert.That(Find("ReportRow0").gameObject.activeSelf, Is.False);
                Assert.That(Find("ReportNavigate").interactable, Is.True);
                Assert.That(view.TryGoBack(), Is.True);
                Find("ReportFilter2").onClick.Invoke();
                Assert.That(view.GetComponentsInChildren<Text>().Any(t => t.text == copy.emptyBookmark), Is.True);
                Find("ReportFilter0").onClick.Invoke();
                Find("ReportRow0").onClick.Invoke();
                progress.Reconcile("next", 1, System.Array.Empty<GuideGoal>());
                view.Bind(progress, "");
                Assert.That(Find("ReportNavigate").interactable, Is.False);
                Assert.That(view.GetComponentsInChildren<Text>().Any(t => t.text.Contains(copy.expired)), Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void 리포트의읽음보관과딥링크는UnityJson으로복원된다()
        {
            var state = new GuideProgressState();
            state.Reconcile("match", 3, new[] { new GuideGoal("slot", GuideGoalKind.PresetIssue,
                GuideTargetKind.PresetSlot, false, "OffPositionAssignment", slotIndex: 4, cardId: "card") }, "season", 2);
            string id = state.FindReportId("slot");
            state.MarkReportRead(id); state.SetReportBookmark(id, true);
            var restored = GuideProgressState.Restore(JsonUtility.FromJson<GuideProgressData>(JsonUtility.ToJson(state.Capture())));
            var report = restored.GetReports().Single();
            Assert.That(report.isRead && report.isBookmarked, Is.True);
            Assert.That(report.createdSeason, Is.EqualTo(2));
            Assert.That(report.ToGoal().CardId, Is.EqualTo("card"));
            Assert.That(report.ToGoal().SlotIndex, Is.EqualTo(4));
        }

        [TestCase(1280, 720)]
        [TestCase(1920, 1080)]
        [TestCase(2048, 1152)]
        [TestCase(2560, 1440)]
        [TestCase(3440, 1440)]
        [TestCase(1280, 720, true)]
        [TestCase(1920, 1080, true)]
        [TestCase(3440, 1440, true)]
        [TestCase(1280, 720, true, true)]
        [TestCase(1920, 1080, true, true)]
        [TestCase(2560, 1440, true, true)]
        [TestCase(3440, 1440, true, true)]
        public void 대시보드세상태는경기와안내를분리하고좌측배경을보존한다(int width, int height, bool imageSkin = false, bool feedback = false)
        {
            var root = new GameObject("DashboardCanvas", typeof(RectTransform), typeof(Canvas));
            var cameraRoot = new GameObject("DashboardCamera", typeof(Camera));
            var target = new RenderTexture(width, height, 24);
            var pixels = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            UI_Scene_OwnerHome home = null;
            try
            {
                float scale = height / 1080f;
                var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
                var rect = (RectTransform)root.transform;
                rect.sizeDelta = new Vector2(width / scale, height / scale); rect.localScale = Vector3.one * scale;
                var camera = cameraRoot.GetComponent<Camera>();
                camera.orthographic = true; camera.orthographicSize = height / 2f;
                camera.transform.position = new Vector3(0, 0, -100); camera.nearClipPlane = .1f; camera.farClipPlane = 500;
                camera.targetTexture = target; canvas.worldCamera = camera;
                var shell = SharedGameShellView.CreateRuntime(root.transform);
                shell.BindProfile(OwnerModeUiProfileFactory.Create());
                shell.BindContext(new ShellContextModel(OwnerNavigationRoutes.Home, "홈", ""));
                shell.SetActionBarVisible(false); shell.SetInspectorVisible(false);
                home = UI_Scene_OwnerHome.CreateRuntime(shell.MainWorkspaceHost, shell.ContextActionBarHost);
                var model = OwnerHomePresentationBuilder.Build(new OwnerHomeSnapshot("2027 시즌", "24주차", "갤럭시 리그", "서울 웨이브스", "1위 · 72승 31패",
                    "R104 · 부산 마리너스 · 원정", imageSkin ? 1284500000L : 99999999999L, imageSkin ? 320 : 99999, imageSkin ? 180 : 99999, 0, 25, 25, 13, 13, 12, 12, 3, 3, 100, true, "",
                    opponentStrengthText: "상대 전력 강함 · 선발과 불펜을 확인하세요."));
                home.Bind(model, true); shell.BindStatus(model.ShellStatus);
                var copy = OwnerGuidePresentationData.Load();
                var guide = UI_System_OwnerGuide.Create(home.ManagerHost, copy, home.SetDashboardState);
                var progress = new GuideProgressState();
                progress.PublishMatch("last", 12, 11);
                progress.Reconcile("match", 23, new[] {
                    new GuideGoal("missing", GuideGoalKind.PresetIssue, GuideTargetKind.PresetSlot, true, "MissingAssignment"),
                    new GuideGoal("pitcher", GuideGoalKind.RosterIssue, GuideTargetKind.Roster, true, "StartingPitcherCount", actual: 4, expected: 5),
                    new GuideGoal("position", GuideGoalKind.PresetIssue, GuideTargetKind.PresetSlot, false, "OffPositionAssignment"),
                    new GuideGoal("debrief", GuideGoalKind.Debrief, GuideTargetKind.Condition, false, ""),
                    new GuideGoal("prepare", GuideGoalKind.Preparation, GuideTargetKind.Analysis, false, "") }, "season");
                guide.Bind(progress, "");
                guide.FeedbackHeightChanged += home.SetManagerFeedbackHeight;
                if (feedback) guide.SetFeedback("시즌 기록을 확인하고 다음 시즌을 준비하세요.");
                guide.ReadRequested += id => { progress.MarkReportRead(id); guide.Bind(progress, ""); };
                guide.BookmarkRequested += (id, value) => { progress.SetReportBookmark(id, value); guide.Bind(progress, ""); };
                string directory = Path.GetFullPath(Path.Combine(Application.dataPath, imageSkin ? "../FrontOfficeScreenshots" : "../DashboardScreenshots"));
                Directory.CreateDirectory(directory);
                Vector3 dashboardScale = Vector3.one;
                var stableRects = new[] { home.GuideDockTarget, home.ManagerHost,
                    (RectTransform)home.GuideDockTarget.Find("NextMatchPanel"),
                    (RectTransform)home.GuideDockTarget.Find("ClubInformationPanel") };
                stableRects = stableRects.Concat(
                    guide.GetComponentsInChildren<RectTransform>(true).Where(item =>
                        item.name == "PortraitViewport" || item.name == "Navigate" || item.name == "Review")).ToArray();
                var initialCorners = stableRects.Select(item => new Vector3[4]).ToArray();
                for (int state = 0; state < 4; state++)
                {
                    if (state == 1) guide.SetOpen(true);
                    if (state == 2)
                    {
                        guide.GetComponentsInChildren<Button>().Single(button => button.name == "Review").onClick.Invoke();
                    }
                    if (state == 3)
                        guide.GetComponentsInChildren<Button>().Single(button => button.name == "ReportRow0").onClick.Invoke();
                    Canvas.ForceUpdateCanvases();
                    typeof(UI_Scene_OwnerHome).GetMethod("ResizeDashboard", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(home, null);
                    Canvas.ForceUpdateCanvases();
                    if (state == 0)
                    {
                        dashboardScale = home.GuideDockTarget.localScale;
                        for (int i = 0; i < stableRects.Length; i++) stableRects[i].GetWorldCorners(initialCorners[i]);
                    }
                    else
                    {
                        Assert.That(home.GuideDockTarget.localScale, Is.EqualTo(dashboardScale), "메뉴 전환으로 홈 전체 배율이 변하면 안 된다.");
                        if (state == 1)
                            for (int i = 0; i < stableRects.Length; i++)
                            {
                                var currentCorners = new Vector3[4];
                                stableRects[i].GetWorldCorners(currentCorners);
                                Assert.That(currentCorners, Is.EqualTo(initialCorners[i]), stableRects[i].name + " 접기·펼치기 경계");
                            }
                    }
                    if (imageSkin)
                    {
                        UIOwnerFrontOfficeSkin.Apply(home.GuideDockTarget);
                        Assert.That(home.GuideDockTarget.GetComponentsInChildren<UIOwnerFrontOfficeButton>().Length, Is.GreaterThan(0));
                    }
                    camera.Render(); RenderTexture.active = target;
                    foreach (var frame in home.GuideDockTarget.GetComponentsInChildren<UIOwnerPanelFrame>())
                    {
                        if (!frame.enabled) continue;
                        Assert.That(frame.raycastTarget, Is.False);
                        var frameMesh = frame.canvasRenderer.GetMesh();
                        Assert.That(frameMesh, Is.Not.Null);
                        Assert.That(frameMesh.vertexCount, Is.GreaterThan(0), frame.name + " 테두리가 실제로 렌더링되어야 한다.");
                    }
                    Assert.That(guide.transform.Find("ManagerSuggestionCard/ThinBorder").GetComponent<Image>().enabled, Is.False);
                    pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
                    File.WriteAllBytes(Path.Combine(directory, $"dashboard-{width}x{height}-{state}{(feedback ? "-feedback" : "")}.png"), pixels.EncodeToPNG());
                    // 확대 초상은 RectMask2D 안에서 잘린다. 원본 이미지 대신 실제 대시보드 외곽을 측정한다.
                    var corners = new Vector3[4];
                    home.GuideDockTarget.GetWorldCorners(corners);
                    var dashboard = new Bounds(rect.InverseTransformPoint(corners[0]), Vector3.zero);
                    foreach (var corner in corners) dashboard.Encapsulate(rect.InverseTransformPoint(corner));
                    Assert.That(guide.GetComponentInChildren<RectMask2D>(true), Is.Not.Null);
                    Assert.That(dashboard.min.x, Is.GreaterThanOrEqualTo(rect.rect.xMin + rect.rect.width * .58f));
                    Assert.That(dashboard.max.x, Is.LessThanOrEqualTo(rect.rect.xMax - 24f));
                    Assert.That(dashboard.max.y, Is.LessThanOrEqualTo(rect.rect.yMax - 104f));
                    var buttons = home.GuideDockTarget.GetComponentsInChildren<Button>();
                    foreach (var button in buttons)
                    {
                        Assert.That(button.GetComponent<OwnerUiButtonSkin>(), Is.Not.Null, button.name);
                        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(rect, button.transform);
                        Assert.That(bounds.size.y * scale, Is.GreaterThanOrEqualTo(44f), button.name);
                    }
                    for (int i = 0; i < buttons.Length; i++) for (int j = i + 1; j < buttons.Length; j++)
                    {
                        var a = RectTransformUtility.CalculateRelativeRectTransformBounds(rect, buttons[i].transform);
                        var b = RectTransformUtility.CalculateRelativeRectTransformBounds(rect, buttons[j].transform);
                        Assert.That(a.Intersects(b), Is.False, buttons[i].name + "/" + buttons[j].name);
                    }
                    foreach (var text in home.GuideDockTarget.GetComponentsInChildren<Text>())
                    {
                        Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1), text.name + ": " + text.text);
                        Assert.That(text.fontSize * scale * home.GuideDockTarget.localScale.x, Is.GreaterThanOrEqualTo(14f), text.name);
                    }
                    Assert.That(buttons.Single(button => button.name == "PlayNextGameButton").interactable, Is.True);
                }
                Assert.That(guide.TryGoBack(), Is.True);
                Assert.That(guide.IsOpen, Is.True);
                Assert.That(guide.TryGoBack(), Is.True);
                Assert.That(guide.IsOpen, Is.True);
                Assert.That(guide.TryGoBack(), Is.True);
                Assert.That(guide.IsOpen, Is.False);
            }
            finally
            {
                RenderTexture.active = previous;
                if (home != null) Object.DestroyImmediate(home.gameObject);
                Object.DestroyImmediate(root); Object.DestroyImmediate(cameraRoot);
                Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
            }
        }
    }
}
