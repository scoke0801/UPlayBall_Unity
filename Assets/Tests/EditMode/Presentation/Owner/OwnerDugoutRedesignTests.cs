using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Baseball.Core.Historical;
using Baseball.Game.Historical;
using Baseball.Presentation.Owner;
using Baseball.Simulation.Historical;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Baseball.Tests.EditMode.Presentation.Owner
{
    /// <summary>덕아웃 임시 인선·방침·취소와 실제 해상도별 표시를 검증한다.</summary>
    public sealed class OwnerDugoutRedesignTests
    {
        [Test]
        public void 저작순서는AI배정을바꾸지않고중복인선은거부한다()
        {
            var text = Resources.Load<TextAsset>("NewGame/DugoutStaffBalance").text;
            var data = JsonUtility.FromJson<DugoutStaffBalanceData>(text);
            Array.Reverse(data.Managers); Array.Reverse(data.Coaches);
            var catalog = data.BuildCatalog();
            Assert.That(catalog.Managers[0].ManagerId, Is.EqualTo("MGR-BALANCED"));
            Assert.That(catalog.HeadCoaches[0].HeadCoachId, Is.EqualTo("HC-CONTACT"));
            data.Managers[0] = data.Managers[1];
            Assert.Throws<ArgumentException>(() => data.BuildCatalog());
        }

        private static IEnumerable<TestCaseData> Cases()
        {
            foreach (var size in new[] { new Vector2Int(1280,720), new Vector2Int(1920,1080), new Vector2Int(2560,1440), new Vector2Int(3440,1440) })
                for (int mode = 0; mode < 3; mode++) yield return new TestCaseData(size.x, size.y, mode);
        }

        [TestCaseSource(nameof(Cases))]
        public void 인선과방침을미리보고취소하며해상도별화면을검증한다(int width, int height, int mode)
        {
            var root = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var cameraObject = new GameObject("Camera", typeof(Camera));
            var events = new GameObject("Events", typeof(EventSystem));
            var target = new RenderTexture(width, height, 24);
            var previous = RenderTexture.active;
            Texture2D texture = null;
            UI_Scene_OwnerDugout view = null;
            try
            {
                var camera = cameraObject.GetComponent<Camera>(); camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Baseball.Presentation.UI.CareerUiTheme.ReferenceCanvas;
                camera.targetTexture = target;
                var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight = .5f;
                var workspace = new GameObject("Workspace", typeof(RectTransform)).GetComponent<RectTransform>(); workspace.SetParent(root.transform, false);
                workspace.anchorMin = new Vector2(.015f,.06f); workspace.anchorMax = new Vector2(.985f,.88f); workspace.offsetMin = workspace.offsetMax = Vector2.zero;
                var snapshot = BuildSnapshot(mode == 1);
                view = UI_Scene_OwnerDugout.CreateRuntime(workspace); view.Bind(snapshot);
                var scene = workspace.Find("OwnerDugoutWorkspace");
                Button FindButton(string name) => scene.GetComponentsInChildren<Button>(true).First(b => b.name == name);
                Text FindText(string name) => scene.GetComponentsInChildren<Text>().First(t => t.name == name);
                Assert.That(FindButton("Confirm").interactable, Is.False);
                var policyRow = scene.GetComponentsInChildren<RectTransform>().First(t => t.name == "PolicyRow0");
                policyRow.Find("PolicySteps/Step4").GetComponent<Button>().onClick.Invoke();
                Assert.That(FindButton("Confirm").interactable, Is.True);
                Assert.That(view.TryHandleCancel(), Is.True);
                Assert.That(FindButton("Confirm").interactable, Is.False);
                // EventSystem은 EditMode에서 OnEnable이 자동 실행되지 않아 명시적으로 등록한다.
                typeof(EventSystem).GetMethod("OnEnable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(events.GetComponent<EventSystem>(), null);
                EventSystem.current = events.GetComponent<EventSystem>();
                var managerButton = FindButton("Manager"); managerButton.Select(); managerButton.onClick.Invoke();
                FindButton("Candidate5").onClick.Invoke();
                var dialog = scene.Find("StaffSelectionOverlay");
                dialog.GetComponentsInChildren<Button>().First(b => b.name == "Confirm").onClick.Invoke();
                Assert.That(FindText("CurrentName").text, Does.Contain("이서윤"));
                Assert.That(policyRow.Find("PolicySteps/Step0").GetComponent<Button>().interactable, Is.True);
                Assert.That(policyRow.Find("PolicySteps/Step4").GetComponent<Button>().interactable, Is.True);
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(managerButton.gameObject));
                if (mode != 1) view.TryHandleCancel();
                if (mode == 2) FindButton("HeadCoach").onClick.Invoke();
                Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)root.transform); Canvas.ForceUpdateCanvases();
                foreach (var art in scene.GetComponentsInChildren<RawImage>()) Assert.That(art.texture, Is.Not.Null, art.name);
                var overflow = new List<string>();
                foreach (var label in scene.GetComponentsInChildren<Text>())
                {
                    if (label.preferredHeight > label.rectTransform.rect.height + 2)
                        overflow.Add(label.name + ": " + label.text + " height=" + label.rectTransform.rect.height + " need=" + label.preferredHeight);
                    var rect = label.rectTransform;
                    var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                    foreach (var corner in corners)
                    {
                        var point = RectTransformUtility.WorldToScreenPoint(camera, corner);
                        Assert.That(point.x, Is.InRange(-1f, width + 1f), label.name);
                        Assert.That(point.y, Is.InRange(-1f, height + 1f), label.name);
                    }
                }
                camera.Render(); foreach (var label in scene.GetComponentsInChildren<Text>()) label.SetVerticesDirty();
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
                texture = new Texture2D(width,height,TextureFormat.RGBA32,false); texture.ReadPixels(new Rect(0,0,width,height),0,0); texture.Apply();
                string output = Environment.GetEnvironmentVariable("BASEBALL_DUGOUT_VISUAL_OUTPUT");
                if (!string.IsNullOrEmpty(output)) { Directory.CreateDirectory(output); File.WriteAllBytes(Path.Combine(output,$"dugout-{mode}-{width}x{height}.png"),texture.EncodeToPNG()); }
                Assert.That(overflow, Is.Empty, string.Join("\n",overflow));
            }
            finally
            {
                RenderTexture.active = previous;
                if (texture != null) Object.DestroyImmediate(texture);
                if (view != null) Object.DestroyImmediate(view.gameObject);
                cameraObject.GetComponent<Camera>().targetTexture = null; target.Release(); Object.DestroyImmediate(target);
                typeof(EventSystem).GetMethod("OnDisable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(events.GetComponent<EventSystem>(), null);
                Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(root); Object.DestroyImmediate(events);
            }
        }

        private static OwnerDugoutSnapshot BuildSnapshot(bool female)
        {
            var catalog = DugoutStaffBalanceLoader.Load();
            var managers = catalog.Managers.Select(item => new OwnerDugoutStaffCandidate(item.ManagerId, item.DisplayName,
                item.StyleName, item.Description, item.TraitDescription, "UI/OwnerDugout/Individuals/" + item.ManagerId)).ToArray();
            var coaches = catalog.HeadCoaches.Select(item => new OwnerDugoutStaffCandidate(item.HeadCoachId, item.DisplayName,
                item.SpecialtyName, item.Description, item.Description, "UI/OwnerDugout/Individuals/" + item.HeadCoachId)).ToArray();
            var state = new DugoutManagementState(female ? "MGR-FLEXIBLE" : "MGR-BALANCED", female ? "HC-DEFENSE" : "HC-CONTACT", DugoutPolicySettings.Neutral);
            return new OwnerDugoutSnapshot(managers,coaches,state.ManagerId,state.HeadCoachId,state.Policy,20,2,
                new DugoutTacticalProfileResolver().Resolve(state,catalog),catalog);
        }
    }
}
