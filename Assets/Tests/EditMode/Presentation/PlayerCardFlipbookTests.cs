using System;
using System.IO;
using System.Reflection;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Presentation.Owner;
using Baseball.Presentation.Career;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Baseball.Tests.EditMode.Presentation
{
    /// <summary>실제 카드의 FX 배치·왕복 프레임·재바인딩과 해상도별 합성을 검증한다.</summary>
    public sealed class PlayerCardFlipbookTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void Flipbook_TraversesEveryCellAndRestartsWithoutBlockingInput()
        {
            var host = new GameObject("FxPlayback", typeof(RectTransform));
            var portrait = new GameObject("Portrait", typeof(RectTransform));
            portrait.transform.SetParent(host.transform, false);
            try
            {
                UIPlayerCardFlipbook.Bind((RectTransform)host.transform, Vector2.zero, Vector2.one, portrait.transform);
                UIPlayerCardFlipbook.Bind((RectTransform)host.transform, Vector2.zero, Vector2.one, portrait.transform);
                var effects = host.GetComponentsInChildren<UIPlayerCardFlipbook>();
                Assert.That(effects.Length, Is.EqualTo(1));
                var effect = effects[0];
                Invoke(effect, "OnEnable");
                RawImage image = effect.GetComponent<RawImage>();
                Assert.That(image.texture, Is.Not.Null);
                Assert.That(image.raycastTarget, Is.False);
                Assert.That(effect.transform.GetSiblingIndex(), Is.LessThan(portrait.transform.GetSiblingIndex()));
                for (int step = 0; step < 30; step++)
                {
                    Sample(effect, step);
                    int expected = step <= 15 ? step : 30 - step;
                    Assert.That(image.uvRect.x, Is.EqualTo(expected % 4 / 4f + .5f / 1024).Within(.00001f));
                    Assert.That(image.uvRect.y, Is.EqualTo(1 - (expected / 4 + 1) / 4f + .5f / 1536).Within(.00001f));
                }
                Invoke(effect, "OnEnable");
                Assert.That(image.uvRect.y, Is.GreaterThan(.75f));
            }
            finally { Object.DestroyImmediate(host); }
        }

        [TestCase(1280, 720)]
        [TestCase(1920, 1080)]
        [TestCase(2560, 1440)]
        [TestCase(3440, 1440)]
        public void Gallery_AloneAddsFlipbookWhenEnabledAndKeepsInput(int width, int height)
        {
            var host = new GameObject("CardFxTest", typeof(RectTransform), typeof(Canvas));
            var cameraObject = new GameObject("Camera", typeof(Camera));
            var galleryObject = new GameObject("GalleryController", typeof(RectTransform), typeof(UI_Scene_NewGame));
            var target = new RenderTexture(width, height, 24);
            var fontDefinition = ScriptableObject.CreateInstance<UIProjectFonts>();
            FieldInfo defaultFontField = typeof(UIProjectFonts).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
            object previousFont = defaultFontField.GetValue(null);
            try
            {
                // FX 합성 검수는 폰트 설정 저작 작업과 분리하고 프로젝트 원본 Medium을 사용한다.
                Font font = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/08.Fonts/esamanru Medium.ttf");
                Assert.That(font, Is.Not.Null, "프로젝트 원본 폰트가 검증 프로젝트에 필요합니다.");
                typeof(UIProjectFonts).GetField("_medium", PrivateInstance).SetValue(fontDefinition, font);
                defaultFontField.SetValue(null, fontDefinition);
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.08f, .09f, .11f);
                camera.targetTexture = target;
                Canvas canvas = host.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 10;
                var frontObject = new GameObject("Front", typeof(RectTransform));
                var front = (RectTransform)frontObject.transform;
                front.SetParent(host.transform, false);
                front.anchorMin = front.anchorMax = new Vector2(.42f, .5f);
                front.sizeDelta = new Vector2(height * .48f, height * .9f);
                var values = new int[PlayerAbilityCatalog.AbilityCount];
                for (int index = 0; index < values.Length; index++) values[index] = 85;
                var card = new OwnerCollectionCardSnapshot("FX-C", "FX-P", "김선수", 2025,
                    PlayerPosition.Shortstop, 10, PlayerCardEdition.Legend, 0, 0, false, false,
                    new AbilityRatings(values));
                typeof(UI_Popup_OwnerPlayerCard).GetMethod("BuildFrontCard", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { front, card });
                var mini = PlayerMiniCardView.CreateRuntime(host.transform);
                mini.UseLineupSlotLayout();
                var miniRect = (RectTransform)mini.transform;
                miniRect.anchorMin = miniRect.anchorMax = new Vector2(.73f, .5f);
                miniRect.sizeDelta = new Vector2(height * .24f, height * .36f);
                var model = new PlayerMiniCardModel("FX-P", "김선수", "유격수", "25", "10", "레전드",
                    frameEdition: PlayerCardEdition.Legend, cost: 10);
                mini.Bind(model, front.Find("PortraitWindow/Silhouette").GetComponent<Image>().sprite);
                mini.Bind(model, front.Find("PortraitWindow/Silhouette").GetComponent<Image>().sprite);
                Assert.That(host.GetComponentsInChildren<UIPlayerCardFlipbook>(), Is.Empty,
                    "일반 공용 카드에는 FX가 없어야 합니다.");
                var gallery = galleryObject.GetComponent<UI_Scene_NewGame>();
                FieldInfo enabledField = typeof(UI_Scene_NewGame).GetField("_showCardGalleryFx", PrivateInstance);
                MethodInfo attach = typeof(UI_Scene_NewGame).GetMethod("AttachGalleryFx", PrivateInstance);
                enabledField.SetValue(gallery, false);
                attach.Invoke(gallery, new object[] { front, 6, false });
                attach.Invoke(gallery, new object[] { miniRect, 6, true });
                Assert.That(host.GetComponentsInChildren<UIPlayerCardFlipbook>(), Is.Empty, "FX OFF는 광채를 만들지 않습니다.");
                enabledField.SetValue(gallery, true);
                attach.Invoke(gallery, new object[] { front, 6, false });
                attach.Invoke(gallery, new object[] { miniRect, 6, true });
                var effects = host.GetComponentsInChildren<UIPlayerCardFlipbook>();
                Assert.That(effects.Length, Is.EqualTo(2));
                foreach (var effect in effects)
                {
                    // EditMode에서는 MonoBehaviour 생명주기를 명시적으로 호출한다.
                    Invoke(effect, "OnEnable");
                    RawImage image = effect.GetComponent<RawImage>();
                    Assert.That(image.texture, Is.Not.Null);
                    Assert.That(image.texture.name, Is.EqualTo("PlayerCardFX_GoldHolographic_Flipbook_v2"));
                    Assert.That(image.raycastTarget, Is.False);
                    Assert.That(image.texture.width, Is.EqualTo(1024));
                    Assert.That(image.texture.height, Is.EqualTo(1536));
                    for (int step = 0; step < 30; step++)
                    {
                        Sample(effect, step);
                        int expected = step <= 15 ? step : 30 - step;
                        Assert.That(image.uvRect.x, Is.EqualTo(expected % 4 / 4f + .5f / 1024).Within(.00001f));
                        Assert.That(image.uvRect.y, Is.EqualTo(1 - (expected / 4 + 1) / 4f + .5f / 1536).Within(.00001f));
                    }
                    Invoke(effect, "OnEnable");
                    Assert.That(image.uvRect.y, Is.GreaterThan(.75f));
                }
                Transform photoWindow = front.Find("PortraitWindow");
                Assert.That(photoWindow.Find("PortraitFlipbook").GetSiblingIndex(),
                    Is.LessThan(photoWindow.Find("Silhouette").GetSiblingIndex()));
                Assert.That(photoWindow.GetComponent<Mask>(), Is.Not.Null);
                Assert.That(mini.GetComponent<Button>().interactable, Is.True);
                foreach (int step in new[] { 0, 7, 15 })
                {
                    foreach (var effect in effects) Sample(effect, step);
                    Canvas.ForceUpdateCanvases();
                    Capture(camera, target, step);
                }
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(galleryObject);
                defaultFontField.SetValue(null, previousFont);
                Object.DestroyImmediate(fontDefinition);
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        private static void Sample(UIPlayerCardFlipbook effect, int step)
        {
            typeof(UIPlayerCardFlipbook).GetField("_elapsed", PrivateInstance).SetValue(effect, (step + .01f) / 12f);
            Invoke(effect, "RefreshFrame");
        }

        private static void Invoke(UIPlayerCardFlipbook effect, string method) =>
            typeof(UIPlayerCardFlipbook).GetMethod(method, PrivateInstance).Invoke(effect, null);

        private static void Capture(Camera camera, RenderTexture target, int step)
        {
            string output = Environment.GetEnvironmentVariable("BASEBALL_CARD_FX_CAPTURE");
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
                File.WriteAllBytes(Path.Combine(output, $"cards-{target.width}-frame{step}.png"), image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(image);
            }
        }
    }
}
