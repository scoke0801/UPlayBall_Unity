using System;
using Baseball.Core.Players;
using Baseball.Game.Career;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Career
{
    public sealed partial class UI_Scene_CareerRecords
    {
        private const float RecordTableViewportWidth = 1034f;
        private const float RecordTableViewportHeight = 370f;
        private const float RecordTableHeaderHeight = 34f;
        private const float RecordTableRowHeight = 35f;
        private const float RecordTableIdentityWidth = 342f;
        private const float RecordTableMetricWidth = 92f;

        private static void RenderScrollableLeaderboardTable(
            Transform panel,
            CareerRecordsView view)
        {
            UIXScrollView table = CreateRecordTable(
                panel,
                "LeaderboardTable",
                view.LeaderboardColumns,
                view.Leaderboard.Length);
            RenderRecordTableHeader(
                CreateStickyTableHeader(table),
                view.LeaderboardColumns,
                includeRank: true,
                showSeasonRank: false);

            for (int index = 0; index < view.Leaderboard.Length; index++)
            {
                CareerRecordLeaderboardRow row = view.Leaderboard[index];
                float top = RecordTableHeaderHeight + index * RecordTableRowHeight;
                Color background = row.IsMyPlayer
                    ? new Color(0.025f, 0.18f, 0.34f, 1f)
                    : index % 2 == 0
                        ? new Color(0.01f, 0.042f, 0.071f, 1f)
                        : PanelDarkColor;
                RectTransform rowRoot = CreateTopLeftImage(
                    "Player_" + row.PlayerId,
                    table.Content,
                    background,
                    new Vector2(table.Content.sizeDelta.x, RecordTableRowHeight - 2f),
                    0f,
                    top + 1f);
                if (row.IsMyPlayer)
                {
                    CreateTopLeftImage(
                        "Selection",
                        rowRoot,
                        BrightAccentColor,
                        new Vector2(4f, RecordTableRowHeight - 6f),
                        0f,
                        2f);
                }

                Color valueColor = row.IsMyPlayer ? BrightAccentColor : PrimaryTextColor;
                CreateTopLeftText("Rank", rowRoot, row.Rank.ToString(), 15, FontStyle.Bold,
                    TextAnchor.MiddleCenter, new Vector2(64f, 31f), 0f, 1f, valueColor);
                CreateTopLeftText("Name", rowRoot, row.PlayerName, 15,
                    row.IsMyPlayer ? FontStyle.Bold : FontStyle.Normal,
                    TextAnchor.MiddleLeft, new Vector2(190f, 31f), 64f, 1f, valueColor);
                CreateTopLeftText("Team", rowRoot, GetTeamShortName(row.TeamName), 14, FontStyle.Normal,
                    TextAnchor.MiddleCenter, new Vector2(88f, 31f), 254f, 1f, SecondaryTextColor);
                RenderMetricValues(rowRoot, row.Metrics, valueColor);
            }
        }

        private static void RenderScrollableSeasonTable(
            Transform panel,
            CareerRecordMetric[] columns,
            CareerRecordSeasonRow[] seasons,
            bool showRank)
        {
            UIXScrollView table = CreateRecordTable(panel, "SeasonTable", columns, seasons.Length);
            RenderRecordTableHeader(
                CreateStickyTableHeader(table), columns, includeRank: false, showSeasonRank: showRank);

            for (int index = 0; index < seasons.Length; index++)
            {
                CareerRecordSeasonRow row = seasons[index];
                float top = RecordTableHeaderHeight + index * RecordTableRowHeight;
                Color background = row.IsCurrent
                    ? new Color(0.025f, 0.18f, 0.34f, 1f)
                    : index % 2 == 0
                        ? new Color(0.01f, 0.042f, 0.071f, 1f)
                        : PanelDarkColor;
                RectTransform rowRoot = CreateTopLeftImage(
                    "Season_" + row.Year,
                    table.Content,
                    background,
                    new Vector2(table.Content.sizeDelta.x, RecordTableRowHeight - 2f),
                    0f,
                    top + 1f);
                Color valueColor = row.IsCurrent ? BrightAccentColor : PrimaryTextColor;
                CreateTopLeftText("Year", rowRoot, showRank ? (index + 1).ToString() : row.Year.ToString(),
                    15, FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(64f, 31f), 0f, 1f, valueColor);
                string teamName = row.IsCurrent ? row.TeamName + "  (진행 중)" : row.TeamName;
                CreateTopLeftText("Team", rowRoot, teamName, 14,
                    row.IsCurrent ? FontStyle.Bold : FontStyle.Normal,
                    TextAnchor.MiddleLeft, new Vector2(190f, 31f), 64f, 1f, valueColor);
                CreateTopLeftText("League", rowRoot, GetLeagueLabel(row.LeagueLevel), 13, FontStyle.Normal,
                    TextAnchor.MiddleCenter, new Vector2(88f, 31f), 254f, 1f, SecondaryTextColor);
                RenderMetricValues(rowRoot, row.Metrics, valueColor);
            }
        }

        private static UIXScrollView CreateRecordTable(
            Transform panel,
            string name,
            CareerRecordMetric[] columns,
            int rowCount)
        {
            float contentWidth = Mathf.Max(
                RecordTableViewportWidth - 13f,
                RecordTableIdentityWidth + columns.Length * RecordTableMetricWidth);
            float contentHeight = Mathf.Max(
                RecordTableViewportHeight - 13f,
                RecordTableHeaderHeight + rowCount * RecordTableRowHeight);
            bool canScrollHorizontally = contentWidth > RecordTableViewportWidth - 13f;
            bool canScrollVertically = contentHeight > RecordTableViewportHeight - 13f;
            return UIXScrollView.Create(
                panel,
                name,
                new Vector2(RecordTableViewportWidth, RecordTableViewportHeight),
                new Vector2(0f, -8f),
                new Vector2(contentWidth, contentHeight),
                canScrollHorizontally,
                canScrollVertically,
                new Color(0.006f, 0.028f, 0.05f, 0.82f),
                new Color(0.025f, 0.09f, 0.14f, 1f),
                new Color(0.18f, 0.52f, 0.76f, 1f));
        }

        private static RectTransform CreateStickyTableHeader(UIXScrollView table)
        {
            return table.CreateStickyHeader(
                table.Content.sizeDelta.x,
                RecordTableHeaderHeight,
                new Color(0.006f, 0.028f, 0.05f, 1f));
        }

        private static void RenderRecordTableHeader(
            Transform header,
            CareerRecordMetric[] columns,
            bool includeRank,
            bool showSeasonRank)
        {
            CreateTopLeftText("Rank", header, includeRank || showSeasonRank ? "순위" : "연도",
                14, FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(64f, 30f),
                0f, 2f, SecondaryTextColor);
            CreateTopLeftText("Name", header, includeRank ? "선수명" : "소속 구단",
                14, FontStyle.Bold, TextAnchor.MiddleLeft, new Vector2(190f, 30f),
                64f, 2f, SecondaryTextColor);
            CreateTopLeftText("Team", header, includeRank ? "팀" : "리그",
                14, FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(88f, 30f),
                254f, 2f, SecondaryTextColor);
            for (int index = 0; index < columns.Length; index++)
            {
                CreateTopLeftText(
                    "Metric_" + index,
                    header,
                    GetMetricLabel(columns[index], false),
                    13,
                    FontStyle.Bold,
                    TextAnchor.MiddleCenter,
                    new Vector2(RecordTableMetricWidth, 30f),
                    RecordTableIdentityWidth + index * RecordTableMetricWidth,
                    2f,
                    SecondaryTextColor);
            }
        }

        private static void RenderMetricValues(
            Transform row,
            CareerRecordMetricValue[] metrics,
            Color color)
        {
            for (int index = 0; index < metrics.Length; index++)
            {
                CareerRecordMetricValue metric = metrics[index];
                CreateTopLeftText(
                    "Value_" + index,
                    row,
                    FormatMetric(metric.Metric, metric.Value),
                    14,
                    FontStyle.Normal,
                    TextAnchor.MiddleCenter,
                    new Vector2(RecordTableMetricWidth, 31f),
                    RecordTableIdentityWidth + index * RecordTableMetricWidth,
                    1f,
                    color);
            }
        }

        private static void RenderScrollableMetricGrid(
            Transform panel,
            CareerRecordMetricValue[] metrics)
        {
            const float cellWidth = 229f;
            const float cellHeight = 38f;
            int rowCount = Math.Max(1, (metrics.Length + 1) / 2);
            float contentHeight = Mathf.Max(196f, rowCount * cellHeight);
            UIXScrollView scroll = UIXScrollView.Create(
                panel,
                "MetricScroll",
                new Vector2(486f, 196f),
                new Vector2(0f, -5f),
                new Vector2(470f, contentHeight),
                horizontal: false,
                vertical: contentHeight > 196f,
                new Color(0.006f, 0.028f, 0.05f, 0.5f),
                new Color(0.025f, 0.09f, 0.14f, 1f),
                new Color(0.18f, 0.52f, 0.76f, 1f));

            for (int index = 0; index < metrics.Length; index++)
            {
                int column = index % 2;
                int rowIndex = index / 2;
                CareerRecordMetricValue metric = metrics[index];
                RectTransform cell = CreateTopLeftImage(
                    "Metric_" + metric.Metric,
                    scroll.Content,
                    rowIndex % 2 == 0 ? new Color(0.01f, 0.045f, 0.074f, 1f) : PanelDarkColor,
                    new Vector2(cellWidth, cellHeight - 2f),
                    column * (cellWidth + 6f),
                    rowIndex * cellHeight + 1f);
                CreateTopLeftText("Label", cell, GetMetricLabel(metric.Metric, true), 13, FontStyle.Normal,
                    TextAnchor.MiddleLeft, new Vector2(100f, 32f), 8f, 2f, SecondaryTextColor);
                CreateTopLeftText("Value", cell, FormatMetric(metric.Metric, metric.Value), 15, FontStyle.Bold,
                    TextAnchor.MiddleRight, new Vector2(67f, 32f), 105f, 2f, PrimaryTextColor);
                CreateTopLeftText("Rank", cell, metric.HasRank ? metric.Rank + "위" : "-", 13, FontStyle.Bold,
                    TextAnchor.MiddleRight, new Vector2(46f, 32f), 176f, 2f,
                    metric.HasRank ? RankColor : MutedTextColor);
            }
        }

        private static void RenderScrollableMovementRows(
            Transform panel,
            CareerRecordsView view)
        {
            const float rowHeight = 54f;
            int totalRows = view.TradeHistory.Length + view.TeamSplits.Length;
            float contentHeight = Mathf.Max(322f, totalRows * rowHeight);
            UIXScrollView scroll = UIXScrollView.Create(
                panel,
                "MovementScroll",
                new Vector2(484f, 322f),
                new Vector2(0f, -18f),
                new Vector2(470f, contentHeight),
                horizontal: false,
                vertical: contentHeight > 322f,
                new Color(0.006f, 0.028f, 0.05f, 0.5f),
                new Color(0.025f, 0.09f, 0.14f, 1f),
                new Color(0.18f, 0.52f, 0.76f, 1f));

            int rowIndex = 0;
            for (int index = 0; index < view.TradeHistory.Length; index++)
            {
                CareerTradeHistoryView trade = view.TradeHistory[index];
                RectTransform row = CreateTopLeftImage(
                    "Trade_" + index,
                    scroll.Content,
                    new Color(0.025f, 0.15f, 0.27f, 1f),
                    new Vector2(470f, 49f),
                    0f,
                    rowIndex * rowHeight);
                CreateTopLeftText("TradeDate", row, $"{trade.Year} · {trade.GameIndex}경기 후", 11,
                    FontStyle.Bold, TextAnchor.MiddleLeft, new Vector2(145f, 20f),
                    12f, 5f, BrightAccentColor);
                CreateTopLeftText("TradeTeams", row,
                    $"{GetTeamShortName(trade.PreviousTeamName)} → {GetTeamShortName(trade.NewTeamName)}", 14,
                    FontStyle.Bold, TextAnchor.MiddleLeft, new Vector2(290f, 22f),
                    165f, 4f, PrimaryTextColor);
                CreateTopLeftText("TradeRole", row,
                    $"{GetExpectedRoleLabel(trade.PreviousRole)} → {GetExpectedRoleLabel(trade.ProjectedRole)}", 11,
                    FontStyle.Normal, TextAnchor.MiddleLeft, new Vector2(440f, 18f),
                    12f, 27f, SecondaryTextColor);
                rowIndex++;
            }

            for (int index = 0; index < view.TeamSplits.Length; index++)
            {
                CareerTeamStatisticsSplitView split = view.TeamSplits[index];
                RectTransform row = CreateTopLeftImage(
                    "Split_" + split.Year + "_" + split.TeamId,
                    scroll.Content,
                    rowIndex % 2 == 0 ? new Color(0.01f, 0.042f, 0.071f, 1f) : PanelDarkColor,
                    new Vector2(470f, 49f),
                    0f,
                    rowIndex * rowHeight);
                CreateTopLeftText("Season", row, $"{split.Year} {GetTeamShortName(split.TeamName)}", 13,
                    FontStyle.Bold, TextAnchor.MiddleLeft, new Vector2(230f, 22f),
                    12f, 4f, split.IsCurrentSeason ? BrightAccentColor : PrimaryTextColor);
                CreateTopLeftText("TeamGames", row, $"팀 {split.TeamGames}경기", 11,
                    FontStyle.Normal, TextAnchor.MiddleRight, new Vector2(105f, 20f),
                    345f, 5f, MutedTextColor);
                CreateTopLeftText("Metrics", row, FormatSplitSummary(split.Metrics), 11,
                    FontStyle.Normal, TextAnchor.MiddleLeft, new Vector2(440f, 18f),
                    12f, 27f, SecondaryTextColor);
                rowIndex++;
            }
        }

        private void RenderScrollableAwardRows(
            Transform panel,
            CareerAwardRecordView[] awards)
        {
            const float headerHeight = 34f;
            const float rowHeight = 35f;
            float contentHeight = Mathf.Max(382f, headerHeight + awards.Length * rowHeight);
            UIXScrollView scroll = UIXScrollView.Create(
                panel,
                "AwardScroll",
                new Vector2(1034f, 382f),
                new Vector2(0f, -10f),
                new Vector2(1021f, contentHeight),
                horizontal: false,
                vertical: contentHeight > 382f,
                new Color(0.006f, 0.028f, 0.05f, 0.82f),
                new Color(0.025f, 0.09f, 0.14f, 1f),
                new Color(0.18f, 0.52f, 0.76f, 1f));
            RectTransform header = scroll.CreateStickyHeader(
                1021f, headerHeight, new Color(0.006f, 0.028f, 0.05f, 1f));
            string[] labels = { "연도", "수상", "리그", "포지션" };
            float[] starts = { 0f, 100f, 620f, 820f };
            float[] widths = { 100f, 520f, 200f, 190f };
            for (int index = 0; index < labels.Length; index++)
            {
                CreateTopLeftText("Header_" + index, header, labels[index], 13, FontStyle.Bold,
                    index == 1 ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter,
                    new Vector2(widths[index], 30f), starts[index], 2f, SecondaryTextColor);
            }

            for (int index = 0; index < awards.Length; index++)
            {
                CareerAwardRecordView award = awards[index];
                RectTransform row = CreateTopLeftImage("Award_" + index, scroll.Content,
                    index % 2 == 0 ? new Color(0.01f, 0.042f, 0.071f, 1f) : PanelDarkColor,
                    new Vector2(1021f, rowHeight - 2f), 0f, headerHeight + index * rowHeight + 1f);
                if (CareerPresentationRequestFactory.TryCreateAwardReplay(
                        award,
                        _manager.CurrentCareer.MyPlayer.Name,
                        out CareerPresentationRequest replayRequest))
                {
                    Image rowImage = row.GetComponent<Image>();
                    var replayButton = row.gameObject.AddComponent<Button>();
                    replayButton.targetGraphic = rowImage;
                    ColorBlock colors = replayButton.colors;
                    colors.highlightedColor = new Color(0.025f, 0.13f, 0.20f, 1f);
                    colors.pressedColor = new Color(0.02f, 0.09f, 0.15f, 1f);
                    replayButton.colors = colors;
                    replayButton.onClick.AddListener(() =>
                        UI_CareerPresentation.Instance?.Replay(replayRequest));
                }
                CreateTopLeftText("Year", row, award.Year.ToString(), 14, FontStyle.Bold,
                    TextAnchor.MiddleCenter, new Vector2(widths[0], 31f), starts[0], 1f,
                    award.IsCurrent ? BrightAccentColor : PrimaryTextColor);
                CreateTopLeftText("Award", row, GetAwardLabel(award.Category), 14, FontStyle.Bold,
                    TextAnchor.MiddleLeft, new Vector2(widths[1], 31f), starts[1], 1f, GoldColor);
                CreateTopLeftText("League", row, GetLeagueLabel(award.LeagueLevel), 13, FontStyle.Normal,
                    TextAnchor.MiddleCenter, new Vector2(widths[2], 31f), starts[2], 1f, SecondaryTextColor);
                CreateTopLeftText("Position", row, GetPositionLabel(award.Position), 13, FontStyle.Normal,
                    TextAnchor.MiddleCenter, new Vector2(widths[3], 31f), starts[3], 1f, SecondaryTextColor);
            }
        }

        private static void RenderScrollableHighlightRows(
            Transform panel,
            CareerRecordHighlightView[] highlights,
            PlayerPosition position)
        {
            bool isPitcher = position is PlayerPosition.StartingPitcher or PlayerPosition.ReliefPitcher;
            string[] headers = isPitcher
                ? new[] { "경기", "결과", "상대", "구분", "역할", "스코어", "이닝", "자책", "탈삼진", "볼넷", "사구" }
                : new[] { "경기", "결과", "상대", "구분", "역할", "스코어", "타수", "안타", "홈런", "타점", "볼넷", "사구" };
            float[] widths = isPitcher
                ? new[] { 72f, 58f, 125f, 70f, 105f, 90f, 70f, 65f, 65f, 65f, 70f }
                : new[] { 72f, 58f, 125f, 70f, 105f, 90f, 65f, 65f, 65f, 65f, 65f, 70f };
            float contentWidth = 0f;
            for (int index = 0; index < widths.Length; index++)
                contentWidth += widths[index];
            contentWidth = Mathf.Max(1034f, contentWidth);
            float contentHeight = 34f + highlights.Length * 43f;
            UIXScrollView scroll = UIXScrollView.Create(
                panel,
                "HighlightScroll",
                new Vector2(1034f, 382f),
                new Vector2(0f, -10f),
                new Vector2(contentWidth, Mathf.Max(382f, contentHeight)),
                horizontal: contentWidth > 1034f,
                vertical: contentHeight > 382f,
                new Color(0.006f, 0.028f, 0.05f, 0.82f),
                new Color(0.025f, 0.09f, 0.14f, 1f),
                new Color(0.18f, 0.52f, 0.76f, 1f));
            RectTransform header = scroll.CreateStickyHeader(
                contentWidth, 34f, new Color(0.006f, 0.028f, 0.05f, 1f));
            float start = 0f;
            for (int index = 0; index < headers.Length; index++)
            {
                CreateTopLeftText("Header_" + index, header, headers[index], 13, FontStyle.Bold,
                    TextAnchor.MiddleCenter, new Vector2(widths[index], 30f), start, 2f, SecondaryTextColor);
                start += widths[index];
            }

            for (int index = 0; index < highlights.Length; index++)
            {
                CareerRecordHighlightView highlight = highlights[index];
                PlayerGameLogState game = highlight.Game;
                RectTransform row = CreateTopLeftImage("Game_" + game.GameId, scroll.Content,
                    index % 2 == 0 ? new Color(0.01f, 0.042f, 0.071f, 1f) : PanelDarkColor,
                    new Vector2(contentWidth, 39f), 0f, 36f + index * 43f);
                string result = game.TeamRuns == game.OpponentRuns ? "무" : game.DidWin ? "승" : "패";
                string[] values = isPitcher
                    ? new[]
                    {
                        "#" + game.GameId,
                        result,
                        GetTeamShortName(highlight.OpponentName),
                        game.IsHome ? "홈" : "원정",
                        GetRoleLabel(game.Role, position),
                        $"{game.TeamRuns}:{game.OpponentRuns}",
                        FormatInnings(game.OutsRecorded),
                        game.EarnedRuns.ToString(),
                        game.Strikeouts.ToString(),
                        game.WalksAllowed.ToString(),
                        game.HitBatters.ToString()
                    }
                    : new[]
                    {
                        "#" + game.GameId,
                        result,
                        GetTeamShortName(highlight.OpponentName),
                        game.IsHome ? "홈" : "원정",
                        GetRoleLabel(game.Role, position),
                        $"{game.TeamRuns}:{game.OpponentRuns}",
                        game.AtBats.ToString(),
                        game.Hits.ToString(),
                        game.HomeRuns.ToString(),
                        game.RunsBattedIn.ToString(),
                        game.Walks.ToString(),
                        game.HitByPitches.ToString()
                    };
                start = 0f;
                for (int valueIndex = 0; valueIndex < values.Length; valueIndex++)
                {
                    Color color = valueIndex == 1
                        ? game.TeamRuns == game.OpponentRuns ? GoldColor : game.DidWin ? WinColor : LossColor
                        : PrimaryTextColor;
                    CreateTopLeftText("Value_" + valueIndex, row, values[valueIndex], 14,
                        valueIndex == 1 ? FontStyle.Bold : FontStyle.Normal,
                        TextAnchor.MiddleCenter, new Vector2(widths[valueIndex], 35f), start, 2f, color);
                    start += widths[valueIndex];
                }
            }
        }

        private static RectTransform CreateTopLeftImage(
            string name,
            Transform parent,
            Color color,
            Vector2 size,
            float left,
            float top)
        {
            RectTransform rect = CreateImage(name, parent, color, size, Vector2.zero);
            SetTopLeft(rect, size, left, top);
            return rect;
        }

        private static void CreateTopLeftText(
            string name,
            Transform parent,
            string value,
            int fontSize,
            FontStyle style,
            TextAnchor alignment,
            Vector2 size,
            float left,
            float top,
            Color color)
        {
            RectTransform rect = CreateText(
                name,
                parent,
                value,
                fontSize,
                style,
                alignment,
                size,
                Vector2.zero,
                color).rectTransform;
            SetTopLeft(rect, size, left, top);
        }

        private static void SetTopLeft(
            RectTransform rect,
            Vector2 size,
            float left,
            float top)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(left, -top);
        }
    }
}
