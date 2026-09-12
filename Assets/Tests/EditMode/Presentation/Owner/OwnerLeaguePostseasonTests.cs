using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Baseball.Core.Historical;
using Baseball.Game.Historical;
using Baseball.Presentation.Owner;
using Baseball.Presentation.SharedScreens;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Baseball.Tests.EditMode.Presentation.Owner
{
    /// <summary>순위표의 구분 전환·실제 대진·진출 상태와 화면 경계를 검증한다.</summary>
    public sealed class OwnerLeaguePostseasonTests
    {
        [Test]
        public void 탭전환은정규기록을보존하고포커스와선택을복원한다()
        {
            var host = new GameObject("Host", typeof(RectTransform));
            var events = new GameObject("Events", typeof(EventSystem));
            // EditMode는 EventSystem의 MonoBehaviour 생명주기를 자동 실행하지 않는다.
            bool needsEventLifecycle = EventSystem.current == null;
            if (needsEventLifecycle) InvokeEventLifecycle(events, "OnEnable");
            try
            {
                var view = UI_Scene_OwnerLeague.CreateRuntime((RectTransform)host.transform);
                view.Bind(BuildLeague(), Review("semifinal"));
                view.ShowTab(0);
                SelectPostseason(view);
                Assert.That(view.transform.Find("LeagueTable"), Is.Null);
                Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("PostseasonTab"));
                var selectedTeam = view.transform.Find("PostseasonBoard/WildCard/ContentSafeRect/HigherSeed")
                    .GetComponent<Button>();
                selectedTeam.onClick.Invoke();
                view.gameObject.SetActive(false);
                view.gameObject.SetActive(true);
                view.RestoreFocus();
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(selectedTeam.gameObject));
                view.ShowTab(1);
                Assert.That(view.transform.Find("PostseasonBoard"), Is.Null);
                view.ShowTab(0);
                Assert.That(view.transform.Find("PostseasonBoard"), Is.Not.Null);
                view.transform.Find("PennantRaceTab").GetComponent<Button>().onClick.Invoke();
                Assert.That(view.transform.Find("LeagueTable/Team_0/Cell_3").GetComponent<Text>().text, Is.EqualTo("1"));
                SelectPostseason(view);
                view.Bind(BuildLeague(), Review("semifinal", 2));
                Assert.That(view.transform.Find("LeagueTable"), Is.Not.Null, "다음 시즌에는 정규시즌 순위로 돌아간다.");
            }
            finally
            {
                Object.DestroyImmediate(host);
                if (needsEventLifecycle) InvokeEventLifecycle(events, "OnDisable");
                Object.DestroyImmediate(events);
            }
        }

        [TestCase("pending")]
        [TestCase("semifinal")]
        [TestCase("waiting")]
        [TestCase("completed")]
        [TestCase("two")]
        public void 실제대진만표시하며확정전우승과가짜시리즈를만들지않는다(string stage)
        {
            var host = new GameObject("Host", typeof(RectTransform));
            try
            {
                var view = UI_Scene_OwnerLeague.CreateRuntime((RectTransform)host.transform);
                view.Bind(BuildLeague(), Review(stage));
                view.ShowTab(0);
                SelectPostseason(view);
                string labels = string.Join("\n", view.GetComponentsInChildren<Text>().Select(text => text.text));
                Assert.That(labels, Does.Not.Contain("semifinal-a"));
                if (stage == "pending")
                {
                    Assert.That(labels, Does.Contain("대진 확정 전"));
                    Assert.That(view.transform.Find("PostseasonBoard/Championship"), Is.Null);
                }
                else if (stage == "completed")
                {
                    Assert.That(labels, Does.Contain("4승 · 우승"));
                    Assert.That(view.transform.Find("PostseasonBoard/Champion/WinnerHost/Winner/Name").GetComponent<Text>().text,
                        Is.EqualTo("2025 롯데 자이언츠"));
                }
                else Assert.That(labels, Does.Contain("우승 구단 대기"));
                if (stage == "two") Assert.That(view.transform.Find("PostseasonBoard/WildCard"), Is.Null);
                if (stage == "waiting")
                    Assert.That(view.transform.Find("PostseasonBoard/Championship/ContentSafeRect/HigherSeed/Name")
                        .GetComponent<Text>().text, Is.EqualTo("2025 KIA 타이거즈"));
                if (stage == "semifinal")
                {
                    string selected = null;
                    view.TeamSelected += key => selected = key;
                    view.transform.Find("PostseasonBoard/WildCard/ContentSafeRect/HigherSeed")
                        .GetComponent<Button>().onClick.Invoke();
                    Assert.That(selected, Is.EqualTo("d"));
                }
            }
            finally { Object.DestroyImmediate(host); }
        }

        private static IEnumerable<TestCaseData> VisualCases()
        {
            foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080),
                new Vector2Int(2560, 1440), new Vector2Int(3440, 1440) })
                foreach (string stage in new[] { "pending", "semifinal", "completed", "two" })
                    yield return new TestCaseData(size.x, size.y, stage);
        }

        [TestCaseSource(nameof(VisualCases))]
        public void 대진표를해상도별로검수한다(int width, int height, string stage)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("그래픽 장치가 필요합니다.");
            var root = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var cameraObject = new GameObject("Camera", typeof(Camera));
            var target = new RenderTexture(width, height, 24);
            Texture2D texture = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.white;
                camera.targetTexture = target;
                Canvas canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                CanvasScaler scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = .5f;
                // 실제 셸의 상단 내비게이션과 상태 바가 차지하는 영역을 남긴다.
                var workspace = new GameObject("Workspace", typeof(RectTransform)).GetComponent<RectTransform>();
                workspace.SetParent(root.transform, false);
                workspace.anchorMin = new Vector2(.015f, .08f);
                workspace.anchorMax = new Vector2(.985f, .86f);
                workspace.offsetMin = workspace.offsetMax = Vector2.zero;
                var view = UI_Scene_OwnerLeague.CreateRuntime(workspace);
                view.Bind(BuildLeague(), Review(stage));
                view.ShowTab(0);
                SelectPostseason(view);
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)root.transform);
                Canvas.ForceUpdateCanvases();
                Assert.That(view.transform.Find("PostseasonBoard/Backdrop").GetComponent<Image>().sprite, Is.Not.Null);
                foreach (Text text in view.GetComponentsInChildren<Text>())
                {
                    RectTransform bounds = text.transform.parent as RectTransform;
                    Vector3[] corners = new Vector3[4];
                    text.rectTransform.GetWorldCorners(corners);
                    foreach (Vector3 corner in corners)
                    {
                        Vector3 point = bounds.InverseTransformPoint(corner);
                        Assert.That(point.x, Is.InRange(bounds.rect.xMin - 1, bounds.rect.xMax + 1), text.name);
                        Assert.That(point.y, Is.InRange(bounds.rect.yMin - 1, bounds.rect.yMax + 1), text.name);
                    }
                }
                camera.Render();
                // 동적 폰트 Atlas가 첫 렌더에서 확장된 뒤 모든 글자 Mesh를 갱신해 캡처한다.
                foreach (Text text in view.GetComponentsInChildren<Text>()) text.SetVerticesDirty();
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                string output = Environment.GetEnvironmentVariable("BASEBALL_LEAGUE_VISUAL_OUTPUT");
                if (!string.IsNullOrEmpty(output))
                {
                    Directory.CreateDirectory(output);
                    File.WriteAllBytes(Path.Combine(output, $"postseason-{stage}-{width}x{height}.png"), texture.EncodeToPNG());
                }
            }
            finally
            {
                RenderTexture.active = previous;
                if (texture != null) Object.DestroyImmediate(texture);
                cameraObject.GetComponent<Camera>().targetTexture = null;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(root);
            }
        }

        private static void SelectPostseason(UI_Scene_OwnerLeague view) =>
            view.transform.Find("PostseasonTab").GetComponent<Button>().onClick.Invoke();

        private static void InvokeEventLifecycle(GameObject events, string method) =>
            typeof(EventSystem).GetMethod(method, System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic).Invoke(events.GetComponent<EventSystem>(), null);

        private static OwnerLeaguePresentationModel BuildLeague()
        {
            var teams = new[] { new ScheduleTeamSnapshot("a", "2025 KIA 타이거즈", "TeamEmblem/1"),
                new ScheduleTeamSnapshot("b", "2025 롯데 자이언츠", "TeamEmblem/2"),
                new ScheduleTeamSnapshot("c", "2025 삼성 라이온즈", "TeamEmblem/3"),
                new ScheduleTeamSnapshot("d", "2025 NC 다이노스", "TeamEmblem/4"),
                new ScheduleTeamSnapshot("e", "2025 두산 베어스", "TeamEmblem/5") };
            return new OwnerLeaguePresentationModel(new ScheduleScreenSnapshot("2025 시즌", "루키 리그", "정규시즌 종료", "a",
                new[] { new ScheduleGameSnapshot("1", 1, "1R", teams[0], teams[3], true, 5, 1, ScheduleFocusSide.None),
                    new ScheduleGameSnapshot("2", 1, "1R", teams[1], teams[2], true, 3, 1, ScheduleFocusSide.None),
                    new ScheduleGameSnapshot("3", 2, "2R", teams[3], teams[4], true, 3, 1, ScheduleFocusSide.None),
                    new ScheduleGameSnapshot("4", 3, "3R", teams[2], teams[3], true, 5, 1, ScheduleFocusSide.None) }));
        }

        private static OwnerSeasonReviewSnapshot Review(string stage, int season = 1)
        {
            var series = new List<OwnerPostseasonSeriesReview>();
            bool finished = stage == "completed";
            bool semifinalsFinished = finished || stage == "waiting";
            if (stage != "pending" && stage != "two")
            {
                series.Add(new OwnerPostseasonSeriesReview("wild-card", OwnerPostseasonRound.WildCard,
                    "d", "e", semifinalsFinished ? 1 : 0, 0, 2, semifinalsFinished));
                if (semifinalsFinished)
                {
                    series.Add(new OwnerPostseasonSeriesReview("semi-playoff", OwnerPostseasonRound.SemiPlayoff,
                        "c", "d", 0, 3, 3, true));
                    series.Add(new OwnerPostseasonSeriesReview("playoff", OwnerPostseasonRound.Playoff,
                        "b", "d", 3, 1, 3, true));
                }
            }
            if (finished || stage == "two")
                series.Add(new OwnerPostseasonSeriesReview("final", OwnerPostseasonRound.Championship,
                    "a", "b", 2, finished ? 4 : 1, 4, finished));
            return new OwnerSeasonReviewSnapshot(season, LeagueGrade.Rookie, null, "a", 1, stage == "two" ? 2 : 5,
                80, 64, 0, 700, 620, stage != "pending", finished, stage != "pending", null,
                finished ? "b" : null, series);
        }
    }
}
