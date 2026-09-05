using System;
using Baseball.Core.Players;
using Baseball.Game.Career;
using Baseball.Presentation.SharedScreens;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Career
{
    public sealed partial class UI_Scene_CareerRecords
    {
        private static void RenderScrollableLeaderboardTable(
            Transform panel,
            CareerRecordsView view)
        {
            RecordsScreenSnapshot snapshot = CareerRecordsSnapshotAdapter.Create(view);
            RecordTableView table = RecordTableView.CreateRuntime(
                panel,
                new Vector2(1034f, 370f),
                new Vector2(0f, -8f),
                "LeaderboardTable");
            UiContentStateModel state = view.Leaderboard.Length > 0
                ? UiContentStateModel.Ready
                : UiContentStateModel.CreateEmpty(
                    "기록 없음",
                    !view.HasScopeData
                        ? "선택한 경기 범위에 아직 기록이 없습니다."
                        : view.Category == CareerRecordCategory.Baserunning
                            ? "아직 도루 시도가 없어 주루 순위가 생성되지 않았습니다."
                            : "현재 규정 자격을 충족한 선수가 없습니다.");
            table.Bind(snapshot.Table, state, snapshot.FocusedRowId);
        }

        private static void RenderScrollableSeasonTable(
            Transform panel,
            CareerRecordsView view,
            CareerRecordSeasonRow[] seasons,
            bool showRank)
        {
            RecordTableModel model = CareerRecordsSnapshotAdapter.CreateSeasonTable(view, seasons, showRank);
            RecordTableView table = RecordTableView.CreateRuntime(
                panel,
                new Vector2(1034f, 370f),
                new Vector2(0f, -8f),
                "SeasonTable");
            string focusedRowId = string.Empty;
            for (int index = 0; index < seasons.Length; index++)
            {
                if (seasons[index].IsCurrent)
                {
                    focusedRowId = "season-" + seasons[index].Year;
                    break;
                }
            }
            UiContentStateModel state = seasons.Length > 0
                ? UiContentStateModel.Ready
                : UiContentStateModel.CreateEmpty("시즌 기록 없음", "선택한 경기 범위의 시즌 기록이 아직 없습니다.");
            table.Bind(model, state, focusedRowId);
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
                WithAlpha(CareerUiTheme.PanelDark, 0.5f),
                CareerUiTheme.Surface,
                CareerUiTheme.Primary);

            for (int index = 0; index < metrics.Length; index++)
            {
                int column = index % 2;
                int rowIndex = index / 2;
                CareerRecordMetricValue metric = metrics[index];
                RectTransform cell = CreateTopLeftImage(
                    "Metric_" + metric.Metric,
                    scroll.Content,
                    rowIndex % 2 == 0 ? CareerUiTheme.SurfaceSubtle : PanelDarkColor,
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
                WithAlpha(CareerUiTheme.PanelDark, 0.5f),
                CareerUiTheme.Surface,
                CareerUiTheme.Primary);

            int rowIndex = 0;
            for (int index = 0; index < view.TradeHistory.Length; index++)
            {
                CareerTradeHistoryView trade = view.TradeHistory[index];
                RectTransform row = CreateTopLeftImage(
                    "Trade_" + index,
                    scroll.Content,
                    CareerUiTheme.SurfaceSelected,
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
                    rowIndex % 2 == 0 ? CareerUiTheme.SurfaceSubtle : PanelDarkColor,
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
                WithAlpha(CareerUiTheme.PanelDark, 0.82f),
                CareerUiTheme.Surface,
                CareerUiTheme.Primary);
            RectTransform header = scroll.CreateStickyHeader(
                1021f, headerHeight, CareerUiTheme.PanelDark);
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
                    index % 2 == 0 ? CareerUiTheme.SurfaceSubtle : PanelDarkColor,
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
                    colors.highlightedColor = CareerUiTheme.SurfaceSelected;
                    colors.pressedColor = CareerUiTheme.PrimaryAction;
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
                WithAlpha(CareerUiTheme.PanelDark, 0.82f),
                CareerUiTheme.Surface,
                CareerUiTheme.Primary);
            RectTransform header = scroll.CreateStickyHeader(
                contentWidth, 34f, CareerUiTheme.PanelDark);
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
                    index % 2 == 0 ? CareerUiTheme.SurfaceSubtle : PanelDarkColor,
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

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
