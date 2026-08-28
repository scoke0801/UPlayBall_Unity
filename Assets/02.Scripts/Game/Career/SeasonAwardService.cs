using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Players;

namespace Baseball.Game.Career
{
    /// <summary>동결된 리그 기록에서 통계 기반 수상을 결정론적으로 평가한다.</summary>
    public sealed class SeasonAwardService
    {
        private static readonly PlayerPosition[] GoldGlovePositions =
        {
            PlayerPosition.Catcher,
            PlayerPosition.FirstBase,
            PlayerPosition.SecondBase,
            PlayerPosition.ThirdBase,
            PlayerPosition.Shortstop,
            PlayerPosition.LeftField,
            PlayerPosition.CenterField,
            PlayerPosition.RightField
        };

        private readonly SeasonAwardBalance _balance;

        public SeasonAwardService(SeasonAwardBalance balance)
        {
            _balance = balance;
        }

        public SeasonAwardsState Evaluate(SeasonState season, int championTeamId)
        {
            if (season == null) throw new ArgumentNullException(nameof(season));
            if (!season.LeagueStatistics.RegularSeason.IsFrozen)
                throw new InvalidOperationException("정규 시즌 기록을 동결한 뒤 수상을 평가해야 합니다.");

            season.LeagueStatistics.Postseason.Freeze();
            PlayerCompetitionStatisticsState[] regular = ToSortedPlayers(
                season.LeagueStatistics.RegularSeason.Players);
            var awards = new SeasonAwardsState();

            AddRankedAward(awards, "regular_season_mvp", AwardScope.RegularSeason,
                AwardCategory.RegularSeasonMvp, EvaluateRoleCandidates(season, regular, 1d, true, false));
            AddRankedAward(awards, "rookie_of_year", AwardScope.RegularSeason,
                AwardCategory.RookieOfYear,
                EvaluateRoleCandidates(season, regular, _balance.RookieEligibilityFactor, false, true));

            AddRecordAwards(awards, season, regular);
            AddGoldGloves(awards, season, regular);
            AddPostseasonMvp(awards, season.LeagueStatistics.Postseason, championTeamId);
            return awards;
        }

        private List<AwardCandidateResult> EvaluateRoleCandidates(
            SeasonState season,
            PlayerCompetitionStatisticsState[] players,
            double eligibilityFactor,
            bool includeTeam,
            bool rookiesOnly)
        {
            var eligible = new List<PlayerCompetitionStatisticsState>();
            for (int index = 0; index < players.Length; index++)
            {
                PlayerCompetitionStatisticsState player = players[index];
                if (rookiesOnly && !season.IsRookieEligible(player.PlayerId)) continue;
                int teamGames = GetTeamGames(season, player.TeamId);
                if (IsEligible(player, teamGames, eligibilityFactor)) eligible.Add(player);
            }

            var results = new List<AwardCandidateResult>(eligible.Count);
            for (int index = 0; index < eligible.Count; index++)
            {
                PlayerCompetitionStatisticsState player = eligible[index];
                List<PlayerCompetitionStatisticsState> rolePool = GetRolePool(eligible, player.PrimaryPosition);
                results.Add(ScoreRoleCandidate(season, rolePool, player, includeTeam));
            }
            results.Sort(CompareCandidate);
            return results;
        }

        private AwardCandidateResult ScoreRoleCandidate(
            SeasonState season,
            List<PlayerCompetitionStatisticsState> pool,
            PlayerCompetitionStatisticsState player,
            bool includeTeam)
        {
            var breakdown = new List<AwardScoreBreakdown>(9);
            bool isStarter = player.PrimaryPosition == PlayerPosition.StartingPitcher;
            bool isReliever = player.PrimaryPosition == PlayerPosition.ReliefPitcher;
            bool hasFielding = HasAnyFieldingData(pool);

            if (!isStarter && !isReliever)
            {
                AddMetric(breakdown, "OPS", Percentile(pool, player, p => p.Batting.OnBasePlusSlugging),
                    _balance.HitterOpsWeight, true);
                AddMetric(breakdown, "TotalBases", Percentile(pool, player, p => p.Batting.TotalBases),
                    _balance.HitterTotalBasesWeight, true);
                AddMetric(breakdown, "HomeRuns", Percentile(pool, player, p => p.Batting.HomeRuns),
                    _balance.HitterHomeRunsWeight, true);
                AddMetric(breakdown, "RunProduction", Percentile(pool, player,
                        p => p.Batting.Runs + p.Batting.RunsBattedIn - p.Batting.HomeRuns),
                    _balance.HitterRunProductionWeight, true);
                AddMetric(breakdown, "Discipline", Percentile(pool, player, p => p.Batting.WalkStrikeoutRatio),
                    _balance.HitterDisciplineWeight, true);
                AddMetric(breakdown, "Baserunning", Percentile(pool, player,
                        p => p.Batting.StolenBases * 0.4d - p.Batting.CaughtStealing * 0.7d),
                    _balance.HitterBaserunningWeight, true);
                AddMetric(breakdown, "Fielding", Percentile(pool, player, GetFieldingRunsSaved),
                    _balance.HitterFieldingWeight, hasFielding);
                AddMetric(breakdown, "PlateAppearances", Percentile(pool, player, p => p.Batting.PlateAppearances),
                    _balance.HitterPlayingTimeWeight, true);
                AddMetric(breakdown, "TeamWinningPercentage", Percentile(pool, player,
                        p => GetWinningPercentage(season, p.TeamId)),
                    _balance.HitterTeamWeight, includeTeam);
            }
            else if (isStarter)
            {
                AddMetric(breakdown, "ERA", Percentile(pool, player, p => p.Pitching.EarnedRunAverage, true),
                    _balance.StarterEraWeight, true);
                AddMetric(breakdown, "WHIP", Percentile(pool, player,
                        p => p.Pitching.WalksHitsPerInningPitched, true), _balance.StarterWhipWeight, true);
                AddMetric(breakdown, "StrikeoutWalk", Percentile(pool, player,
                        p => p.Pitching.StrikeoutWalkRatio), _balance.StarterStrikeoutWalkWeight, true);
                AddMetric(breakdown, "HomeRunPrevention", Percentile(pool, player,
                        p => p.Pitching.HomeRunsPerNineInnings, true), _balance.StarterHomeRunPreventionWeight, true);
                AddMetric(breakdown, "OutsRecorded", Percentile(pool, player,
                        p => p.Pitching.OutsRecorded), _balance.StarterInningsWeight, true);
                AddMetric(breakdown, "Results", Percentile(pool, player,
                        p => p.Pitching.Wins + p.Pitching.QualityStarts), _balance.StarterResultsWeight, true);
                AddMetric(breakdown, "TeamWinningPercentage", Percentile(pool, player,
                        p => GetWinningPercentage(season, p.TeamId)), _balance.StarterTeamWeight, includeTeam);
            }
            else
            {
                AddMetric(breakdown, "ERA", Percentile(pool, player, p => p.Pitching.EarnedRunAverage, true),
                    _balance.RelieverEraWeight, true);
                AddMetric(breakdown, "WHIP", Percentile(pool, player,
                        p => p.Pitching.WalksHitsPerInningPitched, true), _balance.RelieverWhipWeight, true);
                AddMetric(breakdown, "StrikeoutWalk", Percentile(pool, player,
                        p => p.Pitching.StrikeoutWalkRatio), _balance.RelieverStrikeoutWalkWeight, true);
                AddMetric(breakdown, "ReliefResults", Percentile(pool, player,
                        p => p.Pitching.Saves + p.Pitching.Holds * 0.5d - p.Pitching.BlownSaves),
                    _balance.RelieverResultsWeight, true);
                AddMetric(breakdown, "OutsRecorded", Percentile(pool, player,
                        p => p.Pitching.OutsRecorded), _balance.RelieverInningsWeight, true);
                AddMetric(breakdown, "TeamWinningPercentage", Percentile(pool, player,
                        p => GetWinningPercentage(season, p.TeamId)), _balance.RelieverTeamWeight, includeTeam);
            }

            double finalScore = NormalizeBreakdown(breakdown);
            if (isReliever) finalScore *= _balance.ReliefPitcherScoreAdjustment;
            double teamWeight = includeTeam
                ? isStarter ? _balance.StarterTeamWeight : isReliever ? _balance.RelieverTeamWeight : _balance.HitterTeamWeight
                : 0d;
            double individualScore = teamWeight <= 0d
                ? finalScore
                : NormalizeWithoutMetric(breakdown, "TeamWinningPercentage") * (isReliever
                    ? _balance.ReliefPitcherScoreAdjustment
                    : 1d);
            double participation = isStarter || isReliever
                ? player.Pitching.OutsRecorded
                : player.Batting.PlateAppearances;
            return new AwardCandidateResult(
                player.PlayerId,
                player.PlayerName,
                player.TeamId,
                Math.Min(100d, finalScore),
                Math.Min(100d, individualScore),
                participation,
                GetRecentScore(player),
                breakdown);
        }

        private void AddRecordAwards(
            SeasonAwardsState awards,
            SeasonState season,
            PlayerCompetitionStatisticsState[] players)
        {
            AddRecordAward(awards, season, players, AwardCategory.BattingAverage, "batting_average",
                p => p.Batting.BattingAverage,
                p => p.Batting.PlateAppearances >= GetTeamGames(season, p.TeamId) * 3d, false);
            AddRecordAward(awards, season, players, AwardCategory.HomeRun, "home_run",
                p => p.Batting.HomeRuns, p => p.Batting.PlateAppearances > 0, false);
            AddRecordAward(awards, season, players, AwardCategory.RunsBattedIn, "runs_batted_in",
                p => p.Batting.RunsBattedIn, p => p.Batting.PlateAppearances > 0, false);
            AddRecordAward(awards, season, players, AwardCategory.StolenBase, "stolen_base",
                p => p.Batting.StolenBases, p => p.Batting.PlateAppearances > 0, false);
            AddRecordAward(awards, season, players, AwardCategory.EarnedRunAverage, "earned_run_average",
                p => p.Pitching.EarnedRunAverage,
                p => p.Pitching.OutsRecorded >= GetTeamGames(season, p.TeamId) * 3d, true);
            AddRecordAward(awards, season, players, AwardCategory.Win, "win",
                p => p.Pitching.Wins, p => p.Pitching.Appearances > 0, false);
            AddRecordAward(awards, season, players, AwardCategory.Strikeout, "strikeout",
                p => p.Pitching.Strikeouts, p => p.Pitching.Appearances > 0, false);
            AddRecordAward(awards, season, players, AwardCategory.Save, "save",
                p => p.Pitching.Saves, p => p.Pitching.Appearances > 0, false);
        }

        private static void AddRecordAward(
            SeasonAwardsState awards,
            SeasonState season,
            PlayerCompetitionStatisticsState[] players,
            AwardCategory category,
            string awardId,
            Func<PlayerCompetitionStatisticsState, double> selector,
            Func<PlayerCompetitionStatisticsState, bool> isEligible,
            bool lowerIsBetter)
        {
            var eligible = new List<PlayerCompetitionStatisticsState>();
            for (int index = 0; index < players.Length; index++)
            {
                if (isEligible(players[index])) eligible.Add(players[index]);
            }
            if (eligible.Count == 0) return;

            eligible.Sort((left, right) =>
            {
                int value = lowerIsBetter
                    ? selector(left).CompareTo(selector(right))
                    : selector(right).CompareTo(selector(left));
                return value != 0 ? value : left.PlayerId.CompareTo(right.PlayerId);
            });
            double winningValue = selector(eligible[0]);
            var coWinners = new List<int>();
            var top = new List<AwardCandidateResult>();
            for (int index = 0; index < eligible.Count; index++)
            {
                PlayerCompetitionStatisticsState player = eligible[index];
                if (index > 0 && selector(player).Equals(winningValue)) coWinners.Add(player.PlayerId);
                if (index < 3)
                    top.Add(CreateRawCandidate(player, selector(player), awardId));
            }
            awards.Add(new SeasonAwardResultState(
                awardId,
                AwardScope.RegularSeason,
                category,
                PlayerPosition.Unknown,
                eligible[0].PlayerId,
                coWinners.ToArray(),
                winningValue,
                top));
        }

        private void AddGoldGloves(
            SeasonAwardsState awards,
            SeasonState season,
            PlayerCompetitionStatisticsState[] players)
        {
            for (int positionIndex = 0; positionIndex < GoldGlovePositions.Length; positionIndex++)
            {
                PlayerPosition position = GoldGlovePositions[positionIndex];
                var eligible = new List<PlayerCompetitionStatisticsState>();
                for (int index = 0; index < players.Length; index++)
                {
                    PlayerCompetitionStatisticsState player = players[index];
                    FieldingStatisticsState fielding = player.GetFielding(position);
                    if (fielding == null || fielding.Opportunities == 0) continue;
                    double minimum = position == PlayerPosition.Catcher
                        ? _balance.CatcherGoldGloveMinimumInningsPerTeamGame
                        : _balance.GoldGloveMinimumInningsPerTeamGame;
                    if (fielding.DefensiveOuts >= GetTeamGames(season, player.TeamId) * minimum * 3d)
                        eligible.Add(player);
                }
                if (eligible.Count == 0) continue;

                var candidates = new List<AwardCandidateResult>(eligible.Count);
                for (int index = 0; index < eligible.Count; index++)
                {
                    PlayerCompetitionStatisticsState player = eligible[index];
                    FieldingStatisticsState fielding = player.GetFielding(position);
                    var breakdown = new List<AwardScoreBreakdown>(4);
                    AddMetric(breakdown, "EstimatedRunsSaved",
                        PercentileFielding(eligible, player, position, f => f.EstimatedRunsSaved),
                        _balance.GoldGloveRunsSavedWeight, true);
                    AddMetric(breakdown, "AdjustedSuccessRate",
                        PercentileFielding(eligible, player, position,
                            f => GetAdjustedSuccessRate(f, eligible, position)),
                        _balance.GoldGloveStabilityWeight, true);
                    AddMetric(breakdown, "DifficultPlays",
                        PercentileFielding(eligible, player, position,
                            f => f.DifficultPlayAttempts == 0 ? 0d :
                                f.DifficultPlaysMade / (double)f.DifficultPlayAttempts + f.DifficultPlaysMade * 0.01d),
                        _balance.GoldGloveDifficultPlayWeight, true);
                    AddMetric(breakdown, "DefensiveOuts",
                        PercentileFielding(eligible, player, position, f => f.DefensiveOuts),
                        _balance.GoldGloveInningsWeight, true);
                    candidates.Add(new AwardCandidateResult(
                        player.PlayerId,
                        player.PlayerName,
                        player.TeamId,
                        NormalizeBreakdown(breakdown),
                        fielding.EstimatedRunsSaved,
                        fielding.DefensiveOuts,
                        fielding.DifficultPlaysMade,
                        breakdown));
                }
                candidates.Sort(CompareGoldGloveCandidate);
                AddRankedAward(awards, $"gold_glove_{position.ToString().ToLowerInvariant()}",
                    AwardScope.RegularSeason, AwardCategory.GoldGlove, candidates, position);
            }
        }

        private static void AddPostseasonMvp(
            SeasonAwardsState awards,
            CompetitionStatisticsState postseason,
            int championTeamId)
        {
            var candidates = new List<AwardCandidateResult>();
            foreach (PlayerCompetitionStatisticsState player in postseason.Players.Values)
            {
                if (player.TeamId != championTeamId) continue;
                if (player.GamesPlayed < 2 && player.Batting.PlateAppearances < 6 && player.Pitching.OutsRecorded < 9)
                    continue;
                double total = 0d;
                double championship = 0d;
                double clinching = 0d;
                for (int index = 0; index < player.GameContributions.Count; index++)
                {
                    PlayerGameContributionState game = player.GameContributions[index];
                    total += game.WeightedScore;
                    if (game.IsChampionship) championship += game.WeightedScore;
                    if (game.IsSeriesClinching) clinching += game.WeightedScore;
                }
                candidates.Add(new AwardCandidateResult(
                    player.PlayerId,
                    player.PlayerName,
                    player.TeamId,
                    total,
                    championship,
                    player.Batting.PlateAppearances + player.Pitching.OutsRecorded,
                    clinching,
                    new[]
                    {
                        new AwardScoreBreakdown("PostseasonContribution", total, 1d),
                        new AwardScoreBreakdown("ChampionshipContribution", championship, 0d),
                        new AwardScoreBreakdown("ClinchingContribution", clinching, 0d)
                    }));
            }
            candidates.Sort(ComparePostseasonCandidate);
            AddRankedAward(awards, "postseason_mvp", AwardScope.Postseason,
                AwardCategory.PostseasonMvp, candidates);
        }

        private static void AddRankedAward(
            SeasonAwardsState awards,
            string awardId,
            AwardScope scope,
            AwardCategory category,
            List<AwardCandidateResult> candidates,
            PlayerPosition position = PlayerPosition.Unknown)
        {
            if (candidates == null || candidates.Count == 0) return;
            int count = Math.Min(3, candidates.Count);
            var top = new AwardCandidateResult[count];
            candidates.CopyTo(0, top, 0, count);
            awards.Add(new SeasonAwardResultState(
                awardId,
                scope,
                category,
                position,
                candidates[0].PlayerId,
                Array.Empty<int>(),
                candidates[0].FinalScore,
                top));
        }

        private bool IsEligible(PlayerCompetitionStatisticsState player, int teamGames, double factor)
        {
            if (player.PrimaryPosition == PlayerPosition.StartingPitcher)
                return player.Pitching.OutsRecorded >= teamGames * _balance.StarterMinimumInningsPerTeamGame * 3d * factor;
            if (player.PrimaryPosition == PlayerPosition.ReliefPitcher)
                return player.Pitching.Appearances >= teamGames * _balance.RelieverMinimumAppearanceRate * factor;
            return player.Batting.PlateAppearances >=
                       teamGames * _balance.BatterMinimumPlateAppearancesPerTeamGame * factor ||
                   player.Batting.GamesStarted >= teamGames * _balance.BatterMinimumStartRate * factor;
        }

        private static List<PlayerCompetitionStatisticsState> GetRolePool(
            List<PlayerCompetitionStatisticsState> players,
            PlayerPosition position)
        {
            bool starter = position == PlayerPosition.StartingPitcher;
            bool reliever = position == PlayerPosition.ReliefPitcher;
            var result = new List<PlayerCompetitionStatisticsState>();
            for (int index = 0; index < players.Count; index++)
            {
                PlayerPosition candidate = players[index].PrimaryPosition;
                if ((starter && candidate == PlayerPosition.StartingPitcher) ||
                    (reliever && candidate == PlayerPosition.ReliefPitcher) ||
                    (!starter && !reliever && candidate is not (PlayerPosition.StartingPitcher or PlayerPosition.ReliefPitcher)))
                    result.Add(players[index]);
            }
            return result;
        }

        private static double Percentile(
            List<PlayerCompetitionStatisticsState> pool,
            PlayerCompetitionStatisticsState target,
            Func<PlayerCompetitionStatisticsState, double> selector,
            bool lowerIsBetter = false)
        {
            if (pool.Count <= 1) return 100d;
            double value = selector(target);
            int worse = 0;
            int equal = 0;
            for (int index = 0; index < pool.Count; index++)
            {
                double other = selector(pool[index]);
                int comparison = other.CompareTo(value);
                if (comparison == 0) equal++;
                else if (lowerIsBetter ? comparison > 0 : comparison < 0) worse++;
            }
            return 100d * (worse + (equal - 1) * 0.5d) / (pool.Count - 1d);
        }

        private static double PercentileFielding(
            List<PlayerCompetitionStatisticsState> pool,
            PlayerCompetitionStatisticsState target,
            PlayerPosition position,
            Func<FieldingStatisticsState, double> selector)
        {
            if (pool.Count <= 1) return 100d;
            double value = selector(target.GetFielding(position));
            int worse = 0;
            int equal = 0;
            for (int index = 0; index < pool.Count; index++)
            {
                double other = selector(pool[index].GetFielding(position));
                int comparison = other.CompareTo(value);
                if (comparison == 0) equal++;
                else if (comparison < 0) worse++;
            }
            return 100d * (worse + (equal - 1) * 0.5d) / (pool.Count - 1d);
        }

        private static void AddMetric(
            List<AwardScoreBreakdown> breakdown,
            string metricId,
            double score,
            double weight,
            bool available)
        {
            if (available && weight > 0d)
                breakdown.Add(new AwardScoreBreakdown(metricId, score, weight));
        }

        private static double NormalizeBreakdown(List<AwardScoreBreakdown> breakdown)
        {
            double score = 0d;
            double weight = 0d;
            for (int index = 0; index < breakdown.Count; index++)
            {
                score += breakdown[index].WeightedScore;
                weight += breakdown[index].Weight;
            }
            return weight <= 0d ? 0d : score / weight;
        }

        private static double NormalizeWithoutMetric(List<AwardScoreBreakdown> breakdown, string metricId)
        {
            double score = 0d;
            double weight = 0d;
            for (int index = 0; index < breakdown.Count; index++)
            {
                if (breakdown[index].MetricId == metricId) continue;
                score += breakdown[index].WeightedScore;
                weight += breakdown[index].Weight;
            }
            return weight <= 0d ? 0d : score / weight;
        }

        private static bool HasAnyFieldingData(List<PlayerCompetitionStatisticsState> pool)
        {
            for (int index = 0; index < pool.Count; index++)
            {
                foreach (FieldingStatisticsState fielding in pool[index].FieldingByPosition.Values)
                {
                    if (fielding.Opportunities > 0) return true;
                }
            }
            return false;
        }

        private static double GetFieldingRunsSaved(PlayerCompetitionStatisticsState player)
        {
            double total = 0d;
            foreach (FieldingStatisticsState fielding in player.FieldingByPosition.Values)
                total += fielding.EstimatedRunsSaved;
            return total;
        }

        private double GetAdjustedSuccessRate(
            FieldingStatisticsState target,
            List<PlayerCompetitionStatisticsState> pool,
            PlayerPosition position)
        {
            int successes = 0;
            int opportunities = 0;
            for (int index = 0; index < pool.Count; index++)
            {
                FieldingStatisticsState fielding = pool[index].GetFielding(position);
                successes += fielding.SuccessfulPlays;
                opportunities += fielding.Opportunities;
            }
            double leagueAverage = opportunities == 0 ? 0d : successes / (double)opportunities;
            return (target.SuccessfulPlays + leagueAverage * _balance.FieldingRegressionOpportunities) /
                   (target.Opportunities + _balance.FieldingRegressionOpportunities);
        }

        private static int CompareCandidate(AwardCandidateResult left, AwardCandidateResult right)
        {
            int value = right.FinalScore.CompareTo(left.FinalScore);
            if (value != 0) return value;
            value = right.IndividualScore.CompareTo(left.IndividualScore);
            if (value != 0) return value;
            value = right.ParticipationScore.CompareTo(left.ParticipationScore);
            if (value != 0) return value;
            value = right.RecentScore.CompareTo(left.RecentScore);
            return value != 0 ? value : left.PlayerId.CompareTo(right.PlayerId);
        }

        private static int CompareGoldGloveCandidate(AwardCandidateResult left, AwardCandidateResult right)
        {
            int value = right.FinalScore.CompareTo(left.FinalScore);
            if (value != 0) return value;
            value = right.IndividualScore.CompareTo(left.IndividualScore);
            if (value != 0) return value;
            value = right.RecentScore.CompareTo(left.RecentScore);
            if (value != 0) return value;
            value = right.ParticipationScore.CompareTo(left.ParticipationScore);
            return value != 0 ? value : left.PlayerId.CompareTo(right.PlayerId);
        }

        private static int ComparePostseasonCandidate(AwardCandidateResult left, AwardCandidateResult right)
        {
            int value = right.FinalScore.CompareTo(left.FinalScore);
            if (value != 0) return value;
            value = right.IndividualScore.CompareTo(left.IndividualScore);
            if (value != 0) return value;
            value = right.RecentScore.CompareTo(left.RecentScore);
            if (value != 0) return value;
            value = right.ParticipationScore.CompareTo(left.ParticipationScore);
            return value != 0 ? value : left.PlayerId.CompareTo(right.PlayerId);
        }

        private static double GetRecentScore(PlayerCompetitionStatisticsState player)
        {
            int count = player.GameContributions.Count;
            if (count == 0) return 0d;
            int start = count - Math.Max(1, (int)Math.Ceiling(count * 0.3d));
            double total = 0d;
            for (int index = start; index < count; index++) total += player.GameContributions[index].RawScore;
            return total;
        }

        private static AwardCandidateResult CreateRawCandidate(
            PlayerCompetitionStatisticsState player,
            double value,
            string metricId)
        {
            return new AwardCandidateResult(
                player.PlayerId,
                player.PlayerName,
                player.TeamId,
                value,
                value,
                player.GamesPlayed,
                0d,
                new[] { new AwardScoreBreakdown(metricId, value, 1d) });
        }

        private static PlayerCompetitionStatisticsState[] ToSortedPlayers(
            IReadOnlyDictionary<int, PlayerCompetitionStatisticsState> source)
        {
            var result = new PlayerCompetitionStatisticsState[source.Count];
            int index = 0;
            foreach (PlayerCompetitionStatisticsState player in source.Values) result[index++] = player;
            Array.Sort(result, (left, right) => left.PlayerId.CompareTo(right.PlayerId));
            return result;
        }

        private static int GetTeamGames(SeasonState season, int teamId)
        {
            return season.GetTeamRecord(teamId)?.GamesPlayed ?? 0;
        }

        private static double GetWinningPercentage(SeasonState season, int teamId)
        {
            return season.GetTeamRecord(teamId)?.WinningPercentage ?? 0d;
        }
    }
}
