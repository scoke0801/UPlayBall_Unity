#if UNITY_EDITOR
using Baseball.Presentation.Career;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Tests.EditMode.Presentation
{
    /// <summary>카드 디자인 갤러리 FX가 읽기와 입력을 방해하지 않는지 검증한다.</summary>
    public sealed class CardDesignGalleryFxTests
    {
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("CardDesignGalleryFxTests_Root", typeof(RectTransform));
            _root.GetComponent<RectTransform>().sizeDelta = new Vector2(300f, 420f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                Object.DestroyImmediate(_root);
        }

        [Test]
        public void Initialize_테두리Glint와Sparkle을한번만생성한다()
        {
            CardDesignGalleryFx fx = _root.AddComponent<CardDesignGalleryFx>();

            fx.Initialize("Legend", false, true);
            int childCount = _root.transform.childCount;
            fx.Initialize("Normal", true, false);

            Assert.That(_root.transform.Find("TopGlow"), Is.Not.Null);
            Assert.That(_root.transform.Find("BottomGlow"), Is.Not.Null);
            Assert.That(_root.transform.Find("LeftGlow"), Is.Not.Null);
            Assert.That(_root.transform.Find("RightGlow"), Is.Not.Null);
            Assert.That(_root.transform.Find("TopGlint"), Is.Not.Null);
            Assert.That(_root.transform.Find("RightGlint"), Is.Not.Null);
            Assert.That(_root.transform.Find("CornerSparkleA/Cross"), Is.Not.Null);
            Assert.That(_root.transform.childCount, Is.EqualTo(childCount));
        }

        [Test]
        public void Initialize_모든Graphic은카드입력을가로채지않는다()
        {
            _root.AddComponent<CardDesignGalleryFx>().Initialize("MVP", true, true);

            Image[] graphics = _root.GetComponentsInChildren<Image>(true);

            Assert.That(graphics, Is.Not.Empty);
            for (int index = 0; index < graphics.Length; index++)
                Assert.That(graphics[index].raycastTarget, Is.False, graphics[index].name);
        }

        [Test]
        public void Initialize_Legend가Normal보다강한테두리를사용한다()
        {
            var normalRoot = new GameObject("Normal", typeof(RectTransform));
            normalRoot.transform.SetParent(_root.transform, false);
            normalRoot.GetComponent<RectTransform>().sizeDelta = new Vector2(151f, 212f);
            normalRoot.AddComponent<CardDesignGalleryFx>().Initialize("Normal", true, true);

            var legendRoot = new GameObject("Legend", typeof(RectTransform));
            legendRoot.transform.SetParent(_root.transform, false);
            legendRoot.GetComponent<RectTransform>().sizeDelta = new Vector2(151f, 212f);
            legendRoot.AddComponent<CardDesignGalleryFx>().Initialize("Legend", true, true);

            float normalAlpha = normalRoot.transform.Find("TopGlow").GetComponent<Image>().color.a;
            float legendAlpha = legendRoot.transform.Find("TopGlow").GetComponent<Image>().color.a;

            Assert.That(legendAlpha, Is.GreaterThan(normalAlpha));
        }
    }
}
#endif
