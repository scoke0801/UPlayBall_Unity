using System;
using System.IO;
using System.Reflection;
using Baseball.Core.Historical;
using Baseball.Game.Historical;
using Baseball.Presentation.Owner;
using Baseball.Presentation.SharedScreens;
using Baseball.Presentation.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Baseball.Tests.EditMode.Presentation.Owner
{
    /// <summary>매니저 선택 입력과 네 해상도의 실제 초상·콘텐츠 배치를 검증한다.</summary>
    public sealed class OwnerFrontManagerPopupTests
    {
        [TestCase(1280, 720)]
        [TestCase(1920, 1080)]
        [TestCase(2560, 1440)]
        [TestCase(3440, 1440)]
        public void Popup_세초상과선택입력을안전영역안에표시한다(int width, int height)
        {
            var host = new GameObject("FrontManagerTest", typeof(RectTransform), typeof(Canvas));
            var cameraObject = new GameObject("Camera", typeof(Camera));
            var eventObject = new GameObject("EventSystem", typeof(EventSystem));
            typeof(EventSystem).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(eventObject.GetComponent<EventSystem>(), null);
            var target = new RenderTexture(width, height, 24);
            var fontDefinition = ScriptableObject.CreateInstance<UIProjectFonts>();
            var fontField = typeof(UIProjectFonts).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
            object previousFont = fontField.GetValue(null);
            try
            {
                Font font = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/08.Fonts/esamanru Medium.ttf");
                Assert.That(font, Is.Not.Null);
                typeof(UIProjectFonts).GetField("_medium", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(fontDefinition, font);
                fontField.SetValue(null, fontDefinition);
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = CareerUiTheme.Background;
                camera.targetTexture = target;
                Canvas canvas = host.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 10;
                Canvas.ForceUpdateCanvases();
                var popup = UI_Popup_OwnerFrontManager.CreateRuntime((RectTransform)host.transform);
                string selected = null;
                popup.SelectionRequested += id => selected = id;
                popup.CloseRequested += popup.Hide;
                popup.Show(FrontManagerIds.DefaultAnalysis);
                Canvas.ForceUpdateCanvases();
                int portraitCount = 0;
                foreach (Image image in popup.GetComponentsInChildren<Image>())
                {
                    if (image.name != "Portrait") continue;
                    portraitCount++;
                    Assert.That(image.sprite, Is.Not.Null);
                    Assert.That(image.raycastTarget, Is.False);
                }
                Assert.That(portraitCount, Is.EqualTo(3));
                var corners = new Vector3[4];
                foreach (RectTransform rect in popup.GetComponentsInChildren<RectTransform>())
                {
                    rect.GetWorldCorners(corners);
                    foreach (Vector3 corner in corners)
                    {
                        Vector3 screen = camera.WorldToScreenPoint(corner);
                        Assert.That(screen.x, Is.InRange(-1f, width + 1f), rect.name);
                        Assert.That(screen.y, Is.InRange(-1f, height + 1f), rect.name);
                    }
                }
                foreach (CareerUiFrame frame in popup.GetComponentsInChildren<CareerUiFrame>())
                {
                    foreach (Button button in frame.ContentSafeArea.GetComponentsInChildren<Button>())
                    {
                        button.GetComponent<RectTransform>().GetWorldCorners(corners);
                        foreach (Vector3 corner in corners)
                        {
                            Vector3 local = frame.ContentSafeArea.InverseTransformPoint(corner);
                            Rect safe = frame.ContentSafeArea.rect;
                            Assert.That(local.x, Is.InRange(safe.xMin - 1f, safe.xMax + 1f));
                            Assert.That(local.y, Is.InRange(safe.yMin - 1f, safe.yMax + 1f));
                        }
                    }
                }
                Capture(camera, target);
                Button[] buttons = popup.GetComponentsInChildren<Button>();
                Assert.That(buttons[0].interactable, Is.False);
                Assert.That(buttons[0].GetComponentInChildren<Text>().text, Is.EqualTo("현재 매니저"));
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(buttons[1].gameObject));
                buttons[1].onClick.Invoke();
                Assert.That(selected, Is.EqualTo(FrontManagerIds.DefaultTest));
                buttons[2].onClick.Invoke();
                Assert.That(selected, Is.EqualTo(FrontManagerIds.DefaultEnergetic));
                foreach (Button button in buttons)
                {
                    if (!button.interactable) continue;
                    Assert.That(button.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
                    Assert.That(button.navigation.selectOnRight.transform.IsChildOf(popup.transform), Is.True);
                    Assert.That(button.navigation.selectOnLeft.transform.IsChildOf(popup.transform), Is.True);
                }
                buttons[3].onClick.Invoke();
                Assert.That(popup.gameObject.activeSelf, Is.False);
                popup.Show(FrontManagerIds.DefaultEnergetic);
                Assert.That(buttons[0].interactable, Is.True);
                Assert.That(buttons[2].interactable, Is.False);
                popup.Hide();
                var scene = UI_Scene_OwnerClubInformation.CreateRuntime((RectTransform)host.transform);
                var sceneRect = (RectTransform)scene.transform;
                sceneRect.anchorMin = new Vector2(0f, .02f);
                sceneRect.anchorMax = new Vector2(1f, .84f);
                sceneRect.offsetMin = sceneRect.offsetMax = Vector2.zero;
                scene.Bind(CreateClubModel(FrontManagerIds.DefaultAnalysis));
                scene.ShowTab(true);
                int requests = 0;
                scene.ChangeFrontManagerRequested += () => requests++;
                Button change = scene.GetComponentInChildren<Button>();
                Assert.That(change.name, Is.EqualTo("ChangeFrontManager"));
                change.onClick.Invoke();
                Assert.That(requests, Is.EqualTo(1));
                Sprite previousPortrait = scene.transform.Find("FrontManager/Office/Portrait").GetComponent<Image>().sprite;
                scene.Bind(CreateClubModel(FrontManagerIds.DefaultEnergetic));
                scene.FocusFrontManagerButton();
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(scene.GetComponentInChildren<Button>().gameObject));
                Assert.That(scene.transform.Find("FrontManager/Office/Portrait").GetComponent<Image>().sprite,
                    Is.Not.EqualTo(previousPortrait));
                Canvas.ForceUpdateCanvases();
                RectTransform buttonRect = scene.GetComponentInChildren<Button>().GetComponent<RectTransform>();
                RectTransform officeRect = (RectTransform)scene.transform.Find("FrontManager/Office");
                Assert.That(buttonRect.anchorMax.y, Is.LessThan(officeRect.anchorMin.y));
                Capture(camera, target, "club");
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(cameraObject);
                typeof(EventSystem).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(eventObject.GetComponent<EventSystem>(), null);
                Object.DestroyImmediate(eventObject);
                Object.DestroyImmediate(target);
                fontField.SetValue(null, previousFont);
                Object.DestroyImmediate(fontDefinition);
            }
        }

        private static OwnerClubInformationPresentationModel CreateClubModel(string managerId)
        {
            var home = new OwnerHomeSnapshot("2025 시즌", "1주차", "루키 리그", "서울 불사조", "1위", "다음 경기",
                9999999999L, 99999, 99999, 0, 25, 25, 14, 14, 11, 11, 0, 3, 25, true, "");
            var finance = new OwnerFinanceSnapshot(0, 0, 0, 0, 0, 0);
            var facilityTypes = (FacilityType[])Enum.GetValues(typeof(FacilityType));
            var facilities = new OwnerFacilitySnapshot[facilityTypes.Length];
            for (int index = 0; index < facilities.Length; index++)
                facilities[index] = new OwnerFacilitySnapshot(facilityTypes[index], 0, 1, 100, true, "");
            var operation = new OwnerClubOperationSnapshot(1, 10000, null, false, "최고 단계", 53, 61, null, null,
                TicketPriceTier.Standard, facilities, finance, finance);
            return new OwnerClubInformationPresentationModel(home,
                new OwnerCollectionSnapshot(Array.Empty<OwnerCollectionCardSnapshot>()), operation,
                new ScheduleScreenSnapshot("2025 시즌", "루키 리그", "1주차", "team", Array.Empty<ScheduleGameSnapshot>()),
                "열두글자구단주이름테스트", managerId, region: "서울");
        }

        private static void Capture(Camera camera, RenderTexture target, string name = "manager")
        {
            string output = Environment.GetEnvironmentVariable("BASEBALL_FRONT_MANAGER_CAPTURE");
            if (string.IsNullOrEmpty(output)) return;
            RenderTexture previous = RenderTexture.active;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            try
            {
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                Directory.CreateDirectory(output);
                File.WriteAllBytes(Path.Combine(output, $"{name}-{target.width}.png"), image.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(image); }
        }
    }
}
