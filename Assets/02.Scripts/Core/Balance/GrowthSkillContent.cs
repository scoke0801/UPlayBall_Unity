using System;
using Baseball.Core.Growth;

namespace Baseball.Core.Balance
{
    /// <summary>
    /// 기본 4×4 성장판과 타자·투수 계통별 뽑기 블록 풀을 한곳에서 제공한다.
    /// </summary>
    public static class GrowthSkillContent
    {
        private static readonly TetrominoShape[] StandardShapes =
        {
            TetrominoShape.I,
            TetrominoShape.O,
            TetrominoShape.T,
            TetrominoShape.S,
            TetrominoShape.Z,
            TetrominoShape.J,
            TetrominoShape.L
        };

        private static readonly SkillBlockRarity[] Rarities =
        {
            SkillBlockRarity.Normal,
            SkillBlockRarity.Rare,
            SkillBlockRarity.Elite,
            SkillBlockRarity.Unique,
            SkillBlockRarity.Legendary
        };

        /// <summary>
        /// 계통·등급이 모양을 제한하지 않도록, 같은 계통·등급 안에 7종 모양을 모두 만든다.
        /// 오프셋 순열은 0~6을 한 번씩 쓰므로 어떤 계통·등급을 뽑아도 표준 테트로미노 전부가
        /// 후보가 된다. 앞 두 값을 0, 3으로 둔 것은 기존 세이브에 남은 변종 0·1번 블록의
        /// 모양을 그대로 유지하기 위해서다.
        /// </summary>
        private static readonly int[] ShapeVariantOffsets = { 0, 3, 1, 4, 2, 5, 6 };

        /// <summary>계통 하나가 어떤 능력치를 올리는지와 블록 ID 접두사를 묶은 정의다.</summary>
        private readonly struct SkillBlockLine
        {
            public SkillBlockLine(string idPrefix, SkillBlockCategory category, PlayerAbility ability)
            {
                IdPrefix = idPrefix;
                Category = category;
                Ability = ability;
            }

            public string IdPrefix { get; }
            public SkillBlockCategory Category { get; }
            public PlayerAbility Ability { get; }
        }

        private static readonly SkillBlockLine[] BlockLines =
        {
            new SkillBlockLine("contact", SkillBlockCategory.Contact, PlayerAbility.Contact),
            new SkillBlockLine("power", SkillBlockCategory.Power, PlayerAbility.Power),
            new SkillBlockLine("baserunning", SkillBlockCategory.Baserunning, PlayerAbility.Speed),
            // BlockId는 기존 세이브의 배치 블록을 찾을 수 있도록 유지한다.
            new SkillBlockLine("bunt", SkillBlockCategory.Arm, PlayerAbility.Arm),
            new SkillBlockLine("defense", SkillBlockCategory.Defense, PlayerAbility.Defense),
            new SkillBlockLine("batter_mental", SkillBlockCategory.BatterMental, PlayerAbility.BatterMental),
            new SkillBlockLine("velocity", SkillBlockCategory.Velocity, PlayerAbility.Velocity),
            new SkillBlockLine("control", SkillBlockCategory.Control, PlayerAbility.Control),
            new SkillBlockLine("breaking", SkillBlockCategory.Breaking, PlayerAbility.Breaking),
            new SkillBlockLine("pitcher_physical", SkillBlockCategory.PitcherPhysical, PlayerAbility.Stamina),
            new SkillBlockLine("stuff", SkillBlockCategory.Stuff, PlayerAbility.Stuff),
            new SkillBlockLine("pitcher_mental", SkillBlockCategory.PitcherMental, PlayerAbility.PitcherMental)
        };

        public static SkillBoardDefinition CreateDefaultBoard()
        {
            return SkillBoardDefinition.CreateDefault();
        }

        public static SkillBlockDefinition[] CreateDefaultBlocks()
        {
            var result = new SkillBlockDefinition[
                BlockLines.Length * Rarities.Length * ShapeVariantOffsets.Length];
            int writeIndex = 0;
            for (int lineIndex = 0; lineIndex < BlockLines.Length; lineIndex++)
            {
                SkillBlockLine line = BlockLines[lineIndex];
                for (int rarityIndex = 0; rarityIndex < Rarities.Length; rarityIndex++)
                {
                    for (int variant = 0; variant < ShapeVariantOffsets.Length; variant++)
                    {
                        result[writeIndex++] = CreateBlock(
                            line.IdPrefix,
                            line.Category,
                            line.Ability,
                            Rarities[rarityIndex],
                            variant);
                    }
                }
            }
            return result;
        }

        private static SkillBlockDefinition CreateBlock(
            string idPrefix,
            SkillBlockCategory category,
            PlayerAbility ability,
            SkillBlockRarity rarity,
            int shapeVariant)
        {
            TetrominoShape shape = GetShape(category, rarity, shapeVariant);
            BoardCell[] shapeCells = TetrominoShapeCatalog.CreateCells(shape);
            int bonus = rarity switch
            {
                SkillBlockRarity.Normal => 1,
                SkillBlockRarity.Rare => 2,
                SkillBlockRarity.Elite => 4,
                SkillBlockRarity.Unique => 5,
                SkillBlockRarity.Legendary => 7,
                _ => throw new ArgumentOutOfRangeException(nameof(rarity))
            };
            long sellValue = rarity switch
            {
                SkillBlockRarity.Normal => MoneyAmount.FromTenThousandWon(120L),
                SkillBlockRarity.Rare => MoneyAmount.FromTenThousandWon(260L),
                SkillBlockRarity.Elite => MoneyAmount.FromTenThousandWon(520L),
                SkillBlockRarity.Unique => MoneyAmount.FromTenThousandWon(900L),
                SkillBlockRarity.Legendary => MoneyAmount.FromTenThousandWon(1_500L),
                _ => throw new ArgumentOutOfRangeException(nameof(rarity))
            };
            string blockId = shapeVariant == 0
                ? idPrefix + "_" + rarity.ToString().ToLowerInvariant()
                : idPrefix + "_" + rarity.ToString().ToLowerInvariant() + "_v" + shapeVariant;
            return new SkillBlockDefinition(
                blockId,
                rarity,
                category,
                shapeCells,
                canRotate: shape != TetrominoShape.O,
                new[] { new AbilityChange(ability, bonus) },
                sellValue);
        }

        /// <summary>
        /// 계통·등급으로 시작점을 흩고 변종 오프셋을 더해 모양을 고른다. 시작점이 달라도
        /// 오프셋이 0~6을 모두 쓰므로 어떤 계통·등급이든 7종 모양이 한 번씩 나온다.
        /// </summary>
        private static TetrominoShape GetShape(
            SkillBlockCategory category,
            SkillBlockRarity rarity,
            int shapeVariant)
        {
            int offset = ShapeVariantOffsets[shapeVariant];
            int shapeIndex = ((int)category * 5 + (int)rarity + offset) % StandardShapes.Length;
            return StandardShapes[shapeIndex];
        }
    }
}
