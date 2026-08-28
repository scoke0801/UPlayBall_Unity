using System;

namespace Baseball.Core.Players
{
    internal static class AttributeRating
    {
        public const int Minimum = 0;
        public const int Maximum = 100;

        public static int Validate(int value, string parameterName)
        {
            if (value < Minimum || value > Maximum)
                throw new ArgumentOutOfRangeException(parameterName, value, "능력치는 0~100 범위여야 합니다.");

            return value;
        }
    }
}
