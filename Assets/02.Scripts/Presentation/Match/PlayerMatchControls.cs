using System;

namespace Baseball.Presentation.Match
{
    /// <summary>선수 경기 UI가 노출할 타격 의도다.</summary>
    public enum PlayerBattingIntent
    {
        Patient = 0,
        Balanced = 1,
        Contact = 2,
        Power = 3
    }

    /// <summary>선수 경기 UI가 노출할 투구 의도다.</summary>
    public enum PlayerPitchingIntent
    {
        Balanced = 0,
        FullPower = 1,
        ControlFirst = 2,
        InduceChase = 3,
        QuickAttack = 4
    }

    /// <summary>현재 경기 장면에서 선수에게 허용된 입력만 나타낸다.</summary>
    public readonly struct PlayerMatchControlAvailability
    {
        /// <summary>Player 전용 입력 가능 여부를 한 시점의 값으로 묶는다.</summary>
        public PlayerMatchControlAvailability(
            bool canTogglePause,
            bool canChooseBattingIntent,
            bool canConfirmBattingIntent,
            bool canChoosePitchingIntent,
            bool canConfirmPitchingIntent,
            bool canAutoCompleteCurrentPlayerMoment,
            bool canUseBattingMiniGame,
            bool canUsePitchingMiniGame)
        {
            CanTogglePause = canTogglePause;
            CanChooseBattingIntent = canChooseBattingIntent;
            CanConfirmBattingIntent = canConfirmBattingIntent;
            CanChoosePitchingIntent = canChoosePitchingIntent;
            CanConfirmPitchingIntent = canConfirmPitchingIntent;
            CanAutoCompleteCurrentPlayerMoment = canAutoCompleteCurrentPlayerMoment;
            CanUseBattingMiniGame = canUseBattingMiniGame;
            CanUsePitchingMiniGame = canUsePitchingMiniGame;
        }

        public bool CanTogglePause { get; }
        public bool CanChooseBattingIntent { get; }
        public bool CanConfirmBattingIntent { get; }
        public bool CanChoosePitchingIntent { get; }
        public bool CanConfirmPitchingIntent { get; }
        public bool CanAutoCompleteCurrentPlayerMoment { get; }
        public bool CanUseBattingMiniGame { get; }
        public bool CanUsePitchingMiniGame { get; }
    }

    /// <summary>선수 경기 입력을 실제 Career Match 명령으로 전달하는 대상이다.</summary>
    public interface IPlayerMatchControlCommandSink
    {
        void SelectBattingIntent(PlayerBattingIntent intent);
        void ConfirmBattingIntent();
        void SelectPitchingIntent(PlayerPitchingIntent intent);
        void ConfirmPitchingIntent();
        void TogglePause();
        void AutoCompleteCurrentPlayerMoment();
    }

    /// <summary>선수 모드 경기 입력 계층이 제공하는 공개 계약이다.</summary>
    public interface IPlayerMatchControls
    {
        PlayerMatchControlAvailability Availability { get; }
        void UpdateAvailability(PlayerMatchControlAvailability availability);
        bool TrySelectBattingIntent(PlayerBattingIntent intent);
        bool TryConfirmBattingIntent();
        bool TrySelectPitchingIntent(PlayerPitchingIntent intent);
        bool TryConfirmPitchingIntent();
        bool TryTogglePause();
        bool TryAutoCompleteCurrentPlayerMoment();
    }

    /// <summary>가용성 검사를 통과한 선수 개인 입력만 Command Sink로 전달한다.</summary>
    public sealed class PlayerMatchControls : IPlayerMatchControls
    {
        private readonly IPlayerMatchControlCommandSink _sink;

        /// <summary>실제 Career Match 명령을 수행할 Sink를 주입받는다.</summary>
        public PlayerMatchControls(IPlayerMatchControlCommandSink sink)
        {
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        }

        public PlayerMatchControlAvailability Availability { get; private set; }

        /// <summary>현재 공개된 이벤트와 입력 대기 상태가 계산한 권한으로 교체한다.</summary>
        public void UpdateAvailability(PlayerMatchControlAvailability availability)
        {
            Availability = availability;
        }

        public bool TrySelectBattingIntent(PlayerBattingIntent intent)
        {
            if (!Availability.CanChooseBattingIntent ||
                !Enum.IsDefined(typeof(PlayerBattingIntent), intent))
                return false;
            _sink.SelectBattingIntent(intent);
            return true;
        }

        public bool TryConfirmBattingIntent()
        {
            if (!Availability.CanConfirmBattingIntent)
                return false;
            _sink.ConfirmBattingIntent();
            return true;
        }

        public bool TrySelectPitchingIntent(PlayerPitchingIntent intent)
        {
            if (!Availability.CanChoosePitchingIntent ||
                !Enum.IsDefined(typeof(PlayerPitchingIntent), intent))
                return false;
            _sink.SelectPitchingIntent(intent);
            return true;
        }

        public bool TryConfirmPitchingIntent()
        {
            if (!Availability.CanConfirmPitchingIntent)
                return false;
            _sink.ConfirmPitchingIntent();
            return true;
        }

        public bool TryTogglePause()
        {
            if (!Availability.CanTogglePause)
                return false;
            _sink.TogglePause();
            return true;
        }

        public bool TryAutoCompleteCurrentPlayerMoment()
        {
            if (!Availability.CanAutoCompleteCurrentPlayerMoment)
                return false;
            _sink.AutoCompleteCurrentPlayerMoment();
            return true;
        }
    }

    /// <summary>기존 Career Match 메서드를 Player 입력 Sink로 연결하는 콜백 구현이다.</summary>
    public sealed class PlayerMatchControlCallbacks : IPlayerMatchControlCommandSink
    {
        private readonly Action<PlayerBattingIntent> _selectBattingIntent;
        private readonly Action _confirmBattingIntent;
        private readonly Action<PlayerPitchingIntent> _selectPitchingIntent;
        private readonly Action _confirmPitchingIntent;
        private readonly Action _togglePause;
        private readonly Action _autoCompleteCurrentPlayerMoment;

        /// <summary>Player가 실제로 가진 여섯 가지 입력 동작만 주입받는다.</summary>
        public PlayerMatchControlCallbacks(
            Action<PlayerBattingIntent> selectBattingIntent,
            Action confirmBattingIntent,
            Action<PlayerPitchingIntent> selectPitchingIntent,
            Action confirmPitchingIntent,
            Action togglePause,
            Action autoCompleteCurrentPlayerMoment)
        {
            _selectBattingIntent = selectBattingIntent ?? throw new ArgumentNullException(nameof(selectBattingIntent));
            _confirmBattingIntent = confirmBattingIntent ?? throw new ArgumentNullException(nameof(confirmBattingIntent));
            _selectPitchingIntent = selectPitchingIntent ?? throw new ArgumentNullException(nameof(selectPitchingIntent));
            _confirmPitchingIntent = confirmPitchingIntent ?? throw new ArgumentNullException(nameof(confirmPitchingIntent));
            _togglePause = togglePause ?? throw new ArgumentNullException(nameof(togglePause));
            _autoCompleteCurrentPlayerMoment = autoCompleteCurrentPlayerMoment ??
                                               throw new ArgumentNullException(nameof(autoCompleteCurrentPlayerMoment));
        }

        public void SelectBattingIntent(PlayerBattingIntent intent) => _selectBattingIntent(intent);
        public void ConfirmBattingIntent() => _confirmBattingIntent();
        public void SelectPitchingIntent(PlayerPitchingIntent intent) => _selectPitchingIntent(intent);
        public void ConfirmPitchingIntent() => _confirmPitchingIntent();
        public void TogglePause() => _togglePause();
        public void AutoCompleteCurrentPlayerMoment() => _autoCompleteCurrentPlayerMoment();
    }
}
