using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Simulation.Historical;
using Baseball.Simulation.Match;
using Baseball.Simulation.Random;

namespace Baseball.Game.Historical
{
    /// <summary>전체 구단주 조의 포스트시즌을 기존 상세 경기 엔진으로 순서대로 진행한다.</summary>
    public sealed class OwnerPostseasonService
    {
        private const ulong PostseasonStream = 0x4F574E4552504F53UL;
        private const int GameIdBase = 1_500_000;
        private readonly BalanceTable _balance;

        public OwnerPostseasonService(BalanceTable balance)
        {
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
        }

        /// <summary>모든 정규시즌 순위를 잠근 뒤 조별 시드를 한 번만 만든다.</summary>
        public void EnsureInitialized(ManagerHistoricalRuntimeState runtime)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            OwnerLeagueWorldState world = runtime.LeagueWorld ?? throw new InvalidOperationException("구단주 리그 월드가 없습니다.");
            if (!world.IsRegularSeasonCompleted) throw new InvalidOperationException("모든 조의 정규시즌 종료가 필요합니다.");
            var rankingService = new OwnerLeagueWorldService(_balance);
            for (int groupIndex = 0; groupIndex < world.Groups.Count; groupIndex++)
            {
                OwnerLeagueGroupState group = world.Groups[groupIndex];
                if (group.Postseason != null) continue;
                OwnerLeagueStanding[] ranking = rankingService.Rank(group.Season);
                var teamIds = new Dictionary<string, int>(StringComparer.Ordinal);
                for (int index = 0; index < group.Season.Teams.Count; index++)
                    teamIds.Add(group.Season.Teams[index].TeamSeasonKey, group.Season.Teams[index].TeamId);
                group.SetPostseason(new OwnerPostseasonState(group.Season.SeasonId,
                    OwnerPostseasonBracket.SelectSeeds(ranking, teamIds)));
                group.Postseason.EnsureCurrentSeries();
                // 저장되는 포스트시즌 생성 경계에서만 지급하므로 재진입·복원 시 위의 continue가 중복 지급을 막는다.
                if (string.Equals(ranking[0].TeamKey, runtime.PlayerTeamSeasonKey, StringComparison.Ordinal))
                    runtime.Economy.AddScoutingPoints(_balance.ScoutEconomy.PennantChampionshipScoutingPoints);
            }
        }

        /// <summary>월드 정렬 순서에서 다음 포스트시즌 경기 하나를 확정한다.</summary>
        public OwnerPostseasonAdvanceResult AdvanceNextGame(
            ManagerHistoricalRuntimeState runtime,
            ManagerModeMatchService matchService,
            IMatchEventSink playerEventSink = null,
            MatchExecutionProfile? playerExecutionProfile = null)
        {
            if (matchService == null) throw new ArgumentNullException(nameof(matchService));
            EnsureInitialized(runtime);
            OwnerLeagueWorldState world = runtime.LeagueWorld;
            for (int groupIndex = 0; groupIndex < world.Groups.Count; groupIndex++)
            {
                OwnerLeagueGroupState group = world.Groups[groupIndex];
                if (group.Postseason.IsCompleted) continue;
                OwnerPostseasonSeriesState series = group.Postseason.EnsureCurrentSeries();
                int seriesIndex = FindSeriesIndex(group.Postseason, series);
                var pendingGame = series.Games.Count > 0 && !series.Games[series.Games.Count - 1].IsCompleted
                    ? series.Games[series.Games.Count - 1] : null;
                int gameIndex = pendingGame == null ? series.Games.Count : series.Games.Count - 1;
                int gameId = checked(GameIdBase + group.Season.SeasonNumber * 100_000 + groupIndex * 1_000 + seriesIndex * 100 + gameIndex + 1);
                ulong seed = DeterministicSeed.Derive(runtime.WorldHistory.WorldHistorySeed,
                    PostseasonStream ^ (ulong)(uint)gameId);
                // 출전 검증에서 멈춘 경기는 같은 ID와 Seed로 재개한다.
                var game = pendingGame ?? series.AppendNextGame(gameId, seed);
                MatchResult match = matchService.PlayPostseasonGame(runtime, group, series, game,
                    playerEventSink, playerExecutionProfile, out ManagerModeMatchResult playerResult);
                RecordCompletedGame(runtime, group, series, game);
                return new OwnerPostseasonAdvanceResult(group.League.LeagueInstanceId, groupIndex,
                    series, game, match, playerResult, world.IsPostseasonCompleted);
            }
            throw new InvalidOperationException("진행할 포스트시즌 경기가 없습니다.");
        }

        private void RecordCompletedGame(ManagerHistoricalRuntimeState runtime, OwnerLeagueGroupState group,
            OwnerPostseasonSeriesState series, Baseball.Game.Career.ScheduledGameState game)
        {
            series.RecordCompletedGame(game);
            group.Postseason.EnsureCurrentSeries();
            // 시리즈 원장이 같은 경기의 재반영을 거부하므로 우승 상여도 한 번만 지급된다.
            if (group.Postseason.IsCompleted && string.Equals(
                group.Season.GetTeamSeasonKey(group.Postseason.ChampionTeamId),
                runtime.PlayerTeamSeasonKey, StringComparison.Ordinal))
                runtime.Economy.AddScoutingPoints(_balance.ScoutEconomy.PostseasonChampionshipScoutingPoints);
        }

        public int Complete(ManagerHistoricalRuntimeState runtime, ManagerModeMatchService matchService,
            Action<OwnerPostseasonAdvanceResult> gameCompleted = null)
        {
            EnsureInitialized(runtime);
            int count = 0;
            while (!runtime.LeagueWorld.IsPostseasonCompleted)
            {
                OwnerPostseasonAdvanceResult result = AdvanceNextGame(runtime, matchService);
                count++;
                gameCompleted?.Invoke(result);
            }
            return count;
        }

        private static int FindSeriesIndex(OwnerPostseasonState postseason, OwnerPostseasonSeriesState series)
        {
            for (int index = 0; index < postseason.Series.Count; index++)
                if (ReferenceEquals(postseason.Series[index], series)) return index;
            throw new InvalidOperationException("현재 시리즈를 찾을 수 없습니다.");
        }
    }

    /// <summary>한 포스트시즌 경기 확정 뒤 UI와 진행 세션이 소비하는 결과다.</summary>
    public sealed class OwnerPostseasonAdvanceResult
    {
        public OwnerPostseasonAdvanceResult(string leagueGroupId, int groupIndex,
            OwnerPostseasonSeriesState series, Baseball.Game.Career.ScheduledGameState game,
            MatchResult match, ManagerModeMatchResult playerMatch, bool isWorldCompleted)
        {
            LeagueGroupId = leagueGroupId ?? string.Empty;
            GroupIndex = groupIndex;
            Series = series ?? throw new ArgumentNullException(nameof(series));
            Game = game ?? throw new ArgumentNullException(nameof(game));
            Match = match ?? throw new ArgumentNullException(nameof(match));
            PlayerMatch = playerMatch;
            IsWorldCompleted = isWorldCompleted;
        }
        public string LeagueGroupId { get; }
        public int GroupIndex { get; }
        public OwnerPostseasonSeriesState Series { get; }
        public Baseball.Game.Career.ScheduledGameState Game { get; }
        public MatchResult Match { get; }
        public ManagerModeMatchResult PlayerMatch { get; }
        public bool IsWorldCompleted { get; }
    }
}
