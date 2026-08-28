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
        {
            Year = year;
            LeagueLevel = leagueLevel;
            TeamId = teamId;
            TeamName = teamName;
            TeamRecord = teamRecord;
            Statistics = statistics;
        }

        public int Year { get; }
        public LeagueLevel LeagueLevel { get; }
        public int TeamId { get; }
        public string TeamName { get; }
        public TeamSeasonRecordState TeamRecord { get; }
        public PlayerSeasonStatisticsState Statistics { get; }
    }
}
