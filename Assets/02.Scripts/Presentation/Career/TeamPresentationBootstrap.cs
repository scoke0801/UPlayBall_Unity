using Baseball.Game.Manager;
using Baseball.Game.SceneFlow;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Baseball.Presentation.Career
{
    /// <summary>
    /// 공용 탭 라우터가 구단 화면을 찾을 수 있도록 Management Scene에서 미리 준비한다.
    /// </summary>
    public static class TeamPresentationBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            UI_Scene_Team team = Object.FindFirstObjectByType<UI_Scene_Team>(FindObjectsInactive.Include);
            if (scene.name != SceneCatalog.ManagementSceneName)
            {
                team?.Hide();
                return;
            }

            if (team == null)
            {
                UIManager uiManager = GameManager.EnsureExists().EnsureManager<UIManager>("UIManager");
                team = UI_Scene_Team.CreateRuntime(uiManager.Root.GetLayerRoot(UILayer.Scene));
            }
            team.Hide();
        }
    }
}
