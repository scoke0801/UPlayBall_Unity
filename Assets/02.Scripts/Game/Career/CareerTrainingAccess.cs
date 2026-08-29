using System;
using Baseball.Core.Growth;

namespace Baseball.Game.Career
{
    /// <summary>
    /// 커리어 리그 단계와 성장 프로그램 접근 등급의 단방향 매핑을 제공한다.
    /// </summary>
    public static class CareerTrainingAccess
    {
        public static TrainingAccessTier GetAccessTier(LeagueLevel leagueLevel)
        {
            if (!LeagueLevelRules.IsValid(leagueLevel))
                throw new ArgumentOutOfRangeException(nameof(leagueLevel));
            if (leagueLevel == LeagueLevel.Rookie)
                return TrainingAccessTier.Foundation;
            return leagueLevel == LeagueLevel.Minor
                ? TrainingAccessTier.Advanced
                : TrainingAccessTier.Elite;
        }

        public static bool CanAccess(
            TrainingProgramDefinition program,
            LeagueLevel leagueLevel)
        {
            if (program == null)
                throw new ArgumentNullException(nameof(program));
            return program.CanAccess(GetAccessTier(leagueLevel));
        }
    }
}
