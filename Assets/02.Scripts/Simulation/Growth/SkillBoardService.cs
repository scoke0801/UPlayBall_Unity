using System;
using System.Collections.Generic;
using Baseball.Core.Growth;

namespace Baseball.Simulation.Growth
{
    /// <summary>
    /// 선택 블록이 차지할 실제 보드 좌표와 배치 가능 여부를 함께 반환한다.
    /// </summary>
    public readonly struct SkillBlockPlacementPreview
    {
        public SkillBlockPlacementPreview(BoardCell[] cells, bool canPlace)
        {
            Cells = cells ?? Array.Empty<BoardCell>();
            CanPlace = canPlace;
        }

        public BoardCell[] Cells { get; }
        public bool CanPlace { get; }
    }

    /// <summary>
    /// 블록의 회전·경계·겹침·Trait Socket 조건을 검증하고 보드 상태를 변경한다.
    /// </summary>
    public sealed class SkillBoardService
    {
        private readonly SkillBoardDefinition _boardDefinition;
        private readonly SkillBlockDefinition[] _blockDefinitions;

        public SkillBoardService(SkillBoardDefinition boardDefinition, SkillBlockDefinition[] blockDefinitions)
        {
            _boardDefinition = boardDefinition ?? throw new ArgumentNullException(nameof(boardDefinition));
            _blockDefinitions = blockDefinitions ?? throw new ArgumentNullException(nameof(blockDefinitions));
        }

        public void PlaceBlock(
            SkillBoardState state,
            int instanceId,
            int originX,
            int originY,
            int rotationQuarterTurns)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            SkillBlockInstance instance = state.FindOwnedBlock(instanceId);
            if (instance.InstanceId == 0)
                throw new InvalidOperationException("보유 중인 블록만 장착할 수 있습니다.");
            SkillBlockDefinition definition = FindDefinition(instance.DefinitionId);
            ValidatePlacement(state, definition, originX, originY, rotationQuarterTurns);
            state.PlaceOwnedBlock(new PlacedSkillBlock(instance, originX, originY, rotationQuarterTurns));
        }

        public void RemoveBlock(SkillBoardState state, int instanceId)
        {
            state.RemovePlacedBlock(instanceId, returnToInventory: false);
        }

        /// <summary>
        /// 배치 상태의 회전을 적용한 실제 보드 좌표를 반환한다.
        /// </summary>
        public BoardCell[] GetOccupiedCells(PlacedSkillBlock placement)
        {
            SkillBlockDefinition definition = FindDefinition(placement.Instance.DefinitionId);
            return BuildOccupiedCells(
                definition,
                placement.OriginX,
                placement.OriginY,
                placement.RotationQuarterTurns);
        }

        /// <summary>
        /// 상태를 바꾸지 않고 선택 블록의 실제 보드 좌표와 배치 가능 여부를 계산한다.
        /// </summary>
        public SkillBlockPlacementPreview GetPlacementPreview(
            SkillBoardState state,
            int instanceId,
            int originX,
            int originY,
            int rotationQuarterTurns)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            SkillBlockInstance instance = state.FindOwnedBlock(instanceId);
            if (instance.InstanceId == 0)
                return new SkillBlockPlacementPreview(Array.Empty<BoardCell>(), false);

            SkillBlockDefinition definition = FindDefinition(instance.DefinitionId);
            if (rotationQuarterTurns < 0 || rotationQuarterTurns > 3 ||
                !definition.CanRotate && rotationQuarterTurns != 0)
            {
                return new SkillBlockPlacementPreview(Array.Empty<BoardCell>(), false);
            }

            BoardCell[] cells = BuildOccupiedCells(
                definition,
                originX,
                originY,
                rotationQuarterTurns);
            return new SkillBlockPlacementPreview(
                cells,
                GetPlacementFailure(state, cells) == SkillBlockPlacementFailure.None);
        }

        public void Redesign(
            SkillBoardState state,
            CareerEconomyState economy,
            OffseasonState offseason,
            int seasonYear,
            long cost)
        {
            if (offseason == null) throw new ArgumentNullException(nameof(offseason));
            if (offseason.SeasonYear != seasonYear)
                throw new InvalidOperationException("현재 오프시즌에만 재설계할 수 있습니다.");
            if (offseason.BoardRedesignUsed || state.LastRedesignSeason == seasonYear)
                throw new InvalidOperationException("전문 재설계는 오프시즌당 한 번만 가능합니다.");
            economy.Spend(seasonYear, MoneyTransactionType.TrainingExpense, "skill_board_redesign", cost);
            offseason.MarkBoardRedesignUsed();
            state.Redesign(seasonYear);
        }

        public int GetAbilityBonus(SkillBoardState state, PlayerAbility ability)
        {
            int total = 0;
            for (int index = 0; index < state.PlacedBlocks.Count; index++)
            {
                SkillBlockDefinition definition = FindDefinition(state.PlacedBlocks[index].Instance.DefinitionId);
                for (int bonusIndex = 0; bonusIndex < definition.AbilityBonuses.Length; bonusIndex++)
                {
                    if (definition.AbilityBonuses[bonusIndex].Ability == ability)
                        total += definition.AbilityBonuses[bonusIndex].Amount;
                }
            }
            return total;
        }

        public int GetStableAbility(SkillBoardState state, PlayerGrowthState player, PlayerAbility ability)
        {
            int value = player.BaseAbilities.Get(ability) + GetAbilityBonus(state, ability);
            if (value < AbilityRatings.Minimum) return AbilityRatings.Minimum;
            return value > AbilityRatings.Maximum ? AbilityRatings.Maximum : value;
        }

        public string[] GetActiveTraitIds(SkillBoardState state)
        {
            var traits = new List<string>();
            for (int index = 0; index < state.PlacedBlocks.Count; index++)
            {
                PlacedSkillBlock placement = state.PlacedBlocks[index];
                SkillBlockDefinition definition = FindDefinition(placement.Instance.DefinitionId);
                if (string.IsNullOrEmpty(definition.TraitId))
                    continue;
                if (definition.TraitSocketRule == TraitSocketRule.None || CoversTraitSocket(definition, placement))
                    traits.Add(definition.TraitId);
            }
            return traits.ToArray();
        }

        private void ValidatePlacement(
            SkillBoardState state,
            SkillBlockDefinition definition,
            int originX,
            int originY,
            int rotationQuarterTurns)
        {
            if (rotationQuarterTurns < 0 || rotationQuarterTurns > 3)
                throw new ArgumentOutOfRangeException(nameof(rotationQuarterTurns));
            if (!definition.CanRotate && rotationQuarterTurns != 0)
                throw new InvalidOperationException("회전할 수 없는 블록입니다.");

            BoardCell[] cells = BuildOccupiedCells(
                definition,
                originX,
                originY,
                rotationQuarterTurns);
            SkillBlockPlacementFailure failure = GetPlacementFailure(state, cells);
            if (failure == SkillBlockPlacementFailure.OutOfBounds)
                throw new InvalidOperationException("블록이 성장판 경계를 벗어납니다.");
            if (failure == SkillBlockPlacementFailure.Occupied)
                throw new InvalidOperationException("이미 다른 블록이 놓인 칸입니다.");
        }

        private SkillBlockPlacementFailure GetPlacementFailure(
            SkillBoardState state,
            BoardCell[] cells)
        {
            for (int index = 0; index < cells.Length; index++)
            {
                int x = cells[index].X;
                int y = cells[index].Y;
                if (x < 0 || x >= _boardDefinition.Width || y < 0 || y >= _boardDefinition.Height)
                    return SkillBlockPlacementFailure.OutOfBounds;
                if (IsOccupied(state, x, y))
                    return SkillBlockPlacementFailure.Occupied;
            }
            return SkillBlockPlacementFailure.None;
        }

        private bool IsOccupied(SkillBoardState state, int x, int y)
        {
            for (int index = 0; index < state.PlacedBlocks.Count; index++)
            {
                PlacedSkillBlock placed = state.PlacedBlocks[index];
                SkillBlockDefinition definition = FindDefinition(placed.Instance.DefinitionId);
                BoardCell[] cells = GetNormalizedCells(definition, placed.RotationQuarterTurns);
                for (int cellIndex = 0; cellIndex < cells.Length; cellIndex++)
                {
                    if (placed.OriginX + cells[cellIndex].X == x && placed.OriginY + cells[cellIndex].Y == y)
                        return true;
                }
            }
            return false;
        }

        private bool CoversTraitSocket(SkillBlockDefinition definition, PlacedSkillBlock placement)
        {
            BoardCell[] cells = GetNormalizedCells(definition, placement.RotationQuarterTurns);
            for (int index = 0; index < cells.Length; index++)
            {
                int x = placement.OriginX + cells[index].X;
                int y = placement.OriginY + cells[index].Y;
                for (int socketIndex = 0; socketIndex < _boardDefinition.TraitSockets.Length; socketIndex++)
                {
                    if (_boardDefinition.TraitSockets[socketIndex].X == x &&
                        _boardDefinition.TraitSockets[socketIndex].Y == y)
                        return true;
                }
            }
            return false;
        }

        private static BoardCell[] GetNormalizedCells(SkillBlockDefinition definition, int rotation)
        {
            var cells = new BoardCell[definition.ShapeCells.Length];
            int minimumX = int.MaxValue;
            int minimumY = int.MaxValue;
            for (int index = 0; index < definition.ShapeCells.Length; index++)
            {
                BoardCell source = definition.ShapeCells[index];
                int x;
                int y;
                switch (rotation)
                {
                    case 1: x = source.Y; y = -source.X; break;
                    case 2: x = -source.X; y = -source.Y; break;
                    case 3: x = -source.Y; y = source.X; break;
                    default: x = source.X; y = source.Y; break;
                }
                cells[index] = new BoardCell(x, y);
                minimumX = Math.Min(minimumX, x);
                minimumY = Math.Min(minimumY, y);
            }
            for (int index = 0; index < cells.Length; index++)
                cells[index] = new BoardCell(cells[index].X - minimumX, cells[index].Y - minimumY);
            return cells;
        }

        private static BoardCell[] BuildOccupiedCells(
            SkillBlockDefinition definition,
            int originX,
            int originY,
            int rotationQuarterTurns)
        {
            BoardCell[] normalized = GetNormalizedCells(definition, rotationQuarterTurns);
            var result = new BoardCell[normalized.Length];
            for (int index = 0; index < normalized.Length; index++)
            {
                result[index] = new BoardCell(
                    originX + normalized[index].X,
                    originY + normalized[index].Y);
            }
            return result;
        }

        private enum SkillBlockPlacementFailure
        {
            None,
            OutOfBounds,
            Occupied
        }

        private SkillBlockDefinition FindDefinition(string definitionId)
        {
            for (int index = 0; index < _blockDefinitions.Length; index++)
            {
                if (string.Equals(_blockDefinitions[index].BlockId, definitionId, StringComparison.Ordinal))
                    return _blockDefinitions[index];
            }
            throw new InvalidOperationException("스킬 블록 정의를 찾을 수 없습니다.");
        }
    }
}
