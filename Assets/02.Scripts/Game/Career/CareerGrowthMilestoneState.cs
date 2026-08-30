using Baseball.Core.Balance;

namespace Baseball.Game.Career
{
    /// <summary>최초 리그 진출로 영구 해금되는 성장 선택 권한을 보존한다.</summary>
    public sealed class CareerGrowthMilestoneState
    {
        public int AdditionalProgramCandidates { get; private set; }
        public bool HasSeasonalRepetitionWaiver { get; private set; }
        public bool CanRedirectTrainingGrowth { get; private set; }
        public bool IsLegacyTraitConversionUnlocked { get; private set; }

        public void RecordFirstReach(LeagueLevel level, GrowthProgressionBalance progression = null)
        {
            GrowthProgressionBalance rules = progression ?? GrowthProgressionBalance.CreateDefault();
            int progressionLevel = (int)level;
            if (progressionLevel >= rules.AdditionalCandidateLevel)
                AdditionalProgramCandidates = rules.AdditionalProgramCandidates;
            if (progressionLevel >= rules.RepetitionWaiverLevel)
                HasSeasonalRepetitionWaiver = true;
            if (progressionLevel >= rules.GrowthRedirectLevel)
                CanRedirectTrainingGrowth = true;
            if (progressionLevel >= rules.LegacyTraitLevel)
                IsLegacyTraitConversionUnlocked = true;
        }

        /// <summary>구버전의 최고 도달 리그로 누락된 영구 성장 권한을 복원한다.</summary>
        public void MigrateFromHighestReachedLeague(
            LeagueLevel level,
            GrowthProgressionBalance progression = null)
        {
            RecordFirstReach(level, progression);
        }
    }
}
