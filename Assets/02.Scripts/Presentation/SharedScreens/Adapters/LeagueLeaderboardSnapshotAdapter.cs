using System;
using System.Globalization;
using Baseball.Game.Career;

namespace Baseball.Presentation.SharedScreens
{
    /// <summary>
    /// 선수 커리어 모드와 구단주 모드가 확정한 같은 리더보드 행을 공용 Stable Sort 기록표로 변환한다.
    /// </summary>
    public static class LeagueLeaderboardSnapshotAdapter
    {
        /// <summary>표시 값과 원시 정렬 값을 분리한 리더보드 기록표를 만들고 대표 지표로 정렬한다.</summary>
        public static RecordTableModel CreateLeaderboardTable(
            CareerRecordMetric[] metrics,
            CareerRecordLeaderboardRow[] rows,
            CareerRecordMetric primaryMetric,
            string highlightReason)
        {
            CareerRecordMetric[] safeMetrics = metrics ?? Array.Empty<CareerRecordMetric>();
            CareerRecordLeaderboardRow[] safeRows = rows ?? Array.Empty<CareerRecordLeaderboardRow>();
            var columns = new RecordTableColumnModel[safeMetrics.Length + 3];
            columns[0] = new RecordTableColumnModel(
                "Rank", "순위", RecordSortValueKind.Number, true,
                RecordSortDirection.Ascending, 0.65f);
            columns[1] = new RecordTableColumnModel(
                "Player", "선수", RecordSortValueKind.Text, true,
                RecordSortDirection.Ascending, 1.8f, RecordCellAlignment.Left);
            columns[2] = new RecordTableColumnModel(
                "Team", "구단", RecordSortValueKind.Text, true,
                RecordSortDirection.Ascending, 1.3f, RecordCellAlignment.Left);
            for (int i = 0; i < safeMetrics.Length; i++)
            {
                columns[i + 3] = new RecordTableColumnModel(
                    MetricColumnId(safeMetrics[i]),
                    CareerSharedSnapshotFormatters.FormatMetricLabel(safeMetrics[i]),
                    RecordSortValueKind.Number,
                    true,
                    PrefersAscendingSort(safeMetrics[i])
                        ? RecordSortDirection.Ascending
                        : RecordSortDirection.Descending,
                    0.9f);
            }

            var tableRows = new RecordTableRowModel[safeRows.Length];
            for (int rowIndex = 0; rowIndex < safeRows.Length; rowIndex++)
            {
                CareerRecordLeaderboardRow source = safeRows[rowIndex];
                var cells = new RecordTableCellModel[columns.Length];
                cells[0] = NumberCell("Rank", source.Rank.ToString(CultureInfo.InvariantCulture), source.Rank);
                cells[1] = TextCell("Player", source.PlayerName);
                cells[2] = TextCell("Team", source.TeamName);
                for (int metricIndex = 0; metricIndex < safeMetrics.Length; metricIndex++)
                {
                    CareerRecordMetricValue? metric = FindMetric(source.Metrics, safeMetrics[metricIndex]);
                    cells[metricIndex + 3] = metric.HasValue
                        ? NumberCell(
                            MetricColumnId(safeMetrics[metricIndex]),
                            CareerSharedSnapshotFormatters.FormatMetricValue(
                                safeMetrics[metricIndex],
                                metric.Value.Value),
                            metric.Value.Value)
                        : EmptyNumberCell(MetricColumnId(safeMetrics[metricIndex]));
                }

                tableRows[rowIndex] = new RecordTableRowModel(
                    CreateRowId(source.PlayerId),
                    cells,
                    source.IsMyPlayer,
                    source.IsMyPlayer ? highlightReason ?? string.Empty : string.Empty);
            }

            var table = new RecordTableModel(columns, tableRows);
            if (table.Columns.Count > 3 && ContainsMetric(safeMetrics, primaryMetric))
            {
                return table.SortBy(
                    MetricColumnId(primaryMetric),
                    LeagueLeaderboardService.IsLowerBetter(primaryMetric)
                        ? RecordSortDirection.Ascending
                        : RecordSortDirection.Descending);
            }
            return table;
        }

        /// <summary>강조 대상 행이 있으면 그 RowId를 돌려 화면이 해당 줄로 이동할 수 있게 한다.</summary>
        public static string FindHighlightedRowId(CareerRecordLeaderboardRow[] rows)
        {
            CareerRecordLeaderboardRow[] safeRows = rows ?? Array.Empty<CareerRecordLeaderboardRow>();
            for (int i = 0; i < safeRows.Length; i++)
            {
                if (safeRows[i].IsMyPlayer)
                    return CreateRowId(safeRows[i].PlayerId);
            }
            return string.Empty;
        }

        internal static string CreateRowId(int playerId) =>
            "player-" + playerId.ToString(CultureInfo.InvariantCulture);

        internal static string MetricColumnId(CareerRecordMetric metric) => $"Metric.{metric}";

        /// <summary>
        /// 사람이 열 머리를 눌렀을 때의 기본 정렬 방향이며, 순위 판정용
        /// <see cref="LeagueLeaderboardService.IsLowerBetter"/>와는 다른 질문에 답한다.
        /// 도루자는 적을수록 좋아 오름차순이 기본이지만, 그 자체로 순위를 매기는 지표는 아니다.
        /// </summary>
        internal static bool PrefersAscendingSort(CareerRecordMetric metric)
        {
            return metric == CareerRecordMetric.CaughtStealing ||
                LeagueLeaderboardService.IsLowerBetter(metric);
        }

        internal static CareerRecordMetricValue? FindMetric(
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

        internal static bool ContainsMetric(CareerRecordMetric[] metrics, CareerRecordMetric target)
        {
            for (int i = 0; i < metrics.Length; i++)
            {
                if (metrics[i] == target)
                    return true;
            }
            return false;
        }

        internal static RecordTableCellModel TextCell(string id, string value)
        {
            string safe = value ?? string.Empty;
            return new RecordTableCellModel(id, safe, RecordSortValue.FromText(safe));
        }

        internal static RecordTableCellModel NumberCell(string id, string display, double value)
        {
            return new RecordTableCellModel(id, display, RecordSortValue.FromNumber(value));
        }

        internal static RecordTableCellModel EmptyNumberCell(string id)
        {
            return new RecordTableCellModel(id, "-", RecordSortValue.Empty());
        }
    }
}
