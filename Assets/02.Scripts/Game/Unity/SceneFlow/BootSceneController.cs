using System.Collections;
using Baseball.Game.Historical;
using Baseball.Game.Manager;
using UnityEngine;

namespace Baseball.Game.SceneFlow
{
    /// <summary>
    /// Boot Scene에서 영속 매니저 준비 후 첫 콘텐츠 Scene 진입을 시작한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BootSceneController : MonoBehaviour
    {
        [SerializeField] private SceneId _initialScene = SceneId.Management;
        [SerializeField, Min(0f)] private float _minimumLoadingTime = 0.5f;

        private IEnumerator Start()
        {
            // BeforeSceneLoad 부트스트랩과 SceneContext.Start가 모두 끝난 다음 전환을 시작한다.
            yield return null;

            GameManager gameManager = GameManager.EnsureExists();

            // 역사 콘텐츠와 World 준비를 여기서 걸어 두면 Loading Scene이 도는 동안 함께 진행된다.
            // Loading 화면이 완료를 기다리므로, 타이틀에 도착했을 때는 두 모드 모두 즉시 열린다.
            gameManager.EnsureManager<HistoricalWarmupManager>("HistoricalWarmupManager").BeginWarmup();

            SceneLoadManager sceneLoadManager = gameManager
                .EnsureManager<SceneLoadManager>("SceneLoadManager");
            if (!sceneLoadManager.LoadScene(
                    _initialScene,
                    SceneTransitionMode.LoadingScreen,
                    _minimumLoadingTime))
            {
                Debug.LogError($"[BootSceneController] 첫 Scene 진입에 실패했습니다: {_initialScene}");
            }
        }
    }
}
