using Baseball.Game.Input;
using Baseball.Game.Career;
using Baseball.Game.SceneFlow;
using Baseball.Game.Sound;
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
            GameManager gameManager = GameManager.EnsureExists();
            gameManager.EnsureManager<InputManager>("InputManager");
            gameManager.EnsureManager<CareerManager>("CareerManager");
            gameManager.EnsureManager<NewGameManager>("NewGameManager");
            gameManager.EnsureManager<SceneLoadManager>("SceneLoadManager");
            gameManager.EnsureManager<SoundManager>("SoundManager");
            gameManager.EnsureManager<BgmDirector>("BgmDirector");
        }
    }
}
