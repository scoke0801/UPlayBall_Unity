using System;

namespace Baseball.Presentation.Match
{
    /// <summary>구단주 경기 중계가 지원하는 표현 계층 재생 속도다.</summary>
    public enum OwnerMatchPlaybackSpeed
    {
        Normal = 1,
        Fast = 2,
        VeryFast = 5
    }

    /// <summary>선택 배속을 자동 중계의 실제 이벤트 공개 간격으로 변환한다.</summary>
    public static class OwnerMatchPlaybackTiming
    {
        private const float NormalAdvanceIntervalSeconds = 0.8f;

        public static float GetAdvanceIntervalSeconds(OwnerMatchPlaybackSpeed speed)
        {
            if (!Enum.IsDefined(typeof(OwnerMatchPlaybackSpeed), speed))
                throw new ArgumentOutOfRangeException(nameof(speed));
            return NormalAdvanceIntervalSeconds / (int)speed;
        }
    }

    /// <summary>구단주 경기 Overlay가 표시할 관전 상태와 권한 안내를 묶는다.</summary>
    public readonly struct OwnerMatchOverlayState
    {
        public OwnerMatchOverlayState(
            int visibleEventCount,
            int totalEventCount,
            bool isPaused,
            OwnerMatchPlaybackSpeed speed,
            string permissionMessage)
        {
            if (visibleEventCount < 0)
                throw new ArgumentOutOfRangeException(nameof(visibleEventCount));
            if (totalEventCount < visibleEventCount)
                throw new ArgumentOutOfRangeException(nameof(totalEventCount));
            if (!Enum.IsDefined(typeof(OwnerMatchPlaybackSpeed), speed))
                throw new ArgumentOutOfRangeException(nameof(speed));

            VisibleEventCount = visibleEventCount;
            TotalEventCount = totalEventCount;
            IsPaused = isPaused;
            Speed = speed;
            PermissionMessage = permissionMessage ?? string.Empty;
        }

        public int VisibleEventCount { get; }
        public int TotalEventCount { get; }
        public bool IsPaused { get; }
        public OwnerMatchPlaybackSpeed Speed { get; }
        public string PermissionMessage { get; }
        public bool HasMatch => TotalEventCount > 0;
        public bool IsComplete => HasMatch && VisibleEventCount >= TotalEventCount;
        public bool CanAdvance => HasMatch && !IsComplete;
        public bool CanTogglePause => CanAdvance;
        public bool CanChangeSpeed => CanAdvance;
    }

    /// <summary>구단주 경기의 실제 권한인 결과 관전과 재생 제어만 노출한다.</summary>
    public interface IOwnerMatchOverlay
    {
        OwnerMatchOverlayState State { get; }
        MatchHudPresentationModel CurrentHud { get; }
        bool TryTogglePause();
        bool TrySetPlaybackSpeed(OwnerMatchPlaybackSpeed speed);
        bool TryAdvance();
        bool TryRevealAll();
    }

    /// <summary>진행할 경기가 없을 때 사용하는 안전한 관전 Overlay다.</summary>
    public sealed class EmptyOwnerMatchOverlay : IOwnerMatchOverlay
    {
        private const string EmptyMessage = "관전할 경기가 없습니다.";

        public static EmptyOwnerMatchOverlay Instance { get; } = new EmptyOwnerMatchOverlay();

        private EmptyOwnerMatchOverlay()
        {
        }

        public OwnerMatchOverlayState State => new OwnerMatchOverlayState(
            0,
            0,
            false,
            OwnerMatchPlaybackSpeed.Normal,
            EmptyMessage);

        public MatchHudPresentationModel CurrentHud => null;

        public bool TryTogglePause() => false;
        public bool TrySetPlaybackSpeed(OwnerMatchPlaybackSpeed speed) => false;
        public bool TryAdvance() => false;
        public bool TryRevealAll() => false;
    }
}
