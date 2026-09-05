using System;
using Baseball.Core.Players;
using Baseball.Core.Teams;

namespace Baseball.Game.Career
{
    public enum GameMode
    {
        PlayerCareer = 0,
        OwnerCareer = 1,
        [Obsolete("게임 모드 명칭은 OwnerCareer를 사용하세요. 실제 야구 감독 개념과 구분하기 위한 Legacy alias입니다.")]
        ManagerCareer = OwnerCareer
    }

    public enum BatterStyle
    {
        Balanced = 0,
        Contact = 1,
        Power = 2,
        Patient = 3,
        Aggressive = 4
    }

    public enum MatchProgressMode
    {
        FullGameWatch = 0,
        InterveneOnPlayer = 1,
        PlayerFocusAutomatic = 2,
        InstantResult = 3,
        MiniGame = 4
    }

    public enum PlayModeType
    {
        Simulation = 0,
        MiniGame = 1
    }

    public enum MiniGameScope
    {
        AllInvolvement = 0,
        KeyMoments = 1,
        ManualIntervention = 2,
        RecommendedByRole = 3
    }

    public enum MiniGameDifficulty
    {
        Beginner = 0,
        Standard = 1,
        Professional = 2
    }

    /// <summary>
    /// 커리어 중 바꿀 수 있는 경기 관전·방침 설정이다.
    /// </summary>
    public sealed class CareerGameSettings
    {
        public CareerGameSettings(
            BattingApproach battingApproach,
            PitchingApproach pitchingApproach,
            MatchProgressMode matchProgressMode,
            int gameSpeed,
            bool autoSlowOnPlayerEvent)
        {
            SetBattingApproach(battingApproach);
            SetPitchingApproach(pitchingApproach);
            SetMatchProgressMode(matchProgressMode);
            SetGameSpeed(gameSpeed);
            AutoSlowOnPlayerEvent = autoSlowOnPlayerEvent;
            MiniGameScope = MiniGameScope.RecommendedByRole;
            MiniGameDifficulty = MiniGameDifficulty.Standard;
        }

        public BattingApproach BattingApproach { get; private set; }
        public PitchingApproach PitchingApproach { get; private set; }
        public MatchProgressMode MatchProgressMode { get; private set; }
        public int GameSpeed { get; private set; }
        public bool AutoSlowOnPlayerEvent { get; private set; }
        public PlayModeType PlayMode => MatchProgressMode == MatchProgressMode.MiniGame
            ? PlayModeType.MiniGame
            : PlayModeType.Simulation;
        public MiniGameScope MiniGameScope { get; private set; }
        public MiniGameDifficulty MiniGameDifficulty { get; private set; }

        public void SetBattingApproach(BattingApproach approach)
        {
            if (!Enum.IsDefined(typeof(BattingApproach), approach))
                throw new ArgumentOutOfRangeException(nameof(approach));
            BattingApproach = approach;
        }

        public void SetPitchingApproach(PitchingApproach approach)
        {
            if (!Enum.IsDefined(typeof(PitchingApproach), approach))
                throw new ArgumentOutOfRangeException(nameof(approach));
            PitchingApproach = approach;
        }

        public void SetMatchProgressMode(MatchProgressMode mode)
        {
            if (!Enum.IsDefined(typeof(MatchProgressMode), mode))
                throw new ArgumentOutOfRangeException(nameof(mode));
            MatchProgressMode = mode;
        }

        public void SetGameSpeed(int speed)
        {
            if (speed is not (1 or 2 or 3 or 5))
                throw new ArgumentOutOfRangeException(nameof(speed), "경기 속도는 1×, 2×, 3×, 5× 중 하나여야 합니다.");
            GameSpeed = speed;
        }

        public void SetAutoSlowOnPlayerEvent(bool enabled) => AutoSlowOnPlayerEvent = enabled;

        public void SetMiniGameScope(MiniGameScope scope)
        {
            if (!Enum.IsDefined(typeof(MiniGameScope), scope))
                throw new ArgumentOutOfRangeException(nameof(scope));
            MiniGameScope = scope;
        }

        public void SetMiniGameDifficulty(MiniGameDifficulty difficulty)
        {
            if (!Enum.IsDefined(typeof(MiniGameDifficulty), difficulty))
                throw new ArgumentOutOfRangeException(nameof(difficulty));
            MiniGameDifficulty = difficulty;
        }

        public CareerGameSettings Clone()
        {
            var clone = new CareerGameSettings(
                BattingApproach,
                PitchingApproach,
                MatchProgressMode,
                GameSpeed,
                AutoSlowOnPlayerEvent);
            clone.SetMiniGameScope(MiniGameScope);
            clone.SetMiniGameDifficulty(MiniGameDifficulty);
            return clone;
        }

        public static CareerGameSettings CreateDefault()
        {
            return new CareerGameSettings(
                BattingApproach.Balanced,
                PitchingApproach.Balanced,
                MatchProgressMode.InterveneOnPlayer,
                gameSpeed: 2,
                autoSlowOnPlayerEvent: true);
        }
    }

    /// <summary>
    /// 최종 확정 전까지만 존재하는 신규 커리어 생성 입력 모음이다.
    /// </summary>
    public sealed class CareerCreationDraft
    {
        private int[] _initialAttributes = Array.Empty<int>();
        private PitchRepertoireEntry[] _pitchRepertoire = Array.Empty<PitchRepertoireEntry>();

        public GameMode GameMode { get; set; } = GameMode.PlayerCareer;
        public string PlayerName { get; set; } = string.Empty;
        public PlayerType? PlayerType { get; set; }
        public Handedness ThrowHand { get; set; } = Handedness.Right;
        public Handedness BatHand { get; set; } = Handedness.Right;
        public PlayerPosition FieldPosition { get; set; } = PlayerPosition.Unknown;
        public PitcherRole PreferredPitcherRole { get; set; } = PitcherRole.Starter;
        public BatterStyle BatterStyle { get; set; } = BatterStyle.Balanced;
        public CareerGameSettings GameSettings { get; set; } = CareerGameSettings.CreateDefault();
        public int[] InitialAttributes => (int[])_initialAttributes.Clone();
        public PitchRepertoireEntry[] PitchRepertoire => (PitchRepertoireEntry[])_pitchRepertoire.Clone();

        public void SetInitialAttributes(int[] values)
        {
            _initialAttributes = values == null ? Array.Empty<int>() : (int[])values.Clone();
        }

        public void SetPitchRepertoire(PitchRepertoireEntry[] entries)
        {
            _pitchRepertoire = entries == null
                ? Array.Empty<PitchRepertoireEntry>()
                : (PitchRepertoireEntry[])entries.Clone();
        }

        public CareerCreationProfile CreateProfile()
        {
            return new CareerCreationProfile(
                GameMode,
                PlayerType ?? Baseball.Core.Players.PlayerType.Batter,
                FieldPosition,
                PreferredPitcherRole,
                BatterStyle,
                _initialAttributes,
                _pitchRepertoire,
                GameSettings);
        }
    }

    /// <summary>
    /// 최종 확인 뒤 커리어 상태에 남기는 생성 결과다.
    /// </summary>
    public sealed class CareerCreationProfile
    {
        private readonly int[] _initialAttributes;
        private readonly PitchRepertoireEntry[] _pitchRepertoire;

        public CareerCreationProfile(
            GameMode gameMode,
            PlayerType playerType,
            PlayerPosition fieldPosition,
            PitcherRole preferredPitcherRole,
            BatterStyle batterStyle,
            int[] initialAttributes,
            PitchRepertoireEntry[] pitchRepertoire,
            CareerGameSettings gameSettings)
        {
            GameMode = gameMode;
            PlayerType = playerType;
            FieldPosition = fieldPosition;
            PreferredPitcherRole = preferredPitcherRole;
            BatterStyle = batterStyle;
            _initialAttributes = initialAttributes == null ? Array.Empty<int>() : (int[])initialAttributes.Clone();
            _pitchRepertoire = pitchRepertoire == null ? Array.Empty<PitchRepertoireEntry>() : (PitchRepertoireEntry[])pitchRepertoire.Clone();
            GameSettings = gameSettings?.Clone() ?? CareerGameSettings.CreateDefault();
        }

        public GameMode GameMode { get; }
        public PlayerType PlayerType { get; }
        public PlayerPosition FieldPosition { get; }
        public PitcherRole PreferredPitcherRole { get; }
        public BatterStyle BatterStyle { get; }
        public CareerGameSettings GameSettings { get; }
        public int[] InitialAttributes => (int[])_initialAttributes.Clone();
        public PitchRepertoireEntry[] PitchRepertoire => (PitchRepertoireEntry[])_pitchRepertoire.Clone();
    }
}
