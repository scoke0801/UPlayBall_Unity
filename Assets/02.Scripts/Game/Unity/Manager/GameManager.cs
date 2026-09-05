using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Baseball.Game.Manager
{
    /// <summary>
    /// 런타임 매니저의 등록 순서, 업데이트, 종료를 한곳에서 관리한다.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class GameManager : MonoBehaviour
    {
        private readonly List<IManager> _managers = new();
        private readonly List<IUpdatableManager> _updatableManagers = new();
        private readonly Dictionary<Type, IManager> _managerByType = new();
        private bool _isShuttingDown;
        private string _activeSceneName = string.Empty;

        public static GameManager Instance { get; private set; }
        public static bool HasInstance => Instance != null;
        public IReadOnlyList<IManager> Managers => _managers;

        /// <summary>
        /// 영속 GameRoot와 GameManager가 없으면 생성하고 반환한다.
        /// </summary>
        public static GameManager EnsureExists()
        {
            if (Instance != null)
                return Instance;

            Instance = FindFirstObjectByType<GameManager>();
            if (Instance != null)
                return Instance;

            var root = new GameObject("GameRoot");
            if (Application.isPlaying)
                DontDestroyOnLoad(root);
            GameManager created = root.AddComponent<GameManager>();
            // EditMode에서는 Awake가 자동 호출되지 않으므로 테스트 가능한 명시적 생성 경계에서
            // Instance만 확정한다. PlayMode에서는 Awake가 이미 같은 인스턴스를 등록한다.
            if (Instance == null)
                Instance = created;
            return created;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (Application.isPlaying)
                DontDestroyOnLoad(gameObject);
            _activeSceneName = SceneManager.GetActiveScene().name;
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        }

        /// <summary>
        /// 매니저를 초기화 순서에 맞춰 등록하고 즉시 사용할 수 있게 한다.
        /// </summary>
        public bool Register(IManager manager)
        {
            if (manager == null || _isShuttingDown || _managers.Contains(manager))
                return false;

            Type managerType = manager.GetType();
            if (_managerByType.ContainsKey(managerType))
            {
                Debug.LogWarning($"[GameManager] {managerType.Name}은 이미 등록되어 있습니다.");
                return false;
            }

            int insertIndex = FindInsertIndex(manager.InitializationOrder);
            _managers.Insert(insertIndex, manager);
            _managerByType.Add(managerType, manager);

            if (manager is Component component && component.transform.parent != transform)
                component.transform.SetParent(transform, false);

            manager.Initialize();
            manager.AfterInitialize();

            if (manager is IUpdatableManager updatableManager)
                _updatableManagers.Add(updatableManager);

            return true;
        }

        /// <summary>
        /// 매니저를 등록 해제하고 보유 자원을 정리한다.
        /// </summary>
        public bool Unregister(IManager manager)
        {
            if (manager == null || !_managers.Remove(manager))
                return false;

            _managerByType.Remove(manager.GetType());
            if (manager is IUpdatableManager updatableManager)
                _updatableManagers.Remove(updatableManager);

            if (!_isShuttingDown)
                manager.Shutdown();

            return true;
        }

        /// <summary>
        /// 정확한 타입으로 등록된 매니저를 조회한다.
        /// </summary>
        public bool TryGetManager<T>(out T manager) where T : class, IManager
        {
            if (_managerByType.TryGetValue(typeof(T), out IManager registered))
            {
                manager = registered as T;
                return manager != null;
            }

            for (int i = 0; i < _managers.Count; i++)
            {
                if (_managers[i] is T assignable)
                {
                    manager = assignable;
                    return true;
                }
            }

            manager = null;
            return false;
        }

        /// <summary>
        /// GameRoot 자식으로 컴포넌트 매니저를 한 번만 생성한다.
        /// </summary>
        public T EnsureManager<T>(string objectName) where T : ManagerBehaviour<T>
        {
            if (TryGetManager(out T existing))
                return existing;

            var managerObject = new GameObject(objectName);
            managerObject.transform.SetParent(transform, false);
            T created = managerObject.AddComponent<T>();
            // EditMode AddComponent는 ManagerBehaviour.Awake를 보장하지 않는다.
            if (!TryGetManager(out T registered))
            {
                Register(created);
                return created;
            }
            return registered;
        }

        private int FindInsertIndex(int initializationOrder)
        {
            for (int i = 0; i < _managers.Count; i++)
            {
                if (_managers[i].InitializationOrder > initializationOrder)
                    return i;
            }

            return _managers.Count;
        }

        private void Update()
        {
            float deltaTime = Time.unscaledDeltaTime;
            for (int i = 0; i < _updatableManagers.Count; i++)
                _updatableManagers[i].Tick(deltaTime);
        }

        private void HandleActiveSceneChanged(Scene previousScene, Scene currentScene)
        {
            string previousSceneName = string.IsNullOrEmpty(_activeSceneName)
                ? previousScene.name
                : _activeSceneName;
            _activeSceneName = currentScene.name;

            for (int i = 0; i < _managers.Count; i++)
            {
                if (_managers[i] is ISceneChangedManager sceneChangedManager)
                    sceneChangedManager.OnSceneChanged(previousSceneName, currentScene.name);
            }
        }

        private void OnDestroy()
        {
            if (Instance != this)
                return;

            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
            _isShuttingDown = true;

            for (int i = _managers.Count - 1; i >= 0; i--)
                _managers[i]?.Shutdown();

            _updatableManagers.Clear();
            _managerByType.Clear();
            _managers.Clear();
            Instance = null;
        }
    }
}
