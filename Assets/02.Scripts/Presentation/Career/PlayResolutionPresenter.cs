using System;
using Baseball.Core.Players;
using Baseball.Game.Career;
using Baseball.Simulation.Match;
using Baseball.Simulation.PlateAppearance;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Career
{
    /// <summary>Plate View가 투구와 스윙의 정규화 좌표를 화면 좌표로 변환하는 기준이다.</summary>
    internal static class PlayResolutionPlateLayout
    {
        internal const float ZoneScaleX = 134f;
        internal const float ZoneScaleY = 106f;
        internal const float ZoneCenterY = -36f;

        internal static Vector2 ToPosition(in PlatePoint point)
        {
            return new Vector2(
                (float)point.X * ZoneScaleX,
                ZoneCenterY + (float)point.Y * ZoneScaleY);
        }
    }

    /// <summary>공통 PlayResolutionSequence를 Plate View와 2D Field View에 투영한다.</summary>
    public sealed class PlayResolutionPresenter
    {
        private const float SwingStartAngleDegrees = 58f;
        private const float SwingEndAngleDegrees = -34f;
        private const float BatSweetSpotDistance = 145f;

        private static readonly Color FielderColor = new(0.16f, 0.42f, 0.58f, 1f);
        private static readonly Color ActiveFielderColor = new(0.22f, 0.84f, 0.62f, 1f);
        private static readonly Color RunnerColor = new(0.96f, 0.66f, 0.2f, 1f);
        private static readonly Color PrimaryTextColor = new(0.94f, 0.97f, 1f, 1f);
        private static readonly Color SecondaryTextColor = new(0.63f, 0.72f, 0.8f, 1f);
        private static readonly Color OutColor = new(0.92f, 0.35f, 0.31f, 1f);
        private static readonly Color SafeColor = new(0.27f, 0.78f, 0.49f, 1f);
        private static readonly Color ImpactColor = new(0.96f, 0.7f, 0.22f, 1f);

        private readonly RectTransform _root;
        private readonly RectTransform _plateView;
        private readonly RectTransform _fieldView;
        private readonly RectTransform _plateBall;
        private readonly RectTransform _plateBat;
        private readonly RectTransform _impactRing;
        private readonly RectTransform _fieldBall;
        private readonly RectTransform _throwLine;
        private readonly FielderVisual[] _fielders;
        private readonly RectTransform[] _runners;
        private readonly Text[] _runnerLabels;
        private readonly int[] _runnerIds;
        private readonly int[] _runnerInitialBases;
        private readonly Text _phaseText;
        private readonly Text _callText;
        private readonly Text _detailText;

        private string _fielderName = string.Empty;
        private bool _isLeftHandedBatter;

        public PlayResolutionPresenter(
            RectTransform root,
            RectTransform plateView,
            RectTransform fieldView,
            RectTransform plateBall,
            RectTransform plateBat,
            RectTransform impactRing,
            RectTransform fieldBall,
            RectTransform throwLine,
            FielderVisual[] fielders,
            RectTransform[] runners,
            Text[] runnerLabels,
            Text phaseText,
            Text callText,
            Text detailText)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _plateView = plateView ?? throw new ArgumentNullException(nameof(plateView));
            _fieldView = fieldView ?? throw new ArgumentNullException(nameof(fieldView));
            _plateBall = plateBall ?? throw new ArgumentNullException(nameof(plateBall));
            _plateBat = plateBat ?? throw new ArgumentNullException(nameof(plateBat));
            _impactRing = impactRing ?? throw new ArgumentNullException(nameof(impactRing));
            _fieldBall = fieldBall ?? throw new ArgumentNullException(nameof(fieldBall));
            _throwLine = throwLine ?? throw new ArgumentNullException(nameof(throwLine));
            _fielders = fielders ?? throw new ArgumentNullException(nameof(fielders));
            _runners = runners ?? throw new ArgumentNullException(nameof(runners));
            _runnerLabels = runnerLabels ?? throw new ArgumentNullException(nameof(runnerLabels));
            _phaseText = phaseText ?? throw new ArgumentNullException(nameof(phaseText));
            _callText = callText ?? throw new ArgumentNullException(nameof(callText));
            _detailText = detailText ?? throw new ArgumentNullException(nameof(detailText));
            if (_runners.Length != _runnerLabels.Length)
                throw new ArgumentException("주자 표시 요소 수가 일치하지 않습니다.");
            _runnerIds = new int[_runners.Length];
            _runnerInitialBases = new int[_runners.Length];
            Hide();
        }

        /// <summary>현재 타자의 타석 방향에 맞춰 배트 이미지와 스윙 궤적을 좌우 반전한다.</summary>
        public void SetBattingHand(Handedness battingHand)
        {
            _isLeftHandedBatter = battingHand == Handedness.Left;
            _plateBat.localScale = new Vector3(_isLeftHandedBatter ? 1f : -1f, 1f, 1f);
        }

        public void Begin(
            PlayResolutionSequence sequence,
            in CareerMatchPlaybackSnapshot snapshot,
            string fielderName)
        {
            if (sequence == null) throw new ArgumentNullException(nameof(sequence));
            _fielderName = fielderName ?? string.Empty;
            Array.Clear(_runnerIds, 0, _runnerIds.Length);
            Array.Clear(_runnerInitialBases, 0, _runnerInitialBases.Length);
            int slot = 0;
            slot = RegisterRunner(snapshot.FirstRunnerId, 1, slot);
            slot = RegisterRunner(snapshot.SecondRunnerId, 2, slot);
            slot = RegisterRunner(snapshot.ThirdRunnerId, 3, slot);
            RegisterRunner(sequence.BatterId, 0, slot);
            _root.gameObject.SetActive(true);
            Render(sequence, 0d);
        }

        public void Render(PlayResolutionSequence sequence, double elapsedSeconds)
        {
            if (sequence == null || !_root.gameObject.activeSelf)
                return;

            bool showField = sequence.IsBallInPlay && elapsedSeconds >= sequence.FieldTransitionSeconds;
            _plateView.gameObject.SetActive(!showField);
            _fieldView.gameObject.SetActive(showField);
            _phaseText.text = showField ? "FIELD VIEW · 인플레이" : "PLATE VIEW · 타석 승부";
            _phaseText.color = showField ? ActiveFielderColor : SecondaryTextColor;

            ResetPlateVisuals(sequence);
            ResetFieldVisuals(sequence);
            ApplyCues(sequence, elapsedSeconds, showField);
            UpdateCallText(sequence, elapsedSeconds, showField);
        }

        public void Hide()
        {
            _root.gameObject.SetActive(false);
        }

        private void ResetPlateVisuals(PlayResolutionSequence sequence)
        {
            PitchFlightDescriptor pitch = sequence.PitchPlay.Pitch;
            Vector2 pitchPosition = PlayResolutionPlateLayout.ToPosition(pitch.PlatePoint);
            _plateBall.gameObject.SetActive(true);
            _plateBall.anchoredPosition = pitchPosition;
            _plateBall.localScale = Vector3.one;
            _plateBat.gameObject.SetActive(false);
            _plateBat.anchoredPosition = GetBatHandlePosition(sequence.PitchPlay.Swing.BatPoint);
            _plateBat.localRotation = Quaternion.Euler(0f, 0f, ResolveSwingAngle(SwingStartAngleDegrees));
            _impactRing.gameObject.SetActive(false);
            _impactRing.anchoredPosition = pitchPosition;
        }

        private void ResetFieldVisuals(PlayResolutionSequence sequence)
        {
            _fieldBall.gameObject.SetActive(false);
            _throwLine.gameObject.SetActive(false);
            PlayerPosition activePosition = NormalizePitcherPosition(
                sequence.BallInPlay.Fielding.FielderPosition);
            for (int index = 0; index < _fielders.Length; index++)
            {
                FielderVisual visual = _fielders[index];
                visual.Root.anchoredPosition = ToFieldPosition(
                    PlayResolutionFieldLayout.GetFielderPoint(visual.Position));
                bool isActive = visual.Position == activePosition;
                visual.Background.color = isActive ? ActiveFielderColor : FielderColor;
                visual.Root.localScale = isActive ? Vector3.one * 1.12f : Vector3.one;
            }

            for (int index = 0; index < _runners.Length; index++)
            {
                int runnerId = _runnerIds[index];
                _runners[index].gameObject.SetActive(runnerId != 0);
                if (runnerId == 0)
                    continue;
                _runners[index].anchoredPosition = ToFieldPosition(
                    PlayResolutionFieldLayout.GetBasePoint(_runnerInitialBases[index]));
                _runners[index].localScale = Vector3.one;
                _runnerLabels[index].text = runnerId == sequence.BatterId ? "타" : "주";
            }
        }

        private void ApplyCues(
            PlayResolutionSequence sequence,
            double elapsedSeconds,
            bool showField)
        {
            PlayResolutionCue[] cues = sequence.Cues;
            for (int index = 0; index < cues.Length; index++)
            {
                PlayResolutionCue cue = cues[index];
                if (elapsedSeconds < cue.StartSeconds)
                    continue;
                float progress = EvaluateProgress(cue, elapsedSeconds);
                switch (cue.Type)
                {
                    case PlayResolutionCueType.BatterSwing:
                        if (!showField) ApplyBatterSwing(progress);
                        break;
                    case PlayResolutionCueType.Contact:
                        if (!showField) ApplyImpact(cue, elapsedSeconds, progress);
                        break;
                    case PlayResolutionCueType.FoulBall:
                        if (!showField) ApplyFoulBall(cue, progress);
                        break;
                    case PlayResolutionCueType.BattedBallFlight:
                        if (showField) ApplyBattedBall(sequence, cue, progress);
                        break;
                    case PlayResolutionCueType.FielderMove:
                        if (showField) ApplyFielderMove(cue, progress);
                        break;
                    case PlayResolutionCueType.Throw:
                        if (showField) ApplyThrow(cue, progress);
                        break;
                    case PlayResolutionCueType.Catch:
                    case PlayResolutionCueType.BallPickup:
                    case PlayResolutionCueType.FieldingError:
                        if (showField) HoldFieldBall(cue);
                        break;
                    case PlayResolutionCueType.RunnerMove:
                        if (showField) ApplyRunnerMove(cue, elapsedSeconds, progress);
                        break;
                }
            }
        }

        private void ApplyBatterSwing(float progress)
        {
            _plateBat.gameObject.SetActive(true);
            _plateBat.localRotation = Quaternion.Euler(
                0f,
                0f,
                ResolveSwingAngle(Mathf.Lerp(
                    SwingStartAngleDegrees,
                    SwingEndAngleDegrees,
                    Smooth(progress))));
        }

        private float ResolveSwingAngle(float rightHandedAngle)
        {
            return _isLeftHandedBatter ? -rightHandedAngle : rightHandedAngle;
        }

        private Vector2 GetBatHandlePosition(in PlatePoint batPoint)
        {
            Vector2 sweetSpot = PlayResolutionPlateLayout.ToPosition(batPoint);
            float radians = ResolveSwingAngle(SwingEndAngleDegrees) * Mathf.Deg2Rad;
            float horizontalDirection = _isLeftHandedBatter ? 1f : -1f;
            var sweetSpotOffsetFromHandle = horizontalDirection * new Vector2(
                BatSweetSpotDistance * Mathf.Cos(radians),
                BatSweetSpotDistance * Mathf.Sin(radians));
            return sweetSpot - sweetSpotOffsetFromHandle;
        }

        private void ApplyImpact(in PlayResolutionCue cue, double elapsedSeconds, float progress)
        {
            bool isActive = elapsedSeconds <= cue.EndSeconds;
            _impactRing.gameObject.SetActive(isActive);
            if (!isActive)
                return;
            float scale = Mathf.Lerp(0.5f, 1.8f, progress);
            _impactRing.localScale = new Vector3(scale, scale, 1f);
            Image image = _impactRing.GetComponent<Image>();
            image.color = new Color(ImpactColor.r, ImpactColor.g, ImpactColor.b, 1f - progress);
        }

        private void ApplyFoulBall(in PlayResolutionCue cue, float progress)
        {
            _plateBall.anchoredPosition = Vector2.Lerp(
                ToFieldPosition(cue.StartPoint),
                ToFieldPosition(cue.EndPoint),
                Smooth(progress));
            _plateBall.localScale = Vector3.one * Mathf.Lerp(1f, 0.55f, progress);
        }

        private void ApplyBattedBall(
            PlayResolutionSequence sequence,
            in PlayResolutionCue cue,
            float progress)
        {
            _fieldBall.gameObject.SetActive(progress < 1f || sequence.BallInPlay.BattedBall.IsHomeRun);
            Vector2 start = ToFieldPosition(cue.StartPoint);
            Vector2 end = ToFieldPosition(cue.EndPoint);
            Vector2 position = Vector2.Lerp(start, end, Smooth(progress));
            BattedBallType type = sequence.BallInPlay.BattedBall.Type;
            if (type is BattedBallType.FlyBall or BattedBallType.PopUp)
                position.y += Mathf.Sin(progress * Mathf.PI) * (type == BattedBallType.PopUp ? 105f : 68f);
            else if (type == BattedBallType.LineDrive)
                position.y += Mathf.Sin(progress * Mathf.PI) * 25f;
            else
                position.y += Mathf.Abs(Mathf.Sin(progress * Mathf.PI * 5f)) * 5f;
            _fieldBall.anchoredPosition = position;
            float scale = type is BattedBallType.FlyBall or BattedBallType.PopUp
                ? Mathf.Lerp(0.7f, 1.15f, Mathf.Sin(progress * Mathf.PI))
                : 0.82f;
            _fieldBall.localScale = new Vector3(scale, scale, 1f);
        }

        private void ApplyFielderMove(in PlayResolutionCue cue, float progress)
        {
            PlayerPosition position = NormalizePitcherPosition(cue.FielderPosition);
            for (int index = 0; index < _fielders.Length; index++)
            {
                if (_fielders[index].Position != position)
                    continue;
                _fielders[index].Root.anchoredPosition = Vector2.Lerp(
                    ToFieldPosition(cue.StartPoint),
                    ToFieldPosition(cue.EndPoint),
                    Smooth(progress));
                break;
            }
        }

        private void ApplyThrow(in PlayResolutionCue cue, float progress)
        {
            Vector2 start = ToFieldPosition(cue.StartPoint);
            Vector2 end = ToFieldPosition(cue.EndPoint);
            Vector2 current = Vector2.Lerp(start, end, Smooth(progress));
            _fieldBall.gameObject.SetActive(true);
            _fieldBall.anchoredPosition = current;
            _fieldBall.localScale = Vector3.one * 0.75f;
            _throwLine.gameObject.SetActive(progress < 1f);
            SetLine(_throwLine, start, current);
        }

        private void HoldFieldBall(in PlayResolutionCue cue)
        {
            _fieldBall.gameObject.SetActive(true);
            _fieldBall.anchoredPosition = ToFieldPosition(cue.EndPoint);
            _fieldBall.localScale = Vector3.one * 0.82f;
        }

        private void ApplyRunnerMove(in PlayResolutionCue cue, double elapsedSeconds, float progress)
        {
            int slot = FindRunnerSlot(cue.PlayerId);
            if (slot < 0)
                return;
            bool hasScored = cue.ToBase == 4 && elapsedSeconds > cue.EndSeconds + 0.08d;
            _runners[slot].gameObject.SetActive(!hasScored);
            if (hasScored)
                return;
            _runners[slot].anchoredPosition = Vector2.Lerp(
                ToFieldPosition(cue.StartPoint),
                ToFieldPosition(cue.EndPoint),
                Smooth(progress));
        }

        private void UpdateCallText(
            PlayResolutionSequence sequence,
            double elapsedSeconds,
            bool showField)
        {
            PlayResolutionCueType? call = null;
            for (int index = 0; index < sequence.Cues.Length; index++)
            {
                PlayResolutionCue cue = sequence.Cues[index];
                if (elapsedSeconds < cue.StartSeconds || elapsedSeconds > cue.EndSeconds)
                    continue;
                if (IsCallCue(cue.Type))
                    call = cue.Type;
            }

            if (call.HasValue)
            {
                _callText.text = GetCallLabel(call.Value, sequence);
                _callText.color = GetCallColor(call.Value);
            }
            else
            {
                _callText.text = showField ? GetBattedBallLabel(sequence) : GetContactLabel(sequence);
                _callText.color = showField ? PrimaryTextColor : ImpactColor;
            }

            _detailText.text = showField
                ? GetFieldDetail(sequence)
                : GetPlateDetail(sequence);
        }

        private int RegisterRunner(int playerId, int baseNumber, int slot)
        {
            if (playerId == 0 || slot >= _runnerIds.Length)
                return slot;
            for (int index = 0; index < slot; index++)
            {
                if (_runnerIds[index] == playerId && playerId > 0)
                    return slot;
            }
            _runnerIds[slot] = playerId;
            _runnerInitialBases[slot] = baseNumber;
            return slot + 1;
        }

        private int FindRunnerSlot(int playerId)
        {
            for (int index = 0; index < _runnerIds.Length; index++)
            {
                if (_runnerIds[index] == playerId)
                    return index;
            }
            return -1;
        }

        private static bool IsCallCue(PlayResolutionCueType type)
        {
            return type is PlayResolutionCueType.SwingAndMiss or
                PlayResolutionCueType.PlateCall or
                PlayResolutionCueType.FieldingError or
                PlayResolutionCueType.OutCall or
                PlayResolutionCueType.SafeCall or
                PlayResolutionCueType.ScoreCall or
                PlayResolutionCueType.HomeRunCall or
                PlayResolutionCueType.FinalResult or
                PlayResolutionCueType.ResultHold;
        }

        private static string GetCallLabel(PlayResolutionCueType type, PlayResolutionSequence sequence)
        {
            return type switch
            {
                PlayResolutionCueType.SwingAndMiss => "SWING & MISS",
                PlayResolutionCueType.PlateCall => GetPitchCallLabel(sequence.PitchPlay.Contact.PitchResult),
                PlayResolutionCueType.FieldingError => "ERROR",
                PlayResolutionCueType.OutCall => "OUT",
                PlayResolutionCueType.SafeCall => "SAFE",
                PlayResolutionCueType.ScoreCall => "SCORE",
                PlayResolutionCueType.HomeRunCall => "HOME RUN",
                PlayResolutionCueType.FinalResult or PlayResolutionCueType.ResultHold =>
                    GetFinalResultLabel(sequence),
                _ => string.Empty
            };
        }

        private static Color GetCallColor(PlayResolutionCueType type)
        {
            return type switch
            {
                PlayResolutionCueType.OutCall or PlayResolutionCueType.SwingAndMiss => OutColor,
                PlayResolutionCueType.SafeCall or PlayResolutionCueType.ScoreCall => SafeColor,
                PlayResolutionCueType.HomeRunCall => ImpactColor,
                PlayResolutionCueType.FieldingError => ImpactColor,
                _ => PrimaryTextColor
            };
        }

        private static string GetPitchCallLabel(PitchResult result)
        {
            return result switch
            {
                PitchResult.Ball => "BALL",
                PitchResult.CalledStrike => "STRIKE",
                PitchResult.SwingingStrike => "SWING & MISS",
                PitchResult.Foul => "FOUL",
                PitchResult.HitByPitch => "HIT BY PITCH",
                _ => "IN PLAY"
            };
        }

        private static string GetContactLabel(PlayResolutionSequence sequence)
        {
            ContactProfile contact = sequence.PitchPlay.Contact;
            if (!sequence.PitchPlay.Swing.DidSwing)
                return "TAKE";
            return contact.Grade switch
            {
                ContactGrade.Barrel => "PERFECT CONTACT",
                ContactGrade.Solid => contact.TimingFeedback == SwingTimingFeedback.Perfect
                    ? "PERFECT"
                    : "GOOD CONTACT",
                ContactGrade.Normal => "CONTACT",
                ContactGrade.Weak => "WEAK CONTACT",
                ContactGrade.FoulTip => "FOUL TIP",
                _ => string.Empty
            };
        }

        private static string GetBattedBallLabel(PlayResolutionSequence sequence)
        {
            if (!sequence.BallInPlay.HasValue)
                return string.Empty;
            return sequence.BallInPlay.BattedBall.Type switch
            {
                BattedBallType.GroundBall => "땅볼 타구",
                BattedBallType.LineDrive => "라인드라이브",
                BattedBallType.FlyBall => "뜬공",
                BattedBallType.PopUp => "높은 팝플라이",
                BattedBallType.Bunt => "번트 타구",
                _ => "인플레이"
            };
        }

        private static string GetFinalResultLabel(PlayResolutionSequence sequence)
        {
            if (sequence.FinalResult == PlateAppearanceResult.None)
                return GetPitchCallLabel(sequence.PitchPlay.Contact.PitchResult);
            if (sequence.OutsOnPlay >= 2)
                return "DOUBLE PLAY";
            return sequence.FinalResult switch
            {
                PlateAppearanceResult.Walk => "볼넷",
                PlateAppearanceResult.IntentionalWalk => "고의4구",
                PlateAppearanceResult.Strikeout => "삼진",
                PlateAppearanceResult.GroundOut => "땅볼 아웃",
                PlateAppearanceResult.FlyOut or PlateAppearanceResult.BuntPopOut => "플라이 아웃",
                PlateAppearanceResult.Single or PlateAppearanceResult.BuntSingle => "안타",
                PlateAppearanceResult.Double => "2루타",
                PlateAppearanceResult.Triple => "3루타",
                PlateAppearanceResult.HomeRun => "HOME RUN",
                PlateAppearanceResult.HitByPitch => "몸에 맞는 공",
                PlateAppearanceResult.ReachedOnError => "실책 출루",
                PlateAppearanceResult.FieldersChoice => "야수 선택",
                PlateAppearanceResult.SacrificeBunt => "희생 번트",
                _ => sequence.FinalResult.ToString()
            };
        }

        private string GetFieldDetail(PlayResolutionSequence sequence)
        {
            if (!sequence.BallInPlay.HasValue)
                return string.Empty;
            string zone = GetFieldZoneLabel(sequence.BallInPlay.BattedBall.FieldZone);
            if (sequence.BallInPlay.BattedBall.IsHomeRun)
                return $"{zone} 방향 · 외야 경계 밖으로 넘어갑니다";
            string fielder = string.IsNullOrEmpty(_fielderName)
                ? GetPositionLabel(sequence.BallInPlay.Fielding.FielderPosition)
                : $"{GetPositionLabel(sequence.BallInPlay.Fielding.FielderPosition)} {_fielderName}";
            return $"{zone} 방향 · {fielder} 수비 플레이";
        }

        private static string GetPlateDetail(PlayResolutionSequence sequence)
        {
            ContactProfile contact = sequence.PitchPlay.Contact;
            if (!sequence.PitchPlay.Swing.DidSwing)
                return "타자는 배트를 내지 않고 공을 끝까지 지켜봤습니다";
            return $"타이밍 {GetTimingLabel(contact.TimingFeedback)} · " +
                   $"배트 위치 {GetLocationLabel(contact.LocationFeedback)}";
        }

        private static string GetTimingLabel(SwingTimingFeedback feedback)
        {
            return feedback switch
            {
                SwingTimingFeedback.VeryEarly => "매우 빠름",
                SwingTimingFeedback.Early => "빠름",
                SwingTimingFeedback.Perfect => "정확",
                SwingTimingFeedback.Late => "늦음",
                SwingTimingFeedback.VeryLate => "매우 늦음",
                _ => "보통"
            };
        }

        private static string GetLocationLabel(SwingLocationFeedback feedback)
        {
            return feedback switch
            {
                SwingLocationFeedback.Center => "정확",
                SwingLocationFeedback.High => "높음",
                SwingLocationFeedback.Low => "낮음",
                SwingLocationFeedback.Inside => "몸쪽",
                SwingLocationFeedback.Outside => "바깥쪽",
                SwingLocationFeedback.Missed => "빗나감",
                _ => "보통"
            };
        }

        private static string GetFieldZoneLabel(FieldZone zone)
        {
            return zone switch
            {
                FieldZone.Pitcher => "투수 앞",
                FieldZone.Catcher => "포수 앞",
                FieldZone.FirstBase => "1루",
                FieldZone.SecondBase => "2루",
                FieldZone.ThirdBase => "3루",
                FieldZone.Shortstop => "유격수",
                FieldZone.LeftField => "좌익수",
                FieldZone.CenterField => "중견수",
                FieldZone.RightField => "우익수",
                FieldZone.LeftFieldLine => "좌익선",
                FieldZone.RightFieldLine => "우익선",
                _ => "중앙"
            };
        }

        private static string GetPositionLabel(PlayerPosition position)
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

        private static PlayerPosition NormalizePitcherPosition(PlayerPosition position)
        {
            return position == PlayerPosition.ReliefPitcher
                ? PlayerPosition.StartingPitcher
                : position;
        }

        private static float EvaluateProgress(in PlayResolutionCue cue, double elapsedSeconds)
        {
            if (cue.DurationSeconds <= 0d)
                return 1f;
            return Mathf.Clamp01((float)((elapsedSeconds - cue.StartSeconds) / cue.DurationSeconds));
        }

        private static float Smooth(float value)
        {
            return value * value * (3f - 2f * value);
        }

        private static Vector2 ToFieldPosition(in NormalizedFieldPoint point)
        {
            return new Vector2(
                (float)(point.X - 0.5d) * 820f,
                (float)(point.Y - 0.5d) * 500f - 8f);
        }

        private static void SetLine(RectTransform line, Vector2 start, Vector2 end)
        {
            Vector2 delta = end - start;
            line.sizeDelta = new Vector2(delta.magnitude, 3f);
            line.anchoredPosition = (start + end) * 0.5f;
            line.localRotation = Quaternion.Euler(
                0f,
                0f,
                Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }
    }

    /// <summary>고정 야수 아이콘과 포지션을 묶어 Presenter에 전달한다.</summary>
    public readonly struct FielderVisual
    {
        public FielderVisual(
            PlayerPosition position,
            RectTransform root,
            Image background)
        {
            Position = position;
            Root = root;
            Background = background;
        }

        public PlayerPosition Position { get; }
        public RectTransform Root { get; }
        public Image Background { get; }
    }
}
