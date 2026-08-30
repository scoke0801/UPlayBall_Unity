using Baseball.Game.Manager;
using UnityEngine;

namespace Baseball.Presentation.UI
{
    /// <summary>
    /// UI 기반을 씬 프리팹 유무와 무관하게 준비한다.
    /// </summary>
    public static class PresentationBootstrap
    {
        /// <summary>
        /// UIManager를 준비한다. 씬 로드 전 자동 호출이지만, GameManager를 파괴하는
        /// 테스트가 같은 계약을 다시 만들 수 있도록 공개한다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void EnsureUiRoot()
        {
            GameManager.EnsureExists().EnsureManager<UIManager>("UIManager");
        }
    }
}
