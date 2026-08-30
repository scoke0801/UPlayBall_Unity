using System;
using Baseball.Core.Players;
using Baseball.Game.Career;
using Baseball.Simulation.Match;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Baseball.Presentation.Career
{
    public sealed partial class UI_Scene_CareerMatch
    {
        private const float MiniGamePlateScaleX = 130f;
        private const float MiniGamePlateScaleY = 120f;
        private const float MiniGameAimSpeed = 1.75f;

        private int _miniGameStateKey = int.MinValue;
        private float _miniGamePitchStartedAt;
        private PlatePoint _miniGameBatPoint;
        private PlatePoint _miniGamePitchTarget;
        private PitchType _miniGameSelectedPitch;
        private RectTransform _miniGamePlateRect;
        private RectTransform _miniGameBall;
        private RectTransform _miniGameBatCursor;
        private RectTransform _miniGameArrivalGuide;
        private RectTransform _miniGamePitchAim;
        private RectTransform _miniGameCommandEllipse;
        private Text _miniGameProgressText;
        private Text _miniGameTargetText;

        private bool IsMiniGameInputReady(CareerMatchSession session)
        {
            return session != null &&
                   session.Mode == CareerMatchMode.MiniGame &&
                   session.Phase == CareerMatchPhase.Playing &&
                   (session.PendingPitchSelection.HasValue || session.PendingSwingExecution.HasValue) &&
                   !_hasControlledResult &&
                   !_playback.HasPendingEvents(session.Events);
        }

        private bool UpdateMiniGameInput(CareerMatchSession session, Keyboard keyboard)
        {
            if (!IsMiniGameInputReady(session))
                return false;

            EnsureMiniGameState(session);
            if (session.PendingPitchSelection.HasValue)
            {
                UpdatePitchAimInput(keyboard);
                if (keyboard != null)
                {
                    if (keyboard.digit1Key.wasPressedThisFrame) SelectMiniGamePitchByIndex(0);
                    else if (keyboard.digit2Key.wasPressedThisFrame) SelectMiniGamePitchByIndex(1);
                    else if (keyboard.digit3Key.wasPressedThisFrame) SelectMiniGamePitchByIndex(2);
                    else if (keyboard.digit4Key.wasPressedThisFrame) SelectMiniGamePitchByIndex(3);
                    else if (keyboard.digit5Key.wasPressedThisFrame) SelectMiniGamePitchByIndex(4);
                    else if (keyboard.spaceKey.wasPressedThisFrame) SubmitMiniGamePitch();
                    else if (keyboard.aKey.wasPressedThisFrame) AutoCompleteMiniGamePlateAppearance();
                }
                return true;
            }

            UpdateBattingCursorInput(keyboard);
            BatterMiniGameRequest request = session.PendingSwingExecution.Value;
            float flightSeconds = Mathf.Max(0.18f, (float)request.Pitch.PlateArrivalMilliseconds / 1000f);
            float progress = Mathf.Clamp01((Time.unscaledTime - _miniGamePitchStartedAt) / flightSeconds);
            UpdateBattingMiniGameVisuals(request, progress);
            Mouse mouse = Mouse.current;
            bool swung = keyboard != null && keyboard.spaceKey.wasPressedThisFrame ||
                         mouse != null && mouse.leftButton.wasPressedThisFrame;
            if (swung)
                SubmitMiniGameSwing(progress);
            else if (keyboard != null && keyboard.aKey.wasPressedThisFrame)
                AutoCompleteMiniGamePlateAppearance();
            else if (progress >= 1f)
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
                _miniGameSelectedPitch = request.SuggestedPitch.PitchType;
                _miniGamePitchTarget = request.SuggestedPitch.TargetPoint;
            }
            else
            {
                BatterMiniGameRequest request = session.PendingSwingExecution.Value;
                _miniGameBatPoint = new PlatePoint(0d, 0d);
                _miniGamePitchStartedAt = Time.unscaledTime;
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
            EnsureMiniGameState(session);
            if (session.PendingPitchSelection.HasValue)
                RenderPitchingMiniGameStage(panel, session.PendingPitchSelection.Value);
            else
                RenderBattingMiniGameStage(panel, session.PendingSwingExecution.Value);
        }

        private void RenderBattingMiniGameStage(
            RectTransform panel,
            BatterMiniGameRequest request)
        {
            CreateText("MiniGameTitle", panel, "직접 타격", 25, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(360f, 38f), new Vector2(0f, 248f), RoleColor);
            CreateText("MiniGameSituation", panel,
                $"{request.Inning}회{GetHalfLabel(request.Half)} · {request.Outs}사 · " +
                $"볼 {request.Balls} · 스트라이크 {request.Strikes} · {GetPitchTypeLabel(request.Pitch.PitchType)}",
                15, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(820f, 28f), new Vector2(0f, 216f), SecondaryTextColor);

            _miniGamePlateRect = CreateImage(
                "BattingPlane", panel, new Color(0.015f, 0.045f, 0.07f, 1f),
                new Vector2(430f, 350f), new Vector2(0f, 2f));
            RectTransform strikeZone = CreateImage(
                "StrikeZone", _miniGamePlateRect, new Color(0.10f, 0.27f, 0.38f, 0.24f),
                new Vector2(MiniGamePlateScaleX * 2f, MiniGamePlateScaleY * 2f), Vector2.zero);
            Image strikeImage = strikeZone.GetComponent<Image>();
            strikeImage.raycastTarget = false;
            CreateZoneGrid(strikeZone);

            _miniGameArrivalGuide = CreateImage(
                "ArrivalGuide", _miniGamePlateRect, new Color(0.20f, 0.76f, 1f, 0.25f),
                new Vector2(38f, 38f), ToPlatePosition(request.Pitch.PlatePoint));
            _miniGameArrivalGuide.gameObject.SetActive(false);
            _miniGameBall = CreateImage(
                "Ball", _miniGamePlateRect, new Color(0.98f, 0.98f, 0.92f, 1f),
                new Vector2(22f, 22f), new Vector2(0f, 145f));
            _miniGameBatCursor = CreateImage(
                "BatCursor", _miniGamePlateRect, new Color(0.98f, 0.68f, 0.18f, 0.72f),
                new Vector2(86f, 36f), ToPlatePosition(_miniGameBatPoint));
            _miniGameProgressText = CreateText(
                "FlightGuide", panel, "공의 궤적을 읽고 SPACE 또는 클릭으로 스윙",
                14, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(760f, 26f), new Vector2(0f, -205f), SecondaryTextColor);
            CreateText("PitchMetrics", panel,
                $"{request.Pitch.VelocityMph:0.0} mph · 변화량 " +
                $"{Math.Abs(request.Pitch.HorizontalBreak) + Math.Abs(request.Pitch.VerticalBreak):0.00}",
                13, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(480f, 24f), new Vector2(0f, -234f), MutedTextColor);
            UpdateBattingMiniGameVisuals(request, 0f);
        }

        private void RenderPitchingMiniGameStage(
            RectTransform panel,
            PitchSelectionRequest request)
        {
            CreateText("MiniGameTitle", panel, "직접 투구", 25, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(360f, 38f), new Vector2(0f, 248f), RoleColor);
            CreateText("MiniGameSituation", panel,
                $"{request.Inning}회{GetHalfLabel(request.Half)} · {request.Outs}사 · " +
                $"볼 {request.Balls} · 스트라이크 {request.Strikes}",
                15, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(820f, 28f), new Vector2(0f, 216f), SecondaryTextColor);

            _miniGamePlateRect = CreateImage(
                "PitchTargetPlane", panel, new Color(0.015f, 0.045f, 0.07f, 1f),
                new Vector2(430f, 350f), new Vector2(0f, 2f));
            Image targetImage = _miniGamePlateRect.GetComponent<Image>();
            targetImage.raycastTarget = true;
            Button targetButton = _miniGamePlateRect.gameObject.AddComponent<Button>();
            targetButton.transition = Selectable.Transition.None;
            targetButton.onClick.AddListener(UpdatePitchTargetFromPointer);

            RectTransform strikeZone = CreateImage(
                "StrikeZone", _miniGamePlateRect, new Color(0.10f, 0.27f, 0.38f, 0.28f),
                new Vector2(MiniGamePlateScaleX * 2f, MiniGamePlateScaleY * 2f), Vector2.zero);
            CreateZoneGrid(strikeZone);
            PitchOption option = FindSelectedPitchOption(request);
            _miniGameCommandEllipse = CreateImage(
                "CommandEllipse", _miniGamePlateRect, new Color(0.12f, 0.64f, 1f, 0.18f),
                new Vector2(
                    Mathf.Max(20f, (float)option.CommandEllipse.RadiusX * MiniGamePlateScaleX * 4f),
                    Mathf.Max(20f, (float)option.CommandEllipse.RadiusY * MiniGamePlateScaleY * 4f)),
                ToPlatePosition(_miniGamePitchTarget));
            _miniGameCommandEllipse.localRotation = Quaternion.Euler(
                0f, 0f, (float)option.CommandEllipse.RotationDegrees);
            _miniGamePitchAim = CreateImage(
                "PitchAim", _miniGamePlateRect, new Color(0.98f, 0.70f, 0.18f, 0.92f),
                new Vector2(18f, 18f), ToPlatePosition(_miniGamePitchTarget));
            _miniGameTargetText = CreateText(
                "TargetGuide", panel,
                $"목표 X {_miniGamePitchTarget.X:+0.00;-0.00;0.00} · Y {_miniGamePitchTarget.Y:+0.00;-0.00;0.00}",
                14, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(620f, 26f), new Vector2(0f, -205f), GoldColor);
            CreateText("CommandGuide", panel,
                "타원은 현재 피로·Control·구종 숙련도를 반영한 예상 제구 범위입니다.",
                13, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(760f, 24f), new Vector2(0f, -235f), SecondaryTextColor);
        }

        private void RenderMiniGameControlPanel(RectTransform panel, CareerMatchSession session)
        {
            if (session.PendingPitchSelection.HasValue)
                RenderPitchSelectionControls(panel, session.PendingPitchSelection.Value);
            else
                RenderSwingControls(panel, session.PendingSwingExecution.Value);
        }

        private void RenderPitchSelectionControls(RectTransform panel, PitchSelectionRequest request)
        {
            CreateStatusPill(panel, "구종 + 목표 위치", new Vector2(450f, 50f), new Vector2(0f, 396f));
            CreateText("PitchTitle", panel, "보유 구종", 24, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(420f, 36f), new Vector2(0f, 340f), PrimaryTextColor);
            int count = Mathf.Min(5, request.AvailablePitches.Count);
            for (int index = 0; index < count; index++)
            {
                PitchOption option = request.AvailablePitches[index];
                bool selected = option.PitchType == _miniGameSelectedPitch;
                Button button = CreateButton(
                    "PitchType_" + option.PitchType,
                    panel,
                    $"{index + 1}  {GetPitchTypeLabel(option.PitchType)}   " +
                    $"{option.MinimumVelocityMph:0}-{option.MaximumVelocityMph:0} mph",
                    new Vector2(420f, 54f),
                    new Vector2(0f, 278f - index * 61f),
                    selected ? new Color(0.025f, 0.32f, 0.52f, 1f) : PanelDarkColor,
                    selected ? PrimaryTextColor : SecondaryTextColor);
                PitchType selectedType = option.PitchType;
                button.onClick.AddListener(() => SelectMiniGamePitch(selectedType));
            }

            Button throwPitch = CreateButton(
                "ThrowPitch", panel, "이 위치로 투구   SPACE",
                new Vector2(430f, 64f), new Vector2(0f, -92f),
                new Color(0.02f, 0.38f, 0.7f, 1f), PrimaryTextColor);
            throwPitch.onClick.AddListener(SubmitMiniGamePitch);
            Button autoBatter = CreateButton(
                "AutoBatter", panel, "이번 타자 자동   A",
                new Vector2(205f, 48f), new Vector2(-110f, -160f),
                PanelDarkColor, SecondaryTextColor);
            autoBatter.onClick.AddListener(AutoCompleteMiniGamePlateAppearance);
            Button autoInning = CreateButton(
                "AutoInning", panel, "이번 이닝 자동",
                new Vector2(205f, 48f), new Vector2(110f, -160f),
                PanelDarkColor, SecondaryTextColor);
            autoInning.onClick.AddListener(() =>
                _manager.AutoCompleteCurrentPitchingInning(_selectedPitchingApproach));
            CreateText("Pattern", panel, BuildPitchPatternGuide(request),
                14, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(430f, 72f), new Vector2(0f, -240f), SecondaryTextColor);
            RenderLatestMiniGameFeedback(panel, new Vector2(0f, -338f));
        }

        private void RenderSwingControls(RectTransform panel, BatterMiniGameRequest request)
        {
            CreateStatusPill(panel, "위치 + 타이밍", new Vector2(450f, 50f), new Vector2(0f, 396f));
            CreateText("SwingTitle", panel, "타격 의도", 24, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(420f, 36f), new Vector2(0f, 340f), PrimaryTextColor);
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
                    new Vector2(0f, 278f - index * 59f),
                    selected ? new Color(0.025f, 0.32f, 0.52f, 1f) : PanelDarkColor,
                    selected ? PrimaryTextColor : SecondaryTextColor);
                button.onClick.AddListener(() => SelectMiniGameSwingIntent(approach));
            }

            CreateText("SwingGuide", panel,
                "커서를 공의 도착 위치에 맞추고 임팩트 직전에 스윙하세요.\n스윙하지 않으면 실제 위치로 볼·스트라이크가 판정됩니다.",
                14, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(430f, 62f), new Vector2(0f, -70f), SecondaryTextColor);
            Button take = CreateButton(
                "TakePitch", panel, "이번 공 지켜보기",
                new Vector2(205f, 48f), new Vector2(-110f, -145f),
                PanelDarkColor, SecondaryTextColor);
            take.onClick.AddListener(SubmitMiniGameTake);
            Button auto = CreateButton(
                "AutoPlateAppearance", panel, "이번 타석 자동   A",
                new Vector2(205f, 48f), new Vector2(110f, -145f),
                PanelDarkColor, SecondaryTextColor);
            auto.onClick.AddListener(AutoCompleteMiniGamePlateAppearance);
            RenderLatestMiniGameFeedback(panel, new Vector2(0f, -260f));
        }

        private void UpdateBattingCursorInput(Keyboard keyboard)
        {
            Mouse mouse = Mouse.current;
            if (mouse != null && _miniGamePlateRect != null &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _miniGamePlateRect,
                    mouse.position.ReadValue(),
                    null,
                    out Vector2 local))
            {
                _miniGameBatPoint = ClampPlatePoint(new PlatePoint(
                    local.x / MiniGamePlateScaleX,
                    local.y / MiniGamePlateScaleY));
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
                _miniGameBatCursor.anchoredPosition = ToPlatePosition(_miniGameBatPoint);
        }

        private void UpdatePitchAimInput(Keyboard keyboard)
        {
            if (keyboard != null)
            {
                double horizontal = (keyboard.rightArrowKey.isPressed ? 1d : 0d) -
                                    (keyboard.leftArrowKey.isPressed ? 1d : 0d);
                double vertical = (keyboard.upArrowKey.isPressed ? 1d : 0d) -
                                  (keyboard.downArrowKey.isPressed ? 1d : 0d);
                if (horizontal != 0d || vertical != 0d)
                {
                    double delta = MiniGameAimSpeed * Time.unscaledDeltaTime;
                    _miniGamePitchTarget = ClampPlatePoint(new PlatePoint(
                        _miniGamePitchTarget.X + horizontal * delta,
                        _miniGamePitchTarget.Y + vertical * delta));
                }
            }
            UpdatePitchAimVisuals();
        }

        private void UpdatePitchTargetFromPointer()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || _miniGamePlateRect == null)
                return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _miniGamePlateRect,
                    mouse.position.ReadValue(),
                    null,
                    out Vector2 local))
                return;
            _miniGamePitchTarget = ClampPlatePoint(new PlatePoint(
                local.x / MiniGamePlateScaleX,
                local.y / MiniGamePlateScaleY));
            UpdatePitchAimVisuals();
        }

        private void UpdatePitchAimVisuals()
        {
            Vector2 position = ToPlatePosition(_miniGamePitchTarget);
            if (_miniGamePitchAim != null) _miniGamePitchAim.anchoredPosition = position;
            if (_miniGameCommandEllipse != null) _miniGameCommandEllipse.anchoredPosition = position;
            if (_miniGameTargetText != null)
            {
                _miniGameTargetText.text =
                    $"목표 X {_miniGamePitchTarget.X:+0.00;-0.00;0.00} · " +
                    $"Y {_miniGamePitchTarget.Y:+0.00;-0.00;0.00}";
            }
        }

        private void UpdateBattingMiniGameVisuals(BatterMiniGameRequest request, float progress)
        {
            if (_miniGameBall != null)
            {
                float eased = progress * progress * (3f - 2f * progress);
                float curve = Mathf.Sin(progress * Mathf.PI);
                float x = Mathf.Lerp((float)request.Pitch.ReleasePoint.X * 70f,
                              (float)request.Pitch.PlatePoint.X * MiniGamePlateScaleX,
                              eased) +
                          (float)request.Pitch.HorizontalBreak * curve * 55f;
                float y = Mathf.Lerp(145f,
                              (float)request.Pitch.PlatePoint.Y * MiniGamePlateScaleY,
                              eased) +
                          (float)request.Pitch.VerticalBreak * curve * 45f;
                _miniGameBall.anchoredPosition = new Vector2(x, y);
                float scale = Mathf.Lerp(0.52f, 1.2f, eased);
                _miniGameBall.localScale = new Vector3(scale, scale, 1f);
            }

            MiniGameDifficulty difficulty = _manager.CurrentCareer.GameSettings.MiniGameDifficulty;
            if (_miniGameArrivalGuide != null)
                _miniGameArrivalGuide.gameObject.SetActive(
                    difficulty == MiniGameDifficulty.Beginner && progress >= 0.68f);
            if (_miniGameProgressText != null)
                _miniGameProgressText.text = progress < 0.82f
                    ? "공의 궤적을 읽으세요"
                    : "지금!  SPACE 또는 클릭";
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
            _manager.SubmitSwingExecution(command);
        }

        private void SubmitMiniGameTake()
        {
            CareerMatchSession session = _manager.ActiveMatch;
            if (!IsMiniGameInputReady(session) || !session.PendingSwingExecution.HasValue)
                return;
            BatterMiniGameRequest request = session.PendingSwingExecution.Value;
            _manager.SubmitSwingExecution(new SwingCommand(
                request.RequestId,
                false,
                _miniGameBatPoint,
                request.IdealSwingTime01,
                _selectedApproach,
                _selectedApproach == BattingApproach.Bunt));
        }

        private void SubmitMiniGamePitch()
        {
            CareerMatchSession session = _manager.ActiveMatch;
            if (!IsMiniGameInputReady(session) || !session.PendingPitchSelection.HasValue)
                return;
            PitchSelectionRequest request = session.PendingPitchSelection.Value;
            _manager.SubmitPitchSelection(new PitchSelectionCommand(
                request.RequestId,
                _miniGameSelectedPitch,
                _miniGamePitchTarget,
                _selectedPitchingApproach));
        }

        private void AutoCompleteMiniGamePlateAppearance()
        {
            _manager.AutoCompleteCurrentPlateAppearance();
        }

        private void SelectMiniGamePitch(PitchType pitchType)
        {
            _miniGameSelectedPitch = pitchType;
            Render();
        }

        private void SelectMiniGamePitchByIndex(int index)
        {
            CareerMatchSession session = _manager.ActiveMatch;
            if (!session.PendingPitchSelection.HasValue)
                return;
            PitchSelectionRequest request = session.PendingPitchSelection.Value;
            if (index < 0 || index >= request.AvailablePitches.Count)
                return;
            SelectMiniGamePitch(request.AvailablePitches[index].PitchType);
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

        private PitchOption FindSelectedPitchOption(PitchSelectionRequest request)
        {
            for (int index = 0; index < request.AvailablePitches.Count; index++)
            {
                if (request.AvailablePitches[index].PitchType == _miniGameSelectedPitch)
                    return request.AvailablePitches[index];
            }
            return request.AvailablePitches[0];
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

        private static Vector2 ToPlatePosition(PlatePoint point)
        {
            return new Vector2(
                (float)point.X * MiniGamePlateScaleX,
                (float)point.Y * MiniGamePlateScaleY);
        }

        private static PlatePoint ClampPlatePoint(PlatePoint point)
        {
            return new PlatePoint(
                Math.Max(-1.30d, Math.Min(1.30d, point.X)),
                Math.Max(-1.25d, Math.Min(1.25d, point.Y)));
        }
    }
}
