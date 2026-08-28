using Baseball.Game.Manager;
using UnityEngine;

namespace Baseball.Game.SceneFlow
{
    /// <summary>
    /// 활성화된 Scene의 런타임 준비 완료를 SceneLoadManager에 알린다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SceneContext : MonoBehaviour
    {
        [SerializeField] private SceneId _sceneId;

        public SceneId SceneId => _sceneId;

        private void Start()
        {
            SceneLoadManager sceneLoadManager = GameManager.EnsureExists()
                .EnsureManager<SceneLoadManager>("SceneLoadManager");
            sceneLoadManager.NotifySceneReady(this);
        }
    }
}
