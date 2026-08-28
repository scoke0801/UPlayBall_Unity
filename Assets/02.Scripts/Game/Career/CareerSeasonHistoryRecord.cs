namespace Baseball.Game.Career
{
    /// <summary>
    /// 완료된 한 시즌의 소속 구단·팀 성적·개인 기록 스냅샷을 커리어 이력으로 보관한다.
    /// </summary>
    public readonly struct CareerSeasonHistoryRecord
    {
        public CareerSeasonHistoryRecord(
            int year,
            LeagueLevel leagueLevel,
            int teamId,
            string teamName,
            TeamSeasonRecordState teamRecord,
            PlayerSeasonStatisticsState statistics)
            : this(
                year,
                leagueLevel,
                teamId,
                teamName,
                teamRecord,
                statistics,
                postseasonStatistics: null,
                postseason: null,
                awards: null,
                settlement: null)
        {
        }

        public CareerSeasonHistoryRecord(
            int year,
            LeagueLevel leagueLevel,
            int teamId,
            string teamName,
            TeamSeasonRecordState teamRecord,
            PlayerSeasonStatisticsState statistics,
            PlayerSeasonStatisticsState postseasonStatistics,
            PostseasonState postseason,
            SeasonAwardsState awards,
            SeasonSettlementState settlement)
        {
            Year = year;
            LeagueLevel = leagueLevel;
            TeamId = teamId;
            TeamName = teamName;
            TeamRecord = teamRecord;
            Statistics = statistics;
            PostseasonStatistics = postseasonStatistics;
            Postseason = postseason;
            Awards = awards;
            Settlement = settlement;
        }

        public int Year { get; }
        public LeagueLevel LeagueLevel { get; }
        public int TeamId { get; }
        public string TeamName { get; }
        public TeamSeasonRecordState TeamRecord { get; }
        public PlayerSeasonStatisticsState Statistics { get; }
        public PlayerSeasonStatisticsState PostseasonStatistics { get; }
        public PostseasonState Postseason { get; }
        public SeasonAwardsState Awards { get; }
        public SeasonSettlementState Settlement { get; }
    }
}
