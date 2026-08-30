using System.Text;
using UnityEngine;

namespace Baseball.Game.Data
{
    /// <summary>최초 승격으로 영구 해금되는 성장 선택 보상을 저작한다.</summary>
    [CreateAssetMenu(fileName = "LeagueGrowthMilestone", menuName = "Baseball/Data/Growth/League Growth Milestone")]
    public sealed class LeagueGrowthMilestoneAsset : ScriptableObject
    {
        [SerializeField, Range(0, 9)] private int _additionalCandidateLevel = 4;
        [SerializeField, Min(0)] private int _additionalProgramCandidates = 1;
        [SerializeField, Range(0, 9)] private int _repetitionWaiverLevel = 6;
        [SerializeField, Min(0)] private int _repetitionPenaltyWaivers = 1;
        [SerializeField, Range(0, 9)] private int _growthRedirectLevel = 8;
        [SerializeField, Range(0, 9)] private int _legacyTraitLevel = 9;

        public int AdditionalCandidateLevel => _additionalCandidateLevel;
        public int AdditionalProgramCandidates => _additionalProgramCandidates;
        public int RepetitionWaiverLevel => _repetitionWaiverLevel;
        public int RepetitionPenaltyWaivers => _repetitionPenaltyWaivers;
        public int GrowthRedirectLevel => _growthRedirectLevel;
        public int LegacyTraitLevel => _legacyTraitLevel;

        internal void AppendContent(StringBuilder builder)
        {
            builder.Append("milestones:").Append(_additionalCandidateLevel).Append('|')
                .Append(_additionalProgramCandidates).Append('|').Append(_repetitionWaiverLevel)
                .Append('|').Append(_repetitionPenaltyWaivers).Append('|')
                .Append(_growthRedirectLevel).Append('|').Append(_legacyTraitLevel).Append('|');
        }
    }
}
