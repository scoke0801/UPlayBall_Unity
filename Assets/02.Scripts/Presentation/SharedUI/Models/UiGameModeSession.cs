using System;

namespace Baseball.Presentation.SharedUI
{
    /// <summary>동시에 여러 Save가 존재해도 화면에는 하나의 게임 모드 셸만 노출하도록 현재 선택을 소유한다.</summary>
    public static class UiGameModeSession
    {
        /// <summary>현재 사용자가 선택한 UI 모드이며 타이틀에서 아직 선택하지 않았다면 null이다.</summary>
        public static UiGameMode? CurrentMode { get; private set; }

        /// <summary>현재 모드가 바뀌면 각 모드 Coordinator에 새 선택을 전달한다.</summary>
        public static event Action<UiGameMode?> ModeChanged;

        /// <summary>새 게임 시작 또는 Save Load가 확정한 모드를 활성화한다.</summary>
        public static void Select(UiGameMode mode)
        {
            if (!Enum.IsDefined(typeof(UiGameMode), mode))
                throw new ArgumentOutOfRangeException(nameof(mode));
            if (CurrentMode == mode)
                return;

            CurrentMode = mode;
            ModeChanged?.Invoke(CurrentMode);
        }

        /// <summary>현재 선택이 지정 모드인지 확인한다.</summary>
        public static bool IsSelected(UiGameMode mode)
        {
            return CurrentMode == mode;
        }

        /// <summary>Management 진입 시 활성 Runtime만으로 모드를 안전하게 추론하며 둘 다 있으면 선택을 요구한다.</summary>
        public static UiGameMode? InferInitialMode(
            UiGameMode? currentMode,
            bool hasActivePlayerCareer,
            bool hasActiveOwnerRuntime)
        {
            if (currentMode == UiGameMode.PlayerCareer && hasActivePlayerCareer)
                return currentMode;
            if (currentMode == UiGameMode.OwnerCareer && hasActiveOwnerRuntime)
                return currentMode;
            if (currentMode.HasValue)
                return null;
            if (hasActivePlayerCareer == hasActiveOwnerRuntime)
                return null;
            return hasActivePlayerCareer ? UiGameMode.PlayerCareer : UiGameMode.OwnerCareer;
        }

        /// <summary>단 하나의 Runtime만 활성화된 복원 경로에서만 세션 선택을 확정한다.</summary>
        public static UiGameMode? ResolveInitialMode(
            bool hasActivePlayerCareer,
            bool hasActiveOwnerRuntime)
        {
            UiGameMode? previous = CurrentMode;
            UiGameMode? resolved = InferInitialMode(
                CurrentMode,
                hasActivePlayerCareer,
                hasActiveOwnerRuntime);
            if (previous.HasValue && !resolved.HasValue)
                Clear();
            if (resolved.HasValue && !CurrentMode.HasValue)
                Select(resolved.Value);
            return resolved;
        }

        /// <summary>타이틀로 돌아가 새 모드를 선택할 수 있도록 현재 선택을 비운다.</summary>
        public static void Clear()
        {
            if (!CurrentMode.HasValue)
                return;

            CurrentMode = null;
            ModeChanged?.Invoke(null);
        }

        /// <summary>Domain Reload를 끈 Play Mode에서도 이전 실행의 정적 구독과 선택을 제거한다.</summary>
        internal static void ResetForRuntime()
        {
            CurrentMode = null;
            ModeChanged = null;
        }
    }
}
