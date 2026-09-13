using System;
using System.Collections.Generic;

namespace Baseball.Game.Guide
{
    public enum ManagerReportView { Current, History, Bookmarked, News }

    /// <summary>동일한 조치의 개별 경고를 묶되 모든 대상과 원본 이동 정보를 보존한다.</summary>
    public sealed class ManagerReportCase
    {
        private readonly List<ManagerReportData> _members = new();

        internal ManagerReportCase(string key) { Key = key; }
        public string Key { get; }
        public IReadOnlyList<ManagerReportData> Members => _members;
        public ManagerReportData Primary => _members[0];
        public bool HasUnread { get; private set; }
        public bool IsAccepted { get; private set; } = true;
        public bool IsDeferred { get; private set; } = true;
        public bool HasUpdates
        {
            get
            {
                foreach (var member in _members)
                    if (!member.isRead && (member.hasChanged || member.occurrence > 1))
                        return true;
                return false;
            }
        }

        internal void Add(ManagerReportData report, bool isDeferred)
        {
            _members.Add(report);
            HasUnread |= !report.isRead && !report.isExpired;
            IsAccepted &= report.isAccepted;
            IsDeferred &= report.isAccepted || isDeferred;
        }

        internal void SortMembers() => _members.Sort((left, right) =>
        {
            int group = left.group.CompareTo(right.group);
            if (group != 0) return group;
            int slot = left.slotIndex.CompareTo(right.slotIndex);
            return slot != 0 ? slot : string.CompareOrdinal(left.deduplicationKey, right.deduplicationKey);
        });

        internal static string CreateKey(ManagerReportData report)
        {
            if (report.news != null)
                return "news:" + report.createdSeason + ":" + report.createdWeek + ":" +
                    (report.news.kind == ManagerNewsKind.Study ? ManagerNewsKind.Training : report.news.kind);
            // 필수 문제와 선택 경고는 합치지 않는다. 역할 불일치는 투수 보직 전체가 같은 조치다.
            string preset = report.presetId ?? string.Empty;
            string evidence = report.evidence ?? string.Empty;
            return preset.Length + ":" + preset + ":" + report.kind + ":" + report.target + ":" +
                report.priority + ":" + evidence.Length + ":" + evidence;
        }
    }

    public sealed partial class GuideProgressState
    {
        /// <summary>현재 안건과 종료 기록을 분리한다. 열람으로 목록 순서가 바뀌지 않는다.</summary>
        public IReadOnlyList<ManagerReportCase> GetReportCases(ManagerReportView view)
        {
            var cases = new List<ManagerReportCase>();
            var byKey = new Dictionary<string, ManagerReportCase>(StringComparer.Ordinal);
            // 이전 버전의 동일 키 이력이 남아 있어도 가장 최근 원본만 현재 안건에 투영한다.
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int index = _reports.Count - 1; index >= 0; index--)
            {
                var report = _reports[index];
                bool latest = seen.Add(report.deduplicationKey);
                if (view == ManagerReportView.Current && (!latest || report.isExpired || report.news != null)) continue;
                if (view == ManagerReportView.News && (report.news == null || report.isExpired)) continue;
                if (view == ManagerReportView.History && !report.isExpired) continue;
                if (view == ManagerReportView.Bookmarked && !report.isBookmarked) continue;
                string key = ManagerReportCase.CreateKey(report);
                if (view == ManagerReportView.History) key += ":" + report.updatedSeason;
                if (view == ManagerReportView.Bookmarked) key += report.isExpired ? ":ended" : ":active";
                if (!byKey.TryGetValue(key, out var item))
                {
                    item = new ManagerReportCase(key); byKey.Add(key, item); cases.Add(item);
                }
                item.Add(report.Copy(), report.snoozedUntil > _currentWeek && report.snoozedSeason == _currentSeasonNumber);
            }
            foreach (var item in cases) item.SortMembers();
            cases.Sort((left, right) =>
            {
                if (view != ManagerReportView.Current)
                {
                    int season = right.Primary.createdSeason.CompareTo(left.Primary.createdSeason);
                    if (season != 0) return season;
                    int week = right.Primary.createdWeek.CompareTo(left.Primary.createdWeek);
                    if (week != 0) return week;
                }
                bool leftRequired = left.Primary.priority == ManagerReportPriority.Critical;
                bool rightRequired = right.Primary.priority == ManagerReportPriority.Critical;
                if (leftRequired != rightRequired) return rightRequired.CompareTo(leftRequired);
                int deferred = left.IsDeferred.CompareTo(right.IsDeferred);
                if (deferred != 0 && view == ManagerReportView.Current) return deferred;
                int priority = right.Primary.priority.CompareTo(left.Primary.priority);
                if (priority != 0) return priority;
                return string.CompareOrdinal(left.Key, right.Key);
            });
            return cases;
        }
    }
}
