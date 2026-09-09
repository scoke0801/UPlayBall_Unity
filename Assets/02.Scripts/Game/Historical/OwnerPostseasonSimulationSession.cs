using System;

namespace Baseball.Game.Historical
{
    /// <summary>프레임 단위 포스트시즌 진행량을 Unity UI에 전달하는 불변 값이다.</summary>
    public readonly struct OwnerPostseasonSimulationProgress
    {
        public OwnerPostseasonSimulationProgress(int completedGames, int maximumGames,
            int completedGroups, int totalGroups, string nextLeagueGroupId, string nextSeriesId)
        {
            CompletedGames = completedGames;
            MaximumGames = maximumGames;
            CompletedGroups = completedGroups;
            TotalGroups = totalGroups;
            NextLeagueGroupId = nextLeagueGroupId ?? string.Empty;
            NextSeriesId = nextSeriesId ?? string.Empty;
        }
        public int CompletedGames { get; }
        public int MaximumGames { get; }
        public int CompletedGroups { get; }
        public int TotalGroups { get; }
        public string NextLeagueGroupId { get; }
        public string NextSeriesId { get; }
    }

    /// <summary>메인 스레드 한 프레임에 상세 경기 하나만 처리하는 구단주 포스트시즌 세션이다.</summary>
    public sealed class OwnerPostseasonSimulationSession
    {
        private readonly ManagerHistoricalRuntimeState _runtime;
        private readonly ManagerModeMatchService _matchService;
        private readonly OwnerPostseasonService _postseasonService;
        private readonly int _maximumGames;

        public OwnerPostseasonSimulationSession(ManagerHistoricalRuntimeState runtime,
            ManagerModeMatchService matchService, Baseball.Core.Balance.BalanceTable balance)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _matchService = matchService ?? throw new ArgumentNullException(nameof(matchService));
            _postseasonService = new OwnerPostseasonService(balance ?? throw new ArgumentNullException(nameof(balance)));
            _postseasonService.EnsureInitialized(runtime);
            _maximumGames = CountMaximumGames(runtime, balance);
        }

        public bool IsCompleted => _runtime.LeagueWorld.IsPostseasonCompleted;
        public OwnerPostseasonAdvanceResult LastResult { get; private set; }

        public OwnerPostseasonAdvanceResult AdvanceNextStep()
        {
            if (IsCompleted) throw new InvalidOperationException("포스트시즌이 이미 완료됐습니다.");
            LastResult = _postseasonService.AdvanceNextGame(_runtime, _matchService);
            return LastResult;
        }

        public OwnerPostseasonSimulationProgress CreateProgressSnapshot()
        {
            int completedGames = 0;
            int completedGroups = 0;
            string groupId = string.Empty;
            string seriesId = string.Empty;
            for (int groupIndex = 0; groupIndex < _runtime.LeagueWorld.Groups.Count; groupIndex++)
            {
                OwnerLeagueGroupState group = _runtime.LeagueWorld.Groups[groupIndex];
                for (int seriesIndex = 0; seriesIndex < group.Postseason.Series.Count; seriesIndex++)
                {
                    OwnerPostseasonSeriesState series = group.Postseason.Series[seriesIndex];
                    for (int gameIndex = 0; gameIndex < series.Games.Count; gameIndex++)
                        if (series.Games[gameIndex].IsCompleted) completedGames++;
                }
                if (group.Postseason.IsCompleted) completedGroups++;
                else if (groupId.Length == 0)
                {
                    groupId = group.League.LeagueInstanceId;
                    seriesId = group.Postseason.CurrentSeries?.SeriesId ?? string.Empty;
                }
            }
            return new OwnerPostseasonSimulationProgress(completedGames, _maximumGames,
                completedGroups, _runtime.LeagueWorld.Groups.Count, groupId, seriesId);
        }

        private static int CountMaximumGames(ManagerHistoricalRuntimeState runtime,
            Baseball.Core.Balance.BalanceTable balance)
        {
            int count = 0;
            for (int index = 0; index < runtime.LeagueWorld.Groups.Count; index++)
            {
                int seeds = runtime.LeagueWorld.Groups[index].Postseason.SeedTeamIds.Count;
                count += seeds == 4
                    ? balance.Postseason.SemifinalSeriesGames * 2 + balance.Postseason.ChampionshipSeriesGames
                    : balance.Postseason.ChampionshipSeriesGames;
            }
            return count;
        }
    }
}
