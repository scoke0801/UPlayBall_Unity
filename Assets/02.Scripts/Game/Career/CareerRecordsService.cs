using System;
using System.Collections.Generic;
using Baseball.Core.Players;

namespace Baseball.Game.Career
{
    /// <summary>커리어 상태의 원본 기록을 순위·시즌 추이·통산 합계 화면 모델로 투영한다.</summary>
    public sealed partial class CareerRecordsService
    {
        private const int LeaderboardLimit = 10;
        private const double PlateAppearancesPerTeamGame = 3.1d;
        private const int PitchingOutsPerTeamGame = 3;
        private const double TieTolerance = 0.0000001d;

        public CareerRecordsView Build(CareerState career, CareerRecordCategory category)
        {
            return Build(
                career,
                category,
                CareerRecordViewMode.Basic,
                CompetitionScope.RegularSeason);
        }

        /// <summary>선택한 경기 범위와 지표 밀도에 맞춰 기록 화면 읽기 모델을 만든다.</summary>
        public CareerRecordsView Build(
            CareerState career,
            CareerRecordCategory category,
            CareerRecordViewMode viewMode,
            CompetitionScope scope)
        {
            if (career == null)
                throw new ArgumentNullException(nameof(career));

            SeasonState currentSeason = career.CurrentLeague.CurrentSeason;
            PlayerState myPlayer = career.MyPlayer;
            CompetitionStatisticsState competition = currentSeason.LeagueStatistics.Get(scope);
            CareerRecordMetric[] columns = GetColumns(category, viewMode);
            CareerRecordMetric primaryMetric = columns[0];
            List<PlayerCompetitionStatisticsState> qualified = CollectQualifiedPlayers(
                competition,
                currentSeason,
                category,
                scope);
            qualified.Sort((left, right) => ComparePlayers(left, right, primaryMetric));

            PlayerCompetitionStatisticsState myStatistics = competition.GetPlayer(myPlayer.PlayerId);
            CareerRecordMetric[] summaryMetrics = GetSummaryMetrics(category, viewMode);
            return new CareerRecordsView
            {
                SeasonYear = currentSeason.Year,
                LeagueLevel = currentSeason.LeagueLevel,
                PlayerName = myPlayer.Name,
                Category = category,
                ViewMode = viewMode,
                Scope = scope,
                PrimaryMetric = primaryMetric,
                LeaderboardColumns = columns,
                Leaderboard = BuildLeaderboard(career, qualified, columns, myPlayer.PlayerId),
                MyRecordMetrics = BuildMyMetrics(myStatistics, qualified, summaryMetrics),
                Seasons = BuildSeasons(career, columns, scope),
                CareerTotals = BuildCareerTotals(career, summaryMetrics, scope),
                Trend = BuildTrend(career, primaryMetric, scope),
                Awards = BuildAwards(career),
                Highlights = BuildHighlights(career, scope),
                TeamSplits = BuildTeamSplits(career, columns, scope),
                TradeHistory = BuildTradeHistory(career),
                IsMyPlayerQualified = myStatistics != null && IsQualified(
                    myStatistics,
                    category,
                    GetTeamGames(currentSeason, myStatistics.TeamId),
                    scope),
                QualifiedPlayerCount = qualified.Count,
                HasScopeData = competition.Players.Count > 0 || HasArchivedScopeData(career, scope)
            };
        }

        private static CareerRecordLeaderboardRow[] BuildLeaderboard(
            CareerState career,
            List<PlayerCompetitionStatisticsState> qualified,
            CareerRecordMetric[] columns,
            int myPlayerId)
        {
            int count = Math.Min(LeaderboardLimit, qualified.Count);
            var rows = new CareerRecordLeaderboardRow[count];
            int rank = 0;
            double previousValue = 0d;
            for (int index = 0; index < count; index++)
            {
                PlayerCompetitionStatisticsState player = qualified[index];
                double value = GetValue(player, columns[0]);
                if (index == 0 || Math.Abs(value - previousValue) > TieTolerance)
                    rank = index + 1;
                previousValue = value;
                rows[index] = new CareerRecordLeaderboardRow(
                    rank,
                    player.PlayerId,
                    player.PlayerName,
                    player.TeamId,
                    GetTeamName(career, player.TeamId),
                    player.PlayerId == myPlayerId,
                    BuildMetricValues(player, columns));
            }
            return rows;
        }

        private static CareerRecordMetricValue[] BuildMyMetrics(
            PlayerCompetitionStatisticsState myStatistics,
            List<PlayerCompetitionStatisticsState> qualified,
            CareerRecordMetric[] metrics)
        {
            var values = new CareerRecordMetricValue[metrics.Length];
            bool isQualified = myStatistics != null && ContainsPlayer(qualified, myStatistics.PlayerId);
            for (int index = 0; index < metrics.Length; index++)
            {
                double value = myStatistics == null ? 0d : GetValue(myStatistics, metrics[index]);
                int rank = isQualified ? CalculateRank(qualified, myStatistics, metrics[index]) : 0;
                values[index] = new CareerRecordMetricValue(metrics[index], value, rank);
            }
            return values;
        }

        private static CareerRecordSeasonRow[] BuildSeasons(
            CareerState career,
            CareerRecordMetric[] columns,
            CompetitionScope scope)
        {
            var rows = new List<CareerRecordSeasonRow>(career.SeasonHistory.Count + 1);
            SeasonState current = career.CurrentLeague.CurrentSeason;
            PlayerSeasonStatisticsState currentStatistics = GetPlayerStatistics(current, scope);
            if (currentStatistics != null)
            {
                rows.Add(new CareerRecordSeasonRow(
                    current.Year,
                    current.LeagueLevel,
                    GetTeamName(career, career.MyPlayer.CurrentTeamId),
                    true,
                    BuildMetricValues(currentStatistics, columns)));
            }

            for (int index = career.SeasonHistory.Count - 1; index >= 0; index--)
            {
                CareerSeasonHistoryRecord history = career.SeasonHistory[index];
                PlayerSeasonStatisticsState statistics = GetPlayerStatistics(history, scope);
                if (statistics == null)
                    continue;
                rows.Add(new CareerRecordSeasonRow(
                    history.Year,
                    history.LeagueLevel,
                    history.TeamName,
                    false,
                    BuildMetricValues(statistics, columns)));
            }
            return rows.ToArray();
        }

        private static CareerRecordMetricValue[] BuildCareerTotals(
            CareerState career,
            CareerRecordMetric[] metrics,
            CompetitionScope scope)
        {
            var totals = new PlayerStatisticsTotals();
            totals.Add(GetPlayerStatistics(career.CurrentLeague.CurrentSeason, scope));
            for (int index = 0; index < career.SeasonHistory.Count; index++)
                totals.Add(GetPlayerStatistics(career.SeasonHistory[index], scope));

            var result = new CareerRecordMetricValue[metrics.Length];
            for (int index = 0; index < metrics.Length; index++)
                result[index] = new CareerRecordMetricValue(metrics[index], totals.GetValue(metrics[index]));
            return result;
        }

        private static CareerRecordTrendPoint[] BuildTrend(
            CareerState career,
            CareerRecordMetric primaryMetric,
            CompetitionScope scope)
        {
            var trend = new List<CareerRecordTrendPoint>(career.SeasonHistory.Count + 1);
            for (int index = 0; index < career.SeasonHistory.Count; index++)
            {
                CareerSeasonHistoryRecord history = career.SeasonHistory[index];
                PlayerSeasonStatisticsState statistics = GetPlayerStatistics(history, scope);
                if (statistics == null)
                    continue;
                trend.Add(new CareerRecordTrendPoint(
                    history.Year,
                    GetValue(statistics, primaryMetric),
                    false));
            }

            SeasonState current = career.CurrentLeague.CurrentSeason;
            PlayerSeasonStatisticsState currentStatistics = GetPlayerStatistics(current, scope);
            if (currentStatistics != null)
            {
                trend.Add(new CareerRecordTrendPoint(
                    current.Year,
                    GetValue(currentStatistics, primaryMetric),
                    true));
            }
            return trend.ToArray();
        }

        private static CareerAwardRecordView[] BuildAwards(CareerState career)
        {
            var awards = new List<CareerAwardRecordView>();
            SeasonState current = career.CurrentLeague.CurrentSeason;
            AddAwards(awards, current.Year, current.LeagueLevel, current.Awards, career.MyPlayer.PlayerId, true);
            for (int index = career.SeasonHistory.Count - 1; index >= 0; index--)
            {
                CareerSeasonHistoryRecord history = career.SeasonHistory[index];
                AddAwards(
                    awards,
                    history.Year,
                    history.LeagueLevel,
                    history.Awards,
                    career.MyPlayer.PlayerId,
                    false);
            }
            return awards.ToArray();
        }

        private static void AddAwards(
            List<CareerAwardRecordView> destination,
            int year,
            LeagueLevel leagueLevel,
            SeasonAwardsState awards,
            int playerId,
            bool isCurrent)
        {
            if (awards == null)
                return;
            for (int index = 0; index < awards.Results.Count; index++)
            {
                SeasonAwardResultState award = awards.Results[index];
                if (!award.IncludesWinner(playerId))
                    continue;
                destination.Add(new CareerAwardRecordView(
                    year,
                    leagueLevel,
                    award.Category,
                    award.Position,
                    isCurrent));
            }
        }

        private static CareerRecordHighlightView[] BuildHighlights(
            CareerState career,
            CompetitionScope scope)
        {
            PlayerSeasonStatisticsState statistics = GetPlayerStatistics(
                career.CurrentLeague.CurrentSeason,
                scope);
            if (statistics == null)
                return Array.Empty<CareerRecordHighlightView>();
            IReadOnlyList<PlayerGameLogState> recent = statistics.RecentGames;
            var highlights = new CareerRecordHighlightView[recent.Count];
            for (int index = 0; index < recent.Count; index++)
            {
                PlayerGameLogState game = recent[recent.Count - 1 - index];
                highlights[index] = new CareerRecordHighlightView(
                    game,
                    GetTeamName(career, game.OpponentTeamId));
            }
            return highlights;
        }

        private static CareerTeamStatisticsSplitView[] BuildTeamSplits(
            CareerState career,
            CareerRecordMetric[] columns,
            CompetitionScope scope)
        {
            var splits = new List<CareerTeamStatisticsSplitView>();
            SeasonState current = career.CurrentLeague.CurrentSeason;
            AddTeamSplits(
                splits,
                career,
                current.Year,
                GetPlayerStatistics(current, scope),
                columns,
                isCurrentSeason: true);

            for (int index = career.SeasonHistory.Count - 1; index >= 0; index--)
            {
                CareerSeasonHistoryRecord history = career.SeasonHistory[index];
                AddTeamSplits(
                    splits,
                    career,
                    history.Year,
                    GetPlayerStatistics(history, scope),
                    columns,
                    isCurrentSeason: false);
            }

            splits.Sort((left, right) =>
            {
                int year = right.Year.CompareTo(left.Year);
                return year != 0 ? year : left.TeamId.CompareTo(right.TeamId);
            });
            return splits.ToArray();
        }

        private static void AddTeamSplits(
            List<CareerTeamStatisticsSplitView> destination,
            CareerState career,
            int year,
            PlayerSeasonStatisticsState statistics,
            CareerRecordMetric[] columns,
            bool isCurrentSeason)
        {
            if (statistics == null || statistics.TeamSplits.Count < 2)
                return;

            foreach (KeyValuePair<int, PlayerTeamStatisticsSplitState> pair in statistics.TeamSplits)
            {
                PlayerTeamStatisticsSplitState split = pair.Value;
                destination.Add(new CareerTeamStatisticsSplitView(
                    year,
                    split.TeamId,
                    GetTeamName(career, split.TeamId),
                    split.TeamGames,
                    isCurrentSeason,
                    BuildMetricValues(split, columns)));
            }
        }

        private static CareerTradeHistoryView[] BuildTradeHistory(CareerState career)
        {
            IReadOnlyList<TradeHistoryRecord> history = career.TradeState.History;
            var rows = new CareerTradeHistoryView[history.Count];
            for (int index = 0; index < history.Count; index++)
            {
                TradeHistoryRecord trade = history[history.Count - 1 - index];
                rows[index] = new CareerTradeHistoryView(
                    trade.Year,
                    trade.GameIndex,
                    GetTeamName(career, trade.PreviousTeamId),
                    GetTeamName(career, trade.NewTeamId),
                    trade.PreviousRole,
                    trade.ProjectedRole);
            }
            return rows;
        }

        private static List<PlayerCompetitionStatisticsState> CollectQualifiedPlayers(
            CompetitionStatisticsState competition,
            SeasonState season,
            CareerRecordCategory category,
            CompetitionScope scope)
        {
            var result = new List<PlayerCompetitionStatisticsState>(competition.Players.Count);
            foreach (PlayerCompetitionStatisticsState player in competition.Players.Values)
            {
                if (IsQualified(player, category, GetTeamGames(season, player.TeamId), scope))
                    result.Add(player);
            }
            return result;
        }

        private static bool IsQualified(
            PlayerCompetitionStatisticsState player,
            CareerRecordCategory category,
            int teamGames,
            CompetitionScope scope)
        {
            if (scope == CompetitionScope.Postseason)
                return HasCategoryParticipation(player, category);

            return category switch
            {
                CareerRecordCategory.Batting =>
                    player.Batting.PlateAppearances >= Math.Ceiling(teamGames * PlateAppearancesPerTeamGame) &&
                    player.Batting.PlateAppearances > 0,
                CareerRecordCategory.Pitching =>
                    player.Pitching.OutsRecorded >= teamGames * PitchingOutsPerTeamGame &&
                    player.Pitching.OutsRecorded > 0,
                CareerRecordCategory.Fielding => GetFieldingTotals(player).Opportunities > 0,
                CareerRecordCategory.Baserunning =>
                    player.Batting.StolenBases + player.Batting.CaughtStealing > 0,
                _ => false
            };
        }

        private static bool HasCategoryParticipation(
            PlayerCompetitionStatisticsState player,
            CareerRecordCategory category)
        {
            return category switch
            {
                CareerRecordCategory.Batting => player.Batting.PlateAppearances > 0,
                CareerRecordCategory.Pitching => player.Pitching.OutsRecorded > 0,
                CareerRecordCategory.Fielding => GetFieldingTotals(player).Opportunities > 0,
                CareerRecordCategory.Baserunning =>
                    player.Batting.StolenBases + player.Batting.CaughtStealing > 0,
                _ => false
            };
        }

        private static PlayerSeasonStatisticsState GetPlayerStatistics(
            SeasonState season,
            CompetitionScope scope)
        {
            return scope == CompetitionScope.Postseason
                ? season.PostseasonPlayerStatistics
                : season.PlayerStatistics;
        }

        private static PlayerSeasonStatisticsState GetPlayerStatistics(
            CareerSeasonHistoryRecord history,
            CompetitionScope scope)
        {
            return scope == CompetitionScope.Postseason
                ? history.PostseasonStatistics
                : history.Statistics;
        }

        private static bool HasArchivedScopeData(CareerState career, CompetitionScope scope)
        {
            for (int index = 0; index < career.SeasonHistory.Count; index++)
            {
                if (GetPlayerStatistics(career.SeasonHistory[index], scope) != null)
                    return true;
            }
            return false;
        }

        private static int GetTeamGames(SeasonState season, int teamId)
        {
            TeamSeasonRecordState record = season.GetTeamRecord(teamId);
            return record?.GamesPlayed ?? 0;
        }

        private static int ComparePlayers(
            PlayerCompetitionStatisticsState left,
            PlayerCompetitionStatisticsState right,
            CareerRecordMetric metric)
        {
            double leftValue = GetValue(left, metric);
            double rightValue = GetValue(right, metric);
            int comparison = IsLowerBetter(metric)
                ? leftValue.CompareTo(rightValue)
                : rightValue.CompareTo(leftValue);
            if (comparison != 0)
                return comparison;
            return left.PlayerId.CompareTo(right.PlayerId);
        }

        private static int CalculateRank(
            List<PlayerCompetitionStatisticsState> qualified,
            PlayerCompetitionStatisticsState player,
            CareerRecordMetric metric)
        {
            double playerValue = GetValue(player, metric);
            int betterCount = 0;
            for (int index = 0; index < qualified.Count; index++)
            {
                if (qualified[index].PlayerId == player.PlayerId)
                    continue;
                double otherValue = GetValue(qualified[index], metric);
                if (IsLowerBetter(metric)
                        ? otherValue < playerValue - TieTolerance
                        : otherValue > playerValue + TieTolerance)
                {
                    betterCount++;
                }
            }
            return betterCount + 1;
        }

        private static bool ContainsPlayer(List<PlayerCompetitionStatisticsState> players, int playerId)
        {
            for (int index = 0; index < players.Count; index++)
            {
                if (players[index].PlayerId == playerId)
                    return true;
            }
            return false;
        }

        private static CareerRecordMetricValue[] BuildMetricValues(
            PlayerCompetitionStatisticsState player,
            CareerRecordMetric[] metrics)
        {
            var values = new CareerRecordMetricValue[metrics.Length];
            for (int index = 0; index < metrics.Length; index++)
                values[index] = new CareerRecordMetricValue(metrics[index], GetValue(player, metrics[index]));
            return values;
        }

        private static CareerRecordMetricValue[] BuildMetricValues(
            PlayerSeasonStatisticsState statistics,
            CareerRecordMetric[] metrics)
        {
            var values = new CareerRecordMetricValue[metrics.Length];
            for (int index = 0; index < metrics.Length; index++)
                values[index] = new CareerRecordMetricValue(metrics[index], GetValue(statistics, metrics[index]));
            return values;
        }

        private static CareerRecordMetricValue[] BuildMetricValues(
            PlayerTeamStatisticsSplitState statistics,
            CareerRecordMetric[] metrics)
        {
            var values = new CareerRecordMetricValue[metrics.Length];
            for (int index = 0; index < metrics.Length; index++)
                values[index] = new CareerRecordMetricValue(metrics[index], GetValue(statistics, metrics[index]));
            return values;
        }

        private static double GetValue(
            PlayerTeamStatisticsSplitState statistics,
            CareerRecordMetric metric)
        {
            BattingStatisticsState batting = statistics.Batting;
            PitchingStatisticsState pitching = statistics.Pitching;
            FieldingTotals fielding = GetFieldingTotals(statistics);
            return metric switch
            {
                CareerRecordMetric.Games => batting.Games + pitching.Appearances,
                CareerRecordMetric.GamesStarted => batting.GamesStarted + pitching.Starts,
                CareerRecordMetric.PlateAppearances => batting.PlateAppearances,
                CareerRecordMetric.AtBats => batting.AtBats,
                CareerRecordMetric.Runs => batting.Runs,
                CareerRecordMetric.Hits => batting.Hits,
                CareerRecordMetric.Singles => batting.Singles,
                CareerRecordMetric.Doubles => batting.Doubles,
                CareerRecordMetric.Triples => batting.Triples,
                CareerRecordMetric.HomeRuns => batting.HomeRuns,
                CareerRecordMetric.RunsBattedIn => batting.RunsBattedIn,
                CareerRecordMetric.Walks => batting.Walks,
                CareerRecordMetric.HitByPitches => batting.HitByPitches,
                CareerRecordMetric.BattingStrikeouts => batting.Strikeouts,
                CareerRecordMetric.SacrificeFlies => batting.SacrificeFlies,
                CareerRecordMetric.GroundedIntoDoublePlays => batting.GroundedIntoDoublePlays,
                CareerRecordMetric.TotalBases => batting.TotalBases,
                CareerRecordMetric.BattingAverage => batting.BattingAverage,
                CareerRecordMetric.OnBasePercentage => batting.OnBasePercentage,
                CareerRecordMetric.SluggingPercentage => batting.SluggingPercentage,
                CareerRecordMetric.OnBasePlusSlugging => batting.OnBasePlusSlugging,
                CareerRecordMetric.WalkStrikeoutRatio => batting.WalkStrikeoutRatio,
                CareerRecordMetric.PitchingAppearances => pitching.Appearances,
                CareerRecordMetric.PitchingStarts => pitching.Starts,
                CareerRecordMetric.OutsRecorded => pitching.OutsRecorded,
                CareerRecordMetric.Wins => pitching.Wins,
                CareerRecordMetric.Losses => pitching.Losses,
                CareerRecordMetric.Saves => pitching.Saves,
                CareerRecordMetric.Holds => pitching.Holds,
                CareerRecordMetric.BlownSaves => pitching.BlownSaves,
                CareerRecordMetric.HitsAllowed => pitching.HitsAllowed,
                CareerRecordMetric.HomeRunsAllowed => pitching.HomeRunsAllowed,
                CareerRecordMetric.RunsAllowed => pitching.RunsAllowed,
                CareerRecordMetric.EarnedRuns => pitching.EarnedRuns,
                CareerRecordMetric.WalksAllowed => pitching.WalksAllowed,
                CareerRecordMetric.HitBatters => pitching.HitBatters,
                CareerRecordMetric.PitchingStrikeouts => pitching.Strikeouts,
                CareerRecordMetric.BattersFaced => pitching.BattersFaced,
                CareerRecordMetric.QualityStarts => pitching.QualityStarts,
                CareerRecordMetric.EarnedRunAverage => pitching.EarnedRunAverage,
                CareerRecordMetric.WalksHitsPerInningPitched => pitching.WalksHitsPerInningPitched,
                CareerRecordMetric.StrikeoutWalkRatio => pitching.StrikeoutWalkRatio,
                CareerRecordMetric.HomeRunsPerNineInnings => pitching.HomeRunsPerNineInnings,
                CareerRecordMetric.DefensiveOuts => fielding.DefensiveOuts,
                CareerRecordMetric.FieldingOpportunities => fielding.Opportunities,
                CareerRecordMetric.SuccessfulFieldingPlays => fielding.SuccessfulPlays,
                CareerRecordMetric.Putouts => fielding.Putouts,
                CareerRecordMetric.Assists => fielding.Assists,
                CareerRecordMetric.Errors => fielding.Errors,
                CareerRecordMetric.DoublePlays => fielding.DoublePlays,
                CareerRecordMetric.DifficultPlayAttempts => fielding.DifficultPlayAttempts,
                CareerRecordMetric.DifficultPlaysMade => fielding.DifficultPlaysMade,
                CareerRecordMetric.ExpectedOuts => fielding.ExpectedOuts,
                CareerRecordMetric.EstimatedRunsSaved => fielding.EstimatedRunsSaved,
                CareerRecordMetric.FieldingSuccessRate => fielding.SuccessRate,
                CareerRecordMetric.StolenBases => batting.StolenBases,
                CareerRecordMetric.CaughtStealing => batting.CaughtStealing,
                CareerRecordMetric.StolenBasePercentage => batting.StolenBasePercentage,
                _ => 0d
            };
        }

        private static FieldingTotals GetFieldingTotals(PlayerTeamStatisticsSplitState statistics)
        {
            var totals = new FieldingTotals();
            for (int positionIndex = (int)PlayerPosition.Catcher;
                 positionIndex <= (int)PlayerPosition.ReliefPitcher;
                 positionIndex++)
            {
                FieldingStatisticsState fielding = statistics.GetFielding((PlayerPosition)positionIndex);
                if (fielding != null)
                    totals.Add(fielding);
            }
            return totals;
        }

        private static double GetValue(PlayerCompetitionStatisticsState player, CareerRecordMetric metric)
        {
            BattingStatisticsState batting = player.Batting;
            PitchingStatisticsState pitching = player.Pitching;
            FieldingTotals fielding = GetFieldingTotals(player);
            return metric switch
            {
                CareerRecordMetric.Games => player.GamesPlayed,
                CareerRecordMetric.GamesStarted => batting.GamesStarted + pitching.Starts,
                CareerRecordMetric.PlateAppearances => batting.PlateAppearances,
                CareerRecordMetric.AtBats => batting.AtBats,
                CareerRecordMetric.Runs => batting.Runs,
                CareerRecordMetric.Hits => batting.Hits,
                CareerRecordMetric.Singles => batting.Singles,
                CareerRecordMetric.Doubles => batting.Doubles,
                CareerRecordMetric.Triples => batting.Triples,
                CareerRecordMetric.HomeRuns => batting.HomeRuns,
                CareerRecordMetric.RunsBattedIn => batting.RunsBattedIn,
                CareerRecordMetric.Walks => batting.Walks,
                CareerRecordMetric.HitByPitches => batting.HitByPitches,
                CareerRecordMetric.BattingStrikeouts => batting.Strikeouts,
                CareerRecordMetric.SacrificeFlies => batting.SacrificeFlies,
                CareerRecordMetric.GroundedIntoDoublePlays => batting.GroundedIntoDoublePlays,
                CareerRecordMetric.TotalBases => batting.TotalBases,
                CareerRecordMetric.BattingAverage => batting.BattingAverage,
                CareerRecordMetric.OnBasePercentage => batting.OnBasePercentage,
                CareerRecordMetric.SluggingPercentage => batting.SluggingPercentage,
                CareerRecordMetric.OnBasePlusSlugging => batting.OnBasePlusSlugging,
                CareerRecordMetric.WalkStrikeoutRatio => batting.WalkStrikeoutRatio,
                CareerRecordMetric.PitchingAppearances => pitching.Appearances,
                CareerRecordMetric.PitchingStarts => pitching.Starts,
                CareerRecordMetric.OutsRecorded => pitching.OutsRecorded,
                CareerRecordMetric.Wins => pitching.Wins,
                CareerRecordMetric.Losses => pitching.Losses,
                CareerRecordMetric.Saves => pitching.Saves,
                CareerRecordMetric.Holds => pitching.Holds,
                CareerRecordMetric.BlownSaves => pitching.BlownSaves,
                CareerRecordMetric.HitsAllowed => pitching.HitsAllowed,
                CareerRecordMetric.HomeRunsAllowed => pitching.HomeRunsAllowed,
                CareerRecordMetric.RunsAllowed => pitching.RunsAllowed,
                CareerRecordMetric.EarnedRuns => pitching.EarnedRuns,
                CareerRecordMetric.WalksAllowed => pitching.WalksAllowed,
                CareerRecordMetric.HitBatters => pitching.HitBatters,
                CareerRecordMetric.PitchingStrikeouts => pitching.Strikeouts,
                CareerRecordMetric.BattersFaced => pitching.BattersFaced,
                CareerRecordMetric.QualityStarts => pitching.QualityStarts,
                CareerRecordMetric.EarnedRunAverage => pitching.EarnedRunAverage,
                CareerRecordMetric.WalksHitsPerInningPitched => pitching.WalksHitsPerInningPitched,
                CareerRecordMetric.StrikeoutWalkRatio => pitching.StrikeoutWalkRatio,
                CareerRecordMetric.HomeRunsPerNineInnings => pitching.HomeRunsPerNineInnings,
                CareerRecordMetric.DefensiveOuts => fielding.DefensiveOuts,
                CareerRecordMetric.FieldingOpportunities => fielding.Opportunities,
                CareerRecordMetric.SuccessfulFieldingPlays => fielding.SuccessfulPlays,
                CareerRecordMetric.Putouts => fielding.Putouts,
                CareerRecordMetric.Assists => fielding.Assists,
                CareerRecordMetric.Errors => fielding.Errors,
                CareerRecordMetric.DoublePlays => fielding.DoublePlays,
                CareerRecordMetric.DifficultPlayAttempts => fielding.DifficultPlayAttempts,
                CareerRecordMetric.DifficultPlaysMade => fielding.DifficultPlaysMade,
                CareerRecordMetric.ExpectedOuts => fielding.ExpectedOuts,
                CareerRecordMetric.EstimatedRunsSaved => fielding.EstimatedRunsSaved,
                CareerRecordMetric.FieldingSuccessRate => fielding.SuccessRate,
                CareerRecordMetric.StolenBases => batting.StolenBases,
                CareerRecordMetric.CaughtStealing => batting.CaughtStealing,
                CareerRecordMetric.StolenBasePercentage => batting.StolenBasePercentage,
                _ => 0d
            };
        }

        private static double GetValue(
            PlayerSeasonStatisticsState statistics,
            CareerRecordMetric metric)
        {
            if (statistics == null)
                return 0d;
            FieldingTotals fielding = GetFieldingTotals(statistics);
            return metric switch
            {
                CareerRecordMetric.Games => statistics.GamesPlayed,
                CareerRecordMetric.GamesStarted => statistics.GamesStarted,
                CareerRecordMetric.PlateAppearances => statistics.PlateAppearances,
                CareerRecordMetric.AtBats => statistics.AtBats,
                CareerRecordMetric.Runs => statistics.Runs,
                CareerRecordMetric.Hits => statistics.Hits,
                CareerRecordMetric.Singles => statistics.Singles,
                CareerRecordMetric.Doubles => statistics.Doubles,
                CareerRecordMetric.Triples => statistics.Triples,
                CareerRecordMetric.HomeRuns => statistics.HomeRuns,
                CareerRecordMetric.RunsBattedIn => statistics.RunsBattedIn,
                CareerRecordMetric.Walks => statistics.Walks,
                CareerRecordMetric.HitByPitches => statistics.HitByPitches,
                CareerRecordMetric.BattingStrikeouts => statistics.BattingStrikeouts,
                CareerRecordMetric.SacrificeFlies => statistics.SacrificeFlies,
                CareerRecordMetric.GroundedIntoDoublePlays => statistics.GroundedIntoDoublePlays,
                CareerRecordMetric.TotalBases => statistics.TotalBases,
                CareerRecordMetric.BattingAverage => statistics.BattingAverage,
                CareerRecordMetric.OnBasePercentage => statistics.OnBasePercentage,
                CareerRecordMetric.SluggingPercentage => statistics.SluggingPercentage,
                CareerRecordMetric.OnBasePlusSlugging => statistics.OnBasePlusSlugging,
                CareerRecordMetric.WalkStrikeoutRatio => statistics.WalkStrikeoutRatio,
                CareerRecordMetric.PitchingAppearances => statistics.PitchingAppearances,
                CareerRecordMetric.PitchingStarts => statistics.PitchingStarts,
                CareerRecordMetric.OutsRecorded => statistics.OutsRecorded,
                CareerRecordMetric.Wins => statistics.Wins,
                CareerRecordMetric.Losses => statistics.Losses,
                CareerRecordMetric.Saves => statistics.Saves,
                CareerRecordMetric.Holds => statistics.Holds,
                CareerRecordMetric.BlownSaves => statistics.BlownSaves,
                CareerRecordMetric.HitsAllowed => statistics.HitsAllowed,
                CareerRecordMetric.HomeRunsAllowed => statistics.HomeRunsAllowed,
                CareerRecordMetric.RunsAllowed => statistics.RunsAllowed,
                CareerRecordMetric.EarnedRuns => statistics.EarnedRuns,
                CareerRecordMetric.WalksAllowed => statistics.WalksAllowed,
                CareerRecordMetric.HitBatters => statistics.HitBatters,
                CareerRecordMetric.PitchingStrikeouts => statistics.PitchingStrikeouts,
                CareerRecordMetric.BattersFaced => statistics.BattersFaced,
                CareerRecordMetric.QualityStarts => statistics.QualityStarts,
                CareerRecordMetric.EarnedRunAverage => statistics.EarnedRunAverage,
                CareerRecordMetric.WalksHitsPerInningPitched => statistics.WalksHitsPerInningPitched,
                CareerRecordMetric.StrikeoutWalkRatio => statistics.StrikeoutWalkRatio,
                CareerRecordMetric.HomeRunsPerNineInnings => statistics.HomeRunsPerNineInnings,
                CareerRecordMetric.DefensiveOuts => fielding.DefensiveOuts,
                CareerRecordMetric.FieldingOpportunities => fielding.Opportunities,
                CareerRecordMetric.SuccessfulFieldingPlays => fielding.SuccessfulPlays,
                CareerRecordMetric.Putouts => fielding.Putouts,
                CareerRecordMetric.Assists => fielding.Assists,
                CareerRecordMetric.Errors => fielding.Errors,
                CareerRecordMetric.DoublePlays => fielding.DoublePlays,
                CareerRecordMetric.DifficultPlayAttempts => fielding.DifficultPlayAttempts,
                CareerRecordMetric.DifficultPlaysMade => fielding.DifficultPlaysMade,
                CareerRecordMetric.ExpectedOuts => fielding.ExpectedOuts,
                CareerRecordMetric.EstimatedRunsSaved => fielding.EstimatedRunsSaved,
                CareerRecordMetric.FieldingSuccessRate => fielding.SuccessRate,
                CareerRecordMetric.StolenBases => statistics.StolenBases,
                CareerRecordMetric.CaughtStealing => statistics.CaughtStealing,
                CareerRecordMetric.StolenBasePercentage => statistics.StolenBasePercentage,
                _ => 0d
            };
        }

        private static FieldingTotals GetFieldingTotals(PlayerCompetitionStatisticsState player)
        {
            var totals = new FieldingTotals();
            for (int positionIndex = (int)PlayerPosition.Catcher;
                 positionIndex <= (int)PlayerPosition.ReliefPitcher;
                 positionIndex++)
            {
                FieldingStatisticsState fielding = player.GetFielding((PlayerPosition)positionIndex);
                if (fielding != null)
                    totals.Add(fielding);
            }
            return totals;
        }

        private static FieldingTotals GetFieldingTotals(PlayerSeasonStatisticsState statistics)
        {
            var totals = new FieldingTotals();
            for (int positionIndex = (int)PlayerPosition.Catcher;
                 positionIndex <= (int)PlayerPosition.ReliefPitcher;
                 positionIndex++)
            {
                FieldingStatisticsState fielding = statistics.GetFielding((PlayerPosition)positionIndex);
                if (fielding != null)
                    totals.Add(fielding);
            }
            return totals;
        }

        private static string GetTeamName(CareerState career, int teamId)
        {
            for (int index = 0; index < career.CurrentLeague.Teams.Count; index++)
            {
                TeamState team = career.CurrentLeague.Teams[index];
                if (team.TeamId == teamId)
                    return team.Name;
            }
            return string.Empty;
        }

        private static CareerRecordMetric[] GetColumns(
            CareerRecordCategory category,
            CareerRecordViewMode viewMode)
        {
            if (viewMode == CareerRecordViewMode.Basic)
                return GetBasicColumns(category);

            return category switch
            {
                CareerRecordCategory.Batting => new[]
                {
                    CareerRecordMetric.BattingAverage,
                    CareerRecordMetric.OnBasePlusSlugging,
                    CareerRecordMetric.Games,
                    CareerRecordMetric.PlateAppearances,
                    CareerRecordMetric.Hits,
                    CareerRecordMetric.HomeRuns,
                    CareerRecordMetric.RunsBattedIn,
                    CareerRecordMetric.Runs,
                    CareerRecordMetric.AtBats,
                    CareerRecordMetric.GamesStarted,
                    CareerRecordMetric.Doubles,
                    CareerRecordMetric.Triples,
                    CareerRecordMetric.Walks,
                    CareerRecordMetric.HitByPitches,
                    CareerRecordMetric.BattingStrikeouts,
                    CareerRecordMetric.SacrificeFlies,
                    CareerRecordMetric.GroundedIntoDoublePlays,
                    CareerRecordMetric.TotalBases,
                    CareerRecordMetric.OnBasePercentage,
                    CareerRecordMetric.SluggingPercentage,
                    CareerRecordMetric.WalkStrikeoutRatio
                },
                CareerRecordCategory.Pitching => new[]
                {
                    CareerRecordMetric.EarnedRunAverage,
                    CareerRecordMetric.WalksHitsPerInningPitched,
                    CareerRecordMetric.Wins,
                    CareerRecordMetric.PitchingStrikeouts,
                    CareerRecordMetric.OutsRecorded,
                    CareerRecordMetric.PitchingAppearances,
                    CareerRecordMetric.PitchingStarts,
                    CareerRecordMetric.Losses,
                    CareerRecordMetric.Saves,
                    CareerRecordMetric.Holds,
                    CareerRecordMetric.BlownSaves,
                    CareerRecordMetric.OutsRecorded,
                    CareerRecordMetric.HitsAllowed,
                    CareerRecordMetric.RunsAllowed,
                    CareerRecordMetric.EarnedRuns,
                    CareerRecordMetric.HomeRunsAllowed,
                    CareerRecordMetric.WalksAllowed,
                    CareerRecordMetric.HitBatters,
                    CareerRecordMetric.BattersFaced,
                    CareerRecordMetric.QualityStarts,
                    CareerRecordMetric.StrikeoutWalkRatio,
                    CareerRecordMetric.HomeRunsPerNineInnings
                },
                CareerRecordCategory.Fielding => new[]
                {
                    CareerRecordMetric.EstimatedRunsSaved,
                    CareerRecordMetric.FieldingSuccessRate,
                    CareerRecordMetric.FieldingOpportunities,
                    CareerRecordMetric.SuccessfulFieldingPlays,
                    CareerRecordMetric.Putouts,
                    CareerRecordMetric.Errors,
                    CareerRecordMetric.Assists,
                    CareerRecordMetric.DefensiveOuts,
                    CareerRecordMetric.DoublePlays,
                    CareerRecordMetric.DifficultPlayAttempts,
                    CareerRecordMetric.DifficultPlaysMade,
                    CareerRecordMetric.ExpectedOuts
                },
                _ => new[]
                {
                    CareerRecordMetric.StolenBases,
                    CareerRecordMetric.StolenBasePercentage,
                    CareerRecordMetric.CaughtStealing,
                    CareerRecordMetric.Games,
                    CareerRecordMetric.Runs,
                    CareerRecordMetric.Hits,
                    CareerRecordMetric.PlateAppearances,
                    CareerRecordMetric.Triples,
                    CareerRecordMetric.OnBasePercentage
                }
            };
        }

        private static CareerRecordMetric[] GetBasicColumns(CareerRecordCategory category)
        {
            return category switch
            {
                CareerRecordCategory.Batting => new[]
                {
                    CareerRecordMetric.BattingAverage,
                    CareerRecordMetric.Games,
                    CareerRecordMetric.Hits,
                    CareerRecordMetric.HomeRuns,
                    CareerRecordMetric.RunsBattedIn,
                    CareerRecordMetric.OnBasePlusSlugging
                },
                CareerRecordCategory.Pitching => new[]
                {
                    CareerRecordMetric.EarnedRunAverage,
                    CareerRecordMetric.PitchingAppearances,
                    CareerRecordMetric.OutsRecorded,
                    CareerRecordMetric.Wins,
                    CareerRecordMetric.PitchingStrikeouts,
                    CareerRecordMetric.WalksHitsPerInningPitched
                },
                CareerRecordCategory.Fielding => new[]
                {
                    CareerRecordMetric.EstimatedRunsSaved,
                    CareerRecordMetric.FieldingOpportunities,
                    CareerRecordMetric.SuccessfulFieldingPlays,
                    CareerRecordMetric.Putouts,
                    CareerRecordMetric.Errors,
                    CareerRecordMetric.FieldingSuccessRate
                },
                _ => new[]
                {
                    CareerRecordMetric.StolenBases,
                    CareerRecordMetric.CaughtStealing,
                    CareerRecordMetric.StolenBasePercentage,
                    CareerRecordMetric.Games,
                    CareerRecordMetric.Runs,
                    CareerRecordMetric.Hits
                }
            };
        }

        private static CareerRecordMetric[] GetSummaryMetrics(
            CareerRecordCategory category,
            CareerRecordViewMode viewMode)
        {
            if (viewMode == CareerRecordViewMode.Basic)
            {
                return category switch
                {
                    CareerRecordCategory.Batting => new[]
                    {
                        CareerRecordMetric.BattingAverage,
                        CareerRecordMetric.Hits,
                        CareerRecordMetric.HomeRuns,
                        CareerRecordMetric.RunsBattedIn,
                        CareerRecordMetric.OnBasePercentage,
                        CareerRecordMetric.SluggingPercentage,
                        CareerRecordMetric.OnBasePlusSlugging
                    },
                    CareerRecordCategory.Pitching => new[]
                    {
                        CareerRecordMetric.EarnedRunAverage,
                        CareerRecordMetric.Wins,
                        CareerRecordMetric.Saves,
                        CareerRecordMetric.PitchingAppearances,
                        CareerRecordMetric.OutsRecorded,
                        CareerRecordMetric.PitchingStrikeouts,
                        CareerRecordMetric.WalksHitsPerInningPitched
                    },
                    CareerRecordCategory.Fielding => new[]
                    {
                        CareerRecordMetric.EstimatedRunsSaved,
                        CareerRecordMetric.FieldingOpportunities,
                        CareerRecordMetric.SuccessfulFieldingPlays,
                        CareerRecordMetric.Putouts,
                        CareerRecordMetric.Assists,
                        CareerRecordMetric.Errors,
                        CareerRecordMetric.FieldingSuccessRate
                    },
                    _ => new[]
                    {
                        CareerRecordMetric.StolenBases,
                        CareerRecordMetric.CaughtStealing,
                        CareerRecordMetric.StolenBasePercentage,
                        CareerRecordMetric.Games,
                        CareerRecordMetric.Runs,
                        CareerRecordMetric.Hits,
                        CareerRecordMetric.BattingAverage
                    }
                };
            }

            return GetColumns(category, CareerRecordViewMode.Expanded);
        }

        private static bool IsLowerBetter(CareerRecordMetric metric)
        {
            return metric is CareerRecordMetric.EarnedRunAverage or
                CareerRecordMetric.WalksHitsPerInningPitched or
                CareerRecordMetric.HomeRunsPerNineInnings or
                CareerRecordMetric.Errors;
        }

    }
}
