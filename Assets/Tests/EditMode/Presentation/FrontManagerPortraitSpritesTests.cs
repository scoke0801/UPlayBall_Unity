using Baseball.Presentation.SharedUI;
using NUnit.Framework;
using UnityEngine;

namespace Baseball.Tests.EditMode.Presentation
{
    /// <summary>새 게임에서 두 프런트 매니저가 서로 다른 전용 초상화를 사용하는지 검증한다.</summary>
    public sealed class FrontManagerPortraitSpritesTests
    {
        [Test]
        public void Load_두ManagerNeutral초상화는서로다른Texture다()
        {
            Sprite analysis = FrontManagerPortraitSprites.Load("FM_01_NEUTRAL", fallbackAssetKey: null);
            Sprite field = FrontManagerPortraitSprites.Load("FM_02_NEUTRAL", fallbackAssetKey: null);

            Assert.That(analysis, Is.Not.Null);
            Assert.That(field, Is.Not.Null);
            Assert.That(field.texture, Is.Not.SameAs(analysis.texture));
        }
    }
}
