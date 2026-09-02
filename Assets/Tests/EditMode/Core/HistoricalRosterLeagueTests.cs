using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Core
{
    /// <summary>역사 시뮬레이션의 공통 Roster·League 데이터 계약을 검증한다.</summary>
    public sealed class HistoricalRosterLeagueTests
    {
        [Test]
        public void ActiveRosterCompositionRule_정확한25인계약과역할매핑을제공한다()
        {
            ActiveRosterCompositionRule rule = ActiveRosterCompositionRule.Standard;

            Assert.That(ActiveRosterCompositionRule.ActiveRosterSize, Is.EqualTo(25));
            Assert.That(ActiveRosterCompositionRule.HitterCount, Is.EqualTo(14));
            Assert.That(ActiveRosterCompositionRule.PitcherCount, Is.EqualTo(11));
            Assert.That(ActiveRosterCompositionRule.BullpenPitcherCount, Is.EqualTo(4));
            Assert.That(ActiveRosterCompositionRule.MaxForeignPlayers, Is.EqualTo(3));
            Assert.That(
                rule.GetAssignedPosition(ActiveRosterRole.StartingDesignatedHitter),
                Is.EqualTo(PlayerPosition.DesignatedHitter));
            Assert.That(
                rule.GetAssignedPitcherRole(ActiveRosterRole.Bullpen4),
                Is.EqualTo(PitcherRole.MiddleRelief));
        }

        [Test]
        public void CurrentRosterState_입력목록을복사해외부변경과분리한다()
        {
            var source = new List<ActiveRosterEntry>
            {
                CreateEntry(1, ActiveRosterRole.BenchHitter)
            };

            var state = new CurrentRosterState("COMETS_2011", source);
            source.Clear();

            Assert.That(state.Entries.Count, Is.EqualTo(1));
            Assert.That(state.Entries[0].PlayerPersonId, Is.EqualTo("PERSON_001"));
        }

        [Test]
        public void LeagueInstance_정규Franchise는정확히10개이고특수팀은별도로센다()
        {
            string[] regularTeams = CreateRegularTeams(10);
            var specialTeams = new[]
            {
                new SpecialCompositeTeamRegistration(
                    "SPECIAL_ALLSTAR_2011", 2011, SpecialCompositeTeamType.AllStarComposite),
                new SpecialCompositeTeamRegistration(
                    "SPECIAL_GG_2011", 2011, SpecialCompositeTeamType.GoldenGloveComposite),
                new SpecialCompositeTeamRegistration(
                    "SPECIAL_YEAR_2011", 2011, SpecialCompositeTeamType.YearSelectComposite)
            };

            var league = new LeagueInstance("ROOKIE_2011", LeagueGrade.Rookie, regularTeams, specialTeams);

            Assert.That(league.RegularFranchiseTeamCount, Is.EqualTo(10));
            Assert.That(league.SpecialCompositeTeams.Count, Is.EqualTo(3));
            Assert.That(league.ParticipantTeamCount, Is.EqualTo(13));
            Assert.Throws<ArgumentException>(() =>
                new LeagueInstance("INVALID", LeagueGrade.Rookie, CreateRegularTeams(9)));
        }

        [Test]
        public void TeamSeasonLeagueState_모든신규TeamSeason은Rookie에서시작한다()
        {
            var state = new TeamSeasonLeagueState("COMETS_2011");

            Assert.That(state.Grade, Is.EqualTo(LeagueGrade.Rookie));
        }

        [Test]
        public void Team_비주포지션교체를하드거부하지않는다()
        {
            Lineup lineup = CreateLineup(100);
            Player substitute = CreatePlayer(999, PlayerPosition.FirstBase);
            var plan = new PositionPlayerSubstitutionPlan(substitute, 0, 7, 2);
            Player starter = CreatePlayer(1000, PlayerPosition.StartingPitcher);

            Assert.DoesNotThrow(() =>
                new Team(1, "테스트", lineup, starter, null, 0, plan));
        }

        private static ActiveRosterEntry CreateEntry(int id, ActiveRosterRole role)
        {
            return new ActiveRosterEntry(
                $"CARD_{id:D3}",
                $"SEASON_{id:D3}",
                $"PERSON_{id:D3}",
                RegistrationType.Domestic,
                role);
        }

        private static string[] CreateRegularTeams(int count)
        {
            var result = new string[count];
            for (int index = 0; index < count; index++)
                result[index] = $"FRANCHISE_{index:D2}_2011";
            return result;
        }

        private static Lineup CreateLineup(int idBase)
        {
            var slots = new LineupSlot[9];
            for (int index = 0; index < slots.Length; index++)
            {
                PlayerPosition position = (PlayerPosition)(index + 1);
                slots[index] = new LineupSlot(CreatePlayer(idBase + index, position), position);
            }
            return new Lineup(slots);
        }

        private static Player CreatePlayer(int id, PlayerPosition position)
        {
            return new Player(
                id,
                $"선수 {id}",
                position,
                Handedness.Right,
                Handedness.Right,
                new BatterAttributes(50, 50, 50, 50, 50, 50),
                new PitcherAttributes(50, 50, 50, 50, 50, 50));
        }
    }
}
