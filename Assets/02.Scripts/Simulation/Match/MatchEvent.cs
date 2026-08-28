using System;
using Baseball.Simulation.PlateAppearance;

namespace Baseball.Simulation.Match
{
    /// <summary>
    /// 경기 이벤트가 발생한 공격 방향을 나타낸다.
    /// </summary>
    public enum InningHalf
    {
        Top = 0,
        Bottom = 1
    }

    /// <summary>
    /// 표현 레이어가 소비할 경기 사건 종류를 정의한다.
    /// </summary>
    public enum MatchEventType
    {
        Pitch = 0,
        Contact = 1,
        Hit = 2,
        RunnerAdvance = 3,
        Score = 4,
        Out = 5,
        PlateAppearanceEnded = 6,
        HalfInningEnded = 7,
        MatchEnded = 8
    }

    /// <summary>
    /// 할당 없이 전달할 수 있는 단일 경기 이벤트 값이다.
    /// </summary>
    public readonly struct MatchEvent : IEquatable<MatchEvent>
    {
        /// <summary>
        /// 이벤트 스트림의 한 항목을 생성한다.
        /// </summary>
        public MatchEvent(
            int sequence,
            MatchEventType eventType,
            int inning,
            InningHalf half,
            int batterId,
            int pitcherId,
            int playerId,
            PitchResult pitchResult,
            PlateAppearanceResult plateAppearanceResult,
            int fromBase,
            int toBase,
            int balls,
            int strikes,
            int outs,
            int awayScore,
            int homeScore)
        {
            Sequence = sequence;
            EventType = eventType;
            Inning = inning;
            Half = half;
            BatterId = batterId;
            PitcherId = pitcherId;
            PlayerId = playerId;
            PitchResult = pitchResult;
            PlateAppearanceResult = plateAppearanceResult;
            FromBase = fromBase;
            ToBase = toBase;
            Balls = balls;
            Strikes = strikes;
            Outs = outs;
            AwayScore = awayScore;
            HomeScore = homeScore;
        }

        public int Sequence { get; }
        public MatchEventType EventType { get; }
        public int Inning { get; }
        public InningHalf Half { get; }
        public int BatterId { get; }
        public int PitcherId { get; }
        public int PlayerId { get; }
        public PitchResult PitchResult { get; }
        public PlateAppearanceResult PlateAppearanceResult { get; }
        public int FromBase { get; }
        public int ToBase { get; }
        public int Balls { get; }
        public int Strikes { get; }
        public int Outs { get; }
        public int AwayScore { get; }
        public int HomeScore { get; }

        /// <summary>
        /// 결정론 테스트를 위해 모든 이벤트 필드가 같은지 비교한다.
        /// </summary>
        public bool Equals(MatchEvent other)
        {
            return Sequence == other.Sequence &&
                   EventType == other.EventType &&
                   Inning == other.Inning &&
                   Half == other.Half &&
                   BatterId == other.BatterId &&
                   PitcherId == other.PitcherId &&
                   PlayerId == other.PlayerId &&
                   PitchResult == other.PitchResult &&
                   PlateAppearanceResult == other.PlateAppearanceResult &&
                   FromBase == other.FromBase &&
                   ToBase == other.ToBase &&
                   Balls == other.Balls &&
                   Strikes == other.Strikes &&
                   Outs == other.Outs &&
                   AwayScore == other.AwayScore &&
                   HomeScore == other.HomeScore;
        }

        /// <summary>
        /// 다른 객체와 이벤트 값이 같은지 비교한다.
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is MatchEvent other && Equals(other);
        }

        /// <summary>
        /// 이벤트의 안정적인 해시 값을 반환한다.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Sequence;
                hash = hash * 397 ^ (int)EventType;
                hash = hash * 397 ^ Inning;
                hash = hash * 397 ^ (int)Half;
                hash = hash * 397 ^ BatterId;
                hash = hash * 397 ^ PitcherId;
                hash = hash * 397 ^ PlayerId;
                hash = hash * 397 ^ (int)PitchResult;
                hash = hash * 397 ^ (int)PlateAppearanceResult;
                hash = hash * 397 ^ FromBase;
                hash = hash * 397 ^ ToBase;
                hash = hash * 397 ^ Balls;
                hash = hash * 397 ^ Strikes;
                hash = hash * 397 ^ Outs;
                hash = hash * 397 ^ AwayScore;
                hash = hash * 397 ^ HomeScore;
                return hash;
            }
        }
    }
}
