using System;
using System.Globalization;
using System.Text;

namespace Baseball.Presentation.Owner
{
    /// <summary>원 단위 Runtime 정수를 억·만원 단위의 한국어 금액으로 정확하게 표시한다.</summary>
    public static class OwnerMoneyFormatter
    {
        private const long WonPerMan = 10_000L;
        private const long WonPerEok = 100_000_000L;

        public static string Format(long won)
        {
            decimal absolute = Math.Abs((decimal)won);
            long eok = (long)(absolute / WonPerEok);
            long afterEok = (long)(absolute % WonPerEok);
            long man = afterEok / WonPerMan;
            long remainderWon = afterEok % WonPerMan;
            var builder = new StringBuilder(32);
            if (won < 0L) builder.Append('-');
            if (eok > 0L)
                AppendUnit(builder, eok, "억");
            if (man > 0L)
                AppendUnit(builder, man, "만");
            if (remainderWon > 0L || (eok == 0L && man == 0L))
                AppendUnit(builder, remainderWon, string.Empty);
            builder.Append('원');
            return builder.ToString();
        }

        public static string FormatSigned(long won)
        {
            return won > 0L ? "+" + Format(won) : Format(won);
        }

        private static void AppendUnit(StringBuilder builder, long value, string unit)
        {
            if (builder.Length > 0 && builder[builder.Length - 1] != '-')
                builder.Append(' ');
            builder.Append(value.ToString("N0", CultureInfo.InvariantCulture));
            builder.Append(unit);
        }
    }
}
