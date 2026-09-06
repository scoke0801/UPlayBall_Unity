using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Tests.EditMode.Presentation
{
    /// <summary>기존 월드 슬롯과 무관한 구단 상징 연결 및 이미지 재바인딩을 검증한다.</summary>
    public sealed class TeamEmblemSpritesTests
    {
        private static readonly Type SpriteType = Assembly.Load("Baseball.Presentation")
            .GetType("Baseball.Presentation.UI.TeamEmblemSprites", true);

        [TestCase("수원 가디언즈", 129)]
        [TestCase("안양 베어스", 13)]
        [TestCase("포항 볼트즈", 65)]
        [TestCase("대전 파이오니어스", 107)]
        [TestCase("전주 스타즈", 87)]
        [TestCase("강릉 웨이브즈", 78)]
        [TestCase("울산 파워스", 75)]
        [TestCase("대구 포지", 118)]
        [TestCase("청주 레이더스", 134)]
        [TestCase("인천 하버스", 101)]
        [TestCase("프로 서울 베어스", 13)]
        public void ResolveEmblemId_UsesIdentityInsteadOfWorldSlot(string name, int expected)
        {
            MethodInfo resolve = SpriteType.GetMethod("ResolveEmblemId");
            Assert.That(resolve.Invoke(null, new object[] { name, 1 }), Is.EqualTo(expected));
            Assert.That(resolve.Invoke(null, new object[] { name, 128 }), Is.EqualTo(expected));
        }

        [Test]
        public void Catalog_AllAliasesResolveToLoadableSprites()
        {
            var catalog = JsonUtility.FromJson<Catalog>(
                Resources.Load<TextAsset>("TeamEmblems/TeamEmblemIdentities").text);
            MethodInfo resolve = SpriteType.GetMethod("ResolveEmblemId");
            MethodInfo get = SpriteType.GetMethod("Get");
            foreach (Entry entry in catalog.entries)
            {
                foreach (string name in entry.names)
                    Assert.That(resolve.Invoke(null, new object[] { "서울 " + name, 0 }),
                        Is.EqualTo(entry.emblemId), name);
                var sprite = (Sprite)get.Invoke(null, new object[] { entry.emblemId });
                Assert.That(sprite, Is.Not.Null, entry.names[0]);
                Assert.That(sprite.rect.xMin, Is.GreaterThanOrEqualTo(0));
                Assert.That(sprite.rect.yMin, Is.GreaterThanOrEqualTo(0));
                Assert.That(sprite.rect.xMax, Is.LessThanOrEqualTo(sprite.texture.width));
                Assert.That(sprite.rect.yMax, Is.LessThanOrEqualTo(sprite.texture.height));
            }
        }

        [Test]
        public void TryApply_RebindingUsesIdentityAndClearsMissingImage()
        {
            var root = new GameObject("Emblem", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            try
            {
                Image image = root.GetComponent<Image>();
                MethodInfo apply = SpriteType.GetMethod("TryApply", new[] { typeof(Image), typeof(int), typeof(string) });
                Assert.That(apply.Invoke(null, new object[] { image, 6, "안양 베어스" }), Is.True);
                Assert.That(image.sprite.name, Is.EqualTo("TeamEmblem_013"));
                Assert.That(apply.Invoke(null, new object[] { image, 1, "수원 가디언즈" }), Is.True);
                Assert.That(image.sprite.name, Is.EqualTo("TeamEmblem_129"));
                Assert.That(apply.Invoke(null, new object[] { image, 0, "미등록 구단" }), Is.False);
                Assert.That(image.sprite, Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("사용자 구단")]
        public void ResolveEmblemId_UnregisteredIdentityPreservesExplicitFallback(string name)
        {
            Assert.That(SpriteType.GetMethod("ResolveEmblemId").Invoke(null, new object[] { name, 23 }),
                Is.EqualTo(23));
        }

        [Serializable] private sealed class Catalog { public Entry[] entries; }
        [Serializable] private sealed class Entry { public string[] names; public int emblemId; }
    }
}
