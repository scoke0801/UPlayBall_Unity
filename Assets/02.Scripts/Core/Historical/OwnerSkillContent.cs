using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Growth;

namespace Baseball.Core.Historical
{
    /// <summary>구단주에만 작은 블록과 인접 세트 규칙을 추가한다. 선수 커리어 콘텐츠는 보존한다.</summary>
    public static class OwnerSkillContent
    {
        public static GrowthBalanceTable Compose(GrowthBalanceTable source, int setBonus)
        {
            var result = new List<SkillBlockDefinition>();
            var categories = new HashSet<string>(StringComparer.Ordinal);
            foreach (var block in source.SkillBlocks)
            {
                result.Add(new SkillBlockDefinition(block.BlockId, block.Rarity, block.Category, block.ShapeCells,
                    block.CanRotate, block.AbilityBonuses, block.SellValue, block.TraitId, block.IsUniqueReward,
                    adjacencySetBonus: setBonus));
                string key = block.Category + "_" + block.Rarity;
                if (!categories.Add(key) || block.Rarity > SkillBlockRarity.Unique) continue;
                for (int count = 1; count <= 3; count++)
                {
                    var cells = new BoardCell[count];
                    for (int i = 0; i < count; i++) cells[i] = new BoardCell(i, 0);
                    var bonuses = new AbilityChange[block.AbilityBonuses.Length];
                    for (int i = 0; i < bonuses.Length; i++) bonuses[i] = new AbilityChange(block.AbilityBonuses[i].Ability,
                        Math.Max(1, block.AbilityBonuses[i].Amount * count / 4));
                    result.Add(new SkillBlockDefinition("owner_compact_" + key + "_" + count, block.Rarity,
                        block.Category, cells, true, bonuses, block.SellValue * count / 4,
                        allowCompactShape: true, adjacencySetBonus: setBonus));
                }
            }
            return source.WithAuthoredContent(null, result.ToArray(), null, null, null);
        }
    }
}
