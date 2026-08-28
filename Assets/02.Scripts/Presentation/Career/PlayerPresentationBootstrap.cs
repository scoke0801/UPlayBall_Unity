using Baseball.Game.Career;
using Baseball.Game.Manager;
using Baseball.Game.SceneFlow;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Baseball.Presentation.Career
{
    /// <summary>공용 탭 라우터가 Management Scene에서 선수 화면을 찾을 수 있도록 준비한다.</summary>
    public static class PlayerPresentationBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            UI_Scene_Player player = Object.FindFirstObjectByType<UI_Scene_Player>(FindObjectsInactive.Include);
            if (scene.name != SceneCatalog.ManagementSceneName)
            {
                player?.Hide();
                return;
            }

            if (player == null)
            {
                UIManager uiManager = GameManager.EnsureExists().EnsureManager<UIManager>("UIManager");
                player = UI_Scene_Player.CreateRuntime(uiManager.Root.GetLayerRoot(UILayer.Scene));
            }
            player.Hide();
        }
    }
}
