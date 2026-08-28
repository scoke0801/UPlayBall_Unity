using System;

namespace Baseball.Game.SceneFlow
{
    /// <summary>
    /// Scene Load 상태를 로딩 화면과 진단 코드에 노출한다.
    /// </summary>
    public enum SceneLoadState
    {
        Idle,
        LoadingTransitionScene,
        LoadingTargetScene,
        AwaitingActivation,
        Activating,
        WaitingForSceneContext,
        Completed,
        Failed
    }

    /// <summary>
    /// 한 번의 Scene 전환에 필요한 대상과 표현 정책을 묶는다.
    /// </summary>
    public readonly struct SceneLoadRequest
    {
        public SceneLoadRequest(
            SceneId targetScene,
            SceneTransitionMode transitionMode,
            float minimumLoadingTime)
        {
            TargetScene = targetScene;
            TransitionMode = transitionMode;
            MinimumLoadingTime = Math.Max(0f, minimumLoadingTime);
        }

        public SceneId TargetScene { get; }
        public SceneTransitionMode TransitionMode { get; }
        public float MinimumLoadingTime { get; }
    }
}
