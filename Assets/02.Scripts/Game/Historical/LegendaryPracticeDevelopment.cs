using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Simulation.Historical;

namespace Baseball.Game.Historical
{
    /// <summary>역사 순위 확정 후 적용하는 도전용 성장 단계다. 순위 산정 경기와 분리한다.</summary>
    [Serializable]
    public sealed class LegendaryPracticeDevelopmentTier
    {
        public int maximumRank, blockCount, enhancement, studyCompletions;
        public SkillBlockRarity blockRarity;
        public CardTraitRank traitRank;
        public CardStudyUnlockKind studyTier;
    }

    /// <summary>연습경기 상대의 성장 강도를 JSON에서 주입한다.</summary>
    [Serializable]
    public sealed class LegendaryPracticeDevelopmentBalance
    {
        public LegendaryPracticeDevelopmentTier[] tiers;

        /// <summary>100위 전체를 덮고 상위권 성장 강도가 내려가지 않는지 검사한다.</summary>
        public void Validate()
        {
            if (tiers == null || tiers.Length == 0) throw new ArgumentException("연습경기 성장 단계가 없습니다.");
            int previous = 0;
            foreach (var tier in tiers)
            {
                if (tier == null || tier.maximumRank <= previous || tier.maximumRank > 100 ||
                    tier.blockCount < 1 || tier.studyCompletions < 1 || tier.enhancement < 1 ||
                    tier.enhancement > OwnedPlayerCardState.MaximumEnhancementLevel ||
                    !Enum.IsDefined(typeof(SkillBlockRarity), tier.blockRarity) ||
                    !Enum.IsDefined(typeof(CardStudyUnlockKind), tier.studyTier) ||
                    tier.traitRank < CardTraitRank.C || tier.traitRank > CardTraitRank.S)
                    throw new ArgumentException("연습경기 성장 단계가 올바르지 않습니다.");
                previous = tier.maximumRank;
            }
            if (previous != 100) throw new ArgumentException("100위까지 성장 단계를 지정해야 합니다.");
            for (int i = 1; i < tiers.Length; i++)
                if (tiers[i].blockCount > tiers[i - 1].blockCount || tiers[i].blockRarity > tiers[i - 1].blockRarity ||
                    tiers[i].enhancement > tiers[i - 1].enhancement || tiers[i].studyCompletions > tiers[i - 1].studyCompletions ||
                    tiers[i].studyTier > tiers[i - 1].studyTier ||
                    tiers[i].traitRank > tiers[i - 1].traitRank)
                    throw new ArgumentException("상위 순위의 성장 단계가 더 낮을 수 없습니다.");
        }

        /// <summary>확정된 역사 순위가 속한 성장 단계를 찾는다.</summary>
        public LegendaryPracticeDevelopmentTier GetTier(int rank)
        {
            if (rank < 1 || rank > 100) throw new ArgumentOutOfRangeException(nameof(rank));
            foreach (var tier in tiers) if (rank <= tier.maximumRank) return tier;
            throw new InvalidOperationException("순위 성장 단계가 없습니다.");
        }
    }

    /// <summary>플레이어 재화와 보관함을 건드리지 않고 실제 카드 성장 상태를 결정론적으로 구성한다.</summary>
    public sealed class LegendaryPracticeDevelopment
    {
        private readonly BalanceTable _balance;
        private readonly LegendaryPracticeDevelopmentBalance _development;

        public LegendaryPracticeDevelopment(BalanceTable balance, LegendaryPracticeDevelopmentBalance development)
        {
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            _development = development ?? throw new ArgumentNullException(nameof(development));
            development.Validate();
        }

        /// <summary>장착·유학 완료·특성·강화를 포함한 독립된 상대 선수 상태를 만든다.</summary>
        public OwnedPlayerCardState Create(PlayerCardDefinition card, PlayerSeasonDefinition season, int rank)
        {
            var tier = _development.GetTier(rank);
            var owned = new OwnedPlayerCardState(card.CardId, tier.enhancement);
            EquipBlocks(owned, season, tier);
            CompleteStudies(owned, season, tier);
            // 여러 시즌에 걸친 완료 상태다. 같은 오프시즌의 유학·특성 중복 참가를 만들지 않는다.
            var trait = SelectTrait(season);
            owned.Trait = new PlayerTraitProgress { trait = trait.kind, rank = tier.traitRank,
                experience = _balance.TraitTraining.experience[(int)tier.traitRank - 1],
                trainingSeason = tier.studyCompletions + 1 };
            owned.Trait.Validate();
            return owned;
        }

        private void EquipBlocks(OwnedPlayerCardState owned, PlayerSeasonDefinition season, LegendaryPracticeDevelopmentTier tier)
        {
            var candidates = new List<SkillBlockDefinition>();
            foreach (var block in _balance.Growth.SkillBlocks)
                if (block.Rarity == tier.blockRarity && SkillBlockCategoryCatalog.IsAvailableTo(block.Category, season.PlayerType))
                    candidates.Add(block);
            candidates.Sort((a, b) =>
            {
                // 네 블록을 채우는 상위권도 빈 틈 없이 들어가도록 같은 등급의 정사각형 모양을 먼저 쓴다.
                int shape = IsSquare(b).CompareTo(IsSquare(a));
                if (shape != 0) return shape;
                int score = Score(b.AbilityBonuses, season).CompareTo(Score(a.AbilityBonuses, season));
                return score != 0 ? score : string.CompareOrdinal(a.BlockId, b.BlockId);
            });
            var inventory = new OwnerSkillBlockInventoryState();
            var service = new OwnerSkillBoardService(_balance.Growth);
            foreach (var block in candidates)
            {
                var instance = inventory.Add(block.BlockId);
                service.TryPlaceFirstAvailable(inventory, owned.SkillBoard, instance.InstanceId);
                if (owned.SkillBoard.Placements.Count == tier.blockCount) return;
            }
            throw new InvalidOperationException("연습경기 성장 설정의 스킬블록을 보드에 배치할 수 없습니다.");
        }

        private static bool IsSquare(SkillBlockDefinition block)
        {
            foreach (var cell in block.ShapeCells) if (cell.X > 1 || cell.Y > 1) return false;
            return true;
        }

        private static int Score(IReadOnlyList<AbilityChange> rewards, PlayerSeasonDefinition season)
        {
            int score = 0;
            var attributes = season.CreateBaseAttributes();
            foreach (var reward in rewards) score += reward.Amount * attributes.Get(reward.Ability);
            return score;
        }

        private void CompleteStudies(OwnedPlayerCardState owned, PlayerSeasonDefinition season, LegendaryPracticeDevelopmentTier tier)
        {
            for (int completed = 0; completed < tier.studyCompletions; completed++)
            {
                CardStudyProgramDefinition best = null;
                int bestScore = -1;
                var source = season.CreateBaseAttributes();
                var ceiling = season.CreateTrainingCeiling();
                foreach (var program in _balance.OwnerCardGrowth.StudyPrograms)
                {
                    if (program.PlayerType != season.PlayerType || program.UnlockRequirement.Kind > tier.studyTier) continue;
                    int score = 0;
                    foreach (var reward in program.Rewards)
                        score += Math.Min(reward.Amount, Math.Max(0, ceiling.Get(reward.Ability) -
                            source.Get(reward.Ability) - owned.Training.GetBonus(reward.Ability))) * source.Get(reward.Ability);
                    if (score > bestScore || score == bestScore && string.CompareOrdinal(program.ProgramId, best.ProgramId) < 0)
                    { best = program; bestScore = score; }
                }
                if (best == null) throw new InvalidOperationException("연습경기 선수 유형의 유학 과정이 없습니다.");
                OwnerCardStudyResolver.Complete(owned, season, best);
                owned.RecordStudySeason(completed + 1);
            }
        }

        private CardTraitDefinition SelectTrait(PlayerSeasonDefinition season)
        {
            CardTraitDefinition best = null;
            var attributes = season.CreateBaseAttributes();
            foreach (var trait in _balance.TraitTraining.definitions)
            {
                if (trait.playerType != season.PlayerType ||
                    trait.position != PlayerPosition.Unknown && trait.position != season.Position ||
                    attributes.Get(trait.ability) < trait.minimumAbility) continue;
                // 조건이 같은 후보는 안정적인 enum 순서로 고정한다. 역할 제한은 정규 특성 정의를 따른다.
                if (best == null || trait.kind < best.kind) best = trait;
            }
            return best ?? throw new InvalidOperationException("연습경기 선수에게 맞는 특성이 없습니다.");
        }
    }
}
