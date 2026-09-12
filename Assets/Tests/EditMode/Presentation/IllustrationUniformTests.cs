using System;
using System.IO;
using System.Reflection;
using Baseball.Presentation.Match;
using Baseball.Presentation.Match.Sprites;
using Baseball.Simulation.Match;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Baseball.Presentation.Tests
{
    /// <summary>연출 주체의 공수와 배경 보호 마스크를 실제 셰이더 출력으로 검증한다.</summary>
    public sealed class IllustrationUniformTests
    {
        [Serializable] private sealed class Catalog { public Entry[] images; }
        [Serializable] private sealed class Entry { public string resourcePath; }
        private static Material Resolve(string path) => (Material)typeof(SpriteActor).Assembly
            .GetType("Baseball.Presentation.Match.Sprites.MatchUniformMaterials", true)
            .GetMethod("GetForIllustration", BindingFlags.Static | BindingFlags.Public)
            .Invoke(null, new object[] { "FRANCHISE_35294c0c8039e3d5d238", path });

        [TestCase(OwnerMatchHighlightKind.BatContact, false)]
        [TestCase(OwnerMatchHighlightKind.Bunt, false)]
        [TestCase(OwnerMatchHighlightKind.Slide, false)]
        [TestCase(OwnerMatchHighlightKind.HomeRun, false)]
        [TestCase(OwnerMatchHighlightKind.GloveCatch, true)]
        [TestCase(OwnerMatchHighlightKind.GreatCatch, true)]
        [TestCase(OwnerMatchHighlightKind.Throw, true)]
        public void HighlightSubjectFollowsEventHalf(OwnerMatchHighlightKind kind, bool isDefense)
        {
            Assert.That(OwnerMatchHighlightCue.IsHomeTeamSubject(kind, InningHalf.Top), Is.EqualTo(isDefense));
            Assert.That(OwnerMatchHighlightCue.IsHomeTeamSubject(kind, InningHalf.Bottom), Is.EqualTo(!isDefense));
        }

        [Test]
        public void IllustrationShaderPreservesBackgroundAndRendersEveryUniformRegion()
        {
            Catalog catalog = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("UI/SpriteMatch/IllustrationUniformRegions").text);
            Assert.That(catalog.images.Length, Is.EqualTo(9));
            Directory.CreateDirectory("output/owner-match-uniforms/illustrations");
            foreach (Entry entry in catalog.images)
            {
                Texture source = Resources.Load<Texture2D>(entry.resourcePath);
                Assert.That(source, Is.Not.Null, entry.resourcePath);
                Material material = Resolve(entry.resourcePath);
                if (entry.resourcePath.EndsWith("/home-run", StringComparison.Ordinal))
                {
                    Assert.That(material, Is.Null, "선수가 없는 컷은 원화 그대로 표시한다.");
                    continue;
                }
                Assert.That(material, Is.Not.Null, entry.resourcePath);
                Assert.That(Resolve(entry.resourcePath), Is.SameAs(material));
                var baseline = new Material(material);
                baseline.SetTexture("_UniformMask", Texture2D.blackTexture);
                Texture2D original = Render(source, baseline);
                Texture2D dyed = Render(source, material);
                Texture2D mask = Render(material.GetTexture("_UniformMask"), baseline);
                try
                {
                    Color32[] before = original.GetPixels32(), after = dyed.GetPixels32(), regions = mask.GetPixels32();
                    int changed = 0, preserved = 0, backgroundChanges = 0;
                    for (int i = 0; i < before.Length; i++)
                    {
                        if (regions[i].r == 0)
                        {
                            if (!after[i].Equals(before[i])) backgroundChanges++;
                            preserved++;
                        }
                        if (!after[i].Equals(before[i])) changed++;
                    }
                    Assert.That(changed, Is.GreaterThan(500), entry.resourcePath);
                    Assert.That(backgroundChanges, Is.Zero, entry.resourcePath + " 의상 영역 밖 픽셀 변경");
                    Assert.That(preserved, Is.GreaterThan(before.Length / 3), entry.resourcePath);
                    File.WriteAllBytes("output/owner-match-uniforms/illustrations/" + Path.GetFileName(entry.resourcePath) + ".png", dyed.EncodeToPNG());
                }
                finally
                {
                    Object.DestroyImmediate(baseline);
                    Object.DestroyImmediate(original);
                    Object.DestroyImmediate(dyed);
                    Object.DestroyImmediate(mask);
                }
            }
        }

        private static Texture2D Render(Texture texture, Material source)
        {
            var target = RenderTexture.GetTemporary(1024, 576, 0);
            RenderTexture previous = RenderTexture.active;
            var material = new Material(source);
            try
            {
                RenderTexture.active = target;
                GL.Clear(true, true, Color.black);
                GL.PushMatrix();
                GL.LoadPixelMatrix(0, 1, 0, 1);
                material.mainTexture = texture;
                material.SetPass(0);
                GL.Begin(GL.QUADS);
                GL.Color(Color.white);
                GL.TexCoord2(0, 0); GL.Vertex3(0, 0, 0);
                GL.TexCoord2(1, 0); GL.Vertex3(1, 0, 0);
                GL.TexCoord2(1, 1); GL.Vertex3(1, 1, 0);
                GL.TexCoord2(0, 1); GL.Vertex3(0, 1, 0);
                GL.End();
                GL.PopMatrix();
                var result = new Texture2D(1024, 576, TextureFormat.RGB24, false);
                result.ReadPixels(new Rect(0, 0, 1024, 576), 0, 0);
                result.Apply();
                return result;
            }
            finally
            {
                Object.DestroyImmediate(material);
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }
        }
    }
}
