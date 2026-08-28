using System;

namespace Baseball.Core.Growth
{
    public enum SkillBlockRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic
    }

    public enum SkillBlockCategory
    {
        Contact,
        Power,
        Baserunning,
        Defense,
        BatterMental,
        Velocity,
        Control,
        Breaking,
        PitcherPhysical,
        PitcherMental
    }

    public enum TraitSocketRule
    {
        None,
        CoversSocket
    }

    public readonly struct BoardCell
    {
        public BoardCell(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }
        public int Y { get; }
    }

    /// <summary>
    /// 모양·능력치 보너스·Trait 활성 조건을 가진 읽기 전용 스킬 블록 정의다.
    /// </summary>
    public sealed class SkillBlockDefinition
    {
        public SkillBlockDefinition(
            string blockId,
            SkillBlockRarity rarity,
            SkillBlockCategory category,
            BoardCell[] shapeCells,
            bool canRotate,
            AbilityChange[] abilityBonuses,
            long sellValue,
            string traitId = "",
            TraitSocketRule traitSocketRule = TraitSocketRule.None,
            bool isUniqueReward = false)
        {
            if (string.IsNullOrWhiteSpace(blockId))
                throw new ArgumentException("BlockId는 비어 있을 수 없습니다.", nameof(blockId));
            if (shapeCells == null || shapeCells.Length == 0 || shapeCells.Length > 4)
                throw new ArgumentException("블록은 1~4칸이어야 합니다.", nameof(shapeCells));
            if (sellValue < 0L)
                throw new ArgumentOutOfRangeException(nameof(sellValue));
            if (traitSocketRule != TraitSocketRule.None && string.IsNullOrWhiteSpace(traitId))
                throw new ArgumentException("Trait 규칙에는 TraitId가 필요합니다.", nameof(traitId));
            ValidateShape(shapeCells);

            BlockId = blockId.Trim();
            Rarity = rarity;
            Category = category;
            ShapeCells = CopyCells(shapeCells);
            CanRotate = canRotate;
            AbilityBonuses = abilityBonuses ?? Array.Empty<AbilityChange>();
            SellValue = sellValue;
            TraitId = traitId?.Trim() ?? string.Empty;
            TraitSocketRule = traitSocketRule;
            IsUniqueReward = isUniqueReward;
        }

        public string BlockId { get; }
        public SkillBlockRarity Rarity { get; }
        public SkillBlockCategory Category { get; }
        public BoardCell[] ShapeCells { get; }
        public bool CanRotate { get; }
        public AbilityChange[] AbilityBonuses { get; }
        public long SellValue { get; }
        public string TraitId { get; }
        public TraitSocketRule TraitSocketRule { get; }
        public bool IsUniqueReward { get; }

        private static void ValidateShape(BoardCell[] cells)
        {
            for (int index = 0; index < cells.Length; index++)
            {
                if (cells[index].X < 0 || cells[index].Y < 0)
                    throw new ArgumentException("기본 모양 좌표는 음수일 수 없습니다.", nameof(cells));
                for (int previous = 0; previous < index; previous++)
                {
                    if (cells[index].X == cells[previous].X && cells[index].Y == cells[previous].Y)
                        throw new ArgumentException("블록 모양 셀은 중복될 수 없습니다.", nameof(cells));
                }
            }
        }

        private static BoardCell[] CopyCells(BoardCell[] source)
        {
            var result = new BoardCell[source.Length];
            Array.Copy(source, result, source.Length);
            return result;
        }
    }
}
