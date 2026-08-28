using System;

namespace Baseball.Game.Career
{
    /// <summary>
    /// 경쟁 단계와 분리해 월드 안의 리그를 영구적으로 식별한다.
    /// </summary>
    public readonly struct LeagueId : IEquatable<LeagueId>, IComparable<LeagueId>
    {
        public static readonly LeagueId Unassigned = new LeagueId("Unassigned");
        public static readonly LeagueId RookieMain = new LeagueId("Rookie.Main");
        public static readonly LeagueId MinorMain = new LeagueId("Minor.Main");
        public static readonly LeagueId MajorMain = new LeagueId("Major.Main");

        public LeagueId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("LeagueId는 비어 있을 수 없습니다.", nameof(value));
            Value = value.Trim();
        }

        public string Value { get; }
        public bool IsAssigned =>
            !string.IsNullOrWhiteSpace(Value)
            && !Equals(Unassigned);

        public static LeagueId FromLevel(LeagueLevel level)
        {
            return level switch
            {
                LeagueLevel.Rookie => RookieMain,
                LeagueLevel.Minor => MinorMain,
                LeagueLevel.Major => MajorMain,
                _ => throw new ArgumentOutOfRangeException(nameof(level))
            };
        }

        public int CompareTo(LeagueId other) =>
            string.Compare(Value, other.Value, StringComparison.Ordinal);

        public bool Equals(LeagueId other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is LeagueId other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                uint hash = 2166136261u;
                string value = Value ?? string.Empty;
                for (int index = 0; index < value.Length; index++)
                {
                    hash ^= value[index];
                    hash *= 16777619u;
                }
                return (int)hash;
            }
        }

        public override string ToString() => Value ?? string.Empty;

        public static bool operator ==(LeagueId left, LeagueId right) => left.Equals(right);
        public static bool operator !=(LeagueId left, LeagueId right) => !left.Equals(right);
    }
}
