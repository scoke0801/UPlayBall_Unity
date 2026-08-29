using System;
using System.Collections.Generic;

namespace Baseball.Game.Career
{
    /// <summary>한 경쟁 디비전의 표시·전력·경제·승강 규칙을 정의한다.</summary>
    public sealed class LeagueDefinition
    {
        public LeagueDefinition(
            string definitionId,
            LeagueLevel tier,
            string displayName,
            string uiDisplayName,
            int sortOrder,
            int targetRosterOverall,
            int overallSpread,
            double salaryMultiplier,
            double prizeMultiplier,
            int promotionSlots,
            int relegationSlots,
            int postseasonTeamCount,
            string postseasonFormat,
            string[] trainingUnlocks,
            int draftMinimumOverall,
            int draftMaximumOverall,
            int aiTacticalLevel,
            long firstReachReward,
            double prestigeMultiplier,
            int minimumPlayerAge,
            int maximumPlayerAge,
            string teamNamePrefix)
        {
            if (string.IsNullOrWhiteSpace(definitionId))
                throw new ArgumentException("LeagueDefinition ID는 비어 있을 수 없습니다.", nameof(definitionId));
            if (!LeagueLevelRules.IsValid(tier)) throw new ArgumentOutOfRangeException(nameof(tier));
            if (sortOrder < 0) throw new ArgumentOutOfRangeException(nameof(sortOrder));
            if (targetRosterOverall < 0 || targetRosterOverall > 100)
                throw new ArgumentOutOfRangeException(nameof(targetRosterOverall));
            if (overallSpread < 0 || salaryMultiplier <= 0d || prizeMultiplier <= 0d || prestigeMultiplier <= 0d)
                throw new ArgumentOutOfRangeException(nameof(overallSpread));
            if (promotionSlots < 0 || relegationSlots < 0 || postseasonTeamCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(promotionSlots));
            if (string.IsNullOrWhiteSpace(postseasonFormat))
                throw new ArgumentException("PostseasonFormat은 비어 있을 수 없습니다.", nameof(postseasonFormat));
            if (draftMinimumOverall < 0 || draftMaximumOverall > 100 || draftMinimumOverall > draftMaximumOverall)
                throw new ArgumentOutOfRangeException(nameof(draftMinimumOverall));
            if (aiTacticalLevel < 1 || aiTacticalLevel > LeagueLevelRules.Count)
                throw new ArgumentOutOfRangeException(nameof(aiTacticalLevel));
            if (firstReachReward < 0L)
                throw new ArgumentOutOfRangeException(nameof(firstReachReward));
            ValidateAgeRange(minimumPlayerAge, maximumPlayerAge, nameof(minimumPlayerAge));

            DefinitionId = definitionId.Trim();
            Tier = tier;
            DisplayName = displayName ?? string.Empty;
            UiDisplayName = uiDisplayName ?? string.Empty;
            SortOrder = sortOrder;
            TargetRosterOverall = targetRosterOverall;
            OverallSpread = overallSpread;
            SalaryMultiplier = salaryMultiplier;
            PrizeMultiplier = prizeMultiplier;
            PromotionSlots = promotionSlots;
            RelegationSlots = relegationSlots;
            PostseasonTeamCount = postseasonTeamCount;
            PostseasonFormat = postseasonFormat.Trim();
            TrainingUnlocks = CopyStrings(trainingUnlocks);
            DraftMinimumOverall = draftMinimumOverall;
            DraftMaximumOverall = draftMaximumOverall;
            AiTacticalLevel = aiTacticalLevel;
            FirstReachReward = firstReachReward;
            PrestigeMultiplier = prestigeMultiplier;
            MinimumPlayerAge = minimumPlayerAge;
            MaximumPlayerAge = maximumPlayerAge;
            TeamNamePrefix = teamNamePrefix ?? string.Empty;
        }

        public string DefinitionId { get; }
        public LeagueLevel Tier { get; }
        public string DisplayName { get; }
        public string UiDisplayName { get; }
        public int SortOrder { get; }
        public int TargetRosterOverall { get; }
        public int OverallSpread { get; }
        public double SalaryMultiplier { get; }
        public double PrizeMultiplier { get; }
        public int PromotionSlots { get; }
        public int RelegationSlots { get; }
        public int PostseasonTeamCount { get; }
        public string PostseasonFormat { get; }
        public IReadOnlyList<string> TrainingUnlocks { get; }
        public int DraftMinimumOverall { get; }
        public int DraftMaximumOverall { get; }
        public int AiTacticalLevel { get; }
        public long FirstReachReward { get; }
        public double PrestigeMultiplier { get; }
        public int MinimumPlayerAge { get; }
        public int MaximumPlayerAge { get; }
        public string TeamNamePrefix { get; }

        private static string[] CopyStrings(string[] source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<string>();
            var result = new string[source.Length];
            for (int index = 0; index < source.Length; index++)
            {
                if (string.IsNullOrWhiteSpace(source[index]))
                    throw new ArgumentException("TrainingUnlocks에는 빈 값이 들어갈 수 없습니다.", nameof(source));
                result[index] = source[index].Trim();
            }
            return result;
        }

        private static void ValidateAgeRange(int minimum, int maximum, string parameterName)
        {
            if (minimum < 16 || maximum > 50 || maximum < minimum)
                throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    /// <summary>
    /// 기본 월드의 리그별 로스터 수준과 임시 구단명 구분 규칙을 보관한다.
    /// </summary>
    public sealed class WorldGenerationConfiguration
    {
        public const int DefaultRosterSize = 25;
        public const int RookieTargetOverall = 52;

        private readonly LeagueDefinition[] _leagueDefinitions;

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
            RosterSize = DefaultRosterSize;
            _leagueDefinitions = CreateDefinitions(
                minorOverallBonus,
                majorOverallBonus,
                minorTeamNamePrefix,
                majorTeamNamePrefix,
                rookieMinimumAge,
                rookieMaximumAge,
                minorMinimumAge,
                minorMaximumAge,
                majorMinimumAge,
                majorMaximumAge);
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
        public int RosterSize { get; }
        public IReadOnlyList<LeagueDefinition> LeagueDefinitions => _leagueDefinitions;

        public LeagueDefinition GetDefinition(LeagueLevel leagueLevel)
        {
            for (int index = 0; index < _leagueDefinitions.Length; index++)
            {
                if (_leagueDefinitions[index].Tier == leagueLevel)
                    return _leagueDefinitions[index];
            }
            throw new ArgumentOutOfRangeException(nameof(leagueLevel));
        }

        public int GetCompetitionOverallBonus(LeagueLevel leagueLevel) =>
            GetDefinition(leagueLevel).TargetRosterOverall - RookieTargetOverall;

        /// <summary>지정 리그 단계의 AI 선수 최소 초기 나이를 반환한다.</summary>
        public int GetMinimumAge(LeagueLevel leagueLevel)
        {
            return GetDefinition(leagueLevel).MinimumPlayerAge;
        }

        /// <summary>지정 리그 단계의 AI 선수 최대 초기 나이를 반환한다.</summary>
        public int GetMaximumAge(LeagueLevel leagueLevel)
        {
            return GetDefinition(leagueLevel).MaximumPlayerAge;
        }

        public static WorldGenerationConfiguration CreateDefault()
        {
            return new WorldGenerationConfiguration(
                minorOverallBonus: 4,
                majorOverallBonus: 8,
                minorTeamNamePrefix: "마이너 ",
                majorTeamNamePrefix: "메이저 ");
        }

        /// <summary>밸런스·표시 시스템이 같은 기본 리그 정의를 사용하게 한다.</summary>
        public static LeagueDefinition GetDefaultDefinition(LeagueLevel leagueLevel) =>
            CreateDefault().GetDefinition(leagueLevel);

        private static LeagueDefinition[] CreateDefinitions(
            int minorOverallBonus,
            int majorOverallBonus,
            string minorPrefix,
            string majorPrefix,
            int rookieMinimumAge,
            int rookieMaximumAge,
            int minorMinimumAge,
            int minorMaximumAge,
            int majorMinimumAge,
            int majorMaximumAge)
        {
            int minorTarget = RookieTargetOverall + minorOverallBonus;
            int majorTarget = RookieTargetOverall + majorOverallBonus;
            if (majorTarget > 88)
                throw new ArgumentOutOfRangeException(nameof(majorOverallBonus), "Galaxy 목표 Overall 88을 넘길 수 없습니다.");
            return new[]
            {
                CreateDefinition(LeagueLevel.Rookie, "ROOKIE LEAGUE", "ROOKIE", 52, 1.00d, 0.25d, rookieMinimumAge, rookieMaximumAge, string.Empty, "기초 훈련"),
                CreateDefinition(LeagueLevel.Minor, "MINOR LEAGUE", "MINOR", minorTarget, 1.25d, 0.35d, minorMinimumAge, minorMaximumAge, minorPrefix, "포지션 특화 훈련"),
                CreateDefinition(LeagueLevel.Major, "MAJOR LEAGUE", "MAJOR", majorTarget, 1.60d, 0.50d, majorMinimumAge, majorMaximumAge, majorPrefix, "고급 타격·투구 프로그램"),
                CreateDefinition(LeagueLevel.World, "WORLD LEAGUE", "WORLD", Math.Max(64, majorTarget + 4), 2.10d, 0.65d, 21, 33, "월드 ", "유명 코치 프로그램"),
                CreateDefinition(LeagueLevel.AllStar, "ALL-STAR LEAGUE", "STAR", Math.Max(68, majorTarget + 8), 2.80d, 0.80d, 21, 35, "스타 ", "고급 훈련 파트너"),
                CreateDefinition(LeagueLevel.Classic, "CLASSIC LEAGUE", "CLASSIC", Math.Max(72, majorTarget + 12), 3.70d, 1.00d, 22, 37, "클래식 ", "해외 유학 상위 과정"),
                CreateDefinition(LeagueLevel.Winners, "WINNERS LEAGUE", "WINNERS", Math.Max(76, majorTarget + 16), 4.90d, 1.15d, 22, 38, "위너스 ", "정상급 선수 합동 훈련"),
                CreateDefinition(LeagueLevel.Champion, "CHAMPION LEAGUE", "CHAMPION", Math.Max(80, majorTarget + 20), 6.50d, 1.35d, 23, 39, "챔피언 ", "최상급 기술 개조"),
                CreateDefinition(LeagueLevel.Master, "MASTER LEAGUE", "MASTER", Math.Max(84, majorTarget + 24), 8.50d, 1.60d, 23, 40, "마스터 ", "개인 맞춤형 프로그램"),
                CreateDefinition(LeagueLevel.Galaxy, "GALAXY LEAGUE", "GALAXY", Math.Max(88, majorTarget + 28), 11.00d, 2.00d, 23, 42, "갤럭시 ", "전성기 유지·레거시 훈련")
            };
        }

        private static LeagueDefinition CreateDefinition(
            LeagueLevel tier,
            string displayName,
            string uiName,
            int targetOverall,
            double salaryMultiplier,
            double prestigeMultiplier,
            int minimumAge,
            int maximumAge,
            string prefix,
            string trainingUnlock)
        {
            if (targetOverall > 100)
                throw new ArgumentOutOfRangeException(nameof(targetOverall));
            return new LeagueDefinition(
                $"{tier}.Standard",
                tier,
                displayName,
                uiName,
                (int)tier,
                targetOverall,
                overallSpread: 6,
                salaryMultiplier,
                prizeMultiplier: salaryMultiplier,
                promotionSlots: tier == LeagueLevel.Galaxy ? 0 : 2,
                relegationSlots: tier == LeagueLevel.Rookie ? 0 : 2,
                postseasonTeamCount: 4,
                postseasonFormat: "FourTeamStepladder",
                trainingUnlocks: new[] { trainingUnlock },
                draftMinimumOverall: Math.Max(35, targetOverall - 12),
                draftMaximumOverall: Math.Min(95, targetOverall - 2),
                aiTacticalLevel: (int)tier + 1,
                firstReachReward: tier == LeagueLevel.Rookie
                    ? 0L
                    : (long)Math.Round(1_000_000d * salaryMultiplier, MidpointRounding.AwayFromZero),
                prestigeMultiplier,
                minimumAge,
                maximumAge,
                prefix);
        }

        private static void ValidateAgeRange(int minimum, int maximum, string parameterName)
        {
            if (minimum < 16 || maximum > 50 || maximum < minimum)
                throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
