using Baseball.Game.Career;
using Baseball.Game.Manager;
using Baseball.Game.SceneFlow;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Baseball.Presentation.Career
{
    /// <summary>Management Scene에서 새 은퇴 스냅샷을 감지해 회고 Popup을 한 번 연다.</summary>
    public static class RetirementRecapPresentationBootstrap
    {
        private static CareerManager _manager;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_manager != null)
                _manager.RetirementRecapReady -= HandleRecapReady;
            _manager = GameManager.EnsureExists().EnsureManager<CareerManager>("CareerManager");
            _manager.RetirementRecapReady += HandleRecapReady;

            if (scene.name != SceneCatalog.ManagementSceneName)
                return;
            if (_manager.RetirementRecap != null && !UI_Popup_RetirementRecap.IsOpen)
                UI_Popup_RetirementRecap.ShowRuntime(_manager.RetirementRecap);
        }

        private static void HandleRecapReady()
        {
            if (_manager?.RetirementRecap == null)
                return;
            UI_Popup_RetirementRecap.ShowRuntime(_manager.RetirementRecap);
        }
    }
}
