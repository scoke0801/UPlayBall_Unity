using Baseball.Game.Manager;
using Baseball.Game.SceneFlow;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Baseball.Presentation.Career
{
    /// <summary>Management Scene의 Popup 최상단에 공통 커리어 챕터 컷 프리팹을 한 번만 생성한다.</summary>
    public static class CareerPresentationBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            UI_CareerPresentation presentation = Object.FindFirstObjectByType<UI_CareerPresentation>(
                FindObjectsInactive.Include);
            if (scene.name != SceneCatalog.ManagementSceneName)
            {
                presentation?.Suspend();
                return;
            }

            UIManager uiManager = GameManager.EnsureExists().EnsureManager<UIManager>("UIManager");
            if (presentation == null)
            {
                GameObject prefab = Resources.Load<GameObject>("UI/UI_CareerPresentation");
                if (prefab != null)
                {
                    GameObject instance = Object.Instantiate(
                        prefab,
                        uiManager.Root.GetLayerRoot(UILayer.Popup),
                        false);
                    presentation = instance.GetComponent<UI_CareerPresentation>();
                }
                presentation ??= UI_CareerPresentation.CreateRuntime(
                    uiManager.Root.GetLayerRoot(UILayer.Popup));
            }
            presentation.Initialize();
            presentation.ResumeObservation();
        }
    }
}
