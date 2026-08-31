using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Career;

namespace Baseball.Game.Career
{
    /// <summary>
    /// 리그 상태의 원본 순위·선수 기록·일정을 동일한 동률 규칙으로 정렬해 리그 화면에 제공한다.
    /// </summary>
    public sealed class LeagueHubService
    {
        private const int LeaderCount = 5;
        private const int RecentResultCount = 5;
        private const int RecentFormCount = 5;
        private const double RateQualificationPerTeamGame = 3d;

        private readonly CareerState _career;
        private readonly BalanceTable _balance;
        private readonly SeasonState _season;

        public LeagueHubService(CareerState career, BalanceTable balance)
        {
            _career = career ?? throw new ArgumentNullException(nameof(career));
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            _season = career.CurrentLeague.CurrentSeason ??
                      throw new InvalidOperationException("현재 시즌이 시작되지 않았습니다.");
        }

        /// <summary>현재 커리어의 리그 화면 스냅샷을 생성한다.</summary>
        public LeagueHubView Build()
        {
            if (_season.TeamRecords == null || _season.Schedule == null)
                throw new InvalidOperationException("정규 시즌 일정과 순위가 아직 생성되지 않았습니다.");

            int myTeamId = _career.MyPlayer.CurrentTeamId;
            TeamState myTeam = GetTeam(myTeamId);
            LeagueStandingView[] standings = BuildStandings(myTeamId);
            LeagueScheduleGameView[] recentResults = BuildRecentResults(myTeamId);
            LeagueScheduleGameView[] nextRoundGames = BuildNextRoundGames(myTeamId);
            DateTime currentDate = GetCurrentDate(recentResults, nextRoundGames);
            TeamSeasonRecordState myRecord = _season.GetTeamRecord(myTeamId);

            return new LeagueHubView(
                _season.Year,
                _season.LeagueLevel,
                _season.Phase,
                currentDate,
                myRecord?.GamesPlayed ?? 0,
                _balance.CareerSeason.RegularSeasonGamesPerTeam,
                _balance.Postseason.PlayoffTeamCount,
                myTeamId,
                myTeam.Name,
                _career.MyPlayer.PlayerId,
                standings,
                BuildBattingLeaderboards(),
                BuildPitchingLeaderboards(),
                BuildTeamMetrics(myTeamId),
                recentResults,
                nextRoundGames,
                WorldGenerationConfiguration.GetDefaultDefinition(_season.LeagueLevel),
                LeagueLevelRules.TryGetLower(_season.LeagueLevel, out LeagueLevel lower)
                    ? WorldGenerationConfiguration.GetDefaultDefinition(lower)
                    : null,
                LeagueLevelRules.TryGetHigher(_season.LeagueLevel, out LeagueLevel higher)
                    ? WorldGenerationConfiguration.GetDefaultDefinition(higher)
                    : null,
                _career.Reputation.HighestReachedTier);
        }

        private LeagueStandingView[] BuildStandings(int myTeamId)
        {
            var entries = new TeamStandingEntry[_season.TeamRecords.Count];
            for (int index = 0; index < entries.Length; index++)
            {
                TeamSeasonRecordState record = _season.TeamRecords[index];
                entries[index] = new TeamStandingEntry(
                    record.TeamId,
                    record.Wins,
                    record.Losses,
                    record.RunsScored,
                    record.RunsAllowed,
                    record.FixedTiebreaker,
                    record.GetHeadToHeadEntries());
            }

            // 포스트시즌과 같은 정렬기를 전 구단에 적용해 화면 순위와 실제 시드가 어긋나지 않게 한다.
            int[] orderedTeamIds = PostseasonBracket.SelectSeeds(entries, entries.Length);
            TeamSeasonRecordState leader = _season.GetTeamRecord(orderedTeamIds[0]);
            var views = new LeagueStandingView[orderedTeamIds.Length];
            for (int index = 0; index < orderedTeamIds.Length; index++)
            {
                int teamId = orderedTeamIds[index];
                TeamState team = GetTeam(teamId);
                TeamSeasonRecordState record = _season.GetTeamRecord(teamId);
                TeamGameOutcome? streak = GetStreak(teamId, out int streakLength);
                views[index] = new LeagueStandingView(
                    index + 1,
                    teamId,
                    team.Name,
                    team.PrimaryColor,
                    record.GamesPlayed,
                    record.Wins,
                    record.Losses,
                    record.Ties,
                    record.WinningPercentage,
                    CalculateGamesBehind(leader, record),
                    streak,
                    streakLength,
                    GetRecentForm(teamId),
                    index < _balance.Postseason.PlayoffTeamCount,
                    teamId == myTeamId,
                    GetStandingZone(_season.LeagueLevel, index + 1),
                    team.EmblemId);
            }
            return views;
        }

        private static LeagueStandingZone GetStandingZone(LeagueLevel tier, int rank)
        {
            if (rank <= 2 && tier != LeagueLevel.Galaxy)
                return LeagueStandingZone.Promotion;
            if (rank <= 4)
                return LeagueStandingZone.PostseasonRetention;
            if (rank >= 7 && tier != LeagueLevel.Rookie)
                return LeagueStandingZone.Relegation;
            return LeagueStandingZone.Retention;
        }

        private LeagueBattingLeaderboardView[] BuildBattingLeaderboards()
        {
            const int categoryCount = (int)LeagueBattingCategory.OnBasePlusSlugging + 1;
            var leaderboards = new LeagueBattingLeaderboardView[categoryCount];
            for (int categoryIndex = 0; categoryIndex < categoryCount; categoryIndex++)
            {
                var category = (LeagueBattingCategory)categoryIndex;
                var candidates = new List<PlayerCompetitionStatisticsState>();
                foreach (PlayerCompetitionStatisticsState player in
                         _season.LeagueStatistics.RegularSeason.Players.Values)
                {
                    if (IsBattingEligible(player, category))
                        candidates.Add(player);
                }

                PlayerCompetitionStatisticsState[] ordered = candidates.ToArray();
                Array.Sort(ordered, (left, right) => CompareBatting(left, right, category));
                int visibleCount = Math.Min(LeaderCount, ordered.Length);
                var leaders = new LeagueBattingLeaderView[visibleCount];
                LeagueBattingLeaderView? myPlayer = null;
                for (int index = 0; index < ordered.Length; index++)
                {
                    LeagueBattingLeaderView row = CreateBattingLeader(ordered[index], index + 1);
                    if (index < visibleCount)
                        leaders[index] = row;
                    if (row.PlayerId == _career.MyPlayer.PlayerId)
                        myPlayer = row;
                }
                leaderboards[categoryIndex] = new LeagueBattingLeaderboardView(category, leaders, myPlayer);
            }
            return leaderboards;
        }

        private LeaguePitchingLeaderboardView[] BuildPitchingLeaderboards()
        {
            const int categoryCount = (int)LeaguePitchingCategory.WalksHitsPerInningPitched + 1;
            var leaderboards = new LeaguePitchingLeaderboardView[categoryCount];
            for (int categoryIndex = 0; categoryIndex < categoryCount; categoryIndex++)
            {
                var category = (LeaguePitchingCategory)categoryIndex;
                var candidates = new List<PlayerCompetitionStatisticsState>();
                foreach (PlayerCompetitionStatisticsState player in
                         _season.LeagueStatistics.RegularSeason.Players.Values)
                {
                    if (IsPitchingEligible(player, category))
                        candidates.Add(player);
                }

                PlayerCompetitionStatisticsState[] ordered = candidates.ToArray();
                Array.Sort(ordered, (left, right) => ComparePitching(left, right, category));
                int visibleCount = Math.Min(LeaderCount, ordered.Length);
                var leaders = new LeaguePitchingLeaderView[visibleCount];
                LeaguePitchingLeaderView? myPlayer = null;
                for (int index = 0; index < ordered.Length; index++)
                {
                    LeaguePitchingLeaderView row = CreatePitchingLeader(ordered[index], index + 1);
                    if (index < visibleCount)
                        leaders[index] = row;
                    if (row.PlayerId == _career.MyPlayer.PlayerId)
                        myPlayer = row;
                }
                leaderboards[categoryIndex] = new LeaguePitchingLeaderboardView(category, leaders, myPlayer);
            }
            return leaderboards;
        }

        private bool IsBattingEligible(
            PlayerCompetitionStatisticsState player,
            LeagueBattingCategory category)
        {
            if (player.Batting.PlateAppearances <= 0)
                return false;
            if (category is not (LeagueBattingCategory.BattingAverage or
                LeagueBattingCategory.OnBasePlusSlugging))
            {
                return true;
            }

            int teamGames = _season.GetTeamRecord(player.TeamId)?.GamesPlayed ?? 0;
            return player.Batting.PlateAppearances >= teamGames * RateQualificationPerTeamGame;
        }

        private bool IsPitchingEligible(
            PlayerCompetitionStatisticsState player,
            LeaguePitchingCategory category)
        {
            if (player.Pitching.Appearances <= 0)
                return false;
            if (category is not (LeaguePitchingCategory.EarnedRunAverage or
                LeaguePitchingCategory.WalksHitsPerInningPitched))
            {
                return true;
            }

            int teamGames = _season.GetTeamRecord(player.TeamId)?.GamesPlayed ?? 0;
            return player.Pitching.OutsRecorded >= teamGames * RateQualificationPerTeamGame;
        }

        private static int CompareBatting(
            PlayerCompetitionStatisticsState left,
            PlayerCompetitionStatisticsState right,
            LeagueBattingCategory category)
        {
            int byValue = GetBattingValue(right, category).CompareTo(GetBattingValue(left, category));
            if (byValue != 0)
                return byValue;
            int byPlayingTime = right.Batting.PlateAppearances.CompareTo(left.Batting.PlateAppearances);
            return byPlayingTime != 0 ? byPlayingTime : left.PlayerId.CompareTo(right.PlayerId);
        }

        private static int ComparePitching(
            PlayerCompetitionStatisticsState left,
            PlayerCompetitionStatisticsState right,
            LeaguePitchingCategory category)
        {
            double leftValue = GetPitchingValue(left, category);
            double rightValue = GetPitchingValue(right, category);
            int byValue = category is LeaguePitchingCategory.EarnedRunAverage or
                LeaguePitchingCategory.WalksHitsPerInningPitched
                ? leftValue.CompareTo(rightValue)
                : rightValue.CompareTo(leftValue);
            if (byValue != 0)
                return byValue;
            int byOuts = right.Pitching.OutsRecorded.CompareTo(left.Pitching.OutsRecorded);
            return byOuts != 0 ? byOuts : left.PlayerId.CompareTo(right.PlayerId);
        }

        private static double GetBattingValue(
            PlayerCompetitionStatisticsState player,
            LeagueBattingCategory category)
        {
            return category switch
            {
                LeagueBattingCategory.BattingAverage => player.Batting.BattingAverage,
                LeagueBattingCategory.HomeRuns => player.Batting.HomeRuns,
                LeagueBattingCategory.RunsBattedIn => player.Batting.RunsBattedIn,
                LeagueBattingCategory.StolenBases => player.Batting.StolenBases,
                LeagueBattingCategory.OnBasePlusSlugging => player.Batting.OnBasePlusSlugging,
                _ => 0d
            };
        }

        private static double GetPitchingValue(
            PlayerCompetitionStatisticsState player,
            LeaguePitchingCategory category)
        {
            return category switch
            {
                LeaguePitchingCategory.EarnedRunAverage => player.Pitching.EarnedRunAverage,
                LeaguePitchingCategory.Wins => player.Pitching.Wins,
                LeaguePitchingCategory.Saves => player.Pitching.Saves,
                LeaguePitchingCategory.Strikeouts => player.Pitching.Strikeouts,
                LeaguePitchingCategory.WalksHitsPerInningPitched =>
                    player.Pitching.WalksHitsPerInningPitched,
                _ => 0d
            };
        }

        private LeagueBattingLeaderView CreateBattingLeader(
            PlayerCompetitionStatisticsState player,
            int rank)
        {
            return new LeagueBattingLeaderView(
                rank,
                player.PlayerId,
                player.PlayerName,
                player.TeamId,
                GetTeam(player.TeamId).Name,
                player.PrimaryPosition,
                player.Batting.Games,
                player.Batting.PlateAppearances,
                player.Batting.BattingAverage,
                player.Batting.HomeRuns,
                player.Batting.RunsBattedIn,
                player.Batting.StolenBases,
                player.Batting.OnBasePlusSlugging,
                player.PlayerId == _career.MyPlayer.PlayerId);
        }

        private LeaguePitchingLeaderView CreatePitchingLeader(
            PlayerCompetitionStatisticsState player,
            int rank)
        {
            return new LeaguePitchingLeaderView(
                rank,
                player.PlayerId,
                player.PlayerName,
                player.TeamId,
                GetTeam(player.TeamId).Name,
                player.PrimaryPosition,
                player.Pitching.Appearances,
                player.Pitching.OutsRecorded,
                player.Pitching.Wins,
                player.Pitching.Losses,
                player.Pitching.Saves,
                player.Pitching.Strikeouts,
                player.Pitching.EarnedRunAverage,
                player.Pitching.WalksHitsPerInningPitched,
                player.PlayerId == _career.MyPlayer.PlayerId);
        }

        private LeagueTeamMetricView[] BuildTeamMetrics(int myTeamId)
        {
            var aggregates = new TeamAggregate[_career.CurrentLeague.Teams.Count];
            for (int index = 0; index < aggregates.Length; index++)
            {
                TeamState team = _career.CurrentLeague.Teams[index];
                aggregates[index] = new TeamAggregate(team.TeamId, team.Name);
            }

            foreach (PlayerCompetitionStatisticsState player in
                     _season.LeagueStatistics.RegularSeason.Players.Values)
            {
                TeamAggregate aggregate = GetAggregate(aggregates, player.TeamId);
                aggregate.AtBats += player.Batting.AtBats;
                aggregate.Hits += player.Batting.Hits;
                aggregate.PlateAppearances += player.Batting.PlateAppearances;
                aggregate.HomeRuns += player.Batting.HomeRuns;
                aggregate.PitchingOuts += player.Pitching.OutsRecorded;
                aggregate.EarnedRuns += player.Pitching.EarnedRuns;
                aggregate.Strikeouts += player.Pitching.Strikeouts;
            }

            const int metricCount = (int)LeagueTeamMetric.Strikeouts + 1;
            var result = new LeagueTeamMetricView[metricCount];
            for (int metricIndex = 0; metricIndex < metricCount; metricIndex++)
                result[metricIndex] = BuildTeamMetric(aggregates, myTeamId, (LeagueTeamMetric)metricIndex);
            return result;
        }

        private static LeagueTeamMetricView BuildTeamMetric(
            TeamAggregate[] aggregates,
            int myTeamId,
            LeagueTeamMetric metric)
        {
            var eligible = new List<TeamAggregate>(aggregates.Length);
            for (int index = 0; index < aggregates.Length; index++)
            {
                if (HasMetricData(aggregates[index], metric))
                    eligible.Add(aggregates[index]);
            }
            if (eligible.Count == 0)
                return new LeagueTeamMetricView(metric, false, string.Empty, 0d, 0d, 0d, 0);

            TeamAggregate[] ordered = eligible.ToArray();
            Array.Sort(ordered, (left, right) => CompareTeamMetric(left, right, metric));
            double sum = 0d;
            double myValue = 0d;
            int myRank = 0;
            for (int index = 0; index < ordered.Length; index++)
            {
                double value = GetTeamMetricValue(ordered[index], metric);
                sum += value;
                if (ordered[index].TeamId == myTeamId)
                {
                    myValue = value;
                    myRank = index + 1;
                }
            }
            return new LeagueTeamMetricView(
                metric,
                true,
                ordered[0].TeamName,
                GetTeamMetricValue(ordered[0], metric),
                sum / ordered.Length,
                myValue,
                myRank);
        }

        private static bool HasMetricData(TeamAggregate aggregate, LeagueTeamMetric metric)
        {
            return metric switch
            {
                LeagueTeamMetric.BattingAverage => aggregate.AtBats > 0,
                LeagueTeamMetric.HomeRuns => aggregate.PlateAppearances > 0,
                LeagueTeamMetric.EarnedRunAverage => aggregate.PitchingOuts > 0,
                LeagueTeamMetric.Strikeouts => aggregate.PitchingOuts > 0,
                _ => false
            };
        }

        private static int CompareTeamMetric(TeamAggregate left, TeamAggregate right, LeagueTeamMetric metric)
        {
            double leftValue = GetTeamMetricValue(left, metric);
            double rightValue = GetTeamMetricValue(right, metric);
            int byValue = metric == LeagueTeamMetric.EarnedRunAverage
                ? leftValue.CompareTo(rightValue)
                : rightValue.CompareTo(leftValue);
            return byValue != 0 ? byValue : left.TeamId.CompareTo(right.TeamId);
        }

        private static double GetTeamMetricValue(TeamAggregate aggregate, LeagueTeamMetric metric)
        {
            return metric switch
            {
                LeagueTeamMetric.BattingAverage => aggregate.AtBats == 0
                    ? 0d
                    : aggregate.Hits / (double)aggregate.AtBats,
                LeagueTeamMetric.HomeRuns => aggregate.HomeRuns,
                LeagueTeamMetric.EarnedRunAverage => aggregate.PitchingOuts == 0
                    ? 0d
                    : aggregate.EarnedRuns * 27d / aggregate.PitchingOuts,
                LeagueTeamMetric.Strikeouts => aggregate.Strikeouts,
                _ => 0d
            };
        }

        private LeagueScheduleGameView[] BuildRecentResults(int myTeamId)
        {
            var result = new List<LeagueScheduleGameView>(RecentResultCount);
            IReadOnlyList<ScheduledGameState> games = _season.Schedule.Games;
            for (int index = games.Count - 1; index >= 0 && result.Count < RecentResultCount; index--)
            {
                if (!games[index].IsCompleted)
                    continue;
                result.Add(CreateScheduleGame(games[index], myTeamId));
            }
            return result.ToArray();
        }

        private LeagueScheduleGameView[] BuildNextRoundGames(int myTeamId)
        {
            IReadOnlyList<ScheduledGameState> games = _season.Schedule.Games;
            int nextRound = 0;
            for (int index = 0; index < games.Count; index++)
            {
                if (!games[index].IsCompleted)
                {
                    nextRound = games[index].Round;
                    break;
                }
            }
            if (nextRound == 0)
                return Array.Empty<LeagueScheduleGameView>();

            var result = new List<LeagueScheduleGameView>();
            for (int index = 0; index < games.Count; index++)
            {
                if (!games[index].IsCompleted && games[index].Round == nextRound)
                    result.Add(CreateScheduleGame(games[index], myTeamId));
            }
            return result.ToArray();
        }

        private LeagueScheduleGameView CreateScheduleGame(ScheduledGameState game, int myTeamId)
        {
            return new LeagueScheduleGameView(
                game.GameId,
                game.Round,
                GetGameDate(game.Round),
                game.AwayTeamId,
                GetTeam(game.AwayTeamId).Name,
                game.HomeTeamId,
                GetTeam(game.HomeTeamId).Name,
                game.IsCompleted,
                game.AwayRuns,
                game.HomeRuns,
                game.IncludesTeam(myTeamId));
        }

        private DateTime GetCurrentDate(
            LeagueScheduleGameView[] recentResults,
            LeagueScheduleGameView[] nextRoundGames)
        {
            if (nextRoundGames.Length > 0)
                return nextRoundGames[0].Date;
            if (recentResults.Length > 0)
                return recentResults[0].Date;
            return new DateTime(
                _season.Year,
                _balance.CareerSeason.SeasonOpeningMonth,
                _balance.CareerSeason.SeasonOpeningDay);
        }

        private DateTime GetGameDate(int round)
        {
            int playedDays = round - 1;
            int restDays = playedDays / _balance.CareerSeason.GamesBetweenRestDays;
            return new DateTime(
                    _season.Year,
                    _balance.CareerSeason.SeasonOpeningMonth,
                    _balance.CareerSeason.SeasonOpeningDay)
                .AddDays(playedDays + restDays);
        }

        private TeamGameOutcome? GetStreak(int teamId, out int length)
        {
            TeamGameOutcome? streak = null;
            length = 0;
            IReadOnlyList<ScheduledGameState> games = _season.Schedule.Games;
            for (int index = games.Count - 1; index >= 0; index--)
            {
                ScheduledGameState game = games[index];
                if (!game.IsCompleted || !game.IncludesTeam(teamId))
                    continue;
                TeamGameOutcome outcome = GetOutcome(game, teamId);
                if (streak.HasValue && streak.Value != outcome)
                    break;
                streak = outcome;
                length++;
            }
            return streak;
        }

        private TeamGameOutcome[] GetRecentForm(int teamId)
        {
            var result = new List<TeamGameOutcome>(RecentFormCount);
            IReadOnlyList<ScheduledGameState> games = _season.Schedule.Games;
            for (int index = games.Count - 1; index >= 0 && result.Count < RecentFormCount; index--)
            {
                ScheduledGameState game = games[index];
                if (game.IsCompleted && game.IncludesTeam(teamId))
                    result.Add(GetOutcome(game, teamId));
            }
            return result.ToArray();
        }

        private static TeamGameOutcome GetOutcome(ScheduledGameState game, int teamId)
        {
            int teamRuns = game.HomeTeamId == teamId ? game.HomeRuns : game.AwayRuns;
            int opponentRuns = game.HomeTeamId == teamId ? game.AwayRuns : game.HomeRuns;
            return teamRuns > opponentRuns
                ? TeamGameOutcome.Win
                : teamRuns < opponentRuns ? TeamGameOutcome.Loss : TeamGameOutcome.Tie;
        }

        private static double CalculateGamesBehind(
            TeamSeasonRecordState leader,
            TeamSeasonRecordState team)
        {
            if (leader == null || team == null || leader.TeamId == team.TeamId)
                return 0d;
            return ((leader.Wins - team.Wins) + (team.Losses - leader.Losses)) * 0.5d;
        }

        private TeamState GetTeam(int teamId)
        {
            for (int index = 0; index < _career.CurrentLeague.Teams.Count; index++)
            {
                TeamState team = _career.CurrentLeague.Teams[index];
                if (team.TeamId == teamId)
                    return team;
            }
            throw new InvalidOperationException($"TeamId {teamId}를 찾을 수 없습니다.");
        }

        private static TeamAggregate GetAggregate(TeamAggregate[] aggregates, int teamId)
        {
            for (int index = 0; index < aggregates.Length; index++)
            {
                if (aggregates[index].TeamId == teamId)
                    return aggregates[index];
            }
            throw new InvalidOperationException($"TeamId {teamId}의 집계 대상을 찾을 수 없습니다.");
        }

        private sealed class TeamAggregate
        {
            public TeamAggregate(int teamId, string teamName)
            {
                TeamId = teamId;
                TeamName = teamName;
            }

            public int TeamId { get; }
            public string TeamName { get; }
            public int AtBats { get; set; }
            public int Hits { get; set; }
            public int PlateAppearances { get; set; }
            public int HomeRuns { get; set; }
            public int PitchingOuts { get; set; }
            public int EarnedRuns { get; set; }
            public int Strikeouts { get; set; }
        }
    }
}
