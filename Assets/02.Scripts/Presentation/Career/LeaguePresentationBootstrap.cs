using Baseball.Game.Career;
using Baseball.Game.Manager;
using Baseball.Game.SceneFlow;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Baseball.Presentation.Career
{
    /// <summary>Management Scene에서 리그 탭 화면만 독립적으로 생성하고 수명주기를 관리한다.</summary>
    public static class LeaguePresentationBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            UI_Scene_League league = Object.FindFirstObjectByType<UI_Scene_League>(FindObjectsInactive.Include);
            if (scene.name != SceneCatalog.ManagementSceneName)
            {
                league?.Hide();
                return;
            }

            UIManager uiManager = GameManager.EnsureExists().EnsureManager<UIManager>("UIManager");
            if (league == null)
                league = UI_Scene_League.CreateRuntime(uiManager.Root.GetLayerRoot(UILayer.Scene));

            // Management 진입 기본 화면은 홈이다. 리그는 하단 탭으로 명시적으로 진입한다.
            league.Hide();
            if (CareerManager.Instance == null || !CareerManager.Instance.HasActiveCareer)
                league.Hide();
        }
    }
}
