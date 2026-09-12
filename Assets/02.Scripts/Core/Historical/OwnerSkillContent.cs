using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Growth;

namespace Baseball.Core.Historical
{
    /// <summary>공통 테트로미노 콘텐츠에 구단주 전용 인접 세트 규칙을 적용한다.</summary>
    public static class OwnerSkillContent
    {
        /// <summary>블록의 모양과 보상을 유지하면서 구단주 인접 세트 보너스를 설정한다.</summary>
        public static GrowthBalanceTable Compose(GrowthBalanceTable source, int setBonus)
        {
            var result = new List<SkillBlockDefinition>();
            foreach (var block in source.SkillBlocks)
            {
                result.Add(new SkillBlockDefinition(block.BlockId, block.Rarity, block.Category, block.ShapeCells,
                    block.CanRotate, block.AbilityBonuses, block.SellValue, block.TraitId, block.IsUniqueReward,
                    adjacencySetBonus: setBonus));
            }
            return source.WithAuthoredContent(null, result.ToArray(), null, null, null);
        }
    }
}
