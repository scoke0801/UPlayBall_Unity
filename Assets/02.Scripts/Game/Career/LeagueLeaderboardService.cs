using System;
using System.Collections.Generic;
using Baseball.Core.Players;

namespace Baseball.Game.Career
{
    /// <summary>포지션별로 나뉜 수비 기록을 한 선수 합계로 모은다.</summary>
    internal struct FieldingTotals
    {
        public int DefensiveOuts;
        public int Opportunities;
        public int SuccessfulPlays;
        public int Putouts;
        public int Assists;
        public int Errors;
        public int DoublePlays;
        public int DifficultPlayAttempts;
        public int DifficultPlaysMade;
        public double ExpectedOuts;
        public double EstimatedRunsSaved;
        public double SuccessRate => Opportunities == 0 ? 0d : SuccessfulPlays / (double)Opportunities;

        public void Add(FieldingStatisticsState fielding)
        {
            DefensiveOuts += fielding.DefensiveOuts;
            Opportunities += fielding.Opportunities;
            SuccessfulPlays += fielding.SuccessfulPlays;
            Putouts += fielding.Putouts;
            Assists += fielding.Assists;
            Errors += fielding.Errors;
            DoublePlays += fielding.DoublePlays;
            DifficultPlayAttempts += fielding.DifficultPlayAttempts;
            DifficultPlaysMade += fielding.DifficultPlaysMade;
            ExpectedOuts += fielding.ExpectedOuts;
            EstimatedRunsSaved += fielding.EstimatedRunsSaved;
        }
    }

    /// <summary>
    /// 선수 커리어 모드와 구단주 모드가 같은 규정·지표·순위 규칙으로 리그 기록표를 만든다.
    /// 두 모드가 같은 <see cref="CompetitionStatisticsState"/>를 쓰므로 규정 상수와 지표 계산을 여기 한 벌만 둔다.
    /// </summary>
    public static class LeagueLeaderboardService
    {
        public const int DefaultLeaderboardLimit = 10;

        // 규정 타석·이닝은 실제 프로야구 기준(팀 경기당 3.1타석 / 1이닝)을 따른다.
        // 두 모드가 다른 규정을 쓰면 같은 성적이 모드에 따라 순위에 들거나 빠져 설명할 수 없게 된다.
        internal const double PlateAppearancesPerTeamGame = 3.1d;
        internal const int PitchingOutsPerTeamGame = 3;

        // 부동소수 누적 오차로 동률이 우열로 바뀌지 않게 하는 비교 허용치다.
        internal const double TieTolerance = 0.0000001d;

        /// <summary>부문별 기본 표시 지표를 반환하며 첫 번째 지표가 정렬 기준이 된다.</summary>
        public static CareerRecordMetric[] GetBasicColumns(CareerRecordCategory category)
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

        /// <summary>값이 작을수록 좋은 지표인지 알려 정렬 방향을 결정한다.</summary>
        public static bool IsLowerBetter(CareerRecordMetric metric)
        {
            return metric is CareerRecordMetric.EarnedRunAverage or
                CareerRecordMetric.WalksHitsPerInningPitched or
                CareerRecordMetric.HomeRunsPerNineInnings or
                CareerRecordMetric.Errors;
        }

        /// <summary>한 선수의 누적 기록에서 화면 지표 하나의 값을 계산한다.</summary>
        public static double GetMetricValue(PlayerCompetitionStatisticsState player, CareerRecordMetric metric)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
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

        /// <summary>표시 지표 목록을 한 선수의 값 배열로 만든다.</summary>
        public static CareerRecordMetricValue[] BuildMetricValues(
            PlayerCompetitionStatisticsState player,
            CareerRecordMetric[] metrics)
        {
            if (metrics == null) throw new ArgumentNullException(nameof(metrics));
            var values = new CareerRecordMetricValue[metrics.Length];
            for (int index = 0; index < metrics.Length; index++)
                values[index] = new CareerRecordMetricValue(metrics[index], GetMetricValue(player, metrics[index]));
            return values;
        }

        /// <summary>정규 시즌은 규정 기록을, 포스트시즌은 출전 사실만 자격 조건으로 본다.</summary>
        public static bool IsQualified(
            PlayerCompetitionStatisticsState player,
            CareerRecordCategory category,
            int teamGames,
            CompetitionScope scope)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
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

        /// <summary>부문 규정을 통과한 선수만 모은다.</summary>
        public static List<PlayerCompetitionStatisticsState> CollectQualifiedPlayers(
            CompetitionStatisticsState competition,
            CareerRecordCategory category,
            CompetitionScope scope,
            Func<int, int> getTeamGames)
        {
            if (competition == null) throw new ArgumentNullException(nameof(competition));
            if (getTeamGames == null) throw new ArgumentNullException(nameof(getTeamGames));
            var result = new List<PlayerCompetitionStatisticsState>(competition.Players.Count);
            foreach (PlayerCompetitionStatisticsState player in competition.Players.Values)
            {
                if (IsQualified(player, category, getTeamGames(player.TeamId), scope))
                    result.Add(player);
            }
            return result;
        }

        /// <summary>Dictionary 순회 순서가 결과에 남지 않도록 지표와 PlayerId로 전순서를 만든다.</summary>
        public static void SortByMetric(
            List<PlayerCompetitionStatisticsState> players,
            CareerRecordMetric metric)
        {
            if (players == null) throw new ArgumentNullException(nameof(players));
            players.Sort((left, right) => ComparePlayers(left, right, metric));
        }

        /// <summary>동률을 공동 순위로 처리한 규정 자격자 안의 순위를 계산한다.</summary>
        public static int CalculateRank(
            IReadOnlyList<PlayerCompetitionStatisticsState> qualified,
            PlayerCompetitionStatisticsState player,
            CareerRecordMetric metric)
        {
            if (qualified == null) throw new ArgumentNullException(nameof(qualified));
            double playerValue = GetMetricValue(player, metric);
            int betterCount = 0;
            for (int index = 0; index < qualified.Count; index++)
            {
                if (qualified[index].PlayerId == player.PlayerId)
                    continue;
                double otherValue = GetMetricValue(qualified[index], metric);
                if (IsLowerBetter(metric)
                        ? otherValue < playerValue - TieTolerance
                        : otherValue > playerValue + TieTolerance)
                {
                    betterCount++;
                }
            }
            return betterCount + 1;
        }

        /// <summary>
        /// 지표 순으로 정렬된 규정 자격자 목록의 상위 구간을 표시용 리더보드 행으로 만든다.
        /// 강조 대상은 모드마다 다르므로(선수 모드는 내 선수, 구단주 모드는 내 구단 선수) 판정을 주입받는다.
        /// </summary>
        public static CareerRecordLeaderboardRow[] BuildLeaderboard(
            IReadOnlyList<PlayerCompetitionStatisticsState> qualified,
            CareerRecordMetric[] columns,
            Func<int, string> getTeamName,
            Func<PlayerCompetitionStatisticsState, bool> isHighlighted,
            int limit = DefaultLeaderboardLimit,
            Func<int, string> getPlayerName = null)
        {
            if (qualified == null) throw new ArgumentNullException(nameof(qualified));
            if (columns == null || columns.Length == 0)
                throw new ArgumentException("표시 지표가 최소 하나 필요합니다.", nameof(columns));
            if (getTeamName == null) throw new ArgumentNullException(nameof(getTeamName));
            if (isHighlighted == null) throw new ArgumentNullException(nameof(isHighlighted));
            if (limit <= 0) throw new ArgumentOutOfRangeException(nameof(limit));

            int count = Math.Min(limit, qualified.Count);
            var rows = new CareerRecordLeaderboardRow[count];
            int rank = 0;
            double previousValue = 0d;
            for (int index = 0; index < count; index++)
            {
                PlayerCompetitionStatisticsState player = qualified[index];
                double value = GetMetricValue(player, columns[0]);
                if (index == 0 || Math.Abs(value - previousValue) > TieTolerance)
                    rank = index + 1;
                previousValue = value;
                rows[index] = new CareerRecordLeaderboardRow(
                    rank,
                    player.PlayerId,
                    getPlayerName?.Invoke(player.PlayerId) ?? player.PlayerName,
                    player.TeamId,
                    getTeamName(player.TeamId),
                    isHighlighted(player),
                    BuildMetricValues(player, columns));
            }
            return rows;
        }

        internal static bool HasCategoryParticipation(
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

        internal static int ComparePlayers(
            PlayerCompetitionStatisticsState left,
            PlayerCompetitionStatisticsState right,
            CareerRecordMetric metric)
        {
            double leftValue = GetMetricValue(left, metric);
            double rightValue = GetMetricValue(right, metric);
            int comparison = IsLowerBetter(metric)
                ? leftValue.CompareTo(rightValue)
                : rightValue.CompareTo(leftValue);
            if (comparison != 0)
                return comparison;
            return left.PlayerId.CompareTo(right.PlayerId);
        }

        internal static FieldingTotals GetFieldingTotals(PlayerCompetitionStatisticsState player)
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
    }
}
