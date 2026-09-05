using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;

namespace Baseball.Simulation.Historical
{
    /// <summary>프리셋 검증에 필요한 공개 availability와 본래 포지션만 투영한다.</summary>
    public sealed class LineupPresetPlayerContext
    {
        public LineupPresetPlayerContext(
            string cardId,
            PlayerPosition naturalPosition,
            PitcherRole? naturalPitcherRole,
            PitcherRoleConfidence? naturalPitcherRoleConfidence,
            bool isAvailable)
        {
            if (string.IsNullOrWhiteSpace(cardId))
                throw new ArgumentException("CardId는 비어 있을 수 없습니다.", nameof(cardId));
            if (!Enum.IsDefined(typeof(PlayerPosition), naturalPosition) || naturalPosition == PlayerPosition.Unknown)
                throw new ArgumentOutOfRangeException(nameof(naturalPosition));
            bool isPitcher = naturalPosition == PlayerPosition.StartingPitcher ||
                             naturalPosition == PlayerPosition.ReliefPitcher;
            if (isPitcher && (!naturalPitcherRole.HasValue || !naturalPitcherRoleConfidence.HasValue))
                throw new ArgumentException("투수 Context에는 본래 PitcherRole과 Confidence가 필요합니다.");
            if (!isPitcher && (naturalPitcherRole.HasValue || naturalPitcherRoleConfidence.HasValue))
                throw new ArgumentException("야수 Context에는 PitcherRole을 지정하지 않습니다.");
            if (naturalPitcherRole.HasValue &&
                !Enum.IsDefined(typeof(PitcherRole), naturalPitcherRole.Value))
                throw new ArgumentOutOfRangeException(nameof(naturalPitcherRole));
            if (naturalPitcherRoleConfidence.HasValue &&
                !Enum.IsDefined(typeof(PitcherRoleConfidence), naturalPitcherRoleConfidence.Value))
                throw new ArgumentOutOfRangeException(nameof(naturalPitcherRoleConfidence));

            CardId = cardId.Trim();
            NaturalPosition = naturalPosition;
            NaturalPitcherRole = naturalPitcherRole;
            NaturalPitcherRoleConfidence = naturalPitcherRoleConfidence;
            IsAvailable = isAvailable;
        }

        public string CardId { get; }
        public PlayerPosition NaturalPosition { get; }
        public PitcherRole? NaturalPitcherRole { get; }
        public PitcherRoleConfidence? NaturalPitcherRoleConfidence { get; }
        public bool IsAvailable { get; }
        public bool IsHitter => NaturalPosition >= PlayerPosition.Catcher &&
                                NaturalPosition <= PlayerPosition.DesignatedHitter;
        public bool IsPitcher => NaturalPosition == PlayerPosition.StartingPitcher ||
                                 NaturalPosition == PlayerPosition.ReliefPitcher;
    }

    /// <summary>현재 25인과 선택 가능 TeamColor/Tactic을 프리셋 검증에 제공한다.</summary>
    public sealed class LineupPresetValidationContext
    {
        private readonly LineupPresetPlayerContext[] _players;
        private readonly string[] _availableTeamColorIds;
        private readonly string[] _availableTacticCardIds;

        public LineupPresetValidationContext(
            CurrentRosterState activeRoster,
            IReadOnlyList<LineupPresetPlayerContext> players,
            PositionAssignmentRule positionAssignmentRule,
            IReadOnlyList<string> availableTeamColorIds = null,
            IReadOnlyList<string> availableTacticCardIds = null)
        {
            ActiveRoster = activeRoster ?? throw new ArgumentNullException(nameof(activeRoster));
            PositionAssignmentRule = positionAssignmentRule ?? throw new ArgumentNullException(nameof(positionAssignmentRule));
            _players = CopyPlayers(players);
            _availableTeamColorIds = CopyOptionalIds(availableTeamColorIds, nameof(availableTeamColorIds));
            _availableTacticCardIds = CopyOptionalIds(availableTacticCardIds, nameof(availableTacticCardIds));
            CanValidateTeamColors = availableTeamColorIds != null;
            CanValidateTactics = availableTacticCardIds != null;
        }

        public CurrentRosterState ActiveRoster { get; }
        public IReadOnlyList<LineupPresetPlayerContext> Players => _players;
        public PositionAssignmentRule PositionAssignmentRule { get; }
        public IReadOnlyList<string> AvailableTeamColorIds => _availableTeamColorIds;
        public IReadOnlyList<string> AvailableTacticCardIds => _availableTacticCardIds;
        public bool CanValidateTeamColors { get; }
        public bool CanValidateTactics { get; }

        private static LineupPresetPlayerContext[] CopyPlayers(IReadOnlyList<LineupPresetPlayerContext> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var result = new LineupPresetPlayerContext[source.Count];
            for (int index = 0; index < source.Count; index++)
                result[index] = source[index] ?? throw new ArgumentException("null 선수 Context가 있습니다.", nameof(source));
            return result;
        }

        private static string[] CopyOptionalIds(IReadOnlyList<string> source, string parameterName)
        {
            if (source == null) return Array.Empty<string>();
            var result = new string[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                if (string.IsNullOrWhiteSpace(source[index]))
                    throw new ArgumentException("식별자는 비어 있을 수 없습니다.", parameterName);
                result[index] = source[index].Trim();
            }
            return result;
        }
    }

    /// <summary>오래된 프리셋을 조용히 보정하지 않고 현재 로스터와 다시 대조한다.</summary>
    public sealed class LineupPresetValidator
    {
        private readonly ActiveRosterValidator _activeRosterValidator;
        private readonly PositionAssignmentPenaltyResolver _assignmentResolver;
        private readonly ActiveRosterCompositionRule _rosterRule;

        public LineupPresetValidator(
            ActiveRosterValidator activeRosterValidator = null,
            PositionAssignmentPenaltyResolver assignmentResolver = null,
            ActiveRosterCompositionRule rosterRule = null)
        {
            _rosterRule = rosterRule ?? ActiveRosterCompositionRule.Standard;
            _activeRosterValidator = activeRosterValidator ?? new ActiveRosterValidator(_rosterRule);
            _assignmentResolver = assignmentResolver ?? new PositionAssignmentPenaltyResolver();
        }

        public LineupPresetValidationResult Validate(
            LineupPresetState preset,
            LineupPresetValidationContext context)
        {
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            if (context == null) throw new ArgumentNullException(nameof(context));

            var issues = new List<LineupPresetValidationIssue>();
            var rosterByCardId = BuildRosterIndex(context.ActiveRoster, issues);
            var playerByCardId = BuildPlayerIndex(context.Players, issues);
            AddActiveRosterIssues(context.ActiveRoster, issues);

            var startingIds = new HashSet<string>(StringComparer.Ordinal);
            var startingPositions = new bool[ActiveRosterCompositionRule.StartingHitterCount];
            for (int index = 0; index < preset.StartingLineupSlots.Count; index++)
            {
                LineupPresetSlot slot = preset.StartingLineupSlots[index];
                ValidateHitter(
                    slot.CardId,
                    slot.Position,
                    LineupPresetAssignmentGroup.StartingLineup,
                    index,
                    context,
                    rosterByCardId,
                    playerByCardId,
                    issues);
                AddDuplicateIssue(startingIds, slot.CardId, LineupPresetAssignmentGroup.StartingLineup, index, issues);
                int positionIndex = (int)slot.Position - (int)PlayerPosition.Catcher;
                if (startingPositions[positionIndex])
                {
                    issues.Add(new LineupPresetValidationIssue(
                        LineupPresetValidationIssueCode.DuplicateDefensivePosition,
                        LineupPresetIssueSeverity.Error,
                        LineupPresetAssignmentGroup.StartingLineup,
                        index,
                        slot.CardId,
                        $"{slot.Position} 포지션이 중복되었습니다."));
                }
                startingPositions[positionIndex] = true;
            }
            for (int index = 0; index < startingPositions.Length; index++)
            {
                if (startingPositions[index]) continue;
                PlayerPosition missing = (PlayerPosition)((int)PlayerPosition.Catcher + index);
                issues.Add(new LineupPresetValidationIssue(
                    LineupPresetValidationIssueCode.MissingDefensivePosition,
                    LineupPresetIssueSeverity.Error,
                    LineupPresetAssignmentGroup.StartingLineup,
                    -1,
                    null,
                    $"{missing} 포지션이 누락되었습니다."));
            }

            var battingIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < preset.BattingOrderCardIds.Count; index++)
            {
                string cardId = preset.BattingOrderCardIds[index];
                ValidateHitter(
                    cardId,
                    null,
                    LineupPresetAssignmentGroup.BattingOrder,
                    index,
                    context,
                    rosterByCardId,
                    playerByCardId,
                    issues);
                AddDuplicateIssue(battingIds, cardId, LineupPresetAssignmentGroup.BattingOrder, index, issues);
            }
            if (!startingIds.SetEquals(battingIds))
            {
                issues.Add(new LineupPresetValidationIssue(
                    LineupPresetValidationIssueCode.BattingOrderMismatch,
                    LineupPresetIssueSeverity.Error,
                    LineupPresetAssignmentGroup.BattingOrder,
                    -1,
                    null,
                    "StartingLineup과 BattingOrder의 CardId 집합이 다릅니다."));
            }

            var allHitterAssignments = new HashSet<string>(startingIds, StringComparer.Ordinal);
            for (int index = 0; index < preset.BenchPriorityCardIds.Count; index++)
            {
                string cardId = preset.BenchPriorityCardIds[index];
                ValidateHitter(
                    cardId,
                    null,
                    LineupPresetAssignmentGroup.Bench,
                    index,
                    context,
                    rosterByCardId,
                    playerByCardId,
                    issues);
                AddDuplicateIssue(allHitterAssignments, cardId, LineupPresetAssignmentGroup.Bench, index, issues);
            }

            var pitcherAssignments = new HashSet<string>(StringComparer.Ordinal);
            ValidatePitcherList(
                preset.StarterRotationCardIds,
                PitcherRole.Starter,
                LineupPresetAssignmentGroup.StarterRotation,
                context,
                rosterByCardId,
                playerByCardId,
                pitcherAssignments,
                issues);
            ValidatePitcherList(
                preset.BullpenAssignmentCardIds,
                PitcherRole.MiddleRelief,
                LineupPresetAssignmentGroup.Bullpen,
                context,
                rosterByCardId,
                playerByCardId,
                pitcherAssignments,
                issues);
            ValidatePitcher(
                preset.SetupPitcherCardId,
                PitcherRole.Setup,
                LineupPresetAssignmentGroup.Setup,
                0,
                context,
                rosterByCardId,
                playerByCardId,
                pitcherAssignments,
                issues);
            ValidatePitcher(
                preset.CloserPitcherCardId,
                PitcherRole.Closer,
                LineupPresetAssignmentGroup.Closer,
                0,
                context,
                rosterByCardId,
                playerByCardId,
                pitcherAssignments,
                issues);

            ValidateLoadoutIds(
                preset.TeamColorIds,
                context.AvailableTeamColorIds,
                context.CanValidateTeamColors,
                LineupPresetAssignmentGroup.TeamColor,
                LineupPresetValidationIssueCode.TeamColorUnavailable,
                issues);
            ValidateLoadoutIds(
                preset.DefaultTacticCardIds,
                context.AvailableTacticCardIds,
                context.CanValidateTactics,
                LineupPresetAssignmentGroup.Tactic,
                LineupPresetValidationIssueCode.TacticCardUnavailable,
                issues);

            return new LineupPresetValidationResult(preset.PresetId, issues);
        }

        private Dictionary<string, ActiveRosterEntry> BuildRosterIndex(
            CurrentRosterState roster,
            ICollection<LineupPresetValidationIssue> issues)
        {
            var result = new Dictionary<string, ActiveRosterEntry>(StringComparer.Ordinal);
            for (int index = 0; index < roster.Entries.Count; index++)
            {
                ActiveRosterEntry entry = roster.Entries[index];
                if (!result.TryAdd(entry.CardId, entry))
                {
                    issues.Add(new LineupPresetValidationIssue(
                        LineupPresetValidationIssueCode.ActiveRosterInvalid,
                        LineupPresetIssueSeverity.Error,
                        LineupPresetAssignmentGroup.ActiveRoster,
                        index,
                        entry.CardId,
                        "ActiveRoster에 같은 CardId가 중복되었습니다."));
                }
            }
            return result;
        }

        private static Dictionary<string, LineupPresetPlayerContext> BuildPlayerIndex(
            IReadOnlyList<LineupPresetPlayerContext> players,
            ICollection<LineupPresetValidationIssue> issues)
        {
            var result = new Dictionary<string, LineupPresetPlayerContext>(StringComparer.Ordinal);
            for (int index = 0; index < players.Count; index++)
            {
                LineupPresetPlayerContext player = players[index];
                if (!result.TryAdd(player.CardId, player))
                {
                    issues.Add(new LineupPresetValidationIssue(
                        LineupPresetValidationIssueCode.ActiveRosterInvalid,
                        LineupPresetIssueSeverity.Error,
                        LineupPresetAssignmentGroup.ActiveRoster,
                        index,
                        player.CardId,
                        "같은 CardId의 선수 Context가 중복되었습니다."));
                }
            }
            return result;
        }

        private void AddActiveRosterIssues(
            CurrentRosterState roster,
            ICollection<LineupPresetValidationIssue> issues)
        {
            RosterValidationResult rosterResult = _activeRosterValidator.Validate(roster);
            for (int index = 0; index < rosterResult.Issues.Count; index++)
            {
                RosterValidationIssue issue = rosterResult.Issues[index];
                issues.Add(new LineupPresetValidationIssue(
                    LineupPresetValidationIssueCode.ActiveRosterInvalid,
                    LineupPresetIssueSeverity.Error,
                    LineupPresetAssignmentGroup.ActiveRoster,
                    index,
                    null,
                    $"{issue.Code}:{issue.Expected}:{issue.Actual}:{issue.Context}"));
            }
        }

        private void ValidateHitter(
            string cardId,
            PlayerPosition? assignedPosition,
            LineupPresetAssignmentGroup group,
            int slotIndex,
            LineupPresetValidationContext validationContext,
            IReadOnlyDictionary<string, ActiveRosterEntry> rosterByCardId,
            IReadOnlyDictionary<string, LineupPresetPlayerContext> playerByCardId,
            ICollection<LineupPresetValidationIssue> issues)
        {
            if (!TryGetAssignment(
                    cardId,
                    group,
                    slotIndex,
                    rosterByCardId,
                    playerByCardId,
                    issues,
                    out ActiveRosterEntry rosterEntry,
                    out LineupPresetPlayerContext player))
                return;

            if (!_rosterRule.IsHitterRole(rosterEntry.Role) || !player.IsHitter)
            {
                issues.Add(CreateIssue(
                    LineupPresetValidationIssueCode.NonHitterAssignment,
                    LineupPresetIssueSeverity.Error,
                    group,
                    slotIndex,
                    cardId,
                    "야수 슬롯에는 ActiveRoster의 야수만 배치할 수 있습니다."));
                return;
            }
            if (!assignedPosition.HasValue) return;

            PositionAssignmentPenalty penalty = _assignmentResolver.EvaluateHitter(
                player.NaturalPosition,
                assignedPosition.Value,
                validationContext.PositionAssignmentRule);
            if (!penalty.IsOffPosition) return;
            issues.Add(new LineupPresetValidationIssue(
                LineupPresetValidationIssueCode.OffPositionAssignment,
                LineupPresetIssueSeverity.Warning,
                group,
                slotIndex,
                cardId,
                $"{player.NaturalPosition}->{assignedPosition.Value}",
                penalty.ConditionPenalty,
                penalty.FieldingErrorProbabilityMultiplier));
        }

        private void ValidatePitcherList(
            IReadOnlyList<string> cardIds,
            PitcherRole assignedRole,
            LineupPresetAssignmentGroup group,
            LineupPresetValidationContext context,
            IReadOnlyDictionary<string, ActiveRosterEntry> rosterByCardId,
            IReadOnlyDictionary<string, LineupPresetPlayerContext> playerByCardId,
            HashSet<string> pitcherAssignments,
            ICollection<LineupPresetValidationIssue> issues)
        {
            for (int index = 0; index < cardIds.Count; index++)
            {
                ValidatePitcher(
                    cardIds[index],
                    assignedRole,
                    group,
                    index,
                    context,
                    rosterByCardId,
                    playerByCardId,
                    pitcherAssignments,
                    issues);
            }
        }

        private void ValidatePitcher(
            string cardId,
            PitcherRole assignedRole,
            LineupPresetAssignmentGroup group,
            int slotIndex,
            LineupPresetValidationContext validationContext,
            IReadOnlyDictionary<string, ActiveRosterEntry> rosterByCardId,
            IReadOnlyDictionary<string, LineupPresetPlayerContext> playerByCardId,
            HashSet<string> pitcherAssignments,
            ICollection<LineupPresetValidationIssue> issues)
        {
            AddDuplicateIssue(pitcherAssignments, cardId, group, slotIndex, issues);
            if (!TryGetAssignment(
                    cardId,
                    group,
                    slotIndex,
                    rosterByCardId,
                    playerByCardId,
                    issues,
                    out ActiveRosterEntry rosterEntry,
                    out LineupPresetPlayerContext player))
                return;

            if (!_rosterRule.IsPitcherRole(rosterEntry.Role) || !player.IsPitcher)
            {
                issues.Add(CreateIssue(
                    LineupPresetValidationIssueCode.NonPitcherAssignment,
                    LineupPresetIssueSeverity.Error,
                    group,
                    slotIndex,
                    cardId,
                    "투수 슬롯에는 ActiveRoster의 투수만 배치할 수 있습니다."));
                return;
            }

            PositionAssignmentPenalty penalty = _assignmentResolver.EvaluatePitcher(
                player.NaturalPitcherRole.Value,
                assignedRole,
                player.NaturalPitcherRoleConfidence.Value,
                validationContext.PositionAssignmentRule);
            if (!penalty.IsOffPosition) return;
            issues.Add(new LineupPresetValidationIssue(
                LineupPresetValidationIssueCode.PitcherRoleMismatch,
                LineupPresetIssueSeverity.Warning,
                group,
                slotIndex,
                cardId,
                $"{player.NaturalPitcherRole.Value}->{assignedRole}",
                penalty.ConditionPenalty));
        }

        private static bool TryGetAssignment(
            string cardId,
            LineupPresetAssignmentGroup group,
            int slotIndex,
            IReadOnlyDictionary<string, ActiveRosterEntry> rosterByCardId,
            IReadOnlyDictionary<string, LineupPresetPlayerContext> playerByCardId,
            ICollection<LineupPresetValidationIssue> issues,
            out ActiveRosterEntry rosterEntry,
            out LineupPresetPlayerContext player)
        {
            rosterEntry = null;
            player = null;
            if (string.IsNullOrWhiteSpace(cardId))
            {
                issues.Add(CreateIssue(
                    LineupPresetValidationIssueCode.MissingAssignment,
                    LineupPresetIssueSeverity.Incomplete,
                    group,
                    slotIndex,
                    null,
                    "선수 지정이 필요합니다."));
                return false;
            }
            if (!rosterByCardId.TryGetValue(cardId, out rosterEntry))
            {
                issues.Add(CreateIssue(
                    LineupPresetValidationIssueCode.CardNotOnActiveRoster,
                    LineupPresetIssueSeverity.Incomplete,
                    group,
                    slotIndex,
                    cardId,
                    "현재 ActiveRoster 25인에 없는 카드입니다."));
                return false;
            }
            if (!playerByCardId.TryGetValue(cardId, out player))
            {
                issues.Add(CreateIssue(
                    LineupPresetValidationIssueCode.PlayerContextMissing,
                    LineupPresetIssueSeverity.Incomplete,
                    group,
                    slotIndex,
                    cardId,
                    "현재 선수 상태를 확인할 수 없습니다."));
                return false;
            }
            if (!player.IsAvailable)
            {
                issues.Add(CreateIssue(
                    LineupPresetValidationIssueCode.CardUnavailable,
                    LineupPresetIssueSeverity.Incomplete,
                    group,
                    slotIndex,
                    cardId,
                    "현재 경기에 출전할 수 없는 선수입니다."));
            }
            return true;
        }

        private static void AddDuplicateIssue(
            HashSet<string> seen,
            string cardId,
            LineupPresetAssignmentGroup group,
            int slotIndex,
            ICollection<LineupPresetValidationIssue> issues)
        {
            if (string.IsNullOrWhiteSpace(cardId)) return;
            if (seen.Add(cardId)) return;
            issues.Add(CreateIssue(
                LineupPresetValidationIssueCode.DuplicateCard,
                LineupPresetIssueSeverity.Error,
                group,
                slotIndex,
                cardId,
                "같은 선수를 중복 배치할 수 없습니다."));
        }

        private static void ValidateLoadoutIds(
            IReadOnlyList<string> selectedIds,
            IReadOnlyList<string> availableIds,
            bool canValidate,
            LineupPresetAssignmentGroup group,
            LineupPresetValidationIssueCode unavailableCode,
            ICollection<LineupPresetValidationIssue> issues)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < selectedIds.Count; index++)
            {
                string selectedId = selectedIds[index];
                if (string.IsNullOrWhiteSpace(selectedId)) continue;
                if (!seen.Add(selectedId))
                {
                    issues.Add(CreateIssue(
                        LineupPresetValidationIssueCode.DuplicateCard,
                        LineupPresetIssueSeverity.Error,
                        group,
                        index,
                        selectedId,
                        "같은 항목을 두 슬롯에 중복 장착할 수 없습니다."));
                }
                if (canValidate && !Contains(availableIds, selectedId))
                {
                    issues.Add(CreateIssue(
                        unavailableCode,
                        LineupPresetIssueSeverity.Incomplete,
                        group,
                        index,
                        selectedId,
                        "현재 선택 가능한 항목이 아닙니다."));
                }
            }
        }

        private static bool Contains(IReadOnlyList<string> values, string expected)
        {
            for (int index = 0; index < values.Count; index++)
                if (string.Equals(values[index], expected, StringComparison.Ordinal)) return true;
            return false;
        }

        private static LineupPresetValidationIssue CreateIssue(
            LineupPresetValidationIssueCode code,
            LineupPresetIssueSeverity severity,
            LineupPresetAssignmentGroup group,
            int slotIndex,
            string cardId,
            string context)
        {
            return new LineupPresetValidationIssue(code, severity, group, slotIndex, cardId, context);
        }
    }
}
