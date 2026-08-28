using System.Collections.Generic;

namespace Baseball.Game.Career
{
    /// <summary>
    /// 세이브 대상이 되는 리그의 커리어 런타임 상태다. 동일 RandomSeed로 재생성 가능한
    /// 구단 목록을 저장 시점의 실제 상태로 고정해 보관한다.
    /// </summary>
    public sealed class LeagueState
    {
        private readonly TeamState[] _teams;

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
            if (teams == null)
                throw new System.ArgumentNullException(nameof(teams));
            _teams = new TeamState[teams.Count];
            for (int index = 0; index < teams.Count; index++)
                _teams[index] = teams[index];
            CurrentSeason = currentSeason;
        }

        public int SaveVersion { get; }
        public int LeagueYear { get; }
        public ulong RandomSeed { get; }
        public IReadOnlyList<TeamState> Teams => _teams;
        public SeasonState CurrentSeason { get; }

        /// <summary>
        /// 트레이드로 바뀐 두 구단의 로스터 상태를 TeamId 위치에 원자적으로 교체한다.
        /// </summary>
        public void ReplaceTeams(TeamState first, TeamState second)
        {
            if (first == null || second == null || first.TeamId == second.TeamId)
                throw new System.ArgumentException("서로 다른 두 구단 상태가 필요합니다.");
            int firstIndex = FindTeamIndex(first.TeamId);
            int secondIndex = FindTeamIndex(second.TeamId);
            _teams[firstIndex] = first;
            _teams[secondIndex] = second;
        }

        private int FindTeamIndex(int teamId)
        {
            for (int index = 0; index < _teams.Length; index++)
            {
                if (_teams[index].TeamId == teamId)
                    return index;
            }
            throw new System.InvalidOperationException($"TeamId {teamId}를 찾을 수 없습니다.");
        }
    }
}
