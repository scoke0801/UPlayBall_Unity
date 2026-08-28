using Baseball.Game.Career;
using Baseball.Game.Manager;
using Baseball.Game.SceneFlow;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Baseball.Presentation.Career
{
    /// <summary>
    /// Management Scene에 계약 화면만 독립적으로 준비해 다른 메뉴 부트스트랩과의 수정 충돌을 막는다.
    /// </summary>
    public static class ContractPresentationBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            UI_Scene_Contract contract = Object.FindFirstObjectByType<UI_Scene_Contract>(
                FindObjectsInactive.Include);
            if (scene.name != SceneCatalog.ManagementSceneName)
            {
                contract?.Hide();
                return;
            }

            UIManager uiManager = GameManager.EnsureExists().EnsureManager<UIManager>("UIManager");
            if (contract == null)
            {
                contract = UI_Scene_Contract.CreateRuntime(
                    uiManager.Root.GetLayerRoot(UILayer.Scene));
            }
            contract.Hide();

            if (CareerManager.Instance != null && CareerManager.Instance.HasActiveCareer)
            {
                UI_Scene_CareerDashboard dashboard = Object.FindFirstObjectByType<UI_Scene_CareerDashboard>(
                    FindObjectsInactive.Include);
                if (dashboard == null || !dashboard.IsVisible)
                    CareerTabNavigation.Show(CareerMainTab.Home);
            }
        }
    }
}
