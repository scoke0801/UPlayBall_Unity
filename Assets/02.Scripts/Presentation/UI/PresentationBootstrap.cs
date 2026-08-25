using Baseball.Game.Manager;
using UnityEngine;

namespace Baseball.Presentation.UI
{
    /// <summary>
    /// UI 기반을 씬 프리팹 유무와 무관하게 준비한다.
    /// </summary>
    public static class PresentationBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            GameManager.EnsureExists().EnsureManager<UIManager>("UIManager");
        }
    }
}
