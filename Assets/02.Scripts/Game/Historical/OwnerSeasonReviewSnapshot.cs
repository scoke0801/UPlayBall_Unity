using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Simulation.Historical;

namespace Baseball.Game.Historical
{
    /// <summary>시즌 연출이 계산 없이 표시하는 정규시즌·포스트시즌·승강 결산이다.</summary>
    public sealed class OwnerSeasonReviewSnapshot
    {
        public OwnerSeasonReviewSnapshot(int seasonNumber, LeagueGrade currentGrade, LeagueGrade? nextGrade,
            string playerTeamSeasonKey, int rank, int teamCount, int wins, int losses, int draws,
            int runs, int runsAllowed, bool isPostseasonInitialized, bool isPostseasonCompleted,
            bool isQualified, OwnerTeamPostseasonResult? postseasonResult, string championTeamSeasonKey,
            IReadOnlyList<OwnerPostseasonSeriesReview> series)
            : this(seasonNumber, currentGrade, nextGrade, playerTeamSeasonKey, rank, teamCount,
                wins, losses, draws, runs, runsAllowed, isPostseasonInitialized,
                isPostseasonCompleted, isPostseasonCompleted,
                isPostseasonCompleted ? 1 : 0, 1, isQualified, postseasonResult,
                championTeamSeasonKey, series)
        {
        }

        public OwnerSeasonReviewSnapshot(int seasonNumber, LeagueGrade currentGrade, LeagueGrade? nextGrade,
            string playerTeamSeasonKey, int rank, int teamCount, int wins, int losses, int draws,
            int runs, int runsAllowed, bool isPostseasonInitialized, bool isPlayerPostseasonCompleted,
            bool isPostseasonCompleted, int completedPostseasonGroups, int totalPostseasonGroups,
            bool isQualified, OwnerTeamPostseasonResult? postseasonResult, string championTeamSeasonKey,
            IReadOnlyList<OwnerPostseasonSeriesReview> series)
        {
            SeasonNumber = seasonNumber;
            CurrentGrade = currentGrade;
            NextGrade = nextGrade;
            PlayerTeamSeasonKey = playerTeamSeasonKey ?? string.Empty;
            Rank = rank;
            TeamCount = teamCount;
            Wins = wins;
            Losses = losses;
            Draws = draws;
            Runs = runs;
            RunsAllowed = runsAllowed;
            IsPostseasonInitialized = isPostseasonInitialized;
            IsPlayerPostseasonCompleted = isPlayerPostseasonCompleted;
            IsPostseasonCompleted = isPostseasonCompleted;
            CompletedPostseasonGroups = Math.Max(0, completedPostseasonGroups);
            TotalPostseasonGroups = Math.Max(0, totalPostseasonGroups);
            IsQualified = isQualified;
            PostseasonResult = postseasonResult;
            ChampionTeamSeasonKey = championTeamSeasonKey ?? string.Empty;
            Series = series ?? Array.Empty<OwnerPostseasonSeriesReview>();
        }
        public int SeasonNumber { get; }
        public LeagueGrade CurrentGrade { get; }
        public LeagueGrade? NextGrade { get; }
        public string PlayerTeamSeasonKey { get; }
        public int Rank { get; }
        public int TeamCount { get; }
        public int Wins { get; }
        public int Losses { get; }
        public int Draws { get; }
        public int Runs { get; }
        public int RunsAllowed { get; }
        public int RunDifferential => Runs - RunsAllowed;
        public double WinningPercentage => Wins + Losses == 0 ? 0d : Wins / (double)(Wins + Losses);
        public bool IsPostseasonInitialized { get; }
        public bool IsPlayerPostseasonCompleted { get; }
        /// <summary>다음 시즌 진입 조건인 전체 월드 포스트시즌 완료 여부다.</summary>
        public bool IsPostseasonCompleted { get; }
        public int CompletedPostseasonGroups { get; }
        public int TotalPostseasonGroups { get; }
        public bool IsQualified { get; }
        public OwnerTeamPostseasonResult? PostseasonResult { get; }
        public string ChampionTeamSeasonKey { get; }
        public IReadOnlyList<OwnerPostseasonSeriesReview> Series { get; }

        /// <summary>탈락한 시리즈를 다시 관전 대상으로 안내하지 않는다.</summary>
        public OwnerPostseasonSeriesReview PlayerSeries
        {
            get
            {
                for (int index = Series.Count - 1; index >= 0; index--)
                {
                    OwnerPostseasonSeriesReview series = Series[index];
                    if (series.HigherSeedTeamSeasonKey == PlayerTeamSeasonKey ||
                        series.LowerSeedTeamSeasonKey == PlayerTeamSeasonKey) return series;
                }
                return null;
            }
        }
        public bool CanWatchPlayerGame => IsQualified && !IsPlayerPostseasonCompleted &&
            (PlayerSeries == null || !PlayerSeries.IsCompleted ||
             (PlayerSeries.HigherSeedTeamSeasonKey == PlayerTeamSeasonKey
                 ? PlayerSeries.HigherSeedWins : PlayerSeries.LowerSeedWins) == PlayerSeries.WinsRequired);
    }

    public sealed class OwnerPostseasonSeriesReview
    {
        public OwnerPostseasonSeriesReview(string seriesId, OwnerPostseasonRound round,
            string higherSeedTeamSeasonKey, string lowerSeedTeamSeasonKey,
            int higherSeedWins, int lowerSeedWins, int winsRequired, bool isCompleted)
        {
            SeriesId = seriesId ?? string.Empty;
            Round = round;
            HigherSeedTeamSeasonKey = higherSeedTeamSeasonKey ?? string.Empty;
            LowerSeedTeamSeasonKey = lowerSeedTeamSeasonKey ?? string.Empty;
            HigherSeedWins = higherSeedWins;
            LowerSeedWins = lowerSeedWins;
            WinsRequired = winsRequired;
            IsCompleted = isCompleted;
        }
        public string SeriesId { get; }
        public OwnerPostseasonRound Round { get; }
        public string HigherSeedTeamSeasonKey { get; }
        public string LowerSeedTeamSeasonKey { get; }
        public int HigherSeedWins { get; }
        public int LowerSeedWins { get; }
        public int WinsRequired { get; }
        public bool IsCompleted { get; }
    }

    /// <summary>현재 월드의 확정 상태에서 시즌 연출용 불변 결산을 만든다.</summary>
    public static class OwnerSeasonReviewService
    {
        public static OwnerSeasonReviewSnapshot Create(ManagerHistoricalRuntimeState runtime,
            Baseball.Core.Balance.BalanceTable balance)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (balance == null) throw new ArgumentNullException(nameof(balance));
            OwnerLeagueGroupState group = runtime.LeagueWorld.GetGroup(runtime.PlayerTeamSeasonKey);
            OwnerLeagueStanding[] ranking = new OwnerLeagueWorldService(balance).Rank(group.Season);
            int rank = 0;
            OwnerLeagueStanding player = null;
            for (int index = 0; index < ranking.Length; index++)
            {
                if (!string.Equals(ranking[index].TeamKey, runtime.PlayerTeamSeasonKey, StringComparison.Ordinal)) continue;
                rank = index + 1;
                player = ranking[index];
                break;
            }
            if (player == null) throw new InvalidOperationException("플레이어 구단의 최종 순위를 찾을 수 없습니다.");

            int draws = group.Season.GetCompletedGameCount(group.Season.PlayerTeamId) - player.Wins - player.Losses;
            int runs = 0;
            int allowed = 0;
            foreach (var game in group.Season.Schedule.Games)
            {
                if (!game.IsCompleted || !game.IncludesTeam(group.Season.PlayerTeamId)) continue;
                bool home = game.HomeTeamId == group.Season.PlayerTeamId;
                runs += home ? game.HomeRuns : game.AwayRuns;
                allowed += home ? game.AwayRuns : game.HomeRuns;
            }
            OwnerPostseasonState postseason = group.Postseason;
            var series = new List<OwnerPostseasonSeriesReview>();
            string championKey = string.Empty;
            OwnerTeamPostseasonResult? result = null;
            if (postseason != null)
            {
                for (int index = 0; index < postseason.Series.Count; index++)
                {
                    OwnerPostseasonSeriesState item = postseason.Series[index];
                    series.Add(new OwnerPostseasonSeriesReview(item.SeriesId, item.Round,
                        group.Season.GetTeamSeasonKey(item.HigherSeedTeamId),
                        group.Season.GetTeamSeasonKey(item.LowerSeedTeamId),
                        item.HigherSeedWins, item.LowerSeedWins, item.WinsRequired, item.IsCompleted));
                }
                if (postseason.IsCompleted)
                {
                    championKey = group.Season.GetTeamSeasonKey(postseason.ChampionTeamId);
                    result = postseason.GetTeamResult(group.Season.PlayerTeamId);
                }
            }
            LeagueGrade? nextGrade = null;
            if (runtime.LeagueWorld.IsPostseasonCompleted)
                nextGrade = new OwnerLeagueWorldService(balance).ResolveNextGrade(runtime, runtime.PlayerTeamSeasonKey);
            int completedGroups = 0;
            for (int index = 0; index < runtime.LeagueWorld.Groups.Count; index++)
                if (runtime.LeagueWorld.Groups[index].Postseason?.IsCompleted == true) completedGroups++;
            return new OwnerSeasonReviewSnapshot(group.Season.SeasonNumber, group.League.Grade, nextGrade,
                runtime.PlayerTeamSeasonKey, rank, ranking.Length, player.Wins, player.Losses, Math.Max(0, draws),
                runs, allowed, postseason != null, postseason?.IsCompleted == true,
                runtime.LeagueWorld.IsPostseasonCompleted, completedGroups, runtime.LeagueWorld.Groups.Count,
                postseason?.IsQualified(group.Season.PlayerTeamId) == true, result, championKey, series);
        }
    }
}
