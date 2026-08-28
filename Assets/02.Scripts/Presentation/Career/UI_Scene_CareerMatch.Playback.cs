using System.Collections.Generic;
using Baseball.Game.Career;
using Baseball.Simulation.Match;
using Baseball.Simulation.PlateAppearance;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Career
{
    public sealed partial class UI_Scene_CareerMatch
    {
        private void EnsurePlayback(CareerMatchSession session)
        {
            if (!ReferenceEquals(_playbackSession, session))
            {
                _playbackSession = session;
                _isPlaybackInitialized = false;
            }

            if (_isPlaybackInitialized || session.Phase == CareerMatchPhase.Preparation)
                return;

            _playback.Reset();
            ClearControlledResult();
            _isPlaybackInitialized = true;
            _nextAutomaticPlayAt = Time.unscaledTime + GetAutomaticPlayIntervalSeconds();
        }

        private void ResetPlayback()
        {
            _playback.Reset();
            _playbackSession = null;
            ClearControlledResult();
            _isPlaybackInitialized = false;
            _nextAutomaticPlayAt = 0f;
            SetPlaybackSpeedControlVisible(false);
        }

        private bool UpdateAutomaticPlayback(CareerMatchSession session)
        {
            if (!IsAutomaticPlaybackActive(session))
                return false;
            if (Time.unscaledTime < _nextAutomaticPlayAt)
                return true;

            if (_hasControlledResult)
            {
                ClearControlledResult();
                if (!_playback.HasPendingEvents(session.Events))
                {
                    Render();
                    return true;
                }
            }

            if (_playback.AdvanceAutomatic(session.Events, session.ControlledPlayerId))
            {
                _nextAutomaticPlayAt = Time.unscaledTime + GetAutomaticPlayIntervalSeconds();
                Render();
            }

            return true;
        }

        private bool IsAutomaticPlaybackActive(CareerMatchSession session)
        {
            return session != null &&
                   session.Phase != CareerMatchPhase.Preparation &&
                   session.Mode == CareerMatchMode.PlayerFocus &&
                   ReferenceEquals(_playbackSession, session) &&
                   _isPlaybackInitialized &&
                   (_hasControlledResult || _playback.HasPendingEvents(session.Events));
        }

        private bool IsDecisionInputReady(CareerMatchSession session)
        {
            return session != null &&
                   session.Phase == CareerMatchPhase.Playing &&
                   session.PendingDecision.HasValue &&
                   !_hasControlledResult &&
                   !_playback.HasPendingEvents(session.Events);
        }

        private void SubmitSelectedApproach()
        {
            CareerMatchSession session = _manager?.ActiveMatch;
            if (!IsDecisionInputReady(session))
                return;
            if (!_manager.SubmitBattingApproach(_selectedApproach))
                return;

            RevealControlledPlayAndRender();
        }

        private void AutoCompleteCurrentPlateAppearance()
        {
            CareerMatchSession session = _manager?.ActiveMatch;
            if (!IsDecisionInputReady(session))
                return;
            if (!_manager.AutoCompleteCurrentPlateAppearance())
                return;

            RevealControlledPlayAndRender();
        }

        private void AutoCompleteMatch()
        {
            CareerMatchSession session = _manager?.ActiveMatch;
            if (session == null || session.Phase == CareerMatchPhase.Preparation)
                return;
            if (session.Phase == CareerMatchPhase.Playing && !_manager.AutoCompleteActiveMatch())
                return;

            session = _manager.ActiveMatch;
            EnsurePlayback(session);
            _playback.RevealAll(session.Events);
            ClearControlledResult();
            Render();
        }

        private void RevealControlledPlayAndRender()
        {
            CareerMatchSession session = _manager.ActiveMatch;
            EnsurePlayback(session);
            int firstRevealedEventIndex = _playback.VisibleEventCount;
            _playback.RevealControlledPlay(session.Events, session.ControlledPlayerId);
            if (_playback.TryGetControlledPlateAppearanceSummary(
                    session.Events,
                    firstRevealedEventIndex,
                    session.ControlledPlayerId,
                    out CareerPlateAppearanceSummary summary))
            {
                _controlledResult = summary;
                _hasControlledResult = true;
                _nextAutomaticPlayAt = Time.unscaledTime + GetControlledResultHoldSeconds();
            }
            else
            {
                _nextAutomaticPlayAt = Time.unscaledTime + GetAutomaticPlayIntervalSeconds();
            }
            Render();
        }

        private void RenderAutomaticCommandPanel(
            RectTransform panel,
            CareerMatchSession session,
            CareerMatchPlaybackSnapshot snapshot)
        {
            string playerSide = GetPlayerSideLabel(session, snapshot.Half);
            Color sideColor = playerSide == "공격 중" ? GoldColor : AccentColor;
            bool canStopForPlayer = session.CanReceiveBattingDecisions;
            CreateStatusPill(
                panel,
                canStopForPlayer
                    ? $"{GetPlaybackSpeedLabel()} · 내 선수 출전 시 정지"
                    : $"{GetPlaybackSpeedLabel()} · 입력 대기 없음",
                new Vector2(410f, 46f),
                new Vector2(0f, 382f));
            CreateText(
                "Title", panel, playerSide, 30, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(410f, 46f), new Vector2(0f, 326f), sideColor);
            CreateText(
                "Guide", panel, "1회부터 모든 타자의 결과를 빠르게 따라갑니다.", 15,
                FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(410f, 30f), new Vector2(0f, 286f), SecondaryTextColor);

            string batterName = snapshot.BatterId == 0
                ? "다음 타자 준비"
                : FindPlayerName(session.Input, snapshot.BatterId);
            RenderPlaybackCard(panel, "현재 타자", batterName, 184f, PrimaryTextColor);
            RenderPlaybackCard(
                panel,
                "주자 상황",
                GetRunnerSituation(snapshot),
                76f,
                IsControlledPlayerOnBase(snapshot, session.ControlledPlayerId) ? RoleColor : PrimaryTextColor);
            RenderPlaybackCard(
                panel,
                "방금 전 플레이",
                GetPlaybackMomentLabel(snapshot),
                -32f,
                GoldColor);

            int completedPlateAppearances = CountCompletedPlateAppearances(
                session.Events,
                _playback.VisibleEventCount);
            CreateText(
                "ProgressLabel", panel, $"진행된 타석  {completedPlateAppearances}", 14,
                FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(410f, 28f), new Vector2(0f, -122f), SecondaryTextColor);
            RectTransform track = CreateImage(
                "ProgressTrack", panel, new Color(0.06f, 0.13f, 0.17f, 1f),
                new Vector2(410f, 8f), new Vector2(0f, -151f));
            float progress = session.Events.Count == 0
                ? 0f
                : (float)_playback.VisibleEventCount / session.Events.Count;
            RectTransform fill = CreateImage(
                "ProgressFill", track, sideColor,
                new Vector2(410f * progress, 8f), Vector2.zero);
            fill.anchorMin = new Vector2(0f, 0.5f);
            fill.anchorMax = new Vector2(0f, 0.5f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.anchoredPosition = Vector2.zero;

            CreateText(
                "StopGuide", panel,
                canStopForPlayer
                    ? "선발 또는 교체 출전한 내 선수의 타석에서 멈추고\n타격 접근 선택과 다음 투구 버튼이 열립니다."
                    : "현재 역할에는 경기 중 입력이 없습니다.\n원하면 아래 버튼으로 결과를 바로 확인할 수 있습니다.",
                17, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(410f, 78f), new Vector2(0f, -220f), PrimaryTextColor);
            Button finishMatch = CreateButton(
                "FinishMatch", panel, "경기 종료까지 진행",
                new Vector2(410f, 54f), new Vector2(0f, -302f),
                new Color(0.07f, 0.16f, 0.21f, 1f), PrimaryTextColor);
            finishMatch.onClick.AddListener(AutoCompleteMatch);
        }

        private void RenderControlledResultCommandPanel(
            RectTransform panel,
            CareerMatchSession session,
            CareerMatchPlaybackSnapshot snapshot)
        {
            string resultLabel = GetControlledResultLabel(_controlledResult);
            Color resultColor = GetControlledResultColor(_controlledResult);
            CreateStatusPill(
                panel,
                "내 타석 결과 확인",
                new Vector2(410f, 46f),
                new Vector2(0f, 382f));
            CreateText(
                "Eyebrow", panel, "MY AT-BAT RESULT", 12, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(410f, 26f), new Vector2(0f, 326f), AccentColor);
            CreateText(
                "Result", panel, resultLabel, 38, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(410f, 58f), new Vector2(0f, 278f), resultColor);
            CreateText(
                "Description", panel, GetControlledResultDescription(_controlledResult), 16,
                FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(410f, 40f), new Vector2(0f, 232f), SecondaryTextColor);

            RenderPlaybackCard(
                panel,
                "결과 반영",
                $"{snapshot.Outs}사 · {GetRunnerSituation(snapshot)}",
                150f,
                PrimaryTextColor);
            PlayerTodayLine today = CalculateTodayLine(
                session.Events,
                _playback.VisibleEventCount,
                session.ControlledPlayerId);
            RenderPlaybackCard(
                panel,
                "오늘 기록",
                $"{today.PlateAppearances}타석  {today.Hits}안타  {today.RunsBattedIn}타점",
                44f,
                resultColor);

            CreateText(
                "Approach", panel, $"마지막 선택 · {GetApproachLabel(_selectedApproach)}", 15,
                FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(410f, 30f), new Vector2(0f, -40f), AccentColor);
            CreateText(
                "ContinueGuide", panel,
                $"결과를 충분히 확인한 뒤 {GetPlaybackSpeedLabel()}으로 자동 진행합니다.",
                15, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(410f, 46f), new Vector2(0f, -92f), SecondaryTextColor);
            Button finishMatch = CreateButton(
                "FinishMatch", panel, "경기 종료까지 진행",
                new Vector2(410f, 54f), new Vector2(0f, -166f),
                new Color(0.07f, 0.16f, 0.21f, 1f), PrimaryTextColor);
            finishMatch.onClick.AddListener(AutoCompleteMatch);
        }

        /// <summary>
        /// 진행 속도 슬라이더를 한 번만 만들어 Render()가 지우지 않는 오버레이에 붙인다.
        /// </summary>
        private void CreatePlaybackSpeedControl(RectTransform parent, Vector2 position)
        {
            _playbackSpeedControl = CreateRect(
                "PlaybackSpeedControl", parent, new Vector2(410f, 68f), position);
            _playbackSpeedValueLabel = CreateText(
                "Value", _playbackSpeedControl, GetPlaybackSpeedCaption(), 13,
                FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(410f, 24f), new Vector2(0f, 22f), SecondaryTextColor);

            RectTransform track = CreateImage(
                "Track", _playbackSpeedControl, new Color(0.06f, 0.13f, 0.17f, 1f),
                new Vector2(410f, 12f), new Vector2(0f, -13f));
            RectTransform fill = CreateImage("Fill", track, AccentColor, Vector2.zero, Vector2.zero);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;

            // 손잡이가 양 끝에서 트랙 밖으로 튀어나오지 않도록 반지름만큼 안쪽으로 좁힌 영역에서 움직인다.
            RectTransform handleArea = CreateRect("HandleArea", track, Vector2.zero, Vector2.zero);
            handleArea.anchorMin = Vector2.zero;
            handleArea.anchorMax = Vector2.one;
            handleArea.offsetMin = new Vector2(13f, 0f);
            handleArea.offsetMax = new Vector2(-13f, 0f);
            RectTransform handle = CreateImage(
                "Handle", handleArea, PrimaryTextColor, new Vector2(26f, 30f), Vector2.zero);

            var slider = track.gameObject.AddComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.wholeNumbers = true;
            slider.minValue = 0f;
            slider.maxValue = PlaybackSpeedRates.Length - 1;
            slider.SetValueWithoutNotify(_playbackSpeedStepIndex);
            slider.onValueChanged.AddListener(HandlePlaybackSpeedSliderChanged);
            _playbackSpeedSlider = slider;

            CreateText(
                "MinLabel", _playbackSpeedControl, FormatPlaybackSpeedRate(PlaybackSpeedRates[0]), 11,
                FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(80f, 20f), new Vector2(-165f, -34f), MutedTextColor);
            CreateText(
                "MaxLabel", _playbackSpeedControl,
                FormatPlaybackSpeedRate(PlaybackSpeedRates[PlaybackSpeedRates.Length - 1]), 11,
                FontStyle.Normal, TextAnchor.MiddleRight,
                new Vector2(80f, 20f), new Vector2(165f, -34f), MutedTextColor);
        }

        private void SetPlaybackSpeedControlVisible(bool isVisible)
        {
            if (_playbackSpeedControl != null && _playbackSpeedControl.gameObject.activeSelf != isVisible)
                _playbackSpeedControl.gameObject.SetActive(isVisible);
        }

        private bool IsPlaybackViewVisible(CareerMatchSession session)
        {
            return session != null &&
                   session.Mode == CareerMatchMode.PlayerFocus &&
                   (session.Phase == CareerMatchPhase.Playing || IsAutomaticPlaybackActive(session));
        }

        private void HandlePlaybackSpeedSliderChanged(float value)
        {
            int stepIndex = Mathf.Clamp(Mathf.RoundToInt(value), 0, PlaybackSpeedRates.Length - 1);
            if (stepIndex == _playbackSpeedStepIndex)
                return;

            _playbackSpeedStepIndex = stepIndex;
            if (_playbackSpeedValueLabel != null)
                _playbackSpeedValueLabel.text = GetPlaybackSpeedCaption();

            // 남은 대기 시간이 이전 배속으로 잡혀 있으므로 새 배속 기준으로 다시 잡는다.
            _nextAutomaticPlayAt = Time.unscaledTime + (_hasControlledResult
                ? GetControlledResultHoldSeconds()
                : GetAutomaticPlayIntervalSeconds());
            Render();
        }

        private float GetAutomaticPlayIntervalSeconds()
        {
            return automaticPlayIntervalSeconds / GetPlaybackSpeedRate();
        }

        private float GetControlledResultHoldSeconds()
        {
            return Mathf.Max(
                minimumControlledResultHoldSeconds,
                controlledResultHoldSeconds / GetPlaybackSpeedRate());
        }

        private float GetPlaybackSpeedRate()
        {
            return PlaybackSpeedRates[
                Mathf.Clamp(_playbackSpeedStepIndex, 0, PlaybackSpeedRates.Length - 1)];
        }

        private void ClearControlledResult()
        {
            _controlledResult = default;
            _hasControlledResult = false;
        }

        private static void RenderPlaybackCard(
            RectTransform parent,
            string label,
            string value,
            float y,
            Color valueColor)
        {
            RectTransform card = CreateImage(
                "Playback_" + label,
                parent,
                PanelDarkColor,
                new Vector2(410f, 92f),
                new Vector2(0f, y));
            CreateText(
                "Label", card, label, 13, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(150f, 26f), new Vector2(-105f, 23f), SecondaryTextColor);
            CreateText(
                "Value", card, value, 21, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(360f, 36f), new Vector2(0f, -12f), valueColor);
        }

        private string GetPlayerSideLabel(CareerMatchSession session, InningHalf half)
        {
            int playerTeamId = _manager.CurrentCareer.MyPlayer.CurrentTeamId;
            bool isPlayerTeamBatting = half == InningHalf.Top
                ? session.Input.AwayTeam.TeamId == playerTeamId
                : session.Input.HomeTeam.TeamId == playerTeamId;
            return isPlayerTeamBatting ? "공격 중" : "수비 중";
        }

        private static bool IsControlledPlayerOnBase(
            CareerMatchPlaybackSnapshot snapshot,
            int controlledPlayerId)
        {
            return snapshot.FirstRunnerId == controlledPlayerId ||
                   snapshot.SecondRunnerId == controlledPlayerId ||
                   snapshot.ThirdRunnerId == controlledPlayerId;
        }

        private static string GetPlaybackMomentLabel(CareerMatchPlaybackSnapshot snapshot)
        {
            if (snapshot.LatestEventType == MatchEventType.HalfInningEnded)
                return "공수 교대";
            if (snapshot.LatestEventType == MatchEventType.MatchEnded)
                return "경기 종료";
            if (snapshot.LatestEventType == MatchEventType.Score)
                return "득점";
            if (snapshot.LatestEventType == MatchEventType.RunnerAdvance)
                return "주자 진루";
            if (snapshot.LatestEventType == MatchEventType.PlateAppearanceEnded &&
                snapshot.LatestPlateAppearanceResult != PlateAppearanceResult.None)
                return GetPlateAppearanceResultLabel(snapshot.LatestPlateAppearanceResult);
            return $"Count {snapshot.Balls}-{snapshot.Strikes}";
        }

        private static bool IsVisibleLogEvent(MatchEvent matchEvent, int controlledPlayerId)
        {
            return matchEvent.EventType switch
            {
                MatchEventType.Pitch => matchEvent.BatterId == controlledPlayerId,
                MatchEventType.RunnerAdvance => matchEvent.ToBase is >= 1 and <= 3,
                MatchEventType.Score => true,
                MatchEventType.PlateAppearanceEnded => true,
                MatchEventType.PlayerSubstitution => true,
                MatchEventType.HalfInningEnded => true,
                _ => false
            };
        }

        private static int CountCompletedPlateAppearances(
            IReadOnlyList<MatchEvent> events,
            int visibleEventCount)
        {
            int count = 0;
            for (int index = 0; index < visibleEventCount; index++)
            {
                if (events[index].EventType == MatchEventType.PlateAppearanceEnded)
                    count++;
            }
            return count;
        }

        private static string GetBaseLabel(int baseNumber)
        {
            return baseNumber switch
            {
                0 => "타석",
                1 => "1루",
                2 => "2루",
                3 => "3루",
                4 => "홈",
                _ => string.Empty
            };
        }

        private string GetPlaybackSpeedLabel()
        {
            return FormatPlaybackSpeedRate(GetPlaybackSpeedRate());
        }

        private string GetPlaybackSpeedCaption()
        {
            return $"경기 진행 속도 · {GetPlaybackSpeedLabel()}";
        }

        private static string FormatPlaybackSpeedRate(float rate)
        {
            return $"{rate:0.#}배속";
        }

        private static string GetControlledResultLabel(CareerPlateAppearanceSummary summary)
        {
            if (summary.IsDoublePlay)
                return "병살타";
            if (summary.IsSacrificeFly)
                return "희생플라이";
            return GetPlateAppearanceResultLabel(summary.Result);
        }

        private static string GetControlledResultDescription(CareerPlateAppearanceSummary summary)
        {
            string description = summary.Result switch
            {
                PlateAppearanceResult.Walk => "볼넷으로 출루했습니다.",
                PlateAppearanceResult.HitByPitch => "몸에 맞는 공으로 출루했습니다.",
                PlateAppearanceResult.Strikeout => "삼진으로 타석이 끝났습니다.",
                PlateAppearanceResult.GroundOut when summary.IsDoublePlay =>
                    "주자와 타자가 함께 아웃되었습니다.",
                PlateAppearanceResult.GroundOut => "땅볼로 아웃되었습니다.",
                PlateAppearanceResult.FlyOut when summary.IsSacrificeFly =>
                    "뜬공으로 주자를 홈에 불러들였습니다.",
                PlateAppearanceResult.FlyOut => "뜬공으로 아웃되었습니다.",
                PlateAppearanceResult.Single => "안타로 1루에 출루했습니다.",
                PlateAppearanceResult.Double => "2루타로 득점 기회를 만들었습니다.",
                PlateAppearanceResult.Triple => "3루타로 단숨에 득점권에 도달했습니다.",
                PlateAppearanceResult.HomeRun => "홈런으로 모든 주자를 불러들였습니다.",
                _ => "타석 결과가 확정되었습니다."
            };
            return summary.RunsBattedIn > 0
                ? $"{description}  {summary.RunsBattedIn}타점"
                : description;
        }

        private static Color GetControlledResultColor(CareerPlateAppearanceSummary summary)
        {
            return summary.Result switch
            {
                PlateAppearanceResult.Single or PlateAppearanceResult.Double or
                    PlateAppearanceResult.Triple => RoleColor,
                PlateAppearanceResult.HomeRun => GoldColor,
                PlateAppearanceResult.Walk or PlateAppearanceResult.HitByPitch => AccentColor,
                _ => DangerColor
            };
        }

        private static int CountOutsInPlateAppearance(
            IReadOnlyList<MatchEvent> events,
            int plateAppearanceEndIndex)
        {
            int outs = 0;
            for (int index = plateAppearanceEndIndex - 1; index >= 0; index--)
            {
                MatchEvent matchEvent = events[index];
                if (matchEvent.EventType is MatchEventType.PlateAppearanceEnded or
                    MatchEventType.HalfInningEnded)
                {
                    break;
                }
                if (matchEvent.EventType == MatchEventType.Out)
                    outs++;
            }
            return outs;
        }
    }
}
