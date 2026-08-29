using System;
using System.Collections.Generic;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Core.Teams;

namespace Baseball.Game.Career
{
    /// <summary>회고에서 구분할 수 있는 실제 커리어 사건 종류다.</summary>
    public enum CareerMemoryType
    {
        CareerDebut,
        FirstHit,
        FirstHomeRun,
        FirstPitchingWin,
        FirstSave,
        RoleBreakthrough,
        ExceptionalGame,
        Postseason,
        Championship,
        Award,
        Injury,
        InjuryReturn,
        Training,
        Study,
        SkillBuild,
        ContractAccepted,
        ContractDeclined,
        Transfer,
        TradePreference,
        FinalSeasonDeclared,
        FinalAppearance,
        Retirement
    }

    /// <summary>은퇴가 확정된 직접적인 사유를 구분한다.</summary>
    public enum RetirementReason
    {
        Voluntary,
        DeclaredFinalSeason,
        Medical,
        Unsigned
    }

    /// <summary>대표 순간 카드가 원본 숫자를 다시 계산하지 않도록 한 값을 고정한다.</summary>
    public readonly struct MemoryStatValue
    {
        public MemoryStatValue(string statKey, double value, string formatKey = "number")
        {
            StatKey = statKey ?? string.Empty;
            Value = value;
            FormatKey = formatKey ?? string.Empty;
        }

        public string StatKey { get; }
        public double Value { get; }
        public string FormatKey { get; }
    }

    /// <summary>한 사건의 사실, 원본 연결, 선정 근거와 연출 자산 키를 변경 불가능하게 보관한다.</summary>
    public sealed class CareerMemoryRecord
    {
        private readonly MemoryStatValue[] _stats;
        private readonly string[] _tags;

        public CareerMemoryRecord(
            string memoryId,
            int playerId,
            int season,
            int dateIndex,
            int teamId,
            CareerMemoryType type,
            string titleKey,
            string narrativeKey,
            int matchId,
            string newsId,
            int contractId,
            int importanceScore,
            int careerImpactScore,
            int playerAgencyScore,
            int rarityScore,
            int emotionalContextScore,
            MemoryStatValue[] stats,
            string[] tags,
            string presentationAssetKey)
        {
            if (string.IsNullOrWhiteSpace(memoryId))
                throw new ArgumentException("MemoryId는 비어 있을 수 없습니다.", nameof(memoryId));
            if (playerId <= 0)
                throw new ArgumentOutOfRangeException(nameof(playerId));
            if (season <= 0 || dateIndex < 0 || teamId < 0)
                throw new ArgumentOutOfRangeException(nameof(season));

            MemoryId = memoryId;
            PlayerId = playerId;
            Season = season;
            DateIndex = dateIndex;
            TeamId = teamId;
            Type = type;
            TitleKey = titleKey ?? string.Empty;
            NarrativeKey = narrativeKey ?? string.Empty;
            MatchId = matchId;
            NewsId = newsId ?? string.Empty;
            ContractId = contractId;
            ImportanceScore = ClampScore(importanceScore);
            CareerImpactScore = ClampScore(careerImpactScore);
            PlayerAgencyScore = ClampScore(playerAgencyScore);
            RarityScore = ClampScore(rarityScore);
            EmotionalContextScore = ClampScore(emotionalContextScore);
            _stats = stats == null ? Array.Empty<MemoryStatValue>() : (MemoryStatValue[])stats.Clone();
            _tags = tags == null ? Array.Empty<string>() : (string[])tags.Clone();
            PresentationAssetKey = presentationAssetKey ?? string.Empty;
        }

        public string MemoryId { get; }
        public int PlayerId { get; }
        public int Season { get; }
        public int DateIndex { get; }
        public int TeamId { get; }
        public CareerMemoryType Type { get; }
        public string TitleKey { get; }
        public string NarrativeKey { get; }
        public int MatchId { get; }
        public string NewsId { get; }
        public int ContractId { get; }
        public int ImportanceScore { get; }
        public int CareerImpactScore { get; }
        public int PlayerAgencyScore { get; }
        public int RarityScore { get; }
        public int EmotionalContextScore { get; }
        public IReadOnlyList<MemoryStatValue> Stats => _stats;
        public IReadOnlyList<string> Tags => _tags;
        public string PresentationAssetKey { get; }

        /// <summary>기획의 30/25/20/15/10 가중치를 그대로 적용한 대표 순간 점수다.</summary>
        public double MemoryScore =>
            ImportanceScore * 0.30d +
            CareerImpactScore * 0.25d +
            PlayerAgencyScore * 0.20d +
            RarityScore * 0.15d +
            EmotionalContextScore * 0.10d;

        public bool HasTag(string tag)
        {
            for (int index = 0; index < _tags.Length; index++)
            {
                if (string.Equals(_tags[index], tag, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static int ClampScore(int value) => value < 0 ? 0 : value > 100 ? 100 : value;
    }

    /// <summary>문자열 ID 하나의 누적 횟수를 결정론적 정렬이 가능한 값으로 보관한다.</summary>
    public readonly struct CareerNamedCount
    {
        public CareerNamedCount(string key, int count)
        {
            Key = key ?? string.Empty;
            Count = count < 0 ? throw new ArgumentOutOfRangeException(nameof(count)) : count;
        }

        public string Key { get; }
        public int Count { get; }
    }

    /// <summary>한 시즌 동안 발생한 플레이 방침·훈련·기용·구종 사용을 누적한다.</summary>
    public sealed class CareerSeasonExperienceState
    {
        private readonly int[] _battingApproachCounts = new int[6];
        private readonly int[] _pitchingApproachCounts = new int[6];
        private readonly int[] _pitchTypeCounts = new int[8];
        private readonly int[] _roleCounts = new int[6];
        private readonly List<CareerNamedCount> _trainingCounts = new();

        public CareerSeasonExperienceState(int seasonId, int year)
        {
            if (seasonId <= 0 || year <= 0)
                throw new ArgumentOutOfRangeException(nameof(seasonId));
            SeasonId = seasonId;
            Year = year;
        }

        public int SeasonId { get; }
        public int Year { get; }
        public long GrowthMoneySpent { get; private set; }
        public int StudyCount { get; private set; }
        public IReadOnlyList<CareerNamedCount> TrainingCounts => _trainingCounts;

        public int GetBattingApproachCount(BattingApproach approach) =>
            _battingApproachCounts[(int)approach];

        public int GetPitchingApproachCount(PitchingApproach approach) =>
            _pitchingApproachCounts[(int)approach];

        public int GetPitchTypeCount(PitchType pitchType) => _pitchTypeCounts[(int)pitchType];

        public int GetRoleCount(PlayerGameRole role) => _roleCounts[(int)role];

        public void RecordBattingApproach(BattingApproach approach, int count = 1)
        {
            Add(_battingApproachCounts, (int)approach, count);
        }

        public void RecordPitchingApproach(PitchingApproach approach, int count = 1)
        {
            Add(_pitchingApproachCounts, (int)approach, count);
        }

        public void RecordPitchType(PitchType pitchType, int count = 1)
        {
            Add(_pitchTypeCounts, (int)pitchType, count);
        }

        public void RecordRole(PlayerGameRole role)
        {
            Add(_roleCounts, (int)role, 1);
        }

        public void RecordTraining(string programId, long moneySpent, bool isStudy)
        {
            if (string.IsNullOrWhiteSpace(programId))
                throw new ArgumentException("ProgramId는 비어 있을 수 없습니다.", nameof(programId));
            if (moneySpent < 0L)
                throw new ArgumentOutOfRangeException(nameof(moneySpent));
            int index = FindNamedCount(programId);
            if (index < 0)
                _trainingCounts.Add(new CareerNamedCount(programId, 1));
            else
                _trainingCounts[index] = new CareerNamedCount(programId, _trainingCounts[index].Count + 1);
            _trainingCounts.Sort((left, right) => string.CompareOrdinal(left.Key, right.Key));
            GrowthMoneySpent += moneySpent;
            if (isStudy)
                StudyCount++;
        }

        private int FindNamedCount(string key)
        {
            for (int index = 0; index < _trainingCounts.Count; index++)
            {
                if (string.Equals(_trainingCounts[index].Key, key, StringComparison.Ordinal))
                    return index;
            }
            return -1;
        }

        private static void Add(int[] counts, int index, int count)
        {
            if (index < 0 || index >= counts.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            counts[index] += count;
        }
    }

    /// <summary>플레이 도중에만 알 수 있는 선택과 사건을 은퇴 전까지 순서대로 누적한다.</summary>
    public sealed class CareerMemoryLog
    {
        private readonly List<CareerMemoryRecord> _records = new();
        private readonly HashSet<string> _memoryIds = new(StringComparer.Ordinal);
        private readonly List<CareerSeasonExperienceState> _seasonExperiences = new();

        public IReadOnlyList<CareerMemoryRecord> Records => _records;
        public IReadOnlyList<CareerSeasonExperienceState> SeasonExperiences => _seasonExperiences;

        public void Append(CareerMemoryRecord record)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));
            if (!_memoryIds.Add(record.MemoryId))
                throw new InvalidOperationException($"MemoryId {record.MemoryId}가 중복되었습니다.");
            if (_records.Count > 0 && CompareChronology(_records[^1], record) > 0)
                throw new InvalidOperationException("커리어 기억의 날짜는 역행할 수 없습니다.");
            _records.Add(record);
        }

        public bool ContainsType(CareerMemoryType type)
        {
            for (int index = 0; index < _records.Count; index++)
            {
                if (_records[index].Type == type)
                    return true;
            }
            return false;
        }

        public CareerSeasonExperienceState GetOrCreateSeason(int seasonId, int year)
        {
            for (int index = 0; index < _seasonExperiences.Count; index++)
            {
                if (_seasonExperiences[index].SeasonId == seasonId)
                    return _seasonExperiences[index];
            }
            var created = new CareerSeasonExperienceState(seasonId, year);
            _seasonExperiences.Add(created);
            _seasonExperiences.Sort((left, right) => left.SeasonId.CompareTo(right.SeasonId));
            return created;
        }

        public CareerSeasonExperienceState FindSeason(int seasonId)
        {
            for (int index = 0; index < _seasonExperiences.Count; index++)
            {
                if (_seasonExperiences[index].SeasonId == seasonId)
                    return _seasonExperiences[index];
            }
            return null;
        }

        private static int CompareChronology(CareerMemoryRecord left, CareerMemoryRecord right)
        {
            int season = left.Season.CompareTo(right.Season);
            if (season != 0)
                return season;
            return left.DateIndex.CompareTo(right.DateIndex);
        }
    }
}
