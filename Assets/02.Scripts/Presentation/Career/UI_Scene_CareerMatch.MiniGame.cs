using System;
using Baseball.Core.Players;
using Baseball.Game.Career;
using Baseball.Presentation.UI;
using Baseball.Simulation.Match;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Baseball.Presentation.Career
{
    public sealed partial class UI_Scene_CareerMatch
    {
        private const float MiniGameAimSpeed = 1.75f;
        private const float BattingZoneScaleX = 112f;
        private const float BattingZoneScaleY = 88f;
        private const float BattingZoneCenterY = -36f;
        private const int PitchTrailCount = 5;

        [SerializeField, Min(0.2f)] private float battingPitcherWindupSeconds = 0.72f;
        [SerializeField, Min(0.4f)] private float beginnerBattingPitchFlightSeconds = 1.28f;
        [SerializeField, Min(0.4f)] private float standardBattingPitchFlightSeconds = 1.02f;
        [SerializeField, Min(0.4f)] private float professionalBattingPitchFlightSeconds = 0.84f;

        private int _miniGameStateKey = int.MinValue;
        private float _miniGamePitchElapsedSeconds;
        private BattingMiniGamePhase _battingMiniGamePhase;
        private bool _miniGameIsTakingPitch;
        private PlatePoint _miniGameBatPoint;
        private RectTransform _miniGamePlateRect;
        private RectTransform _miniGameBall;
        private RectTransform _miniGameBatCursor;
        private RectTransform _miniGameBatTimingRing;
        private RectTransform _miniGameTrajectoryTunnel;
        private RectTransform _miniGameTimingMarker;
        private readonly RectTransform[] _miniGamePitchTrail = new RectTransform[PitchTrailCount];
        private RectTransform _miniGameArrivalGuide;
        private Text _miniGameProgressText;
        private Text _miniGamePitchReadText;
        private Text _miniGameTrackingStatusText;

        private static Sprite _miniGameSolidCircleSprite;
        private static Sprite _miniGameRingSprite;

        private enum BattingMiniGamePhase
        {
            AwaitingReady,
            Windup,
            Tracking
        }

        private bool IsMiniGameInputReady(CareerMatchSession session)
        {
            return session != null &&
                   session.Mode == CareerMatchMode.MiniGame &&
                   session.Phase == CareerMatchPhase.Playing &&
                   (session.PendingPitchSelection.HasValue || session.PendingSwingExecution.HasValue) &&
                   !_hasControlledResult &&
                   !_playback.HasPendingEvents(session.Events);
        }

        private bool IsMiniGameStageVisible(CareerMatchSession session)
        {
            return IsMiniGameInputReady(session) || IsPitchMiniGameStageVisible(session);
        }

        private bool UpdateMiniGameInput(CareerMatchSession session, Keyboard keyboard)
        {
            if (!IsMiniGameInputReady(session))
                return false;

            EnsureMiniGameState(session);
            if (session.PendingPitchSelection.HasValue)
            {
                return UpdatePitchMiniGameInput(session, keyboard);
            }

            BatterMiniGameRequest request = session.PendingSwingExecution.Value;
            if (_battingMiniGamePhase == BattingMiniGamePhase.AwaitingReady)
            {
                UpdateBattingMiniGameVisuals(request, 0f, 0f);
                if (keyboard != null)
                {
                    if (keyboard.digit1Key.wasPressedThisFrame) SelectMiniGameSwingIntent(BattingApproach.Contact);
                    else if (keyboard.digit2Key.wasPressedThisFrame) SelectMiniGameSwingIntent(BattingApproach.Balanced);
                    else if (keyboard.digit3Key.wasPressedThisFrame) SelectMiniGameSwingIntent(BattingApproach.Power);
                    else if (keyboard.digit4Key.wasPressedThisFrame) SelectMiniGameSwingIntent(BattingApproach.Patient);
                    else if (keyboard.digit5Key.wasPressedThisFrame) SelectMiniGameSwingIntent(BattingApproach.Bunt);
                    else if (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame)
                        BeginMiniGamePitchTracking();
                    else if (keyboard.aKey.wasPressedThisFrame)
                        AutoCompleteMiniGamePlateAppearance();
                }
                return true;
            }

            UpdateBattingCursorInput(keyboard);
            _miniGamePitchElapsedSeconds += Time.unscaledDeltaTime;
            float elapsed = _miniGamePitchElapsedSeconds;
            float windupProgress = Mathf.Clamp01(elapsed / Mathf.Max(0.2f, battingPitcherWindupSeconds));
            float flightProgress = Mathf.Clamp01(
                (elapsed - battingPitcherWindupSeconds) / GetBattingPitchFlightSeconds());
            if (_battingMiniGamePhase == BattingMiniGamePhase.Windup && windupProgress >= 1f)
                _battingMiniGamePhase = BattingMiniGamePhase.Tracking;

            UpdateBattingMiniGameVisuals(request, flightProgress, windupProgress);
            bool canSwing = _battingMiniGamePhase == BattingMiniGamePhase.Tracking;
            bool swung = canSwing && !_miniGameIsTakingPitch &&
                         (keyboard != null && keyboard.spaceKey.wasPressedThisFrame ||
                          WasBattingPlaneClicked());
            if (swung)
                SubmitMiniGameSwing(flightProgress);
            else if (keyboard != null && keyboard.aKey.wasPressedThisFrame)
                AutoCompleteMiniGamePlateAppearance();
            else if (canSwing && flightProgress >= 1f)
                SubmitMiniGameTake();
            return true;
        }

        private void EnsureMiniGameState(CareerMatchSession session)
        {
            int key;
            if (session.PendingPitchSelection.HasValue)
                key = -1 - session.PendingPitchSelection.Value.RequestId * 2;
            else
                key = 1 + session.PendingSwingExecution.Value.RequestId * 2;
            if (_miniGameStateKey == key)
                return;

            _miniGameStateKey = key;
            if (session.PendingPitchSelection.HasValue)
            {
                PitchSelectionRequest request = session.PendingPitchSelection.Value;
                EnsurePitchMiniGameRequest(request);
            }
            else
            {
                BatterMiniGameRequest request = session.PendingSwingExecution.Value;
                _miniGameBatPoint = new PlatePoint(0d, 0d);
                _miniGamePitchElapsedSeconds = 0f;
                _battingMiniGamePhase = BattingMiniGamePhase.AwaitingReady;
                _miniGameIsTakingPitch = false;
                if (_manager.CurrentCareer.GameSettings.MiniGameDifficulty == MiniGameDifficulty.Beginner)
                {
                    _miniGameBatPoint = new PlatePoint(
                        request.Pitch.PlatePoint.X * 0.25d,
                        request.Pitch.PlatePoint.Y * 0.25d);
                }
            }
        }

        private void RenderMiniGameStage(RectTransform panel, CareerMatchSession session)
        {
            if (IsPitchMiniGameStageVisible(session))
            {
                if (session.PendingPitchSelection.HasValue)
                    EnsurePitchMiniGameRequest(session.PendingPitchSelection.Value);
                RenderPitchMiniGameStage(panel);
            }
            else
            {
                EnsureMiniGameState(session);
                RenderBattingMiniGameStage(panel, session.PendingSwingExecution.Value);
            }
        }

        private void RenderBattingMiniGameStage(
            RectTransform panel,
            BatterMiniGameRequest request)
        {
            CreateText("MiniGameTitle", panel, "직접 타격", 25, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(360f, 36f), new Vector2(0f, 275f), PrimaryTextColor);
            RenderBattingProgressSteps(panel);
            CreateText("MiniGameSituation", panel,
                $"{request.Inning}회{GetHalfLabel(request.Half)} · {request.Outs}사 · " +
                $"볼 {request.Balls} · 스트라이크 {request.Strikes}",
                15, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(820f, 26f), new Vector2(0f, 205f), SecondaryTextColor);

            _miniGamePlateRect = CreateFramedSurface(
                "BattingPlane", panel, new Color(0.008f, 0.027f, 0.045f, 1f),
                new Vector2(880f, 350f), new Vector2(0f, -4f));
            _miniGamePlateRect.gameObject.AddComponent<RectMask2D>();
            RenderBattingField(_miniGamePlateRect);

            RectTransform strikeZone = CreateImage(
                "StrikeZone", _miniGamePlateRect, new Color(0.07f, 0.34f, 0.45f, 0.10f),
                new Vector2(BattingZoneScaleX * 2f, BattingZoneScaleY * 2f),
                new Vector2(0f, BattingZoneCenterY));
            Image strikeImage = strikeZone.GetComponent<Image>();
            strikeImage.raycastTarget = false;
            Outline strikeOutline = strikeZone.gameObject.AddComponent<Outline>();
            strikeOutline.effectColor = new Color(0.26f, 0.72f, 0.82f, 0.52f);
            strikeOutline.effectDistance = new Vector2(1f, -1f);
            CreateZoneGrid(strikeZone);

            _miniGameTrajectoryTunnel = CreateMiniGameSpriteImage(
                "TrajectoryTunnel", _miniGamePlateRect, GetMiniGameSolidCircleSprite(),
                new Color(0.12f, 0.78f, 0.78f, 0.12f),
                new Vector2(128f, 58f), new Vector2(0f, 104f));
            _miniGameArrivalGuide = CreateMiniGameSpriteImage(
                "ArrivalGuide", _miniGamePlateRect, GetMiniGameRingSprite(),
                new Color(0.20f, 0.76f, 1f, 0.25f), new Vector2(42f, 42f),
                ToBattingScenePosition(request.Pitch.PlatePoint));
            _miniGameArrivalGuide.gameObject.SetActive(false);

            for (int index = PitchTrailCount - 1; index >= 0; index--)
            {
                float alpha = Mathf.Lerp(0.06f, 0.26f, 1f - index / (float)PitchTrailCount);
                _miniGamePitchTrail[index] = CreateMiniGameSpriteImage(
                    "PitchTrail_" + index, _miniGamePlateRect, GetMiniGameSolidCircleSprite(),
                    new Color(0.86f, 0.95f, 1f, alpha), new Vector2(16f, 16f),
                    new Vector2(0f, 112f));
                _miniGamePitchTrail[index].gameObject.SetActive(false);
            }

            _miniGameBall = CreateBaseballIllustration(
                "Ball", _miniGamePlateRect, new Vector2(27f, 27f), new Vector2(0f, 112f));
            RenderBattingCursor(_miniGamePlateRect, request);

            _miniGameProgressText = CreateText(
                "FlightGuide", panel, "타격 의도를 고른 뒤 준비하세요",
                14, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(760f, 24f), new Vector2(0f, -195f), SecondaryTextColor);
            _miniGamePitchReadText = CreateText(
                "PitchRead", panel, "준비 전에는 투구가 시작되지 않습니다",
                12, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(620f, 22f), new Vector2(0f, -217f), MutedTextColor);
            RenderSwingTimingGauge(panel);
            UpdateBattingMiniGameVisuals(request, 0f, 0f);
        }

        private void RenderBattingProgressSteps(RectTransform panel)
        {
            bool isReady = _battingMiniGamePhase == BattingMiniGamePhase.AwaitingReady;
            Color readyColor = isReady ? GoldColor : RoleColor;
            Color trackingColor = isReady ? MutedTextColor : RoleColor;
            string readyLabel = isReady ? "1  타격 준비" : "✓  타격 준비";
            CreateText("ReadyStep", panel, readyLabel, 15, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(190f, 28f), new Vector2(-245f, 240f), readyColor);
            CreateText("ReadyArrow", panel, "→", 18, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(38f, 28f), new Vector2(-108f, 240f), MutedTextColor);
            CreateText("TrackingStep", panel, "2  투구 추적", 15, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(190f, 28f), new Vector2(0f, 240f), trackingColor);
            CreateText("SwingArrow", panel, "→", 18, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(38f, 28f), new Vector2(108f, 240f), MutedTextColor);
            CreateText("SwingStep", panel, "3  스윙", 15, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(190f, 28f), new Vector2(245f, 240f), MutedTextColor);
        }

        private void RenderBattingField(RectTransform field)
        {
            CreatePitchFieldIllustration(field, new Vector2(844f, 475f));
        }

        private void RenderBattingCursor(RectTransform field, in BatterMiniGameRequest request)
        {
            Vector2 contactSize = GetBattingCursorSize(_selectedApproach);
            _miniGameBatCursor = CreateRect(
                "BatCursor", field, new Vector2(140f, 100f), ToBattingScenePosition(_miniGameBatPoint));
            _miniGameBatTimingRing = CreateMiniGameSpriteImage(
                "TimingRing", _miniGameBatCursor, GetMiniGameRingSprite(),
                new Color(0.96f, 0.70f, 0.22f, 0.72f), contactSize + new Vector2(36f, 38f), Vector2.zero);
            CreateMiniGameSpriteImage(
                "ContactArea", _miniGameBatCursor, GetMiniGameRingSprite(),
                new Color(0.16f, 0.88f, 0.84f, 0.86f), contactSize, Vector2.zero);
            Sprite batSprite = CareerMatchMiniGameSprites.GetBaseballBatCursorIllustration();
            if (batSprite != null)
            {
                bool isLeftHanded = ResolveBattingSide(
                    _manager?.ActiveMatch?.Input,
                    request.BatterId,
                    request.PitcherId) == Handedness.Left;
                RectTransform bat = CreateMiniGameSpriteImage(
                    "BatIllustration", _miniGameBatCursor, batSprite,
                    Color.white, new Vector2(190f, 56f), Vector2.zero);
                bat.pivot = new Vector2(0.5f, 0.5f);
                bat.localScale = new Vector3(isLeftHanded ? 1f : -1f, 1f, 1f);
                bat.localRotation = Quaternion.Euler(0f, 0f, isLeftHanded ? -14f : 14f);
                bat.GetComponent<Image>().preserveAspect = true;
            }
            CreateMiniGameSpriteImage(
                "SweetSpot", _miniGameBatCursor, GetMiniGameSolidCircleSprite(),
                new Color(1f, 0.78f, 0.24f, 1f), new Vector2(12f, 12f), Vector2.zero);
            CreateImage("CrossHorizontal", _miniGameBatCursor, new Color(0.9f, 0.98f, 1f, 0.82f),
                new Vector2(28f, 2f), Vector2.zero);
            CreateImage("CrossVertical", _miniGameBatCursor, new Color(0.9f, 0.98f, 1f, 0.82f),
                new Vector2(2f, 28f), Vector2.zero);
        }

        private void RenderSwingTimingGauge(RectTransform panel)
        {
            const float trackWidth = 700f;
            CreateText("TimingVeryEarly", panel, "너무 빠름", 12, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(130f, 20f), new Vector2(-280f, -238f), MutedTextColor);
            CreateText("TimingEarly", panel, "빠름", 12, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(120f, 20f), new Vector2(-140f, -238f), SecondaryTextColor);
            CreateText("TimingPerfect", panel, "정타", 13, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(100f, 20f), new Vector2(0f, -238f), GoldColor);
            CreateText("TimingLate", panel, "늦음", 12, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(120f, 20f), new Vector2(140f, -238f), SecondaryTextColor);
            CreateText("TimingVeryLate", panel, "너무 늦음", 12, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(130f, 20f), new Vector2(280f, -238f), MutedTextColor);

            RectTransform track = CreateImage(
                "TimingTrack", panel, new Color(0.06f, 0.18f, 0.22f, 1f),
                new Vector2(trackWidth, 14f), new Vector2(0f, -260f));
            CreateImage("TimingEarlyZone", track, new Color(0.10f, 0.48f, 0.48f, 0.65f),
                new Vector2(210f, 14f), new Vector2(-140f, 0f));
            CreateImage("TimingPerfectZone", track, GoldColor,
                new Vector2(52f, 14f), Vector2.zero);
            CreateImage("TimingLateZone", track, new Color(0.10f, 0.48f, 0.48f, 0.65f),
                new Vector2(210f, 14f), new Vector2(140f, 0f));
            _miniGameTimingMarker = CreateImage(
                "TimingMarker", track, PrimaryTextColor, new Vector2(4f, 28f), new Vector2(-350f, 0f));
            CreateText("BatPositionInput", panel, "마우스 또는 방향키 · 배트 위치", 12,
                FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(330f, 22f), new Vector2(-205f, -286f), SecondaryTextColor);
            CreateText("SwingInput", panel, "SPACE 또는 클릭 · 스윙", 12,
                FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(300f, 22f), new Vector2(220f, -286f), PrimaryTextColor);
        }

        private void RenderMiniGameControlPanel(RectTransform panel, CareerMatchSession session)
        {
            if (IsPitchMiniGameStageVisible(session))
                RenderPitchMiniGameControlPanel(panel);
            else
                RenderSwingControls(panel, session.PendingSwingExecution.Value);
        }

        private void RenderSwingControls(RectTransform panel, BatterMiniGameRequest request)
        {
            bool isAwaitingReady = _battingMiniGamePhase == BattingMiniGamePhase.AwaitingReady;
            CreateStatusPill(panel, "위치 + 타이밍", ControlStatusPillSize, ControlStatusPillPosition);
            CreateText("SwingTitle", panel, "타격 조작", 24, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(420f, 36f), new Vector2(0f, 340f), PrimaryTextColor);
            CreateText("SwingIntentLabel", panel, "타격 의도", 13, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(410f, 24f), new Vector2(-145f, 306f), MutedTextColor);
            BattingApproach[] approaches =
            {
                BattingApproach.Contact,
                BattingApproach.Balanced,
                BattingApproach.Power,
                BattingApproach.Patient,
                BattingApproach.Bunt
            };
            for (int index = 0; index < approaches.Length; index++)
            {
                BattingApproach approach = approaches[index];
                bool selected = _selectedApproach == approach ||
                                index == 1 && _selectedApproach is not (BattingApproach.Contact or BattingApproach.Power or BattingApproach.Patient or BattingApproach.Bunt);
                Button button = CreateButton(
                    "SwingIntent_" + approach,
                    panel,
                    $"{index + 1}  {GetMiniGameSwingIntentLabel(approach)}",
                    new Vector2(410f, 52f),
                    new Vector2(0f, 260f - index * 57f),
                    selected ? new Color(0.025f, 0.32f, 0.52f, 1f) : PanelDarkColor,
                    selected ? PrimaryTextColor : SecondaryTextColor);
                button.interactable = isAwaitingReady;
                button.onClick.AddListener(() => SelectMiniGameSwingIntent(approach));
            }

            CreateText("SwingGuide", panel,
                isAwaitingReady
                    ? "의도를 선택한 뒤 준비하면 투수가 와인드업을 시작합니다."
                    : "공의 움직임을 읽고 배트 위치와 스윙 시점을 맞추세요.",
                13, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(430f, 44f), new Vector2(0f, -48f), SecondaryTextColor);

            Button primaryAction = CreateButton(
                isAwaitingReady ? "BattingReady" : "PitchTrackingState",
                panel,
                isAwaitingReady ? "타격 준비   SPACE / ENTER" : "투구 추적 중",
                new Vector2(430f, 64f), new Vector2(0f, -104f),
                isAwaitingReady
                    ? new Color(0.02f, 0.38f, 0.7f, 1f)
                    : new Color(0.08f, 0.16f, 0.20f, 1f),
                isAwaitingReady ? PrimaryTextColor : MutedTextColor);
            if (isAwaitingReady)
                primaryAction.onClick.AddListener(BeginMiniGamePitchTracking);
            else
                primaryAction.interactable = false;
            _miniGameTrackingStatusText = primaryAction.transform.Find("Label")?.GetComponent<Text>();

            Button take = CreateButton(
                "TakePitch", panel, _miniGameIsTakingPitch ? "지켜보는 중" : "이번 공 지켜보기",
                new Vector2(205f, 48f), new Vector2(-110f, -178f),
                PanelDarkColor, SecondaryTextColor);
            take.interactable = !_miniGameIsTakingPitch;
            take.onClick.AddListener(WatchMiniGamePitch);
            Button auto = CreateButton(
                "AutoPlateAppearance", panel, "이번 타석 자동   A",
                new Vector2(205f, 48f), new Vector2(110f, -178f),
                PanelDarkColor, SecondaryTextColor);
            auto.onClick.AddListener(AutoCompleteMiniGamePlateAppearance);
            CreateText("TakeGuide", panel,
                "스윙하지 않으면 실제 공 위치로 볼·스트라이크가 판정됩니다.",
                12, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(430f, 30f), new Vector2(0f, -224f), MutedTextColor);
            RenderLatestMiniGameFeedback(panel, new Vector2(0f, -310f));
        }

        private void UpdateBattingCursorInput(Keyboard keyboard)
        {
            Mouse mouse = Mouse.current;
            if (mouse != null && _miniGamePlateRect != null &&
                RectTransformUtility.RectangleContainsScreenPoint(
                    _miniGamePlateRect,
                    mouse.position.ReadValue()) &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _miniGamePlateRect,
                    mouse.position.ReadValue(),
                    null,
                    out Vector2 local))
            {
                _miniGameBatPoint = ClampPlatePoint(new PlatePoint(
                    local.x / BattingZoneScaleX,
                    (local.y - BattingZoneCenterY) / BattingZoneScaleY));
            }
            else if (keyboard != null)
            {
                double horizontal = (keyboard.rightArrowKey.isPressed ? 1d : 0d) -
                                    (keyboard.leftArrowKey.isPressed ? 1d : 0d);
                double vertical = (keyboard.upArrowKey.isPressed ? 1d : 0d) -
                                  (keyboard.downArrowKey.isPressed ? 1d : 0d);
                if (horizontal != 0d || vertical != 0d)
                {
                    double delta = MiniGameAimSpeed * Time.unscaledDeltaTime;
                    _miniGameBatPoint = ClampPlatePoint(new PlatePoint(
                        _miniGameBatPoint.X + horizontal * delta,
                        _miniGameBatPoint.Y + vertical * delta));
                }
            }

            if (_miniGameBatCursor != null)
                _miniGameBatCursor.anchoredPosition = ToBattingScenePosition(_miniGameBatPoint);
        }

        private void UpdateBattingMiniGameVisuals(
            BatterMiniGameRequest request,
            float flightProgress,
            float windupProgress)
        {
            bool isPitchInFlight = _battingMiniGamePhase == BattingMiniGamePhase.Tracking;
            Vector2 ballPosition = CalculateBattingPitchPosition(request, flightProgress);
            if (_miniGameBall != null)
            {
                _miniGameBall.gameObject.SetActive(isPitchInFlight);
                _miniGameBall.anchoredPosition = ballPosition;
                float ballScale = Mathf.Lerp(0.48f, 1.28f, flightProgress);
                _miniGameBall.localScale = new Vector3(ballScale, ballScale, 1f);
            }

            if (_miniGameTrajectoryTunnel != null)
            {
                _miniGameTrajectoryTunnel.gameObject.SetActive(isPitchInFlight);
                _miniGameTrajectoryTunnel.anchoredPosition = ballPosition;
                _miniGameTrajectoryTunnel.sizeDelta = new Vector2(
                    Mathf.Lerp(138f, 48f, flightProgress),
                    Mathf.Lerp(62f, 28f, flightProgress));
                _miniGameTrajectoryTunnel.localRotation = Quaternion.Euler(
                    0f,
                    0f,
                    Mathf.Lerp(-10f, 8f, (float)request.Pitch.HorizontalBreak * 0.5f + 0.5f));
            }

            UpdatePitchTrail(request, flightProgress, isPitchInFlight);
            UpdateSwingTimingMarker(request, flightProgress);

            if (_miniGameBatTimingRing != null)
            {
                float pressureScale = isPitchInFlight
                    ? Mathf.Lerp(1.16f, 0.82f, flightProgress)
                    : 1.16f;
                _miniGameBatTimingRing.localScale = new Vector3(pressureScale, pressureScale, 1f);
            }

            MiniGameDifficulty difficulty = _manager.CurrentCareer.GameSettings.MiniGameDifficulty;
            if (_miniGameArrivalGuide != null)
            {
                bool showArrivalAssist = difficulty == MiniGameDifficulty.Beginner &&
                                         isPitchInFlight &&
                                         flightProgress >= 0.72f;
                _miniGameArrivalGuide.gameObject.SetActive(showArrivalAssist);
            }

            UpdateBattingInstructionText(request, flightProgress, windupProgress);
        }

        private void BeginMiniGamePitchTracking()
        {
            CareerMatchSession session = _manager?.ActiveMatch;
            if (!IsMiniGameInputReady(session) ||
                !session.PendingSwingExecution.HasValue ||
                _battingMiniGamePhase != BattingMiniGamePhase.AwaitingReady)
                return;

            _battingMiniGamePhase = BattingMiniGamePhase.Windup;
            _miniGamePitchElapsedSeconds = 0f;
            Render();
        }

        private void WatchMiniGamePitch()
        {
            CareerMatchSession session = _manager?.ActiveMatch;
            if (!IsMiniGameInputReady(session) || !session.PendingSwingExecution.HasValue)
                return;

            _miniGameIsTakingPitch = true;
            if (_battingMiniGamePhase == BattingMiniGamePhase.AwaitingReady)
            {
                BeginMiniGamePitchTracking();
                return;
            }
            Render();
        }

        private float GetBattingPitchFlightSeconds()
        {
            return _manager.CurrentCareer.GameSettings.MiniGameDifficulty switch
            {
                MiniGameDifficulty.Beginner => beginnerBattingPitchFlightSeconds,
                MiniGameDifficulty.Professional => professionalBattingPitchFlightSeconds,
                _ => standardBattingPitchFlightSeconds
            };
        }

        private bool WasBattingPlaneClicked()
        {
            Mouse mouse = Mouse.current;
            return mouse != null &&
                   mouse.leftButton.wasPressedThisFrame &&
                   _miniGamePlateRect != null &&
                   RectTransformUtility.RectangleContainsScreenPoint(
                       _miniGamePlateRect,
                       mouse.position.ReadValue());
        }

        private Vector2 CalculateBattingPitchPosition(
            BatterMiniGameRequest request,
            float progress)
        {
            float time = Mathf.Clamp01(progress);
            PitchTrajectoryPoint point = request.Pitch.Evaluate(time);
            double linearX = request.Pitch.ReleasePoint.X +
                             (request.Pitch.PlatePoint.X - request.Pitch.ReleasePoint.X) * time;
            double linearY = request.Pitch.ReleasePoint.Y +
                             (request.Pitch.PlatePoint.Y - request.Pitch.ReleasePoint.Y) * time;
            double breakEmphasis = pitchTrajectoryPresentation.BreakEmphasis;
            double emphasizedX = linearX + (point.X - linearX) * breakEmphasis;
            double emphasizedY = linearY + (point.Y - linearY) * breakEmphasis;
            return new Vector2(
                (float)emphasizedX * BattingZoneScaleX,
                BattingZoneCenterY + (float)emphasizedY * BattingZoneScaleY +
                Mathf.Lerp(66f, 0f, time));
        }

        private void UpdatePitchTrail(
            BatterMiniGameRequest request,
            float progress,
            bool isPitchInFlight)
        {
            for (int index = 0; index < _miniGamePitchTrail.Length; index++)
            {
                RectTransform trail = _miniGamePitchTrail[index];
                if (trail == null)
                    continue;

                float trailProgress = progress - (index + 1) * 0.045f;
                bool isVisible = isPitchInFlight && trailProgress > 0f;
                trail.gameObject.SetActive(isVisible);
                if (!isVisible)
                    continue;

                trail.anchoredPosition = CalculateBattingPitchPosition(request, trailProgress);
                float scale = Mathf.Lerp(0.34f, 0.92f, trailProgress) *
                              Mathf.Lerp(1f, 0.48f, index / (float)PitchTrailCount);
                trail.localScale = new Vector3(scale, scale, 1f);
            }
        }

        private void UpdateSwingTimingMarker(BatterMiniGameRequest request, float progress)
        {
            if (_miniGameTimingMarker == null)
                return;

            float ideal = Mathf.Clamp((float)request.IdealSwingTime01, 0.01f, 0.99f);
            float normalizedOffset = progress <= ideal
                ? Mathf.Lerp(-1f, 0f, progress / ideal)
                : Mathf.Lerp(0f, 1f, (progress - ideal) / (1f - ideal));
            _miniGameTimingMarker.anchoredPosition = new Vector2(normalizedOffset * 350f, 0f);
            _miniGameTimingMarker.gameObject.SetActive(
                _battingMiniGamePhase != BattingMiniGamePhase.AwaitingReady);
        }

        private void UpdateBattingInstructionText(
            BatterMiniGameRequest request,
            float flightProgress,
            float windupProgress)
        {
            if (_miniGameProgressText == null)
                return;

            switch (_battingMiniGamePhase)
            {
                case BattingMiniGamePhase.AwaitingReady:
                    _miniGameProgressText.text = "타격 의도를 고른 뒤 준비하세요";
                    if (_miniGamePitchReadText != null)
                        _miniGamePitchReadText.text = "준비 전에는 투구가 시작되지 않습니다";
                    break;
                case BattingMiniGamePhase.Windup:
                    _miniGameProgressText.text = windupProgress < 0.55f
                        ? "투수의 와인드업을 읽으세요"
                        : "릴리스 순간에 집중하세요";
                    if (_miniGamePitchReadText != null)
                        _miniGamePitchReadText.text = "공은 릴리스 뒤에 나타납니다";
                    if (_miniGameTrackingStatusText != null)
                        _miniGameTrackingStatusText.text = "투구 동작 중";
                    break;
                default:
                    _miniGameProgressText.text = _miniGameIsTakingPitch
                        ? "스윙하지 않고 공을 끝까지 지켜보는 중"
                        : flightProgress < 0.78f
                            ? "공의 움직임을 따라 배트 위치를 맞추세요"
                            : "지금!  SPACE 또는 클릭";
                    if (_miniGamePitchReadText != null)
                    {
                        _miniGamePitchReadText.text = flightProgress < 0.55f
                            ? "구종 판독 중"
                            : $"{GetPitchTypeLabel(request.Pitch.PitchType)} · " +
                              $"{request.Pitch.VelocityMph:0} mph 추정";
                    }
                    if (_miniGameTrackingStatusText != null)
                        _miniGameTrackingStatusText.text = "투구 추적 중";
                    break;
            }
        }

        private void SubmitMiniGameSwing(float progress)
        {
            CareerMatchSession session = _manager.ActiveMatch;
            if (!IsMiniGameInputReady(session) || !session.PendingSwingExecution.HasValue)
                return;
            BatterMiniGameRequest request = session.PendingSwingExecution.Value;
            PlatePoint batPoint = ApplyAimAssist(_miniGameBatPoint, request);
            var command = new SwingCommand(
                request.RequestId,
                true,
                batPoint,
                Mathf.Clamp01(progress),
                _selectedApproach,
                _selectedApproach == BattingApproach.Bunt);
            int firstNewEventIndex = session.Events.Count;
            if (!_manager.SubmitSwingExecution(command))
                return;
            TryBeginPlayResolution(_manager.ActiveMatch, firstNewEventIndex);
            Render();
        }

        private void SubmitMiniGameTake()
        {
            CareerMatchSession session = _manager.ActiveMatch;
            if (!IsMiniGameInputReady(session) || !session.PendingSwingExecution.HasValue)
                return;
            BatterMiniGameRequest request = session.PendingSwingExecution.Value;
            int firstNewEventIndex = session.Events.Count;
            if (!_manager.SubmitSwingExecution(new SwingCommand(
                request.RequestId,
                false,
                _miniGameBatPoint,
                request.IdealSwingTime01,
                _selectedApproach,
                _selectedApproach == BattingApproach.Bunt)))
            {
                return;
            }
            TryBeginPlayResolution(_manager.ActiveMatch, firstNewEventIndex);
            Render();
        }

        private void AutoCompleteMiniGamePlateAppearance()
        {
            _manager.AutoCompleteCurrentPlateAppearance();
        }

        private void SelectMiniGameSwingIntent(BattingApproach approach)
        {
            _selectedApproach = approach;
            Render();
        }

        private PlatePoint ApplyAimAssist(PlatePoint input, BatterMiniGameRequest request)
        {
            double assist = _manager.CurrentCareer.GameSettings.MiniGameDifficulty switch
            {
                MiniGameDifficulty.Beginner => 0.26d,
                MiniGameDifficulty.Standard => 0.08d,
                _ => 0d
            };
            return new PlatePoint(
                input.X + (request.Pitch.PlatePoint.X - input.X) * assist,
                input.Y + (request.Pitch.PlatePoint.Y - input.Y) * assist);
        }

        private void RenderLatestMiniGameFeedback(RectTransform panel, Vector2 position)
        {
            MatchEvent? latest = null;
            CareerMatchSession session = _manager.ActiveMatch;
            for (int index = _playback.VisibleEventCount - 1; index >= 0; index--)
            {
                MatchEvent current = session.Events[index];
                if (current.EventType == MatchEventType.Pitch && current.PitchPlayData.HasValue)
                {
                    latest = current;
                    break;
                }
            }
            if (!latest.HasValue)
                return;

            PitchPlayData data = latest.Value.PitchPlayData;
            string feedback = $"직전 투구 · {GetPitchTypeLabel(data.Pitch.PitchType)} " +
                              $"{data.Pitch.VelocityMph:0.0} mph\n" +
                              GetContactFeedback(data.Contact);
            CreateText("MiniGameFeedback", panel, feedback,
                15, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(430f, 72f), position, GoldColor);
        }

        private static string BuildPitchPatternGuide(PitchSelectionRequest request)
        {
            int count = request.RecentPitchSequence.Count;
            if (count < 2)
                return "첫 배합입니다. 타자의 능력과 Count를 보고 코스를 선택하세요.";
            PitchType last = request.RecentPitchSequence[count - 1];
            PitchType previous = request.RecentPitchSequence[count - 2];
            return last == previous
                ? $"패턴 경고 · {GetPitchTypeLabel(last)} 연속 사용으로 타자가 대비합니다."
                : $"최근 배합 · {GetPitchTypeLabel(previous)} → {GetPitchTypeLabel(last)}";
        }

        private static string GetContactFeedback(ContactProfile contact)
        {
            if (contact.PitchResult == Baseball.Simulation.PlateAppearance.PitchResult.Ball) return "존 밖 · 지켜봄";
            if (contact.PitchResult == Baseball.Simulation.PlateAppearance.PitchResult.CalledStrike) return "존 안 · 지켜봄";
            if (contact.PitchResult == Baseball.Simulation.PlateAppearance.PitchResult.SwingingStrike) return "헛스윙";
            if (contact.PitchResult == Baseball.Simulation.PlateAppearance.PitchResult.Foul) return "커트 · 파울";
            if (contact.PitchResult == Baseball.Simulation.PlateAppearance.PitchResult.HitByPitch) return "몸에 맞는 공";
            return $"{GetContactGradeLabel(contact.Grade)} · {GetTimingLabel(contact.TimingFeedback)} · " +
                   $"타구 {contact.ExitVelocityMph:0.0} mph";
        }

        private static string GetContactGradeLabel(ContactGrade grade)
        {
            return grade switch
            {
                ContactGrade.Barrel => "완벽한 타격",
                ContactGrade.Solid => "정타",
                ContactGrade.Normal => "보통 타구",
                ContactGrade.Weak => "빗맞음",
                ContactGrade.FoulTip => "커트",
                _ => "헛스윙"
            };
        }

        private static string GetTimingLabel(SwingTimingFeedback feedback)
        {
            return feedback switch
            {
                SwingTimingFeedback.VeryEarly => "매우 빠름",
                SwingTimingFeedback.Early => "빠름",
                SwingTimingFeedback.Perfect => "정확",
                SwingTimingFeedback.Late => "늦음",
                _ => "매우 늦음"
            };
        }

        private static string GetMiniGameSwingIntentLabel(BattingApproach approach)
        {
            return approach switch
            {
                BattingApproach.Contact => "컨택",
                BattingApproach.Power => "강타",
                BattingApproach.Patient => "신중",
                BattingApproach.Bunt => "번트",
                _ => "일반"
            };
        }

        private static string GetPitchTypeLabel(PitchType pitchType)
        {
            return pitchType switch
            {
                PitchType.FourSeamFastball => "포심",
                PitchType.TwoSeamFastball => "투심",
                PitchType.Cutter => "커터",
                PitchType.Slider => "슬라이더",
                PitchType.Curveball => "커브",
                PitchType.Changeup => "체인지업",
                PitchType.Splitter => "스플리터",
                PitchType.Sinker => "싱커",
                _ => pitchType.ToString()
            };
        }

        private static Vector2 GetBattingCursorSize(BattingApproach approach)
        {
            return approach switch
            {
                BattingApproach.Contact => new Vector2(118f, 52f),
                BattingApproach.Power => new Vector2(82f, 38f),
                BattingApproach.Bunt => new Vector2(132f, 30f),
                _ => new Vector2(100f, 44f)
            };
        }

        private static RectTransform CreateMiniGameSpriteImage(
            string name,
            Transform parent,
            Sprite sprite,
            Color color,
            Vector2 size,
            Vector2 position)
        {
            RectTransform rect = CreateImage(name, parent, color, size, position);
            Image image = rect.GetComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.raycastTarget = false;
            rect.gameObject.AddComponent<CareerUiVisualElement>()
                .Initialize(CareerUiVisualRole.DataImage);
            return rect;
        }

        private static RectTransform CreatePitchFieldIllustration(Transform parent, Vector2 size)
        {
            Sprite sprite = CareerMatchMiniGameSprites.GetPitchFieldIllustration();
            if (sprite == null)
                return null;

            RectTransform rect = CreateMiniGameSpriteImage(
                "PitchFieldIllustration",
                parent,
                sprite,
                Color.white,
                size,
                Vector2.zero);
            rect.GetComponent<Image>().preserveAspect = true;
            return rect;
        }

        private static RectTransform CreateBaseballIllustration(
            string name,
            Transform parent,
            Vector2 size,
            Vector2 position)
        {
            Sprite sprite = CareerMatchMiniGameSprites.GetBaseballBallIllustration() ??
                            GetMiniGameSolidCircleSprite();
            RectTransform rect = CreateMiniGameSpriteImage(
                name,
                parent,
                sprite,
                Color.white,
                size,
                position);
            rect.GetComponent<Image>().preserveAspect = true;
            return rect;
        }

        private static RectTransform CreateBroadcastFieldIllustration(
            Transform parent,
            Vector2 size,
            Vector2 position,
            bool preserveAspect = false)
        {
            Sprite sprite = CareerMatchMiniGameSprites.GetBroadcastFieldIllustration();
            if (sprite == null)
                return null;

            RectTransform rect = CreateMiniGameSpriteImage(
                "BroadcastFieldIllustration",
                parent,
                sprite,
                Color.white,
                size,
                position);
            rect.GetComponent<Image>().preserveAspect = preserveAspect;
            return rect;
        }

        private static Sprite GetMiniGameSolidCircleSprite()
        {
            if (_miniGameSolidCircleSprite == null)
                _miniGameSolidCircleSprite = CreateMiniGameCircleSprite(false);
            return _miniGameSolidCircleSprite;
        }

        private static Sprite GetMiniGameRingSprite()
        {
            if (_miniGameRingSprite == null)
                _miniGameRingSprite = CreateMiniGameCircleSprite(true);
            return _miniGameRingSprite;
        }

        private static Sprite CreateMiniGameCircleSprite(bool isRing)
        {
            const int textureSize = 64;
            var texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
            {
                name = isRing ? "MiniGameRingTexture" : "MiniGameCircleTexture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[textureSize * textureSize];
            Vector2 center = new Vector2((textureSize - 1) * 0.5f, (textureSize - 1) * 0.5f);
            float radius = textureSize * 0.48f;
            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    float normalizedDistance = Vector2.Distance(new Vector2(x, y), center) / radius;
                    float alpha;
                    if (isRing)
                    {
                        float ringDistance = Mathf.Abs(normalizedDistance - 0.86f);
                        alpha = 1f - Mathf.SmoothStep(0.045f, 0.11f, ringDistance);
                    }
                    else
                    {
                        alpha = 1f - Mathf.SmoothStep(0.88f, 1f, normalizedDistance);
                    }
                    pixels[y * textureSize + x] = new Color32(255, 255, 255, (byte)(255f * alpha));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, textureSize, textureSize),
                new Vector2(0.5f, 0.5f),
                textureSize);
            sprite.name = isRing ? "MiniGameRing" : "MiniGameCircle";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static void CreateZoneGrid(RectTransform strikeZone)
        {
            for (int index = 1; index <= 2; index++)
            {
                float x = -strikeZone.sizeDelta.x * 0.5f + strikeZone.sizeDelta.x * index / 3f;
                float y = -strikeZone.sizeDelta.y * 0.5f + strikeZone.sizeDelta.y * index / 3f;
                CreateImage("Vertical_" + index, strikeZone, new Color(0.35f, 0.65f, 0.82f, 0.35f),
                    new Vector2(1f, strikeZone.sizeDelta.y), new Vector2(x, 0f));
                CreateImage("Horizontal_" + index, strikeZone, new Color(0.35f, 0.65f, 0.82f, 0.35f),
                    new Vector2(strikeZone.sizeDelta.x, 1f), new Vector2(0f, y));
            }
        }

        private static Vector2 ToBattingScenePosition(PlatePoint point)
        {
            return new Vector2(
                (float)point.X * BattingZoneScaleX,
                BattingZoneCenterY + (float)point.Y * BattingZoneScaleY);
        }

        private static PlatePoint ClampPlatePoint(PlatePoint point)
        {
            return new PlatePoint(
                Math.Max(-1.30d, Math.Min(1.30d, point.X)),
                Math.Max(-1.25d, Math.Min(1.25d, point.Y)));
        }
    }
}
