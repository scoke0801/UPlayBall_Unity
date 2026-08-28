using Baseball.Game.Career;
using Baseball.Simulation.Match;
using Baseball.Simulation.PlateAppearance;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game
{
    /// <summary>
    /// 경기 이벤트 자동 중계가 타석 단위 진행과 내 선수 입력 정지를 지키는지 검증한다.
    /// </summary>
    public sealed class CareerMatchPlaybackTests
    {
        [Test]
        public void AdvanceAutomatic_한타석의결과와주자진루까지한번에공개한다()
        {
            MatchEvent[] events =
            {
                CreateEvent(0, MatchEventType.Pitch, 10, 0, 0, PitchResult.Ball),
                CreateEvent(1, MatchEventType.Pitch, 10, 0, 0, PitchResult.InPlay),
                CreateEvent(
                    2,
                    MatchEventType.RunnerAdvance,
                    10,
                    10,
                    0,
                    PitchResult.None,
                    PlateAppearanceResult.None,
                    0,
                    1),
                CreateEvent(
                    3,
                    MatchEventType.PlateAppearanceEnded,
                    10,
                    10,
                    0,
                    PitchResult.None,
                    PlateAppearanceResult.Single),
                CreateEvent(4, MatchEventType.Pitch, 20, 0, 0, PitchResult.CalledStrike)
            };
            var playback = new CareerMatchPlayback();

            bool didAdvance = playback.AdvanceAutomatic(events, controlledPlayerId: 99);
            CareerMatchPlaybackSnapshot snapshot = playback.BuildSnapshot(events);

            Assert.That(didAdvance, Is.True);
            Assert.That(playback.VisibleEventCount, Is.EqualTo(4));
            Assert.That(snapshot.FirstRunnerId, Is.EqualTo(10));
            Assert.That(snapshot.LatestPlateAppearanceResult, Is.EqualTo(PlateAppearanceResult.Single));
            Assert.That(snapshot.BatterId, Is.EqualTo(10));
        }

        [Test]
        public void AdvanceAutomatic_내선수타격이벤트앞에서멈추고버튼결과만공개한다()
        {
            MatchEvent[] events =
            {
                CreateEvent(0, MatchEventType.Pitch, 99, 0, 0, PitchResult.InPlay),
                CreateEvent(
                    1,
                    MatchEventType.PlateAppearanceEnded,
                    99,
                    99,
                    1,
                    PitchResult.None,
                    PlateAppearanceResult.GroundOut),
                CreateEvent(2, MatchEventType.Pitch, 20, 0, 1, PitchResult.InPlay),
                CreateEvent(
                    3,
                    MatchEventType.PlateAppearanceEnded,
                    20,
                    20,
                    2,
                    PitchResult.None,
                    PlateAppearanceResult.FlyOut)
            };
            var playback = new CareerMatchPlayback();

            Assert.That(playback.AdvanceAutomatic(events, controlledPlayerId: 99), Is.False);
            Assert.That(playback.VisibleEventCount, Is.Zero);

            Assert.That(playback.RevealControlledPlay(events, controlledPlayerId: 99), Is.True);
            Assert.That(playback.VisibleEventCount, Is.EqualTo(2));
            Assert.That(playback.AdvanceAutomatic(events, controlledPlayerId: 99), Is.True);
            Assert.That(playback.VisibleEventCount, Is.EqualTo(events.Length));
        }

        [Test]
        public void BuildSnapshot_진루와득점후현재주자상태를복원한다()
        {
            MatchEvent[] events =
            {
                CreateEvent(
                    0,
                    MatchEventType.RunnerAdvance,
                    10,
                    31,
                    0,
                    PitchResult.None,
                    PlateAppearanceResult.None,
                    0,
                    1),
                CreateEvent(
                    1,
                    MatchEventType.RunnerAdvance,
                    20,
                    31,
                    0,
                    PitchResult.None,
                    PlateAppearanceResult.None,
                    1,
                    3),
                CreateEvent(
                    2,
                    MatchEventType.RunnerAdvance,
                    20,
                    20,
                    0,
                    PitchResult.None,
                    PlateAppearanceResult.None,
                    0,
                    1),
                CreateEvent(
                    3,
                    MatchEventType.RunnerAdvance,
                    30,
                    31,
                    0,
                    PitchResult.None,
                    PlateAppearanceResult.None,
                    3,
                    4,
                    awayScore: 1),
                CreateEvent(
                    4,
                    MatchEventType.PlateAppearanceEnded,
                    30,
                    30,
                    0,
                    PitchResult.None,
                    PlateAppearanceResult.Single,
                    awayScore: 1)
            };
            var playback = new CareerMatchPlayback();
            playback.RevealAll(events);

            CareerMatchPlaybackSnapshot snapshot = playback.BuildSnapshot(events);

            Assert.That(snapshot.AwayScore, Is.EqualTo(1));
            Assert.That(snapshot.FirstRunnerId, Is.EqualTo(20));
            Assert.That(snapshot.SecondRunnerId, Is.Zero);
            Assert.That(snapshot.ThirdRunnerId, Is.Zero);
        }

        [Test]
        public void RevealControlledPlay_병살의주자아웃까지같은타석결과로공개한다()
        {
            MatchEvent[] events =
            {
                CreateEvent(0, MatchEventType.Pitch, 99, 99, 0, PitchResult.InPlay),
                CreateEvent(
                    1,
                    MatchEventType.Out,
                    31,
                    31,
                    1,
                    PitchResult.None,
                    PlateAppearanceResult.GroundOut),
                CreateEvent(
                    2,
                    MatchEventType.Out,
                    99,
                    99,
                    2,
                    PitchResult.None,
                    PlateAppearanceResult.GroundOut),
                CreateEvent(
                    3,
                    MatchEventType.PlateAppearanceEnded,
                    99,
                    99,
                    2,
                    PitchResult.None,
                    PlateAppearanceResult.GroundOut)
            };
            var playback = new CareerMatchPlayback();

            Assert.That(playback.RevealControlledPlay(events, controlledPlayerId: 99), Is.True);
            Assert.That(playback.VisibleEventCount, Is.EqualTo(events.Length));
            Assert.That(
                playback.TryGetControlledPlateAppearanceSummary(
                    events,
                    firstEventIndex: 0,
                    controlledPlayerId: 99,
                    out CareerPlateAppearanceSummary summary),
                Is.True);
            Assert.That(summary.IsDoublePlay, Is.True);
            Assert.That(summary.OutsOnPlay, Is.EqualTo(2));
        }

        [Test]
        public void AdvanceAutomatic_내선수교체출전이벤트를공개한뒤입력대기로넘긴다()
        {
            MatchEvent[] events =
            {
                CreateEvent(0, MatchEventType.PlayerSubstitution, 99, 31, 1, PitchResult.None)
            };
            var playback = new CareerMatchPlayback();

            Assert.That(playback.AdvanceAutomatic(events, controlledPlayerId: 99), Is.True);
            Assert.That(playback.VisibleEventCount, Is.EqualTo(1));
        }

        private static MatchEvent CreateEvent(
            int sequence,
            MatchEventType eventType,
            int batterId,
            int playerId,
            int outs,
            PitchResult pitchResult,
            PlateAppearanceResult plateAppearanceResult = PlateAppearanceResult.None,
            int fromBase = 0,
            int toBase = 0,
            int awayScore = 0,
            int homeScore = 0)
        {
            return new MatchEvent(
                sequence,
                eventType,
                1,
                InningHalf.Top,
                batterId,
                100,
                playerId,
                pitchResult,
                plateAppearanceResult,
                fromBase,
                toBase,
                0,
                0,
                outs,
                awayScore,
                homeScore);
        }
    }
}
