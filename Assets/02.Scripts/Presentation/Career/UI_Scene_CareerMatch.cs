using System;
using System.Collections.Generic;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Career;
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
        /// 진행 속도 슬라이더의 눈금. 0.5배속 관전부터 3배속 속행까지 0.5배속 간격으로 고른다.
        /// </summary>
        private static readonly float[] PlaybackSpeedRates = { 0.5f, 1f, 1.5f, 2f, 2.5f, 3f };
        private const int DefaultPlaybackSpeedStepIndex = 1;

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

        private CareerManager _manager;
        private RectTransform _content;
        private BattingApproach _selectedApproach = BattingApproach.Balanced;
        [SerializeField, Min(0.1f)] private float automaticPlayIntervalSeconds = 0.42f;
        [SerializeField, Min(0.5f)] private float controlledResultHoldSeconds = 2f;

        /// <summary>
        /// 내 타석 결과는 배속을 올려도 이 시간보다 짧게 지나가지 않는다.
        /// 커리어에서 가장 중요한 장면을 읽지 못한 채 넘기지 않기 위한 하한이다.
        /// </summary>
        [SerializeField, Min(0.2f)] private float minimumControlledResultHoldSeconds = 1.1f;

        private readonly CareerMatchPlayback _playback = new CareerMatchPlayback();
        private CareerMatchSession _playbackSession;
        private RectTransform _overlay;
        private RectTransform _playbackSpeedControl;
        private Slider _playbackSpeedSlider;
        private Text _playbackSpeedValueLabel;
        private int _playbackSpeedStepIndex = DefaultPlaybackSpeedStepIndex;
        private CareerPlateAppearanceSummary _controlledResult;
        private bool _hasControlledResult;
        private bool _isPlaybackInitialized;
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

            // 속도 슬라이더는 Render()가 매 진행마다 지우는 _content 밖에 둔다.
            // 같은 곳에 있으면 드래그 도중 슬라이더가 파괴돼 조작이 끊긴다.
            _overlay = CreateRect("Overlay", root, new Vector2(1920f, 1080f), Vector2.zero);
            CreatePlaybackSpeedControl(_overlay, new Vector2(700f, -442f));
            SetPlaybackSpeedControlVisible(false);
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

            Keyboard keyboard = Keyboard.current;
            CareerMatchSession session = _manager.ActiveMatch;
            if (session.Phase == CareerMatchPhase.Preparation)
            {
                if (keyboard == null)
                    return;
                if (keyboard.rKey.wasPressedThisFrame)
                    _manager.StartPreparedGame(CareerMatchMode.ResultsOnly);
                else if (IsConfirmKeyPressed(keyboard))
                    _manager.StartPreparedGame(CareerMatchMode.PlayerFocus);
                return;
            }

            EnsurePlayback(session);
            if (UpdateAutomaticPlayback(session))
                return;

            if (session.Phase == CareerMatchPhase.Completed)
            {
                if (keyboard != null && IsConfirmKeyPressed(keyboard))
                    _manager.ReturnHomeFromCompletedMatch();
                return;
            }

            if (keyboard == null || !IsDecisionInputReady(session))
                return;
            if (keyboard.digit1Key.wasPressedThisFrame)
                SelectApproach(BattingApproach.Balanced);
            else if (keyboard.digit2Key.wasPressedThisFrame)
                SelectApproach(BattingApproach.Contact);
            else if (keyboard.digit3Key.wasPressedThisFrame)
                SelectApproach(BattingApproach.Power);
            else if (keyboard.digit4Key.wasPressedThisFrame)
                SelectApproach(BattingApproach.Patient);
            else if (keyboard.spaceKey.wasPressedThisFrame)
                SubmitSelectedApproach();
            else if (keyboard.aKey.wasPressedThisFrame)
                AutoCompleteCurrentPlateAppearance();
        }

        private void HandleCareerChanged()
        {
            if (_manager == null || !_manager.HasActiveMatch)
            {
                ResetPlayback();
                Hide();
                return;
            }

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
            SetPlaybackSpeedControlVisible(IsPlaybackViewVisible(session));
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
                        "1회부터 타자 결과가 자동으로 흐르고, 내 타석에서는 직접 눌러 다음 투구를 진행합니다.",
                    PlayerGameRole.Bench =>
                        "벤치에서 경기를 지켜보다 대타로 투입되면 자동 진행이 멈추고 내 타석 입력이 열립니다.",
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
            CreateText(
                "ModeValue", mode, "내 선수 중심", 24, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(320f, 38f), new Vector2(-300f, -6f), PrimaryTextColor);
            string modeGuide = session.CanReceiveBattingDecisions
                ? "모든 타석은 빠르게 자동 중계하고, 선발 또는 교체 출전한 내 선수의 타석에서 입력을 기다립니다."
                : "모든 타석을 빠르게 자동 중계하며 경기 중 언제든 결과 화면으로 바로 진행할 수 있습니다.";
            CreateText(
                "ModeGuide", mode, modeGuide,
                15, FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(650f, 30f), new Vector2(-42f, -39f), SecondaryTextColor);
            CreateStatusPill(
                mode,
                session.CanReceiveBattingDecisions ? "정지 조건 · 내 선수 타석" : "입력 대기 없음",
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
                _manager.StartPreparedGame(CareerMatchMode.PlayerFocus);
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
            RenderScoreboard(session, snapshot, isDecisionInputReady);

            RectTransform playerPanel = CreatePanel(
                "PlayerPanel", _content, new Vector2(420f, 860f), new Vector2(-735f, -45f));
            RenderPlayerPanel(playerPanel, session, _playback.VisibleEventCount);

            RectTransform fieldPanel = CreatePanel(
                "FieldPanel", _content, new Vector2(830f, 530f), new Vector2(-95f, 120f));
            RenderField(fieldPanel, session, snapshot, isDecisionInputReady);

            RectTransform logPanel = CreatePanel(
                "LogPanel", _content, new Vector2(830f, 310f), new Vector2(-95f, -315f));
            RenderRecentEvents(
                logPanel,
                session.Input,
                session.Events,
                _playback.VisibleEventCount,
                session.ControlledPlayerId);

            RectTransform commandPanel = CreatePanel(
                "CommandPanel", _content, new Vector2(500f, 860f), new Vector2(700f, -45f));
            RenderCommandPanel(commandPanel, session, snapshot, isDecisionInputReady);
        }

        private void RenderScoreboard(
            CareerMatchSession session,
            CareerMatchPlaybackSnapshot snapshot,
            bool isDecisionInputReady)
        {
            RectTransform bar = CreateImage(
                "Scoreboard", _content, TopBarColor, new Vector2(1920f, 100f), new Vector2(0f, 490f));
            CreateText(
                "Away", bar, session.Input.AwayTeam.Name, 19, FontStyle.Bold, TextAnchor.MiddleRight,
                new Vector2(330f, 38f), new Vector2(-510f, 14f), SecondaryTextColor);
            CreateText(
                "AwayScore", bar, snapshot.AwayScore.ToString(), 42, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(90f, 60f), new Vector2(-285f, 0f), PrimaryTextColor);
            CreateText(
                "Inning", bar, $"{snapshot.Inning}회{GetHalfLabel(snapshot.Half)}", 24, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(180f, 40f), new Vector2(0f, 13f), AccentColor);
            string playState = GetPlayerSideLabel(session, snapshot.Half);
            bool isShowingControlledResult = _hasControlledResult;
            string waitReason = isShowingControlledResult
                ? $"내 타석 결과 — {GetControlledResultLabel(_controlledResult)}"
                : isDecisionInputReady
                    ? $"입력 대기 — {_manager.CurrentCareer.MyPlayer.Name} 타석"
                    : $"{playState} · {GetPlaybackSpeedLabel()} 자동 중계";
            CreateText(
                "WaitReason", bar, waitReason, 14,
                FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(390f, 28f), new Vector2(0f, -24f),
                isShowingControlledResult
                    ? GetControlledResultColor(_controlledResult)
                    : isDecisionInputReady ? RoleColor : GoldColor);
            CreateText(
                "HomeScore", bar, snapshot.HomeScore.ToString(), 42, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(90f, 60f), new Vector2(285f, 0f), PrimaryTextColor);
            CreateText(
                "Home", bar, session.Input.HomeTeam.Name, 19, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(330f, 38f), new Vector2(510f, 14f), SecondaryTextColor);
            CreateText(
                "Count", bar, $"B {snapshot.Balls}  /  S {snapshot.Strikes}  /  O {snapshot.Outs}",
                18, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(300f, 38f), new Vector2(780f, 0f), PrimaryTextColor);
        }

        private void RenderPlayerPanel(
            RectTransform panel,
            CareerMatchSession session,
            int visibleEventCount)
        {
            PlayerState player = _manager.CurrentCareer.MyPlayer;
            CreateText(
                "Eyebrow", panel, "MY PLAYER", 12, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(180f, 24f), new Vector2(-100f, 386f), AccentColor);
            CreateText(
                "Name", panel, $"{player.Name} · {GetPositionCode(player.PrimaryPosition)}", 27,
                FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(360f, 42f), new Vector2(0f, 346f), PrimaryTextColor);
            CreateText(
                "Role", panel, GetRoleLabel(session.PlayerRole, player.PrimaryPosition), 15,
                FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(350f, 30f), new Vector2(0f, 312f), RoleColor);

            PlayerTodayLine today = CalculateTodayLine(
                session.Events,
                visibleEventCount,
                session.ControlledPlayerId);
            RectTransform todayCard = CreateImage(
                "Today", panel, CardColor, new Vector2(360f, 126f), new Vector2(0f, 212f));
            CreateText(
                "Label", todayCard, "오늘 경기", 13, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(150f, 25f), new Vector2(-82f, 38f), SecondaryTextColor);
            CreateText(
                "Value", todayCard, $"{today.PlateAppearances}타석  {today.Hits}안타  {today.RunsBattedIn}타점",
                25, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(320f, 40f), new Vector2(0f, 1f), PrimaryTextColor);
            CreateText(
                "Detail", todayCard, $"삼진 {today.Strikeouts}  ·  홈런 {today.HomeRuns}", 14,
                FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(320f, 26f), new Vector2(0f, -39f), SecondaryTextColor);

            CreateMetricCard(panel, "컨디션", session.ConditionBefore.ToString(), new Vector2(0f, 95f));
            CreateMetricCard(
                panel,
                "감독평가",
                GetEvaluationGrade(session.ManagerEvaluationBefore),
                new Vector2(0f, -5f));

            bool hasBattingInput = session.CanReceiveBattingDecisions;
            RectTransform instruction = CreateImage(
                "Instruction", panel, PanelDarkColor, new Vector2(360f, 145f), new Vector2(0f, -148f));
            CreateText(
                "Label", instruction,
                session.PlayerRole == PlayerGameRole.Bench && hasBattingInput
                    ? "대타 대기"
                    : hasBattingInput ? "타격 포커스" : "경기 포커스",
                13,
                FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(150f, 26f), new Vector2(-83f, 48f), GoldColor);
            CreateText(
                "Value", instruction,
                session.PlayerRole == PlayerGameRole.Bench && hasBattingInput
                    ? "교체 출전 시 내 타석에서 자동 정지"
                    : hasBattingInput ? "현재 상황에 맞춰 타격 방식을 선택" : "공격·수비 흐름과 주자 상황 관전",
                17, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Vector2(320f, 32f), new Vector2(0f, 10f), PrimaryTextColor);
            CreateText(
                "Guide", instruction,
                hasBattingInput
                    ? session.PlayerRole == PlayerGameRole.Bench
                        ? "감독이 대타로 호출하면 선발 타자와 같은 방식으로 타격을 선택합니다."
                        : "선택의 장단점은 실제 투구·타구 확률에 반영됩니다."
                    : "감독 AI의 기용과 경기 결과는 같은 이벤트 흐름으로 표시됩니다.",
                13,
                FontStyle.Normal, TextAnchor.UpperLeft,
                new Vector2(320f, 48f), new Vector2(0f, -39f), SecondaryTextColor);

            CreateText(
                "Season", panel,
                $"시즌  AVG {FormatAverage(_manager.Dashboard.Statistics.BattingAverage)}  ·  " +
                $"HR {_manager.Dashboard.Statistics.HomeRuns}  ·  RBI {_manager.Dashboard.Statistics.RunsBattedIn}",
                14, FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(360f, 32f), new Vector2(0f, -300f), MutedTextColor);
        }

        private void RenderField(
            RectTransform panel,
            CareerMatchSession session,
            CareerMatchPlaybackSnapshot snapshot,
            bool isDecisionInputReady)
        {
            CreateText(
                "Situation", panel,
                $"{snapshot.Inning}회{GetHalfLabel(snapshot.Half)} · {snapshot.Outs}사 · " +
                GetRunnerSituation(snapshot),
                17, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(650f, 34f), new Vector2(0f, 220f), SecondaryTextColor);

            RectTransform diamond = CreateImage(
                "Diamond", panel, new Color(0.03f, 0.19f, 0.13f, 1f),
                new Vector2(410f, 250f), new Vector2(-165f, 42f));
            CreateBase(
                diamond,
                "Second",
                snapshot.HasRunnerOnSecond,
                new Vector2(0f, 75f),
                isControlledRunner: snapshot.SecondRunnerId == session.ControlledPlayerId);
            CreateBase(
                diamond,
                "First",
                snapshot.HasRunnerOnFirst,
                new Vector2(90f, 0f),
                isControlledRunner: snapshot.FirstRunnerId == session.ControlledPlayerId);
            CreateBase(
                diamond,
                "Third",
                snapshot.HasRunnerOnThird,
                new Vector2(-90f, 0f),
                isControlledRunner: snapshot.ThirdRunnerId == session.ControlledPlayerId);
            CreateBase(diamond, "Home", true, new Vector2(0f, -75f), isHome: true);
            if (IsControlledPlayerOnBase(snapshot, session.ControlledPlayerId))
            {
                CreateText(
                    "ControlledRunner", diamond, "내 선수 주루 중", 13, FontStyle.Bold,
                    TextAnchor.MiddleCenter, new Vector2(200f, 26f), new Vector2(0f, -106f), RoleColor);
            }

            string batterName = snapshot.BatterId == 0
                ? "공수 교대"
                : FindPlayerName(session.Input, snapshot.BatterId);
            string pitcherName = FindPlayerName(session.Input, snapshot.PitcherId);
            RectTransform matchup = CreateImage(
                "Matchup", panel, PanelDarkColor, new Vector2(330f, 250f), new Vector2(205f, 42f));
            CreateText(
                "Label", matchup, "CURRENT MATCHUP", 11, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(280f, 24f), new Vector2(0f, 88f), AccentColor);
            CreateText(
                "Batter", matchup, batterName, 24, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(290f, 38f), new Vector2(0f, 42f), PrimaryTextColor);
            CreateText(
                "Vs", matchup, "VS", 14, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(80f, 26f), new Vector2(0f, 7f), SecondaryTextColor);
            CreateText(
                "Pitcher", matchup, pitcherName, 21, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(290f, 35f), new Vector2(0f, -28f), PrimaryTextColor);
            string momentLabel = _hasControlledResult
                ? GetControlledResultLabel(_controlledResult)
                : isDecisionInputReady && session.PendingDecision.HasValue
                    ? $"{session.PendingDecision.Value.PitchNumber}구 예정  ·  {snapshot.Balls}-{snapshot.Strikes}"
                    : GetPlaybackMomentLabel(snapshot);
            CreateText(
                "Count", matchup, momentLabel, 15,
                FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(270f, 30f), new Vector2(0f, -76f), GoldColor);

            string hint = _hasControlledResult
                ? $"내 타석 결과를 확인 중입니다. 잠시 후 {GetPlaybackSpeedLabel()}으로 이어집니다."
                : isDecisionInputReady
                    ? "타격 방식을 고르고 Space로 다음 투구를 진행합니다."
                    : session.CanReceiveBattingDecisions
                        ? "각 타자의 결과를 자동 중계 중입니다. 내 선수가 출전하면 자동으로 멈춥니다."
                        : "각 타자의 결과를 자동 중계 중입니다. 경기 종료까지 바로 진행할 수도 있습니다.";
            CreateText(
                "Hint", panel, hint,
                14, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(730f, 30f), new Vector2(0f, -205f), SecondaryTextColor);
        }

        private void RenderRecentEvents(
            RectTransform panel,
            MatchInput input,
            IReadOnlyList<MatchEvent> events,
            int visibleEventCount,
            int controlledPlayerId)
        {
            CreateText(
                "Title", panel, "실시간 경기 흐름", 16, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(260f, 30f), new Vector2(-255f, 122f), PrimaryTextColor);
            int shown = 0;
            for (int index = visibleEventCount - 1; index >= 0 && shown < 5; index--)
            {
                if (!IsVisibleLogEvent(events[index], controlledPlayerId))
                    continue;

                string description = DescribeEvent(input, events, index);
                if (string.IsNullOrEmpty(description))
                    continue;

                Color color = events[index].EventType is MatchEventType.Score or MatchEventType.PlateAppearanceEnded
                    ? GoldColor
                    : SecondaryTextColor;
                CreateText(
                    "Log" + shown, panel, description, 15, FontStyle.Normal, TextAnchor.MiddleLeft,
                    new Vector2(750f, 34f), new Vector2(0f, 78f - shown * 42f), color);
                shown++;
            }

            if (shown == 0)
            {
                CreateText(
                    "Empty", panel, "1회초 첫 타자의 결과부터 자동으로 중계합니다.", 15,
                    FontStyle.Normal, TextAnchor.MiddleCenter,
                    new Vector2(700f, 40f), Vector2.zero, SecondaryTextColor);
            }
        }

        private void RenderCommandPanel(
            RectTransform panel,
            CareerMatchSession session,
            CareerMatchPlaybackSnapshot snapshot,
            bool isDecisionInputReady)
        {
            if (!isDecisionInputReady)
            {
                if (_hasControlledResult)
                    RenderControlledResultCommandPanel(panel, session, snapshot);
                else
                    RenderAutomaticCommandPanel(panel, session, snapshot);
                return;
            }

            MatchDecisionRequest request = session.PendingDecision.Value;
            CreateStatusPill(panel, "입력 대기 · 내 타석", new Vector2(410f, 46f), new Vector2(0f, 382f));
            CreateText(
                "Title", panel, "타격 접근 선택", 25, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(410f, 42f), new Vector2(0f, 330f), PrimaryTextColor);
            CreateText(
                "Count", panel, $"현재 Count  {request.Balls}-{request.Strikes}", 15,
                FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(410f, 30f), new Vector2(0f, 296f), SecondaryTextColor);

            CreateApproachButton(panel, BattingApproach.Balanced, "1  균형 타격", "기본값 · 특별한 보정 없음", 222f);
            CreateApproachButton(panel, BattingApproach.Contact, "2  컨택 타격", "접촉 높음 · 장타 낮음", 130f);
            CreateApproachButton(panel, BattingApproach.Power, "3  장타 타격", "장타 높음 · 헛스윙 위험", 38f);
            CreateApproachButton(panel, BattingApproach.Patient, "4  신중한 타격", "볼 고르기 · 좋은 공을 놓칠 수 있음", -54f);

            CreateText(
                "Current", panel, $"현재 선택 · {GetApproachLabel(_selectedApproach)}", 16,
                FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(410f, 34f), new Vector2(0f, -119f), AccentColor);
            Button nextPitch = CreateButton(
                "NextPitch", panel, "다음 투구   SPACE", new Vector2(410f, 68f), new Vector2(0f, -172f),
                new Color(0.02f, 0.38f, 0.7f, 1f), PrimaryTextColor);
            nextPitch.onClick.AddListener(() =>
            {
                nextPitch.interactable = false;
                SubmitSelectedApproach();
            });
            Button autoPlateAppearance = CreateButton(
                "AutoPlateAppearance", panel, "현재 타석 자동 진행   A",
                new Vector2(410f, 56f), new Vector2(0f, -242f), PanelDarkColor, SecondaryTextColor);
            autoPlateAppearance.onClick.AddListener(AutoCompleteCurrentPlateAppearance);
            Button autoMatch = CreateButton(
                "AutoMatch", panel, "경기 종료까지 진행",
                new Vector2(410f, 48f), new Vector2(0f, -306f),
                new Color(0.09f, 0.14f, 0.18f, 1f), MutedTextColor);
            autoMatch.onClick.AddListener(AutoCompleteMatch);

            if (!string.IsNullOrEmpty(_manager.LastError))
            {
                CreateText(
                    "Error", panel, _manager.LastError, 13, FontStyle.Normal, TextAnchor.MiddleCenter,
                    new Vector2(420f, 22f), new Vector2(0f, -344f), DangerColor);
            }
        }

        private void RenderCompleted(CareerMatchSession session)
        {
            MatchResult result = session.MatchResult;
            CareerGameAdvanceResult careerResult = session.CareerResult ?? default;
            bool isHome = session.Input.HomeTeam.TeamId == _manager.CurrentCareer.MyPlayer.CurrentTeamId;
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

            RectTransform scoreCard = CreatePanel(
                "ScoreCard", _content, new Vector2(1180f, 210f), new Vector2(0f, 205f));
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
                ? $"{careerResult.AtBats}타수  {careerResult.Hits}안타  {careerResult.HomeRuns}홈런  {careerResult.RunsBattedIn}타점"
                : GetRoleResultLabel(
                    session.PlayerRole,
                    _manager.CurrentCareer.MyPlayer.PrimaryPosition,
                    careerResult,
                    CountPlayerPlateAppearances(session.Events, session.ControlledPlayerId));
            CreateText(
                "Line", personal, personalLine, 26, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(630f, 44f), new Vector2(0f, 62f), PrimaryTextColor);
            CreateText(
                "Discipline", personal,
                $"{BuildDisciplineSummary(careerResult, session.PlayerRole)}  ·  최종 Score {playerRuns}:{opponentRuns}",
                16, FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(630f, 32f), new Vector2(0f, 20f), SecondaryTextColor);
            CreateText(
                "Highlight", personal,
                BuildHighlightText(
                    careerResult,
                    _manager.CurrentCareer.MyPlayer.PrimaryPosition,
                    CountPlayerPlateAppearances(session.Events, session.ControlledPlayerId)),
                16, FontStyle.Normal,
                TextAnchor.UpperLeft, new Vector2(630f, 94f), new Vector2(0f, -56f), GoldColor);
            CreateText(
                "LogGuide", personal, "전체 경기 로그는 결과 화면이 닫히기 전까지 현재 세션에 보존됩니다.",
                13, FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(630f, 28f), new Vector2(0f, -155f), MutedTextColor);

            RectTransform change = CreatePanel(
                "Changes", _content, new Vector2(470f, 410f), new Vector2(370f, -145f));
            CreateText(
                "Label", change, "경기 후 변화", 14, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(200f, 28f), new Vector2(-120f, 168f), AccentColor);
            RenderChangeRow(
                change, "감독평가", GetEvaluationGrade(session.ManagerEvaluationBefore),
                GetEvaluationGrade(session.ManagerEvaluationAfter), 102f);
            RenderChangeRow(
                change, "컨디션", session.ConditionBefore.ToString(), session.ConditionAfter.ToString(), 35f);
            RenderChangeRow(
                change,
                "현재 역할",
                GetShortRoleLabel(session.PlayerRole, _manager.CurrentCareer.MyPlayer.PrimaryPosition),
                "반영 완료",
                -32f);
            RenderChangeRow(
                change, "시즌 기록",
                "경기 전",
                session.PlayerRole == PlayerGameRole.StartingBatter
                    ? $"AVG {FormatAverage(_manager.Dashboard.Statistics.BattingAverage)}"
                    : "기록 갱신",
                -99f);

            Button nextDay = CreateButton(
                "NextDay", _content, "다음 날로   SPACE / ENTER", new Vector2(520f, 72f), new Vector2(0f, -448f),
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
            float y)
        {
            bool isSelected = _selectedApproach == approach;
            Color background = isSelected ? new Color(0.035f, 0.24f, 0.39f, 1f) : PanelDarkColor;
            Button button = CreateButton(
                "Approach_" + approach, parent, string.Empty,
                new Vector2(410f, 78f), new Vector2(0f, y), background, PrimaryTextColor);
            if (isSelected)
                CreateImage("Selected", button.transform, AccentColor, new Vector2(5f, 70f), new Vector2(-202f, 0f));
            CreateText(
                "Title", button.transform, title, 18, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(350f, 30f), new Vector2(8f, 15f), PrimaryTextColor);
            CreateText(
                "Description", button.transform, description, 13, FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(350f, 25f), new Vector2(8f, -17f), isSelected ? AccentColor : SecondaryTextColor);
            button.onClick.AddListener(() => SelectApproach(approach));
        }

        private void SelectApproach(BattingApproach approach)
        {
            _selectedApproach = approach;
            Render();
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
            float y)
        {
            CreateText(
                label, parent, label, 14, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(120f, 30f), new Vector2(-145f, y), SecondaryTextColor);
            CreateText(
                label + "Before", parent, before, 17, FontStyle.Bold, TextAnchor.MiddleRight,
                new Vector2(100f, 32f), new Vector2(-35f, y), MutedTextColor);
            CreateText(
                label + "Arrow", parent, "→", 17, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(40f, 32f), new Vector2(38f, y), SecondaryTextColor);
            CreateText(
                label + "After", parent, after, 18, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(150f, 32f), new Vector2(135f, y), RoleColor);
        }

        private static void CreateMetricCard(RectTransform parent, string label, string value, Vector2 position)
        {
            RectTransform card = CreateImage(
                label, parent, PanelDarkColor, new Vector2(360f, 82f), position);
            CreateText(
                "Label", card, label, 13, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(150f, 28f), new Vector2(-82f, 18f), SecondaryTextColor);
            CreateText(
                "Value", card, value, 25, FontStyle.Bold, TextAnchor.MiddleRight,
                new Vector2(130f, 38f), new Vector2(92f, -2f), RoleColor);
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
                }
                else if (matchEvent.EventType == MatchEventType.Score && matchEvent.BatterId == playerId)
                {
                    result.RunsBattedIn++;
                }
            }
            return result;
        }

        private static string DescribeEvent(
            MatchInput input,
            IReadOnlyList<MatchEvent> events,
            int eventIndex)
        {
            MatchEvent matchEvent = events[eventIndex];
            string prefix = $"{matchEvent.Inning}회{GetHalfLabel(matchEvent.Half)}";
            string batterName = FindPlayerName(input, matchEvent.BatterId);
            string playerName = FindPlayerName(input, matchEvent.PlayerId);
            return matchEvent.EventType switch
            {
                MatchEventType.Pitch =>
                    $"{prefix} · {batterName} · {matchEvent.Balls}-{matchEvent.Strikes} " +
                    GetPitchResultLabel(matchEvent.PitchResult),
                MatchEventType.RunnerAdvance =>
                    $"{prefix} · {playerName} · {GetBaseLabel(matchEvent.FromBase)} → " +
                    GetBaseLabel(matchEvent.ToBase),
                MatchEventType.Score =>
                    $"{prefix} · {playerName} 홈인 · {matchEvent.AwayScore}:{matchEvent.HomeScore}",
                MatchEventType.PlateAppearanceEnded =>
                    $"{prefix} · {batterName} · " +
                    GetPlateAppearanceResultLabel(
                        matchEvent.PlateAppearanceResult,
                        CountOutsInPlateAppearance(events, eventIndex)),
                MatchEventType.PlayerSubstitution =>
                    $"{prefix} · {batterName} 대타 출전 · {playerName} 교체",
                MatchEventType.HalfInningEnded => $"{prefix} 종료 · {matchEvent.AwayScore}:{matchEvent.HomeScore}",
                _ => string.Empty
            };
        }

        private static string FindPlayerName(MatchInput input, int playerId)
        {
            string name = FindPlayerName(input.AwayTeam, playerId);
            return string.IsNullOrEmpty(name) ? FindPlayerName(input.HomeTeam, playerId) : name;
        }

        private static string FindPlayerName(Team team, int playerId)
        {
            for (int index = 0; index < team.Lineup.Count; index++)
            {
                if (team.Lineup[index].Player.PlayerId == playerId)
                    return team.Lineup[index].Player.Name;
            }
            if (team.StartingPitcher.PlayerId == playerId)
                return team.StartingPitcher.Name;
            if (team.ReliefPitcher != null && team.ReliefPitcher.PlayerId == playerId)
                return team.ReliefPitcher.Name;
            if (team.PositionPlayerSubstitution?.Player.PlayerId == playerId)
                return team.PositionPlayerSubstitution.Player.Name;
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

        private static string BuildHighlightText(
            CareerGameAdvanceResult result,
            PlayerPosition position,
            int plateAppearances)
        {
            if (result.HomeRuns > 0)
                return $"핵심 장면 · 홈런 {result.HomeRuns}개로 {result.RunsBattedIn}타점 기록";
            if (result.Hits >= 2)
                return $"핵심 장면 · 멀티히트 {result.Hits}안타";
            if (result.Hits == 1)
                return "핵심 장면 · 안타로 출루에 성공";
            if (result.Walks + result.HitByPitches > 0)
                return $"핵심 장면 · 사사구 {result.Walks + result.HitByPitches}개로 출루";
            if (CareerGameRoleFormatter.IsPitcherRest(result.Role, position))
                return $"감독 결정 · 오늘은 {CareerGameRoleFormatter.GetPitcherRestLabel(position)}";
            if (result.Role == PlayerGameRole.Bench && plateAppearances == 0)
                return "감독 결정 · 오늘은 벤치에서 대기";
            if (result.Role == PlayerGameRole.Bench)
                return "대타 기회를 얻었지만 출루에는 실패했습니다.";
            return "다음 경기에서 반등을 노립니다.";
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
        }
    }
}
