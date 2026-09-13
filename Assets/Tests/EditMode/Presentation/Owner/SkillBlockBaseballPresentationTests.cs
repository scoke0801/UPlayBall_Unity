using System;
using System.IO;
using System.Reflection;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Core.Historical;
using Baseball.Game.Historical;
using Baseball.Presentation.UI;
using Baseball.Presentation.Owner;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Baseball.Tests.EditMode.Presentation.Owner
{
    /// <summary>등급·계통의 분리, 회전 후 글자 방향과 실제 축소 렌더링을 확인한다.</summary>
    public sealed class SkillBlockBaseballPresentationTests
    {
        private static readonly Type Visual = typeof(UIProjectFonts).Assembly.GetType("Baseball.Presentation.UI.SkillBlockVisual");

        [TestCase(1280, 720)]
        [TestCase(1920, 1080)]
        [TestCase(2560, 1440)]
        [TestCase(3440, 1440)]
        public void Visual_현재스택목록의성장화면을출력한다(int width, int height)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("그래픽 장치가 필요합니다.");
            var fixture = new OwnerGrowthPresentationTests();
            var cameraObject = new GameObject("GrowthCamera", typeof(Camera));
            var target = new RenderTexture(width, height, 24);
            Texture2D output = null;
            try
            {
                fixture.SetUp();
                var root = (GameObject)typeof(OwnerGrowthPresentationTests).GetField("_root",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(fixture);
                var view = (UI_Scene_OwnerGrowth)typeof(OwnerGrowthPresentationTests).GetField("_view",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(fixture);
                BindOccupiedBoard(view);
                view.ShowRoute(OwnerNavigationRoutes.PowerUpSkills);
                Button first = null;
                foreach (Button button in root.GetComponentsInChildren<Button>())
                    if (button.name.StartsWith("BlockStack_", StringComparison.Ordinal)) { first = button; break; }
                Assert.That(first, Is.Not.Null, "현재 스택 목록에서 실제 블록을 선택한다.");
                first.onClick.Invoke();
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color32(13, 29, 47, 255);
                camera.orthographic = true;
                camera.targetTexture = target;
                Canvas canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                Canvas.ForceUpdateCanvases();
                typeof(UI_Scene_OwnerGrowth).GetMethod("Resize", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(view, null);
                Canvas.ForceUpdateCanvases();
                foreach (Text label in root.GetComponentsInChildren<Text>())
                    if (label.name == "GradeLabel") label.SendMessage("LateUpdate");
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                output = new Texture2D(width, height, TextureFormat.RGBA32, false);
                output.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                output.Apply();
                RenderTexture.active = previous;
                string directory = Environment.GetEnvironmentVariable("BASEBALL_GROWTH_VISUAL_OUTPUT")
                    ?? Path.GetFullPath("Temp/SkillBaseballReview");
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(Path.Combine(directory, "baseball-growth-" + width + ".png"), output.EncodeToPNG());
            }
            finally
            {
                fixture.TearDown();
                if (output != null) Object.DestroyImmediate(output);
                Object.DestroyImmediate(cameraObject);
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        private static void BindOccupiedBoard(UI_Scene_OwnerGrowth view)
        {
            var source = (OwnerGrowthSnapshot)typeof(UI_Scene_OwnerGrowth).GetField("_snapshot",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
            SkillBlockCategory[] categories = { SkillBlockCategory.Contact, SkillBlockCategory.Power,
                SkillBlockCategory.Baserunning, SkillBlockCategory.Defense, SkillBlockCategory.BatterMental,
                SkillBlockCategory.Bunt };
            PlayerAbility[] abilities = { PlayerAbility.Contact, PlayerAbility.Power, PlayerAbility.Speed,
                PlayerAbility.Defense, PlayerAbility.BatterMental, PlayerAbility.Bunt };
            var definitions = new SkillBlockDefinition[12];
            var inventory = new SkillBlockInstance[12];
            var placements = new PlacedSkillBlock[4];
            for (int index = 0; index < definitions.Length; index++)
            {
                definitions[index] = new SkillBlockDefinition("visual" + index, (SkillBlockRarity)((index + 3) % 6),
                    categories[index % 6], TetrominoShapeCatalog.CreateCells(TetrominoShape.I), true,
                    new[] { new AbilityChange(abilities[index % 6], 1) }, 100);
                inventory[index] = new SkillBlockInstance(index + 1, definitions[index].BlockId);
                if (index < placements.Length) placements[index] = new PlacedSkillBlock(inventory[index], 0, index, 0);
            }
            var card = new OwnerGrowthCardSnapshot(source.Cards[0].Card, placements, source.Cards[0].Studies);
            view.Bind(new OwnerGrowthSnapshot(new[] { card }, inventory, definitions, source.Board, 250, 0, 2,
                new OwnerSchedulePermission(true, string.Empty), OwnerSeasonPhase.Offseason));
        }

        [Test]
        public void Grade_재사용과회전에도문자는정방향이고타입색은등급과독립이다()
        {
            var root = new GameObject("Tile", typeof(RectTransform), typeof(RawImage));
            try
            {
                var tile = root.GetComponent<RawImage>();
                tile.rectTransform.sizeDelta = new Vector2(64, 64);
                BoardCell[] cells = TetrominoShapeCatalog.CreateCells(TetrominoShape.T);
                MethodInfo apply = Visual.GetMethod("ApplyDirectionalTile");
                Texture first = null;
                for (int grade = 0; grade < SkillBlockGradeCatalog.Count; grade++)
                {
                    apply.Invoke(null, new object[] { tile, (SkillBlockRarity)grade, cells, 0, 0, Color.blue });
                    if (first == null) first = tile.texture;
                    Assert.That(tile.texture, Is.SameAs(first), "등급은 바탕색을 바꾸지 않는다.");
                    Assert.That(tile.GetComponentsInChildren<Text>().Length, Is.EqualTo(1));
                    Text label = tile.GetComponentInChildren<Text>();
                    Assert.That(label.text, Is.EqualTo(SkillBlockGradeCatalog.GetLabel((SkillBlockRarity)grade)));
                    Assert.That(label.raycastTarget, Is.False);
                    apply.Invoke(null, new object[] { tile, (SkillBlockRarity)grade, cells, 0, 1, Color.blue });
                    Assert.That(label.transform.localEulerAngles, Is.EqualTo(Vector3.zero));
                    tile.color = new Color(1, 1, 1, .3f);
                    label.SendMessage("LateUpdate");
                    Assert.That(label.color.a, Is.EqualTo(.3f).Within(.001f));
                    tile.enabled = false;
                    label.SendMessage("LateUpdate");
                    Assert.That(label.color.a, Is.Zero);
                }
                apply.Invoke(null, new object[] { tile, SkillBlockRarity.Normal, cells, 0, 0, Color.red });
                Assert.That(tile.texture, Is.Not.SameAs(first), "타입이 다르면 바탕색도 다르다.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Visual_모든등급과타입을실제칸크기로출력한다()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("그래픽 장치가 필요합니다.");
            var root = new GameObject("SkillSheet", typeof(RectTransform), typeof(Canvas));
            var cameraObject = new GameObject("SkillCamera", typeof(Camera));
            var target = new RenderTexture(1200, 700, 24);
            Texture2D output = null;
            try
            {
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color32(13, 29, 47, 255);
                camera.orthographic = true;
                camera.targetTexture = target;
                Canvas canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                Canvas.ForceUpdateCanvases();
                int[] sizes = { 60, 36, 24 };
                for (int row = 0; row < sizes.Length; row++)
                for (int grade = 0; grade < SkillBlockGradeCatalog.Count; grade++)
                {
                    Color color = (Color)Visual.GetMethod("GetCategoryColor").Invoke(null,
                        new object[] { (SkillBlockCategory)(row * 4) });
                    Visual.GetMethod("Create").Invoke(null, new object[]
                    {
                        root.transform, TetrominoShapeCatalog.CreateCells(TetrominoShape.L), 0,
                        (SkillBlockRarity)grade, new Vector2(-490 + grade * 196, 180 - row * 210),
                        new Vector2(180, 230), (float)sizes[row], "Grade" + row + grade, color
                    });
                }
                Canvas.ForceUpdateCanvases();
                foreach (Text label in root.GetComponentsInChildren<Text>()) label.SendMessage("LateUpdate");
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                output = new Texture2D(1200, 700, TextureFormat.RGBA32, false);
                output.ReadPixels(new Rect(0, 0, 1200, 700), 0, 0);
                output.Apply();
                RenderTexture.active = previous;
                string directory = Environment.GetEnvironmentVariable("BASEBALL_GROWTH_VISUAL_OUTPUT")
                    ?? Path.GetFullPath("Temp/SkillBaseballReview");
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(Path.Combine(directory, "baseball-grades.png"), output.EncodeToPNG());
            }
            finally
            {
                if (output != null) Object.DestroyImmediate(output);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(cameraObject);
                target.Release();
                Object.DestroyImmediate(target);
            }
        }
    }
}
