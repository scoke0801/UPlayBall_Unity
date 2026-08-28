using System;
using System.Collections.Generic;
using Baseball.Game.Career;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Career
{
    public sealed partial class UI_Scene_CareerSchedule
    {
        private static readonly string[] WeekdayLabels = { "일", "월", "화", "수", "목", "금", "토" };

        private void RenderCalendar(CareerScheduleMonthView month)
        {
            RectTransform panel = CreateFrame(
                "Calendar", _content, new Vector2(1320f, 570f), new Vector2(-270f, -4f), PanelDarkColor);
            const float cellWidth = 185f;
            const float cellHeight = 81f;
            const float startX = -555f;

            RectTransform weekdayHeader = CreateImage(
                "WeekdayHeader", panel, new Color(0.025f, 0.09f, 0.145f, 1f),
                new Vector2(1300f, 38f), new Vector2(0f, 257f));
            for (int column = 0; column < WeekdayLabels.Length; column++)
            {
                Color color = column == 0 ? LossColor : column == 6 ? BrightAccentColor : SecondaryTextColor;
                CreateText(
                    "Weekday_" + column,
                    weekdayHeader,
                    WeekdayLabels[column],
                    14,
                    FontStyle.Bold,
                    TextAnchor.MiddleCenter,
                    new Vector2(cellWidth, 34f),
                    new Vector2(startX + column * cellWidth, 0f),
                    color);
            }

            for (int index = 0; index < month.Days.Count; index++)
            {
                int row = index / 7;
                int column = index % 7;
                RenderCalendarCell(
                    panel,
                    month.Days[index],
                    index,
                    new Vector2(startX + column * cellWidth, 195f - row * cellHeight));
            }
        }

        private void RenderCalendarCell(
            Transform parent,
            CareerScheduleDayView day,
            int index,
            Vector2 position)
        {
            Color background = day.IsCurrentDate
                ? new Color(0.035f, 0.19f, 0.34f, 1f)
                : day.IsVisibleMonth ? new Color(0.012f, 0.047f, 0.078f, 1f) : new Color(0.007f, 0.027f, 0.045f, 1f);
            RectTransform cell = CreateImage(
                "Day_" + day.Date.ToString("yyyyMMdd"), parent, background,
                new Vector2(181f, 77f), position);
            if (day.IsCurrentDate)
            {
                CreateImage("CurrentTop", cell, BrightAccentColor, new Vector2(177f, 3f), new Vector2(0f, 36f));
                CreateImage("CurrentLeft", cell, BrightAccentColor, new Vector2(3f, 73f), new Vector2(-89f, 0f));
            }

            int weekday = index % 7;
            Color dayColor = !day.IsVisibleMonth
                ? MutedColor
                : weekday == 0 ? LossColor : weekday == 6 ? BrightAccentColor : PrimaryTextColor;
            CreateText(
                "Number", cell, day.Date.Day.ToString(), 15, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(42f, 22f), new Vector2(-66f, 25f), dayColor);

            if (!day.IsVisibleMonth)
                return;
            if (day.Games.Count == 0)
            {
                if (day.IsRestDay)
                {
                    CreateText("RestIcon", cell, "◇", 19, FontStyle.Bold, TextAnchor.MiddleCenter,
                        new Vector2(38f, 26f), new Vector2(0f, 4f), MutedColor);
                    CreateText("Rest", cell, "휴식일", 12, FontStyle.Normal, TextAnchor.MiddleCenter,
                        new Vector2(90f, 22f), new Vector2(0f, -22f), MutedColor);
                }
                return;
            }

            if (_scope == CareerScheduleScope.EntireLeague)
            {
                int completed = 0;
                for (int gameIndex = 0; gameIndex < day.Games.Count; gameIndex++)
                    completed += day.Games[gameIndex].IsCompleted ? 1 : 0;
                CreateText("LeagueGames", cell, $"리그 {day.Games.Count}경기", 13, FontStyle.Bold,
                    TextAnchor.MiddleCenter, new Vector2(150f, 24f), new Vector2(0f, 5f), PrimaryTextColor);
                string status = completed == day.Games.Count ? "전체 종료" : $"{day.Games.Count - completed}경기 예정";
                CreateText("LeagueStatus", cell, status, 11, FontStyle.Normal,
                    TextAnchor.MiddleCenter, new Vector2(150f, 22f), new Vector2(0f, -21f),
                    completed == day.Games.Count ? WinColor : SecondaryTextColor);
                return;
            }

            CareerScheduleGameView game = day.Games[0];
            CreateTeamBadge(cell, game.OpponentName, game.OpponentColor, new Vector2(-62f, -4f), 33f);
            string venue = game.IsPlayerHome ? "VS" : "@";
            CreateText("Venue", cell, venue, 11, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(26f, 22f), new Vector2(-34f, 5f),
                game.IsPlayerHome ? AccentColor : GoldColor);
            CreateText("Opponent", cell, GetShortTeamName(game.OpponentName), 12, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Vector2(92f, 24f), new Vector2(29f, 5f), PrimaryTextColor);
            string result = game.IsCompleted
                ? $"{GetOutcomeCode(game.Outcome)}  {game.PlayerTeamRuns} : {game.OpponentRuns}"
                : "경기 예정";
            CreateText("Result", cell, result, 12, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(136f, 22f), new Vector2(15f, -22f),
                game.IsCompleted ? GetOutcomeColor(game.Outcome) : SecondaryTextColor);
        }

        private void RenderList(CareerScheduleMonthView month)
        {
            RectTransform panel = CreateFrame(
                "ScheduleList", _content, new Vector2(1320f, 570f), new Vector2(-270f, -4f), PanelDarkColor);
            CreateText("Title", panel,
                _scope == CareerScheduleScope.MyTeam ? "내 구단 월간 일정" : "리그 일자별 일정",
                18, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(600f, 34f), new Vector2(-340f, 252f), PrimaryTextColor);
            CreateText("Description", panel,
                _scope == CareerScheduleScope.MyTeam
                    ? "완료 결과와 앞으로의 대진을 한 흐름으로 확인합니다."
                    : "한 날짜의 전체 리그 경기를 묶어 표시합니다.",
                12, FontStyle.Normal, TextAnchor.MiddleRight,
                new Vector2(620f, 30f), new Vector2(325f, 252f), SecondaryTextColor);
            CreateImage("HeaderLine", panel, DividerColor, new Vector2(1288f, 2f), new Vector2(0f, 230f));

            if (month.DisplayedGames.Count == 0)
            {
                CreateText("Empty", panel, "이 달에는 표시할 경기가 없습니다.", 18, FontStyle.Bold,
                    TextAnchor.MiddleCenter, new Vector2(800f, 80f), Vector2.zero, MutedColor);
                return;
            }

            if (_scope == CareerScheduleScope.MyTeam)
                RenderMyTeamList(panel, month.DisplayedGames);
            else
                RenderLeagueDateList(panel, month.Days);
        }

        private static void RenderMyTeamList(
            Transform panel,
            IReadOnlyList<CareerScheduleGameView> games)
        {
            int visible = Math.Min(games.Count, 30);
            for (int index = 0; index < visible; index++)
            {
                int column = index / 15;
                int row = index % 15;
                float x = column == 0 ? -328f : 328f;
                float y = 207f - row * 31f;
                RenderMyTeamListRow(panel, games[index], index, new Vector2(x, y));
            }
        }

        private static void RenderMyTeamListRow(
            Transform parent,
            CareerScheduleGameView game,
            int index,
            Vector2 position)
        {
            Color rowColor = index % 2 == 0 ? new Color(0.018f, 0.064f, 0.103f, 1f) : PanelDarkColor;
            RectTransform row = CreateImage("Game_" + game.GameId, parent, rowColor,
                new Vector2(636f, 28f), position);
            CreateText("Date", row, game.Date.ToString("MM.dd"), 12, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(60f, 24f), new Vector2(-278f, 0f), SecondaryTextColor);
            CreateText("Venue", row, game.IsPlayerHome ? "HOME" : "AWAY", 10, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(54f, 22f), new Vector2(-217f, 0f),
                game.IsPlayerHome ? AccentColor : GoldColor);
            CreateText("Opponent", row, game.OpponentName, 12, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(250f, 24f), new Vector2(-45f, 0f), PrimaryTextColor);
            string value = game.IsCompleted
                ? $"{GetOutcomeCode(game.Outcome)}  {game.PlayerTeamRuns}:{game.OpponentRuns}"
                : "예정";
            CreateText("Result", row, value, 12, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(84f, 24f), new Vector2(264f, 0f),
                game.IsCompleted ? GetOutcomeColor(game.Outcome) : SecondaryTextColor);
        }

        private static void RenderLeagueDateList(
            Transform panel,
            IReadOnlyList<CareerScheduleDayView> days)
        {
            int resultIndex = 0;
            for (int dayIndex = 0; dayIndex < days.Count && resultIndex < 30; dayIndex++)
            {
                CareerScheduleDayView day = days[dayIndex];
                if (!day.IsVisibleMonth || day.Games.Count == 0)
                    continue;
                int column = resultIndex / 15;
                int rowIndex = resultIndex % 15;
                float x = column == 0 ? -328f : 328f;
                float y = 207f - rowIndex * 31f;
                int completed = 0;
                for (int gameIndex = 0; gameIndex < day.Games.Count; gameIndex++)
                    completed += day.Games[gameIndex].IsCompleted ? 1 : 0;
                RectTransform row = CreateImage("LeagueDay_" + day.Date.ToString("MMdd"), panel,
                    resultIndex % 2 == 0 ? new Color(0.018f, 0.064f, 0.103f, 1f) : PanelDarkColor,
                    new Vector2(636f, 28f), new Vector2(x, y));
                CreateText("Date", row, day.Date.ToString("MM.dd (ddd)"), 12, FontStyle.Bold,
                    TextAnchor.MiddleLeft, new Vector2(150f, 24f), new Vector2(-228f, 0f), PrimaryTextColor);
                CreateText("Count", row, $"{day.Games.Count}경기", 12, FontStyle.Bold,
                    TextAnchor.MiddleCenter, new Vector2(100f, 24f), new Vector2(5f, 0f), AccentColor);
                CreateText("Status", row,
                    completed == day.Games.Count ? "전체 종료" : $"예정 {day.Games.Count - completed}",
                    12, FontStyle.Bold, TextAnchor.MiddleRight,
                    new Vector2(150f, 24f), new Vector2(224f, 0f),
                    completed == day.Games.Count ? WinColor : SecondaryTextColor);
                resultIndex++;
            }
        }

        private void RenderSplit(CareerScheduleMonthView month)
        {
            RectTransform panel = CreateFrame(
                "ScheduleSplit", _content, new Vector2(1320f, 570f), new Vector2(-270f, -4f), PanelDarkColor);
            CreateText("Title", panel, "월간 홈 / 원정 스플릿", 18, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(600f, 34f), new Vector2(-340f, 250f), PrimaryTextColor);
            CreateText("Description", panel, "완료된 내 구단 경기만 사용합니다.", 12, FontStyle.Normal,
                TextAnchor.MiddleRight, new Vector2(500f, 30f), new Vector2(380f, 250f), SecondaryTextColor);

            RenderSplitCard(panel, "HomeSplit", "HOME", month.Summary.HomeGames,
                month.Summary.HomeWins, month.Summary.HomeWinningPercentage,
                new Vector2(-320f, 88f), AccentColor);
            RenderSplitCard(panel, "AwaySplit", "AWAY", month.Summary.AwayGames,
                month.Summary.AwayWins, month.Summary.AwayWinningPercentage,
                new Vector2(320f, 88f), GoldColor);

            RectTransform total = CreateFrame(
                "MonthTotal", panel, new Vector2(1240f, 112f), new Vector2(0f, -175f), CardColor);
            CreateText("Label", total, "MONTH TOTAL", 11, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(180f, 24f), new Vector2(-500f, 28f), AccentColor);
            CreateText("Games", total, $"{month.Summary.CompletedGames} 경기", 25, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(190f, 45f), new Vector2(-330f, -8f), PrimaryTextColor);
            CreateText("Record", total,
                $"{month.Summary.Wins}승  {month.Summary.Losses}패  {month.Summary.Ties}무",
                25, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(360f, 45f), new Vector2(0f, -8f), PrimaryTextColor);
            CreateText("Rate", total, month.Summary.WinningPercentage.ToString(".000"), 30, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(190f, 50f), new Vector2(360f, -6f), WinColor);
        }

        private static void RenderSplitCard(
            Transform parent,
            string name,
            string label,
            int games,
            int wins,
            double winningPercentage,
            Vector2 position,
            Color accent)
        {
            RectTransform card = CreateFrame(name, parent, new Vector2(590f, 250f), position, CardColor);
            CreateImage("Accent", card, accent, new Vector2(8f, 220f), new Vector2(-284f, 0f));
            CreateText("Label", card, label, 18, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(180f, 36f), new Vector2(-180f, 82f), accent);
            CreateText("Games", card, $"{games} 경기", 42, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(230f, 64f), new Vector2(-135f, 8f), PrimaryTextColor);
            CreateText("Wins", card, $"{wins}승", 24, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(150f, 50f), new Vector2(90f, 10f), SecondaryTextColor);
            CreateText("RateLabel", card, "승률", 11, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(100f, 24f), new Vector2(120f, -62f), MutedColor);
            CreateText("Rate", card, winningPercentage.ToString(".000"), 28, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(180f, 46f), new Vector2(120f, -91f), accent);
        }

        private static string GetOutcomeCode(CareerScheduleOutcome outcome)
        {
            return outcome switch
            {
                CareerScheduleOutcome.Win => "W",
                CareerScheduleOutcome.Loss => "L",
                CareerScheduleOutcome.Tie => "T",
                _ => "-"
            };
        }

        private static Color GetOutcomeColor(CareerScheduleOutcome outcome)
        {
            return outcome switch
            {
                CareerScheduleOutcome.Win => WinColor,
                CareerScheduleOutcome.Loss => LossColor,
                CareerScheduleOutcome.Tie => TieColor,
                _ => SecondaryTextColor
            };
        }
    }
}
