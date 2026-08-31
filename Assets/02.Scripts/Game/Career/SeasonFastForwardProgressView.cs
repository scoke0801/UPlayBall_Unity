using System;

namespace Baseball.Game.Career
{
    /// <summary>진행 화면이 변경 중인 CareerState를 직접 읽지 않도록 확정 값만 복사한다.</summary>
    public readonly struct SeasonFastForwardProgressView
    {
        public SeasonFastForwardProgressView(
            SeasonFastForwardStepResult progress,
            DateTime currentDate,
            string playerName,
            string teamName,
            int teamRank,
            int teamWins,
            int teamLosses,
            int teamTies,
            PlayerSeasonStatisticsView statistics,
            string latestNewsHeadline)
        {
            Progress = progress;
            CurrentDate = currentDate.Date;
            PlayerName = playerName ?? string.Empty;
            TeamName = teamName ?? string.Empty;
            TeamRank = teamRank;
            TeamWins = teamWins;
            TeamLosses = teamLosses;
            TeamTies = teamTies;
            Statistics = statistics;
            LatestNewsHeadline = latestNewsHeadline ?? string.Empty;
        }

        public SeasonFastForwardStepResult Progress { get; }
        public DateTime CurrentDate { get; }
        public string PlayerName { get; }
        public string TeamName { get; }
        public int TeamRank { get; }
        public int TeamWins { get; }
        public int TeamLosses { get; }
        public int TeamTies { get; }
        public PlayerSeasonStatisticsView Statistics { get; }
        public string LatestNewsHeadline { get; }
    }
}
