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
            PlayerAvailabilityStatus availability,
            int condition = 100,
            int conditionLevel = 10,
            string conditionLabel = "절정",
            PitchingWorkloadState pitchingWorkload = default)
        {
            if (condition < 0 || condition > 100)
                throw new ArgumentOutOfRangeException(nameof(condition));
            if (conditionLevel < 1 || conditionLevel > 10)
                throw new ArgumentOutOfRangeException(nameof(conditionLevel));
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
            Condition = condition;
            ConditionLevel = conditionLevel;
            ConditionLabel = string.IsNullOrWhiteSpace(conditionLabel) ? "상태 확인 필요" : conditionLabel.Trim();
            PitchingWorkload = pitchingWorkload;
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
        public int Condition { get; }
        public int ConditionLevel { get; }
        public string ConditionLabel { get; }
        public PitchingWorkloadState PitchingWorkload { get; }
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
            : this(id, displayName, string.Empty, 0)
        {
        }

        public OwnerLoadoutCandidateSnapshot(string id, string displayName, string artworkKey, int ownedCount)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("후보 ID가 필요합니다.", nameof(id));
            if (ownedCount < 0) throw new ArgumentOutOfRangeException(nameof(ownedCount));
            Id = id.Trim();
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? Id : displayName.Trim();
            ArtworkKey = artworkKey ?? string.Empty;
            OwnedCount = ownedCount;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public string ArtworkKey { get; }
        public int OwnedCount { get; }
    }

    /// <summary>Game의 로스터 및 프리셋 Resolver 결과를 변경 없이 묶은 화면 Snapshot이다.</summary>
    public sealed class OwnerRosterLineupSnapshot
    {
        private readonly OwnerRosterPlayerSnapshot[] _players;
        private readonly OwnerCollectionCardSnapshot[] _ownedPlayers;
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
            IReadOnlyList<OwnerLoadoutCandidateSnapshot> tacticCandidates,
            IReadOnlyList<OwnerCollectionCardSnapshot> ownedPlayers = null)
        {
            RosterStatus = rosterStatus ?? throw new ArgumentNullException(nameof(rosterStatus));
            if (players == null) throw new ArgumentNullException(nameof(players));
            _players = new OwnerRosterPlayerSnapshot[players.Count];
            for (int index = 0; index < players.Count; index++)
                _players[index] = players[index] ?? throw new ArgumentException("null 선수 Snapshot이 있습니다.", nameof(players));
            _ownedPlayers = ownedPlayers == null
                ? CreateOwnedPlayerFallback(_players)
                : CopyRequired(ownedPlayers, nameof(ownedPlayers));
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
        public IReadOnlyList<OwnerCollectionCardSnapshot> OwnedPlayers => _ownedPlayers;
        public IReadOnlyList<OwnerRosterPresetSnapshot> Presets => _presets;
        public int SelectedPresetIndex { get; }
        public LineupPresetState Preset => _presets[SelectedPresetIndex].Preset;
        public LineupPresetValidationResult PresetValidation => _presets[SelectedPresetIndex].Validation;
        public string ValidationUnavailableReason => _presets[SelectedPresetIndex].ValidationUnavailableReason;
        public IReadOnlyList<OwnerLoadoutCandidateSnapshot> TeamColorCandidates => _teamColorCandidates;
        public IReadOnlyList<OwnerLoadoutCandidateSnapshot> TacticCandidates => _tacticCandidates;

        /// <summary>저장 상태를 바꾸지 않고 선택 프리셋만 Validator 결과와 함께 교체한 Preview Snapshot을 만든다.</summary>
        public OwnerRosterLineupSnapshot CreatePreview(
            LineupPresetState previewPreset,
            LineupPresetValidationResult validation)
        {
            if (previewPreset == null) throw new ArgumentNullException(nameof(previewPreset));
            if (!string.Equals(previewPreset.PresetId, Preset.PresetId, StringComparison.Ordinal))
                throw new ArgumentException("현재 선택 프리셋과 Preview ID가 다릅니다.", nameof(previewPreset));

            var previews = new OwnerRosterPresetSnapshot[_presets.Length];
            for (int index = 0; index < previews.Length; index++)
            {
                OwnerRosterPresetSnapshot source = _presets[index];
                previews[index] = index == SelectedPresetIndex
                    ? new OwnerRosterPresetSnapshot(previewPreset, validation)
                    : source;
            }

            return new OwnerRosterLineupSnapshot(
                RosterStatus,
                _players,
                previews,
                previewPreset.PresetId,
                _teamColorCandidates,
                _tacticCandidates,
                _ownedPlayers);
        }

        private OwnerRosterLineupSnapshot(
            OwnerModeRosterStatus rosterStatus,
            OwnerRosterPlayerSnapshot[] players,
            OwnerRosterPresetSnapshot[] presets,
            string selectedPresetId,
            OwnerLoadoutCandidateSnapshot[] teamColorCandidates,
            OwnerLoadoutCandidateSnapshot[] tacticCandidates,
            OwnerCollectionCardSnapshot[] ownedPlayers)
        {
            RosterStatus = rosterStatus ?? throw new ArgumentNullException(nameof(rosterStatus));
            _players = players ?? throw new ArgumentNullException(nameof(players));
            _presets = presets ?? throw new ArgumentNullException(nameof(presets));
            _teamColorCandidates = teamColorCandidates ?? throw new ArgumentNullException(nameof(teamColorCandidates));
            _tacticCandidates = tacticCandidates ?? throw new ArgumentNullException(nameof(tacticCandidates));
            _ownedPlayers = ownedPlayers ?? throw new ArgumentNullException(nameof(ownedPlayers));

            for (int index = 0; index < _presets.Length; index++)
            {
                if (!string.Equals(_presets[index].Preset.PresetId, selectedPresetId, StringComparison.Ordinal))
                    continue;
                SelectedPresetIndex = index;
                return;
            }
            throw new ArgumentException("선택된 프리셋이 저장 목록에 없습니다.", nameof(selectedPresetId));
        }

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

        private static OwnerCollectionCardSnapshot[] CreateOwnedPlayerFallback(
            IReadOnlyList<OwnerRosterPlayerSnapshot> players)
        {
            var result = new OwnerCollectionCardSnapshot[players.Count];
            for (int index = 0; index < players.Count; index++)
            {
                OwnerRosterPlayerSnapshot player = players[index];
                result[index] = new OwnerCollectionCardSnapshot(
                    player.CardId,
                    player.CardId,
                    player.DisplayName,
                    player.OriginYear,
                    player.NaturalPosition,
                    player.Cost,
                    player.Edition,
                    0,
                    0,
                    false,
                    false);
            }
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
            string warningText,
            OwnerRosterPlayerSnapshot player = null)
        {
            Group = group;
            Index = index;
            Label = label ?? string.Empty;
            PlayerText = playerText ?? string.Empty;
            WarningText = warningText ?? string.Empty;
            Player = player;
        }

        public OwnerLineupSwapGroup Group { get; }
        public int Index { get; }
        public string Label { get; }
        public string PlayerText { get; }
        public string WarningText { get; }
        public OwnerRosterPlayerSnapshot Player { get; }
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
        public bool CanSave => Snapshot.RosterStatus.Validation.IsValid &&
                               Snapshot.PresetValidation != null &&
                               Snapshot.PresetValidation.Status == LineupPresetValidationStatus.Valid;

        /// <summary>내부 Validator 용어 없이 저장 전 배치에서 플레이어가 취할 행동만 안내한다.</summary>
        public string CreatePendingChangeMessage(
            int rosterReplacementCount = 0,
            int clearedTeamColorCount = 0)
        {
            if (rosterReplacementCount < 0)
                throw new ArgumentOutOfRangeException(nameof(rosterReplacementCount));
            if (clearedTeamColorCount < 0 || clearedTeamColorCount > LineupPresetState.TeamColorSlotCount)
                throw new ArgumentOutOfRangeException(nameof(clearedTeamColorCount));
            string rosterChange = rosterReplacementCount > 0
                ? $"1군 교체 {rosterReplacementCount}건 · "
                : string.Empty;
            string teamColorChange = clearedTeamColorCount > 0
                ? $"발동 조건을 잃은 팀컬러 {clearedTeamColorCount}개 해제 · "
                : string.Empty;
            return CanSave
                ? rosterChange + teamColorChange + "변경 내용을 확인한 뒤 배치 저장을 눌러 주세요."
                : rosterChange + teamColorChange + "저장할 수 없습니다. " + CreateCompactValidationText();
        }

        private string CreateCompactValidationText()
        {
            int firstLineBreak = ValidationText.IndexOf('\n');
            if (firstLineBreak < 0) return ValidationText;
            int additionalIssueCount = 1;
            for (int index = firstLineBreak + 1; index < ValidationText.Length; index++)
                if (ValidationText[index] == '\n') additionalIssueCount++;
            return ValidationText.Substring(0, firstLineBreak) + $" 외 {additionalIssueCount}건";
        }

        public string RosterSummaryText =>
            $"1군 {Snapshot.RosterStatus.ActiveRosterCount}/{Snapshot.RosterStatus.ActiveRosterCapacity} · " +
            $"야수 {Snapshot.RosterStatus.HitterCount}/{Snapshot.RosterStatus.RequiredHitterCount} · " +
            $"투수 {Snapshot.RosterStatus.PitcherCount}/{Snapshot.RosterStatus.RequiredPitcherCount} · " +
            $"외국인 {Snapshot.RosterStatus.ForeignPlayerCount}/{Snapshot.RosterStatus.ForeignPlayerLimit}";

        public string EvaluationText =>
            OwnerRosterEvaluationFormatter.FormatStrength(Snapshot.RosterStatus.Strength) + "\n" +
            OwnerRosterEvaluationFormatter.FormatUnits(Snapshot.RosterStatus.Strength) + "\n" +
            OwnerRosterEvaluationFormatter.FormatCost(Snapshot.RosterStatus.Cost);

        public string EvaluationBasisText =>
            "전력: 등록 선수의 시즌 기본 6능력 평균\n" +
            "훈련·카드 보너스·컨디션 미포함\n" +
            "비용: 주전 타자 9명+투수 11명";

        public string TeamColorSlotText(int slotIndex) => FormatLoadoutSlot(
            "팀컬러",
            slotIndex,
            Snapshot.Preset.TeamColorIds,
            Snapshot.TeamColorCandidates);

        public string TacticSlotText(int slotIndex) => FormatLoadoutSlot(
            "전술카드",
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
                : index == relief.Length - 2 ? "셋업" : "마무리";
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
            OwnerRosterPlayerSnapshot player = null;
            if (!string.IsNullOrEmpty(cardId) && players.TryGetValue(cardId, out player))
            {
                string foreign = player.RegistrationType == RegistrationType.Foreign ? " · 외국인" : string.Empty;
                playerText = $"{player.DisplayName} · {player.OriginYear} · {FormatPosition(player.NaturalPosition)} · " +
                $"비용 {player.Cost} · {FormatEdition(player.Edition)}{foreign}";
            }
            return new OwnerLineupSlotModel(group, index, label, playerText,
                FindIssue(snapshot.PresetValidation, issueGroup, index, cardId), player);
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
                lines.Add($"1군 · {FormatRosterIssueCode(issue.Code)} · 필요 {issue.Expected}, 현재 {issue.Actual}");
            }
            if (snapshot.PresetValidation == null)
            {
                lines.Add(string.IsNullOrWhiteSpace(snapshot.ValidationUnavailableReason)
                    ? "경기 프리셋 검증 결과 없음"
                    : snapshot.ValidationUnavailableReason);
            }
            else
            {
                AddValidationIssues(lines, snapshot.PresetValidation.Issues, includeWarnings: false);
                AddValidationIssues(lines, snapshot.PresetValidation.Issues, includeWarnings: true);
            }
            return lines.Count == 0 ? "현재 1군과 경기 프리셋이 구성 검증을 통과했습니다." : string.Join("\n", lines);
        }

        private static void AddValidationIssues(
            ICollection<string> lines,
            IReadOnlyList<LineupPresetValidationIssue> issues,
            bool includeWarnings)
        {
            for (int index = 0; index < issues.Count; index++)
            {
                LineupPresetValidationIssue issue = issues[index];
                bool isWarning = issue.Severity == LineupPresetIssueSeverity.Warning;
                if (isWarning != includeWarnings) continue;
                lines.Add(FormatIssue(issue));
            }
        }

        private static string FormatIssue(LineupPresetValidationIssue issue)
        {
            string penalty = issue.ConditionPenalty > 0 ? $" · 컨디션 -{issue.ConditionPenalty}" : string.Empty;
            string errorRisk = issue.FieldingErrorProbabilityMultiplier > 1d
                ? $" · 실책 위험 ×{issue.FieldingErrorProbabilityMultiplier:0.##}" : string.Empty;
            string detail = FormatLineupIssueCode(issue.Code);
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
                RosterValidationIssueCode.SetupPitcherCount => "셋업 투수 인원",
                RosterValidationIssueCode.CloserPitcherCount => "마무리 투수 인원",
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
                LineupPresetValidationIssueCode.OffPositionAssignment => "익숙하지 않은 수비 위치",
                LineupPresetValidationIssueCode.PitcherRoleMismatch => "익숙하지 않은 투수 역할",
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

        public static string FormatEdition(PlayerCardEdition edition)
        {
            return Baseball.Game.Historical.PlayerCardEditionText.Get(edition);
        }

        public static string FormatPosition(PlayerPosition position)
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

    /// <summary>투수 역할 화면에서 한 선수를 비교할 때 사용하는 실제 부하·구종·기록 문구다.</summary>
    public sealed class OwnerPitchingPlayerPresentationModel
    {
        internal OwnerPitchingPlayerPresentationModel(
            OwnerLineupSlotModel slot,
            string conditionText,
            string workloadText,
            string pitchesText,
            string recentRecordText)
        {
            Slot = slot ?? throw new ArgumentNullException(nameof(slot));
            ConditionText = conditionText ?? string.Empty;
            WorkloadText = workloadText ?? string.Empty;
            PitchesText = pitchesText ?? string.Empty;
            RecentRecordText = recentRecordText ?? string.Empty;
        }

        public OwnerLineupSlotModel Slot { get; }
        public string ConditionText { get; }
        public string WorkloadText { get; }
        public string PitchesText { get; }
        public string RecentRecordText { get; }
    }

    /// <summary>라인업 원본을 투수 Rotation·Bullpen 전용 View State로 투영한다.</summary>
    public sealed class OwnerRosterPitchingPresentationModel
    {
        internal OwnerRosterPitchingPresentationModel(
            OwnerRosterLineupPresentationModel lineup,
            IReadOnlyList<OwnerPitchingPlayerPresentationModel> pitchers)
        {
            Lineup = lineup ?? throw new ArgumentNullException(nameof(lineup));
            Pitchers = pitchers ?? throw new ArgumentNullException(nameof(pitchers));
        }

        public OwnerRosterLineupPresentationModel Lineup { get; }
        public IReadOnlyList<OwnerPitchingPlayerPresentationModel> Pitchers { get; }

        public OwnerPitchingPlayerPresentationModel Find(string cardId)
        {
            for (int index = 0; index < Pitchers.Count; index++)
                if (string.Equals(Pitchers[index].Slot.Player?.CardId, cardId, StringComparison.Ordinal))
                    return Pitchers[index];
            return null;
        }
    }

    /// <summary>저장된 최근 3일 투구 부하와 카드 원본 구종·시즌 기록을 투수 역할에 결합한다.</summary>
    public static class OwnerRosterPitchingPresentationBuilder
    {
        public static OwnerRosterPitchingPresentationModel Build(OwnerRosterLineupPresentationModel lineup)
        {
            if (lineup == null) throw new ArgumentNullException(nameof(lineup));
            var result = new OwnerPitchingPlayerPresentationModel[
                lineup.StarterRotation.Count + lineup.ReliefPitching.Count];
            int targetIndex = 0;
            for (int index = 0; index < lineup.StarterRotation.Count; index++)
                result[targetIndex++] = BuildPlayer(lineup.Snapshot, lineup.StarterRotation[index]);
            for (int index = 0; index < lineup.ReliefPitching.Count; index++)
                result[targetIndex++] = BuildPlayer(lineup.Snapshot, lineup.ReliefPitching[index]);
            return new OwnerRosterPitchingPresentationModel(lineup, result);
        }

        private static OwnerPitchingPlayerPresentationModel BuildPlayer(
            OwnerRosterLineupSnapshot snapshot,
            OwnerLineupSlotModel slot)
        {
            if (slot.Player == null)
                return new OwnerPitchingPlayerPresentationModel(
                    slot,
                    "컨디션 · 미지정",
                    "최근 투구 부하 · 미지정",
                    "구종 · 선수 배정 필요",
                    "시즌 기록 · 선수 배정 필요");

            OwnerRosterPlayerSnapshot player = slot.Player;
            PitchingWorkloadState workload = player.PitchingWorkload;
            int recentPitches = checked(
                workload.PreviousDayPitches + workload.TwoDaysAgoPitches + workload.ThreeDaysAgoPitches);
            int restDays = workload.PreviousDayPitches > 0 ? 0 :
                workload.TwoDaysAgoPitches > 0 ? 1 : workload.ThreeDaysAgoPitches > 0 ? 2 : 3;
            string workloadText = recentPitches == 0
                ? "최근 3일 등판 없음 · 휴식 3일 이상"
                : $"최근 3일 {recentPitches}구 · 휴식 {restDays}일 · " +
                  $"일별 {workload.PreviousDayPitches}/{workload.TwoDaysAgoPitches}/{workload.ThreeDaysAgoPitches}구";
            OwnerCollectionCardSnapshot card = FindOwnedCard(snapshot.OwnedPlayers, player.CardId);
            return new OwnerPitchingPlayerPresentationModel(
                slot,
                $"컨디션 · {player.ConditionLabel} {player.ConditionLevel}단계",
                workloadText,
                FormatPitches(card),
                FormatRecord(card));
        }

        private static OwnerCollectionCardSnapshot FindOwnedCard(
            IReadOnlyList<OwnerCollectionCardSnapshot> cards,
            string cardId)
        {
            for (int index = 0; index < cards.Count; index++)
                if (string.Equals(cards[index].CardId, cardId, StringComparison.Ordinal)) return cards[index];
            return null;
        }

        private static string FormatPitches(OwnerCollectionCardSnapshot card)
        {
            if (card == null || card.Pitches.Count == 0) return "구종 · 기록 없음";
            var values = new string[card.Pitches.Count];
            for (int index = 0; index < values.Length; index++)
            {
                OwnerPitchCardSnapshot pitch = card.Pitches[index];
                values[index] = $"{pitch.DisplayName} {pitch.Grade} {pitch.VelocityKph:0.#}km/h";
            }
            return "구종 · " + string.Join(" / ", values);
        }

        private static string FormatRecord(OwnerCollectionCardSnapshot card)
        {
            if (card == null || card.SeasonRecord.Count == 0) return "시즌 기록 · 기록 없음";
            var values = new string[card.SeasonRecord.Count];
            for (int index = 0; index < values.Length; index++)
                values[index] = $"{card.SeasonRecord[index].Label} {card.SeasonRecord[index].Value}";
            return "시즌 기록 · " + string.Join(" · ", values);
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

        /// <summary>선택 슬롯에 이미 1군인 카드를 배치하며 기존 역할이 있으면 두 선수의 역할을 맞바꾼다.</summary>
        public static LineupPresetState AssignCard(
            LineupPresetState source,
            OwnerLineupSwapGroup targetGroup,
            int targetIndex,
            string incomingCardId)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (string.IsNullOrWhiteSpace(incomingCardId))
                throw new ArgumentException("배치할 CardId가 필요합니다.", nameof(incomingCardId));
            string targetCardId = GetAssignedCardId(source, targetGroup, targetIndex);
            string incomingId = incomingCardId.Trim();
            if (string.Equals(targetCardId, incomingId, StringComparison.Ordinal)) return source;

            if (TryFindAssignment(source, targetGroup, incomingId, out int sourceIndex))
                return Swap(source, targetGroup, targetIndex, sourceIndex);
            return SwapCardIdentities(source, targetCardId, incomingId);
        }

        /// <summary>1군에서 빠질 카드 ID를 새 보유 카드 ID로 모든 경기 역할에서 일관되게 교체한다.</summary>
        public static LineupPresetState ReplaceCard(
            LineupPresetState source,
            string outgoingCardId,
            string incomingCardId)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (string.IsNullOrWhiteSpace(outgoingCardId))
                throw new ArgumentException("교체할 CardId가 필요합니다.", nameof(outgoingCardId));
            if (string.IsNullOrWhiteSpace(incomingCardId))
                throw new ArgumentException("등록할 CardId가 필요합니다.", nameof(incomingCardId));
            string outgoingId = outgoingCardId.Trim();
            string incomingId = incomingCardId.Trim();
            if (string.Equals(outgoingId, incomingId, StringComparison.Ordinal)) return source;

            LineupPresetState result = ReplaceCardIdentity(source, outgoingId, incomingId);
            if (ReferenceEquals(result, source))
                throw new InvalidOperationException("교체할 카드가 프리셋에 배치되어 있지 않습니다.");
            return result;
        }

        public static string GetAssignedCardId(
            LineupPresetState source,
            OwnerLineupSwapGroup group,
            int index)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return group switch
            {
                OwnerLineupSwapGroup.DefensiveLineup => GetDefenseCardId(source.StartingLineupSlots, index),
                OwnerLineupSwapGroup.BattingOrder => GetId(source.BattingOrderCardIds, index),
                OwnerLineupSwapGroup.Bench => GetId(source.BenchPriorityCardIds, index),
                OwnerLineupSwapGroup.StarterRotation => GetId(source.StarterRotationCardIds, index),
                OwnerLineupSwapGroup.ReliefPitching => GetReliefCardId(source, index),
                _ => throw new ArgumentOutOfRangeException(nameof(group))
            };
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

        private static bool TryFindAssignment(
            LineupPresetState source,
            OwnerLineupSwapGroup group,
            string cardId,
            out int foundIndex)
        {
            int count = group switch
            {
                OwnerLineupSwapGroup.DefensiveLineup => source.StartingLineupSlots.Count,
                OwnerLineupSwapGroup.BattingOrder => source.BattingOrderCardIds.Count,
                OwnerLineupSwapGroup.Bench => source.BenchPriorityCardIds.Count,
                OwnerLineupSwapGroup.StarterRotation => source.StarterRotationCardIds.Count,
                OwnerLineupSwapGroup.ReliefPitching => source.BullpenAssignmentCardIds.Count + 2,
                _ => throw new ArgumentOutOfRangeException(nameof(group))
            };
            for (int index = 0; index < count; index++)
            {
                if (!string.Equals(GetAssignedCardId(source, group, index), cardId, StringComparison.Ordinal))
                    continue;
                foundIndex = index;
                return true;
            }
            foundIndex = -1;
            return false;
        }

        private static LineupPresetState SwapCardIdentities(
            LineupPresetState source,
            string firstCardId,
            string secondCardId)
        {
            var defense = new LineupPresetSlot[source.StartingLineupSlots.Count];
            for (int index = 0; index < defense.Length; index++)
            {
                LineupPresetSlot slot = source.StartingLineupSlots[index];
                defense[index] = new LineupPresetSlot(
                    SwapIdentity(slot.CardId, firstCardId, secondCardId),
                    slot.Position);
            }
            return new LineupPresetState(
                source.PresetId,
                source.Name,
                defense,
                SwapIdentities(source.BattingOrderCardIds, firstCardId, secondCardId),
                SwapIdentities(source.BenchPriorityCardIds, firstCardId, secondCardId),
                SwapIdentities(source.StarterRotationCardIds, firstCardId, secondCardId),
                SwapIdentities(source.BullpenAssignmentCardIds, firstCardId, secondCardId),
                SwapIdentity(source.SetupPitcherCardId, firstCardId, secondCardId),
                SwapIdentity(source.CloserPitcherCardId, firstCardId, secondCardId),
                source.TeamColorIds,
                source.DefaultTacticCardIds);
        }

        private static LineupPresetState ReplaceCardIdentity(
            LineupPresetState source,
            string oldCardId,
            string newCardId)
        {
            bool changed = false;
            var defense = new LineupPresetSlot[source.StartingLineupSlots.Count];
            for (int index = 0; index < defense.Length; index++)
            {
                LineupPresetSlot slot = source.StartingLineupSlots[index];
                string cardId = ReplaceIdentity(slot.CardId, oldCardId, newCardId, ref changed);
                defense[index] = new LineupPresetSlot(cardId, slot.Position);
            }
            string[] batting = ReplaceIdentities(source.BattingOrderCardIds, oldCardId, newCardId, ref changed);
            string[] bench = ReplaceIdentities(source.BenchPriorityCardIds, oldCardId, newCardId, ref changed);
            string[] starters = ReplaceIdentities(source.StarterRotationCardIds, oldCardId, newCardId, ref changed);
            string[] bullpen = ReplaceIdentities(source.BullpenAssignmentCardIds, oldCardId, newCardId, ref changed);
            string setup = ReplaceIdentity(source.SetupPitcherCardId, oldCardId, newCardId, ref changed);
            string closer = ReplaceIdentity(source.CloserPitcherCardId, oldCardId, newCardId, ref changed);
            if (!changed) return source;
            return new LineupPresetState(source.PresetId, source.Name, defense, batting, bench, starters,
                bullpen, setup, closer, source.TeamColorIds, source.DefaultTacticCardIds);
        }

        private static string GetDefenseCardId(IReadOnlyList<LineupPresetSlot> slots, int index)
        {
            ValidateIndices(slots.Count, index, index);
            return slots[index].CardId;
        }

        private static string GetId(IReadOnlyList<string> values, int index)
        {
            ValidateIndices(values.Count, index, index);
            return values[index];
        }

        private static string GetReliefCardId(LineupPresetState source, int index)
        {
            int bullpenCount = source.BullpenAssignmentCardIds.Count;
            ValidateIndices(bullpenCount + 2, index, index);
            if (index < bullpenCount) return source.BullpenAssignmentCardIds[index];
            return index == bullpenCount ? source.SetupPitcherCardId : source.CloserPitcherCardId;
        }

        private static string[] SwapIdentities(
            IReadOnlyList<string> source,
            string firstCardId,
            string secondCardId)
        {
            var result = new string[source.Count];
            for (int index = 0; index < result.Length; index++)
                result[index] = SwapIdentity(source[index], firstCardId, secondCardId);
            return result;
        }

        private static string SwapIdentity(string value, string firstCardId, string secondCardId)
        {
            if (string.Equals(value, firstCardId, StringComparison.Ordinal)) return secondCardId;
            if (string.Equals(value, secondCardId, StringComparison.Ordinal)) return firstCardId;
            return value;
        }

        private static string[] ReplaceIdentities(
            IReadOnlyList<string> source,
            string oldCardId,
            string newCardId,
            ref bool changed)
        {
            var result = new string[source.Count];
            for (int index = 0; index < result.Length; index++)
                result[index] = ReplaceIdentity(source[index], oldCardId, newCardId, ref changed);
            return result;
        }

        private static string ReplaceIdentity(
            string value,
            string oldCardId,
            string newCardId,
            ref bool changed)
        {
            if (!string.Equals(value, oldCardId, StringComparison.Ordinal)) return value;
            changed = true;
            return newCardId;
        }
    }
}
