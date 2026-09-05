using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Historical;
using Baseball.Simulation.Historical;

namespace Baseball.Presentation.Owner
{
    /// <summary>프리셋에서 같은 성격의 두 슬롯을 교환하는 UI Command 범위다.</summary>
    public enum OwnerLineupSwapGroup
    {
        DefensiveLineup,
        BattingOrder,
        Bench,
        StarterRotation,
        ReliefPitching
    }

    /// <summary>현재 1군 카드 한 장의 실제 정의와 등록 상태를 UI에 전달한다.</summary>
    public sealed class OwnerRosterPlayerSnapshot
    {
        public OwnerRosterPlayerSnapshot(
            string cardId,
            string displayName,
            int originYear,
            PlayerPosition naturalPosition,
            PitcherRole pitcherRole,
            PlayerCardEdition edition,
            int cost,
            RegistrationType registrationType,
            ActiveRosterRole activeRosterRole,
            PlayerAvailabilityStatus availability)
        {
            CardId = cardId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            OriginYear = originYear;
            NaturalPosition = naturalPosition;
            PitcherRole = pitcherRole;
            Edition = edition;
            Cost = cost;
            RegistrationType = registrationType;
            ActiveRosterRole = activeRosterRole;
            Availability = availability;
        }

        public string CardId { get; }
        public string DisplayName { get; }
        public int OriginYear { get; }
        public PlayerPosition NaturalPosition { get; }
        public PitcherRole PitcherRole { get; }
        public PlayerCardEdition Edition { get; }
        public int Cost { get; }
        public RegistrationType RegistrationType { get; }
        public ActiveRosterRole ActiveRosterRole { get; }
        public PlayerAvailabilityStatus Availability { get; }
    }

    /// <summary>저장 프리셋 하나와 현재 Runtime에서 다시 계산한 Validator 결과다.</summary>
    public sealed class OwnerRosterPresetSnapshot
    {
        public OwnerRosterPresetSnapshot(
            LineupPresetState preset,
            LineupPresetValidationResult validation,
            string validationUnavailableReason = null)
        {
            Preset = preset ?? throw new ArgumentNullException(nameof(preset));
            if (validation != null && !string.Equals(
                    validation.PresetId,
                    preset.PresetId,
                    StringComparison.Ordinal))
                throw new ArgumentException("프리셋과 Validator 결과의 ID가 다릅니다.", nameof(validation));
            Validation = validation;
            ValidationUnavailableReason = validationUnavailableReason ?? string.Empty;
        }

        public LineupPresetState Preset { get; }
        public LineupPresetValidationResult Validation { get; }
        public string ValidationUnavailableReason { get; }
    }

    /// <summary>실제 Runtime catalog에서 선택 가능한 장착 후보의 ID와 표시 이름이다.</summary>
    public sealed class OwnerLoadoutCandidateSnapshot
    {
        public OwnerLoadoutCandidateSnapshot(string id, string displayName)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("후보 ID가 필요합니다.", nameof(id));
            Id = id.Trim();
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? Id : displayName.Trim();
        }

        public string Id { get; }
        public string DisplayName { get; }
    }

    /// <summary>Game의 로스터 및 프리셋 Resolver 결과를 변경 없이 묶은 화면 Snapshot이다.</summary>
    public sealed class OwnerRosterLineupSnapshot
    {
        private readonly OwnerRosterPlayerSnapshot[] _players;
        private readonly OwnerRosterPresetSnapshot[] _presets;
        private readonly OwnerLoadoutCandidateSnapshot[] _teamColorCandidates;
        private readonly OwnerLoadoutCandidateSnapshot[] _tacticCandidates;

        public OwnerRosterLineupSnapshot(
            OwnerModeRosterStatus rosterStatus,
            IReadOnlyList<OwnerRosterPlayerSnapshot> players,
            LineupPresetState preset,
            LineupPresetValidationResult presetValidation,
            string validationUnavailableReason)
            : this(
                rosterStatus,
                players,
                new[] { new OwnerRosterPresetSnapshot(preset, presetValidation, validationUnavailableReason) },
                preset?.PresetId,
                Array.Empty<OwnerLoadoutCandidateSnapshot>(),
                Array.Empty<OwnerLoadoutCandidateSnapshot>())
        {
        }

        public OwnerRosterLineupSnapshot(
            OwnerModeRosterStatus rosterStatus,
            IReadOnlyList<OwnerRosterPlayerSnapshot> players,
            IReadOnlyList<OwnerRosterPresetSnapshot> presets,
            string selectedPresetId,
            IReadOnlyList<OwnerLoadoutCandidateSnapshot> teamColorCandidates,
            IReadOnlyList<OwnerLoadoutCandidateSnapshot> tacticCandidates)
        {
            RosterStatus = rosterStatus ?? throw new ArgumentNullException(nameof(rosterStatus));
            if (players == null) throw new ArgumentNullException(nameof(players));
            _players = new OwnerRosterPlayerSnapshot[players.Count];
            for (int index = 0; index < players.Count; index++)
                _players[index] = players[index] ?? throw new ArgumentException("null 선수 Snapshot이 있습니다.", nameof(players));
            _presets = CopyRequired(presets, nameof(presets));
            _teamColorCandidates = CopyRequired(teamColorCandidates, nameof(teamColorCandidates));
            _tacticCandidates = CopyRequired(tacticCandidates, nameof(tacticCandidates));
            string normalizedSelectedId = string.IsNullOrWhiteSpace(selectedPresetId)
                ? string.Empty
                : selectedPresetId.Trim();
            for (int index = 0; index < _presets.Length; index++)
            {
                if (!string.Equals(_presets[index].Preset.PresetId, normalizedSelectedId, StringComparison.Ordinal))
                    continue;
                SelectedPresetIndex = index;
                return;
            }
            throw new ArgumentException("선택된 프리셋이 저장 목록에 없습니다.", nameof(selectedPresetId));
        }

        public OwnerModeRosterStatus RosterStatus { get; }
        public IReadOnlyList<OwnerRosterPlayerSnapshot> Players => _players;
        public IReadOnlyList<OwnerRosterPresetSnapshot> Presets => _presets;
        public int SelectedPresetIndex { get; }
        public LineupPresetState Preset => _presets[SelectedPresetIndex].Preset;
        public LineupPresetValidationResult PresetValidation => _presets[SelectedPresetIndex].Validation;
        public string ValidationUnavailableReason => _presets[SelectedPresetIndex].ValidationUnavailableReason;
        public IReadOnlyList<OwnerLoadoutCandidateSnapshot> TeamColorCandidates => _teamColorCandidates;
        public IReadOnlyList<OwnerLoadoutCandidateSnapshot> TacticCandidates => _tacticCandidates;

        private static T[] CopyRequired<T>(IReadOnlyList<T> source, string parameterName) where T : class
        {
            if (source == null) throw new ArgumentNullException(parameterName);
            if (source.Count == 0 && typeof(T) == typeof(OwnerRosterPresetSnapshot))
                throw new ArgumentException("하나 이상의 저장 프리셋이 필요합니다.", parameterName);
            var result = new T[source.Count];
            for (int index = 0; index < result.Length; index++)
                result[index] = source[index] ?? throw new ArgumentException("null Snapshot이 있습니다.", parameterName);
            return result;
        }
    }

    public sealed class OwnerRosterPresetChoiceModel
    {
        internal OwnerRosterPresetChoiceModel(string presetId, string name, string statusText, bool isSelected)
        {
            PresetId = presetId;
            Name = name;
            StatusText = statusText;
            IsSelected = isSelected;
        }

        public string PresetId { get; }
        public string Name { get; }
        public string StatusText { get; }
        public bool IsSelected { get; }
    }

    /// <summary>한 역할 슬롯에 표시할 선수와 Resolver 경고다.</summary>
    public sealed class OwnerLineupSlotModel
    {
        public OwnerLineupSlotModel(
            OwnerLineupSwapGroup group,
            int index,
            string label,
            string playerText,
            string warningText)
        {
            Group = group;
            Index = index;
            Label = label ?? string.Empty;
            PlayerText = playerText ?? string.Empty;
            WarningText = warningText ?? string.Empty;
        }

        public OwnerLineupSwapGroup Group { get; }
        public int Index { get; }
        public string Label { get; }
        public string PlayerText { get; }
        public string WarningText { get; }
        public bool HasWarning => !string.IsNullOrEmpty(WarningText);
    }

    /// <summary>25인 등록과 경기 프리셋 역할을 한 Workspace에서 표시하는 모델이다.</summary>
    public sealed class OwnerRosterLineupPresentationModel
    {
        internal OwnerRosterLineupPresentationModel(
            OwnerRosterLineupSnapshot snapshot,
            IReadOnlyList<OwnerRosterPresetChoiceModel> presets,
            IReadOnlyList<OwnerLineupSlotModel> defensiveLineup,
            IReadOnlyList<OwnerLineupSlotModel> battingOrder,
            IReadOnlyList<OwnerLineupSlotModel> bench,
            IReadOnlyList<OwnerLineupSlotModel> starterRotation,
            IReadOnlyList<OwnerLineupSlotModel> reliefPitching,
            string validationText)
        {
            Snapshot = snapshot;
            Presets = presets;
            DefensiveLineup = defensiveLineup;
            BattingOrder = battingOrder;
            Bench = bench;
            StarterRotation = starterRotation;
            ReliefPitching = reliefPitching;
            ValidationText = validationText ?? string.Empty;
        }

        public OwnerRosterLineupSnapshot Snapshot { get; }
        public IReadOnlyList<OwnerRosterPresetChoiceModel> Presets { get; }
        public IReadOnlyList<OwnerLineupSlotModel> DefensiveLineup { get; }
        public IReadOnlyList<OwnerLineupSlotModel> BattingOrder { get; }
        public IReadOnlyList<OwnerLineupSlotModel> Bench { get; }
        public IReadOnlyList<OwnerLineupSlotModel> StarterRotation { get; }
        public IReadOnlyList<OwnerLineupSlotModel> ReliefPitching { get; }
        public string ValidationText { get; }
        public string RosterSummaryText =>
            $"1군 {Snapshot.RosterStatus.ActiveRosterCount}/{Snapshot.RosterStatus.ActiveRosterCapacity} · " +
            $"야수 {Snapshot.RosterStatus.HitterCount}/{Snapshot.RosterStatus.RequiredHitterCount} · " +
            $"투수 {Snapshot.RosterStatus.PitcherCount}/{Snapshot.RosterStatus.RequiredPitcherCount} · " +
            $"외국인 {Snapshot.RosterStatus.ForeignPlayerCount}/{Snapshot.RosterStatus.ForeignPlayerLimit}";

        public string TeamColorSlotText(int slotIndex) => FormatLoadoutSlot(
            "TC",
            slotIndex,
            Snapshot.Preset.TeamColorIds,
            Snapshot.TeamColorCandidates);

        public string TacticSlotText(int slotIndex) => FormatLoadoutSlot(
            "전술",
            slotIndex,
            Snapshot.Preset.DefaultTacticCardIds,
            Snapshot.TacticCandidates);

        private static string FormatLoadoutSlot(
            string prefix,
            int slotIndex,
            IReadOnlyList<string> selectedIds,
            IReadOnlyList<OwnerLoadoutCandidateSnapshot> candidates)
        {
            string selectedId = slotIndex < selectedIds.Count ? selectedIds[slotIndex] : null;
            string displayName = string.IsNullOrEmpty(selectedId) ? "선택 없음" : $"{selectedId} · 사용 불가";
            for (int index = 0; index < candidates.Count; index++)
            {
                if (!string.Equals(candidates[index].Id, selectedId, StringComparison.Ordinal)) continue;
                displayName = candidates[index].DisplayName;
                break;
            }
            return $"{prefix}{slotIndex + 1} · {displayName}";
        }
    }

    /// <summary>Runtime Snapshot을 고밀도 역할 슬롯과 읽기 가능한 Resolver 근거로 변환한다.</summary>
    public static class OwnerRosterLineupPresentationBuilder
    {
        public static OwnerRosterLineupPresentationModel Build(OwnerRosterLineupSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var players = new Dictionary<string, OwnerRosterPlayerSnapshot>(StringComparer.Ordinal);
            for (int index = 0; index < snapshot.Players.Count; index++)
                players[snapshot.Players[index].CardId] = snapshot.Players[index];

            LineupPresetState preset = snapshot.Preset;
            var presets = new OwnerRosterPresetChoiceModel[snapshot.Presets.Count];
            for (int index = 0; index < presets.Length; index++)
            {
                OwnerRosterPresetSnapshot candidate = snapshot.Presets[index];
                string status = candidate.Validation == null
                    ? "검증 대기"
                    : candidate.Validation.Status == LineupPresetValidationStatus.Valid
                        ? "사용 가능"
                        : candidate.Validation.Status == LineupPresetValidationStatus.PartiallyValid
                            ? "수정 필요"
                            : "사용 불가";
                presets[index] = new OwnerRosterPresetChoiceModel(
                    candidate.Preset.PresetId,
                    candidate.Preset.Name,
                    status,
                    index == snapshot.SelectedPresetIndex);
            }
            var defense = new OwnerLineupSlotModel[preset.StartingLineupSlots.Count];
            for (int index = 0; index < defense.Length; index++)
                defense[index] = CreateSlot(snapshot, players, OwnerLineupSwapGroup.DefensiveLineup, index,
                    FormatPosition(preset.StartingLineupSlots[index].Position), preset.StartingLineupSlots[index].CardId,
                    LineupPresetAssignmentGroup.StartingLineup);
            var batting = CreateIdSlots(snapshot, players, OwnerLineupSwapGroup.BattingOrder,
                preset.BattingOrderCardIds, "번", LineupPresetAssignmentGroup.BattingOrder);
            var bench = CreateIdSlots(snapshot, players, OwnerLineupSwapGroup.Bench,
                preset.BenchPriorityCardIds, "순위", LineupPresetAssignmentGroup.Bench);
            var starters = CreateIdSlots(snapshot, players, OwnerLineupSwapGroup.StarterRotation,
                preset.StarterRotationCardIds, "선발", LineupPresetAssignmentGroup.StarterRotation);
            var reliefIds = new string[preset.BullpenAssignmentCardIds.Count + 2];
            for (int index = 0; index < preset.BullpenAssignmentCardIds.Count; index++)
                reliefIds[index] = preset.BullpenAssignmentCardIds[index];
            reliefIds[reliefIds.Length - 2] = preset.SetupPitcherCardId;
            reliefIds[reliefIds.Length - 1] = preset.CloserPitcherCardId;
            var relief = new OwnerLineupSlotModel[reliefIds.Length];
            for (int index = 0; index < relief.Length; index++)
            {
                LineupPresetAssignmentGroup issueGroup = index < preset.BullpenAssignmentCardIds.Count
                    ? LineupPresetAssignmentGroup.Bullpen
                    : index == relief.Length - 2 ? LineupPresetAssignmentGroup.Setup : LineupPresetAssignmentGroup.Closer;
                string label = index < preset.BullpenAssignmentCardIds.Count
                    ? $"불펜 {index + 1}"
                    : index == relief.Length - 2 ? "Setup" : "Closer";
                relief[index] = CreateSlot(snapshot, players, OwnerLineupSwapGroup.ReliefPitching,
                    index, label, reliefIds[index], issueGroup);
            }

            return new OwnerRosterLineupPresentationModel(snapshot, presets, defense, batting, bench, starters, relief,
                BuildValidationText(snapshot));
        }

        private static OwnerLineupSlotModel[] CreateIdSlots(
            OwnerRosterLineupSnapshot snapshot,
            IDictionary<string, OwnerRosterPlayerSnapshot> players,
            OwnerLineupSwapGroup group,
            IReadOnlyList<string> cardIds,
            string labelSuffix,
            LineupPresetAssignmentGroup issueGroup)
        {
            var result = new OwnerLineupSlotModel[cardIds.Count];
            for (int index = 0; index < result.Length; index++)
                result[index] = CreateSlot(snapshot, players, group, index,
                    $"{index + 1}{labelSuffix}", cardIds[index], issueGroup);
            return result;
        }

        private static OwnerLineupSlotModel CreateSlot(
            OwnerRosterLineupSnapshot snapshot,
            IDictionary<string, OwnerRosterPlayerSnapshot> players,
            OwnerLineupSwapGroup group,
            int index,
            string label,
            string cardId,
            LineupPresetAssignmentGroup issueGroup)
        {
            string playerText = "미지정";
            if (!string.IsNullOrEmpty(cardId) && players.TryGetValue(cardId, out OwnerRosterPlayerSnapshot player))
            {
                string foreign = player.RegistrationType == RegistrationType.Foreign ? " · 외국인" : string.Empty;
                playerText = $"{player.DisplayName} · {player.OriginYear} · {FormatPosition(player.NaturalPosition)} · " +
                    $"Cost {player.Cost} · {FormatEdition(player.Edition)}{foreign}";
            }
            return new OwnerLineupSlotModel(group, index, label, playerText,
                FindIssue(snapshot.PresetValidation, issueGroup, index, cardId));
        }

        private static string FindIssue(
            LineupPresetValidationResult validation,
            LineupPresetAssignmentGroup group,
            int index,
            string cardId)
        {
            if (validation == null) return string.Empty;
            for (int issueIndex = 0; issueIndex < validation.Issues.Count; issueIndex++)
            {
                LineupPresetValidationIssue issue = validation.Issues[issueIndex];
                if (issue.Group != group) continue;
                if (issue.SlotIndex >= 0 && issue.SlotIndex != index) continue;
                if (!string.IsNullOrEmpty(issue.CardId) && !string.Equals(issue.CardId, cardId, StringComparison.Ordinal))
                    continue;
                return FormatIssue(issue);
            }
            return string.Empty;
        }

        private static string BuildValidationText(OwnerRosterLineupSnapshot snapshot)
        {
            var lines = new List<string>();
            RosterValidationResult roster = snapshot.RosterStatus.Validation;
            for (int index = 0; index < roster.Issues.Count; index++)
            {
                RosterValidationIssue issue = roster.Issues[index];
                string context = string.IsNullOrWhiteSpace(issue.Context) ? string.Empty : $" · {issue.Context}";
                lines.Add($"1군 · {FormatRosterIssueCode(issue.Code)}{context} · 필요 {issue.Expected}, 현재 {issue.Actual}");
            }
            if (snapshot.PresetValidation == null)
            {
                lines.Add(string.IsNullOrWhiteSpace(snapshot.ValidationUnavailableReason)
                    ? "경기 프리셋 검증 결과 없음"
                    : snapshot.ValidationUnavailableReason);
            }
            else
            {
                for (int index = 0; index < snapshot.PresetValidation.Issues.Count; index++)
                    lines.Add(FormatIssue(snapshot.PresetValidation.Issues[index]));
            }
            return lines.Count == 0 ? "현재 1군과 경기 프리셋이 Resolver 검증을 통과했습니다." : string.Join("\n", lines);
        }

        private static string FormatIssue(LineupPresetValidationIssue issue)
        {
            string penalty = issue.ConditionPenalty > 0 ? $" · Condition -{issue.ConditionPenalty}" : string.Empty;
            string errorRisk = issue.FieldingErrorProbabilityMultiplier > 1d
                ? $" · 실책 위험 ×{issue.FieldingErrorProbabilityMultiplier:0.##}" : string.Empty;
            string detail = string.IsNullOrWhiteSpace(issue.Context)
                ? FormatLineupIssueCode(issue.Code)
                : issue.Context;
            return $"{FormatSeverity(issue.Severity)} · {detail}{penalty}{errorRisk}";
        }

        private static string FormatRosterIssueCode(RosterValidationIssueCode code)
        {
            return code switch
            {
                RosterValidationIssueCode.TotalCount => "1군 총원",
                RosterValidationIssueCode.HitterCount => "야수 인원",
                RosterValidationIssueCode.StartingHitterCount => "주전 야수 인원",
                RosterValidationIssueCode.BenchHitterCount => "벤치 인원",
                RosterValidationIssueCode.PitcherCount => "투수 인원",
                RosterValidationIssueCode.StartingPitcherCount => "선발투수 인원",
                RosterValidationIssueCode.BullpenPitcherCount => "불펜 인원",
                RosterValidationIssueCode.SetupPitcherCount => "Setup 인원",
                RosterValidationIssueCode.CloserPitcherCount => "Closer 인원",
                RosterValidationIssueCode.ForeignPlayerCount => "외국인 등록",
                RosterValidationIssueCode.DuplicatePlayerPersonId => "동일 선수 중복",
                RosterValidationIssueCode.FixedRoleCount => "고정 역할 인원",
                _ => "로스터 구성"
            };
        }

        private static string FormatLineupIssueCode(LineupPresetValidationIssueCode code)
        {
            return code switch
            {
                LineupPresetValidationIssueCode.ActiveRosterInvalid => "1군 등록 오류",
                LineupPresetValidationIssueCode.MissingAssignment => "역할 미지정",
                LineupPresetValidationIssueCode.CardNotOnActiveRoster => "1군 미등록 선수",
                LineupPresetValidationIssueCode.CardUnavailable => "출전 불가 선수",
                LineupPresetValidationIssueCode.DuplicateCard => "동일 선수 중복 배치",
                LineupPresetValidationIssueCode.DuplicateDefensivePosition => "수비 위치 중복",
                LineupPresetValidationIssueCode.MissingDefensivePosition => "수비 위치 누락",
                LineupPresetValidationIssueCode.BattingOrderMismatch => "수비 라인업과 타순 불일치",
                LineupPresetValidationIssueCode.NonHitterAssignment => "야수 슬롯에 투수 배치",
                LineupPresetValidationIssueCode.NonPitcherAssignment => "투수 슬롯에 야수 배치",
                LineupPresetValidationIssueCode.PlayerContextMissing => "선수 상태 정보 누락",
                LineupPresetValidationIssueCode.OffPositionAssignment => "비주포지션 배치",
                LineupPresetValidationIssueCode.PitcherRoleMismatch => "투수 역할 불일치",
                LineupPresetValidationIssueCode.TeamColorUnavailable => "사용할 수 없는 팀컬러",
                LineupPresetValidationIssueCode.TacticCardUnavailable => "사용할 수 없는 전술카드",
                _ => "프리셋 확인 필요"
            };
        }

        private static string FormatSeverity(LineupPresetIssueSeverity severity)
        {
            return severity switch
            {
                LineupPresetIssueSeverity.Warning => "경고",
                LineupPresetIssueSeverity.Incomplete => "미완성",
                LineupPresetIssueSeverity.Error => "오류",
                _ => "확인 필요"
            };
        }

        private static string FormatEdition(PlayerCardEdition edition)
        {
            return edition switch
            {
                PlayerCardEdition.Normal => "일반",
                PlayerCardEdition.AllStar => "올스타",
                PlayerCardEdition.GoldenGlove => "골든글러브",
                PlayerCardEdition.Mvp => "MVP",
                _ => "Edition 확인 필요"
            };
        }

        private static string FormatPosition(PlayerPosition position)
        {
            return position switch
            {
                PlayerPosition.Catcher => "C",
                PlayerPosition.FirstBase => "1B",
                PlayerPosition.SecondBase => "2B",
                PlayerPosition.ThirdBase => "3B",
                PlayerPosition.Shortstop => "SS",
                PlayerPosition.LeftField => "LF",
                PlayerPosition.CenterField => "CF",
                PlayerPosition.RightField => "RF",
                PlayerPosition.DesignatedHitter => "DH",
                PlayerPosition.StartingPitcher => "SP",
                PlayerPosition.ReliefPitcher => "RP",
                _ => "-"
            };
        }
    }

    /// <summary>기존 프리셋의 다른 필드를 보존하면서 두 역할 슬롯만 교환한다.</summary>
    public static class OwnerLineupPresetCommandBuilder
    {
        public static LineupPresetState Swap(
            LineupPresetState source,
            OwnerLineupSwapGroup group,
            int firstIndex,
            int secondIndex)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var defense = CopyDefense(source.StartingLineupSlots);
            string[] batting = Copy(source.BattingOrderCardIds);
            string[] bench = Copy(source.BenchPriorityCardIds);
            string[] starters = Copy(source.StarterRotationCardIds);
            string[] bullpen = Copy(source.BullpenAssignmentCardIds);
            string setup = source.SetupPitcherCardId;
            string closer = source.CloserPitcherCardId;

            switch (group)
            {
                case OwnerLineupSwapGroup.DefensiveLineup:
                    ValidateIndices(defense.Length, firstIndex, secondIndex);
                    string firstCard = defense[firstIndex].CardId;
                    defense[firstIndex] = new LineupPresetSlot(defense[secondIndex].CardId, defense[firstIndex].Position);
                    defense[secondIndex] = new LineupPresetSlot(firstCard, defense[secondIndex].Position);
                    break;
                case OwnerLineupSwapGroup.BattingOrder:
                    SwapIds(batting, firstIndex, secondIndex);
                    break;
                case OwnerLineupSwapGroup.Bench:
                    SwapIds(bench, firstIndex, secondIndex);
                    break;
                case OwnerLineupSwapGroup.StarterRotation:
                    SwapIds(starters, firstIndex, secondIndex);
                    break;
                case OwnerLineupSwapGroup.ReliefPitching:
                    var relief = new string[bullpen.Length + 2];
                    Array.Copy(bullpen, relief, bullpen.Length);
                    relief[relief.Length - 2] = setup;
                    relief[relief.Length - 1] = closer;
                    SwapIds(relief, firstIndex, secondIndex);
                    Array.Copy(relief, bullpen, bullpen.Length);
                    setup = relief[relief.Length - 2];
                    closer = relief[relief.Length - 1];
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(group));
            }

            return new LineupPresetState(source.PresetId, source.Name, defense, batting, bench, starters,
                bullpen, setup, closer, source.TeamColorIds, source.DefaultTacticCardIds);
        }

        /// <summary>실제 활성 TeamColor 후보 안에서 한 슬롯만 순환하고 나머지 프리셋을 보존한다.</summary>
        public static LineupPresetState CycleTeamColor(
            LineupPresetState source,
            int slotIndex,
            IReadOnlyList<string> candidateIds)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            ValidateCandidateSlot(slotIndex, LineupPresetState.TeamColorSlotCount, candidateIds);
            string[] teamColors = Copy(source.TeamColorIds);
            teamColors[slotIndex] = FindNextDistinctCandidate(
                teamColors[slotIndex],
                teamColors[1 - slotIndex],
                candidateIds);
            return CopyWithLoadout(source, teamColors, source.DefaultTacticCardIds);
        }

        /// <summary>실제 보유 전술 후보 안에서 한 슬롯만 순환하고 중복 장착을 만들지 않는다.</summary>
        public static LineupPresetState CycleTactic(
            LineupPresetState source,
            int slotIndex,
            IReadOnlyList<string> candidateIds)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            ValidateCandidateSlot(slotIndex, LineupPresetState.MaximumTacticCardCount, candidateIds);
            if (slotIndex > source.DefaultTacticCardIds.Count)
                throw new InvalidOperationException("앞쪽 전술 슬롯을 먼저 선택해야 합니다.");
            int tacticCount = Math.Max(source.DefaultTacticCardIds.Count, slotIndex + 1);
            var tactics = new string[tacticCount];
            for (int index = 0; index < source.DefaultTacticCardIds.Count; index++)
                tactics[index] = source.DefaultTacticCardIds[index];
            tactics[slotIndex] = FindNextDistinctCandidate(
                tactics[slotIndex],
                tacticCount == LineupPresetState.MaximumTacticCardCount ? tactics[1 - slotIndex] : null,
                candidateIds);
            return CopyWithLoadout(source, source.TeamColorIds, tactics);
        }

        private static LineupPresetState CopyWithLoadout(
            LineupPresetState source,
            IReadOnlyList<string> teamColors,
            IReadOnlyList<string> tactics)
        {
            return new LineupPresetState(
                source.PresetId,
                source.Name,
                source.StartingLineupSlots,
                source.BattingOrderCardIds,
                source.BenchPriorityCardIds,
                source.StarterRotationCardIds,
                source.BullpenAssignmentCardIds,
                source.SetupPitcherCardId,
                source.CloserPitcherCardId,
                teamColors,
                tactics);
        }

        private static string FindNextDistinctCandidate(
            string currentId,
            string otherSlotId,
            IReadOnlyList<string> candidateIds)
        {
            int currentIndex = -1;
            for (int index = 0; index < candidateIds.Count; index++)
                if (string.Equals(candidateIds[index], currentId, StringComparison.Ordinal)) currentIndex = index;
            for (int offset = 1; offset <= candidateIds.Count; offset++)
            {
                string candidate = candidateIds[(currentIndex + offset + candidateIds.Count) % candidateIds.Count];
                if (!string.Equals(candidate, otherSlotId, StringComparison.Ordinal)) return candidate;
            }
            throw new InvalidOperationException("다른 슬롯과 중복되지 않는 장착 후보가 없습니다.");
        }

        private static void ValidateCandidateSlot(
            int slotIndex,
            int slotCount,
            IReadOnlyList<string> candidateIds)
        {
            if (slotIndex < 0 || slotIndex >= slotCount) throw new ArgumentOutOfRangeException(nameof(slotIndex));
            if (candidateIds == null || candidateIds.Count == 0)
                throw new InvalidOperationException("선택 가능한 장착 후보가 없습니다.");
            for (int index = 0; index < candidateIds.Count; index++)
                if (string.IsNullOrWhiteSpace(candidateIds[index]))
                    throw new ArgumentException("장착 후보 ID는 비어 있을 수 없습니다.", nameof(candidateIds));
        }

        private static LineupPresetSlot[] CopyDefense(IReadOnlyList<LineupPresetSlot> source)
        {
            var result = new LineupPresetSlot[source.Count];
            for (int index = 0; index < result.Length; index++)
                result[index] = new LineupPresetSlot(source[index].CardId, source[index].Position);
            return result;
        }

        private static string[] Copy(IReadOnlyList<string> source)
        {
            var result = new string[source.Count];
            for (int index = 0; index < result.Length; index++) result[index] = source[index];
            return result;
        }

        private static void SwapIds(string[] values, int firstIndex, int secondIndex)
        {
            ValidateIndices(values.Length, firstIndex, secondIndex);
            (values[firstIndex], values[secondIndex]) = (values[secondIndex], values[firstIndex]);
        }

        private static void ValidateIndices(int count, int firstIndex, int secondIndex)
        {
            if (firstIndex < 0 || firstIndex >= count) throw new ArgumentOutOfRangeException(nameof(firstIndex));
            if (secondIndex < 0 || secondIndex >= count) throw new ArgumentOutOfRangeException(nameof(secondIndex));
        }
    }
}
