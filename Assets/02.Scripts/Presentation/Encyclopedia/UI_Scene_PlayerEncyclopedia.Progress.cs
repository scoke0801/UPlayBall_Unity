using System;
using System.Collections.Generic;
using System.Text;
using Baseball.Presentation.Owner;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Encyclopedia
{
    public sealed partial class UI_Scene_PlayerEncyclopedia
    {
        private int _yearPage;
        private int _franchisePage;
        private const int MatrixYearPageSize = 10;
        private const int MatrixFranchisePageSize = 8;

        private void BuildMatrix()
        {
            OwnerRuntimeUiFactory.ClearChildren(_matrix);
            var years = new List<int>();
            var franchises = new SortedDictionary<string, string>(StringComparer.Ordinal);
            var cells = new Dictionary<string, EncyclopediaProgressCell>(StringComparer.Ordinal);
            foreach (var cell in _snapshot.Progress)
            {
                if (!years.Contains(cell.OriginYear)) years.Add(cell.OriginYear);
                franchises[cell.FranchiseId] = cell.FranchiseDisplayName;
                cells[MatrixKey(cell.FranchiseId, cell.OriginYear)] = cell;
            }
            years.Sort();
            var franchiseIds = new List<string>(franchises.Keys);
            _yearPage = Math.Min(_yearPage, Math.Max(0, (years.Count - 1) / MatrixYearPageSize));
            _franchisePage = Math.Min(_franchisePage, Math.Max(0, (franchiseIds.Count - 1) / MatrixFranchisePageSize));
            RectTransform navigation = Row(_matrix, "MatrixNavigation", 0, 30);
            Button previous = OwnerWorkspaceUiFactory.CreateButton(navigation, "PreviousYears", "◀ 이전 연도", () => { _yearPage--; BuildMatrix(); });
            previous.interactable = _yearPage > 0;
            int firstYear = _yearPage * MatrixYearPageSize;
            int yearCount = Math.Min(MatrixYearPageSize, years.Count - firstYear);
            string range = yearCount > 0 ? years[firstYear] + " ~ " + years[firstYear + yearCount - 1] : "연도 없음";
            OwnerWorkspaceUiFactory.CreateText(navigation, "YearRange", range, 14, FontStyle.Bold, TextAnchor.MiddleCenter);
            Button next = OwnerWorkspaceUiFactory.CreateButton(navigation, "NextYears", "다음 연도 ▶", () => { _yearPage++; BuildMatrix(); });
            next.interactable = firstYear + yearCount < years.Count;
            RectTransform header = Row(_matrix, "MatrixHeader", 38, 28);
            MatrixLabel(header, "구단 / 획득 카드", true);
            for (int i = 0; i < yearCount; i++) MatrixLabel(header, years[firstYear + i].ToString(), false);
            int firstFranchise = _franchisePage * MatrixFranchisePageSize;
            int franchiseCount = Math.Min(MatrixFranchisePageSize, franchiseIds.Count - firstFranchise);
            for (int i = 0; i < franchiseCount; i++)
            {
                string franchiseId = franchiseIds[firstFranchise + i];
                RectTransform row = Row(_matrix, "FranchiseRow" + i, 70 + i * 36, 30);
                MatrixLabel(row, franchises[franchiseId], true);
                for (int j = 0; j < yearCount; j++)
                {
                    int year = years[firstYear + j];
                    cells.TryGetValue(MatrixKey(franchiseId, year), out EncyclopediaProgressCell cell);
                    bool exists = cell != null && cell.HasTeamSeason;
                    string label = exists ? cell.EverAcquiredCardCount + "/" + cell.CollectibleCardCount : "—";
                    Button button = OwnerWorkspaceUiFactory.CreateButton(row, "Year" + year, label, () => OpenProgressCell(cell));
                    button.interactable = exists;
                    LayoutElement layout = button.GetComponent<LayoutElement>();
                    layout.minWidth = 28; layout.preferredWidth = 50; layout.flexibleWidth = 1;
                    button.transform.Find("Label").GetComponent<Text>().fontSize = 11;
                }
            }
            float footerTop = 76 + franchiseCount * 36;
            RectTransform paging = Row(_matrix, "FranchisePaging", footerTop, 30);
            Button previousTeams = OwnerWorkspaceUiFactory.CreateButton(paging, "PreviousFranchises", "◀ 이전 구단", () => { _franchisePage--; BuildMatrix(); });
            previousTeams.interactable = _franchisePage > 0;
            OwnerWorkspaceUiFactory.CreateText(paging, "MatrixHelp", "셀 선택: 해당 구단·연도 카드 보기", 12, FontStyle.Normal, TextAnchor.MiddleCenter);
            Button nextTeams = OwnerWorkspaceUiFactory.CreateButton(paging, "NextFranchises", "다음 구단 ▶", () => { _franchisePage++; BuildMatrix(); });
            nextTeams.interactable = firstFranchise + franchiseCount < franchiseIds.Count;
            var summary = new StringBuilder("Edition별 획득 / 전체 카드\n");
            var editions = new List<EncyclopediaEditionProgressSnapshot>(_snapshot.Editions);
            editions.Sort((left, right) => left.SortPriority != right.SortPriority ? left.SortPriority.CompareTo(right.SortPriority) : string.Compare(left.EditionId, right.EditionId, StringComparison.Ordinal));
            foreach (var edition in editions)
                summary.Append(edition.DisplayName).Append("  ").Append(edition.EverAcquired).Append(" / ").Append(edition.Total).Append("  · 현재 보유 ").Append(edition.Owned).Append(" · 위시 ").Append(edition.Wishlist).Append('\n');
            if (editions.Count == 0) summary.Append(_snapshot.EditionSummary);
            Text text = OwnerWorkspaceUiFactory.CreateText(_matrix, "EditionSummary", summary.ToString(), 13, FontStyle.Normal, TextAnchor.UpperLeft);
            OwnerRuntimeUiFactory.SetAnchors(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, -footerTop - 40));
            _count.text = "수집률은 한 번이라도 획득한 카드 기준입니다. 카드 판매 후에도 획득 이력은 유지됩니다.";
            CompactButtons(_matrix);
        }

        private static void MatrixLabel(RectTransform parent, string value, bool franchise)
        {
            Text text = OwnerWorkspaceUiFactory.CreateText(parent, "Heading", value, 12, FontStyle.Bold, TextAnchor.MiddleCenter);
            LayoutElement layout = text.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = franchise ? 86 : 28; layout.preferredWidth = franchise ? 110 : 50; layout.flexibleWidth = franchise ? 0 : 1;
        }

        private void OpenProgressCell(EncyclopediaProgressCell cell)
        {
            if (cell == null || !cell.HasTeamSeason) return;
            _filter = new EncyclopediaScreenFilter { FranchiseId = cell.FranchiseId, OriginYear = cell.OriginYear };
            _tab = 1;
            BuildFilters();
            Refresh();
            SetFeedback($"{cell.FranchiseDisplayName} {cell.OriginYear} · 선수 시즌 {cell.AcquiredPlayerSeasonCount}/{cell.PlayerSeasonCount} · 카드 {cell.EverAcquiredCardCount}/{cell.CollectibleCardCount} · 보유 {cell.OwnedCardCount} · 위시 {cell.WishlistCardCount}");
        }

        private static string MatrixKey(string franchiseId, int year) => franchiseId + "\n" + year;
    }
}
