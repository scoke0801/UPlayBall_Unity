using Baseball.Game.Input;
using UnityEngine;

namespace Baseball.Game.Manager
{
    /// <summary>
    /// 씬 구성과 무관하게 필수 Game 레이어 매니저를 생성한다.
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            GameManager gameManager = GameManager.EnsureExists();
            gameManager.EnsureManager<InputManager>("InputManager");
        }
    }
}
