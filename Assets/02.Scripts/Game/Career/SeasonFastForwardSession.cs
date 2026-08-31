using System;
using System.Diagnostics;
using System.Reflection;
using Baseball.Core.Balance;
using Baseball.Game.Career.News;

namespace Baseball.Game.Career
{
    public enum SeasonFastForwardExecutionMode
    {
        CooperativeMainThread = 0,
        WorkingCopyBackground = 1
    }

    public enum SeasonFastForwardStatus
    {
        Ready = 0,
        Running = 1,
        Completed = 2,
        Faulted = 3,
        StoppedByUser = 4,
        AbortedBySceneUnload = 5
    }

    /// <summary>
    /// 한 번의 안전한 시즌 진행 뒤 확정된 월드 경기 수와 단계 상태를 전달한다.
    /// </summary>
    public readonly struct SeasonFastForwardStepResult
    {
        public SeasonFastForwardStepResult(
            SeasonFastForwardStatus status,
            SeasonPhase targetPhase,
            int completedSteps,
            int processedWorldGames,
            int totalWorldGames,
            int lastCompletedRound,
            long lastStepElapsedTicks,
            long elapsedTicks,
            long allocatedBytes)
        {
            Status = status;
            TargetPhase = targetPhase;
            CompletedSteps = completedSteps;
            ProcessedWorldGames = processedWorldGames;
            TotalWorldGames = totalWorldGames;
            LastCompletedRound = lastCompletedRound;
            LastStepElapsedTicks = lastStepElapsedTicks;
            ElapsedTicks = elapsedTicks;
            AllocatedBytes = allocatedBytes;
        }

        public SeasonFastForwardStatus Status { get; }
        public SeasonPhase TargetPhase { get; }
        public int CompletedSteps { get; }
        public int ProcessedWorldGames { get; }
        public int TotalWorldGames { get; }
        public int LastCompletedRound { get; }
        public long LastStepElapsedTicks { get; }
        public long ElapsedTicks { get; }
        public long AllocatedBytes { get; }
        public bool IsCompleted => Status == SeasonFastForwardStatus.Completed;
        public bool IsStopped => Status is SeasonFastForwardStatus.StoppedByUser or
            SeasonFastForwardStatus.AbortedBySceneUnload;
        public bool HasKnownTotal => TotalWorldGames > 0;
    }

    /// <summary>
    /// 기존 시즌 진행 서비스를 월드 라운드 또는 포스트시즌 경기 단위로 재개 가능하게 실행한다.
    /// </summary>
    public sealed class SeasonFastForwardSession
    {
        private static bool _usesExactAllocationCounter;
        private static readonly Func<long> ReadAllocatedBytes = CreateAllocationCounter();

        private readonly CareerState _career;
        private readonly SeasonState _season;
        private readonly SeasonPhase _targetPhase;
        private readonly CareerSeasonService _regularSeason;
        private readonly CareerPostseasonService _postseason;
        private readonly int _regularSeasonGamesBefore;
        private readonly int _postseasonGamesBefore;
        private readonly int _worldGamesBefore;
        private readonly int _totalWorldGames;

        private SeasonFastForwardStatus _status;
        private int _completedSteps;
        private int _lastCompletedRound;
        private long _lastStepElapsedTicks;
        private long _elapsedTicks;
        private long _maximumStepElapsedTicks;
        private long _allocatedBytes;
        private int _generationZeroCollections;
        private Exception _fault;

        public SeasonFastForwardSession(
            CareerState career,
            BalanceTable balance,
            CareerNewsConfiguration newsConfiguration = null)
        {
            _career = career ?? throw new ArgumentNullException(nameof(career));
            if (balance == null) throw new ArgumentNullException(nameof(balance));
            _season = career.CurrentLeague.CurrentSeason ??
                      throw new InvalidOperationException("현재 시즌이 없습니다.");
            _targetPhase = _season.Phase;
            _regularSeasonGamesBefore = _season.PlayerStatistics.TeamGames;
            _postseasonGamesBefore = CountPostseasonGames(_season);
            _worldGamesBefore = CountCompletedWorldGames(career, _targetPhase);
            _totalWorldGames = _targetPhase == SeasonPhase.RegularSeason
                ? CountRemainingRegularSeasonGames(career)
                : 0;
            _generationZeroCollections = GC.CollectionCount(0);
            _status = SeasonFastForwardStatus.Ready;

            switch (_targetPhase)
            {
                case SeasonPhase.RegularSeason:
                    _regularSeason = new CareerSeasonService(career, balance, newsConfiguration);
                    if (_regularSeason.NextPlayerGame == null)
                        throw new InvalidOperationException("자동 진행할 정규시즌 경기가 없습니다.");
                    break;
                case SeasonPhase.Postseason:
                    _postseason = new CareerPostseasonService(career, balance, newsConfiguration);
                    if (_postseason.IsCompleted)
                        throw new InvalidOperationException("이미 끝난 포스트시즌입니다.");
                    break;
                default:
                    throw new InvalidOperationException(
                        "정규시즌 또는 포스트시즌에서만 자동 진행 세션을 만들 수 있습니다.");
            }
        }

        public SeasonFastForwardStatus Status => _status;
        public SeasonFastForwardExecutionMode ExecutionMode =>
            SeasonFastForwardExecutionMode.CooperativeMainThread;
        public SeasonPhase TargetPhase => _targetPhase;
        public int CompletedSteps => _completedSteps;
        public Exception Fault => _fault;
        public bool IsCompleted => _status == SeasonFastForwardStatus.Completed;
        public bool IsStopped => _status is SeasonFastForwardStatus.StoppedByUser or
            SeasonFastForwardStatus.AbortedBySceneUnload;

        /// <summary>
        /// 정규시즌은 월드 라운드 하나, 포스트시즌은 동기화된 다음 경기 하나를 확정한다.
        /// </summary>
        public SeasonFastForwardStepResult AdvanceNextStep()
        {
            if (_status == SeasonFastForwardStatus.Completed)
                throw new InvalidOperationException("이미 완료된 자동 진행 세션입니다.");
            if (_status == SeasonFastForwardStatus.Faulted)
                throw new InvalidOperationException("실패한 자동 진행 세션은 다시 진행할 수 없습니다.", _fault);
            if (IsStopped)
                throw new InvalidOperationException("중단된 자동 진행 세션은 다시 진행할 수 없습니다.");

            _status = SeasonFastForwardStatus.Running;
            long allocationBefore = ReadAllocatedBytes();
            long startedAt = Stopwatch.GetTimestamp();
            try
            {
                if (_targetPhase == SeasonPhase.RegularSeason)
                {
                    CareerGameAdvanceResult result = _regularSeason.AdvanceNextRound();
                    _lastCompletedRound = result.Round;
                }
                else
                {
                    _postseason.AdvanceNextGame();
                }

                _completedSteps++;
                if (HasReachedTargetBoundary())
                    _status = SeasonFastForwardStatus.Completed;
                RecordStepPerformance(startedAt, allocationBefore);
                return CreateStepResult();
            }
            catch (Exception exception)
            {
                RecordStepPerformance(startedAt, allocationBefore);
                _fault = exception;
                _status = SeasonFastForwardStatus.Faulted;
                throw;
            }
        }

        /// <summary>
        /// 지정한 안전 진행 단위 수만큼 처리하거나 현재 시즌 단계의 끝에서 멈춘다.
        /// </summary>
        public SeasonFastForwardStepResult AdvanceBatch(int maximumSteps)
        {
            if (maximumSteps <= 0)
                throw new ArgumentOutOfRangeException(nameof(maximumSteps));

            SeasonFastForwardStepResult result = CreateStepResult();
            int advanced = 0;
            while (!IsCompleted && !IsStopped && advanced < maximumSteps)
            {
                result = AdvanceNextStep();
                advanced++;
            }
            return result;
        }

        /// <summary>
        /// 현재 시즌 단계의 안전 경계를 모두 처리하고 기존 자동완료 결과를 반환한다.
        /// </summary>
        public CareerSeasonAutoCompletionResult Complete()
        {
            while (!IsCompleted)
                AdvanceNextStep();
            return CreateCompletionResult();
        }

        public SeasonFastForwardStepResult CreateProgressSnapshot() => CreateStepResult();

        /// <summary>현재 안전 경계까지의 결과는 유지하고 사용자 요청으로 더 진행하지 않는다.</summary>
        public SeasonFastForwardStepResult StopByUser()
        {
            Stop(SeasonFastForwardStatus.StoppedByUser);
            return CreateStepResult();
        }

        /// <summary>화면이 사라진 안전 경계에서 세션을 폐기하고 더 진행하지 않는다.</summary>
        public SeasonFastForwardStepResult AbortBySceneUnload()
        {
            Stop(SeasonFastForwardStatus.AbortedBySceneUnload);
            return CreateStepResult();
        }

        /// <summary>Player·Editor 실행에서 수집한 세션 시간과 관리 할당 요약을 만든다.</summary>
        public SeasonFastForwardPerformanceReport CreatePerformanceReport()
        {
            SeasonFastForwardStepResult progress = CreateStepResult();
            return new SeasonFastForwardPerformanceReport(
                _targetPhase,
                _status,
                _completedSteps,
                progress.ProcessedWorldGames,
                _elapsedTicks,
                _maximumStepElapsedTicks,
                _allocatedBytes,
                GC.CollectionCount(0) - _generationZeroCollections,
                _usesExactAllocationCounter);
        }

        public CareerSeasonAutoCompletionResult CreateCompletedResult() => CreateCompletionResult();

        private bool HasReachedTargetBoundary()
        {
            return _targetPhase switch
            {
                SeasonPhase.RegularSeason =>
                    _season.Phase == SeasonPhase.Postseason && _season.Postseason != null,
                SeasonPhase.Postseason =>
                    _season.Phase == SeasonPhase.SeasonReview && _season.Postseason?.IsCompleted == true,
                _ => false
            };
        }

        private SeasonFastForwardStepResult CreateStepResult()
        {
            int processedWorldGames = CountCompletedWorldGames(_career, _targetPhase) - _worldGamesBefore;
            return new SeasonFastForwardStepResult(
                _status,
                _targetPhase,
                _completedSteps,
                processedWorldGames,
                _totalWorldGames,
                _lastCompletedRound,
                _lastStepElapsedTicks,
                _elapsedTicks,
                _allocatedBytes);
        }

        private void Stop(SeasonFastForwardStatus stopStatus)
        {
            if (_status == SeasonFastForwardStatus.Completed || _status == SeasonFastForwardStatus.Faulted)
                return;
            _status = stopStatus;
        }

        private void RecordStepPerformance(long startedAt, long allocationBefore)
        {
            _lastStepElapsedTicks = Stopwatch.GetTimestamp() - startedAt;
            _elapsedTicks += _lastStepElapsedTicks;
            if (_lastStepElapsedTicks > _maximumStepElapsedTicks)
                _maximumStepElapsedTicks = _lastStepElapsedTicks;
            long allocated = ReadAllocatedBytes() - allocationBefore;
            if (allocated > 0)
                _allocatedBytes += allocated;
        }

        private static Func<long> CreateAllocationCounter()
        {
            MethodInfo method = typeof(GC).GetMethod(
                "GetAllocatedBytesForCurrentThread",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null);
            if (method != null && method.ReturnType == typeof(long))
            {
                try
                {
                    _usesExactAllocationCounter = true;
                    return (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), method);
                }
                // 일부 IL2CPP 프로필은 메서드 메타데이터를 보여도 delegate 생성을 지원하지 않는다.
                catch (Exception)
                {
                    _usesExactAllocationCounter = false;
                }
            }

            return () => GC.GetTotalMemory(forceFullCollection: false);
        }

        private CareerSeasonAutoCompletionResult CreateCompletionResult()
        {
            if (!IsCompleted)
                throw new InvalidOperationException("완료되지 않은 자동 진행 세션입니다.");

            if (_targetPhase == SeasonPhase.RegularSeason)
            {
                return new CareerSeasonAutoCompletionResult(
                    SeasonPhase.RegularSeason,
                    _season.PlayerStatistics.TeamGames - _regularSeasonGamesBefore,
                    postseasonGames: 0,
                    championTeamId: 0,
                    isPlayerTeamChampion: false);
            }

            return new CareerSeasonAutoCompletionResult(
                SeasonPhase.Postseason,
                regularSeasonGames: 0,
                CountPostseasonGames(_season) - _postseasonGamesBefore,
                _season.Postseason.ChampionTeamId,
                _season.Postseason.ChampionTeamId == _career.MyPlayer.CurrentTeamId);
        }

        private static int CountRemainingRegularSeasonGames(CareerState career)
        {
            int count = 0;
            for (int leagueIndex = 0; leagueIndex < career.World.Leagues.Count; leagueIndex++)
            {
                SeasonState season = career.World.Leagues[leagueIndex].CurrentSeason;
                if (season?.Schedule == null)
                    continue;
                for (int gameIndex = 0; gameIndex < season.Schedule.Games.Count; gameIndex++)
                {
                    if (!season.Schedule.Games[gameIndex].IsCompleted)
                        count++;
                }
            }
            return count;
        }

        private static int CountCompletedWorldGames(CareerState career, SeasonPhase phase)
        {
            int count = 0;
            for (int leagueIndex = 0; leagueIndex < career.World.Leagues.Count; leagueIndex++)
            {
                SeasonState season = career.World.Leagues[leagueIndex].CurrentSeason;
                if (season == null)
                    continue;
                if (phase == SeasonPhase.RegularSeason && season.Schedule != null)
                {
                    for (int gameIndex = 0; gameIndex < season.Schedule.Games.Count; gameIndex++)
                    {
                        if (season.Schedule.Games[gameIndex].IsCompleted)
                            count++;
                    }
                }
                else if (phase == SeasonPhase.Postseason)
                {
                    count += CountPostseasonGames(season);
                }
            }
            return count;
        }

        private static int CountPostseasonGames(SeasonState season)
        {
            if (season?.Postseason == null)
                return 0;

            int count = 0;
            for (int index = 0; index < season.Postseason.Series.Count; index++)
                count += season.Postseason.Series[index].Games.Count;
            return count;
        }
    }
}
