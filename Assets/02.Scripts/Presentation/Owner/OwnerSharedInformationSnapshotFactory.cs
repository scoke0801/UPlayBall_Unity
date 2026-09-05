using System;
using System.Collections.Generic;
using System.Globalization;
using Baseball.Core.Historical;
using Baseball.Game.Career;
using Baseball.Game.Historical;
using Baseball.Presentation.SharedScreens;

namespace Baseball.Presentation.Owner
{
    /// <summary>Owner Runtime의 확정 일정과 역사 기록만 공용 정보 화면 Snapshot으로 투영한다.</summary>
    public sealed class OwnerSharedInformationSnapshotFactory
    {
        /// <summary>현재 Save의 전체 대진과 완료 점수를 날짜를 발명하지 않는 Round 일정으로 복사한다.</summary>
        public ScheduleScreenSnapshot CreateSchedule(OwnerModeManager manager)
        {
            ManagerHistoricalRuntimeState runtime = RequireRuntime(manager);
            ManagerLiveSeasonState liveSeason = runtime.ManagerMode.LiveSeason;
            return CreateSchedule(
                liveSeason,
                runtime.League.Grade.ToString(),
                teamSeasonKey => manager.GetTeamDisplayName(teamSeasonKey));
        }

        /// <summary>Owner 일정 원본과 이름 Resolver를 날짜 없는 공용 Round Snapshot으로 복사한다.</summary>
        public ScheduleScreenSnapshot CreateSchedule(
            ManagerLiveSeasonState liveSeason,
            string leagueLabel,
            Func<string, string> teamDisplayNameResolver)
        {
            if (liveSeason == null)
                throw new ArgumentNullException(nameof(liveSeason));
            if (teamDisplayNameResolver == null)
                throw new ArgumentNullException(nameof(teamDisplayNameResolver));
            IReadOnlyList<ScheduledGameState> source = liveSeason.Schedule.Games;
            var games = new ScheduleGameSnapshot[source.Count];

            for (int index = 0; index < games.Length; index++)
            {
                ScheduledGameState game = source[index];
                string awayKey = liveSeason.GetTeamSeasonKey(game.AwayTeamId);
                string homeKey = liveSeason.GetTeamSeasonKey(game.HomeTeamId);
                ScheduleFocusSide focusSide = ResolveFocusSide(game, liveSeason.PlayerTeamId);
                games[index] = new ScheduleGameSnapshot(
                    game.GameId.ToString(CultureInfo.InvariantCulture),
                    game.Round,
                    game.Round.ToString(CultureInfo.InvariantCulture) + "R",
                    new ScheduleTeamSnapshot(
                        awayKey,
                        teamDisplayNameResolver(awayKey)),
                    new ScheduleTeamSnapshot(
                        homeKey,
                        teamDisplayNameResolver(homeKey)),
                    game.IsCompleted,
                    game.AwayRuns,
                    game.HomeRuns,
                    focusSide,
                    ScheduleFocusOutcome.Pending);
            }

            return new ScheduleScreenSnapshot(
                liveSeason.OriginYear.ToString(CultureInfo.InvariantCulture) + " 시즌",
                leagueLabel,
                (liveSeason.CurrentWeekIndex + 1).ToString(CultureInfo.InvariantCulture) + "주차",
                liveSeason.GetTeamSeasonKey(liveSeason.PlayerTeamId),
                games);
        }

        /// <summary>새 게임 생성 때 확정된 WorldHistory 정규 시즌 타격 기록을 현재 시즌 기록과 혼동되지 않게 복사한다.</summary>
        public RecordsScreenSnapshot CreateHistoricalBattingRecords(OwnerModeManager manager)
        {
            ManagerHistoricalRuntimeState runtime = RequireRuntime(manager);
            int originYear = runtime.ManagerMode.LiveSeason.OriginYear;
            Dictionary<string, PlayerSeasonDefinition> seasons = IndexPlayerSeasons(runtime.WorldCardCatalog);
            var rows = new List<RecordTableRowModel>();
            IReadOnlyList<SeasonStatistics> statistics = runtime.WorldHistory.Statistics;

            for (int index = 0; index < statistics.Count; index++)
            {
                SeasonStatistics record = statistics[index];
                if (record.SeasonYear != originYear ||
                    record.IsFirstHalf ||
                    record.IsPostseason ||
                    record.IsAllStarGame ||
                    record.PlateAppearances <= 0)
                    continue;

                if (!seasons.TryGetValue(record.PlayerSeasonId, out PlayerSeasonDefinition season))
                    throw new InvalidOperationException(
                        $"WorldHistory 기록의 PlayerSeasonId {record.PlayerSeasonId}가 카드 카탈로그에 없습니다.");

                string playerName = runtime.IdentityRegistry.GetPlayerDisplayName(season.PlayerPersonId);
                string teamName = manager.GetTeamDisplayName(record.TeamSeasonKey);
                rows.Add(CreateBattingRow(record, playerName, teamName,
                    string.Equals(record.TeamSeasonKey, runtime.PlayerTeamSeasonKey, StringComparison.Ordinal)));
            }

            RecordTableModel table = new RecordTableModel(CreateBattingColumns(), rows)
                .SortBy("Hits", RecordSortDirection.Descending);
            return new RecordsScreenSnapshot(
                originYear.ToString(CultureInfo.InvariantCulture) + " 시즌",
                runtime.League.Grade.ToString(),
                "월드 히스토리 확정 기록",
                "정규 시즌 타격",
                table,
                "현재 Owner 시즌 누적이 아니라 새 게임 생성 시 확정된 역사 시뮬레이션 기록입니다.");
        }

        private static ScheduleFocusSide ResolveFocusSide(ScheduledGameState game, int focusTeamId)
        {
            if (game.HomeTeamId == focusTeamId)
                return ScheduleFocusSide.Home;
            if (game.AwayTeamId == focusTeamId)
                return ScheduleFocusSide.Away;
            return ScheduleFocusSide.None;
        }

        private static Dictionary<string, PlayerSeasonDefinition> IndexPlayerSeasons(WorldCardCatalog catalog)
        {
            var result = new Dictionary<string, PlayerSeasonDefinition>(StringComparer.Ordinal);
            for (int index = 0; index < catalog.Cards.Count; index++)
            {
                PlayerCardDefinition card = catalog.Cards[index];
                if (result.ContainsKey(card.PlayerSeasonId))
                    continue;
                PlayerSeasonDefinition season = catalog.GetPlayerSeason(card);
                result.Add(season.PlayerSeasonId, season);
            }
            return result;
        }

        private static RecordTableColumnModel[] CreateBattingColumns()
        {
            return new[]
            {
                new RecordTableColumnModel(
                    "Player", "선수", RecordSortValueKind.Text, true,
                    RecordSortDirection.Ascending, 2.2f, RecordCellAlignment.Left),
                new RecordTableColumnModel(
                    "Team", "구단", RecordSortValueKind.Text, true,
                    RecordSortDirection.Ascending, 1.7f, RecordCellAlignment.Left),
                new RecordTableColumnModel("PA", "PA", RecordSortValueKind.Number),
                new RecordTableColumnModel("Hits", "H", RecordSortValueKind.Number),
                new RecordTableColumnModel("HR", "HR", RecordSortValueKind.Number),
                new RecordTableColumnModel("BB", "BB", RecordSortValueKind.Number),
                new RecordTableColumnModel("SO", "SO", RecordSortValueKind.Number),
                new RecordTableColumnModel("AVG", "AVG", RecordSortValueKind.Number)
            };
        }

        private static RecordTableRowModel CreateBattingRow(
            SeasonStatistics record,
            string playerName,
            string teamName,
            bool isPlayerTeam)
        {
            return new RecordTableRowModel(
                string.Concat("history:", record.PlayerSeasonId, ":", record.TeamSeasonKey),
                new[]
                {
                    TextCell("Player", playerName),
                    TextCell("Team", teamName),
                    NumberCell("PA", record.PlateAppearances),
                    NumberCell("Hits", record.Hits),
                    NumberCell("HR", record.HomeRuns),
                    NumberCell("BB", record.Walks),
                    NumberCell("SO", record.Strikeouts),
                    new RecordTableCellModel(
                        "AVG",
                        record.BattingAverage.ToString("0.000", CultureInfo.InvariantCulture),
                        RecordSortValue.FromNumber(record.BattingAverage))
                },
                isPlayerTeam,
                isPlayerTeam ? "현재 구단의 확정 역사 기록" : string.Empty);
        }

        private static RecordTableCellModel TextCell(string columnId, string value)
        {
            return new RecordTableCellModel(columnId, value, RecordSortValue.FromText(value));
        }

        private static RecordTableCellModel NumberCell(string columnId, int value)
        {
            return new RecordTableCellModel(
                columnId,
                value.ToString(CultureInfo.InvariantCulture),
                RecordSortValue.FromNumber(value));
        }

        private static ManagerHistoricalRuntimeState RequireRuntime(OwnerModeManager manager)
        {
            if (manager == null)
                throw new ArgumentNullException(nameof(manager));
            if (!manager.HasActiveRuntime || manager.Runtime == null || !manager.Runtime.HasManagerMode)
                throw new InvalidOperationException("활성 구단주 Runtime이 필요합니다.");
            return manager.Runtime;
        }
    }

    /// <summary>읽기 전용 Owner 공용 화면이 존재하지 않는 Command를 노출하지 않게 한다.</summary>
    public sealed class OwnerReadOnlySharedScreenActionProvider : ISharedScreenActionProvider
    {
        public static OwnerReadOnlySharedScreenActionProvider Instance { get; } =
            new OwnerReadOnlySharedScreenActionProvider();

        private OwnerReadOnlySharedScreenActionProvider()
        {
        }

        /// <summary>읽기 전용 Owner 일정·역사 기록 화면에는 Action을 공급하지 않는다.</summary>
        public IReadOnlyList<SharedScreenActionModel> GetActions(SharedScreenContext context)
        {
            return Array.Empty<SharedScreenActionModel>();
        }

        /// <summary>가짜 Owner Command를 실행하지 않고 항상 false를 반환한다.</summary>
        public bool TryExecute(string actionId, SharedScreenContext context)
        {
            return false;
        }
    }
}
