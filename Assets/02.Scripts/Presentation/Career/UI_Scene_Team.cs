using System;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Career;
using Baseball.Game.Manager;
using Baseball.Presentation.UI;
using Baseball.Simulation.Career;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Career
{
    /// <summary>
    /// 로스터·다음 경기 기용·포지션 경쟁·구단 정책을 열람하는 구단 화면이다.
    /// </summary>
    public sealed partial class UI_Scene_Team : UISceneBase, ICareerTabScreen
    {
        private static readonly Color BackgroundColor = new(0.006f, 0.02f, 0.034f, 1f);
        private static readonly Color TopBarColor = new(0.008f, 0.027f, 0.052f, 1f);
        private static readonly Color PanelColor = new(0.018f, 0.065f, 0.108f, 0.99f);
        private static readonly Color PanelDarkColor = new(0.009f, 0.035f, 0.061f, 0.99f);
        private static readonly Color BorderColor = new(0.28f, 0.46f, 0.62f, 1f);
        private static readonly Color DividerColor = new(0.14f, 0.31f, 0.45f, 1f);
        private static readonly Color AccentColor = new(0.13f, 0.55f, 0.92f, 1f);
        private static readonly Color RoleColor = new(0.27f, 0.77f, 0.47f, 1f);
        private static readonly Color GoldColor = new(0.95f, 0.69f, 0.22f, 1f);
        private static readonly Color WarningColor = new(0.94f, 0.56f, 0.16f, 1f);
        private static readonly Color PrimaryTextColor = new(0.94f, 0.97f, 1f, 1f);
        private static readonly Color SecondaryTextColor = new(0.62f, 0.71f, 0.8f, 1f);
        private static readonly Color MutedColor = new(0.34f, 0.40f, 0.49f, 1f);

        private static readonly PlayerPosition[] CompetitionPositions =
        {
            PlayerPosition.Catcher,
            PlayerPosition.FirstBase,
            PlayerPosition.SecondBase,
            PlayerPosition.ThirdBase,
            PlayerPosition.Shortstop,
            PlayerPosition.LeftField,
            PlayerPosition.CenterField,
            PlayerPosition.RightField,
            PlayerPosition.DesignatedHitter,
            PlayerPosition.StartingPitcher,
            PlayerPosition.ReliefPitcher
        };

        private CareerManager _manager;
        private RectTransform _content;
        private RosterFilter _rosterFilter;
        private PlayerPosition _selectedPosition;
        private int _renderedPlayerId;

        public override bool BlocksLowerInput => true;
        public CareerMainTab MainTab => CareerMainTab.Team;

        /// <summary>
        /// 프리팹이 없는 프로토타입 환경에서 구단 화면을 런타임 생성한다.
        /// </summary>
        public static UI_Scene_Team CreateRuntime(Transform parent)
        {
            var screenObject = new GameObject(nameof(UI_Scene_Team), typeof(RectTransform), typeof(CanvasGroup));
            screenObject.transform.SetParent(parent, false);
            UI_Scene_Team screen = screenObject.AddComponent<UI_Scene_Team>();
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

            CareerDashboardView dashboard = _manager.Dashboard;
            TeamOverviewView view = _manager.TeamOverview;
            if (dashboard == null || view == null)
                return;
            if (_renderedPlayerId != view.MyPlayerId)
            {
                _selectedPosition = view.MyPlayerPosition;
                _renderedPlayerId = view.MyPlayerId;
            }

            ClearChildren(_content);
            RenderBackgroundAccents();
            RenderTopBar(dashboard, view);
            RenderClubSummary(view);
            RenderRoster(view);
            RenderUsagePlan(view);
            RenderCompetition(view);
            RenderClubBriefing(view);
            RenderPolicy(view);
            CareerNavigationChrome.Create(_content, CareerMainTab.Team);
        }

        private void RenderBackgroundAccents()
        {
            CreateImage("TopGlow", _content, new Color(0.02f, 0.18f, 0.31f, 0.24f),
                new Vector2(1920f, 5f), new Vector2(0f, 456f));
            CreateImage("BottomGlow", _content, new Color(0.02f, 0.16f, 0.28f, 0.2f),
                new Vector2(1920f, 4f), new Vector2(0f, -443f));
        }

        private void RenderTopBar(CareerDashboardView dashboard, TeamOverviewView view)
        {
            RectTransform bar = CreateImage(
                "TopBar", _content, TopBarColor, new Vector2(1920f, 80f), new Vector2(0f, 500f));
            CreateImage("TopBarBottom", bar, BorderColor, new Vector2(1920f, 2f), new Vector2(0f, -39f));
            Text logo = CreateText(
                "Logo", bar, "UPlayBall", 34, FontStyle.BoldAndItalic, TextAnchor.MiddleLeft,
                new Vector2(310f, 50f), new Vector2(-800f, 5f), PrimaryTextColor);
            AddTextOutline(logo, new Color(0.05f, 0.34f, 0.62f, 0.9f), 1.5f);
            CreateText("LogoCaption", bar, "BASEBALL CAREER", 10, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(230f, 18f), new Vector2(-796f, -23f), AccentColor);
            CreateTopBarSegment(
                bar, "LEAGUE", $"{view.SeasonYear}  {GetLeagueLabel(view.LeagueLevel)} LEAGUE",
                new Vector2(-365f, 0f), new Vector2(420f, 64f));
            string dateText = dashboard.NextGame.HasValue
                ? $"{dashboard.NextGame.Value.Date:M월 d일} ({GetKoreanDayOfWeek(dashboard.NextGame.Value.Date.DayOfWeek)})"
                : "시즌 일정 종료";
            CreateTopBarSegment(bar, "DATE", dateText, new Vector2(25f, 0f), new Vector2(300f, 64f));
            CreateTopBarSegment(
                bar, "MONEY", FormatMoney(dashboard.AvailableMoney), new Vector2(390f, 0f), new Vector2(370f, 64f));
            CreateText("ReadOnly", bar, "구단 정보 · 커리어 이동", 13, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(190f, 44f), new Vector2(700f, 0f), SecondaryTextColor);
        }

        private static void CreateTopBarSegment(
            Transform parent, string eyebrow, string value, Vector2 position, Vector2 size)
        {
            RectTransform segment = CreateRect(eyebrow + "Segment", parent, size, position);
            CreateImage("LeftDivider", segment, DividerColor, new Vector2(2f, size.y - 10f),
                new Vector2(-size.x * 0.5f + 1f, 0f));
            CreateText("Eyebrow", segment, eyebrow, 10, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(size.x - 42f, 18f), new Vector2(14f, 15f), AccentColor);
            CreateText("Value", segment, value, 20, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(size.x - 42f, 31f), new Vector2(14f, -8f), PrimaryTextColor);
        }

        private void RenderClubSummary(TeamOverviewView view)
        {
            RectTransform panel = CreatePanel(
                "ClubSummary", "CLUB PROFILE", "구단 현황", new Vector2(440f, 558f), new Vector2(-720f, 166f));
            Color teamColor = ToColor(view.PrimaryColor);
            RectTransform badge = CreateImage("TeamBadge", panel, Color.Lerp(teamColor, BorderColor, 0.34f),
                new Vector2(154f, 154f), new Vector2(-112f, 118f));
            RectTransform badgeInner = CreateImage("BadgeInner", badge, new Color(0.012f, 0.08f, 0.13f, 1f),
                new Vector2(142f, 142f), Vector2.zero);
            CreateImage("TeamColor", badgeInner, teamColor, new Vector2(126f, 5f), new Vector2(0f, 64f));
            Text monogram = CreateText(
                "Monogram", badgeInner, CareerTeamNameFormatter.GetMonogram(view.TeamName), 43, FontStyle.Bold,
                TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, PrimaryTextColor, stretch: true);
            AddTextOutline(monogram, teamColor, 1.4f);
            CreateText("TeamName", panel, view.TeamName, 28, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(226f, 48f), new Vector2(95f, 146f), PrimaryTextColor);
            CreateText("Season", panel, $"{view.SeasonYear} 시즌 성적", 13, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(226f, 25f), new Vector2(95f, 108f), SecondaryTextColor);
            CreateText("Record", panel, $"{view.Wins}승 {view.Losses}패 {view.Ties}무 / {view.TeamRank}위",
                22, FontStyle.Bold, TextAnchor.MiddleLeft, new Vector2(226f, 39f), new Vector2(95f, 75f), GoldColor);
            CreateText("RunDiff", panel, $"득실차  {FormatSigned(view.RunsScored - view.RunsAllowed)}",
                14, FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(226f, 25f), new Vector2(95f, 45f), SecondaryTextColor);
            CreateImage("SummaryDivider", panel, DividerColor, new Vector2(400f, 1f), new Vector2(0f, 22f));
            RenderRatingRow(panel, "야수층", view.FieldPlayerOverall, 0f);
            RenderRatingRow(panel, "선발진", view.StartingPitcherOverall, -55f);
            RenderRatingRow(panel, "불펜층", view.ReliefPitcherOverall, -110f);
            RenderRatingRow(panel, "육성", view.Archetype.Development, -165f);
            RenderRatingRow(panel, "재정", view.Archetype.Budget, -220f);
        }

        private static void RenderRatingRow(Transform parent, string label, int rating, float y)
        {
            CreateText("Label_" + label, parent, label, 15, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(72f, 28f), new Vector2(-170f, y), SecondaryTextColor);
            CreateProgressBar(parent, rating / 100f, new Vector2(224f, 13f), new Vector2(-6f, y),
                GetRatingColor(rating));
            CreateText("Grade_" + label, parent, GetRatingGrade(rating), 19, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(55f, 30f), new Vector2(169f, y), GetRatingColor(rating));
        }

        private void RenderUsagePlan(TeamOverviewView view)
        {
            string title = view.HasNextGamePlan ? $"다음 경기 기용 계획 · {view.NextGameRound}R" : "기용 현황";
            RectTransform panel = CreatePanel("Usage", "MANAGER USAGE", title,
                new Vector2(700f, 558f), new Vector2(525f, 166f));
            RectTransform lineup = CreateSection("Lineup", panel, new Vector2(376f, 448f),
                new Vector2(-145f, -19f), PanelDarkColor);
            CreateText("Title", lineup, "선발 라인업", 16, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(210f, 30f), new Vector2(-70f, 199f), PrimaryTextColor);
            CreateText("Stat", lineup, "타율", 11, FontStyle.Bold, TextAnchor.MiddleRight,
                new Vector2(60f, 25f), new Vector2(145f, 199f), MutedColor);
            for (int index = 0; index < view.StartingLineup.Length; index++)
                RenderLineupRow(lineup, view.StartingLineup[index], index);
            RectTransform rotation = CreateSection("Rotation", panel, new Vector2(270f, 214f),
                new Vector2(195f, 98f), PanelDarkColor);
            RenderPitchingGroup(rotation, "선발 로테이션", view.StartingRotation);
            RectTransform bullpen = CreateSection("Bullpen", panel, new Vector2(270f, 214f),
                new Vector2(195f, -136f), PanelDarkColor);
            RenderPitchingGroup(bullpen, "불펜", view.Bullpen);
        }

        private static void RenderLineupRow(Transform parent, TeamLineupSlotView slot, int index)
        {
            float y = 157f - index * 39f;
            Color rowColor = slot.Player.IsMyPlayer
                ? new Color(0.025f, 0.22f, 0.42f, 1f)
                : index % 2 == 0 ? new Color(0.018f, 0.075f, 0.12f, 1f) : new Color(0.012f, 0.052f, 0.086f, 1f);
            RectTransform row = CreateImage("Lineup_" + slot.BattingOrder, parent, rowColor,
                new Vector2(354f, 35f), new Vector2(0f, y));
            CreateText("Order", row, slot.BattingOrder.ToString(), 13, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(30f, 30f), new Vector2(-157f, 0f), SecondaryTextColor);
            CreateText("Position", row, GetPositionCode(slot.Position), 13, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(45f, 30f), new Vector2(-119f, 0f), AccentColor);
            CreateText("Player", row, (slot.Player.IsMyPlayer ? "★ " : string.Empty) + slot.Player.Name,
                14, slot.Player.IsMyPlayer ? FontStyle.Bold : FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(166f, 30f), new Vector2(-5f, 0f), PrimaryTextColor);
            CreateText("Average", row, slot.Player.HasBattingRecord ? slot.Player.BattingAverage.ToString(".000") : "—",
                13, FontStyle.Bold, TextAnchor.MiddleRight, new Vector2(65f, 30f), new Vector2(139f, 0f),
                slot.Player.HasBattingRecord ? SecondaryTextColor : MutedColor);
        }

        private static void RenderPitchingGroup(Transform parent, string title, TeamRosterPlayerView[] players)
        {
            CreateText("Title", parent, title, 16, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(172f, 29f), new Vector2(-38f, 83f), PrimaryTextColor);
            CreateText("Stat", parent, "평균자책", 11, FontStyle.Bold, TextAnchor.MiddleRight,
                new Vector2(48f, 25f), new Vector2(103f, 83f), MutedColor);
            int visibleCount = Math.Min(players.Length, 4);
            for (int index = 0; index < visibleCount; index++)
            {
                TeamRosterPlayerView player = players[index];
                float y = 45f - index * 38f;
                Color background = player.IsInNextGamePlan
                    ? new Color(0.03f, 0.19f, 0.28f, 1f)
                    : new Color(0.014f, 0.06f, 0.1f, 1f);
                RectTransform row = CreateImage("Pitcher_" + player.PlayerId, parent, background,
                    new Vector2(250f, 34f), new Vector2(0f, y));
                CreateText("Marker", row, player.IsInNextGamePlan ? "●" : "○", 12, FontStyle.Bold,
                    TextAnchor.MiddleCenter, new Vector2(25f, 28f), new Vector2(-110f, 0f),
                    player.IsInNextGamePlan ? RoleColor : MutedColor);
                CreateText("Name", row, (player.IsMyPlayer ? "★ " : string.Empty) + player.Name,
                    13, player.IsMyPlayer ? FontStyle.Bold : FontStyle.Normal, TextAnchor.MiddleLeft,
                    new Vector2(132f, 28f), new Vector2(-33f, 0f), PrimaryTextColor);
                CreateText("Era", row, player.HasPitchingRecord ? player.EarnedRunAverage.ToString("0.00") : "—",
                    13, FontStyle.Bold, TextAnchor.MiddleRight, new Vector2(60f, 28f), new Vector2(93f, 0f),
                    SecondaryTextColor);
            }
        }

        private void RenderCompetition(TeamOverviewView view)
        {
            RectTransform panel = CreatePanel("Competition", "POSITION DEPTH", "포지션 경쟁 현황",
                new Vector2(500f, 300f), new Vector2(-690f, -288f));
            RenderPositionTabs(panel);
            int rowIndex = 0;
            for (int index = 0; index < view.Roster.Length && rowIndex < 3; index++)
            {
                TeamRosterPlayerView player = view.Roster[index];
                if (player.Position != _selectedPosition)
                    continue;
                RenderCompetitionRow(panel, player, view.MyPlayerExpectedRole, rowIndex++);
            }
            CreateText("Guide", panel, "OVR만이 아니라 계약 역할·컨디션·감독 평가가 실제 기용에 반영됩니다.",
                11, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(456f, 31f), new Vector2(0f, -126f), MutedColor);
        }

        private void RenderPositionTabs(Transform panel)
        {
            for (int index = 0; index < CompetitionPositions.Length; index++)
            {
                PlayerPosition position = CompetitionPositions[index];
                bool selected = position == _selectedPosition;
                Button button = CreateButton("Position_" + position, panel, GetPositionCode(position),
                    new Vector2(39f, 30f), new Vector2(-210f + index * 42f, 83f),
                    selected ? new Color(0.025f, 0.28f, 0.54f, 1f) : PanelDarkColor, out Text label);
                label.fontSize = 12;
                label.color = selected ? PrimaryTextColor : SecondaryTextColor;
                button.onClick.AddListener(() =>
                {
                    _selectedPosition = position;
                    Render();
                });
            }
        }

        private static void RenderCompetitionRow(
            Transform parent,
            TeamRosterPlayerView player,
            ExpectedRole myPlayerExpectedRole,
            int index)
        {
            float y = 38f - index * 48f;
            Color background = player.IsMyPlayer ? new Color(0.025f, 0.22f, 0.42f, 1f) : PanelDarkColor;
            RectTransform row = CreateImage("Depth_" + player.PlayerId, parent, background,
                new Vector2(456f, 42f), new Vector2(0f, y));
            CreateText("Marker", row, player.IsMyPlayer ? "★" : player.IsInNextGamePlan ? "●" : "○",
                14, FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(28f, 32f), new Vector2(-207f, 0f),
                player.IsMyPlayer ? GoldColor : player.IsInNextGamePlan ? RoleColor : MutedColor);
            CreateText("Name", row, player.Name, 15, player.IsMyPlayer ? FontStyle.Bold : FontStyle.Normal,
                TextAnchor.MiddleLeft, new Vector2(190f, 32f), new Vector2(-85f, 0f), PrimaryTextColor);
            CreateText("Overall", row, $"OVR {player.Overall}", 14, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(82f, 32f), new Vector2(68f, 0f), GetRatingColor(player.Overall));
            CreateText("Role", row, GetRosterRoleLabel(player, myPlayerExpectedRole), 13, FontStyle.Bold,
                TextAnchor.MiddleRight, new Vector2(105f, 32f), new Vector2(170f, 0f),
                player.IsMyPlayer ? GoldColor : player.IsInNextGamePlan ? RoleColor : SecondaryTextColor);
        }

        private void RenderClubBriefing(TeamOverviewView view)
        {
            RectTransform panel = CreatePanel("Briefing", "CLUB BRIEFING", "최근 구단 브리핑",
                new Vector2(620f, 300f), new Vector2(-115f, -288f));
            int positionCount = CountPlayersAtPosition(view, view.MyPlayerPosition);
            TeamRosterPlayerView myPlayer = FindPlayer(view, view.MyPlayerId);
            RenderBriefingRow(panel, "TEAM", $"{view.TeamRank}위 · {view.Wins}승 {view.Losses}패 {view.Ties}무",
                $"득실차 {FormatSigned(view.RunsScored - view.RunsAllowed)}", 73f, AccentColor);
            RenderBriefingRow(panel, "ROLE", GetPlannedRoleLabel(
                    view.PlannedPlayerRole, view.MyPlayerPosition, view.MyPlayerBattingOrder),
                view.HasNextGamePlan ? $"다음 경기 {view.NextGameRound}R" : "일정 종료", 17f,
                view.HasNextGamePlan ? RoleColor : GoldColor);
            RenderBriefingRow(panel, "DEPTH", $"{GetPositionCode(view.MyPlayerPosition)} 경쟁 {positionCount}명 · 내 OVR {myPlayer.Overall}",
                GetRosterRoleLabel(myPlayer, view.MyPlayerExpectedRole), -39f, GoldColor);
            RenderBriefingRow(panel, "ROSTER", $"등록 선수 {view.Roster.Length}명 · 야수층 OVR {view.FieldPlayerOverall}",
                "열람 전용", -95f, SecondaryTextColor);
        }

        private static void RenderBriefingRow(
            Transform parent, string tag, string message, string meta, float y, Color accent)
        {
            RectTransform row = CreateImage("Briefing_" + tag, parent, PanelDarkColor,
                new Vector2(578f, 48f), new Vector2(0f, y));
            CreateImage("Accent", row, accent, new Vector2(4f, 40f), new Vector2(-285f, 0f));
            CreateText("Tag", row, tag, 10, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(58f, 26f), new Vector2(-250f, 0f), accent);
            CreateText("Message", row, message, 14, FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(365f, 32f), new Vector2(-33f, 0f), PrimaryTextColor);
            CreateText("Meta", row, meta, 11, FontStyle.Normal, TextAnchor.MiddleRight,
                new Vector2(110f, 28f), new Vector2(228f, 0f), MutedColor);
        }

        private void RenderPolicy(TeamOverviewView view)
        {
            RectTransform panel = CreatePanel("Policy", "CAREER MOVEMENT", "트레이드 시장",
                new Vector2(680f, 300f), new Vector2(550f, -288f));
            string interestText = view.TradeInterests.Length == 0
                ? "관심 구단 없음"
                : $"{view.TopTradeInterestTeamName} 외 {view.TradeInterests.Length - 1}개 구단";
            if (view.TradeInterests.Length == 1)
                interestText = view.TopTradeInterestTeamName;
            CreatePolicySummary(panel, "현재 태도", GetTradePreferenceLabel(view.TradePreference), 82f);
            CreatePolicySummary(panel, "시장 상태", interestText, 39f);
            CreateText(
                "Deadline", panel,
                $"마감 {view.TradeDeadlineGameIndex}경기 · 현재 {view.CurrentTeamGameIndex}경기" +
                (view.IsOnTradeBlock ? " · 트레이드 블록" : string.Empty),
                12, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(620f, 25f), new Vector2(0f, 1f),
                view.IsOnTradeBlock ? WarningColor : SecondaryTextColor);
            RenderTradePreferenceButton(panel, view, TradePreference.PreferToStay, "잔류 선호", -240f, -51f);
            RenderTradePreferenceButton(panel, view, TradePreference.Neutral, "중립", -80f, -51f);
            RenderTradePreferenceButton(panel, view, TradePreference.OpenToTrade, "이적 가능", 80f, -51f);
            RenderTradePreferenceButton(panel, view, TradePreference.RequestTrade, "이적 요청", 240f, -51f);
            string guide = view.TradeInterests.Length > 0
                ? $"관심 단계: {GetTradeStageLabel(view.TradeInterests[0].Stage)} · 예상 출장 {view.TradeInterests[0].ProjectedPlayingTime:P0}"
                : "태도는 거래 가능성에 영향을 주지만 일반 계약에는 트레이드 거부권이 없습니다.";
            CreateText("TradeGuide", panel, guide, 11, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(620f, 38f), new Vector2(0f, -112f), MutedColor);
        }

        private void RenderTradePreferenceButton(
            Transform parent,
            TeamOverviewView view,
            TradePreference preference,
            string label,
            float x,
            float y)
        {
            bool selected = view.TradePreference == preference;
            Button button = CreateButton(
                "TradePreference_" + preference,
                parent,
                label,
                new Vector2(145f, 42f),
                new Vector2(x, y),
                selected ? new Color(0.03f, 0.28f, 0.52f, 1f) : PanelDarkColor,
                out Text text);
            text.fontSize = 13;
            button.interactable = view.CanChangeTradePreference;
            button.onClick.AddListener(() => _manager.SetTradePreference(preference));
        }

        private static string GetTradePreferenceLabel(TradePreference preference)
        {
            return preference switch
            {
                TradePreference.PreferToStay => "잔류 선호",
                TradePreference.OpenToTrade => "트레이드 가능",
                TradePreference.RequestTrade => "트레이드 요청",
                _ => "중립"
            };
        }

        private static string GetTradeStageLabel(TradeInterestStage stage)
        {
            return stage switch
            {
                TradeInterestStage.Interest => "관심",
                TradeInterestStage.Rumor => "루머",
                TradeInterestStage.Negotiating => "구단 간 협상",
                TradeInterestStage.Completed => "성사",
                _ => "무산"
            };
        }

        private static void CreatePolicySummary(Transform parent, string label, string value, float y)
        {
            CreateText("Label_" + label, parent, label, 13, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(110f, 29f), new Vector2(-247f, y), SecondaryTextColor);
            RectTransform valueBox = CreateImage("Value_" + label, parent, PanelDarkColor,
                new Vector2(475f, 32f), new Vector2(80f, y));
            CreateText("Value", valueBox, value, 15, FontStyle.Bold, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.zero, PrimaryTextColor, stretch: true);
        }

        private static void CreatePolicyBar(Transform parent, string label, int rating, float y)
        {
            CreateText("Label_" + label, parent, label, 13, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(110f, 27f), new Vector2(-247f, y), SecondaryTextColor);
            CreateProgressBar(parent, rating / 100f, new Vector2(380f, 12f), new Vector2(32f, y),
                GetRatingColor(rating));
            CreateText("Value_" + label, parent, $"{GetRatingGrade(rating)}  {rating}", 13, FontStyle.Bold,
                TextAnchor.MiddleRight, new Vector2(82f, 28f), new Vector2(273f, y), GetRatingColor(rating));
        }

        private RectTransform CreatePanel(string name, string eyebrow, string title, Vector2 size, Vector2 position)
        {
            RectTransform panel = CreateRect(name, _content, size, position);
            RectTransform decorativeFrame = CreateImage(
                "DecorativeFrame", panel, Color.white, Vector2.zero, Vector2.zero, stretch: true);
            MarkVisual(decorativeFrame, CareerUiVisualRole.DecorativeFrame);
            RectTransform content = CreateRect("ContentSafeArea", panel, size, Vector2.zero);
            RectTransform interaction = CreateRect("InteractionRoot", panel, size, Vector2.zero);
            RectTransform header = CreateRect("HeaderRoot", panel, new Vector2(size.x - 72f, 48f),
                new Vector2(0f, size.y * 0.5f - 54f));
            CreateImage("HeaderLine", header, AccentColor, new Vector2(size.x * 0.34f, 2f),
                new Vector2(-size.x * 0.29f, -21f));
            CreateText("Eyebrow", header, eyebrow, 10, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(size.x * 0.3f, 18f), new Vector2(-size.x * 0.33f, 9f), AccentColor);
            CreateText("Heading", header, title, 20, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(size.x * 0.68f, 32f), new Vector2(0f, -7f), PrimaryTextColor);
            CareerUiFrame frame = panel.gameObject.AddComponent<CareerUiFrame>();
            frame.Initialize(
                decorativeFrame.GetComponent<Image>(), header, content, interaction,
                CareerUiTheme.UniversalFramePadding, false);
            return content;
        }

        private static int CountPlayersAtPosition(TeamOverviewView view, PlayerPosition position)
        {
            int count = 0;
            for (int index = 0; index < view.Roster.Length; index++)
            {
                if (view.Roster[index].Position == position)
                    count++;
            }
            return count;
        }

        private static TeamRosterPlayerView FindPlayer(TeamOverviewView view, int playerId)
        {
            for (int index = 0; index < view.Roster.Length; index++)
            {
                if (view.Roster[index].PlayerId == playerId)
                    return view.Roster[index];
            }
            throw new InvalidOperationException($"PlayerId {playerId}를 찾을 수 없습니다.");
        }

    }
}
