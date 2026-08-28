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
                CreateBlock("contact", SkillBlockCategory.Contact, PlayerAbility.Contact, SkillBlockRarity.Common),
                CreateBlock("contact", SkillBlockCategory.Contact, PlayerAbility.Contact, SkillBlockRarity.Uncommon),
                CreateBlock("contact", SkillBlockCategory.Contact, PlayerAbility.Contact, SkillBlockRarity.Rare),
                CreateBlock("contact", SkillBlockCategory.Contact, PlayerAbility.Contact, SkillBlockRarity.Epic),
                CreateBlock("power", SkillBlockCategory.Power, PlayerAbility.Power, SkillBlockRarity.Common),
                CreateBlock("power", SkillBlockCategory.Power, PlayerAbility.Power, SkillBlockRarity.Uncommon),
                CreateBlock("power", SkillBlockCategory.Power, PlayerAbility.Power, SkillBlockRarity.Rare),
                CreateBlock("power", SkillBlockCategory.Power, PlayerAbility.Power, SkillBlockRarity.Epic),
                CreateBlock("defense", SkillBlockCategory.Defense, PlayerAbility.Defense, SkillBlockRarity.Common),
                CreateBlock("defense", SkillBlockCategory.Defense, PlayerAbility.Defense, SkillBlockRarity.Uncommon),
                CreateBlock("defense", SkillBlockCategory.Defense, PlayerAbility.Defense, SkillBlockRarity.Rare),
                CreateBlock("defense", SkillBlockCategory.Defense, PlayerAbility.Defense, SkillBlockRarity.Epic),
                CreateBlock("batter_mental", SkillBlockCategory.BatterMental, PlayerAbility.BatterMental, SkillBlockRarity.Common),
                CreateBlock("batter_mental", SkillBlockCategory.BatterMental, PlayerAbility.BatterMental, SkillBlockRarity.Uncommon),
                CreateBlock("batter_mental", SkillBlockCategory.BatterMental, PlayerAbility.BatterMental, SkillBlockRarity.Rare),
                CreateBlock("batter_mental", SkillBlockCategory.BatterMental, PlayerAbility.BatterMental, SkillBlockRarity.Epic),
                CreateBlock("velocity", SkillBlockCategory.Velocity, PlayerAbility.Velocity, SkillBlockRarity.Common),
                CreateBlock("velocity", SkillBlockCategory.Velocity, PlayerAbility.Velocity, SkillBlockRarity.Uncommon),
                CreateBlock("velocity", SkillBlockCategory.Velocity, PlayerAbility.Velocity, SkillBlockRarity.Rare),
                CreateBlock("velocity", SkillBlockCategory.Velocity, PlayerAbility.Velocity, SkillBlockRarity.Epic),
                CreateBlock("control", SkillBlockCategory.Control, PlayerAbility.Control, SkillBlockRarity.Common),
                CreateBlock("control", SkillBlockCategory.Control, PlayerAbility.Control, SkillBlockRarity.Uncommon),
                CreateBlock("control", SkillBlockCategory.Control, PlayerAbility.Control, SkillBlockRarity.Rare),
                CreateBlock("control", SkillBlockCategory.Control, PlayerAbility.Control, SkillBlockRarity.Epic),
                CreateBlock("breaking", SkillBlockCategory.Breaking, PlayerAbility.Breaking, SkillBlockRarity.Common),
                CreateBlock("breaking", SkillBlockCategory.Breaking, PlayerAbility.Breaking, SkillBlockRarity.Uncommon),
                CreateBlock("breaking", SkillBlockCategory.Breaking, PlayerAbility.Breaking, SkillBlockRarity.Rare),
                CreateBlock("breaking", SkillBlockCategory.Breaking, PlayerAbility.Breaking, SkillBlockRarity.Epic),
                CreateBlock("pitcher_mental", SkillBlockCategory.PitcherMental, PlayerAbility.PitcherMental, SkillBlockRarity.Common),
                CreateBlock("pitcher_mental", SkillBlockCategory.PitcherMental, PlayerAbility.PitcherMental, SkillBlockRarity.Uncommon),
                CreateBlock("pitcher_mental", SkillBlockCategory.PitcherMental, PlayerAbility.PitcherMental, SkillBlockRarity.Rare),
                CreateBlock("pitcher_mental", SkillBlockCategory.PitcherMental, PlayerAbility.PitcherMental, SkillBlockRarity.Epic)
            };
        }

        private static SkillBlockDefinition CreateBlock(
            string idPrefix,
            SkillBlockCategory category,
            PlayerAbility ability,
            SkillBlockRarity rarity)
        {
            TetrominoShape shape = GetShape(category, rarity);
            int bonus = (int)rarity + 1;
            long sellValue = rarity switch
            {
                SkillBlockRarity.Common => 120L,
                SkillBlockRarity.Uncommon => 260L,
                SkillBlockRarity.Rare => 520L,
                SkillBlockRarity.Epic => 900L,
                _ => throw new ArgumentOutOfRangeException(nameof(rarity))
            };
            return new SkillBlockDefinition(
                idPrefix + "_" + rarity.ToString().ToLowerInvariant(),
                rarity,
                category,
                TetrominoShapeCatalog.CreateCells(shape),
                canRotate: shape != TetrominoShape.O,
                new[] { new AbilityChange(ability, bonus) },
                sellValue);
        }

        private static TetrominoShape GetShape(
            SkillBlockCategory category,
            SkillBlockRarity rarity)
        {
            int shapeIndex = ((int)category * 4 + (int)rarity) % StandardShapes.Length;
            return StandardShapes[shapeIndex];
        }
    }
}
