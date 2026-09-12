using System;
using System.IO;
using System.Reflection;
using Baseball.Presentation.Match.Sprites;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Baseball.Presentation.Tests
{
    /// <summary>카드 계보 공유·공수 의상 배정과 실제 셰이더 렌더링을 검증한다.</summary>
    public sealed class MatchUniformPresentationTests
    {
        [Serializable] private sealed class Catalog { public Lineage[] lineages; }
        [Serializable] private sealed class Lineage { public string uniform; public string[] franchises; }
        private static Material Resolve(string franchise) => (Material)typeof(SpriteActor).Assembly
            .GetType("Baseball.Presentation.Match.Sprites.MatchUniformMaterials", true)
            .GetMethod("GetForFranchise", BindingFlags.Static | BindingFlags.Public).Invoke(null, new object[] { franchise });

        [Test]
        public void EveryCardLineageSharesOneUniformIncludingSameClubOpponents()
        {
            Catalog catalog = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("UI/Portraits/player_uniform_assignments").text);
            foreach (Lineage lineage in catalog.lineages)
            {
                Material expected = Resolve(lineage.franchises[0]);
                Assert.That(expected.name, Is.EqualTo("MatchUniform-" + lineage.uniform));
                Assert.That(expected.shader.isSupported, Is.True);
                foreach (string franchise in lineage.franchises)
                    Assert.That(Resolve(franchise), Is.SameAs(expected));
            }
            Assert.That(Resolve(""), Is.Null);
        }

        [Test]
        public void SwappingSidesUpdatesBatterAllRunnersAndAllNineFielders()
        {
            var root = new GameObject("UniformTest", typeof(RectTransform));
            try
            {
                var catalog = Resources.Load<SpriteAnimationCatalog>("UI/SpriteMatch/AnimationCatalog");
                var stage = new SpriteMatchStage(root.GetComponent<RectTransform>(), catalog, null);
                Material away = Resolve("FRANCHISE_35294c0c8039e3d5d238");
                Material home = Resolve("FRANCHISE_a23e5d3c82759518a0e1");
                Assert.That(away, Is.Not.SameAs(home));
                for (int half = 0; half < 2; half++)
                {
                    Material offense = half == 0 ? away : home, defense = half == 0 ? home : away;
                    stage.SetUniforms(offense, defense);
                    int actors = 0;
                    foreach (Image image in root.GetComponentsInChildren<Image>(true))
                    {
                        if (image.name != "Pose") continue;
                        bool isFielder = image.transform.parent.name.StartsWith("Fielder", StringComparison.Ordinal);
                        Assert.That(image.material, Is.SameAs(isFielder ? defense : offense), image.transform.parent.name);
                        actors++;
                    }
                    Assert.That(actors, Is.EqualTo(14));
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void RenderUniformReview()
        {
            string[] clips = { "Pitcher.Pitch.R", "Catcher.Idle", "Fielder.InfieldGrounder", "Batter.DuelSwing.R", "Runner.Run" };
            var cameraRoot = new GameObject("UniformReviewCamera", typeof(Camera));
            var canvasRoot = new GameObject("UniformReview", typeof(RectTransform), typeof(Canvas));
            var target = new RenderTexture(1600, 900, 24);
            var capture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                Camera camera = cameraRoot.GetComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 450;
                camera.transform.position = new Vector3(0, 0, -100);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.12f, 0.15f, 0.14f);
                camera.targetTexture = target;
                camera.cullingMask = 1 << 5;
                Canvas canvas = canvasRoot.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;
                canvasRoot.GetComponent<RectTransform>().sizeDelta = new Vector2(1600, 900);
                for (int row = 0; row < 2; row++)
                for (int col = 0; col < clips.Length; col++)
                {
                    var go = new GameObject("UniformActor", typeof(RectTransform), typeof(Image)) { layer = 5 };
                    go.transform.SetParent(canvasRoot.transform, false);
                    Image image = go.GetComponent<Image>();
                    image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/04.Images/SpriteMatch/" + clips[col] + "/frame_000.png");
                    Assert.That(image.sprite, Is.Not.Null);
                    image.material = Resolve(row == 0 ? "FRANCHISE_35294c0c8039e3d5d238" : "FRANCHISE_a23e5d3c82759518a0e1");
                    image.preserveAspect = true;
                    image.rectTransform.sizeDelta = new Vector2(290, 390);
                    image.rectTransform.anchoredPosition = new Vector2(-640 + col * 320, 220 - row * 440);
                }
                RenderTexture.active = target;
                GL.Clear(true, true, camera.backgroundColor);
                GL.PushMatrix();
                GL.LoadPixelMatrix(0, 1600, 0, 900);
                foreach (Image image in canvasRoot.GetComponentsInChildren<Image>())
                {
                    var material = new Material(image.material);
                    try
                    {
                        Sprite sprite = image.sprite;
                        material.mainTexture = sprite.texture;
                        material.SetPass(0);
                        Rect uv = sprite.textureRect;
                        float scale = Mathf.Min(290 / uv.width, 390 / uv.height);
                        Vector2 center = image.rectTransform.anchoredPosition + new Vector2(800, 450);
                        float left = center.x - uv.width * scale / 2, right = center.x + uv.width * scale / 2;
                        float bottom = center.y - uv.height * scale / 2, top = center.y + uv.height * scale / 2;
                        float u0 = uv.xMin / sprite.texture.width, u1 = uv.xMax / sprite.texture.width;
                        float v0 = uv.yMin / sprite.texture.height, v1 = uv.yMax / sprite.texture.height;
                        GL.Begin(GL.QUADS);
                        GL.Color(Color.white);
                        GL.TexCoord2(u0, v0); GL.Vertex3(left, bottom, 0);
                        GL.TexCoord2(u1, v0); GL.Vertex3(right, bottom, 0);
                        GL.TexCoord2(u1, v1); GL.Vertex3(right, top, 0);
                        GL.TexCoord2(u0, v1); GL.Vertex3(left, top, 0);
                        GL.End();
                    }
                    finally { Object.DestroyImmediate(material); }
                }
                GL.PopMatrix();
                capture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                capture.Apply();
                int visiblePixels = 0;
                foreach (Color32 pixel in capture.GetPixels32())
                    if (pixel.r > 100 || pixel.g > 100 || pixel.b > 100) visiblePixels++;
                Assert.That(visiblePixels, Is.GreaterThan(10000), "검수 이미지에 실제 선수 그림이 있어야 합니다.");
                Directory.CreateDirectory("output/owner-match-uniforms");
                File.WriteAllBytes("output/owner-match-uniforms/lg-samsung.png", capture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(canvasRoot);
                Object.DestroyImmediate(cameraRoot);
                Object.DestroyImmediate(capture);
                Object.DestroyImmediate(target);
            }
        }
    }
}
