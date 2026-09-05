using System;
using System.Collections.Generic;

namespace Baseball.Core.Historical
{
    /// <summary>선수가 현재 경기 엔트리에 포함될 수 있는지를 구분한다.</summary>
    public enum PlayerAvailabilityStatus
    {
        Available,
        DayToDay,
        Unavailable
    }

    /// <summary>경기 사이에 보존할 최근 3일 투구 부하다.</summary>
    public readonly struct PitchingWorkloadState
    {
        public PitchingWorkloadState(int previousDayPitches, int twoDaysAgoPitches, int threeDaysAgoPitches)
        {
            if (previousDayPitches < 0) throw new ArgumentOutOfRangeException(nameof(previousDayPitches));
            if (twoDaysAgoPitches < 0) throw new ArgumentOutOfRangeException(nameof(twoDaysAgoPitches));
            if (threeDaysAgoPitches < 0) throw new ArgumentOutOfRangeException(nameof(threeDaysAgoPitches));

            PreviousDayPitches = previousDayPitches;
            TwoDaysAgoPitches = twoDaysAgoPitches;
            ThreeDaysAgoPitches = threeDaysAgoPitches;
        }

        public int PreviousDayPitches { get; }
        public int TwoDaysAgoPitches { get; }
        public int ThreeDaysAgoPitches { get; }

        /// <summary>하루가 지난 뒤 오늘 투구 수를 가장 최근 기록으로 옮긴다.</summary>
        public PitchingWorkloadState AdvanceDay(int pitchesThrownToday)
        {
            if (pitchesThrownToday < 0)
                throw new ArgumentOutOfRangeException(nameof(pitchesThrownToday));
            return new PitchingWorkloadState(pitchesThrownToday, PreviousDayPitches, TwoDaysAgoPitches);
        }
    }

    /// <summary>구단주 모드에서 한 선수 인물의 저장 가능한 당일 상태를 보관한다.</summary>
    public sealed class TeamSeasonPlayerStatus
    {
        public TeamSeasonPlayerStatus(
            string playerPersonId,
            int storedBaseCondition,
            PlayerAvailabilityStatus availability = PlayerAvailabilityStatus.Available,
            PitchingWorkloadState pitchingWorkload = default)
        {
            PlayerPersonId = RequireId(playerPersonId, nameof(playerPersonId));
            if (!Enum.IsDefined(typeof(PlayerAvailabilityStatus), availability))
                throw new ArgumentOutOfRangeException(nameof(availability));
            ValidateCondition(storedBaseCondition, nameof(storedBaseCondition));

            StoredBaseCondition = storedBaseCondition;
            Availability = availability;
            PitchingWorkload = pitchingWorkload;
        }

        public string PlayerPersonId { get; }
        public int StoredBaseCondition { get; private set; }
        public PlayerAvailabilityStatus Availability { get; private set; }
        public PitchingWorkloadState PitchingWorkload { get; private set; }

        /// <summary>회복과 경기 소모를 0~100 범위에서 한 번 반영한다.</summary>
        public void ChangeCondition(int delta)
        {
            long next = (long)StoredBaseCondition + delta;
            StoredBaseCondition = next < 0L ? 0 : next > 100L ? 100 : (int)next;
        }

        /// <summary>Save 복원 또는 명시적 상태 전환에서 원본 Condition을 교체한다.</summary>
        public void SetCondition(int condition)
        {
            ValidateCondition(condition, nameof(condition));
            StoredBaseCondition = condition;
        }

        /// <summary>현재 경기 출전 가능 상태를 교체한다.</summary>
        public void SetAvailability(PlayerAvailabilityStatus availability)
        {
            if (!Enum.IsDefined(typeof(PlayerAvailabilityStatus), availability))
                throw new ArgumentOutOfRangeException(nameof(availability));
            Availability = availability;
        }

        /// <summary>날짜가 바뀔 때 오늘 투구 수를 최근 부하에 한 번 기록한다.</summary>
        public void AdvancePitchingWorkload(int pitchesThrownToday)
        {
            PitchingWorkload = PitchingWorkload.AdvanceDay(pitchesThrownToday);
        }

        private static void ValidateCondition(int condition, string parameterName)
        {
            if (condition < 0 || condition > 100)
                throw new ArgumentOutOfRangeException(parameterName);
        }

        private static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("PlayerPersonId는 비어 있을 수 없습니다.", parameterName);
            return value.Trim();
        }
    }

    /// <summary>한 TeamSeason의 선수별 Condition·가용성·투구 부하 원본을 소유한다.</summary>
    public sealed class TeamSeasonPlayerStatusState
    {
        private readonly TeamSeasonPlayerStatus[] _players;

        public TeamSeasonPlayerStatusState(
            string teamSeasonKey,
            IReadOnlyList<TeamSeasonPlayerStatus> players)
        {
            if (string.IsNullOrWhiteSpace(teamSeasonKey))
                throw new ArgumentException("TeamSeasonKey는 비어 있을 수 없습니다.", nameof(teamSeasonKey));
            if (players == null)
                throw new ArgumentNullException(nameof(players));

            TeamSeasonKey = teamSeasonKey.Trim();
            _players = new TeamSeasonPlayerStatus[players.Count];
            for (int index = 0; index < players.Count; index++)
            {
                TeamSeasonPlayerStatus player = players[index] ??
                    throw new ArgumentException("null 선수 상태가 있습니다.", nameof(players));
                for (int previous = 0; previous < index; previous++)
                {
                    if (string.Equals(
                        _players[previous].PlayerPersonId,
                        player.PlayerPersonId,
                        StringComparison.Ordinal))
                    {
                        throw new ArgumentException("PlayerPersonId 상태는 중복될 수 없습니다.", nameof(players));
                    }
                }
                _players[index] = player;
            }
        }

        public string TeamSeasonKey { get; }
        public IReadOnlyList<TeamSeasonPlayerStatus> Players => _players;

        /// <summary>인물 ID에 해당하는 상태를 순서에 영향 없이 찾는다.</summary>
        public bool TryGetPlayer(string playerPersonId, out TeamSeasonPlayerStatus player)
        {
            if (string.IsNullOrWhiteSpace(playerPersonId))
            {
                player = null;
                return false;
            }

            string normalized = playerPersonId.Trim();
            for (int index = 0; index < _players.Length; index++)
            {
                if (string.Equals(_players[index].PlayerPersonId, normalized, StringComparison.Ordinal))
                {
                    player = _players[index];
                    return true;
                }
            }

            player = null;
            return false;
        }

        /// <summary>인물 ID에 해당하는 상태를 반환하고 없으면 계약 오류로 처리한다.</summary>
        public TeamSeasonPlayerStatus GetRequiredPlayer(string playerPersonId)
        {
            if (TryGetPlayer(playerPersonId, out TeamSeasonPlayerStatus player))
                return player;
            throw new KeyNotFoundException($"PlayerPersonId '{playerPersonId}'의 상태가 없습니다.");
        }
    }

    /// <summary>두 선수 인물 ID를 순서와 무관한 안정 키로 정규화한다.</summary>
    public readonly struct PlayerPersonPairKey : IEquatable<PlayerPersonPairKey>, IComparable<PlayerPersonPairKey>
    {
        public PlayerPersonPairKey(string playerPersonIdA, string playerPersonIdB)
        {
            string first = RequireId(playerPersonIdA, nameof(playerPersonIdA));
            string second = RequireId(playerPersonIdB, nameof(playerPersonIdB));
            if (string.Equals(first, second, StringComparison.Ordinal))
                throw new ArgumentException("궁합 Pair에는 서로 다른 두 선수가 필요합니다.");

            if (string.CompareOrdinal(first, second) < 0)
            {
                FirstPlayerPersonId = first;
                SecondPlayerPersonId = second;
            }
            else
            {
                FirstPlayerPersonId = second;
                SecondPlayerPersonId = first;
            }
        }

        public string FirstPlayerPersonId { get; }
        public string SecondPlayerPersonId { get; }

        public int CompareTo(PlayerPersonPairKey other)
        {
            int first = string.CompareOrdinal(FirstPlayerPersonId, other.FirstPlayerPersonId);
            return first != 0 ? first : string.CompareOrdinal(SecondPlayerPersonId, other.SecondPlayerPersonId);
        }

        public bool Equals(PlayerPersonPairKey other)
        {
            return string.Equals(FirstPlayerPersonId, other.FirstPlayerPersonId, StringComparison.Ordinal) &&
                   string.Equals(SecondPlayerPersonId, other.SecondPlayerPersonId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => obj is PlayerPersonPairKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return ((FirstPlayerPersonId?.GetHashCode() ?? 0) * 397) ^
                       (SecondPlayerPersonId?.GetHashCode() ?? 0);
            }
        }

        public override string ToString() => $"{FirstPlayerPersonId}|{SecondPlayerPersonId}";

        private static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("PlayerPersonId는 비어 있을 수 없습니다.", parameterName);
            return value.Trim();
        }
    }

    /// <summary>같은 Pair의 타선 경험과 배터리 경험을 서로 다른 축으로 보존한다.</summary>
    public sealed class ChemistryFamiliarityEntry
    {
        public ChemistryFamiliarityEntry(
            PlayerPersonPairKey pair,
            int lineupFamiliarity = 0,
            int batteryFamiliarity = 0)
        {
            if (lineupFamiliarity < 0) throw new ArgumentOutOfRangeException(nameof(lineupFamiliarity));
            if (batteryFamiliarity < 0) throw new ArgumentOutOfRangeException(nameof(batteryFamiliarity));
            Pair = pair;
            LineupFamiliarity = lineupFamiliarity;
            BatteryFamiliarity = batteryFamiliarity;
        }

        public PlayerPersonPairKey Pair { get; }
        public int LineupFamiliarity { get; private set; }
        public int BatteryFamiliarity { get; private set; }

        /// <summary>실제 동시 선발 출장 경험을 설정된 Cap까지 누적한다.</summary>
        public void AddLineupFamiliarity(int amount, int cap)
        {
            LineupFamiliarity = AddCapped(LineupFamiliarity, amount, cap);
        }

        /// <summary>실제 배터리 이닝 경험을 설정된 Cap까지 누적한다.</summary>
        public void AddBatteryFamiliarity(int amount, int cap)
        {
            BatteryFamiliarity = AddCapped(BatteryFamiliarity, amount, cap);
        }

        private static int AddCapped(int current, int amount, int cap)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (cap < 0 || current > cap) throw new ArgumentOutOfRangeException(nameof(cap));
            long next = (long)current + amount;
            return next >= cap ? cap : (int)next;
        }
    }

    /// <summary>실제로 함께 출전한 선수 Pair만 TeamSeason 범위에서 저장한다.</summary>
    public sealed class TeamChemistryFamiliarityState
    {
        private readonly List<ChemistryFamiliarityEntry> _entries;

        public TeamChemistryFamiliarityState(
            string teamSeasonKey,
            IReadOnlyList<ChemistryFamiliarityEntry> entries = null)
        {
            if (string.IsNullOrWhiteSpace(teamSeasonKey))
                throw new ArgumentException("TeamSeasonKey는 비어 있을 수 없습니다.", nameof(teamSeasonKey));
            TeamSeasonKey = teamSeasonKey.Trim();
            _entries = new List<ChemistryFamiliarityEntry>(entries?.Count ?? 0);

            if (entries == null)
                return;
            for (int index = 0; index < entries.Count; index++)
            {
                ChemistryFamiliarityEntry entry = entries[index] ??
                    throw new ArgumentException("null Familiarity 항목이 있습니다.", nameof(entries));
                int insertionIndex = FindInsertionIndex(entry.Pair, out bool exists);
                if (exists)
                    throw new ArgumentException("같은 PlayerPerson Pair는 중복될 수 없습니다.", nameof(entries));
                _entries.Insert(insertionIndex, entry);
            }
        }

        public string TeamSeasonKey { get; }
        public IReadOnlyList<ChemistryFamiliarityEntry> Entries => _entries;

        public int GetLineupFamiliarity(PlayerPersonPairKey pair)
        {
            int index = FindInsertionIndex(pair, out bool exists);
            return exists ? _entries[index].LineupFamiliarity : 0;
        }

        public int GetBatteryFamiliarity(PlayerPersonPairKey pair)
        {
            int index = FindInsertionIndex(pair, out bool exists);
            return exists ? _entries[index].BatteryFamiliarity : 0;
        }

        /// <summary>실제 인접 타선 동시 출장을 Pair 원본에 기록한다.</summary>
        public void RecordLineupPair(PlayerPersonPairKey pair, int amount, int cap)
        {
            GetOrCreate(pair).AddLineupFamiliarity(amount, cap);
        }

        /// <summary>실제 투수-포수 동시 이닝을 Pair 원본에 기록한다.</summary>
        public void RecordBatteryPair(PlayerPersonPairKey pair, int amount, int cap)
        {
            GetOrCreate(pair).AddBatteryFamiliarity(amount, cap);
        }

        private ChemistryFamiliarityEntry GetOrCreate(PlayerPersonPairKey pair)
        {
            int index = FindInsertionIndex(pair, out bool exists);
            if (exists)
                return _entries[index];
            var entry = new ChemistryFamiliarityEntry(pair);
            _entries.Insert(index, entry);
            return entry;
        }

        private int FindInsertionIndex(PlayerPersonPairKey pair, out bool exists)
        {
            int low = 0;
            int high = _entries.Count - 1;
            while (low <= high)
            {
                int middle = low + ((high - low) / 2);
                int comparison = _entries[middle].Pair.CompareTo(pair);
                if (comparison == 0)
                {
                    exists = true;
                    return middle;
                }
                if (comparison < 0)
                    low = middle + 1;
                else
                    high = middle - 1;
            }
            exists = false;
            return low;
        }
    }

    /// <summary>연속 Condition 값을 UI의 열 단계와 표시 키로 매핑하는 데이터 항목이다.</summary>
    public sealed class ConditionPresentationBand
    {
        public ConditionPresentationBand(int minimumCondition, string labelKey, string iconKey)
        {
            if (minimumCondition < 0 || minimumCondition > 100)
                throw new ArgumentOutOfRangeException(nameof(minimumCondition));
            if (string.IsNullOrWhiteSpace(labelKey))
                throw new ArgumentException("Condition label key는 비어 있을 수 없습니다.", nameof(labelKey));
            if (string.IsNullOrWhiteSpace(iconKey))
                throw new ArgumentException("Condition icon key는 비어 있을 수 없습니다.", nameof(iconKey));
            MinimumCondition = minimumCondition;
            LabelKey = labelKey.Trim();
            IconKey = iconKey.Trim();
        }

        public int MinimumCondition { get; }
        public string LabelKey { get; }
        public string IconKey { get; }
    }

    /// <summary>Stored Condition을 바꾸지 않고 UI 표시 단계만 계산하는 데이터 테이블이다.</summary>
    public sealed class ConditionPresentationTable
    {
        private readonly ConditionPresentationBand[] _bands;

        public ConditionPresentationTable(IReadOnlyList<ConditionPresentationBand> bands)
        {
            if (bands == null || bands.Count != 10)
                throw new ArgumentException("Condition 표시에는 정확히 10개 단계가 필요합니다.", nameof(bands));
            _bands = new ConditionPresentationBand[10];
            for (int index = 0; index < _bands.Length; index++)
            {
                ConditionPresentationBand band = bands[index] ??
                    throw new ArgumentException("null Condition 표시 단계가 있습니다.", nameof(bands));
                if (index == 0 && band.MinimumCondition != 0)
                    throw new ArgumentException("첫 Condition 표시 단계는 0에서 시작해야 합니다.", nameof(bands));
                if (index > 0 && band.MinimumCondition <= _bands[index - 1].MinimumCondition)
                    throw new ArgumentException("Condition 표시 경계는 오름차순이어야 합니다.", nameof(bands));
                _bands[index] = band;
            }
        }

        public IReadOnlyList<ConditionPresentationBand> Bands => _bands;

        /// <summary>0~100 Condition을 1~10 표시 단계로 변환한다.</summary>
        public int GetLevel(int condition)
        {
            if (condition < 0 || condition > 100)
                throw new ArgumentOutOfRangeException(nameof(condition));
            for (int index = _bands.Length - 1; index >= 0; index--)
            {
                if (condition >= _bands[index].MinimumCondition)
                    return index + 1;
            }
            return 1;
        }

        public ConditionPresentationBand GetBand(int condition) => _bands[GetLevel(condition) - 1];
    }

    /// <summary>기존 타격 능력치에서 파생한 설명용 공격 스타일이다.</summary>
    public enum HitterChemistryStyle
    {
        Balanced,
        TableSetterLike,
        PowerLike
    }

    /// <summary>한 선수의 경기용 Condition 합성과 근거를 보관하는 불변 결과다.</summary>
    public readonly struct EffectiveMatchCondition
    {
        public EffectiveMatchCondition(
            int storedBaseCondition,
            int assignmentModifier,
            int lineupChemistryModifier,
            int batteryChemistryModifier,
            int temporaryModifier)
        {
            if (storedBaseCondition < 0 || storedBaseCondition > 100)
                throw new ArgumentOutOfRangeException(nameof(storedBaseCondition));
            StoredBaseCondition = storedBaseCondition;
            AssignmentModifier = assignmentModifier;
            LineupChemistryModifier = lineupChemistryModifier;
            BatteryChemistryModifier = batteryChemistryModifier;
            TemporaryModifier = temporaryModifier;
            long total = (long)storedBaseCondition + assignmentModifier + lineupChemistryModifier +
                         batteryChemistryModifier + temporaryModifier;
            Value = total < 0L ? 0 : total > 100L ? 100 : (int)total;
        }

        public int StoredBaseCondition { get; }
        public int AssignmentModifier { get; }
        public int LineupChemistryModifier { get; }
        public int BatteryChemistryModifier { get; }
        public int TemporaryModifier { get; }
        public int Value { get; }
    }

    /// <summary>시설과 스태프가 한 번의 회복 계산에 제공하는 합성 입력이다.</summary>
    public readonly struct ConditionRecoveryContext
    {
        public ConditionRecoveryContext(
            int baseRecovery,
            double facilityEfficiencyMultiplier,
            double staffEfficiencyMultiplier)
        {
            if (baseRecovery < 0) throw new ArgumentOutOfRangeException(nameof(baseRecovery));
            ValidateMultiplier(facilityEfficiencyMultiplier, nameof(facilityEfficiencyMultiplier));
            ValidateMultiplier(staffEfficiencyMultiplier, nameof(staffEfficiencyMultiplier));
            BaseRecovery = baseRecovery;
            FacilityEfficiencyMultiplier = facilityEfficiencyMultiplier;
            StaffEfficiencyMultiplier = staffEfficiencyMultiplier;
        }

        public int BaseRecovery { get; }
        public double FacilityEfficiencyMultiplier { get; }
        public double StaffEfficiencyMultiplier { get; }

        private static void ValidateMultiplier(double value, string parameterName)
        {
            if (value < 0d || double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    /// <summary>Condition과 Chemistry의 조정 가능 계수를 한 버전으로 묶는다.</summary>
    public sealed class ConditionChemistryBalanceTable
    {
        public ConditionChemistryBalanceTable(
            ConditionPresentationTable presentation,
            int familiarityCap,
            int lineupSharedStartGain,
            int batterySharedInningGain,
            double familiarityScoreWeight,
            double styleComplementScore,
            double styleConflictScore,
            double catcherDefenseWeight,
            double catcherMentalWeight,
            double pitcherMentalStabilityWeight,
            double goodScoreThreshold,
            double badScoreThreshold,
            int conditionLevelStep,
            int maximumChemistryLevelDelta,
            int tableSetterLeadThreshold,
            int powerLeadThreshold,
            int neutralMatchCondition = 80,
            int conditionPointsPerRating = 10,
            int maximumConditionRatingModifier = 3,
            int weeklyBaseRecovery = 6,
            int startingHitterConditionCost = 4,
            int pitcherConditionCostPerThirtyPitches = 6)
        {
            Presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
            if (familiarityCap <= 0) throw new ArgumentOutOfRangeException(nameof(familiarityCap));
            if (lineupSharedStartGain <= 0) throw new ArgumentOutOfRangeException(nameof(lineupSharedStartGain));
            if (batterySharedInningGain <= 0) throw new ArgumentOutOfRangeException(nameof(batterySharedInningGain));
            ValidateFiniteNonNegative(familiarityScoreWeight, nameof(familiarityScoreWeight));
            ValidateFiniteNonNegative(styleComplementScore, nameof(styleComplementScore));
            ValidateFiniteNonNegative(styleConflictScore, nameof(styleConflictScore));
            ValidateFiniteNonNegative(catcherDefenseWeight, nameof(catcherDefenseWeight));
            ValidateFiniteNonNegative(catcherMentalWeight, nameof(catcherMentalWeight));
            ValidateFiniteNonNegative(pitcherMentalStabilityWeight, nameof(pitcherMentalStabilityWeight));
            if (goodScoreThreshold <= 0d || double.IsNaN(goodScoreThreshold))
                throw new ArgumentOutOfRangeException(nameof(goodScoreThreshold));
            if (badScoreThreshold >= 0d || double.IsNaN(badScoreThreshold))
                throw new ArgumentOutOfRangeException(nameof(badScoreThreshold));
            if (conditionLevelStep <= 0) throw new ArgumentOutOfRangeException(nameof(conditionLevelStep));
            if (maximumChemistryLevelDelta <= 0) throw new ArgumentOutOfRangeException(nameof(maximumChemistryLevelDelta));
            if (tableSetterLeadThreshold <= 0) throw new ArgumentOutOfRangeException(nameof(tableSetterLeadThreshold));
            if (powerLeadThreshold <= 0) throw new ArgumentOutOfRangeException(nameof(powerLeadThreshold));
            if (neutralMatchCondition < 0 || neutralMatchCondition > 100)
                throw new ArgumentOutOfRangeException(nameof(neutralMatchCondition));
            if (conditionPointsPerRating <= 0)
                throw new ArgumentOutOfRangeException(nameof(conditionPointsPerRating));
            if (maximumConditionRatingModifier <= 0)
                throw new ArgumentOutOfRangeException(nameof(maximumConditionRatingModifier));
            if (weeklyBaseRecovery < 0)
                throw new ArgumentOutOfRangeException(nameof(weeklyBaseRecovery));
            if (startingHitterConditionCost < 0)
                throw new ArgumentOutOfRangeException(nameof(startingHitterConditionCost));
            if (pitcherConditionCostPerThirtyPitches < 0)
                throw new ArgumentOutOfRangeException(nameof(pitcherConditionCostPerThirtyPitches));

            FamiliarityCap = familiarityCap;
            LineupSharedStartGain = lineupSharedStartGain;
            BatterySharedInningGain = batterySharedInningGain;
            FamiliarityScoreWeight = familiarityScoreWeight;
            StyleComplementScore = styleComplementScore;
            StyleConflictScore = styleConflictScore;
            CatcherDefenseWeight = catcherDefenseWeight;
            CatcherMentalWeight = catcherMentalWeight;
            PitcherMentalStabilityWeight = pitcherMentalStabilityWeight;
            GoodScoreThreshold = goodScoreThreshold;
            BadScoreThreshold = badScoreThreshold;
            ConditionLevelStep = conditionLevelStep;
            MaximumChemistryLevelDelta = maximumChemistryLevelDelta;
            TableSetterLeadThreshold = tableSetterLeadThreshold;
            PowerLeadThreshold = powerLeadThreshold;
            NeutralMatchCondition = neutralMatchCondition;
            ConditionPointsPerRating = conditionPointsPerRating;
            MaximumConditionRatingModifier = maximumConditionRatingModifier;
            WeeklyBaseRecovery = weeklyBaseRecovery;
            StartingHitterConditionCost = startingHitterConditionCost;
            PitcherConditionCostPerThirtyPitches = pitcherConditionCostPerThirtyPitches;
        }

        public ConditionPresentationTable Presentation { get; }
        public int FamiliarityCap { get; }
        public int LineupSharedStartGain { get; }
        public int BatterySharedInningGain { get; }
        public double FamiliarityScoreWeight { get; }
        public double StyleComplementScore { get; }
        public double StyleConflictScore { get; }
        public double CatcherDefenseWeight { get; }
        public double CatcherMentalWeight { get; }
        public double PitcherMentalStabilityWeight { get; }
        public double GoodScoreThreshold { get; }
        public double BadScoreThreshold { get; }
        public int ConditionLevelStep { get; }
        public int MaximumChemistryLevelDelta { get; }
        public int TableSetterLeadThreshold { get; }
        public int PowerLeadThreshold { get; }
        public int NeutralMatchCondition { get; }
        public int ConditionPointsPerRating { get; }
        public int MaximumConditionRatingModifier { get; }
        public int WeeklyBaseRecovery { get; }
        public int StartingHitterConditionCost { get; }
        public int PitcherConditionCostPerThirtyPitches { get; }

        public static ConditionChemistryBalanceTable CreateDefault()
        {
            var bands = new ConditionPresentationBand[10];
            string[] labels =
            {
                "condition.worst", "condition.very_bad", "condition.bad", "condition.somewhat_bad",
                "condition.normal", "condition.somewhat_good", "condition.good", "condition.very_good",
                "condition.excellent", "condition.peak"
            };
            for (int index = 0; index < bands.Length; index++)
                bands[index] = new ConditionPresentationBand(index * 10, labels[index], $"condition.level_{index + 1}");

            return new ConditionChemistryBalanceTable(
                new ConditionPresentationTable(bands),
                familiarityCap: 100,
                lineupSharedStartGain: 2,
                batterySharedInningGain: 1,
                familiarityScoreWeight: 0.35d,
                styleComplementScore: 18d,
                styleConflictScore: 12d,
                catcherDefenseWeight: 0.30d,
                catcherMentalWeight: 0.30d,
                pitcherMentalStabilityWeight: 0.20d,
                goodScoreThreshold: 22d,
                badScoreThreshold: -12d,
                conditionLevelStep: 10,
                maximumChemistryLevelDelta: 1,
                tableSetterLeadThreshold: 12,
                powerLeadThreshold: 10);
        }

        private static void ValidateFiniteNonNegative(double value, string parameterName)
        {
            if (value < 0d || double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
