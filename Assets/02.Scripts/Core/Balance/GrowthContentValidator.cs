using System;
using System.Collections.Generic;
using Baseball.Core.Growth;

namespace Baseball.Core.Balance
{
    public enum ContentValidationSeverity
    {
        Warning,
        Error
    }

    public readonly struct ContentValidationIssue
    {
        public ContentValidationIssue(
            ContentValidationSeverity severity,
            string code,
            string contentId,
            string message)
        {
            Severity = severity;
            Code = code ?? string.Empty;
            ContentId = contentId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public ContentValidationSeverity Severity { get; }
        public string Code { get; }
        public string ContentId { get; }
        public string Message { get; }
    }

    /// <summary>성장 프로그램·스킬 블록·뽑기 풀의 배포 전 정합성을 순수 C#에서 검사한다.</summary>
    public sealed class GrowthContentValidator
    {
        public ContentValidationIssue[] Validate(GrowthBalanceTable balance)
        {
            if (balance == null) throw new ArgumentNullException(nameof(balance));
            var issues = new List<ContentValidationIssue>();
            ValidatePrograms(balance, issues);
            ValidateBlocks(balance, issues);
            ValidateGachaPools(balance, issues);
            return issues.ToArray();
        }

        private static void ValidatePrograms(
            GrowthBalanceTable balance,
            List<ContentValidationIssue> issues)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            bool hasFreeProgression = false;
            for (int index = 0; index < balance.Programs.Length; index++)
            {
                TrainingProgramDefinition program = balance.Programs[index];
                if (!ids.Add(program.ProgramId))
                    AddError(issues, "DUPLICATE_PROGRAM_ID", program.ProgramId, "ProgramId가 중복되었습니다.");
                if (program.DurationWeeks <= 0 || program.DurationWeeks > balance.OffseasonWeeks)
                {
                    AddError(
                        issues,
                        "INVALID_PROGRAM_DURATION",
                        program.ProgramId,
                        $"기간 {program.DurationWeeks}주는 오프시즌 {balance.OffseasonWeeks}주 안에 있어야 합니다.");
                }
                if (program.MoneyCost == 0L &&
                    program.ActivityType is OffseasonActivityType.Rest or OffseasonActivityType.PersonalTraining)
                {
                    hasFreeProgression = true;
                }
                if (program.CanRaisePotential && program.PotentialBreakthroughChanceMultiplier <= 0d)
                {
                    AddError(
                        issues,
                        "UNREACHABLE_POTENTIAL_BREAKTHROUGH",
                        program.ProgramId,
                        "Potential 프로그램의 돌파 배율이 0입니다.");
                }
            }
            if (!hasFreeProgression)
            {
                AddError(
                    issues,
                    "NO_FREE_OFFSEASON_OPTION",
                    "growth_programs",
                    "자금이 없어도 진행할 수 있는 휴식 또는 무료 훈련이 필요합니다.");
            }

            for (int tier = (int)TrainingAccessTier.Foundation;
                 tier <= (int)TrainingAccessTier.Legacy;
                 tier++)
            {
                bool hasBatter = false;
                bool hasPitcher = false;
                for (int index = 0; index < balance.Programs.Length; index++)
                {
                    TrainingProgramDefinition program = balance.Programs[index];
                    if ((int)program.MinimumAccessTier != tier || program.ProgramPower <= 0d)
                        continue;
                    hasBatter |= program.CanUse(Baseball.Core.Players.PlayerType.Batter);
                    hasPitcher |= program.CanUse(Baseball.Core.Players.PlayerType.Pitcher);
                }
                if (!hasBatter || !hasPitcher)
                {
                    AddError(
                        issues,
                        "EMPTY_TRAINING_TIER",
                        ((TrainingAccessTier)tier).ToString(),
                        "모든 성장 티어에는 타자와 투수의 성장 선택이 각각 필요합니다.");
                }
            }
        }

        private static void ValidateBlocks(
            GrowthBalanceTable balance,
            List<ContentValidationIssue> issues)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < balance.SkillBlocks.Length; index++)
            {
                SkillBlockDefinition block = balance.SkillBlocks[index];
                if (!ids.Add(block.BlockId))
                    AddError(issues, "DUPLICATE_BLOCK_ID", block.BlockId, "BlockId가 중복되었습니다.");
                int expectedBonus = GetExpectedBonus(block.Rarity);
                for (int bonusIndex = 0; bonusIndex < block.AbilityBonuses.Length; bonusIndex++)
                {
                    if (block.AbilityBonuses[bonusIndex].Amount > expectedBonus)
                    {
                        AddError(
                            issues,
                            "BLOCK_BONUS_EXCEEDS_RARITY",
                            block.BlockId,
                            $"{block.Rarity} 블록의 능력치 보너스는 +{expectedBonus}를 넘을 수 없습니다.");
                    }
                }
                if (block.Rarity >= SkillBlockRarity.Unique && string.IsNullOrEmpty(block.TraitId))
                {
                    AddError(
                        issues,
                        "HIGH_RARITY_TRAIT_MISSING",
                        block.BlockId,
                        "Unique 이상 블록은 수치 외 Trait을 가져야 합니다.");
                }
                long purchasePrice = balance.SkillGacha.GetPrice((SkillGachaPurchaseTier)block.Rarity);
                if (block.SellValue >= purchasePrice)
                {
                    AddError(
                        issues,
                        "SELL_PRICE_ARBITRAGE",
                        block.BlockId,
                        "판매가가 대응 등급 1회 구매가보다 낮아야 합니다.");
                }
            }
        }

        private static void ValidateGachaPools(
            GrowthBalanceTable balance,
            List<ContentValidationIssue> issues)
        {
            int categoryCount = Enum.GetValues(typeof(SkillBlockCategory)).Length;
            int rarityCount = Enum.GetValues(typeof(SkillBlockRarity)).Length;
            var poolCounts = new int[categoryCount, rarityCount];
            for (int index = 0; index < balance.SkillBlocks.Length; index++)
            {
                SkillBlockDefinition block = balance.SkillBlocks[index];
                poolCounts[(int)block.Category, (int)block.Rarity]++;
            }
            for (int category = 0; category < categoryCount; category++)
            {
                for (int rarity = 0; rarity < rarityCount; rarity++)
                {
                    if (poolCounts[category, rarity] > 0)
                        continue;
                    AddError(
                        issues,
                        "EMPTY_GACHA_POOL",
                        $"{(SkillBlockCategory)category}/{(SkillBlockRarity)rarity}",
                        "선택 가능한 계통·등급 조합의 뽑기 풀이 비었습니다.");
                }
            }
        }

        private static int GetExpectedBonus(SkillBlockRarity rarity)
        {
            return rarity switch
            {
                SkillBlockRarity.Normal => 1,
                SkillBlockRarity.Rare => 2,
                SkillBlockRarity.Elite => 3,
                SkillBlockRarity.Unique => 4,
                SkillBlockRarity.Legendary => 5,
                _ => 0
            };
        }

        private static void AddError(
            List<ContentValidationIssue> issues,
            string code,
            string contentId,
            string message)
        {
            issues.Add(new ContentValidationIssue(
                ContentValidationSeverity.Error,
                code,
                contentId,
                message));
        }
    }
}
