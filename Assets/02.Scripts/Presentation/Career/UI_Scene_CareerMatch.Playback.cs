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
            _isPlaybackInitialized = true;
            _nextAutomaticPlayAt = Time.unscaledTime + automaticPlayIntervalSeconds;
        }

        private void ResetPlayback()
        {
            _playback.Reset();
            _playbackSession = null;
            _isPlaybackInitialized = false;
            _nextAutomaticPlayAt = 0f;
        }

        private bool UpdateAutomaticPlayback(CareerMatchSession session)
        {
            if (!IsAutomaticPlaybackActive(session))
                return false;
            if (Time.unscaledTime < _nextAutomaticPlayAt)
                return true;

            if (_playback.AdvanceAutomatic(session.Events, session.ControlledPlayerId))
            {
                _nextAutomaticPlayAt = Time.unscaledTime + automaticPlayIntervalSeconds;
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
                   _playback.HasPendingEvents(session.Events);
        }

        private bool IsDecisionInputReady(CareerMatchSession session)
        {
            return session != null &&
                   session.Phase == CareerMatchPhase.Playing &&
                   session.PendingDecision.HasValue &&
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
            if (!IsDecisionInputReady(session))
                return;
            if (!_manager.AutoCompleteActiveMatch())
                return;

            session = _manager.ActiveMatch;
            EnsurePlayback(session);
            _playback.RevealAll(session.Events);
            Render();
        }

        private void RevealControlledPlayAndRender()
        {
            CareerMatchSession session = _manager.ActiveMatch;
            EnsurePlayback(session);
            _playback.RevealControlledPlay(session.Events, session.ControlledPlayerId);
            _nextAutomaticPlayAt = Time.unscaledTime + automaticPlayIntervalSeconds;
            Render();
        }

        private void RenderAutomaticCommandPanel(
            RectTransform panel,
            CareerMatchSession session,
            CareerMatchPlaybackSnapshot snapshot)
        {
            string playerSide = GetPlayerSideLabel(session, snapshot.Half);
            Color sideColor = playerSide == "공격 중" ? GoldColor : AccentColor;
            CreateStatusPill(
                panel,
                "자동 진행 · 내 타석에서 정지",
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
                "내 타석이 오면 자동 진행이 멈추고\n타격 접근 선택과 다음 투구 버튼이 열립니다.",
                17, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(410f, 78f), new Vector2(0f, -220f), PrimaryTextColor);
            CreateText(
                "RunnerGuide", panel,
                "안타와 아웃뿐 아니라 주자의 진루와 득점도\n왼쪽 구장과 실시간 로그에 함께 표시됩니다.",
                14, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(410f, 70f), new Vector2(0f, -302f), SecondaryTextColor);
            CreateText(
                "Speed", panel, $"자동 중계 간격  {automaticPlayIntervalSeconds:0.00}초 / 타석", 12,
                FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(410f, 25f), new Vector2(0f, -370f), MutedTextColor);
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
            if (snapshot.LatestPlateAppearanceResult != PlateAppearanceResult.None)
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
    }
}
