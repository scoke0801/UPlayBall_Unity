using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Core.Players;
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

    /// <summary>
    /// 상대 분석 로스터 표에 한 줄로 나오는 선수와 우클릭 상세 카드를 함께 전달한다.
    /// 상대 구단은 Scouting Report가 공개한 선수만 담기므로, 정보가 없는 자리는 아예 들어오지 않는다.
    /// </summary>
    public sealed class OwnerPregameRosterCardSnapshot
    {
        public OwnerPregameRosterCardSnapshot(
            string cardId,
            string displayName,
            string positionText,
            bool isOwnTeam,
            bool isPitcher,
            OwnerCollectionCardSnapshot detail)
        {
            if (string.IsNullOrWhiteSpace(cardId)) throw new ArgumentException("CardId가 필요합니다.", nameof(cardId));
            CardId = cardId.Trim();
            DisplayName = displayName ?? string.Empty;
            PositionText = positionText ?? string.Empty;
            IsOwnTeam = isOwnTeam;
            IsPitcher = isPitcher;
            Detail = detail;
        }

        public string CardId { get; }
        public string DisplayName { get; }
        public string PositionText { get; }
        public bool IsOwnTeam { get; }
        public bool IsPitcher { get; }

        /// <summary>상세 카드를 만들 수 없으면 null이며, 그 행은 우클릭해도 상세가 열리지 않는다.</summary>
        public OwnerCollectionCardSnapshot Detail { get; }
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
        private readonly OwnerPregameRosterCardSnapshot[] _rosterCards;

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
            string matchStartUnavailableReason = null,
            int ownTeamEmblemId = 0,
            int opponentTeamEmblemId = 0,
            PlayerMiniCardModel ownStarterCard = null,
            OwnerCollectionCardSnapshot ownStarterDetail = null,
            PlayerMiniCardModel opponentStarterCard = null,
            OwnerCollectionCardSnapshot opponentStarterDetail = null,
            IReadOnlyList<OwnerPregameRosterCardSnapshot> rosterCards = null)
        {
            if (ownTeamEmblemId < 0) throw new ArgumentOutOfRangeException(nameof(ownTeamEmblemId));
            if (opponentTeamEmblemId < 0) throw new ArgumentOutOfRangeException(nameof(opponentTeamEmblemId));
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
            OwnTeamEmblemId = ownTeamEmblemId;
            OpponentTeamEmblemId = opponentTeamEmblemId;
            ValidateStarterDetail(ownStarterCard, ownStarterDetail, nameof(ownStarterDetail));
            ValidateStarterDetail(opponentStarterCard, opponentStarterDetail, nameof(opponentStarterDetail));
            OwnStarterCard = ownStarterCard;
            OwnStarterDetail = ownStarterDetail;
            OpponentStarterCard = opponentStarterCard;
            OpponentStarterDetail = opponentStarterDetail;
            _rosterCards = CopyRequired(rosterCards, nameof(rosterCards));

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
        public int OwnTeamEmblemId { get; }
        public int OpponentTeamEmblemId { get; }
        public PlayerMiniCardModel OwnStarterCard { get; }
        public OwnerCollectionCardSnapshot OwnStarterDetail { get; }
        public PlayerMiniCardModel OpponentStarterCard { get; }
        public OwnerCollectionCardSnapshot OpponentStarterDetail { get; }

        /// <summary>상대 분석 표에 나오는 양 구단 로스터 선수를 표시 순서대로 담는다.</summary>
        public IReadOnlyList<OwnerPregameRosterCardSnapshot> RosterCards => _rosterCards;

        /// <summary>표 행이 우클릭 상세로 열 카드를 찾는다. 공개되지 않은 선수는 null이다.</summary>
        public OwnerCollectionCardSnapshot FindRosterCardDetail(string cardId)
        {
            if (string.IsNullOrWhiteSpace(cardId)) return null;
            for (int index = 0; index < _rosterCards.Length; index++)
                if (string.Equals(_rosterCards[index].CardId, cardId, StringComparison.Ordinal))
                    return _rosterCards[index].Detail;
            return null;
        }

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

        private static void ValidateStarterDetail(
            PlayerMiniCardModel card,
            OwnerCollectionCardSnapshot detail,
            string parameterName)
        {
            if (card == null && detail == null) return;
            if (card == null || detail == null ||
                !string.Equals(card.PlayerId, detail.CardId, StringComparison.Ordinal))
                throw new ArgumentException("선발 Mini Card와 상세 정보의 CardId가 같아야 합니다.", parameterName);
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

    /// <summary>
    /// 상대 분석 로스터 표 한 행의 문구와 우클릭 상세 대상을 함께 동결한다.
    /// 표를 문자열로 넘기고 View가 다시 쪼개는 방식은 상세 카드를 붙일 CardId를 잃어버려서 쓰지 않는다.
    /// </summary>
    public sealed class OwnerPregameRosterRowModel
    {
        internal OwnerPregameRosterRowModel(
            string nameText,
            string positionText,
            string statusText,
            OwnerCollectionCardSnapshot detail)
        {
            NameText = nameText ?? string.Empty;
            PositionText = positionText ?? string.Empty;
            StatusText = statusText ?? string.Empty;
            Detail = detail;
        }

        public string NameText { get; }
        public string PositionText { get; }
        public string StatusText { get; }

        /// <summary>공개 정보가 없는 행은 null이며 우클릭해도 상세가 열리지 않는다.</summary>
        public OwnerCollectionCardSnapshot Detail { get; }
    }

    /// <summary>경기 준비 View가 표시만 수행하도록 모든 문구와 활성 상태를 동결한다.</summary>
    public sealed class OwnerPregamePresentationModel
    {
        internal OwnerPregamePresentationModel(
            OwnerPregameSnapshot snapshot,
            IReadOnlyList<OwnerPregamePresetModel> presets,
            IReadOnlyList<OwnerPregamePlayerModel> lineup,
            IReadOnlyList<OwnerPregameRosterRowModel> ownHitters,
            IReadOnlyList<OwnerPregameRosterRowModel> ownPitchers,
            IReadOnlyList<OwnerPregameRosterRowModel> opponentHitters,
            IReadOnlyList<OwnerPregameRosterRowModel> opponentPitchers,
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
            OwnHitterRows = Copy(ownHitters);
            OwnPitcherRows = Copy(ownPitchers);
            OpponentHitterRows = Copy(opponentHitters);
            OpponentPitcherRows = Copy(opponentPitchers);
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
        public IReadOnlyList<OwnerPregameRosterRowModel> OwnHitterRows { get; }
        public IReadOnlyList<OwnerPregameRosterRowModel> OwnPitcherRows { get; }
        public IReadOnlyList<OwnerPregameRosterRowModel> OpponentHitterRows { get; }
        public IReadOnlyList<OwnerPregameRosterRowModel> OpponentPitcherRows { get; }
        public IReadOnlyList<string> KeyThreats { get; }
        public string IntelText { get; }
        public string ProbableStarterText { get; }
        public string RecentFormText { get; }
        public string ManagerTendencyText { get; }
        public bool CanStartMatch { get; }
        public string MatchStartDisabledReason { get; }

        /// <summary>상대 분석 보드의 네 로스터 탭이 같은 행 모델을 쓰도록 한 곳에서 고른다.</summary>
        public IReadOnlyList<OwnerPregameRosterRowModel> GetRosterRows(bool isOwnTeam, bool pitchers)
        {
            if (isOwnTeam) return pitchers ? OwnPitcherRows : OwnHitterRows;
            return pitchers ? OpponentPitcherRows : OpponentHitterRows;
        }

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
                    Array.Empty<OwnerPregamePlayerModel>(),
                    Array.Empty<OwnerPregameRosterRowModel>(), Array.Empty<OwnerPregameRosterRowModel>(),
                    Array.Empty<OwnerPregameRosterRowModel>(), Array.Empty<OwnerPregameRosterRowModel>(),
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
            string reason = canStart ? string.Empty : !selected.Validation.CanStartGame
                ? BuildValidationReason(selected.Validation.Issues)
                : snapshot.MatchStartUnavailableReason;
            return new OwnerPregamePresentationModel(
                snapshot,
                presets,
                players,
                BuildOwnHitterRows(players, snapshot),
                BuildOwnPitcherRows(snapshot),
                BuildOpponentHitterRows(report, snapshot),
                BuildOpponentPitcherRows(report, snapshot),
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
            return $"{name} · {FormatHandedness(value.Value.ThrowingHand)} · {FormatIntelState(value.State)}";
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

        /// <summary>우리 야수 표는 이미 확정된 라인업이라 타순 그대로 컨디션·호흡을 보여준다.</summary>
        private static OwnerPregameRosterRowModel[] BuildOwnHitterRows(
            IReadOnlyList<OwnerPregamePlayerModel> lineup,
            OwnerPregameSnapshot snapshot)
        {
            if (lineup.Count == 0) return new[] { CreateMissingRow("정보 부족", "등록 정보 없음") };
            var rows = new OwnerPregameRosterRowModel[lineup.Count];
            for (int index = 0; index < rows.Length; index++)
            {
                OwnerPregamePlayerModel player = lineup[index];
                rows[index] = new OwnerPregameRosterRowModel(
                    player.DisplayName,
                    player.PositionText,
                    string.IsNullOrEmpty(player.WarningText)
                        ? "컨디션 " + player.ExpectedConditionText + " · 호흡 " + player.LineupChemistryText
                        : player.WarningText,
                    snapshot.FindRosterCardDetail(player.CardId));
            }
            return rows;
        }

        private static OwnerPregameRosterRowModel[] BuildOwnPitcherRows(OwnerPregameSnapshot snapshot)
        {
            var rows = new List<OwnerPregameRosterRowModel>();
            for (int index = 0; index < snapshot.RosterCards.Count; index++)
            {
                OwnerPregameRosterCardSnapshot card = snapshot.RosterCards[index];
                if (!card.IsOwnTeam || !card.IsPitcher) continue;
                rows.Add(new OwnerPregameRosterRowModel(
                    card.DisplayName, card.PositionText, "등록 로스터", card.Detail));
            }
            if (rows.Count == 0) rows.Add(CreateMissingRow("정보 부족", "등록 정보 없음"));
            return rows.ToArray();
        }

        /// <summary>상대 야수는 Scouting Report가 공개한 선수만 상세를 붙이고, 나머지는 확인 불가로 남긴다.</summary>
        private static OwnerPregameRosterRowModel[] BuildOpponentHitterRows(
            OpponentScoutingReport report,
            OwnerPregameSnapshot snapshot)
        {
            if (report.ExpectedLineup.Count == 0) return new[] { CreateMissingRow("정보 부족", "등록 정보 없음") };
            var rows = new OwnerPregameRosterRowModel[report.ExpectedLineup.Count];
            for (int index = 0; index < rows.Length; index++)
            {
                ScoutedValue<ExpectedLineupEntry> value = report.ExpectedLineup[index];
                if (!value.HasValue)
                {
                    rows[index] = CreateMissingRow($"{index + 1}. 확인 불가", "정보 부족");
                    continue;
                }
                string cardId = value.Value.Player.CardId;
                rows[index] = new OwnerPregameRosterRowModel(
                    $"{value.Value.BattingOrder}. {snapshot.ResolveText(cardId, cardId)}",
                    FormatPosition(value.Value.Position),
                    FormatIntelState(value.State),
                    snapshot.FindRosterCardDetail(cardId));
            }
            return rows;
        }

        private static OwnerPregameRosterRowModel[] BuildOpponentPitcherRows(
            OpponentScoutingReport report,
            OwnerPregameSnapshot snapshot)
        {
            if (report.BullpenReadiness.Count == 0) return new[] { CreateMissingRow("정보 부족", "등록 정보 없음") };
            var rows = new OwnerPregameRosterRowModel[report.BullpenReadiness.Count];
            for (int index = 0; index < rows.Length; index++)
            {
                ScoutedValue<BullpenReadinessEntry> value = report.BullpenReadiness[index];
                if (!value.HasValue)
                {
                    rows[index] = CreateMissingRow("확인 불가", "정보 부족");
                    continue;
                }
                string cardId = value.Value.Player.CardId;
                rows[index] = new OwnerPregameRosterRowModel(
                    snapshot.ResolveText(cardId, cardId),
                    FormatReadiness(value.Value.Readiness),
                    FormatIntelState(value.State),
                    snapshot.FindRosterCardDetail(cardId));
            }
            return rows;
        }

        private static OwnerPregameRosterRowModel CreateMissingRow(string nameText, string statusText)
        {
            return new OwnerPregameRosterRowModel(nameText, "—", statusText, null);
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

        private static string FormatHandedness(Handedness handedness)
        {
            return handedness switch
            {
                Handedness.Right => "우투",
                Handedness.Left => "좌투",
                _ => "투구 손 확인 필요"
            };
        }

        private static string FormatPosition(PlayerPosition position)
        {
            return position switch
            {
                PlayerPosition.Catcher => "포수",
                PlayerPosition.FirstBase => "1루수",
                PlayerPosition.SecondBase => "2루수",
                PlayerPosition.ThirdBase => "3루수",
                PlayerPosition.Shortstop => "유격수",
                PlayerPosition.LeftField => "좌익수",
                PlayerPosition.CenterField => "중견수",
                PlayerPosition.RightField => "우익수",
                PlayerPosition.DesignatedHitter => "지명타자",
                PlayerPosition.StartingPitcher => "선발투수",
                PlayerPosition.ReliefPitcher => "구원투수",
                _ => "포지션 확인 필요"
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
                if (string.Equals(issues[index].CardId, cardId, StringComparison.Ordinal))
                    return FormatLineupIssue(issues[index].Code);
            return string.Empty;
        }

        private static string BuildValidationReason(IReadOnlyList<LineupPresetValidationIssue> issues)
        {
            for (int index = 0; index < issues.Count; index++)
                if (issues[index].Severity != LineupPresetIssueSeverity.Warning)
                    return FormatLineupIssue(issues[index].Code);
            return "선수 배치를 다시 확인해 주세요.";
        }

        private static string FormatLineupIssue(LineupPresetValidationIssueCode code)
        {
            return code switch
            {
                LineupPresetValidationIssueCode.ActiveRosterInvalid => "1군 등록을 확인해 주세요",
                LineupPresetValidationIssueCode.MissingAssignment => "역할이 지정되지 않았습니다",
                LineupPresetValidationIssueCode.CardNotOnActiveRoster => "1군 미등록 선수입니다",
                LineupPresetValidationIssueCode.CardUnavailable => "현재 출전할 수 없습니다",
                LineupPresetValidationIssueCode.DuplicateCard => "같은 선수가 중복 배치되었습니다",
                LineupPresetValidationIssueCode.DuplicateDefensivePosition => "수비 위치가 겹칩니다",
                LineupPresetValidationIssueCode.MissingDefensivePosition => "수비 위치가 비어 있습니다",
                LineupPresetValidationIssueCode.BattingOrderMismatch => "타순과 수비 라인업이 다릅니다",
                LineupPresetValidationIssueCode.NonHitterAssignment => "야수 자리에 투수가 배치되었습니다",
                LineupPresetValidationIssueCode.NonPitcherAssignment => "투수 자리에 야수가 배치되었습니다",
                LineupPresetValidationIssueCode.PlayerContextMissing => "선수 상태를 확인할 수 없습니다",
                LineupPresetValidationIssueCode.OffPositionAssignment => "익숙하지 않은 포지션입니다",
                LineupPresetValidationIssueCode.PitcherRoleMismatch => "익숙하지 않은 투수 역할입니다",
                LineupPresetValidationIssueCode.TeamColorUnavailable => "사용할 수 없는 팀컬러입니다",
                LineupPresetValidationIssueCode.TacticCardUnavailable => "사용할 수 없는 전술카드입니다",
                _ => "라인업을 확인해 주세요"
            };
        }

        private static OwnerPregamePresetSnapshot FindPreset(IReadOnlyList<OwnerPregamePresetSnapshot> presets, string presetId)
        {
            for (int index = 0; index < presets.Count; index++)
                if (string.Equals(presets[index].PresetId, presetId, StringComparison.Ordinal)) return presets[index];
            throw new InvalidOperationException("선택된 프리셋이 없습니다.");
        }
    }
}
