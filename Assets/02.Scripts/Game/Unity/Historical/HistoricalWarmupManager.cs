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
        Failed = 4
    }

    /// <summary>
    /// Boot~Loading 구간에서 역사 콘텐츠와 World를 미리 만들어 둔다.
    /// 타이틀에서 어느 모드를 눌러도 이미 만들어 둔 결과를 그대로 쓰게 하는 것이 목적이다.
    ///
    /// 실패해도 게임은 계속 진행한다. 워밍업은 비용을 앞당길 뿐이고,
    /// 각 진입점은 캐시가 없으면 스스로 만드는 경로를 그대로 갖고 있다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HistoricalWarmupManager : ManagerBehaviour<HistoricalWarmupManager>
    {
        private UnityHistoricalContentProvider _contentProvider;
        private Task _warmupTask;
        private bool _isFailureReported;
        private volatile int _progressPermille;
        private volatile int _state = (int)HistoricalWarmupState.Idle;
        private string _lastError = string.Empty;

        public override int InitializationOrder => -10;

        public HistoricalWarmupState State => (HistoricalWarmupState)_state;
        public float Progress => _progressPermille / 1000f;
        public bool IsRunning => State == HistoricalWarmupState.ReadingAssets || State == HistoricalWarmupState.Building;

        /// <summary>실패했더라도 더 기다릴 이유가 없다는 뜻이므로 완료로 본다.</summary>
        public bool IsSettled => State == HistoricalWarmupState.Completed || State == HistoricalWarmupState.Failed;

        public string LastError
        {
            get { lock (this) return _lastError; }
            private set { lock (this) _lastError = value; }
        }

        protected override void OnShutdown()
        {
            _warmupTask = null;
            _contentProvider = null;
            _isFailureReported = false;
        }

        /// <summary>
        /// 워밍업을 시작한다. 메인 스레드에서 호출해야 하며, TextAsset 바이트 확보만 여기서 하고
        /// 파싱과 World 생성은 워커 스레드로 넘긴다. 이미 시작했다면 아무것도 하지 않는다.
        /// </summary>
        public void BeginWarmup()
        {
            if (State != HistoricalWarmupState.Idle)
                return;

            try
            {
                _state = (int)HistoricalWarmupState.ReadingAssets;
                _contentProvider = NewGameDefinition.LoadSharedHistoricalContentProvider();
                _contentProvider.CacheAssetBytesOnMainThread();
                SetProgress(0.05f);
            }
            catch (Exception exception)
            {
                Fail("역사 콘텐츠 Asset을 읽지 못했습니다.", exception);
                return;
            }

            OwnerModeManager ownerMode = OwnerModeManager.Instance;
            NewGameManager newGame = NewGameManager.Instance;
            _state = (int)HistoricalWarmupState.Building;
            _warmupTask = Task.Run(() => RunWarmup(_contentProvider, ownerMode, newGame));
        }

        private void RunWarmup(
            UnityHistoricalContentProvider contentProvider,
            OwnerModeManager ownerMode,
            NewGameManager newGame)
        {
            try
            {
                // 1) 23.6MB 역사 payload 파싱. 두 모드가 같은 Provider 인스턴스를 공유한다.
                contentProvider.Load();
                SetProgress(0.35f);

                // 2) 구단주 모드 시작 World. Bake가 있으면 복원, 없으면 여기서 44시즌을 돌린다.
                ownerMode?.PrewarmNewGameWorld();
                SetProgress(0.75f);

                // 3) 커리어 "구단 오퍼 확인"이 쓸 Content.
                newGame?.PrewarmCareerContent();
                SetProgress(1f);

                contentProvider.ReleaseAssetByteCache();
                _state = (int)HistoricalWarmupState.Completed;
            }
            catch (Exception exception)
            {
                Fail("역사 World 준비에 실패했습니다.", exception);
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
            _warmupTask = null;
            Debug.LogWarning(
                $"[HistoricalWarmupManager] 사전 준비를 건너뜁니다. 새 게임 시작이 느려질 수 있습니다. {LastError}");
        }
    }
}
