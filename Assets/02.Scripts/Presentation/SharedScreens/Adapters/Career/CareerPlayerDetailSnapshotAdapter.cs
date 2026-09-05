using System;
using System.Globalization;
using Baseball.Core.Players;
using Baseball.Game.Career;

namespace Baseball.Presentation.SharedScreens
{
    /// <summary>
    /// Career 선수 상세를 소유 카드나 성장판 Command가 없는 공용 선수 Snapshot으로 변환한다.
    /// </summary>
    public static class CareerPlayerDetailSnapshotAdapter
    {
        /// <summary>
        /// 선수 기본 정보, 능력치, 시즌 기록과 통산 기록을 공용 Snapshot으로 만든다.
        /// </summary>
        public static PlayerDetailSnapshot Create(PlayerProfileView view)
        {
            if (view == null)
                throw new ArgumentNullException(nameof(view));

            return new PlayerDetailSnapshot(
                CareerSharedSnapshotFormatters.FormatId(view.PlayerId),
                view.PlayerName,
                string.Empty,
                view.TeamName,
                view.SeasonYear.ToString(CultureInfo.InvariantCulture),
                CareerSharedSnapshotFormatters.FormatPosition(view.Position),
                string.Empty,
                CareerSharedSnapshotFormatters.FormatTeamColor(view.TeamColor),
                CreateSummary(view),
                CreateAbilityTable(view),
                CreateSeasonRecordTable(view),
                CreateCareerRecordTable(view));
        }

        private static DetailValueModel[] CreateSummary(PlayerProfileView view)
        {
            return new[]
            {
                Detail("Nationality", "국적", view.Nationality),
                Detail("Age", "나이", $"{view.Age.ToString(CultureInfo.InvariantCulture)}세"),
                Detail("PlayerType", "유형", CareerSharedSnapshotFormatters.FormatPlayerType(view.PlayerType)),
                Detail(
                    "Handedness",
                    "투타",
                    $"{CareerSharedSnapshotFormatters.FormatHandedness(view.ThrowingHand)}투 " +
                    $"{CareerSharedSnapshotFormatters.FormatHandedness(view.BattingHand)}타"),
                Detail("League", "리그", CareerSharedSnapshotFormatters.FormatLeague(view.LeagueLevel)),
                Detail("Overall", "종합", view.Overall.ToString(CultureInfo.InvariantCulture)),
                Detail("Condition", "컨디션", view.Condition.ToString(CultureInfo.InvariantCulture)),
                Detail("Fatigue", "피로", view.Fatigue.ToString(CultureInfo.InvariantCulture)),
                Detail("Durability", "내구", view.Durability.ToString(CultureInfo.InvariantCulture)),
                Detail("CareerPhase", "커리어 단계", CareerSharedSnapshotFormatters.FormatCareerPhase(view.CareerPhase))
            };
        }

        private static RecordTableModel CreateAbilityTable(PlayerProfileView view)
        {
            PlayerProfileAbilityView[] source = view.Abilities ?? Array.Empty<PlayerProfileAbilityView>();
            var columns = new[]
            {
                Column("Ability", "능력", RecordSortValueKind.Text, RecordSortDirection.Ascending, 1.4f, RecordCellAlignment.Left),
                Column("Stable", "현재", RecordSortValueKind.Number, RecordSortDirection.Descending),
                Column("Base", "기본", RecordSortValueKind.Number, RecordSortDirection.Descending),
                Column("Bonus", "보너스", RecordSortValueKind.Number, RecordSortDirection.Descending),
                Column("Potential", "잠재", RecordSortValueKind.Number, RecordSortDirection.Descending),
                Column("GrowthRoom", "성장 여지", RecordSortValueKind.Number, RecordSortDirection.Descending)
            };
            var rows = new RecordTableRowModel[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                PlayerProfileAbilityView ability = source[i];
                string label = CareerSharedSnapshotFormatters.FormatAbility(ability.Ability);
                rows[i] = new RecordTableRowModel(
                    $"ability-{ability.Ability}",
                    new[]
                    {
                        TextCell("Ability", label),
                        NumberCell("Stable", ability.StableValue),
                        NumberCell("Base", ability.BaseValue),
                        NumberCell("Bonus", ability.BoardBonus),
                        NumberCell("Potential", ability.Potential),
                        NumberCell("GrowthRoom", ability.GrowthRoom)
                    });
            }
            return new RecordTableModel(columns, rows);
        }

        private static RecordTableModel CreateSeasonRecordTable(PlayerProfileView view)
        {
            return view.PlayerType == PlayerType.Pitcher
                ? CreatePitchingSeasonTable(view.SeasonStatistics)
                : CreateBattingSeasonTable(view.SeasonStatistics);
        }

        private static RecordTableModel CreateBattingSeasonTable(PlayerProfileStatisticsView stats)
        {
            var columns = new[]
            {
                NumericColumn("Games", "G"), NumericColumn("AtBats", "AB"), NumericColumn("Hits", "H"),
                NumericColumn("HomeRuns", "HR"), NumericColumn("Rbi", "RBI"),
                NumericColumn("Average", "AVG"), NumericColumn("Ops", "OPS")
            };
            var row = new RecordTableRowModel(
                "current-season",
                new[]
                {
                    NumberCell("Games", stats.GamesPlayed), NumberCell("AtBats", stats.AtBats),
                    NumberCell("Hits", stats.Hits), NumberCell("HomeRuns", stats.HomeRuns),
                    NumberCell("Rbi", stats.RunsBattedIn),
                    NumberCell("Average", CareerSharedSnapshotFormatters.FormatRate(stats.BattingAverage), stats.BattingAverage),
                    NumberCell("Ops", CareerSharedSnapshotFormatters.FormatRate(stats.OnBasePlusSlugging), stats.OnBasePlusSlugging)
                });
            return new RecordTableModel(columns, new[] { row });
        }

        private static RecordTableModel CreatePitchingSeasonTable(PlayerProfileStatisticsView stats)
        {
            var columns = new[]
            {
                NumericColumn("Games", "G"), NumericColumn("Starts", "GS"), NumericColumn("Wins", "W"),
                NumericColumn("Losses", "L"), NumericColumn("Saves", "SV"), NumericColumn("Innings", "IP"),
                NumericColumn("Strikeouts", "SO"), NumericColumn("Era", "ERA", RecordSortDirection.Ascending),
                NumericColumn("Whip", "WHIP", RecordSortDirection.Ascending)
            };
            var row = new RecordTableRowModel(
                "current-season",
                new[]
                {
                    NumberCell("Games", stats.PitchingAppearances), NumberCell("Starts", stats.PitchingStarts),
                    NumberCell("Wins", stats.Wins), NumberCell("Losses", stats.Losses), NumberCell("Saves", stats.Saves),
                    NumberCell("Innings", CareerSharedSnapshotFormatters.FormatInnings(stats.OutsRecorded), stats.OutsRecorded),
                    NumberCell("Strikeouts", stats.PitchingStrikeouts),
                    NumberCell("Era", CareerSharedSnapshotFormatters.FormatDecimal(stats.EarnedRunAverage), stats.EarnedRunAverage),
                    NumberCell("Whip", CareerSharedSnapshotFormatters.FormatDecimal(stats.WalksHitsPerInningPitched), stats.WalksHitsPerInningPitched)
                });
            return new RecordTableModel(columns, new[] { row });
        }

        private static RecordTableModel CreateCareerRecordTable(PlayerProfileView view)
        {
            CareerRecordMetricValue[] source = view.CareerTotals ?? Array.Empty<CareerRecordMetricValue>();
            var columns = new[]
            {
                Column("Metric", "기록", RecordSortValueKind.Text, RecordSortDirection.Ascending, 1.8f, RecordCellAlignment.Left),
                Column("Value", "통산", RecordSortValueKind.Number, RecordSortDirection.Descending, 1f, RecordCellAlignment.Right)
            };
            var rows = new RecordTableRowModel[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                CareerRecordMetricValue metric = source[i];
                rows[i] = new RecordTableRowModel(
                    $"metric-{metric.Metric}",
                    new[]
                    {
                        TextCell("Metric", CareerSharedSnapshotFormatters.FormatMetricLabel(metric.Metric)),
                        NumberCell(
                            "Value",
                            CareerSharedSnapshotFormatters.FormatMetricValue(metric.Metric, metric.Value),
                            metric.Value)
                    });
            }
            return new RecordTableModel(columns, rows);
        }

        private static DetailValueModel Detail(string id, string label, string value) =>
            new DetailValueModel(id, label, value ?? string.Empty);

        private static RecordTableColumnModel NumericColumn(
            string id,
            string label,
            RecordSortDirection direction = RecordSortDirection.Descending) =>
            Column(id, label, RecordSortValueKind.Number, direction);

        private static RecordTableColumnModel Column(
            string id,
            string label,
            RecordSortValueKind kind,
            RecordSortDirection direction,
            float width = 1f,
            RecordCellAlignment alignment = RecordCellAlignment.Center) =>
            new RecordTableColumnModel(id, label, kind, true, direction, width, alignment);

        private static RecordTableCellModel TextCell(string id, string value)
        {
            string safe = value ?? string.Empty;
            return new RecordTableCellModel(id, safe, RecordSortValue.FromText(safe));
        }

        private static RecordTableCellModel NumberCell(string id, int value) =>
            NumberCell(id, value.ToString(CultureInfo.InvariantCulture), value);

        private static RecordTableCellModel NumberCell(string id, string display, double value) =>
            new RecordTableCellModel(id, display, RecordSortValue.FromNumber(value));
    }
}
