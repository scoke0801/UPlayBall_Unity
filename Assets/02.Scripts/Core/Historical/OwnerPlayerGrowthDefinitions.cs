using System;
using System.Collections.Generic;
using Baseball.Core.Growth;
using Baseball.Core.Players;

namespace Baseball.Core.Historical
{
    /// <summary>유학지 해금에 사용하는 구단 성취 종류다.</summary>
    public enum CardStudyUnlockKind
    {
        Always,
        ReachLeagueGrade,
        WinPostseason
    }

    /// <summary>유학 과정의 해금 성취를 리그 진행 상태와 비교하는 순수 규칙이다.</summary>
    public readonly struct CardStudyUnlockRequirement
    {
        public CardStudyUnlockRequirement(
            CardStudyUnlockKind kind,
            LeagueGrade requiredLeagueGrade = LeagueGrade.Rookie,
            int requiredChampionships = 0)
        {
            if (!Enum.IsDefined(typeof(CardStudyUnlockKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (!Enum.IsDefined(typeof(LeagueGrade), requiredLeagueGrade))
                throw new ArgumentOutOfRangeException(nameof(requiredLeagueGrade));
            if (kind == CardStudyUnlockKind.WinPostseason && requiredChampionships <= 0)
                throw new ArgumentOutOfRangeException(nameof(requiredChampionships));
            if (kind != CardStudyUnlockKind.WinPostseason && requiredChampionships != 0)
                throw new ArgumentException("우승 횟수는 포스트시즌 우승 조건에서만 사용합니다.", nameof(requiredChampionships));

            Kind = kind;
            RequiredLeagueGrade = requiredLeagueGrade;
            RequiredChampionships = requiredChampionships;
        }

        public CardStudyUnlockKind Kind { get; }
        public LeagueGrade RequiredLeagueGrade { get; }
        public int RequiredChampionships { get; }

        public bool IsSatisfied(LeagueGrade highestLeagueGrade, int postseasonChampionships)
        {
            if (!Enum.IsDefined(typeof(LeagueGrade), highestLeagueGrade) || postseasonChampionships < 0)
                throw new ArgumentOutOfRangeException(nameof(highestLeagueGrade));
            return Kind switch
            {
                CardStudyUnlockKind.Always => true,
                CardStudyUnlockKind.ReachLeagueGrade => highestLeagueGrade >= RequiredLeagueGrade,
                CardStudyUnlockKind.WinPostseason => postseasonChampionships >= RequiredChampionships,
                _ => false
            };
        }

        public static CardStudyUnlockRequirement Reach(LeagueGrade grade) =>
            new CardStudyUnlockRequirement(CardStudyUnlockKind.ReachLeagueGrade, grade);

        public static CardStudyUnlockRequirement WinPostseason(int count = 1) =>
            new CardStudyUnlockRequirement(CardStudyUnlockKind.WinPostseason, LeagueGrade.Rookie, count);
    }

    /// <summary>카드 유학 한 과정의 목적지·해금 조건·비용·기간·확정 성장량을 정의한다.</summary>
    public sealed class CardStudyProgramDefinition
    {
        private readonly AbilityChange[] _rewards;

        public CardStudyProgramDefinition(
            string programId,
            string displayName,
            PlayerType playerType,
            int developmentPointCost,
            int durationWeeks,
            IReadOnlyList<AbilityChange> rewards,
            string destinationName = "해외 훈련 거점",
            int mapXPermille = 500,
            int mapYPermille = 500,
            CardStudyUnlockRequirement unlockRequirement = default, long moneyCost = 0,
            double greatSuccessProbability = 0, int greatSuccessBonus = 0)
        {
            if (string.IsNullOrWhiteSpace(programId)) throw new ArgumentException("ProgramId가 필요합니다.", nameof(programId));
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("표시 이름이 필요합니다.", nameof(displayName));
            if (developmentPointCost < 0 || moneyCost < 0 || developmentPointCost == 0 && moneyCost == 0 || durationWeeks <= 0
                || greatSuccessProbability < 0 || greatSuccessProbability > 1 || double.IsNaN(greatSuccessProbability) || greatSuccessBonus < 0)
                throw new ArgumentOutOfRangeException(nameof(developmentPointCost));
            if (rewards == null || rewards.Count == 0) throw new ArgumentException("유학 성장 보상이 필요합니다.", nameof(rewards));
            if (string.IsNullOrWhiteSpace(destinationName)) throw new ArgumentException("유학지 이름이 필요합니다.", nameof(destinationName));
            if (mapXPermille < 0 || mapXPermille > 1000 || mapYPermille < 0 || mapYPermille > 1000)
                throw new ArgumentOutOfRangeException(nameof(mapXPermille));
            ProgramId = programId.Trim();
            DisplayName = displayName.Trim();
            DestinationName = destinationName.Trim();
            PlayerType = playerType;
            DevelopmentPointCost = developmentPointCost;
            DurationWeeks = durationWeeks;
            MapXPermille = mapXPermille;
            MapYPermille = mapYPermille;
            UnlockRequirement = unlockRequirement;
            MoneyCost = moneyCost; GreatSuccessProbability = greatSuccessProbability; GreatSuccessBonus = greatSuccessBonus;
            _rewards = new AbilityChange[rewards.Count];
            for (int index = 0; index < rewards.Count; index++)
            {
                if (rewards[index].Amount <= 0) throw new ArgumentException("유학 성장량은 양수여야 합니다.", nameof(rewards));
                _rewards[index] = rewards[index];
            }
        }

        public string ProgramId { get; }
        public string DisplayName { get; }
        public string DestinationName { get; }
        public PlayerType PlayerType { get; }
        public int DevelopmentPointCost { get; }
        public long MoneyCost { get; }
        public double GreatSuccessProbability { get; }
        public int GreatSuccessBonus { get; }
        public int DurationWeeks { get; }
        public int MapXPermille { get; }
        public int MapYPermille { get; }
        public CardStudyUnlockRequirement UnlockRequirement { get; }
        public IReadOnlyList<AbilityChange> Rewards => _rewards;
    }

    /// <summary>구단주 카드 훈련과 야수·투수 유학 과정을 한 밸런스 계약으로 제공한다.</summary>
    public sealed class OwnerCardGrowthBalanceTable
    {
        private readonly CardTrainingProgramDefinition[] _trainingPrograms;
        private readonly CardStudyProgramDefinition[] _studyPrograms;

        public OwnerCardGrowthBalanceTable(
            IReadOnlyList<CardTrainingProgramDefinition> trainingPrograms,
            IReadOnlyList<CardStudyProgramDefinition> studyPrograms)
        {
            _trainingPrograms = Copy(trainingPrograms, PlayerAbilityCatalog.AbilityCount, nameof(trainingPrograms));
            if (studyPrograms == null || studyPrograms.Count == 0)
                throw new ArgumentException("유학 과정이 필요합니다.", nameof(studyPrograms));
            _studyPrograms = Copy(studyPrograms, studyPrograms.Count, nameof(studyPrograms));
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (CardStudyProgramDefinition program in _studyPrograms)
                if (!ids.Add(program.ProgramId))
                    throw new ArgumentException("유학 과정 ID가 중복되었습니다.", nameof(studyPrograms));
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

        /// <summary>레벨 0 구단도 유학 결정을 한 번은 내릴 수 있게 1슬롯을 보장하고, 업그레이드마다 1슬롯씩 늘린다.</summary>
        public int GetStudyCapacity(int trainingCenterLevel) =>
            Math.Max(1, Math.Min(MaxTrainingCenterLevel, trainingCenterLevel) + 1);

        /// <summary>ClubOperationBalanceTable의 트레이닝 센터 최고 레벨. 그 위로는 슬롯이 늘지 않는다.</summary>
        private const int MaxTrainingCenterLevel = 3;

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
                Study("study_contact", "정교 타격 아카데미", "도쿄", 820, 370,
                    PlayerType.Batter, PlayerAbility.Contact, PlayerAbility.BatterMental),
                Study("study_power", "장타 강화 캠프", "로스앤젤레스", 160, 390,
                    PlayerType.Batter, PlayerAbility.Power, PlayerAbility.Speed,
                    CardStudyUnlockRequirement.Reach(LeagueGrade.Minor)),
                new CardStudyProgramDefinition("study_defense", "수비 전문 학교", PlayerType.Batter, 100, 4,
                    new[] { new AbilityChange(PlayerAbility.Defense, 3) }, "암스테르담", 485, 220),
                Study("study_batter_allround", "야수 실전 리그", "시드니", 820, 745,
                    PlayerType.Batter, PlayerAbility.Contact, PlayerAbility.Defense,
                    CardStudyUnlockRequirement.WinPostseason()),
                Study("study_velocity", "구속 연구소", "텍사스", 170, 420,
                    PlayerType.Pitcher, PlayerAbility.Velocity, PlayerAbility.Stuff,
                    CardStudyUnlockRequirement.WinPostseason()),
                Study("study_command", "제구 아카데미", "오사카", 820, 370,
                    PlayerType.Pitcher, PlayerAbility.Control, PlayerAbility.PitcherMental),
                Study("study_breaking", "변화구 디자인 랩", "카리브", 315, 585,
                    PlayerType.Pitcher, PlayerAbility.Breaking, PlayerAbility.Stuff,
                    CardStudyUnlockRequirement.Reach(LeagueGrade.Minor)),
                Study("study_stamina", "선발 체력 리그", "시드니", 820, 745,
                    PlayerType.Pitcher, PlayerAbility.Stamina, PlayerAbility.PitcherMental),
                // 추가 과정도 100포인트·4주·총 +3을 유지해 보상 규모보다 성장 방향을 선택하게 한다.
                Study("study_baserunning", "주루 판단 캠프", "밴쿠버", 140, 150,
                    PlayerType.Batter, PlayerAbility.Speed, PlayerAbility.BatterMental),
                Study("study_bunt", "번트 기술 아카데미", "아바나", 300, 500,
                    PlayerType.Batter, PlayerAbility.Bunt, PlayerAbility.Contact),
                Study("study_batter_mental", "타석 집중 훈련", "상파울루", 335, 770,
                    PlayerType.Batter, PlayerAbility.BatterMental, PlayerAbility.Power,
                    CardStudyUnlockRequirement.Reach(LeagueGrade.Minor)),
                Study("study_range", "수비 범위 캠프", "나이로비", 660, 600,
                    PlayerType.Batter, PlayerAbility.Defense, PlayerAbility.Speed,
                    CardStudyUnlockRequirement.Reach(LeagueGrade.Minor)),
                Study("study_stuff", "구위 강화 캠프", "밴쿠버", 140, 150,
                    PlayerType.Pitcher, PlayerAbility.Stuff, PlayerAbility.Control,
                    CardStudyUnlockRequirement.Reach(LeagueGrade.Minor)),
                Study("study_pitcher_mental", "투구 운영 학교", "암스테르담", 485, 220,
                    PlayerType.Pitcher, PlayerAbility.PitcherMental, PlayerAbility.Stamina),
                Study("study_pitch_mix", "완급 조절 아카데미", "로마", 490, 460,
                    PlayerType.Pitcher, PlayerAbility.Control, PlayerAbility.Breaking,
                    CardStudyUnlockRequirement.Reach(LeagueGrade.Minor)),
                Study("study_pitcher_endurance", "투구 지구력 캠프", "케이프타운", 540, 820,
                    PlayerType.Pitcher, PlayerAbility.Stamina, PlayerAbility.Stuff,
                    CardStudyUnlockRequirement.WinPostseason()),
                Study("study_gap_hitting", "갭 타격 캠프", "뉴욕", 320, 170,
                    PlayerType.Batter, PlayerAbility.Contact, PlayerAbility.Power),
                Study("study_power_focus", "장타 집중 아카데미", "로마", 490, 460,
                    PlayerType.Batter, PlayerAbility.Power, PlayerAbility.BatterMental,
                    CardStudyUnlockRequirement.Reach(LeagueGrade.Minor)),
                Study("study_small_ball", "작전 주루 학교", "타이베이", 820, 120,
                    PlayerType.Batter, PlayerAbility.Speed, PlayerAbility.Bunt),
                Study("study_clutch", "승부 집중 캠프", "두바이", 660, 180,
                    PlayerType.Batter, PlayerAbility.BatterMental, PlayerAbility.Contact,
                    CardStudyUnlockRequirement.WinPostseason()),
                Study("study_power_pitching", "파워 피칭 캠프", "뉴욕", 320, 170,
                    PlayerType.Pitcher, PlayerAbility.Stuff, PlayerAbility.Velocity,
                    CardStudyUnlockRequirement.Reach(LeagueGrade.Minor)),
                Study("study_breaking_command", "변화구 제구 학교", "타이베이", 820, 120,
                    PlayerType.Pitcher, PlayerAbility.Breaking, PlayerAbility.Control),
                Study("study_velocity_endurance", "구속 유지 아카데미", "두바이", 660, 180,
                    PlayerType.Pitcher, PlayerAbility.Velocity, PlayerAbility.Stamina,
                    CardStudyUnlockRequirement.WinPostseason()),
                Study("study_pressure_pitching", "위기 관리 캠프", "나이로비", 660, 560,
                    PlayerType.Pitcher, PlayerAbility.PitcherMental, PlayerAbility.Control)
            });
        }

        private static CardStudyProgramDefinition Study(
            string id,
            string name,
            string destination,
            int mapX,
            int mapY,
            PlayerType type,
            PlayerAbility primary,
            PlayerAbility secondary,
            CardStudyUnlockRequirement unlockRequirement = default) =>
            new CardStudyProgramDefinition(id, name, type, 100, 4,
                new[] { new AbilityChange(primary, 2), new AbilityChange(secondary, 1) },
                destination, mapX, mapY, unlockRequirement);

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
        public int NextInstanceId => _nextInstanceId;
        public void RestoreNextInstanceId(int value)
        {
            if (value < _nextInstanceId) throw new ArgumentOutOfRangeException(nameof(value));
            _nextInstanceId = value;
        }

        public IReadOnlyList<SkillBlockInstance> Blocks => _blocks;
        public int PityEliteCount { get; private set; }
        public int PityUniqueCount { get; private set; }
        public int PityLegendaryCount { get; private set; }
        public int TotalPullCount { get; private set; }
        public int ResearchCount { get; private set; }
        public int FusionCount { get; private set; }
        public int FusionPoints { get; private set; }
        private int[] _fusionFailures = new int[4];
        /// <summary>저등급 재료로 고등급 천장을 채우지 못하도록 최고 재료 등급별로 분리한다.</summary>
        public int GetFusionFailures(SkillBlockRarity rarity) => rarity == SkillBlockRarity.Legendary ? 0 : _fusionFailures[(int)rarity];
        public int[] CopyFusionFailures() => (int[])_fusionFailures.Clone();
        public void RestoreFusion(int count, int points, int[] failures)
        {
            if (count < 0 || points < 0 || points > count || failures == null || failures.Length != 4)
                throw new ArgumentException("합성 이력이 올바르지 않습니다.");
            long total = 0;
            foreach (int value in failures) { if (value < 0) throw new ArgumentException("합성 실패 횟수가 잘못되었습니다."); total += value; }
            if (total > count) throw new ArgumentException("실패 횟수가 누적 합성을 초과합니다.");
            FusionCount = count; FusionPoints = points; _fusionFailures = (int[])failures.Clone();
        }
        public void RecordFusion(SkillBlockRarity highest, SkillBlockRarity result)
        {
            FusionCount = checked(FusionCount + 1); FusionPoints = checked(FusionPoints + 1);
            if (highest != SkillBlockRarity.Legendary)
                _fusionFailures[(int)highest] = result > highest ? 0 : checked(_fusionFailures[(int)highest] + 1);
        }
        public void ExchangeFusionPoints(int cost)
        {
            if (cost <= 0 || FusionPoints < cost) throw new InvalidOperationException("합성 포인트가 부족합니다.");
            FusionPoints -= cost;
        }
        public int SelectionBoxes { get; private set; }
        public void RestoreResearch(int researchCount, int selectionBoxes)
        {
            if (researchCount < 0 || selectionBoxes < 0 || selectionBoxes > researchCount / 10) throw new ArgumentOutOfRangeException(nameof(researchCount));
            ResearchCount = researchCount; SelectionBoxes = selectionBoxes;
        }
        public void RecordResearch()
        {
            ResearchCount = checked(ResearchCount + 1);
            if (ResearchCount % 10 == 0) SelectionBoxes++;
        }
        public void ConsumeSelectionBox()
        {
            if (SelectionBoxes < 1) throw new InvalidOperationException("S 선택 상자가 없습니다.");
            SelectionBoxes--;
        }
        public void Remove(int instanceId)
        {
            for (int i = 0; i < _blocks.Count; i++)
                if (_blocks[i].InstanceId == instanceId) { _blocks.RemoveAt(i); return; }
            throw new InvalidOperationException("보유 블록이 없습니다.");
        }

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
        public CardStudyProjectState(string cardId, string programId, int startedSeason, int remainingWeeks,
            int durationWeeks = 0, long paidMoney = 0, int paidDevelopmentPoints = 0, ulong resultSeed = 0, int resultBonus = 0)
        {
            if (string.IsNullOrWhiteSpace(cardId) || string.IsNullOrWhiteSpace(programId)) throw new ArgumentException("유학 식별자가 필요합니다.");
            if (remainingWeeks <= 0) throw new ArgumentOutOfRangeException(nameof(remainingWeeks));
            CardId = cardId.Trim();
            ProgramId = programId.Trim();
            StartedSeason = startedSeason;
            RemainingWeeks = remainingWeeks;
            DurationWeeks = durationWeeks == 0 ? remainingWeeks : durationWeeks;
            if (DurationWeeks < remainingWeeks || paidMoney < 0 || paidDevelopmentPoints < 0 || resultBonus < 0)
                throw new ArgumentException("유학 비용·결과 상태가 올바르지 않습니다.");
            PaidMoney = paidMoney; PaidDevelopmentPoints = paidDevelopmentPoints; ResultSeed = resultSeed; ResultBonus = resultBonus;
        }

        public string CardId { get; }
        public string ProgramId { get; }
        public int StartedSeason { get; }
        public int RemainingWeeks { get; private set; }
        public int DurationWeeks { get; }
        public long PaidMoney { get; }
        public int PaidDevelopmentPoints { get; }
        public ulong ResultSeed { get; }
        public int ResultBonus { get; }
        public bool AdvanceWeek() => --RemainingWeeks == 0;
    }

    /// <summary>구단주 모드 카드 성장의 공유 인벤토리와 진행 중 유학을 소유한다.</summary>
    public sealed class OwnerPlayerGrowthState
    {
        private readonly List<CardStudyProjectState> _studyProjects = new List<CardStudyProjectState>();

        public OwnerPlayerGrowthState(OwnerSkillBlockInventoryState inventory = null,
            OwnerOffseasonState offseason = null)
        {
            Inventory = inventory ?? new OwnerSkillBlockInventoryState();
            Offseason = offseason ?? new OwnerOffseasonState();
        }

        public OwnerOffseasonState Offseason { get; }
        public OwnerTraitTrainingState Traits { get; set; } = new OwnerTraitTrainingState();
        public OwnerSupportState Support { get; set; } = new OwnerSupportState();
        public OwnerSloganState Slogan { get; set; }
        public int StudySequence { get; private set; }
        public void RestoreStudySequence(int value)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            StudySequence = value;
        }
        public void RecordStudyStarted() => StudySequence = checked(StudySequence + 1);
        public OwnerSkillBlockInventoryState Inventory { get; }
        public IReadOnlyList<CardStudyProjectState> StudyProjects => _studyProjects;
        public void AddStudy(CardStudyProjectState project) => _studyProjects.Add(project ?? throw new ArgumentNullException(nameof(project)));
        public void RemoveStudyAt(int index) => _studyProjects.RemoveAt(index);
    }
}
