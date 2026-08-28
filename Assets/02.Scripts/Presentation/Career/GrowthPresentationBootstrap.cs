using Baseball.Game.Manager;
using Baseball.Game.SceneFlow;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Baseball.Presentation.Career
{
    /// <summary>
    /// Management Scene에 성장 화면만 독립적으로 생성해 다른 메뉴 부트스트랩과 충돌하지 않게 한다.
    /// </summary>
    public static class GrowthPresentationBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            UI_Scene_CareerGrowth screen = Object.FindFirstObjectByType<UI_Scene_CareerGrowth>(
                FindObjectsInactive.Include);
            UI_Popup_GrowthActivityConfirmation popup =
                Object.FindFirstObjectByType<UI_Popup_GrowthActivityConfirmation>(
                    FindObjectsInactive.Include);
            if (scene.name != SceneCatalog.ManagementSceneName)
            {
                screen?.Hide();
                popup?.Hide();
                return;
            }

            UIManager uiManager = GameManager.EnsureExists().EnsureManager<UIManager>("UIManager");
            if (screen == null)
            {
                screen = UI_Scene_CareerGrowth.CreateRuntime(
                    uiManager.Root.GetLayerRoot(UILayer.Scene));
            }
            screen.Hide();
            if (popup == null)
            {
                popup = UI_Popup_GrowthActivityConfirmation.CreateRuntime(
                    uiManager.Root.GetLayerRoot(UILayer.Popup));
            }
            popup.Hide();
        }
    }
}
