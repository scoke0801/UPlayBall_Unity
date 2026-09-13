using System;
using System.Collections.Generic;
using Baseball.Core.Growth;

namespace Baseball.Game.Guide
{
    public sealed partial class GuideProgressState
    {
        private ManagerNewsProgressData _news = new();
        private readonly Dictionary<string, int> _newsCursors = new(StringComparer.Ordinal);
        private readonly HashSet<string> _announcedCases = new(StringComparer.Ordinal);
        private readonly List<ManagerDecisionObservation> _decisions = new();

        /// <summary>동일 명령의 재수신은 무시하고 같은 선수의 주간 성장 결과는 합산한다.</summary>
        public bool PublishGrowth(string stream, int sequence, string cardId, int season, int week,
            ManagerNewsKind kind, string label, IReadOnlyList<int> growth)
        {
            if (string.IsNullOrWhiteSpace(stream) || string.IsNullOrWhiteSpace(cardId) || sequence <= 0 || season <= 0 || week < 0 ||
                (kind != ManagerNewsKind.Training && kind != ManagerNewsKind.Study) || growth == null || growth.Count != PlayerAbilityCatalog.AbilityCount)
                throw new ArgumentException("성장 소식의 확정 근거가 잘못되었습니다.");
            foreach (int value in growth) if (value < 0) throw new ArgumentException("성장 결과는 음수일 수 없습니다.");
            if (_newsCursors.TryGetValue(stream, out int previous) && sequence <= previous) return false;
            string key = "growth:" + kind + ":" + season + ":" + week + ":" + cardId;
            var report = _reports.Find(item => item.deduplicationKey == key);
            if (report != null)
            {
                for (int i = 0; i < growth.Count; i++) _ = checked(report.news.growth[i] + growth[i]);
                for (int i = 0; i < growth.Count; i++) report.news.growth[i] += growth[i];
                if (report.news.label != label) report.news.label = string.Empty;
            }
            else
            {
                var evidence = new ManagerNewsEvidence { kind = kind, label = label ?? string.Empty };
                for (int i = 0; i < growth.Count; i++) evidence.growth[i] = growth[i];
                PublishNews(key, cardId, season, week, evidence);
            }
            _newsCursors[stream] = sequence;
            return true;
        }

        private ManagerReportData PublishNews(string key, string cardId, int season, int week, ManagerNewsEvidence evidence)
        {
            var existing = _reports.Find(item => item.deduplicationKey == key);
            if (existing != null) return existing;
            AdvanceNewsClock(season, week);
            var report = new ManagerReportData
            {
                reportId = (++_reportSequence).ToString(System.Globalization.CultureInfo.InvariantCulture),
                deduplicationKey = key, sequence = _reportSequence, kind = GuideGoalKind.News,
                target = string.IsNullOrEmpty(cardId) ? GuideTargetKind.Analysis : GuideTargetKind.PlayerCard,
                cardId = cardId ?? string.Empty, presetId = string.Empty, evidence = evidence.kind.ToString(),
                category = evidence.kind == ManagerNewsKind.Training || evidence.kind == ManagerNewsKind.Study
                    ? ManagerReportCategory.Growth : ManagerReportCategory.PowerAnalysis,
                priority = ManagerReportPriority.Normal, slotIndex = -1,
                createdSeason = season, updatedSeason = season, createdWeek = week, updatedWeek = week,
                news = evidence.Copy()
            };
            string caseKey = ManagerReportCase.CreateKey(report);
            bool hasCase = _reports.Exists(item => item.news != null && ManagerReportCase.CreateKey(item) == caseKey);
            report.isHomeEligible = !hasCase && ReserveHomeNotice(caseKey, season, week);
            // 이미 알린 주간 묶음의 추가 내용은 조용히 합친다. 다음 주 알림 큐로 이월하지 않는다.
            report.isRead = hasCase || !report.isHomeEligible;
            _reports.Add(report);
            ExpireNews();
            return report;
        }

        private void AdvanceNewsClock(int season, int week)
        {
            if (season < _news.budgetSeason || (season == _news.budgetSeason && week <= _news.budgetWeek)) return;
            _news.budgetSeason = season; _news.budgetWeek = week; _announcedCases.Clear();
            ExpireNews();
        }

        private bool ReserveHomeNotice(string caseKey, int season, int week)
        {
            AdvanceNewsClock(season, week);
            if (season != _news.budgetSeason || week != _news.budgetWeek || _announcedCases.Contains(caseKey) ||
                _announcedCases.Count >= _reportPolicy.maximumHomeNewsPerWeek) return false;
            _announcedCases.Add(caseKey);
            return true;
        }

        private void ExpireNews()
        {
            foreach (var report in _reports)
            {
                if (report.news == null || report.isExpired) continue;
                if (report.createdSeason == _news.budgetSeason &&
                    report.createdWeek >= _news.budgetWeek - _reportPolicy.newsVisibleWeeks + 1) continue;
                report.isExpired = true; report.isRead = true;
            }
            TrimReportHistory();
        }

        /// <summary>구단 소식 묶음을 열람해도 원본 운영 안건의 판단은 바꾸지 않는다.</summary>
        public void MarkNewsCaseRead(string reportId)
        {
            var selected = _reports.Find(item => item.reportId == reportId);
            if (selected?.news == null) { MarkReportRead(reportId); return; }
            string key = ManagerReportCase.CreateKey(selected);
            foreach (var report in _reports)
                if (report.news != null && ManagerReportCase.CreateKey(report) == key) report.isRead = true;
        }

        /// <summary>발행 한도를 통과한 최신 소식 한 묶음만 홈 대표로 선택한다.</summary>
        public ManagerReportCase GetHomeNews()
        {
            foreach (var item in GetReportCases(ManagerReportView.News))
                if (item.HasUnread) return item;
            return null;
        }

        private ManagerNewsProgressData CaptureNews()
        {
            var keys = new List<string>(_newsCursors.Keys); keys.Sort(StringComparer.Ordinal);
            var cases = new List<string>(_announcedCases); cases.Sort(StringComparer.Ordinal);
            var data = new ManagerNewsProgressData
            {
                budgetSeason = _news.budgetSeason, budgetWeek = _news.budgetWeek,
                matchSeason = _news.matchSeason, lastMatchOrdinal = _news.lastMatchOrdinal,
                weeklyIndex = _news.weeklyIndex, weekly = _news.weekly.Copy(), previousWeek = _news.previousWeek.Copy(),
                decisionSequence = _news.decisionSequence, announcedCases = cases.ToArray(),
                cursors = new ManagerNewsCursor[keys.Count], decisions = new ManagerDecisionObservation[_decisions.Count]
            };
            for (int i = 0; i < keys.Count; i++) data.cursors[i] = new ManagerNewsCursor { stream = keys[i], sequence = _newsCursors[keys[i]] };
            for (int i = 0; i < _decisions.Count; i++) data.decisions[i] = _decisions[i].Copy();
            return data;
        }

        private void RestoreNews(ManagerNewsProgressData data)
        {
            if (data == null) return;
            if (data.budgetSeason < 0 || data.budgetWeek < 0 || data.matchSeason < 0 || data.lastMatchOrdinal < 0 ||
                data.weeklyIndex < 0 || data.decisionSequence < 0 || data.weekly == null || data.previousWeek == null)
                throw new ArgumentException("구단 소식 저장 상태가 잘못되었습니다.");
            data.weekly.Validate(); data.previousWeek.Validate();
            foreach (var cursor in data.cursors ?? Array.Empty<ManagerNewsCursor>())
                if (cursor == null || string.IsNullOrWhiteSpace(cursor.stream) || cursor.sequence <= 0 || !_newsCursors.TryAdd(cursor.stream, cursor.sequence))
                    throw new ArgumentException("구단 소식 수신 이력이 잘못되었습니다.");
            foreach (string key in data.announcedCases ?? Array.Empty<string>())
                if (string.IsNullOrWhiteSpace(key) || !_announcedCases.Add(key)) throw new ArgumentException("구단 소식 발행 이력이 잘못되었습니다.");
            var decisions = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in data.decisions ?? Array.Empty<ManagerDecisionObservation>())
            {
                if (item == null || string.IsNullOrWhiteSpace(item.cardId) || string.IsNullOrWhiteSpace(item.key) ||
                    item.sequence <= 0 || item.sequence > data.decisionSequence || item.season < 0 || item.startGame < 0 ||
                    item.games < 0 || item.appearances < 0 || item.plateAppearances < 0 || item.hits < 0 || item.outs < 0 ||
                    item.earnedRuns < 0 || item.walks < 0 || item.strikeouts < 0 || !decisions.Add(item.key))
                    throw new ArgumentException("기용 관찰 이력이 잘못되었습니다.");
                _decisions.Add(item.Copy());
            }
            _news = new ManagerNewsProgressData
            {
                budgetSeason = data.budgetSeason, budgetWeek = data.budgetWeek, matchSeason = data.matchSeason,
                lastMatchOrdinal = data.lastMatchOrdinal, weeklyIndex = data.weeklyIndex,
                decisionSequence = data.decisionSequence, weekly = data.weekly.Copy(), previousWeek = data.previousWeek.Copy()
            };
        }
    }
}
