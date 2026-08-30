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
                SkillGachaPurchaseTier.Normal,
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
            ValidatePurchaseLimit(board, tier, seasonYear, 1);
            economy.Spend(
                seasonYear,
                MoneyTransactionType.SkillBlockPurchase,
                GetSourceId(tier, 1),
                _balance.GetPrice(tier));
            RecordLimitedPurchase(board, tier, seasonYear, 1);
            return PullOne(board, category, tier, random);
        }

        public SkillBlockInstance[] PullBundle(
            CareerEconomyState economy,
            SkillBoardState board,
            SkillBlockCategory category,
            int seasonYear,
            IRandomSource random)
        {
            return PullBundle(
                economy,
                board,
                new[] { category },
                SkillGachaPurchaseTier.Normal,
                seasonYear,
                random);
        }

        /// <summary>
        /// 할인 금액을 한 번 결제하고 선택 등급과 허용 계통 안에서 다섯 결과를 즉시 확정·저장한다.
        /// </summary>
        public SkillBlockInstance[] PullBundle(
            CareerEconomyState economy,
            SkillBoardState board,
            SkillBlockCategory[] categories,
            SkillGachaPurchaseTier tier,
            int seasonYear,
            IRandomSource random)
        {
            ValidateArguments(economy, board, random);
            if (categories == null || categories.Length == 0)
                throw new ArgumentException("뽑기 계통은 하나 이상 필요합니다.", nameof(categories));
            if (!_balance.GetOffer(tier).SupportsFivePull)
                throw new InvalidOperationException("이 등급은 오프시즌 구매 제한 때문에 5회 뽑기를 지원하지 않습니다.");
            ValidatePurchaseLimit(board, tier, seasonYear, 5);
            economy.Spend(
                seasonYear,
                MoneyTransactionType.SkillBlockPurchase,
                GetSourceId(tier, 5),
                _balance.GetFivePullPrice(tier));
            RecordLimitedPurchase(board, tier, seasonYear, 5);
            var results = new SkillBlockInstance[5];
            for (int index = 0; index < results.Length; index++)
            {
                int categoryIndex = categories.Length == 1
                    ? 0
                    : Math.Min((int)(random.NextDouble() * categories.Length), categories.Length - 1);
                results[index] = PullOne(board, categories[categoryIndex], tier, random);
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
            if (board.IsBlockLocked(instanceId))
                throw new InvalidOperationException("잠긴 블록은 판매할 수 없습니다.");
            board.RemoveOwnedBlock(instanceId);
            if (definition.SellValue > 0L)
                economy.Earn(seasonYear, MoneyTransactionType.SkillBlockSale, definition.BlockId, definition.SellValue);
            return definition.SellValue;
        }

        private SkillBlockInstance PullOne(
            SkillBoardState board,
            SkillBlockCategory category,
            SkillGachaPurchaseTier tier,
            IRandomSource random)
        {
            SkillBlockRarity rarity = RollRarity(board, tier, random);
            SkillBlockRarity minimumRarity = _balance.GetOffer(tier).MinimumRarity;
            if (rarity < minimumRarity)
                rarity = minimumRarity;
            SkillBlockDefinition definition = SelectDefinition(category, rarity, random);
            if (rarity >= SkillBlockRarity.Unique && HasExcessUnplacedDuplicate(board, definition))
                definition = SelectDefinition(category, rarity, random, definition.BlockId);
            board.RecordPull(definition.Rarity);
            SkillBlockInstance result = board.AddOwnedBlock(definition.BlockId);
            if (definition.Rarity >= SkillBlockRarity.Unique)
                board.SetBlockLocked(result.InstanceId, true);
            return result;
        }

        private SkillBlockRarity RollRarity(
            SkillBoardState board,
            SkillGachaPurchaseTier tier,
            IRandomSource random)
        {
            if (board.PityLegendaryCount >= _balance.LegendaryPity)
                return SkillBlockRarity.Legendary;
            if (board.PityUniqueCount >= _balance.UniquePity)
                return SkillBlockRarity.Unique;
            if (board.PityEliteCount >= _balance.ElitePity)
                return SkillBlockRarity.Elite;

            double roll = random.NextDouble();
            double normal = _balance.GetProbability(tier, SkillBlockRarity.Normal);
            if (roll < normal) return SkillBlockRarity.Normal;
            roll -= normal;
            double rare = _balance.GetProbability(tier, SkillBlockRarity.Rare);
            if (roll < rare) return SkillBlockRarity.Rare;
            roll -= rare;
            double elite = _balance.GetProbability(tier, SkillBlockRarity.Elite);
            if (roll < elite) return SkillBlockRarity.Elite;
            roll -= elite;
            return roll < _balance.GetProbability(tier, SkillBlockRarity.Unique)
                ? SkillBlockRarity.Unique
                : SkillBlockRarity.Legendary;
        }

        private SkillBlockDefinition SelectDefinition(
            SkillBlockCategory category,
            SkillBlockRarity rarity,
            IRandomSource random,
            string excludedBlockId = "")
        {
            int count = 0;
            for (int index = 0; index < _definitions.Length; index++)
            {
                if (_definitions[index].Category == category &&
                    _definitions[index].Rarity == rarity &&
                    !string.Equals(_definitions[index].BlockId, excludedBlockId, StringComparison.Ordinal))
                    count++;
            }
            if (count == 0 && !string.IsNullOrEmpty(excludedBlockId))
                return SelectDefinition(category, rarity, random);
            if (count == 0)
                throw new InvalidOperationException($"{category} 계통의 {rarity} 블록 풀이 비어 있습니다.");

            int selected = Math.Min((int)(random.NextDouble() * count), count - 1);
            for (int index = 0; index < _definitions.Length; index++)
            {
                if (_definitions[index].Category != category ||
                    _definitions[index].Rarity != rarity ||
                    string.Equals(_definitions[index].BlockId, excludedBlockId, StringComparison.Ordinal))
                    continue;
                if (selected-- == 0)
                    return _definitions[index];
            }
            throw new InvalidOperationException("스킬 블록 선택에 실패했습니다.");
        }

        private bool HasExcessUnplacedDuplicate(
            SkillBoardState board,
            SkillBlockDefinition candidate)
        {
            int duplicateCount = 0;
            for (int index = 0; index < board.OwnedBlocks.Count; index++)
            {
                SkillBlockDefinition owned = FindDefinition(board.OwnedBlocks[index].DefinitionId);
                if (owned.Rarity == candidate.Rarity && owned.Category == candidate.Category)
                {
                    duplicateCount++;
                    if (duplicateCount >= 2)
                        return true;
                }
            }
            return false;
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
            int categoryCount = Enum.GetValues(typeof(SkillBlockCategory)).Length;
            for (int category = 0; category < categoryCount; category++)
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

                for (int rarity = 0; rarity <= (int)SkillBlockRarity.Legendary; rarity++)
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

        private void ValidatePurchaseLimit(
            SkillBoardState board,
            SkillGachaPurchaseTier tier,
            int seasonYear,
            int count)
        {
            SkillGachaOfferBalance offer = _balance.GetOffer(tier);
            if (offer.MaxPurchasesPerOffseason == 0)
                return;
            int used = board.GetLimitedPurchaseCount(tier, seasonYear);
            if (used + count > offer.MaxPurchasesPerOffseason)
                throw new InvalidOperationException("이 등급의 오프시즌 구매 가능 횟수를 모두 사용했습니다.");
        }

        private void RecordLimitedPurchase(
            SkillBoardState board,
            SkillGachaPurchaseTier tier,
            int seasonYear,
            int count)
        {
            if (_balance.GetOffer(tier).MaxPurchasesPerOffseason > 0)
                board.RecordTierPurchases(tier, seasonYear, count);
        }

        private static string GetSourceId(SkillGachaPurchaseTier tier, int count)
        {
            if (count == 1)
            {
                return tier switch
                {
                    SkillGachaPurchaseTier.Normal => "skill_gacha_normal_1",
                    SkillGachaPurchaseTier.Rare => "skill_gacha_rare_1",
                    SkillGachaPurchaseTier.Elite => "skill_gacha_elite_1",
                    SkillGachaPurchaseTier.Unique => "skill_gacha_unique_1",
                    SkillGachaPurchaseTier.Legendary => "skill_gacha_legendary_1",
                    _ => throw new ArgumentOutOfRangeException(nameof(tier))
                };
            }
            return tier switch
            {
                SkillGachaPurchaseTier.Normal => "skill_gacha_normal_5",
                SkillGachaPurchaseTier.Rare => "skill_gacha_rare_5",
                SkillGachaPurchaseTier.Elite => "skill_gacha_elite_5",
                SkillGachaPurchaseTier.Unique => "skill_gacha_unique_5",
                SkillGachaPurchaseTier.Legendary => "skill_gacha_legendary_5",
                _ => throw new ArgumentOutOfRangeException(nameof(tier))
            };
        }
    }
}
