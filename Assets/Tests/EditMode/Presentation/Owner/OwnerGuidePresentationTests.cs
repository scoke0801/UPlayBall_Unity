using System;
using System.IO;
using System.Linq;
using Baseball.Core.Historical;
using Baseball.Game.Guide;
using Baseball.Presentation.Guide;
using Baseball.Presentation.Owner;
using Baseball.Presentation.SharedUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Baseball.Tests.EditMode.Presentation.Owner
{
    /// <summary>실제 uGUI의 예약 공간·텍스트·버튼·초상화를 해상도별로 렌더링하고 검사한다.</summary>
    public sealed class OwnerGuidePresentationTests
    {
        [TestCase(1280, 720)]
        [TestCase(1920, 1080)]
        [TestCase(2560, 1440)]
        [TestCase(3440, 1440)]
        public void 안내는안전영역과버튼을침범하지않는다(int width, int height)
        {
            var root = new GameObject("GuideCanvas", typeof(RectTransform), typeof(Canvas));
            var cameraObject = new GameObject("GuideCamera", typeof(Camera));
            var target = new RenderTexture(width, height, 24);
            var pixels = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
                var rect = (RectTransform)root.transform; rect.sizeDelta = new Vector2(width, height);
                var camera = cameraObject.GetComponent<Camera>();
                camera.orthographic = true; camera.orthographicSize = height / 2f;
                camera.transform.position = new Vector3(0, 0, -100); camera.nearClipPlane = 0.1f; camera.farClipPlane = 500;
                camera.targetTexture = target; canvas.worldCamera = camera;
                var shell = SharedGameShellView.CreateRuntime(root.transform);
                shell.BindProfile(OwnerModeUiProfileFactory.Create());
                shell.BindContext(new ShellContextModel(OwnerNavigationRoutes.RosterLineup, "선수 오더", ""));
                var copy = OwnerGuidePresentationData.Load(); copy.textScale = 1.3f;
                var view = UI_System_OwnerGuide.Create(shell, copy);
                var state = new GuideProgressState();
                state.Reconcile("scope", 0, new[] { new GuideGoal("warning", GuideGoalKind.PresetIssue,
                    GuideTargetKind.PresetSlot, false, nameof(LineupPresetValidationIssueCode.OffPositionAssignment)) });
                view.Bind(state, ""); view.SetOpen(true);
                foreach (string message in new[] { copy.preparation, copy.confirmation, string.Format(copy.debrief, 12, 11), copy.snoozedEmpty })
                {
                    view.SetFeedback(message);
                    Canvas.ForceUpdateCanvases();
                    var body = view.GetComponentsInChildren<Text>().Single(text => text.name == "Evidence");
                    Assert.That(body.preferredHeight, Is.LessThanOrEqualTo(body.rectTransform.rect.height + 1), message);
                }
                view.Bind(state, "");
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)view.transform);
                Canvas.ForceUpdateCanvases();
                Bounds guide = RectTransformUtility.CalculateRelativeRectTransformBounds(rect, view.transform);
                Bounds workspace = RectTransformUtility.CalculateRelativeRectTransformBounds(rect, shell.MainWorkspaceHost);
                Bounds action = RectTransformUtility.CalculateRelativeRectTransformBounds(rect, shell.ContextActionBarHost);
                Assert.That(guide.min.y, Is.GreaterThanOrEqualTo(action.max.y));
                Assert.That(guide.max.y, Is.LessThanOrEqualTo(workspace.min.y));
                Assert.That(guide.min.x, Is.GreaterThanOrEqualTo(-width / 2f));
                Assert.That(guide.max.x, Is.LessThanOrEqualTo(width / 2f));
                foreach (Text text in view.GetComponentsInChildren<Text>())
                {
                    Assert.That(text.resizeTextForBestFit, Is.False, text.name);
                    Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1), text.name);
                    Assert.That(text.text, Does.Not.Contain("OffPositionAssignment"));
                }
                var buttons = view.GetComponentsInChildren<Button>();
                foreach (Button button in buttons) Assert.That(button.GetComponent<OwnerUiButtonSkin>(), Is.Not.Null, button.name);
                for (int first = 0; first < buttons.Length; first++)
                    for (int second = first + 1; second < buttons.Length; second++)
                    {
                        Bounds a = RectTransformUtility.CalculateRelativeRectTransformBounds(rect, buttons[first].transform);
                        Bounds b = RectTransformUtility.CalculateRelativeRectTransformBounds(rect, buttons[second].transform);
                        Assert.That(a.Intersects(b), Is.False, buttons[first].name + "/" + buttons[second].name);
                    }
                camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
                string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../GuideScreenshots"));
                Directory.CreateDirectory(directory);
                File.WriteAllLines(Path.Combine(directory, $"guide-colors-{width}.txt"),
                    view.GetComponentsInChildren<Image>().Select(item => item.name + " " + item.color + " " + item.rectTransform.rect));
                File.WriteAllBytes(Path.Combine(directory, $"guide-{width}x{height}.png"), pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(root); Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
            }
        }

        [Test]
        public void 닫기는원래포커스를복원하고안내버튼은게임명령을대신하지않는다()
        {
            var root = new GameObject("GuideFocus", typeof(RectTransform));
            var eventRoot = new GameObject("GuideEvents", typeof(EventSystem));
            EventSystem previousEventSystem = EventSystem.current;
            typeof(EventSystem).GetMethod("OnEnable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(eventRoot.GetComponent<EventSystem>(), null);
            EventSystem.current = eventRoot.GetComponent<EventSystem>();
            try
            {
                var shell = SharedGameShellView.CreateRuntime(root.transform);
                var view = UI_System_OwnerGuide.Create(shell, OwnerGuidePresentationData.Load());
                var state = new GuideProgressState();
                state.Reconcile("scope", 0, new[] { new GuideGoal("warning", GuideGoalKind.PresetIssue,
                    GuideTargetKind.PresetSlot, false, "OffPositionAssignment") });
                view.Bind(state, "");
                Button dock = view.GetComponentsInChildren<Button>(true).Single(button => button.name == "ManagerDock");
                eventRoot.GetComponent<EventSystem>().SetSelectedGameObject(dock.gameObject);
                int navigations = 0; view.ActionRequested += goal => navigations++;
                view.SetOpen(true);
                view.GetComponentsInChildren<Button>().Single(button => button.name == "Navigate").onClick.Invoke();
                Assert.That(navigations, Is.EqualTo(1));
                Assert.That(state.GetStatus("warning"), Is.EqualTo(GuideGoalStatus.Pending));
                view.SetOpen(false);
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(dock.gameObject));
            }
            finally
            {
                Object.DestroyImmediate(root);
                typeof(EventSystem).GetMethod("OnDisable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(eventRoot.GetComponent<EventSystem>(), null);
                Object.DestroyImmediate(eventRoot);
                if (previousEventSystem != null) EventSystem.current = previousEventSystem;
            }
        }
    }
}
