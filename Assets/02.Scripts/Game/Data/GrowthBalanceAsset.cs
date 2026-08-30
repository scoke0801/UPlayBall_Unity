using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using UnityEngine;

namespace Baseball.Game.Data
{
    /// <summary>
    /// 성장 저작 에셋을 하나의 순수 GrowthBalanceTable로 변환하고 버전 고정용 ContentHash를 만든다.
    /// </summary>
    [CreateAssetMenu(fileName = "GrowthBalance", menuName = "Baseball/Data/Growth/Growth Balance")]
    public sealed class GrowthBalanceAsset : ScriptableObject
    {
        [SerializeField] private string _contentVersion = "growth-v1";
        [SerializeField] private TrainingProgramCatalogAsset _trainingPrograms;
        [SerializeField] private TrainingAccessTierAsset _trainingAccess;
        [SerializeField] private LeagueGrowthMilestoneAsset _leagueMilestones;
        [SerializeField] private SkillBlockCatalogAsset _skillBlocks;
        [SerializeField] private SkillGachaOfferCatalogAsset _skillGachaOffers;
        [SerializeField] private TraitDefinitionCatalogAsset _traits;

        public GrowthBalanceTable Build()
        {
            GrowthBalanceTable builtIn = GrowthBalanceTable.CreateDefault();
            TrainingProgramDefinition[] programs = _trainingPrograms != null
                ? _trainingPrograms.Build(builtIn.Programs)
                : builtIn.Programs;
            SkillBlockDefinition[] blocks = _skillBlocks != null
                ? _skillBlocks.Build(builtIn.SkillBlocks)
                : builtIn.SkillBlocks;
            SkillGachaBalanceTable gacha = _skillGachaOffers != null
                ? _skillGachaOffers.Build(builtIn.SkillGacha)
                : builtIn.SkillGacha;
            SkillTraitBalance traits = _traits != null
                ? _traits.Build()
                : builtIn.SkillTraits;
            GrowthProgressionBalance progression = BuildProgression(builtIn.Progression);
            return builtIn.WithAuthoredContent(programs, blocks, gacha, traits, progression);
        }

        public string CreateContentHash()
        {
            var canonical = new StringBuilder(4096);
            canonical.Append(_contentVersion?.Trim() ?? string.Empty).Append('|');
            if (_trainingPrograms != null) _trainingPrograms.AppendContent(canonical);
            else canonical.Append("programs:null|");
            if (_trainingAccess != null) _trainingAccess.AppendContent(canonical);
            else canonical.Append("access:null|");
            if (_leagueMilestones != null) _leagueMilestones.AppendContent(canonical);
            else canonical.Append("milestones:null|");
            if (_skillBlocks != null) _skillBlocks.AppendContent(canonical);
            else canonical.Append("blocks:null|");
            if (_skillGachaOffers != null) _skillGachaOffers.AppendContent(canonical);
            else canonical.Append("gacha:null|");
            if (_traits != null) _traits.AppendContent(canonical);
            else canonical.Append("traits:null|");
            using SHA256 sha = SHA256.Create();
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString()));
            var result = new StringBuilder(bytes.Length * 2);
            for (int index = 0; index < bytes.Length; index++)
                result.Append(bytes[index].ToString("x2"));
            return result.ToString();
        }

        private GrowthProgressionBalance BuildProgression(GrowthProgressionBalance builtIn)
        {
            if (_trainingAccess == null || _leagueMilestones == null)
                return builtIn;
            return new GrowthProgressionBalance(
                _trainingAccess.BuildAccessTiers(),
                _trainingAccess.BuildMinimumGachaLevels(),
                _leagueMilestones.AdditionalCandidateLevel,
                _leagueMilestones.RepetitionWaiverLevel,
                _leagueMilestones.GrowthRedirectLevel,
                _leagueMilestones.LegacyTraitLevel,
                _leagueMilestones.AdditionalProgramCandidates,
                _leagueMilestones.RepetitionPenaltyWaivers);
        }

    }

    internal static class GrowthContentHashFormatting
    {
        public static void AppendDouble(StringBuilder builder, double value)
        {
            builder.Append(value.ToString("R", CultureInfo.InvariantCulture));
        }
    }
}
