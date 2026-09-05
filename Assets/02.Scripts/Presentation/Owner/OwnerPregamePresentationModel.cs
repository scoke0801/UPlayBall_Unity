using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Presentation.SharedUI;

namespace Baseball.Presentation.Owner
{
    /// <summary>Resolver가 계산한 선수별 Condition과 Chemistry 표시 결과를 경기 준비 UI에 전달한다.</summary>
    public sealed class OwnerPregamePlayerSnapshot
    {
        public OwnerPregamePlayerSnapshot(
            string cardId,
            string displayName,
            string positionText,
            string baseConditionText,
            string lineupChemistryText,
            string batteryChemistryText,
            string expectedConditionText)
        {
            if (string.IsNullOrWhiteSpace(cardId)) throw new ArgumentException("CardId가 필요합니다.", nameof(cardId));
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("선수 이름이 필요합니다.", nameof(displayName));
            CardId = cardId.Trim();
            DisplayName = displayName.Trim();
            PositionText = positionText ?? string.Empty;
            BaseConditionText = baseConditionText ?? string.Empty;
            LineupChemistryText = lineupChemistryText ?? string.Empty;
            BatteryChemistryText = batteryChemistryText ?? string.Empty;
            ExpectedConditionText = expectedConditionText ?? string.Empty;
        }

        public string CardId { get; }
        public string DisplayName { get; }
        public string PositionText { get; }
        public string BaseConditionText { get; }
        public string LineupChemistryText { get; }
        public string BatteryChemistryText { get; }
        public string ExpectedConditionText { get; }
    }

    /// <summary>저장된 프리셋 하나와 현재 Runtime 재검증 결과를 함께 전달한다.</summary>
    public sealed class OwnerPregamePresetSnapshot
    {
        public OwnerPregamePresetSnapshot(string presetId, string displayName, LineupPresetValidationResult validation)
        {
            if (string.IsNullOrWhiteSpace(presetId)) throw new ArgumentException("PresetId가 필요합니다.", nameof(presetId));
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("프리셋 이름이 필요합니다.", nameof(displayName));
            Validation = validation ?? throw new ArgumentNullException(nameof(validation));
            if (!string.Equals(presetId.Trim(), validation.PresetId, StringComparison.Ordinal))
                throw new ArgumentException("프리셋과 검증 결과의 ID가 다릅니다.", nameof(validation));
            PresetId = presetId.Trim();
            DisplayName = displayName.Trim();
        }

        public string PresetId { get; }
        public string DisplayName { get; }
        public LineupPresetValidationResult Validation { get; }
    }

    /// <summary>Game 레이어가 상대 분석과 현재 프리셋 결과를 한 번 준비한 불변 UI 입력이다.</summary>
    public sealed class OwnerPregameSnapshot
    {
        private readonly OwnerPregamePresetSnapshot[] _presets;
        private readonly OwnerPregamePlayerSnapshot[] _lineup;
        private readonly string[] _teamColors;
        private readonly string[] _tactics;
        private readonly Dictionary<string, string> _displayTexts;

        public OwnerPregameSnapshot(
            UiContentStateModel contentState,
            string nextMatchText,
            string opponentName,
            OpponentScoutingReport scoutingReport,
            IReadOnlyList<OwnerPregamePresetSnapshot> presets,
            string selectedPresetId,
            IReadOnlyList<OwnerPregamePlayerSnapshot> lineup,
            IReadOnlyList<string> teamColors,
            IReadOnlyList<string> tactics,
            IReadOnlyDictionary<string, string> displayTexts,
            bool isMatchStartAvailable,
            string matchStartUnavailableReason = null)
        {
            ContentState = contentState ?? throw new ArgumentNullException(nameof(contentState));
            NextMatchText = nextMatchText ?? string.Empty;
            OpponentName = opponentName ?? string.Empty;
            ScoutingReport = scoutingReport;
            _presets = CopyRequired(presets, nameof(presets));
            _lineup = CopyRequired(lineup, nameof(lineup));
            _teamColors = CopyText(teamColors, LineupPresetState.TeamColorSlotCount, nameof(teamColors));
            _tactics = CopyText(tactics, LineupPresetState.MaximumTacticCardCount, nameof(tactics), true);
            _displayTexts = CopyMap(displayTexts);
            SelectedPresetId = Normalize(selectedPresetId);
            IsMatchStartAvailable = isMatchStartAvailable;
            MatchStartUnavailableReason = matchStartUnavailableReason ?? string.Empty;

            if (ContentState.Kind == UiContentStateKind.Ready)
            {
                if (scoutingReport == null) throw new ArgumentNullException(nameof(scoutingReport));
                if (_presets.Length == 0) throw new ArgumentException("경기 준비에는 프리셋이 필요합니다.", nameof(presets));
                bool found = false;
                for (int index = 0; index < _presets.Length; index++)
                    found |= string.Equals(_presets[index].PresetId, SelectedPresetId, StringComparison.Ordinal);
                if (!found) throw new ArgumentException("선택된 프리셋이 목록에 없습니다.", nameof(selectedPresetId));
            }
            if (!isMatchStartAvailable && string.IsNullOrWhiteSpace(MatchStartUnavailableReason))
                throw new ArgumentException("경기 시작 불가 사유가 필요합니다.", nameof(matchStartUnavailableReason));
        }

        public UiContentStateModel ContentState { get; }
        public string NextMatchText { get; }
        public string OpponentName { get; }
        public OpponentScoutingReport ScoutingReport { get; }
        public IReadOnlyList<OwnerPregamePresetSnapshot> Presets => _presets;
        public string SelectedPresetId { get; }
        public IReadOnlyList<OwnerPregamePlayerSnapshot> Lineup => _lineup;
        public IReadOnlyList<string> TeamColors => _teamColors;
        public IReadOnlyList<string> Tactics => _tactics;
        public bool IsMatchStartAvailable { get; }
        public string MatchStartUnavailableReason { get; }

        public string ResolveText(string key, string fallback = null)
        {
            if (!string.IsNullOrWhiteSpace(key) && _displayTexts.TryGetValue(key.Trim(), out string value))
                return value;
            return string.IsNullOrWhiteSpace(fallback) ? "확인 불가" : fallback;
        }

        private static T[] CopyRequired<T>(IReadOnlyList<T> source, string parameterName) where T : class
        {
            if (source == null) return Array.Empty<T>();
            var result = new T[source.Count];
            for (int index = 0; index < result.Length; index++)
                result[index] = source[index] ?? throw new ArgumentException("null 항목이 있습니다.", parameterName);
            return result;
        }

        private static string[] CopyText(IReadOnlyList<string> source, int count, string parameterName, bool maximum = false)
        {
            if (source == null) throw new ArgumentNullException(parameterName);
            if ((!maximum && source.Count != count) || (maximum && source.Count > count))
                throw new ArgumentException($"{parameterName} 항목 수가 올바르지 않습니다.", parameterName);
            var result = new string[source.Count];
            for (int index = 0; index < result.Length; index++) result[index] = source[index] ?? string.Empty;
            return result;
        }

        private static Dictionary<string, string> CopyMap(IReadOnlyDictionary<string, string> source)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (source == null) return result;
            foreach (KeyValuePair<string, string> pair in source)
            {
                if (!string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
                    result.Add(pair.Key.Trim(), pair.Value.Trim());
            }
            return result;
        }

        private static string Normalize(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public sealed class OwnerPregamePresetModel
    {
        internal OwnerPregamePresetModel(string presetId, string displayName, string statusText, bool isSelected)
        {
            PresetId = presetId;
            DisplayName = displayName;
            StatusText = statusText;
            IsSelected = isSelected;
        }

        public string PresetId { get; }
        public string DisplayName { get; }
        public string StatusText { get; }
        public bool IsSelected { get; }
    }

    public sealed class OwnerPregamePlayerModel
    {
        internal OwnerPregamePlayerModel(OwnerPregamePlayerSnapshot source, string warningText)
        {
            CardId = source.CardId;
            DisplayName = source.DisplayName;
            PositionText = source.PositionText;
            BaseConditionText = source.BaseConditionText;
            LineupChemistryText = source.LineupChemistryText;
            BatteryChemistryText = source.BatteryChemistryText;
            ExpectedConditionText = source.ExpectedConditionText;
            WarningText = warningText ?? string.Empty;
        }

        public string CardId { get; }
        public string DisplayName { get; }
        public string PositionText { get; }
        public string BaseConditionText { get; }
        public string LineupChemistryText { get; }
        public string BatteryChemistryText { get; }
        public string ExpectedConditionText { get; }
        public string WarningText { get; }
    }

    /// <summary>경기 준비 View가 표시만 수행하도록 모든 문구와 활성 상태를 동결한다.</summary>
    public sealed class OwnerPregamePresentationModel
    {
        internal OwnerPregamePresentationModel(
            OwnerPregameSnapshot snapshot,
            IReadOnlyList<OwnerPregamePresetModel> presets,
            IReadOnlyList<OwnerPregamePlayerModel> lineup,
            IReadOnlyList<string> expectedLineup,
            IReadOnlyList<string> bullpen,
            IReadOnlyList<string> threats,
            string intelText,
            string probableStarterText,
            string recentFormText,
            string managerTendencyText,
            bool canStart,
            string startReason)
        {
            Snapshot = snapshot;
            Presets = Copy(presets);
            Lineup = Copy(lineup);
            ExpectedLineup = Copy(expectedLineup);
            Bullpen = Copy(bullpen);
            KeyThreats = Copy(threats);
            IntelText = intelText;
            ProbableStarterText = probableStarterText;
            RecentFormText = recentFormText;
            ManagerTendencyText = managerTendencyText;
            CanStartMatch = canStart;
            MatchStartDisabledReason = startReason;
        }

        public OwnerPregameSnapshot Snapshot { get; }
        public IReadOnlyList<OwnerPregamePresetModel> Presets { get; }
        public IReadOnlyList<OwnerPregamePlayerModel> Lineup { get; }
        public IReadOnlyList<string> ExpectedLineup { get; }
        public IReadOnlyList<string> Bullpen { get; }
        public IReadOnlyList<string> KeyThreats { get; }
        public string IntelText { get; }
        public string ProbableStarterText { get; }
        public string RecentFormText { get; }
        public string ManagerTendencyText { get; }
        public bool CanStartMatch { get; }
        public string MatchStartDisabledReason { get; }

        private static T[] Copy<T>(IReadOnlyList<T> source)
        {
            var result = new T[source?.Count ?? 0];
            for (int index = 0; index < result.Length; index++) result[index] = source[index];
            return result;
        }
    }

    /// <summary>Scouting/Validation/Condition Resolver 결과를 재계산하지 않고 고밀도 표시 모델로 변환한다.</summary>
    public static class OwnerPregamePresentationBuilder
    {
        public static OwnerPregamePresentationModel Build(OwnerPregameSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.ContentState.Kind != UiContentStateKind.Ready)
                return new OwnerPregamePresentationModel(snapshot, Array.Empty<OwnerPregamePresetModel>(),
                    Array.Empty<OwnerPregamePlayerModel>(), Array.Empty<string>(), Array.Empty<string>(),
                    Array.Empty<string>(), "정보 부족", "확인 불가", "확인 불가", "확인 불가", false,
                    snapshot.ContentState.Message);

            OpponentScoutingReport report = snapshot.ScoutingReport;
            OwnerPregamePresetSnapshot selected = FindPreset(snapshot.Presets, snapshot.SelectedPresetId);
            var presets = new OwnerPregamePresetModel[snapshot.Presets.Count];
            for (int index = 0; index < presets.Length; index++)
            {
                OwnerPregamePresetSnapshot preset = snapshot.Presets[index];
                presets[index] = new OwnerPregamePresetModel(
                    preset.PresetId,
                    preset.DisplayName,
                    FormatValidation(preset.Validation.Status),
                    string.Equals(preset.PresetId, snapshot.SelectedPresetId, StringComparison.Ordinal));
            }

            var players = new OwnerPregamePlayerModel[snapshot.Lineup.Count];
            for (int index = 0; index < players.Length; index++)
                players[index] = new OwnerPregamePlayerModel(
                    snapshot.Lineup[index],
                    FindWarning(selected.Validation.Issues, snapshot.Lineup[index].CardId));

            bool canStart = snapshot.IsMatchStartAvailable && selected.Validation.CanStartGame;
            string reason = canStart ? string.Empty : !snapshot.IsMatchStartAvailable
                ? snapshot.MatchStartUnavailableReason
                : BuildValidationReason(selected.Validation.Issues);
            return new OwnerPregamePresentationModel(
                snapshot,
                presets,
                players,
                BuildExpectedLineup(report, snapshot),
                BuildBullpen(report, snapshot),
                BuildNotes(report.KeyThreats, snapshot),
                FormatConfidence(report.ReportConfidenceSummary.State, report.ReportConfidenceSummary.Confidence01),
                FormatProbableStarter(report.ProbableStarter, snapshot),
                FormatRecentForm(report.RecentForm),
                FormatManagerTendency(report.ManagerTendencyEstimate, snapshot),
                canStart,
                reason);
        }

        public static string FormatIntelState(IntelState state)
        {
            return state switch
            {
                IntelState.Confirmed => "확정",
                IntelState.HighConfidence => "높은 신뢰",
                IntelState.Estimated => "추정",
                IntelState.LowConfidence => "낮은 신뢰",
                _ => "정보 부족"
            };
        }

        private static string FormatConfidence(IntelState state, double confidence)
        {
            return state == IntelState.Unknown
                ? "정보 부족"
                : $"{FormatIntelState(state)} · {confidence:P0}";
        }

        private static string FormatProbableStarter(ScoutedValue<ProbableStarterProjection> value, OwnerPregameSnapshot snapshot)
        {
            if (!value.HasValue) return "확인 불가";
            string name = snapshot.ResolveText(value.Value.Player.CardId, value.Value.Player.CardId);
            return $"{name} · {value.Value.ThrowingHand} · {FormatIntelState(value.State)}";
        }

        private static string FormatRecentForm(ScoutedValue<OpponentRecentForm> value)
        {
            return value.HasValue
                ? $"{value.Value.Wins}승 {value.Value.Losses}패 {value.Value.Ties}무 · {FormatIntelState(value.State)}"
                : "확인 불가";
        }

        private static string FormatManagerTendency(ScoutedValue<ManagerTendencyEstimate> value, OwnerPregameSnapshot snapshot)
        {
            if (!value.HasValue || value.Value.TendencyKeys.Count == 0) return "정보 부족";
            var labels = new string[value.Value.TendencyKeys.Count];
            for (int index = 0; index < labels.Length; index++)
                labels[index] = snapshot.ResolveText(value.Value.TendencyKeys[index], "추정 정보");
            return $"{string.Join(" · ", labels)} · {FormatIntelState(value.State)}";
        }

        private static string[] BuildExpectedLineup(OpponentScoutingReport report, OwnerPregameSnapshot snapshot)
        {
            if (report.ExpectedLineup.Count == 0) return new[] { "정보 부족" };
            var rows = new string[report.ExpectedLineup.Count];
            for (int index = 0; index < rows.Length; index++)
            {
                ScoutedValue<ExpectedLineupEntry> value = report.ExpectedLineup[index];
                rows[index] = value.HasValue
                    ? $"{value.Value.BattingOrder}. {snapshot.ResolveText(value.Value.Player.CardId, value.Value.Player.CardId)} · " +
                      $"{value.Value.Position} · {FormatIntelState(value.State)}"
                    : $"{index + 1}. 확인 불가";
            }
            return rows;
        }

        private static string[] BuildBullpen(OpponentScoutingReport report, OwnerPregameSnapshot snapshot)
        {
            if (report.BullpenReadiness.Count == 0) return new[] { "정보 부족" };
            var rows = new string[report.BullpenReadiness.Count];
            for (int index = 0; index < rows.Length; index++)
            {
                ScoutedValue<BullpenReadinessEntry> value = report.BullpenReadiness[index];
                rows[index] = value.HasValue
                    ? $"{snapshot.ResolveText(value.Value.Player.CardId, value.Value.Player.CardId)} · " +
                      $"{FormatReadiness(value.Value.Readiness)} · {FormatIntelState(value.State)}"
                    : "확인 불가";
            }
            return rows;
        }

        private static string[] BuildNotes(IReadOnlyList<ScoutingReportNote> notes, OwnerPregameSnapshot snapshot)
        {
            if (notes.Count == 0) return new[] { "정보 부족" };
            var result = new string[notes.Count];
            for (int index = 0; index < result.Length; index++)
            {
                ScoutingReportNote note = notes[index];
                string subject = string.IsNullOrEmpty(note.SubjectCardId)
                    ? string.Empty
                    : $" · {snapshot.ResolveText(note.SubjectCardId, note.SubjectCardId)}";
                result[index] = snapshot.ResolveText(note.NoteKey, "추정 위협") + subject;
            }
            return result;
        }

        private static string FormatReadiness(BullpenReadiness readiness)
        {
            return readiness switch
            {
                BullpenReadiness.Fresh => "충분한 휴식",
                BullpenReadiness.Available => "등판 가능",
                BullpenReadiness.Tired => "피로 추정",
                BullpenReadiness.VeryTired => "강한 피로 추정",
                _ => "등판 불가"
            };
        }

        private static string FormatValidation(LineupPresetValidationStatus status)
        {
            return status switch
            {
                LineupPresetValidationStatus.Valid => "사용 가능",
                LineupPresetValidationStatus.PartiallyValid => "수정 필요",
                _ => "사용 불가"
            };
        }

        private static string FindWarning(IReadOnlyList<LineupPresetValidationIssue> issues, string cardId)
        {
            for (int index = 0; index < issues.Count; index++)
                if (string.Equals(issues[index].CardId, cardId, StringComparison.Ordinal)) return issues[index].Context;
            return string.Empty;
        }

        private static string BuildValidationReason(IReadOnlyList<LineupPresetValidationIssue> issues)
        {
            for (int index = 0; index < issues.Count; index++)
                if (issues[index].Severity != LineupPresetIssueSeverity.Warning) return issues[index].Context;
            return "현재 프리셋을 다시 확인해 주세요.";
        }

        private static OwnerPregamePresetSnapshot FindPreset(IReadOnlyList<OwnerPregamePresetSnapshot> presets, string presetId)
        {
            for (int index = 0; index < presets.Count; index++)
                if (string.Equals(presets[index].PresetId, presetId, StringComparison.Ordinal)) return presets[index];
            throw new InvalidOperationException("선택된 프리셋이 없습니다.");
        }
    }
}
