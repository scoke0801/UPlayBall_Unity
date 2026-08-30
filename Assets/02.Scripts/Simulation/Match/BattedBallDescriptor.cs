using Baseball.Core.Players;

namespace Baseball.Simulation.Match
{
    public enum BattedBallType
    {
        GroundBall = 0,
        LineDrive = 1,
        FlyBall = 2,
        PopUp = 3,
        Bunt = 4
    }

    public enum BattedBallDirection
    {
        Pull = 0,
        Center = 1,
        Opposite = 2
    }

    public enum FieldZone
    {
        Pitcher = 0,
        Catcher = 1,
        FirstBase = 2,
        SecondBase = 3,
        ThirdBase = 4,
        Shortstop = 5,
        LeftField = 6,
        CenterField = 7,
        RightField = 8,
        LeftFieldLine = 9,
        RightFieldLine = 10
    }

    public enum BallFlightBand
    {
        Short = 0,
        Medium = 1,
        Long = 2
    }

    public enum BallPaceBand
    {
        Slow = 0,
        Medium = 1,
        Fast = 2
    }

    /// <summary>
    /// 실제 좌표 물리 대신 수비·주루가 함께 소비할 범주형 타구 정보를 보관한다.
    /// </summary>
    public readonly struct BattedBallDescriptor
    {
        public BattedBallDescriptor(
            BattedBallType type,
            BattedBallDirection direction,
            FieldZone fieldZone,
            double quality,
            BallFlightBand hangTime,
            BallPaceBand pace,
            bool isHomeRun)
            : this(
                type,
                direction,
                fieldZone,
                quality,
                hangTime,
                pace,
                isHomeRun,
                0d,
                0d,
                0d,
                0d)
        {
        }

        public BattedBallDescriptor(
            BattedBallType type,
            BattedBallDirection direction,
            FieldZone fieldZone,
            double quality,
            BallFlightBand hangTime,
            BallPaceBand pace,
            bool isHomeRun,
            double exitVelocityMph,
            double launchAngleDegrees,
            double sprayAngleDegrees,
            double spinRateRpm)
        {
            Type = type;
            Direction = direction;
            FieldZone = fieldZone;
            Quality = quality;
            HangTime = hangTime;
            Pace = pace;
            IsHomeRun = isHomeRun;
            ExitVelocityMph = exitVelocityMph;
            LaunchAngleDegrees = launchAngleDegrees;
            SprayAngleDegrees = sprayAngleDegrees;
            SpinRateRpm = spinRateRpm;
        }

        public BattedBallType Type { get; }
        public BattedBallDirection Direction { get; }
        public FieldZone FieldZone { get; }
        public double Quality { get; }
        public BallFlightBand HangTime { get; }
        public BallPaceBand Pace { get; }
        public bool IsHomeRun { get; }
        public double ExitVelocityMph { get; }
        public double LaunchAngleDegrees { get; }
        public double SprayAngleDegrees { get; }
        public double SpinRateRpm { get; }
    }

    /// <summary>
    /// 레거시 선수의 여섯 능력치와 ID에서 소폭 변주된 타구 성향을 안정적으로 파생한다.
    /// </summary>
    public readonly struct BattingTendencyProfile
    {
        public BattingTendencyProfile(double pull, double groundBall, double flyBall, double aggressiveness)
        {
            PullTendency = pull;
            GroundBallTendency = groundBall;
            FlyBallTendency = flyBall;
            Aggressiveness = aggressiveness;
        }

        public double PullTendency { get; }
        public double GroundBallTendency { get; }
        public double FlyBallTendency { get; }
        public double Aggressiveness { get; }

        public static BattingTendencyProfile Derive(Player player)
        {
            uint hash = StablePlayerHash(player.PlayerId);
            double variation = ((hash & 255U) / 255d - 0.5d) * 0.08d;
            BatterAttributes ratings = player.BatterAttributes;
            return new BattingTendencyProfile(
                Clamp01(0.44d + (ratings.Power - ratings.Contact) * 0.002d + variation),
                Clamp01(0.47d + (50d - ratings.Power) * 0.0015d - variation * 0.5d),
                Clamp01(0.37d + (ratings.Power - 50d) * 0.0015d + variation * 0.5d),
                Clamp01(0.50d + (ratings.Mental - 50d) * 0.002d));
        }

        private static uint StablePlayerHash(int playerId)
        {
            uint value = unchecked((uint)playerId) + 0x9E3779B9U;
            value ^= value >> 16;
            value *= 0x85EBCA6BU;
            value ^= value >> 13;
            value *= 0xC2B2AE35U;
            return value ^ (value >> 16);
        }

        private static double Clamp01(double value)
        {
            if (value < 0d) return 0d;
            if (value > 1d) return 1d;
            return value;
        }
    }
}
