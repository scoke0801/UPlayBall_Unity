using System;
using System.Collections.Generic;
using Baseball.Game.Guide;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    public sealed partial class UI_System_OwnerGuide
    {
        private readonly List<ManagerReportData> _filteredReports = new();
        private readonly Button[] _reportRows = new Button[3];
        private readonly Button[] _filters = new Button[3];
        private readonly Text[] _rowDates = new Text[3];
        private readonly Text[] _rowStatuses = new Text[3];
        private Text _reportTitle;
        private Text _reportSubject;
        private Text _reportBody, _pageLabel;
        private Button _reportBack, _reportAction, _bookmark, _pageBack, _pageNext;
        private int _filter, _page;
        private string _selectedReportId;

        private void BuildReports()
        {
            _reportsRoot = OwnerWorkspaceUiFactory.CreateRoot(_content, "ManagerReportPanel", false);
            _reportBack = MakeButton(_reportsRoot, "ReportBack", _copy.back, ReturnToReportList, OwnerButtonRole.Quiet);
            Place(_reportBack, 8, 480, 164, 548);
            var title = _reportTitle = MakeText(_reportsRoot, "ReportTitle", 24); title.text = _copy.review;
            OwnerDashboardStyle.SetTypography(title, true);
            SetRect(title.rectTransform, Vector2.zero, Vector2.zero, new Vector2(180, 488), new Vector2(536, 540));
            var close = MakeButton(_reportsRoot, "ReportClose", _copy.close, () => SetState(0), OwnerButtonRole.Quiet);
            Place(close, 552, 480, 708, 548);
            string[] filterLabels = { _copy.all, _copy.important, _copy.bookmarked };
            for (int i = 0; i < _filters.Length; i++)
            {
                int index = i;
                _filters[i] = MakeButton(_reportsRoot, "ReportFilter" + i, filterLabels[i], () =>
                { _filter = index; _page = 0; _selectedReportId = null; RenderReports(); }, OwnerButtonRole.Tab);
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
            _reportBody = MakeText(_reportsRoot, "ReportDetail", 24);
            _reportSubject = MakeText(_reportsRoot, "ReportSubject", 28);
            OwnerDashboardStyle.SetTypography(_reportSubject, true);
            SetRect(_reportSubject.rectTransform, Vector2.zero, Vector2.zero, new Vector2(24, 384), new Vector2(692, 464));
            _reportBody.alignment = TextAnchor.UpperLeft;
            SetRect(_reportBody.rectTransform, Vector2.zero, Vector2.zero, new Vector2(24, 156), new Vector2(692, 368));
            _reportAction = MakeButton(_reportsRoot, "ReportNavigate", _copy.action, () =>
            {
                var report = SelectedReport();
                if (report == null) return;
                if (report.isExpired) { _reportBody.text = _copy.expired; return; }
                ActionRequested?.Invoke(report.ToGoal());
            });
            Place(_reportAction, 8, 76, 392, 144);
            _bookmark = MakeButton(_reportsRoot, "ReportBookmark", _copy.bookmarked, () =>
            {
                var report = SelectedReport();
                if (report != null) BookmarkRequested?.Invoke(report.reportId, !report.isBookmarked);
            }, OwnerButtonRole.Quiet);
            Place(_bookmark, 400, 76, 708, 144);
            _pageBack = MakeButton(_reportsRoot, "PreviousPage", _copy.previousPage, () => { _page--; RenderReports(); }, OwnerButtonRole.Quiet);
            Place(_pageBack, 8, 0, 212, 68);
            _pageNext = MakeButton(_reportsRoot, "NextPage", _copy.nextPage, () => { _page++; RenderReports(); }, OwnerButtonRole.Quiet);
            Place(_pageNext, 504, 0, 708, 68);
            _pageLabel = MakeText(_reportsRoot, "ReportPage", 22); _pageLabel.alignment = TextAnchor.MiddleCenter;
            SetRect(_pageLabel.rectTransform, Vector2.zero, Vector2.zero, new Vector2(220, 0), new Vector2(496, 68));
        }

        private void SelectReport(int row)
        {
            int index = _page * _reportRows.Length + row;
            if (index >= _filteredReports.Count) return;
            var report = _filteredReports[index];
            _selectedReportId = report.reportId;
            ReadRequested?.Invoke(report.reportId);
            RenderReports();
            _reportBack.Select();
        }

        private void ReturnToReportList()
        {
            if (_selectedReportId == null) { SetState(1); return; }
            string previous = _selectedReportId;
            _selectedReportId = null;
            RenderReports();
            int index = _filteredReports.FindIndex(report => report.reportId == previous);
            if (index >= 0)
            {
                _page = index / _reportRows.Length;
                RenderReports();
                _reportRows[index % _reportRows.Length].Select();
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
            if (_progress != null) foreach (var report in _progress.GetReports())
                if (_filter == 0 || (_filter == 1 && report.priority != ManagerReportPriority.Normal && !report.isExpired) ||
                    (_filter == 2 && report.isBookmarked)) _filteredReports.Add(report);
            int pageCount = Math.Max(1, (_filteredReports.Count + _reportRows.Length - 1) / _reportRows.Length);
            _page = Mathf.Clamp(_page, 0, pageCount - 1);
            var selected = SelectedReport();
            bool detail = selected != null;
            _reportTitle.text = detail ? _copy.reportDetail : _copy.review;
            _reportBack.GetComponentInChildren<Text>().text = detail ? _copy.reportList : _copy.back;
            for (int i = 0; i < _filters.Length; i++)
            {
                OwnerUiButtonSkin.SetSelected(_filters[i], i == _filter);
                _filters[i].gameObject.SetActive(!detail);
                string label = i == 0 ? _copy.all : i == 1 ? _copy.important : _copy.bookmarked;
                _filters[i].GetComponentInChildren<Text>().text = label;
            }
            for (int i = 0; i < _reportRows.Length; i++)
            {
                int index = _page * _reportRows.Length + i;
                _reportRows[i].gameObject.SetActive(!detail && index < _filteredReports.Count);
                if (index >= _filteredReports.Count) continue;
                var report = _filteredReports[index];
                string status = report.isExpired ? _copy.expiredLabel : report.isRead ? _copy.read : _copy.unreadLabel;
                string category = report.priority == ManagerReportPriority.Critical && !report.isExpired
                    ? _copy.urgent : _copy.categories[(int)report.category];
                _reportRows[i].GetComponentInChildren<Text>().text = FormatReportTitle(report.ToGoal());
                _rowDates[i].text = (report.createdSeason > 0 ? string.Format(_copy.reportDate, report.createdSeason, report.createdWeek + 1)
                    : string.Format(_copy.createdWeek, report.createdWeek + 1)) + " · " + category;
                _rowStatuses[i].text = status;
                _rowStatuses[i].color = report.isRead || report.isExpired ? OwnerDashboardStyle.Muted : OwnerDashboardStyle.Gold;
                OwnerUiButtonSkin.SetSelected(_reportRows[i], report.reportId == _selectedReportId);
                _reportRows[i].GetComponent<UIOwnerFrontOfficeButton>()?.SetUnread(!report.isRead && !report.isExpired);
            }
            _reportBody.gameObject.SetActive(detail || _filteredReports.Count == 0);
            _reportSubject.gameObject.SetActive(detail);
            if (detail) _reportSubject.text = FormatReportTitle(selected.ToGoal());
            _reportBody.text = selected == null ? _filter == 2 ? _copy.emptyBookmark : _filter == 1 ? _copy.emptyImportant : _copy.noReports
                : FormatBody(selected.ToGoal(), selected.homeScore, selected.awayScore)
                    + (selected.isExpired ? "\n\n" + _copy.expired : "");
            _reportAction.gameObject.SetActive(selected != null);
            _bookmark.gameObject.SetActive(selected != null);
            if (selected != null)
            {
                _reportAction.interactable = !selected.isExpired;
                _reportAction.GetComponentInChildren<Text>().text = selected.isExpired ? _copy.expiredLabel : ActionLabel(selected.ToGoal());
                _bookmark.GetComponentInChildren<Text>().text = selected.isBookmarked ? _copy.unbookmark : _copy.bookmarked;
            }
            _pageBack.interactable = _page > 0;
            _pageNext.interactable = _page + 1 < pageCount;
            _pageBack.gameObject.SetActive(!detail && _filteredReports.Count > 0);
            _pageNext.gameObject.SetActive(!detail && _filteredReports.Count > 0);
            _pageLabel.gameObject.SetActive(!detail && _filteredReports.Count > 0);
            _pageLabel.text = string.Format(_copy.reportPage, _page + 1, pageCount, _filteredReports.Count);
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
