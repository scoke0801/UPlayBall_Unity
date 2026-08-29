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
        public static readonly LeagueId WorldMain = new LeagueId("World.Main");
        public static readonly LeagueId AllStarMain = new LeagueId("AllStar.Main");
        public static readonly LeagueId ClassicMain = new LeagueId("Classic.Main");
        public static readonly LeagueId WinnersMain = new LeagueId("Winners.Main");
        public static readonly LeagueId ChampionMain = new LeagueId("Champion.Main");
        public static readonly LeagueId MasterMain = new LeagueId("Master.Main");
        public static readonly LeagueId GalaxyMain = new LeagueId("Galaxy.Main");

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
                LeagueLevel.World => WorldMain,
                LeagueLevel.AllStar => AllStarMain,
                LeagueLevel.Classic => ClassicMain,
                LeagueLevel.Winners => WinnersMain,
                LeagueLevel.Champion => ChampionMain,
                LeagueLevel.Master => MasterMain,
                LeagueLevel.Galaxy => GalaxyMain,
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
