using System.IO;
using System.Linq;
using Baseball.Presentation.Owner;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Baseball.Tests.EditMode.Presentation.Owner
{
    /// <summary>실제 Sprite 임포트·입력 상태·9-slice 렌더링을 검수한다.</summary>
    public sealed class OwnerFrontOfficeSkinTests
    {
        [Test]
        public void 공통이미지는동일크기상태와슬라이스설정을유지한다()
        {
            var sprites = Resources.LoadAll<Sprite>(UIOwnerFrontOfficeSkin.ResourceRoot);
            Assert.That(sprites.Length, Is.EqualTo(29));
            foreach (var sprite in sprites)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sprite));
                Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect));
                Assert.That(importer.mipmapEnabled, Is.False);
                Assert.That(importer.alphaIsTransparency, Is.True);
                Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
                Assert.That(sprite.pixelsPerUnit, Is.EqualTo(100));
                if (!sprite.name.Contains("Badge")) Assert.That(sprite.border.x, Is.GreaterThan(0));
                string family = sprite.name.Substring(0, sprite.name.LastIndexOf('_'));
                var normal = sprites.FirstOrDefault(item => item.name == family + "_Normal");
                if (normal != null) Assert.That(sprite.rect.size, Is.EqualTo(normal.rect.size));
            }
        }

        [Test]
        public void 프리뷰프리팹과상태표는실제UnityImage로렌더링된다()
        {
            var root = new GameObject("FrontOfficeSkinPreview", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var cameraRoot = new GameObject("PreviewCamera", typeof(Camera));
            try
            {
                var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
                var rect = (RectTransform)root.transform; rect.sizeDelta = new Vector2(1920, 1600);
                root.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920,1080);
                var camera = cameraRoot.GetComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 800;
                camera.transform.position = new Vector3(0,0,-100); camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color32(16,23,29,255); canvas.worldCamera = camera;
                string[] families = { "Primary", "Secondary", "Utility", "ListItem", "Tab" };
                for (int row = 0; row < families.Length; row++)
                {
                    string family = families[row];
                    string folder = family == "ListItem" ? "ListItems/UI_ListItem_" : family == "Tab" ? "Tabs/UI_Tab_" : "Buttons/UI_Button_" + family + "_";
                    string[] states = family == "ListItem" ? new[] { "Normal", "Hover", "Selected", "Unread", "Disabled" }
                        : family == "Tab" ? new[] { "Normal", "Hover", "Selected", "Disabled" }
                        : family == "Secondary" ? new[] { "Normal", "Hover", "Pressed", "Disabled", "Selected" }
                        : new[] { "Normal", "Hover", "Pressed", "Disabled" };
                    for (int column = 0; column < states.Length; column++)
                    {
                        float x = 40 + column * 372, y = 50 + row * 160;
                        AddLabel(root.transform, family + " / " + states[column], x, y, 350);
                        AddImage(root.transform, UIOwnerFrontOfficeSkin.Load(folder + states[column]), x, y + 36,
                            family == "Utility" ? 64 : 344, family == "Tab" ? 4 : 72);
                    }
                }
                string[] names = { "MainDashboard", "ManagerCard", "ManagerReport", "CompactStrip" };
                for (int i = 0; i < names.Length; i++)
                {
                    float x = 40 + i * 470;
                    AddLabel(root.transform, names[i], x, 870, 450);
                    AddImage(root.transform, UIOwnerFrontOfficeSkin.Load("Frames/UI_Frame_" + names[i]), x, 906, 440, 320);
                }
                string[] badges = { "Count", "Unread", "Important" };
                for (int i = 0; i < badges.Length; i++)
                {
                    AddLabel(root.transform, "Badge / " + badges[i], 40 + i * 372, 1290, 350);
                    var image = AddImage(root.transform, UIOwnerFrontOfficeSkin.Load("Badges/UI_Badge_" + badges[i]), 40 + i * 372, 1330, badges[i] == "Unread" ? 12 : 40, badges[i] == "Unread" ? 12 : 40);
                    image.type = Image.Type.Simple; image.preserveAspect = true;
                }
                string directory = Path.GetFullPath(Path.Combine(Application.dataPath,"../FrontOfficeScreenshots"));
                Directory.CreateDirectory(directory);
                Capture(camera, 1920, 1600, Path.Combine(directory,"asset-states-dark.png"));
                camera.backgroundColor = new Color32(233,237,240,255);
                Capture(camera, 1920, 1600, Path.Combine(directory,"asset-states-light.png"));
                string prefabDirectory = "Assets/03.Prefabs/FrontOfficeV2";
                Directory.CreateDirectory(prefabDirectory); AssetDatabase.Refresh();
                canvas.worldCamera = null;
                PrefabUtility.SaveAsPrefabAsset(root, prefabDirectory + "/FrontOfficeSkinPreview.prefab");
                foreach (Transform child in root.transform.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
                canvas.worldCamera = camera; camera.backgroundColor = new Color32(16,23,29,255);
                for (int row = 0; row < 3; row++)
                {
                    float factor = 1.5f + row * .5f;
                    AddLabel(root.transform, "9-slice / " + factor.ToString("F1") + "x", 40, 30 + row * 510, 700);
                    for (int column = 0; column < names.Length; column++)
                        AddImage(root.transform, UIOwnerFrontOfficeSkin.Load("Frames/UI_Frame_" + names[column]), 40 + column * 470,
                            70 + row * 510, 176 * factor, 150 * factor);
                }
                Capture(camera,1920,1600,Path.Combine(directory,"nine-slice-150-250.png"));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(cameraRoot); }
        }

        [Test]
        public void 버튼은마우스상태와비활성스프라이트를전환한다()
        {
            var events = new GameObject("Events", typeof(EventSystem));
            var root = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            try
            {
                var button = root.GetComponent<Button>();
                root.AddComponent<UIOwnerFrontOfficeButton>().Configure(button,"Buttons","UI_Button_Primary",true,false);
                var image = root.GetComponent<Image>();
                button.OnPointerEnter(new PointerEventData(events.GetComponent<EventSystem>()));
                Assert.That(image.overrideSprite.name, Is.EqualTo("UI_Button_Primary_Hover"));
                button.OnPointerDown(new PointerEventData(events.GetComponent<EventSystem>()) { button = PointerEventData.InputButton.Left });
                Assert.That(image.overrideSprite.name, Is.EqualTo("UI_Button_Primary_Pressed"));
                button.interactable = false;
                Assert.That(image.overrideSprite.name, Is.EqualTo("UI_Button_Primary_Disabled"));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(events); }
        }

        private static Image AddImage(Transform parent, Sprite sprite, float x, float y, float width, float height)
        {
            Assert.That(sprite, Is.Not.Null);
            var root = new GameObject(sprite.name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)root.transform; rect.SetParent(parent,false);
            rect.anchorMin = rect.anchorMax = new Vector2(0,1); rect.pivot = new Vector2(0,1);
            rect.anchoredPosition = new Vector2(x,-y); rect.sizeDelta = new Vector2(width,height);
            var image = root.GetComponent<Image>(); image.sprite = sprite; image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 2; image.raycastTarget = false; return image;
        }

        private static void AddLabel(Transform parent, string value, float x, float y, float width)
        {
            var root = new GameObject(value,typeof(RectTransform),typeof(Text));
            var rect = (RectTransform)root.transform; rect.SetParent(parent,false);
            rect.anchorMin = rect.anchorMax = new Vector2(0,1); rect.pivot = new Vector2(0,1);
            rect.anchoredPosition = new Vector2(x,-y); rect.sizeDelta = new Vector2(width,32);
            var text = root.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 22; text.text = value; text.color = new Color32(174,150,100,255); text.raycastTarget = false;
        }

        private static void Capture(Camera camera,int width,int height,string path)
        {
            var target = new RenderTexture(width,height,24); var pixels = new Texture2D(width,height,TextureFormat.RGBA32,false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target; Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0,0,width,height),0,0); pixels.Apply(); File.WriteAllBytes(path,pixels.EncodeToPNG());
            }
            finally { camera.targetTexture = null; RenderTexture.active = previous; Object.DestroyImmediate(target); Object.DestroyImmediate(pixels); }
        }
    }
}
