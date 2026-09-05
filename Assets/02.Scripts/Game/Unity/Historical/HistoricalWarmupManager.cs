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
        private string _statusMessage = string.Empty;
        private volatile bool _isSimulatingWorldHistory;
        private bool _isBakeMissReported;

        public override int InitializationOrder => -10;

        public HistoricalWarmupState State => (HistoricalWarmupState)_state;
        public float Progress => _progressPermille / 1000f;

        /// <summary>Bake가 맞지 않아 44시즌을 실제로 돌리는 중인지. 로딩이 길어지는 유일한 경우다.</summary>
        public bool IsSimulatingWorldHistory => _isSimulatingWorldHistory;

        /// <summary>로딩 화면에 그대로 띄울 현재 단계 문구다.</summary>
        public string StatusMessage
        {
            get { lock (_stateLock) return _statusMessage; }
            private set { lock (_stateLock) _statusMessage = value; }
        }

        public bool IsRunning =>
            State == HistoricalWarmupState.ReadingAssets || State == HistoricalWarmupState.Building;

        /// <summary>
        /// 더 기다릴 것이 없는 상태다. Idle은 워밍업을 아예 시작하지 않은 경우이므로 기다릴 대상이 아니다.
        /// 실패·취소도 기다림의 끝이다. 각 진입점이 스스로 만드는 경로를 갖고 있기 때문이다.
        /// </summary>
        public bool IsSettled =>
            State == HistoricalWarmupState.Idle ||
            State == HistoricalWarmupState.Completed ||
            State == HistoricalWarmupState.Failed ||
            State == HistoricalWarmupState.Canceled;

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
            _isBakeMissReported = false;
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

            OwnerModeManager ownerMode = OwnerModeManager.Instance;
            try
            {
                _state = (int)HistoricalWarmupState.ReadingAssets;
                StatusMessage = "역사 데이터를 읽는 중…";
                _contentProvider = NewGameDefinition.LoadSharedHistoricalContentProvider();
                _contentProvider.CacheAssetBytesOnMainThread();

                // TextAsset은 워커 스레드에서 읽을 수 없다. Bake 바이트도 여기서 미리 떠 둔다.
                ownerMode?.CacheBakedWorldHistoryBytesOnMainThread();
                SetProgress(0.05f);
            }
            catch (Exception exception)
            {
                Fail("역사 콘텐츠 Asset을 읽지 못했습니다.", exception);
                return;
            }

            NewGameManager newGame = NewGameManager.Instance;
            UnityHistoricalContentProvider provider = _contentProvider;
            _cancellation = new CancellationTokenSource();
            CancellationToken token = _cancellation.Token;
            _state = (int)HistoricalWarmupState.Building;
            _warmupTask = Task.Run(() => RunWarmup(provider, ownerMode, newGame, token), token);
        }

        /// <summary>
        /// World 준비를 Bake 유무로 건너뛰지 않는다. 로딩 화면이 여기 완료를 기다리므로,
        /// Bake가 맞지 않으면 그 자리에서 44시즌을 시뮬레이션해서라도 World를 만들어 둔다.
        /// 비용을 뒤로 미루면 결국 타이틀에서 버튼을 누른 사용자가 아무 안내 없이 그 시간을 맞는다.
        /// </summary>
        private void RunWarmup(
            UnityHistoricalContentProvider contentProvider,
            OwnerModeManager ownerMode,
            NewGameManager newGame,
            CancellationToken cancellationToken)
        {
            try
            {
                // 1) 23.6MB 역사 payload 파싱. 두 모드가 같은 Provider 인스턴스를 공유한다.
                contentProvider.Load();
                cancellationToken.ThrowIfCancellationRequested();
                SetProgress(0.35f);

                // 2) 맞는 Bake가 있으면 여기서 복원되고, 그 결과를 아래 World 생성이 그대로 쓴다.
                bool hasMatchingBake = ownerMode != null && ownerMode.HasMatchingBakedWorldHistory();
                cancellationToken.ThrowIfCancellationRequested();
                _isSimulatingWorldHistory = !hasMatchingBake;
                StatusMessage = hasMatchingBake
                    ? "역사 World를 불러오는 중…"
                    : "맞는 역사 Bake가 없어 44시즌을 시뮬레이션합니다. 시간이 걸립니다…";
                SetProgress(0.45f);

                // 3) 구단주 모드 시작 World. Bake가 적중하면 즉시, 아니면 실제 시뮬레이션으로 만든다.
                ownerMode?.PrewarmNewGameWorld(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                _isSimulatingWorldHistory = false;
                SetProgress(0.85f);

                // 4) 커리어 "구단 오퍼 확인"이 쓸 Content.
                StatusMessage = "커리어 콘텐츠를 준비하는 중…";
                newGame?.PrewarmCareerContent(cancellationToken);
                SetProgress(1f);

                contentProvider.ReleaseAssetByteCache();
                ownerMode?.ReleaseBakedWorldHistoryByteCache();
                StatusMessage = "준비 완료";
                _state = (int)HistoricalWarmupState.Completed;
            }
            catch (OperationCanceledException)
            {
                _isSimulatingWorldHistory = false;
                SetProgress(1f);
                _state = (int)HistoricalWarmupState.Canceled;
            }
            catch (Exception exception)
            {
                _isSimulatingWorldHistory = false;
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
            // 워커에서 만든 상태는 메인 스레드 로그로 옮겨야 눈에 띈다.
            if (!_isBakeMissReported && _isSimulatingWorldHistory)
            {
                _isBakeMissReported = true;
                Debug.LogWarning(
                    "[HistoricalWarmupManager] 맞는 World History Bake가 없어 44시즌을 실제로 시뮬레이션합니다. " +
                    "로딩이 길어집니다. 툴 런처의 '역사 콘텐츠 파이프라인'에서 다시 구우면 즉시 열립니다.");
            }

            if (_isFailureReported || State != HistoricalWarmupState.Failed)
                return;
            _isFailureReported = true;
            Debug.LogWarning(
                $"[HistoricalWarmupManager] 사전 준비를 건너뜁니다. 새 게임 시작이 느려질 수 있습니다. {LastError}");
        }
    }
}
