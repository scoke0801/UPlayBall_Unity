using System.Collections.Generic;

namespace Baseball.Game.Career
{
    /// <summary>
    /// 세이브 대상이 되는 리그의 커리어 런타임 상태다. 동일 RandomSeed로 재생성 가능한
    /// 구단 목록을 저장 시점의 실제 상태로 고정해 보관한다.
    /// </summary>
    public sealed class LeagueState
    {
        /// <summary>
        /// 새 게임에서 확정된 리그 상태를 생성한다.
        /// </summary>
        public LeagueState(int saveVersion, int leagueYear, ulong randomSeed, IReadOnlyList<TeamState> teams)
            : this(saveVersion, leagueYear, randomSeed, teams, currentSeason: null)
        {
        }

        /// <summary>
        /// 현재 시즌 상태를 포함한 리그 런타임 상태를 생성한다.
        /// </summary>
        public LeagueState(
            int saveVersion,
            int leagueYear,
            ulong randomSeed,
            IReadOnlyList<TeamState> teams,
            SeasonState currentSeason)
        {
            SaveVersion = saveVersion;
            LeagueYear = leagueYear;
            RandomSeed = randomSeed;
            Teams = teams;
            CurrentSeason = currentSeason;
        }

        public int SaveVersion { get; }
        public int LeagueYear { get; }
        public ulong RandomSeed { get; }
        public IReadOnlyList<TeamState> Teams { get; }
        public SeasonState CurrentSeason { get; }
    }
}
