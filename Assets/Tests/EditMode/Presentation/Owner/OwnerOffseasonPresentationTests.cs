using System.IO;
using System.Linq;
using Baseball.Game.Historical;
using Baseball.Presentation.Owner;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Baseball.Tests.EditMode.Presentation.Owner
{
    /// <summary>일정 확정·취소·중복 입력과 네 해상도의 실제 uGUI 렌더링을 검증한다.</summary>
    public sealed class OwnerOffseasonPresentationTests
    {
        [Test]
        public void 유학대기열은_세명을넘어도_모두조회할수있다()
        {
            var root = new GameObject("Workspace",typeof(RectTransform));
            try
            {
                root.GetComponent<RectTransform>().sizeDelta = new Vector2(1920,840);
                var view=UI_Popup_OwnerOffseason.CreateRuntime(root.GetComponent<RectTransform>());
            var rows=Enumerable.Range(1,7).Select(i=>new OwnerOffseasonTrainingRow("선수 "+i,"유학",2)).ToArray();
                view.Show(new OwnerOffseasonPresentationModel(OwnerSeasonPhase.Offseason,0,rows));
                var next=view.GetComponentsInChildren<Button>().Single(b=>b.name=="NextTraining");
                next.onClick.Invoke(); next.onClick.Invoke();
                Assert.That(view.GetComponentsInChildren<Text>().Any(t=>t.text=="선수 7"),Is.True);
                Assert.That(next.interactable,Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }
        [Test]
        public void 정산확정은이전주차를전달하고중복입력을막으며취소할수있다()
        {
            var root = new GameObject("Workspace", typeof(RectTransform));
            var events = new GameObject("Events", typeof(EventSystem));
            try
            {
                root.GetComponent<RectTransform>().sizeDelta = new Vector2(1920, 840);
                var view = UI_Popup_OwnerOffseason.CreateRuntime(root.GetComponent<RectTransform>());
                int requests = 0;
                int expectedWeek = -1;
                view.WeekAdvanceRequested += week => { requests++; expectedWeek = week; };
                view.Show(Model(2));
                var button = view.GetComponentsInChildren<Button>().Single(item => item.name == "AdvanceWeek");
                button.onClick.Invoke();
                Assert.That(requests, Is.Zero);
                Assert.That(view.TryHandleCancel(), Is.True);
                Assert.That(view.IsVisible, Is.True);
                button.onClick.Invoke(); button.onClick.Invoke(); button.onClick.Invoke();
                Assert.That(requests, Is.EqualTo(1));
                Assert.That(expectedWeek, Is.EqualTo(2));
                Assert.That(button.interactable, Is.False);
                view.ShowError();
                Assert.That(button.interactable, Is.True);
                view.Bind(Model(3));
                button.onClick.Invoke(); button.onClick.Invoke();
                Assert.That(expectedWeek, Is.EqualTo(3));
                view.Bind(Model(4));
                Assert.That(button.interactable, Is.False);
                button.onClick.Invoke();
                Assert.That(requests, Is.EqualTo(2));
                Assert.That(view.TryHandleCancel(), Is.True);
                Assert.That(view.IsVisible, Is.False);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(events); }
        }

        [TestCase(1280, 720, 0)]
        [TestCase(1920, 1080, 2)]
        [TestCase(2560, 1440, 3)]
        [TestCase(3440, 1440, 4)]
        public void 일정표를해상도별로렌더링하고콘텐츠침범을검사한다(int width, int height, int completedWeeks)
        {
            var root = new GameObject("CalendarCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var cameraObject = new GameObject("Camera", typeof(Camera));
            var target = new RenderTexture(width, height, 24);
            var previous = RenderTexture.active;
            Texture2D output = null;
            try
            {
                var camera = cameraObject.GetComponent<Camera>();
                camera.orthographic = true;
                camera.backgroundColor = new Color(.06f, .09f, .14f);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.targetTexture = target;
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                var scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = .5f;
                var workspace = new GameObject("Workspace", typeof(RectTransform)).GetComponent<RectTransform>();
                workspace.SetParent(root.transform, false);
                workspace.anchorMin = new Vector2(.015f, .08f);
                workspace.anchorMax = new Vector2(.985f, .86f);
                workspace.offsetMin = workspace.offsetMax = Vector2.zero;
                var view = UI_Popup_OwnerOffseason.CreateRuntime(workspace);
                Canvas.ForceUpdateCanvases();
                view.Show(Model(completedWeeks));
                Canvas.ForceUpdateCanvases();
                Canvas.ForceUpdateCanvases();
                Assert.That(view.GetComponentInChildren<RawImage>().texture, Is.Not.Null, "ImageGen 원화 연결");
                foreach (var text in view.GetComponentsInChildren<Text>())
                    Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 2), text.name);
                var frame = view.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "OffseasonCalendar");
                var corners = new Vector3[4]; frame.GetWorldCorners(corners);
                foreach (var point in corners)
                    Assert.That(RectTransformUtility.RectangleContainsScreenPoint(workspace, camera.WorldToScreenPoint(point), camera), Is.True);
                camera.Render();
                RenderTexture.active = target;
                output = new Texture2D(width, height, TextureFormat.RGB24, false);
                output.ReadPixels(new Rect(0, 0, width, height), 0, 0); output.Apply();
                string directory = System.Environment.GetEnvironmentVariable("BASEBALL_OFFSEASON_VISUAL_OUTPUT");
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                    File.WriteAllBytes(Path.Combine(directory, $"offseason-{width}x{height}-week{completedWeeks}.png"), output.EncodeToPNG());
                }
            }
            finally
            {
                RenderTexture.active = previous;
                if (output != null) Object.DestroyImmediate(output);
                Object.DestroyImmediate(root); Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void 다음시즌은남은훈련을확인후시작하고파견중에는귀환을요구한다()
        {
            var root = new GameObject("Workspace", typeof(RectTransform));
            try
            {
                root.GetComponent<RectTransform>().sizeDelta = new Vector2(1920, 840);
                var view = UI_Popup_OwnerOffseason.CreateRuntime(root.GetComponent<RectTransform>());
                int starts = 0;
                int training = 0;
                view.SeasonAdvanceRequested += week => starts++;
                view.TrainingRequested += () => training++;
                view.ShowSeasonExit(new OwnerOffseasonPresentationModel(OwnerSeasonPhase.Offseason, 0, null));
                var advance = view.GetComponentsInChildren<Button>().Single(button => button.name == "AdvanceWeek");
                Assert.That(view.GetComponentsInChildren<Text>().Single(text => text.name == "Message").text, Does.Contain("4주"));
                advance.onClick.Invoke(); Assert.That(starts, Is.Zero);
                advance.onClick.Invoke(); Assert.That(starts, Is.EqualTo(1));
                view.ShowSeasonExit(Model(2));
                Assert.That(advance.interactable, Is.False);
                advance.onClick.Invoke(); Assert.That(starts, Is.EqualTo(1));
                view.GetComponentsInChildren<Button>().Single(button => button.name == "Training").onClick.Invoke();
                Assert.That(training, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void 시즌중에는일정을조회하고정산버튼은잠긴다()
        {
            var root = new GameObject("Workspace", typeof(RectTransform));
            try
            {
                root.GetComponent<RectTransform>().sizeDelta = new Vector2(1920, 840);
                var view = UI_Popup_OwnerOffseason.CreateRuntime(root.GetComponent<RectTransform>());
                view.Show(new OwnerOffseasonPresentationModel(OwnerSeasonPhase.Postseason, 0, null));
                Assert.That(view.GetComponentsInChildren<Button>().Single(button => button.name == "AdvanceWeek").interactable, Is.False);
                Assert.That(view.GetComponentsInChildren<Text>().Single(text => text.name == "Message").text, Does.Contain("포스트시즌"));
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static OwnerOffseasonPresentationModel Model(int week) => new OwnerOffseasonPresentationModel(
            OwnerSeasonPhase.Offseason, week, week == 4 ? null : new[]
            {
                new OwnerOffseasonTrainingRow("김도윤", "정교 타격 아카데미", 4 - week),
                new OwnerOffseasonTrainingRow("박현준", "제구 아카데미", 1),
                new OwnerOffseasonTrainingRow("이준서", "수비 전문 학교", 4 - week)
            });
    }
}
