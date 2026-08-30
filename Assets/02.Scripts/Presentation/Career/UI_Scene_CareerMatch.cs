using System;
using System.Collections.Generic;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Career;
using Baseball.Game.Career.Narrative;
using Baseball.Game.Manager;
using Baseball.Presentation.UI;
using Baseball.Simulation.Match;
using Baseball.Simulation.PlateAppearance;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Baseball.Presentation.Career
{
    /// <summary>
    /// 경기 준비, 내 선수 중심 진행, 경기 후 결과를 한 흐름으로 표현한다.
    /// </summary>
    public sealed partial class UI_Scene_CareerMatch : UISceneBase
    {
        /// <summary>
        /// 진행 속도 선택지. 슬라이더 대신 버튼으로 고르므로 실제로 쓰이는 배속만 남긴다.
        /// </summary>
        private static readonly float[] PlaybackSpeedRates = { 1f, 2f, 3f, 5f };
        private const int DefaultPlaybackSpeedStepIndex = 1;

        private const int TimelineRowCapacity = 8;

        /// <summary>이닝 머리글을 포함해 타임라인 패널에 들어가는 최대 줄 수다.</summary>
        private const int TimelineLineCapacity = 8;
        private const float TimelineRowHeight = 29f;
        private const int BattingOrderPreviewCount = 4;

        private static readonly Color BackgroundColor = new(0.004f, 0.018f, 0.032f, 1f);
        private static readonly Color TopBarColor = new(0.008f, 0.038f, 0.065f, 1f);
        private static readonly Color PanelColor = new(0.018f, 0.068f, 0.108f, 0.99f);
        private static readonly Color PanelDarkColor = new(0.009f, 0.035f, 0.057f, 0.99f);
        private static readonly Color CardColor = new(0.026f, 0.096f, 0.147f, 1f);
        private static readonly Color BorderColor = new(0.18f, 0.43f, 0.62f, 1f);
        private static readonly Color AccentColor = new(0.12f, 0.64f, 1f, 1f);
        private static readonly Color RoleColor = new(0.27f, 0.78f, 0.49f, 1f);
        private static readonly Color GoldColor = new(0.96f, 0.7f, 0.22f, 1f);
        private static readonly Color DangerColor = new(0.92f, 0.35f, 0.31f, 1f);
        private static readonly Color PrimaryTextColor = new(0.94f, 0.97f, 1f, 1f);
        private static readonly Color SecondaryTextColor = new(0.63f, 0.72f, 0.8f, 1f);
        private static readonly Color MutedTextColor = new(0.38f, 0.47f, 0.55f, 1f);
        private static readonly Color EmptyDotColor = new(0.13f, 0.22f, 0.28f, 1f);

        /// <summary>오른쪽 조작 패널의 크기와 위치. 재생성되지 않는 조작 계층이 같은 자리를 덮어야 하므로 상수로 둔다.</summary>
        private static readonly Vector2 ControlPanelSize = new(500f, 900f);
        private static readonly Vector2 ControlPanelPosition = new(700f, -32f);

        private CareerManager _manager;
        private RectTransform _content;
        private RectTransform _controlHost;
        private RectTransform _settingsHost;
        private BattingApproach _selectedApproach = BattingApproach.Balanced;
        private PitchingApproach _selectedPitchingApproach = PitchingApproach.Balanced;
        [SerializeField, Min(0.1f)] private float automaticPlayIntervalSeconds = 0.42f;
        [SerializeField, Min(0.5f)] private float controlledResultHoldSeconds = 2f;

        /// <summary>
        /// 내 타석 결과는 배속을 올려도 이 시간보다 짧게 지나가지 않는다.
        /// 커리어에서 가장 중요한 장면을 읽지 못한 채 넘기지 않기 위한 하한이다.
        /// </summary>
        [SerializeField, Min(0.2f)] private float minimumControlledResultHoldSeconds = 1.1f;

        [SerializeField, Min(0.2f)] private float sideChangeHoldSeconds = 0.8f;

        private readonly CareerMatchPlayback _playback = new CareerMatchPlayback();
        private readonly List<int> _timelineRows = new List<int>(TimelineRowCapacity);
        private CareerMatchSession _playbackSession;
        private int _playbackSpeedStepIndex = DefaultPlaybackSpeedStepIndex;
        private CareerPlateAppearanceSummary _controlledResult;
        private bool _hasControlledResult;
        private bool _isControlledPlayerPlaybackStep;
        private bool _isPlaybackInitialized;
        private bool _isPaused;
        private bool _isCallUpAcknowledged;
        private float _nextAutomaticPlayAt;

        public override bool BlocksLowerInput => true;

        /// <summary>
        /// 프리팹이 없는 프로토타입 환경에서 경기 화면을 런타임 생성한다.
        /// </summary>
        public static UI_Scene_CareerMatch CreateRuntime(Transform parent)
        {
            var screenObject = new GameObject(
                nameof(UI_Scene_CareerMatch),
                typeof(RectTransform),
                typeof(CanvasGroup));
            screenObject.transform.SetParent(parent, false);
            UI_Scene_CareerMatch screen = screenObject.AddComponent<UI_Scene_CareerMatch>();
            Stretch(screenObject.GetComponent<RectTransform>());
            return screen;
        }

        protected override void OnInitialize()
        {
            _manager = GameManager.EnsureExists().EnsureManager<CareerManager>("CareerManager");
            _manager.CareerChanged += HandleCareerChanged;
            RectTransform root = (RectTransform)transform;
            Stretch(root);
            CreateImage("Background", root, BackgroundColor, Vector2.zero, Vector2.zero, true);
            _content = CreateRect("Content", root, new Vector2(1920f, 1080f), Vector2.zero);
            // 자동 진행 중에는 _content가 매 스텝 통째로 재생성되므로, 클릭이 성립하려면
            // 조작 버튼은 그 바깥의 계층에 남아 있어야 한다.
            RectTransform controlLayer = CreateRect("ControlLayer", root, new Vector2(1920f, 1080f), Vector2.zero);
            _controlHost = CreateRect("ControlHost", controlLayer, ControlPanelSize, ControlPanelPosition);
            _settingsHost = CreateRect("SettingsHost", controlLayer, new Vector2(100f, 44f), new Vector2(890f, 512f));
            Button settings = CreateButton(
                "Settings", _settingsHost, "설정", new Vector2(100f, 44f), Vector2.zero,
                new Color(0.025f, 0.08f, 0.13f, 1f), SecondaryTextColor);
            settings.onClick.AddListener(() => UI_Popup_CareerSettings.ShowRuntime());
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

        /// <summary>
        /// 준비 단계의 Cancel만 홈 복귀로 처리하고 진행 중 경기가 화면 뒤로 숨지 않게 한다.
        /// </summary>
        public override void Close()
        {
            CareerMatchSession session = _manager?.ActiveMatch;
            if (session == null)
            {
                Hide();
                return;
            }

            if (session.Phase == CareerMatchPhase.Preparation)
                _manager.CancelPreparedGame();
            else if (IsAutomaticPlaybackActive(session))
            {
                _playback.RevealAll(session.Events);
                ClearControlledResult();
                Render();
            }
            else if (session.Phase == CareerMatchPhase.Completed)
                _manager.ReturnHomeFromCompletedMatch();
        }

        private void Update()
        {
            if (!IsVisible || _manager?.ActiveMatch == null)
                return;
            if (UI_Popup_CareerSettings.IsOpen)
                return;

            Keyboard keyboard = Keyboard.current;
            CareerMatchSession session = _manager.ActiveMatch;
            if (session.Phase == CareerMatchPhase.Preparation)
            {
                if (keyboard == null)
                    return;
                if (keyboard.rKey.wasPressedThisFrame)
                    _manager.StartPreparedGame(CareerMatchMode.ResultsOnly);
                else if (IsConfirmKeyPressed(keyboard))
                    _manager.StartPreparedGameFromSettings();
                return;
            }

            EnsurePlayback(session);
            if (keyboard != null && IsPendingCallUpAcknowledgement(session))
            {
                if (IsConfirmKeyPressed(keyboard))
                    AcknowledgeCallUp();
                return;
            }

            if (UpdateAutomaticPlayback(session))
            {
                if (keyboard != null && keyboard.pKey.wasPressedThisFrame)
                    TogglePause();
                return;
            }

            if (session.Phase == CareerMatchPhase.Completed)
            {
                if (keyboard != null && IsConfirmKeyPressed(keyboard))
                    _manager.ReturnHomeFromCompletedMatch();
                return;
            }

            if (keyboard == null)
            {
                UpdateMiniGameInput(session, null);
                return;
            }
            if (UpdateMiniGameInput(session, keyboard))
                return;
            if (IsPitchingDecisionInputReady(session))
            {
                if (keyboard.digit1Key.wasPressedThisFrame) SelectPitchingApproach(PitchingApproach.Balanced);
                else if (keyboard.digit2Key.wasPressedThisFrame) SelectPitchingApproach(PitchingApproach.FullPower);
                else if (keyboard.digit3Key.wasPressedThisFrame) SelectPitchingApproach(PitchingApproach.ControlFirst);
                else if (keyboard.digit4Key.wasPressedThisFrame) SelectPitchingApproach(PitchingApproach.InduceChase);
                else if (keyboard.digit5Key.wasPressedThisFrame) SelectPitchingApproach(PitchingApproach.QuickAttack);
                else if (keyboard.spaceKey.wasPressedThisFrame) StartSelectedPitchingInning();
                return;
            }

            if (IsDecisionInputReady(session))
            {
                if (keyboard.digit1Key.wasPressedThisFrame) SelectApproach(BattingApproach.Patient);
                else if (keyboard.digit2Key.wasPressedThisFrame) SelectApproach(BattingApproach.Balanced);
                else if (keyboard.digit3Key.wasPressedThisFrame) SelectApproach(BattingApproach.Contact);
                else if (keyboard.digit4Key.wasPressedThisFrame) SelectApproach(BattingApproach.Power);
                else if (keyboard.spaceKey.wasPressedThisFrame) SubmitSelectedApproach();
                else if (keyboard.aKey.wasPressedThisFrame) AutoCompleteCurrentPlateAppearance();
            }
        }

        private void HandleCareerChanged()
        {
            if (_manager == null || !_manager.HasActiveMatch)
            {
                ResetPlayback();
                Hide();
                return;
            }

            SyncCareerGameSettings();

            if (!IsVisible)
            {
                Show();
                return;
            }

            Render();
        }

        private void Render()
        {
            if (_content == null || _manager == null || !_manager.HasActiveMatch)
                return;

            ClearChildren(_content);
            CareerMatchSession session = _manager.ActiveMatch;
            if (session.Phase != CareerMatchPhase.Preparation)
                EnsurePlayback(session);
            switch (session.Phase)
            {
                case CareerMatchPhase.Preparation:
                    RenderPreparation(session);
                    break;
                case CareerMatchPhase.Playing:
                    RenderPlaying(session);
                    break;
                case CareerMatchPhase.Completed:
                    if (IsAutomaticPlaybackActive(session))
                        RenderPlaying(session);
                    else
                        RenderCompleted(session);
                    break;
            }
        }

        private void RenderPreparation(CareerMatchSession session)
        {
            ClearPersistentControls();
            SyncCareerGameSettings();
            CreateText(
                "Eyebrow", _content, "GAME DAY", 15, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(400f, 28f), new Vector2(0f, 465f), AccentColor);
            CreateText(
                "Title", _content, "경기 준비", 42, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(600f, 60f), new Vector2(0f, 420f), PrimaryTextColor);
            CreateText(
                "Date", _content, $"{session.GameDate:M월 d일 dddd} · {session.Input.HomeTeam.Name} 홈구장",
                18, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(760f, 34f), new Vector2(0f, 376f), SecondaryTextColor);

            RectTransform card = CreatePanel(
                "PreparationCard", _content, new Vector2(1240f, 680f), new Vector2(0f, -5f));
            RenderVersusHeader(card, session.Input.AwayTeam.Name, session.Input.HomeTeam.Name, 205f);

            RectTransform role = CreateImage(
                "ConfirmedRole", card, new Color(0.035f, 0.16f, 0.19f, 1f),
                new Vector2(970f, 86f), new Vector2(0f, 66f));
            CreateText(
                "Label", role, "출전 역할 확정", 13, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(180f, 30f), new Vector2(-355f, 16f), RoleColor);
            CreateText(
                "Value", role, GetRoleLabel(session.PlayerRole, _manager.CurrentCareer.MyPlayer.PrimaryPosition),
                27, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(500f, 42f), new Vector2(-195f, -14f), PrimaryTextColor);
            CreateText(
                "Condition", role,
                $"컨디션 {session.ConditionBefore}   ·   감독평가 {GetEvaluationGrade(session.ManagerEvaluationBefore)}",
                17, FontStyle.Bold, TextAnchor.MiddleRight,
                new Vector2(360f, 34f), new Vector2(282f, 0f), SecondaryTextColor);

            PlayerPosition playerPosition = _manager.CurrentCareer.MyPlayer.PrimaryPosition;
            string roleGuide = CareerGameRoleFormatter.IsPitcherRest(session.PlayerRole, playerPosition)
                ? "오늘은 등판 없이 회복하며 경기를 관전합니다."
                : session.PlayerRole switch
                {
                PlayerGameRole.StartingBatter =>
                        "내 타석에서 방망이 위치와 스윙 시점을 직접 결정할 수 있습니다.",
                    PlayerGameRole.Bench =>
                        "벤치에서 경기를 지켜보다 대타로 투입되면 자동 진행이 멈추고 내 타석 입력이 열립니다.",
                    PlayerGameRole.StartingPitcher or PlayerGameRole.ReliefPitcher =>
                        "직접 플레이에서는 구종 배합과 홈플레이트 목표 위치를 투구마다 결정합니다.",
                    _ => "1회부터 공격·수비와 주자 움직임을 자동 관전하고, 기용 결과를 경기 후 확인합니다."
                };
            CreateText(
                "Guide", card, roleGuide, 17, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(980f, 48f), new Vector2(0f, -22f), SecondaryTextColor);

            RectTransform mode = CreateImage(
                "Mode", card, PanelDarkColor, new Vector2(970f, 118f), new Vector2(0f, -112f));
            CreateText(
                "ModeLabel", mode, "진행 방식", 13, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(160f, 26f), new Vector2(-380f, 32f), AccentColor);
            MatchProgressMode progressMode = _manager.CurrentCareer.GameSettings.MatchProgressMode;
            bool canReceivePlayerDecision = session.CanReceiveBattingDecisions ||
                                            session.CanReceivePitchingDecisions;
            CreateText(
                "ModeValue", mode, GetMatchProgressModeLabel(progressMode), 24, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(320f, 38f), new Vector2(-300f, -6f), PrimaryTextColor);
            string modeGuide = GetMatchProgressModeGuide(
                progressMode,
                canReceivePlayerDecision,
                session.CanReceivePitchingDecisions);
            CreateText(
                "ModeGuide", mode, modeGuide,
                15, FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(650f, 30f), new Vector2(-42f, -39f), SecondaryTextColor);
            CreateStatusPill(
                mode,
                GetMatchProgressModeStatus(
                    progressMode,
                    canReceivePlayerDecision,
                    session.CanReceivePitchingDecisions),
                new Vector2(305f, 42f),
                new Vector2(310f, 15f));

            Button cancel = CreateButton(
                "Cancel", card, "돌아가기", new Vector2(210f, 62f), new Vector2(-380f, -242f),
                PanelDarkColor, SecondaryTextColor);
            cancel.onClick.AddListener(() => _manager.CancelPreparedGame());
            Button resultsOnly = CreateButton(
                "ResultsOnly", card, "결과만 보기   R", new Vector2(270f, 62f), new Vector2(-120f, -242f),
                new Color(0.08f, 0.15f, 0.2f, 1f), SecondaryTextColor);
            resultsOnly.onClick.AddListener(() =>
            {
                resultsOnly.interactable = false;
                _manager.StartPreparedGame(CareerMatchMode.ResultsOnly);
            });
            Button start = CreateButton(
                "Start", card, "경기 시작   SPACE / ENTER", new Vector2(460f, 70f), new Vector2(285f, -242f),
                new Color(0.02f, 0.37f, 0.68f, 1f), PrimaryTextColor);
            start.onClick.AddListener(() =>
            {
                start.interactable = false;
                _manager.StartPreparedGameFromSettings();
            });
        }

        private void RenderPlaying(CareerMatchSession session)
        {
            bool isDecisionInputReady = IsDecisionInputReady(session);
            MatchDecisionRequest? visibleDecision = isDecisionInputReady
                ? session.PendingDecision
                : null;
            CareerMatchPlaybackSnapshot snapshot = _playback.BuildSnapshot(
                session.Events,
                visibleDecision);
            MatchProgressViewState view = BuildViewState(session, snapshot, isDecisionInputReady);

            RenderScoreboard(session, snapshot, view);

            RectTransform playerPanel = CreatePanel(
                "PlayerPanel", _content, new Vector2(360f, 900f), new Vector2(-770f, -32f));
            RenderPlayerPanel(playerPanel, session, snapshot, view);

            RectTransform stagePanel = CreatePanel(
                "StagePanel", _content, new Vector2(1000f, 566f), new Vector2(-70f, 135f));
            RenderStage(stagePanel, session, snapshot, view);

            RectTransform timelinePanel = CreatePanel(
                "TimelinePanel", _content, new Vector2(1000f, 324f), new Vector2(-70f, -320f));
            RenderTimeline(timelinePanel, session);

            RectTransform controlPanel = CreatePanel(
                "ControlPanel", _content, ControlPanelSize, ControlPanelPosition);
            RenderControlPanel(controlPanel, session, snapshot, view);
        }

        /// <summary>
        /// 상단 스코어를 공격 팀 강조와 B/S/O 인디케이터 중심으로 그린다.
        /// </summary>
        private void RenderScoreboard(
            CareerMatchSession session,
            CareerMatchPlaybackSnapshot snapshot,
            MatchProgressViewState view)
        {
            RectTransform bar = CreateImage(
                "Scoreboard", _content, TopBarColor, new Vector2(1920f, 116f), new Vector2(0f, 482f));

            RenderScoreboardTeam(
                bar, session.Input.AwayTeam.Name, snapshot.AwayScore, -1f, view.IsAwayTeamBatting);
            RenderScoreboardTeam(
                bar, session.Input.HomeTeam.Name, snapshot.HomeScore, 1f, !view.IsAwayTeamBatting);

            CreateText(
                "Inning", bar, $"{snapshot.Inning}회{GetHalfLabel(snapshot.Half)}", 23, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(240f, 32f), new Vector2(0f, 30f), AccentColor);
            string battingTeamName = view.IsAwayTeamBatting
                ? session.Input.AwayTeam.Name
                : session.Input.HomeTeam.Name;
            CreateText(
                "Attacking", bar, $"{battingTeamName} 공격", 15, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(320f, 26f), new Vector2(0f, 4f), SecondaryTextColor);

            bool isSideChange = view.Flow == MatchFlowState.SideChange;
            CreateCountDots(bar, "B", isSideChange ? 0 : snapshot.Balls, 3, new Vector2(-118f, -30f), AccentColor);
            CreateCountDots(bar, "S", isSideChange ? 0 : snapshot.Strikes, 2, new Vector2(0f, -30f), GoldColor);
            CreateCountDots(bar, "O", isSideChange ? 0 : snapshot.Outs, 2, new Vector2(112f, -30f), DangerColor);

            CreateText(
                "Runners", bar, isSideChange ? "주자 없음" : GetRunnerSituation(snapshot), 15,
                FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(260f, 28f), new Vector2(-800f, -32f), SecondaryTextColor);
            CreateText(
                "FlowStatus", bar, GetFlowStatusLine(view), 15, FontStyle.Bold, TextAnchor.MiddleRight,
                new Vector2(340f, 28f), new Vector2(790f, -32f), GetFlowStatusColor(view));
        }

        private static void RenderScoreboardTeam(
            RectTransform bar,
            string teamName,
            int score,
            float direction,
            bool isBatting)
        {
            CreateText(
                "Team", bar, teamName, 20, FontStyle.Bold,
                direction < 0f ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft,
                new Vector2(340f, 34f), new Vector2(330f * direction, 20f),
                isBatting ? PrimaryTextColor : SecondaryTextColor);
            if (isBatting)
            {
                CreateImage(
                    "AttackingUnderline", bar, GoldColor,
                    new Vector2(180f, 3f), new Vector2(330f * direction, -6f));
            }
            CreateText(
                "Score", bar, score.ToString(), 46, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(110f, 62f), new Vector2(140f * direction, 8f), PrimaryTextColor);
        }

        /// <summary>
        /// 볼·스트라이크·아웃을 채워지는 점으로 표시한다. 숫자보다 남은 여유가 한눈에 들어온다.
        /// </summary>
        private static void CreateCountDots(
            RectTransform parent,
            string label,
            int filled,
            int total,
            Vector2 position,
            Color filledColor)
        {
            CreateText(
                "CountLabel_" + label, parent, label, 14, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(22f, 24f), position, SecondaryTextColor);
            for (int index = 0; index < total; index++)
            {
                Color color = index < filled ? filledColor : EmptyDotColor;
                CreateImage(
                    $"Dot_{label}_{index}", parent, color, new Vector2(12f, 12f),
                    new Vector2(position.x + 22f + index * 18f, position.y));
            }
        }

        /// <summary>
        /// 왼쪽 패널을 커리어 정보창이 아니라 경기 참여 상태창으로 그린다.
        /// </summary>
        private void RenderPlayerPanel(
            RectTransform panel,
            CareerMatchSession session,
            CareerMatchPlaybackSnapshot snapshot,
            MatchProgressViewState view)
        {
            PlayerState player = _manager.CurrentCareer.MyPlayer;
            CreateText(
                "Eyebrow", panel, "내 선수", 13, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(300f, 24f), new Vector2(0f, 412f), AccentColor);
            CreatePlayerPortrait(panel, player.Name, player.PrimaryPosition, new Vector2(0f, 326f));
            CreateText(
                "Name", panel, player.Name, 27, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(320f, 42f), new Vector2(0f, 244f), PrimaryTextColor);
            CreateText(
                "Role", panel, GetRoleLabel(session.PlayerRole, player.PrimaryPosition), 15,
                FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(320f, 26f), new Vector2(0f, 214f), SecondaryTextColor);

            Color stateColor = GetPlayerStateColor(view.PlayerState);
            RectTransform stateBadge = CreateImage(
                "StateBadge", panel, PanelDarkColor, new Vector2(320f, 58f), new Vector2(0f, 152f));
            CreateImage("StateBar", stateBadge, stateColor, new Vector2(5f, 46f), new Vector2(-157f, 0f));
            CreateText(
                "StateLabel", stateBadge, GetPlayerStateLabel(view.PlayerState), 20, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(300f, 30f), Vector2.zero, stateColor);
            CreateText(
                "StateDetail", panel, GetPlayerStateDetail(session, view), 14, FontStyle.Normal,
                TextAnchor.MiddleCenter, new Vector2(320f, 26f), new Vector2(0f, 108f), SecondaryTextColor);

            PlayerTodayLine today = CalculateTodayLine(
                session.Events,
                _playback.VisibleEventCount,
                session.ControlledPlayerId);
            RectTransform todayCard = CreateImage(
                "Today", panel, CardColor, new Vector2(320f, 96f), new Vector2(0f, 32f));
            CreateText(
                "Label", todayCard, "오늘", 13, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(150f, 24f), new Vector2(-77f, 28f), SecondaryTextColor);
            CreateText(
                "Value", todayCard, $"{today.PlateAppearances}타석  {today.Hits}안타  {today.RunsBattedIn}타점",
                22, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(290f, 34f), new Vector2(0f, 0f), PrimaryTextColor);
            CreateText(
                "Detail", todayCard, BuildTodayDetailLine(today), 13,
                FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(290f, 22f), new Vector2(0f, -30f), MutedTextColor);

            RenderConditionCard(panel, session.ConditionBefore, new Vector2(0f, -66f));

            RectTransform nextEvent = CreateImage(
                "NextEvent", panel, PanelDarkColor, new Vector2(320f, 132f), new Vector2(0f, -198f));
            CreateText(
                "Label", nextEvent, "다음 이벤트", 13, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(150f, 24f), new Vector2(-77f, 46f), GoldColor);
            CreateText(
                "Value", nextEvent, GetNextEventGuide(session, view), 15, FontStyle.Normal,
                TextAnchor.UpperLeft, new Vector2(290f, 88f), new Vector2(0f, -16f), PrimaryTextColor);

            CreateText(
                "ManagerTrust", panel,
                $"감독 신뢰 {GetEvaluationGrade(session.ManagerEvaluationBefore)}  ·  " +
                GetSubstitutionPriorityLabel(session),
                13, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(320f, 24f), new Vector2(0f, -300f), MutedTextColor);
            CreateText(
                "Season", panel,
                $"시즌  {FormatAverage(_manager.Dashboard.Statistics.BattingAverage)}  /  " +
                $"{_manager.Dashboard.Statistics.HomeRuns}홈런  /  " +
                $"{_manager.Dashboard.Statistics.RunsBattedIn}타점",
                14, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(320f, 26f), new Vector2(0f, -354f), SecondaryTextColor);

            if (IsControlledPlayerOnBase(snapshot, session.ControlledPlayerId))
            {
                CreateText(
                    "RunnerFlag", panel, "지금 주루 중", 14, FontStyle.Bold, TextAnchor.MiddleCenter,
                    new Vector2(320f, 24f), new Vector2(0f, -400f), RoleColor);
            }
        }

        private static void RenderConditionCard(RectTransform panel, int condition, Vector2 position)
        {
            RectTransform card = CreateImage(
                "Condition", panel, PanelDarkColor, new Vector2(320f, 84f), position);
            CreateText(
                "Label", card, "컨디션", 13, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(150f, 24f), new Vector2(-77f, 24f), SecondaryTextColor);
            CreateText(
                "Value", card, $"{condition} · {GetConditionLabel(condition)}", 18, FontStyle.Bold,
                TextAnchor.MiddleRight, new Vector2(200f, 26f), new Vector2(52f, 24f), GetConditionColor(condition));
            RectTransform track = CreateImage(
                "Track", card, new Color(0.06f, 0.13f, 0.17f, 1f), new Vector2(280f, 8f), new Vector2(0f, -18f));
            RectTransform fill = CreateImage(
                "Fill", track, GetConditionColor(condition),
                new Vector2(280f * Mathf.Clamp01(condition / 100f), 8f), Vector2.zero);
            fill.anchorMin = new Vector2(0f, 0.5f);
            fill.anchorMax = new Vector2(0f, 0.5f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.anchoredPosition = Vector2.zero;
        }

        /// <summary>
        /// 중앙 무대를 흐름 상태에 따라 완전히 다른 화면으로 전환한다.
        /// </summary>
        private void RenderStage(
            RectTransform panel,
            CareerMatchSession session,
            CareerMatchPlaybackSnapshot snapshot,
            MatchProgressViewState view)
        {
            if (IsMiniGameInputReady(session))
            {
                RenderMiniGameStage(panel, session);
                return;
            }
            switch (view.Flow)
            {
                case MatchFlowState.SideChange:
                    RenderSideChangeStage(panel, session, snapshot);
                    return;
                case MatchFlowState.PlayerCallUp:
                    RenderCallUpStage(panel, session, snapshot);
                    return;
                case MatchFlowState.PlayerAtBat:
                    RenderPlateAppearanceStage(panel, session, snapshot);
                    return;
                case MatchFlowState.PlayerAtBatResult:
                    RenderPlateAppearanceResultStage(panel, session, snapshot);
                    return;
                default:
                    RenderBroadcastStage(panel, session, snapshot, view);
                    return;
            }
        }

        private void RenderBroadcastStage(
            RectTransform panel,
            CareerMatchSession session,
            CareerMatchPlaybackSnapshot snapshot,
            MatchProgressViewState view)
        {
            CreateText(
                "Situation", panel,
                $"{snapshot.Inning}회{GetHalfLabel(snapshot.Half)} · {snapshot.Outs}사 · " +
                GetRunnerSituation(snapshot),
                18, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(700f, 32f), new Vector2(0f, 246f), SecondaryTextColor);

            RenderDiamond(panel, session, snapshot, new Vector2(-300f, 22f));
            RenderMatchupCard(panel, session, snapshot, new Vector2(148f, 26f));

            RectTransform moment = CreateImage(
                "Moment", panel, PanelDarkColor, new Vector2(940f, 74f), new Vector2(0f, -218f));
            CreateText(
                "Label", moment, "최근 플레이", 12, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(160f, 22f), new Vector2(-380f, 20f), MutedTextColor);
            CreateText(
                "Value", moment, GetLatestPlayDescription(session), 19, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Vector2(880f, 30f), new Vector2(0f, -10f),
                view.Flow == MatchFlowState.Paused ? SecondaryTextColor : GoldColor);
        }

        private void RenderDiamond(
            RectTransform panel,
            CareerMatchSession session,
            CareerMatchPlaybackSnapshot snapshot,
            Vector2 position)
        {
            RectTransform diamond = CreateImage(
                "Diamond", panel, new Color(0.03f, 0.19f, 0.13f, 1f),
                new Vector2(360f, 250f), position);
            CreateBase(
                diamond, "Second", snapshot.HasRunnerOnSecond, new Vector2(0f, 78f),
                isControlledRunner: snapshot.SecondRunnerId == session.ControlledPlayerId);
            CreateBase(
                diamond, "First", snapshot.HasRunnerOnFirst, new Vector2(94f, 0f),
                isControlledRunner: snapshot.FirstRunnerId == session.ControlledPlayerId);
            CreateBase(
                diamond, "Third", snapshot.HasRunnerOnThird, new Vector2(-94f, 0f),
                isControlledRunner: snapshot.ThirdRunnerId == session.ControlledPlayerId);
            CreateBase(diamond, "Home", true, new Vector2(0f, -78f), isHome: true);

            RenderRunnerName(diamond, session, snapshot.SecondRunnerId, new Vector2(0f, 106f));
            RenderRunnerName(diamond, session, snapshot.FirstRunnerId, new Vector2(94f, -32f));
            RenderRunnerName(diamond, session, snapshot.ThirdRunnerId, new Vector2(-94f, -32f));
        }

        private void RenderRunnerName(
            RectTransform diamond,
            CareerMatchSession session,
            int runnerId,
            Vector2 position)
        {
            if (runnerId == 0)
                return;

            CreateText(
                "Runner" + runnerId, diamond, FindPlayerName(session.Input, runnerId), 12,
                FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(140f, 20f), position,
                runnerId == session.ControlledPlayerId ? RoleColor : GoldColor);
        }

        private void RenderMatchupCard(
            RectTransform panel,
            CareerMatchSession session,
            CareerMatchPlaybackSnapshot snapshot,
            Vector2 position)
        {
            RectTransform card = CreateImage(
                "Matchup", panel, PanelDarkColor, new Vector2(560f, 300f), position);
            CreateText(
                "Label", card, "현재 승부", 12, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(280f, 22f), new Vector2(0f, 124f), AccentColor);

            bool hasBatter = snapshot.BatterId != 0;
            CreateText(
                "Batter", card, hasBatter ? FindPlayerName(session.Input, snapshot.BatterId) : "타자 준비",
                27, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(520f, 40f), new Vector2(0f, 76f),
                snapshot.BatterId == session.ControlledPlayerId ? RoleColor : PrimaryTextColor);
            if (hasBatter)
            {
                CreateText(
                    "BatterDetail", card, BuildBatterContextLine(session, snapshot.Half, snapshot.BatterId), 14,
                    FontStyle.Normal, TextAnchor.MiddleCenter,
                    new Vector2(520f, 24f), new Vector2(0f, 44f), SecondaryTextColor);
            }

            CreateText(
                "Vs", card, "VS", 14, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(80f, 24f), new Vector2(0f, 12f), MutedTextColor);
            CreateText(
                "Pitcher", card, FindPlayerName(session.Input, snapshot.PitcherId), 23, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(520f, 34f), new Vector2(0f, -22f), PrimaryTextColor);
            CreateText(
                "PitcherDetail", card, "상대 투수", 14, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(520f, 24f), new Vector2(0f, -50f), SecondaryTextColor);

            CreateImage("Divider", card, BorderColor, new Vector2(500f, 1f), new Vector2(0f, -80f));
            CreateText(
                "Count", card, $"볼 {snapshot.Balls}  ·  스트라이크 {snapshot.Strikes}", 18, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(520f, 30f), new Vector2(0f, -110f), GoldColor);
        }

        private void RenderSideChangeStage(
            RectTransform panel,
            CareerMatchSession session,
            CareerMatchPlaybackSnapshot snapshot)
        {
            CreateText(
                "Title", panel, $"{snapshot.Inning}회{GetHalfLabel(snapshot.Half)} 종료", 44, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(800f, 62f), new Vector2(0f, 132f), PrimaryTextColor);
            CreateText(
                "Score", panel,
                $"{session.Input.AwayTeam.Name} {snapshot.AwayScore}   ·   " +
                $"{snapshot.HomeScore} {session.Input.HomeTeam.Name}",
                24, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(880f, 40f), new Vector2(0f, 62f), SecondaryTextColor);
            CreateStatusPill(panel, "공수 교대", new Vector2(260f, 48f), new Vector2(0f, -14f));

            bool isNextHalfBottom = snapshot.Half == InningHalf.Top;
            string nextTeamName = isNextHalfBottom
                ? session.Input.HomeTeam.Name
                : session.Input.AwayTeam.Name;
            CreateText(
                "NextLabel", panel, "다음 공격", 13, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(400f, 24f), new Vector2(0f, -90f), MutedTextColor);
            CreateText(
                "NextTeam", panel, nextTeamName, 28, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(700f, 42f), new Vector2(0f, -128f), AccentColor);
        }

        private void RenderCallUpStage(
            RectTransform panel,
            CareerMatchSession session,
            CareerMatchPlaybackSnapshot snapshot)
        {
            PlayerState player = _manager.CurrentCareer.MyPlayer;
            CreateStatusPill(panel, "감독 호출", new Vector2(240f, 48f), new Vector2(0f, 208f));
            CreateText(
                "Situation", panel,
                $"{snapshot.Inning}회{GetHalfLabel(snapshot.Half)} · {snapshot.Outs}사 · " +
                GetRunnerSituation(snapshot),
                19, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(760f, 32f), new Vector2(0f, 152f), SecondaryTextColor);
            CreateText(
                "Name", panel, player.Name, 40, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(700f, 58f), new Vector2(0f, 92f), RoleColor);
            CreateText(
                "Reason", panel, BuildCallUpDescription(session), 19, FontStyle.Normal,
                TextAnchor.MiddleCenter, new Vector2(860f, 34f), new Vector2(0f, 40f), PrimaryTextColor);
            CreateText(
                "Pitcher", panel,
                $"상대 투수 {FindPlayerName(session.Input, snapshot.PitcherId)}",
                17, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(700f, 30f), new Vector2(0f, -8f), SecondaryTextColor);
            CreateText(
                "Guide", panel, "감독 결정으로 이미 확정된 출전입니다.", 14, FontStyle.Normal,
                TextAnchor.MiddleCenter, new Vector2(700f, 26f), new Vector2(0f, -48f), MutedTextColor);

            Button enter = CreateButton(
                "EnterPlateAppearance", panel, "타석으로 이동   SPACE",
                new Vector2(460f, 68f), new Vector2(0f, -150f),
                new Color(0.02f, 0.38f, 0.7f, 1f), PrimaryTextColor);
            enter.onClick.AddListener(AcknowledgeCallUp);
        }

        private void RenderPlateAppearanceStage(
            RectTransform panel,
            CareerMatchSession session,
            CareerMatchPlaybackSnapshot snapshot)
        {
            MatchDecisionRequest request = session.PendingDecision.Value;
            PlayerState player = _manager.CurrentCareer.MyPlayer;
            CreateText(
                "Title", panel, $"{player.Name}의 타석", 30, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(700f, 44f), new Vector2(0f, 244f), RoleColor);
            CreateText(
                "Situation", panel,
                $"{request.Inning}회{GetHalfLabel(request.Half)} · {request.Outs}사 · " +
                $"{GetRunnerSituation(snapshot)}  ·  볼 {request.Balls} · 스트라이크 {request.Strikes}",
                16, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(860f, 28f), new Vector2(0f, 208f), SecondaryTextColor);
            CreateText(
                "Pitcher", panel,
                $"상대 투수 {FindPlayerName(session.Input, request.PitcherId)}  ·  {request.PitchNumber}구 예정",
                15, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(860f, 26f), new Vector2(0f, 178f), MutedTextColor);

            CreateApproachButton(
                panel, BattingApproach.Patient, "1  신중하게", "볼을 골라 출루를 노립니다.",
                new Vector2(-242f, 108f));
            CreateApproachButton(
                panel, BattingApproach.Balanced, "2  균형 있게", "컨택과 장타를 균형 있게 노립니다.",
                new Vector2(242f, 108f));
            CreateApproachButton(
                panel, BattingApproach.Contact, "3  컨택 중심", "접촉 확률이 높고 장타는 줄어듭니다.",
                new Vector2(-242f, 6f));
            CreateApproachButton(
                panel, BattingApproach.Power, "4  적극적으로", "장타 가능성이 높지만 헛스윙이 늘어납니다.",
                new Vector2(242f, 6f));

            CreateText(
                "LastPitch", panel, GetLatestPitchDescription(session), 15, FontStyle.Normal,
                TextAnchor.MiddleCenter, new Vector2(860f, 26f), new Vector2(0f, -78f), SecondaryTextColor);

            Button nextPitch = CreateButton(
                "NextPitch", panel, "다음 투구   SPACE", new Vector2(460f, 68f), new Vector2(0f, -152f),
                new Color(0.02f, 0.38f, 0.7f, 1f), PrimaryTextColor);
            nextPitch.onClick.AddListener(() =>
            {
                nextPitch.interactable = false;
                SubmitSelectedApproach();
            });
            Button autoPlateAppearance = CreateButton(
                "AutoPlateAppearance", panel, "현재 타석 자동 진행   A",
                new Vector2(340f, 44f), new Vector2(0f, -226f), PanelDarkColor, SecondaryTextColor);
            autoPlateAppearance.onClick.AddListener(AutoCompleteCurrentPlateAppearance);

            if (!string.IsNullOrEmpty(_manager.LastError))
            {
                CreateText(
                    "Error", panel, _manager.LastError, 13, FontStyle.Normal, TextAnchor.MiddleCenter,
                    new Vector2(860f, 22f), new Vector2(0f, -262f), DangerColor);
            }
        }

        private void RenderPlateAppearanceResultStage(
            RectTransform panel,
            CareerMatchSession session,
            CareerMatchPlaybackSnapshot snapshot)
        {
            Color resultColor = GetControlledResultColor(_controlledResult);
            CreateText(
                "Result", panel, GetControlledResultLabel(_controlledResult), 52, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(880f, 72f), new Vector2(0f, 150f), resultColor);
            CreateText(
                "Description", panel, GetControlledResultDescription(_controlledResult), 20,
                FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(880f, 34f), new Vector2(0f, 82f), PrimaryTextColor);
            CreateText(
                "Situation", panel,
                $"{snapshot.Outs}사 · {GetRunnerSituation(snapshot)}", 20, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(700f, 32f), new Vector2(0f, 22f), SecondaryTextColor);

            PlayerTodayLine today = CalculateTodayLine(
                session.Events,
                _playback.VisibleEventCount,
                session.ControlledPlayerId);
            RectTransform todayCard = CreateImage(
                "TodayCard", panel, PanelDarkColor, new Vector2(560f, 82f), new Vector2(0f, -60f));
            CreateText(
                "Label", todayCard, "오늘 기록", 13, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(160f, 24f), new Vector2(-190f, 22f), MutedTextColor);
            CreateText(
                "Value", todayCard,
                $"{today.PlateAppearances}타석  {today.Hits}안타  {today.RunsBattedIn}타점",
                24, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(520f, 34f), new Vector2(0f, -12f), resultColor);

            CreateText(
                "Approach", panel, $"선택한 접근 · {GetApproachLabel(_selectedApproach)}", 15,
                FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(700f, 26f), new Vector2(0f, -134f), AccentColor);
            CreateText(
                "Continue", panel,
                _manager.CurrentCareer.GameSettings.AutoSlowOnPlayerEvent
                    ? $"내 선수 장면 1배속 적용 중 · 잠시 후 {GetConfiguredPlaybackSpeedLabel()} 자동 중계로 돌아갑니다."
                    : $"잠시 후 {GetConfiguredPlaybackSpeedLabel()} 자동 중계로 돌아갑니다.",
                15,
                FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(820f, 26f), new Vector2(0f, -172f), SecondaryTextColor);
        }

        /// <summary>
        /// 경기 로그를 이닝 단위로 묶고 이벤트 종류에 따라 강조를 다르게 준다.
        /// </summary>
        private void RenderTimeline(RectTransform panel, CareerMatchSession session)
        {
            CreateText(
                "Title", panel, "실시간 경기", 16, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(260f, 28f), new Vector2(-355f, 130f), PrimaryTextColor);

            IReadOnlyList<MatchEvent> events = session.Events;
            _timelineRows.Clear();
            for (int index = _playback.VisibleEventCount - 1;
                 index >= 0 && _timelineRows.Count < TimelineRowCapacity;
                 index--)
            {
                if (!IsVisibleLogEvent(events[index], session.ControlledPlayerId))
                    continue;
                if (string.IsNullOrEmpty(DescribeTimelineEvent(session.Input, events, index)))
                    continue;

                _timelineRows.Add(index);
            }

            if (_timelineRows.Count == 0)
            {
                CreateText(
                    "Empty", panel, "1회초 첫 타자의 결과부터 자동으로 중계합니다.", 15,
                    FontStyle.Normal, TextAnchor.MiddleCenter,
                    new Vector2(700f, 40f), new Vector2(0f, 20f), SecondaryTextColor);
                return;
            }

            // 최신 이벤트가 위에 오므로 이닝 머리글은 각 그룹의 첫 줄 바로 위에 놓는다.
            int line = 0;
            float top = 88f;
            for (int row = 0; row < _timelineRows.Count && line < TimelineLineCapacity; row++)
            {
                MatchEvent matchEvent = events[_timelineRows[row]];
                bool isGroupStart = row == 0 ||
                                    events[_timelineRows[row - 1]].Inning != matchEvent.Inning ||
                                    events[_timelineRows[row - 1]].Half != matchEvent.Half;
                if (isGroupStart)
                {
                    CreateInningHeader(panel, matchEvent, line, top - line * TimelineRowHeight);
                    line++;
                }

                CreateTimelineRow(
                    panel,
                    session,
                    matchEvent,
                    DescribeTimelineEvent(session.Input, events, _timelineRows[row]),
                    top - line * TimelineRowHeight);
                line++;
            }
        }

        private static void CreateInningHeader(RectTransform panel, MatchEvent matchEvent, int line, float y)
        {
            CreateText(
                $"InningHeader{line}", panel, $"{matchEvent.Inning}회{GetHalfLabel(matchEvent.Half)}", 14,
                FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(200f, 24f), new Vector2(-395f, y), AccentColor);
            CreateImage(
                $"InningRule{line}", panel, new Color(0.1f, 0.24f, 0.34f, 1f),
                new Vector2(830f, 1f), new Vector2(80f, y));
        }

        private void CreateTimelineRow(
            RectTransform panel,
            CareerMatchSession session,
            MatchEvent matchEvent,
            string description,
            float y)
        {
            bool isControlledEvent = matchEvent.BatterId == session.ControlledPlayerId ||
                                     matchEvent.PlayerId == session.ControlledPlayerId;
            bool isScore = matchEvent.EventType == MatchEventType.Score;
            if (isScore || isControlledEvent)
            {
                CreateImage(
                    $"RowHighlight_{matchEvent.Sequence}", panel,
                    isScore ? new Color(0.14f, 0.1f, 0.02f, 1f) : new Color(0.03f, 0.14f, 0.1f, 1f),
                    new Vector2(940f, TimelineRowHeight - 2f), new Vector2(0f, y));
            }

            CreateImage(
                $"RowMarker_{matchEvent.Sequence}", panel, GetTimelineMarkerColor(matchEvent, isControlledEvent),
                new Vector2(4f, 18f), new Vector2(-458f, y));
            CreateText(
                $"Row_{matchEvent.Sequence}", panel, description, 15, FontStyle.Normal,
                TextAnchor.MiddleLeft, new Vector2(700f, 26f), new Vector2(-90f, y),
                isControlledEvent ? RoleColor : PrimaryTextColor);
            CreateText(
                $"RowTrail_{matchEvent.Sequence}", panel, GetTimelineTrailingLabel(matchEvent), 14,
                FontStyle.Bold, TextAnchor.MiddleRight, new Vector2(240f, 26f), new Vector2(455f, y),
                isScore ? GoldColor : MutedTextColor);
        }

        private static Color GetTimelineMarkerColor(MatchEvent matchEvent, bool isControlledEvent)
        {
            if (isControlledEvent)
                return RoleColor;
            return matchEvent.EventType switch
            {
                MatchEventType.Score => GoldColor,
                MatchEventType.PlayerSubstitution => AccentColor,
                MatchEventType.PitcherEntered or MatchEventType.PitcherRemoved => AccentColor,
                MatchEventType.PinchHitterEntered or MatchEventType.PinchRunnerEntered => AccentColor,
                MatchEventType.FieldingError or MatchEventType.ThrowingError => DangerColor,
                MatchEventType.StealSucceeded or MatchEventType.DoublePlay => RoleColor,
                MatchEventType.RunnerAdvance => new Color(0.4f, 0.6f, 0.7f, 1f),
                _ => new Color(0.2f, 0.32f, 0.4f, 1f)
            };
        }
        private void RenderCompleted(CareerMatchSession session)
        {
            ClearPersistentControls();
            MatchResult result = session.MatchResult;
            CareerGameAdvanceResult careerResult = session.CareerResult ?? default;
            MatchNarrativeSnapshot narrative = session.NarrativeSnapshot ??
                                                 throw new InvalidOperationException(
                                                     "완료 경기의 내러티브 스냅샷이 없습니다.");
            bool isHome = session.ScheduledGame.HomeTeamId == narrative.TeamId;
            int playerRuns = isHome ? result.HomeBoxScore.Runs : result.AwayBoxScore.Runs;
            int opponentRuns = isHome ? result.AwayBoxScore.Runs : result.HomeBoxScore.Runs;
            string outcome = playerRuns > opponentRuns ? "WIN" : playerRuns < opponentRuns ? "LOSS" : "DRAW";
            Color outcomeColor = playerRuns > opponentRuns ? RoleColor : playerRuns < opponentRuns ? DangerColor : GoldColor;

            CreateText(
                "Eyebrow", _content, "FINAL", 15, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(300f, 28f), new Vector2(0f, 456f), AccentColor);
            CreateText(
                "Title", _content, "경기 종료", 44, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(500f, 62f), new Vector2(0f, 410f), PrimaryTextColor);
            CreateText(
                "Outcome", _content, outcome, 28, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(220f, 42f), new Vector2(0f, 358f), outcomeColor);
            CreateText(
                "NarrativeHeadline", _content, narrative.Headline, 18, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(1080f, 36f), new Vector2(0f, 318f), PrimaryTextColor);

            RectTransform scoreCard = CreatePanel(
                "ScoreCard", _content, new Vector2(1180f, 195f), new Vector2(0f, 190f));
            RenderFinalTeam(scoreCard, session.Input.AwayTeam.Name, result.AwayBoxScore.Runs, new Vector2(-300f, 0f));
            CreateText(
                "Colon", scoreCard, ":", 44, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(70f, 70f), Vector2.zero, SecondaryTextColor);
            RenderFinalTeam(scoreCard, session.Input.HomeTeam.Name, result.HomeBoxScore.Runs, new Vector2(300f, 0f));

            RectTransform personal = CreatePanel(
                "Personal", _content, new Vector2(720f, 410f), new Vector2(-245f, -145f));
            CreateText(
                "Label", personal, "개인 결과", 14, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(180f, 28f), new Vector2(-245f, 168f), AccentColor);
            CreateText(
                "Name", personal, _manager.CurrentCareer.MyPlayer.Name, 31, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Vector2(450f, 48f), new Vector2(-110f, 125f), PrimaryTextColor);

            string personalLine = session.PlayerRole == PlayerGameRole.StartingBatter
                ? BuildBatterGameLine(careerResult)
                : GetRoleResultLabel(
                    session.PlayerRole,
                    _manager.CurrentCareer.MyPlayer.PrimaryPosition,
                    careerResult,
                    CountPlayerPlateAppearances(session.Events, session.ControlledPlayerId));
            CreateText(
                "Line", personal, personalLine, 26, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(630f, 44f), new Vector2(0f, 72f), PrimaryTextColor);
            CreateText(
                "Discipline", personal,
                BuildDisciplineSummary(careerResult, session.PlayerRole),
                16, FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(630f, 32f), new Vector2(0f, 36f), SecondaryTextColor);
            CreateText(
                "SeasonRecord", personal, BuildSeasonRecordChange(narrative), 14, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Vector2(630f, 26f), new Vector2(0f, 8f), MutedTextColor);
            CreateText(
                "Performance", personal, narrative.PerformanceEvaluation, 17, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Vector2(630f, 32f), new Vector2(0f, -28f), GoldColor);
            CreateText(
                "PerformanceDetail", personal, narrative.PerformanceDetail, 15, FontStyle.Normal,
                TextAnchor.MiddleLeft, new Vector2(630f, 30f), new Vector2(0f, -65f), SecondaryTextColor);
            CreateText(
                "GameImpact", personal, narrative.GameImpact, 15, FontStyle.Normal,
                TextAnchor.MiddleLeft, new Vector2(630f, 42f), new Vector2(0f, -105f), PrimaryTextColor);
            if (!string.IsNullOrEmpty(narrative.RecentForm))
            {
                CreateText(
                    "RecentForm", personal, narrative.RecentForm, 14, FontStyle.Bold,
                    TextAnchor.MiddleLeft, new Vector2(630f, 28f), new Vector2(0f, -149f), AccentColor);
            }

            RectTransform change = CreatePanel(
                "Changes", _content, new Vector2(470f, 410f), new Vector2(370f, -145f));
            CreateText(
                "Label", change, "경기 후 변화", 14, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(200f, 28f), new Vector2(-120f, 168f), AccentColor);
            RenderChangeRow(
                change,
                "감독 신뢰",
                $"{GetEvaluationGrade(narrative.ManagerTrustBefore)} {narrative.ManagerTrustBefore}",
                $"{GetEvaluationGrade(narrative.ManagerTrustAfter)} {narrative.ManagerTrustAfter}",
                narrative.ManagerTrustAfter - narrative.ManagerTrustBefore,
                narrative.ManagerTrustReason,
                100f);
            RenderChangeRow(
                change,
                "컨디션",
                narrative.ConditionBefore.ToString(),
                narrative.ConditionAfter.ToString(),
                narrative.ConditionAfter - narrative.ConditionBefore,
                narrative.ConditionReason,
                24f);
            RenderStatusRow(
                change,
                "현재 역할",
                $"{GetShortRoleLabel(narrative.RoleAfter, narrative.PlayerPosition)} 유지",
                narrative.RoleReason,
                -52f);
            CreateText(
                "ManagerCommentLabel", change, $"감독 코멘트 · {GetManagerStyleLabel(narrative.ManagerStyle)}", 13, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Vector2(405f, 24f), new Vector2(0f, -112f), AccentColor);
            CreateText(
                "ManagerComment", change, narrative.ManagerComment, 14, FontStyle.Normal,
                TextAnchor.UpperLeft, new Vector2(405f, 60f), new Vector2(0f, -151f), PrimaryTextColor);

            Button nextDay = CreateButton(
                "NextDay", _content,
                _manager.CurrentCareer.Narrative.PendingReaction == null
                    ? "다음 날로   SPACE / ENTER"
                    : "경기 후 인터뷰로   SPACE / ENTER",
                new Vector2(520f, 72f), new Vector2(0f, -448f),
                new Color(0.02f, 0.38f, 0.7f, 1f), PrimaryTextColor);
            nextDay.onClick.AddListener(() => _manager.ReturnHomeFromCompletedMatch());
        }

        private static bool IsConfirmKeyPressed(Keyboard keyboard)
        {
            return keyboard.spaceKey.wasPressedThisFrame ||
                   keyboard.enterKey.wasPressedThisFrame ||
                   keyboard.numpadEnterKey.wasPressedThisFrame;
        }

        private void CreateApproachButton(
            RectTransform parent,
            BattingApproach approach,
            string title,
            string description,
            Vector2 position)
        {
            bool isSelected = _selectedApproach == approach;
            Color background = isSelected ? new Color(0.035f, 0.24f, 0.39f, 1f) : PanelDarkColor;
            Button button = CreateButton(
                "Approach_" + approach, parent, string.Empty,
                new Vector2(468f, 92f), position, background, PrimaryTextColor);
            if (isSelected)
                CreateImage("Selected", button.transform, AccentColor, new Vector2(5f, 84f), new Vector2(-231f, 0f));
            CreateText(
                "Title", button.transform, title, 19, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(400f, 30f), new Vector2(14f, 17f), PrimaryTextColor);
            CreateText(
                "Description", button.transform, description, 13, FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(400f, 25f), new Vector2(14f, -17f), isSelected ? AccentColor : SecondaryTextColor);
            button.onClick.AddListener(() => SelectApproach(approach));
        }

        private void SelectApproach(BattingApproach approach)
        {
            _selectedApproach = approach;
            CareerGameSettings settings = _manager.CurrentCareer.GameSettings;
            _manager.UpdateGameSettings(
                approach,
                settings.PitchingApproach,
                settings.MatchProgressMode,
                settings.GameSpeed,
                settings.AutoSlowOnPlayerEvent);
        }

        private void SyncCareerGameSettings()
        {
            CareerGameSettings settings = _manager?.CurrentCareer?.GameSettings;
            if (settings == null)
                return;
            _selectedApproach = settings.BattingApproach;
            _selectedPitchingApproach = settings.PitchingApproach;
            for (int index = 0; index < PlaybackSpeedRates.Length; index++)
            {
                if (Mathf.Approximately(PlaybackSpeedRates[index], settings.GameSpeed))
                {
                    _playbackSpeedStepIndex = index;
                    break;
                }
            }
        }

        private static string GetMatchProgressModeLabel(MatchProgressMode mode)
        {
            return mode switch
            {
                MatchProgressMode.FullGameWatch => "전체 경기 관전",
                MatchProgressMode.InterveneOnPlayer => "내 선수 때만 개입",
                MatchProgressMode.PlayerFocusAutomatic => "내 선수 중심 자동",
                MatchProgressMode.InstantResult => "즉시 결과",
                MatchProgressMode.MiniGame => "직접 플레이",
                _ => "내 선수 때만 개입"
            };
        }

        private static string GetMatchProgressModeGuide(
            MatchProgressMode mode,
            bool canReceiveDecision,
            bool isPitchingDecision)
        {
            return mode switch
            {
                MatchProgressMode.FullGameWatch => "경기 시작부터 종료까지 모든 타석을 선택한 배속으로 관전합니다.",
                MatchProgressMode.InterveneOnPlayer when canReceiveDecision =>
                    isPitchingDecision
                        ? "다른 장면은 빠르게 진행하고 등판·새 이닝 시작에서 투구 방침 입력을 기다립니다."
                        : "다른 선수는 빠르게 진행하고 내 선수 타석 직전에 멈춰 방침 입력을 기다립니다.",
                MatchProgressMode.InterveneOnPlayer =>
                    "현재 역할은 직접 입력 지점이 없어 모든 타석을 자동으로 진행합니다.",
                MatchProgressMode.PlayerFocusAutomatic =>
                    "다른 선수는 빠르게 진행하고 내 선수 장면은 멈추지 않고 강조해 보여줍니다.",
                MatchProgressMode.InstantResult => "연출을 생략하고 전체 경기를 한 번에 계산합니다.",
                MatchProgressMode.MiniGame when canReceiveDecision =>
                    isPitchingDecision
                        ? "내 등판에서 구종과 목표 위치를 직접 선택하고 결과 판정은 시뮬레이션에 맡깁니다."
                        : "내 타석에서 배트 위치와 스윙 시점을 직접 입력하고 타석 전체를 진행합니다.",
                MatchProgressMode.MiniGame => "오늘 역할에는 직접 플레이할 선수 관여 상황이 없습니다.",
                _ => string.Empty
            };
        }

        private static string GetMatchProgressModeStatus(
            MatchProgressMode mode,
            bool canReceiveDecision,
            bool isPitchingDecision)
        {
            return mode switch
            {
                MatchProgressMode.InterveneOnPlayer when canReceiveDecision =>
                    isPitchingDecision ? "자동 정지 · 새 투구 이닝" : "자동 정지 · 내 선수 타석",
                MatchProgressMode.InstantResult => "배속 사용 안 함",
                MatchProgressMode.MiniGame when canReceiveDecision =>
                    isPitchingDecision ? "직접 입력 · 내 투구" : "직접 입력 · 내 타석",
                _ => "자동 정지 없음"
            };
        }

        private void SelectPitchingApproach(PitchingApproach approach)
        {
            _selectedPitchingApproach = approach;
            CareerGameSettings settings = _manager.CurrentCareer.GameSettings;
            _manager.UpdateGameSettings(
                settings.BattingApproach,
                approach,
                settings.MatchProgressMode,
                settings.GameSpeed,
                settings.AutoSlowOnPlayerEvent);
        }

        private static void RenderVersusHeader(RectTransform parent, string away, string home, float y)
        {
            CreateTeamBadge(parent, away, new Vector2(-305f, y));
            CreateText(
                "AwayTeam", parent, away, 25, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(360f, 44f), new Vector2(-305f, y - 78f), PrimaryTextColor);
            CreateText(
                "Vs", parent, "VS", 48, FontStyle.BoldAndItalic, TextAnchor.MiddleCenter,
                new Vector2(120f, 70f), new Vector2(0f, y - 12f), PrimaryTextColor);
            CreateTeamBadge(parent, home, new Vector2(305f, y));
            CreateText(
                "HomeTeam", parent, home, 25, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(360f, 44f), new Vector2(305f, y - 78f), PrimaryTextColor);
        }

        private static void RenderFinalTeam(RectTransform parent, string name, int score, Vector2 position)
        {
            CreateText(
                name, parent, name, 22, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(380f, 38f), position + new Vector2(0f, 48f), SecondaryTextColor);
            CreateText(
                "Score_" + name, parent, score.ToString(), 54, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(180f, 72f), position + new Vector2(0f, -23f), PrimaryTextColor);
        }

        private static void RenderChangeRow(
            RectTransform parent,
            string label,
            string before,
            string after,
            int delta,
            string reason,
            float y)
        {
            bool isStable = delta == 0;
            Color valueColor = isStable ? MutedTextColor : delta > 0 ? RoleColor : DangerColor;
            string value = isStable
                ? $"{after} 유지"
                : $"{before}  →  {after}   {(delta > 0 ? "+" : string.Empty)}{delta}";
            CreateText(
                label, parent, label, 14, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(120f, 26f), new Vector2(-145f, y + 10f), SecondaryTextColor);
            CreateText(
                label + "Value", parent, value, 16, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(275f, 28f), new Vector2(72f, y + 10f), valueColor);
            CreateText(
                label + "Reason", parent, reason, 12, FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(275f, 22f), new Vector2(72f, y - 16f), MutedTextColor);
        }

        private static void RenderStatusRow(
            RectTransform parent,
            string label,
            string value,
            string reason,
            float y)
        {
            CreateText(
                label, parent, label, 14, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(120f, 26f), new Vector2(-145f, y + 10f), SecondaryTextColor);
            CreateText(
                label + "Value", parent, value, 16, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(275f, 28f), new Vector2(72f, y + 10f), MutedTextColor);
            CreateText(
                label + "Reason", parent, reason, 12, FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(275f, 22f), new Vector2(72f, y - 16f), MutedTextColor);
        }

        private static void CreateBase(
            RectTransform parent,
            string name,
            bool isOccupied,
            Vector2 position,
            bool isHome = false,
            bool isControlledRunner = false)
        {
            Color color = isHome
                ? new Color(0.8f, 0.86f, 0.9f, 1f)
                : isControlledRunner
                    ? RoleColor
                    : isOccupied ? GoldColor : new Color(0.2f, 0.32f, 0.34f, 1f);
            RectTransform baseRect = CreateImage(name, parent, color, new Vector2(38f, 38f), position);
            baseRect.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }

        private static void CreateStatusPill(Transform parent, string label, Vector2 size, Vector2 position)
        {
            RectTransform pill = CreateImage(
                "StatusPill", parent, new Color(0.04f, 0.18f, 0.18f, 1f), size, position);
            CreateText(
                "Label", pill, label, 14, FontStyle.Bold, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.zero, RoleColor, true);
        }

        private static PlayerTodayLine CalculateTodayLine(
            IReadOnlyList<MatchEvent> events,
            int visibleEventCount,
            int playerId)
        {
            var result = new PlayerTodayLine();
            for (int index = 0; index < visibleEventCount; index++)
            {
                MatchEvent matchEvent = events[index];
                if (matchEvent.EventType == MatchEventType.PlateAppearanceEnded && matchEvent.BatterId == playerId)
                {
                    result.PlateAppearances++;
                    if (matchEvent.PlateAppearanceResult is PlateAppearanceResult.Single or
                        PlateAppearanceResult.Double or PlateAppearanceResult.Triple or PlateAppearanceResult.HomeRun)
                    {
                        result.Hits++;
                    }
                    if (matchEvent.PlateAppearanceResult == PlateAppearanceResult.HomeRun)
                        result.HomeRuns++;
                    if (matchEvent.PlateAppearanceResult == PlateAppearanceResult.Strikeout)
                        result.Strikeouts++;
                    if (matchEvent.PlateAppearanceResult == PlateAppearanceResult.Walk)
                        result.Walks++;
                    if (matchEvent.PlateAppearanceResult == PlateAppearanceResult.HitByPitch)
                        result.HitByPitches++;
                }
                else if (matchEvent.EventType == MatchEventType.Score && matchEvent.BatterId == playerId)
                {
                    result.RunsBattedIn++;
                }
            }
            return result;
        }

        /// <summary>
        /// 오늘 경기 카드의 보조 지표 줄을 만든다. 사구는 발생한 경기에서만 붙여 줄이 길어지지 않게 한다.
        /// </summary>
        private static string BuildTodayDetailLine(PlayerTodayLine today)
        {
            string summary = $"볼넷 {today.Walks}  ·  삼진 {today.Strikeouts}  ·  홈런 {today.HomeRuns}";
            return today.HitByPitches > 0 ? $"{summary}  ·  사구 {today.HitByPitches}" : summary;
        }

        private static string FindPlayerName(MatchInput input, int playerId)
        {
            string name = FindPlayerName(input.AwayRoster, playerId);
            return string.IsNullOrEmpty(name) ? FindPlayerName(input.HomeRoster, playerId) : name;
        }

        private static string FindPlayerName(MatchRosterSnapshot roster, int playerId)
        {
            for (int index = 0; index < roster.StartingLineup.Count; index++)
            {
                if (roster.StartingLineup[index].Player.PlayerId == playerId)
                    return roster.StartingLineup[index].Player.Name;
            }
            if (roster.StartingPitcher.Player.PlayerId == playerId)
                return roster.StartingPitcher.Player.Name;
            for (int index = 0; index < roster.Bullpen.Count; index++)
            {
                if (roster.Bullpen[index].Player.PlayerId == playerId)
                    return roster.Bullpen[index].Player.Name;
            }
            for (int index = 0; index < roster.Bench.Count; index++)
            {
                if (roster.Bench[index].PlayerId == playerId)
                    return roster.Bench[index].Name;
            }
            return string.Empty;
        }

        private static string GetRunnerSituation(CareerMatchPlaybackSnapshot snapshot)
        {
            if (!snapshot.HasRunnerOnFirst && !snapshot.HasRunnerOnSecond && !snapshot.HasRunnerOnThird)
                return "주자 없음";
            if (snapshot.HasRunnerOnFirst && snapshot.HasRunnerOnSecond && snapshot.HasRunnerOnThird)
                return "만루";

            var labels = new List<string>(3);
            if (snapshot.HasRunnerOnFirst) labels.Add("1루");
            if (snapshot.HasRunnerOnSecond) labels.Add("2루");
            if (snapshot.HasRunnerOnThird) labels.Add("3루");
            return "주자 " + string.Join(", ", labels);
        }

        private static string GetRoleLabel(PlayerGameRole role, PlayerPosition position)
        {
            if (CareerGameRoleFormatter.IsPitcherRest(role, position))
                return CareerGameRoleFormatter.GetPitcherRestLabel(position);

            return role switch
            {
                PlayerGameRole.StartingBatter => $"선발 · {GetPositionCode(position)}",
                PlayerGameRole.StartingPitcher => "선발투수",
                PlayerGameRole.ReliefPitcher => "구원투수 대기",
                PlayerGameRole.Bench => "벤치 대기",
                _ => "출전 명단 제외"
            };
        }

        private static string GetShortRoleLabel(PlayerGameRole role, PlayerPosition position)
        {
            if (CareerGameRoleFormatter.IsPitcherRest(role, position))
                return CareerGameRoleFormatter.GetPitcherRestLabel(position);

            return role switch
            {
                PlayerGameRole.StartingBatter => "선발",
                PlayerGameRole.StartingPitcher => "선발투수",
                PlayerGameRole.ReliefPitcher => "구원",
                PlayerGameRole.Bench => "벤치",
                _ => "미출전"
            };
        }

        private static string GetRoleResultLabel(
            PlayerGameRole role,
            PlayerPosition position,
            CareerGameAdvanceResult result,
            int plateAppearances)
        {
            if (CareerGameRoleFormatter.IsPitcherRest(role, position))
                return $"{CareerGameRoleFormatter.GetPitcherRestLabel(position)} · 등판 없음";

            return role switch
            {
                PlayerGameRole.StartingPitcher or PlayerGameRole.ReliefPitcher =>
                    $"{result.OutsRecorded / 3}.{result.OutsRecorded % 3}이닝  {result.EarnedRuns}자책  {result.Strikeouts}삼진",
                PlayerGameRole.Bench when plateAppearances > 0 =>
                    $"대타 출전 · {result.AtBats}타수  {result.Hits}안타  {result.RunsBattedIn}타점",
                PlayerGameRole.Bench => "벤치 대기 · 출전 없음",
                _ => "오늘 경기 출전 없음"
            };
        }

        /// <summary>
        /// 볼넷·사구·삼진을 역할에 맞는 주체(타자가 얻은 것 / 투수가 허용한 것)로 묶는다.
        /// </summary>
        private static string BuildDisciplineSummary(CareerGameAdvanceResult result, PlayerGameRole role)
        {
            bool isPitcher = role is PlayerGameRole.StartingPitcher or PlayerGameRole.ReliefPitcher;
            int walks = isPitcher ? result.WalksAllowed : result.Walks;
            int hitByPitches = isPitcher ? result.HitBatters : result.HitByPitches;
            string summary = $"볼넷 {walks}  ·  삼진 {result.Strikeouts}";
            return hitByPitches > 0 ? $"{summary}  ·  사구 {hitByPitches}" : summary;
        }

        private static string BuildBatterGameLine(CareerGameAdvanceResult result)
        {
            string line = $"{result.AtBats}타수  {result.Hits}안타";
            if (result.HomeRuns > 0) line += $"  ·  {result.HomeRuns}홈런";
            if (result.RunsBattedIn > 0) line += $"  ·  {result.RunsBattedIn}타점";
            if (result.Walks > 0) line += $"  ·  {result.Walks}볼넷";
            if (result.HitByPitches > 0) line += $"  ·  {result.HitByPitches}사구";
            return line;
        }

        private static string BuildSeasonRecordChange(MatchNarrativeSnapshot narrative)
        {
            bool isPitcher = narrative.PlayerPosition is
                PlayerPosition.StartingPitcher or PlayerPosition.ReliefPitcher;
            if (isPitcher)
            {
                string before = narrative.SeasonEarnedRunAverageBefore.ToString("0.00");
                string after = narrative.SeasonEarnedRunAverageAfter.ToString("0.00");
                return before == after
                    ? $"시즌 평균자책점 {after} 유지"
                    : $"시즌 평균자책점 {before} → {after}";
            }

            string battingBefore = FormatAverage(narrative.SeasonBattingAverageBefore);
            string battingAfter = FormatAverage(narrative.SeasonBattingAverageAfter);
            return battingBefore == battingAfter
                ? $"시즌 타율 {battingAfter} 유지"
                : $"시즌 타율 {battingBefore} → {battingAfter}";
        }

        private static string GetManagerStyleLabel(ManagerNarrativeStyle style)
        {
            return style switch
            {
                ManagerNarrativeStyle.Results => "성과 중시형",
                ManagerNarrativeStyle.Development => "육성형",
                ManagerNarrativeStyle.Conservative => "보수형",
                _ => "분석형"
            };
        }

        private static string GetApproachLabel(BattingApproach approach)
        {
            return approach switch
            {
                BattingApproach.Contact => "컨택 타격",
                BattingApproach.Power => "장타 타격",
                BattingApproach.Patient => "신중한 타격",
                _ => "균형 타격"
            };
        }

        private static int CountPlayerPlateAppearances(
            IReadOnlyList<MatchEvent> events,
            int playerId)
        {
            int count = 0;
            for (int index = 0; index < events.Count; index++)
            {
                if (events[index].EventType == MatchEventType.PlateAppearanceEnded &&
                    events[index].BatterId == playerId)
                {
                    count++;
                }
            }
            return count;
        }

        private static string GetPitchResultLabel(PitchResult result)
        {
            return result switch
            {
                PitchResult.Ball => "볼",
                PitchResult.CalledStrike => "루킹 스트라이크",
                PitchResult.SwingingStrike => "헛스윙",
                PitchResult.Foul => "파울",
                PitchResult.InPlay => "인플레이",
                PitchResult.HitByPitch => "몸에 맞는 공",
                _ => string.Empty
            };
        }

        private static string GetPlateAppearanceResultLabel(PlateAppearanceResult result)
        {
            return GetPlateAppearanceResultLabel(result, 0);
        }

        private static string GetPlateAppearanceResultLabel(PlateAppearanceResult result, int outsOnPlay)
        {
            if (result == PlateAppearanceResult.GroundOut && outsOnPlay >= 2)
                return "병살타";
            return result switch
            {
                PlateAppearanceResult.Walk => "볼넷",
                PlateAppearanceResult.HitByPitch => "사구",
                PlateAppearanceResult.Strikeout => "삼진",
                PlateAppearanceResult.GroundOut => "땅볼 아웃",
                PlateAppearanceResult.FlyOut => "뜬공 아웃",
                PlateAppearanceResult.Single => "안타",
                PlateAppearanceResult.Double => "2루타",
                PlateAppearanceResult.Triple => "3루타",
                PlateAppearanceResult.HomeRun => "홈런",
                PlateAppearanceResult.ReachedOnError => "실책 출루",
                PlateAppearanceResult.FieldersChoice => "야수선택",
                PlateAppearanceResult.SacrificeBunt => "희생번트",
                PlateAppearanceResult.BuntSingle => "번트 안타",
                PlateAppearanceResult.BuntPopOut => "번트 뜬공",
                PlateAppearanceResult.IntentionalWalk => "고의사구",
                _ => string.Empty
            };
        }

        private static string GetEvaluationGrade(int value)
        {
            if (value >= 90) return "S";
            if (value >= 80) return "A";
            if (value >= 70) return "B+";
            if (value >= 60) return "B";
            if (value >= 50) return "C+";
            return "C";
        }

        private static string GetHalfLabel(InningHalf half)
        {
            return half == InningHalf.Top ? "초" : "말";
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

        private static string FormatAverage(double value)
        {
            return value.ToString(".000");
        }

        private static RectTransform CreatePanel(string name, Transform parent, Vector2 size, Vector2 position)
        {
            RectTransform panel = CreateImage(name, parent, PanelColor, size, position);
            CreateImage("TopBorder", panel, BorderColor, new Vector2(size.x, 2f), new Vector2(0f, size.y * 0.5f - 1f));
            return panel;
        }

        private static void CreateTeamBadge(Transform parent, string teamName, Vector2 position)
        {
            RectTransform badge = CreateImage(
                "Badge_" + teamName, parent, new Color(0.03f, 0.23f, 0.38f, 1f),
                new Vector2(118f, 118f), position);
            string initial = string.IsNullOrEmpty(teamName) ? "T" : teamName.Substring(0, 1);
            CreateText(
                "Initial", badge, initial, 48, FontStyle.Bold, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.zero, PrimaryTextColor, true);
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            RectTransform rect = gameObject.GetComponent<RectTransform>();
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
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            gameObject.transform.SetParent(parent, false);
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            if (stretch)
                Stretch(rect);
            else
            {
                rect.sizeDelta = size;
                rect.anchoredPosition = position;
            }
            gameObject.GetComponent<Image>().color = color;
            return rect;
        }

        private static Text CreateText(
            string name,
            Transform parent,
            string value,
            int fontSize,
            FontStyle fontStyle,
            TextAnchor alignment,
            Vector2 size,
            Vector2 position,
            Color color,
            bool stretch = false)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            gameObject.transform.SetParent(parent, false);
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            if (stretch)
                Stretch(rect);
            else
            {
                rect.sizeDelta = size;
                rect.anchoredPosition = position;
            }

            Text text = gameObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
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
            Color background,
            Color textColor)
        {
            RectTransform rect = CreateImage(name, parent, background, size, position);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = background * 1.18f;
            colors.pressedColor = background * 0.78f;
            colors.disabledColor = new Color(background.r, background.g, background.b, 0.45f);
            button.colors = colors;
            CreateText(
                "Label", rect, label, 18, FontStyle.Bold, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.zero, textColor, true);
            return button;
        }

        private static void ClearChildren(Transform parent)
        {
            for (int index = parent.childCount - 1; index >= 0; index--)
                Destroy(parent.GetChild(index).gameObject);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private struct PlayerTodayLine
        {
            public int PlateAppearances;
            public int Hits;
            public int HomeRuns;
            public int RunsBattedIn;
            public int Strikeouts;
            public int Walks;
            public int HitByPitches;
        }
    }
}
