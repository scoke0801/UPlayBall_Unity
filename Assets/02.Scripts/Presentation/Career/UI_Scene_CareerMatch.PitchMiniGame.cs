using System;
using Baseball.Core.Players;
using Baseball.Game.Career;
using Baseball.Simulation.Match;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Baseball.Presentation.Career
{
    public sealed partial class UI_Scene_CareerMatch
    {
        private const int PitchPresentationTrailCapacity = 8;
        private const int PitchPreviewDotCount = 8;
        private const float PitchTargetAimSpeed = 1.75f;

        [SerializeField] private PitchTrajectoryPresentationConfig pitchTrajectoryPresentation = new();

        private readonly PitchMiniGamePresentationController _pitchMiniGame = new();
        private readonly RectTransform[] _pitchPresentationTrail =
            new RectTransform[PitchPresentationTrailCapacity];
        private readonly RectTransform[] _pitchPreviewDots = new RectTransform[PitchPreviewDotCount];

        private RectTransform _pitchPresentationRoot;
        private PitchTrajectoryPresenter _pitchTrajectoryPresenter;
        private PitchAimOverlay _pitchAimOverlay;
        private PitchResultFeedbackPresenter _pitchResultFeedbackPresenter;
        private Button _pitchMiniGamePrimaryButton;
        private Button _pitchMiniGameSelectedPitchButton;

        /// <summary>투구마다 재생성하지 않을 공·잔상·목표 피드백 계층을 초기화한다.</summary>
        private void InitializePitchMiniGamePresentation(RectTransform controlLayer)
        {
            _pitchPresentationRoot = CreateRect(
                "PitchPresentationLayer",
                controlLayer,
                new Vector2(660f, 390f),
                new Vector2(45f, 88f));

            for (int index = 0; index < _pitchPreviewDots.Length; index++)
            {
                _pitchPreviewDots[index] = CreateMiniGameSpriteImage(
                    "PitchPreviewDot_" + index,
                    _pitchPresentationRoot,
                    GetMiniGameSolidCircleSprite(),
                    new Color(0.25f, 0.78f, 1f, Mathf.Lerp(0.14f, 0.48f, index / 7f)),
                    new Vector2(7f, 7f),
                    Vector2.zero);
            }

            for (int index = _pitchPresentationTrail.Length - 1; index >= 0; index--)
            {
                _pitchPresentationTrail[index] = CreateMiniGameSpriteImage(
                    "PitchActualTrail_" + index,
                    _pitchPresentationRoot,
                    GetMiniGameSolidCircleSprite(),
                    new Color(0.86f, 0.95f, 1f, Mathf.Lerp(0.05f, 0.28f, 1f - index / 8f)),
                    new Vector2(10f, 10f),
                    Vector2.zero);
            }

            RectTransform connector = CreateImage(
                "PitchCommandConnector",
                _pitchPresentationRoot,
                new Color(0.85f, 0.92f, 0.96f, 0.58f),
                new Vector2(1f, 2f),
                Vector2.zero);
            connector.GetComponent<Image>().raycastTarget = false;
            RectTransform commandEllipse = CreateMiniGameSpriteImage(
                "PitchCommandEllipse",
                _pitchPresentationRoot,
                GetMiniGameRingSprite(),
                new Color(0.12f, 0.64f, 1f, 0.28f),
                new Vector2(80f, 62f),
                Vector2.zero);
            RectTransform target = CreateRect(
                "PitchCommandTarget",
                _pitchPresentationRoot,
                new Vector2(34f, 34f),
                Vector2.zero);
            CreateMiniGameSpriteImage(
                "TargetRing",
                target,
                GetMiniGameRingSprite(),
                new Color(0.98f, 0.70f, 0.18f, 0.94f),
                new Vector2(24f, 24f),
                Vector2.zero);
            CreateImage("TargetHorizontal", target, GoldColor, new Vector2(34f, 2f), Vector2.zero)
                .GetComponent<Image>().raycastTarget = false;
            CreateImage("TargetVertical", target, GoldColor, new Vector2(2f, 34f), Vector2.zero)
                .GetComponent<Image>().raycastTarget = false;
            RectTransform actual = CreateMiniGameSpriteImage(
                "PitchActualPoint",
                _pitchPresentationRoot,
                GetMiniGameRingSprite(),
                new Color(0.30f, 0.92f, 0.66f, 1f),
                new Vector2(30f, 30f),
                Vector2.zero);

            RectTransform ball = CreateMiniGameSpriteImage(
                "PitchPresentationBall",
                _pitchPresentationRoot,
                GetMiniGameSolidCircleSprite(),
                new Color(0.98f, 0.98f, 0.92f, 1f),
                new Vector2(10f, 10f),
                Vector2.zero);
            RectTransform leftStitch = CreateImage(
                "PitchBallStitchLeft",
                ball,
                new Color(0.72f, 0.18f, 0.16f, 0.9f),
                new Vector2(1.6f, 9f),
                new Vector2(-3.5f, 0f));
            leftStitch.GetComponent<Image>().raycastTarget = false;
            RectTransform rightStitch = CreateImage(
                "PitchBallStitchRight",
                ball,
                new Color(0.72f, 0.18f, 0.16f, 0.9f),
                new Vector2(1.6f, 9f),
                new Vector2(3.5f, 0f));
            rightStitch.GetComponent<Image>().raycastTarget = false;
            leftStitch.localRotation = Quaternion.Euler(0f, 0f, -18f);
            rightStitch.localRotation = Quaternion.Euler(0f, 0f, 18f);

            Text commandText = CreateText(
                "PitchCommandFeedback",
                _pitchPresentationRoot,
                string.Empty,
                18,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                new Vector2(560f, 30f),
                new Vector2(0f, 170f),
                RoleColor);
            Text resultText = CreateText(
                "PitchPlayFeedback",
                _pitchPresentationRoot,
                string.Empty,
                14,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                new Vector2(600f, 24f),
                new Vector2(0f, 144f),
                PrimaryTextColor);
            commandText.raycastTarget = false;
            resultText.raycastTarget = false;

            _pitchTrajectoryPresenter = new PitchTrajectoryPresenter(
                _pitchPresentationRoot,
                ball,
                _pitchPresentationTrail,
                _pitchPreviewDots,
                pitchTrajectoryPresentation);
            _pitchAimOverlay = new PitchAimOverlay(target, commandEllipse, actual, connector);
            _pitchResultFeedbackPresenter = new PitchResultFeedbackPresenter(commandText, resultText);
            HidePitchMiniGamePresentation();
        }

        private bool IsPitchMiniGameStageVisible(CareerMatchSession session)
        {
            return session != null &&
                   session.Mode == CareerMatchMode.MiniGame &&
                   (_pitchMiniGame.IsStageVisible ||
                    session.Phase == CareerMatchPhase.Playing &&
                    session.PendingPitchSelection.HasValue &&
                    !_hasControlledResult &&
                    !_playback.HasPendingEvents(session.Events));
        }

        private void EnsurePitchMiniGameRequest(in PitchSelectionRequest request)
        {
            if (_pitchMiniGame.IsPresentationActive ||
                _pitchMiniGame.State == PitchMiniGamePresentationState.PitchConfirmed)
                return;

            if (!_pitchMiniGame.EnsureRequest(request))
                return;

        }

        private void RenderPitchMiniGameStage(RectTransform panel)
        {
            PitchSelectionRequest request = _pitchMiniGame.Request;
            CreateText(
                "MiniGameTitle",
                panel,
                "직접 투구",
                25,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                new Vector2(360f, 36f),
                new Vector2(0f, 294f),
                RoleColor);
            RenderPitchMiniGameProgressSteps(panel);
            CreateText(
                "MiniGameSituation",
                panel,
                $"{request.Inning}회{GetHalfLabel(request.Half)} · {request.Outs}사 · " +
                $"볼 {request.Balls} · 스트라이크 {request.Strikes}",
                15,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                new Vector2(820f, 26f),
                new Vector2(0f, 220f),
                SecondaryTextColor);

            RenderPitchCards(panel, request);
            _miniGamePlateRect = CreateImage(
                "PitchTargetPlane",
                panel,
                new Color(0.008f, 0.027f, 0.045f, 1f),
                new Vector2(660f, 390f),
                new Vector2(115f, -5f));
            Image targetPlaneImage = _miniGamePlateRect.GetComponent<Image>();
            targetPlaneImage.raycastTarget = _pitchMiniGame.IsInputUnlocked;
            if (_pitchMiniGame.IsInputUnlocked)
            {
                Button targetButton = _miniGamePlateRect.gameObject.AddComponent<Button>();
                targetButton.transition = Selectable.Transition.None;
                targetButton.onClick.AddListener(UpdatePitchMiniGameTargetFromPointer);
            }

            RenderPitchMiniGameField(_miniGamePlateRect);
            CreateText(
                "PitchTargetGuide",
                panel,
                "목표 위치 · 드래그 / 방향키",
                13,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                new Vector2(620f, 24f),
                new Vector2(115f, -215f),
                GoldColor);
            CreateText(
                "PitchCommandGuide",
                panel,
                "예상 제구 범위 · Control · 숙련도 · 구종 난이도 반영",
                12,
                FontStyle.Normal,
                TextAnchor.MiddleCenter,
                new Vector2(660f, 22f),
                new Vector2(115f, -238f),
                SecondaryTextColor);
            CreateText(
                "PitchInputGuide",
                panel,
                "마우스 드래그 · 방향키/SHIFT 미세 · 왼쪽 스틱  /  Q·E · RB 방침",
                11,
                FontStyle.Normal,
                TextAnchor.MiddleCenter,
                new Vector2(660f, 20f),
                new Vector2(115f, -260f),
                MutedTextColor);
            RefreshPitchMiniGameOverlay();
        }

        private void RenderPitchMiniGameProgressSteps(RectTransform panel)
        {
            PitchMiniGamePresentationState state = _pitchMiniGame.State;
            bool isPreparing = state is PitchMiniGamePresentationState.PrePitchReady or
                PitchMiniGamePresentationState.PitchSelection or
                PitchMiniGamePresentationState.TargetAiming or
                PitchMiniGamePresentationState.StrategySelection;
            bool isFlight = state is PitchMiniGamePresentationState.PitchConfirmed or
                PitchMiniGamePresentationState.Windup or
                PitchMiniGamePresentationState.BallInFlight;
            bool isResult = state is PitchMiniGamePresentationState.PlateArrival or
                PitchMiniGamePresentationState.BatterReaction or
                PitchMiniGamePresentationState.PitchResult;
            CreateText(
                "PitchPrepareStep",
                panel,
                isPreparing ? "1  투구 설계" : "✓  투구 설계",
                15,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                new Vector2(190f, 28f),
                new Vector2(-245f, 255f),
                isPreparing ? GoldColor : RoleColor);
            CreateText("PitchStepArrow1", panel, "→", 18, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(38f, 28f), new Vector2(-108f, 255f), MutedTextColor);
            CreateText(
                "PitchFlightStep",
                panel,
                isResult ? "✓  실제 궤적" : "2  실제 궤적",
                15,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                new Vector2(190f, 28f),
                new Vector2(0f, 255f),
                isFlight ? AccentColor : isResult ? RoleColor : MutedTextColor);
            CreateText("PitchStepArrow2", panel, "→", 18, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(38f, 28f), new Vector2(108f, 255f), MutedTextColor);
            CreateText(
                "PitchResultStep",
                panel,
                "3  제구 결과",
                15,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                new Vector2(190f, 28f),
                new Vector2(245f, 255f),
                isResult ? RoleColor : MutedTextColor);
        }

        private void RenderPitchCards(RectTransform panel, in PitchSelectionRequest request)
        {
            CreateText(
                "PitchCardTitle",
                panel,
                "구종",
                14,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                new Vector2(210f, 24f),
                new Vector2(-365f, 186f),
                SecondaryTextColor);
            int count = Mathf.Min(5, request.AvailablePitches.Count);
            for (int index = 0; index < count; index++)
            {
                PitchOption option = request.AvailablePitches[index];
                bool selected = option.PitchType == _pitchMiniGame.SelectedPitch;
                Button button = CreateButton(
                    "PitchType_" + option.PitchType,
                    panel,
                    $"{index + 1}  {GetPitchTypeLabel(option.PitchType)}  " +
                    $"{option.MinimumVelocityMph:0}-{option.MaximumVelocityMph:0} mph\n" +
                    $"{GetPitchMovementIcon(option)} · 난도 {GetPitchCommandDifficultyLabel(option.PitchType)} · " +
                    $"숙련 {option.Proficiency}",
                    new Vector2(245f, 55f),
                    new Vector2(-358f, 146f - index * 61f),
                    selected ? new Color(0.025f, 0.32f, 0.52f, 1f) : PanelDarkColor,
                    selected ? PrimaryTextColor : SecondaryTextColor);
                button.interactable = _pitchMiniGame.IsInputUnlocked;
                PitchType pitchType = option.PitchType;
                button.onClick.AddListener(() => SelectPitchMiniGamePitch(pitchType));
                if (selected)
                    _pitchMiniGameSelectedPitchButton = button;
            }
        }

        private void RenderPitchMiniGameField(RectTransform field)
        {
            RectTransform strikeZone = CreateImage(
                "PitchStrikeZone",
                field,
                new Color(0.07f, 0.34f, 0.45f, 0.10f),
                new Vector2(210f, 170f),
                new Vector2(0f, -55f));
            Image strikeImage = strikeZone.GetComponent<Image>();
            strikeImage.raycastTarget = false;
            Outline outline = strikeZone.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.26f, 0.72f, 0.82f, 0.52f);
            outline.effectDistance = new Vector2(1f, -1f);
            CreateZoneGrid(strikeZone);
            CreateImage("PitchHomePlate", field, new Color(0.90f, 0.90f, 0.82f, 0.9f),
                new Vector2(58f, 10f), new Vector2(0f, -184f));
        }

        private void RenderPitchMiniGameControlPanel(RectTransform panel)
        {
            PitchSelectionRequest request = _pitchMiniGame.Request;
            PitchMiniGamePresentationState state = _pitchMiniGame.State;
            CreateStatusPill(
                panel,
                GetPitchMiniGameStateLabel(state),
                new Vector2(450f, 50f),
                new Vector2(0f, 396f));
            CreateText(
                "PitchControlTitle",
                panel,
                state == PitchMiniGamePresentationState.PrePitchReady ? "투구 준비" : "승부 방침",
                24,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                new Vector2(420f, 36f),
                new Vector2(0f, 340f),
                PrimaryTextColor);

            if (state == PitchMiniGamePresentationState.PrePitchReady)
            {
                RenderPitchReadyPanel(panel, request);
                return;
            }

            if (!_pitchMiniGame.IsInputUnlocked)
            {
                RenderPitchLockedPanel(panel);
                return;
            }

            PitchingApproach[] approaches =
            {
                PitchingApproach.Balanced,
                PitchingApproach.FullPower,
                PitchingApproach.ControlFirst,
                PitchingApproach.InduceChase,
                PitchingApproach.QuickAttack
            };
            for (int index = 0; index < approaches.Length; index++)
            {
                PitchingApproach approach = approaches[index];
                bool selected = approach == _pitchMiniGame.Approach;
                Button button = CreateButton(
                    "PitchApproach_" + approach,
                    panel,
                    GetPitchingApproachLabel(approach),
                    new Vector2(410f, 50f),
                    new Vector2(0f, 272f - index * 56f),
                    selected ? new Color(0.025f, 0.32f, 0.52f, 1f) : PanelDarkColor,
                    selected ? PrimaryTextColor : SecondaryTextColor);
                button.onClick.AddListener(() => SelectPitchMiniGameApproach(approach));
            }

            CreateText(
                "PitchApproachGuide",
                panel,
                "방침은 이번 공의 의도를 정합니다. 제구 범위는 기존 판정 수치를 그대로 표시합니다.",
                12,
                FontStyle.Normal,
                TextAnchor.MiddleCenter,
                new Vector2(430f, 42f),
                new Vector2(0f, -35f),
                MutedTextColor);
            _pitchMiniGamePrimaryButton = CreateButton(
                "ConfirmPitch",
                panel,
                "투구 확정   SPACE / GAMEPAD A",
                new Vector2(430f, 64f),
                new Vector2(0f, -100f),
                new Color(0.02f, 0.38f, 0.7f, 1f),
                PrimaryTextColor);
            _pitchMiniGamePrimaryButton.onClick.AddListener(SubmitPitchMiniGameCommand);
            Button back = CreateButton(
                "PitchBack",
                panel,
                "뒤로   ESC / GAMEPAD B",
                new Vector2(205f, 48f),
                new Vector2(-110f, -174f),
                PanelDarkColor,
                SecondaryTextColor);
            back.onClick.AddListener(ReturnPitchMiniGameToReady);
            Button auto = CreateButton(
                "PitchAutoBatter",
                panel,
                "이번 타자 자동   A",
                new Vector2(205f, 48f),
                new Vector2(110f, -174f),
                PanelDarkColor,
                SecondaryTextColor);
            auto.onClick.AddListener(AutoCompletePitchMiniGamePlateAppearance);
            CreateText(
                "PitchPattern",
                panel,
                BuildPitchPatternGuide(request),
                14,
                FontStyle.Normal,
                TextAnchor.MiddleCenter,
                new Vector2(430f, 72f),
                new Vector2(0f, -260f),
                SecondaryTextColor);
            RenderLatestMiniGameFeedback(panel, new Vector2(0f, -350f));
        }

        private void RenderPitchReadyPanel(RectTransform panel, in PitchSelectionRequest request)
        {
            PitchOption option = FindPitchMiniGameOption(request, _pitchMiniGame.SelectedPitch);
            CreateText(
                "PitchReadySummary",
                panel,
                $"추천 · {GetPitchTypeLabel(option.PitchType)}\n" +
                $"{option.MinimumVelocityMph:0}-{option.MaximumVelocityMph:0} mph · 숙련 {option.Proficiency}",
                20,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                new Vector2(420f, 82f),
                new Vector2(0f, 240f),
                PrimaryTextColor);
            CreateText(
                "PitchReadyGuide",
                panel,
                "타자와 Count를 확인한 뒤 투구 설계를 시작하세요.\n준비 전에는 투구가 시작되지 않습니다.",
                14,
                FontStyle.Normal,
                TextAnchor.MiddleCenter,
                new Vector2(420f, 80f),
                new Vector2(0f, 118f),
                SecondaryTextColor);
            _pitchMiniGamePrimaryButton = CreateButton(
                "BeginPitchSelection",
                panel,
                "투구 준비   SPACE / GAMEPAD A",
                new Vector2(430f, 66f),
                new Vector2(0f, 18f),
                new Color(0.02f, 0.38f, 0.7f, 1f),
                PrimaryTextColor);
            _pitchMiniGamePrimaryButton.onClick.AddListener(BeginPitchMiniGameSelection);
            Button auto = CreateButton(
                "PitchReadyAuto",
                panel,
                "이번 타자 자동   A",
                new Vector2(300f, 48f),
                new Vector2(0f, -66f),
                PanelDarkColor,
                SecondaryTextColor);
            auto.onClick.AddListener(AutoCompletePitchMiniGamePlateAppearance);
            CreateText(
                "PitchReadyPattern",
                panel,
                BuildPitchPatternGuide(request),
                14,
                FontStyle.Normal,
                TextAnchor.MiddleCenter,
                new Vector2(430f, 72f),
                new Vector2(0f, -170f),
                SecondaryTextColor);
            RenderLatestMiniGameFeedback(panel, new Vector2(0f, -310f));
        }

        private void RenderPitchLockedPanel(RectTransform panel)
        {
            PitchMiniGamePresentationState state = _pitchMiniGame.State;
            string title = state switch
            {
                PitchMiniGamePresentationState.PitchConfirmed => "입력 확정",
                PitchMiniGamePresentationState.Windup => "와인드업",
                PitchMiniGamePresentationState.BallInFlight => "공이 날아가는 중",
                PitchMiniGamePresentationState.PlateArrival => "포수 미트 도착",
                PitchMiniGamePresentationState.BatterReaction => "타자 반응",
                _ => "투구 결과"
            };
            CreateText(
                "PitchLockedStatus",
                panel,
                title,
                28,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                new Vector2(420f, 52f),
                new Vector2(0f, 242f),
                IsPitchMiniGameResultState(state) ? RoleColor : AccentColor);
            CreateText(
                "PitchLockedSelection",
                panel,
                $"{GetPitchTypeLabel(_pitchMiniGame.SelectedPitch)} · " +
                $"{GetPitchingApproachLabel(_pitchMiniGame.Approach)}\n" +
                BuildPitchTargetLabel(_pitchMiniGame.TargetPoint),
                17,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                new Vector2(420f, 72f),
                new Vector2(0f, 166f),
                PrimaryTextColor);
            Button locked = CreateButton(
                "PitchLockedAction",
                panel,
                IsPitchMiniGameResultState(state) ? "결과 확인 중" : "투구 진행 중",
                new Vector2(430f, 64f),
                new Vector2(0f, 46f),
                new Color(0.08f, 0.16f, 0.20f, 1f),
                MutedTextColor);
            locked.interactable = false;
            if (_pitchMiniGame.HasResolvedPlay && IsPitchMiniGameResultState(state))
            {
                CreateText(
                    "PitchLockedResult",
                    panel,
                    BuildPitchCommandFeedback(_pitchMiniGame.ResolvedPlay.Pitch) + "\n" +
                    GetContactFeedback(_pitchMiniGame.ResolvedPlay.Contact),
                    17,
                    FontStyle.Bold,
                    TextAnchor.MiddleCenter,
                    new Vector2(430f, 90f),
                    new Vector2(0f, -66f),
                    GoldColor);
            }
        }

        private bool UpdatePitchMiniGameInput(CareerMatchSession session, Keyboard keyboard)
        {
            if (!session.PendingPitchSelection.HasValue || _pitchMiniGame.IsPresentationActive)
                return _pitchMiniGame.IsPresentationActive;

            EnsurePitchMiniGameRequest(session.PendingPitchSelection.Value);
            Gamepad gamepad = Gamepad.current;
            if (_pitchMiniGame.State == PitchMiniGamePresentationState.PrePitchReady)
            {
                if (IsPitchSubmitPressed(keyboard, gamepad))
                    BeginPitchMiniGameSelection();
                else if (keyboard != null && keyboard.aKey.wasPressedThisFrame)
                    AutoCompletePitchMiniGamePlateAppearance();
                return true;
            }

            if (!_pitchMiniGame.IsInputUnlocked)
                return true;

            UpdatePitchMiniGameAimInput(keyboard, gamepad);
            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) SelectPitchMiniGamePitchByIndex(0);
                else if (keyboard.digit2Key.wasPressedThisFrame) SelectPitchMiniGamePitchByIndex(1);
                else if (keyboard.digit3Key.wasPressedThisFrame) SelectPitchMiniGamePitchByIndex(2);
                else if (keyboard.digit4Key.wasPressedThisFrame) SelectPitchMiniGamePitchByIndex(3);
                else if (keyboard.digit5Key.wasPressedThisFrame) SelectPitchMiniGamePitchByIndex(4);
                else if (keyboard.qKey.wasPressedThisFrame) CyclePitchMiniGameApproach(-1);
                else if (keyboard.eKey.wasPressedThisFrame) CyclePitchMiniGameApproach(1);
                else if (keyboard.escapeKey.wasPressedThisFrame) ReturnPitchMiniGameToReady();
                else if (keyboard.aKey.wasPressedThisFrame) AutoCompletePitchMiniGamePlateAppearance();
                else if (IsPitchSubmitPressed(keyboard, gamepad)) SubmitPitchMiniGameCommand();
            }
            else if (IsPitchSubmitPressed(null, gamepad))
            {
                SubmitPitchMiniGameCommand();
            }

            if (gamepad != null)
            {
                if (gamepad.leftShoulder.wasPressedThisFrame)
                    CyclePitchMiniGamePitch(-1);
                else if (gamepad.rightShoulder.wasPressedThisFrame)
                    CyclePitchMiniGameApproach(1);
                else if (gamepad.buttonEast.wasPressedThisFrame)
                    ReturnPitchMiniGameToReady();
            }
            return true;
        }

        /// <summary>자동 중계보다 먼저 실제 투구 연출을 진행하고 완료된 이벤트만 공개한다.</summary>
        private bool UpdatePitchMiniGamePresentation(CareerMatchSession session)
        {
            if (!_pitchMiniGame.IsPresentationActive)
                return false;

            if (_pitchMiniGame.State == PitchMiniGamePresentationState.PitchConfirmed)
                return true;

            PitchMiniGamePresentationState previous = _pitchMiniGame.State;
            bool didChangeState = _pitchMiniGame.Tick(Time.unscaledDeltaTime, pitchTrajectoryPresentation);
            PitchMiniGamePresentationState current = _pitchMiniGame.State;

            if (current == PitchMiniGamePresentationState.BallInFlight)
            {
                _pitchTrajectoryPresenter.SetActualProgress(_pitchMiniGame.FlightProgress01);
            }
            else if (current is PitchMiniGamePresentationState.PlateArrival or
                     PitchMiniGamePresentationState.BatterReaction or
                     PitchMiniGamePresentationState.PitchResult)
            {
                _pitchTrajectoryPresenter.HoldAtPlate();
                _pitchAimOverlay.ShowResult(
                    _pitchMiniGame.ResolvedPlay.Pitch.TargetPoint,
                    _pitchMiniGame.ResolvedPlay.Pitch.PlatePoint);
                if (!IsPitchMiniGameResultState(previous))
                {
                    _pitchResultFeedbackPresenter.Show(
                        BuildPitchCommandFeedback(_pitchMiniGame.ResolvedPlay.Pitch),
                        GetContactFeedback(_pitchMiniGame.ResolvedPlay.Contact));
                }
            }

            if (current == PitchMiniGamePresentationState.NextPitchReady)
            {
                _playback.RevealThroughEvent(session.Events, _pitchMiniGame.ResolvedEventIndex);
                _pitchMiniGame.Complete();
                HidePitchMiniGamePresentation();
                _nextAutomaticPlayAt = Time.unscaledTime + GetAutomaticPlayIntervalSeconds();
                Render();
                return true;
            }

            if (didChangeState && previous != current)
                Render();
            return true;
        }

        private void SubmitPitchMiniGameCommand()
        {
            CareerMatchSession session = _manager?.ActiveMatch;
            if (session == null || !session.PendingPitchSelection.HasValue || !_pitchMiniGame.IsInputUnlocked)
                return;

            int firstNewEventIndex = session.Events.Count;
            PitchSelectionCommand command = _pitchMiniGame.Confirm();
            _selectedPitchingApproach = command.Approach;
            HidePitchMiniGamePresentation();
            Render();

            if (!_manager.SubmitPitchSelection(command))
            {
                _pitchMiniGame.CancelConfirmedPitch();
                Render();
                return;
            }

            CareerMatchSession updatedSession = _manager.ActiveMatch;
            if (!TryFindResolvedPitch(
                    updatedSession,
                    firstNewEventIndex,
                    command.RequestId,
                    out int eventIndex,
                    out PitchPlayData play))
            {
                _pitchMiniGame.CancelConfirmedPitch();
                Render();
                return;
            }

            _pitchMiniGame.BeginResolvedPitch(eventIndex, play, pitchTrajectoryPresentation);
            _pitchTrajectoryPresenter.BeginActual(play.Pitch);
            _pitchAimOverlay.ShowAim(
                command.TargetPoint,
                FindPitchMiniGameOption(_pitchMiniGame.Request, command.PitchType).CommandEllipse,
                0f);
            _pitchResultFeedbackPresenter.Hide();
            Render();
        }

        private void BeginPitchMiniGameSelection()
        {
            if (!_pitchMiniGame.HasRequest ||
                _pitchMiniGame.State != PitchMiniGamePresentationState.PrePitchReady)
                return;
            _pitchMiniGame.BeginSelection();
            Render();
        }

        private void ReturnPitchMiniGameToReady()
        {
            if (!_pitchMiniGame.IsInputUnlocked)
                return;
            _pitchMiniGame.ReturnToReady();
            Render();
        }

        private void SelectPitchMiniGamePitch(PitchType pitchType)
        {
            if (!_pitchMiniGame.IsInputUnlocked)
                return;
            _pitchMiniGame.SelectPitch(pitchType);
            Render();
        }

        private void SelectPitchMiniGamePitchByIndex(int index)
        {
            if (!_pitchMiniGame.HasRequest || index < 0 || index >= _pitchMiniGame.Request.AvailablePitches.Count)
                return;
            SelectPitchMiniGamePitch(_pitchMiniGame.Request.AvailablePitches[index].PitchType);
        }

        private void SelectPitchMiniGameApproach(PitchingApproach approach)
        {
            if (!_pitchMiniGame.IsInputUnlocked)
                return;
            _pitchMiniGame.SelectApproach(approach);
            _selectedPitchingApproach = approach;
            Render();
        }

        private void CyclePitchMiniGamePitch(int direction)
        {
            PitchSelectionRequest request = _pitchMiniGame.Request;
            int currentIndex = 0;
            for (int index = 0; index < request.AvailablePitches.Count; index++)
            {
                if (request.AvailablePitches[index].PitchType == _pitchMiniGame.SelectedPitch)
                {
                    currentIndex = index;
                    break;
                }
            }
            int next = (currentIndex + direction + request.AvailablePitches.Count) %
                       request.AvailablePitches.Count;
            SelectPitchMiniGamePitchByIndex(next);
        }

        private void CyclePitchMiniGameApproach(int direction)
        {
            PitchingApproach[] approaches =
            {
                PitchingApproach.Balanced,
                PitchingApproach.FullPower,
                PitchingApproach.ControlFirst,
                PitchingApproach.InduceChase,
                PitchingApproach.QuickAttack
            };
            int current = Array.IndexOf(approaches, _pitchMiniGame.Approach);
            int next = (current + direction + approaches.Length) % approaches.Length;
            SelectPitchMiniGameApproach(approaches[next]);
        }

        private void UpdatePitchMiniGameAimInput(Keyboard keyboard, Gamepad gamepad)
        {
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed && _miniGamePlateRect != null &&
                RectTransformUtility.RectangleContainsScreenPoint(_miniGamePlateRect, mouse.position.ReadValue()))
            {
                UpdatePitchMiniGameTargetFromPointer();
                return;
            }

            Vector2 direction = Vector2.zero;
            if (keyboard != null)
            {
                direction.x = (keyboard.rightArrowKey.isPressed ? 1f : 0f) -
                              (keyboard.leftArrowKey.isPressed ? 1f : 0f);
                direction.y = (keyboard.upArrowKey.isPressed ? 1f : 0f) -
                              (keyboard.downArrowKey.isPressed ? 1f : 0f);
            }
            if (gamepad != null && direction.sqrMagnitude <= 0f)
                direction = gamepad.leftStick.ReadValue() + gamepad.dpad.ReadValue();
            if (direction.sqrMagnitude <= 0.001f)
            {
                RefreshPitchMiniGameOverlay();
                return;
            }

            double step = PitchTargetAimSpeed * Time.unscaledDeltaTime;
            if (keyboard != null &&
                (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed))
                step *= 0.25d;
            PlatePoint target = ClampPlatePoint(new PlatePoint(
                _pitchMiniGame.TargetPoint.X + direction.x * step,
                _pitchMiniGame.TargetPoint.Y + direction.y * step));
            _pitchMiniGame.SetTarget(target);
            RefreshPitchMiniGameOverlay();
        }

        private void UpdatePitchMiniGameTargetFromPointer()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || _miniGamePlateRect == null || !_pitchMiniGame.IsInputUnlocked)
                return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _miniGamePlateRect,
                    mouse.position.ReadValue(),
                    null,
                    out Vector2 local))
                return;

            PlatePoint target = ClampPlatePoint(PitchTrajectoryPresenter.FromPlateScreenPosition(local));
            _pitchMiniGame.SetTarget(target);
            RefreshPitchMiniGameOverlay();
        }

        private void RefreshPitchMiniGameOverlay()
        {
            if (_pitchTrajectoryPresenter == null || !_pitchMiniGame.HasRequest)
                return;

            _pitchTrajectoryPresenter.SetRootVisible(true);
            PitchMiniGamePresentationState state = _pitchMiniGame.State;
            if (state == PitchMiniGamePresentationState.PrePitchReady)
            {
                _pitchTrajectoryPresenter.Hide();
                _pitchAimOverlay.Hide();
                _pitchResultFeedbackPresenter.Hide();
                return;
            }

            if (_pitchMiniGame.IsInputUnlocked)
            {
                PitchOption option = FindPitchMiniGameOption(
                    _pitchMiniGame.Request,
                    _pitchMiniGame.SelectedPitch);
                _pitchTrajectoryPresenter.ShowPreview(option, _pitchMiniGame.TargetPoint);
                float pulse = (Mathf.Sin(Time.unscaledTime * 4f) + 1f) * 0.5f;
                _pitchAimOverlay.ShowAim(_pitchMiniGame.TargetPoint, option.CommandEllipse, pulse);
                _pitchResultFeedbackPresenter.Hide();
                return;
            }

            if (_pitchMiniGame.HasResolvedPlay &&
                IsPitchMiniGameResultState(state))
            {
                _pitchAimOverlay.ShowResult(
                    _pitchMiniGame.ResolvedPlay.Pitch.TargetPoint,
                    _pitchMiniGame.ResolvedPlay.Pitch.PlatePoint);
                _pitchResultFeedbackPresenter.Show(
                    BuildPitchCommandFeedback(_pitchMiniGame.ResolvedPlay.Pitch),
                    GetContactFeedback(_pitchMiniGame.ResolvedPlay.Contact));
            }
        }

        private void AutoCompletePitchMiniGamePlateAppearance()
        {
            _pitchMiniGame.Complete();
            HidePitchMiniGamePresentation();
            _manager.AutoCompleteCurrentPlateAppearance();
        }

        private void HidePitchMiniGamePresentation()
        {
            _pitchTrajectoryPresenter?.Hide();
            _pitchAimOverlay?.Hide();
            _pitchResultFeedbackPresenter?.Hide();
        }

        private void RestorePitchMiniGameFocus()
        {
            Button focus = _pitchMiniGamePrimaryButton != null
                ? _pitchMiniGamePrimaryButton
                : _pitchMiniGameSelectedPitchButton;
            if (focus != null && focus.interactable && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(focus.gameObject);
        }

        private static bool TryFindResolvedPitch(
            CareerMatchSession session,
            int firstEventIndex,
            int requestId,
            out int eventIndex,
            out PitchPlayData play)
        {
            int start = Mathf.Clamp(firstEventIndex, 0, session.Events.Count);
            for (int index = start; index < session.Events.Count; index++)
            {
                MatchEvent matchEvent = session.Events[index];
                PitchPlayData candidate = matchEvent.PitchPlayData;
                if (matchEvent.EventType != MatchEventType.Pitch ||
                    !candidate.HasValue ||
                    candidate.PitchSelection.RequestId != requestId)
                    continue;

                eventIndex = index;
                play = candidate;
                return true;
            }

            eventIndex = -1;
            play = default;
            return false;
        }

        private static PitchOption FindPitchMiniGameOption(
            in PitchSelectionRequest request,
            PitchType pitchType)
        {
            for (int index = 0; index < request.AvailablePitches.Count; index++)
            {
                if (request.AvailablePitches[index].PitchType == pitchType)
                    return request.AvailablePitches[index];
            }
            return request.AvailablePitches[0];
        }

        private static string GetPitchMovementIcon(in PitchOption option)
        {
            double horizontal = option.HorizontalBreak;
            double vertical = option.VerticalBreak;
            if (Math.Abs(vertical) > Math.Abs(horizontal) * 1.35d)
                return vertical < 0d ? "↓" : "↑";
            if (horizontal < -0.04d)
                return vertical < -0.05d ? "↙" : "←";
            if (horizontal > 0.04d)
                return vertical < -0.05d ? "↘" : "→";
            return vertical < -0.05d ? "↓" : "직선";
        }

        private static string GetPitchCommandDifficultyLabel(PitchType pitchType)
        {
            double difficulty = PitchTypeProfileCatalog.Get(pitchType).CommandDifficulty;
            if (difficulty <= 0d) return "낮음";
            if (difficulty <= 0.02d) return "보통";
            return "높음";
        }

        private static string GetPitchMiniGameStateLabel(PitchMiniGamePresentationState state)
        {
            return state switch
            {
                PitchMiniGamePresentationState.PrePitchReady => "투구 전 준비",
                PitchMiniGamePresentationState.PitchSelection => "구종 선택",
                PitchMiniGamePresentationState.TargetAiming => "목표 위치",
                PitchMiniGamePresentationState.StrategySelection => "승부 방침",
                PitchMiniGamePresentationState.PitchConfirmed => "입력 잠금",
                PitchMiniGamePresentationState.Windup => "와인드업",
                PitchMiniGamePresentationState.BallInFlight => "실제 궤적",
                PitchMiniGamePresentationState.PlateArrival => "Plate 도착",
                PitchMiniGamePresentationState.BatterReaction => "타자 반응",
                _ => "제구 피드백"
            };
        }

        private static bool IsPitchMiniGameResultState(PitchMiniGamePresentationState state)
        {
            return state is PitchMiniGamePresentationState.PlateArrival or
                PitchMiniGamePresentationState.BatterReaction or
                PitchMiniGamePresentationState.PitchResult;
        }

        private static string BuildPitchTargetLabel(in PlatePoint point)
        {
            return $"목표 X {point.X:+0.00;-0.00;0.00} · Y {point.Y:+0.00;-0.00;0.00}";
        }

        private static string BuildPitchCommandFeedback(in PitchFlightDescriptor pitch)
        {
            double deltaX = pitch.PlatePoint.X - pitch.TargetPoint.X;
            double deltaY = pitch.PlatePoint.Y - pitch.TargetPoint.Y;
            double distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            bool targetWasAwayFromCenter = Math.Abs(pitch.TargetPoint.X) > 0.55d ||
                                           Math.Abs(pitch.TargetPoint.Y) > 0.55d;
            bool actualIsCenter = Math.Abs(pitch.PlatePoint.X) < 0.30d &&
                                  Math.Abs(pitch.PlatePoint.Y) < 0.30d;
            if (distance <= 0.12d) return "목표에 정확히 제구";
            if (targetWasAwayFromCenter && actualIsCenter) return "가운데로 몰림";
            if (!pitch.IsStrike && distance >= 0.72d) return "존을 크게 벗어남";
            if (Math.Abs(deltaY) >= Math.Abs(deltaX))
                return deltaY > 0d ? "조금 높게 들어감" : "조금 낮게 들어감";
            return deltaX > 0d ? "오른쪽으로 빠짐" : "왼쪽으로 빠짐";
        }

        private static bool IsPitchSubmitPressed(Keyboard keyboard, Gamepad gamepad)
        {
            return keyboard != null &&
                   (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame) ||
                   gamepad != null && gamepad.buttonSouth.wasPressedThisFrame;
        }
    }
}
