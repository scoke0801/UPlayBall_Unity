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

    public enum TetrominoShape
    {
        I,
        O,
        T,
        S,
        Z,
        J,
        L
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
    /// 표준 테트리스 7종 모양을 같은 크기의 정사각형 네 칸 좌표로 제공한다.
    /// </summary>
    public static class TetrominoShapeCatalog
    {
        public const int CellCount = 4;

        public static BoardCell[] CreateCells(TetrominoShape shape)
        {
            return shape switch
            {
                TetrominoShape.I => new[]
                {
                    new BoardCell(0, 0), new BoardCell(1, 0),
                    new BoardCell(2, 0), new BoardCell(3, 0)
                },
                TetrominoShape.O => new[]
                {
                    new BoardCell(0, 0), new BoardCell(1, 0),
                    new BoardCell(0, 1), new BoardCell(1, 1)
                },
                TetrominoShape.T => new[]
                {
                    new BoardCell(0, 0), new BoardCell(1, 0), new BoardCell(2, 0),
                    new BoardCell(1, 1)
                },
                TetrominoShape.S => new[]
                {
                    new BoardCell(1, 0), new BoardCell(2, 0),
                    new BoardCell(0, 1), new BoardCell(1, 1)
                },
                TetrominoShape.Z => new[]
                {
                    new BoardCell(0, 0), new BoardCell(1, 0),
                    new BoardCell(1, 1), new BoardCell(2, 1)
                },
                TetrominoShape.J => new[]
                {
                    new BoardCell(0, 0),
                    new BoardCell(0, 1), new BoardCell(1, 1), new BoardCell(2, 1)
                },
                TetrominoShape.L => new[]
                {
                    new BoardCell(2, 0),
                    new BoardCell(0, 1), new BoardCell(1, 1), new BoardCell(2, 1)
                },
                _ => throw new ArgumentOutOfRangeException(nameof(shape))
            };
        }
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
            if (shapeCells == null || shapeCells.Length != TetrominoShapeCatalog.CellCount)
                throw new ArgumentException("블록은 정사각형 네 칸으로 구성된 테트로미노여야 합니다.", nameof(shapeCells));
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
            int minimumX = int.MaxValue;
            int minimumY = int.MaxValue;
            for (int index = 0; index < cells.Length; index++)
            {
                if (cells[index].X < 0 || cells[index].Y < 0)
                    throw new ArgumentException("기본 모양 좌표는 음수일 수 없습니다.", nameof(cells));
                minimumX = Math.Min(minimumX, cells[index].X);
                minimumY = Math.Min(minimumY, cells[index].Y);
                for (int previous = 0; previous < index; previous++)
                {
                    if (cells[index].X == cells[previous].X && cells[index].Y == cells[previous].Y)
                        throw new ArgumentException("블록 모양 셀은 중복될 수 없습니다.", nameof(cells));
                }
            }

            if (minimumX != 0 || minimumY != 0)
                throw new ArgumentException("블록 모양 좌표는 좌상단 (0, 0)에서 시작해야 합니다.", nameof(cells));
            if (!IsConnected(cells))
                throw new ArgumentException("블록의 네 칸은 상하좌우로 연결된 테트로미노여야 합니다.", nameof(cells));
        }

        private static bool IsConnected(BoardCell[] cells)
        {
            var visited = new bool[cells.Length];
            var queue = new int[cells.Length];
            int readIndex = 0;
            int writeIndex = 1;
            int visitedCount = 1;
            visited[0] = true;
            queue[0] = 0;

            while (readIndex < writeIndex)
            {
                BoardCell current = cells[queue[readIndex++]];
                for (int index = 0; index < cells.Length; index++)
                {
                    if (visited[index])
                        continue;
                    int distance = Math.Abs(cells[index].X - current.X) +
                                   Math.Abs(cells[index].Y - current.Y);
                    if (distance != 1)
                        continue;
                    visited[index] = true;
                    queue[writeIndex++] = index;
                    visitedCount++;
                }
            }
            return visitedCount == cells.Length;
        }

        private static BoardCell[] CopyCells(BoardCell[] source)
        {
            var result = new BoardCell[source.Length];
            Array.Copy(source, result, source.Length);
            return result;
        }
    }
}
