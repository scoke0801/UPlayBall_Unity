using Baseball.Game.Manager;
using Baseball.Game.SceneFlow;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Baseball.Presentation.Career
{
    /// <summary>Management Scene의 팝업 레이어에 전체 뉴스 화면을 독립 생성한다.</summary>
    public static class CareerNewsPresentationBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            UI_Popup_CareerNews popup = Object.FindFirstObjectByType<UI_Popup_CareerNews>(
                FindObjectsInactive.Include);
            if (scene.name != SceneCatalog.ManagementSceneName)
            {
                popup?.Hide();
                return;
            }

            UIManager uiManager = GameManager.EnsureExists().EnsureManager<UIManager>("UIManager");
            if (popup == null)
            {
                popup = UI_Popup_CareerNews.CreateRuntime(
                    uiManager.Root.GetLayerRoot(UILayer.Popup));
            }
            popup.Hide();
        }
    }
}
