using System;
using System.Linq;
using System.Reflection;
using Baseball.Game.Historical;
using Baseball.Presentation.Owner;
using Baseball.Presentation.SharedUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Baseball.Tests.EditMode.Presentation
{
    /// <summary>실제 버튼 입력이 Game 거래를 확정하고 재료를 정확히 한 번 소비하는지 검증한다.</summary>
    public sealed class SpecialRecruitInteractionTests
    {
        [TestCase(false, 1280, 720)]
        [TestCase(false, 1920, 1080)]
        [TestCase(false, 2560, 1440)]
        [TestCase(false, 3440, 1440)]
        [TestCase(true, 1280, 720)]
        [TestCase(true, 1920, 1080)]
        [TestCase(true, 2560, 1440)]
        [TestCase(true, 3440, 1440)]
        public void SpecialCards_AutoSelectAndConfirmRecruitThroughView(bool legend, int width, int height)
        {
            // Game 테스트의 검증된 거래 Fixture를 재사용하여 표현 테스트에 별도 야구 월드를 만들지 않는다.
            Type fixture = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(
                "Baseball.Tests.EditMode.Game.Historical.ManagerHistoricalSaveTests")).First(t => t != null);
            object[] arguments = { null, null, legend };
            var runtime = (ManagerHistoricalRuntimeState)fixture.GetMethod("CreateSpecialCardRuntime",
                BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, arguments);
            string target = (string)arguments[0];
            string[] materials = (string[])arguments[1];
            var host = new GameObject("SpecialRecruitTest", typeof(RectTransform), typeof(Canvas));
            var cameraObject = new GameObject("RecruitCamera", typeof(Camera));
            var eventSystem = new GameObject("RecruitInput", typeof(EventSystem));
            // EditMode는 EventSystem의 활성화 콜백을 자동 호출하지 않는다.
            typeof(EventSystem).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(eventSystem.GetComponent<EventSystem>(), null);
            var render = new RenderTexture(width, height, 24);
            var managerObject = new GameObject("RecruitManager");
            try
            {
                var manager = managerObject.AddComponent<OwnerModeManager>();
                typeof(OwnerModeManager).GetField("_balance", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(manager, Baseball.Core.Balance.BalanceTable.CreateDefault());
                typeof(OwnerModeManager).GetProperty("Runtime").SetValue(manager, runtime);
                // 구단 표시명이 ContentProvider를 읽으므로 Game Fixture의 같은 팀 정의도 주입한다.
                object fixtureData = fixture.GetNestedType("Fixture", BindingFlags.NonPublic)
                    .GetMethod("Create", BindingFlags.Public | BindingFlags.Static).Invoke(null,
                        new object[] { Baseball.Core.Historical.WorldRecordMode.SimulatedHistory, false });
                typeof(OwnerModeManager).GetField("_contentProvider", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(manager, fixtureData.GetType().GetProperty("Provider").GetValue(fixtureData));
                var camera = cameraObject.GetComponent<Camera>();
                camera.orthographic = true;
                camera.targetTexture = render;
                var canvas = host.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 10;
                var shell = SharedGameShellView.CreateRuntime(host.transform);
                shell.BindProfile(OwnerModeUiProfileFactory.Create());
                shell.BindContext(new ShellContextModel(legend ? UI_Scene_OwnerSpecialRecruit.LegendRoute :
                    UI_Scene_OwnerSpecialRecruit.CareerHighRoute, legend ? "레전드 영입" : "커리어하이 영입"));
                shell.SetInspectorVisible(false);
                shell.SetActionBarVisible(false);
                var view = UI_Scene_OwnerSpecialRecruit.CreateRuntime(shell.MainWorkspaceHost);
                view.ShowRoute(legend ? UI_Scene_OwnerSpecialRecruit.LegendRoute : UI_Scene_OwnerSpecialRecruit.CareerHighRoute);
                view.Bind(manager);
                var content = view.transform.Find("IssuedRecruitContent");
                var confirm = content.Find("ConfirmRecruit").GetComponent<Button>();
                Assert.That(confirm.interactable, Is.False);
                Layout(view);
                Assert.That(content.GetComponentsInChildren<PlayerMiniCardView>().Length, Is.EqualTo(8));
                var front = content.Find("TargetPanel/ContentSafeRect/TargetPreview/Front");
                for (int ability = 0; ability < 6; ability++)
                    Assert.That(front.Find("Value" + ability).GetComponent<Text>().text, Is.Not.Empty,
                        "상세보기와 같은 카드 앞면의 6개 능력치를 표시한다.");
                Assert.That(front.Find("MainFrame").GetComponent<Image>().sprite, Is.Not.Null);
                foreach (var card in content.GetComponentsInChildren<PlayerMiniCardView>())
                    AssertInside((RectTransform)card.transform, (RectTransform)card.transform.parent);
                foreach (var button in content.GetComponentsInChildren<Button>().Where(b => b.name == "ChooseMaterial"))
                {
                    Text label = button.GetComponentInChildren<Text>();
                    Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 1),
                        "720p에서도 재료 버튼의 상태와 행동 문구가 잘리지 않는다.");
                }
                AssertInside((RectTransform)front, (RectTransform)front.parent);
                Capture(camera, render, legend, "empty", width, height);
                var dropdown = view.GetComponentInChildren<UIRecruitTargetDropdown>();
                dropdown.Open();
                Canvas.ForceUpdateCanvases();
                var targetSheet = view.transform.Find("TargetDropdown/Sheet");
                AssertInside((RectTransform)targetSheet, (RectTransform)view.transform);
                var targetName = targetSheet.Find("Option0/Name").GetComponent<Text>();
                Assert.That(targetName.text, Is.Not.Empty);
                Assert.That(targetName.cachedTextGenerator.vertexCount, Is.GreaterThan(4), "한글 후보 이름이 실제 메시로 생성되어야 한다.");
                Capture(camera, render, legend, "dropdown", width, height);
                var search = targetSheet.Find("Search").GetComponent<InputField>();
                search.text = "존재하지않는선수";
                Canvas.ForceUpdateCanvases();
                Assert.That(targetSheet.Find("Empty").gameObject.activeSelf, Is.True);
                Assert.That(view.TryHandleCancel(), Is.True);
                Assert.That(dropdown.IsOpen, Is.False);
                Assert.That(content.GetComponent<CanvasGroup>().interactable, Is.True);
                var choose = content.GetComponentsInChildren<Button>().First(b => b.name == "ChooseMaterial");
                choose.onClick.Invoke();
                Assert.That(view.transform.Find("MaterialPicker").gameObject.activeSelf, Is.True);
                Canvas.ForceUpdateCanvases();
                if (!legend)
                {
                    var picker = view.transform.Find("MaterialPicker");
                    var last = picker.GetComponentsInChildren<Button>().First(b => b.name == "Candidate7");
                    last.Select();
                    AssertInside((RectTransform)last.transform, picker.GetComponentInChildren<ScrollRect>().viewport);
                }
                Capture(camera, render, legend, "picker", width, height);
                Assert.That(view.TryHandleCancel(), Is.True);
                Assert.That(content.GetComponent<CanvasGroup>().interactable, Is.True);
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(choose.gameObject));
                choose.onClick.Invoke();
                view.transform.Find("MaterialPicker").GetComponentsInChildren<Button>()
                    .First(b => b.name == "Candidate0").onClick.Invoke();
                Assert.That(view.transform.Find("MaterialPicker").gameObject.activeSelf, Is.False);
                Assert.That(content.Find("SelectionStatus").GetComponent<Text>().text, Does.Contain("1 / 8"));
                dropdown.Open();
                targetSheet.Find("Option0").GetComponent<Button>().onClick.Invoke();
                Assert.That(content.Find("SelectionStatus").GetComponent<Text>().text, Does.Contain("1 / 8"),
                    "같은 영입 대상을 재선택하면 이미 등록한 재료를 유지한다.");
                runtime.TryGetOwnedCard(materials[7], out var protectedCard);
                protectedCard.IsLocked = true;
                content.Find("AutoSelect").GetComponent<Button>().onClick.Invoke();
                Assert.That(confirm.interactable, Is.False);
                Assert.That(content.Find("SelectionStatus").GetComponent<Text>().text, Does.Contain("7 / 8"));
                protectedCard.IsLocked = false;
                content.Find("AutoSelect").GetComponent<Button>().onClick.Invoke();
                Assert.That(confirm.interactable, Is.True);
                Layout(view);
                Capture(camera, render, legend, "ready", width, height);
                confirm.onClick.Invoke();
                Assert.That(view.TryHandleCancel(), Is.True, "최종 확인 대기만 취소한다.");
                Assert.That(runtime.TryGetOwnedCard(target, out _), Is.False);
                confirm.onClick.Invoke();
                Assert.That(runtime.TryGetOwnedCard(target, out _), Is.False, "최종 확인 전에는 소비하지 않는다.");
                confirm.onClick.Invoke();
                Assert.That(runtime.TryGetOwnedCard(target, out var acquired), Is.True);
                Assert.That(acquired.IsLocked, Is.True);
                Assert.That(materials.All(id => !runtime.TryGetOwnedCard(id, out _)), Is.True);
                Assert.That(confirm.interactable, Is.False);
                Layout(view);
                Capture(camera, render, legend, "owned", width, height);
                dropdown.Bind(Enumerable.Range(0, 25).Select(i => new UIRecruitTargetDropdown.Option(
                    (1990 + i) + " 홍길동", "아주 긴 한국어 구단명 · 해태 타이거즈", i % 2 == 0)).ToArray(),
                    0, legend ? "레전드 · 영입 대상" : "커리어하이 · 영입 대상");
                dropdown.Open();
                Canvas.ForceUpdateCanvases();
                foreach (var label in targetSheet.GetComponentsInChildren<Text>().Where(t => t.name == "Name" || t.name == "Detail"))
                {
                    Assert.That(label.cachedTextGenerator.vertexCount, Is.GreaterThan(4));
                    Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 1));
                }
                Capture(camera, render, legend, "dropdown-long", width, height);
                dropdown.Close();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                UnityEngine.Object.DestroyImmediate(managerObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                typeof(EventSystem).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(eventSystem.GetComponent<EventSystem>(), null);
                UnityEngine.Object.DestroyImmediate(eventSystem);
                render.Release();
                UnityEngine.Object.DestroyImmediate(render);
            }
        }

        [Test]
        public void SpecialCards_Runtime없음은임시옵션과영입버튼을노출하지않는다()
        {
            var host = new GameObject("EmptyRecruit", typeof(RectTransform));
            try
            {
                ((RectTransform)host.transform).sizeDelta = new Vector2(1280, 720);
                var view = UI_Scene_OwnerSpecialRecruit.CreateRuntime((RectTransform)host.transform);
                view.Bind(null);
                Assert.That(view.GetComponentInChildren<UIRecruitTargetDropdown>().OptionCount, Is.Zero);
                Assert.That(view.GetComponentsInChildren<PlayerMiniCardView>(), Is.Empty);
                Assert.That(view.transform.Find("IssuedRecruitContent/ConfirmRecruit").GetComponent<Button>().interactable, Is.False);
                Assert.That(view.transform.Find("IssuedRecruitContent/SelectionStatus").GetComponent<Text>().text, Does.Contain("홈으로"));
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        [Test]
        public void TargetDropdown_SearchAndPagingKeepOriginalSelectionIndex()
        {
            var host = new GameObject("DropdownTest", typeof(RectTransform));
            try
            {
                var root = host.GetComponent<RectTransform>();
                root.sizeDelta = new Vector2(1280, 540);
                var view = UIRecruitTargetDropdown.CreateRuntime(root, root);
                var options = Enumerable.Range(0, 25).Select(i => new UIRecruitTargetDropdown.Option(
                    (1990 + i) + " 홍길동", "아주 긴 한국어 구단명 · 해태 타이거즈", i % 2 == 0)).ToArray();
                int selected = -1;
                view.SelectionChanged += index => selected = index;
                view.Bind(options, 24, "레전드 · 영입 대상");
                view.Open();
                var sheet = root.Find("TargetDropdown/Sheet");
                Assert.That(sheet.Find("Option0/Name").GetComponent<Text>().text, Does.Contain("2014"));
                sheet.Find("PreviousPage").GetComponent<Button>().onClick.Invoke();
                Assert.That(sheet.Find("Option0/Name").GetComponent<Text>().text, Does.Contain("2010"));
                sheet.Find("Search").GetComponent<InputField>().text = "1997";
                sheet.Find("Option0").GetComponent<Button>().onClick.Invoke();
                Assert.That(selected, Is.EqualTo(7), "검색 결과의 행 번호를 원본 후보 번호와 혼동하지 않는다.");
                Assert.That(view.IsOpen, Is.False);
                view.Open();
                root.Find("TargetDropdown/Outside").GetComponent<Button>().onClick.Invoke();
                Assert.That(view.IsOpen, Is.False);
                view.Open();
                view.gameObject.SetActive(false);
                // 일반 MonoBehaviour의 EditMode 검수에서는 생명주기 콜백을 명시적으로 실행한다.
                typeof(UIRecruitTargetDropdown).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(view, null);
                Assert.That(root.Find("TargetDropdown").gameObject.activeSelf, Is.False);
                view.gameObject.SetActive(true);
                view.Bind(null, -1, "레전드");
                view.Open();
                Assert.That(view.IsOpen, Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        private static void Layout(UI_Scene_OwnerSpecialRecruit view)
        {
            Canvas.ForceUpdateCanvases();
            typeof(UI_Scene_OwnerSpecialRecruit).GetMethod("FitCards", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
            Canvas.ForceUpdateCanvases();
        }

        private static void AssertInside(RectTransform child, RectTransform parent)
        {
            var corners = new Vector3[4];
            child.GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                Vector3 local = parent.InverseTransformPoint(corner);
                Assert.That(local.x, Is.InRange(parent.rect.xMin - .1f, parent.rect.xMax + .1f));
                Assert.That(local.y, Is.InRange(parent.rect.yMin - .1f, parent.rect.yMax + .1f));
            }
        }

        private static void Capture(Camera camera, RenderTexture target, bool legend, string state, int width, int height)
        {
            string output = Environment.GetEnvironmentVariable("BASEBALL_RECRUIT_CAPTURE");
            if (string.IsNullOrEmpty(output)) return;
            var previous = RenderTexture.active;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                System.IO.Directory.CreateDirectory(output);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(output,
                    $"{(legend ? "legend" : "careerhigh")}-{state}-{width}.png"), texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
