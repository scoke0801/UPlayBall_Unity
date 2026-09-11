using System;
using System.Collections.Generic;
using System.Globalization;

namespace Baseball.Presentation.SharedScreens
{
    /// <summary>
    /// 모드 중립 일정 Snapshot을 월별 경기 목록용 공용 기록표로 투영한다.
    /// </summary>
    public static class ScheduleRecordTableBuilder
    {
        /// <summary>
        /// 지정 월의 포커스 구단 경기만 날짜순 표로 만들며 Adapter가 확정한 결과를 재계산하지 않는다.
        /// </summary>
        public static RecordTableModel CreateFocusedMonth(
            ScheduleScreenSnapshot snapshot,
            int year,
            int month)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));
            if (!snapshot.HasCalendarDate)
                throw new InvalidOperationException("달력 날짜가 없는 일정은 월별 표로 만들 수 없습니다.");
            if (month < 1 || month > 12)
                throw new ArgumentOutOfRangeException(nameof(month));

            var rows = new List<RecordTableRowModel>();
            for (int i = 0; i < snapshot.Games.Count; i++)
            {
                ScheduleGameSnapshot game = snapshot.Games[i];
                if (game.Date.Year != year || game.Date.Month != month || game.FocusSide == ScheduleFocusSide.None)
                    continue;
                rows.Add(CreateFocusedRow(game));
            }

            var columns = new[]
            {
                new RecordTableColumnModel(
                    "Date", "날짜", RecordSortValueKind.Number, true,
                    RecordSortDirection.Ascending, 0.9f),
                new RecordTableColumnModel(
                    "Venue", "구장", RecordSortValueKind.Text, true,
                    RecordSortDirection.Ascending, 0.8f),
                new RecordTableColumnModel(
                    "Opponent", "상대", RecordSortValueKind.Text, true,
                    RecordSortDirection.Ascending, 2.2f, RecordCellAlignment.Left),
                new RecordTableColumnModel(
                    "Result", "결과", RecordSortValueKind.Text, true,
                    RecordSortDirection.Ascending, 1.1f)
            };
            return new RecordTableModel(columns, rows).SortBy("Date", RecordSortDirection.Ascending);
        }

        /// <summary>
        /// 달력 날짜 유무와 관계없이 포커스 구단의 전체 일정을 확정 Period 순서로 만든다.
        /// </summary>
        public static RecordTableModel CreateFocusedSchedule(ScheduleScreenSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            var rows = new List<RecordTableRowModel>();
            for (int i = 0; i < snapshot.Games.Count; i++)
            {
                ScheduleGameSnapshot game = snapshot.Games[i];
                if (game.FocusSide == ScheduleFocusSide.None)
                    continue;
                rows.Add(CreateFocusedRow(game));
            }

            var columns = new[]
            {
                new RecordTableColumnModel(
                    "Date", "일정", RecordSortValueKind.Number, true,
                    RecordSortDirection.Ascending, 0.9f),
                new RecordTableColumnModel(
                    "Venue", "구장", RecordSortValueKind.Text, true,
                    RecordSortDirection.Ascending, 0.8f),
                new RecordTableColumnModel(
                    "Opponent", "상대", RecordSortValueKind.Text, true,
                    RecordSortDirection.Ascending, 2.2f, RecordCellAlignment.Left),
                new RecordTableColumnModel(
                    "Result", "결과", RecordSortValueKind.Text, true,
                    RecordSortDirection.Ascending, 1.1f)
            };
            return new RecordTableModel(columns, rows).SortBy("Date", RecordSortDirection.Ascending);
        }

        private static RecordTableRowModel CreateFocusedRow(ScheduleGameSnapshot game)
        {
            bool isHome = game.FocusSide == ScheduleFocusSide.Home;
            ScheduleTeamSnapshot opponent = isHome ? game.AwayTeam : game.HomeTeam;
            int focusRuns = isHome ? game.HomeRuns : game.AwayRuns;
            int opponentRuns = isHome ? game.AwayRuns : game.HomeRuns;
            string result = game.IsCompleted
                ? FormatCompletedResult(game.FocusOutcome, focusRuns, opponentRuns)
                : "예정";
            string period = game.HasCalendarDate
                ? game.Date.ToString("MM.dd", CultureInfo.InvariantCulture)
                : game.PeriodLabel;
            double periodSortValue = game.HasCalendarDate ? game.Date.Ticks : game.Round;
            return new RecordTableRowModel(
                "game-" + game.GameId,
                new[]
                {
                    new RecordTableCellModel(
                        "Date",
                        period,
                        RecordSortValue.FromNumber(periodSortValue)),
                    new RecordTableCellModel(
                        "Venue",
                    isHome ? "홈" : "원정",
                    RecordSortValue.FromText(isHome ? "홈" : "원정")),
                    new RecordTableCellModel(
                        "Opponent",
                        opponent.DisplayName,
                        RecordSortValue.FromText(opponent.DisplayName)),
                    new RecordTableCellModel(
                        "Result",
                        result,
                        RecordSortValue.FromText(result))
                });
        }

        private static string FormatCompletedResult(
            ScheduleFocusOutcome outcome,
            int focusRuns,
            int opponentRuns)
        {
            string score = focusRuns.ToString(CultureInfo.InvariantCulture) + ":" +
                opponentRuns.ToString(CultureInfo.InvariantCulture);
            string outcomeText = FormatOutcome(outcome);
            return string.IsNullOrEmpty(outcomeText) ? score : outcomeText + "  " + score;
        }

        private static string FormatOutcome(ScheduleFocusOutcome outcome)
        {
            return outcome switch
            {
                ScheduleFocusOutcome.Win => "승",
                ScheduleFocusOutcome.Loss => "패",
                ScheduleFocusOutcome.Tie => "무",
                _ => string.Empty
            };
        }
    }
}
