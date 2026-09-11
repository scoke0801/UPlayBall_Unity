using System.Collections.Generic;
using System.Reflection;
using Baseball.Core.Players;
using Baseball.Presentation.Match;
using Baseball.Simulation.Match;
using Baseball.Simulation.PlateAppearance;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Tests.EditMode.Presentation.Match
{
    /// <summary>클로즈업 리소스와 공개 사건의 일치·결과 선공개 차단을 검증한다.</summary>
    public sealed class OwnerMatchHighlightInsetTests
    {
        [Test]
        public void 하이라이트여섯종이실제스프라이트와한글설명을가진다()
        {
            OwnerMatchHighlightConfig config = OwnerMatchHighlightConfig.Load();
            Assert.That(config.images.Length, Is.EqualTo(6));
            var kinds = new HashSet<OwnerMatchHighlightKind>();
            foreach (OwnerMatchHighlightImage definition in config.images)
            {
                Assert.That(kinds.Add(definition.kind), Is.True, "같은 장면의 중복 등록");
                Assert.That(definition.kind, Is.Not.EqualTo(OwnerMatchHighlightKind.None));
                Assert.That(definition.caption, Is.Not.Empty);
                Sprite sprite = Resources.Load<Sprite>(definition.resourcePath);
                Assert.That(sprite, Is.Not.Null, definition.resourcePath);
                Assert.That(sprite.rect.width / sprite.rect.height, Is.EqualTo(16f / 9f).Within(0.02f));
            }
        }

        [TestCase(OwnerMatchPlaybackSpeed.Normal)]
        [TestCase(OwnerMatchPlaybackSpeed.Fast)]
        [TestCase(OwnerMatchPlaybackSpeed.FourTimes)]
        public void 모든배속에서삽입컷을읽을최소시간을보장한다(OwnerMatchPlaybackSpeed speed)
        {
            OwnerMatchHighlightConfig config = OwnerMatchHighlightConfig.Load();
            Assert.That(config.GetDuration(speed), Is.GreaterThanOrEqualTo(config.minimumDurationSeconds));
            Assert.That(config.GetDuration(speed), Is.LessThanOrEqualTo(config.durationSeconds));
        }

        [Test]
        public void 타격시점에홈런결과를선공개하지않는다()
        {
            var homeRun = new BallInPlayEventData(new BattedBallDescriptor(BattedBallType.FlyBall, default,
                FieldZone.CenterField, 1, default, default, true), default);
            Assert.That(OwnerMatchHighlightCue.Resolve(Event(MatchEventType.Pitch, PlateAppearanceResult.None,
                homeRun, PitchResult.InPlay)), Is.EqualTo(OwnerMatchHighlightKind.BatContact));
            Assert.That(OwnerMatchHighlightCue.Resolve(Event(MatchEventType.Contact, PlateAppearanceResult.None, homeRun)),
                Is.EqualTo(OwnerMatchHighlightKind.None));
            Assert.That(OwnerMatchHighlightCue.Resolve(Event(MatchEventType.Hit, PlateAppearanceResult.HomeRun)),
                Is.EqualTo(OwnerMatchHighlightKind.HomeRun));
            Assert.That(OwnerMatchHighlightCue.Resolve(Event(MatchEventType.PlateAppearanceEnded, PlateAppearanceResult.HomeRun, homeRun)),
                Is.EqualTo(OwnerMatchHighlightKind.None), "홈런 컷을 타석 종료에서 반복하지 않는다.");
        }

        [TestCase(MatchEventType.StealSucceeded)]
        [TestCase(MatchEventType.CaughtStealing)]
        public void 도루성공과실패모두판정없는슬라이딩컷을사용한다(MatchEventType type)
        {
            Assert.That(OwnerMatchHighlightCue.Resolve(Event(type)), Is.EqualTo(OwnerMatchHighlightKind.Slide));
            Assert.That(OwnerMatchHighlightCue.Resolve(Event(MatchEventType.StealAttempted)), Is.EqualTo(OwnerMatchHighlightKind.None));
        }

        [TestCase(PlateAppearanceResult.Double, OwnerMatchHighlightKind.Slide)]
        [TestCase(PlateAppearanceResult.Triple, OwnerMatchHighlightKind.Slide)]
        [TestCase(PlateAppearanceResult.Walk, OwnerMatchHighlightKind.None)]
        [TestCase(PlateAppearanceResult.Strikeout, OwnerMatchHighlightKind.None)]
        public void 장타도착과볼넷삼진을구별한다(PlateAppearanceResult result, OwnerMatchHighlightKind expected)
        {
            Assert.That(OwnerMatchHighlightCue.Resolve(Event(MatchEventType.PlateAppearanceEnded, result)), Is.EqualTo(expected));
        }

        [TestCase(BattedBallType.FlyBall, true, FieldingFailureType.None, OwnerMatchHighlightKind.GloveCatch)]
        [TestCase(BattedBallType.FlyBall, false, FieldingFailureType.None, OwnerMatchHighlightKind.GloveCatch)]
        [TestCase(BattedBallType.LineDrive, false, FieldingFailureType.None, OwnerMatchHighlightKind.GreatCatch)]
        [TestCase(BattedBallType.LineDrive, true, FieldingFailureType.None, OwnerMatchHighlightKind.GloveCatch)]
        [TestCase(BattedBallType.LineDrive, false, FieldingFailureType.FieldingError, OwnerMatchHighlightKind.None)]
        [TestCase(BattedBallType.LineDrive, false, FieldingFailureType.Reach, OwnerMatchHighlightKind.None)]
        public void 성공한낮은비정형타구만호수비컷으로표시한다(BattedBallType type, bool routine,
            FieldingFailureType failure, OwnerMatchHighlightKind expected)
        {
            var play = Play(type, PlateAppearanceResult.FlyOut, PlayerPosition.CenterField, failure, routine);
            Assert.That(OwnerMatchHighlightCue.Resolve(Event(MatchEventType.PlateAppearanceEnded, PlateAppearanceResult.FlyOut, play)),
                Is.EqualTo(expected));
            Assert.That(OwnerMatchHighlightCue.Resolve(Event(MatchEventType.FieldingPlayStarted, PlateAppearanceResult.None, play)),
                Is.EqualTo(OwnerMatchHighlightKind.None), "수비 시작에 성공 컷을 공개하지 않는다.");
        }

        [TestCase(PlayerPosition.Shortstop, OwnerMatchHighlightKind.Throw)]
        [TestCase(PlayerPosition.StartingPitcher, OwnerMatchHighlightKind.Throw)]
        [TestCase(PlayerPosition.FirstBase, OwnerMatchHighlightKind.GloveCatch)]
        public void 직접베이스를밟는수비에송구장면을붙이지않는다(PlayerPosition position, OwnerMatchHighlightKind expected)
        {
            var play = Play(BattedBallType.GroundBall, PlateAppearanceResult.GroundOut, position, FieldingFailureType.None, true);
            Assert.That(OwnerMatchHighlightCue.Resolve(Event(MatchEventType.PlateAppearanceEnded, PlateAppearanceResult.GroundOut, play)),
                Is.EqualTo(expected));
        }

        [Test]
        public void 삽입컷은초기에숨겨지고기존투구상세를유지하며입력을가로채지않는다()
        {
            var host = new GameObject("HighlightHost", typeof(RectTransform));
            try
            {
                var view = UI_Scene_OwnerMatchSpectator.CreateRuntime((RectTransform)host.transform);
                Transform sidebar = view.transform.Find("BroadcastCanvas/GameCastSidebar");
                Transform inset = sidebar.Find("HighlightInset");
                Assert.That(inset.gameObject.activeSelf, Is.False);
                Assert.That(sidebar.Find("PitchContext").gameObject.activeSelf, Is.True);
                Assert.That(inset.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
                Assert.That(inset.Find("Picture").GetComponent<Image>().raycastTarget, Is.False);
                Assert.That(inset.Find("Picture").GetComponent<Baseball.Presentation.UI.UICircleGraphic>(), Is.Null);
            }
            finally { Object.DestroyImmediate(host); }
        }

        [TestCase(OwnerMatchPlaybackSpeed.Normal)]
        [TestCase(OwnerMatchPlaybackSpeed.Fast)]
        [TestCase(OwnerMatchPlaybackSpeed.FourTimes)]
        public void 삽입컷종료와화면숨김에서상세정보를복원한다(OwnerMatchPlaybackSpeed speed)
        {
            var host = new GameObject("HighlightLifecycle", typeof(RectTransform));
            try
            {
                var view = UI_Scene_OwnerMatchSpectator.CreateRuntime((RectTransform)host.transform);
                view.SetVisible(true);
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                MethodInfo show = typeof(UI_Scene_OwnerMatchSpectator).GetMethod("TryPresentHighlightInset", flags);
                MethodInfo advance = typeof(UI_Scene_OwnerMatchSpectator).GetMethod("AdvanceHighlightInset", flags);
                Transform sidebar = view.transform.Find("BroadcastCanvas/GameCastSidebar");
                Transform inset = sidebar.Find("HighlightInset");
                Transform detail = sidebar.Find("PitchContext");
                Assert.That(show.Invoke(view, new object[] { OwnerMatchHighlightKind.Throw, speed }), Is.EqualTo(true));
                Assert.That(detail.gameObject.activeSelf, Is.False);
                advance.Invoke(view, new object[] { 0.15f });
                Assert.That(inset.GetComponent<CanvasGroup>().alpha, Is.GreaterThan(0.5f));
                advance.Invoke(view, new object[] { 2f });
                Assert.That(inset.gameObject.activeSelf, Is.False);
                Assert.That(detail.gameObject.activeSelf, Is.True);
                show.Invoke(view, new object[] { OwnerMatchHighlightKind.HomeRun, speed });
                view.SetVisible(false);
                view.SetVisible(true);
                Assert.That(inset.gameObject.activeSelf, Is.False, "화면을 다시 열 때 이전 홈런 컷이 남지 않아야 한다.");
                Assert.That(detail.gameObject.activeSelf, Is.True);
            }
            finally { Object.DestroyImmediate(host); }
        }

        private static BallInPlayEventData Play(BattedBallType type, PlateAppearanceResult result, PlayerPosition position,
            FieldingFailureType failure, bool routine) => new BallInPlayEventData(
                new BattedBallDescriptor(type, default, FieldZone.CenterField, 1, default, default, false),
                new FieldingPlayOutcome(result, position, 30, failure, routine, false, 0.5));

        private static MatchEvent Event(MatchEventType type, PlateAppearanceResult result = PlateAppearanceResult.None,
            BallInPlayEventData play = default, PitchResult pitch = PitchResult.None) =>
            new MatchEvent(1, type, 1, InningHalf.Top, 11, 21, 11, pitch, result, 1, 2, 0, 0, 1, 0, 0, ballInPlayData: play);
    }
}
