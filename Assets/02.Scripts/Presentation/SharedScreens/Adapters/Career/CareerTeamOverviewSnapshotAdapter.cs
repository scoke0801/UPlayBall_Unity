using System;
using System.Collections.Generic;
using System.Globalization;
using Baseball.Game.Career;

namespace Baseball.Presentation.SharedScreens
{
    /// <summary>
    /// 감독 AI가 확정한 Career 구단 정보를 규칙 재계산 없이 공용 읽기 전용 Snapshot으로 변환한다.
    /// </summary>
    public static class CareerTeamOverviewSnapshotAdapter
    {
        /// <summary>
        /// 구단 요약, 전력 지표와 역할별 선수단을 공용 구단 Snapshot으로 만든다.
        /// </summary>
        public static TeamOverviewSnapshot Create(TeamOverviewView view)
        {
            if (view == null)
                throw new ArgumentNullException(nameof(view));

            string seasonLabel = view.SeasonYear.ToString(CultureInfo.InvariantCulture);
            return new TeamOverviewSnapshot(
                CareerSharedSnapshotFormatters.FormatId(view.TeamId),
                view.TeamName,
                seasonLabel,
                CareerSharedSnapshotFormatters.FormatLeague(view.LeagueLevel),
                $"{view.Wins}승 {view.Losses}패 {view.Ties}무",
                view.TeamRank > 0 ? $"{view.TeamRank}위" : "-",
                CareerSharedSnapshotFormatters.FormatTeamColor(view.PrimaryColor),
                CareerSharedSnapshotFormatters.FormatTeamEmblemKey(view.EmblemId),
                CreateStrengthTable(view),
                CreateRoster(view, seasonLabel));
        }

        /// <summary>
        /// Career 로스터의 확정된 역할과 다음 경기 계획을 읽기 전용 그룹으로 만든다.
        /// </summary>
        public static ReadOnlyRosterModel CreateRoster(TeamOverviewView view, string seasonLabel = null)
        {
            if (view == null)
                throw new ArgumentNullException(nameof(view));

            var starters = new List<ReadOnlyRosterPlayerModel>();
            var rotation = new List<ReadOnlyRosterPlayerModel>();
            var bullpen = new List<ReadOnlyRosterPlayerModel>();
            var competition = new List<ReadOnlyRosterPlayerModel>();
            var backups = new List<ReadOnlyRosterPlayerModel>();
            TeamRosterPlayerView[] roster = view.Roster ?? Array.Empty<TeamRosterPlayerView>();
            for (int i = 0; i < roster.Length; i++)
            {
                TeamRosterPlayerView player = roster[i];
                ReadOnlyRosterPlayerModel row = CreatePlayer(view, player);
                GetRoleGroup(player.RosterRole, starters, rotation, bullpen, competition, backups).Add(row);
            }

            var groups = new List<ReadOnlyRosterGroupModel>(5);
            AddGroup(groups, "Starting", "주전", starters);
            AddGroup(groups, "Rotation", "선발진", rotation);
            AddGroup(groups, "Bullpen", "불펜", bullpen);
            AddGroup(groups, "Competition", "경쟁", competition);
            AddGroup(groups, "Backup", "백업", backups);
            return new ReadOnlyRosterModel(
                CareerSharedSnapshotFormatters.FormatId(view.TeamId),
                view.TeamName,
                seasonLabel ?? view.SeasonYear.ToString(CultureInfo.InvariantCulture),
                $"등록 {roster.Length.ToString(CultureInfo.InvariantCulture)}명 · 읽기 전용",
                groups);
        }

        private static ReadOnlyRosterPlayerModel CreatePlayer(TeamOverviewView view, TeamRosterPlayerView player)
        {
            bool isPitcher = player.Position is Baseball.Core.Players.PlayerPosition.StartingPitcher or
                Baseball.Core.Players.PlayerPosition.ReliefPitcher;
            string primaryRecord = isPitcher
                ? player.HasPitchingRecord
                    ? "평균자책 " + CareerSharedSnapshotFormatters.FormatDecimal(player.EarnedRunAverage)
                    : "기록 없음"
                : player.HasBattingRecord
                    ? "타율 " + CareerSharedSnapshotFormatters.FormatRate(player.BattingAverage)
                    : "기록 없음";
            string reason = GetHighlightReason(view, player);
            return new ReadOnlyRosterPlayerModel(
                CareerSharedSnapshotFormatters.FormatId(player.PlayerId),
                player.Name,
                CareerSharedSnapshotFormatters.FormatPosition(player.Position),
                player.IsMyPlayer
                    ? CareerSharedSnapshotFormatters.FormatExpectedRole(view.MyPlayerExpectedRole)
                    : CareerSharedSnapshotFormatters.FormatRosterRole(player.RosterRole),
                player.Overall.ToString(CultureInfo.InvariantCulture),
                player.HasCondition ? FormatCondition(player.Condition) : "-",
                primaryRecord,
                string.Empty,
                player.IsMyPlayer || player.IsInNextGamePlan
                    ? RosterPlayerVisualState.Highlighted
                    : RosterPlayerVisualState.Normal,
                reason,
                player.IsInNextGamePlan,
                isPitcher ? RosterPlayerKind.Pitcher : RosterPlayerKind.Batter);
        }

        private static string FormatCondition(int condition)
        {
            if (condition >= 85) return "최상";
            if (condition >= 70) return "좋음";
            if (condition >= 50) return "보통";
            if (condition >= 30) return "나쁨";
            return "최악";
        }

        private static string GetHighlightReason(TeamOverviewView view, TeamRosterPlayerView player)
        {
            if (player.IsMyPlayer && view.HasNextGamePlan)
                return $"내 선수 · {CareerSharedSnapshotFormatters.FormatGameRole(view.PlannedPlayerRole)}";
            if (player.IsMyPlayer)
                return $"내 선수 · {CareerSharedSnapshotFormatters.FormatExpectedRole(view.MyPlayerExpectedRole)}";
            return player.IsInNextGamePlan ? "다음 경기 계획 포함" : string.Empty;
        }

        private static RecordTableModel CreateStrengthTable(TeamOverviewView view)
        {
            var columns = new[]
            {
                new RecordTableColumnModel(
                    "Unit", "구성", RecordSortValueKind.Text, true,
                    RecordSortDirection.Ascending, 1.8f, RecordCellAlignment.Left),
                new RecordTableColumnModel(
                    "Overall", "평균 능력", RecordSortValueKind.Number, true,
                    RecordSortDirection.Descending, 1f, RecordCellAlignment.Right)
            };
            var rows = new[]
            {
                StrengthRow("Field", "야수진", view.FieldPlayerOverall),
                StrengthRow("Rotation", "선발진", view.StartingPitcherOverall),
                StrengthRow("Bullpen", "불펜", view.ReliefPitcherOverall)
            };
            return new RecordTableModel(columns, rows);
        }

        private static RecordTableRowModel StrengthRow(string id, string label, int overall)
        {
            return new RecordTableRowModel(
                id,
                new[]
                {
                    new RecordTableCellModel("Unit", label, RecordSortValue.FromText(label)),
                    new RecordTableCellModel(
                        "Overall",
                        overall.ToString(CultureInfo.InvariantCulture),
                        RecordSortValue.FromNumber(overall))
                });
        }

        private static List<ReadOnlyRosterPlayerModel> GetRoleGroup(
            TeamRosterRole role,
            List<ReadOnlyRosterPlayerModel> starters,
            List<ReadOnlyRosterPlayerModel> rotation,
            List<ReadOnlyRosterPlayerModel> bullpen,
            List<ReadOnlyRosterPlayerModel> competition,
            List<ReadOnlyRosterPlayerModel> backups)
        {
            return role switch
            {
                TeamRosterRole.Starting => starters,
                TeamRosterRole.Rotation => rotation,
                TeamRosterRole.Bullpen => bullpen,
                TeamRosterRole.Competition => competition,
                _ => backups
            };
        }

        private static void AddGroup(
            List<ReadOnlyRosterGroupModel> groups,
            string id,
            string displayName,
            List<ReadOnlyRosterPlayerModel> players)
        {
            if (players.Count > 0)
                groups.Add(new ReadOnlyRosterGroupModel(id, displayName, players));
        }
    }
}
