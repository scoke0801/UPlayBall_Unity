using Baseball.Game.Career;
using Baseball.Game.Historical;
using Baseball.Game.Manager;
using Baseball.Game.SceneFlow;
using Baseball.Presentation.SharedUI;
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
            UI_Scene_CareerMatch match =
                Object.FindFirstObjectByType<UI_Scene_CareerMatch>(FindObjectsInactive.Include);
            if (scene.name != SceneCatalog.ManagementSceneName)
            {
                screen?.Hide();
                dashboard?.Hide();
                match?.Hide();
                return;
            }

            GameManager gameManager = GameManager.EnsureExists();
            UIManager uiManager = gameManager.EnsureManager<UIManager>("UIManager");
            CareerManager careerManager = gameManager.EnsureManager<CareerManager>("CareerManager");
            OwnerModeManager ownerManager = gameManager.EnsureManager<OwnerModeManager>("OwnerModeManager");
            if (screen == null)
                screen = UI_Scene_NewGame.CreateRuntime(uiManager.Root.GetLayerRoot(UILayer.Scene));
            if (dashboard == null)
                dashboard = UI_Scene_CareerDashboard.CreateRuntime(uiManager.Root.GetLayerRoot(UILayer.Scene));
            if (match == null)
                match = UI_Scene_CareerMatch.CreateRuntime(uiManager.Root.GetLayerRoot(UILayer.Scene));

            UiGameMode? selectedMode = UiGameModeSession.ResolveInitialMode(
                careerManager.HasActiveCareer,
                ownerManager.HasActiveRuntime);
            if (selectedMode == UiGameMode.PlayerCareer && careerManager.HasActiveCareer)
            {
                screen.Hide();
                dashboard.Show();
                if (careerManager.HasActiveMatch)
                    match.Show();
                else
                    match.Hide();
            }
            else
            {
                dashboard.Hide();
                match.Hide();
                if (selectedMode == UiGameMode.OwnerCareer && ownerManager.HasActiveRuntime)
                    screen.Hide();
                else
                    screen.Show();
            }
        }
    }
}
