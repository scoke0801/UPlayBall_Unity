using System;
using System.Collections.Generic;
using Baseball.Core.Historical;

namespace Baseball.Game.Guide
{
    public enum ManagerReportCategory { GamePrep, PowerAnalysis, Roster, Growth, Finance, Schedule }
    public enum ManagerReportPriority { Normal, Important, Critical }

    /// <summary>문구 대신 근거와 의미적 목적지를 저장하는 시즌 간 매니저 리포트다.</summary>
    [Serializable]
    public sealed class ManagerReportData
    {
        public string reportId, deduplicationKey, scope, seasonId, evidence, cardId, presetId;
        public GuideGoalKind kind;
        public GuideTargetKind target;
        public LineupPresetAssignmentGroup group;
        public ManagerReportCategory category;
        public ManagerReportPriority priority;
        public int slotIndex, createdWeek, createdSeason, sequence, homeScore, awayScore, actual, expected;
        public bool hasCounts, isRead, isBookmarked, isExpired, isAccepted, hasChanged;
        public string context;
        public int conditionPenalty, occurrence = 1, updatedWeek, updatedSeason, snoozedUntil, snoozedSeason;
        public ManagerNewsEvidence news;
        public bool isHomeEligible;

        /// <summary>기존 딥링크가 요구하는 선수·편성 슬롯 계약으로 변환한다.</summary>
        public GuideGoal ToGoal() => new GuideGoal(deduplicationKey, kind, target,
            priority == ManagerReportPriority.Critical, evidence, group, slotIndex, cardId, presetId,
            hasCounts ? actual : (int?)null, hasCounts ? expected : (int?)null, context, conditionPenalty);

        internal ManagerReportData Copy()
        {
            var copy = (ManagerReportData)MemberwiseClone(); copy.news = news?.Copy(); return copy;
        }
    }

    public sealed partial class GuideProgressState
    {
        private readonly List<ManagerReportData> _reports = new();
        private int _reportSequence;
        private int _currentSeasonNumber;
        private int _currentWeek;

        /// <summary>미확인 중요·일반, 보관, 확인 완료 순으로 분리된 복사본을 반환한다.</summary>
        public IReadOnlyList<ManagerReportData> GetReports()
        {
            var result = new List<ManagerReportData>(_reports.Count);
            foreach (var report in _reports) result.Add(report.Copy());
            result.Sort((a, b) =>
            {
                int rank = ReportOrder(a).CompareTo(ReportOrder(b));
                if (rank != 0) return rank;
                if (ReportOrder(a) == 0)
                {
                    int priority = b.priority.CompareTo(a.priority);
                    if (priority != 0) return priority;
                }
                return b.sequence.CompareTo(a.sequence);
            });
            return result;
        }

        /// <summary>대표 추천만 제한하며 중요 경고 원본과 전체 리포트는 숨기지 않는다.</summary>
        public IReadOnlyList<GuideGoal> GetSuggestionGoals()
        {
            var result = new List<GuideGoal>();
            int critical = 0, important = 0;
            var caseKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var report in GetReports())
            {
                if (report.isRead || report.isAccepted || report.isExpired ||
                    !_current.TryGetValue(report.deduplicationKey, out var goal)) continue;
                var status = GetStatus(goal.Key);
                if (status != GuideGoalStatus.Pending && status != GuideGoalStatus.Tracking) continue;
                if (!caseKeys.Add(ManagerReportCase.CreateKey(report))) continue;
                if (report.priority == ManagerReportPriority.Critical && critical++ >= 1) continue;
                if (report.priority == ManagerReportPriority.Important && important++ >= 3) continue;
                result.Add(goal);
            }
            return result;
        }

        /// <summary>열람은 실제 문제 해결 상태를 변경하지 않는다.</summary>
        public void MarkReportRead(string reportId)
        {
            var report = _reports.Find(item => item.reportId == reportId);
            if (report != null) report.isRead = true;
        }

        /// <summary>문제 해결·보관 상태를 유지하면서 모든 리포트의 새 소식 표시를 해제한다.</summary>
        public void MarkAllReportsRead()
        {
            foreach (var report in _reports) report.isRead = true;
        }

        /// <summary>보관은 열람·판단과 독립적이며 근거가 바뀌어도 사용자의 보관 선택을 유지한다.</summary>
        public void SetReportBookmark(string reportId, bool bookmarked)
        {
            var report = _reports.Find(item => item.reportId == reportId);
            if (report == null) return;
            if (bookmarked && !CanBookmarkReport(reportId)) throw new InvalidOperationException("기존 리포트의 보관을 해제한 뒤 다시 보관해 주세요.");
            report.isBookmarked = bookmarked;
        }

        public string FindReportId(string goalKey) => _reports.FindLast(item =>
            item.deduplicationKey == goalKey && !item.isExpired)?.reportId;

        private void ReconcileReports(string scope, int week, IReadOnlyList<GuideGoal> goals, int seasonNumber)
        {
            _currentSeasonNumber = seasonNumber; _currentWeek = week;
            AdvanceNewsClock(seasonNumber, week);
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var goal in goals) keys.Add(goal.Key);
            var latest = new Dictionary<string, ManagerReportData>(StringComparer.Ordinal);
            var existingCases = new HashSet<string>(StringComparer.Ordinal);
            foreach (var report in _reports) latest[report.deduplicationKey] = report;
            foreach (var report in latest.Values)
                if (!report.isExpired) existingCases.Add(ManagerReportCase.CreateKey(report));
            foreach (var report in _reports)
            {
                if (report.news != null || report.isExpired || (keys.Contains(report.deduplicationKey) &&
                    ReferenceEquals(latest[report.deduplicationKey], report))) continue;
                report.isExpired = true; report.isRead = true;
                report.updatedWeek = week; report.updatedSeason = seasonNumber;
                report.isAccepted = false; report.snoozedUntil = 0;
            }

            foreach (var goal in goals)
            {
                var report = _reports.FindLast(item => item.deduplicationKey == goal.Key);
                bool isNew = report == null;
                // 직렬화기의 누락 필드 기본값 0도 최초 발생으로 해석한다.
                if (report != null && report.occurrence == 0) report.occurrence = 1;
                if (report == null)
                {
                    report = new ManagerReportData { reportId = (++_reportSequence).ToString(System.Globalization.CultureInfo.InvariantCulture),
                        deduplicationKey = goal.Key, scope = scope, sequence = _reportSequence,
                        createdWeek = week, createdSeason = seasonNumber,
                        updatedWeek = week, updatedSeason = seasonNumber };
                    _reports.Add(report);
                }
                else if (report.isExpired || HasChangedEvidence(report, goal))
                {
                    if (report.isExpired) report.occurrence++;
                    report.hasChanged = true;
                    report.isRead = false; report.isAccepted = false; report.snoozedUntil = 0;
                    report.updatedWeek = week; report.updatedSeason = seasonNumber;
                }
                report.isExpired = false;
                if (report.seasonId != SeasonId) report.snoozedUntil = 0;
                report.scope = scope; report.seasonId = SeasonId;
                report.kind = goal.Kind; report.target = goal.Target;
                report.priority = goal.IsRequired ? ManagerReportPriority.Critical :
                    goal.Kind == GuideGoalKind.PresetIssue || goal.Kind == GuideGoalKind.Debrief
                        ? ManagerReportPriority.Important : ManagerReportPriority.Normal;
                report.category = goal.Kind == GuideGoalKind.RosterIssue ? ManagerReportCategory.Roster :
                    goal.Kind == GuideGoalKind.Debrief ? ManagerReportCategory.PowerAnalysis : ManagerReportCategory.GamePrep;
                report.evidence = goal.Evidence; report.group = goal.Group; report.slotIndex = goal.SlotIndex;
                report.cardId = goal.CardId; report.presetId = goal.PresetId;
                report.context = goal.Context; report.conditionPenalty = goal.ConditionPenalty;
                report.hasCounts = goal.Actual.HasValue && goal.Expected.HasValue;
                report.actual = goal.Actual ?? 0; report.expected = goal.Expected ?? 0;
                report.homeScore = HomeScore; report.awayScore = AwayScore;
                if (isNew) report.hasChanged = existingCases.Contains(ManagerReportCase.CreateKey(report));
            }
            TrimReportHistory();
        }

        private static bool HasChangedEvidence(ManagerReportData report, GuideGoal goal) =>
            report.cardId != goal.CardId || report.evidence != goal.Evidence ||
            (report.context ?? string.Empty) != goal.Context ||
            report.hasCounts != (goal.Actual.HasValue && goal.Expected.HasValue) ||
            report.expected != (goal.Expected ?? 0) ||
            (goal.Actual.HasValue && goal.Expected.HasValue &&
                Math.Abs((long)goal.Actual.Value - goal.Expected.Value) > Math.Abs((long)report.actual - report.expected)) ||
            // 실제 불이익의 감소는 재촉 사유가 아니다. 역할 변경과 악화만 다시 판단한다.
            goal.ConditionPenalty > report.conditionPenalty ||
            (goal.IsRequired && report.priority != ManagerReportPriority.Critical);

        /// <summary>현재 주차의 다음 주에 보류한 대상을 다시 검토할 수 있게 한다.</summary>
        public bool SnoozeReport(string reportId)
        {
            var report = _reports.Find(item => item.reportId == reportId && !item.isExpired);
            return report != null && Snooze(report.deduplicationKey, _currentWeek + 1);
        }

        private static int ReportOrder(ManagerReportData report) => !report.isRead && !report.isExpired && !report.isBookmarked
            ? report.priority != ManagerReportPriority.Normal ? 0 : 1 : report.isBookmarked ? 2 : 3;

        private ManagerReportData[] CaptureReports()
        {
            var result = new ManagerReportData[_reports.Count];
            for (int i = 0; i < result.Length; i++) result[i] = _reports[i].Copy();
            return result;
        }

        private void RestoreReports(ManagerReportData[] reports, int sequence)
        {
            _reportSequence = sequence;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var sequences = new HashSet<int>();
            foreach (var report in reports ?? Array.Empty<ManagerReportData>())
            {
                if (report == null || string.IsNullOrWhiteSpace(report.reportId) || !ids.Add(report.reportId) ||
                    string.IsNullOrWhiteSpace(report.deduplicationKey) || report.sequence <= 0 || report.sequence > sequence ||
                    !sequences.Add(report.sequence) || report.occurrence < 0 || report.conditionPenalty < 0 ||
                    report.createdWeek < 0 || report.createdSeason < 0 || report.updatedWeek < 0 || report.updatedSeason < 0 ||
                    (report.isAccepted && report.priority == ManagerReportPriority.Critical) ||
                    !Enum.IsDefined(typeof(ManagerReportPriority), report.priority) ||
                    !Enum.IsDefined(typeof(ManagerReportCategory), report.category) ||
                    !Enum.IsDefined(typeof(GuideGoalKind), report.kind) || !Enum.IsDefined(typeof(GuideTargetKind), report.target))
                    throw new ArgumentException("매니저 리포트 이력이 잘못되었습니다.");
                var news = report.news;
                // JsonUtility의 인라인 클래스 직렬화는 null 대신 None 객체를 만들 수 있다.
                // 일반 리포트의 빈 근거만 정규화하고 실제 소식의 누락·종류 불일치는 거부한다.
                if (report.kind != GuideGoalKind.News && news?.kind == ManagerNewsKind.None)
                    news = null;
                if ((report.kind == GuideGoalKind.News) != (news != null) || news?.kind == ManagerNewsKind.None)
                    throw new ArgumentException("구단 소식 종류가 잘못되었습니다.");
                news?.Validate();
                var restored = report.Copy();
                restored.news = news?.Copy();
                _reports.Add(restored);
            }
            _reports.Sort((left, right) => left.sequence.CompareTo(right.sequence));
        }
    }
}
