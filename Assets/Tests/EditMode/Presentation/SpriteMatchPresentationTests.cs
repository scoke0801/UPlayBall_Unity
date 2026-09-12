using System.Collections.Generic;
using Baseball.Core.Players;
using Baseball.Presentation.Career;
using Baseball.Presentation.Match.Sprites;
using Baseball.Simulation.Match;
using NUnit.Framework;
using UnityEngine;

namespace Baseball.Presentation.Tests
{
    /// <summary>미검수 진입 차단·시간 분할·접점과 투영 계약을 검증한다.</summary>
    public sealed class SpriteMatchPresentationTests
    {
        private Texture2D _texture;
        private Sprite _sprite;
        private SpriteAnimationCatalog _catalog;
        private FieldLayoutDefinition _layout;

        [SetUp]
        public void SetUp()
        {
            _texture = new Texture2D(4, 4);
            _sprite = Sprite.Create(_texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0));
            _catalog = ScriptableObject.CreateInstance<SpriteAnimationCatalog>();
            _layout = ScriptableObject.CreateInstance<FieldLayoutDefinition>();
            _layout.background = _texture;
            _catalog.fieldLayout = _layout;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_catalog);
            Object.DestroyImmediate(_layout);
            Object.DestroyImmediate(_sprite);
            Object.DestroyImmediate(_texture);
        }

        [Test]
        public void CatalogRejectsNeedsReviewAndDuplicateIds()
        {
            SpriteClipDefinition clip = Clip("Pitcher.Pitch.R", SpriteHandedness.NeedsReview);
            _catalog.clips = new[] { clip };
            Assert.That(_catalog.TryGetClip(clip.clipId, out _), Is.False);
            clip.handedness = SpriteHandedness.Right;
            Assert.That(_catalog.TryGetClip(clip.clipId, out _), Is.True);
            _catalog.clips = new[] { clip, clip };
            Assert.That(_catalog.TryGetClip(clip.clipId, out _), Is.False);
        }

        [Test]
        public void ResolverRejectsWrongHandEvenWhenClipIdClaimsRightHand()
        {
            _catalog.clips = new[]
            {
                Clip("Pitcher.Pitch.R", SpriteHandedness.Right), Clip("Batter.DuelSwing.R", SpriteHandedness.Right),
                Clip("Fielder.InfieldGrounder", SpriteHandedness.Shared), Clip("Fielder.OutfieldFlyCatch", SpriteHandedness.Shared)
            };
            Assert.That(BaseballVisualSequenceResolver.CanPresent(_catalog, Handedness.Right, Handedness.Right), Is.True);
            _catalog.clips[0].handedness = SpriteHandedness.Left;
            Assert.That(BaseballVisualSequenceResolver.CanPresent(_catalog, Handedness.Right, Handedness.Right), Is.False);
        }

        [TestCase(1d)]
        [TestCase(2d)]
        [TestCase(4d)]
        public void PlaybackDispatchesEveryCrossedEventInOrderAtEachSpeed(double speed)
        {
            var events = new List<SpriteAnimationEvent>();
            var player = new SpriteSequencePlayer();
            player.EventEntered += events.Add;
            player.Play(Clip("Pitcher.Pitch.R", SpriteHandedness.Right));
            player.Advance(0.61d / speed, speed);
            CollectionAssert.AreEqual(new[]
            {
                SpriteAnimationEvent.BallRelease, SpriteAnimationEvent.BatContact, SpriteAnimationEvent.GloveContact
            }, events);
            Assert.That(player.IsComplete, Is.True);
        }

        [Test]
        public void LoopDispatchAndFinalFrameDoNotDependOnTimePartition()
        {
            SpriteClipDefinition clip = Clip("Loop", SpriteHandedness.Shared);
            clip.loop = true;
            var singleEvents = new List<SpriteAnimationEvent>();
            var partitionedEvents = new List<SpriteAnimationEvent>();
            var single = new SpriteSequencePlayer();
            var partitioned = new SpriteSequencePlayer();
            single.EventEntered += singleEvents.Add;
            partitioned.EventEntered += partitionedEvents.Add;
            single.Play(clip);
            partitioned.Play(clip);
            single.Advance(1.37);
            for (int i = 0; i < 137; i++) partitioned.Advance(0.01);
            CollectionAssert.AreEqual(singleEvents, partitionedEvents);
            Assert.That(single.CurrentFrame, Is.SameAs(partitioned.CurrentFrame));
        }

        [Test]
        public void VariableFrameDurationAndMarkerTimeArePreserved()
        {
            SpriteClipDefinition clip = Clip("Pitcher.Pitch.R", SpriteHandedness.Right);
            Assert.That(SpriteSequencePlayer.Sample(clip, 0.19f), Is.SameAs(clip.frames[1]));
            Assert.That(SpriteSequencePlayer.Sample(clip, 0.31f), Is.SameAs(clip.frames[2]));
            Assert.That(clip.TryGetEventTime(SpriteAnimationEvent.GloveContact, out float time), Is.True);
            Assert.That(time, Is.EqualTo(0.3f).Within(0.00001f));
        }

        [Test]
        public void ProjectionHitsCalibratedBasesAndSeparatesBallHeightFromGround()
        {
            var projection = new FieldProjection(_layout);
            Assert.That(Vector2.Distance(projection.Project(PlayResolutionFieldLayout.Home), _layout.GetAnchor(FieldAnchor.HomePlate)), Is.LessThan(0.0001));
            for (int number = 1; number <= 3; number++)
                Assert.That(Vector2.Distance(projection.Project(PlayResolutionFieldLayout.GetBasePoint(number)), projection.GetBase(number)), Is.LessThan(0.0001));
            Vector2 start = new Vector2(0.5f, 0.8f), end = new Vector2(0.2f, 0.3f);
            Vector3 ground = BallVisualController.Evaluate(start, end, 0.5f, 0);
            Vector3 fly = BallVisualController.Evaluate(start, end, 0.5f, 0.25f);
            Assert.That(fly.x, Is.EqualTo(ground.x));
            Assert.That(fly.y, Is.EqualTo(ground.y));
            Assert.That(fly.z, Is.GreaterThan(ground.z));
            Assert.That(BallVisualController.Evaluate(start, end, 1, 0.25f), Is.EqualTo(new Vector3(end.x, end.y, 0)));
        }

        [Test]
        public void VisualSelectionPreservesGroundAndFlyDistinction()
        {
            Assert.That(BaseballVisualSequenceResolver.FieldingClip(BattedBallType.GroundBall), Is.EqualTo("Fielder.InfieldGrounder"));
            Assert.That(BaseballVisualSequenceResolver.FieldingClip(BattedBallType.FlyBall), Is.EqualTo("Fielder.OutfieldFlyCatch"));
            Assert.That(BaseballVisualSequenceResolver.GetBallHeight(BattedBallType.GroundBall, 0.25f), Is.Zero);
        }

        [Test]
        public void StageReusesItsActorsAndBlocksUnavailableHandOnNextPitch()
        {
            _catalog.clips = new[]
            {
                Clip("Pitcher.Pitch.R", SpriteHandedness.Right), Clip("Batter.DuelSwing.R", SpriteHandedness.Right),
                Clip("Fielder.InfieldGrounder", SpriteHandedness.Shared), Clip("Fielder.OutfieldFlyCatch", SpriteHandedness.Shared)
            };
            var host = new GameObject("StageTest", typeof(RectTransform));
            try
            {
                RectTransform rect = host.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(1280, 720);
                var stage = new SpriteMatchStage(rect, _catalog, _sprite);
                stage.Reset();
                int count = host.GetComponentsInChildren<Transform>(true).Length;
                for (int i = 0; i < 20; i++)
                {
                    stage.Reset();
                    stage.RenderPitch(i / 19f, true);
                    stage.RenderRunner(0, 0, 1, i / 19f);
                }
                Assert.That(host.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
                int balls = 0;
                foreach (Transform child in host.GetComponentsInChildren<Transform>(true))
                    if (child.name == "BallVisual") balls++;
                Assert.That(balls, Is.EqualTo(1));
                Assert.That(stage.SetHands(Handedness.Left, Handedness.Right), Is.False);
                Assert.That(host.transform.GetChild(0).gameObject.activeSelf, Is.False);
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void CatalogRejectsNonFiniteFrameTiming()
        {
            SpriteClipDefinition clip = Clip("Pitcher.Pitch.R", SpriteHandedness.Right);
            clip.frames[0].durationMs = float.NaN;
            Assert.That(clip.IsProductionReady, Is.False);
            Assert.Throws<System.ArgumentException>(() => new SpriteSequencePlayer().Play(clip));
        }

        [Test]
        public void CatalogRejectsDuplicateMarkersAndThrowBeforePickup()
        {
            SpriteClipDefinition clip = Clip("Fielder.InfieldGrounder", SpriteHandedness.Right);
            clip.frames[0].events = new[] { SpriteAnimationEvent.ThrowRelease };
            Assert.That(clip.IsProductionReady, Is.False);
            clip.frames[0].events = new[] { SpriteAnimationEvent.GloveContact };
            clip.frames[2].events = new[] { SpriteAnimationEvent.ThrowRelease };
            Assert.That(clip.IsProductionReady, Is.True);
            clip.frames[1].events = new[] { SpriteAnimationEvent.GloveContact };
            Assert.That(clip.IsProductionReady, Is.False);
            clip.frames[1].events = new[] { (SpriteAnimationEvent)999 };
            Assert.That(clip.IsProductionReady, Is.False);
        }

        [Test]
        public void MissingCatcherUsesMarkerWithoutCoveringBatterWithInfielder()
        {
            _catalog.clips = new[]
            {
                Clip("Pitcher.Pitch.R", SpriteHandedness.Right), Clip("Batter.DuelSwing.R", SpriteHandedness.Right),
                Clip("Fielder.InfieldGrounder", SpriteHandedness.Shared), Clip("Fielder.OutfieldFlyCatch", SpriteHandedness.Shared)
            };
            var host = new GameObject("CatcherTest", typeof(RectTransform));
            try
            {
                var rect = host.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(1280, 720);
                var stage = new SpriteMatchStage(rect, _catalog, _sprite);
                stage.Reset();
                Transform field = host.transform.Find("SpriteMatchStage/FieldCamera");
                Assert.That(field.Find("FielderCatcher").gameObject.activeSelf, Is.False);
                Assert.That(field.Find("CatcherMarker").gameObject.activeSelf, Is.True);
                Assert.That(field.Find("Batter").gameObject.activeSelf, Is.True);
                var clips = new List<SpriteClipDefinition>(_catalog.clips)
                {
                    Clip("Catcher.Idle", SpriteHandedness.Shared)
                };
                _catalog.clips = clips.ToArray();
                stage.SetHands(Handedness.Right, Handedness.Right);
                stage.Reset();
                Assert.That(field.Find("FielderCatcher").gameObject.activeSelf, Is.True);
                Assert.That(field.Find("CatcherMarker").gameObject.activeSelf, Is.False);
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void EventAnchorMustReferenceAFrameContainingThatMarker()
        {
            SpriteClipDefinition clip = Clip("Pitcher.Pitch.R", SpriteHandedness.Right);
            var anchor = new SpriteEventAnchorDefinition
            {
                marker = SpriteAnimationEvent.BallRelease, frameIndex = 0,
                sourceCellSize = new Vector2(362, 362), sourcePositionNormalized = new Vector2(270f / 362f, 143f / 362f)
            };
            clip.eventAnchors = new[] { anchor };
            Assert.That(clip.IsProductionReady, Is.True);
            Assert.That(clip.TryGetEventAnchor(SpriteAnimationEvent.BallRelease, out var found), Is.True);
            Assert.That(found, Is.SameAs(anchor));
            anchor.frameIndex = 1;
            Assert.That(clip.IsProductionReady, Is.False);
            Assert.That(clip.TryGetEventAnchor(SpriteAnimationEvent.BallRelease, out _), Is.False);
        }

        [Test]
        public void DuplicateEventAnchorsAreRejectedByBothReadinessAndLookup()
        {
            SpriteClipDefinition clip = Clip("Pitcher.Pitch.R", SpriteHandedness.Right);
            var anchor = new SpriteEventAnchorDefinition
            {
                marker = SpriteAnimationEvent.BallRelease, frameIndex = 0,
                sourceCellSize = new Vector2(362, 362), sourcePositionNormalized = new Vector2(0.75f, 0.4f)
            };
            clip.eventAnchors = new[] { anchor, anchor };
            _catalog.clips = new[] { clip };
            Assert.That(clip.IsProductionReady, Is.False);
            Assert.That(clip.TryGetEventAnchor(SpriteAnimationEvent.BallRelease, out _), Is.False);
            Assert.That(_catalog.TryGetClip(clip.clipId, out _), Is.False);
        }

        [Test]
        public void SwingingMissUsesPlateTargetInsteadOfBatContactAnchor()
        {
            _catalog.clips = new[]
            {
                Clip("Pitcher.Pitch.R", SpriteHandedness.Right), Clip("Batter.DuelSwing.R", SpriteHandedness.Right),
                Clip("Fielder.InfieldGrounder", SpriteHandedness.Shared), Clip("Fielder.OutfieldFlyCatch", SpriteHandedness.Shared)
            };
            _catalog.clips[1].eventAnchors = new[]
            {
                new SpriteEventAnchorDefinition { marker = SpriteAnimationEvent.BatContact, frameIndex = 1,
                    sourceCellSize = new Vector2(362, 362), sourcePositionNormalized = new Vector2(0.25f, 0.2f) }
            };
            var host = new GameObject("MissTest", typeof(RectTransform));
            try
            {
                var rect = host.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(1280, 720);
                var stage = new SpriteMatchStage(rect, _catalog, _sprite);
                stage.Reset();
                RectTransform field = (RectTransform)host.transform.Find("SpriteMatchStage/FieldCamera");
                RectTransform ball = (RectTransform)field.Find("BallVisual");
                stage.RenderPitch(1, true, true);
                Vector2 contactPosition = ball.anchoredPosition;
                stage.RenderPitch(1, true, false);
                Vector2 plate = _layout.GetAnchor(FieldAnchor.HomePlate) - _layout.batContactOffset;
                Assert.That(Vector2.Distance(ball.anchoredPosition, FieldProjection.ToScreen(plate, field.rect.size)), Is.LessThan(0.001f));
                Assert.That(Vector2.Distance(ball.anchoredPosition, contactPosition), Is.GreaterThan(1f));
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void FieldThrowStartsAtPickupHandOnlyAfterReleaseMarker()
        {
            _catalog.clips = new[]
            {
                Clip("Pitcher.Pitch.R", SpriteHandedness.Right), Clip("Batter.DuelSwing.R", SpriteHandedness.Right),
                Clip("Fielder.InfieldGrounder", SpriteHandedness.Shared), Clip("Fielder.OutfieldFlyCatch", SpriteHandedness.Shared)
            };
            SpriteClipDefinition ground = _catalog.clips[2];
            ground.frames[0].events = new[] { SpriteAnimationEvent.GloveContact };
            ground.frames[1].events = new[] { SpriteAnimationEvent.ThrowRelease };
            ground.frames[2].events = System.Array.Empty<SpriteAnimationEvent>();
            var anchor = new SpriteEventAnchorDefinition { marker = SpriteAnimationEvent.ThrowRelease, frameIndex = 1,
                sourceCellSize = new Vector2(362, 362), sourcePositionNormalized = new Vector2(0.7f, 0.3f) };
            ground.eventAnchors = new[] { anchor };
            var host = new GameObject("ThrowTest", typeof(RectTransform));
            try
            {
                var rect = host.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(1280, 720);
                var stage = new SpriteMatchStage(rect, _catalog, _sprite);
                stage.Reset();
                var field = (RectTransform)host.transform.Find("SpriteMatchStage/FieldCamera");
                var ball = (RectTransform)field.Find("BallVisual");
                var play = new BallInPlayEventData(new BattedBallDescriptor(BattedBallType.GroundBall, default,
                    FieldZone.ThirdBase, 50, default, default, false),
                    new FieldingPlayOutcome(Baseball.Simulation.PlateAppearance.PlateAppearanceResult.GroundOut,
                        PlayerPosition.Shortstop, 9, FieldingFailureType.None, true, false, 1));
                Vector2 destination = stage.Projection.GetBase(1);
                stage.RenderFieldThrow(play, destination, 0);
                Assert.That(ball.gameObject.activeSelf, Is.False);
                float progress = 0.2f;
                stage.RenderFieldThrow(play, destination, progress);
                Assert.That(ball.gameObject.activeSelf, Is.True);
                Vector2 pickup = stage.Projection.Project(PlayResolutionFieldLayout.GetBattedBallTarget(play.BattedBall));
                var actor = new SpriteActor(field, stage.Projection, "ProjectionProbe");
                Vector2 release = actor.ProjectSourcePoint(ground, anchor.sourcePositionNormalized,
                    anchor.sourceCellSize, anchor.sourceRootNormalized, pickup);
                ground.TryGetEventTime(SpriteAnimationEvent.ThrowRelease, out float releaseTime);
                float flightProgress = Mathf.InverseLerp(releaseTime, ground.DurationSeconds, progress * ground.DurationSeconds);
                Vector3 expected = BallVisualController.Evaluate(release, destination, flightProgress, 0.025f);
                Assert.That(Vector2.Distance(ball.anchoredPosition,
                    FieldProjection.ToScreen(new Vector2(expected.x, expected.y), field.rect.size, expected.z)), Is.LessThan(0.001f));
                stage.RenderFieldThrow(play, destination, 1f);
                Assert.That(ball.gameObject.activeSelf, Is.False, "송구가 도착한 뒤 외부 공이 베이스에 남지 않아야 한다.");
                stage.Reset();
                stage.RenderFieldThrowToBase(play, 1, 1f);
                var receiver = (RectTransform)field.Find("FielderFirstBase");
                Vector2 receivePosition = destination - _layout.baseReceiverOffset;
                Assert.That(Vector2.Distance(receiver.anchoredPosition,
                    FieldProjection.ToScreen(receivePosition, field.rect.size)), Is.LessThan(0.001f),
                    "송구 수신 야수가 실제 베이스를 커버해야 한다.");
                Assert.That(ball.gameObject.activeSelf, Is.False);
                var unassisted = new BallInPlayEventData(play.BattedBall,
                    new FieldingPlayOutcome(Baseball.Simulation.PlateAppearance.PlateAppearanceResult.GroundOut,
                        PlayerPosition.FirstBase, 9, FieldingFailureType.None, true, false, 1));
                stage.Reset();
                stage.RenderFieldThrowToBase(unassisted, 1, 0.7f);
                Assert.That(ball.gameObject.activeSelf, Is.False, "베이스를 직접 커버하는 야수는 자신에게 송구하지 않는다.");
                stage.Reset();
                stage.RenderFieldThrowToBase(play, 2, 1f);
                var relayFielder = (RectTransform)field.Find("FielderSecondBase");
                Vector2 relayPosition = relayFielder.anchoredPosition;
                Assert.That(stage.RenderRelayThrow(2, 1, 0), Is.True);
                Assert.That(ball.gameObject.activeSelf, Is.False, "중계 송구도 손 이탈 전에는 외부 공을 숨긴다.");
                stage.RenderRelayThrow(2, 1, 0.7f);
                Assert.That(ball.gameObject.activeSelf, Is.True);
                Assert.That(relayFielder.anchoredPosition, Is.EqualTo(relayPosition), "중계 야수가 최초 타구 위치로 되돌아가지 않는다.");
                stage.RenderRelayThrow(2, 1, 1f);
                Assert.That(ball.gameObject.activeSelf, Is.False);
            }
            finally { Object.DestroyImmediate(host); }
        }

        [TestCase(0.457f)]
        [TestCase(0.8f)]
        public void SourceAnchorProjectionMatchesRenderedSpriteAtEachDepth(float imageY)
        {
            var host = new GameObject("AnchorTest", typeof(RectTransform));
            Sprite anchoredSprite = Sprite.Create(_texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.08f));
            try
            {
                RectTransform parent = host.GetComponent<RectTransform>();
                parent.sizeDelta = new Vector2(1280, 720);
                var projection = new FieldProjection(_layout);
                var actor = new SpriteActor(parent, projection, "Actor");
                SpriteClipDefinition clip = Clip("Pitcher.Pitch.R", SpriteHandedness.Right);
                clip.referenceHeightPixels = 4;
                clip.frames[0].sprite = anchoredSprite;
                Vector2 sourcePoint = new Vector2(0.75f, 0.25f);
                Vector2 sourceRoot = new Vector2(0.5f, 0.92f);
                Vector2 position = new Vector2(0.5f, imageY);
                actor.Render(clip, 0, position);
                Vector2 projected = actor.ProjectSourcePoint(clip, sourcePoint, new Vector2(4, 4), sourceRoot, position);
                // 그림자 다음 자식이 실제 프레임 그림이다.
                RectTransform pose = (RectTransform)host.transform.GetChild(0).GetChild(1);
                Vector2 local = new Vector2((sourcePoint.x - sourceRoot.x) * pose.rect.width,
                    (sourceRoot.y - sourcePoint.y) * pose.rect.height);
                Vector3 rendered = parent.InverseTransformPoint(pose.TransformPoint(local));
                Vector2 renderedNormalized = new Vector2((rendered.x - parent.rect.xMin) / parent.rect.width,
                    (parent.rect.yMax - rendered.y) / parent.rect.height);
                Assert.That(Vector2.Distance(projected, renderedNormalized), Is.LessThan(0.00001f));
                Assert.That(actor.ProjectSourcePoint(clip, sourceRoot, new Vector2(4, 4), sourceRoot, position), Is.EqualTo(position));
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(anchoredSprite);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ContactCameraKeepsContinuityAndReturnsBeforeNextPitch(bool isHomeRun)
        {
            _catalog.clips = new[]
            {
                Clip("Pitcher.Pitch.R", SpriteHandedness.Right), Clip("Batter.DuelSwing.R", SpriteHandedness.Right),
                Clip("Fielder.InfieldGrounder", SpriteHandedness.Shared), Clip("Fielder.OutfieldFlyCatch", SpriteHandedness.Shared)
            };
            var host = new GameObject("CameraTest", typeof(RectTransform));
            try
            {
                var rect = host.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(1280, 720);
                var stage = new SpriteMatchStage(rect, _catalog, _sprite);
                stage.Reset();
                var field = (RectTransform)host.transform.Find("SpriteMatchStage/FieldCamera");
                var play = new BallInPlayEventData(new BattedBallDescriptor(BattedBallType.FlyBall, default,
                    FieldZone.CenterField, 100, default, default, isHomeRun), default);
                stage.RenderPitch(1, true, true);
                Vector3 pitchScale = field.localScale;
                Vector2 pitchPosition = field.anchoredPosition;
                stage.RenderContact(play, 0);
                Assert.That(field.localScale, Is.EqualTo(pitchScale));
                Assert.That(field.anchoredPosition, Is.EqualTo(pitchPosition));

                stage.RenderContact(play, _layout.contactCameraPeakProgress);
                Vector2 peakPosition = field.anchoredPosition;
                Assert.That(field.localScale.x, Is.EqualTo(_layout.contactZoom).Within(0.00001f));
                stage.RenderContact(play, _layout.contactCameraPeakProgress + 0.00001f);
                Assert.That(Vector2.Distance(field.anchoredPosition, peakPosition), Is.LessThan(0.01f),
                    "타격 확대에서 타구 추적으로 바뀌는 경계에 화면 점프가 없어야 한다.");
                stage.RenderContact(play, 1);
                Vector3 resultScale = field.localScale;
                Vector2 resultPosition = field.anchoredPosition;
                Assert.That(resultScale.x, Is.EqualTo(isHomeRun ? _layout.highlightZoom : _layout.fieldZoom).Within(0.00001f));
                stage.BeginReturnToDuel();
                stage.RenderReturnToDuel(0);
                Assert.That(field.localScale, Is.EqualTo(resultScale));
                Assert.That(field.anchoredPosition, Is.EqualTo(resultPosition));
                stage.RenderReturnToDuel(0.5f);
                Vector2 midpoint = field.anchoredPosition;
                stage.RenderReturnToDuel(0.5f);
                Assert.That(field.anchoredPosition, Is.EqualTo(midpoint), "같은 진행률을 다시 그려도 카메라가 이동하지 않아야 한다.");
                stage.RenderReturnToDuel(1);
                Assert.That(field.localScale, Is.EqualTo(pitchScale));
                Assert.That(field.anchoredPosition, Is.EqualTo(pitchPosition));
            }
            finally { Object.DestroyImmediate(host); }
        }

        [TestCase(Handedness.Right, 1)]
        [TestCase(Handedness.Right, 2)]
        [TestCase(Handedness.Right, 4)]
        [TestCase(Handedness.Left, 1)]
        [TestCase(Handedness.Left, 2)]
        [TestCase(Handedness.Left, 4)]
        public void SwingingMissPlaysDuringFlightAndTakeKeepsReadyPose(Handedness hand, int speed)
        {
            var catalog = Resources.Load<SpriteAnimationCatalog>("UI/SpriteMatch/AnimationCatalog");
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.TryGetClip(BaseballVisualSequenceResolver.PitchClip(hand), out var pitch), Is.True);
            Assert.That(catalog.TryGetClip(BaseballVisualSequenceResolver.SwingClip(hand), out var swing), Is.True);
            Assert.That(pitch.TryGetEventTime(SpriteAnimationEvent.BallRelease, out float release), Is.True);
            Assert.That(swing.TryGetEventTime(SpriteAnimationEvent.BatContact, out float contact), Is.True);
            var host = new GameObject("MissTimingTest", typeof(RectTransform));
            try
            {
                var rect = host.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(1280, 720);
                var stage = new SpriteMatchStage(rect, catalog, _sprite);
                Assert.That(stage.SetHands(hand, hand), Is.True);
                stage.Reset();
                var pose = host.transform.Find("SpriteMatchStage/FieldCamera/Batter/Pose")
                    .GetComponent<UnityEngine.UI.Image>();
                float duration = stage.PitchDuration;
                stage.RenderPitch(release / duration, true);
                Assert.That(pose.sprite, Is.SameAs(swing.frames[0].sprite),
                    "공이 출발하기 전에 헛스윙이 진행되면 안 된다.");
                bool sawSwingDuringFlight = false;
                Sprite contactSprite = SpriteSequencePlayer.Sample(swing, contact).sprite;
                for (float elapsed = 0; elapsed < duration; elapsed += speed / 60f)
                {
                    stage.RenderPitch(elapsed / duration, true);
                    if (elapsed > release && pose.sprite == contactSprite) sawSwingDuringFlight = true;
                    stage.RenderPitch(elapsed / duration, false);
                    Assert.That(pose.sprite, Is.SameAs(swing.frames[0].sprite));
                }
                Assert.That(sawSwingDuringFlight, Is.True, "배속 재생에서도 비행 중 스윙 자세가 보여야 한다.");
                stage.RenderPitch(1, true);
                Assert.That(pose.sprite, Is.SameAs(swing.frames[swing.frames.Length - 1].sprite));
            }
            finally { Object.DestroyImmediate(host); }
        }

        private SpriteClipDefinition Clip(string id, SpriteHandedness hand) => new SpriteClipDefinition
        {
            clipId = id, approved = true, handedness = hand,
            frames = new[]
            {
                new SpriteFrameDefinition { sprite = _sprite, durationMs = 100, events = new[] { SpriteAnimationEvent.BallRelease } },
                new SpriteFrameDefinition { sprite = _sprite, durationMs = 200, events = new[] { SpriteAnimationEvent.BatContact } },
                new SpriteFrameDefinition { sprite = _sprite, durationMs = 300, events = new[] { SpriteAnimationEvent.GloveContact } }
            }
        };
    }
}
