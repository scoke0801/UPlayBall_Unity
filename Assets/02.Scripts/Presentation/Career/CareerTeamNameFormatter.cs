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
                return "유플";

            string[] tokens = teamName.Split(
                new[] { ' ', '\t', '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries);
            string cityName = tokens.Length >= 2 ? tokens[tokens.Length - 2] : tokens[0];
            return cityName.Length <= 2 ? cityName : cityName.Substring(0, 2);
        }

        /// <summary>좁은 표에서도 원본 연도와 전체 브랜드를 유지하도록 공백만 제거한다.</summary>
        public static string GetCompactName(string teamName)
        {
            if (string.IsNullOrWhiteSpace(teamName))
                return "-";

            var characters = new char[teamName.Length];
            int count = 0;
            for (int index = 0; index < teamName.Length; index++)
            {
                if (!char.IsWhiteSpace(teamName[index]))
                    characters[count++] = teamName[index];
            }
            return new string(characters, 0, count);
        }
    }
}
