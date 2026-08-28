using System;

namespace Baseball.Core.Teams
{
    /// <summary>
    /// Unity 색상 타입 없이 구단 대표색을 저장하는 RGB 값이다.
    /// </summary>
    public readonly struct TeamColor : IEquatable<TeamColor>
    {
        public TeamColor(byte red, byte green, byte blue)
        {
            Red = red;
            Green = green;
            Blue = blue;
        }

        public byte Red { get; }
        public byte Green { get; }
        public byte Blue { get; }

        public bool Equals(TeamColor other)
        {
            return Red == other.Red && Green == other.Green && Blue == other.Blue;
        }

        public override bool Equals(object obj)
        {
            return obj is TeamColor other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (Red << 16) | (Green << 8) | Blue;
        }
    }

    /// <summary>
    /// 이름 후보와 대표색을 Game 레이어의 SO에서 순수 C# 생성기로 전달한다.
    /// </summary>
    public readonly struct TeamIdentityDefinition
    {
        public TeamIdentityDefinition(string name, TeamColor primaryColor)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("구단 이름은 비어 있을 수 없습니다.", nameof(name));

            Name = name;
            PrimaryColor = primaryColor;
        }

        public string Name { get; }
        public TeamColor PrimaryColor { get; }
    }

    /// <summary>
    /// 계약 시점에 구단이 제시하는 예상 출장 역할이다.
    /// </summary>
    public enum ExpectedRole
    {
        BenchCompetition,
        RosterCompetition,
        StartingCompetition
    }
}
