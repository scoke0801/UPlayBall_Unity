using Baseball.Presentation.Career;
using Baseball.Presentation.Match;
using Baseball.Simulation.Match;
using NUnit.Framework;
using UnityEngine;

namespace Baseball.Tests.EditMode.Presentation.Match
{
    /// <summary>구장 좌표와 공식 사건에 종속된 재생을 검증한다.</summary>
    public sealed class MatchGameCastTests
    {
        [TestCase(0, 0.5f, 0.882f)]
        [TestCase(1, 0.673f, 0.665f)]
        [TestCase(2, 0.5f, 0.474f)]
        [TestCase(3, 0.327f, 0.665f)]
        [TestCase(4, 0.5f, 0.882f)]
        public void 베이스는구장원본의실제위치와일치한다(int number, float x, float y)
        {
            Vector2 point = MatchGameCastConfig.Load().ToTexturePoint(PlayResolutionFieldLayout.GetBasePoint(number));
            Assert.That(point.x, Is.EqualTo(x).Within(0.001f));
            Assert.That(point.y, Is.EqualTo(y).Within(0.001f));
        }

        [Test]
        public void 중요순간임계값은후반접전을더민감하게선별한다()
        {
            MatchGameCastConfig config = MatchGameCastConfig.Load();

            Assert.That(config.highlightLateInning, Is.GreaterThanOrEqualTo(7));
            Assert.That(config.highlightCloseRunMargin, Is.InRange(1, 3));
            Assert.That(config.highlightMultiRunThreshold, Is.GreaterThanOrEqualTo(2));
            Assert.That(config.highlightLateWinExpectancySwing,
                Is.GreaterThan(0f).And.LessThan(config.highlightWinExpectancySwing));
        }

        [Test]
        public void 이중진루는중간베이스를거치며화면부품을추가생성하지않는다()
        {
            var host = new GameObject("Field", typeof(RectTransform));
            try
            {
                var rect = (RectTransform)host.transform;
                rect.sizeDelta = new Vector2(828, 552);
                var config = MatchGameCastConfig.Load();
                var visualizer = new MatchPlayVisualizer(rect, config,
                    Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), id => "검증 주자");
                int count = host.GetComponentsInChildren<Transform>(true).Length;
                var value = new MatchEvent(1, MatchEventType.RunnerAdvance, 1, InningHalf.Top,
                    1, 2, 1, default, default, 0, 2, 0, 0, 0, 0, 0);
                visualizer.Begin(value, default);
                visualizer.Render(0.5f);
                Transform markers = rect.Find("GameCastMarkers");
                Assert.That(markers, Is.Not.Null);
                Assert.That(markers.gameObject.activeSelf, Is.True);
                var runner = (RectTransform)markers.Find("Runner0");
                Assert.That(runner, Is.Not.Null);
                Vector2 first = config.ToTexturePoint(PlayResolutionFieldLayout.GetBasePoint(1));
                Assert.That(runner.anchoredPosition.x, Is.EqualTo(first.x * 828).Within(0.01f));
                Assert.That(runner.anchoredPosition.y, Is.EqualTo(-first.y * 552).Within(0.01f));
                Assert.That(config.GetDuration(value), Is.EqualTo(config.runnerSecondsPerBase * 2));
                visualizer.Render(1f);
                Assert.That(host.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
                visualizer.Reset();
                Assert.That(runner.gameObject.activeSelf, Is.False);
            }
            finally { Object.DestroyImmediate(host); }
        }
    }
}
