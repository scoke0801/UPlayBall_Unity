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
        public void 대시보드세상태는경기와안내를분리하고좌측배경을보존한다(int width, int height)
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
                    "R104 · 부산 마리너스 · 원정", 99999999999L, 99999, 99999, 0, 25, 25, 13, 13, 12, 12, 3, 3, 100, true, "",
                    opponentStrengthText: "상대 전력 강함 · 선발과 불펜을 확인하세요."));
                home.Bind(model, true); shell.BindStatus(model.ShellStatus);
                var copy = OwnerGuidePresentationData.Load();
                var guide = UI_System_OwnerGuide.Create(home.ManagerHost, copy, home.SetDashboardState);
                var progress = new GuideProgressState();
                progress.PublishMatch("last", 12, 11);
                progress.Reconcile("match", 23, new[] {
                    new GuideGoal("debrief", GuideGoalKind.Debrief, GuideTargetKind.Condition, false, ""),
                    new GuideGoal("prepare", GuideGoalKind.Preparation, GuideTargetKind.Analysis, false, "") }, "season");
                guide.Bind(progress, "");
                guide.ReadRequested += id => { progress.MarkReportRead(id); guide.Bind(progress, ""); };
                guide.BookmarkRequested += (id, value) => { progress.SetReportBookmark(id, value); guide.Bind(progress, ""); };
                string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../DashboardScreenshots"));
                Directory.CreateDirectory(directory);
                for (int state = 0; state < 3; state++)
                {
                    if (state == 1) guide.SetOpen(true);
                    if (state == 2)
                    {
                        guide.GetComponentsInChildren<Button>().Single(button => button.name == "Review").onClick.Invoke();
                        guide.GetComponentsInChildren<Button>().Single(button => button.name == "ReportRow0").onClick.Invoke();
                    }
                    Canvas.ForceUpdateCanvases();
                    typeof(UI_Scene_OwnerHome).GetMethod("ResizeDashboard", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(home, null);
                    Canvas.ForceUpdateCanvases();
                    camera.Render(); RenderTexture.active = target;
                    foreach (var frame in home.GuideDockTarget.GetComponentsInChildren<UIOwnerPanelFrame>())
                    {
                        Assert.That(frame.raycastTarget, Is.False);
                        var frameMesh = frame.canvasRenderer.GetMesh();
                        Assert.That(frameMesh, Is.Not.Null);
                        Assert.That(frameMesh.vertexCount, Is.GreaterThan(0), frame.name + " 테두리가 실제로 렌더링되어야 한다.");
                    }
                    Assert.That(guide.transform.Find("ManagerSuggestionCard/ThinBorder").GetComponent<Image>().enabled, Is.False);
                    pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
                    File.WriteAllBytes(Path.Combine(directory, $"dashboard-{width}x{height}-{state}.png"), pixels.EncodeToPNG());
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
