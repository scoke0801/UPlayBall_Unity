using Baseball.Game.Career;
using Baseball.Simulation.Match;
using Baseball.Simulation.PlateAppearance;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game
{
    /// <summary>
    /// 라인 스코어가 공개된 이벤트만으로 이닝별 득점과 미진행 이닝을 구분하는지 검증한다.
    /// </summary>
    public sealed class MatchLineScoreTests
    {
        [Test]
        public void Create_공개된이닝의득점만집계하고나머지는미진행으로남긴다()
        {
            MatchEvent[] events =
            {
                CreateEvent(0, MatchEventType.Score, inning: 1, InningHalf.Top, awayScore: 1),
                CreateEvent(1, MatchEventType.HalfInningEnded, inning: 1, InningHalf.Top, awayScore: 1),
                CreateEvent(2, MatchEventType.Score, inning: 1, InningHalf.Bottom, awayScore: 1, homeScore: 1),
                CreateEvent(3, MatchEventType.Score, inning: 1, InningHalf.Bottom, awayScore: 1, homeScore: 2),
                CreateEvent(4, MatchEventType.HalfInningEnded, inning: 1, InningHalf.Bottom, awayScore: 1, homeScore: 2),
                CreateEvent(5, MatchEventType.PlateAppearanceEnded, inning: 2, InningHalf.Top, awayScore: 1, homeScore: 2)
            };

            MatchLineScore lineScore = MatchLineScore.Create(events, events.Length);

            Assert.That(lineScore.GetAwayRuns(1), Is.EqualTo(1));
            Assert.That(lineScore.GetHomeRuns(1), Is.EqualTo(2));
            Assert.That(lineScore.GetAwayRuns(2), Is.Zero);
            Assert.That(lineScore.GetHomeRuns(2), Is.EqualTo(MatchLineScore.NotPlayed));
            Assert.That(lineScore.GetAwayRuns(9), Is.EqualTo(MatchLineScore.NotPlayed));
            Assert.That(lineScore.AwayTotal, Is.EqualTo(1));
            Assert.That(lineScore.HomeTotal, Is.EqualTo(2));
            Assert.That(lineScore.CurrentInning, Is.EqualTo(2));
            Assert.That(lineScore.InningCount, Is.EqualTo(9));
        }

        [Test]
        public void Create_아직공개하지않은이벤트는집계에넣지않는다()
        {
            MatchEvent[] events =
            {
                CreateEvent(0, MatchEventType.Score, inning: 1, InningHalf.Top, awayScore: 1),
                CreateEvent(1, MatchEventType.Score, inning: 1, InningHalf.Top, awayScore: 2)
            };

            MatchLineScore lineScore = MatchLineScore.Create(events, visibleEventCount: 1);

            Assert.That(lineScore.GetAwayRuns(1), Is.EqualTo(1));
            Assert.That(lineScore.AwayTotal, Is.EqualTo(1));
        }

        [Test]
        public void Create_연장전에서는표시이닝수를늘린다()
        {
            MatchEvent[] events =
            {
                CreateEvent(0, MatchEventType.Score, inning: 11, InningHalf.Top, awayScore: 1)
            };

            MatchLineScore lineScore = MatchLineScore.Create(events, events.Length);

            Assert.That(lineScore.InningCount, Is.EqualTo(11));
            Assert.That(lineScore.GetAwayRuns(11), Is.EqualTo(1));
        }

        private static MatchEvent CreateEvent(
            int sequence,
            MatchEventType eventType,
            int inning,
            InningHalf half,
            int awayScore = 0,
            int homeScore = 0)
        {
            return new MatchEvent(
                sequence,
                eventType,
                inning,
                half,
                batterId: 10,
                pitcherId: 500,
                playerId: 10,
                PitchResult.None,
                PlateAppearanceResult.None,
                fromBase: 0,
                toBase: 0,
                balls: 0,
                strikes: 0,
                outs: 0,
                awayScore,
                homeScore);
        }
    }
}
