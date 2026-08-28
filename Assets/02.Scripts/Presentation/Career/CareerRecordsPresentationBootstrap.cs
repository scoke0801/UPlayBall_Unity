using Baseball.Game.Career;
using Baseball.Game.Manager;
using Baseball.Game.SceneFlow;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Baseball.Presentation.Career
{
    /// <summary>Management Scene에 기록 화면만 독립 생성해 다른 메뉴 bootstrap과의 수정을 분리한다.</summary>
    public static class CareerRecordsPresentationBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            UI_Scene_CareerRecords screen = Object.FindFirstObjectByType<UI_Scene_CareerRecords>(
                FindObjectsInactive.Include);
            if (scene.name != SceneCatalog.ManagementSceneName)
            {
                screen?.Hide();
                return;
            }

            UIManager uiManager = GameManager.EnsureExists().EnsureManager<UIManager>("UIManager");
            if (screen == null)
            {
                screen = UI_Scene_CareerRecords.CreateRuntime(
                    uiManager.Root.GetLayerRoot(UILayer.Scene));
            }
            screen.Hide();
        }
    }
}
