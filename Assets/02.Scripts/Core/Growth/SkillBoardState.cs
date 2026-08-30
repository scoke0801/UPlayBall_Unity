using System;
using System.Collections.Generic;

namespace Baseball.Core.Growth
{
    /// <summary>
    /// 4×4 보드 크기와 Trait Socket 위치를 정의한다.
    /// </summary>
    public sealed class SkillBoardDefinition
    {
        public SkillBoardDefinition(string boardDefinitionId, int width, int height, BoardCell[] traitSockets)
        {
            if (string.IsNullOrWhiteSpace(boardDefinitionId))
                throw new ArgumentException("BoardDefinitionId는 비어 있을 수 없습니다.", nameof(boardDefinitionId));
            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException(nameof(width));
            BoardDefinitionId = boardDefinitionId;
            Width = width;
            Height = height;
            TraitSockets = traitSockets ?? Array.Empty<BoardCell>();
            for (int index = 0; index < TraitSockets.Length; index++)
            {
                if (TraitSockets[index].X < 0 || TraitSockets[index].X >= width ||
                    TraitSockets[index].Y < 0 || TraitSockets[index].Y >= height)
                    throw new ArgumentOutOfRangeException(nameof(traitSockets));
            }
        }

        public string BoardDefinitionId { get; }
        public int Width { get; }
        public int Height { get; }
        public BoardCell[] TraitSockets { get; }

        public static SkillBoardDefinition CreateDefault()
        {
            return new SkillBoardDefinition(
                "standard_4x4",
                4,
                4,
                new[] { new BoardCell(1, 1), new BoardCell(2, 2) });
        }
    }

    public readonly struct SkillBlockInstance
    {
        public SkillBlockInstance(int instanceId, string definitionId)
        {
            if (instanceId <= 0)
                throw new ArgumentOutOfRangeException(nameof(instanceId));
            if (string.IsNullOrWhiteSpace(definitionId))
                throw new ArgumentException("DefinitionId는 비어 있을 수 없습니다.", nameof(definitionId));
            InstanceId = instanceId;
            DefinitionId = definitionId;
        }

        public int InstanceId { get; }
        public string DefinitionId { get; }
    }

    public readonly struct PlacedSkillBlock
    {
        public PlacedSkillBlock(SkillBlockInstance instance, int originX, int originY, int rotationQuarterTurns)
        {
            if (rotationQuarterTurns < 0 || rotationQuarterTurns > 3)
                throw new ArgumentOutOfRangeException(nameof(rotationQuarterTurns));
            Instance = instance;
            OriginX = originX;
            OriginY = originY;
            RotationQuarterTurns = rotationQuarterTurns;
        }

        public SkillBlockInstance Instance { get; }
        public int OriginX { get; }
        public int OriginY { get; }
        public int RotationQuarterTurns { get; }
    }

    /// <summary>
    /// 보유 블록·배치·보장 카운트와 재설계 이력을 저장한다.
    /// </summary>
    public sealed class SkillBoardState
    {
        private readonly List<SkillBlockInstance> _ownedBlocks = new List<SkillBlockInstance>();
        private readonly List<PlacedSkillBlock> _placedBlocks = new List<PlacedSkillBlock>();
        private readonly List<PlacedSkillBlock> _activePlacements = new List<PlacedSkillBlock>();
        private readonly HashSet<int> _lockedBlockIds = new HashSet<int>();
        private int _nextInstanceId = 1;

        public SkillBoardState(string boardDefinitionId)
        {
            BoardDefinitionId = boardDefinitionId ?? throw new ArgumentNullException(nameof(boardDefinitionId));
        }

        public string BoardDefinitionId { get; }
        public IReadOnlyList<SkillBlockInstance> OwnedBlocks => _ownedBlocks;
        public IReadOnlyList<PlacedSkillBlock> PlacedBlocks => _placedBlocks;
        public IReadOnlyList<PlacedSkillBlock> AppliedBlocks => IsSeasonLocked ? _activePlacements : _placedBlocks;
        public bool IsSeasonLocked { get; private set; }
        public int PityEliteCount { get; private set; }
        public int PityUniqueCount { get; private set; }
        public int PityLegendaryCount { get; private set; }
        public int TotalPullCount { get; private set; }
        public int LastRedesignSeason { get; private set; }
        public int LimitedPurchaseSeason { get; private set; }
        public int UniquePurchasesThisOffseason { get; private set; }
        public int LegendaryPurchasesThisOffseason { get; private set; }

        public SkillBlockInstance AddOwnedBlock(string definitionId)
        {
            var instance = new SkillBlockInstance(_nextInstanceId++, definitionId);
            _ownedBlocks.Add(instance);
            return instance;
        }

        public void AddOwnedBlock(SkillBlockInstance instance)
        {
            if (instance.InstanceId <= 0)
                throw new ArgumentOutOfRangeException(nameof(instance));
            if (ContainsInstance(instance.InstanceId))
                throw new InvalidOperationException("같은 InstanceId의 블록을 중복으로 추가할 수 없습니다.");
            _ownedBlocks.Add(instance);
            _nextInstanceId = Math.Max(_nextInstanceId, instance.InstanceId + 1);
        }

        public void RecordPull(SkillBlockRarity rarity)
        {
            TotalPullCount++;
            if (rarity >= SkillBlockRarity.Elite)
                PityEliteCount = 0;
            else
                PityEliteCount++;

            if (rarity >= SkillBlockRarity.Unique)
                PityUniqueCount = 0;
            else
                PityUniqueCount++;

            if (rarity == SkillBlockRarity.Legendary)
                PityLegendaryCount = 0;
            else
                PityLegendaryCount++;
        }

        public int GetLimitedPurchaseCount(SkillGachaPurchaseTier tier, int seasonYear)
        {
            if (LimitedPurchaseSeason != seasonYear)
                return 0;
            return tier switch
            {
                SkillGachaPurchaseTier.Unique => UniquePurchasesThisOffseason,
                SkillGachaPurchaseTier.Legendary => LegendaryPurchasesThisOffseason,
                _ => 0
            };
        }

        public void RecordTierPurchases(SkillGachaPurchaseTier tier, int seasonYear, int count)
        {
            if (count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (LimitedPurchaseSeason != seasonYear)
            {
                LimitedPurchaseSeason = seasonYear;
                UniquePurchasesThisOffseason = 0;
                LegendaryPurchasesThisOffseason = 0;
            }
            if (tier == SkillGachaPurchaseTier.Unique)
                UniquePurchasesThisOffseason += count;
            else if (tier == SkillGachaPurchaseTier.Legendary)
                LegendaryPurchasesThisOffseason += count;
        }

        public bool IsBlockLocked(int instanceId)
        {
            return _lockedBlockIds.Contains(instanceId);
        }

        public void SetBlockLocked(int instanceId, bool isLocked)
        {
            if (!ContainsInstance(instanceId))
                throw new ArgumentException("보유하거나 장착한 블록을 찾을 수 없습니다.", nameof(instanceId));
            if (isLocked)
                _lockedBlockIds.Add(instanceId);
            else
                _lockedBlockIds.Remove(instanceId);
        }

        public SkillBlockInstance FindOwnedBlock(int instanceId)
        {
            for (int index = 0; index < _ownedBlocks.Count; index++)
            {
                if (_ownedBlocks[index].InstanceId == instanceId)
                    return _ownedBlocks[index];
            }
            return default;
        }

        public void PlaceOwnedBlock(PlacedSkillBlock placement)
        {
            EnsureEditable();
            int ownedIndex = FindOwnedIndex(placement.Instance.InstanceId);
            if (ownedIndex < 0)
                throw new InvalidOperationException("보유 중인 블록만 장착할 수 있습니다.");
            _ownedBlocks.RemoveAt(ownedIndex);
            _placedBlocks.Add(placement);
        }

        public SkillBlockInstance RemovePlacedBlock(int instanceId, bool returnToInventory)
        {
            EnsureEditable();
            for (int index = 0; index < _placedBlocks.Count; index++)
            {
                if (_placedBlocks[index].Instance.InstanceId != instanceId)
                    continue;
                SkillBlockInstance instance = _placedBlocks[index].Instance;
                _placedBlocks.RemoveAt(index);
                if (returnToInventory)
                    _ownedBlocks.Add(instance);
                else
                    _lockedBlockIds.Remove(instanceId);
                return instance;
            }
            throw new ArgumentException("장착된 블록을 찾을 수 없습니다.", nameof(instanceId));
        }

        public SkillBlockInstance RemoveOwnedBlock(int instanceId)
        {
            int index = FindOwnedIndex(instanceId);
            if (index < 0)
                throw new ArgumentException("보유 블록을 찾을 수 없습니다.", nameof(instanceId));
            SkillBlockInstance instance = _ownedBlocks[index];
            _ownedBlocks.RemoveAt(index);
            _lockedBlockIds.Remove(instanceId);
            return instance;
        }

        public void Redesign(int seasonYear)
        {
            EnsureEditable();
            if (LastRedesignSeason == seasonYear)
                throw new InvalidOperationException("전문 재설계는 오프시즌당 한 번만 가능합니다.");
            for (int index = 0; index < _placedBlocks.Count; index++)
                _ownedBlocks.Add(_placedBlocks[index].Instance);
            _placedBlocks.Clear();
            LastRedesignSeason = seasonYear;
        }

        /// <summary>개막 시점 배치를 복사해 역할 평가와 경기에서 같은 성장판을 사용하게 한다.</summary>
        public void LockForSeason()
        {
            _activePlacements.Clear();
            for (int index = 0; index < _placedBlocks.Count; index++)
                _activePlacements.Add(_placedBlocks[index]);
            IsSeasonLocked = true;
        }

        public void UnlockForOffseason()
        {
            IsSeasonLocked = false;
            _activePlacements.Clear();
        }

        private void EnsureEditable()
        {
            if (IsSeasonLocked)
                throw new InvalidOperationException("정규시즌에는 확정된 성장판을 변경할 수 없습니다.");
        }

        private int FindOwnedIndex(int instanceId)
        {
            for (int index = 0; index < _ownedBlocks.Count; index++)
            {
                if (_ownedBlocks[index].InstanceId == instanceId)
                    return index;
            }
            return -1;
        }

        private bool ContainsInstance(int instanceId)
        {
            if (FindOwnedIndex(instanceId) >= 0)
                return true;
            for (int index = 0; index < _placedBlocks.Count; index++)
            {
                if (_placedBlocks[index].Instance.InstanceId == instanceId)
                    return true;
            }
            return false;
        }
    }
}
