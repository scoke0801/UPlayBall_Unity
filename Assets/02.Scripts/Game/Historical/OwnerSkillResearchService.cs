using System;
using System.Collections.Generic;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Simulation.Random;

namespace Baseball.Game.Historical
{
    /// <summary>스킬 배치·합성의 변경 범위만 보관하고 동기 저장 실패 시 원상 복구한다.</summary>
    public static class OwnerSkillTransaction
    {
        /// <summary>인벤토리·성장판·합성 비용만 바꾸는 명령을 실행한다. 저장 중 외부에 상태를 알리지 않는다.</summary>
        public static bool Execute(ManagerHistoricalRuntimeState runtime, Func<bool> change, Action save)
        {
            if (runtime == null || change == null || save == null) throw new ArgumentNullException();
            var inventory = runtime.PlayerGrowth.Inventory.Copy();
            long money = runtime.Economy.Money;
            var boards = new PlacedSkillBlock[runtime.OwnedCards.Count][];
            for (int i = 0; i < boards.Length; i++)
            {
                var placements = runtime.OwnedCards[i].SkillBoard.Placements;
                boards[i] = new PlacedSkillBlock[placements.Count];
                for (int j = 0; j < placements.Count; j++) boards[i][j] = placements[j];
            }
            bool committed = false;
            try
            {
                if (!change()) return false;
                save();
                committed = true;
                return true;
            }
            finally
            {
                if (!committed)
                {
                    runtime.PlayerGrowth.Inventory.RestoreFrom(inventory);
                    // 대상 명령은 자금을 소모하기만 하므로 차액 환급으로 정확히 복원한다.
                    runtime.Economy.AddMoney(money - runtime.Economy.Money);
                    for (int i = 0; i < boards.Length; i++)
                    {
                        var board = runtime.OwnedCards[i].SkillBoard;
                        while (board.Placements.Count > 0)
                            board.Remove(board.Placements[board.Placements.Count - 1].Instance.InstanceId);
                        foreach (var placement in boards[i]) board.Add(placement);
                    }
                }
            }
        }
    }

    /// <summary>연구·선택 상자·랜덤 합성·포인트 제작을 구단 공유 인벤토리에 반영한다.</summary>
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
            bool pity = highest < SkillBlockGradeCatalog.Count - 1 && failures >= balance.pityFailures;
            var probabilities = new double[SkillBlockGradeCatalog.Count];
            double sum = 0;
            for (int grade = Math.Max(0, highest - 1); grade < SkillBlockGradeCatalog.Count; grade++)
            {
                if (pity && grade <= highest) continue;
                probabilities[grade] = (balance.gradeWeights[(int)first.Rarity * SkillBlockGradeCatalog.Count + grade] + balance.gradeWeights[(int)second.Rarity * SkillBlockGradeCatalog.Count + grade]) / 2;
                sum += probabilities[grade];
            }
            if (sum <= 0) throw new InvalidOperationException("합성 결과 확률이 비어 있습니다.");
            for (int grade = 0; grade < SkillBlockGradeCatalog.Count; grade++) probabilities[grade] /= sum;
            if (focus != SkillFusionFocus.Grade || highest == SkillBlockGradeCatalog.Count - 1 || pity) return probabilities;
            double upgrade = 0;
            for (int grade = highest + 1; grade < SkillBlockGradeCatalog.Count; grade++) upgrade += probabilities[grade];
            if (upgrade <= 0 || upgrade >= 1) return probabilities;
            double bonus = Math.Min(balance.gradeBonus, 1 - upgrade);
            // 승급 결과끼리의 비율을 보존하며 동급·하위 확률에서 보정분을 이동한다.
            for (int grade = 0; grade < SkillBlockGradeCatalog.Count; grade++)
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
            int rarity = SkillBlockGradeCatalog.Count - 1;
            for (int i = 0; i < SkillBlockGradeCatalog.Count; i++) { cumulative += probabilities[i]; if (roll < cumulative) { rarity = i; break; } }
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

        /// <summary>재료 쌍을 사전 검증하고 순서대로 합성한다. 저장용 후보 상태에서 호출한다.</summary>
        public static SkillBlockDefinition[] FuseBatch(ManagerHistoricalRuntimeState runtime,
            SkillBlockDefinition[] definitions, int[] materialIds, OwnerSkillFusionBalance balance,
            SkillFusionFocus focus, Func<int, IRandomSource> createRandom)
        {
            if (materialIds == null || materialIds.Length == 0 || materialIds.Length % 2 != 0)
                throw new InvalidOperationException("각 합성 슬롯에 재료 2개를 선택하세요.");
            if (createRandom == null) throw new ArgumentNullException(nameof(createRandom));
            balance.Validate();
            OwnerScheduleGateService.Evaluate(runtime, OwnerGrowthAction.SkillBlock).RequireAllowed();
            var available = GetAvailableMaterials(runtime, definitions);
            var selected = new HashSet<int>();
            foreach (int id in materialIds)
                if (!selected.Add(id) || !available.Contains(id))
                    throw new InvalidOperationException("사용할 수 없는 재료가 있습니다. 선택을 다시 확인하세요.");
            int count = materialIds.Length / 2;
            if (runtime.Economy.Money < checked(balance.cost * count))
                throw new InvalidOperationException("전체 합성에 필요한 자금이 부족합니다.");
            for (int i = 0; i < materialIds.Length; i += 2)
            {
                var first = runtime.PlayerGrowth.Inventory.GetRequired(materialIds[i]);
                var second = runtime.PlayerGrowth.Inventory.GetRequired(materialIds[i + 1]);
                if (!IsSamePlayerType(FindDefinition(definitions, first.DefinitionId), FindDefinition(definitions, second.DefinitionId)))
                    throw new InvalidOperationException("야수 또는 투수 블록끼리 합성하세요.");
            }
            var results = new SkillBlockDefinition[count];
            // 개별 실행과 같은 합성 횟수 기반 난수와 승급 보장 순서를 유지한다.
            for (int i = 0; i < count; i++)
                results[i] = Fuse(runtime, definitions, materialIds[i * 2], materialIds[i * 2 + 1],
                    balance, focus, createRandom(runtime.PlayerGrowth.Inventory.FusionCount));
            return results;
        }

        /// <summary>누적 포인트로 S 등급 블록을 지정 제작한다. 랜덤 합성과 별도 보상이다.</summary>
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

        /// <summary>셀 배열의 순서와 무관하게 저작된 모양이 같은지 판단한다.</summary>
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
