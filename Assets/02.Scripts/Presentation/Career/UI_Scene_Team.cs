using System;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Career;
using Baseball.Game.Manager;
using Baseball.Presentation.SharedScreens;
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
        private static readonly Color BackgroundColor = CareerUiTheme.Background;
        private static readonly Color TopBarColor = CareerUiTheme.TopBar;
        private static readonly Color PanelColor = CareerUiTheme.Panel;
        private static readonly Color PanelDarkColor = CareerUiTheme.PanelDark;
        private static readonly Color BorderColor = CareerUiTheme.Border;
        private static readonly Color DividerColor = CareerUiTheme.Divider;
        private static readonly Color AccentColor = CareerUiTheme.Primary;
        private static readonly Color RoleColor = CareerUiTheme.Success;
        private static readonly Color GoldColor = CareerUiTheme.AccentGold;
        private static readonly Color WarningColor = CareerUiTheme.Warning;
        private static readonly Color PrimaryTextColor = CareerUiTheme.TextPrimary;
        private static readonly Color SecondaryTextColor = CareerUiTheme.TextSecondary;
        private static readonly Color MutedColor = CareerUiTheme.TextMuted;
        private static readonly Vector4 TeamFramePadding = new(20f, 52f, 20f, 68f);
        private static readonly Vector2 SharedShellWorkspaceOffset = new(
            0f,
            -(CareerUiTheme.SharedShellChromeHeight * 0.5f + CareerUiTheme.Space2));

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
            _content = CreateRect("Content", root, new Vector2(1920f, 1080f), SharedShellWorkspaceOffset);
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
            TeamOverviewSnapshot snapshot = CareerTeamOverviewSnapshotAdapter.Create(view);

            ClearChildren(_content);
            RenderBackgroundAccents();
            RenderClubSummary(view);
            RenderSharedRoster(snapshot.Roster, view);
            RenderUsagePlan(view);
            RenderCompetition(view);
            RenderClubBriefing(view);
            RenderPolicy(view);
        }

        private void RenderSharedRoster(ReadOnlyRosterModel roster, TeamOverviewView source)
        {
            RectTransform panel = CreatePanel(
                "Roster", $"로스터 명단  {source.Roster.Length}명",
                new Vector2(650f, 558f), new Vector2(-165f, 166f));
            RenderRosterFilters(panel, source);

            ReadOnlyRosterModel visibleRoster = _rosterFilter switch
            {
                RosterFilter.Batter => roster.FilterByKind(RosterPlayerKind.Batter),
                RosterFilter.Pitcher => roster.FilterByKind(RosterPlayerKind.Pitcher),
                _ => roster
            };
            RectTransform host = CreateRect(
                "SharedReadOnlyRosterHost", panel, new Vector2(600f, 354f), new Vector2(0f, -38f));
            ReadOnlyRosterListView rosterView = ReadOnlyRosterListView.CreateRuntime(host);
            rosterView.Bind(visibleRoster);
            rosterView.PlayerSelected += playerId =>
            {
                if (!int.TryParse(playerId, out int parsedPlayerId))
                    return;
                TeamRosterPlayerView selectedPlayer = FindPlayer(source, parsedPlayerId);
                _selectedPosition = selectedPlayer.Position;
                Render();
            };

            CreateText(
                "RosterGuide", panel,
                "선수 선택 시 아래 포지션 경쟁 현황으로 이동 · 편성은 감독 AI 소유",
                12, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(590f, 25f), new Vector2(0f, -218f), MutedColor);
        }

        private void RenderBackgroundAccents()
        {
            CreateImage("TopGlow", _content, CareerUiTheme.TopGlow,
                new Vector2(1920f, 5f), new Vector2(0f, 456f));
            CreateImage("BottomGlow", _content, CareerUiTheme.BottomGlow,
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
                "ClubSummary", "구단 현황", new Vector2(440f, 558f), new Vector2(-720f, 166f));
            Color teamColor = ToColor(view.PrimaryColor);
            RectTransform badge = CreateImage("TeamBadge", panel, Color.Lerp(teamColor, BorderColor, 0.34f),
                new Vector2(154f, 154f), new Vector2(-112f, 118f));
            RectTransform badgeInner = CreateImage("BadgeInner", badge, new Color(0.012f, 0.08f, 0.13f, 1f),
                new Vector2(142f, 142f), Vector2.zero);
            RectTransform emblem = CreateImage(
                "Emblem", badgeInner, Color.clear, new Vector2(132f, 132f), Vector2.zero);
            bool hasEmblem = TeamEmblemSprites.TryApply(emblem.GetComponent<Image>(), view.EmblemId);
            CreateImage("TeamColor", badgeInner, teamColor, new Vector2(126f, 5f), new Vector2(0f, 64f));
            if (!hasEmblem)
            {
                Text monogram = CreateText(
                    "Monogram", badgeInner, CareerTeamNameFormatter.GetMonogram(view.TeamName), 43, FontStyle.Bold,
                    TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, PrimaryTextColor, stretch: true);
                AddTextOutline(monogram, teamColor, 1.4f);
            }
            CreateText("TeamName", panel, view.TeamName, 28, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(210f, 48f), new Vector2(88f, 146f), PrimaryTextColor);
            CreateText("Season", panel, $"{view.SeasonYear} 시즌 성적", 13, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(210f, 25f), new Vector2(88f, 108f), SecondaryTextColor);
            CreateText("Record", panel, $"{view.Wins}승 {view.Losses}패 {view.Ties}무 / {view.TeamRank}위",
                22, FontStyle.Bold, TextAnchor.MiddleLeft, new Vector2(210f, 39f), new Vector2(88f, 75f), GoldColor);
            CreateText("RunDiff", panel, $"득실차  {FormatSigned(view.RunsScored - view.RunsAllowed)}",
                14, FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(210f, 25f), new Vector2(88f, 45f), SecondaryTextColor);
            CreateImage("SummaryDivider", panel, DividerColor, new Vector2(400f, 1f), new Vector2(0f, 22f));
            RenderRatingRow(panel, "야수층", view.FieldPlayerOverall, 4f);
            RenderRatingRow(panel, "선발진", view.StartingPitcherOverall, -40f);
            RenderRatingRow(panel, "불펜층", view.ReliefPitcherOverall, -84f);
            RenderRatingRow(panel, "육성", view.Archetype.Development, -128f);
            RenderRatingRow(panel, "재정", view.Archetype.Budget, -172f);
        }

        private static void RenderRatingRow(Transform parent, string label, int rating, float y)
        {
            CreateText("Label_" + label, parent, label, 15, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(72f, 28f), new Vector2(-164f, y), SecondaryTextColor);
            CreateProgressBar(parent, rating / 100f, new Vector2(210f, 13f), new Vector2(-4f, y),
                GetRatingColor(rating));
            CreateText("Grade_" + label, parent, GetRatingGrade(rating), 19, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(55f, 30f), new Vector2(160f, y), GetRatingColor(rating));
        }

        private void RenderUsagePlan(TeamOverviewView view)
        {
            string title = view.HasNextGamePlan ? $"다음 경기 기용 계획 · {view.NextGameRound}R" : "기용 현황";
            RectTransform panel = CreatePanel("Usage", title,
                new Vector2(700f, 558f), new Vector2(525f, 166f));
            RectTransform lineup = CreateSection("Lineup", panel, new Vector2(376f, 400f),
                new Vector2(-142f, -18f), PanelDarkColor);
            CreateText("Title", lineup, "선발 라인업", 16, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(190f, 30f), new Vector2(-55f, 170f), PrimaryTextColor);
            CreateText("Stat", lineup, "타율", 11, FontStyle.Bold, TextAnchor.MiddleRight,
                new Vector2(46f, 25f), new Vector2(137f, 170f), MutedColor);
            for (int index = 0; index < view.StartingLineup.Length; index++)
                RenderLineupRow(lineup, view.StartingLineup[index], index);
            RectTransform rotation = CreateSection("Rotation", panel, new Vector2(260f, 196f),
                new Vector2(195f, 91f), PanelDarkColor);
            RenderPitchingGroup(rotation, "선발 로테이션", view.StartingRotation);
            RectTransform bullpen = CreateSection("Bullpen", panel, new Vector2(260f, 196f),
                new Vector2(195f, -119f), PanelDarkColor);
            RenderPitchingGroup(bullpen, "불펜", view.Bullpen);
        }

        private static void RenderLineupRow(Transform parent, TeamLineupSlotView slot, int index)
        {
            float y = 143f - index * 37f;
            Color rowColor = slot.Player.IsMyPlayer
                ? new Color(0.025f, 0.22f, 0.42f, 1f)
                : index % 2 == 0 ? new Color(0.018f, 0.075f, 0.12f, 1f) : new Color(0.012f, 0.052f, 0.086f, 1f);
            RectTransform row = CreateImage("Lineup_" + slot.BattingOrder, parent, rowColor,
                new Vector2(316f, 35f), new Vector2(0f, y));
            CreateText("Order", row, slot.BattingOrder.ToString(), 13, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(28f, 30f), new Vector2(-142f, 0f), SecondaryTextColor);
            CreateText("Position", row, GetPositionCode(slot.Position), 13, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(40f, 30f), new Vector2(-106f, 0f), AccentColor);
            CreateText("Player", row, (slot.Player.IsMyPlayer ? "★ " : string.Empty) + slot.Player.Name,
                14, slot.Player.IsMyPlayer ? FontStyle.Bold : FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(154f, 30f), new Vector2(-5f, 0f), PrimaryTextColor);
            CreateText("Average", row, slot.Player.HasBattingRecord ? slot.Player.BattingAverage.ToString(".000") : "—",
                13, FontStyle.Bold, TextAnchor.MiddleRight, new Vector2(56f, 30f), new Vector2(130f, 0f),
                slot.Player.HasBattingRecord ? SecondaryTextColor : MutedColor);
        }

        private static void RenderPitchingGroup(Transform parent, string title, TeamRosterPlayerView[] players)
        {
            CreateText("Title", parent, title, 16, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(144f, 29f), new Vector2(-23f, 68f), PrimaryTextColor);
            CreateText("Stat", parent, "평균자책", 11, FontStyle.Bold, TextAnchor.MiddleRight,
                new Vector2(44f, 25f), new Vector2(91f, 68f), MutedColor);
            int visibleCount = Math.Min(players.Length, 4);
            for (int index = 0; index < visibleCount; index++)
            {
                TeamRosterPlayerView player = players[index];
                float y = 32f - index * 32f;
                Color background = player.IsInNextGamePlan
                    ? new Color(0.03f, 0.19f, 0.28f, 1f)
                    : new Color(0.014f, 0.06f, 0.1f, 1f);
                RectTransform row = CreateImage("Pitcher_" + player.PlayerId, parent, background,
                    new Vector2(220f, 30f), new Vector2(0f, y));
                CreateText("Marker", row, player.IsInNextGamePlan ? "●" : "○", 12, FontStyle.Bold,
                    TextAnchor.MiddleCenter, new Vector2(22f, 26f), new Vector2(-88f, 0f),
                    player.IsInNextGamePlan ? RoleColor : MutedColor);
                CreateText("Name", row, (player.IsMyPlayer ? "★ " : string.Empty) + player.Name,
                    13, player.IsMyPlayer ? FontStyle.Bold : FontStyle.Normal, TextAnchor.MiddleLeft,
                    new Vector2(112f, 26f), new Vector2(-21f, 0f), PrimaryTextColor);
                CreateText("Era", row, player.HasPitchingRecord ? player.EarnedRunAverage.ToString("0.00") : "—",
                    13, FontStyle.Bold, TextAnchor.MiddleRight, new Vector2(48f, 26f), new Vector2(84f, 0f),
                    SecondaryTextColor);
            }
        }

        private void RenderCompetition(TeamOverviewView view)
        {
            RectTransform panel = CreatePanel("Competition", "포지션 경쟁 현황",
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
                new Vector2(440f, 22f), new Vector2(0f, -78f), MutedColor);
        }

        private void RenderPositionTabs(Transform panel)
        {
            for (int index = 0; index < CompetitionPositions.Length; index++)
            {
                PlayerPosition position = CompetitionPositions[index];
                bool selected = position == _selectedPosition;
                Button button = CreateButton("Position_" + position, panel, GetPositionCode(position),
                    new Vector2(39f, 30f), new Vector2(-210f + index * 42f, 72f),
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
            float y = 32f - index * 38f;
            Color background = player.IsMyPlayer ? new Color(0.025f, 0.22f, 0.42f, 1f) : PanelDarkColor;
            RectTransform row = CreateImage("Depth_" + player.PlayerId, parent, background,
                new Vector2(456f, 34f), new Vector2(0f, y));
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
            RectTransform panel = CreatePanel("Briefing", "최근 구단 브리핑",
                new Vector2(620f, 300f), new Vector2(-115f, -288f));
            int positionCount = CountPlayersAtPosition(view, view.MyPlayerPosition);
            TeamRosterPlayerView myPlayer = FindPlayer(view, view.MyPlayerId);
            RenderBriefingRow(panel, "TEAM", $"{view.TeamRank}위 · {view.Wins}승 {view.Losses}패 {view.Ties}무",
                $"득실차 {FormatSigned(view.RunsScored - view.RunsAllowed)}", 56f, AccentColor);
            RenderBriefingRow(panel, "ROLE", GetPlannedRoleLabel(
                    view.PlannedPlayerRole, view.MyPlayerPosition, view.MyPlayerBattingOrder),
                view.HasNextGamePlan ? $"다음 경기 {view.NextGameRound}R" : "일정 종료", 16f,
                view.HasNextGamePlan ? RoleColor : GoldColor);
            RenderBriefingRow(panel, "DEPTH", $"{GetPositionCode(view.MyPlayerPosition)} 경쟁 {positionCount}명 · 내 OVR {myPlayer.Overall}",
                GetRosterRoleLabel(myPlayer, view.MyPlayerExpectedRole), -24f, GoldColor);
            RenderBriefingRow(panel, "ROSTER", $"등록 선수 {view.Roster.Length}명 · 야수층 OVR {view.FieldPlayerOverall}",
                "열람 전용", -64f, SecondaryTextColor);
        }

        private static void RenderBriefingRow(
            Transform parent, string tag, string message, string meta, float y, Color accent)
        {
            RectTransform row = CreateImage("Briefing_" + tag, parent, PanelDarkColor,
                new Vector2(560f, 36f), new Vector2(0f, y));
            CreateImage("Accent", row, accent, new Vector2(4f, 30f), new Vector2(-276f, 0f));
            CreateText("Tag", row, tag, 10, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(56f, 26f), new Vector2(-240f, 0f), accent);
            CreateText("Message", row, message, 14, FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(340f, 30f), new Vector2(-25f, 0f), PrimaryTextColor);
            CreateText("Meta", row, meta, 11, FontStyle.Normal, TextAnchor.MiddleRight,
                new Vector2(95f, 26f), new Vector2(220f, 0f), MutedColor);
        }

        private void RenderPolicy(TeamOverviewView view)
        {
            RectTransform panel = CreatePanel("Policy", "트레이드 시장",
                new Vector2(680f, 300f), new Vector2(550f, -288f));
            string interestText = view.TradeInterests.Length == 0
                ? "관심 구단 없음"
                : $"{view.TopTradeInterestTeamName} 외 {view.TradeInterests.Length - 1}개 구단";
            if (view.TradeInterests.Length == 1)
                interestText = view.TopTradeInterestTeamName;
            CreatePolicySummary(panel, "현재 태도", GetTradePreferenceLabel(view.TradePreference), 72f);
            CreatePolicySummary(panel, "시장 상태", interestText, 40f);
            CreateText(
                "Deadline", panel,
                $"마감 {view.TradeDeadlineGameIndex}경기 · 현재 {view.CurrentTeamGameIndex}경기" +
                (view.IsOnTradeBlock ? " · 트레이드 블록" : string.Empty),
                12, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(600f, 22f), new Vector2(0f, 10f),
                view.IsOnTradeBlock ? WarningColor : SecondaryTextColor);
            RenderTradePreferenceButton(panel, view, TradePreference.PreferToStay, "잔류 선호", -225f, -28f);
            RenderTradePreferenceButton(panel, view, TradePreference.Neutral, "중립", -75f, -28f);
            RenderTradePreferenceButton(panel, view, TradePreference.OpenToTrade, "이적 가능", 75f, -28f);
            RenderTradePreferenceButton(panel, view, TradePreference.RequestTrade, "이적 요청", 225f, -28f);
            string guide = view.TradeInterests.Length > 0
                ? $"관심 단계: {GetTradeStageLabel(view.TradeInterests[0].Stage)} · 예상 출장 {view.TradeInterests[0].ProjectedPlayingTime:P0}"
                : "태도는 거래 가능성에 영향을 주지만 일반 계약에는 트레이드 거부권이 없습니다.";
            CreateText("TradeGuide", panel, guide, 11, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(600f, 24f), new Vector2(0f, -70f), MutedColor);
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
            Button button = CreateFramedButton(
                "TradePreference_" + preference,
                parent,
                label,
                new Vector2(135f, 36f),
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
                new Vector2(450f, 28f), new Vector2(72f, y));
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

        private RectTransform CreatePanel(string name, string title, Vector2 size, Vector2 position)
        {
            RectTransform panel = CreateRect(name, _content, size, position);
            RectTransform decorativeFrame = CreateImage(
                "DecorativeFrame", panel, Color.white, Vector2.zero, Vector2.zero, stretch: true);
            MarkVisual(decorativeFrame, CareerUiVisualRole.DecorativeFrame);
            RectTransform content = CreateRect("ContentSafeArea", panel, size, Vector2.zero);
            RectTransform interaction = CreateRect("InteractionRoot", panel, size, Vector2.zero);
            CareerUiFrame.ApplyContentPadding(content, size, TeamFramePadding);
            CareerUiFrame.ApplyContentPadding(interaction, size, TeamFramePadding);
            content.gameObject.AddComponent<RectMask2D>();
            RectTransform header = CreateRect("HeaderRoot", panel, new Vector2(size.x - 72f, 48f),
                new Vector2(0f, size.y * 0.5f - 54f));
            CreateText("Heading", header, title, 20, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(size.x * 0.68f, 32f), Vector2.zero, PrimaryTextColor);
            CareerUiFrame frame = panel.gameObject.AddComponent<CareerUiFrame>();
            frame.Initialize(
                decorativeFrame.GetComponent<Image>(), header, content, interaction,
                TeamFramePadding, false);
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
