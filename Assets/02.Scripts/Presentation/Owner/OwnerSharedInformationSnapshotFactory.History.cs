using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Game.Historical;
using Baseball.Presentation.SharedScreens;

namespace Baseball.Presentation.Owner
{
    public sealed partial class OwnerSharedInformationSnapshotFactory
    {
        /// <summary>구단의 실제 진행 시즌과 해당 조의 포스트시즌을 동일한 시즌 번호로 연결한다.</summary>
        public OwnerClubHistoryPresentationModel CreateClubHistory(OwnerModeManager manager)
        {
            var runtime = RequireRuntime(manager);
            var seasons = new List<OwnerClubHistorySeason>();
            string name = manager.GetClubDisplayName(runtime.PlayerTeamSeasonKey);
            foreach (var completed in runtime.ManagerMode.CompletedSeasons)
                seasons.Add(CreateHistorySeason(completed.Season, completed.LeagueGrade, name,
                    FindPostseason(runtime.LeagueWorld.CompletedGroups, completed.Season), false));
            var live = runtime.ManagerMode.LiveSeason;
            seasons.Add(CreateHistorySeason(live, runtime.League.Grade, name,
                FindPostseason(runtime.LeagueWorld.Groups, live), true));
            return new OwnerClubHistoryPresentationModel(name, seasons, CreateHistoryColumns());
        }

        /// <summary>조회 전용 시즌 Snapshot을 만들어 UI가 런타임 상태를 소유하지 않게 한다.</summary>
        public OwnerClubHistorySeason CreateHistorySeason(ManagerLiveSeasonState season, LeagueGrade grade,
            string name, OwnerPostseasonState postseason, bool isCurrent)
        {
            var row = CreateClubSeasonRow(season, grade, name, isCurrent);
            var cells = new List<RecordTableCellModel>(row.Cells);
            cells[2] = TextCell("Status", season.IsCompleted ? "정규 완료" : "진행 중");
            OwnerTeamPostseasonResult? result = postseason?.IsCompleted == true
                ? postseason.GetTeamResult(season.PlayerTeamId) : (OwnerTeamPostseasonResult?)null;
            cells.Add(TextCell("Pennant", season.IsCompleted
                ? (row.FindCell("Rank").SortValue.Number == 1 ? "우승" : "—") : "미확정"));
            cells.Add(TextCell("Postseason", result == OwnerTeamPostseasonResult.Champion ? "우승" :
                result == OwnerTeamPostseasonResult.RunnerUp ? "준우승" :
                result == OwnerTeamPostseasonResult.WildCardElimination ? "와일드카드 탈락" :
                result == OwnerTeamPostseasonResult.SemiPlayoffElimination ? "준플레이오프 탈락" :
                result == OwnerTeamPostseasonResult.PlayoffElimination ? "플레이오프 탈락" :
                result == OwnerTeamPostseasonResult.SemifinalElimination ? "4강 탈락" :
                result == OwnerTeamPostseasonResult.DidNotQualify ? "미진출" : isCurrent ? "미확정" : "기록 없음"));
            return new OwnerClubHistorySeason(season.SeasonNumber, grade, season.IsCompleted,
                new RecordTableRowModel(row.RowId, cells, row.IsHighlighted, row.HighlightReason),
                OwnerClubHistoryStatistics.CalculateTotals(season),
                OwnerClubHistoryStatistics.CollectRecords(season),
                result);
        }

        /// <summary>정규시즌과 포스트시즌 타이틀을 시즌 전적 앞부분에서 구분한다.</summary>
        public static IReadOnlyList<RecordTableColumnModel> CreateHistoryColumns()
        {
            var columns = new List<RecordTableColumnModel>(CreateClubSeasonColumns());
            columns.Insert(3, new RecordTableColumnModel("Pennant", "페넌트레이스", RecordSortValueKind.Text, widthWeight: 1.25f));
            columns.Insert(4, new RecordTableColumnModel("Postseason", "포스트시즌", RecordSortValueKind.Text, widthWeight: 1.25f));
            return columns.AsReadOnly();
        }

        private static OwnerPostseasonState FindPostseason(IReadOnlyList<OwnerLeagueGroupState> groups,
            ManagerLiveSeasonState season)
        {
            if (groups == null) return null;
            foreach (var group in groups)
                if (string.Equals(group.Season.SeasonId, season.SeasonId, StringComparison.Ordinal)) return group.Postseason;
            return null;
        }
    }
}
