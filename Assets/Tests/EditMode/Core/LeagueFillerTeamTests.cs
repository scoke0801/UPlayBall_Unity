using System;
using Baseball.Core.Historical;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Core
{
    /// <summary>CPU 임시 구단의 Key 규칙과 조 참가 계약을 검증한다.</summary>
    public sealed class LeagueFillerTeamTests
    {
        [Test]
        public void TeamKey_덱과원본구단을Key에서그대로복원한다()
        {
            string yearTeam = LeagueFillerTeamKey.Create(3, LeagueGrade.Minor, 2, 7,
                LeagueFillerDeckType.YearTeam, "FRANCHISE_abc_1995");
            string legend = LeagueFillerTeamKey.Create(12, LeagueGrade.Galaxy, 0, 1, LeagueFillerDeckType.Legend);

            Assert.That(LeagueFillerTeamKey.TryParse(yearTeam, out LeagueFillerDeckType yearDeck, out string source), Is.True);
            Assert.That(yearDeck, Is.EqualTo(LeagueFillerDeckType.YearTeam));
            Assert.That(source, Is.EqualTo("FRANCHISE_abc_1995"));
            Assert.That(LeagueFillerTeamKey.TryParse(legend, out LeagueFillerDeckType legendDeck, out string none), Is.True);
            Assert.That(legendDeck, Is.EqualTo(LeagueFillerDeckType.Legend));
            Assert.That(none, Is.Null);
            Assert.That(LeagueFillerTeamKey.TryParse("FRANCHISE_abc_1995", out _, out _), Is.False);
        }

        [Test]
        public void TeamKey_연도구단덱만원본구단을가진다()
        {
            Assert.Throws<ArgumentException>(() =>
                LeagueFillerTeamKey.Create(1, LeagueGrade.Rookie, 0, 0, LeagueFillerDeckType.YearTeam));
            Assert.Throws<ArgumentException>(() =>
                LeagueFillerTeamKey.Create(1, LeagueGrade.Galaxy, 0, 0, LeagueFillerDeckType.Legend, "FRANCHISE_abc_1995"));
        }

        [Test]
        public void LeagueInstance_CPU구단은참가팀수에포함되지만승강대상은아니다()
        {
            string filler = LeagueFillerTeamKey.Create(2, LeagueGrade.Minor, 0, 0, LeagueFillerDeckType.YearTeam, "TEAM-09");
            var league = new LeagueInstance("owner:01:0000", LeagueGrade.Minor, new[] { "TEAM-00" },
                isPooledGroup: true, fillerTeamSeasonKeys: new[] { filler });

            Assert.That(league.ParticipantTeamCount, Is.EqualTo(2));
            Assert.That(league.IsPermanentParticipant("TEAM-00"), Is.True);
            Assert.That(league.IsPermanentParticipant(filler), Is.False);
        }

        [Test]
        public void LeagueInstance_CPU구단은재편성조에만배정한다()
        {
            string filler = LeagueFillerTeamKey.Create(2, LeagueGrade.Rookie, 0, 0, LeagueFillerDeckType.YearTeam, "TEAM-09");
            var regular = new[] { "A", "B", "C", "D", "E", "F" };

            Assert.Throws<ArgumentException>(() => new LeagueInstance("fixed", LeagueGrade.Rookie, regular,
                fillerTeamSeasonKeys: new[] { filler }));
            Assert.Throws<ArgumentException>(() => new LeagueInstance("pooled", LeagueGrade.Rookie, new[] { "A" },
                isPooledGroup: true, fillerTeamSeasonKeys: new[] { "NOT-A-FILLER" }));
        }
    }
}
