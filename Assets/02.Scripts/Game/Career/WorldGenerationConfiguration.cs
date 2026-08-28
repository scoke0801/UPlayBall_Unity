using System;

namespace Baseball.Game.Career
{
    /// <summary>
    /// 기본 월드의 리그별 로스터 수준과 임시 구단명 구분 규칙을 보관한다.
    /// </summary>
    public sealed class WorldGenerationConfiguration
    {
        /// <summary>기본 AI 연령 범위를 사용해 리그별 전력과 이름 규칙을 만든다.</summary>
        public WorldGenerationConfiguration(
            int minorOverallBonus,
            int majorOverallBonus,
            string minorTeamNamePrefix,
            string majorTeamNamePrefix)
            : this(
                minorOverallBonus,
                majorOverallBonus,
                minorTeamNamePrefix,
                majorTeamNamePrefix,
                rookieMinimumAge: 18,
                rookieMaximumAge: 24,
                minorMinimumAge: 20,
                minorMaximumAge: 29,
                majorMinimumAge: 23,
                majorMaximumAge: 35)
        {
        }

        /// <summary>리그 단계별 AI 연령 범위까지 명시해 월드 생성 규칙을 만든다.</summary>
        public WorldGenerationConfiguration(
            int minorOverallBonus,
            int majorOverallBonus,
            string minorTeamNamePrefix,
            string majorTeamNamePrefix,
            int rookieMinimumAge,
            int rookieMaximumAge,
            int minorMinimumAge,
            int minorMaximumAge,
            int majorMinimumAge,
            int majorMaximumAge)
        {
            if (minorOverallBonus < 0 || majorOverallBonus <= minorOverallBonus)
                throw new ArgumentOutOfRangeException(nameof(majorOverallBonus));
            ValidateAgeRange(rookieMinimumAge, rookieMaximumAge, nameof(rookieMinimumAge));
            ValidateAgeRange(minorMinimumAge, minorMaximumAge, nameof(minorMinimumAge));
            ValidateAgeRange(majorMinimumAge, majorMaximumAge, nameof(majorMinimumAge));
            MinorOverallBonus = minorOverallBonus;
            MajorOverallBonus = majorOverallBonus;
            MinorTeamNamePrefix = minorTeamNamePrefix ?? string.Empty;
            MajorTeamNamePrefix = majorTeamNamePrefix ?? string.Empty;
            RookieMinimumAge = rookieMinimumAge;
            RookieMaximumAge = rookieMaximumAge;
            MinorMinimumAge = minorMinimumAge;
            MinorMaximumAge = minorMaximumAge;
            MajorMinimumAge = majorMinimumAge;
            MajorMaximumAge = majorMaximumAge;
        }

        public int MinorOverallBonus { get; }
        public int MajorOverallBonus { get; }
        public string MinorTeamNamePrefix { get; }
        public string MajorTeamNamePrefix { get; }
        public int RookieMinimumAge { get; }
        public int RookieMaximumAge { get; }
        public int MinorMinimumAge { get; }
        public int MinorMaximumAge { get; }
        public int MajorMinimumAge { get; }
        public int MajorMaximumAge { get; }

        /// <summary>지정 리그 단계의 AI 선수 최소 초기 나이를 반환한다.</summary>
        public int GetMinimumAge(LeagueLevel leagueLevel)
        {
            return leagueLevel switch
            {
                LeagueLevel.Rookie => RookieMinimumAge,
                LeagueLevel.Minor => MinorMinimumAge,
                LeagueLevel.Major => MajorMinimumAge,
                _ => throw new ArgumentOutOfRangeException(nameof(leagueLevel))
            };
        }

        /// <summary>지정 리그 단계의 AI 선수 최대 초기 나이를 반환한다.</summary>
        public int GetMaximumAge(LeagueLevel leagueLevel)
        {
            return leagueLevel switch
            {
                LeagueLevel.Rookie => RookieMaximumAge,
                LeagueLevel.Minor => MinorMaximumAge,
                LeagueLevel.Major => MajorMaximumAge,
                _ => throw new ArgumentOutOfRangeException(nameof(leagueLevel))
            };
        }

        public static WorldGenerationConfiguration CreateDefault()
        {
            return new WorldGenerationConfiguration(
                minorOverallBonus: 10,
                majorOverallBonus: 20,
                minorTeamNamePrefix: "마이너 ",
                majorTeamNamePrefix: "메이저 ");
        }

        private static void ValidateAgeRange(int minimum, int maximum, string parameterName)
        {
            if (minimum < 16 || maximum > 50 || maximum < minimum)
                throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
