using System;
using System.Collections.Generic;
using Baseball.Game.Career;

namespace Baseball.Game.Historical
{
    public enum ManagerRegularSeasonSimulationStatus
    {
        Ready = 0,
        Running = 1,
        Completed = 2,
        Faulted = 3,
        StoppedByUser = 4,
        AbortedBySceneUnload = 5
    }

    /// <summary>구단주 정규시즌 자동 진행 팝업이 Runtime을 직접 읽지 않도록 안전 단위 진행값을 복사한다.</summary>
    public readonly struct ManagerRegularSeasonSimulationProgress
    {
        public ManagerRegularSeasonSimulationProgress(
            ManagerRegularSeasonSimulationStatus status,
            int playerGamesSimulated,
            int totalPlayerGames,
            int leagueGamesSimulated,
            int totalLeagueGames,
            int lastCompletedRound,
            int nextRound,
            string nextAwayTeamSeasonKey,
            string nextHomeTeamSeasonKey,
            int seasonWins,
            int seasonLosses,
            int seasonDraws)
        {
            Status = status;
            PlayerGamesSimulated = playerGamesSimulated;
            TotalPlayerGames = totalPlayerGames;
            LeagueGamesSimulated = leagueGamesSimulated;
            TotalLeagueGames = totalLeagueGames;
            LastCompletedRound = lastCompletedRound;
            NextRound = nextRound;
            NextAwayTeamSeasonKey = nextAwayTeamSeasonKey ?? string.Empty;
            NextHomeTeamSeasonKey = nextHomeTeamSeasonKey ?? string.Empty;
            SeasonWins = seasonWins;
            SeasonLosses = seasonLosses;
            SeasonDraws = seasonDraws;
        }

        public ManagerRegularSeasonSimulationStatus Status { get; }
        public int PlayerGamesSimulated { get; }
        public int TotalPlayerGames { get; }
        public int LeagueGamesSimulated { get; }
        public int TotalLeagueGames { get; }
        public int LastCompletedRound { get; }
        public int NextRound { get; }
        public string NextAwayTeamSeasonKey { get; }
        public string NextHomeTeamSeasonKey { get; }
        public int SeasonWins { get; }
        public int SeasonLosses { get; }
        public int SeasonDraws { get; }
        public bool IsCompleted => Status == ManagerRegularSeasonSimulationStatus.Completed;
        public bool IsStopped => Status is ManagerRegularSeasonSimulationStatus.StoppedByUser or
            ManagerRegularSeasonSimulationStatus.AbortedBySceneUnload;
    }

    /// <summary>한 프레임에서 확정한 플레이어 경기와 갱신된 시즌 진행값을 함께 반환한다.</summary>
    public readonly struct ManagerRegularSeasonSimulationStepResult
    {
        public ManagerRegularSeasonSimulationStepResult(
            ManagerRegularSeasonSimulationProgress progress,
            ManagerModeMatchResult matchResult)
        {
            Progress = progress;
            MatchResult = matchResult;
        }

        public ManagerRegularSeasonSimulationProgress Progress { get; }
        public ManagerModeMatchResult MatchResult { get; }
    }

    /// <summary>남은 정규시즌을 Detailed 경기 단위로 잘라 재개 가능하게 실행한다.</summary>
    public sealed class ManagerRegularSeasonSimulationSession
    {
        private readonly ManagerHistoricalRuntimeState _runtime;
        private readonly ManagerModeMatchService _matchService;
        private readonly ManagerModeMatchService.PlayerIdMap _playerIds;
        private readonly ManagerModeMatchService.AiScheduleCursor _aiScheduleCursor;
        private readonly ManagerLiveSeasonState _season;
        private readonly int _completedLeagueGamesBefore;
        private readonly int _totalPlayerGames;
        private readonly int _totalLeagueGames;
        private ManagerRegularSeasonSimulationStatus _status;
        private int _playerGamesSimulated;
        private int _leagueGamesSimulated;
        private int _lastCompletedRound;
        private Exception _fault;
        private int _seasonWins;
        private int _seasonLosses;
        private int _seasonDraws;
        private ScheduledGameState _nextPlayerGame;

        public ManagerRegularSeasonSimulationSession(
            ManagerHistoricalRuntimeState runtime,
            ManagerModeMatchService matchService)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _matchService = matchService ?? throw new ArgumentNullException(nameof(matchService));
            if (!runtime.HasManagerMode)
                throw new InvalidOperationException("ManagerMode 상태가 없는 Save는 시즌을 진행할 수 없습니다.");

            _season = runtime.ManagerMode.LiveSeason;
            _nextPlayerGame = _season.NextPlayerGame;
            ResolvePlayerSeasonRecord(_season, out _seasonWins, out _seasonLosses, out _seasonDraws);
            _playerIds = ManagerModeMatchService.PlayerIdMap.Create(runtime);
            _aiScheduleCursor = ManagerModeMatchService.AiScheduleCursor.Create(runtime);
            _completedLeagueGamesBefore = CountWorldGames(runtime, completedOnly: true);
            _totalPlayerGames = CountRemainingPlayerGames(_season);
            _totalLeagueGames = CountWorldGames(runtime, completedOnly: false) - _completedLeagueGamesBefore;
            _status = _totalLeagueGames == 0
                ? ManagerRegularSeasonSimulationStatus.Completed
                : ManagerRegularSeasonSimulationStatus.Ready;
        }

        public ManagerRegularSeasonSimulationStatus Status => _status;
        public Exception Fault => _fault;
        public bool IsCompleted => _status == ManagerRegularSeasonSimulationStatus.Completed;
        public bool IsStopped => _status is ManagerRegularSeasonSimulationStatus.StoppedByUser or
            ManagerRegularSeasonSimulationStatus.AbortedBySceneUnload;

        /// <summary>UI가 매 프레임 양보할 수 있도록 Detailed 경기를 최대 한 건만 확정한다.</summary>
        public ManagerRegularSeasonSimulationStepResult AdvanceNextStep()
        {
            if (IsCompleted)
                throw new InvalidOperationException("이미 완료된 시즌 자동 진행 세션입니다.");
            if (_status == ManagerRegularSeasonSimulationStatus.Faulted)
                throw new InvalidOperationException("실패한 시즌 자동 진행 세션은 다시 진행할 수 없습니다.", _fault);
            if (IsStopped)
                throw new InvalidOperationException("중단된 시즌 자동 진행 세션은 다시 진행할 수 없습니다.");

            _status = ManagerRegularSeasonSimulationStatus.Running;
            try
            {
                ScheduledGameState nextGame = _nextPlayerGame;
                ManagerModeMatchResult matchResult = null;
                bool didAdvanceGame = false;
                int aiThroughRound = nextGame == null ? int.MaxValue : nextGame.Round - 1;
                if (_matchService.TrySimulateNextAiGameThrough(
                    _runtime, _playerIds, _aiScheduleCursor, aiThroughRound, out int completedAiRound))
                {
                    _lastCompletedRound = completedAiRound;
                    didAdvanceGame = true;
                    _leagueGamesSimulated++;
                }
                else if (nextGame != null)
                {
                    int completedRound = nextGame.Round;
                    matchResult = _matchService.PlayNextPlayerGameForSeasonSimulation(_runtime, _playerIds);
                    // 다른 조 수만 경기를 처리할 때 내 구단 일정을 반복 집계하지 않는다.
                    ResolvePlayerSeasonRecord(_season, out _seasonWins, out _seasonLosses, out _seasonDraws);
                    _nextPlayerGame = _season.NextPlayerGame;
                    _lastCompletedRound = completedRound;
                    _playerGamesSimulated++;
                    didAdvanceGame = true;
                    _leagueGamesSimulated++;
                }

                if (_nextPlayerGame == null && !didAdvanceGame)
                {
                    bool hasRemainingAiGame = _matchService.TrySimulateNextAiGameThrough(
                        _runtime, _playerIds, _aiScheduleCursor, int.MaxValue, out int remainingAiRound);
                    if (hasRemainingAiGame)
                    {
                        _lastCompletedRound = remainingAiRound;
                        _leagueGamesSimulated++;
                    }
                    else if (!_season.IsCompleted ||
                             _runtime.LeagueWorld != null && !_runtime.LeagueWorld.IsRegularSeasonCompleted)
                    {
                        throw new InvalidOperationException("플레이어 일정 종료 뒤에도 미완료 AI 대진이 남아 있습니다.");
                    }
                    else
                    {
                        _status = ManagerRegularSeasonSimulationStatus.Completed;
                    }
                }

                return new ManagerRegularSeasonSimulationStepResult(CreateProgressSnapshot(), matchResult);
            }
            catch (Exception exception)
            {
                _fault = exception;
                _status = ManagerRegularSeasonSimulationStatus.Faulted;
                throw;
            }
        }

        public ManagerRegularSeasonSimulationProgress CreateProgressSnapshot()
        {
            ScheduledGameState nextGame = _nextPlayerGame;
            return new ManagerRegularSeasonSimulationProgress(
                _status,
                _playerGamesSimulated,
                _totalPlayerGames,
                _leagueGamesSimulated,
                _totalLeagueGames,
                _lastCompletedRound,
                nextGame?.Round ?? 0,
                nextGame == null ? string.Empty : _season.GetTeamSeasonKey(nextGame.AwayTeamId),
                nextGame == null ? string.Empty : _season.GetTeamSeasonKey(nextGame.HomeTeamId),
                _seasonWins,
                _seasonLosses,
                _seasonDraws);
        }

        public ManagerRegularSeasonCompletionResult CreateCompletionResult()
        {
            if (!IsCompleted)
                throw new InvalidOperationException("완료된 시즌 자동 진행 세션만 최종 결과를 만들 수 있습니다.");
            ManagerRegularSeasonSimulationProgress progress = CreateProgressSnapshot();
            return new ManagerRegularSeasonCompletionResult(
                progress.PlayerGamesSimulated,
                progress.LeagueGamesSimulated,
                progress.SeasonWins,
                progress.SeasonLosses,
                progress.SeasonDraws,
                true);
        }

        public ManagerRegularSeasonSimulationProgress StopByUser()
        {
            Stop(ManagerRegularSeasonSimulationStatus.StoppedByUser);
            return CreateProgressSnapshot();
        }

        public ManagerRegularSeasonSimulationProgress AbortBySceneUnload()
        {
            Stop(ManagerRegularSeasonSimulationStatus.AbortedBySceneUnload);
            return CreateProgressSnapshot();
        }

        private void Stop(ManagerRegularSeasonSimulationStatus status)
        {
            if (IsCompleted || _status == ManagerRegularSeasonSimulationStatus.Faulted || IsStopped) return;
            _status = status;
        }

        private static int CountRemainingPlayerGames(ManagerLiveSeasonState season)
        {
            int count = 0;
            IReadOnlyList<ScheduledGameState> games = season.Schedule.Games;
            for (int index = 0; index < games.Count; index++)
                if (!games[index].IsCompleted && games[index].IncludesTeam(season.PlayerTeamId)) count++;
            return count;
        }

        private static int CountWorldGames(ManagerHistoricalRuntimeState runtime, bool completedOnly)
        {
            if (runtime.LeagueWorld == null)
                return completedOnly ? CountCompletedGames(runtime.ManagerMode.LiveSeason.Schedule.Games) : runtime.ManagerMode.LiveSeason.Schedule.Games.Count;
            int count = 0;
            foreach (var group in runtime.LeagueWorld.Groups)
                count += completedOnly ? CountCompletedGames(group.Season.Schedule.Games) : group.Season.Schedule.Games.Count;
            return count;
        }

        private static int CountCompletedGames(IReadOnlyList<ScheduledGameState> games)
        {
            int count = 0;
            for (int index = 0; index < games.Count; index++)
                if (games[index].IsCompleted) count++;
            return count;
        }

        private static void ResolvePlayerSeasonRecord(
            ManagerLiveSeasonState season,
            out int wins,
            out int losses,
            out int draws)
        {
            wins = 0;
            losses = 0;
            draws = 0;
            IReadOnlyList<ScheduledGameState> games = season.Schedule.Games;
            for (int index = 0; index < games.Count; index++)
            {
                ScheduledGameState game = games[index];
                if (!game.IsCompleted || !game.IncludesTeam(season.PlayerTeamId)) continue;
                if (game.AwayRuns == game.HomeRuns)
                {
                    draws++;
                    continue;
                }

                bool isWin = game.AwayTeamId == season.PlayerTeamId
                    ? game.AwayRuns > game.HomeRuns
                    : game.HomeRuns > game.AwayRuns;
                if (isWin) wins++;
                else losses++;
            }
        }
    }
}
