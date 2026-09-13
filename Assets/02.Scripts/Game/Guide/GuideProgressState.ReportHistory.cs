using System;
using System.Collections.Generic;

namespace Baseball.Game.Guide
{
    /// <summary>경기 밸런스와 독립적인 리포트 상세 보존 정책이다.</summary>
    [Serializable]
    public sealed class ManagerReportPolicy
    {
        public int retainedSeasons = 2;
        public int maximumHistoryTargets = 256;
        public int maximumBookmarks = 100;
        public int maximumHomeNewsPerWeek = 2;
        public int newsVisibleWeeks = 2;
        public int minimumReviewGames = 3;
        public int minimumBullpenOuts = 18;
        public double minimumWalksPerNineChange = 1d;
        public int minimumDecisionAppearances = 3;
        public int minimumDecisionOuts = 9;
        public int minimumDecisionPlateAppearances = 20;
        public int maximumDecisionGames = 12;
        public int[] homeRunMilestones = { 20, 50, 100 };
        public int[] strikeoutMilestones = { 100, 200, 300 };

        internal ManagerReportPolicy Copy()
        {
            if (retainedSeasons < 1 || maximumHistoryTargets < 1 || maximumBookmarks < 1 || maximumHomeNewsPerWeek < 1 ||
                newsVisibleWeeks < 1 || minimumReviewGames < 1 || minimumBullpenOuts < 1 ||
                minimumDecisionAppearances < 1 || minimumDecisionOuts < 1 || minimumDecisionPlateAppearances < 1 ||
                maximumDecisionGames < minimumDecisionAppearances || double.IsNaN(minimumWalksPerNineChange) ||
                double.IsInfinity(minimumWalksPerNineChange) || minimumWalksPerNineChange <= 0)
                throw new ArgumentException("리포트 보존 정책은 양수여야 합니다.");
            var copy = (ManagerReportPolicy)MemberwiseClone();
            copy.homeRunMilestones = CopyMilestones(homeRunMilestones);
            copy.strikeoutMilestones = CopyMilestones(strikeoutMilestones);
            return copy;
        }

        private static int[] CopyMilestones(int[] source)
        {
            if (source == null) throw new ArgumentException("기록 기준이 없습니다.");
            int previous = 0;
            foreach (int value in source)
            { if (value <= previous) throw new ArgumentException("기록 기준은 양수 오름차순이어야 합니다."); previous = value; }
            return (int[])source.Clone();
        }
    }

    /// <summary>오래된 종료 상세만 요약한다. 공식 선수·구단 기록과는 무관하다.</summary>
    [Serializable]
    public sealed class ManagerReportSeasonSummary
    {
        public int season, closedTargets;
        internal ManagerReportSeasonSummary Copy() => (ManagerReportSeasonSummary)MemberwiseClone();
    }

    public sealed partial class GuideProgressState
    {
        private ManagerReportPolicy _reportPolicy = new();
        private readonly List<ManagerReportSeasonSummary> _reportSummaries = new();

        /// <summary>Unity 어댑터가 읽은 저작 정책의 복사본을 적용한다.</summary>
        public void ConfigureReportPolicy(ManagerReportPolicy policy) =>
            _reportPolicy = (policy ?? throw new ArgumentNullException(nameof(policy))).Copy();

        /// <summary>진행 복사본에도 동일한 저작 정책을 주입한다.</summary>
        public ManagerReportPolicy GetReportPolicy() => _reportPolicy.Copy();

        /// <summary>보관 한도를 넘기 전에 UI에서 구체적인 해결 방법을 안내할 수 있게 한다.</summary>
        public bool CanBookmarkReport(string reportId)
        {
            int count = 0;
            bool found = false;
            foreach (var report in _reports)
            {
                if (report.reportId == reportId)
                {
                    if (report.isBookmarked) return true;
                    found = true;
                }
                if (report.isBookmarked) count++;
            }
            return found && count < _reportPolicy.maximumBookmarks;
        }

        /// <summary>시즌별 종료 대상 수를 새 복사본으로 반환한다.</summary>
        public ManagerReportSeasonSummary[] GetReportSummaries()
        {
            var result = new ManagerReportSeasonSummary[_reportSummaries.Count];
            for (int i = 0; i < result.Length; i++) result[i] = _reportSummaries[i].Copy();
            return result;
        }

        private void TrimReportHistory()
        {
            var closed = new List<ManagerReportData>();
            foreach (var report in _reports)
                if (report.isExpired && !report.isBookmarked) closed.Add(report);
            closed.Sort((left, right) =>
            {
                int season = left.updatedSeason.CompareTo(right.updatedSeason);
                if (season != 0) return season;
                int week = left.updatedWeek.CompareTo(right.updatedWeek);
                return week != 0 ? week : left.sequence.CompareTo(right.sequence);
            });
            for (int i = 0; i < closed.Count; i++)
            {
                var report = closed[i];
                if (closed.Count - i <= _reportPolicy.maximumHistoryTargets &&
                    report.updatedSeason >= Math.Max(_currentSeasonNumber, _news.budgetSeason) - _reportPolicy.retainedSeasons + 1) break;
                var summary = _reportSummaries.Find(item => item.season == report.updatedSeason);
                if (summary == null)
                {
                    summary = new ManagerReportSeasonSummary { season = report.updatedSeason };
                    _reportSummaries.Add(summary);
                }
                summary.closedTargets++;
                _reports.Remove(report);
            }
            _reportSummaries.Sort((left, right) => right.season.CompareTo(left.season));
        }

        private void RestoreReportSummaries(ManagerReportSeasonSummary[] summaries)
        {
            var seasons = new HashSet<int>();
            foreach (var summary in summaries ?? Array.Empty<ManagerReportSeasonSummary>())
            {
                if (summary == null || summary.season < 0 || summary.closedTargets < 1 || !seasons.Add(summary.season))
                    throw new ArgumentException("리포트 시즌 요약이 잘못되었습니다.");
                _reportSummaries.Add(summary.Copy());
            }
            _reportSummaries.Sort((left, right) => right.season.CompareTo(left.season));
        }
    }
}
