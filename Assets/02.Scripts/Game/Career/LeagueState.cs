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
        private readonly int[] _teamIds;
        private readonly LeagueSeasonSummaryState[] _completedSeasonSummaries;

        /// <summary>
        /// 새 게임에서 확정된 리그 상태를 생성한다.
        /// </summary>
        public LeagueState(int saveVersion, int leagueYear, ulong randomSeed, IReadOnlyList<TeamState> teams)
            : this(
                saveVersion,
                LeagueId.RookieMain,
                LeagueLevel.Rookie,
                "Standard",
                leagueYear,
                randomSeed,
                teams,
                currentSeason: null)
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
            SeasonState currentSeason,
            IReadOnlyList<LeagueSeasonSummaryState> completedSeasonSummaries = null)
            : this(
                saveVersion,
                LeagueId.FromLevel(currentSeason?.LeagueLevel ?? LeagueLevel.Rookie),
                currentSeason?.LeagueLevel ?? LeagueLevel.Rookie,
                "Standard",
                leagueYear,
                randomSeed,
                teams,
                currentSeason,
                completedSeasonSummaries)
        {
        }

        /// <summary>
        /// 영구 리그 ID와 경쟁 단계를 분리해 리그 런타임 상태를 생성한다.
        /// </summary>
        public LeagueState(
            int saveVersion,
            LeagueId leagueId,
            LeagueLevel leagueLevel,
            string leagueRulesetId,
            int leagueYear,
            ulong randomSeed,
            IReadOnlyList<TeamState> teams,
            SeasonState currentSeason,
            IReadOnlyList<LeagueSeasonSummaryState> completedSeasonSummaries = null,
            int competitionOverallBonus = 0)
        {
            if (!leagueId.IsAssigned)
                throw new System.ArgumentException("리그에는 영구 LeagueId가 필요합니다.", nameof(leagueId));
            if (string.IsNullOrWhiteSpace(leagueRulesetId))
                throw new System.ArgumentException("LeagueRulesetId는 비어 있을 수 없습니다.", nameof(leagueRulesetId));
            if (currentSeason != null && currentSeason.LeagueLevel != leagueLevel)
                throw new System.InvalidOperationException("리그 단계와 시즌 단계가 다릅니다.");
            if (competitionOverallBonus < 0)
                throw new System.ArgumentOutOfRangeException(nameof(competitionOverallBonus));

            SaveVersion = saveVersion;
            LeagueId = leagueId;
            LeagueLevel = leagueLevel;
            LeagueRulesetId = leagueRulesetId;
            LeagueYear = leagueYear;
            RandomSeed = randomSeed;
            CompetitionOverallBonus = competitionOverallBonus;
            if (teams == null)
                throw new System.ArgumentNullException(nameof(teams));
            _teams = new TeamState[teams.Count];
            _teamIds = new int[teams.Count];
            for (int index = 0; index < teams.Count; index++)
            {
                TeamState team = teams[index];
                _teams[index] = team.LeagueId.IsAssigned ? team : team.WithLeague(leagueId);
                if (_teams[index].LeagueId != leagueId)
                    throw new System.InvalidOperationException($"TeamId {team.TeamId}의 LeagueId가 리그와 다릅니다.");
                _teamIds[index] = team.TeamId;
            }
            CurrentSeason = currentSeason;
            _completedSeasonSummaries = CopySeasonSummaries(completedSeasonSummaries);
        }

        public int SaveVersion { get; }
        public LeagueId LeagueId { get; }
        public LeagueLevel LeagueLevel { get; }
        public string LeagueRulesetId { get; }
        public int LeagueYear { get; }
        public ulong RandomSeed { get; }
        public int CompetitionOverallBonus { get; }
        public IReadOnlyList<TeamState> Teams => _teams;
        public IReadOnlyList<int> TeamIds => _teamIds;
        public SeasonState CurrentSeason { get; }
        public IReadOnlyList<LeagueSeasonSummaryState> CompletedSeasonSummaries =>
            _completedSeasonSummaries;

        /// <summary>현재 완료 시즌을 역사에 추가하고 다음 시즌 상태로 교체한 새 리그를 만든다.</summary>
        public LeagueState CreateNextSeason(
            int saveVersion,
            int nextYear,
            IReadOnlyList<TeamState> nextTeams,
            SeasonState nextSeason)
        {
            if (CurrentSeason?.Phase != SeasonPhase.Completed)
                throw new System.InvalidOperationException("현재 시즌을 보관 완료한 뒤 다음 시즌을 만들 수 있습니다.");
            if (nextSeason == null || nextSeason.Year != nextYear || nextSeason.LeagueLevel != LeagueLevel)
                throw new System.InvalidOperationException("다음 시즌의 연도나 리그 단계가 잘못되었습니다.");

            var summaries = new LeagueSeasonSummaryState[_completedSeasonSummaries.Length + 1];
            System.Array.Copy(_completedSeasonSummaries, summaries, _completedSeasonSummaries.Length);
            summaries[^1] = LeagueSeasonSummaryState.Create(this);
            return new LeagueState(
                saveVersion,
                LeagueId,
                LeagueLevel,
                LeagueRulesetId,
                nextYear,
                RandomSeed,
                nextTeams,
                nextSeason,
                summaries,
                CompetitionOverallBonus);
        }

        /// <summary>
        /// 트레이드로 바뀐 두 구단의 로스터 상태를 TeamId 위치에 원자적으로 교체한다.
        /// </summary>
        public void ReplaceTeams(TeamState first, TeamState second)
        {
            if (first == null || second == null || first.TeamId == second.TeamId)
                throw new System.ArgumentException("서로 다른 두 구단 상태가 필요합니다.");
            if (first.LeagueId != LeagueId || second.LeagueId != LeagueId)
                throw new System.InvalidOperationException("다른 리그의 구단으로 현재 리그를 바꿀 수 없습니다.");
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

        private static LeagueSeasonSummaryState[] CopySeasonSummaries(
            IReadOnlyList<LeagueSeasonSummaryState> source)
        {
            if (source == null || source.Count == 0)
                return System.Array.Empty<LeagueSeasonSummaryState>();
            var result = new LeagueSeasonSummaryState[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                result[index] = source[index] ??
                                throw new System.ArgumentException("null 시즌 요약이 있습니다.", nameof(source));
            }
            return result;
        }
    }
}
