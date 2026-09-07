using System;
using System.Collections.Generic;
using System.Globalization;
using Baseball.Game.Career;
using Baseball.Core.Historical;

namespace Baseball.Game.Historical
{
    /// <summary>구단주 기록 화면 한 부문의 확정된 리더보드다.</summary>
    public sealed class OwnerSeasonRecordsCategoryView
    {
        public OwnerSeasonRecordsCategoryView(
            CareerRecordCategory category,
            string displayName,
            CareerRecordMetric[] columns,
            CareerRecordLeaderboardRow[] leaderboard,
            int qualifiedPlayerCount,
            string qualificationText)
        {
            Category = category;
            DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
            Columns = columns ?? throw new ArgumentNullException(nameof(columns));
            Leaderboard = leaderboard ?? throw new ArgumentNullException(nameof(leaderboard));
            QualifiedPlayerCount = qualifiedPlayerCount;
            QualificationText = qualificationText ?? string.Empty;
        }

        public CareerRecordCategory Category { get; }
        public string DisplayName { get; }
        public CareerRecordMetric[] Columns { get; }
        public CareerRecordLeaderboardRow[] Leaderboard { get; }
        public int QualifiedPlayerCount { get; }
        public string QualificationText { get; }
        public CareerRecordMetric PrimaryMetric => Columns[0];
    }

    /// <summary>기록 화면 한 번의 표시에 필요한 네 부문 리더보드를 함께 확정한다.</summary>
    public sealed class OwnerSeasonRecordsView
    {
        public OwnerSeasonRecordsView(
            string seasonLabel,
            string leagueLabel,
            int playerTeamId,
            bool hasAnyRecord,
            IReadOnlyList<OwnerSeasonRecordsCategoryView> categories)
        {
            SeasonLabel = seasonLabel ?? string.Empty;
            LeagueLabel = leagueLabel ?? string.Empty;
            PlayerTeamId = playerTeamId;
            HasAnyRecord = hasAnyRecord;
            Categories = categories ?? throw new ArgumentNullException(nameof(categories));
        }

        public string SeasonLabel { get; }
        public string LeagueLabel { get; }
        public int PlayerTeamId { get; }
        public bool HasAnyRecord { get; }
        public IReadOnlyList<OwnerSeasonRecordsCategoryView> Categories { get; }
    }

    /// <summary>
    /// 구단주 모드의 현재 시즌 누적 기록을 선수 커리어 모드와 같은 규정·지표·순위 규칙으로 화면 모델에 투영한다.
    /// </summary>
    public sealed class OwnerSeasonRecordsService
    {
        private static readonly CareerRecordCategory[] Categories =
        {
            CareerRecordCategory.Batting,
            CareerRecordCategory.Pitching,
            CareerRecordCategory.Fielding,
            CareerRecordCategory.Baserunning
        };

        private static readonly string[] CategoryNames = { "타격", "투구", "수비", "주루" };

        /// <summary>카드의 현재 정규시즌 기록을 경기 집계와 동일한 선수 ID로 조회한다.</summary>
        public static PlayerCompetitionStatisticsState GetCurrentPlayerRecord(
            ManagerHistoricalRuntimeState runtime, string teamSeasonKey, string playerSeasonId)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (!runtime.HasManagerMode) return null;
            // 카드 연도의 역사 기록은 현재 운영 시즌에 출전한 기록을 대신할 수 없다.
            ManagerModeMatchService.PlayerIdMap ids = ManagerModeMatchService.PlayerIdMap.Create(runtime.Rosters);
            return ids.TryGet(teamSeasonKey, playerSeasonId, out int playerId)
                ? runtime.ManagerMode.LiveSeason.Statistics.RegularSeason.GetPlayer(playerId)
                : null;
        }

        /// <summary>네 부문 모두를 한 번에 확정해 화면이 부문 전환에서 다시 계산하지 않게 한다.</summary>
        public OwnerSeasonRecordsView Build(
            ManagerHistoricalRuntimeState runtime,
            Func<string, string> getTeamDisplayName,
            int limit = LeagueLeaderboardService.DefaultLeaderboardLimit,
            int? seasonNumber = null)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (getTeamDisplayName == null) throw new ArgumentNullException(nameof(getTeamDisplayName));
            if (!runtime.HasManagerMode)
                throw new InvalidOperationException("ManagerMode 상태가 없는 Runtime은 기록을 만들 수 없습니다.");

            ManagerLiveSeasonState season = runtime.ManagerMode.LiveSeason;
            LeagueGrade grade = runtime.League.Grade;
            if (seasonNumber.HasValue && seasonNumber.Value != season.SeasonNumber)
            {
                ManagerCompletedSeasonState completed = FindCompletedSeason(runtime.ManagerMode, seasonNumber.Value);
                season = completed.Season;
                grade = completed.LeagueGrade;
            }
            CompetitionStatisticsState competition = season.Statistics.RegularSeason;
            int playerTeamId = season.PlayerTeamId;

            var categories = new OwnerSeasonRecordsCategoryView[Categories.Length];
            for (int index = 0; index < Categories.Length; index++)
            {
                categories[index] = BuildCategory(
                    season,
                    competition,
                    Categories[index],
                    CategoryNames[index],
                    playerTeamId,
                    getTeamDisplayName,
                    limit);
            }

            return new OwnerSeasonRecordsView(
                season.OriginYear.ToString(CultureInfo.InvariantCulture) + " 시즌 " +
                season.SeasonNumber.ToString(CultureInfo.InvariantCulture) + "년차",
                FormatLeagueName(grade),
                playerTeamId,
                competition.Players.Count > 0,
                categories);
        }

        private static ManagerCompletedSeasonState FindCompletedSeason(ManagerModeRuntimeState mode, int seasonNumber)
        {
            for (int index = 0; index < mode.CompletedSeasons.Count; index++)
                if (mode.CompletedSeasons[index].Season.SeasonNumber == seasonNumber) return mode.CompletedSeasons[index];
            throw new ArgumentException("저장된 기록이 없는 시즌입니다.", nameof(seasonNumber));
        }

        private static string FormatLeagueName(LeagueGrade grade) => grade switch
        {
            LeagueGrade.Rookie => "루키 리그",
            LeagueGrade.Minor => "마이너 리그",
            LeagueGrade.Major => "메이저 리그",
            LeagueGrade.World => "월드 리그",
            LeagueGrade.AllStar => "올스타 리그",
            LeagueGrade.Classic => "클래식 리그",
            LeagueGrade.Winners => "위너스 리그",
            LeagueGrade.Champion => "챔피언 리그",
            LeagueGrade.Master => "마스터 리그",
            LeagueGrade.Galaxy => "갤럭시 리그",
            _ => "리그 정보 없음"
        };

        private static OwnerSeasonRecordsCategoryView BuildCategory(
            ManagerLiveSeasonState season,
            CompetitionStatisticsState competition,
            CareerRecordCategory category,
            string displayName,
            int playerTeamId,
            Func<string, string> getTeamDisplayName,
            int limit)
        {
            CareerRecordMetric[] columns = LeagueLeaderboardService.GetBasicColumns(category);
            List<PlayerCompetitionStatisticsState> qualified = LeagueLeaderboardService.CollectQualifiedPlayers(
                competition,
                category,
                CompetitionScope.RegularSeason,
                season.GetCompletedGameCount);
            LeagueLeaderboardService.SortByMetric(qualified, columns[0]);
            CareerRecordLeaderboardRow[] leaderboard = LeagueLeaderboardService.BuildLeaderboard(
                qualified,
                columns,
                teamId => getTeamDisplayName(season.GetTeamSeasonKey(teamId)),
                player => player.TeamId == playerTeamId,
                limit);

            return new OwnerSeasonRecordsCategoryView(
                category,
                displayName,
                columns,
                leaderboard,
                qualified.Count,
                competition.Players.Count == 0
                    ? "아직 진행한 경기가 없습니다."
                    : "규정 충족 " + qualified.Count.ToString(CultureInfo.InvariantCulture) + "명");
        }
    }
}
