using System;
using Baseball.Core.Balance;
using Baseball.Core.Growth;

namespace Baseball.Game.Career
{
    /// <summary>
    /// 커리어 리그 단계와 성장 프로그램 접근 등급의 단방향 매핑을 제공한다.
    /// </summary>
    public static class CareerTrainingAccess
    {
        public static TrainingAccessTier GetAccessTier(
            LeagueLevel leagueLevel,
            GrowthProgressionBalance progression = null)
        {
            if (!LeagueLevelRules.IsValid(leagueLevel))
                throw new ArgumentOutOfRangeException(nameof(leagueLevel));
            return (progression ?? GrowthProgressionBalance.CreateDefault())
                .GetAccessTier((int)leagueLevel);
        }

        public static bool CanAccess(
            TrainingProgramDefinition program,
            LeagueLevel currentLeagueLevel,
            LeagueLevel? highestReachedLeagueLevel = null,
            GrowthProgressionBalance progression = null)
        {
            if (program == null)
                throw new ArgumentNullException(nameof(program));
            LeagueLevel knowledgeLevel = highestReachedLeagueLevel ?? currentLeagueLevel;
            return program.CanAccess(GetAccessTier(knowledgeLevel, progression));
        }

        public static TrainingProgramDefinition ApplyFacilitySupport(
            TrainingProgramDefinition program,
            LeagueLevel currentLeagueLevel,
            LeagueLevel highestReachedLeagueLevel,
            GrowthProgressionBalance progression = null)
        {
            if (!CanAccess(program, currentLeagueLevel, highestReachedLeagueLevel, progression))
                throw new InvalidOperationException("커리어에서 아직 습득하지 못한 성장 프로그램입니다.");
            return GetAccessTier(currentLeagueLevel, progression) < program.MinimumAccessTier
                ? program.ApplyFacilityPenalty()
                : program;
        }

        public static LeagueLevel GetMinimumGachaLeague(
            SkillGachaPurchaseTier tier,
            GrowthProgressionBalance progression = null)
        {
            return (LeagueLevel)(progression ?? GrowthProgressionBalance.CreateDefault())
                .GetMinimumGachaLevel(tier);
        }

        public static bool CanAccessGacha(
            SkillGachaPurchaseTier tier,
            LeagueLevel currentLeagueLevel,
            GrowthProgressionBalance progression = null)
        {
            if (!LeagueLevelRules.IsValid(currentLeagueLevel))
                throw new ArgumentOutOfRangeException(nameof(currentLeagueLevel));
            return currentLeagueLevel >= GetMinimumGachaLeague(tier, progression);
        }
    }
}
