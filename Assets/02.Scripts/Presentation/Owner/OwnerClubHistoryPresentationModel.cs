using System;
using System.Collections.Generic;
using System.Globalization;
using Baseball.Core.Historical;
using Baseball.Game.Career;
using Baseball.Game.Historical;
using Baseball.Presentation.SharedScreens;

namespace Baseball.Presentation.Owner
{
    /// <summary>시즌 전적과 기록의 출처를 연결하는 읽기 전용 표시 자료다.</summary>
    public sealed class OwnerClubHistorySeason
    {
        public OwnerClubHistorySeason(int number, LeagueGrade grade, bool completed, RecordTableRowModel row,
            OwnerClubHistoryTotals totals, IReadOnlyList<OwnerClubRecordCandidate> records, OwnerTeamPostseasonResult? postseason)
        { Number = number; Grade = grade; IsCompleted = completed; Row = row; Totals = totals; Records = records; Postseason = postseason; }
        public int Number { get; }
        public LeagueGrade Grade { get; }
        public bool IsCompleted { get; }
        public RecordTableRowModel Row { get; }
        public OwnerClubHistoryTotals Totals { get; }
        public IReadOnlyList<OwnerClubRecordCandidate> Records { get; }
        public OwnerTeamPostseasonResult? Postseason { get; }
        public string Label => Row.FindCell("Season").DisplayValue;
        public bool HasPennant => IsCompleted && NumberValue("Games") > 0 && NumberValue("Rank") == 1;
        public double NumberValue(string id) => Row.FindCell(id)?.SortValue.Number ?? 0;
    }

    /// <summary>시즌·등급·수상 필터를 같은 역사 Snapshot에 적용한다.</summary>
    public sealed class OwnerClubHistoryPresentationModel
    {
        private readonly IReadOnlyList<RecordTableColumnModel> _seasonColumns;
        public OwnerClubHistoryPresentationModel(string teamName, IReadOnlyList<OwnerClubHistorySeason> seasons,
            IReadOnlyList<RecordTableColumnModel> columns)
        {
            TeamName = string.IsNullOrWhiteSpace(teamName) ? "내 구단" : teamName;
            var sorted = new List<OwnerClubHistorySeason>(seasons);
            sorted.Sort((a, b) => b.Number.CompareTo(a.Number));
            Seasons = sorted.AsReadOnly(); _seasonColumns = columns;
        }
        public string TeamName { get; }
        public IReadOnlyList<OwnerClubHistorySeason> Seasons { get; }

        /// <summary>트로피룸은 성취와 달성 시즌에 집중하고 경기 지표는 기존 시즌 상세에서 제공한다.</summary>
        public RecordTableModel BuildHonorSeasons(LeagueGrade? grade, int honor)
        {
            var source = BuildSeasons(grade, honor < 0 ? -2 : honor);
            var columns = new List<RecordTableColumnModel>();
            foreach (var column in _seasonColumns)
                if (column.ColumnId == "Season" || column.ColumnId == "League" ||
                    column.ColumnId == "Pennant" || column.ColumnId == "Postseason") columns.Add(column);
            return new RecordTableModel(columns, source.Rows);
        }

        /// <summary>우승 종류를 눌렀을 때 해당 시즌만 보여준다.</summary>
        public RecordTableModel BuildSeasons(LeagueGrade? grade, int honor = -1)
        {
            var rows = new List<RecordTableRowModel>();
            foreach (var season in Seasons)
                if (Matches(season, grade) && (honor == -1 || (honor == -2
                    ? HasHonor(season, 0) || HasHonor(season, 1) || HasHonor(season, 2)
                    : HasHonor(season, honor)))) rows.Add(season.Row);
            return new RecordTableModel(_seasonColumns, rows);
        }

        /// <summary>진행 중 시즌을 포함한 통산 원본 합계를 표시한다.</summary>
        public RecordTableModel BuildTotals(LeagueGrade? grade)
        {
            var totals = new OwnerClubHistoryTotals();
            double games = 0, wins = 0, losses = 0, ties = 0, runs = 0, allowed = 0;
            int seasons = 0;
            foreach (var season in Seasons)
            {
                if (!Matches(season, grade)) continue;
                seasons++; totals.Add(season.Totals);
                games += season.NumberValue("Games"); wins += season.NumberValue("Wins");
                losses += season.NumberValue("Losses"); ties += season.NumberValue("Ties");
                runs += season.NumberValue("RS"); allowed += season.NumberValue("RA");
            }
            var rows = new List<RecordTableRowModel>();
            if (seasons == 0)
                return new RecordTableModel(new[] { Column("Metric", "통산 항목"), Column("Value", "기록") }, rows);
            AddTotal(rows, "시즌", seasons.ToString()); AddTotal(rows, "경기", games.ToString("N0"));
            AddTotal(rows, "전적", $"{wins:N0}승 {losses:N0}패 {ties:N0}무");
            AddTotal(rows, "승률", wins + losses == 0 ? "—" : (wins / (wins + losses)).ToString("0.000", CultureInfo.InvariantCulture));
            AddTotal(rows, "득점 / 실점", $"{runs:N0} / {allowed:N0}");
            AddTotal(rows, "타율", totals.AtBats == 0 ? "—" : totals.BattingAverage.ToString("0.000", CultureInfo.InvariantCulture));
            AddTotal(rows, "안타 / 타수", $"{totals.Hits:N0} / {totals.AtBats:N0}");
            AddTotal(rows, "홈런 / 도루", $"{totals.HomeRuns:N0} / {totals.StolenBases:N0}");
            AddTotal(rows, "평균자책점", totals.OutsRecorded == 0 ? "—" : totals.EarnedRunAverage.ToString("0.00", CultureInfo.InvariantCulture));
            AddTotal(rows, "투구 이닝", $"{totals.OutsRecorded / 3:N0}.{totals.OutsRecorded % 3}");
            AddTotal(rows, "탈삼진 / 자책점", $"{totals.Strikeouts:N0} / {totals.EarnedRuns:N0}");
            return new RecordTableModel(new[] { Column("Metric", "통산 항목"), Column("Value", "기록") }, rows);
        }

        /// <summary>공동 최고 기록을 모두 남기고 달성 시즌으로 이동할 수 있는 행을 만든다.</summary>
        public RecordTableModel BuildBest(LeagueGrade? grade, int? seasonNumber = null)
        {
            var rows = new List<RecordTableRowModel>();
            foreach (var metric in OwnerClubHistoryStatistics.RecordMetrics)
            {
                double? best = null;
                var leaders = new List<(OwnerClubHistorySeason season, OwnerClubRecordCandidate record)>();
                foreach (var season in Seasons)
                {
                    if (!Matches(season, grade) || (seasonNumber.HasValue && season.Number != seasonNumber.Value)) continue;
                    foreach (var record in season.Records)
                    {
                        if (record.Metric != metric) continue;
                        bool lower = metric == CareerRecordMetric.EarnedRunAverage;
                        bool better = !best.HasValue || (lower ? record.Value < best.Value - 0.0000001 : record.Value > best.Value + 0.0000001);
                        if (better) { best = record.Value; leaders.Clear(); }
                        if (best.HasValue && Math.Abs(record.Value - best.Value) < 0.0000001) leaders.Add((season, record));
                    }
                }
                foreach (var leader in leaders)
                    rows.Add(new RecordTableRowModel($"best:{leader.season.Number}:{metric}:{leader.record.PlayerId}", new[]
                    {
                        Cell("Metric", CareerSharedSnapshotFormatters.FormatMetricLabel(metric)),
                        Cell("Player", string.IsNullOrWhiteSpace(leader.record.PlayerName) ? "선수 이름 없음" : leader.record.PlayerName),
                        Cell("Value", CareerSharedSnapshotFormatters.FormatMetricValue(metric, leader.record.Value)),
                        Cell("Season", leader.season.Label),
                        Cell("League", OwnerLeagueDisplayNameFormatter.FormatFull(leader.season.Grade))
                    }));
            }
            return new RecordTableModel(new[] { Column("Metric", "기록 부문"), Column("Player", "달성 선수"),
                Column("Value", "최고 기록"), Column("Season", "달성 시즌", 2), Column("League", "리그") }, rows);
        }

        /// <summary>미확정 결과와 사전 생성 역사는 수상 횟수에서 제외한다.</summary>
        public int CountHonors(LeagueGrade? grade, int honor)
        { int count = 0; foreach (var season in Seasons) if (Matches(season, grade) && HasHonor(season, honor)) count++; return count; }

        public OwnerClubHistorySeason FindSeason(string rowId)
        {
            foreach (var season in Seasons)
                if (season.Row.RowId == rowId || rowId.StartsWith("best:" + season.Number + ":", StringComparison.Ordinal)) return season;
            return null;
        }
        private static bool Matches(OwnerClubHistorySeason season, LeagueGrade? grade) => !grade.HasValue || season.Grade == grade.Value;
        private static bool HasHonor(OwnerClubHistorySeason season, int honor) => honor == 0 ? season.HasPennant :
            honor == 1 ? season.Postseason == OwnerTeamPostseasonResult.Champion : season.Postseason == OwnerTeamPostseasonResult.RunnerUp;
        private static RecordTableColumnModel Column(string id, string label, float width = 1) =>
            new RecordTableColumnModel(id, label, RecordSortValueKind.Text, false, widthWeight: width);
        private static RecordTableCellModel Cell(string id, string value) => new RecordTableCellModel(id, value, RecordSortValue.FromText(value));
        private static void AddTotal(List<RecordTableRowModel> rows, string label, string value) =>
            rows.Add(new RecordTableRowModel(label, new[] { Cell("Metric", label), Cell("Value", value) }));
    }
}
