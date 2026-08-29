using System;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Career;
using Baseball.Game.Manager;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Career
{
    /// <summary>리그 순위와 내 시즌·통산·수상 기록을 실제 커리어 원본에서 조회하는 기록 화면이다.</summary>
    public sealed partial class UI_Scene_CareerRecords : UISceneBase, ICareerTabScreen
    {
        private static readonly Color BackgroundColor = new(0.005f, 0.018f, 0.032f, 1f);
        private static readonly Color TopBarColor = new(0.008f, 0.027f, 0.052f, 1f);
        private static readonly Color PanelColor = new(0.014f, 0.052f, 0.087f, 0.99f);
        private static readonly Color PanelDarkColor = new(0.007f, 0.029f, 0.052f, 1f);
        private static readonly Color HeaderColor = new(0.019f, 0.084f, 0.14f, 1f);
        private static readonly Color BorderColor = new(0.13f, 0.34f, 0.51f, 1f);
        private static readonly Color DividerColor = new(0.08f, 0.20f, 0.31f, 1f);
        private static readonly Color AccentColor = new(0.12f, 0.55f, 0.95f, 1f);
        private static readonly Color BrightAccentColor = new(0.22f, 0.68f, 1f, 1f);
        private static readonly Color RankColor = new(0.43f, 0.89f, 0.19f, 1f);
        private static readonly Color GoldColor = new(0.96f, 0.71f, 0.18f, 1f);
        private static readonly Color WinColor = new(0.28f, 0.82f, 0.49f, 1f);
        private static readonly Color LossColor = new(0.91f, 0.34f, 0.39f, 1f);
        private static readonly Color PrimaryTextColor = new(0.94f, 0.97f, 1f, 1f);
        private static readonly Color SecondaryTextColor = new(0.68f, 0.75f, 0.82f, 1f);
        private static readonly Color MutedTextColor = new(0.40f, 0.49f, 0.58f, 1f);

        private readonly CareerRecordsService _recordsService = new();
        private CareerManager _manager;
        private RectTransform _content;
        private CareerRecordsPage _page = CareerRecordsPage.Personal;
        private CareerRecordCategory _category = CareerRecordCategory.Batting;
        private CareerRecordViewMode _viewMode = CareerRecordViewMode.Expanded;
        private CompetitionScope _scope = CompetitionScope.RegularSeason;
        private bool _hasSelectedInitialCategory;

        public override bool BlocksLowerInput => true;
        public CareerMainTab MainTab => CareerMainTab.Records;

        /// <summary>프리팹이 없는 현재 Vertical Slice에서 기록 화면을 런타임 생성한다.</summary>
        public static UI_Scene_CareerRecords CreateRuntime(Transform parent)
        {
            var screenObject = new GameObject(
                nameof(UI_Scene_CareerRecords),
                typeof(RectTransform),
                typeof(CanvasGroup));
            screenObject.transform.SetParent(parent, false);
            UI_Scene_CareerRecords screen = screenObject.AddComponent<UI_Scene_CareerRecords>();
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
            SelectInitialCategory();
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

        private void SelectInitialCategory()
        {
            if (_hasSelectedInitialCategory || _manager?.CurrentCareer == null)
                return;
            PlayerPosition position = _manager.CurrentCareer.MyPlayer.PrimaryPosition;
            _category = position is PlayerPosition.StartingPitcher or PlayerPosition.ReliefPitcher
                ? CareerRecordCategory.Pitching
                : CareerRecordCategory.Batting;
            _hasSelectedInitialCategory = true;
        }

        private void HandleCareerChanged()
        {
            if (_manager == null || !_manager.HasActiveCareer)
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
            CareerRecordsView view = _recordsService.Build(
                _manager.CurrentCareer,
                _category,
                _viewMode,
                _scope);
            RenderBackgroundAccents();
            RenderTopBar(_manager.Dashboard);
            RenderTitle();
            RenderPageTabs();
            RenderCategoryMenu();

            switch (_page)
            {
                case CareerRecordsPage.Personal:
                    RenderPersonalPage(view);
                    break;
                case CareerRecordsPage.Season:
                    RenderSeasonPage(view, sortByRecord: false);
                    break;
                case CareerRecordsPage.Career:
                    RenderSeasonPage(view, sortByRecord: true);
                    break;
                case CareerRecordsPage.Awards:
                    RenderAwardsPage(view);
                    break;
                default:
                    RenderHighlightsPage(view);
                    break;
            }

            CareerNavigationChrome.Create(_content, CareerMainTab.Records);
        }

        private void RenderBackgroundAccents()
        {
            CreateImage("TopGlow", _content, new Color(0.02f, 0.18f, 0.31f, 0.25f),
                new Vector2(1920f, 5f), new Vector2(0f, 456f));
            CreateImage("ContentGlow", _content, new Color(0.02f, 0.12f, 0.21f, 0.16f),
                new Vector2(1880f, 2f), new Vector2(0f, 339f));
        }

        private void RenderTopBar(CareerDashboardView dashboard)
        {
            RectTransform bar = CreateImage(
                "TopBar", _content, TopBarColor, new Vector2(1920f, 80f), new Vector2(0f, 500f));
            CreateImage("TopBarBottom", bar, BorderColor, new Vector2(1920f, 2f), new Vector2(0f, -39f));

            Text logo = CreateText(
                "Logo", bar, "UPlayBall", 34, FontStyle.BoldAndItalic, TextAnchor.MiddleLeft,
                new Vector2(310f, 50f), new Vector2(-800f, 5f), PrimaryTextColor);
            AddTextOutline(logo, new Color(0.05f, 0.34f, 0.62f, 0.9f), 1.5f);
            CreateText(
                "LogoCaption", bar, "ULTIMATE BASEBALL", 9, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(230f, 18f), new Vector2(-796f, -23f), AccentColor);

            CreateTopBarSegment(
                bar,
                "SEASON",
                $"{dashboard.SeasonYear} 시즌  {GetLeagueLabel(dashboard.LeagueLevel)} League",
                new Vector2(-390f, 0f),
                new Vector2(455f, 64f));
            string date = dashboard.NextGame.HasValue
                ? $"{dashboard.NextGame.Value.Date:yyyy년 M월 d일}"
                : "정규 시즌 종료";
            CreateTopBarSegment(bar, "DATE", date, new Vector2(50f, 0f), new Vector2(340f, 64f));
            CreateTopBarSegment(
                bar,
                "MONEY",
                FormatMoney(dashboard.AvailableMoney),
                new Vector2(430f, 0f),
                new Vector2(330f, 64f));
            CreateText("Mail", bar, "MAIL", 11, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(74f, 44f), new Vector2(775f, 0f), SecondaryTextColor);
        }

        private static void CreateTopBarSegment(
            Transform parent,
            string eyebrow,
            string value,
            Vector2 position,
            Vector2 size)
        {
            RectTransform segment = CreateImage(
                eyebrow + "Segment", parent, new Color(0.012f, 0.045f, 0.073f, 1f), size, position);
            CreateText("Eyebrow", segment, eyebrow, 9, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(size.x - 32f, 16f), new Vector2(0f, 14f), MutedTextColor);
            CreateText("Value", segment, value, 18, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(size.x - 32f, 28f), new Vector2(0f, -8f), PrimaryTextColor);
        }

        private void RenderTitle()
        {
            string scope = _scope == CompetitionScope.Postseason ? "포스트시즌" : "정규시즌";
            CreateText("PageTitle", _content, $"기록  ·  {scope}", 32, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(400f, 50f), new Vector2(0f, 430f), PrimaryTextColor);
        }

        private void RenderPageTabs()
        {
            string[] labels = { "개인 기록", "시즌 기록", "역대 기록", "수상 내역", "하이라이트" };
            const float width = 270f;
            for (int index = 0; index < labels.Length; index++)
            {
                var page = (CareerRecordsPage)index;
                bool isActive = page == _page;
                Button button = CreateButton(
                    "PageTab_" + page,
                    _content,
                    labels[index],
                    new Vector2(width - 3f, 56f),
                    new Vector2(-540f + index * width, 375f),
                    isActive ? new Color(0.025f, 0.25f, 0.49f, 1f) : PanelDarkColor,
                    out Text label);
                label.fontSize = 20;
                label.color = isActive ? PrimaryTextColor : SecondaryTextColor;
                if (isActive)
                {
                    CreateImage("ActiveLine", button.transform, BrightAccentColor,
                        new Vector2(width - 14f, 3f), new Vector2(0f, 26f));
                }
                button.onClick.AddListener(() => SelectPage(page));
            }
        }

        private void SelectPage(CareerRecordsPage page)
        {
            if (_page == page)
                return;
            _page = page;
            Render();
        }

        private void RenderCategoryMenu()
        {
            RectTransform panel = CreateBorderedPanel(
                "CategoryMenu", new Vector2(240f, 740f), new Vector2(-825f, -40f));
            CreateText("Header", panel, "기록 구분", 16, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(210f, 38f), new Vector2(0f, 328f), SecondaryTextColor);
            string[] labels = { "타자 기록", "투수 기록", "수비 기록", "주루 기록" };
            for (int index = 0; index < labels.Length; index++)
            {
                var category = (CareerRecordCategory)index;
                bool isActive = category == _category;
                Button button = CreateButton(
                    "Category_" + category,
                    panel,
                    labels[index] + (isActive ? "  ›" : string.Empty),
                    new Vector2(210f, 84f),
                    new Vector2(0f, 250f - index * 102f),
                    isActive ? new Color(0.025f, 0.22f, 0.43f, 1f) : new Color(0.012f, 0.052f, 0.086f, 1f),
                    out Text label);
                label.fontSize = 19;
                label.color = isActive ? PrimaryTextColor : SecondaryTextColor;
                if (isActive)
                {
                    CreateImage("ActiveBorder", button.transform, BrightAccentColor,
                        new Vector2(4f, 70f), new Vector2(-102f, 0f));
                }
                button.onClick.AddListener(() => SelectCategory(category));
            }

            CreateText("ScopeHeader", panel, "경기 범위", 13, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(190f, 24f), new Vector2(0f, -126f), MutedTextColor);
            RenderScopeButtons(panel);
            CreateText("DensityHeader", panel, "표시 정보", 13, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(190f, 24f), new Vector2(0f, -222f), MutedTextColor);
            RenderViewModeButtons(panel);
        }

        private void SelectCategory(CareerRecordCategory category)
        {
            if (_category == category)
                return;
            _category = category;
            Render();
        }

        private void RenderScopeButtons(Transform panel)
        {
            CompetitionScope[] scopes = { CompetitionScope.RegularSeason, CompetitionScope.Postseason };
            string[] labels = { "정규시즌", "포스트" };
            for (int index = 0; index < scopes.Length; index++)
            {
                CompetitionScope scope = scopes[index];
                bool isActive = scope == _scope;
                Button button = CreateButton(
                    "Scope_" + scope,
                    panel,
                    labels[index],
                    new Vector2(100f, 42f),
                    new Vector2(-52f + index * 104f, -165f),
                    isActive ? new Color(0.025f, 0.22f, 0.43f, 1f) : PanelDarkColor,
                    out Text label);
                label.fontSize = 14;
                label.color = isActive ? PrimaryTextColor : SecondaryTextColor;
                button.onClick.AddListener(() => SelectScope(scope));
            }
        }

        private void RenderViewModeButtons(Transform panel)
        {
            CareerRecordViewMode[] modes = { CareerRecordViewMode.Basic, CareerRecordViewMode.Expanded };
            string[] labels = { "핵심", "전체 지표" };
            for (int index = 0; index < modes.Length; index++)
            {
                CareerRecordViewMode mode = modes[index];
                bool isActive = mode == _viewMode;
                Button button = CreateButton(
                    "ViewMode_" + mode,
                    panel,
                    labels[index],
                    new Vector2(100f, 42f),
                    new Vector2(-52f + index * 104f, -261f),
                    isActive ? new Color(0.025f, 0.22f, 0.43f, 1f) : PanelDarkColor,
                    out Text label);
                label.fontSize = 14;
                label.color = isActive ? PrimaryTextColor : SecondaryTextColor;
                button.onClick.AddListener(() => SelectViewMode(mode));
            }
        }

        private void SelectScope(CompetitionScope scope)
        {
            if (_scope == scope)
                return;
            _scope = scope;
            Render();
        }

        private void SelectViewMode(CareerRecordViewMode viewMode)
        {
            if (_viewMode == viewMode)
                return;
            _viewMode = viewMode;
            Render();
        }

        private void RenderPersonalPage(CareerRecordsView view)
        {
            RenderLeaderboard(view);
            RenderMyRecord(view.MyRecordMetrics, view.PlayerName, view.IsMyPlayerQualified);
            RenderTrend(view);
            RenderSummary(view.MyRecordMetrics, "기록 요약", includeRank: true, unrankedLabel: "이번 시즌");
        }

        private void RenderLeaderboard(CareerRecordsView view)
        {
            RectTransform panel = CreateContentPanel(
                "Leaderboard",
                $"주요 {GetCategoryLabel(view.Category)} 기록 (TOP 10)",
                new Vector2(1070f, 510f),
                new Vector2(-150f, 75f));
            if (view.Leaderboard.Length == 0)
            {
                RenderEmptyState(
                    panel,
                    !view.HasScopeData
                        ? "선택한 경기 범위에 아직 기록이 없습니다."
                        : view.Category == CareerRecordCategory.Baserunning
                        ? "아직 도루 시도가 없어 주루 순위가 생성되지 않았습니다."
                        : "현재 규정 자격을 충족한 선수가 없습니다.");
            }
            else
            {
                RenderScrollableLeaderboardTable(panel, view);
            }

            string qualification = view.Category switch
            {
                CareerRecordCategory.Batting => "규정 타석: 팀 경기 × 3.1타석",
                CareerRecordCategory.Pitching => "규정 이닝: 팀 경기 × 1.0이닝",
                CareerRecordCategory.Fielding => "수비 기회가 발생한 선수",
                _ => "도루 시도가 발생한 선수"
            };
            string scopeLabel = view.Scope == CompetitionScope.Postseason
                ? "포스트시즌 참가 선수"
                : qualification;
            CreateText("Qualification", panel,
                $"ⓘ {scopeLabel} · 대상 선수 {view.QualifiedPlayerCount}명" +
                (view.ViewMode == CareerRecordViewMode.Expanded ? " · 하단 바를 드래그해 전체 지표 확인" : string.Empty),
                13, FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(1000f, 28f), new Vector2(0f, -230f), MutedTextColor);
        }

        private void RenderMyRecord(
            CareerRecordMetricValue[] metrics,
            string playerName,
            bool isQualified)
        {
            RectTransform panel = CreateContentPanel(
                "MyRecord", $"내 기록 ({playerName}) · {metrics.Length}개 지표",
                new Vector2(520f, 300f), new Vector2(665f, 180f));
            RenderScrollableMetricGrid(panel, metrics);
            if (!isQualified)
            {
                string message = _scope == CompetitionScope.Postseason
                    ? "이번 포스트시즌 출장 기록 없음"
                    : "규정 자격 전 · 기록은 정상 누적 중";
                CreateText("Unqualified", panel, message, 12,
                    FontStyle.Normal, TextAnchor.MiddleCenter, new Vector2(430f, 24f),
                    new Vector2(0f, -128f), MutedTextColor);
            }
        }

        private void RenderTrend(CareerRecordsView view)
        {
            RectTransform panel = CreateContentPanel(
                "Trend", $"시즌별 {GetMetricLabel(view.PrimaryMetric, true)} 추이",
                new Vector2(520f, 415f), new Vector2(665f, -185f));
            if (view.Trend.Length == 0)
            {
                RenderEmptyState(panel, "시즌 기록이 없습니다.");
                return;
            }

            RectTransform chart = CreateRect("Chart", panel, new Vector2(460f, 300f), new Vector2(0f, -28f));
            for (int line = 0; line < 4; line++)
            {
                float y = -105f + line * 70f;
                CreateImage("Grid_" + line, chart, DividerColor, new Vector2(430f, 1f), new Vector2(10f, y));
            }

            int shown = Math.Min(6, view.Trend.Length);
            double maximum = 0d;
            for (int index = view.Trend.Length - shown; index < view.Trend.Length; index++)
                maximum = Math.Max(maximum, view.Trend[index].Value);
            if (maximum <= 0d)
                maximum = 1d;
            float spacing = 420f / shown;
            for (int shownIndex = 0; shownIndex < shown; shownIndex++)
            {
                CareerRecordTrendPoint point = view.Trend[view.Trend.Length - shown + shownIndex];
                float x = -210f + spacing * (shownIndex + 0.5f);
                float height = Mathf.Max(3f, (float)(point.Value / maximum) * 235f);
                Color color = point.IsCurrent ? BrightAccentColor : AccentColor;
                RectTransform bar = CreateImage(
                    "Bar_" + point.Year,
                    chart,
                    new Color(color.r, color.g, color.b, 0.85f),
                    new Vector2(Mathf.Min(52f, spacing - 12f), height),
                    new Vector2(x, -105f + height * 0.5f));
                CreateImage("Cap", bar, color, new Vector2(bar.sizeDelta.x, 2f),
                    new Vector2(0f, height * 0.5f - 1f));
                CreateText("Value", chart, FormatMetric(view.PrimaryMetric, point.Value), 12,
                    FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(70f, 22f),
                    new Vector2(x, -91f + height), PrimaryTextColor);
                CreateText("Year", chart, point.Year.ToString(), 12, FontStyle.Normal,
                    TextAnchor.MiddleCenter, new Vector2(70f, 22f), new Vector2(x, -127f),
                    point.IsCurrent ? BrightAccentColor : SecondaryTextColor);
            }
        }

        private void RenderSummary(
            CareerRecordMetricValue[] metrics,
            string title,
            bool includeRank,
            string unrankedLabel = "커리어 누적")
        {
            RectTransform panel = CreateContentPanel(
                "Summary", title, new Vector2(1070f, 215f), new Vector2(-150f, -297f));
            int count = Math.Min(6, metrics.Length);
            for (int index = 0; index < count; index++)
            {
                CareerRecordMetricValue metric = metrics[index];
                float x = -440f + index * 176f;
                if (index > 0)
                    CreateImage("Divider_" + index, panel, DividerColor, new Vector2(1f, 105f),
                        new Vector2(x - 88f, -15f));
                CreateText("Metric_" + index, panel, GetMetricLabel(metric.Metric, true), 14,
                    FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(145f, 24f),
                    new Vector2(x, 40f), SecondaryTextColor);
                CreateText("Value_" + index, panel, FormatMetric(metric.Metric, metric.Value), 22,
                    FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(145f, 32f),
                    new Vector2(x, 3f), PrimaryTextColor);
                string sub = includeRank && metric.HasRank ? $"리그 {metric.Rank}위" : unrankedLabel;
                CreateText("Sub_" + index, panel, sub, 13, FontStyle.Bold, TextAnchor.MiddleCenter,
                    new Vector2(145f, 24f), new Vector2(x, -34f),
                    includeRank && metric.HasRank ? RankColor : MutedTextColor);
            }
        }

        private void RenderSeasonPage(CareerRecordsView view, bool sortByRecord)
        {
            CareerRecordSeasonRow[] seasons = (CareerRecordSeasonRow[])view.Seasons.Clone();
            if (sortByRecord)
            {
                Array.Sort(seasons, (left, right) => CompareSeasonRows(left, right, view.PrimaryMetric));
            }
            RectTransform panel = CreateContentPanel(
                "Seasons",
                sortByRecord ? $"역대 {GetMetricLabel(view.PrimaryMetric, true)} 시즌 TOP" : "시즌별 기록",
                new Vector2(1070f, 510f),
                new Vector2(-150f, 75f));
            if (seasons.Length == 0)
                RenderEmptyState(panel, "선택한 경기 범위의 시즌 기록이 아직 없습니다.");
            else
                RenderScrollableSeasonTable(panel, view.LeaderboardColumns, seasons, sortByRecord);

            RenderCareerTotals(view);
            if (view.TradeHistory.Length > 0 || view.TeamSplits.Length > 0)
                RenderMovementHistory(view);
            else
                RenderTrend(view);
            RenderBestSeasons(view, seasons);
        }

        private void RenderMovementHistory(CareerRecordsView view)
        {
            RectTransform panel = CreateContentPanel(
                "MovementHistory", "소속 이동 · 팀별 성적",
                new Vector2(520f, 415f), new Vector2(665f, -185f));
            RenderScrollableMovementRows(panel, view);
        }

        private static string FormatSplitSummary(CareerRecordMetricValue[] metrics)
        {
            int count = Math.Min(4, metrics.Length);
            string result = string.Empty;
            for (int index = 0; index < count; index++)
            {
                CareerRecordMetricValue metric = metrics[index];
                if (index > 0)
                    result += "  ·  ";
                result += $"{GetMetricLabel(metric.Metric, false)} {FormatMetric(metric.Metric, metric.Value)}";
            }
            return result;
        }

        private void RenderCareerTotals(CareerRecordsView view)
        {
            RenderMyRecord(view.CareerTotals, "커리어 합계", isQualified: true);
        }

        private void RenderBestSeasons(CareerRecordsView view, CareerRecordSeasonRow[] seasons)
        {
            var best = new CareerRecordMetricValue[Math.Min(6, view.LeaderboardColumns.Length)];
            for (int metricIndex = 0; metricIndex < best.Length; metricIndex++)
            {
                CareerRecordMetric metric = view.LeaderboardColumns[metricIndex];
                double value = seasons.Length == 0 ? 0d : FindMetric(seasons[0].Metrics, metric);
                for (int seasonIndex = 1; seasonIndex < seasons.Length; seasonIndex++)
                {
                    double candidate = FindMetric(seasons[seasonIndex].Metrics, metric);
                    if (IsBetter(metric, candidate, value))
                        value = candidate;
                }
                best[metricIndex] = new CareerRecordMetricValue(metric, value);
            }
            RenderSummary(best, "커리어 최고 기록", includeRank: false, unrankedLabel: "역대 최고 시즌");
        }

        private void RenderAwardsPage(CareerRecordsView view)
        {
            RectTransform panel = CreateContentPanel(
                "Awards", "수상 내역 · 행을 선택하면 챕터 컷 다시 보기",
                new Vector2(1070f, 510f), new Vector2(-150f, 75f));
            if (view.Awards.Length == 0)
            {
                RenderEmptyState(panel, "아직 수상 기록이 없습니다. 시즌을 완주해 첫 트로피에 도전하세요.");
            }
            else
            {
                RenderScrollableAwardRows(panel, view.Awards);
            }
            RenderAwardSummary(view);
            RenderAwardTimeline(view);
            RenderSummary(view.CareerTotals, "수상 당시 커리어 기반 기록", includeRank: false);
        }

        private void RenderAwardSummary(CareerRecordsView view)
        {
            RectTransform panel = CreateContentPanel(
                "AwardSummary", "수상 요약", new Vector2(520f, 300f), new Vector2(665f, 180f));
            CreateText("Trophy", panel, "TROPHY", 18, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(150f, 32f), new Vector2(0f, 72f), GoldColor);
            CreateText("Count", panel, view.Awards.Length.ToString(), 52, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(180f, 70f), new Vector2(0f, 18f), PrimaryTextColor);
            CreateText("Caption", panel, "커리어 누적 수상", 15, FontStyle.Normal,
                TextAnchor.MiddleCenter, new Vector2(280f, 28f), new Vector2(0f, -36f), SecondaryTextColor);
            string latest = view.Awards.Length == 0
                ? "첫 수상을 기다리는 중"
                : $"최근: {view.Awards[0].Year} {GetAwardLabel(view.Awards[0].Category)}";
            CreateText("Latest", panel, latest, 14, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(430f, 30f), new Vector2(0f, -86f),
                view.Awards.Length == 0 ? MutedTextColor : GoldColor);
        }

        private void RenderAwardTimeline(CareerRecordsView view)
        {
            RectTransform panel = CreateContentPanel(
                "AwardTimeline", "시즌별 트로피", new Vector2(520f, 415f), new Vector2(665f, -185f));
            if (view.Awards.Length == 0)
            {
                RenderEmptyState(panel, "정규시즌·포스트시즌 수상 결과가 이곳에 누적됩니다.");
                return;
            }
            int count = Math.Min(7, view.Awards.Length);
            for (int index = 0; index < count; index++)
            {
                CareerAwardRecordView award = view.Awards[index];
                float y = 128f - index * 43f;
                CreateImage("Marker_" + index, panel, GoldColor, new Vector2(8f, 8f), new Vector2(-208f, y));
                CreateText("Year_" + index, panel, award.Year.ToString(), 14, FontStyle.Bold,
                    TextAnchor.MiddleCenter, new Vector2(72f, 28f), new Vector2(-157f, y), SecondaryTextColor);
                CreateText("Name_" + index, panel, GetAwardLabel(award.Category), 15, FontStyle.Bold,
                    TextAnchor.MiddleLeft, new Vector2(300f, 28f), new Vector2(55f, y), PrimaryTextColor);
            }
        }

        private void RenderHighlightsPage(CareerRecordsView view)
        {
            RectTransform panel = CreateContentPanel(
                "Highlights", "최근 경기 하이라이트", new Vector2(1070f, 510f), new Vector2(-150f, 75f));
            if (view.Highlights.Length == 0)
            {
                RenderEmptyState(panel, "아직 출장 기록이 없습니다. 첫 출전이 기록되면 이곳에 표시됩니다.");
            }
            else
            {
                RenderScrollableHighlightRows(
                    panel,
                    view.Highlights,
                    _manager.CurrentCareer.MyPlayer.PrimaryPosition);
            }
            RenderMyRecord(view.MyRecordMetrics, view.PlayerName, view.IsMyPlayerQualified);
            RenderTrend(view);
            RenderSummary(view.MyRecordMetrics, "현재 시즌 기록", includeRank: true, unrankedLabel: "이번 시즌");
        }

        private static int CompareSeasonRows(
            CareerRecordSeasonRow left,
            CareerRecordSeasonRow right,
            CareerRecordMetric metric)
        {
            double leftValue = FindMetric(left.Metrics, metric);
            double rightValue = FindMetric(right.Metrics, metric);
            int comparison = IsLowerBetter(metric)
                ? leftValue.CompareTo(rightValue)
                : rightValue.CompareTo(leftValue);
            return comparison != 0 ? comparison : right.Year.CompareTo(left.Year);
        }

        private static double FindMetric(CareerRecordMetricValue[] metrics, CareerRecordMetric metric)
        {
            for (int index = 0; index < metrics.Length; index++)
            {
                if (metrics[index].Metric == metric)
                    return metrics[index].Value;
            }
            return 0d;
        }

        private static bool IsBetter(CareerRecordMetric metric, double candidate, double current)
        {
            return IsLowerBetter(metric) ? candidate < current : candidate > current;
        }

        private static bool IsLowerBetter(CareerRecordMetric metric)
        {
            return metric is CareerRecordMetric.EarnedRunAverage or
                CareerRecordMetric.WalksHitsPerInningPitched or
                CareerRecordMetric.HomeRunsPerNineInnings or
                CareerRecordMetric.Errors;
        }

        private RectTransform CreateContentPanel(
            string name,
            string title,
            Vector2 size,
            Vector2 position)
        {
            RectTransform panel = CreateBorderedPanel(name, size, position);
            RectTransform header = CreateImage(
                "Header", panel, HeaderColor, new Vector2(size.x - 4f, 44f),
                new Vector2(0f, size.y * 0.5f - 23f));
            CreateText("Title", header, title, 18, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(size.x - 42f, 36f), Vector2.zero, PrimaryTextColor);
            CreateImage("HeaderLine", header, AccentColor, new Vector2(150f, 2f),
                new Vector2(-size.x * 0.5f + 77f, -21f));
            return panel;
        }

        private RectTransform CreateBorderedPanel(string name, Vector2 size, Vector2 position)
        {
            CreateImage(name + "Shadow", _content, new Color(0f, 0f, 0f, 0.62f),
                size + new Vector2(6f, 6f), position + new Vector2(3f, -4f));
            RectTransform border = CreateImage(name, _content, BorderColor, size, position);
            RectTransform surface = CreateImage("Surface", border, PanelColor, Vector2.zero, Vector2.zero, stretch: true);
            surface.offsetMin = new Vector2(2f, 2f);
            surface.offsetMax = new Vector2(-2f, -2f);
            return border;
        }

        private static void RenderEmptyState(Transform parent, string message)
        {
            float width = parent is RectTransform rect && rect.rect.width > 0f
                ? Mathf.Max(160f, rect.rect.width - 60f)
                : 860f;
            CreateText("Empty", parent, message, 16, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(width, 80f), new Vector2(0f, -5f), MutedTextColor);
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
            Color color)
        {
            RectTransform rect = CreateRect(name, parent, size, position);
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
            text = CreateText("Label", rect, label, 17, FontStyle.Bold, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.zero, PrimaryTextColor);
            Stretch(text.rectTransform);
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
