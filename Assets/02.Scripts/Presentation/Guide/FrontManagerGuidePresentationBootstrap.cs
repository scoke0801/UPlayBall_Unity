using Baseball.Game.Manager;
using Baseball.Game.SceneFlow;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Baseball.Presentation.Guide
{
    /// <summary>Management와 Match Scene의 System 레이어에 Guide Presenter를 한 번만 준비한다.</summary>
    public static class FrontManagerGuidePresentationBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            UI_System_FrontManagerGuide guide = Object.FindFirstObjectByType<UI_System_FrontManagerGuide>(
                FindObjectsInactive.Include);
            bool isContentScene = scene.name == SceneCatalog.ManagementSceneName ||
                                  scene.name == SceneCatalog.MatchSceneName;
            if (!isContentScene)
            {
                guide?.Hide();
                return;
            }

            UIManager uiManager = GameManager.EnsureExists().EnsureManager<UIManager>("UIManager");
            if (guide == null)
            {
                guide = UI_System_FrontManagerGuide.CreateRuntime(
                    uiManager.Root.GetLayerRoot(UILayer.System));
            }
            guide.Hide();
        }
    }
}
