using System;
using System.Collections.Generic;
using Baseball.Core.Players;

namespace Baseball.Core.Historical
{
    /// <summary>프리셋의 한 선발 야수와 실제 수비 위치를 안정 ID로 저장한다.</summary>
    public sealed class LineupPresetSlot
    {
        public LineupPresetSlot(string cardId, PlayerPosition position)
        {
            if (position < PlayerPosition.Catcher || position > PlayerPosition.DesignatedHitter)
                throw new ArgumentOutOfRangeException(nameof(position));
            CardId = NormalizeOptionalId(cardId);
            Position = position;
        }

        public string CardId { get; }
        public PlayerPosition Position { get; }

        private static string NormalizeOptionalId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }

    /// <summary>25인 등록을 바꾸지 않고 한 경기의 운용 방법만 stable ID로 보관한다.</summary>
    public sealed class LineupPresetState
    {
        public const int TeamColorSlotCount = 2;
        public const int MaximumTacticCardCount = 2;

        private readonly LineupPresetSlot[] _startingLineupSlots;
        private readonly string[] _battingOrderCardIds;
        private readonly string[] _benchPriorityCardIds;
        private readonly string[] _starterRotationCardIds;
        private readonly string[] _bullpenAssignmentCardIds;
        private readonly string[] _teamColorIds;
        private readonly string[] _defaultTacticCardIds;

        public LineupPresetState(
            string presetId,
            string name,
            IReadOnlyList<LineupPresetSlot> startingLineupSlots,
            IReadOnlyList<string> battingOrderCardIds,
            IReadOnlyList<string> benchPriorityCardIds,
            IReadOnlyList<string> starterRotationCardIds,
            IReadOnlyList<string> bullpenAssignmentCardIds,
            string setupPitcherCardId,
            string closerPitcherCardId,
            IReadOnlyList<string> teamColorIds,
            IReadOnlyList<string> defaultTacticCardIds)
        {
            PresetId = RequireId(presetId, nameof(presetId));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("프리셋 이름은 비어 있을 수 없습니다.", nameof(name));

            Name = name.Trim();
            _startingLineupSlots = CopySlots(
                startingLineupSlots,
                ActiveRosterCompositionRule.StartingHitterCount,
                nameof(startingLineupSlots));
            _battingOrderCardIds = CopyOptionalIds(
                battingOrderCardIds,
                ActiveRosterCompositionRule.StartingHitterCount,
                nameof(battingOrderCardIds));
            _benchPriorityCardIds = CopyOptionalIds(
                benchPriorityCardIds,
                ActiveRosterCompositionRule.BenchHitterCount,
                nameof(benchPriorityCardIds));
            _starterRotationCardIds = CopyOptionalIds(
                starterRotationCardIds,
                ActiveRosterCompositionRule.StartingPitcherCount,
                nameof(starterRotationCardIds));
            _bullpenAssignmentCardIds = CopyOptionalIds(
                bullpenAssignmentCardIds,
                ActiveRosterCompositionRule.BullpenPitcherCount,
                nameof(bullpenAssignmentCardIds));
            SetupPitcherCardId = NormalizeOptionalId(setupPitcherCardId);
            CloserPitcherCardId = NormalizeOptionalId(closerPitcherCardId);
            _teamColorIds = CopyOptionalIds(teamColorIds, TeamColorSlotCount, nameof(teamColorIds));
            _defaultTacticCardIds = CopyVariableOptionalIds(
                defaultTacticCardIds,
                MaximumTacticCardCount,
                nameof(defaultTacticCardIds));
        }

        public string PresetId { get; }
        public string Name { get; }
        public IReadOnlyList<LineupPresetSlot> StartingLineupSlots => _startingLineupSlots;
        public IReadOnlyList<string> BattingOrderCardIds => _battingOrderCardIds;
        public IReadOnlyList<string> BenchPriorityCardIds => _benchPriorityCardIds;
        public IReadOnlyList<string> StarterRotationCardIds => _starterRotationCardIds;
        public IReadOnlyList<string> BullpenAssignmentCardIds => _bullpenAssignmentCardIds;
        public string SetupPitcherCardId { get; }
        public string CloserPitcherCardId { get; }
        public IReadOnlyList<string> TeamColorIds => _teamColorIds;
        public IReadOnlyList<string> DefaultTacticCardIds => _defaultTacticCardIds;

        private static LineupPresetSlot[] CopySlots(
            IReadOnlyList<LineupPresetSlot> source,
            int expectedCount,
            string parameterName)
        {
            if (source == null) throw new ArgumentNullException(parameterName);
            if (source.Count != expectedCount)
                throw new ArgumentException($"{parameterName} 항목 수는 {expectedCount}여야 합니다.", parameterName);
            var result = new LineupPresetSlot[source.Count];
            for (int index = 0; index < source.Count; index++)
                result[index] = source[index] ?? throw new ArgumentException("null 슬롯이 있습니다.", parameterName);
            return result;
        }

        private static string[] CopyOptionalIds(
            IReadOnlyList<string> source,
            int expectedCount,
            string parameterName)
        {
            if (source == null) throw new ArgumentNullException(parameterName);
            if (source.Count != expectedCount)
                throw new ArgumentException($"{parameterName} 항목 수는 {expectedCount}여야 합니다.", parameterName);
            var result = new string[source.Count];
            for (int index = 0; index < source.Count; index++)
                result[index] = NormalizeOptionalId(source[index]);
            return result;
        }

        private static string[] CopyVariableOptionalIds(
            IReadOnlyList<string> source,
            int maximumCount,
            string parameterName)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<string>();
            if (source.Count > maximumCount)
                throw new ArgumentException($"{parameterName} 항목 수는 {maximumCount} 이하여야 합니다.", parameterName);
            var result = new string[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                result[index] = RequireId(source[index], parameterName);
                for (int previous = 0; previous < index; previous++)
                {
                    if (string.Equals(result[previous], result[index], StringComparison.Ordinal))
                        throw new ArgumentException("같은 전술카드를 중복 지정할 수 없습니다.", parameterName);
                }
            }
            return result;
        }

        private static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("식별자는 비어 있을 수 없습니다.", parameterName);
            return value.Trim();
        }

        private static string NormalizeOptionalId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }

    /// <summary>프리셋 검증 결과의 전체 상태다.</summary>
    public enum LineupPresetValidationStatus
    {
        Valid,
        PartiallyValid,
        Invalid
    }

    /// <summary>경고, 사용자의 보완이 필요한 불완전 상태, 구조 오류를 구분한다.</summary>
    public enum LineupPresetIssueSeverity
    {
        Warning,
        Incomplete,
        Error
    }

    /// <summary>프리셋 문제의 위치를 안정적으로 식별한다.</summary>
    public enum LineupPresetAssignmentGroup
    {
        ActiveRoster,
        StartingLineup,
        BattingOrder,
        Bench,
        StarterRotation,
        Bullpen,
        Setup,
        Closer,
        TeamColor,
        Tactic
    }

    /// <summary>프리셋을 자동 수정하지 않고 호출자에게 돌려주는 문제 코드다.</summary>
    public enum LineupPresetValidationIssueCode
    {
        ActiveRosterInvalid,
        MissingAssignment,
        CardNotOnActiveRoster,
        CardUnavailable,
        DuplicateCard,
        DuplicateDefensivePosition,
        MissingDefensivePosition,
        BattingOrderMismatch,
        NonHitterAssignment,
        NonPitcherAssignment,
        PlayerContextMissing,
        OffPositionAssignment,
        PitcherRoleMismatch,
        TeamColorUnavailable,
        TacticCardUnavailable
    }

    /// <summary>한 프리셋 검증 문제와 기존 Assignment Resolver의 예상 비용이다.</summary>
    public sealed class LineupPresetValidationIssue
    {
        public LineupPresetValidationIssue(
            LineupPresetValidationIssueCode code,
            LineupPresetIssueSeverity severity,
            LineupPresetAssignmentGroup group,
            int slotIndex,
            string cardId,
            string context,
            int conditionPenalty = 0,
            double fieldingErrorProbabilityMultiplier = 1d)
        {
            if (slotIndex < -1) throw new ArgumentOutOfRangeException(nameof(slotIndex));
            if (conditionPenalty < 0) throw new ArgumentOutOfRangeException(nameof(conditionPenalty));
            if (fieldingErrorProbabilityMultiplier < 1d ||
                double.IsNaN(fieldingErrorProbabilityMultiplier))
            {
                throw new ArgumentOutOfRangeException(nameof(fieldingErrorProbabilityMultiplier));
            }
            Code = code;
            Severity = severity;
            Group = group;
            SlotIndex = slotIndex;
            CardId = string.IsNullOrWhiteSpace(cardId) ? null : cardId.Trim();
            Context = context ?? string.Empty;
            ConditionPenalty = conditionPenalty;
            FieldingErrorProbabilityMultiplier = fieldingErrorProbabilityMultiplier;
        }

        public LineupPresetValidationIssueCode Code { get; }
        public LineupPresetIssueSeverity Severity { get; }
        public LineupPresetAssignmentGroup Group { get; }
        public int SlotIndex { get; }
        public string CardId { get; }
        public string Context { get; }
        public int ConditionPenalty { get; }
        public double FieldingErrorProbabilityMultiplier { get; }
    }

    /// <summary>프리셋 적용 가능성과 안정된 순서의 문제 목록이다.</summary>
    public sealed class LineupPresetValidationResult
    {
        private readonly LineupPresetValidationIssue[] _issues;

        public LineupPresetValidationResult(
            string presetId,
            IReadOnlyList<LineupPresetValidationIssue> issues)
        {
            PresetId = RequireId(presetId, nameof(presetId));
            if (issues == null) throw new ArgumentNullException(nameof(issues));
            _issues = new LineupPresetValidationIssue[issues.Count];
            bool hasError = false;
            bool hasIncomplete = false;
            for (int index = 0; index < issues.Count; index++)
            {
                LineupPresetValidationIssue issue = issues[index] ??
                    throw new ArgumentException("null 문제가 있습니다.", nameof(issues));
                _issues[index] = issue;
                hasError |= issue.Severity == LineupPresetIssueSeverity.Error;
                hasIncomplete |= issue.Severity == LineupPresetIssueSeverity.Incomplete;
            }
            Status = hasError
                ? LineupPresetValidationStatus.Invalid
                : hasIncomplete
                    ? LineupPresetValidationStatus.PartiallyValid
                    : LineupPresetValidationStatus.Valid;
        }

        public string PresetId { get; }
        public LineupPresetValidationStatus Status { get; }
        public IReadOnlyList<LineupPresetValidationIssue> Issues => _issues;
        public bool CanStartGame => Status == LineupPresetValidationStatus.Valid;

        private static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("식별자는 비어 있을 수 없습니다.", parameterName);
            return value.Trim();
        }
    }

    /// <summary>경기 시작 직전 검증된 stable ID 편성을 변경 불가능하게 동결한다.</summary>
    public sealed class PreGamePlanSnapshot
    {
        private readonly LineupPresetSlot[] _startingLineupSlots;
        private readonly string[] _battingOrderCardIds;
        private readonly string[] _benchPriorityCardIds;
        private readonly string[] _starterRotationCardIds;
        private readonly string[] _bullpenAssignmentCardIds;
        private readonly string[] _teamColorIds;
        private readonly string[] _tacticCardIds;

        public PreGamePlanSnapshot(
            int scheduledGameId,
            string teamSeasonKey,
            LineupPresetState plan,
            LineupPresetValidationResult validation)
        {
            if (scheduledGameId <= 0) throw new ArgumentOutOfRangeException(nameof(scheduledGameId));
            if (string.IsNullOrWhiteSpace(teamSeasonKey))
                throw new ArgumentException("TeamSeasonKey는 비어 있을 수 없습니다.", nameof(teamSeasonKey));
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (validation == null) throw new ArgumentNullException(nameof(validation));
            if (!string.Equals(plan.PresetId, validation.PresetId, StringComparison.Ordinal))
                throw new ArgumentException("다른 프리셋의 검증 결과로 동결할 수 없습니다.", nameof(validation));
            if (!validation.CanStartGame)
                throw new InvalidOperationException("Valid 프리셋만 경기 계획으로 동결할 수 있습니다.");

            ValidateCompletePlan(plan);
            ScheduledGameId = scheduledGameId;
            TeamSeasonKey = teamSeasonKey.Trim();
            SourcePresetId = plan.PresetId;
            _startingLineupSlots = Copy(plan.StartingLineupSlots);
            _battingOrderCardIds = Copy(plan.BattingOrderCardIds);
            _benchPriorityCardIds = Copy(plan.BenchPriorityCardIds);
            _starterRotationCardIds = Copy(plan.StarterRotationCardIds);
            _bullpenAssignmentCardIds = Copy(plan.BullpenAssignmentCardIds);
            SetupPitcherCardId = plan.SetupPitcherCardId;
            CloserPitcherCardId = plan.CloserPitcherCardId;
            _teamColorIds = Copy(plan.TeamColorIds);
            _tacticCardIds = Copy(plan.DefaultTacticCardIds);
        }

        public int ScheduledGameId { get; }
        public string TeamSeasonKey { get; }
        public string SourcePresetId { get; }
        public IReadOnlyList<LineupPresetSlot> StartingLineupSlots => _startingLineupSlots;
        public IReadOnlyList<string> BattingOrderCardIds => _battingOrderCardIds;
        public IReadOnlyList<string> BenchPriorityCardIds => _benchPriorityCardIds;
        public IReadOnlyList<string> StarterRotationCardIds => _starterRotationCardIds;
        public IReadOnlyList<string> BullpenAssignmentCardIds => _bullpenAssignmentCardIds;
        public string SetupPitcherCardId { get; }
        public string CloserPitcherCardId { get; }
        public IReadOnlyList<string> TeamColorIds => _teamColorIds;
        public IReadOnlyList<string> TacticCardIds => _tacticCardIds;

        private static void ValidateCompletePlan(LineupPresetState plan)
        {
            var lineupIds = new HashSet<string>(StringComparer.Ordinal);
            var positions = new HashSet<PlayerPosition>();
            for (int index = 0; index < plan.StartingLineupSlots.Count; index++)
            {
                AddRequiredUnique(lineupIds, plan.StartingLineupSlots[index].CardId, "선발 라인업");
                if (!positions.Add(plan.StartingLineupSlots[index].Position))
                    throw new InvalidOperationException("선발 라인업에 중복 수비 포지션이 있습니다.");
            }
            for (PlayerPosition position = PlayerPosition.Catcher;
                 position <= PlayerPosition.DesignatedHitter;
                 position++)
            {
                if (!positions.Contains(position))
                    throw new InvalidOperationException($"선발 라인업에 {position} 포지션이 없습니다.");
            }

            var battingIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < plan.BattingOrderCardIds.Count; index++)
                AddRequiredUnique(battingIds, plan.BattingOrderCardIds[index], "타순");
            if (!lineupIds.SetEquals(battingIds))
                throw new InvalidOperationException("선발 라인업과 타순의 선수 집합이 다릅니다.");

            var hitterIds = new HashSet<string>(lineupIds, StringComparer.Ordinal);
            for (int index = 0; index < plan.BenchPriorityCardIds.Count; index++)
                AddRequiredUnique(hitterIds, plan.BenchPriorityCardIds[index], "벤치");

            var pitcherIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < plan.StarterRotationCardIds.Count; index++)
                AddRequiredUnique(pitcherIds, plan.StarterRotationCardIds[index], "선발 로테이션");
            for (int index = 0; index < plan.BullpenAssignmentCardIds.Count; index++)
                AddRequiredUnique(pitcherIds, plan.BullpenAssignmentCardIds[index], "불펜");
            AddRequiredUnique(pitcherIds, plan.SetupPitcherCardId, "Setup");
            AddRequiredUnique(pitcherIds, plan.CloserPitcherCardId, "Closer");
        }

        private static void AddRequiredUnique(HashSet<string> ids, string value, string group)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"{group}에 비어 있는 슬롯이 있습니다.");
            if (!ids.Add(value))
                throw new InvalidOperationException($"{group}에 중복 선수가 있습니다.");
        }

        private static LineupPresetSlot[] Copy(IReadOnlyList<LineupPresetSlot> source)
        {
            var result = new LineupPresetSlot[source.Count];
            for (int index = 0; index < source.Count; index++)
                result[index] = source[index];
            return result;
        }

        private static string[] Copy(IReadOnlyList<string> source)
        {
            var result = new string[source.Count];
            for (int index = 0; index < source.Count; index++)
                result[index] = source[index];
            return result;
        }
    }
}
