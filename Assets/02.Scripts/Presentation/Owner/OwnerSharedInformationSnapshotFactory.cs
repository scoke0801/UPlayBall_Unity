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
                OwnerLeagueDisplayNameFormatter.FormatFull(runtime.League.Grade),
                teamSeasonKey => manager.GetClubDisplayName(teamSeasonKey),
                teamSeasonKey => manager.GetTeamOriginYear(teamSeasonKey),
                manager.GetClubDisplayName(runtime.PlayerTeamSeasonKey));
        }

        /// <summary>Owner 일정 원본과 이름 Resolver를 날짜 없는 공용 Round Snapshot으로 복사한다.</summary>
        /// <param name="focusEmblemTeamName">구단주가 바꾼 이름 대신 내 구단 엠블렘을 찾을 원본 구단명.</param>
        public ScheduleScreenSnapshot CreateSchedule(
            ManagerLiveSeasonState liveSeason,
            string leagueLabel,
            Func<string, string> teamDisplayNameResolver,
            Func<string, int?> teamOriginYearResolver = null,
            string focusEmblemTeamName = null)
        {
            if (liveSeason == null)
                throw new ArgumentNullException(nameof(liveSeason));
            if (teamDisplayNameResolver == null)
                throw new ArgumentNullException(nameof(teamDisplayNameResolver));
            IReadOnlyList<ScheduledGameState> source = liveSeason.Schedule.Games;
            var games = new ScheduleGameSnapshot[source.Count];
            string focusTeamKey = liveSeason.GetTeamSeasonKey(liveSeason.PlayerTeamId);
            var teamDisplayNames = new Dictionary<string, string>(StringComparer.Ordinal);

            for (int index = 0; index < games.Length; index++)
            {
                ScheduledGameState game = source[index];
                string awayKey = liveSeason.GetTeamSeasonKey(game.AwayTeamId);
                string homeKey = liveSeason.GetTeamSeasonKey(game.HomeTeamId);
                ScheduleFocusSide focusSide = ResolveFocusSide(game, liveSeason.PlayerTeamId);
                games[index] = new ScheduleGameSnapshot(
                    game.GameId.ToString(CultureInfo.InvariantCulture),
                    game.Round,
                    game.Round.ToString(CultureInfo.InvariantCulture) + "라운드",
                    new ScheduleTeamSnapshot(
                        awayKey,
                        FormatLeagueTeamDisplayName(
                            awayKey,
                            teamDisplayNameResolver,
                            teamOriginYearResolver,
                            teamDisplayNames),
                        "TeamEmblem/" + game.AwayTeamId.ToString(CultureInfo.InvariantCulture),
                        emblemTeamName: ResolveEmblemTeamName(awayKey, focusTeamKey, focusEmblemTeamName)),
                    new ScheduleTeamSnapshot(
                        homeKey,
                        FormatLeagueTeamDisplayName(
                            homeKey,
                            teamDisplayNameResolver,
                            teamOriginYearResolver,
                            teamDisplayNames),
                        "TeamEmblem/" + game.HomeTeamId.ToString(CultureInfo.InvariantCulture),
                        emblemTeamName: ResolveEmblemTeamName(homeKey, focusTeamKey, focusEmblemTeamName)),
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

        private static string ResolveEmblemTeamName(string teamSeasonKey, string focusTeamSeasonKey, string focusEmblemTeamName)
        {
            return string.Equals(teamSeasonKey, focusTeamSeasonKey, StringComparison.Ordinal) ? focusEmblemTeamName : null;
        }

        private static string FormatLeagueTeamDisplayName(
            string teamSeasonKey,
            Func<string, string> teamDisplayNameResolver,
            Func<string, int?> teamOriginYearResolver,
            IDictionary<string, string> teamDisplayNames)
        {
            if (teamDisplayNames.TryGetValue(teamSeasonKey, out string cachedDisplayName))
                return cachedDisplayName;

            string displayName = teamDisplayNameResolver(teamSeasonKey);
            if (teamOriginYearResolver == null)
            {
                teamDisplayNames.Add(teamSeasonKey, displayName);
                return displayName;
            }

            int? originYear = teamOriginYearResolver(teamSeasonKey);
            if (!originYear.HasValue || originYear.Value <= 0)
            {
                teamDisplayNames.Add(teamSeasonKey, displayName);
                return displayName;
            }

            string yearPrefix = originYear.Value.ToString(CultureInfo.InvariantCulture) + " ";
            string formattedDisplayName = displayName.StartsWith(yearPrefix, StringComparison.Ordinal)
                ? displayName
                : yearPrefix + displayName;
            teamDisplayNames.Add(teamSeasonKey, formattedDisplayName);
            return formattedDisplayName;
        }

        /// <summary>현재 시즌 누적 개인 기록을 네 부문 모두 확정해 화면이 부문 전환에서 재계산하지 않게 한다.</summary>
        public OwnerSeasonRecordsPresentationModel CreateSeasonRecords(OwnerModeManager manager, int? seasonNumber = null)
        {
            ManagerHistoricalRuntimeState runtime = RequireRuntime(manager);
            ManagerModeRuntimeState mode = runtime.ManagerMode;
            var seasonNumbers = new int[mode.CompletedSeasons.Count + 1];
            seasonNumbers[0] = mode.LiveSeason.SeasonNumber;
            int selectedIndex = 0;
            for (int index = 0; index < mode.CompletedSeasons.Count; index++)
            {
                seasonNumbers[index + 1] = mode.CompletedSeasons[mode.CompletedSeasons.Count - 1 - index].Season.SeasonNumber;
                if (seasonNumbers[index + 1] == seasonNumber) selectedIndex = index + 1;
            }
            return new OwnerSeasonRecordsPresentationModel(
                new OwnerSeasonRecordsService().Build(
                    runtime,
                    teamSeasonKey => manager.GetClubDisplayName(teamSeasonKey),
                    playerPersonId => runtime.IdentityRegistry.GetPresentationPlayerName(playerPersonId),
                    seasonNumber: seasonNumbers[selectedIndex]),
                seasonNumbers, selectedIndex);
        }

        /// <summary>현재 Save에서 실제로 진행한 구단 시즌 성적만 시즌 이력 표로 만든다.</summary>
        public RecordsScreenSnapshot CreateClubSeasonHistoryRecords(OwnerModeManager manager)
        {
            ManagerHistoricalRuntimeState runtime = RequireRuntime(manager);
            return CreateClubSeasonHistoryRecords(
                runtime.ManagerMode.LiveSeason,
                runtime.ManagerMode.CompletedSeasons,
                runtime.League.Grade,
                manager.GetClubDisplayName(runtime.PlayerTeamSeasonKey));
        }

        /// <summary>현재 시즌과 완료 시즌을 입력받아 사전 생성 역사를 섞지 않는 구단 시즌 이력을 만든다.</summary>
        public RecordsScreenSnapshot CreateClubSeasonHistoryRecords(
            ManagerLiveSeasonState liveSeason,
            IReadOnlyList<ManagerCompletedSeasonState> completedSeasons,
            LeagueGrade liveLeagueGrade,
            string teamDisplayName)
        {
            if (liveSeason == null)
                throw new ArgumentNullException(nameof(liveSeason));
            if (completedSeasons == null)
                throw new ArgumentNullException(nameof(completedSeasons));
            if (!Enum.IsDefined(typeof(LeagueGrade), liveLeagueGrade))
                throw new ArgumentOutOfRangeException(nameof(liveLeagueGrade));

            string displayName = string.IsNullOrWhiteSpace(teamDisplayName)
                ? liveSeason.GetTeamSeasonKey(liveSeason.PlayerTeamId)
                : teamDisplayName.Trim();
            var rows = new List<RecordTableRowModel>(completedSeasons.Count + 1);
            for (int index = 0; index < completedSeasons.Count; index++)
            {
                ManagerCompletedSeasonState completed = completedSeasons[index]
                    ?? throw new ArgumentException("완료 시즌 이력에 null이 있습니다.", nameof(completedSeasons));
                rows.Add(CreateClubSeasonRow(completed.Season, completed.LeagueGrade, displayName, false));
            }
            rows.Add(CreateClubSeasonRow(liveSeason, liveLeagueGrade, displayName, true));

            RecordTableModel table = new RecordTableModel(CreateClubSeasonColumns(), rows)
                .SortBy("Season", RecordSortDirection.Descending);
            string currentRowId = CreateClubSeasonRowId(liveSeason);
            return new RecordsScreenSnapshot(
                "구단 역사",
                OwnerLeagueDisplayNameFormatter.FormatFull(liveLeagueGrade),
                displayName,
                "시즌 성적",
                table,
                "실제 진행 시즌만 표시 · 현재 시즌 포함",
                currentRowId);
        }

        private static ScheduleFocusSide ResolveFocusSide(ScheduledGameState game, int focusTeamId)
        {
            if (game.HomeTeamId == focusTeamId)
                return ScheduleFocusSide.Home;
            if (game.AwayTeamId == focusTeamId)
                return ScheduleFocusSide.Away;
            return ScheduleFocusSide.None;
        }

        private static RecordTableColumnModel[] CreateClubSeasonColumns()
        {
            return new[]
            {
                new RecordTableColumnModel(
                    "Season", "시즌", RecordSortValueKind.Number, true,
                    RecordSortDirection.Descending, 1.65f, RecordCellAlignment.Left),
                new RecordTableColumnModel(
                    "League", "리그", RecordSortValueKind.Text, true,
                    RecordSortDirection.Ascending, 1.35f, RecordCellAlignment.Left),
                new RecordTableColumnModel("Status", "상태", RecordSortValueKind.Text),
                new RecordTableColumnModel("Rank", "순위", RecordSortValueKind.Number),
                new RecordTableColumnModel("Games", "경기", RecordSortValueKind.Number),
                new RecordTableColumnModel("Wins", "승", RecordSortValueKind.Number),
                new RecordTableColumnModel("Losses", "패", RecordSortValueKind.Number),
                new RecordTableColumnModel("Ties", "무", RecordSortValueKind.Number),
                new RecordTableColumnModel("PCT", "승률", RecordSortValueKind.Number),
                new RecordTableColumnModel("RS", "득점", RecordSortValueKind.Number),
                new RecordTableColumnModel("RA", "실점", RecordSortValueKind.Number),
                new RecordTableColumnModel("HR", "홈런", RecordSortValueKind.Number),
                new RecordTableColumnModel("SB", "도루", RecordSortValueKind.Number),
                new RecordTableColumnModel("AVG", "타율", RecordSortValueKind.Number),
                new RecordTableColumnModel("ERA", "평균자책", RecordSortValueKind.Number, true,
                    RecordSortDirection.Ascending, 1.1f)
            };
        }

        private RecordTableRowModel CreateClubSeasonRow(
            ManagerLiveSeasonState season,
            LeagueGrade leagueGrade,
            string teamDisplayName,
            bool isCurrentSeason)
        {
            string teamSeasonKey = season.GetTeamSeasonKey(season.PlayerTeamId);
            ScheduleScreenSnapshot schedule = CreateSchedule(
                season,
                OwnerLeagueDisplayNameFormatter.FormatFull(leagueGrade),
                key => string.Equals(key, teamSeasonKey, StringComparison.Ordinal) ? teamDisplayName : key);
            var league = new OwnerLeaguePresentationModel(schedule);
            OwnerLeaguePresentationModel.TeamRecord team = FindTeamRecord(league, teamSeasonKey);
            TeamBattingPitchingTotals totals = CalculateTeamTotals(
                season.Statistics.RegularSeason,
                season.PlayerTeamId);
            bool hasGames = team.Games > 0;

            return new RecordTableRowModel(
                CreateClubSeasonRowId(season),
                new[]
                {
                    NumberCell(
                        "Season",
                        season.OriginYear.ToString(CultureInfo.InvariantCulture) + " 시즌 " +
                        season.SeasonNumber.ToString(CultureInfo.InvariantCulture) + "년차",
                        season.SeasonNumber),
                    TextCell("League", OwnerLeagueDisplayNameFormatter.FormatFull(leagueGrade)),
                    TextCell("Status", isCurrentSeason ? "진행 중" : "완료"),
                    OptionalNumberCell("Rank", hasGames ? team.Rank.ToString(CultureInfo.InvariantCulture) + "위" : "—", team.Rank, hasGames),
                    NumberCell("Games", team.Games),
                    NumberCell("Wins", team.Wins),
                    NumberCell("Losses", team.Losses),
                    NumberCell("Ties", team.Ties),
                    OptionalNumberCell("PCT", hasGames ? team.Percentage.ToString("0.000", CultureInfo.InvariantCulture) : "—", team.Percentage, hasGames),
                    NumberCell("RS", team.Runs),
                    NumberCell("RA", team.RunsAllowed),
                    NumberCell("HR", totals.HomeRuns),
                    NumberCell("SB", totals.StolenBases),
                    OptionalNumberCell("AVG", totals.AtBats > 0 ? totals.BattingAverage.ToString("0.000", CultureInfo.InvariantCulture) : "—", totals.BattingAverage, totals.AtBats > 0),
                    OptionalNumberCell("ERA", totals.OutsRecorded > 0 ? totals.EarnedRunAverage.ToString("0.00", CultureInfo.InvariantCulture) : "—", totals.EarnedRunAverage, totals.OutsRecorded > 0)
                },
                isCurrentSeason,
                isCurrentSeason ? "현재 진행 중인 시즌" : string.Empty);
        }

        private static OwnerLeaguePresentationModel.TeamRecord FindTeamRecord(
            OwnerLeaguePresentationModel league,
            string teamSeasonKey)
        {
            for (int index = 0; index < league.Standings.Count; index++)
                if (string.Equals(league.Standings[index].Id, teamSeasonKey, StringComparison.Ordinal))
                    return league.Standings[index];
            throw new InvalidOperationException("시즌 일정에 플레이어 구단 기록이 없습니다.");
        }

        private static TeamBattingPitchingTotals CalculateTeamTotals(
            CompetitionStatisticsState competition,
            int teamId)
        {
            var totals = new TeamBattingPitchingTotals();
            foreach (KeyValuePair<int, PlayerCompetitionStatisticsState> pair in competition.Players)
            {
                PlayerCompetitionStatisticsState player = pair.Value;
                PlayerTeamStatisticsSplitState split = player.GetTeamSplit(teamId);
                if (split != null)
                {
                    totals.Add(split.Batting, split.Pitching);
                    continue;
                }

                // TeamSplit 도입 이전 기록은 현재 소속이 일치할 때만 시즌 합계를 안전하게 사용한다.
                if (player.TeamId == teamId)
                    totals.Add(player.Batting, player.Pitching);
            }
            return totals;
        }

        private static string CreateClubSeasonRowId(ManagerLiveSeasonState season)
        {
            return "club-season:" + season.SeasonId;
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

        private static RecordTableCellModel NumberCell(string columnId, string displayValue, double value)
        {
            return new RecordTableCellModel(columnId, displayValue, RecordSortValue.FromNumber(value));
        }

        private static RecordTableCellModel OptionalNumberCell(
            string columnId,
            string displayValue,
            double value,
            bool hasValue)
        {
            return new RecordTableCellModel(
                columnId,
                displayValue,
                hasValue ? RecordSortValue.FromNumber(value) : RecordSortValue.Empty());
        }

        private sealed class TeamBattingPitchingTotals
        {
            public int AtBats { get; private set; }
            public int Hits { get; private set; }
            public int HomeRuns { get; private set; }
            public int StolenBases { get; private set; }
            public int OutsRecorded { get; private set; }
            public int EarnedRuns { get; private set; }
            public double BattingAverage => AtBats == 0 ? 0d : Hits / (double)AtBats;
            public double EarnedRunAverage => OutsRecorded == 0 ? 0d : EarnedRuns * 27d / OutsRecorded;

            public void Add(BattingStatisticsState batting, PitchingStatisticsState pitching)
            {
                AtBats += batting.AtBats;
                Hits += batting.Hits;
                HomeRuns += batting.HomeRuns;
                StolenBases += batting.StolenBases;
                OutsRecorded += pitching.OutsRecorded;
                EarnedRuns += pitching.EarnedRuns;
            }
        }

        private static ManagerHistoricalRuntimeState RequireRuntime(OwnerModeManager manager)
        {
            if (manager == null)
                throw new ArgumentNullException(nameof(manager));
            if (!manager.HasActiveRuntime || manager.Runtime == null || !manager.Runtime.HasManagerMode)
                throw new InvalidOperationException("활성 구단주 진행 정보가 필요합니다.");
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

        /// <summary>읽기 전용 Owner 일정·구단 시즌 이력 화면에는 Action을 공급하지 않는다.</summary>
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
