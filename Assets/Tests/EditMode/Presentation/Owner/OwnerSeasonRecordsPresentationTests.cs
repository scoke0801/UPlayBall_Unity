using System;
using Baseball.Game.Career;
using Baseball.Game.Historical;
using Baseball.Presentation.Owner;
using Baseball.Presentation.SharedScreens;
using Baseball.Presentation.SharedUI;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Presentation.Owner
{
    /// <summary>구단주 선수 기록 화면이 Game 레이어 확정 결과만 표시하고 리그 탭에서 도달 가능한지 검증한다.</summary>
    public sealed class OwnerSeasonRecordsPresentationTests
    {
        [Test]
        public void Model_네부문표를한번에만들고내구단선수를강조한다()
        {
            OwnerSeasonRecordsPresentationModel model = CreateModel(hasRecord: true);

            Assert.That(model.Categories.Count, Is.EqualTo(4));
            Assert.That(model.SeasonLabel, Is.EqualTo("2028 시즌 1년차"));

            OwnerSeasonRecordsCategoryModel batting = model.Categories[0];
            Assert.That(batting.ContentState.Kind, Is.EqualTo(UiContentStateKind.Ready));
            Assert.That(batting.Table.Rows.Count, Is.EqualTo(2));
            Assert.That(batting.Table.Columns[0].ColumnId, Is.EqualTo("Rank"));
            Assert.That(batting.Table.Columns[1].ColumnId, Is.EqualTo("Player"));
            Assert.That(batting.Table.Columns[2].ColumnId, Is.EqualTo("Team"));
            Assert.That(batting.QualificationText, Is.EqualTo("규정 충족 2명"));

            RecordTableRowModel highlighted = FindRow(batting.Table, "player-2");
            Assert.That(highlighted.IsHighlighted, Is.True);
            Assert.That(highlighted.HighlightReason, Is.EqualTo("내 구단 선수"));
            Assert.That(FindRow(batting.Table, "player-1").IsHighlighted, Is.False);
            Assert.That(batting.FocusedRowId, Is.EqualTo("player-2"));
        }

        [Test]
        public void Model_기록이없으면빈상태문구를만든다()
        {
            OwnerSeasonRecordsPresentationModel model = CreateModel(hasRecord: false);

            OwnerSeasonRecordsCategoryModel batting = model.Categories[0];
            Assert.That(batting.ContentState.Kind, Is.EqualTo(UiContentStateKind.Empty));
            Assert.That(batting.ContentState.Message, Does.Contain("경기를 진행하면"));
        }

        [Test]
        public void Profile_리그탭에서선수기록과일정역사기록에도달할수있다()
        {
            GameModeUiProfile profile = OwnerModeUiProfileFactory.Create();
            NavigationEntry league = profile.Navigation.FindEntry(OwnerNavigationRoutes.League);

            AssertReachableLeagueTab(profile, league,
                OwnerSharedInformationWorkspaceCoordinator.SeasonRecordsRouteId, "선수 기록");
            AssertReachableLeagueTab(profile, league,
                OwnerSharedInformationWorkspaceCoordinator.ScheduleRouteId, "일정");
            AssertReachableLeagueTab(profile, league,
                OwnerSharedInformationWorkspaceCoordinator.RecordsRouteId, "역사 기록");
        }

        private static void AssertReachableLeagueTab(
            GameModeUiProfile profile,
            NavigationEntry league,
            string routeId,
            string displayName)
        {
            NavigationEntry entry = profile.Navigation.FindEntry(routeId);
            Assert.That(entry, Is.Not.Null, routeId);
            Assert.That(entry.DisplayName, Is.EqualTo(displayName));
            Assert.That(entry.IsEnabled, Is.True, routeId);
            Assert.That(entry.IsVisible(profile.Capabilities), Is.True, routeId);

            bool isLeagueChild = false;
            for (int index = 0; index < league.Children.Count; index++)
                if (string.Equals(league.Children[index].RouteId, routeId, StringComparison.Ordinal))
                    isLeagueChild = true;
            Assert.That(isLeagueChild, Is.True, routeId + "가 리그 탭에 없다.");
        }

        private static RecordTableRowModel FindRow(RecordTableModel table, string rowId)
        {
            for (int index = 0; index < table.Rows.Count; index++)
                if (string.Equals(table.Rows[index].RowId, rowId, StringComparison.Ordinal))
                    return table.Rows[index];
            throw new InvalidOperationException($"{rowId} 행이 없습니다.");
        }

        private static OwnerSeasonRecordsPresentationModel CreateModel(bool hasRecord)
        {
            CareerRecordMetric[] columns = LeagueLeaderboardService.GetBasicColumns(CareerRecordCategory.Batting);
            CareerRecordLeaderboardRow[] leaderboard = hasRecord
                ? new[]
                {
                    CreateRow(1, 1, "리그 1위", 1, "상대 구단", isMyPlayer: false, columns, 0.352d),
                    CreateRow(2, 2, "내 구단 4번", 2, "내 구단", isMyPlayer: true, columns, 0.318d)
                }
                : Array.Empty<CareerRecordLeaderboardRow>();

            var categories = new OwnerSeasonRecordsCategoryView[4];
            var names = new[] { "타격", "투구", "수비", "주루" };
            var kinds = new[]
            {
                CareerRecordCategory.Batting,
                CareerRecordCategory.Pitching,
                CareerRecordCategory.Fielding,
                CareerRecordCategory.Baserunning
            };
            for (int index = 0; index < categories.Length; index++)
            {
                CareerRecordLeaderboardRow[] rows = index == 0
                    ? leaderboard
                    : Array.Empty<CareerRecordLeaderboardRow>();
                categories[index] = new OwnerSeasonRecordsCategoryView(
                    kinds[index],
                    names[index],
                    LeagueLeaderboardService.GetBasicColumns(kinds[index]),
                    rows,
                    rows.Length,
                    hasRecord
                        ? "규정 충족 " + rows.Length + "명"
                        : "아직 진행한 경기가 없습니다.");
            }

            return new OwnerSeasonRecordsPresentationModel(
                new OwnerSeasonRecordsView("2028 시즌 1년차", "Rookie", 2, hasRecord, categories));
        }

        private static CareerRecordLeaderboardRow CreateRow(
            int rank,
            int playerId,
            string playerName,
            int teamId,
            string teamName,
            bool isMyPlayer,
            CareerRecordMetric[] columns,
            double primaryValue)
        {
            var metrics = new CareerRecordMetricValue[columns.Length];
            for (int index = 0; index < columns.Length; index++)
                metrics[index] = new CareerRecordMetricValue(columns[index], index == 0 ? primaryValue : index);
            return new CareerRecordLeaderboardRow(
                rank, playerId, playerName, teamId, teamName, isMyPlayer, metrics);
        }
    }
}
