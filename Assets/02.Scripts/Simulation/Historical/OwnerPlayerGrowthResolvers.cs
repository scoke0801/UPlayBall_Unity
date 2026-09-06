using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Simulation.Growth;
using Baseball.Simulation.Random;

namespace Baseball.Simulation.Historical
{
    /// <summary>훈련·Edition·강화·카드별 성장판을 합치는 구단주 카드 능력치 단일 진입점이다.</summary>
    public sealed class OwnerCardAbilityResolver
    {
        private readonly SkillBoardService _skillBoardService;

        public OwnerCardAbilityResolver(GrowthBalanceTable growth)
        {
            if (growth == null) throw new ArgumentNullException(nameof(growth));
            _skillBoardService = new SkillBoardService(growth.SkillBoard, growth.SkillBlocks);
        }

        public int ResolveRawPermanent(
            PlayerSeasonDefinition season,
            PlayerCardDefinition card,
            OwnedPlayerCardState owned,
            PlayerAbility ability)
        {
            if (season == null) throw new ArgumentNullException(nameof(season));
            if (card == null) throw new ArgumentNullException(nameof(card));
            int baseRating = season.CreateBaseAttributes().Get(ability);
            if (owned == null) return checked(baseRating + card.GetModifier(ability));
            int ceiling = season.CreateTrainingCeiling().Get(ability);
            int development = Math.Min(ceiling, checked(baseRating + owned.Training.GetBonus(ability)));
            int board = _skillBoardService.GetAbilityBonus(owned.SkillBoard.Placements, ability);
            return checked(development + card.GetModifier(ability) + owned.EnhancementLevel + board);
        }

        public AbilityRatings ResolvePermanent(
            PlayerSeasonDefinition season,
            PlayerCardDefinition card,
            OwnedPlayerCardState owned)
        {
            var values = new int[PlayerAbilityCatalog.AbilityCount];
            for (int index = 0; index < PlayerAbilityCatalog.AbilityCount; index++)
            {
                var ability = (PlayerAbility)index;
                values[index] = Math.Max(AbilityRatings.Minimum,
                    Math.Min(AbilityRatings.Maximum, ResolveRawPermanent(season, card, owned, ability)));
            }
            return new AbilityRatings(values);
        }

        public string[] ResolveActiveTraitIds(OwnedPlayerCardState owned) =>
            owned == null ? Array.Empty<string>() : _skillBoardService.GetActiveTraitIds(owned.SkillBoard.Placements);
    }

    /// <summary>공용 SkillBoardService의 회전·경계·겹침 규칙으로 카드별 배치를 검증한다.</summary>
    public sealed class OwnerSkillBoardService
    {
        private readonly GrowthBalanceTable _growth;
        private readonly SkillBoardService _service;

        public OwnerSkillBoardService(GrowthBalanceTable growth)
        {
            _growth = growth ?? throw new ArgumentNullException(nameof(growth));
            _service = new SkillBoardService(growth.SkillBoard, growth.SkillBlocks);
        }

        public void Place(
            OwnerSkillBlockInventoryState inventory,
            OwnedCardSkillBoardState board,
            int instanceId,
            int originX,
            int originY,
            int rotationQuarterTurns)
        {
            if (inventory == null || board == null) throw new ArgumentNullException(nameof(inventory));
            SkillBlockInstance instance = inventory.GetRequired(instanceId);
            SkillBoardState validation = BuildValidationState(inventory, board);
            _service.PlaceBlock(validation, instanceId, originX, originY, rotationQuarterTurns);
            board.Add(new PlacedSkillBlock(instance, originX, originY, rotationQuarterTurns));
        }

        public bool TryPlaceFirstAvailable(
            OwnerSkillBlockInventoryState inventory,
            OwnedCardSkillBoardState board,
            int instanceId)
        {
            SkillBlockDefinition definition = FindDefinition(inventory.GetRequired(instanceId).DefinitionId);
            int rotationCount = definition.CanRotate ? 4 : 1;
            for (int rotation = 0; rotation < rotationCount; rotation++)
            for (int y = 0; y < _growth.SkillBoard.Height; y++)
            for (int x = 0; x < _growth.SkillBoard.Width; x++)
            {
                try
                {
                    Place(inventory, board, instanceId, x, y, rotation);
                    return true;
                }
                catch (InvalidOperationException)
                {
                }
            }
            return false;
        }

        public bool Remove(OwnedCardSkillBoardState board, int instanceId) =>
            (board ?? throw new ArgumentNullException(nameof(board))).Remove(instanceId);

        private SkillBoardState BuildValidationState(OwnerSkillBlockInventoryState inventory, OwnedCardSkillBoardState board)
        {
            var validation = new SkillBoardState(_growth.SkillBoard.BoardDefinitionId);
            for (int index = 0; index < inventory.Blocks.Count; index++) validation.AddOwnedBlock(inventory.Blocks[index]);
            for (int index = 0; index < board.Placements.Count; index++)
            {
                PlacedSkillBlock placement = board.Placements[index];
                _service.PlaceBlock(validation, placement.Instance.InstanceId,
                    placement.OriginX, placement.OriginY, placement.RotationQuarterTurns);
            }
            return validation;
        }

        private SkillBlockDefinition FindDefinition(string definitionId)
        {
            for (int index = 0; index < _growth.SkillBlocks.Length; index++)
                if (string.Equals(_growth.SkillBlocks[index].BlockId, definitionId, StringComparison.Ordinal)) return _growth.SkillBlocks[index];
            throw new InvalidOperationException($"스킬 블록 정의 {definitionId}을 찾을 수 없습니다.");
        }
    }

    /// <summary>구단주 전용 공유 인벤토리에 공개 확률과 Pity를 적용해 스킬 블록을 지급한다.</summary>
    public sealed class OwnerSkillGachaResolver
    {
        private readonly SkillGachaBalanceTable _balance;
        private readonly SkillBlockDefinition[] _definitions;

        public OwnerSkillGachaResolver(GrowthBalanceTable growth)
        {
            if (growth == null) throw new ArgumentNullException(nameof(growth));
            _balance = growth.SkillGacha;
            _definitions = growth.SkillBlocks;
        }

        public SkillBlockInstance[] Draw(
            OwnerSkillBlockInventoryState inventory,
            SkillGachaPurchaseTier tier,
            int count,
            IRandomSource random)
        {
            if (inventory == null || random == null) throw new ArgumentNullException(nameof(inventory));
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
            var result = new SkillBlockInstance[count];
            int categoryCount = Enum.GetValues(typeof(SkillBlockCategory)).Length;
            for (int index = 0; index < count; index++)
            {
                SkillBlockRarity rarity = RollRarity(inventory, tier, random);
                SkillBlockRarity minimum = _balance.GetOffer(tier).MinimumRarity;
                if (rarity < minimum) rarity = minimum;
                var category = (SkillBlockCategory)Math.Min((int)(random.NextDouble() * categoryCount), categoryCount - 1);
                SkillBlockDefinition definition = SelectDefinition(category, rarity, random);
                inventory.RecordPull(definition.Rarity);
                result[index] = inventory.Add(definition.BlockId);
            }
            return result;
        }

        private SkillBlockRarity RollRarity(OwnerSkillBlockInventoryState inventory, SkillGachaPurchaseTier tier, IRandomSource random)
        {
            if (inventory.PityLegendaryCount >= _balance.LegendaryPity) return SkillBlockRarity.Legendary;
            if (inventory.PityUniqueCount >= _balance.UniquePity) return SkillBlockRarity.Unique;
            if (inventory.PityEliteCount >= _balance.ElitePity) return SkillBlockRarity.Elite;
            double roll = random.NextDouble();
            for (int rarity = 0; rarity <= (int)SkillBlockRarity.Legendary; rarity++)
            {
                roll -= _balance.GetProbability(tier, (SkillBlockRarity)rarity);
                if (roll < 0d) return (SkillBlockRarity)rarity;
            }
            return SkillBlockRarity.Legendary;
        }

        private SkillBlockDefinition SelectDefinition(SkillBlockCategory category, SkillBlockRarity rarity, IRandomSource random)
        {
            int count = 0;
            for (int index = 0; index < _definitions.Length; index++)
                if (_definitions[index].Category == category && _definitions[index].Rarity == rarity) count++;
            if (count == 0) throw new InvalidOperationException($"{category} {rarity} 스킬 블록 풀이 비어 있습니다.");
            int selected = Math.Min((int)(random.NextDouble() * count), count - 1);
            for (int index = 0; index < _definitions.Length; index++)
                if (_definitions[index].Category == category && _definitions[index].Rarity == rarity && selected-- == 0) return _definitions[index];
            throw new InvalidOperationException("스킬 블록 선택에 실패했습니다.");
        }
    }

    public readonly struct CardStudyCompletion
    {
        public CardStudyCompletion(string cardId, string programId, IReadOnlyList<AbilityChange> changes)
        {
            CardId = cardId;
            ProgramId = programId;
            Changes = changes;
        }
        public string CardId { get; }
        public string ProgramId { get; }
        public IReadOnlyList<AbilityChange> Changes { get; }
    }

    /// <summary>유학 시작과 주간 완료를 TrainingCeiling 안에서 결정론적으로 처리한다.</summary>
    public static class OwnerCardStudyResolver
    {
        public static void Start(
            OwnerPlayerGrowthState growth,
            OwnedPlayerCardState card,
            PlayerSeasonDefinition season,
            CardStudyProgramDefinition program,
            ManagerEconomyState economy,
            int seasonNumber,
            int capacity)
        {
            if (growth == null || card == null || season == null || program == null || economy == null)
                throw new ArgumentNullException(nameof(growth));
            if (season.PlayerType != program.PlayerType) throw new InvalidOperationException("선수 유형과 유학 과정이 맞지 않습니다.");
            if (card.LastStudySeason == seasonNumber) throw new InvalidOperationException("이 카드는 이번 시즌에 이미 유학을 사용했습니다.");
            if (growth.StudyProjects.Count >= capacity) throw new InvalidOperationException("TrainingCenter 유학 정원이 가득 찼습니다.");
            for (int index = 0; index < growth.StudyProjects.Count; index++)
                if (string.Equals(growth.StudyProjects[index].CardId, card.CardId, StringComparison.Ordinal))
                    throw new InvalidOperationException("이 카드는 이미 유학 중입니다.");
            if (!HasGrowthHeadroom(card, season, program))
                throw new InvalidOperationException("이 유학 과정의 대상 능력치가 모두 TrainingCeiling에 도달했습니다.");
            if (!economy.TrySpendDevelopmentPoints(program.DevelopmentPointCost))
                throw new InvalidOperationException("유학에 필요한 DP가 부족합니다.");
            growth.AddStudy(new CardStudyProjectState(card.CardId, program.ProgramId, seasonNumber, program.DurationWeeks));
            card.RecordStudySeason(seasonNumber);
        }

        private static bool HasGrowthHeadroom(
            OwnedPlayerCardState card,
            PlayerSeasonDefinition season,
            CardStudyProgramDefinition program)
        {
            AbilityRatings bases = season.CreateBaseAttributes();
            AbilityRatings ceilings = season.CreateTrainingCeiling();
            for (int index = 0; index < program.Rewards.Count; index++)
            {
                PlayerAbility ability = program.Rewards[index].Ability;
                if (bases.Get(ability) + card.Training.GetBonus(ability) < ceilings.Get(ability)) return true;
            }
            return false;
        }

        public static CardStudyCompletion Complete(
            OwnedPlayerCardState card,
            PlayerSeasonDefinition season,
            CardStudyProgramDefinition program)
        {
            var applied = new List<AbilityChange>(program.Rewards.Count);
            AbilityRatings bases = season.CreateBaseAttributes();
            AbilityRatings ceilings = season.CreateTrainingCeiling();
            for (int index = 0; index < program.Rewards.Count; index++)
            {
                AbilityChange reward = program.Rewards[index];
                int current = bases.Get(reward.Ability) + card.Training.GetBonus(reward.Ability);
                int gained = Math.Min(reward.Amount, Math.Max(0, ceilings.Get(reward.Ability) - current));
                if (gained <= 0) continue;
                card.Training.AddBonus(reward.Ability, gained);
                applied.Add(new AbilityChange(reward.Ability, gained));
            }
            return new CardStudyCompletion(card.CardId, program.ProgramId, applied);
        }
    }
}
