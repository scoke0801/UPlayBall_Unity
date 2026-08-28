using System;
using Baseball.Core.Balance;

namespace Baseball.Game.Career
{
    /// <summary>정규 시즌 라운드를 밸런스에 정의된 실제 달력 날짜로 변환한다.</summary>
    public static class SeasonDateCalculator
    {
        public static DateTime GetGameDate(int year, int round, CareerSeasonBalance balance)
        {
            if (round <= 0)
                throw new ArgumentOutOfRangeException(nameof(round));

            int playedDays = round - 1;
            int restDays = playedDays / balance.GamesBetweenRestDays;
            return new DateTime(year, balance.SeasonOpeningMonth, balance.SeasonOpeningDay)
                .AddDays(playedDays + restDays);
        }
    }
}
