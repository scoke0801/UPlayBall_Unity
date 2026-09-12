using System;

namespace Baseball.Core.Players
{
    /// <summary>저장·성장·경기 입력이 공유하는 선수 능력치 범위다.</summary>
    public static class AttributeRating
    {
        public const int Minimum = 0;
        public const int Maximum = 250;

        /// <summary>선수 능력치 범위를 검증한다. 백분율·숙련도에는 사용하지 않는다.</summary>
        public static int Validate(int value, string parameterName)
        {
            if (value < Minimum || value > Maximum)
                throw new ArgumentOutOfRangeException(parameterName, value, $"능력치는 {Minimum}~{Maximum} 범위여야 합니다.");

            return value;
        }
    }
}
