using System;
using System.Collections.Generic;
using Baseball.Game.Manager;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Baseball.Game.Input
{
    /// <summary>
    /// Input System action map과 화면별 입력 context를 중앙에서 관리한다.
    /// </summary>
    public sealed class InputManager : ManagerBehaviour<InputManager>
    {
        private const string DefaultInputAssetPath = "Input/BaseballInputActions";
        private const string UiMapName = "UI";
        private const string MatchMapName = "Match";

        [SerializeField] private InputActionAsset _inputAsset;

        private readonly List<ContextEntry> _contextStack = new();
        private readonly List<ActionSubscription> _subscriptions = new();
        private InputActionAsset _runtimeInputAsset;
        private int _nextLeaseId = 1;
        private InputContext _baseContext = InputContext.Management;

        public override int InitializationOrder => -100;
        public InputActionAsset Actions => _runtimeInputAsset;
        public InputContext CurrentContext { get; private set; } = InputContext.Management;
        public InputDeviceKind LastInputDevice { get; private set; } = InputDeviceKind.KeyboardMouse;

        public event Action CancelPerformed;
        public event Action SubmitPerformed;
        public event Action PreviousTabPerformed;
        public event Action NextTabPerformed;
        public event Action ToggleMatchPlaybackPerformed;
        public event Action IncreaseMatchSpeedPerformed;
        public event Action DecreaseMatchSpeedPerformed;
        public event Action<InputContext> ContextChanged;
        public event Action<InputDeviceKind> InputDeviceChanged;

        /// <summary>포인터 클릭이 눌린 순간의 화면 좌표를 전달한다.</summary>
        public event Action<Vector2> PointerClicked;

        /// <summary>
        /// 화면 수명 동안 우선 적용할 입력 context를 스택에 추가한다.
        /// </summary>
        public InputContextLease PushContext(InputContext context)
        {
            int leaseId = _nextLeaseId++;
            _contextStack.Add(new ContextEntry(leaseId, context));
            ApplyCurrentContext();
            return new InputContextLease(this, leaseId);
        }

        /// <summary>
        /// 스택이 비어 있을 때 사용할 기본 입력 context를 변경한다.
        /// </summary>
        public void SetBaseContext(InputContext context)
        {
            if (context == InputContext.Modal)
                throw new ArgumentException("Modal context는 PushContext로만 사용해야 합니다.", nameof(context));

            _baseContext = context;
            ApplyCurrentContext();
        }

        internal void ReleaseContext(int leaseId)
        {
            for (int i = _contextStack.Count - 1; i >= 0; i--)
            {
                if (_contextStack[i].LeaseId != leaseId)
                    continue;

                _contextStack.RemoveAt(i);
                ApplyCurrentContext();
                return;
            }
        }

        protected override void OnInitialize()
        {
            InputActionAsset sourceAsset = _inputAsset != null
                ? _inputAsset
                : Resources.Load<InputActionAsset>(DefaultInputAssetPath);

            if (sourceAsset == null)
            {
                Debug.LogError($"[InputManager] Resources/{DefaultInputAssetPath}.inputactions를 찾지 못했습니다.");
                return;
            }

            _runtimeInputAsset = Instantiate(sourceAsset);
            _runtimeInputAsset.name = sourceAsset.name + " (Runtime)";
            SubscribeActions();
            ApplyCurrentContext(forceNotification: true);
        }

        protected override void OnShutdown()
        {
            for (int i = 0; i < _subscriptions.Count; i++)
                _subscriptions[i].Action.performed -= _subscriptions[i].Callback;

            _subscriptions.Clear();
            _contextStack.Clear();

            if (_runtimeInputAsset != null)
            {
                _runtimeInputAsset.Disable();
                DestroyRuntimeObject(_runtimeInputAsset);
            }

            _runtimeInputAsset = null;
            _baseContext = InputContext.Management;
            CurrentContext = InputContext.Management;
        }

        private void SubscribeActions()
        {
            Subscribe(UiMapName, "Cancel", HandleCancel);
            Subscribe(UiMapName, "Submit", HandleSubmit);
            Subscribe(UiMapName, "Navigate", HandleDeviceOnly);
            Subscribe(UiMapName, "Point", HandleDeviceOnly);
            Subscribe(UiMapName, "Click", HandleClick);
            Subscribe(UiMapName, "ScrollWheel", HandleDeviceOnly);
            Subscribe(UiMapName, "PreviousTab", HandlePreviousTab);
            Subscribe(UiMapName, "NextTab", HandleNextTab);
            Subscribe(MatchMapName, "TogglePlayback", HandleToggleMatchPlayback);
            Subscribe(MatchMapName, "IncreaseSpeed", HandleIncreaseMatchSpeed);
            Subscribe(MatchMapName, "DecreaseSpeed", HandleDecreaseMatchSpeed);
        }

        private void Subscribe(
            string mapName,
            string actionName,
            Action<InputAction.CallbackContext> callback)
        {
            InputAction action = _runtimeInputAsset.FindActionMap(mapName, false)?.FindAction(actionName, false);
            if (action == null)
            {
                Debug.LogWarning($"[InputManager] {mapName}/{actionName} action이 없습니다.");
                return;
            }

            action.performed += callback;
            _subscriptions.Add(new ActionSubscription(action, callback));
        }

        private void ApplyCurrentContext(bool forceNotification = false)
        {
            InputContext nextContext = _contextStack.Count == 0
                ? _baseContext
                : _contextStack[_contextStack.Count - 1].Context;

            bool hasChanged = nextContext != CurrentContext;
            CurrentContext = nextContext;

            if (_runtimeInputAsset != null)
            {
                _runtimeInputAsset.Disable();

                if (CurrentContext != InputContext.Disabled)
                    _runtimeInputAsset.FindActionMap(UiMapName, false)?.Enable();

                if (CurrentContext == InputContext.Match)
                    _runtimeInputAsset.FindActionMap(MatchMapName, false)?.Enable();
            }

            if (hasChanged || forceNotification)
                ContextChanged?.Invoke(CurrentContext);
        }

        private void HandleCancel(InputAction.CallbackContext context)
        {
            UpdateInputDevice(context.control?.device);
            CancelPerformed?.Invoke();
        }

        private void HandleDeviceOnly(InputAction.CallbackContext context)
        {
            UpdateInputDevice(context.control?.device);
        }

        private void HandleClick(InputAction.CallbackContext context)
        {
            InputDevice device = context.control?.device;
            UpdateInputDevice(device);

            // 클릭 좌표는 Click action(버튼)에 실려 오지 않으므로 입력을 발생시킨 포인터에서 직접 읽는다.
            if (device is Pointer pointer)
                PointerClicked?.Invoke(pointer.position.ReadValue());
        }

        private void HandleSubmit(InputAction.CallbackContext context)
        {
            UpdateInputDevice(context.control?.device);
            SubmitPerformed?.Invoke();
        }

        private void HandlePreviousTab(InputAction.CallbackContext context)
        {
            UpdateInputDevice(context.control?.device);
            PreviousTabPerformed?.Invoke();
        }

        private void HandleNextTab(InputAction.CallbackContext context)
        {
            UpdateInputDevice(context.control?.device);
            NextTabPerformed?.Invoke();
        }

        private void HandleToggleMatchPlayback(InputAction.CallbackContext context)
        {
            UpdateInputDevice(context.control?.device);
            ToggleMatchPlaybackPerformed?.Invoke();
        }

        private void HandleIncreaseMatchSpeed(InputAction.CallbackContext context)
        {
            UpdateInputDevice(context.control?.device);
            IncreaseMatchSpeedPerformed?.Invoke();
        }

        private void HandleDecreaseMatchSpeed(InputAction.CallbackContext context)
        {
            UpdateInputDevice(context.control?.device);
            DecreaseMatchSpeedPerformed?.Invoke();
        }

        private void UpdateInputDevice(InputDevice device)
        {
            InputDeviceKind nextKind = device is Gamepad
                ? InputDeviceKind.Gamepad
                : InputDeviceKind.KeyboardMouse;

            if (nextKind == LastInputDevice)
                return;

            LastInputDevice = nextKind;
            InputDeviceChanged?.Invoke(nextKind);
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

        private readonly struct ContextEntry
        {
            public ContextEntry(int leaseId, InputContext context)
            {
                LeaseId = leaseId;
                Context = context;
            }

            public int LeaseId { get; }
            public InputContext Context { get; }
        }

        private readonly struct ActionSubscription
        {
            public ActionSubscription(
                InputAction action,
                Action<InputAction.CallbackContext> callback)
            {
                Action = action;
                Callback = callback;
            }

            public InputAction Action { get; }
            public Action<InputAction.CallbackContext> Callback { get; }
        }
    }
}
