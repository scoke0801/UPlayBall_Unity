using System;
using System.Collections.Generic;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Career;
using Baseball.Game.Manager;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Career
{
    /// <summary>리그 순위와 타자·투수 기록 경쟁을 한 화면에서 비교하는 읽기 전용 리그 화면이다.</summary>
    public sealed class UI_Scene_League : UISceneBase, ICareerTabScreen
    {
        private static readonly Color BackgroundColor = new(0.004f, 0.015f, 0.028f, 1f);
        private static readonly Color TopBarColor = new(0.008f, 0.027f, 0.052f, 1f);
        private static readonly Color PanelColor = new(0.012f, 0.047f, 0.079f, 0.99f);
        private static readonly Color PanelDarkColor = new(0.006f, 0.028f, 0.049f, 1f);
        private static readonly Color RowColor = new(0.015f, 0.055f, 0.088f, 0.96f);
        private static readonly Color BorderColor = new(0.28f, 0.46f, 0.62f, 1f);
        private static readonly Color DividerColor = new(0.11f, 0.27f, 0.40f, 1f);
        private static readonly Color AccentColor = new(0.08f, 0.52f, 0.92f, 1f);
        private static readonly Color BrightAccentColor = new(0.12f, 0.68f, 1f, 1f);
        private static readonly Color GoldColor = new(1f, 0.78f, 0.12f, 1f);
        private static readonly Color WinColor = new(0.20f, 0.76f, 0.45f, 1f);
        private static readonly Color LossColor = new(0.88f, 0.29f, 0.34f, 1f);
        private static readonly Color TieColor = new(0.66f, 0.70f, 0.76f, 1f);
        private static readonly Color PrimaryTextColor = new(0.94f, 0.97f, 1f, 1f);
        private static readonly Color SecondaryTextColor = new(0.62f, 0.71f, 0.80f, 1f);
        private static readonly Color MutedColor = new(0.34f, 0.42f, 0.50f, 1f);

        private CareerManager _manager;
        private RectTransform _content;
        private LeagueBattingCategory _battingCategory = LeagueBattingCategory.BattingAverage;
        private LeaguePitchingCategory _pitchingCategory = LeaguePitchingCategory.EarnedRunAverage;

        public override bool BlocksLowerInput => true;
        public CareerMainTab MainTab => CareerMainTab.League;

        /// <summary>프리팹이 없는 프로토타입 환경에서 리그 화면을 런타임 생성한다.</summary>
        public static UI_Scene_League CreateRuntime(Transform parent)
        {
            var screenObject = new GameObject(nameof(UI_Scene_League), typeof(RectTransform), typeof(CanvasGroup));
            screenObject.transform.SetParent(parent, false);
            UI_Scene_League screen = screenObject.AddComponent<UI_Scene_League>();
            Stretch(screenObject.GetComponent<RectTransform>());
            return screen;
        }

        protected override void OnInitialize()
        {
            _manager = GameManager.EnsureExists().EnsureManager<CareerManager>("CareerManager");
            _manager.CareerChanged += HandleCareerChanged;
            BuildHierarchy();
        }

        protected override void OnShow()
        {
            Render();
        }

        protected override void OnDestroy()
        {
            if (_manager != null)
                _manager.CareerChanged -= HandleCareerChanged;
            base.OnDestroy();
        }

        private void BuildHierarchy()
        {
            RectTransform root = (RectTransform)transform;
            Stretch(root);
            CreateImage("Background", root, BackgroundColor, Vector2.zero, Vector2.zero, stretch: true);
            _content = CreateRect("Content", root, new Vector2(1920f, 1080f), Vector2.zero);
        }

        private void HandleCareerChanged()
        {
            if (!_manager.HasActiveCareer)
            {
                Hide();
                return;
            }
            if (IsVisible)
                Render();
        }

        private void Render()
        {
            if (_content == null || _manager == null || !_manager.HasActiveCareer)
                return;

            ClearChildren(_content);
            LeagueHubView view = _manager.LeagueHub;
            RenderBackgroundAccents();
            RenderTopBar(view);
            RenderStandings(view);
            RenderBattingLeaders(view);
            RenderPitchingLeaders(view);
            RenderTeamMetrics(view);
            RenderLeagueFocus(view);
            RenderSchedule(view);
            CareerTabBar.Create(_content, CareerMainTab.League);
        }

        private void RenderBackgroundAccents()
        {
            CreateImage("TopGlow", _content, new Color(0.02f, 0.19f, 0.33f, 0.26f),
                new Vector2(1920f, 5f), new Vector2(0f, 456f));
            CreateImage("BottomGlow", _content, new Color(0.02f, 0.16f, 0.28f, 0.20f),
                new Vector2(1920f, 4f), new Vector2(0f, -443f));
        }

        private void RenderTopBar(LeagueHubView view)
        {
            RectTransform bar = CreateImage(
                "TopBar", _content, TopBarColor, new Vector2(1920f, 80f), new Vector2(0f, 500f));
            CreateImage("TopBarBottom", bar, BorderColor, new Vector2(1920f, 2f), new Vector2(0f, -39f));

            Text logo = CreateText(
                "Logo", bar, "UPlayBall", 34, FontStyle.BoldAndItalic, TextAnchor.MiddleLeft,
                new Vector2(310f, 50f), new Vector2(-800f, 5f), PrimaryTextColor);
            AddTextOutline(logo, new Color(0.05f, 0.34f, 0.62f, 0.9f), 1.5f);
            CreateText(
                "LogoCaption", bar, "ULTIMATE BASEBALL", 10, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(230f, 18f), new Vector2(-796f, -23f), AccentColor);

            CreateTopBarSegment(
                bar, "LEAGUE", $"{view.SeasonYear} {GetLeagueLabel(view.LeagueLevel)} LEAGUE",
                new Vector2(-360f, 0f), new Vector2(430f, 64f));
            CreateTopBarSegment(
                bar, "DATE", $"{view.CurrentDate:M월 d일} ({GetDayLabel(view.CurrentDate.DayOfWeek)})",
                new Vector2(45f, 0f), new Vector2(330f, 64f));
            CreateTopBarSegment(
                bar, "SEASON PROGRESS",
                $"{view.GamesPlayedPerTeam}/{view.RegularSeasonGamesPerTeam} 경기",
                new Vector2(395f, 0f), new Vector2(320f, 64f));
            CreateTeamBadge(bar, view.MyTeamName, GetTeamColor(view, view.MyTeamId), new Vector2(780f, 0f), 52f);
            CreateText(
                "MyTeam", bar, view.MyTeamName, 15, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(145f, 42f), new Vector2(885f, 0f), GoldColor);
        }

        private void RenderStandings(LeagueHubView view)
        {
            RectTransform panel = CreatePanel(
                "Standings", "LEAGUE STANDINGS", "리그 순위", new Vector2(650f, 480f),
                new Vector2(-635f, 210f));
            CreateTableHeader(panel,
                new[] { "순위", "팀", "경기", "승", "패", "승률", "게임차", "최근" },
                new[] { -292f, -207f, 17f, 74f, 121f, 179f, 244f, 297f },
                new[] { 45f, 180f, 54f, 42f, 42f, 60f, 60f, 54f },
                166f);

            for (int index = 0; index < view.Standings.Count; index++)
                RenderStandingRow(panel, view.Standings[index], 126f - index * 39f);

            RectTransform legend = CreateImage(
                "PostseasonLegend", panel, PanelDarkColor, new Vector2(620f, 32f), new Vector2(0f, -213f));
            CreateImage("PostseasonColor", legend, AccentColor, new Vector2(24f, 6f), new Vector2(-268f, 0f));
            CreateText(
                "Legend", legend, $"1~{view.PlayoffTeamCount}위 포스트시즌 진출권",
                12, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(230f, 26f), new Vector2(-130f, 0f), SecondaryTextColor);
            CreateText(
                "MyTeamLegend", legend, "내 구단", 12, FontStyle.Bold, TextAnchor.MiddleRight,
                new Vector2(100f, 26f), new Vector2(256f, 0f), GoldColor);
        }

        private void RenderStandingRow(RectTransform panel, LeagueStandingView row, float y)
        {
            Color background = row.IsMyTeam
                ? new Color(0.025f, 0.18f, 0.30f, 1f)
                : RowColor;
            RectTransform line = CreateImage(
                "Standing_" + row.TeamId, panel, background, new Vector2(620f, 36f), new Vector2(0f, y));
            if (row.IsPostseasonPosition)
                CreateImage("PostseasonBand", line, AccentColor, new Vector2(4f, 32f), new Vector2(-307f, 0f));
            if (row.IsMyTeam)
                CreateImage("MyTeamBand", line, GoldColor, new Vector2(4f, 32f), new Vector2(307f, 0f));

            Color valueColor = row.IsMyTeam ? GoldColor : PrimaryTextColor;
            CreateText("Rank", line, row.Rank.ToString(), 17, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(42f, 32f), new Vector2(-290f, 0f), valueColor);
            CreateTeamBadge(line, row.TeamName, row.TeamColor, new Vector2(-252f, 0f), 27f);
            CreateText("Team", line, row.TeamName, 15, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(160f, 32f), new Vector2(-155f, 0f), valueColor);
            CreateText("Games", line, row.GamesPlayed.ToString(), 14, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(50f, 32f), new Vector2(18f, 0f), SecondaryTextColor);
            CreateText("Wins", line, row.Wins.ToString(), 14, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(38f, 32f), new Vector2(75f, 0f), valueColor);
            CreateText("Losses", line, row.Losses.ToString(), 14, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(38f, 32f), new Vector2(121f, 0f), valueColor);
            CreateText("Pct", line, FormatRate(row.WinningPercentage), 14, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(62f, 32f), new Vector2(181f, 0f), valueColor);
            CreateText("Gb", line, row.Rank == 1 ? "-" : FormatGamesBehind(row.GamesBehind), 14,
                FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(58f, 32f), new Vector2(244f, 0f), SecondaryTextColor);
            CreateText("Streak", line, FormatStreak(row.StreakOutcome, row.StreakLength), 13,
                FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(54f, 32f), new Vector2(292f, 0f), GetStreakColor(row.StreakOutcome));
        }

        private void RenderBattingLeaders(LeagueHubView view)
        {
            RectTransform panel = CreatePanel(
                "BattingLeaders", "BATTING LEADERS", "타자 순위", new Vector2(600f, 480f),
                new Vector2(0f, 210f));
            string[] labels = { "타율", "홈런", "타점", "도루", "OPS" };
            RenderCategoryTabs(panel, labels, (int)_battingCategory, 600f, index =>
            {
                _battingCategory = (LeagueBattingCategory)index;
                Render();
            });
            CreateTableHeader(panel,
                new[] { "순위", "선수", "팀", "경기", "타율", "홈런", "타점", "OPS" },
                new[] { -268f, -198f, -82f, 16f, 79f, 139f, 194f, 256f },
                new[] { 45f, 120f, 88f, 50f, 58f, 50f, 50f, 58f },
                119f);

            LeagueBattingLeaderboardView leaderboard = view.GetBattingLeaderboard(_battingCategory);
            if (leaderboard.Leaders.Count == 0)
            {
                RenderEmptyLeaderboard(panel, "규정 타석을 충족한 선수가 없습니다.");
            }
            else
            {
                for (int index = 0; index < leaderboard.Leaders.Count; index++)
                    RenderBattingRow(panel, view, leaderboard.Leaders[index], 76f - index * 49f);
            }
            RenderMyBattingRank(panel, leaderboard);
        }

        private void RenderBattingRow(
            RectTransform panel,
            LeagueHubView view,
            LeagueBattingLeaderView row,
            float y)
        {
            RectTransform line = CreateImage(
                "Batter_" + row.PlayerId,
                panel,
                row.IsMyPlayer ? new Color(0.12f, 0.12f, 0.04f, 1f) : RowColor,
                new Vector2(570f, 44f),
                new Vector2(0f, y));
            Color color = row.IsMyPlayer ? GoldColor : PrimaryTextColor;
            CreateText("Rank", line, row.Rank.ToString(), 17, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(40f, 38f), new Vector2(-268f, 0f), color);
            CreateText("Position", line, GetPositionCode(row.Position), 10, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(28f, 24f), new Vector2(-238f, 0f), SecondaryTextColor);
            CreateText("Name", line, row.PlayerName, 15, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(115f, 38f), new Vector2(-168f, 0f), color);
            CreateTeamBadge(line, row.TeamName, GetTeamColor(view, row.TeamId), new Vector2(-108f, 0f), 25f);
            CreateText("Team", line, GetTeamMonogram(row.TeamName), 11, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Vector2(55f, 30f), new Vector2(-69f, 0f), SecondaryTextColor);
            CreateText("Games", line, row.Games.ToString(), 13, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(46f, 38f), new Vector2(16f, 0f), SecondaryTextColor);
            CreateText("Average", line, FormatRate(row.BattingAverage), 14, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(58f, 38f), new Vector2(79f, 0f), color);
            CreateText("HomeRuns", line, row.HomeRuns.ToString(), 14, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(48f, 38f), new Vector2(139f, 0f), color);
            CreateText("Rbi", line, row.RunsBattedIn.ToString(), 14, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(48f, 38f), new Vector2(194f, 0f), color);
            CreateText("Ops", line, FormatRate(row.OnBasePlusSlugging), 14, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(58f, 38f), new Vector2(255f, 0f), color);
        }

        private void RenderPitchingLeaders(LeagueHubView view)
        {
            RectTransform panel = CreatePanel(
                "PitchingLeaders", "PITCHING LEADERS", "투수 순위", new Vector2(650f, 480f),
                new Vector2(635f, 210f));
            string[] labels = { "ERA", "승", "세이브", "탈삼진", "WHIP" };
            RenderCategoryTabs(panel, labels, (int)_pitchingCategory, 650f, index =>
            {
                _pitchingCategory = (LeaguePitchingCategory)index;
                Render();
            });
            CreateTableHeader(panel,
                new[] { "순위", "선수", "팀", "승-패", "이닝", "ERA", "SO", "WHIP" },
                new[] { -292f, -216f, -91f, 19f, 86f, 151f, 211f, 276f },
                new[] { 45f, 126f, 86f, 58f, 62f, 58f, 48f, 58f },
                119f);

            LeaguePitchingLeaderboardView leaderboard = view.GetPitchingLeaderboard(_pitchingCategory);
            if (leaderboard.Leaders.Count == 0)
            {
                RenderEmptyLeaderboard(panel, "규정 이닝을 충족한 선수가 없습니다.");
            }
            else
            {
                for (int index = 0; index < leaderboard.Leaders.Count; index++)
                    RenderPitchingRow(panel, view, leaderboard.Leaders[index], 76f - index * 49f);
            }
            RenderMyPitchingRank(panel, leaderboard);
        }

        private void RenderPitchingRow(
            RectTransform panel,
            LeagueHubView view,
            LeaguePitchingLeaderView row,
            float y)
        {
            RectTransform line = CreateImage(
                "Pitcher_" + row.PlayerId,
                panel,
                row.IsMyPlayer ? new Color(0.12f, 0.12f, 0.04f, 1f) : RowColor,
                new Vector2(620f, 44f),
                new Vector2(0f, y));
            Color color = row.IsMyPlayer ? GoldColor : PrimaryTextColor;
            CreateText("Rank", line, row.Rank.ToString(), 17, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(40f, 38f), new Vector2(-292f, 0f), color);
            CreateText("Position", line, GetPositionCode(row.Position), 10, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(28f, 24f), new Vector2(-258f, 0f), SecondaryTextColor);
            CreateText("Name", line, row.PlayerName, 15, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(122f, 38f), new Vector2(-186f, 0f), color);
            CreateTeamBadge(line, row.TeamName, GetTeamColor(view, row.TeamId), new Vector2(-117f, 0f), 25f);
            CreateText("Team", line, GetTeamMonogram(row.TeamName), 11, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Vector2(55f, 30f), new Vector2(-79f, 0f), SecondaryTextColor);
            CreateText("Record", line, $"{row.Wins}-{row.Losses}", 13, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(58f, 38f), new Vector2(19f, 0f), color);
            CreateText("Innings", line, FormatInnings(row.OutsRecorded), 13, FontStyle.Normal,
                TextAnchor.MiddleCenter, new Vector2(60f, 38f), new Vector2(86f, 0f), SecondaryTextColor);
            CreateText("Era", line, row.EarnedRunAverage.ToString("0.00"), 14, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(58f, 38f), new Vector2(151f, 0f), color);
            CreateText("Strikeouts", line, row.Strikeouts.ToString(), 14, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(46f, 38f), new Vector2(211f, 0f), color);
            CreateText("Whip", line, row.WalksHitsPerInningPitched.ToString("0.00"), 14, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(58f, 38f), new Vector2(276f, 0f), color);
        }

        private void RenderTeamMetrics(LeagueHubView view)
        {
            RectTransform panel = CreatePanel(
                "TeamMetrics", "TEAM COMPARISON", "팀 지표 비교", new Vector2(620f, 340f),
                new Vector2(-650f, -220f));
            CreateText("BestHeader", panel, "리그 1위", 11, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(110f, 24f), new Vector2(-150f, 112f), SecondaryTextColor);
            CreateText("AverageHeader", panel, "리그 평균", 11, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(90f, 24f), new Vector2(-22f, 112f), SecondaryTextColor);
            CreateText("MyHeader", panel, view.MyTeamName, 11, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(150f, 24f), new Vector2(127f, 112f), GoldColor);
            CreateText("RankHeader", panel, "순위", 11, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(54f, 24f), new Vector2(269f, 112f), SecondaryTextColor);

            for (int index = 0; index < view.TeamMetrics.Count; index++)
                RenderTeamMetricRow(panel, view.TeamMetrics[index], 71f - index * 61f);
        }

        private static void RenderTeamMetricRow(RectTransform panel, LeagueTeamMetricView metric, float y)
        {
            RectTransform row = CreateImage(
                "Metric_" + metric.Metric, panel, RowColor, new Vector2(590f, 52f), new Vector2(0f, y));
            CreateText("Label", row, GetTeamMetricLabel(metric.Metric), 14, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Vector2(90f, 44f), new Vector2(-246f, 0f), PrimaryTextColor);
            if (!metric.HasData)
            {
                CreateText("Empty", row, "시즌 기록 집계 전", 13, FontStyle.Normal,
                    TextAnchor.MiddleCenter, new Vector2(400f, 42f), new Vector2(70f, 0f), MutedColor);
                return;
            }

            CreateText("Best", row, FormatTeamMetric(metric.Metric, metric.BestValue), 14, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(82f, 42f), new Vector2(-139f, 0f), BrightAccentColor);
            CreateText("Average", row, FormatTeamMetric(metric.Metric, metric.LeagueAverage), 14,
                FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(82f, 42f), new Vector2(-23f, 0f), SecondaryTextColor);
            CreateProgressBar(row, GetTeamMetricRatio(metric), new Vector2(112f, 8f), new Vector2(102f, -13f),
                GoldColor);
            CreateText("Mine", row, FormatTeamMetric(metric.Metric, metric.MyTeamValue), 15, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(100f, 30f), new Vector2(102f, 7f), GoldColor);
            CreateText("Rank", row, metric.MyTeamRank <= 0 ? "-" : $"{metric.MyTeamRank}위", 16,
                FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(55f, 42f), new Vector2(265f, 0f), GoldColor);
        }

        private void RenderLeagueFocus(LeagueHubView view)
        {
            RectTransform panel = CreatePanel(
                "LeagueFocus", "LEAGUE FOCUS", "리그 포커스 / 타이틀 레이스",
                new Vector2(610f, 340f), new Vector2(0f, -220f));

            LeagueStandingView leader = view.Standings.Count > 0 ? view.Standings[0] : default;
            RenderFocusCard(panel, "STANDINGS", "선두 경쟁",
                view.Standings.Count == 0 ? "기록 없음" : leader.TeamName,
                view.Standings.Count == 0 ? "시즌 시작 전" :
                    $"{leader.Wins}승 {leader.Losses}패  {FormatRate(leader.WinningPercentage)}",
                new Vector2(0f, 79f), ToUnityColor(GetTeamColor(view, leader.TeamId)));

            LeagueBattingLeaderboardView batting = view.GetBattingLeaderboard(_battingCategory);
            string battingName = batting.Leaders.Count == 0 ? "기록 없음" : batting.Leaders[0].PlayerName;
            string battingDetail = batting.Leaders.Count == 0
                ? "규정 타석 충족 대기"
                : $"{GetBattingCategoryLabel(_battingCategory)}  " +
                  FormatBattingCategoryValue(batting.Leaders[0], _battingCategory);
            RenderFocusCard(panel, "BATTER", "타자 타이틀 선두", battingName, battingDetail,
                new Vector2(-147f, -47f), AccentColor, new Vector2(286f, 102f));

            LeaguePitchingLeaderboardView pitching = view.GetPitchingLeaderboard(_pitchingCategory);
            string pitchingName = pitching.Leaders.Count == 0 ? "기록 없음" : pitching.Leaders[0].PlayerName;
            string pitchingDetail = pitching.Leaders.Count == 0
                ? "규정 이닝 충족 대기"
                : $"{GetPitchingCategoryLabel(_pitchingCategory)}  " +
                  FormatPitchingCategoryValue(pitching.Leaders[0], _pitchingCategory);
            RenderFocusCard(panel, "PITCHER", "투수 타이틀 선두", pitchingName, pitchingDetail,
                new Vector2(147f, -47f), new Color(0.30f, 0.55f, 0.88f, 1f), new Vector2(286f, 102f));

            RenderMyPlayerRace(panel, batting, pitching);
        }

        private static void RenderFocusCard(
            Transform parent,
            string eyebrow,
            string title,
            string value,
            string detail,
            Vector2 position,
            Color accent,
            Vector2? size = null)
        {
            Vector2 cardSize = size ?? new Vector2(580f, 92f);
            RectTransform card = CreateSection("Focus_" + eyebrow, parent, cardSize, position, PanelDarkColor);
            CreateImage("Accent", card, accent, new Vector2(5f, cardSize.y - 8f),
                new Vector2(-cardSize.x * 0.5f + 5f, 0f));
            CreateText("Eyebrow", card, eyebrow, 9, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(cardSize.x - 30f, 17f), new Vector2(8f, cardSize.y * 0.5f - 16f), accent);
            CreateText("Title", card, title, 12, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(cardSize.x - 30f, 22f), new Vector2(8f, 11f), SecondaryTextColor);
            CreateText("Value", card, value, 20, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(cardSize.x * 0.52f, 31f), new Vector2(-cardSize.x * 0.20f, -18f), PrimaryTextColor);
            CreateText("Detail", card, detail, 12, FontStyle.Bold, TextAnchor.MiddleRight,
                new Vector2(cardSize.x * 0.42f, 27f), new Vector2(cardSize.x * 0.25f, -18f), accent);
        }

        private static void RenderMyPlayerRace(
            RectTransform panel,
            LeagueBattingLeaderboardView batting,
            LeaguePitchingLeaderboardView pitching)
        {
            string text = "내 선수는 현재 선택 부문의 규정 기록이 없습니다.";
            if (batting.MyPlayer.HasValue)
                text = $"내 선수 · 타자 {GetBattingCategoryLabel(batting.Category)} {batting.MyPlayer.Value.Rank}위";
            else if (pitching.MyPlayer.HasValue)
                text = $"내 선수 · 투수 {GetPitchingCategoryLabel(pitching.Category)} {pitching.MyPlayer.Value.Rank}위";
            CreateText("MyPlayerRace", panel, text, 12, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(560f, 24f), new Vector2(0f, -128f), GoldColor);
        }

        private void RenderSchedule(LeagueHubView view)
        {
            RectTransform panel = CreatePanel(
                "Schedule", "LEAGUE SCHEDULE", "최근 결과 / 다음 라운드",
                new Vector2(620f, 340f), new Vector2(650f, -220f));
            CreateText("RecentTitle", panel, "최근 결과", 14, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(250f, 27f), new Vector2(-155f, 108f), PrimaryTextColor);
            CreateText("NextTitle", panel,
                view.NextRoundGames.Count == 0 ? "정규 시즌 종료" : $"{view.CurrentDate:M월 d일} 경기",
                14, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(250f, 27f), new Vector2(155f, 108f), PrimaryTextColor);
            CreateImage("ScheduleDivider", panel, DividerColor, new Vector2(2f, 234f), new Vector2(0f, -17f));

            if (view.RecentResults.Count == 0)
            {
                CreateText("RecentEmpty", panel, "완료된 경기 없음", 13, FontStyle.Normal,
                    TextAnchor.MiddleCenter, new Vector2(270f, 80f), new Vector2(-155f, 20f), MutedColor);
            }
            else
            {
                for (int index = 0; index < view.RecentResults.Count; index++)
                    RenderScheduleRow(panel, view.RecentResults[index], -155f, 69f - index * 44f, true);
            }

            if (view.NextRoundGames.Count == 0)
            {
                CreateText("NextEmpty", panel, "남은 정규 시즌 경기 없음", 13, FontStyle.Normal,
                    TextAnchor.MiddleCenter, new Vector2(270f, 80f), new Vector2(155f, 20f), MutedColor);
            }
            else
            {
                for (int index = 0; index < view.NextRoundGames.Count; index++)
                    RenderScheduleRow(panel, view.NextRoundGames[index], 155f, 69f - index * 50f, false);
            }
        }

        private static void RenderScheduleRow(
            RectTransform panel,
            LeagueScheduleGameView game,
            float x,
            float y,
            bool showScore)
        {
            RectTransform row = CreateImage(
                "Game_" + game.GameId,
                panel,
                game.IncludesMyTeam ? new Color(0.08f, 0.11f, 0.13f, 1f) : RowColor,
                new Vector2(286f, 38f),
                new Vector2(x, y));
            CreateText("Date", row, game.Date.ToString("MM/dd"), 11, FontStyle.Normal,
                TextAnchor.MiddleCenter, new Vector2(47f, 32f), new Vector2(-116f, 0f), SecondaryTextColor);
            CreateText("Away", row, GetShortTeamName(game.AwayTeamName), 12, FontStyle.Bold,
                TextAnchor.MiddleRight, new Vector2(80f, 32f), new Vector2(-49f, 0f),
                game.IncludesMyTeam ? GoldColor : PrimaryTextColor);
            CreateText("Versus", row, showScore ? $"{game.AwayRuns} : {game.HomeRuns}" : "VS", 12,
                FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(52f, 32f), new Vector2(29f, 0f),
                showScore ? BrightAccentColor : SecondaryTextColor);
            CreateText("Home", row, GetShortTeamName(game.HomeTeamName), 12, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Vector2(80f, 32f), new Vector2(97f, 0f),
                game.IncludesMyTeam ? GoldColor : PrimaryTextColor);
        }

        private static void RenderMyBattingRank(
            RectTransform panel,
            LeagueBattingLeaderboardView leaderboard)
        {
            string value = leaderboard.MyPlayer.HasValue
                ? $"내 선수 {GetBattingCategoryLabel(leaderboard.Category)} {leaderboard.MyPlayer.Value.Rank}위 · " +
                  FormatBattingCategoryValue(leaderboard.MyPlayer.Value, leaderboard.Category)
                : "내 선수 · 현재 부문 규정 기록 없음";
            CreateText("MyPlayerRank", panel, value, 12, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(560f, 27f), new Vector2(0f, -207f),
                leaderboard.MyPlayer.HasValue ? GoldColor : MutedColor);
        }

        private static void RenderMyPitchingRank(
            RectTransform panel,
            LeaguePitchingLeaderboardView leaderboard)
        {
            string value = leaderboard.MyPlayer.HasValue
                ? $"내 선수 {GetPitchingCategoryLabel(leaderboard.Category)} {leaderboard.MyPlayer.Value.Rank}위 · " +
                  FormatPitchingCategoryValue(leaderboard.MyPlayer.Value, leaderboard.Category)
                : "내 선수 · 현재 부문 규정 기록 없음";
            CreateText("MyPlayerRank", panel, value, 12, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(610f, 27f), new Vector2(0f, -207f),
                leaderboard.MyPlayer.HasValue ? GoldColor : MutedColor);
        }

        private static void RenderEmptyLeaderboard(RectTransform panel, string message)
        {
            CreateText("Empty", panel, message, 14, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(500f, 100f), new Vector2(0f, -5f), MutedColor);
        }

        private static void RenderCategoryTabs(
            RectTransform panel,
            IReadOnlyList<string> labels,
            int selectedIndex,
            float panelWidth,
            Action<int> onSelected)
        {
            float width = (panelWidth - 30f) / labels.Count;
            float start = -(labels.Count - 1) * width * 0.5f;
            for (int index = 0; index < labels.Count; index++)
            {
                int captured = index;
                bool selected = index == selectedIndex;
                Button button = CreateButton(
                    "Category_" + labels[index],
                    panel,
                    labels[index],
                    new Vector2(width - 3f, 36f),
                    new Vector2(start + index * width, 165f),
                    selected ? new Color(0.025f, 0.27f, 0.54f, 1f) : PanelDarkColor,
                    out Text text);
                text.fontSize = 14;
                text.color = selected ? PrimaryTextColor : SecondaryTextColor;
                if (selected)
                    CreateImage("Selected", button.transform, BrightAccentColor,
                        new Vector2(width - 14f, 3f), new Vector2(0f, -16f));
                button.onClick.AddListener(() => onSelected(captured));
            }
        }

        private RectTransform CreatePanel(
            string name,
            string eyebrow,
            string title,
            Vector2 size,
            Vector2 position)
        {
            CreateImage(name + "Shadow", _content, new Color(0f, 0f, 0f, 0.72f),
                size + new Vector2(8f, 8f), position + new Vector2(4f, -5f));
            RectTransform panel = CreateImage(name, _content, BorderColor, size, position);
            RectTransform surface = CreateImage("Surface", panel, PanelColor, Vector2.zero, Vector2.zero, true);
            surface.offsetMin = new Vector2(3f, 3f);
            surface.offsetMax = new Vector2(-3f, -3f);
            RectTransform header = CreateImage(
                "Header", panel, new Color(0.022f, 0.11f, 0.19f, 1f),
                new Vector2(size.x - 8f, 50f), new Vector2(0f, size.y * 0.5f - 29f));
            CreateImage("HeaderLine", header, AccentColor, new Vector2(size.x * 0.32f, 2f),
                new Vector2(-size.x * 0.3f, -23f));
            CreateText("Eyebrow", header, eyebrow, 9, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(size.x * 0.32f, 18f), new Vector2(-size.x * 0.32f, 11f), AccentColor);
            CreateText("Title", header, title, 20, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(size.x * 0.62f, 36f), new Vector2(0f, -1f), PrimaryTextColor);
            return panel;
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
            CreateText("Value", segment, value, 19, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(size.x - 42f, 31f), new Vector2(14f, -8f), PrimaryTextColor);
        }

        private static void CreateTableHeader(
            Transform parent,
            IReadOnlyList<string> labels,
            IReadOnlyList<float> positions,
            IReadOnlyList<float> widths,
            float y)
        {
            RectTransform header = CreateImage(
                "TableHeader", parent, PanelDarkColor, new Vector2(((RectTransform)parent).sizeDelta.x - 30f, 31f),
                new Vector2(0f, y));
            for (int index = 0; index < labels.Count; index++)
            {
                CreateText("Column_" + labels[index], header, labels[index], 11, FontStyle.Bold,
                    index == 1 ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter,
                    new Vector2(widths[index], 28f), new Vector2(positions[index], 0f), SecondaryTextColor);
            }
        }

        private static RectTransform CreateSection(
            string name,
            Transform parent,
            Vector2 size,
            Vector2 position,
            Color color)
        {
            RectTransform frame = CreateImage(name, parent, DividerColor, size, position);
            RectTransform surface = CreateImage("Surface", frame, color, Vector2.zero, Vector2.zero, true);
            surface.offsetMin = new Vector2(2f, 2f);
            surface.offsetMax = new Vector2(-2f, -2f);
            return frame;
        }

        private static RectTransform CreateTeamBadge(
            Transform parent,
            string teamName,
            TeamColor teamColor,
            Vector2 position,
            float size)
        {
            Color primary = new(teamColor.Red / 255f, teamColor.Green / 255f, teamColor.Blue / 255f, 1f);
            RectTransform outer = CreateImage(
                "TeamBadge_" + teamName, parent, Color.Lerp(primary, Color.white, 0.30f),
                new Vector2(size, size), position);
            RectTransform inner = CreateImage(
                "Inner", outer, Color.Lerp(primary, BackgroundColor, 0.28f),
                new Vector2(size - 4f, size - 4f), Vector2.zero);
            CreateText("Monogram", inner, GetTeamMonogram(teamName), Math.Max(9, (int)(size * 0.31f)),
                FontStyle.Bold, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero,
                PrimaryTextColor, true);
            return outer;
        }

        private static void CreateProgressBar(
            Transform parent,
            float normalizedValue,
            Vector2 size,
            Vector2 position,
            Color fillColor)
        {
            float clamped = Mathf.Clamp01(normalizedValue);
            RectTransform track = CreateImage("Track", parent, new Color(0.11f, 0.16f, 0.20f, 1f), size, position);
            float fillWidth = Mathf.Max(2f, (size.x - 4f) * clamped);
            RectTransform fill = CreateImage("Fill", track, fillColor,
                new Vector2(fillWidth, size.y - 4f), Vector2.zero);
            fill.anchorMin = fill.anchorMax = new Vector2(0f, 0.5f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.anchoredPosition = new Vector2(2f, 0f);
        }

        private static TeamColor GetTeamColor(LeagueHubView view, int teamId)
        {
            for (int index = 0; index < view.Standings.Count; index++)
            {
                if (view.Standings[index].TeamId == teamId)
                    return view.Standings[index].TeamColor;
            }
            return new TeamColor(70, 115, 155);
        }

        private static Color ToUnityColor(TeamColor color)
        {
            return new Color(color.Red / 255f, color.Green / 255f, color.Blue / 255f, 1f);
        }

        private static float GetTeamMetricRatio(LeagueTeamMetricView metric)
        {
            if (!metric.HasData || metric.BestValue <= 0d || metric.MyTeamValue <= 0d)
                return 0f;
            double ratio = metric.Metric == LeagueTeamMetric.EarnedRunAverage
                ? metric.BestValue / metric.MyTeamValue
                : metric.MyTeamValue / metric.BestValue;
            return Mathf.Clamp((float)ratio, 0.08f, 1f);
        }

        private static string GetLeagueLabel(LeagueLevel level)
        {
            return level switch
            {
                LeagueLevel.Rookie => "ROOKIE",
                LeagueLevel.Minor => "MINOR",
                LeagueLevel.Major => "MAJOR",
                _ => "ROOKIE"
            };
        }

        private static string GetDayLabel(DayOfWeek day)
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

        private static string GetPositionCode(PlayerPosition position)
        {
            return position switch
            {
                PlayerPosition.Catcher => "C",
                PlayerPosition.FirstBase => "1B",
                PlayerPosition.SecondBase => "2B",
                PlayerPosition.ThirdBase => "3B",
                PlayerPosition.Shortstop => "SS",
                PlayerPosition.LeftField => "LF",
                PlayerPosition.CenterField => "CF",
                PlayerPosition.RightField => "RF",
                PlayerPosition.DesignatedHitter => "DH",
                PlayerPosition.StartingPitcher => "SP",
                PlayerPosition.ReliefPitcher => "RP",
                _ => "-"
            };
        }

        private static string GetTeamMetricLabel(LeagueTeamMetric metric)
        {
            return metric switch
            {
                LeagueTeamMetric.BattingAverage => "팀 타율",
                LeagueTeamMetric.HomeRuns => "팀 홈런",
                LeagueTeamMetric.EarnedRunAverage => "팀 ERA",
                LeagueTeamMetric.Strikeouts => "팀 탈삼진",
                _ => "-"
            };
        }

        private static string FormatTeamMetric(LeagueTeamMetric metric, double value)
        {
            return metric switch
            {
                LeagueTeamMetric.BattingAverage => FormatRate(value),
                LeagueTeamMetric.EarnedRunAverage => value.ToString("0.00"),
                _ => Math.Round(value).ToString("0")
            };
        }

        private static string GetBattingCategoryLabel(LeagueBattingCategory category)
        {
            return category switch
            {
                LeagueBattingCategory.BattingAverage => "타율",
                LeagueBattingCategory.HomeRuns => "홈런",
                LeagueBattingCategory.RunsBattedIn => "타점",
                LeagueBattingCategory.StolenBases => "도루",
                LeagueBattingCategory.OnBasePlusSlugging => "OPS",
                _ => "기록"
            };
        }

        private static string FormatBattingCategoryValue(
            LeagueBattingLeaderView player,
            LeagueBattingCategory category)
        {
            return category switch
            {
                LeagueBattingCategory.BattingAverage => FormatRate(player.BattingAverage),
                LeagueBattingCategory.HomeRuns => player.HomeRuns.ToString(),
                LeagueBattingCategory.RunsBattedIn => player.RunsBattedIn.ToString(),
                LeagueBattingCategory.StolenBases => player.StolenBases.ToString(),
                LeagueBattingCategory.OnBasePlusSlugging => FormatRate(player.OnBasePlusSlugging),
                _ => "-"
            };
        }

        private static string GetPitchingCategoryLabel(LeaguePitchingCategory category)
        {
            return category switch
            {
                LeaguePitchingCategory.EarnedRunAverage => "ERA",
                LeaguePitchingCategory.Wins => "승",
                LeaguePitchingCategory.Saves => "세이브",
                LeaguePitchingCategory.Strikeouts => "탈삼진",
                LeaguePitchingCategory.WalksHitsPerInningPitched => "WHIP",
                _ => "기록"
            };
        }

        private static string FormatPitchingCategoryValue(
            LeaguePitchingLeaderView player,
            LeaguePitchingCategory category)
        {
            return category switch
            {
                LeaguePitchingCategory.EarnedRunAverage => player.EarnedRunAverage.ToString("0.00"),
                LeaguePitchingCategory.Wins => player.Wins.ToString(),
                LeaguePitchingCategory.Saves => player.Saves.ToString(),
                LeaguePitchingCategory.Strikeouts => player.Strikeouts.ToString(),
                LeaguePitchingCategory.WalksHitsPerInningPitched =>
                    player.WalksHitsPerInningPitched.ToString("0.00"),
                _ => "-"
            };
        }

        private static string FormatRate(double value) => value.ToString(".000");
        private static string FormatInnings(int outs) => $"{outs / 3}.{outs % 3}";
        private static string FormatGamesBehind(double value) => value.ToString("0.0");

        private static string FormatStreak(TeamGameOutcome? outcome, int length)
        {
            if (!outcome.HasValue || length <= 0)
                return "-";
            return outcome.Value switch
            {
                TeamGameOutcome.Win => $"{length}승",
                TeamGameOutcome.Loss => $"{length}패",
                _ => $"{length}무"
            };
        }

        private static Color GetStreakColor(TeamGameOutcome? outcome)
        {
            return outcome switch
            {
                TeamGameOutcome.Win => WinColor,
                TeamGameOutcome.Loss => LossColor,
                TeamGameOutcome.Tie => TieColor,
                _ => MutedColor
            };
        }

        private static string GetTeamMonogram(string teamName)
        {
            if (string.IsNullOrWhiteSpace(teamName))
                return "UP";
            string compact = teamName.Replace(" ", string.Empty);
            return compact.Length == 1 ? compact : compact.Substring(0, 2);
        }

        private static string GetShortTeamName(string teamName)
        {
            if (string.IsNullOrWhiteSpace(teamName))
                return "-";
            int separator = teamName.LastIndexOf(' ');
            return separator >= 0 && separator < teamName.Length - 1 ? teamName.Substring(separator + 1) : teamName;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            var rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private static RectTransform CreateImage(
            string name,
            Transform parent,
            Color color,
            Vector2 size,
            Vector2 position,
            bool stretch = false)
        {
            RectTransform rect = CreateRect(name, parent, size, position);
            if (stretch)
                Stretch(rect);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private static Text CreateText(
            string name,
            Transform parent,
            string value,
            int fontSize,
            FontStyle style,
            TextAnchor alignment,
            Vector2 size,
            Vector2 position,
            Color color,
            bool stretch = false)
        {
            RectTransform rect = CreateRect(name, parent, size, position);
            if (stretch)
                Stretch(rect);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(
            string name,
            Transform parent,
            string label,
            Vector2 size,
            Vector2 position,
            Color color,
            out Text text)
        {
            RectTransform rect = CreateImage(name, parent, color, size, position);
            Button button = rect.gameObject.AddComponent<Button>();
            rect.GetComponent<Image>().raycastTarget = true;
            ColorBlock colors = button.colors;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.12f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            text = CreateText("Label", rect, label, 19, FontStyle.Bold, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.zero, PrimaryTextColor, true);
            return button;
        }

        private static void AddTextOutline(Text text, Color color, float distance)
        {
            Outline outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(distance, -distance);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void ClearChildren(Transform parent)
        {
            for (int index = parent.childCount - 1; index >= 0; index--)
            {
                GameObject child = parent.GetChild(index).gameObject;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(child);
                else
#endif
                    Destroy(child);
            }
        }
    }
}
