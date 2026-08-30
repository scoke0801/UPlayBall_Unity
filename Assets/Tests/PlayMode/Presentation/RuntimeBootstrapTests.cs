using System.Collections;
using Baseball.Game.Input;
using Baseball.Game.Career;
using Baseball.Game.Manager;
using Baseball.Game.SceneFlow;
using Baseball.Presentation.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;

namespace Baseball.Tests.PlayMode.Presentation
{
    /// <summary>
    /// 실제 Player Loop에서 Manager, Input, UI bootstrap 연결을 검증한다.
    /// </summary>
    public sealed class RuntimeBootstrapTests
    {
        [UnityTest]
        public IEnumerator Bootstrap_필수런타임기반을자동생성한다()
        {
            // 앞선 테스트가 GameManager를 파괴하므로 씬 로드 전 자동 호출을 다시 재현한다.
            GameBootstrap.EnsureRuntimeManagers();
            PresentationBootstrap.EnsureUiRoot();
            yield return null;

            Assert.That(GameManager.HasInstance, Is.True);
            Assert.That(InputManager.Instance, Is.Not.Null);
            Assert.That(InputManager.Instance.IsInitialized, Is.True);
            Assert.That(InputManager.Instance.Actions, Is.Not.Null);

            Assert.That(SceneLoadManager.Instance, Is.Not.Null);
            Assert.That(SceneLoadManager.Instance.IsInitialized, Is.True);

            Assert.That(NewGameManager.Instance, Is.Not.Null);
            Assert.That(NewGameManager.Instance.IsInitialized, Is.True);
            Assert.That(NewGameManager.Instance.CurrentStep, Is.EqualTo(NewGameStep.Identity));

            Assert.That(CareerManager.Instance, Is.Not.Null);
            Assert.That(CareerManager.Instance.IsInitialized, Is.True);
            Assert.That(CareerManager.Instance.HasActiveCareer, Is.False);

            Assert.That(UIManager.Instance, Is.Not.Null);
            Assert.That(UIManager.Instance.IsInitialized, Is.True);
            Assert.That(UIManager.Instance.Root, Is.Not.Null);
            Assert.That(UIManager.Instance.Root.GetLayerRoot(UILayer.HUD), Is.Not.Null);
            Assert.That(UIManager.Instance.Root.GetLayerRoot(UILayer.Scene), Is.Not.Null);
            Assert.That(UIManager.Instance.Root.GetLayerRoot(UILayer.Popup), Is.Not.Null);
            Assert.That(UIManager.Instance.Root.GetLayerRoot(UILayer.System), Is.Not.Null);

            Assert.That(EventSystem.current, Is.Not.Null);
            Assert.That(EventSystem.current.GetComponent<InputSystemUIInputModule>(), Is.Not.Null);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameManager.HasInstance)
                Object.Destroy(GameManager.Instance.gameObject);

            yield return null;
        }
    }
}
