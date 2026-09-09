using Baseball.Game.Historical;
using Baseball.Presentation.SharedUI;
using NUnit.Framework;
using UnityEngine;

namespace Baseball.Tests.EditMode.Presentation
{
    /// <summary>선택한 프런트 매니저의 전용 초상화가 모든 표정에서 유지되는지 검증한다.</summary>
    public sealed class FrontManagerPortraitSpritesTests
    {
        [TestCase("NEUTRAL")]
        [TestCase("WELCOME")]
        [TestCase("ANALYSIS")]
        [TestCase("CONCERNED")]
        [TestCase("WARNING")]
        [TestCase("CELEBRATE")]
        [TestCase("SURPRISED")]
        [TestCase("CALM")]
        public void LoadForManager_모든표정에선택한현장형전용사진을쓴다(string expression)
        {
            Sprite expected = Resources.Load<Sprite>("FrontManager/FM_02_" + expression);
            Assert.That(expected, Is.Not.Null, "각 표정은 Sprite로 Import되어야 합니다.");
            Sprite actual = FrontManagerPortraitSprites.LoadForManager(
                FrontManagerIds.DefaultTest, "FM_" + expression);
            Assert.That(actual, Is.SameAs(expected));
        }

        [TestCase("NEUTRAL")]
        [TestCase("WELCOME")]
        [TestCase("ANALYSIS")]
        [TestCase("CONCERNED")]
        [TestCase("WARNING")]
        [TestCase("CELEBRATE")]
        [TestCase("SURPRISED")]
        [TestCase("CALM")]
        public void LoadForManager_활력형프로필의모든표정이전용사진을쓴다(string expression)
        {
            var profile = new OwnerProfileState("구단주", FrontManagerIds.DefaultEnergetic);
            Sprite expected = Resources.Load<Sprite>("FrontManager/FM_03_" + expression);
            Assert.That(expected, Is.Not.Null, "활력형 표정은 Sprite로 Import되어야 합니다.");
            Assert.That(FrontManagerPortraitSprites.LoadForManager(profile.FrontManagerId, "FM_" + expression),
                Is.SameAs(expected));
        }

        [TestCase("FM_MISSING")]
        [TestCase(null)]
        public void LoadForManager_활력형누락표정에서도선택한인물을유지한다(string expression)
        {
            Sprite expected = Resources.Load<Sprite>("FrontManager/FM_03_NEUTRAL");
            Assert.That(expected, Is.Not.Null);
            Assert.That(FrontManagerPortraitSprites.LoadForManager(FrontManagerIds.DefaultEnergetic, expression),
                Is.SameAs(expected));
        }

        [TestCase("FM_MISSING")]
        [TestCase(null)]
        public void LoadForManager_누락된표정은선택한매니저의기본표정을쓴다(string expression)
        {
            Sprite actual = FrontManagerPortraitSprites.LoadForManager(FrontManagerIds.DefaultTest, expression);
            Assert.That(actual, Is.SameAs(FrontManagerPortraitSprites.Load("FM_02_NEUTRAL", null)));
        }

        [TestCase(FrontManagerIds.DefaultTest, "FM_01_WELCOME", "FM_02_WELCOME")]
        [TestCase(FrontManagerIds.DefaultAnalysis, "FM_02_WELCOME", "FM_01_WELCOME")]
        [TestCase(FrontManagerIds.DefaultEnergetic, "FM_01_WELCOME", "FM_03_WELCOME")]
        [TestCase(FrontManagerIds.DefaultEnergetic, "FM_02_WELCOME", "FM_03_WELCOME")]
        [TestCase(FrontManagerIds.DefaultAnalysis, "FM_03_WELCOME", "FM_01_WELCOME")]
        [TestCase(FrontManagerIds.DefaultTest, "FM_03_WELCOME", "FM_02_WELCOME")]
        public void LoadForManager_이미접두사가있는표정도현재선택을따른다(
            string managerId, string expression, string expected)
        {
            Assert.That(FrontManagerPortraitSprites.LoadForManager(managerId, expression),
                Is.SameAs(FrontManagerPortraitSprites.Load(expected, null)));
        }

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
