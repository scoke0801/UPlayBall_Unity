using System;
using Baseball.Presentation.Match;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Presentation.Match
{
    /// <summary>공통 경기 HUD가 모드 권한 없이 공개된 경기 상태만 표현하는지 검증한다.</summary>
    public sealed class MatchHudPresentationModelTests
    {
        [Test]
        public void Build_팀점수카운트주자와현재승부를그대로투영한다()
        {
            MatchHudPresentationModel model = new MatchHudPresentationModelBuilder().Build(
                7,
                MatchHudHalf.Bottom,
                new MatchHudTeamModel("부산 앵커스", 2, false),
                new MatchHudTeamModel("서울 웨이브", 3, true),
                new MatchHudCountModel(2, 1, 1),
                new MatchHudBaseStateModel(
                    new MatchHudParticipantModel(11, "김가람"),
                    null,
                    new MatchHudParticipantModel(18, "박해준")),
                new MatchHudParticipantModel(23, "이도윤"),
                new MatchHudParticipantModel(31, "최민석"),
                false);

            Assert.That(model.Inning, Is.EqualTo(7));
            Assert.That(model.Half, Is.EqualTo(MatchHudHalf.Bottom));
            Assert.That(model.AwayTeam.Score, Is.EqualTo(2));
            Assert.That(model.HomeTeam.Score, Is.EqualTo(3));
            Assert.That(model.BattingTeam.Name, Is.EqualTo("서울 웨이브"));
            Assert.That(model.Count.Balls, Is.EqualTo(2));
            Assert.That(model.Count.Strikes, Is.EqualTo(1));
            Assert.That(model.Count.Outs, Is.EqualTo(1));
            Assert.That(model.Bases.First.Name, Is.EqualTo("김가람"));
            Assert.That(model.Bases.HasRunnerOnSecond, Is.False);
            Assert.That(model.Bases.Third.Name, Is.EqualTo("박해준"));
            Assert.That(model.Batter.Name, Is.EqualTo("이도윤"));
            Assert.That(model.Pitcher.Name, Is.EqualTo("최민석"));
        }

        [Test]
        public void Build_이닝교대중에는이전카운트와주자를노출하지않는다()
        {
            MatchHudPresentationModel model = new MatchHudPresentationModelBuilder().Build(
                4,
                MatchHudHalf.Top,
                new MatchHudTeamModel("원정", 1, true),
                new MatchHudTeamModel("홈", 1, false),
                new MatchHudCountModel(3, 2, 2),
                new MatchHudBaseStateModel(new MatchHudParticipantModel(9, "주자"), null, null),
                MatchHudParticipantModel.Empty,
                MatchHudParticipantModel.Empty,
                true);

            Assert.That(model.IsBetweenInnings, Is.True);
            Assert.That(model.Count.Balls, Is.Zero);
            Assert.That(model.Count.Strikes, Is.Zero);
            Assert.That(model.Count.Outs, Is.Zero);
            Assert.That(model.Bases.HasAnyRunner, Is.False);
            Assert.That(model.Batter.HasValue, Is.False);
            Assert.That(model.Pitcher.HasValue, Is.False);
        }

        [Test]
        public void Build_공격팀이없거나둘이면거부한다()
        {
            var builder = new MatchHudPresentationModelBuilder();

            Assert.Throws<ArgumentException>(() => builder.Build(
                1,
                MatchHudHalf.Top,
                new MatchHudTeamModel("원정", 0, false),
                new MatchHudTeamModel("홈", 0, false),
                MatchHudCountModel.Empty,
                MatchHudBaseStateModel.Empty,
                null,
                null,
                false));
        }

        [Test]
        public void MatchHUDBase_공통View계약을구현한다()
        {
            Assert.That(typeof(IMatchHudView).IsAssignableFrom(typeof(MatchHUDBase)), Is.True);
            Assert.That(typeof(MatchHUDBase).IsAssignableFrom(typeof(MatchHudView)), Is.True);
        }
    }
}
