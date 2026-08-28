using System;
using System.Collections.Generic;
using Baseball.Game.Input;
using Baseball.Game.Manager;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Baseball.Presentation.UI
{
    /// <summary>
    /// UI 등록, 레이어 배치, 표시 스택과 Cancel 입력을 관리한다.
    /// </summary>
    public sealed class UIManager : ManagerBehaviour<UIManager>, ISceneChangedManager
    {
        private const string UiRootResourcePath = "UI/UI_System_Root";

        private readonly Dictionary<Type, UIBase> _uiByType = new();
        private readonly Dictionary<string, UIBase> _uiByName = new(StringComparer.Ordinal);
        private readonly List<UIBase> _visibleStack = new();
        private readonly List<InputActionReference> _inputActionReferences = new();
        private UIRoot _uiRoot;
        private UIClickFeedback _clickFeedback;
        private EventSystem _eventSystem;
        private GameObject _ownedEventSystemObject;
        private InputContextLease _modalInputLease;

        public override int InitializationOrder => 100;
        public UIRoot Root => _uiRoot;
        public int VisibleCount => _visibleStack.Count;

        protected override void OnInitialize()
        {
            CreateUiRoot();
            EnsureEventSystem();
        }

        protected override void OnAfterInitialize()
        {
            InputManager inputManager = GameManager.EnsureExists()
                .EnsureManager<InputManager>("InputManager");
            inputManager.CancelPerformed += HandleCancelPerformed;
            EnsureEventSystem();
            EnsureClickFeedback();
        }

        protected override void OnShutdown()
        {
            if (InputManager.Instance != null)
                InputManager.Instance.CancelPerformed -= HandleCancelPerformed;

            _modalInputLease?.Dispose();
            _modalInputLease = null;

            for (int i = 0; i < _inputActionReferences.Count; i++)
            {
                if (_inputActionReferences[i] != null)
                    DestroyRuntimeObject(_inputActionReferences[i]);
            }
            _inputActionReferences.Clear();

            if (_ownedEventSystemObject != null)
                DestroyRuntimeObject(_ownedEventSystemObject);

            if (_uiRoot != null)
                DestroyRuntimeObject(_uiRoot.gameObject);

            _eventSystem = null;
            _ownedEventSystemObject = null;
            _clickFeedback = null;
            _uiRoot = null;
            _visibleStack.Clear();
            _uiByName.Clear();
            _uiByType.Clear();
        }

        /// <summary>
        /// UI 인스턴스를 타입과 클래스 이름으로 등록하고 해당 Canvas 아래에 배치한다.
        /// </summary>
        public bool Register(UIBase ui)
        {
            if (ui == null)
                return false;

            Type uiType = ui.GetType();
            string uiName = uiType.Name;
            if (_uiByType.ContainsKey(uiType) || _uiByName.ContainsKey(uiName))
                return false;

            _uiByType.Add(uiType, ui);
            _uiByName.Add(uiName, ui);
            ui.Attach(this);

            RectTransform layerRoot = _uiRoot != null ? _uiRoot.GetLayerRoot(ui.Layer) : null;
            if (layerRoot != null && ui.transform.parent != layerRoot)
                ui.transform.SetParent(layerRoot, false);

            ui.Initialize();
            return true;
        }

        /// <summary>
        /// 파괴되거나 명시적으로 제거된 UI를 모든 조회 구조에서 해제한다.
        /// </summary>
        public void Unregister(UIBase ui)
        {
            if (ui == null)
                return;

            _uiByType.Remove(ui.GetType());
            string uiName = ui.GetType().Name;
            if (_uiByName.TryGetValue(uiName, out UIBase registered) && registered == ui)
                _uiByName.Remove(uiName);
            _visibleStack.Remove(ui);
            ui.Detach(this);
            RefreshModalInputContext();
        }

        /// <summary>
        /// 등록된 타입의 UI를 표시하고 반환한다.
        /// </summary>
        public T Show<T>() where T : UIBase
        {
            if (!_uiByType.TryGetValue(typeof(T), out UIBase ui))
                return null;

            ui.Show();
            return (T)ui;
        }

        /// <summary>
        /// 클래스 이름으로 등록된 UI를 표시한다.
        /// </summary>
        public bool Show(string uiName)
        {
            if (!_uiByName.TryGetValue(uiName, out UIBase ui))
                return false;

            ui.Show();
            return true;
        }

        /// <summary>
        /// 표시 스택에서 Cancel로 닫을 수 있는 가장 위 UI를 닫는다.
        /// </summary>
        public bool CloseTopmost()
        {
            for (int i = _visibleStack.Count - 1; i >= 0; i--)
            {
                UIBase ui = _visibleStack[i];
                if (ui == null || !ui.IsVisible || !ui.CanCloseWithCancel)
                    continue;

                ui.Close();
                return true;
            }

            return false;
        }

        internal void NotifyShown(UIBase ui)
        {
            if (ui == null)
                return;

            _visibleStack.Remove(ui);
            _visibleStack.Add(ui);
            RefreshModalInputContext();
        }

        internal void NotifyHidden(UIBase ui)
        {
            _visibleStack.Remove(ui);
            RefreshModalInputContext();
        }

        public void OnSceneChanged(string previousSceneName, string currentSceneName)
        {
            EnsureEventSystem();
        }

        private void CreateUiRoot()
        {
            UIRoot rootPrefab = Resources.Load<UIRoot>(UiRootResourcePath);
            _uiRoot = rootPrefab != null
                ? Instantiate(rootPrefab, transform)
                : UIRoot.CreateRuntime(transform);
            _uiRoot.name = "UI_System_Root";
            _uiRoot.BuildMissingLayers();
        }

        /// <summary>
        /// 클릭 연출 오버레이를 System 레이어 최상단에 한 번만 생성한다.
        /// </summary>
        private void EnsureClickFeedback()
        {
            if (_clickFeedback != null || _uiRoot == null)
                return;

            RectTransform systemLayer = _uiRoot.GetLayerRoot(UILayer.System);
            if (systemLayer == null)
                return;

            _clickFeedback = UIClickFeedback.CreateRuntime(systemLayer);
        }

        private void EnsureEventSystem()
        {
            _eventSystem = EventSystem.current;
            if (_eventSystem == null)
            {
                var eventSystemObject = new GameObject(
                    "UI_System_EventSystem",
                    typeof(EventSystem),
                    typeof(InputSystemUIInputModule));
                eventSystemObject.transform.SetParent(transform, false);
                _eventSystem = eventSystemObject.GetComponent<EventSystem>();
                _ownedEventSystemObject = eventSystemObject;
            }

            InputSystemUIInputModule inputModule = _eventSystem.GetComponent<InputSystemUIInputModule>();
            if (inputModule == null)
                inputModule = _eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();

            ConfigureInputModule(inputModule);
        }

        private void ConfigureInputModule(InputSystemUIInputModule inputModule)
        {
            InputActionAsset actions = InputManager.Instance?.Actions;
            if (actions == null)
                return;

            DestroyInputActionReferences();
            inputModule.actionsAsset = actions;
            inputModule.move = CreateActionReference(actions, "UI/Navigate");
            inputModule.submit = CreateActionReference(actions, "UI/Submit");
            inputModule.cancel = CreateActionReference(actions, "UI/Cancel");
            inputModule.point = CreateActionReference(actions, "UI/Point");
            inputModule.leftClick = CreateActionReference(actions, "UI/Click");
            inputModule.scrollWheel = CreateActionReference(actions, "UI/ScrollWheel");
            inputModule.middleClick = CreateActionReference(actions, "UI/MiddleClick");
            inputModule.rightClick = CreateActionReference(actions, "UI/RightClick");
        }

        private InputActionReference CreateActionReference(InputActionAsset actions, string actionPath)
        {
            InputAction action = actions.FindAction(actionPath, false);
            if (action == null)
                return null;

            InputActionReference reference = InputActionReference.Create(action);
            _inputActionReferences.Add(reference);
            return reference;
        }

        private void DestroyInputActionReferences()
        {
            for (int i = 0; i < _inputActionReferences.Count; i++)
            {
                if (_inputActionReferences[i] != null)
                    DestroyRuntimeObject(_inputActionReferences[i]);
            }
            _inputActionReferences.Clear();
        }

        private static void DestroyRuntimeObject(UnityEngine.Object target)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(target);
                return;
            }
#endif
            Destroy(target);
        }

        private void RefreshModalInputContext()
        {
            bool hasBlockingUi = false;
            for (int i = 0; i < _visibleStack.Count; i++)
            {
                UIBase ui = _visibleStack[i];
                if (ui != null && ui.IsVisible && ui.BlocksLowerInput)
                {
                    hasBlockingUi = true;
                    break;
                }
            }

            if (hasBlockingUi && _modalInputLease == null)
                _modalInputLease = InputManager.Instance?.PushContext(InputContext.Modal);
            else if (!hasBlockingUi && _modalInputLease != null)
            {
                _modalInputLease.Dispose();
                _modalInputLease = null;
            }
        }

        private void HandleCancelPerformed()
        {
            CloseTopmost();
        }
    }
}
