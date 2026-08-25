using Baseball.Game.Input;
using Baseball.Game.Manager;
using NUnit.Framework;
using UnityEngine;

namespace Baseball.Tests.EditMode.Game
{
    /// <summary>
    /// 매니저 등록 생명주기와 입력 context 원복을 검증한다.
    /// </summary>
    public sealed class GameManagerTests
    {
        private GameManager _gameManager;

        [SetUp]
        public void SetUp()
        {
            DestroyGameRoot();
            _gameManager = new GameObject("GameRoot_Test").AddComponent<GameManager>();
        }

        [TearDown]
        public void TearDown()
        {
            DestroyGameRoot();
        }

        [Test]
        public void Register_매니저를초기화하고조회할수있다()
        {
            var manager = new FakeManager(initializationOrder: 10);

            bool registered = _gameManager.Register(manager);

            Assert.That(registered, Is.True);
            Assert.That(manager.InitializeCount, Is.EqualTo(1));
            Assert.That(manager.AfterInitializeCount, Is.EqualTo(1));
            Assert.That(_gameManager.TryGetManager(out FakeManager found), Is.True);
            Assert.That(found, Is.SameAs(manager));
        }

        [Test]
        public void Unregister_매니저를한번만종료한다()
        {
            var manager = new FakeManager(initializationOrder: 10);
            _gameManager.Register(manager);

            bool unregistered = _gameManager.Unregister(manager);
            bool unregisteredAgain = _gameManager.Unregister(manager);

            Assert.That(unregistered, Is.True);
            Assert.That(unregisteredAgain, Is.False);
            Assert.That(manager.ShutdownCount, Is.EqualTo(1));
        }

        [Test]
        public void Register_초기화순서대로목록을정렬한다()
        {
            var later = new FakeManager(initializationOrder: 100);
            var earlier = new OtherFakeManager(initializationOrder: -100);

            _gameManager.Register(later);
            _gameManager.Register(earlier);

            Assert.That(_gameManager.Managers[0], Is.SameAs(earlier));
            Assert.That(_gameManager.Managers[1], Is.SameAs(later));
        }

        [Test]
        public void InputContextLease_중첩Context가끝나면BaseContext로복귀한다()
        {
            InputManager inputManager = _gameManager.EnsureManager<InputManager>("InputManager_Test");
            inputManager.SetBaseContext(InputContext.Match);

            using (inputManager.PushContext(InputContext.Modal))
            {
                Assert.That(inputManager.CurrentContext, Is.EqualTo(InputContext.Modal));
                inputManager.SetBaseContext(InputContext.Management);
                Assert.That(inputManager.CurrentContext, Is.EqualTo(InputContext.Modal));
            }

            Assert.That(inputManager.CurrentContext, Is.EqualTo(InputContext.Management));
        }

        private static void DestroyGameRoot()
        {
            if (GameManager.HasInstance)
                Object.DestroyImmediate(GameManager.Instance.gameObject);
        }

        private class FakeManager : IManager
        {
            public FakeManager(int initializationOrder)
            {
                InitializationOrder = initializationOrder;
            }

            public int InitializationOrder { get; }
            public bool IsInitialized { get; private set; }
            public int InitializeCount { get; private set; }
            public int AfterInitializeCount { get; private set; }
            public int ShutdownCount { get; private set; }

            public void Initialize()
            {
                IsInitialized = true;
                InitializeCount++;
            }

            public void AfterInitialize()
            {
                AfterInitializeCount++;
            }

            public void Shutdown()
            {
                IsInitialized = false;
                ShutdownCount++;
            }
        }

        private sealed class OtherFakeManager : FakeManager
        {
            public OtherFakeManager(int initializationOrder)
                : base(initializationOrder)
            {
            }
        }
    }
}
