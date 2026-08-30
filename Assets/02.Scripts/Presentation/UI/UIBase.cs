using UnityEngine;

namespace Baseball.Presentation.UI
{
    /// <summary>
    /// 모든 런타임 UI의 등록과 표시 생명주기를 통일한다.
    /// </summary>
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public abstract class UIBase : MonoBehaviour
    {
        [SerializeField] private UILayer _layer = UILayer.Scene;
        [SerializeField] private bool _startVisible;
        [SerializeField] private bool _canCloseWithCancel = true;
        [SerializeField] private bool _blocksLowerInput;
        [SerializeField] private bool _destroyOnClose;

        private CanvasGroup _canvasGroup;
        private UIManager _owner;

        public virtual UILayer Layer => _layer;
        public bool IsInitialized { get; private set; }
        public bool IsVisible { get; private set; }
        public virtual bool CanCloseWithCancel => _canCloseWithCancel;
        public virtual bool BlocksLowerInput => _blocksLowerInput;
        public bool DestroyOnClose => _destroyOnClose;

        protected virtual void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            UIManager.Instance?.Register(this);

            if (_startVisible)
                Show();
            else
                ApplyVisibleState(false);
        }

        /// <summary>
        /// 최초 한 번 UI 참조와 이벤트를 초기화한다.
        /// </summary>
        public void Initialize()
        {
            if (IsInitialized)
                return;

            OnInitialize();
            CareerUiSkin.Apply(transform);
            IsInitialized = true;
        }

        /// <summary>
        /// UI를 표시하고 입력 스택의 최상단으로 올린다.
        /// </summary>
        public void Show()
        {
            Initialize();
            if (IsVisible)
                return;

            IsVisible = true;
            ApplyVisibleState(true);
            transform.SetAsLastSibling();
            OnShow();
            CareerUiSkin.Apply(transform);
            _owner?.NotifyShown(this);
        }

        /// <summary>
        /// 인스턴스를 유지한 채 UI를 숨긴다.
        /// </summary>
        public virtual void Hide()
        {
            if (!IsVisible)
                return;

            IsVisible = false;
            _owner?.NotifyHidden(this);
            OnHide();
            ApplyVisibleState(false);
        }

        /// <summary>
        /// UI를 닫고 설정에 따라 인스턴스를 파괴한다.
        /// </summary>
        public virtual void Close()
        {
            Hide();
            OnClose();

            if (_destroyOnClose)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(gameObject);
                else
#endif
                    Destroy(gameObject);
            }
        }

        protected virtual void OnInitialize()
        {
        }

        protected virtual void OnShow()
        {
        }

        protected virtual void OnHide()
        {
        }

        protected virtual void OnClose()
        {
        }

        protected virtual void OnDestroy()
        {
            _owner?.Unregister(this);
        }

        internal void Attach(UIManager owner)
        {
            _owner = owner;
        }

        internal void Detach(UIManager owner)
        {
            if (_owner == owner)
                _owner = null;
        }

        private void ApplyVisibleState(bool isVisible)
        {
            if (_canvasGroup == null)
                _canvasGroup = GetComponent<CanvasGroup>();

            _canvasGroup.alpha = isVisible ? 1f : 0f;
            _canvasGroup.interactable = isVisible;
            _canvasGroup.blocksRaycasts = isVisible;
        }
    }
}
