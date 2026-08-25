using UnityEngine;

namespace Baseball.Game.Manager
{
    /// <summary>
    /// 단일 인스턴스와 GameManager 등록을 제공하는 매니저 공통 기반이다.
    /// </summary>
    public abstract class ManagerBehaviour<T> : MonoBehaviour, IManager
        where T : ManagerBehaviour<T>
    {
        public static T Instance { get; private set; }

        public virtual int InitializationOrder => 0;
        public bool IsInitialized { get; private set; }

        protected virtual void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = (T)this;
            GameManager.EnsureExists().Register(this);
        }

        public void Initialize()
        {
            if (IsInitialized)
                return;

            OnInitialize();
            IsInitialized = true;
        }

        public void AfterInitialize()
        {
            if (IsInitialized)
                OnAfterInitialize();
        }

        public void Shutdown()
        {
            if (!IsInitialized)
                return;

            OnShutdown();
            IsInitialized = false;
        }

        protected virtual void OnInitialize()
        {
        }

        protected virtual void OnAfterInitialize()
        {
        }

        protected virtual void OnShutdown()
        {
        }

        protected virtual void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            if (GameManager.HasInstance)
                GameManager.Instance.Unregister(this);
        }
    }
}
