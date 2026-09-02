using System;
using System.Collections;
using UnityEngine;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace Baseball.Game.SceneFlow
{
    public sealed partial class SceneLoadManager
    {
        private bool StartLoad(SceneLoadRequest request)
        {
            if (!SceneCatalog.IsContentScene(request.TargetScene))
            {
                Debug.LogError($"[SceneLoadManager] 콘텐츠 Scene만 대상으로 지정할 수 있습니다: {request.TargetScene}");
                return false;
            }

            string targetSceneName = SceneCatalog.GetSceneName(request.TargetScene);
            if (!Application.CanStreamedLevelBeLoaded(targetSceneName))
            {
                Debug.LogError(
                    $"[SceneLoadManager] Scene '{targetSceneName}'을 Load할 수 없습니다. " +
                    "Build Settings 등록 상태를 확인하세요.");
                return false;
            }

            if (request.TransitionMode == SceneTransitionMode.LoadingScreen &&
                !Application.CanStreamedLevelBeLoaded(SceneCatalog.LoadingSceneName))
            {
                Debug.LogError(
                    $"[SceneLoadManager] 전환 Scene '{SceneCatalog.LoadingSceneName}'을 Load할 수 없습니다.");
                return false;
            }

            if (IsLoading)
            {
                Debug.LogWarning($"[SceneLoadManager] Load 중 중복 요청을 무시합니다: {request.TargetScene}");
                return false;
            }

            ResetLoadState(request.TransitionMode == SceneTransitionMode.LoadingScreen
                ? SceneLoadState.LoadingTransitionScene
                : SceneLoadState.LoadingTargetScene);
            IsLoading = true;
            _currentRequest = request;
            LoadStarted?.Invoke(request);

            if (request.TransitionMode == SceneTransitionMode.LoadingScreen)
            {
                UnitySceneManager.LoadScene(SceneCatalog.LoadingSceneName);
                return true;
            }

            _pendingLoadStarted = true;
            _loadRoutine = StartCoroutine(LoadTargetScene(request));
            return true;
        }

        private IEnumerator LoadTargetScene(SceneLoadRequest request)
        {
            string sceneName = SceneCatalog.GetSceneName(request.TargetScene);
            AsyncOperation operation;
            try
            {
                operation = UnitySceneManager.LoadSceneAsync(sceneName);
            }
            catch (Exception exception)
            {
                FailCurrentLoad(request.TargetScene, exception.Message);
                yield break;
            }

            if (operation == null)
            {
                FailCurrentLoad(request.TargetScene, "비동기 Load 작업을 생성하지 못했습니다.");
                yield break;
            }

            operation.allowSceneActivation = false;
            LoadState = SceneLoadState.LoadingTargetScene;

            float elapsed = 0f;
            while (operation.progress < 0.9f || elapsed < request.MinimumLoadingTime)
            {
                if (!IsLoading)
                    yield break;

                elapsed += Time.unscaledDeltaTime;
                float assetProgress = Mathf.Clamp01(operation.progress / 0.9f);
                float timeProgress = request.MinimumLoadingTime <= 0f
                    ? 1f
                    : Mathf.Clamp01(elapsed / request.MinimumLoadingTime);
                LoadProgress = Mathf.Min(assetProgress, timeProgress);
                yield return null;
            }

            LoadProgress = 1f;
            LoadState = SceneLoadState.AwaitingActivation;
            IsReadyToActivate = true;
            ReadyToActivate?.Invoke(request);

            if (request.TransitionMode == SceneTransitionMode.Direct)
                _activationRequested = true;

            while (!_activationRequested)
            {
                if (!IsLoading)
                    yield break;

                yield return null;
            }

            IsReadyToActivate = false;
            LoadState = SceneLoadState.Activating;
            operation.allowSceneActivation = true;

            while (!operation.isDone)
                yield return null;

            if (!IsLoading)
                yield break;

            LoadState = SceneLoadState.WaitingForSceneContext;
            float contextWaitElapsed = 0f;
            while (IsLoading && contextWaitElapsed < SceneContextTimeout)
            {
                contextWaitElapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (IsLoading)
            {
                FailCurrentLoad(
                    request.TargetScene,
                    $"SceneContext 준비 신호를 {SceneContextTimeout:F1}초 안에 받지 못했습니다.");
            }
        }
    }
}
