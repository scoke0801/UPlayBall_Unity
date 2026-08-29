using System;
using Baseball.Core.Teams;
using Baseball.Game.Career;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Career
{
    public sealed partial class UI_Scene_CareerSchedule
    {
        private void RenderTopBar(CareerScheduleView view)
        {
            RectTransform bar = CreateImage(
                "TopBar", _content, TopBarColor, new Vector2(1920f, 80f), new Vector2(0f, 500f));
            CreateImage("TopBarBottom", bar, BorderColor, new Vector2(1920f, 2f), new Vector2(0f, -39f));

            Text logo = CreateText(
                "Logo", bar, "UPlayBall", 34, FontStyle.BoldAndItalic, TextAnchor.MiddleLeft,
                new Vector2(310f, 50f), new Vector2(-800f, 5f), PrimaryTextColor);
            Outline outline = logo.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.05f, 0.34f, 0.62f, 0.9f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            CreateText("LogoCaption", bar, "BASEBALL CAREER", 10, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(230f, 18f), new Vector2(-796f, -23f), AccentColor);

            CreateTopBarSegment(
                bar, "LEAGUE", $"{view.SeasonYear}  {GetLeagueLabel(view.LeagueLevel)} LEAGUE",
                new Vector2(-365f, 0f), new Vector2(420f, 64f));
            CreateTopBarSegment(
                bar, "DATE", $"{view.CurrentDate:M월 d일} ({GetKoreanDay(view.CurrentDate.DayOfWeek)})",
                new Vector2(25f, 0f), new Vector2(300f, 64f));
            CreateTopBarSegment(
                bar, "SEASON", GetSeasonPhaseLabel(view.SeasonPhase),
                new Vector2(300f, 0f), new Vector2(230f, 64f));
            CreateTopBarSegment(
                bar, "MONEY", FormatMoney(view.AvailableMoney),
                new Vector2(590f, 0f), new Vector2(330f, 64f));
            CreateText("Mail", bar, "MAIL", 12, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(70f, 44f), new Vector2(805f, 0f), SecondaryTextColor);
        }

        private void RenderScreenHeader()
        {
            RectTransform header = CreateRect("ScheduleHeader", _content, new Vector2(1320f, 58f),
                new Vector2(-270f, 402f));
            CreateText("Title", header, "일정", 30, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(170f, 48f), new Vector2(-565f, 4f), PrimaryTextColor);
            CreateText("Help", header, "?", 14, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(30f, 30f), new Vector2(-484f, 4f), SecondaryTextColor);
            CreateText("Description", header, "내 구단과 리그의 시즌 일정을 확인합니다.", 14,
                FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(560f, 38f), new Vector2(-185f, 2f), SecondaryTextColor);
        }

        private void RenderViewControls(CareerScheduleView view)
        {
            RectTransform tabs = CreateImage(
                "ViewTabs", _content, PanelDarkColor, new Vector2(520f, 46f), new Vector2(-670f, 337f));
            RenderLayoutTab(tabs, ScheduleLayout.Calendar, "달력", -173f);
            RenderLayoutTab(tabs, ScheduleLayout.List, "목록", 0f);
            RenderLayoutTab(tabs, ScheduleLayout.Split, "스플릿", 173f);

            RectTransform navigation = CreateRect(
                "MonthNavigation", _content, new Vector2(510f, 48f), new Vector2(-90f, 337f));
            Button previous = CreateButton(
                "PreviousMonth", navigation, "‹", new Vector2(48f, 42f), new Vector2(-225f, 0f),
                PanelDarkColor, out Text previousText);
            previousText.fontSize = 28;
            previous.interactable = CanMoveMonth(view, -1);
            previous.onClick.AddListener(() => MoveMonth(-1));
            CreateText("Month", navigation, _visibleMonth.ToString("yyyy년 M월"), 24, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(220f, 42f), new Vector2(-90f, 0f), PrimaryTextColor);
            Button next = CreateButton(
                "NextMonth", navigation, "›", new Vector2(48f, 42f), new Vector2(45f, 0f),
                PanelDarkColor, out Text nextText);
            nextText.fontSize = 28;
            next.interactable = CanMoveMonth(view, 1);
            next.onClick.AddListener(() => MoveMonth(1));
            Button today = CreateButton(
                "Today", navigation, "오늘", new Vector2(108f, 42f), new Vector2(160f, 0f),
                new Color(0.025f, 0.11f, 0.18f, 1f), out Text todayText);
            todayText.fontSize = 15;
            today.onClick.AddListener(MoveToCurrentMonth);
        }

        private void RenderLayoutTab(Transform parent, ScheduleLayout layout, string label, float x)
        {
            bool selected = _layout == layout;
            Button button = CreateButton(
                "Tab_" + label,
                parent,
                label,
                new Vector2(170f, 42f),
                new Vector2(x, 0f),
                selected ? new Color(0.025f, 0.27f, 0.54f, 1f) : PanelDarkColor,
                out Text text);
            text.fontSize = 16;
            text.color = selected ? PrimaryTextColor : SecondaryTextColor;
            if (selected)
                CreateImage("Selected", button.transform, BrightAccentColor, new Vector2(156f, 3f), new Vector2(0f, -19f));
            button.onClick.AddListener(() => SetLayout(layout));
        }

        private void RenderTeamSummary(CareerScheduleView view, CareerScheduleMonthView month)
        {
            RectTransform panel = CreateFrame(
                "TeamSummary", _content, new Vector2(520f, 724f), new Vector2(690f, 45f), PanelDarkColor);
            Color teamColor = ToUnityColor(view.PlayerTeamColor);
            CreateTeamBadge(panel, view.PlayerTeamName, view.PlayerTeamColor, new Vector2(-185f, 305f), 72f);
            CreateText("TeamName", panel, view.PlayerTeamName, 24, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(320f, 42f), new Vector2(35f, 326f), PrimaryTextColor);
            CreateText("TeamEnglish", panel, GetShortTeamName(view.PlayerTeamName).ToUpperInvariant(), 12,
                FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(320f, 26f), new Vector2(35f, 296f), SecondaryTextColor);
            CreateText("TeamRecord", panel,
                $"{view.TeamWins}승 {view.TeamLosses}패{(view.TeamTies > 0 ? $" {view.TeamTies}무" : string.Empty)}  /  {view.TeamRank}위",
                18, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(320f, 34f), new Vector2(35f, 263f), teamColor);
            CreateImage("HeaderDivider", panel, DividerColor, new Vector2(492f, 2f), new Vector2(0f, 244f));

            RenderNextGame(panel, view);
            RenderMonthSummary(panel, month.Summary);
            RenderImportantGames(panel, view);
            RenderRecentTimeline(panel, view);
        }

        private static void RenderNextGame(Transform panel, CareerScheduleView view)
        {
            CreateText("NextLabel", panel, "다음 경기", 16, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(450f, 30f), new Vector2(0f, 218f), PrimaryTextColor);
            RectTransform next = CreateFrame(
                "NextGame", panel, new Vector2(492f, 160f), new Vector2(0f, 122f), CardColor);
            if (!view.NextGame.HasValue)
            {
                CreateText("Empty", next, "정규 시즌 일정이 종료되었습니다.", 16, FontStyle.Bold,
                    TextAnchor.MiddleCenter, new Vector2(430f, 80f), Vector2.zero, MutedColor);
                return;
            }

            CareerScheduleGameView game = view.NextGame.Value;
            CreateTeamBadge(next, game.AwayTeamName, game.AwayTeamColor, new Vector2(-150f, 26f), 52f);
            CreateTeamBadge(next, game.HomeTeamName, game.HomeTeamColor, new Vector2(150f, 26f), 52f);
            CreateText("Away", next, GetShortTeamName(game.AwayTeamName), 13, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(165f, 30f), new Vector2(-150f, -27f), PrimaryTextColor);
            CreateText("Versus", next, "VS", 26, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(90f, 44f), new Vector2(0f, 24f), PrimaryTextColor);
            CreateText("Home", next, GetShortTeamName(game.HomeTeamName), 13, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(165f, 30f), new Vector2(150f, -27f), PrimaryTextColor);
            CreateText("Date", next,
                $"{game.Date:M월 d일} ({GetKoreanDay(game.Date.DayOfWeek)})  ·  {(game.IsPlayerHome ? "HOME" : "AWAY")}",
                13, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(430f, 28f), new Vector2(0f, -61f), SecondaryTextColor);
        }

        private static void RenderMonthSummary(Transform panel, CareerScheduleMonthSummaryView summary)
        {
            CreateText("MonthLabel", panel, "이번 달 요약", 15, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(450f, 28f), new Vector2(0f, 25f), PrimaryTextColor);
            RectTransform card = CreateFrame(
                "MonthSummary", panel, new Vector2(492f, 76f), new Vector2(0f, -28f), CardColor);
            RenderSummaryMetric(card, "경기", summary.CompletedGames.ToString(), -160f, PrimaryTextColor);
            RenderSummaryMetric(card, "승 / 패", $"{summary.Wins} / {summary.Losses}", 0f, PrimaryTextColor);
            RenderSummaryMetric(card, "승률", summary.WinningPercentage.ToString(".000"), 160f, WinColor);
        }

        private static void RenderSummaryMetric(
            Transform parent,
            string label,
            string value,
            float x,
            Color valueColor)
        {
            CreateText("Label_" + label, parent, label, 10, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(130f, 20f), new Vector2(x, 20f), SecondaryTextColor);
            CreateText("Value_" + label, parent, value, 20, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(145f, 34f), new Vector2(x, -13f), valueColor);
        }

        private static void RenderImportantGames(Transform panel, CareerScheduleView view)
        {
            CreateText("ImportantLabel", panel, "중요 일정", 15, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(450f, 28f), new Vector2(0f, -83f), PrimaryTextColor);
            RectTransform card = CreateFrame(
                "ImportantGames", panel, new Vector2(492f, 104f), new Vector2(0f, -148f), CardColor);
            int count = Math.Min(view.UpcomingGames.Count, 3);
            if (count == 0)
            {
                CreateText("Empty", card, "남은 정규 시즌 경기가 없습니다.", 13, FontStyle.Normal,
                    TextAnchor.MiddleCenter, new Vector2(430f, 50f), Vector2.zero, MutedColor);
                return;
            }
            for (int index = 0; index < count; index++)
            {
                CareerScheduleGameView game = view.UpcomingGames[index];
                float y = 31f - index * 31f;
                CreateText("Date_" + index, card, game.Date.ToString("M.dd"), 12, FontStyle.Bold,
                    TextAnchor.MiddleLeft, new Vector2(74f, 24f), new Vector2(-195f, y), SecondaryTextColor);
                CreateText("Opponent_" + index,
                    card, $"{(game.IsPlayerHome ? "vs" : "@")} {game.OpponentName}", 12, FontStyle.Normal,
                    TextAnchor.MiddleLeft, new Vector2(300f, 24f), new Vector2(25f, y), PrimaryTextColor);
            }
        }

        private static void RenderRecentTimeline(Transform panel, CareerScheduleView view)
        {
            CreateText("TimelineLabel", panel, "최근 / 예정 일정", 15, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(450f, 28f), new Vector2(0f, -220f), PrimaryTextColor);
            RectTransform card = CreateFrame(
                "RecentTimeline", panel, new Vector2(492f, 118f), new Vector2(0f, -291f), CardColor);
            int recentCount = Math.Min(view.RecentGames.Count, 3);
            int row = 0;
            for (int index = 0; index < recentCount; index++)
            {
                RenderTimelineRow(card, view.RecentGames[index], row++, true);
            }
            if (view.NextGame.HasValue && row < 4)
                RenderTimelineRow(card, view.NextGame.Value, row, false);
        }

        private static void RenderTimelineRow(
            Transform parent,
            CareerScheduleGameView game,
            int row,
            bool isRecent)
        {
            float y = 42f - row * 28f;
            CreateText("Date_" + row, parent, game.Date.ToString("M.dd"), 11, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Vector2(68f, 22f), new Vector2(-198f, y), SecondaryTextColor);
            CreateText("Opponent_" + row,
                parent, $"{(game.IsPlayerHome ? "vs" : "@")} {GetShortTeamName(game.OpponentName)}", 11,
                FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(260f, 22f), new Vector2(-20f, y), PrimaryTextColor);
            string result = isRecent
                ? $"{GetOutcomeCode(game.Outcome)} {game.PlayerTeamRuns}:{game.OpponentRuns}"
                : "예정";
            CreateText("Result_" + row, parent, result, 11, FontStyle.Bold,
                TextAnchor.MiddleRight, new Vector2(90f, 22f), new Vector2(185f, y),
                isRecent ? GetOutcomeColor(game.Outcome) : SecondaryTextColor);
        }

        private void RenderScopeAndLegend(CareerScheduleView view)
        {
            RectTransform bar = CreateRect(
                "ScheduleFilters", _content, new Vector2(1320f, 48f), new Vector2(-270f, -327f));
            RenderScopeButton(bar, CareerScheduleScope.MyTeam, "내 구단", -565f);
            RenderScopeButton(bar, CareerScheduleScope.EntireLeague, "리그 전체", -430f);

            CreateLegend(bar, AccentColor, "홈 경기", -220f);
            CreateLegend(bar, GoldColor, "원정 경기", -105f);
            CreateLegend(bar, MutedColor, "◇ 휴식일", 20f);

            Button reset = CreateButton(
                "ResetFilters", bar, "필터 초기화", new Vector2(136f, 40f), new Vector2(555f, 0f),
                PanelDarkColor, out Text resetText);
            resetText.fontSize = 14;
            reset.onClick.AddListener(ResetFilters);
        }

        private void RenderScopeButton(
            Transform parent,
            CareerScheduleScope scope,
            string label,
            float x)
        {
            bool selected = _scope == scope;
            Button button = CreateButton(
                "Scope_" + scope, parent, label, new Vector2(128f, 40f), new Vector2(x, 0f),
                selected ? new Color(0.025f, 0.27f, 0.54f, 1f) : PanelDarkColor,
                out Text text);
            text.fontSize = 14;
            text.color = selected ? PrimaryTextColor : SecondaryTextColor;
            button.onClick.AddListener(() => SetScope(scope));
        }

        private static void CreateLegend(Transform parent, Color color, string label, float x)
        {
            CreateImage("Swatch_" + label, parent, color, new Vector2(12f, 12f), new Vector2(x - 36f, 0f));
            CreateText("Legend_" + label, parent, label, 12, FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(100f, 28f), new Vector2(x + 22f, 0f), SecondaryTextColor);
        }

        private bool CanMoveMonth(CareerScheduleView view, int offset)
        {
            DateTime target = _visibleMonth.AddMonths(offset);
            DateTime first = new(view.SeasonStartDate.Year, view.SeasonStartDate.Month, 1);
            DateTime last = new(view.SeasonEndDate.Year, view.SeasonEndDate.Month, 1);
            return target >= first && target <= last;
        }

        private static void CreateTopBarSegment(
            Transform parent,
            string eyebrow,
            string value,
            Vector2 position,
            Vector2 size)
        {
            RectTransform segment = CreateImage(
                eyebrow + "Segment", parent, new Color(0.02f, 0.07f, 0.12f, 0.76f), size, position);
            CreateImage("LeftDivider", segment, DividerColor, new Vector2(2f, size.y - 10f),
                new Vector2(-size.x * 0.5f + 1f, 0f));
            CreateText("Eyebrow", segment, eyebrow, 9, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(size.x - 42f, 18f), new Vector2(14f, 15f), AccentColor);
            CreateText("Value", segment, value, 18, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(size.x - 42f, 31f), new Vector2(14f, -8f), PrimaryTextColor);
        }

        private static RectTransform CreateTeamBadge(
            Transform parent,
            string teamName,
            TeamColor teamColor,
            Vector2 position,
            float size)
        {
            Color primary = ToUnityColor(teamColor);
            RectTransform outer = CreateImage(
                "TeamBadge_" + teamName, parent, Color.Lerp(primary, Color.white, 0.30f),
                new Vector2(size, size), position);
            RectTransform inner = CreateImage(
                "Inner", outer, Color.Lerp(primary, BackgroundColor, 0.28f),
                new Vector2(size - 4f, size - 4f), Vector2.zero);
            CreateText("Monogram", inner, CareerTeamNameFormatter.GetMonogram(teamName), Math.Max(9, (int)(size * 0.30f)),
                FontStyle.Bold, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, PrimaryTextColor, true);
            return outer;
        }

        private static Color ToUnityColor(TeamColor color)
        {
            return new Color(color.Red / 255f, color.Green / 255f, color.Blue / 255f, 1f);
        }

        private static string GetShortTeamName(string teamName)
        {
            if (string.IsNullOrWhiteSpace(teamName))
                return "-";
            int separator = teamName.LastIndexOf(' ');
            return separator >= 0 && separator < teamName.Length - 1
                ? teamName.Substring(separator + 1)
                : teamName;
        }

        private static string GetLeagueLabel(LeagueLevel level)
        {
            return WorldGenerationConfiguration.GetDefaultDefinition(level).UiDisplayName;
        }

        private static string GetSeasonPhaseLabel(SeasonPhase phase)
        {
            return phase switch
            {
                SeasonPhase.Preseason => "프리시즌",
                SeasonPhase.RegularSeason => "정규 시즌",
                SeasonPhase.Postseason => "포스트시즌",
                SeasonPhase.SeasonReview => "시즌 결산",
                SeasonPhase.Offseason => "오프시즌",
                SeasonPhase.Completed => "시즌 완료",
                _ => "시즌"
            };
        }

        private static string GetKoreanDay(DayOfWeek day)
        {
            return day switch
            {
                DayOfWeek.Monday => "월",
                DayOfWeek.Tuesday => "화",
                DayOfWeek.Wednesday => "수",
                DayOfWeek.Thursday => "목",
                DayOfWeek.Friday => "금",
                DayOfWeek.Saturday => "토",
                _ => "일"
            };
        }

        private static string FormatMoney(long money)
        {
            if (money >= 100_000_000L)
                return $"{money / 100_000_000d:0.#}억원";
            return $"{money / 10_000L:N0}만원";
        }
    }
}
