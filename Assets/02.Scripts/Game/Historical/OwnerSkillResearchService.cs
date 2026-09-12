using System;
using System.Collections.Generic;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Simulation.Random;

namespace Baseball.Game.Historical
{
    /// <summary>연구·선택 상자·동일 계열 합성을 오프시즌 공유 인벤토리에 반영한다.</summary>
    public static class OwnerSkillResearchService
    {
        public static void Research(ManagerHistoricalRuntimeState runtime, SkillBlockDefinition[] definitions,
            OwnerDevelopmentBalance balance, IRandomSource random)
        {
            OwnerScheduleGateService.Evaluate(runtime, OwnerGrowthAction.SkillBlock).RequireAllowed();
            balance.Validate();
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (runtime.Economy.Money < balance.researchCost) throw new InvalidOperationException("스킬 연구에 필요한 PT가 부족합니다.");
            var rewards = new SkillBlockDefinition[2];
            for (int i = 0; i < rewards.Length; i++)
            {
                double roll = random.NextDouble() * 100;
                int rarity = roll < balance.researchWeights[0] ? 0 : roll < balance.researchWeights[0] + balance.researchWeights[1] ? 1 : 2;
                var pool = new List<SkillBlockDefinition>();
                foreach (var definition in definitions) if ((int)definition.Rarity == rarity) pool.Add(definition);
                if (pool.Count == 0) throw new InvalidOperationException("연구 블록 목록이 비어 있습니다.");
                pool.Sort((a,b) => string.CompareOrdinal(a.BlockId,b.BlockId));
                rewards[i] = pool[Math.Min(pool.Count - 1, (int)(random.NextDouble() * pool.Count))];
            }
            runtime.Economy.TrySpendMoney(balance.researchCost);
            foreach (var reward in rewards) runtime.PlayerGrowth.Inventory.Add(reward.BlockId);
            runtime.PlayerGrowth.Inventory.RecordResearch();
        }
        public static void OpenSelectionBox(ManagerHistoricalRuntimeState runtime, SkillBlockDefinition selected)
        {
            OwnerScheduleGateService.Evaluate(runtime, OwnerGrowthAction.SkillBlock).RequireAllowed();
            if (selected.Rarity != SkillBlockRarity.Unique) throw new InvalidOperationException("S 등급 블록을 선택하세요.");
            runtime.PlayerGrowth.Inventory.ConsumeSelectionBox();
            runtime.PlayerGrowth.Inventory.Add(selected.BlockId);
        }
        public static List<int> GetFusionMaterials(ManagerHistoricalRuntimeState runtime, SkillBlockDefinition[] definitions,
            SkillBlockCategory category, SkillBlockRarity rarity)
        {
            var result = new List<int>();
            foreach (var instance in runtime.PlayerGrowth.Inventory.Blocks)
            {
                bool equipped = false;
                foreach (var owned in runtime.OwnedCards)
                    foreach (var placement in owned.SkillBoard.Placements)
                        if (placement.Instance.InstanceId == instance.InstanceId) equipped = true;
                if (equipped) continue;
                foreach (var definition in definitions)
                    if (definition.BlockId == instance.DefinitionId && definition.Category == category && definition.Rarity == rarity)
                        result.Add(instance.InstanceId);
            }
            result.Sort();
            return result;
        }
        public static void Fuse(ManagerHistoricalRuntimeState runtime, SkillBlockDefinition[] definitions, SkillBlockDefinition selected)
        {
            OwnerScheduleGateService.Evaluate(runtime, OwnerGrowthAction.SkillBlock).RequireAllowed();
            if (selected.Rarity < SkillBlockRarity.Rare || selected.Rarity > SkillBlockRarity.Unique)
                throw new InvalidOperationException("합성 결과는 B~S 등급에서 선택하세요.");
            var materials = GetFusionMaterials(runtime, definitions, selected.Category, selected.Rarity - 1);
            if (materials.Count < 5) throw new InvalidOperationException("같은 계열의 미장착 하위 등급 블록 5개가 필요합니다.");
            for (int i = 0; i < 5; i++) runtime.PlayerGrowth.Inventory.Remove(materials[i]);
            runtime.PlayerGrowth.Inventory.Add(selected.BlockId);
        }
    }
}
