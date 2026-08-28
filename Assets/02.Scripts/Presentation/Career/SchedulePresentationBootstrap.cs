using Baseball.Game.Career;
using Baseball.Game.Manager;
using Baseball.Game.SceneFlow;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Baseball.Presentation.Career
{
    /// <summary>Management Scene에서 일정 탭 화면만 독립적으로 생성하고 수명주기를 관리한다.</summary>
    public static class SchedulePresentationBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            UI_Scene_CareerSchedule schedule = Object.FindFirstObjectByType<UI_Scene_CareerSchedule>(
                FindObjectsInactive.Include);
            if (scene.name != SceneCatalog.ManagementSceneName)
            {
                schedule?.Hide();
                return;
            }

            UIManager uiManager = GameManager.EnsureExists().EnsureManager<UIManager>("UIManager");
            if (schedule == null)
                schedule = UI_Scene_CareerSchedule.CreateRuntime(uiManager.Root.GetLayerRoot(UILayer.Scene));
            schedule.Hide();
        }
    }
}
