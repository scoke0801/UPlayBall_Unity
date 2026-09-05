using System;
using System.Globalization;
using Baseball.Game.Career;

namespace Baseball.Presentation.SharedScreens
{
    /// <summary>
    /// Career 기록 리더보드를 공용 Stable Sort 표 Snapshot으로 변환한다.
    /// </summary>
    public static class CareerRecordsSnapshotAdapter
    {
        /// <summary>
        /// 현재 부문과 규정 자격 정보가 포함된 공용 기록 화면 Snapshot을 만든다.
        /// </summary>
        public static RecordsScreenSnapshot Create(CareerRecordsView view)
        {
            if (view == null)
                throw new ArgumentNullException(nameof(view));

            RecordTableModel table = CreateLeaderboard(view);
            string qualification = view.HasScopeData
                ? $"규정 충족 {view.QualifiedPlayerCount.ToString(CultureInfo.InvariantCulture)}명"
                : "표시할 경기 기록이 없습니다.";
            string focusedRowId = FindFocusedRowId(view);
            return new RecordsScreenSnapshot(
                view.SeasonYear.ToString(CultureInfo.InvariantCulture),
                CareerSharedSnapshotFormatters.FormatLeague(view.LeagueLevel),
                view.Scope == CompetitionScope.Postseason ? "포스트시즌" : "정규시즌",
                CareerSharedSnapshotFormatters.FormatRecordCategory(view.Category),
                table,
                qualification,
                focusedRowId);
        }

        /// <summary>
        /// Career 리더보드의 표시 값과 원시 Metric 값을 분리한 공용 기록표를 만든다.
        /// </summary>
        public static RecordTableModel CreateLeaderboard(CareerRecordsView view)
        {
            if (view == null)
                throw new ArgumentNullException(nameof(view));

            CareerRecordMetric[] metrics = view.LeaderboardColumns ?? Array.Empty<CareerRecordMetric>();
            CareerRecordLeaderboardRow[] sourceRows = view.Leaderboard ?? Array.Empty<CareerRecordLeaderboardRow>();
            var columns = new RecordTableColumnModel[metrics.Length + 3];
            columns[0] = new RecordTableColumnModel(
                "Rank", "순위", RecordSortValueKind.Number, true,
                RecordSortDirection.Ascending, 0.65f);
            columns[1] = new RecordTableColumnModel(
                "Player", "선수", RecordSortValueKind.Text, true,
                RecordSortDirection.Ascending, 1.8f, RecordCellAlignment.Left);
            columns[2] = new RecordTableColumnModel(
                "Team", "구단", RecordSortValueKind.Text, true,
                RecordSortDirection.Ascending, 1.3f, RecordCellAlignment.Left);
            for (int i = 0; i < metrics.Length; i++)
            {
                columns[i + 3] = new RecordTableColumnModel(
                    MetricColumnId(metrics[i]),
                    CareerSharedSnapshotFormatters.FormatMetricLabel(metrics[i]),
                    RecordSortValueKind.Number,
                    true,
                    IsLowerBetter(metrics[i]) ? RecordSortDirection.Ascending : RecordSortDirection.Descending,
                    0.9f);
            }

            var rows = new RecordTableRowModel[sourceRows.Length];
            for (int rowIndex = 0; rowIndex < sourceRows.Length; rowIndex++)
            {
                CareerRecordLeaderboardRow source = sourceRows[rowIndex];
                var cells = new RecordTableCellModel[columns.Length];
                cells[0] = NumberCell("Rank", source.Rank.ToString(CultureInfo.InvariantCulture), source.Rank);
                cells[1] = TextCell("Player", source.PlayerName);
                cells[2] = TextCell("Team", source.TeamName);
                for (int metricIndex = 0; metricIndex < metrics.Length; metricIndex++)
                {
                    CareerRecordMetricValue? metric = FindMetric(source.Metrics, metrics[metricIndex]);
                    cells[metricIndex + 3] = metric.HasValue
                        ? NumberCell(
                            MetricColumnId(metrics[metricIndex]),
                            CareerSharedSnapshotFormatters.FormatMetricValue(metrics[metricIndex], metric.Value.Value),
                            metric.Value.Value)
                        : EmptyNumberCell(MetricColumnId(metrics[metricIndex]));
                }

                rows[rowIndex] = new RecordTableRowModel(
                    $"player-{source.PlayerId.ToString(CultureInfo.InvariantCulture)}",
                    cells,
                    source.IsMyPlayer,
                    source.IsMyPlayer ? "내 선수" : string.Empty);
            }

            var table = new RecordTableModel(columns, rows);
            string primaryColumn = MetricColumnId(view.PrimaryMetric);
            if (table.Columns.Count > 3 && ContainsMetric(metrics, view.PrimaryMetric))
            {
                return table.SortBy(
                    primaryColumn,
                    IsLowerBetter(view.PrimaryMetric)
                        ? RecordSortDirection.Ascending
                        : RecordSortDirection.Descending);
            }
            return table;
        }

        /// <summary>
        /// Career 시즌 이력을 Stable Year ID와 원시 Metric 정렬 값을 가진 공용 기록표로 변환한다.
        /// </summary>
        public static RecordTableModel CreateSeasonTable(
            CareerRecordsView view,
            CareerRecordSeasonRow[] sourceRows,
            bool showPrimaryMetricRank)
        {
            if (view == null)
                throw new ArgumentNullException(nameof(view));

            CareerRecordMetric[] metrics = view.LeaderboardColumns ?? Array.Empty<CareerRecordMetric>();
            CareerRecordSeasonRow[] safeRows = sourceRows ?? Array.Empty<CareerRecordSeasonRow>();
            string identityColumnId = showPrimaryMetricRank ? "Rank" : "Year";
            var columns = new RecordTableColumnModel[metrics.Length + 3];
            columns[0] = new RecordTableColumnModel(
                identityColumnId,
                showPrimaryMetricRank ? "순위" : "연도",
                RecordSortValueKind.Number,
                true,
                RecordSortDirection.Ascending,
                0.65f);
            columns[1] = new RecordTableColumnModel(
                "Team", "소속 구단", RecordSortValueKind.Text, true,
                RecordSortDirection.Ascending, 1.8f, RecordCellAlignment.Left);
            columns[2] = new RecordTableColumnModel(
                "League", "리그", RecordSortValueKind.Text, true,
                RecordSortDirection.Ascending, 1.3f, RecordCellAlignment.Left);
            for (int i = 0; i < metrics.Length; i++)
            {
                columns[i + 3] = new RecordTableColumnModel(
                    MetricColumnId(metrics[i]),
                    CareerSharedSnapshotFormatters.FormatMetricLabel(metrics[i]),
                    RecordSortValueKind.Number,
                    true,
                    IsLowerBetter(metrics[i]) ? RecordSortDirection.Ascending : RecordSortDirection.Descending,
                    0.9f);
            }

            var rows = new RecordTableRowModel[safeRows.Length];
            for (int rowIndex = 0; rowIndex < safeRows.Length; rowIndex++)
            {
                CareerRecordSeasonRow source = safeRows[rowIndex];
                var cells = new RecordTableCellModel[columns.Length];
                int identityValue = showPrimaryMetricRank ? rowIndex + 1 : source.Year;
                cells[0] = NumberCell(
                    identityColumnId,
                    identityValue.ToString(CultureInfo.InvariantCulture),
                    identityValue);
                cells[1] = TextCell(
                    "Team",
                    source.IsCurrent ? source.TeamName + "  (진행 중)" : source.TeamName);
                cells[2] = TextCell("League", CareerSharedSnapshotFormatters.FormatLeague(source.LeagueLevel));
                for (int metricIndex = 0; metricIndex < metrics.Length; metricIndex++)
                {
                    CareerRecordMetricValue? metric = FindMetric(source.Metrics, metrics[metricIndex]);
                    cells[metricIndex + 3] = metric.HasValue
                        ? NumberCell(
                            MetricColumnId(metrics[metricIndex]),
                            CareerSharedSnapshotFormatters.FormatMetricValue(metrics[metricIndex], metric.Value.Value),
                            metric.Value.Value)
                        : EmptyNumberCell(MetricColumnId(metrics[metricIndex]));
                }

                rows[rowIndex] = new RecordTableRowModel(
                    $"season-{source.Year.ToString(CultureInfo.InvariantCulture)}",
                    cells,
                    source.IsCurrent,
                    source.IsCurrent ? "현재 시즌" : string.Empty);
            }

            if (showPrimaryMetricRank && ContainsMetric(metrics, view.PrimaryMetric))
            {
                return new RecordTableModel(
                    columns,
                    rows,
                    MetricColumnId(view.PrimaryMetric),
                    IsLowerBetter(view.PrimaryMetric)
                        ? RecordSortDirection.Ascending
                        : RecordSortDirection.Descending);
            }

            if (showPrimaryMetricRank)
                return new RecordTableModel(columns, rows).SortBy("Rank", RecordSortDirection.Ascending);

            return new RecordTableModel(columns, rows).SortBy("Year", RecordSortDirection.Descending);
        }

        private static CareerRecordMetricValue? FindMetric(
            CareerRecordMetricValue[] metrics,
            CareerRecordMetric target)
        {
            if (metrics == null)
                return null;
            for (int i = 0; i < metrics.Length; i++)
            {
                if (metrics[i].Metric == target)
                    return metrics[i];
            }
            return null;
        }

        private static bool ContainsMetric(CareerRecordMetric[] metrics, CareerRecordMetric target)
        {
            for (int i = 0; i < metrics.Length; i++)
            {
                if (metrics[i] == target)
                    return true;
            }
            return false;
        }

        private static string FindFocusedRowId(CareerRecordsView view)
        {
            CareerRecordLeaderboardRow[] rows = view.Leaderboard ?? Array.Empty<CareerRecordLeaderboardRow>();
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i].IsMyPlayer)
                    return $"player-{rows[i].PlayerId.ToString(CultureInfo.InvariantCulture)}";
            }
            return string.Empty;
        }

        private static bool IsLowerBetter(CareerRecordMetric metric)
        {
            return metric == CareerRecordMetric.EarnedRunAverage ||
                metric == CareerRecordMetric.WalksHitsPerInningPitched ||
                metric == CareerRecordMetric.Errors ||
                metric == CareerRecordMetric.CaughtStealing ||
                metric == CareerRecordMetric.HomeRunsPerNineInnings;
        }

        private static string MetricColumnId(CareerRecordMetric metric) => $"Metric.{metric}";

        private static RecordTableCellModel TextCell(string id, string value)
        {
            string safe = value ?? string.Empty;
            return new RecordTableCellModel(id, safe, RecordSortValue.FromText(safe));
        }

        private static RecordTableCellModel NumberCell(string id, string display, double value)
        {
            return new RecordTableCellModel(id, display, RecordSortValue.FromNumber(value));
        }

        private static RecordTableCellModel EmptyNumberCell(string id)
        {
            return new RecordTableCellModel(id, "-", RecordSortValue.Empty());
        }
    }
}
