using System;
using System.Collections.Generic;
using System.Globalization;
using Baseball.Game.Career;

namespace Baseball.Presentation.SharedScreens
{
    /// <summary>
    /// 선수 Career의 리그 Hub 읽기 모델을 모드 중립 공용 표 Snapshot으로 변환한다.
    /// </summary>
    public static class CareerLeagueSnapshotAdapter
    {
        /// <summary>
        /// 현재 선택 부문의 순위표까지 포함한 공용 리그 화면 Snapshot을 만든다.
        /// </summary>
        public static LeagueScreenSnapshot Create(
            LeagueHubView view,
            LeagueBattingCategory battingCategory = LeagueBattingCategory.BattingAverage,
            LeaguePitchingCategory pitchingCategory = LeaguePitchingCategory.EarnedRunAverage)
        {
            if (view == null)
                throw new ArgumentNullException(nameof(view));

            return new LeagueScreenSnapshot(
                view.SeasonYear.ToString(CultureInfo.InvariantCulture),
                CareerSharedSnapshotFormatters.FormatLeague(view.LeagueLevel),
                $"{view.GamesPlayedPerTeam}/{view.RegularSeasonGamesPerTeam} 경기",
                CareerSharedSnapshotFormatters.FormatId(view.MyTeamId),
                CreateStandingsTable(view),
                CreateScheduleTable(view.RecentResults, "RecentResults"),
                CreateScheduleTable(view.NextRoundGames, "NextRoundGames"),
                CreateBattingTable(view.GetBattingLeaderboard(battingCategory), battingCategory),
                CreatePitchingTable(view.GetPitchingLeaderboard(pitchingCategory), pitchingCategory),
                CreateTeamMetricsTable(view.TeamMetrics));
        }

        /// <summary>
        /// 기존 리그 순위 데이터를 Stable ID와 원시 정렬 값을 가진 공용 기록표로 변환한다.
        /// </summary>
        public static RecordTableModel CreateStandingsTable(LeagueHubView view)
        {
            if (view == null)
                throw new ArgumentNullException(nameof(view));

            var columns = new[]
            {
                Column("Rank", "순위", RecordSortValueKind.Number, RecordSortDirection.Ascending, 0.7f),
                Column("Team", "구단", RecordSortValueKind.Text, RecordSortDirection.Ascending, 2.2f, RecordCellAlignment.Left),
                Column("Games", "경기", RecordSortValueKind.Number, RecordSortDirection.Descending, 0.8f),
                Column("Wins", "승", RecordSortValueKind.Number, RecordSortDirection.Descending, 0.7f),
                Column("Losses", "패", RecordSortValueKind.Number, RecordSortDirection.Ascending, 0.7f),
                Column("Pct", "승률", RecordSortValueKind.Number, RecordSortDirection.Descending, 1f),
                Column("GamesBehind", "게임차", RecordSortValueKind.Number, RecordSortDirection.Ascending, 0.9f),
                Column("Streak", "최근", RecordSortValueKind.Text, RecordSortDirection.Descending, 0.9f)
            };
            var rows = new RecordTableRowModel[view.Standings.Count];
            for (int i = 0; i < view.Standings.Count; i++)
            {
                LeagueStandingView row = view.Standings[i];
                rows[i] = new RecordTableRowModel(
                    $"team-{row.TeamId.ToString(CultureInfo.InvariantCulture)}",
                    new[]
                    {
                        NumberCell("Rank", row.Rank.ToString(CultureInfo.InvariantCulture), row.Rank),
                        TextCell("Team", row.TeamName),
                        NumberCell("Games", row.GamesPlayed.ToString(CultureInfo.InvariantCulture), row.GamesPlayed),
                        NumberCell("Wins", row.Wins.ToString(CultureInfo.InvariantCulture), row.Wins),
                        NumberCell("Losses", row.Losses.ToString(CultureInfo.InvariantCulture), row.Losses),
                        NumberCell("Pct", CareerSharedSnapshotFormatters.FormatRate(row.WinningPercentage), row.WinningPercentage),
                        NumberCell("GamesBehind", row.Rank == 1 ? "-" : row.GamesBehind.ToString("0.0", CultureInfo.InvariantCulture), row.GamesBehind),
                        TextCell("Streak", FormatStreak(row.StreakOutcome, row.StreakLength))
                    },
                    row.IsMyTeam,
                    row.IsMyTeam ? "현재 구단" : FormatZone(row.Zone));
            }

            return new RecordTableModel(columns, rows, "Rank", RecordSortDirection.Ascending);
        }

        /// <summary>
        /// 선택한 타격 부문의 상위권과 포커스 선수를 공용 기록표로 변환한다.
        /// </summary>
        public static RecordTableModel CreateBattingTable(
            LeagueBattingLeaderboardView leaderboard,
            LeagueBattingCategory category)
        {
            if (leaderboard == null)
                throw new ArgumentNullException(nameof(leaderboard));

            var columns = new[]
            {
                Column("Rank", "순위", RecordSortValueKind.Number, RecordSortDirection.Ascending, 0.6f),
                Column("Player", "선수", RecordSortValueKind.Text, RecordSortDirection.Ascending, 1.8f, RecordCellAlignment.Left),
                Column("Position", "포지션", RecordSortValueKind.Text, RecordSortDirection.Ascending, 0.7f),
                Column("Team", "구단", RecordSortValueKind.Text, RecordSortDirection.Ascending, 1.3f, RecordCellAlignment.Left),
                Column("Games", "경기", RecordSortValueKind.Number, RecordSortDirection.Descending, 0.7f),
                Column("Average", "타율", RecordSortValueKind.Number, RecordSortDirection.Descending, 0.9f),
                Column("HomeRuns", "홈런", RecordSortValueKind.Number, RecordSortDirection.Descending, 0.8f),
                Column("Rbi", "타점", RecordSortValueKind.Number, RecordSortDirection.Descending, 0.8f),
                Column("StolenBases", "도루", RecordSortValueKind.Number, RecordSortDirection.Descending, 0.8f),
                Column("Ops", "출루+장타", RecordSortValueKind.Number, RecordSortDirection.Descending, 0.9f)
            };
            var rows = new List<RecordTableRowModel>(leaderboard.Leaders.Count + 1);
            for (int i = 0; i < leaderboard.Leaders.Count; i++)
                rows.Add(CreateBattingRow(leaderboard.Leaders[i]));
            if (leaderboard.MyPlayer.HasValue && !ContainsPlayer(rows, leaderboard.MyPlayer.Value.PlayerId))
                rows.Add(CreateBattingRow(leaderboard.MyPlayer.Value));

            string sortColumn = GetBattingSortColumn(category);
            return new RecordTableModel(columns, rows).SortBy(sortColumn, RecordSortDirection.Descending);
        }

        /// <summary>
        /// 선택한 투구 부문의 상위권과 포커스 선수를 공용 기록표로 변환한다.
        /// </summary>
        public static RecordTableModel CreatePitchingTable(
            LeaguePitchingLeaderboardView leaderboard,
            LeaguePitchingCategory category)
        {
            if (leaderboard == null)
                throw new ArgumentNullException(nameof(leaderboard));

            var columns = new[]
            {
                Column("Rank", "순위", RecordSortValueKind.Number, RecordSortDirection.Ascending, 0.6f),
                Column("Player", "선수", RecordSortValueKind.Text, RecordSortDirection.Ascending, 1.8f, RecordCellAlignment.Left),
                Column("Position", "포지션", RecordSortValueKind.Text, RecordSortDirection.Ascending, 0.7f),
                Column("Team", "구단", RecordSortValueKind.Text, RecordSortDirection.Ascending, 1.3f, RecordCellAlignment.Left),
                Column("Record", "승-패", RecordSortValueKind.Number, RecordSortDirection.Descending, 0.9f),
                Column("Saves", "세이브", RecordSortValueKind.Number, RecordSortDirection.Descending, 0.8f),
                Column("Innings", "이닝", RecordSortValueKind.Number, RecordSortDirection.Descending, 0.9f),
                Column("Era", "평균자책", RecordSortValueKind.Number, RecordSortDirection.Ascending, 1f),
                Column("Strikeouts", "탈삼진", RecordSortValueKind.Number, RecordSortDirection.Descending, 0.9f),
                Column("Whip", "이닝당출루", RecordSortValueKind.Number, RecordSortDirection.Ascending, 0.9f)
            };
            var rows = new List<RecordTableRowModel>(leaderboard.Leaders.Count + 1);
            for (int i = 0; i < leaderboard.Leaders.Count; i++)
                rows.Add(CreatePitchingRow(leaderboard.Leaders[i]));
            if (leaderboard.MyPlayer.HasValue && !ContainsPlayer(rows, leaderboard.MyPlayer.Value.PlayerId))
                rows.Add(CreatePitchingRow(leaderboard.MyPlayer.Value));

            string sortColumn = GetPitchingSortColumn(category);
            RecordSortDirection direction = category == LeaguePitchingCategory.EarnedRunAverage ||
                category == LeaguePitchingCategory.WalksHitsPerInningPitched
                    ? RecordSortDirection.Ascending
                    : RecordSortDirection.Descending;
            return new RecordTableModel(columns, rows).SortBy(sortColumn, direction);
        }

        private static RecordTableModel CreateScheduleTable(
            IReadOnlyList<LeagueScheduleGameView> games,
            string rowPrefix)
        {
            var columns = new[]
            {
                Column("Date", "날짜", RecordSortValueKind.Number, RecordSortDirection.Ascending, 1.1f),
                Column("Round", "라운드", RecordSortValueKind.Number, RecordSortDirection.Ascending, 0.7f),
                Column("Away", "원정", RecordSortValueKind.Text, RecordSortDirection.Ascending, 1.5f, RecordCellAlignment.Right),
                Column("Score", "결과", RecordSortValueKind.Text, RecordSortDirection.Descending, 0.9f),
                Column("Home", "홈", RecordSortValueKind.Text, RecordSortDirection.Ascending, 1.5f, RecordCellAlignment.Left)
            };
            var rows = new RecordTableRowModel[games.Count];
            for (int i = 0; i < games.Count; i++)
            {
                LeagueScheduleGameView game = games[i];
                rows[i] = new RecordTableRowModel(
                    $"{rowPrefix}-{game.GameId.ToString(CultureInfo.InvariantCulture)}",
                    new[]
                    {
                        NumberCell("Date", game.Date.ToString("M/d", CultureInfo.InvariantCulture), game.Date.Ticks),
                        NumberCell("Round", game.Round.ToString(CultureInfo.InvariantCulture), game.Round),
                        TextCell("Away", game.AwayTeamName),
                        TextCell("Score", game.IsCompleted ? $"{game.AwayRuns}-{game.HomeRuns}" : "예정"),
                        TextCell("Home", game.HomeTeamName)
                    },
                    game.IncludesMyTeam,
                    game.IncludesMyTeam ? "현재 구단 경기" : string.Empty);
            }
            return new RecordTableModel(columns, rows, "Date", RecordSortDirection.Ascending);
        }

        private static RecordTableModel CreateTeamMetricsTable(IReadOnlyList<LeagueTeamMetricView> metrics)
        {
            var columns = new[]
            {
                Column("Metric", "지표", RecordSortValueKind.Text, RecordSortDirection.Ascending, 1.2f, RecordCellAlignment.Left),
                Column("Best", "리그 최고", RecordSortValueKind.Number, RecordSortDirection.Descending, 1f),
                Column("Average", "리그 평균", RecordSortValueKind.Number, RecordSortDirection.Descending, 1f),
                Column("Focus", "현재 구단", RecordSortValueKind.Number, RecordSortDirection.Descending, 1f),
                Column("Rank", "순위", RecordSortValueKind.Number, RecordSortDirection.Ascending, 0.7f)
            };
            var rows = new RecordTableRowModel[metrics.Count];
            for (int i = 0; i < metrics.Count; i++)
            {
                LeagueTeamMetricView metric = metrics[i];
                rows[i] = new RecordTableRowModel(
                    $"metric-{metric.Metric}",
                    new[]
                    {
                        TextCell("Metric", FormatTeamMetricLabel(metric.Metric)),
                        NumberCell("Best", FormatTeamMetric(metric.Metric, metric.BestValue), metric.BestValue, metric.HasData),
                        NumberCell("Average", FormatTeamMetric(metric.Metric, metric.LeagueAverage), metric.LeagueAverage, metric.HasData),
                        NumberCell("Focus", FormatTeamMetric(metric.Metric, metric.MyTeamValue), metric.MyTeamValue, metric.HasData),
                        NumberCell("Rank", metric.HasData ? $"{metric.MyTeamRank}위" : "-", metric.MyTeamRank, metric.HasData)
                    });
            }
            return new RecordTableModel(columns, rows);
        }

        private static RecordTableRowModel CreateBattingRow(LeagueBattingLeaderView row)
        {
            return new RecordTableRowModel(
                $"player-{row.PlayerId.ToString(CultureInfo.InvariantCulture)}",
                new[]
                {
                    NumberCell("Rank", row.Rank.ToString(CultureInfo.InvariantCulture), row.Rank),
                    TextCell("Player", row.PlayerName),
                    TextCell("Position", CareerSharedSnapshotFormatters.FormatPosition(row.Position)),
                    TextCell("Team", row.TeamName),
                    NumberCell("Games", row.Games.ToString(CultureInfo.InvariantCulture), row.Games),
                    NumberCell("Average", CareerSharedSnapshotFormatters.FormatRate(row.BattingAverage), row.BattingAverage),
                    NumberCell("HomeRuns", row.HomeRuns.ToString(CultureInfo.InvariantCulture), row.HomeRuns),
                    NumberCell("Rbi", row.RunsBattedIn.ToString(CultureInfo.InvariantCulture), row.RunsBattedIn),
                    NumberCell("StolenBases", row.StolenBases.ToString(CultureInfo.InvariantCulture), row.StolenBases),
                    NumberCell("Ops", CareerSharedSnapshotFormatters.FormatRate(row.OnBasePlusSlugging), row.OnBasePlusSlugging)
                },
                row.IsMyPlayer,
                row.IsMyPlayer ? "내 선수" : string.Empty);
        }

        private static RecordTableRowModel CreatePitchingRow(LeaguePitchingLeaderView row)
        {
            return new RecordTableRowModel(
                $"player-{row.PlayerId.ToString(CultureInfo.InvariantCulture)}",
                new[]
                {
                    NumberCell("Rank", row.Rank.ToString(CultureInfo.InvariantCulture), row.Rank),
                    TextCell("Player", row.PlayerName),
                    TextCell("Position", CareerSharedSnapshotFormatters.FormatPosition(row.Position)),
                    TextCell("Team", row.TeamName),
                    NumberCell("Record", $"{row.Wins}-{row.Losses}", row.Wins),
                    NumberCell("Saves", row.Saves.ToString(CultureInfo.InvariantCulture), row.Saves),
                    NumberCell("Innings", CareerSharedSnapshotFormatters.FormatInnings(row.OutsRecorded), row.OutsRecorded),
                    NumberCell("Era", CareerSharedSnapshotFormatters.FormatDecimal(row.EarnedRunAverage), row.EarnedRunAverage),
                    NumberCell("Strikeouts", row.Strikeouts.ToString(CultureInfo.InvariantCulture), row.Strikeouts),
                    NumberCell("Whip", CareerSharedSnapshotFormatters.FormatDecimal(row.WalksHitsPerInningPitched), row.WalksHitsPerInningPitched)
                },
                row.IsMyPlayer,
                row.IsMyPlayer ? "내 선수" : string.Empty);
        }

        private static bool ContainsPlayer(List<RecordTableRowModel> rows, int playerId)
        {
            string rowId = $"player-{playerId.ToString(CultureInfo.InvariantCulture)}";
            for (int i = 0; i < rows.Count; i++)
            {
                if (string.Equals(rows[i].RowId, rowId, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static string GetBattingSortColumn(LeagueBattingCategory category)
        {
            return category switch
            {
                LeagueBattingCategory.HomeRuns => "HomeRuns",
                LeagueBattingCategory.RunsBattedIn => "Rbi",
                LeagueBattingCategory.StolenBases => "StolenBases",
                LeagueBattingCategory.OnBasePlusSlugging => "Ops",
                _ => "Average"
            };
        }

        private static string GetPitchingSortColumn(LeaguePitchingCategory category)
        {
            return category switch
            {
                LeaguePitchingCategory.Wins => "Record",
                LeaguePitchingCategory.Saves => "Saves",
                LeaguePitchingCategory.Strikeouts => "Strikeouts",
                LeaguePitchingCategory.WalksHitsPerInningPitched => "Whip",
                _ => "Era"
            };
        }

        private static string FormatStreak(TeamGameOutcome? outcome, int length)
        {
            if (!outcome.HasValue || length <= 0)
                return "-";
            return outcome.Value switch
            {
                TeamGameOutcome.Win => $"{length}승",
                TeamGameOutcome.Loss => $"{length}패",
                _ => $"{length}무"
            };
        }

        private static string FormatZone(LeagueStandingZone zone)
        {
            return zone switch
            {
                LeagueStandingZone.Promotion => "승격권",
                LeagueStandingZone.PostseasonRetention => "포스트시즌권",
                LeagueStandingZone.Relegation => "강등권",
                _ => string.Empty
            };
        }

        private static string FormatTeamMetricLabel(LeagueTeamMetric metric)
        {
            return metric switch
            {
                LeagueTeamMetric.BattingAverage => "팀 타율",
                LeagueTeamMetric.HomeRuns => "팀 홈런",
                LeagueTeamMetric.EarnedRunAverage => "팀 평균자책",
                _ => "팀 탈삼진"
            };
        }

        private static string FormatTeamMetric(LeagueTeamMetric metric, double value)
        {
            return metric == LeagueTeamMetric.BattingAverage
                ? CareerSharedSnapshotFormatters.FormatRate(value)
                : metric == LeagueTeamMetric.EarnedRunAverage
                    ? CareerSharedSnapshotFormatters.FormatDecimal(value)
                    : Math.Round(value).ToString("0", CultureInfo.InvariantCulture);
        }

        private static RecordTableColumnModel Column(
            string id,
            string name,
            RecordSortValueKind kind,
            RecordSortDirection direction,
            float width,
            RecordCellAlignment alignment = RecordCellAlignment.Center)
        {
            return new RecordTableColumnModel(id, name, kind, true, direction, width, alignment);
        }

        private static RecordTableCellModel TextCell(string id, string value)
        {
            string safe = value ?? string.Empty;
            return new RecordTableCellModel(id, safe, RecordSortValue.FromText(safe));
        }

        private static RecordTableCellModel NumberCell(string id, string display, double value, bool hasValue = true)
        {
            return new RecordTableCellModel(
                id,
                display ?? string.Empty,
                hasValue ? RecordSortValue.FromNumber(value) : RecordSortValue.Empty());
        }
    }
}
