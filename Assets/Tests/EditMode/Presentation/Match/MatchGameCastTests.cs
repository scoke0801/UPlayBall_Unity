using Baseball.Presentation.Career;
using Baseball.Presentation.Match;
using Baseball.Presentation.Match.Sprites;
using Baseball.Core.Players;
using Baseball.Simulation.PlateAppearance;
using Baseball.Simulation.Match;
using NUnit.Framework;
using UnityEngine;

namespace Baseball.Tests.EditMode.Presentation.Match
{
    /// <summary>구장 좌표와 공식 사건에 종속된 재생을 검증한다.</summary>
    public sealed class MatchGameCastTests
    {
        [Test]
        public void 같은타구의기존주자가함께출발하고각자사건으로연속재생된다()
        {
            MatchEvent Event(int sequence, MatchEventType type, int player, int from = 0, int to = 0,
                PlateAppearanceResult result = PlateAppearanceResult.None) => new MatchEvent(sequence, type,
                1, InningHalf.Top, 1, 2, player, PitchResult.InPlay, result, from, to, 0, 0, 0, 0, 0);
            var events = new[] { Event(1, MatchEventType.Contact, 1),
                Event(2, MatchEventType.Out, 11, result: PlateAppearanceResult.GroundOut),
                Event(3, MatchEventType.RunnerThrownOut, 11, 1, 2),
                Event(4, MatchEventType.RunnerAdvance, 12, 2, 3),
                Event(5, MatchEventType.RunnerAdvance, 13, 3, 4),
                Event(6, MatchEventType.PlateAppearanceEnded, 1),
                Event(7, MatchEventType.RunnerAdvance, 99, 1, 2) };
            var routes = new OwnerMatchRunnerRoute[3];
            int count = OwnerMatchRunnerRoute.Collect(events, 0, 1, 11, 12, 13, routes);
            Assert.That(count, Is.EqualTo(3));
            Assert.That(routes[0].PlayerId, Is.EqualTo(11));
            Assert.That(routes[0].FromBase, Is.EqualTo(1));
            Assert.That(routes[0].ToBase, Is.EqualTo(2));
            Assert.That(routes[2].PlayerId, Is.EqualTo(13));
            var host = new GameObject("Field", typeof(RectTransform));
            try
            {
                var rect = (RectTransform)host.transform;
                rect.sizeDelta = new Vector2(828, 552);
                var visualizer = new MatchPlayVisualizer(rect, MatchGameCastConfig.Load(),
                    Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), id => "주자",
                    (pitcher, batter) => new OwnerMatchHandedness(Handedness.Right, Handedness.Right));
                visualizer.Begin(Event(0, MatchEventType.Pitch, 1), default);
                var hud = new MatchHudPresentationModelBuilder().Build(1, MatchHudHalf.Top,
                    new MatchHudTeamModel("원정", 0, true), new MatchHudTeamModel("홈", 0, false), MatchHudCountModel.Empty,
                    new MatchHudBaseStateModel(new MatchHudParticipantModel(11, "1루"),
                        new MatchHudParticipantModel(12, "2루"), new MatchHudParticipantModel(13, "3루")), null, null, false);
                visualizer.PresentBases(hud);
                Transform field = rect.Find("SpriteMatchStage/FieldCamera");
                var positions = new Vector2[3];
                for (int index = 0; index < 3; index++) positions[index] = ((RectTransform)field.Find("Runner" + index)).anchoredPosition;
                visualizer.PrepareRunnerRoutes(routes, count);
                var play = new BallInPlayEventData(new BattedBallDescriptor(BattedBallType.GroundBall,
                    BattedBallDirection.Center, FieldZone.Shortstop, 0.5, BallFlightBand.Medium, BallPaceBand.Medium, false), default);
                visualizer.Begin(events[0], play);
                visualizer.Render(0.8f);
                for (int index = 0; index < 3; index++)
                {
                    var runner = (RectTransform)field.Find("Runner" + index);
                    Assert.That(Vector2.Distance(runner.anchoredPosition, positions[index]), Is.GreaterThan(1));
                    positions[index] = runner.anchoredPosition;
                }
                visualizer.PresentBases(hud);
                for (int index = 0; index < 3; index++)
                    Assert.That(((RectTransform)field.Find("Runner" + index)).anchoredPosition, Is.EqualTo(positions[index]));
                visualizer.Begin(events[1], play);
                Assert.That(((RectTransform)field.Find("Runner0")).anchoredPosition, Is.EqualTo(positions[0]));
                visualizer.Render(0.5f);
                Assert.That(Vector2.Distance(((RectTransform)field.Find("Runner1")).anchoredPosition, positions[1]), Is.GreaterThan(1));
                Assert.That(Vector2.Distance(((RectTransform)field.Find("Runner2")).anchoredPosition, positions[2]), Is.GreaterThan(1));
                visualizer.Reset();
                visualizer.Begin(Event(0, MatchEventType.Pitch, 1), default);
                visualizer.PresentBases(hud);
                Vector2 thirdBase = ((RectTransform)field.Find("Runner2")).anchoredPosition;
                visualizer.PrepareRunnerRoutes(new[] { routes[2] }, 1);
                var caughtFly = new BallInPlayEventData(new BattedBallDescriptor(BattedBallType.FlyBall,
                    BattedBallDirection.Center, FieldZone.CenterField, 0.5, BallFlightBand.Long, BallPaceBand.Medium, false),
                    new FieldingPlayOutcome(PlateAppearanceResult.FlyOut, PlayerPosition.CenterField, 20,
                        FieldingFailureType.None, true, false, 1));
                visualizer.Begin(events[0], caughtFly);
                visualizer.Render(1f);
                Assert.That(((RectTransform)field.Find("Runner2")).anchoredPosition, Is.EqualTo(thirdBase),
                    "태그업 주자는 포구 전에 출발하지 않는다.");
                visualizer.Begin(Event(2, MatchEventType.Out, 1, result: PlateAppearanceResult.FlyOut), caughtFly);
                visualizer.Render(0.5f);
                Assert.That(Vector2.Distance(((RectTransform)field.Find("Runner2")).anchoredPosition, thirdBase), Is.GreaterThan(1));
            }
            finally { Object.DestroyImmediate(host); }
        }

        [TestCase(PitchResult.Ball)]
        [TestCase(PitchResult.CalledStrike)]
        [TestCase(PitchResult.SwingingStrike)]
        [TestCase(PitchResult.Foul)]
        public void 인플레이가아닌투구는판정후공이타석에남지않는다(PitchResult result)
        {
            var host = new GameObject("Field", typeof(RectTransform));
            try
            {
                var rect = (RectTransform)host.transform;
                rect.sizeDelta = new Vector2(828, 552);
                var visualizer = new MatchPlayVisualizer(rect, MatchGameCastConfig.Load(),
                    Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), id => "검증 선수",
                    (pitcher, batter) => new OwnerMatchHandedness(Handedness.Right, Handedness.Right));
                var pitch = new MatchEvent(1, MatchEventType.Pitch, 1, InningHalf.Top,
                    1, 2, 1, result, default, 0, 0, 0, 0, 0, 0, 0);
                visualizer.Begin(pitch, default);
                visualizer.Render(0.95f);
                Transform ball = rect.Find("SpriteMatchStage/FieldCamera/BallVisual");
                Assert.That(ball.gameObject.activeSelf, Is.True);
                visualizer.Render(1f);
                Assert.That(ball.gameObject.activeSelf, Is.False);
            }
            finally { Object.DestroyImmediate(host); }
        }

        [TestCase(Handedness.Right, Handedness.Right)]
        [TestCase(Handedness.Right, Handedness.Left)]
        [TestCase(Handedness.Left, Handedness.Right)]
        [TestCase(Handedness.Left, Handedness.Left)]
        public void 경기용카탈로그로모든투타조합의스프라이트가재생된다(Handedness throwing, Handedness batting)
        {
            var catalog = Resources.Load<SpriteAnimationCatalog>("UI/SpriteMatch/AnimationCatalog");
            Assert.That(BaseballVisualSequenceResolver.CanPresent(catalog, throwing, batting), Is.True);
            var host = new GameObject("Field", typeof(RectTransform));
            try
            {
                var rect = (RectTransform)host.transform;
                rect.sizeDelta = new Vector2(828, 552);
                var visualizer = new MatchPlayVisualizer(rect, MatchGameCastConfig.Load(),
                    Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), id => "검증 선수",
                    (pitcher, batter) => new OwnerMatchHandedness(throwing, batting));
                var pitch = new MatchEvent(1, MatchEventType.Pitch, 1, InningHalf.Top,
                    1, 2, 1, PitchResult.InPlay, default, 0, 0, 0, 0, 0, 0, 0);
                visualizer.Begin(pitch, default);
                Transform stage = rect.Find("SpriteMatchStage");
                Assert.That(stage.gameObject.activeSelf, Is.True);
                Assert.That(rect.Find("GameCastMarkers").gameObject.activeSelf, Is.False);
                var pose = stage.Find("FieldCamera/FielderStartingPitcher/Pose").GetComponent<UnityEngine.UI.Image>();
                Sprite first = pose.sprite;
                int count = host.GetComponentsInChildren<Transform>(true).Length;
                visualizer.Render(0.7f);
                Assert.That(pose.sprite, Is.Not.SameAs(first));
                visualizer.Render(1f);
                var play = new BallInPlayEventData(new BattedBallDescriptor(BattedBallType.FlyBall,
                    BattedBallDirection.Center, FieldZone.CenterField, 0.5, BallFlightBand.Long, BallPaceBand.Medium, false), default);
                var contact = new MatchEvent(2, MatchEventType.Contact, 1, InningHalf.Top,
                    1, 2, 1, PitchResult.InPlay, default, 0, 0, 0, 0, 0, 0, 0);
                var camera = (RectTransform)stage.Find("FieldCamera");
                Vector3 pitchZoom = camera.localScale;
                Vector2 pitchCameraPosition = camera.anchoredPosition;
                visualizer.Begin(contact, play);
                Assert.That(camera.localScale, Is.EqualTo(pitchZoom));
                Assert.That(camera.anchoredPosition, Is.EqualTo(pitchCameraPosition));
                float peak = catalog.fieldLayout.contactCameraPeakProgress;
                visualizer.Render(peak);
                Assert.That(camera.localScale.x, Is.EqualTo(catalog.fieldLayout.contactZoom).Within(0.001f));
                Vector2 peakPosition = camera.anchoredPosition;
                visualizer.Render(peak + 0.00001f);
                Assert.That(Vector2.Distance(camera.anchoredPosition, peakPosition), Is.LessThan(0.01f));
                Assert.That(visualizer.GetDuration(contact, 0.32f), Is.EqualTo(MatchGameCastConfig.Load().GetContactDuration(play)));
                visualizer.Render(1f);
                var movingRunner = (RectTransform)stage.Find("FieldCamera/Runner0");
                Vector2 departure = movingRunner.anchoredPosition;
                Assert.That(movingRunner.gameObject.activeSelf, Is.True);
                Assert.That(stage.Find("FieldCamera/Batter").gameObject.activeSelf, Is.False);
                visualizer.PresentBases(new MatchHudPresentationModelBuilder().Build(1, MatchHudHalf.Top,
                    new MatchHudTeamModel("원정", 0, true), new MatchHudTeamModel("홈", 0, false),
                    MatchHudCountModel.Empty, MatchHudBaseStateModel.Empty, null, null, false));
                Assert.That(movingRunner.anchoredPosition, Is.EqualTo(departure), "HUD가 갱신돼도 출발 중인 주자가 홈으로 되돌아가지 않는다.");
                var runner = new MatchEvent(2, MatchEventType.RunnerAdvance, 1, InningHalf.Top,
                    1, 2, 1, default, default, 0, 2, 0, 0, 0, 0, 0);
                visualizer.Begin(runner, default);
                Assert.That(Vector2.Distance(movingRunner.anchoredPosition, departure), Is.LessThan(0.001f));
                visualizer.Render(0.75f);
                Assert.That(stage.Find("FieldCamera/Runner0").gameObject.activeSelf, Is.True);
                Assert.That(stage.Find("FieldCamera/RunnerMarker0").gameObject.activeSelf, Is.False);
                Assert.That(stage.Find("FieldCamera/Batter").gameObject.activeSelf, Is.False);
                Assert.That(stage.Find("FieldCamera/Runner0/Pose").localScale.x, Is.LessThan(0));
                visualizer.Render(1f);
                Assert.That(catalog.TryGetClip("Runner.Idle", out SpriteClipDefinition idle), Is.True);
                var runnerPose = stage.Find("FieldCamera/Runner0/Pose").GetComponent<UnityEngine.UI.Image>();
                CollectionAssert.Contains(System.Array.ConvertAll(idle.frames, frame => frame.sprite), runnerPose.sprite);
                Vector3 fieldZoom = camera.localScale;
                Vector2 fieldCameraPosition = camera.anchoredPosition;
                var ended = new MatchEvent(3, MatchEventType.PlateAppearanceEnded, 1, InningHalf.Top,
                    1, 2, 1, default, PlateAppearanceResult.Double, 0, 0, 0, 0, 0, 0, 0);
                visualizer.Begin(ended, play);
                Assert.That(camera.localScale, Is.EqualTo(fieldZoom));
                Assert.That(camera.anchoredPosition, Is.EqualTo(fieldCameraPosition));
                visualizer.Render(1f);
                Assert.That(camera.localScale.x, Is.EqualTo(catalog.fieldLayout.duelZoom).Within(0.001f));
                Assert.That(camera.anchoredPosition, Is.EqualTo(Vector2.zero));
                Assert.That(host.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
                visualizer.Reset();
                Assert.That(stage.gameObject.activeSelf, Is.False);
                visualizer.Begin(pitch, default);
                Assert.That(stage.Find("FieldCamera/Batter").gameObject.activeSelf, Is.True);
            }
            finally { Object.DestroyImmediate(host); }
        }

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
