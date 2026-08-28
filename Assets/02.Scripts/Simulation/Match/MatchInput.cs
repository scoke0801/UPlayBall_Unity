using System;
using Baseball.Core.Teams;

namespace Baseball.Simulation.Match
{
    /// <summary>
    /// 동일 Seed 재현에 필요한 경기 식별자와 두 팀 입력을 불변으로 보관한다.
    /// </summary>
    public sealed class MatchInput
    {
        /// <summary>
        /// 한 경기의 잠금된 입력을 생성한다.
        /// </summary>
        public MatchInput(int seasonId, int gameId, ulong randomSeed, Team awayTeam, Team homeTeam)
            : this(seasonId, gameId, randomSeed, awayTeam, homeTeam, requiresWinner: false)
        {
        }

        /// <summary>
        /// 정규 시즌 무승부 허용 여부까지 포함해 한 경기의 잠금된 입력을 생성한다.
        /// </summary>
        public MatchInput(
            int seasonId,
            int gameId,
            ulong randomSeed,
            Team awayTeam,
            Team homeTeam,
            bool requiresWinner)
        {
            if (seasonId <= 0)
                throw new ArgumentOutOfRangeException(nameof(seasonId));
            if (gameId <= 0)
                throw new ArgumentOutOfRangeException(nameof(gameId));

            SeasonId = seasonId;
            GameId = gameId;
            RandomSeed = randomSeed;
            AwayTeam = awayTeam ?? throw new ArgumentNullException(nameof(awayTeam));
            HomeTeam = homeTeam ?? throw new ArgumentNullException(nameof(homeTeam));
            RequiresWinner = requiresWinner;

            if (AwayTeam.TeamId == HomeTeam.TeamId)
                throw new ArgumentException("서로 다른 두 팀이 경기해야 합니다.", nameof(homeTeam));
        }

        public int SeasonId { get; }
        public int GameId { get; }
        public ulong RandomSeed { get; }
        public Team AwayTeam { get; }
        public Team HomeTeam { get; }
        public bool RequiresWinner { get; }
    }
}
