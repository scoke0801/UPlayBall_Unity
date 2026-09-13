using System;
using System.Collections.Generic;
using Baseball.Core.Historical;

namespace Baseball.Game.Guide
{
    /// <summary>안내 열람과 원본 문제 해결을 구분하는 저장 상태다.</summary>
    public enum GuideGoalStatus { Pending, Tracking, Snoozed, Resolved, AcceptedAsIs, Expired }
    public enum GuideGoalKind { RosterIssue, PresetIssue, Preparation, Debrief, PlanConfirmation, News }
    public enum GuideTargetKind { Roster, PresetSlot, Analysis, Condition, TeamColor, Tactic, PlanConfirmation, PlayerCard }
    public enum GuideArrivalStatus { Requested, RouteReady, TargetReady, Cancelled, Unsupported, TargetMissing }

    /// <summary>표현 객체 없이 실제 원본 문제와 의미적 조작 대상을 연결한다.</summary>
    public sealed class GuideGoal
    {
        public GuideGoal(string key, GuideGoalKind kind, GuideTargetKind target, bool isRequired,
            string evidence, LineupPresetAssignmentGroup group = default, int slotIndex = -1,
            string cardId = "", string presetId = "", int? actual = null, int? expected = null,
            string context = "", int conditionPenalty = 0)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("목표 식별자가 필요합니다.", nameof(key));
            Key = key; Kind = kind; Target = target; IsRequired = isRequired;
            Evidence = evidence ?? string.Empty; Group = group; SlotIndex = slotIndex;
            CardId = cardId ?? string.Empty; PresetId = presetId ?? string.Empty;
            Actual = actual; Expected = expected;
            Context = context ?? string.Empty; ConditionPenalty = conditionPenalty;
        }
        public string Key { get; }
        public GuideGoalKind Kind { get; }
        public GuideTargetKind Target { get; }
        public bool IsRequired { get; }
        public string Evidence { get; }
        public LineupPresetAssignmentGroup Group { get; }
        public int SlotIndex { get; }
        public string CardId { get; }
        public string PresetId { get; }
        public int? Actual { get; }
        public int? Expected { get; }
        public string Context { get; }
        public int ConditionPenalty { get; }
    }

    [Serializable]
    public sealed class GuideProgressData
    {
        public string scope = "";
        public string seasonId = "";
        public string trackedKey = "";
        public GuideGoalEntryData[] entries = Array.Empty<GuideGoalEntryData>();
        public string publishedMatchKey = "";
        public string reviewedMatchKey = "";
        public int homeScore;
        public int awayScore;
        public ManagerReportData[] reports = Array.Empty<ManagerReportData>();
        public int reportSequence;
        public ManagerReportSeasonSummary[] reportSummaries = Array.Empty<ManagerReportSeasonSummary>();
        public ManagerNewsProgressData newsProgress;
    }

    [Serializable]
    public sealed class GuideGoalEntryData
    {
        public string key;
        public GuideGoalStatus status;
        public int occurrence;
        public int snoozedUntil;
        public bool wasPresent;
    }

    /// <summary>게임 범위별 문제 회차와 보류를 관리한다. 큐·문구 선택·경기 RNG는 소유하지 않는다.</summary>
    public sealed partial class GuideProgressState
    {
        private readonly Dictionary<string, GuideGoalEntryData> _entries = new(StringComparer.Ordinal);
        private readonly Dictionary<string, GuideGoal> _current = new(StringComparer.Ordinal);
        public string Scope { get; private set; } = "";
        public string SeasonId { get; private set; } = "";
        public string TrackedKey { get; private set; } = "";
        public string PublishedMatchKey { get; private set; } = "";
        public string ReviewedMatchKey { get; private set; } = "";
        public string PendingMatchKey => PublishedMatchKey == ReviewedMatchKey ? "" : PublishedMatchKey;
        public int HomeScore { get; private set; }
        public int AwayScore { get; private set; }

        /// <summary>화면 초안이 아닌 적용된 원본의 완전한 후보 집합으로만 문제를 재평가한다.</summary>
        public void Reconcile(string scope, int progress, IReadOnlyList<GuideGoal> goals, string seasonId = "", int seasonNumber = 0)
        {
            if (string.IsNullOrWhiteSpace(scope)) throw new ArgumentException("진행 범위가 필요합니다.", nameof(scope));
            if (goals == null) throw new ArgumentNullException(nameof(goals));
            var inputKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var goal in goals)
                if (goal == null || !inputKeys.Add(goal.Key)) throw new ArgumentException("중복되거나 비어 있는 안내 목표입니다.", nameof(goals));
            if (!string.Equals(Scope, scope, StringComparison.Ordinal))
            {
                // 경기 범위가 끝난 상세 이력은 보존하지 않아 장기 커리어 저장이 무한히 커지지 않는다.
                var retained = new List<GuideGoalEntryData>();
                if (!string.IsNullOrEmpty(seasonId) && SeasonId == seasonId)
                    foreach (GuideGoal goal in goals)
                        if (_entries.TryGetValue(goal.Key, out var prior) &&
                            prior.status == GuideGoalStatus.Snoozed && prior.snoozedUntil > progress)
                            retained.Add(prior);
                _entries.Clear();
                foreach (var entry in retained) _entries.Add(entry.key, entry);
                TrackedKey = ""; Scope = scope;
            }
            SeasonId = seasonId ?? "";
            ReconcileReports(scope, progress, goals, seasonNumber);
            _current.Clear();
            foreach (GuideGoal goal in goals)
            {
                if (!_current.TryAdd(goal.Key, goal)) throw new ArgumentException("중복 안내 목표입니다.");
                if (!_entries.TryGetValue(goal.Key, out GuideGoalEntryData entry))
                {
                    entry = new GuideGoalEntryData { key = goal.Key, occurrence = 1 };
                    _entries.Add(goal.Key, entry);
                }
                else if (!entry.wasPresent)
                {
                    entry.occurrence++; entry.status = GuideGoalStatus.Pending;
                }
                if (entry.status == GuideGoalStatus.Snoozed && progress >= entry.snoozedUntil)
                    entry.status = GuideGoalStatus.Pending;
                if (goal.IsRequired && entry.status == GuideGoalStatus.AcceptedAsIs)
                    entry.status = GuideGoalStatus.Pending;
                var report = _reports.FindLast(item => !item.isExpired && item.deduplicationKey == goal.Key);
                // 경기 Scope의 임시 목표가 초기화되어도 저장된 대상별 판단은 유지한다.
                if (report != null && (goal.Kind == GuideGoalKind.PresetIssue || goal.Kind == GuideGoalKind.RosterIssue))
                {
                    entry.occurrence = report.occurrence;
                    entry.status = report.isAccepted ? GuideGoalStatus.AcceptedAsIs :
                        report.snoozedUntil > progress && report.snoozedSeason == seasonNumber
                            ? GuideGoalStatus.Snoozed : GuideGoalStatus.Pending;
                    if (TrackedKey == goal.Key && entry.status == GuideGoalStatus.Pending)
                        entry.status = GuideGoalStatus.Tracking;
                    entry.snoozedUntil = report.snoozedUntil;
                }
                entry.wasPresent = true;
            }
            foreach (GuideGoalEntryData entry in _entries.Values)
            {
                if (_current.ContainsKey(entry.key)) continue;
                if (entry.wasPresent) entry.status = GuideGoalStatus.Resolved;
                entry.wasPresent = false;
                if (TrackedKey == entry.key) TrackedKey = "";
            }
        }

        /// <summary>필수 경고·현재 추적·안정 ID 순서로 현재 유효한 목표를 반환한다.</summary>
        public IReadOnlyList<GuideGoal> GetVisibleGoals(bool includeSnoozed = false)
        {
            var result = new List<GuideGoal>();
            foreach (GuideGoal goal in _current.Values)
            {
                GuideGoalStatus status = _entries[goal.Key].status;
                if (status == GuideGoalStatus.Pending || status == GuideGoalStatus.Tracking ||
                    (includeSnoozed && status == GuideGoalStatus.Snoozed)) result.Add(goal);
            }
            result.Sort((left, right) =>
            {
                int required = right.IsRequired.CompareTo(left.IsRequired);
                if (required != 0) return required;
                int tracked = (right.Key == TrackedKey).CompareTo(left.Key == TrackedKey);
                if (tracked != 0) return tracked;
                int kind = Order(left.Kind).CompareTo(Order(right.Kind));
                return kind != 0 ? kind : string.CompareOrdinal(left.Key, right.Key);
            });
            return result;
        }

        public GuideGoalStatus GetStatus(string key) => _entries.TryGetValue(key, out var entry)
            ? entry.status : GuideGoalStatus.Expired;
        public int GetOccurrence(string key) => _entries.TryGetValue(key, out var entry) ? entry.occurrence : 0;

        /// <summary>추적 목표를 바꾸어도 이전 목표의 실제 해결 여부는 바꾸지 않는다.</summary>
        public bool Track(string key)
        {
            if (!_current.ContainsKey(key)) return false;
            if (_entries.TryGetValue(TrackedKey, out var previous) && previous.status == GuideGoalStatus.Tracking)
                previous.status = GuideGoalStatus.Pending;
            TrackedKey = key; _entries[key].status = GuideGoalStatus.Tracking; return true;
        }

        public bool Snooze(string key, int untilProgress)
        {
            if (!_current.ContainsKey(key)) return false;
            _entries[key].status = GuideGoalStatus.Snoozed;
            _entries[key].snoozedUntil = untilProgress;
            var report = _reports.FindLast(item => !item.isExpired && item.deduplicationKey == key);
            if (report != null)
            {
                report.snoozedUntil = untilProgress; report.snoozedSeason = _currentSeasonNumber;
                report.isRead = true;
            }
            if (TrackedKey == key) TrackedKey = "";
            return true;
        }

        /// <summary>선택 사항 유지와 실제 수정 성공을 별도 상태로 저장한다.</summary>
        public bool AcceptAsIs(string key)
        {
            if (!_current.TryGetValue(key, out var goal) || goal.IsRequired || goal.Kind != GuideGoalKind.PresetIssue)
                return false;
            if (_entries[key].status == GuideGoalStatus.AcceptedAsIs) return true;
            _entries[key].status = GuideGoalStatus.AcceptedAsIs;
            StartDecisionObservation(goal);
            var report = _reports.FindLast(item => !item.isExpired && item.deduplicationKey == key);
            if (report != null) { report.isAccepted = true; report.isRead = true; report.snoozedUntil = 0; }
            if (TrackedKey == key) TrackedKey = "";
            return true;
        }

        /// <summary>유지했던 선택 경고를 다시 검토한다. 경기 규칙과 배치는 바꾸지 않는다.</summary>
        public bool Reconsider(string key)
        {
            if (!_current.ContainsKey(key)) return false;
            var report = _reports.FindLast(item => !item.isExpired && item.deduplicationKey == key);
            if (report == null) return false;
            report.isAccepted = false; report.snoozedUntil = 0;
            _decisions.RemoveAll(item => item.key == key);
            _entries[key].status = GuideGoalStatus.Pending;
            return true;
        }

        /// <summary>유지·보류 중인 안건도 현재 적용된 대상인지 확인한다.</summary>
        public bool IsCurrentTarget(GuideGoal goal) => goal != null &&
            _current.TryGetValue(goal.Key, out var current) && current.CardId == goal.CardId &&
            current.PresetId == goal.PresetId;

        /// <summary>열람형 목표만 실제 대상 준비가 확인된 뒤 완료한다.</summary>
        public bool RecordArrival(string key, GuideArrivalStatus arrival)
        {
            if (arrival != GuideArrivalStatus.TargetReady || !_current.TryGetValue(key, out var goal) ||
                (goal.Kind != GuideGoalKind.Preparation && goal.Kind != GuideGoalKind.Debrief)) return false;
            _entries[key].status = GuideGoalStatus.Resolved;
            if (goal.Kind == GuideGoalKind.Debrief) ReviewedMatchKey = PublishedMatchKey;
            if (TrackedKey == key) TrackedKey = "";
            return true;
        }

        /// <summary>관전 결과 공개 후의 확정 점수만 보관한다. 미공개 경기 결과로 호출하지 않는다.</summary>
        public void PublishMatch(string matchKey, int homeScore, int awayScore)
        {
            if (string.IsNullOrWhiteSpace(matchKey) || homeScore < 0 || awayScore < 0) throw new ArgumentException("공개 경기 기록이 잘못되었습니다.");
            PublishedMatchKey = matchKey; HomeScore = homeScore; AwayScore = awayScore;
        }

        /// <summary>실제 경기 실행이 확정된 경우에만 경기 준비 목표를 완료한다.</summary>
        public void RecordPlanConfirmed()
        {
            if (_entries.TryGetValue("plan-confirmation", out var entry)) entry.status = GuideGoalStatus.Resolved;
            if (TrackedKey == "plan-confirmation") TrackedKey = "";
        }

        public GuideProgressData Capture()
        {
            var keys = new List<string>(_entries.Keys); keys.Sort(StringComparer.Ordinal);
            var data = new GuideProgressData { scope = Scope, seasonId = SeasonId, trackedKey = TrackedKey,
                publishedMatchKey = PublishedMatchKey, homeScore = HomeScore, awayScore = AwayScore,
                reviewedMatchKey = ReviewedMatchKey,
                entries = new GuideGoalEntryData[keys.Count], reports = CaptureReports(), reportSequence = _reportSequence,
                reportSummaries = GetReportSummaries(), newsProgress = CaptureNews() };
            for (int index = 0; index < keys.Count; index++) data.entries[index] = Copy(_entries[keys[index]]);
            return data;
        }

        public static GuideProgressState Restore(GuideProgressData data)
        {
            var state = new GuideProgressState();
            if (data == null) return state;
            state.Scope = data.scope ?? ""; state.TrackedKey = data.trackedKey ?? "";
            state.SeasonId = data.seasonId ?? "";
            state.PublishedMatchKey = data.publishedMatchKey ?? "";
            state.ReviewedMatchKey = data.reviewedMatchKey ?? "";
            if (data.homeScore < 0 || data.awayScore < 0) throw new ArgumentException("안내 경기 점수가 잘못되었습니다.");
            state.HomeScore = data.homeScore; state.AwayScore = data.awayScore;
            state.RestoreReports(data.reports, data.reportSequence);
            state.RestoreReportSummaries(data.reportSummaries);
            state.RestoreNews(data.newsProgress);
            foreach (var entry in data.entries ?? Array.Empty<GuideGoalEntryData>())
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.key) || entry.occurrence < 1 ||
                    !Enum.IsDefined(typeof(GuideGoalStatus), entry.status) || !state._entries.TryAdd(entry.key, Copy(entry)))
                    throw new ArgumentException("안내 목표 이력이 잘못되었습니다.");
            }
            if (state.TrackedKey.Length > 0 && !state._entries.ContainsKey(state.TrackedKey))
                throw new ArgumentException("추적 목표 이력이 없습니다.");
            return state;
        }

        private static GuideGoalEntryData Copy(GuideGoalEntryData entry) => new()
        { key = entry.key, status = entry.status, occurrence = entry.occurrence,
            snoozedUntil = entry.snoozedUntil, wasPresent = entry.wasPresent };

        private static int Order(GuideGoalKind kind) => kind switch
        { GuideGoalKind.Debrief => 0, GuideGoalKind.RosterIssue => 1, GuideGoalKind.PresetIssue => 2,
            GuideGoalKind.Preparation => 3, _ => 4 };
    }
}
