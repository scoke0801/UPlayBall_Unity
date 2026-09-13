using System.Collections.Generic;
using Baseball.Game.Guide;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    public sealed partial class UI_System_OwnerGuide
    {
        private bool _bookmarksOnly;
        private int _historySeason;
        private Button _historyBookmarks, _historyPrevious, _historyNext;
        private Text _historySeasonLabel;
        private int ReportPageSize => _filter == 2 ? 2 : _reportRows.Length;
        private static int ReportSeason(ManagerReportData report) => report.news == null ? report.updatedSeason : report.createdSeason;
        private ManagerReportView ReportView => _filter == 0 ? ManagerReportView.Current :
            _filter == 1 ? ManagerReportView.News : _bookmarksOnly ? ManagerReportView.Bookmarked : ManagerReportView.History;

        private void BuildHistoryFilters()
        {
            _historyBookmarks = MakeButton(_reportsRoot, "HistoryBookmarks", _copy.bookmarked, () =>
            { _bookmarksOnly = !_bookmarksOnly; _page = 0; RenderReports(); }, OwnerButtonRole.Tab);
            _historyPrevious = MakeButton(_reportsRoot, "HistoryPreviousSeason", "◀", () => ChangeHistorySeason(-1), OwnerButtonRole.Quiet);
            _historyNext = MakeButton(_reportsRoot, "HistoryNextSeason", "▶", () => ChangeHistorySeason(1), OwnerButtonRole.Quiet);
            _historySeasonLabel = MakeText(_reportsRoot, "HistorySeason", 22);
            _historySeasonLabel.alignment = TextAnchor.MiddleCenter;
            Place(_historyBookmarks, 8, 332, 228, 400);
            Place(_historyPrevious, 236, 332, 304, 400);
            Place(_historyNext, 640, 332, 708, 400);
            SetRect(_historySeasonLabel.rectTransform, Vector2.zero, Vector2.zero, new Vector2(312, 332), new Vector2(632, 400));
        }

        private void RenderHistoryFilters(bool detail)
        {
            bool visible = _filter == 2 && !detail;
            _historyBookmarks.gameObject.SetActive(visible);
            _historyPrevious.gameObject.SetActive(visible); _historyNext.gameObject.SetActive(visible);
            _historySeasonLabel.gameObject.SetActive(visible);
            OwnerUiButtonSkin.SetSelected(_historyBookmarks, _bookmarksOnly);
            _historyBookmarks.GetComponentInChildren<Text>().text = _bookmarksOnly ? _copy.bookmarksOnly : _copy.allHistory;
            _historySeasonLabel.text = _historySeason == 0 ? _copy.allSeasons : string.Format(_copy.seasonOnly, _historySeason);
        }

        private void ChangeHistorySeason(int direction)
        {
            var seasons = new List<int> { 0 };
            if (_progress != null)
            {
                foreach (var report in _progress.GetReports())
                    if (!seasons.Contains(ReportSeason(report))) seasons.Add(ReportSeason(report));
                foreach (var summary in _progress.GetReportSummaries())
                    if (!seasons.Contains(summary.season)) seasons.Add(summary.season);
            }
            seasons.Sort();
            int index = seasons.IndexOf(_historySeason);
            _historySeason = seasons[(index + direction + seasons.Count) % seasons.Count];
            _page = 0; RenderReports();
        }
    }
}
