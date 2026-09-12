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
        private readonly Button[] _reportRows = new Button[2];
        private readonly Button[] _filters = new Button[3];
        private Text _reportBody, _pageLabel;
        private Button _reportBack, _reportAction, _bookmark, _pageBack, _pageNext;
        private int _filter, _page;
        private string _selectedReportId;

        private void BuildReports()
        {
            _reportsRoot = OwnerWorkspaceUiFactory.CreateRoot(_content, "ManagerReportPanel", false);
            _reportBack = MakeButton(_reportsRoot, "ReportBack", _copy.back, () => SetState(1), OwnerButtonRole.Quiet);
            Place(_reportBack, 8, 480, 164, 548);
            var title = MakeText(_reportsRoot, "ReportTitle", 24); title.text = _copy.review;
            SetRect(title.rectTransform, Vector2.zero, Vector2.zero, new Vector2(180, 488), new Vector2(536, 540));
            var close = MakeButton(_reportsRoot, "ReportClose", _copy.close, () => SetState(0), OwnerButtonRole.Quiet);
            Place(close, 552, 480, 708, 548);
            string[] filterLabels = { _copy.all, _copy.important, _copy.bookmarked };
            for (int i = 0; i < _filters.Length; i++)
            {
                int index = i;
                _filters[i] = MakeButton(_reportsRoot, "ReportFilter" + i, filterLabels[i], () =>
                { _filter = index; _page = 0; _selectedReportId = null; RenderReports(); }, OwnerButtonRole.Quiet);
                Place(_filters[i], 8 + i * 236, 404, 236 + i * 236, 472);
                OwnerDashboardStyle.Rule(_filters[i].transform, "SelectedRule", Vector2.zero, new Vector2(1, 0),
                    new Vector2(16, 0), new Vector2(-16, 2), OwnerDashboardStyle.Gold);
            }
            for (int i = 0; i < _reportRows.Length; i++)
            {
                int index = i;
                _reportRows[i] = MakeButton(_reportsRoot, "ReportRow" + i, _copy.loading, () => SelectReport(index), OwnerButtonRole.Quiet);
                Place(_reportRows[i], 8, 324 - i * 80, 708, 396 - i * 80);
                _reportRows[i].GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
                OwnerDashboardStyle.Rule(_reportRows[i].transform, "RowDivider", Vector2.zero, new Vector2(1, 0),
                    Vector2.zero, new Vector2(0, 1), OwnerDashboardStyle.Line);
            }
            _reportBody = MakeText(_reportsRoot, "ReportDetail", 22);
            SetRect(_reportBody.rectTransform, Vector2.zero, Vector2.zero, new Vector2(8, 152), new Vector2(708, 240));
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
                if (_filter == 0 || (_filter == 1 && report.priority != ManagerReportPriority.Normal) ||
                    (_filter == 2 && report.isBookmarked)) _filteredReports.Add(report);
            int pageCount = Math.Max(1, (_filteredReports.Count + 1) / 2);
            _page = Mathf.Clamp(_page, 0, pageCount - 1);
            for (int i = 0; i < _filters.Length; i++)
            {
                OwnerUiButtonSkin.SetSelected(_filters[i], i == _filter);
                _filters[i].transform.Find("SelectedRule").gameObject.SetActive(i == _filter);
                string label = i == 0 ? _copy.all : i == 1 ? _copy.important : _copy.bookmarked;
                _filters[i].GetComponentInChildren<Text>().text = label;
            }
            for (int i = 0; i < _reportRows.Length; i++)
            {
                int index = _page * 2 + i;
                _reportRows[i].gameObject.SetActive(index < _filteredReports.Count);
                if (index >= _filteredReports.Count) continue;
                var report = _filteredReports[index];
                string status = report.isExpired ? _copy.expiredLabel : report.isRead ? _copy.read : _copy.unreadLabel;
                string priority = report.priority == ManagerReportPriority.Normal ? "" : _copy.important + " · ";
                string bookmark = report.isBookmarked ? " · " + _copy.bookmarked : "";
                string category = _copy.categories[(int)report.category];
                _reportRows[i].GetComponentInChildren<Text>().text = priority + ActionLabel(report.ToGoal()) + " · " + status + bookmark +
                    "\n<color=#9BA8AF>" + category + " · " + string.Format(_copy.createdWeek, report.createdWeek + 1) +
                    (report.createdSeason > 0 ? " · " + report.createdSeason + "시즌" : "") + "</color>";
                OwnerUiButtonSkin.SetSelected(_reportRows[i], report.reportId == _selectedReportId);
            }
            var selected = SelectedReport();
            _reportBody.text = selected == null ? _filteredReports.Count == 0 ? _copy.noReports : _copy.selectReport
                : FormatBody(selected.ToGoal(), selected.homeScore, selected.awayScore);
            _reportAction.gameObject.SetActive(selected != null);
            _bookmark.gameObject.SetActive(selected != null);
            if (selected != null)
            {
                _reportAction.GetComponentInChildren<Text>().text = selected.isExpired ? _copy.expiredLabel : ActionLabel(selected.ToGoal());
                _bookmark.GetComponentInChildren<Text>().text = selected.isBookmarked ? _copy.unbookmark : _copy.bookmarked;
            }
            _pageBack.interactable = _page > 0;
            _pageNext.interactable = _page + 1 < pageCount;
            _pageLabel.text = (_page + 1) + " / " + pageCount;
        }

        private static void Place(Button button, float left, float bottom, float right, float top) =>
            SetRect((RectTransform)button.transform, Vector2.zero, Vector2.zero, new Vector2(left, bottom), new Vector2(right, top));
    }
}
