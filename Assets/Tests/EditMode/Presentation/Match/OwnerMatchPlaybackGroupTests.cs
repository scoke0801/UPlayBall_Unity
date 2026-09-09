using Baseball.Presentation.Match;
using Baseball.Simulation.Match;
using Baseball.Simulation.PlateAppearance;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Presentation.Match
{
    /// <summary>기록 순서가 다른 송구 아웃의 인과와 목적지 근거를 검증한다.</summary>
    public sealed class OwnerMatchPlaybackGroupTests
    {
        [Test]
        public void 송구아웃은선행Out과함께송구재생이끝난뒤공개할묶음이된다()
        {
            MatchEvent outEvent = Event(MatchEventType.Out, 11);
            MatchEvent throwEvent = Event(MatchEventType.RunnerThrownOut, 11, from: 1, to: 3);
            OwnerMatchPlaybackGroup group = OwnerMatchPlaybackGroup.Resolve(outEvent, throwEvent);
            Assert.That(group.EventCount, Is.EqualTo(2));
            Assert.That(group.VisualEvent, Is.EqualTo(throwEvent));
            Assert.That(group.VisualEvent.ToBase, Is.EqualTo(3));
            Assert.That(OwnerMatchPlaybackGroup.Resolve(outEvent, Event(MatchEventType.RunnerThrownOut, 12)).EventCount, Is.EqualTo(1));
        }

        [Test]
        public void 병살의선행주자는2루타자는1루로향한다()
        {
            Assert.That(OwnerMatchPlaybackGroup.ResolveOutBase(Event(MatchEventType.Out, 11, PlateAppearanceResult.GroundOut), 7, 11, 0, 0), Is.EqualTo(2));
            Assert.That(OwnerMatchPlaybackGroup.ResolveOutBase(Event(MatchEventType.Out, 7, PlateAppearanceResult.GroundOut), 7, 0, 0, 0), Is.EqualTo(1));
        }

        [Test]
        public void 야수선택은공개강제진루관계만사용하고근거없으면목적지를만들지않는다()
        {
            MatchEvent choice = Event(MatchEventType.Out, 11, PlateAppearanceResult.FieldersChoice);
            Assert.That(OwnerMatchPlaybackGroup.ResolveOutBase(choice, 7, 11, 0, 0), Is.EqualTo(2));
            Assert.That(OwnerMatchPlaybackGroup.ResolveOutBase(choice, 7, 0, 11, 0), Is.Zero);
            Assert.That(OwnerMatchPlaybackGroup.ResolveOutBase(choice, 7, 0, 0, 0), Is.Zero);
            Assert.That(OwnerMatchPlaybackGroup.ResolveOutBase(Event(MatchEventType.Out, 7, PlateAppearanceResult.Strikeout), 7, 11, 0, 0), Is.Zero);
        }

        private static MatchEvent Event(MatchEventType type, int playerId, PlateAppearanceResult result = PlateAppearanceResult.None, int from = 0, int to = 0) =>
            new MatchEvent(1, type, 1, InningHalf.Top, 0, 2, playerId, PitchResult.None, result, from, to, 0, 0, 1, 0, 0);
    }
}
