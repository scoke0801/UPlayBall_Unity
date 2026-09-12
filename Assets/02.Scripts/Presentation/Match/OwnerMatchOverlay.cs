using System;
using UnityEngine;

namespace Baseball.Presentation.Match
{
    /// <summary>오버레이 묶음과 수평 반전으로 표현할 실제 투타 방향이다.</summary>
    public readonly struct OwnerMatchHandedness
    {
        public OwnerMatchHandedness(
            Baseball.Core.Players.Handedness throwingHand,
            Baseball.Core.Players.Handedness battingHand)
        {
            ThrowingHand = throwingHand;
            BattingHand = battingHand;
        }

        public Baseball.Core.Players.Handedness ThrowingHand { get; }
        public Baseball.Core.Players.Handedness BattingHand { get; }
        public bool IsPitcherLeftHanded => ThrowingHand == Baseball.Core.Players.Handedness.Left;
        public bool UsesRightPitcherLeftBatterSet => IsPitcherLeftHanded !=
            (BattingHand == Baseball.Core.Players.Handedness.Left);
    }

    /// <summary>구단주 경기 중계가 지원하는 표현 계층 재생 속도다.</summary>
    public enum OwnerMatchPlaybackSpeed
    {
        Normal = 1,
        Fast = 2,
        FourTimes = 4,
        VeryFast = 5
    }

    /// <summary>확정된 경기 이벤트를 어느 밀도로 공개할지 정한다.</summary>
    public enum OwnerMatchViewingMode
    {
        EveryMoment = 0,
        KeyMoments = 1,
        ResultOnly = 2
    }

    /// <summary>구단주 경기 관전 화면에 적용할 로컬 사용자 설정 값이다.</summary>
    public readonly struct OwnerMatchPresentationOptions
    {
        public OwnerMatchPresentationOptions(
            OwnerMatchPlaybackSpeed playbackSpeed,
            OwnerMatchViewingMode viewingMode)
        {
            if (!Enum.IsDefined(typeof(OwnerMatchPlaybackSpeed), playbackSpeed))
                throw new ArgumentOutOfRangeException(nameof(playbackSpeed));
            if (!Enum.IsDefined(typeof(OwnerMatchViewingMode), viewingMode))
                throw new ArgumentOutOfRangeException(nameof(viewingMode));

            PlaybackSpeed = playbackSpeed;
            ViewingMode = viewingMode;
        }

        public OwnerMatchPlaybackSpeed PlaybackSpeed { get; }
        public OwnerMatchViewingMode ViewingMode { get; }
        public bool ShouldPlayMatchAudio => ViewingMode != OwnerMatchViewingMode.ResultOnly;
    }

    /// <summary>세이브 진행도와 독립적인 구단주 경기 관전 기본값을 보존한다.</summary>
    public static class OwnerMatchPresentationSettings
    {
        private const string PlaybackSpeedKey = "Baseball.OwnerMatch.PlaybackSpeed";
        private const string ViewingModeKey = "Baseball.OwnerMatch.ViewingMode";

        private static readonly OwnerMatchPresentationOptions DefaultOptions =
            new OwnerMatchPresentationOptions(
                OwnerMatchPlaybackSpeed.Normal,
                OwnerMatchViewingMode.EveryMoment);

        public static OwnerMatchPresentationOptions Load()
        {
            var speed = (OwnerMatchPlaybackSpeed)PlayerPrefs.GetInt(
                PlaybackSpeedKey,
                (int)DefaultOptions.PlaybackSpeed);
            if (speed == OwnerMatchPlaybackSpeed.FourTimes)
                speed = OwnerMatchPlaybackSpeed.VeryFast;
            if (!Enum.IsDefined(typeof(OwnerMatchPlaybackSpeed), speed))
                speed = DefaultOptions.PlaybackSpeed;

            var viewingMode = (OwnerMatchViewingMode)PlayerPrefs.GetInt(
                ViewingModeKey,
                (int)DefaultOptions.ViewingMode);
            if (!Enum.IsDefined(typeof(OwnerMatchViewingMode), viewingMode))
                viewingMode = DefaultOptions.ViewingMode;

            return new OwnerMatchPresentationOptions(speed, viewingMode);
        }

        public static void SetPlaybackSpeed(OwnerMatchPlaybackSpeed speed)
        {
            if (!Enum.IsDefined(typeof(OwnerMatchPlaybackSpeed), speed))
                throw new ArgumentOutOfRangeException(nameof(speed));
            PlayerPrefs.SetInt(PlaybackSpeedKey, (int)speed);
        }

        public static void SetViewingMode(OwnerMatchViewingMode mode)
        {
            if (!Enum.IsDefined(typeof(OwnerMatchViewingMode), mode))
                throw new ArgumentOutOfRangeException(nameof(mode));
            PlayerPrefs.SetInt(ViewingModeKey, (int)mode);
        }

        /// <summary>테스트와 사용자 설정 초기화가 같은 기본값 계약을 사용한다.</summary>
        public static void ResetToDefaults()
        {
            PlayerPrefs.DeleteKey(PlaybackSpeedKey);
            PlayerPrefs.DeleteKey(ViewingModeKey);
        }
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
            string permissionMessage,
            OwnerMatchViewingMode viewingMode = OwnerMatchViewingMode.EveryMoment)
        {
            if (visibleEventCount < 0)
                throw new ArgumentOutOfRangeException(nameof(visibleEventCount));
            if (totalEventCount < visibleEventCount)
                throw new ArgumentOutOfRangeException(nameof(totalEventCount));
            if (!Enum.IsDefined(typeof(OwnerMatchPlaybackSpeed), speed))
                throw new ArgumentOutOfRangeException(nameof(speed));
            if (!Enum.IsDefined(typeof(OwnerMatchViewingMode), viewingMode))
                throw new ArgumentOutOfRangeException(nameof(viewingMode));

            VisibleEventCount = visibleEventCount;
            TotalEventCount = totalEventCount;
            IsPaused = isPaused;
            Speed = speed;
            PermissionMessage = permissionMessage ?? string.Empty;
            ViewingMode = viewingMode;
        }

        public int VisibleEventCount { get; }
        public int TotalEventCount { get; }
        public bool IsPaused { get; }
        public OwnerMatchPlaybackSpeed Speed { get; }
        public string PermissionMessage { get; }
        public OwnerMatchViewingMode ViewingMode { get; }
        public bool HasMatch => TotalEventCount > 0;
        public bool IsComplete => HasMatch && VisibleEventCount >= TotalEventCount;
        public bool CanAdvance => HasMatch && !IsComplete;
        public bool CanTogglePause => CanAdvance;
        public bool CanChangeSpeed => CanAdvance;
        public bool CanChangeViewingMode => HasMatch && !IsComplete;
    }

    /// <summary>구단주 경기의 실제 권한인 결과 관전과 재생 제어만 노출한다.</summary>
    public interface IOwnerMatchOverlay
    {
        OwnerMatchOverlayState State { get; }
        MatchHudPresentationModel CurrentHud { get; }
        bool TryTogglePause();
        bool TrySetPlaybackSpeed(OwnerMatchPlaybackSpeed speed);
        bool TrySetViewingMode(OwnerMatchViewingMode mode);
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
        public bool TrySetViewingMode(OwnerMatchViewingMode mode) => false;
        public bool TryAdvance() => false;
        public bool TryRevealAll() => false;
    }
}
