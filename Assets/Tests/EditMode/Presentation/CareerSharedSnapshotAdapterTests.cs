using System;
using Baseball.Core.Teams;
using Baseball.Game.Career;
using Baseball.Presentation.SharedScreens;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Presentation
{
    /// <summary>
    /// Career 읽기 모델 Adapter가 표시 문자열과 분리된 원시 정렬 값을 보존하는지 검증한다.
    /// </summary>
    public sealed class CareerSharedSnapshotAdapterTests
    {
        [Test]
        public void LeagueStandings_현재구단과원시순위를공용표로보존한다()
        {
            LeagueHubView source = CreateLeagueHub();

            RecordTableModel table = CareerLeagueSnapshotAdapter.CreateStandingsTable(source);

            Assert.That(table.Rows[0].RowId, Is.EqualTo("team-10"));
            Assert.That(table.Rows[0].IsHighlighted, Is.True);
            Assert.That(table.Rows[0].FindCell("Pct").SortValue.Number, Is.EqualTo(.625d));
            Assert.That(table.SortedColumnId, Is.EqualTo("Rank"));
            Assert.That(table.SortDirection, Is.EqualTo(RecordSortDirection.Ascending));
        }

        private static LeagueHubView CreateLeagueHub()
        {
            var standings = new[]
            {
                new LeagueStandingView(
                    1, 10, "서울", new TeamColor(80, 110, 95), 8, 5, 3, 0, .625d, 0d,
                    TeamGameOutcome.Win, 2, Array.Empty<TeamGameOutcome>(), true, true,
                    LeagueStandingZone.Promotion),
                new LeagueStandingView(
                    2, 20, "부산", new TeamColor(90, 95, 110), 8, 4, 4, 0, .5d, 1d,
                    TeamGameOutcome.Loss, 1, Array.Empty<TeamGameOutcome>(), true, false,
                    LeagueStandingZone.Promotion)
            };
            return new LeagueHubView(
                2028,
                LeagueLevel.Major,
                SeasonPhase.RegularSeason,
                new DateTime(2028, 4, 15),
                8,
                126,
                4,
                10,
                "서울",
                100,
                standings,
                Array.Empty<LeagueBattingLeaderboardView>(),
                Array.Empty<LeaguePitchingLeaderboardView>(),
                Array.Empty<LeagueTeamMetricView>(),
                Array.Empty<LeagueScheduleGameView>(),
                Array.Empty<LeagueScheduleGameView>());
        }
    }
}
