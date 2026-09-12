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
        public bool hasCounts, isRead, isBookmarked, isExpired;

        /// <summary>기존 딥링크가 요구하는 선수·편성 슬롯 계약으로 변환한다.</summary>
        public GuideGoal ToGoal() => new GuideGoal(deduplicationKey, kind, target,
            priority == ManagerReportPriority.Critical, evidence, group, slotIndex, cardId, presetId,
            hasCounts ? actual : (int?)null, hasCounts ? expected : (int?)null);

        internal ManagerReportData Copy() => (ManagerReportData)MemberwiseClone();
    }

    public sealed partial class GuideProgressState
    {
        private readonly List<ManagerReportData> _reports = new();
        private int _reportSequence;

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
            foreach (var report in GetReports())
            {
                if (report.isRead || report.isBookmarked || report.isExpired ||
                    !_current.TryGetValue(report.deduplicationKey, out var goal)) continue;
                var status = GetStatus(goal.Key);
                if (status != GuideGoalStatus.Pending && status != GuideGoalStatus.Tracking) continue;
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

        /// <summary>보관한 리포트는 추천에서 제외하지만 기록과 경고 원본은 유지한다.</summary>
        public void SetReportBookmark(string reportId, bool bookmarked)
        {
            var report = _reports.Find(item => item.reportId == reportId);
            if (report == null) return;
            report.isBookmarked = bookmarked;
            if (bookmarked) report.isRead = true;
        }

        public string FindReportId(string goalKey) => _reports.FindLast(item =>
            item.deduplicationKey == goalKey && !item.isExpired)?.reportId;

        private void ReconcileReports(string scope, int week, IReadOnlyList<GuideGoal> goals, int seasonNumber)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var goal in goals) keys.Add(goal.Key);
            foreach (var report in _reports)
                if (!report.isExpired && (report.seasonId != SeasonId || !keys.Contains(report.deduplicationKey) ||
                    (report.scope != scope && (report.kind == GuideGoalKind.Preparation || report.kind == GuideGoalKind.PlanConfirmation))))
                { report.isExpired = true; report.isRead = true; }

            foreach (var goal in goals)
            {
                var report = _reports.FindLast(item => !item.isExpired && item.deduplicationKey == goal.Key);
                if (report == null)
                {
                    report = new ManagerReportData { reportId = (++_reportSequence).ToString(System.Globalization.CultureInfo.InvariantCulture),
                        deduplicationKey = goal.Key, scope = scope, sequence = _reportSequence,
                        createdWeek = week, createdSeason = seasonNumber };
                    _reports.Add(report);
                }
                // 같은 슬롯의 다른 선수나 인원 변화는 새 판단 근거다. 화면·경기 전환만으로 재알림하지 않는다.
                else if (report.cardId != goal.CardId || report.actual != (goal.Actual ?? 0) ||
                    report.expected != (goal.Expected ?? 0) || report.evidence != goal.Evidence)
                {
                    report.isRead = false; report.isBookmarked = false;
                    report.createdWeek = week; report.createdSeason = seasonNumber;
                }
                report.scope = scope; report.seasonId = SeasonId;
                if (goal.IsRequired && report.priority != ManagerReportPriority.Critical)
                { report.isRead = false; report.isBookmarked = false; }
                report.kind = goal.Kind; report.target = goal.Target;
                report.priority = goal.IsRequired ? ManagerReportPriority.Critical :
                    goal.Kind == GuideGoalKind.PresetIssue || goal.Kind == GuideGoalKind.Debrief
                        ? ManagerReportPriority.Important : ManagerReportPriority.Normal;
                report.category = goal.Kind == GuideGoalKind.RosterIssue ? ManagerReportCategory.Roster :
                    goal.Kind == GuideGoalKind.Debrief ? ManagerReportCategory.PowerAnalysis : ManagerReportCategory.GamePrep;
                report.evidence = goal.Evidence; report.group = goal.Group; report.slotIndex = goal.SlotIndex;
                report.cardId = goal.CardId; report.presetId = goal.PresetId;
                report.hasCounts = goal.Actual.HasValue && goal.Expected.HasValue;
                report.actual = goal.Actual ?? 0; report.expected = goal.Expected ?? 0;
                report.homeScore = HomeScore; report.awayScore = AwayScore;
            }
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
            foreach (var report in reports ?? Array.Empty<ManagerReportData>())
            {
                if (report == null || string.IsNullOrWhiteSpace(report.reportId) || !ids.Add(report.reportId) ||
                    string.IsNullOrWhiteSpace(report.deduplicationKey) || report.sequence <= 0 || report.sequence > sequence ||
                    !Enum.IsDefined(typeof(ManagerReportPriority), report.priority) ||
                    !Enum.IsDefined(typeof(ManagerReportCategory), report.category) ||
                    !Enum.IsDefined(typeof(GuideGoalKind), report.kind) || !Enum.IsDefined(typeof(GuideTargetKind), report.target))
                    throw new ArgumentException("매니저 리포트 이력이 잘못되었습니다.");
                _reports.Add(report.Copy());
            }
        }
    }
}
