using Baseball.Game.Career.News;
using Baseball.Game.Data;
using Baseball.Game.Diagnostics;
using Baseball.Game.Input;
using Baseball.Game.Career;
using Baseball.Game.SceneFlow;
using Baseball.Game.Sound;
using Baseball.Game.Guide;
using Baseball.Game.Historical;
using UnityEngine;

namespace Baseball.Game.Manager
{
    /// <summary>
    /// 씬 구성과 무관하게 필수 Game 레이어 매니저를 생성한다.
    /// </summary>
    public static class GameBootstrap
    {
        /// <summary>
        /// 필수 Game 매니저를 모두 준비한다. 진입점은 씬 로드 전 자동 호출이지만,
        /// GameManager를 파괴하는 테스트가 같은 계약을 다시 만들 수 있도록 공개한다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void EnsureRuntimeManagers()
        {
            RegisterUnityAdapters();
            GameManager gameManager = GameManager.EnsureExists();
            gameManager.EnsureManager<InputManager>("InputManager");
            gameManager.EnsureManager<CareerManager>("CareerManager");
            gameManager.EnsureManager<NewGameManager>("NewGameManager");
            gameManager.EnsureManager<OwnerModeManager>("OwnerModeManager");
            gameManager.EnsureManager<GuideManager>("GuideManager");
            gameManager.EnsureManager<SceneLoadManager>("SceneLoadManager");
            gameManager.EnsureManager<SoundManager>("SoundManager");
            gameManager.EnsureManager<BgmDirector>("BgmDirector");
        }

        /// <summary>
        /// Unity를 참조하지 않는 Game 레이어에 Unity 전용 구현을 주입한다.
        /// </summary>
        /// <remarks>
        /// 어댑터를 등록하지 않은 프로세스(EditMode 테스트, Headless 러너)에서는 각 계약이
        /// 코드 기본값이나 no-op으로 동작하므로 같은 로직이 그대로 돈다.
        /// </remarks>
        private static void RegisterUnityAdapters()
        {
            ProfilerSectionSink.Current ??= new UnityProfilerSectionSink();
            CareerNewsConfigurationProvider.SetLoader(CareerNewsDefinition.LoadConfiguration);
        }
    }
}
