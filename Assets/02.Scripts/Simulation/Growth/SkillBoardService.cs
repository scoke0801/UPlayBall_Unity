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
        public const int MaximumBonusPerAbility = 9;
        public const int MaximumTotalAbilityBonus = 18;

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

        /// <summary>
        /// 오프시즌에는 블록이 파괴되지만 시즌 중 편집은 확정 비용을 따로 받으므로 그대로 회수한다.
        /// </summary>
        public void RemoveBlock(SkillBoardState state, int instanceId)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            state.RemovePlacedBlock(instanceId, returnToInventory: state.IsSeasonLocked);
        }

        /// <summary>
        /// 임시 편집 배치를 전부 검증한 뒤 한 번에 적용하며 기존 장착을 바꾸면 안전 회수를 사용한다.
        /// </summary>
        public bool ApplyLayout(
            SkillBoardState state,
            PlacedSkillBlock[] layout,
            CareerEconomyState economy,
            OffseasonState offseason,
            int seasonYear,
            long safeRecoveryCost)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            ValidateLayout(state, layout);

            bool requiresSafeRecovery = RequiresSafeRecovery(state, layout);
            if (requiresSafeRecovery)
            {
                if (state.IsSeasonLocked)
                    state.ReclaimPlacedBlocks();
                else
                    Redesign(state, economy, offseason, seasonYear, safeRecoveryCost);
            }

            for (int index = 0; index < layout.Length; index++)
            {
                if (IsSamePlacement(state, layout[index]))
                    continue;
                PlaceBlock(
                    state,
                    layout[index].Instance.InstanceId,
                    layout[index].OriginX,
                    layout[index].OriginY,
                    layout[index].RotationQuarterTurns);
            }
            return requiresSafeRecovery;
        }

        public bool RequiresSafeRecovery(SkillBoardState state, PlacedSkillBlock[] layout)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            for (int index = 0; index < state.PlacedBlocks.Count; index++)
            {
                PlacedSkillBlock current = state.PlacedBlocks[index];
                bool found = false;
                for (int layoutIndex = 0; layoutIndex < layout.Length; layoutIndex++)
                {
                    if (HasSamePlacement(current, layout[layoutIndex]))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                    return true;
            }
            return false;
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

        /// <summary>
        /// 정의 변경 뒤 경계나 겹침 조건을 위반한 기존 장착만 무료로 보관함에 되돌린다.
        /// </summary>
        public int RecoverInvalidPlacements(SkillBoardState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var placements = new PlacedSkillBlock[state.PlacedBlocks.Count];
            for (int index = 0; index < placements.Length; index++)
                placements[index] = state.PlacedBlocks[index];

            var validation = new SkillBoardState(state.BoardDefinitionId);
            int recoveredCount = 0;
            for (int index = 0; index < placements.Length; index++)
            {
                PlacedSkillBlock placement = placements[index];
                validation.AddOwnedBlock(placement.Instance);
                SkillBlockPlacementPreview preview = GetPlacementPreview(
                    validation,
                    placement.Instance.InstanceId,
                    placement.OriginX,
                    placement.OriginY,
                    placement.RotationQuarterTurns);
                if (preview.CanPlace)
                {
                    PlaceBlock(
                        validation,
                        placement.Instance.InstanceId,
                        placement.OriginX,
                        placement.OriginY,
                        placement.RotationQuarterTurns);
                    continue;
                }

                state.RemovePlacedBlock(placement.Instance.InstanceId, returnToInventory: true);
                recoveredCount++;
            }
            return recoveredCount;
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

        /// <summary>
        /// 시즌 중 편집한 배치를 비용을 받고 활성 보드로 확정한다.
        /// 확정 전까지는 경기와 역할 평가가 이전 활성 보드를 계속 사용한다.
        /// </summary>
        public void CommitInSeason(
            SkillBoardState state,
            CareerEconomyState economy,
            int seasonYear,
            long cost)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (economy == null) throw new ArgumentNullException(nameof(economy));
            if (!state.IsSeasonLocked)
                throw new InvalidOperationException("시즌 중 확정은 정규시즌에만 사용할 수 있습니다.");
            if (!state.HasUncommittedPlacements)
                throw new InvalidOperationException("확정할 성장판 변경이 없습니다.");
            economy.Spend(
                seasonYear,
                MoneyTransactionType.TrainingExpense,
                "skill_board_in_season_commit",
                cost);
            state.CommitInSeasonPlacements(seasonYear);
        }

        public int GetAbilityBonus(SkillBoardState state, PlayerAbility ability)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            int[] bonuses = BuildEffectiveBonusArray(state);
            return bonuses[(int)ability];
        }

        /// <summary>중첩 체감과 성장판 상한을 적용하기 전 표시용 원시 합계를 반환한다.</summary>
        public int GetRawAbilityBonus(SkillBoardState state, PlayerAbility ability)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            int total = 0;
            IReadOnlyList<PlacedSkillBlock> applied = state.AppliedBlocks;
            for (int index = 0; index < applied.Count; index++)
            {
                SkillBlockDefinition definition = FindDefinition(applied[index].Instance.DefinitionId);
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

        private int[] BuildEffectiveBonusArray(SkillBoardState state)
        {
            int abilityCount = PlayerAbilityCatalog.AbilityCount;
            var result = new int[abilityCount];
            var stackCounts = new int[abilityCount];
            IReadOnlyList<PlacedSkillBlock> applied = state.AppliedBlocks;
            for (int blockIndex = 0; blockIndex < applied.Count; blockIndex++)
            {
                SkillBlockDefinition definition = FindDefinition(
                    applied[blockIndex].Instance.DefinitionId);
                for (int bonusIndex = 0; bonusIndex < definition.AbilityBonuses.Length; bonusIndex++)
                {
                    AbilityChange bonus = definition.AbilityBonuses[bonusIndex];
                    int abilityIndex = (int)bonus.Ability;
                    double multiplier = stackCounts[abilityIndex] switch
                    {
                        0 => 1d,
                        1 => 0.6d,
                        2 => 0.3d,
                        _ => 0d
                    };
                    stackCounts[abilityIndex]++;
                    int effective = (int)Math.Round(
                        bonus.Amount * multiplier,
                        MidpointRounding.AwayFromZero);
                    result[abilityIndex] = Math.Min(
                        MaximumBonusPerAbility,
                        result[abilityIndex] + effective);
                }
            }

            ApplyTotalBonusCap(result);
            return result;
        }

        private static void ApplyTotalBonusCap(int[] bonuses)
        {
            int total = 0;
            for (int index = 0; index < bonuses.Length; index++)
                total += bonuses[index];
            if (total <= MaximumTotalAbilityBonus)
                return;

            var remainders = new double[bonuses.Length];
            int allocated = 0;
            for (int index = 0; index < bonuses.Length; index++)
            {
                double scaled = bonuses[index] * MaximumTotalAbilityBonus / (double)total;
                int floor = (int)Math.Floor(scaled);
                bonuses[index] = floor;
                remainders[index] = scaled - floor;
                allocated += floor;
            }

            while (allocated < MaximumTotalAbilityBonus)
            {
                int selected = 0;
                for (int index = 1; index < remainders.Length; index++)
                {
                    if (remainders[index] > remainders[selected])
                        selected = index;
                }
                bonuses[selected]++;
                remainders[selected] = -1d;
                allocated++;
            }
        }

        public string[] GetActiveTraitIds(SkillBoardState state)
        {
            var traits = new List<string>();
            IReadOnlyList<PlacedSkillBlock> applied = state.AppliedBlocks;
            for (int index = 0; index < applied.Count; index++)
            {
                PlacedSkillBlock placement = applied[index];
                SkillBlockDefinition definition = FindDefinition(placement.Instance.DefinitionId);
                if (string.IsNullOrEmpty(definition.TraitId))
                    continue;
                if ((definition.TraitSocketRule == TraitSocketRule.None || CoversTraitSocket(definition, placement)) &&
                    !traits.Contains(definition.TraitId))
                {
                    traits.Add(definition.TraitId);
                }
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

        private void ValidateLayout(SkillBoardState state, PlacedSkillBlock[] layout)
        {
            var validation = new SkillBoardState(state.BoardDefinitionId);
            for (int index = 0; index < state.OwnedBlocks.Count; index++)
                validation.AddOwnedBlock(state.OwnedBlocks[index]);
            for (int index = 0; index < state.PlacedBlocks.Count; index++)
                validation.AddOwnedBlock(state.PlacedBlocks[index].Instance);

            for (int index = 0; index < layout.Length; index++)
            {
                PlaceBlock(
                    validation,
                    layout[index].Instance.InstanceId,
                    layout[index].OriginX,
                    layout[index].OriginY,
                    layout[index].RotationQuarterTurns);
            }
        }

        private static bool HasSamePlacement(PlacedSkillBlock left, PlacedSkillBlock right)
        {
            return left.Instance.InstanceId == right.Instance.InstanceId &&
                   left.OriginX == right.OriginX &&
                   left.OriginY == right.OriginY &&
                   left.RotationQuarterTurns == right.RotationQuarterTurns;
        }

        private static bool IsSamePlacement(SkillBoardState state, PlacedSkillBlock target)
        {
            for (int index = 0; index < state.PlacedBlocks.Count; index++)
            {
                if (HasSamePlacement(state.PlacedBlocks[index], target))
                    return true;
            }
            return false;
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

    /// <summary>한 능력치의 Base부터 경기 적용값까지 단계별 계산 결과다.</summary>
    public readonly struct AbilityBreakdown
    {
        public AbilityBreakdown(
            int baseAbility,
            int potential,
            int skillBonusRaw,
            int skillBonusEffective,
            int peakBonus,
            int conditionModifier,
            int injuryModifier,
            int tacticalModifier)
        {
            BaseAbility = baseAbility;
            Potential = potential;
            SkillBonusRaw = skillBonusRaw;
            SkillBonusEffective = skillBonusEffective;
            PeakBonus = peakBonus;
            ConditionModifier = conditionModifier;
            InjuryModifier = injuryModifier;
            TacticalModifier = tacticalModifier;
            RosterAbility = Clamp(baseAbility + skillBonusEffective);
            CurrentAbility = Clamp(RosterAbility + peakBonus);
            MatchAbility = Clamp(CurrentAbility + conditionModifier + injuryModifier + tacticalModifier);
        }

        public int BaseAbility { get; }
        public int Potential { get; }
        public int SkillBonusRaw { get; }
        public int SkillBonusEffective { get; }
        public int PeakBonus { get; }
        public int ConditionModifier { get; }
        public int InjuryModifier { get; }
        public int TacticalModifier { get; }
        public int RosterAbility { get; }
        public int CurrentAbility { get; }
        public int MatchAbility { get; }

        private static int Clamp(int value)
        {
            if (value < AbilityRatings.Minimum) return AbilityRatings.Minimum;
            return value > AbilityRatings.Maximum ? AbilityRatings.Maximum : value;
        }
    }

    /// <summary>경기 한정 보정을 명시적으로 전달해 안정 전력과 현재 기량의 혼용을 막는다.</summary>
    public readonly struct EffectiveAbilityContext
    {
        public EffectiveAbilityContext(int conditionModifier, int injuryModifier, int tacticalModifier)
        {
            ConditionModifier = conditionModifier;
            InjuryModifier = injuryModifier;
            TacticalModifier = tacticalModifier;
        }

        public int ConditionModifier { get; }
        public int InjuryModifier { get; }
        public int TacticalModifier { get; }
        public static EffectiveAbilityContext Neutral => new EffectiveAbilityContext(0, 0, 0);
    }

    /// <summary>경기·역할·계약·UI가 공유하는 최종 능력치 단일 계산 진입점이다.</summary>
    public sealed class EffectiveAbilityResolver
    {
        private readonly SkillBoardService _skillBoardService;

        public EffectiveAbilityResolver(SkillBoardService skillBoardService)
        {
            _skillBoardService = skillBoardService ?? throw new ArgumentNullException(nameof(skillBoardService));
        }

        public AbilityBreakdown Resolve(
            PlayerGrowthState growth,
            SkillBoardState board,
            PlayerAbility ability,
            EffectiveAbilityContext context)
        {
            if (growth == null) throw new ArgumentNullException(nameof(growth));
            if (board == null) throw new ArgumentNullException(nameof(board));
            return new AbilityBreakdown(
                growth.BaseAbilities.Get(ability),
                growth.PotentialByAbility.Get(ability),
                _skillBoardService.GetRawAbilityBonus(board, ability),
                _skillBoardService.GetAbilityBonus(board, ability),
                growth.GetPeakBonus(ability),
                context.ConditionModifier,
                context.InjuryModifier,
                context.TacticalModifier);
        }
    }
}
