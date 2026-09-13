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
        /// <summary>장착·특별 보상 블록을 제외한 실제 보유 재료를 반환한다.</summary>
        public static List<int> GetAvailableMaterials(ManagerHistoricalRuntimeState runtime, SkillBlockDefinition[] definitions)
        {
            var result = new List<int>();
            foreach (var instance in runtime.PlayerGrowth.Inventory.Blocks)
            {
                var definition = FindDefinition(definitions, instance.DefinitionId);
                if (definition == null || definition.IsUniqueReward) continue;
                bool equipped = false;
                foreach (var card in runtime.OwnedCards)
                    foreach (var placement in card.SkillBoard.Placements)
                        if (placement.Instance.InstanceId == instance.InstanceId) equipped = true;
                if (!equipped) result.Add(instance.InstanceId);
            }
            result.Sort();
            return result;
        }

        /// <summary>미리보기와 추첨이 공유하는 실제 등급 확률을 계산한다. 난수는 소비하지 않는다.</summary>
        public static double[] GetGradeProbabilities(SkillBlockDefinition first, SkillBlockDefinition second,
            OwnerSkillFusionBalance balance, SkillFusionFocus focus, int failures)
        {
            balance.Validate();
            if (!Enum.IsDefined(typeof(SkillFusionFocus), focus)) throw new ArgumentOutOfRangeException(nameof(focus));
            int highest = Math.Max((int)first.Rarity, (int)second.Rarity);
            bool pity = highest < 4 && failures >= balance.pityFailures;
            var probabilities = new double[5];
            double sum = 0;
            for (int grade = Math.Max(0, highest - 1); grade < 5; grade++)
            {
                if (pity && grade <= highest) continue;
                probabilities[grade] = (balance.gradeWeights[(int)first.Rarity * 5 + grade] + balance.gradeWeights[(int)second.Rarity * 5 + grade]) / 2;
                sum += probabilities[grade];
            }
            if (sum <= 0) throw new InvalidOperationException("합성 결과 확률이 비어 있습니다.");
            for (int grade = 0; grade < 5; grade++) probabilities[grade] /= sum;
            if (focus != SkillFusionFocus.Grade || highest == 4 || pity) return probabilities;
            double upgrade = 0;
            for (int grade = highest + 1; grade < 5; grade++) upgrade += probabilities[grade];
            if (upgrade <= 0 || upgrade >= 1) return probabilities;
            double bonus = Math.Min(balance.gradeBonus, 1 - upgrade);
            // 승급 결과끼리의 비율을 보존하며 동급·하위 확률에서 보정분을 이동한다.
            for (int grade = 0; grade < 5; grade++)
                probabilities[grade] *= grade > highest ? (upgrade + bonus) / upgrade : (1 - upgrade - bonus) / (1 - upgrade);
            return probabilities;
        }

        /// <summary>등급·계열·모양을 추첨한다. 동일 재료 보정은 표시된 정확한 확률로 적용한다.</summary>
        public static SkillBlockDefinition RollFusion(SkillBlockDefinition[] definitions, SkillBlockDefinition first,
            SkillBlockDefinition second, OwnerSkillFusionBalance balance, SkillFusionFocus focus, int failures, IRandomSource random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            double[] probabilities = GetGradeProbabilities(first, second, balance, focus, failures);
            double roll = random.NextDouble(), cumulative = 0;
            int rarity = 4;
            for (int i = 0; i < 5; i++) { cumulative += probabilities[i]; if (roll < cumulative) { rarity = i; break; } }
            var pool = new List<SkillBlockDefinition>();
            foreach (var definition in definitions)
                if ((int)definition.Rarity == rarity && !definition.IsUniqueReward && IsSamePlayerType(first, definition)) pool.Add(definition);
            pool.Sort((a, b) => string.CompareOrdinal(a.BlockId, b.BlockId));
            if (pool.Count == 0) throw new InvalidOperationException("합성 결과 블록 목록이 비어 있습니다.");
            var categories = new List<SkillBlockCategory>();
            foreach (var definition in pool) if (!categories.Contains(definition.Category)) categories.Add(definition.Category);
            categories.Sort();
            SkillBlockCategory category;
            if (first.Category == second.Category && categories.Count > 1 && categories.Contains(first.Category))
            {
                double chance = Math.Min(1, balance.sameCategoryChance + (focus == SkillFusionFocus.Ability ? balance.categoryBonus : 0));
                if (random.NextDouble() < chance) category = first.Category;
                else { categories.Remove(first.Category); category = categories[Pick(categories.Count, random)]; }
            }
            else category = categories[Pick(categories.Count, random)];
            pool.RemoveAll(item => item.Category != category);
            if (HaveSameShape(first, second))
            {
                var matching = pool.FindAll(item => HaveSameShape(first, item));
                var other = pool.FindAll(item => !HaveSameShape(first, item));
                if (matching.Count > 0 && other.Count > 0)
                {
                    double chance = Math.Min(1, balance.sameShapeChance + (focus == SkillFusionFocus.Shape ? balance.shapeBonus : 0));
                    pool = random.NextDouble() < chance ? matching : other;
                }
            }
            return pool[Pick(pool.Count, random)];
        }

        /// <summary>두 재료와 PT를 소모하고 추첨 블록 하나와 합성 포인트를 지급한다.</summary>
        public static SkillBlockDefinition Fuse(ManagerHistoricalRuntimeState runtime, SkillBlockDefinition[] definitions,
            int firstId, int secondId, OwnerSkillFusionBalance balance, SkillFusionFocus focus, IRandomSource random)
        {
            OwnerScheduleGateService.Evaluate(runtime, OwnerGrowthAction.SkillBlock).RequireAllowed();
            balance.Validate();
            var available = GetAvailableMaterials(runtime, definitions);
            if (firstId == secondId || !available.Contains(firstId) || !available.Contains(secondId))
                throw new InvalidOperationException("서로 다른 미장착 블록 2개를 선택하세요.");
            var inventory = runtime.PlayerGrowth.Inventory;
            var first = FindDefinition(definitions, inventory.GetRequired(firstId).DefinitionId);
            var second = FindDefinition(definitions, inventory.GetRequired(secondId).DefinitionId);
            if (!IsSamePlayerType(first, second)) throw new InvalidOperationException("야수 또는 투수 블록끼리 합성하세요.");
            if (runtime.Economy.Money < balance.cost) throw new InvalidOperationException("합성에 필요한 PT가 부족합니다.");
            if (inventory.NextInstanceId == int.MaxValue || inventory.FusionCount == int.MaxValue)
                throw new InvalidOperationException("합성 보관 한도에 도달했습니다.");
            var highest = (SkillBlockRarity)Math.Max((int)first.Rarity, (int)second.Rarity);
            var result = RollFusion(definitions, first, second, balance, focus, inventory.GetFusionFailures(highest), random);
            if (!runtime.Economy.TrySpendMoney(balance.cost)) throw new InvalidOperationException("합성에 필요한 PT가 부족합니다.");
            inventory.Remove(firstId); inventory.Remove(secondId);
            inventory.Add(result.BlockId);
            inventory.RecordFusion(highest, result.Rarity);
            return result;
        }

        /// <summary>누적 포인트로 유니크 블록을 지정 제작한다. 랜덤 합성과 별도 보상이다.</summary>
        public static void Craft(ManagerHistoricalRuntimeState runtime, SkillBlockDefinition selected, OwnerSkillFusionBalance balance)
        {
            OwnerScheduleGateService.Evaluate(runtime, OwnerGrowthAction.SkillBlock).RequireAllowed();
            balance.Validate();
            if (selected.Rarity != SkillBlockRarity.Unique || selected.IsUniqueReward)
                throw new InvalidOperationException("유니크 일반 블록을 선택하세요.");
            if (runtime.PlayerGrowth.Inventory.NextInstanceId == int.MaxValue) throw new InvalidOperationException("블록 보관 한도입니다.");
            runtime.PlayerGrowth.Inventory.ExchangeFusionPoints(balance.pointsPerCraft);
            runtime.PlayerGrowth.Inventory.Add(selected.BlockId);
        }

        public static bool HaveSameShape(SkillBlockDefinition first, SkillBlockDefinition second)
        {
            foreach (var cell in first.ShapeCells)
            {
                bool found = false;
                foreach (var other in second.ShapeCells) if (cell.X == other.X && cell.Y == other.Y) found = true;
                if (!found) return false;
            }
            return true;
        }
        private static bool IsSamePlayerType(SkillBlockDefinition first, SkillBlockDefinition second) =>
            SkillBlockCategoryCatalog.IsAvailableTo(first.Category, Baseball.Core.Players.PlayerType.Pitcher) ==
            SkillBlockCategoryCatalog.IsAvailableTo(second.Category, Baseball.Core.Players.PlayerType.Pitcher);
        private static int Pick(int count, IRandomSource random) => Math.Min(count - 1, (int)(random.NextDouble() * count));
        private static SkillBlockDefinition FindDefinition(SkillBlockDefinition[] definitions, string id)
        {
            foreach (var definition in definitions) if (definition.BlockId == id) return definition;
            return null;
        }
    }
}
