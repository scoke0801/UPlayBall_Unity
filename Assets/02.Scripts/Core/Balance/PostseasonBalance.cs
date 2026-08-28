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
            int semifinalSeriesGames,
            int championshipSeriesGames)
        {
            if (playoffTeamCount != 4)
                throw new ArgumentOutOfRangeException(nameof(playoffTeamCount), "4강 토너먼트는 4팀만 지원한다.");
            ValidateSeriesLength(semifinalSeriesGames, nameof(semifinalSeriesGames));
            ValidateSeriesLength(championshipSeriesGames, nameof(championshipSeriesGames));

            PlayoffTeamCount = playoffTeamCount;
            SemifinalSeriesGames = semifinalSeriesGames;
            ChampionshipSeriesGames = championshipSeriesGames;
        }

        public int PlayoffTeamCount { get; }
        public int SemifinalSeriesGames { get; }
        public int ChampionshipSeriesGames { get; }

        /// <summary>
        /// 준결승 3전 2선승, 결승 5전 3선승의 4강 토너먼트 값을 만든다.
        /// </summary>
        public static PostseasonBalance CreateDefault()
        {
            return new PostseasonBalance(
                playoffTeamCount: 4,
                semifinalSeriesGames: 3,
                championshipSeriesGames: 5);
        }

        private static void ValidateSeriesLength(int games, string parameterName)
        {
            if (games <= 0 || games % 2 == 0)
                throw new ArgumentOutOfRangeException(parameterName, games, "시리즈 경기 수는 홀수여야 한다.");
        }
    }
}
