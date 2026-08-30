using System.Text;
using Baseball.Core.Balance;
using UnityEngine;

namespace Baseball.Game.Data
{
    /// <summary>고등급 블록 Trait의 경기 효과 계수를 저작한다.</summary>
    [CreateAssetMenu(fileName = "TraitDefinitionCatalog", menuName = "Baseball/Data/Growth/Trait Definition Catalog")]
    public sealed class TraitDefinitionCatalogAsset : ScriptableObject
    {
        [SerializeField, Min(0f)] private double _twoStrikeContactBonus = 3d;
        [SerializeField, Min(0f)] private double _scoringPositionContactBonus = 2d;
        [SerializeField, Min(0f)] private double _scoringPositionHardHitBonus = 0.008d;
        [SerializeField, Min(0f)] private double _lateInningStuffBonus = 3d;
        [SerializeField, Min(0f)] private double _crisisPitchingBonus = 2d;
        [SerializeField, Range(0f, 1f)] private double _fatiguePenaltyMitigation = 0.20d;
        [SerializeField, Range(0f, 1f)] private double _aggressiveRunningThresholdReduction = 0.04d;
        [SerializeField, Min(0)] private int _defensiveFocusAbilityBonus = 3;

        public SkillTraitBalance Build()
        {
            return new SkillTraitBalance(
                _twoStrikeContactBonus,
                _scoringPositionContactBonus,
                _scoringPositionHardHitBonus,
                _lateInningStuffBonus,
                _crisisPitchingBonus,
                _fatiguePenaltyMitigation,
                _aggressiveRunningThresholdReduction,
                _defensiveFocusAbilityBonus);
        }

        internal void AppendContent(StringBuilder builder)
        {
            builder.Append("traits:");
            GrowthContentHashFormatting.AppendDouble(builder, _twoStrikeContactBonus);
            builder.Append('|');
            GrowthContentHashFormatting.AppendDouble(builder, _scoringPositionContactBonus);
            builder.Append('|');
            GrowthContentHashFormatting.AppendDouble(builder, _scoringPositionHardHitBonus);
            builder.Append('|');
            GrowthContentHashFormatting.AppendDouble(builder, _lateInningStuffBonus);
            builder.Append('|');
            GrowthContentHashFormatting.AppendDouble(builder, _crisisPitchingBonus);
            builder.Append('|');
            GrowthContentHashFormatting.AppendDouble(builder, _fatiguePenaltyMitigation);
            builder.Append('|');
            GrowthContentHashFormatting.AppendDouble(builder, _aggressiveRunningThresholdReduction);
            builder.Append('|').Append(_defensiveFocusAbilityBonus).Append('|');
        }
    }
}
