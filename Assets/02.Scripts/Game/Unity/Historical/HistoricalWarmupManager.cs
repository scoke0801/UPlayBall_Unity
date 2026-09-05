using System;
using System.Threading;
using System.Threading.Tasks;
using Baseball.Game.Career;
using Baseball.Game.Data;
using Baseball.Game.Manager;
using UnityEngine;

namespace Baseball.Game.Historical
{
    /// <summary>워밍업이 지금 어느 단계인지 나타낸다.</summary>
    public enum HistoricalWarmupState
    {
        Idle = 0,
        ReadingAssets = 1,
        Building = 2,
        Completed = 3,
        Failed = 4,
        Canceled = 5
    }

    /// <summary>
    /// Boot~Loading 구간에서 역사 콘텐츠와 World를 미리 만들어 둔다.
    /// 타이틀에서 어느 모드를 눌러도 이미 만들어 둔 결과를 그대로 쓰게 하는 것이 목적이다.
    ///
    /// 실패하거나 취소해도 게임은 계속 진행한다. 워밍업은 비용을 앞당길 뿐이고,
    /// 각 진입점은 캐시가 없으면 스스로 만드는 경로를 그대로 갖고 있다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HistoricalWarmupManager : ManagerBehaviour<HistoricalWarmupManager>
    {
        /// <summary>Domain Reload를 이 시간 이상 붙잡지 않는다.</summary>
        private const int CancelWaitMilliseconds = 2000;

        private UnityHistoricalContentProvider _contentProvider;
        private CancellationTokenSource _cancellation;
        private Task _warmupTask;
        private bool _isFailureReported;
        private volatile int _progressPermille;
        private volatile int _state = (int)HistoricalWarmupState.Idle;
        private string _lastError = string.Empty;

        public override int InitializationOrder => -10;

        public HistoricalWarmupState State => (HistoricalWarmupState)_state;
        public float Progress => _progressPermille / 1000f;

        public bool IsRunning =>
            State == HistoricalWarmupState.ReadingAssets || State == HistoricalWarmupState.Building;

        public string LastError
        {
            get { lock (_stateLock) return _lastError; }
            private set { lock (_stateLock) _lastError = value; }
        }

        private readonly object _stateLock = new object();

        protected override void OnShutdown()
        {
            CancelAndWait();
            _contentProvider = null;
            _isFailureReported = false;
        }

        private void OnDisable()
        {
            // Play Mode 종료와 Domain Reload 모두 여기를 지난다.
            // 워커가 44시즌을 돌고 있는 채로 Domain이 내려가면 Editor가 멈춘다.
            CancelAndWait();
        }

        private void OnApplicationQuit()
        {
            CancelAndWait();
        }

        /// <summary>
        /// 워밍업을 시작한다. 메인 스레드에서 호출해야 하며, TextAsset 바이트 확보만 여기서 하고
        /// 파싱과 World 생성은 워커 스레드로 넘긴다. 이미 시작했다면 아무것도 하지 않는다.
        /// </summary>
        public void BeginWarmup()
        {
            if (State != HistoricalWarmupState.Idle)
                return;

            bool hasBakedWorld;
            try
            {
                _state = (int)HistoricalWarmupState.ReadingAssets;
                _contentProvider = NewGameDefinition.LoadSharedHistoricalContentProvider();
                _contentProvider.CacheAssetBytesOnMainThread();

                // Bake가 없으면 World 준비는 44시즌을 실제로 돌린다는 뜻이다.
                // 그 작업을 백그라운드에 띄워 두면 Editor에서 Domain Reload를 붙잡고,
                // 빌드에서도 로딩 화면이 수십 초 길어진다. 그럴 바에는 준비하지 않는다.
                hasBakedWorld = NewGameDefinition.LoadBakedWorldHistorySource() != null;
                SetProgress(0.05f);
            }
            catch (Exception exception)
            {
                Fail("역사 콘텐츠 Asset을 읽지 못했습니다.", exception);
                return;
            }

            OwnerModeManager ownerMode = OwnerModeManager.Instance;
            NewGameManager newGame = NewGameManager.Instance;
            UnityHistoricalContentProvider provider = _contentProvider;
            _cancellation = new CancellationTokenSource();
            CancellationToken token = _cancellation.Token;
            _state = (int)HistoricalWarmupState.Building;
            _warmupTask = Task.Run(
                () => RunWarmup(provider, ownerMode, newGame, hasBakedWorld, token),
                token);
        }

        private void RunWarmup(
            UnityHistoricalContentProvider contentProvider,
            OwnerModeManager ownerMode,
            NewGameManager newGame,
            bool hasBakedWorld,
            CancellationToken cancellationToken)
        {
            try
            {
                // 1) 23.6MB 역사 payload 파싱. 두 모드가 같은 Provider 인스턴스를 공유한다.
                contentProvider.Load();
                cancellationToken.ThrowIfCancellationRequested();
                SetProgress(hasBakedWorld ? 0.35f : 1f);

                if (hasBakedWorld)
                {
                    // 2) 구단주 모드 시작 World. Bake를 복원하므로 짧게 끝난다.
                    ownerMode?.PrewarmNewGameWorld(cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    SetProgress(0.75f);

                    // 3) 커리어 "구단 오퍼 확인"이 쓸 Content.
                    newGame?.PrewarmCareerContent(cancellationToken);
                    SetProgress(1f);
                }

                contentProvider.ReleaseAssetByteCache();
                _state = (int)HistoricalWarmupState.Completed;
            }
            catch (OperationCanceledException)
            {
                SetProgress(1f);
                _state = (int)HistoricalWarmupState.Canceled;
            }
            catch (Exception exception)
            {
                Fail("역사 World 준비에 실패했습니다.", exception);
            }
        }

        /// <summary>취소를 알리고 워커가 빠져나갈 시간을 짧게 준다. 그 이상은 기다리지 않는다.</summary>
        private void CancelAndWait()
        {
            CancellationTokenSource cancellation = _cancellation;
            Task warmupTask = _warmupTask;
            _cancellation = null;
            _warmupTask = null;
            if (cancellation == null)
                return;

            try
            {
                cancellation.Cancel();
                warmupTask?.Wait(CancelWaitMilliseconds);
            }
            catch (Exception exception) when (exception is AggregateException ||
                                              exception is OperationCanceledException ||
                                              exception is ObjectDisposedException)
            {
                // 취소 경로에서 나온 예외는 무시한다. 워밍업 실패는 게임 진행을 막지 않는다.
            }
            finally
            {
                cancellation.Dispose();
            }
        }

        private void SetProgress(float value)
        {
            Interlocked.Exchange(ref _progressPermille, Mathf.Clamp(Mathf.RoundToInt(value * 1000f), 0, 1000));
        }

        private void Fail(string message, Exception exception)
        {
            LastError = message + " " + exception.Message;
            SetProgress(1f);
            _state = (int)HistoricalWarmupState.Failed;
        }

        private void Update()
        {
            // 워커에서 던진 예외는 메인 스레드 로그로 옮겨야 눈에 띈다.
            if (_isFailureReported || State != HistoricalWarmupState.Failed)
                return;
            _isFailureReported = true;
            Debug.LogWarning(
                $"[HistoricalWarmupManager] 사전 준비를 건너뜁니다. 새 게임 시작이 느려질 수 있습니다. {LastError}");
        }
    }
}
