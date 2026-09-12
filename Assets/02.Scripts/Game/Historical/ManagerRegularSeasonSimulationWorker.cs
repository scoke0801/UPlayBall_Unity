using System;
using System.Threading;
using System.Threading.Tasks;

namespace Baseball.Game.Historical
{
    /// <summary>한 작업 스레드가 시즌 상태를 독점하고 UI에는 완료된 경기의 진행값만 전달한다.</summary>
    public sealed class ManagerRegularSeasonSimulationWorker
    {
        private readonly ManagerRegularSeasonSimulationSession _session;
        private readonly Action<ManagerModeMatchResult> _onPlayerMatchCompleted;
        private readonly object _progressLock = new object();
        private readonly Task _task;
        private ManagerRegularSeasonSimulationProgress _progress;
        private int _stopRequested;

        /// <summary>호출자는 작업 종료까지 세션과 해당 Runtime을 읽거나 변경하지 않는다.</summary>
        public ManagerRegularSeasonSimulationWorker(ManagerRegularSeasonSimulationSession session,
            Action<ManagerModeMatchResult> onPlayerMatchCompleted = null)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _onPlayerMatchCompleted = onPlayerMatchCompleted;
            _progress = session.CreateProgressSnapshot();
            _task = Task.Run(Run);
        }

        public bool IsCompleted => _task.IsCompleted;
        public Exception Fault { get; private set; }
        public ManagerModeMatchResult LastMatch { get; private set; }

        /// <summary>Runtime 접근 없이 마지막으로 완료된 경기의 진행값을 복사한다.</summary>
        public ManagerRegularSeasonSimulationProgress ReadProgress()
        {
            lock (_progressLock) return _progress;
        }

        /// <summary>현재 경기를 마친 뒤 중단하도록 요청한다.</summary>
        public void RequestStop() => Interlocked.Exchange(ref _stopRequested, 1);

        /// <summary>장면 종료에서도 작업을 남기지 않고 경기 경계에서 상태 소유권을 회수한다.</summary>
        public void StopAndWait()
        {
            RequestStop();
            _task.GetAwaiter().GetResult();
        }

        /// <summary>장면 종료 원인을 보존하고 작업 스레드가 멈출 때까지 기다린다.</summary>
        public void AbortAndWait()
        {
            Interlocked.Exchange(ref _stopRequested, 2);
            _task.GetAwaiter().GetResult();
        }

        private void Run()
        {
            try
            {
                while (!_session.IsCompleted && Volatile.Read(ref _stopRequested) == 0)
                {
                    ManagerRegularSeasonSimulationStepResult step = _session.AdvanceNextStep();
                    if (step.MatchResult != null)
                    {
                        LastMatch = step.MatchResult;
                        _onPlayerMatchCompleted?.Invoke(step.MatchResult);
                    }
                    lock (_progressLock) _progress = step.Progress;
                }
                if (!_session.IsCompleted)
                {
                    if (Volatile.Read(ref _stopRequested) == 2) _session.AbortBySceneUnload();
                    else _session.StopByUser();
                }
            }
            catch (Exception exception)
            {
                // Task 예외를 방치하지 않고 소유 스레드에서 오류 표시와 상태 회수를 수행한다.
                Fault = exception;
            }
            finally
            {
                lock (_progressLock) _progress = _session.CreateProgressSnapshot();
            }
        }
    }
}
