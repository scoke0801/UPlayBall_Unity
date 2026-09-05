using UnityEngine;

namespace Baseball.Presentation.SharedUI
{
    /// <summary>Play Mode 실행 경계에서 정적 UI 모드 선택을 초기화한다.</summary>
    public static class UiGameModeSessionRuntimeReset
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            UiGameModeSession.ResetForRuntime();
        }
    }
}
