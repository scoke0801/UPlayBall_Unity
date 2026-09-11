using System;
using System.Collections.Generic;
using System.Globalization;
using Baseball.Presentation.SharedScreens;
using Baseball.Simulation.Historical;

namespace Baseball.Presentation.Owner
{
    /// <summary>완료 대진에서 현재 순위, 상대 전적과 라운드별 순위 이력을 재구성한다.</summary>
    public sealed class OwnerLeaguePresentationModel
    {
        /// <summary>구단 하나의 현재 시즌 누적 기록이다.</summary>
        public sealed class TeamRecord
        {
            private readonly List<int> _rankHistory = new List<int>();
            public string Id { get; internal set; }
            public string Name { get; internal set; }
            public int EmblemId { get; internal set; }
            public int Rank { get; internal set; }
            public int Wins { get; internal set; }
            public int Losses { get; internal set; }
            public int Ties { get; internal set; }
            public int Runs { get; internal set; }
            public int RunsAllowed { get; internal set; }
            public int Streak { get; internal set; }
            public int StreakOutcome { get; internal set; }
            public int Games => Wins + Losses + Ties;
            public double Percentage => Wins + Losses == 0 ? 0 : (double)Wins / (Wins + Losses);
            public IReadOnlyList<int> RankHistory => _rankHistory;

            internal void AddRankHistory(int rank) => _rankHistory.Add(rank);
        }

        /// <summary>행 구단 기준 승·패·무다.</summary>
        public sealed class MatchupRecord
        {
            public int Wins { get; internal set; }
            public int Losses { get; internal set; }
            public int Ties { get; internal set; }
        }

        private readonly Dictionary<string, TeamRecord> _teams = new Dictionary<string, TeamRecord>(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<string, MatchupRecord>> _matchups =
            new Dictionary<string, Dictionary<string, MatchupRecord>>(StringComparer.Ordinal);
        private readonly List<TeamRecord> _standings = new List<TeamRecord>();
        private readonly List<int> _rounds = new List<int>();
        public IReadOnlyList<TeamRecord> Standings => _standings;
        public IReadOnlyList<int> Rounds => _rounds;
        public string FocusTeamId { get; }
        public string FocusOwnerName { get; }
        public string SeasonLabel { get; }

        /// <summary>입력 순서와 무관하게 라운드 단위로 집계하고 미완료 경기는 제외한다.</summary>
        public OwnerLeaguePresentationModel(ScheduleScreenSnapshot snapshot, string focusOwnerName = null)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            FocusTeamId = snapshot.FocusTeamId;
            FocusOwnerName = string.IsNullOrWhiteSpace(focusOwnerName) ? string.Empty : focusOwnerName.Trim();
            SeasonLabel = snapshot.SeasonLabel + " · " + snapshot.LeagueLabel;
            var games = new List<ScheduleGameSnapshot>();
            foreach (var game in snapshot.Games)
            {
                AddTeam(game.HomeTeam);
                AddTeam(game.AwayTeam);
                if (game.IsCompleted) games.Add(game);
            }
            games.Sort((a, b) =>
            {
                int order = a.Round.CompareTo(b.Round);
                return order != 0 ? order : string.CompareOrdinal(a.GameId, b.GameId);
            });
            for (int index = 0; index < games.Count; index++)
            {
                var game = games[index];
                Record(game.HomeTeam.TeamId, game.AwayTeam.TeamId, game.HomeRuns, game.AwayRuns);
                Record(game.AwayTeam.TeamId, game.HomeTeam.TeamId, game.AwayRuns, game.HomeRuns);
                if (index + 1 < games.Count && games[index + 1].Round == game.Round) continue;
                RankTeams();
                _rounds.Add(game.Round);
                foreach (var team in _standings) team.AddRankHistory(team.Rank);
            }
            RankTeams();
        }

        /// <summary>상대 전적은 행 구단 시점으로 반환한다.</summary>
        public MatchupRecord GetMatchup(string teamId, string opponentId)
        {
            return _matchups.TryGetValue(teamId, out var opponents) && opponents.TryGetValue(opponentId, out var record)
                ? record : new MatchupRecord();
        }

        private void AddTeam(ScheduleTeamSnapshot source)
        {
            if (_teams.ContainsKey(source.TeamId)) return;
            var team = new TeamRecord
            {
                Id = source.TeamId,
                Name = source.DisplayName,
                EmblemId = ParseEmblemId(source.EmblemAssetKey)
            };
            _teams.Add(team.Id, team);
            _standings.Add(team);
            _matchups.Add(team.Id, new Dictionary<string, MatchupRecord>(StringComparer.Ordinal));
        }

        private void Record(string teamId, string opponentId, int runs, int allowed)
        {
            var team = _teams[teamId];
            if (!_matchups[teamId].TryGetValue(opponentId, out var matchup))
            {
                matchup = new MatchupRecord();
                _matchups[teamId].Add(opponentId, matchup);
            }
            int outcome = runs.CompareTo(allowed);
            if (outcome > 0) { team.Wins++; matchup.Wins++; }
            else if (outcome < 0) { team.Losses++; matchup.Losses++; }
            else { team.Ties++; matchup.Ties++; }
            team.Runs += runs;
            team.RunsAllowed += allowed;
            team.Streak = team.StreakOutcome == outcome ? team.Streak + 1 : 1;
            team.StreakOutcome = outcome;
        }

        private void RankTeams()
        {
            var input = new List<OwnerLeagueStanding>(_standings.Count);
            foreach (var team in _standings)
                input.Add(new OwnerLeagueStanding(team.Id, team.Wins, team.Losses, team.Runs - team.RunsAllowed));
            OwnerLeagueStanding[] ranked = new OwnerLeagueAllocationResolver().Rank(input);
            _standings.Clear();
            for (int index = 0; index < ranked.Length; index++)
            {
                TeamRecord team = _teams[ranked[index].TeamKey];
                team.Rank = index + 1;
                _standings.Add(team);
            }
        }

        private static int ParseEmblemId(string assetKey)
        {
            const string prefix = "TeamEmblem/";
            if (string.IsNullOrEmpty(assetKey) || !assetKey.StartsWith(prefix, StringComparison.Ordinal))
                return 0;
            return int.TryParse(assetKey.Substring(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture,
                out int emblemId) ? emblemId : 0;
        }
    }
}
