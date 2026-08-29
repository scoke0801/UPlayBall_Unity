using System;

namespace Baseball.Presentation.Career
{
    /// <summary>
    /// 리그 접두사가 포함된 구단 이름을 UI용 짧은 표기로 변환한다.
    /// </summary>
    public static class CareerTeamNameFormatter
    {
        /// <summary>
        /// 구단명의 마지막 두 토큰을 연고지와 별칭으로 보고 연고지 두 글자를 반환한다.
        /// </summary>
        public static string GetMonogram(string teamName)
        {
            if (string.IsNullOrWhiteSpace(teamName))
                return "UP";

            string[] tokens = teamName.Split(
                new[] { ' ', '\t', '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries);
            string cityName = tokens.Length >= 2 ? tokens[tokens.Length - 2] : tokens[0];
            return cityName.Length <= 2 ? cityName : cityName.Substring(0, 2);
        }
    }
}
