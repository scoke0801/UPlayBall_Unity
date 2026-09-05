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
        private static readonly PlayerPosition[] PlayResolutionFielderPositions =
        {
            PlayerPosition.StartingPitcher,
            PlayerPosition.Catcher,
            PlayerPosition.FirstBase,
            PlayerPosition.SecondBase,
            PlayerPosition.ThirdBase,
            PlayerPosition.Shortstop,
            PlayerPosition.LeftField,
            PlayerPosition.CenterField,
            PlayerPosition.RightField
        };

        [SerializeField] private PlayResolutionPresentationConfig playResolutionPresentation = new();

        private readonly PlayResolutionSequenceBuilder _playResolutionBuilder = new();
        private readonly PlayResolutionSequenceController _playResolution = new();

        private RectTransform _playResolutionRoot;
        private PlayResolutionPresenter _playResolutionPresenter;

        /// <summary>타자와 투수가 공유하는 Plate/Field 결과 연출 계층을 한 번만 생성한다.</summary>
        private void InitializePlayResolutionPresentation(RectTransform controlLayer)
        {
            _playResolutionRoot = CreateImage(
                "PlayResolutionLayer",
                controlLayer,
                WithAlpha(CareerUiTheme.InputBlocker, 0.995f),
                new Vector2(1280f, 800f),
                new Vector2(-145f, 20f));
            Image rootImage = _playResolutionRoot.GetComponent<Image>();
            rootImage.raycastTarget = true;

            RectTransform plateView = CreateImage(
                "PlateView",
                _playResolutionRoot,
                CareerUiTheme.PanelDark,
                new Vector2(1248f, 666f),
                new Vector2(0f, 12f));
            CreatePitchFieldIllustration(plateView, new Vector2(1248f, 700f));
            CreatePlateViewGuides(plateView, out RectTransform plateBall, out RectTransform plateBat,
                out RectTransform impactRing);

            RectTransform fieldView = CreateImage(
                "FieldView",
                _playResolutionRoot,
                CareerUiTheme.RoleBand,
                new Vector2(1248f, 666f),
                new Vector2(0f, 12f));
            fieldView.gameObject.AddComponent<RectMask2D>();
            CreateFieldViewGround(fieldView);
            FielderVisual[] fielders = CreateFielders(fieldView);
            RectTransform[] runners = new RectTransform[4];
            Text[] runnerLabels = new Text[4];
            for (int index = 0; index < runners.Length; index++)
                CreateRunnerVisual(fieldView, index, out runners[index], out runnerLabels[index]);

            RectTransform throwLine = CreateImage(
                "ThrowLine",
                fieldView,
                WithAlpha(CareerUiTheme.TextSecondary, 0.48f),
                new Vector2(1f, 3f),
                Vector2.zero);
            throwLine.GetComponent<Image>().raycastTarget = false;
            RectTransform fieldBall = CreateBaseballIllustration(
                "FieldBall",
                fieldView,
                new Vector2(18f, 18f),
                Vector2.zero);
            fieldBall.SetAsLastSibling();

            Text phaseText = CreateText(
                "ResolutionPhase",
                _playResolutionRoot,
                string.Empty,
                16,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                new Vector2(580f, 28f),
                new Vector2(-334f, 376f),
                SecondaryTextColor);
            Text callText = CreateText(
                "ResolutionCall",
                _playResolutionRoot,
                string.Empty,
                34,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                new Vector2(760f, 50f),
                new Vector2(0f, -347f),
                PrimaryTextColor);
            Text detailText = CreateText(
                "ResolutionDetail",
                _playResolutionRoot,
                string.Empty,
                15,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                new Vector2(1040f, 28f),
                new Vector2(0f, -383f),
                SecondaryTextColor);

            _playResolutionPresenter = new PlayResolutionPresenter(
                _playResolutionRoot,
                plateView,
                fieldView,
                plateBall,
                plateBat,
                impactRing,
                fieldBall,
                throwLine,
                fielders,
                runners,
                runnerLabels,
                phaseText,
                callText,
                detailText);
            HidePlayResolutionPresentation();
        }

        /// <summary>새로 확정된 경기 이벤트를 결과를 선공개하지 않는 공통 Cue 시퀀스로 시작한다.</summary>
        private bool TryBeginPlayResolution(CareerMatchSession session, int firstEventIndex)
        {
            if (session == null || _playResolution.IsActive || _playResolutionPresenter == null)
                return false;
            if (!_playResolutionBuilder.TryBuild(
                    session.Events,
                    firstEventIndex,
                    playResolutionPresentation,
                    out PlayResolutionSequence sequence))
            {
                return false;
            }

            CareerMatchPlaybackSnapshot snapshot = _playback.BuildSnapshot(session.Events);
            string fielderName = sequence.FielderId > 0
                ? FindPlayerName(session.Input, sequence.FielderId)
                : string.Empty;
            _playResolutionPresenter.SetBattingHand(
                ResolveBattingSide(session.Input, sequence.BatterId, sequence.PitcherId));
            _playResolution.Begin(sequence);
            _playResolutionPresenter.Begin(sequence, snapshot, fielderName);
            SetPlayResolutionInputLocked(true);
            return true;
        }

        /// <summary>재생 시계를 진행하고 Cue가 발생한 직후에만 HUD와 로그 이벤트를 공개한다.</summary>
        private bool UpdatePlayResolutionPresentation(CareerMatchSession session, Keyboard keyboard)
        {
            if (!_playResolution.IsActive)
                return false;

            if (keyboard != null && keyboard.pKey.wasPressedThisFrame)
                _playerMatchControls.TryTogglePause();

            double deltaSeconds = _isPaused ? 0d : Time.unscaledDeltaTime;
            bool isComplete = _playResolution.Tick(deltaSeconds);
            PlayResolutionSequence sequence = _playResolution.Sequence;
            _playResolutionPresenter.Render(sequence, _playResolution.ElapsedSeconds);

            int revealThroughEventIndex = _playResolution.GetRevealThroughEventIndex();
            if (revealThroughEventIndex >= _playback.VisibleEventCount &&
                revealThroughEventIndex < session.Events.Count)
            {
                _playback.RevealThroughEvent(session.Events, revealThroughEventIndex);
                Render();
                SetPlayResolutionInputLocked(true);
            }

            if (!isComplete)
                return true;

            if (sequence.LastEventIndex >= _playback.VisibleEventCount &&
                sequence.LastEventIndex < session.Events.Count)
            {
                _playback.RevealThroughEvent(session.Events, sequence.LastEventIndex);
            }

            _playResolution.Complete();
            HidePlayResolutionPresentation();
            SetPlayResolutionInputLocked(false);
            _nextAutomaticPlayAt = Time.unscaledTime + GetAutomaticPlayIntervalSeconds();
            Render();
            return true;
        }

        private void ResetPlayResolutionPresentation()
        {
            _playResolution.Complete();
            HidePlayResolutionPresentation();
            SetPlayResolutionInputLocked(false);
        }

        private void HidePlayResolutionPresentation()
        {
            _playResolutionPresenter?.Hide();
            if (_playResolutionRoot != null)
                _playResolutionRoot.gameObject.SetActive(false);
        }

        private void SetPlayResolutionInputLocked(bool isLocked)
        {
            if (_controlHost != null)
                _controlHost.gameObject.SetActive(!isLocked);
        }

        private static void CreatePlateViewGuides(
            RectTransform plateView,
            out RectTransform ball,
            out RectTransform bat,
            out RectTransform impactRing)
        {
            RectTransform strikeZone = CreateImage(
                "ResolutionStrikeZone",
                plateView,
                WithAlpha(CareerUiTheme.Primary, 0.12f),
                new Vector2(
                    PlayResolutionPlateLayout.ZoneScaleX * 2f,
                    PlayResolutionPlateLayout.ZoneScaleY * 2f),
                new Vector2(0f, PlayResolutionPlateLayout.ZoneCenterY));
            Outline zoneOutline = strikeZone.gameObject.AddComponent<Outline>();
            zoneOutline.effectColor = WithAlpha(CareerUiTheme.PrimaryBright, 0.82f);
            zoneOutline.effectDistance = new Vector2(1f, -1f);
            CreateZoneGrid(strikeZone);

            ball = CreateBaseballIllustration(
                "ResolutionPlateBall",
                plateView,
                new Vector2(32f, 32f),
                new Vector2(0f, PlayResolutionPlateLayout.ZoneCenterY));
            impactRing = CreateMiniGameSpriteImage(
                "ResolutionImpactRing",
                plateView,
                GetMiniGameRingSprite(),
                WithAlpha(CareerUiTheme.AccentGold, 0.92f),
                new Vector2(76f, 76f),
                new Vector2(0f, PlayResolutionPlateLayout.ZoneCenterY));
            bat = CreateMiniGameSpriteImage(
                "ResolutionBat",
                plateView,
                CareerMatchMiniGameSprites.GetBaseballBatIllustration(),
                Color.white,
                new Vector2(208f, 104f),
                Vector2.zero);
            bat.pivot = new Vector2(0.08f, 0.5f);
            bat.localScale = new Vector3(-1f, 1f, 1f);
            bat.GetComponent<Image>().preserveAspect = true;
        }

        private static void CreateFieldViewGround(RectTransform fieldView)
        {
            RectTransform outfield = CreateImage(
                "OutfieldShade",
                fieldView,
                CareerUiTheme.SuccessAction,
                new Vector2(770f, 770f),
                new Vector2(0f, 190f));
            outfield.localRotation = Quaternion.Euler(0f, 0f, 45f);
            outfield.GetComponent<Image>().raycastTarget = false;

            RectTransform infield = CreateImage(
                "InfieldDirt",
                fieldView,
                WithAlpha(CareerUiTheme.Warning, 0.94f),
                new Vector2(245f, 245f),
                new Vector2(0f, -52f));
            infield.localRotation = Quaternion.Euler(0f, 0f, 45f);
            infield.GetComponent<Image>().raycastTarget = false;

            CreateFieldLine(fieldView, new Vector2(0f, -258f), new Vector2(-430f, 184f));
            CreateFieldLine(fieldView, new Vector2(0f, -258f), new Vector2(430f, 184f));
            for (int baseNumber = 1; baseNumber <= 4; baseNumber++)
            {
                RectTransform plate = CreateImage(
                    "Base_" + baseNumber,
                    fieldView,
                    CareerUiTheme.TextPrimary,
                    baseNumber == 4 ? new Vector2(20f, 12f) : new Vector2(15f, 15f),
                    ToPlayResolutionFieldPosition(PlayResolutionFieldLayout.GetBasePoint(baseNumber)));
                plate.localRotation = Quaternion.Euler(0f, 0f, baseNumber == 4 ? 0f : 45f);
                plate.GetComponent<Image>().raycastTarget = false;
            }
        }

        private static FielderVisual[] CreateFielders(RectTransform fieldView)
        {
            var visuals = new FielderVisual[PlayResolutionFielderPositions.Length];
            for (int index = 0; index < PlayResolutionFielderPositions.Length; index++)
            {
                PlayerPosition position = PlayResolutionFielderPositions[index];
                RectTransform root = CreateRect(
                    "Fielder_" + position,
                    fieldView,
                    new Vector2(54f, 54f),
                    ToPlayResolutionFieldPosition(PlayResolutionFieldLayout.GetFielderPoint(position)));
                RectTransform badge = CreateMiniGameSpriteImage(
                    "Badge",
                    root,
                    GetMiniGameSolidCircleSprite(),
                    CareerUiTheme.PrimaryAction,
                    new Vector2(38f, 38f),
                    Vector2.zero);
                Text label = CreateText(
                    "Position",
                    root,
                    GetPlayResolutionPositionLabel(position),
                    12,
                    FontStyle.Bold,
                    TextAnchor.MiddleCenter,
                    new Vector2(44f, 24f),
                    Vector2.zero,
                    PrimaryTextColor);
                label.transform.SetAsLastSibling();
                visuals[index] = new FielderVisual(position, root, badge.GetComponent<Image>());
            }
            return visuals;
        }

        private static void CreateRunnerVisual(
            RectTransform fieldView,
            int index,
            out RectTransform root,
            out Text label)
        {
            root = CreateRect("Runner_" + index, fieldView, new Vector2(46f, 46f), Vector2.zero);
            CreateMiniGameSpriteImage(
                "RunnerBadge",
                root,
                GetMiniGameSolidCircleSprite(),
                CareerUiTheme.Warning,
                new Vector2(32f, 32f),
                Vector2.zero);
            label = CreateText(
                "RunnerLabel",
                root,
                "주",
                12,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                new Vector2(38f, 22f),
                Vector2.zero,
                CareerUiTheme.PanelDark);
            label.transform.SetAsLastSibling();
        }

        private static void CreateFieldLine(RectTransform parent, Vector2 start, Vector2 end)
        {
            Vector2 delta = end - start;
            RectTransform line = CreateImage(
                "FoulLine",
                parent,
                WithAlpha(CareerUiTheme.TextSecondary, 0.48f),
                new Vector2(delta.magnitude, 2f),
                (start + end) * 0.5f);
            line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            line.GetComponent<Image>().raycastTarget = false;
        }

        private static Vector2 ToPlayResolutionFieldPosition(in NormalizedFieldPoint point)
        {
            return new Vector2(
                (float)(point.X - 0.5d) * 820f,
                (float)(point.Y - 0.5d) * 500f - 8f);
        }

        private static string GetPlayResolutionPositionLabel(PlayerPosition position)
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
                _ => "P"
            };
        }
    }
}
