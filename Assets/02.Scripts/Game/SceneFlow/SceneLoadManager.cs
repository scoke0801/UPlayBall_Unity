using System;
using Baseball.Game.Manager;
using UnityEngine;

namespace Baseball.Game.SceneFlow
{
    /// <summary>
    /// Boot, Loading, 콘텐츠 Scene 사이의 단일 전환 상태를 소유한다.
    /// </summary>
    public sealed partial class SceneLoadManager : ManagerBehaviour<SceneLoadManager>
    {
        public const float DefaultMinimumLoadingTime = 0.5f;

        private const float SceneContextTimeout = 5f;

        private Coroutine _loadRoutine;
        private bool _activationRequested;
        private bool _pendingLoadStarted;
        private SceneLoadRequest? _currentRequest;

        public override int InitializationOrder => -50;
        public SceneId? CurrentSceneId { get; private set; }
        public SceneId? TargetSceneId => _currentRequest?.TargetScene;
        public SceneLoadState LoadState { get; private set; } = SceneLoadState.Idle;
        public float LoadProgress { get; private set; }
        public bool IsLoading { get; private set; }
        public bool IsReadyToActivate { get; private set; }
        public string LastLoadFailure { get; private set; }

        public event Action<SceneLoadRequest> LoadStarted;
        public event Action<SceneLoadRequest> ReadyToActivate;
        public event Action<SceneId> SceneReady;
        public event Action<SceneId> LoadCompleted;
        public event Action<SceneId, string> LoadFailed;

        /// <summary>
        /// 지정한 방식으로 콘텐츠 Scene 전환을 요청한다.
        /// </summary>
        public bool LoadScene(
            SceneId targetScene,
            SceneTransitionMode transitionMode = SceneTransitionMode.LoadingScreen,
            float minimumLoadingTime = DefaultMinimumLoadingTime)
        {
            return StartLoad(new SceneLoadRequest(
                targetScene,
                transitionMode,
                minimumLoadingTime));
        }

        /// <summary>
        /// Loading Scene이 활성화된 뒤 보류 중인 대상 Scene의 비동기 Load를 시작한다.
        /// </summary>
        public bool StartPendingLoad()
        {
            if (!IsLoading || LoadState != SceneLoadState.LoadingTransitionScene)
            {
                Debug.LogWarning($"[SceneLoadManager] 현재 상태에서는 대상 Scene Load를 시작할 수 없습니다: {LoadState}");
                return false;
            }

            if (_pendingLoadStarted || !_currentRequest.HasValue)
            {
                Debug.LogWarning("[SceneLoadManager] 대상 Scene Load가 이미 시작됐거나 요청 정보가 없습니다.");
                return false;
            }

            _pendingLoadStarted = true;
            _loadRoutine = StartCoroutine(LoadTargetScene(_currentRequest.Value));
            return true;
        }

        /// <summary>
        /// 진행률 연출이 끝난 대상 Scene의 실제 활성화를 허용한다.
        /// </summary>
        public bool ActivatePendingScene()
        {
            if (!IsLoading || LoadState != SceneLoadState.AwaitingActivation)
                return false;

            _activationRequested = true;
            return true;
        }

        /// <summary>
        /// SceneContext가 보낸 준비 신호를 수신하고 대상과 일치할 때 Load를 완료한다.
        /// </summary>
        public void NotifySceneReady(SceneContext context)
        {
            if (context == null)
                return;

            CurrentSceneId = context.SceneId;
            SceneReady?.Invoke(context.SceneId);

            if (!IsLoading || context.SceneId == SceneId.Loading)
                return;

            if (!_currentRequest.HasValue || context.SceneId != _currentRequest.Value.TargetScene)
            {
                Debug.LogWarning(
                    $"[SceneLoadManager] 대상과 다른 SceneContext 신호를 무시합니다. " +
                    $"대상={TargetSceneId}, 수신={context.SceneId}");
                return;
            }

            CompleteCurrentLoad(context.SceneId);
        }

        protected override void OnShutdown()
        {
            if (_loadRoutine != null)
                StopCoroutine(_loadRoutine);

            ResetLoadState(SceneLoadState.Idle);
            CurrentSceneId = null;
            LoadStarted = null;
            ReadyToActivate = null;
            SceneReady = null;
            LoadCompleted = null;
            LoadFailed = null;
        }

        private void CompleteCurrentLoad(SceneId sceneId)
        {
            if (!IsLoading)
                return;

            IsLoading = false;
            IsReadyToActivate = false;
            LoadProgress = 1f;
            LoadState = SceneLoadState.Completed;
            _loadRoutine = null;
            _activationRequested = false;
            _pendingLoadStarted = false;
            _currentRequest = null;
            LoadCompleted?.Invoke(sceneId);
        }

        private void FailCurrentLoad(SceneId sceneId, string reason)
        {
            LastLoadFailure = string.IsNullOrWhiteSpace(reason)
                ? "알 수 없는 Scene Load 오류"
                : reason;
            IsLoading = false;
            IsReadyToActivate = false;
            LoadState = SceneLoadState.Failed;
            _loadRoutine = null;
            _activationRequested = false;
            _pendingLoadStarted = false;
            _currentRequest = null;
            LoadFailed?.Invoke(sceneId, LastLoadFailure);
            Debug.LogError($"[SceneLoadManager] Scene '{sceneId}' Load 실패: {LastLoadFailure}");
        }

        private void ResetLoadState(SceneLoadState state)
        {
            IsLoading = false;
            IsReadyToActivate = false;
            LoadProgress = 0f;
            LastLoadFailure = null;
            LoadState = state;
            _loadRoutine = null;
            _activationRequested = false;
            _pendingLoadStarted = false;
            _currentRequest = null;
        }
    }
}
