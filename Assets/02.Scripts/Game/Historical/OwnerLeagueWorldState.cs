using System;
using System.Collections.Generic;
using Baseball.Core.Historical;

namespace Baseball.Game.Historical
{
    /// <summary>한 조의 소속과 실제 경기 결과를 함께 보관한다.</summary>
    public sealed class OwnerLeagueGroupState
    {
        public OwnerLeagueGroupState(LeagueInstance league, ManagerLiveSeasonState season,
            OwnerPostseasonState postseason = null)
        {
            League = league ?? throw new ArgumentNullException(nameof(league));
            Season = season ?? throw new ArgumentNullException(nameof(season));
            if (league.ParticipantTeamCount != season.Teams.Count)
                throw new ArgumentException("조 소속과 시즌 참가팀 수가 다릅니다.");
            var keys = new HashSet<string>(league.RegularTeamSeasonKeys, StringComparer.Ordinal);
            foreach (var special in league.SpecialCompositeTeams) keys.Add(special.TeamSeasonKey);
            foreach (string filler in league.FillerTeamSeasonKeys) keys.Add(filler);
            foreach (var team in season.Teams)
                if (!keys.Remove(team.TeamSeasonKey)) throw new ArgumentException("조 소속과 일정 구단이 다릅니다.");
            if (postseason != null && !string.Equals(postseason.SeasonId, season.SeasonId, StringComparison.Ordinal))
                throw new ArgumentException("포스트시즌과 정규시즌의 SeasonId가 다릅니다.", nameof(postseason));
            Postseason = postseason;
        }

        public LeagueInstance League { get; }
        public ManagerLiveSeasonState Season { get; }
        public OwnerPostseasonState Postseason { get; private set; }

        /// <summary>정규시즌 종료 뒤 이 조의 포스트시즌 상태를 한 번만 연결한다.</summary>
        internal void SetPostseason(OwnerPostseasonState postseason)
        {
            if (Postseason != null) throw new InvalidOperationException("포스트시즌이 이미 생성됐습니다.");
            if (postseason == null || !string.Equals(postseason.SeasonId, Season.SeasonId, StringComparison.Ordinal))
                throw new ArgumentException("현재 시즌의 포스트시즌이 필요합니다.", nameof(postseason));
            Postseason = postseason;
        }
    }

    /// <summary>모든 연도별 구단의 소속 조·시즌과 로스터를 영속적으로 보관한다.</summary>
    public sealed class OwnerLeagueWorldState
    {
        private readonly List<CurrentRosterState> _rosters;
        private readonly Dictionary<string, ManagerTeamReference> _teams = new Dictionary<string, ManagerTeamReference>(StringComparer.Ordinal);
        public OwnerLeagueWorldState(IReadOnlyList<OwnerLeagueGroupState> groups, IReadOnlyList<CurrentRosterState> rosters,
            IReadOnlyList<OwnerLeagueGroupState> completedGroups = null)
        {
            if (groups == null || groups.Count == 0) throw new ArgumentException("월드 조가 필요합니다.", nameof(groups));
            if (rosters == null) throw new ArgumentNullException(nameof(rosters));
            var sorted = new List<OwnerLeagueGroupState>(groups);
            foreach (var group in sorted) if (group == null) throw new ArgumentException("월드 조가 누락됐습니다.");
            sorted.Sort((a, b) =>
            {
                int order = a.League.Grade.CompareTo(b.League.Grade);
                return order != 0 ? order : string.CompareOrdinal(a.League.LeagueInstanceId, b.League.LeagueInstanceId);
            });
            var ids = new HashSet<int>();
            var groupIds = new HashSet<string>(StringComparer.Ordinal);
            int seasonNumber = sorted[0].Season.SeasonNumber;
            foreach (var group in sorted)
            {
                if (!groupIds.Add(group.League.LeagueInstanceId) || group.Season.SeasonNumber != seasonNumber)
                    throw new ArgumentException("월드 조 ID 또는 시즌 번호가 올바르지 않습니다.");
                foreach (var team in group.Season.Teams)
                {
                    if (!ids.Add(team.TeamId) || !_teams.TryAdd(team.TeamSeasonKey, team))
                        throw new ArgumentException("월드 구단 ID와 소속은 유일해야 합니다.");
                }
            }
            var copied = new List<CurrentRosterState>(rosters);
            copied.Sort((a, b) => string.CompareOrdinal(a.TeamSeasonKey, b.TeamSeasonKey));
            var rosterKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var roster in copied)
                if (!_teams.ContainsKey(roster.TeamSeasonKey) || !rosterKeys.Add(roster.TeamSeasonKey))
                    throw new ArgumentException("월드 로스터의 소속이 올바르지 않습니다.");
            if (rosterKeys.Count != _teams.Count) throw new ArgumentException("월드 구단 로스터가 누락됐습니다.");
            Groups = sorted.AsReadOnly();
            _rosters = copied;
            Rosters = _rosters.AsReadOnly();
            var history = completedGroups == null ? new List<OwnerLeagueGroupState>() : new List<OwnerLeagueGroupState>(completedGroups);
            foreach (var group in history)
                if (group == null || !group.Season.IsCompleted || group.Season.SeasonNumber >= seasonNumber)
                    throw new ArgumentException("마감 조 이력이 올바르지 않습니다.");
            history.Sort((a, b) =>
            {
                int order = a.Season.SeasonNumber.CompareTo(b.Season.SeasonNumber);
                return order != 0 ? order : string.CompareOrdinal(a.League.LeagueInstanceId, b.League.LeagueInstanceId);
            });
            CompletedGroups = history.AsReadOnly();
        }

        public IReadOnlyList<OwnerLeagueGroupState> Groups { get; }
        public IReadOnlyList<CurrentRosterState> Rosters { get; }
        public IReadOnlyList<OwnerLeagueGroupState> CompletedGroups { get; }
        /// <summary>1군 등록 변경 결과를 경기 ID 원장·저장이 읽는 월드 로스터에도 반영한다.</summary>
        internal void ReplaceRoster(CurrentRosterState roster)
        {
            if (roster == null) throw new ArgumentNullException(nameof(roster));
            for (int index = 0; index < _rosters.Count; index++)
            {
                if (!string.Equals(_rosters[index].TeamSeasonKey, roster.TeamSeasonKey, StringComparison.Ordinal))
                    continue;
                _rosters[index] = roster;
                return;
            }
            throw new KeyNotFoundException(roster.TeamSeasonKey);
        }
        internal ManagerModeMatchService.PlayerIdMap PlayerIds { get; set; }
        public bool IsRegularSeasonCompleted
        {
            get { foreach (var group in Groups) if (!group.Season.IsCompleted) return false; return true; }
        }
        public bool IsPostseasonCompleted
        {
            get { foreach (var group in Groups) if (group.Postseason == null || !group.Postseason.IsCompleted) return false; return true; }
        }
        public bool IsCompleted => IsRegularSeasonCompleted && IsPostseasonCompleted;

        /// <summary>소속 조가 바뀌어도 유지되는 구단 식별자를 반환한다.</summary>
        public ManagerTeamReference GetTeam(string key) => _teams.TryGetValue(key, out var team)
            ? team : throw new KeyNotFoundException(key);

        /// <summary>현재 구단이 소속된 조를 반환한다.</summary>
        public OwnerLeagueGroupState GetGroup(string key)
        {
            foreach (var group in Groups)
                foreach (var team in group.Season.Teams)
                    if (string.Equals(team.TeamSeasonKey, key, StringComparison.Ordinal)) return group;
            throw new KeyNotFoundException(key);
        }

        /// <summary>경기 구단의 실제 소속 등급을 AI 정책 입력에 제공한다.</summary>
        public LeagueGrade GetGrade(int teamId)
        {
            foreach (var group in Groups)
                foreach (var team in group.Season.Teams)
                    if (team.TeamId == teamId) return group.League.Grade;
            throw new KeyNotFoundException(teamId.ToString());
        }
    }
}
