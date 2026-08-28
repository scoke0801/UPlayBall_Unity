using System;

namespace Baseball.Core.Balance
{
    /// <summary>
    /// 정규 시즌 종료 후 진행하는 상위 4팀 토너먼트의 시리즈 길이를 보관한다.
    /// </summary>
    public readonly struct PostseasonBalance
    {
        public PostseasonBalance(
            int playoffTeamCount,
            int wildCardSeriesGames,
            int playoffSeriesGames,
            int championshipSeriesGames,
            int maximumTieReplays)
        {
            if (playoffTeamCount != 4)
                throw new ArgumentOutOfRangeException(nameof(playoffTeamCount), "계단식 대진은 4팀만 지원한다.");
            ValidateSeriesLength(wildCardSeriesGames, nameof(wildCardSeriesGames));
            ValidateSeriesLength(playoffSeriesGames, nameof(playoffSeriesGames));
            ValidateSeriesLength(championshipSeriesGames, nameof(championshipSeriesGames));
            if (maximumTieReplays < 0)
                throw new ArgumentOutOfRangeException(nameof(maximumTieReplays));

            PlayoffTeamCount = playoffTeamCount;
            WildCardSeriesGames = wildCardSeriesGames;
            PlayoffSeriesGames = playoffSeriesGames;
            ChampionshipSeriesGames = championshipSeriesGames;
            MaximumTieReplays = maximumTieReplays;
        }

        public int PlayoffTeamCount { get; }
        public int WildCardSeriesGames { get; }
        public int PlayoffSeriesGames { get; }
        public int ChampionshipSeriesGames { get; }
        public int SemifinalSeriesGames => WildCardSeriesGames;

        /// <summary>
        /// 무승부는 승수로 세지 않고 재경기하므로, 시리즈가 끝나지 않는 것을 막는 재경기 상한이다.
        /// </summary>
        public int MaximumTieReplays { get; }

        /// <summary>
        /// 준결승 3전 2선승, 결승 5전 3선승의 4강 토너먼트 값을 만든다.
        /// </summary>
        public static PostseasonBalance CreateDefault()
        {
            return new PostseasonBalance(
                playoffTeamCount: 4,
                wildCardSeriesGames: 3,
                playoffSeriesGames: 3,
                championshipSeriesGames: 5,
                maximumTieReplays: 0);
        }

        private static void ValidateSeriesLength(int games, string parameterName)
        {
            if (games <= 0 || games % 2 == 0)
                throw new ArgumentOutOfRangeException(parameterName, games, "시리즈 경기 수는 홀수여야 한다.");
        }
    }
}
