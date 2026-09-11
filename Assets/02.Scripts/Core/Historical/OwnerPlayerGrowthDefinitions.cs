using System;
using System.Collections.Generic;
using Baseball.Core.Growth;
using Baseball.Core.Players;

namespace Baseball.Core.Historical
{
    /// <summary>카드 유학 한 과정의 비용·기간·확정 성장량을 정의한다.</summary>
    public sealed class CardStudyProgramDefinition
    {
        private readonly AbilityChange[] _rewards;

        public CardStudyProgramDefinition(
            string programId,
            string displayName,
            PlayerType playerType,
            int developmentPointCost,
            int durationWeeks,
            IReadOnlyList<AbilityChange> rewards)
        {
            if (string.IsNullOrWhiteSpace(programId)) throw new ArgumentException("ProgramId가 필요합니다.", nameof(programId));
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("표시 이름이 필요합니다.", nameof(displayName));
            if (developmentPointCost <= 0 || durationWeeks <= 0) throw new ArgumentOutOfRangeException(nameof(developmentPointCost));
            if (rewards == null || rewards.Count == 0) throw new ArgumentException("유학 성장 보상이 필요합니다.", nameof(rewards));
            ProgramId = programId.Trim();
            DisplayName = displayName.Trim();
            PlayerType = playerType;
            DevelopmentPointCost = developmentPointCost;
            DurationWeeks = durationWeeks;
            _rewards = new AbilityChange[rewards.Count];
            for (int index = 0; index < rewards.Count; index++)
            {
                if (rewards[index].Amount <= 0) throw new ArgumentException("유학 성장량은 양수여야 합니다.", nameof(rewards));
                _rewards[index] = rewards[index];
            }
        }

        public string ProgramId { get; }
        public string DisplayName { get; }
        public PlayerType PlayerType { get; }
        public int DevelopmentPointCost { get; }
        public int DurationWeeks { get; }
        public IReadOnlyList<AbilityChange> Rewards => _rewards;
    }

    /// <summary>구단주 카드 훈련 12종과 유학 8종을 한 밸런스 계약으로 제공한다.</summary>
    public sealed class OwnerCardGrowthBalanceTable
    {
        private readonly CardTrainingProgramDefinition[] _trainingPrograms;
        private readonly CardStudyProgramDefinition[] _studyPrograms;

        public OwnerCardGrowthBalanceTable(
            IReadOnlyList<CardTrainingProgramDefinition> trainingPrograms,
            IReadOnlyList<CardStudyProgramDefinition> studyPrograms)
        {
            _trainingPrograms = Copy(trainingPrograms, PlayerAbilityCatalog.AbilityCount, nameof(trainingPrograms));
            _studyPrograms = Copy(studyPrograms, 8, nameof(studyPrograms));
        }

        public IReadOnlyList<CardTrainingProgramDefinition> TrainingPrograms => _trainingPrograms;
        public IReadOnlyList<CardStudyProgramDefinition> StudyPrograms => _studyPrograms;

        public CardTrainingProgramDefinition GetTrainingProgram(string programId)
        {
            for (int index = 0; index < _trainingPrograms.Length; index++)
                if (string.Equals(_trainingPrograms[index].ProgramId, programId, StringComparison.Ordinal)) return _trainingPrograms[index];
            throw new KeyNotFoundException($"카드 훈련 프로그램 {programId}을 찾을 수 없습니다.");
        }

        public CardStudyProgramDefinition GetStudyProgram(string programId)
        {
            for (int index = 0; index < _studyPrograms.Length; index++)
                if (string.Equals(_studyPrograms[index].ProgramId, programId, StringComparison.Ordinal)) return _studyPrograms[index];
            throw new KeyNotFoundException($"카드 유학 프로그램 {programId}을 찾을 수 없습니다.");
        }

        public int GetStudyCapacity(int trainingCenterLevel) => Math.Max(0, Math.Min(3, trainingCenterLevel));

        public static OwnerCardGrowthBalanceTable CreateDefault()
        {
            var training = new CardTrainingProgramDefinition[PlayerAbilityCatalog.AbilityCount];
            for (int index = 0; index < training.Length; index++)
            {
                var ability = (PlayerAbility)index;
                training[index] = new CardTrainingProgramDefinition(
                    $"card_training_{ability.ToString().ToLowerInvariant()}", ability, 12, 1);
            }

            return new OwnerCardGrowthBalanceTable(training, new[]
            {
                Study("study_contact", "정교 타격 아카데미", PlayerType.Batter, PlayerAbility.Contact, PlayerAbility.BatterMental),
                Study("study_power", "장타 강화 캠프", PlayerType.Batter, PlayerAbility.Power, PlayerAbility.Speed),
                new CardStudyProgramDefinition("study_defense", "수비 전문 학교", PlayerType.Batter, 100, 4,
                    new[] { new AbilityChange(PlayerAbility.Defense, 3) }),
                Study("study_batter_allround", "야수 실전 리그", PlayerType.Batter, PlayerAbility.Contact, PlayerAbility.Defense),
                Study("study_velocity", "구속 연구소", PlayerType.Pitcher, PlayerAbility.Velocity, PlayerAbility.Stuff),
                Study("study_command", "제구 아카데미", PlayerType.Pitcher, PlayerAbility.Control, PlayerAbility.PitcherMental),
                Study("study_breaking", "변화구 디자인 랩", PlayerType.Pitcher, PlayerAbility.Breaking, PlayerAbility.Stuff),
                Study("study_stamina", "선발 체력 리그", PlayerType.Pitcher, PlayerAbility.Stamina, PlayerAbility.PitcherMental)
            });
        }

        private static CardStudyProgramDefinition Study(
            string id, string name, PlayerType type, PlayerAbility primary, PlayerAbility secondary) =>
            new CardStudyProgramDefinition(id, name, type, 100, 4,
                new[] { new AbilityChange(primary, 2), new AbilityChange(secondary, 1) });

        private static T[] Copy<T>(IReadOnlyList<T> source, int expectedCount, string name) where T : class
        {
            if (source == null || source.Count != expectedCount)
                throw new ArgumentException($"{expectedCount}개 정의가 필요합니다.", name);
            var result = new T[source.Count];
            for (int index = 0; index < result.Length; index++)
                result[index] = source[index] ?? throw new ArgumentException("null 정의가 있습니다.", name);
            return result;
        }
    }

    /// <summary>한 선수 카드의 4×4 성장판에 장착된 블록만 저장한다.</summary>
    public sealed class OwnedCardSkillBoardState
    {
        private readonly List<PlacedSkillBlock> _placements = new List<PlacedSkillBlock>();

        public IReadOnlyList<PlacedSkillBlock> Placements => _placements;

        public void Add(PlacedSkillBlock placement)
        {
            if (FindIndex(placement.Instance.InstanceId) >= 0)
                throw new InvalidOperationException("같은 블록 인스턴스를 한 카드에 두 번 장착할 수 없습니다.");
            _placements.Add(placement);
        }

        public bool Remove(int instanceId)
        {
            int index = FindIndex(instanceId);
            if (index < 0) return false;
            _placements.RemoveAt(index);
            return true;
        }

        private int FindIndex(int instanceId)
        {
            for (int index = 0; index < _placements.Count; index++)
                if (_placements[index].Instance.InstanceId == instanceId) return index;
            return -1;
        }
    }

    /// <summary>구단 전체가 공유하는 스킬 블록 인벤토리와 뽑기 보장 카운트를 저장한다.</summary>
    public sealed class OwnerSkillBlockInventoryState
    {
        private readonly List<SkillBlockInstance> _blocks = new List<SkillBlockInstance>();
        private int _nextInstanceId = 1;

        public IReadOnlyList<SkillBlockInstance> Blocks => _blocks;
        public int PityEliteCount { get; private set; }
        public int PityUniqueCount { get; private set; }
        public int PityLegendaryCount { get; private set; }
        public int TotalPullCount { get; private set; }

        public SkillBlockInstance Add(string definitionId)
        {
            var instance = new SkillBlockInstance(_nextInstanceId++, definitionId);
            _blocks.Add(instance);
            return instance;
        }

        public void Restore(SkillBlockInstance instance)
        {
            if (Contains(instance.InstanceId)) throw new InvalidOperationException("스킬 블록 InstanceId가 중복되었습니다.");
            _blocks.Add(instance);
            _nextInstanceId = Math.Max(_nextInstanceId, instance.InstanceId + 1);
        }

        public bool Contains(int instanceId)
        {
            for (int index = 0; index < _blocks.Count; index++)
                if (_blocks[index].InstanceId == instanceId) return true;
            return false;
        }

        public SkillBlockInstance GetRequired(int instanceId)
        {
            for (int index = 0; index < _blocks.Count; index++)
                if (_blocks[index].InstanceId == instanceId) return _blocks[index];
            throw new KeyNotFoundException($"스킬 블록 인스턴스 {instanceId}을 찾을 수 없습니다.");
        }

        public void RecordPull(SkillBlockRarity rarity)
        {
            TotalPullCount++;
            PityEliteCount = rarity >= SkillBlockRarity.Elite ? 0 : PityEliteCount + 1;
            PityUniqueCount = rarity >= SkillBlockRarity.Unique ? 0 : PityUniqueCount + 1;
            PityLegendaryCount = rarity == SkillBlockRarity.Legendary ? 0 : PityLegendaryCount + 1;
        }

        public void RestorePity(int elite, int unique, int legendary, int total)
        {
            if (elite < 0 || unique < 0 || legendary < 0 || total < 0) throw new ArgumentOutOfRangeException(nameof(elite));
            PityEliteCount = elite;
            PityUniqueCount = unique;
            PityLegendaryCount = legendary;
            TotalPullCount = total;
        }
    }

    /// <summary>진행 중인 카드 유학 한 건을 주 단위로 저장한다.</summary>
    public sealed class CardStudyProjectState
    {
        public CardStudyProjectState(string cardId, string programId, int startedSeason, int remainingWeeks)
        {
            if (string.IsNullOrWhiteSpace(cardId) || string.IsNullOrWhiteSpace(programId)) throw new ArgumentException("유학 식별자가 필요합니다.");
            if (remainingWeeks <= 0) throw new ArgumentOutOfRangeException(nameof(remainingWeeks));
            CardId = cardId.Trim();
            ProgramId = programId.Trim();
            StartedSeason = startedSeason;
            RemainingWeeks = remainingWeeks;
        }

        public string CardId { get; }
        public string ProgramId { get; }
        public int StartedSeason { get; }
        public int RemainingWeeks { get; private set; }
        public bool AdvanceWeek() => --RemainingWeeks == 0;
    }

    /// <summary>구단주 모드 카드 성장의 공유 인벤토리와 진행 중 유학을 소유한다.</summary>
    public sealed class OwnerPlayerGrowthState
    {
        private readonly List<CardStudyProjectState> _studyProjects = new List<CardStudyProjectState>();

        public OwnerPlayerGrowthState(OwnerSkillBlockInventoryState inventory = null) =>
            Inventory = inventory ?? new OwnerSkillBlockInventoryState();

        public OwnerSkillBlockInventoryState Inventory { get; }
        public IReadOnlyList<CardStudyProjectState> StudyProjects => _studyProjects;
        public void AddStudy(CardStudyProjectState project) => _studyProjects.Add(project ?? throw new ArgumentNullException(nameof(project)));
        public void RemoveStudyAt(int index) => _studyProjects.RemoveAt(index);
    }
}
