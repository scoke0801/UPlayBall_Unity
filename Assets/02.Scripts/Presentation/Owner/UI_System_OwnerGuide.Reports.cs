using System;
using System.Collections.Generic;
using Baseball.Game.Guide;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    public sealed partial class UI_System_OwnerGuide
    {
        private readonly List<ManagerReportCase> _filteredReports = new();
        private readonly Button[] _reportRows = new Button[3];
        private readonly Button[] _filters = new Button[3];
        private readonly Text[] _rowDates = new Text[3];
        private readonly Text[] _rowStatuses = new Text[3];
        private Text _reportTitle;
        private Text _reportSubject;
        private Text _reportBody, _pageLabel;
        private Button _reportBack, _reportAction, _bookmark, _pageBack, _pageNext, _reportReadAll;
        private int _filter, _page;
        private string _selectedReportId;
        private string _selectedCaseKey;
        private Button _reportKeep, _reportDefer;
        private ScrollRect _reportScroll;
        private Button _reportScrollUp, _reportScrollDown;
        private string _displayedReportId;

        private void BuildReports()
        {
            _reportsRoot = OwnerWorkspaceUiFactory.CreateRoot(_content, "ManagerReportPanel", false);
            _reportBack = MakeButton(_reportsRoot, "ReportBack", _copy.back, ReturnToReportList, OwnerButtonRole.Quiet);
            Place(_reportBack, 8, 480, 164, 548);
            var title = _reportTitle = MakeText(_reportsRoot, "ReportTitle", 24); title.text = _copy.review;
            OwnerDashboardStyle.SetTypography(title, true);
            SetRect(title.rectTransform, Vector2.zero, Vector2.zero, new Vector2(180, 488), new Vector2(350, 540));
            _reportReadAll = MakeButton(_reportsRoot, "ReadAllReports", _copy.allRead, () =>
            {
                AllReadRequested?.Invoke();
                _reportBack.Select();
            }, OwnerButtonRole.Quiet);
            Place(_reportReadAll, 360, 480, 536, 548);
            var close = MakeButton(_reportsRoot, "ReportClose", _copy.close, () => SetState(0), OwnerButtonRole.Quiet);
            Place(close, 552, 480, 708, 548);
            string[] filterLabels = { _copy.all, _copy.newsTab, _copy.historyTab };
            for (int i = 0; i < _filters.Length; i++)
            {
                int index = i;
                _filters[i] = MakeButton(_reportsRoot, "ReportFilter" + i, filterLabels[i], () =>
                { _filter = index; _page = 0; _selectedReportId = null; _selectedCaseKey = null; RenderReports(); }, OwnerButtonRole.Tab);
                Place(_filters[i], 8 + i * 236, 404, 236 + i * 236, 472);
            }
            for (int i = 0; i < _reportRows.Length; i++)
            {
                int index = i;
                _reportRows[i] = MakeButton(_reportsRoot, "ReportRow" + i, _copy.loading, () => SelectReport(index), OwnerButtonRole.ListItem);
                Place(_reportRows[i], 8, 294 - i * 110, 708, 396 - i * 110);
                var rowTitle = _reportRows[i].GetComponentInChildren<Text>();
                rowTitle.alignment = TextAnchor.MiddleLeft;
                rowTitle.fontSize = 24;
                OwnerDashboardStyle.SetTypography(rowTitle, true);
                SetRect(rowTitle.rectTransform, Vector2.zero, Vector2.one, new Vector2(20, 45), new Vector2(-20, -10));
                _rowDates[i] = MakeText(_reportRows[i].transform, "Date", 22);
                _rowDates[i].color = OwnerDashboardStyle.Muted;
                SetRect(_rowDates[i].rectTransform, Vector2.zero, Vector2.one, new Vector2(20, 10), new Vector2(-212, -58));
                _rowStatuses[i] = MakeText(_reportRows[i].transform, "Status", 22);
                _rowStatuses[i].alignment = TextAnchor.MiddleRight;
                SetRect(_rowStatuses[i].rectTransform, Vector2.zero, Vector2.one, new Vector2(496, 10), new Vector2(-20, -58));
            }
            _reportScroll = OwnerRuntimeUiFactory.CreateVerticalScroll("ReportEvidenceScroll", _reportsRoot, out var evidenceContent);
            SetRect((RectTransform)_reportScroll.transform, Vector2.zero, Vector2.zero, new Vector2(24, 224), new Vector2(612, 388));
            _reportBody = MakeText(evidenceContent, "ReportDetail", 24);
            _reportScrollUp = MakeButton(_reportsRoot, "EvidenceUp", "▲", () => ScrollReportEvidence(1), OwnerButtonRole.Quiet);
            _reportScrollDown = MakeButton(_reportsRoot, "EvidenceDown", "▼", () => ScrollReportEvidence(-1), OwnerButtonRole.Quiet);
            Place(_reportScrollUp, 624, 316, 692, 384);
            Place(_reportScrollDown, 624, 232, 692, 300);
            _reportScroll.onValueChanged.AddListener(_ => RefreshEvidenceScrollButtons());
            _reportSubject = MakeText(_reportsRoot, "ReportSubject", 28);
            OwnerDashboardStyle.SetTypography(_reportSubject, true);
            SetRect(_reportSubject.rectTransform, Vector2.zero, Vector2.zero, new Vector2(24, 396), new Vector2(692, 464));
            _reportBody.alignment = TextAnchor.UpperLeft;
            _reportAction = MakeButton(_reportsRoot, "ReportNavigate", _copy.action, () =>
            {
                var report = SelectedReport();
                if (report == null) return;
                if (report.isExpired) { _reportBody.text = _copy.expired; return; }
                ActionRequested?.Invoke(report.ToGoal());
            });
            Place(_reportAction, 8, 148, 344, 216);
            _reportKeep = MakeButton(_reportsRoot, "ReportKeep", _copy.keep, () =>
            {
                var report = SelectedReport();
                if (report != null) KeepRequested?.Invoke(report.deduplicationKey, !report.isAccepted);
                _reportBack.Select();
            }, OwnerButtonRole.Quiet);
            Place(_reportKeep, 352, 148, 708, 216);
            _bookmark = MakeButton(_reportsRoot, "ReportBookmark", _copy.bookmarked, () =>
            {
                var report = SelectedReport();
                if (report != null && !report.isBookmarked && !_progress.CanBookmarkReport(report.reportId))
                { SetFeedback(_copy.bookmarkLimit); return; }
                if (report != null) BookmarkRequested?.Invoke(report.reportId, !report.isBookmarked);
            }, OwnerButtonRole.Quiet);
            Place(_bookmark, 8, 76, 344, 144);
            _reportDefer = MakeButton(_reportsRoot, "ReportDefer", _copy.defer, () =>
            {
                var report = SelectedReport();
                if (report != null) DeferRequested?.Invoke(report.reportId);
                _reportBack.Select();
            }, OwnerButtonRole.Quiet);
            Place(_reportDefer, 352, 76, 708, 144);
            _pageBack = MakeButton(_reportsRoot, "PreviousPage", _copy.previousPage, () => ChangeReportPage(-1), OwnerButtonRole.Quiet);
            Place(_pageBack, 8, 0, 212, 68);
            _pageNext = MakeButton(_reportsRoot, "NextPage", _copy.nextPage, () => ChangeReportPage(1), OwnerButtonRole.Quiet);
            Place(_pageNext, 504, 0, 708, 68);
            _pageLabel = MakeText(_reportsRoot, "ReportPage", 22); _pageLabel.alignment = TextAnchor.MiddleCenter;
            SetRect(_pageLabel.rectTransform, Vector2.zero, Vector2.zero, new Vector2(220, 0), new Vector2(496, 68));
            BuildHistoryFilters();
        }

        private void SelectReport(int row)
        {
            int index = _page * ReportPageSize + row;
            if (index >= _filteredReports.Count) return;
            var item = _filteredReports[index];
            _selectedCaseKey = item.Key;
            _selectedReportId = item.Primary.reportId;
            ReadRequested?.Invoke(_selectedReportId);
            RenderReports();
            _reportBack.Select();
        }

        private void ReturnToReportList()
        {
            if (_selectedReportId == null) { SetState(1); return; }
            string previous = _selectedCaseKey;
            _selectedReportId = null;
            _selectedCaseKey = null;
            RenderReports();
            int index = _filteredReports.FindIndex(report => report.Key == previous);
            if (index >= 0)
            {
                _page = index / ReportPageSize;
                RenderReports();
                _reportRows[index % ReportPageSize].Select();
            }
            else if (_reportRows[0].gameObject.activeInHierarchy) _reportRows[0].Select();
            else _filters[_filter].Select();
        }

        private ManagerReportData SelectedReport()
        {
            if (_progress == null) return null;
            foreach (var report in _progress.GetReports()) if (report.reportId == _selectedReportId) return report;
            return null;
        }

        private void RenderReports()
        {
            _filteredReports.Clear();
            if (_progress != null) foreach (var item in _progress.GetReportCases(ReportView))
                if (_filter != 2 || _historySeason == 0 || ReportSeason(item.Primary) == _historySeason) _filteredReports.Add(item);
            int pageCount = Math.Max(1, (_filteredReports.Count + ReportPageSize - 1) / ReportPageSize);
            _page = Mathf.Clamp(_page, 0, pageCount - 1);
            var selected = SelectedReport();
            bool detail = selected != null;
            bool emptyHistory = !detail && _filter == 2;
            SetRect((RectTransform)_reportScroll.transform, Vector2.zero, Vector2.zero,
                new Vector2(24, emptyHistory ? 152 : 224), new Vector2(612, emptyHistory ? 316 : 388));
            Place(_reportScrollUp, 624, emptyHistory ? 244 : 316, 692, emptyHistory ? 312 : 384);
            Place(_reportScrollDown, 624, emptyHistory ? 160 : 232, 692, emptyHistory ? 228 : 300);
            RenderHistoryFilters(detail);
            bool hasUnread = false;
            if (_progress != null) foreach (var report in _progress.GetReports())
                if (!report.isRead && !report.isExpired) { hasUnread = true; break; }
            _reportReadAll.gameObject.SetActive(!detail && hasUnread);
            _reportTitle.gameObject.SetActive(true);
            _reportTitle.text = _copy.shortTitle;
            _reportBack.GetComponentInChildren<Text>().text = detail ? _copy.reportList : _copy.back;
            for (int i = 0; i < _filters.Length; i++)
            {
                OwnerUiButtonSkin.SetSelected(_filters[i], i == _filter);
                _filters[i].gameObject.SetActive(!detail);
                string label = i == 0 ? _copy.all : i == 1 ? _copy.newsTab : _copy.historyTab;
                _filters[i].GetComponentInChildren<Text>().text = label;
            }
            for (int i = 0; i < _reportRows.Length; i++)
            {
                int index = _page * ReportPageSize + i;
                _reportRows[i].gameObject.SetActive(!detail && i < ReportPageSize && index < _filteredReports.Count);
                Place(_reportRows[i], 8, (_filter == 2 ? 222 : 294) - i * 110, 708, (_filter == 2 ? 324 : 396) - i * 110);
                if (index >= _filteredReports.Count) continue;
                var item = _filteredReports[index];
                var report = item.Primary;
                string status = report.news != null ? report.isExpired ? _copy.pastNews : item.HasUnread ? _copy.newNews : _copy.read :
                    report.isExpired ? _copy.expiredLabel : report.priority == ManagerReportPriority.Critical
                    ? _copy.urgent : item.IsAccepted ? _copy.accepted :
                    item.IsDeferred ? _copy.deferred : item.HasUnread ? item.HasUpdates ? _copy.updated : _copy.unreadLabel : _copy.read;
                _reportRows[i].GetComponentInChildren<Text>().text = FormatCaseTitle(item);
                _rowDates[i].text = FormatCaseSummary(item);
                _rowStatuses[i].text = status;
                _rowStatuses[i].color = item.HasUnread ? OwnerDashboardStyle.Gold : OwnerDashboardStyle.Muted;
                OwnerUiButtonSkin.SetSelected(_reportRows[i], item.Key == _selectedCaseKey);
                _reportRows[i].GetComponent<UIOwnerFrontOfficeButton>()?.SetUnread(item.HasUnread);
            }
            _reportBody.gameObject.SetActive(detail || _filteredReports.Count == 0);
            _reportScroll.gameObject.SetActive(detail || _filteredReports.Count == 0);
            _reportScrollUp.gameObject.SetActive(detail || _filteredReports.Count == 0);
            _reportScrollDown.gameObject.SetActive(detail || _filteredReports.Count == 0);
            _reportSubject.gameObject.SetActive(detail);
            if (detail) _reportSubject.text = selected.news == null ? FormatReportTitle(selected.ToGoal()) : FormatNewsTitle(selected);
            _reportBody.text = selected == null ? _filter == 2 ? _bookmarksOnly ? _copy.emptyBookmark : _copy.emptyImportant : _filter == 1 ? _copy.emptyNews : _copy.noReports
                : FormatReportDetail(selected);
            _reportAction.gameObject.SetActive(selected != null && !selected.isExpired);
            _bookmark.gameObject.SetActive(selected != null);
            _reportKeep.gameObject.SetActive(selected != null && !selected.isExpired &&
                selected.kind == GuideGoalKind.PresetIssue && selected.priority != ManagerReportPriority.Critical);
            _reportDefer.gameObject.SetActive(selected != null && selected.news == null && !selected.isExpired && !selected.isAccepted);
            if (selected != null)
            {
                _reportAction.interactable = !selected.isExpired;
                _reportAction.GetComponentInChildren<Text>().text = selected.isExpired ? _copy.expiredLabel :
                    selected.news != null && selected.target == GuideTargetKind.Analysis ? _copy.recordsAction : ActionLabel(selected.ToGoal());
                _bookmark.GetComponentInChildren<Text>().text = selected.isBookmarked ? _copy.unbookmark : _copy.bookmarked;
                _reportKeep.GetComponentInChildren<Text>().text = selected.isAccepted ? _copy.reconsider : _copy.keep;
            }
            var selectedCase = SelectedCase();
            int member = FindSelectedMember(selectedCase);
            int count = detail ? selectedCase?.Members.Count ?? 1 : pageCount;
            int position = detail ? member : _page;
            _pageBack.interactable = position > 0;
            _pageNext.interactable = position + 1 < count;
            _pageBack.gameObject.SetActive(detail ? count > 1 : _filteredReports.Count > 0);
            _pageNext.gameObject.SetActive(detail ? count > 1 : _filteredReports.Count > 0);
            _pageLabel.gameObject.SetActive(detail || _filteredReports.Count > 0);
            _pageBack.GetComponentInChildren<Text>().text = detail ? _copy.previousTarget : _copy.previousPage;
            _pageNext.GetComponentInChildren<Text>().text = detail ? _copy.nextTarget : _copy.nextPage;
            _pageLabel.text = detail ? string.Format(_copy.targetPage, member + 1, count) :
                string.Format(_copy.reportPage, _page + 1, pageCount, _filteredReports.Count);
            if (!detail && _filter == 2 && !_bookmarksOnly && _progress != null)
            {
                var summaries = _progress.GetReportSummaries();
                int targets = 0, seasons = 0;
                foreach (var summary in summaries)
                    if (_historySeason == 0 || summary.season == _historySeason) { targets += summary.closedTargets; seasons++; }
                if (targets > 0)
                {
                    string summary = string.Format(_copy.historySummary, seasons, targets);
                    if (_filteredReports.Count == 0) _reportBody.text += "\n\n" + summary;
                    else _pageLabel.text += "\n" + string.Format(_copy.historySummaryShort, seasons, targets);
                }
            }
            if (_reportScroll.gameObject.activeSelf)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_reportScroll.transform);
                if (_displayedReportId != _selectedReportId) _reportScroll.verticalNormalizedPosition = 1;
                _displayedReportId = _selectedReportId;
                RefreshEvidenceScrollButtons();
            }
        }

        private void RefreshEvidenceScrollButtons()
        {
            bool overflow = _reportScroll.content.rect.height > _reportScroll.viewport.rect.height + 1;
            _reportScrollUp.interactable = overflow && _reportScroll.verticalNormalizedPosition < .99f;
            _reportScrollDown.interactable = overflow && _reportScroll.verticalNormalizedPosition > .01f;
        }

        private void ScrollReportEvidence(int direction)
        {
            float range = _reportScroll.content.rect.height - _reportScroll.viewport.rect.height;
            if (range <= 0) return;
            _reportScroll.verticalNormalizedPosition = Mathf.Clamp01(_reportScroll.verticalNormalizedPosition +
                direction * _reportScroll.viewport.rect.height / range);
            RefreshEvidenceScrollButtons();
            if (direction > 0 && !_reportScrollUp.interactable) _reportScrollDown.Select();
            if (direction < 0 && !_reportScrollDown.interactable) _reportScrollUp.Select();
        }

        private ManagerReportCase SelectedCase() => _filteredReports.Find(item => item.Key == _selectedCaseKey);

        private int FindSelectedMember(ManagerReportCase item)
        {
            if (item != null) for (int i = 0; i < item.Members.Count; i++)
                if (item.Members[i].reportId == _selectedReportId) return i;
            return 0;
        }

        private void ChangeReportPage(int direction)
        {
            var item = SelectedCase();
            if (_selectedReportId != null && item != null)
            {
                int index = Mathf.Clamp(FindSelectedMember(item) + direction, 0, item.Members.Count - 1);
                _selectedReportId = item.Members[index].reportId;
                ReadRequested?.Invoke(_selectedReportId);
            }
            else _page += direction;
            RenderReports();
            _reportBack.Select();
        }

        private Baseball.Presentation.Guide.OwnerGuideIssueCopy FindIssueCopy(GuideGoal goal)
        {
            if (_copy.issues == null || goal == null) return null;
            foreach (var issue in _copy.issues)
                if (issue.kind == goal.Kind.ToString() && issue.evidence == goal.Evidence) return issue;
            return null;
        }

        private string FormatReportTitle(GuideGoal goal)
        {
            var issue = FindIssueCopy(goal);
            if (issue != null) return issue.title;
            return goal.Kind switch
            {
                GuideGoalKind.Preparation => _copy.preparationTitle,
                GuideGoalKind.PlanConfirmation => _copy.confirmationTitle,
                GuideGoalKind.Debrief => _copy.debriefTitle,
                GuideGoalKind.RosterIssue => _copy.rosterTitle,
                _ => _copy.presetTitle
            };
        }

        private static void Place(Button button, float left, float bottom, float right, float top) =>
            SetRect((RectTransform)button.transform, Vector2.zero, Vector2.zero, new Vector2(left, bottom), new Vector2(right, top));
    }
}
