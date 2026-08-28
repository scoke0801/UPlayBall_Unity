using System;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Simulation.Random;

namespace Baseball.Simulation.Growth
{
    /// <summary>
    /// 공개 확률·보장 카운트·Money 비용으로 빈손 없는 스킬 블록 결과를 만든다.
    /// </summary>
    public sealed class SkillGachaService
    {
        private readonly SkillGachaBalanceTable _balance;
        private readonly SkillBlockDefinition[] _definitions;

        public SkillGachaService(SkillGachaBalanceTable balance, SkillBlockDefinition[] definitions)
        {
            _balance = balance;
            _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            if (_definitions.Length == 0)
                throw new ArgumentException("스킬 블록 뽑기 풀이 비어 있습니다.", nameof(definitions));
            ValidateDefinitionPools();
        }

        public SkillBlockInstance PullSingle(
            CareerEconomyState economy,
            SkillBoardState board,
            SkillBlockCategory category,
            int seasonYear,
            IRandomSource random)
        {
            return PullSingle(
                economy,
                board,
                category,
                SkillGachaPurchaseTier.Standard,
                seasonYear,
                random);
        }

        public SkillBlockInstance PullSingle(
            CareerEconomyState economy,
            SkillBoardState board,
            SkillBlockCategory category,
            SkillGachaPurchaseTier tier,
            int seasonYear,
            IRandomSource random)
        {
            ValidateArguments(economy, board, random);
            string sourceId = tier == SkillGachaPurchaseTier.Premium
                ? "skill_gacha_premium"
                : "skill_gacha_single";
            economy.Spend(
                seasonYear,
                MoneyTransactionType.SkillBlockPurchase,
                sourceId,
                _balance.GetPrice(tier));
            return PullOne(board, category, tier, random, null);
        }

        public SkillBlockInstance[] PullBundle(
            CareerEconomyState economy,
            SkillBoardState board,
            SkillBlockCategory category,
            int seasonYear,
            IRandomSource random)
        {
            ValidateArguments(economy, board, random);
            economy.Spend(seasonYear, MoneyTransactionType.SkillBlockPurchase, "skill_gacha_bundle", _balance.BundlePrice);
            var results = new SkillBlockInstance[5];
            bool hasUncommonOrBetter = false;
            for (int index = 0; index < results.Length; index++)
            {
                SkillBlockRarity? minimum = index == results.Length - 1 && !hasUncommonOrBetter
                    ? SkillBlockRarity.Uncommon
                    : (SkillBlockRarity?)null;
                results[index] = PullOne(
                    board,
                    category,
                    SkillGachaPurchaseTier.Standard,
                    random,
                    minimum);
                SkillBlockDefinition definition = FindDefinition(results[index].DefinitionId);
                if (definition.Rarity >= SkillBlockRarity.Uncommon)
                    hasUncommonOrBetter = true;
            }
            return results;
        }

        public long SellOwnedBlock(
            CareerEconomyState economy,
            SkillBoardState board,
            int instanceId,
            int seasonYear)
        {
            SkillBlockInstance instance = board.FindOwnedBlock(instanceId);
            if (instance.InstanceId == 0)
                throw new ArgumentException("보유 블록을 찾을 수 없습니다.", nameof(instanceId));
            SkillBlockDefinition definition = FindDefinition(instance.DefinitionId);
            if (definition.IsUniqueReward)
                throw new InvalidOperationException("고유 보상 블록은 판매할 수 없습니다.");
            board.RemoveOwnedBlock(instanceId);
            if (definition.SellValue > 0L)
                economy.Earn(seasonYear, MoneyTransactionType.SkillBlockSale, definition.BlockId, definition.SellValue);
            return definition.SellValue;
        }

        private SkillBlockInstance PullOne(
            SkillBoardState board,
            SkillBlockCategory category,
            SkillGachaPurchaseTier tier,
            IRandomSource random,
            SkillBlockRarity? minimumRarity)
        {
            SkillBlockRarity rarity = RollRarity(board, tier, random);
            if (minimumRarity.HasValue && rarity < minimumRarity.Value)
                rarity = minimumRarity.Value;
            SkillBlockDefinition definition = SelectDefinition(category, rarity, random);
            board.RecordPull(definition.Rarity);
            return board.AddOwnedBlock(definition.BlockId);
        }

        private SkillBlockRarity RollRarity(
            SkillBoardState board,
            SkillGachaPurchaseTier tier,
            IRandomSource random)
        {
            if (board.PityEpicCount >= _balance.EpicPity)
                return SkillBlockRarity.Epic;
            if (board.PityRareCount >= _balance.RarePity)
                return SkillBlockRarity.Rare;

            double roll = random.NextDouble();
            double common = _balance.GetProbability(tier, SkillBlockRarity.Common);
            if (roll < common) return SkillBlockRarity.Common;
            roll -= common;
            double uncommon = _balance.GetProbability(tier, SkillBlockRarity.Uncommon);
            if (roll < uncommon) return SkillBlockRarity.Uncommon;
            roll -= uncommon;
            return roll < _balance.GetProbability(tier, SkillBlockRarity.Rare)
                ? SkillBlockRarity.Rare
                : SkillBlockRarity.Epic;
        }

        private SkillBlockDefinition SelectDefinition(
            SkillBlockCategory category,
            SkillBlockRarity rarity,
            IRandomSource random)
        {
            int count = 0;
            for (int index = 0; index < _definitions.Length; index++)
            {
                if (_definitions[index].Category == category && _definitions[index].Rarity == rarity)
                    count++;
            }
            if (count == 0)
                throw new InvalidOperationException($"{category} 계통의 {rarity} 블록 풀이 비어 있습니다.");

            int selected = Math.Min((int)(random.NextDouble() * count), count - 1);
            for (int index = 0; index < _definitions.Length; index++)
            {
                if (_definitions[index].Category != category || _definitions[index].Rarity != rarity)
                    continue;
                if (selected-- == 0)
                    return _definitions[index];
            }
            throw new InvalidOperationException("스킬 블록 선택에 실패했습니다.");
        }

        private SkillBlockDefinition FindDefinition(string definitionId)
        {
            for (int index = 0; index < _definitions.Length; index++)
            {
                if (string.Equals(_definitions[index].BlockId, definitionId, StringComparison.Ordinal))
                    return _definitions[index];
            }
            throw new InvalidOperationException("스킬 블록 정의를 찾을 수 없습니다.");
        }

        private void ValidateDefinitionPools()
        {
            for (int category = 0; category <= (int)SkillBlockCategory.PitcherMental; category++)
            {
                bool categoryExists = false;
                for (int index = 0; index < _definitions.Length; index++)
                {
                    if ((int)_definitions[index].Category == category)
                    {
                        categoryExists = true;
                        break;
                    }
                }
                if (!categoryExists)
                    continue;

                for (int rarity = 0; rarity <= (int)SkillBlockRarity.Epic; rarity++)
                {
                    bool found = false;
                    for (int index = 0; index < _definitions.Length; index++)
                    {
                        if ((int)_definitions[index].Category == category &&
                            (int)_definitions[index].Rarity == rarity)
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                        throw new ArgumentException("사용하는 각 계통에는 모든 등급의 뽑기 풀이 필요합니다.", nameof(_definitions));
                }
            }
        }

        private static void ValidateArguments(CareerEconomyState economy, SkillBoardState board, IRandomSource random)
        {
            if (economy == null) throw new ArgumentNullException(nameof(economy));
            if (board == null) throw new ArgumentNullException(nameof(board));
            if (random == null) throw new ArgumentNullException(nameof(random));
        }
    }
}
