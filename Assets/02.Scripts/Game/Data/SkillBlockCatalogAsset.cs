using System;
using System.Collections.Generic;
using System.Text;
using Baseball.Core.Growth;
using UnityEngine;

namespace Baseball.Game.Data
{
    /// <summary>스킬 블록의 모양·수치·Trait 연결을 저작 데이터에서 만든다.</summary>
    [CreateAssetMenu(fileName = "SkillBlockCatalog", menuName = "Baseball/Data/Growth/Skill Block Catalog")]
    public sealed class SkillBlockCatalogAsset : ScriptableObject
    {
        [Serializable]
        private struct AbilityBonusData
        {
            [SerializeField] private PlayerAbility _ability;
            [SerializeField] private int _amount;
            public AbilityChange ToDefinition() => new AbilityChange(_ability, _amount);
            public void AppendContent(StringBuilder builder) => builder.Append((int)_ability).Append(':').Append(_amount).Append(';');
        }

        [Serializable]
        private struct BlockData
        {
            [SerializeField] private string _blockId;
            [SerializeField] private SkillBlockRarity _rarity;
            [SerializeField] private SkillBlockCategory _category;
            [SerializeField] private TetrominoShape _shape;
            [SerializeField] private bool _canRotate;
            [SerializeField] private AbilityBonusData[] _abilityBonuses;
            [SerializeField, Min(0)] private long _sellValue;
            [SerializeField] private string _traitId;
            [SerializeField] private TraitSocketRule _traitSocketRule;
            [SerializeField] private bool _isUniqueReward;

            public SkillBlockDefinition ToDefinition()
            {
                var bonuses = new AbilityChange[_abilityBonuses?.Length ?? 0];
                for (int index = 0; index < bonuses.Length; index++)
                    bonuses[index] = _abilityBonuses[index].ToDefinition();
                return new SkillBlockDefinition(
                    _blockId,
                    _rarity,
                    _category,
                    TetrominoShapeCatalog.CreateCells(_shape),
                    _canRotate,
                    bonuses,
                    _sellValue,
                    _traitId,
                    _traitSocketRule,
                    _isUniqueReward);
            }

            public void AppendContent(StringBuilder builder)
            {
                builder.Append(_blockId).Append('|').Append((int)_rarity).Append('|')
                    .Append((int)_category).Append('|').Append((int)_shape).Append('|')
                    .Append(_canRotate).Append('|').Append(_sellValue).Append('|')
                    .Append(_traitId).Append('|').Append((int)_traitSocketRule).Append('|')
                    .Append(_isUniqueReward).Append('|');
                for (int index = 0; index < (_abilityBonuses?.Length ?? 0); index++)
                    _abilityBonuses[index].AppendContent(builder);
            }
        }

        [SerializeField] private bool _replaceBuiltInBlocks;
        [SerializeField] private BlockData[] _blocks = Array.Empty<BlockData>();

        public SkillBlockDefinition[] Build(SkillBlockDefinition[] builtIn)
        {
            if (_blocks == null || _blocks.Length == 0)
                return builtIn;
            var result = _replaceBuiltInBlocks
                ? new List<SkillBlockDefinition>(_blocks.Length)
                : new List<SkillBlockDefinition>(builtIn);
            for (int index = 0; index < _blocks.Length; index++)
            {
                SkillBlockDefinition authored = _blocks[index].ToDefinition();
                int existingIndex = FindBlock(result, authored.BlockId);
                if (existingIndex >= 0)
                    result[existingIndex] = authored;
                else
                    result.Add(authored);
            }
            return result.ToArray();
        }

        private static int FindBlock(List<SkillBlockDefinition> blocks, string blockId)
        {
            for (int index = 0; index < blocks.Count; index++)
                if (string.Equals(blocks[index].BlockId, blockId, StringComparison.Ordinal)) return index;
            return -1;
        }

        internal void AppendContent(StringBuilder builder)
        {
            builder.Append("blocks:").Append(_replaceBuiltInBlocks).Append('|');
            for (int index = 0; index < (_blocks?.Length ?? 0); index++)
                _blocks[index].AppendContent(builder);
        }
    }
}
