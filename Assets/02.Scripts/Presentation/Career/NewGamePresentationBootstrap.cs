using Baseball.Game.Career;
using Baseball.Game.Manager;
using Baseball.Game.SceneFlow;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Baseball.Presentation.Career
{
    /// <summary>
    /// Management Scene에서 새 게임 화면을 생성하고 다른 Scene에서는 숨긴다.
    /// </summary>
    public static class NewGamePresentationBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            UI_Scene_NewGame screen = Object.FindFirstObjectByType<UI_Scene_NewGame>(FindObjectsInactive.Include);
            UI_Scene_CareerDashboard dashboard =
                Object.FindFirstObjectByType<UI_Scene_CareerDashboard>(FindObjectsInactive.Include);
            if (scene.name != SceneCatalog.ManagementSceneName)
            {
                screen?.Hide();
                dashboard?.Hide();
                return;
            }

            UIManager uiManager = GameManager.EnsureExists().EnsureManager<UIManager>("UIManager");
            if (screen == null)
                screen = UI_Scene_NewGame.CreateRuntime(uiManager.Root.GetLayerRoot(UILayer.Scene));
            if (dashboard == null)
                dashboard = UI_Scene_CareerDashboard.CreateRuntime(uiManager.Root.GetLayerRoot(UILayer.Scene));

            if (CareerManager.Instance != null && CareerManager.Instance.HasActiveCareer)
            {
                screen.Hide();
                dashboard.Show();
            }
            else
            {
                dashboard.Hide();
                screen.Show();
            }
        }
    }
}
