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

        public static SkillBoardDefinition CreateDefaultBoard()
        {
            return SkillBoardDefinition.CreateDefault();
        }

        public static SkillBlockDefinition[] CreateDefaultBlocks()
        {
            return new[]
            {
                CreateBlock("contact", SkillBlockCategory.Contact, PlayerAbility.Contact, SkillBlockRarity.Normal),
                CreateBlock("contact", SkillBlockCategory.Contact, PlayerAbility.Contact, SkillBlockRarity.Rare),
                CreateBlock("contact", SkillBlockCategory.Contact, PlayerAbility.Contact, SkillBlockRarity.Elite),
                CreateBlock("contact", SkillBlockCategory.Contact, PlayerAbility.Contact, SkillBlockRarity.Unique),
                CreateBlock("contact", SkillBlockCategory.Contact, PlayerAbility.Contact, SkillBlockRarity.Legendary),
                CreateBlock("power", SkillBlockCategory.Power, PlayerAbility.Power, SkillBlockRarity.Normal),
                CreateBlock("power", SkillBlockCategory.Power, PlayerAbility.Power, SkillBlockRarity.Rare),
                CreateBlock("power", SkillBlockCategory.Power, PlayerAbility.Power, SkillBlockRarity.Elite),
                CreateBlock("power", SkillBlockCategory.Power, PlayerAbility.Power, SkillBlockRarity.Unique),
                CreateBlock("power", SkillBlockCategory.Power, PlayerAbility.Power, SkillBlockRarity.Legendary),
                CreateBlock("defense", SkillBlockCategory.Defense, PlayerAbility.Defense, SkillBlockRarity.Normal),
                CreateBlock("defense", SkillBlockCategory.Defense, PlayerAbility.Defense, SkillBlockRarity.Rare),
                CreateBlock("defense", SkillBlockCategory.Defense, PlayerAbility.Defense, SkillBlockRarity.Elite),
                CreateBlock("defense", SkillBlockCategory.Defense, PlayerAbility.Defense, SkillBlockRarity.Unique),
                CreateBlock("defense", SkillBlockCategory.Defense, PlayerAbility.Defense, SkillBlockRarity.Legendary),
                CreateBlock("batter_mental", SkillBlockCategory.BatterMental, PlayerAbility.BatterMental, SkillBlockRarity.Normal),
                CreateBlock("batter_mental", SkillBlockCategory.BatterMental, PlayerAbility.BatterMental, SkillBlockRarity.Rare),
                CreateBlock("batter_mental", SkillBlockCategory.BatterMental, PlayerAbility.BatterMental, SkillBlockRarity.Elite),
                CreateBlock("batter_mental", SkillBlockCategory.BatterMental, PlayerAbility.BatterMental, SkillBlockRarity.Unique),
                CreateBlock("batter_mental", SkillBlockCategory.BatterMental, PlayerAbility.BatterMental, SkillBlockRarity.Legendary),
                CreateBlock("velocity", SkillBlockCategory.Velocity, PlayerAbility.Velocity, SkillBlockRarity.Normal),
                CreateBlock("velocity", SkillBlockCategory.Velocity, PlayerAbility.Velocity, SkillBlockRarity.Rare),
                CreateBlock("velocity", SkillBlockCategory.Velocity, PlayerAbility.Velocity, SkillBlockRarity.Elite),
                CreateBlock("velocity", SkillBlockCategory.Velocity, PlayerAbility.Velocity, SkillBlockRarity.Unique),
                CreateBlock("velocity", SkillBlockCategory.Velocity, PlayerAbility.Velocity, SkillBlockRarity.Legendary),
                CreateBlock("control", SkillBlockCategory.Control, PlayerAbility.Control, SkillBlockRarity.Normal),
                CreateBlock("control", SkillBlockCategory.Control, PlayerAbility.Control, SkillBlockRarity.Rare),
                CreateBlock("control", SkillBlockCategory.Control, PlayerAbility.Control, SkillBlockRarity.Elite),
                CreateBlock("control", SkillBlockCategory.Control, PlayerAbility.Control, SkillBlockRarity.Unique),
                CreateBlock("control", SkillBlockCategory.Control, PlayerAbility.Control, SkillBlockRarity.Legendary),
                CreateBlock("breaking", SkillBlockCategory.Breaking, PlayerAbility.Breaking, SkillBlockRarity.Normal),
                CreateBlock("breaking", SkillBlockCategory.Breaking, PlayerAbility.Breaking, SkillBlockRarity.Rare),
                CreateBlock("breaking", SkillBlockCategory.Breaking, PlayerAbility.Breaking, SkillBlockRarity.Elite),
                CreateBlock("breaking", SkillBlockCategory.Breaking, PlayerAbility.Breaking, SkillBlockRarity.Unique),
                CreateBlock("breaking", SkillBlockCategory.Breaking, PlayerAbility.Breaking, SkillBlockRarity.Legendary),
                CreateBlock("pitcher_mental", SkillBlockCategory.PitcherMental, PlayerAbility.PitcherMental, SkillBlockRarity.Normal),
                CreateBlock("pitcher_mental", SkillBlockCategory.PitcherMental, PlayerAbility.PitcherMental, SkillBlockRarity.Rare),
                CreateBlock("pitcher_mental", SkillBlockCategory.PitcherMental, PlayerAbility.PitcherMental, SkillBlockRarity.Elite),
                CreateBlock("pitcher_mental", SkillBlockCategory.PitcherMental, PlayerAbility.PitcherMental, SkillBlockRarity.Unique),
                CreateBlock("pitcher_mental", SkillBlockCategory.PitcherMental, PlayerAbility.PitcherMental, SkillBlockRarity.Legendary)
            };
        }

        private static SkillBlockDefinition CreateBlock(
            string idPrefix,
            SkillBlockCategory category,
            PlayerAbility ability,
            SkillBlockRarity rarity)
        {
            TetrominoShape shape = GetShape(category, rarity);
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
            return new SkillBlockDefinition(
                idPrefix + "_" + rarity.ToString().ToLowerInvariant(),
                rarity,
                category,
                shapeCells,
                canRotate: shape != TetrominoShape.O,
                new[] { new AbilityChange(ability, bonus) },
                sellValue);
        }

        private static TetrominoShape GetShape(
            SkillBlockCategory category,
            SkillBlockRarity rarity)
        {
            int shapeIndex = ((int)category * 5 + (int)rarity) % StandardShapes.Length;
            return StandardShapes[shapeIndex];
        }
    }
}
