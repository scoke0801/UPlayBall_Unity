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

        /// <summary>
        /// 같은 카테고리·등급이라도 모양이 하나로 고정되지 않도록, 등급별로 서로 다른
        /// 모양을 배정할 변종 오프셋이다. 7종 모양에 대해 나머지가 겹치지 않도록
        /// 3칸씩 어긋나게 잡는다.
        /// </summary>
        private static readonly int[] ShapeVariantOffsets = { 0, 3 };

        public static SkillBoardDefinition CreateDefaultBoard()
        {
            return SkillBoardDefinition.CreateDefault();
        }

        public static SkillBlockDefinition[] CreateDefaultBlocks()
        {
            return new[]
            {
                CreateBlock("contact", SkillBlockCategory.Contact, PlayerAbility.Contact, SkillBlockRarity.Normal, 0),
                CreateBlock("contact", SkillBlockCategory.Contact, PlayerAbility.Contact, SkillBlockRarity.Normal, 1),
                CreateBlock("contact", SkillBlockCategory.Contact, PlayerAbility.Contact, SkillBlockRarity.Rare, 0),
                CreateBlock("contact", SkillBlockCategory.Contact, PlayerAbility.Contact, SkillBlockRarity.Rare, 1),
                CreateBlock("contact", SkillBlockCategory.Contact, PlayerAbility.Contact, SkillBlockRarity.Elite, 0),
                CreateBlock("contact", SkillBlockCategory.Contact, PlayerAbility.Contact, SkillBlockRarity.Elite, 1),
                CreateBlock("contact", SkillBlockCategory.Contact, PlayerAbility.Contact, SkillBlockRarity.Unique, 0),
                CreateBlock("contact", SkillBlockCategory.Contact, PlayerAbility.Contact, SkillBlockRarity.Unique, 1),
                CreateBlock("contact", SkillBlockCategory.Contact, PlayerAbility.Contact, SkillBlockRarity.Legendary, 0),
                CreateBlock("contact", SkillBlockCategory.Contact, PlayerAbility.Contact, SkillBlockRarity.Legendary, 1),

                CreateBlock("power", SkillBlockCategory.Power, PlayerAbility.Power, SkillBlockRarity.Normal, 0),
                CreateBlock("power", SkillBlockCategory.Power, PlayerAbility.Power, SkillBlockRarity.Normal, 1),
                CreateBlock("power", SkillBlockCategory.Power, PlayerAbility.Power, SkillBlockRarity.Rare, 0),
                CreateBlock("power", SkillBlockCategory.Power, PlayerAbility.Power, SkillBlockRarity.Rare, 1),
                CreateBlock("power", SkillBlockCategory.Power, PlayerAbility.Power, SkillBlockRarity.Elite, 0),
                CreateBlock("power", SkillBlockCategory.Power, PlayerAbility.Power, SkillBlockRarity.Elite, 1),
                CreateBlock("power", SkillBlockCategory.Power, PlayerAbility.Power, SkillBlockRarity.Unique, 0),
                CreateBlock("power", SkillBlockCategory.Power, PlayerAbility.Power, SkillBlockRarity.Unique, 1),
                CreateBlock("power", SkillBlockCategory.Power, PlayerAbility.Power, SkillBlockRarity.Legendary, 0),
                CreateBlock("power", SkillBlockCategory.Power, PlayerAbility.Power, SkillBlockRarity.Legendary, 1),

                CreateBlock("baserunning", SkillBlockCategory.Baserunning, PlayerAbility.Speed, SkillBlockRarity.Normal, 0),
                CreateBlock("baserunning", SkillBlockCategory.Baserunning, PlayerAbility.Speed, SkillBlockRarity.Normal, 1),
                CreateBlock("baserunning", SkillBlockCategory.Baserunning, PlayerAbility.Speed, SkillBlockRarity.Rare, 0),
                CreateBlock("baserunning", SkillBlockCategory.Baserunning, PlayerAbility.Speed, SkillBlockRarity.Rare, 1),
                CreateBlock("baserunning", SkillBlockCategory.Baserunning, PlayerAbility.Speed, SkillBlockRarity.Elite, 0),
                CreateBlock("baserunning", SkillBlockCategory.Baserunning, PlayerAbility.Speed, SkillBlockRarity.Elite, 1),
                CreateBlock("baserunning", SkillBlockCategory.Baserunning, PlayerAbility.Speed, SkillBlockRarity.Unique, 0),
                CreateBlock("baserunning", SkillBlockCategory.Baserunning, PlayerAbility.Speed, SkillBlockRarity.Unique, 1),
                CreateBlock("baserunning", SkillBlockCategory.Baserunning, PlayerAbility.Speed, SkillBlockRarity.Legendary, 0),
                CreateBlock("baserunning", SkillBlockCategory.Baserunning, PlayerAbility.Speed, SkillBlockRarity.Legendary, 1),

                CreateBlock("bunt", SkillBlockCategory.Bunt, PlayerAbility.Bunt, SkillBlockRarity.Normal, 0),
                CreateBlock("bunt", SkillBlockCategory.Bunt, PlayerAbility.Bunt, SkillBlockRarity.Normal, 1),
                CreateBlock("bunt", SkillBlockCategory.Bunt, PlayerAbility.Bunt, SkillBlockRarity.Rare, 0),
                CreateBlock("bunt", SkillBlockCategory.Bunt, PlayerAbility.Bunt, SkillBlockRarity.Rare, 1),
                CreateBlock("bunt", SkillBlockCategory.Bunt, PlayerAbility.Bunt, SkillBlockRarity.Elite, 0),
                CreateBlock("bunt", SkillBlockCategory.Bunt, PlayerAbility.Bunt, SkillBlockRarity.Elite, 1),
                CreateBlock("bunt", SkillBlockCategory.Bunt, PlayerAbility.Bunt, SkillBlockRarity.Unique, 0),
                CreateBlock("bunt", SkillBlockCategory.Bunt, PlayerAbility.Bunt, SkillBlockRarity.Unique, 1),
                CreateBlock("bunt", SkillBlockCategory.Bunt, PlayerAbility.Bunt, SkillBlockRarity.Legendary, 0),
                CreateBlock("bunt", SkillBlockCategory.Bunt, PlayerAbility.Bunt, SkillBlockRarity.Legendary, 1),

                CreateBlock("defense", SkillBlockCategory.Defense, PlayerAbility.Defense, SkillBlockRarity.Normal, 0),
                CreateBlock("defense", SkillBlockCategory.Defense, PlayerAbility.Defense, SkillBlockRarity.Normal, 1),
                CreateBlock("defense", SkillBlockCategory.Defense, PlayerAbility.Defense, SkillBlockRarity.Rare, 0),
                CreateBlock("defense", SkillBlockCategory.Defense, PlayerAbility.Defense, SkillBlockRarity.Rare, 1),
                CreateBlock("defense", SkillBlockCategory.Defense, PlayerAbility.Defense, SkillBlockRarity.Elite, 0),
                CreateBlock("defense", SkillBlockCategory.Defense, PlayerAbility.Defense, SkillBlockRarity.Elite, 1),
                CreateBlock("defense", SkillBlockCategory.Defense, PlayerAbility.Defense, SkillBlockRarity.Unique, 0),
                CreateBlock("defense", SkillBlockCategory.Defense, PlayerAbility.Defense, SkillBlockRarity.Unique, 1),
                CreateBlock("defense", SkillBlockCategory.Defense, PlayerAbility.Defense, SkillBlockRarity.Legendary, 0),
                CreateBlock("defense", SkillBlockCategory.Defense, PlayerAbility.Defense, SkillBlockRarity.Legendary, 1),

                CreateBlock("batter_mental", SkillBlockCategory.BatterMental, PlayerAbility.BatterMental, SkillBlockRarity.Normal, 0),
                CreateBlock("batter_mental", SkillBlockCategory.BatterMental, PlayerAbility.BatterMental, SkillBlockRarity.Normal, 1),
                CreateBlock("batter_mental", SkillBlockCategory.BatterMental, PlayerAbility.BatterMental, SkillBlockRarity.Rare, 0),
                CreateBlock("batter_mental", SkillBlockCategory.BatterMental, PlayerAbility.BatterMental, SkillBlockRarity.Rare, 1),
                CreateBlock("batter_mental", SkillBlockCategory.BatterMental, PlayerAbility.BatterMental, SkillBlockRarity.Elite, 0),
                CreateBlock("batter_mental", SkillBlockCategory.BatterMental, PlayerAbility.BatterMental, SkillBlockRarity.Elite, 1),
                CreateBlock("batter_mental", SkillBlockCategory.BatterMental, PlayerAbility.BatterMental, SkillBlockRarity.Unique, 0),
                CreateBlock("batter_mental", SkillBlockCategory.BatterMental, PlayerAbility.BatterMental, SkillBlockRarity.Unique, 1),
                CreateBlock("batter_mental", SkillBlockCategory.BatterMental, PlayerAbility.BatterMental, SkillBlockRarity.Legendary, 0),
                CreateBlock("batter_mental", SkillBlockCategory.BatterMental, PlayerAbility.BatterMental, SkillBlockRarity.Legendary, 1),

                CreateBlock("velocity", SkillBlockCategory.Velocity, PlayerAbility.Velocity, SkillBlockRarity.Normal, 0),
                CreateBlock("velocity", SkillBlockCategory.Velocity, PlayerAbility.Velocity, SkillBlockRarity.Normal, 1),
                CreateBlock("velocity", SkillBlockCategory.Velocity, PlayerAbility.Velocity, SkillBlockRarity.Rare, 0),
                CreateBlock("velocity", SkillBlockCategory.Velocity, PlayerAbility.Velocity, SkillBlockRarity.Rare, 1),
                CreateBlock("velocity", SkillBlockCategory.Velocity, PlayerAbility.Velocity, SkillBlockRarity.Elite, 0),
                CreateBlock("velocity", SkillBlockCategory.Velocity, PlayerAbility.Velocity, SkillBlockRarity.Elite, 1),
                CreateBlock("velocity", SkillBlockCategory.Velocity, PlayerAbility.Velocity, SkillBlockRarity.Unique, 0),
                CreateBlock("velocity", SkillBlockCategory.Velocity, PlayerAbility.Velocity, SkillBlockRarity.Unique, 1),
                CreateBlock("velocity", SkillBlockCategory.Velocity, PlayerAbility.Velocity, SkillBlockRarity.Legendary, 0),
                CreateBlock("velocity", SkillBlockCategory.Velocity, PlayerAbility.Velocity, SkillBlockRarity.Legendary, 1),

                CreateBlock("control", SkillBlockCategory.Control, PlayerAbility.Control, SkillBlockRarity.Normal, 0),
                CreateBlock("control", SkillBlockCategory.Control, PlayerAbility.Control, SkillBlockRarity.Normal, 1),
                CreateBlock("control", SkillBlockCategory.Control, PlayerAbility.Control, SkillBlockRarity.Rare, 0),
                CreateBlock("control", SkillBlockCategory.Control, PlayerAbility.Control, SkillBlockRarity.Rare, 1),
                CreateBlock("control", SkillBlockCategory.Control, PlayerAbility.Control, SkillBlockRarity.Elite, 0),
                CreateBlock("control", SkillBlockCategory.Control, PlayerAbility.Control, SkillBlockRarity.Elite, 1),
                CreateBlock("control", SkillBlockCategory.Control, PlayerAbility.Control, SkillBlockRarity.Unique, 0),
                CreateBlock("control", SkillBlockCategory.Control, PlayerAbility.Control, SkillBlockRarity.Unique, 1),
                CreateBlock("control", SkillBlockCategory.Control, PlayerAbility.Control, SkillBlockRarity.Legendary, 0),
                CreateBlock("control", SkillBlockCategory.Control, PlayerAbility.Control, SkillBlockRarity.Legendary, 1),

                CreateBlock("breaking", SkillBlockCategory.Breaking, PlayerAbility.Breaking, SkillBlockRarity.Normal, 0),
                CreateBlock("breaking", SkillBlockCategory.Breaking, PlayerAbility.Breaking, SkillBlockRarity.Normal, 1),
                CreateBlock("breaking", SkillBlockCategory.Breaking, PlayerAbility.Breaking, SkillBlockRarity.Rare, 0),
                CreateBlock("breaking", SkillBlockCategory.Breaking, PlayerAbility.Breaking, SkillBlockRarity.Rare, 1),
                CreateBlock("breaking", SkillBlockCategory.Breaking, PlayerAbility.Breaking, SkillBlockRarity.Elite, 0),
                CreateBlock("breaking", SkillBlockCategory.Breaking, PlayerAbility.Breaking, SkillBlockRarity.Elite, 1),
                CreateBlock("breaking", SkillBlockCategory.Breaking, PlayerAbility.Breaking, SkillBlockRarity.Unique, 0),
                CreateBlock("breaking", SkillBlockCategory.Breaking, PlayerAbility.Breaking, SkillBlockRarity.Unique, 1),
                CreateBlock("breaking", SkillBlockCategory.Breaking, PlayerAbility.Breaking, SkillBlockRarity.Legendary, 0),
                CreateBlock("breaking", SkillBlockCategory.Breaking, PlayerAbility.Breaking, SkillBlockRarity.Legendary, 1),

                CreateBlock("pitcher_physical", SkillBlockCategory.PitcherPhysical, PlayerAbility.Stamina, SkillBlockRarity.Normal, 0),
                CreateBlock("pitcher_physical", SkillBlockCategory.PitcherPhysical, PlayerAbility.Stamina, SkillBlockRarity.Normal, 1),
                CreateBlock("pitcher_physical", SkillBlockCategory.PitcherPhysical, PlayerAbility.Stamina, SkillBlockRarity.Rare, 0),
                CreateBlock("pitcher_physical", SkillBlockCategory.PitcherPhysical, PlayerAbility.Stamina, SkillBlockRarity.Rare, 1),
                CreateBlock("pitcher_physical", SkillBlockCategory.PitcherPhysical, PlayerAbility.Stamina, SkillBlockRarity.Elite, 0),
                CreateBlock("pitcher_physical", SkillBlockCategory.PitcherPhysical, PlayerAbility.Stamina, SkillBlockRarity.Elite, 1),
                CreateBlock("pitcher_physical", SkillBlockCategory.PitcherPhysical, PlayerAbility.Stamina, SkillBlockRarity.Unique, 0),
                CreateBlock("pitcher_physical", SkillBlockCategory.PitcherPhysical, PlayerAbility.Stamina, SkillBlockRarity.Unique, 1),
                CreateBlock("pitcher_physical", SkillBlockCategory.PitcherPhysical, PlayerAbility.Stamina, SkillBlockRarity.Legendary, 0),
                CreateBlock("pitcher_physical", SkillBlockCategory.PitcherPhysical, PlayerAbility.Stamina, SkillBlockRarity.Legendary, 1),

                CreateBlock("stuff", SkillBlockCategory.Stuff, PlayerAbility.Stuff, SkillBlockRarity.Normal, 0),
                CreateBlock("stuff", SkillBlockCategory.Stuff, PlayerAbility.Stuff, SkillBlockRarity.Normal, 1),
                CreateBlock("stuff", SkillBlockCategory.Stuff, PlayerAbility.Stuff, SkillBlockRarity.Rare, 0),
                CreateBlock("stuff", SkillBlockCategory.Stuff, PlayerAbility.Stuff, SkillBlockRarity.Rare, 1),
                CreateBlock("stuff", SkillBlockCategory.Stuff, PlayerAbility.Stuff, SkillBlockRarity.Elite, 0),
                CreateBlock("stuff", SkillBlockCategory.Stuff, PlayerAbility.Stuff, SkillBlockRarity.Elite, 1),
                CreateBlock("stuff", SkillBlockCategory.Stuff, PlayerAbility.Stuff, SkillBlockRarity.Unique, 0),
                CreateBlock("stuff", SkillBlockCategory.Stuff, PlayerAbility.Stuff, SkillBlockRarity.Unique, 1),
                CreateBlock("stuff", SkillBlockCategory.Stuff, PlayerAbility.Stuff, SkillBlockRarity.Legendary, 0),
                CreateBlock("stuff", SkillBlockCategory.Stuff, PlayerAbility.Stuff, SkillBlockRarity.Legendary, 1),

                CreateBlock("pitcher_mental", SkillBlockCategory.PitcherMental, PlayerAbility.PitcherMental, SkillBlockRarity.Normal, 0),
                CreateBlock("pitcher_mental", SkillBlockCategory.PitcherMental, PlayerAbility.PitcherMental, SkillBlockRarity.Normal, 1),
                CreateBlock("pitcher_mental", SkillBlockCategory.PitcherMental, PlayerAbility.PitcherMental, SkillBlockRarity.Rare, 0),
                CreateBlock("pitcher_mental", SkillBlockCategory.PitcherMental, PlayerAbility.PitcherMental, SkillBlockRarity.Rare, 1),
                CreateBlock("pitcher_mental", SkillBlockCategory.PitcherMental, PlayerAbility.PitcherMental, SkillBlockRarity.Elite, 0),
                CreateBlock("pitcher_mental", SkillBlockCategory.PitcherMental, PlayerAbility.PitcherMental, SkillBlockRarity.Elite, 1),
                CreateBlock("pitcher_mental", SkillBlockCategory.PitcherMental, PlayerAbility.PitcherMental, SkillBlockRarity.Unique, 0),
                CreateBlock("pitcher_mental", SkillBlockCategory.PitcherMental, PlayerAbility.PitcherMental, SkillBlockRarity.Unique, 1),
                CreateBlock("pitcher_mental", SkillBlockCategory.PitcherMental, PlayerAbility.PitcherMental, SkillBlockRarity.Legendary, 0),
                CreateBlock("pitcher_mental", SkillBlockCategory.PitcherMental, PlayerAbility.PitcherMental, SkillBlockRarity.Legendary, 1)
            };
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
        /// 같은 카테고리·등급이라도 <paramref name="shapeVariant"/>가 다르면 서로 다른
        /// 모양이 나오도록 오프셋을 더해 뽑는다. 오프셋 3은 7종 모양에 대해
        /// 나머지가 겹치지 않도록 고른 값이다.
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
