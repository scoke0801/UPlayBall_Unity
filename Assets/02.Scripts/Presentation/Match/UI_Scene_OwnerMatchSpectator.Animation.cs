using System;
using System.Text;
using Baseball.Core.Players;
using Baseball.Presentation.Career;
using Baseball.Simulation.Match;
using Baseball.Simulation.PlateAppearance;
using UnityEngine;

namespace Baseball.Presentation.Match
{
    public sealed partial class UI_Scene_OwnerMatchSpectator
    {
        private int _playbackBoundary = -1;
        private bool _hasPendingEvent;
        private MatchEvent _pendingEvent;
        private int _pendingEventCount;
        private float _eventElapsed, _eventDuration;
        private readonly MatchEvent[] _recentPitches = new MatchEvent[12];
        private readonly OwnerMatchRunnerRoute[] _upcomingRunnerRoutes = new OwnerMatchRunnerRoute[3];
        private readonly StringBuilder _historyText = new StringBuilder(256);

        private void ResetGameCast()
        {
            _playbackBoundary = -1;
            _hasPendingEvent = false;
            _eventElapsed = 0f;
            _playVisualizer.Reset();
            foreach (var dot in _pitchDots) dot.gameObject.SetActive(false);
            _zoneBall.gameObject.SetActive(false);
            _pitchHistory.text = "첫 투구를 기다립니다.";
            _decisionNote.text = "경기 중 기용과 운영은 감독 AI가 결정합니다.";
            _playDetail.text = "타구와 주자의 움직임을 함께 확인하세요.";
            _currentPitch.text = "투구 기록";
        }

        private void UpdateGameCast(float deltaSeconds)
        {
            if (_session.State.IsPaused || _session.State.IsComplete) return;
            float remaining = deltaSeconds * (int)_session.State.Speed;
            // 배속이 높아도 영시간 사건은 같은 프레임에 순서대로 소비한다.
            for (int iteration = 0; iteration < 64 && !_session.State.IsComplete; iteration++)
            {
                if (!_hasPendingEvent)
                {
                    if (_session.State.VisibleEventCount > _playbackBoundary)
                    {
                        if (!_session.TryPreparePlayback(out _playbackBoundary)) return;
                        RefreshControls();
                    }
                    OwnerMatchPlaybackGroup group = _session.PeekPlaybackGroup();
                    _pendingEvent = group.VisualEvent;
                    _pendingEventCount = group.EventCount;
                    _eventDuration = _gameCastConfig.GetDuration(_pendingEvent);
                    _eventElapsed = 0f;
                    _hasPendingEvent = true;
                    if (_pendingEvent.EventType == MatchEventType.Contact)
                    {
                        int count = _session.CopyUpcomingRunnerRoutes(_upcomingRunnerRoutes);
                        _playVisualizer.PrepareRunnerRoutes(_upcomingRunnerRoutes, count);
                    }
                    _playVisualizer.Begin(_pendingEvent, _session.PeekBallInPlay());
                    _eventDuration = _playVisualizer.GetDuration(_pendingEvent, _eventDuration);
                    if (_pendingEvent.EventType == MatchEventType.Pitch)
                    {
                        _scorePanel.gameObject.SetActive(false);
                        _pitcherLabel.text = _session.GetParticipantName(_pendingEvent.PitcherId);
                        _batterLabel.text = _session.GetParticipantName(_pendingEvent.BatterId);
                        _announcement.text = "";
                    }
                }

                float consumed = Mathf.Min(remaining, Mathf.Max(0f, _eventDuration - _eventElapsed));
                _eventElapsed += consumed;
                remaining -= consumed;
                float progress = _eventDuration > 0 ? _eventElapsed / _eventDuration : 1f;
                _playVisualizer.Render(progress);
                RenderPitchInFlight(progress);
                if (_eventElapsed < _eventDuration) return;

                _zoneBall.gameObject.SetActive(false);
                _hasPendingEvent = false;
                if (!_session.TryRevealPlaybackGroup(_pendingEventCount)) return;
                RefreshControls();
                if (remaining <= 0f && _eventDuration > 0f) return;
            }
        }

        private void RenderPitchInFlight(float progress)
        {
            if (_pendingEvent.EventType != MatchEventType.Pitch || !_pendingEvent.PitchPlayData.HasValue) return;
            var pitch = _pendingEvent.PitchPlayData.Pitch;
            PitchTrajectoryPoint point = PitchTrajectoryEvaluator.Evaluate(pitch, progress);
            _zoneBall.gameObject.SetActive(true);
            float radius = _zoneBall.sizeDelta.x * 0.5f;
            _zoneBall.anchoredPosition = new Vector2(100 + (float)point.X * 60 - radius,
                -(90 - (float)point.Y * 60 - radius));
        }

        private void RenderGameCastHistory()
        {
            if (_session == null) return;
            int visible = _session.State.VisibleEventCount;
            int pitcherId = CurrentModel.Pitcher.PlayerId;
            int batterId = CurrentModel.Batter.PlayerId;
            int pitcherPitches = 0, atBats = 0, hits = 0, pitchCount = 0;
            for (int index = 0; index < visible; index++)
            {
                MatchEvent value = _session.GetVisibleEvent(index);
                if (value.EventType == MatchEventType.Pitch && value.PitcherId == pitcherId) pitcherPitches++;
                // 진행 중인 안타만 먼저 집계해 0타석 1안타가 표시되는 불일치를 막는다.
                if (value.EventType == MatchEventType.PlateAppearanceEnded && value.BatterId == batterId)
                {
                    atBats++;
                    if (value.PlateAppearanceResult is PlateAppearanceResult.Single or PlateAppearanceResult.Double or
                        PlateAppearanceResult.Triple or PlateAppearanceResult.HomeRun or PlateAppearanceResult.BuntSingle)
                        hits++;
                }
                if (value.EventType == MatchEventType.Pitch && value.BatterId == batterId)
                {
                    if (index > 0)
                    {
                        // 같은 타자의 다음 타석은 이전 타석의 존 표시를 이어 쓰지 않는다.
                        MatchEvent previous = _session.GetVisibleEvent(index - 1);
                        if (previous.EventType is MatchEventType.BattingApproachSelected)
                            pitchCount = 0;
                    }
                    _recentPitches[pitchCount % _recentPitches.Length] = value;
                    pitchCount++;
                }
                if (value.EventType == MatchEventType.PlateAppearanceEnded && index < visible - 1) pitchCount = 0;
                if (value.ReasonCode != DecisionReasonCode.None && IsDecisionEvent(value.EventType))
                    _decisionNote.text = _session.GetParticipantName(value.PlayerId) + " · " +
                        DescribeDecision(value.EventType) + "\n" + DescribeReason(value.ReasonCode);
            }
            _pitcherDetail.text = CurrentModel.IsBetweenInnings ? "다음 수비 준비" : "오늘 " + pitcherPitches + "구";
            _batterDetail.text = CurrentModel.IsBetweenInnings ? "다음 공격 준비" : "오늘 " + atBats + "타석 " + hits + "안타";
            if (pitcherId > 0 && batterId > 0)
            {
                OwnerMatchHandedness hand = _session.GetHandedness(pitcherId, batterId);
                _pitcherRole.text = "마운드 · " + (hand.IsPitcherLeftHanded ? "좌투" : "우투");
                _batterRole.text = "타석 · " + (hand.BattingHand == Handedness.Left ? "좌타" : "우타");
            }
            _historyText.Clear();
            if (pitchCount == 0) _currentPitch.text = "투구 기록";
            foreach (var dot in _pitchDots) dot.gameObject.SetActive(false);
            int first = Math.Max(0, pitchCount - _recentPitches.Length);
            for (int index = first; index < pitchCount; index++)
            {
                MatchEvent value = _recentPitches[index % _recentPitches.Length];
                int slot = index - first;
                if (value.PitchPlayData.HasValue)
                {
                    var point = value.PitchPlayData.Pitch.PlatePoint;
                    _pitchDots[slot].gameObject.SetActive(true);
                    _pitchDots[slot].color = value.PitchResult == PitchResult.Ball
                        ? new Color32(52, 135, 94, 255) : Blue;
                    _pitchDots[slot].rectTransform.anchoredPosition =
                        new Vector2(Mathf.Clamp(100 + (float)point.X * 60, 12, 188) - 10.5f,
                            -(Mathf.Clamp(90 - (float)point.Y * 60, 12, 168) - 10.5f));
                    _pitchNumbers[slot].text = (index + 1).ToString();
                    _currentPitch.text = FormatPitchDescription(value);
                }
                if (index < pitchCount - 6) continue;
                _historyText.Append(index + 1).Append("  ").Append(FormatEventResult(value))
                    .Append(" · ").Append(FormatPitchDescription(value)).Append('\n');
            }
            _pitchHistory.text = pitchCount == 0 ? "첫 투구를 기다립니다." : _historyText.ToString();
            var score = _session.CreateVisibleLineScore();
            _miniLineScore.text = "이닝별 득점  " + BuildInningSummary(score, true) + "\n" + BuildInningSummary(score, false);
        }

        private string BuildInningSummary(Baseball.Game.Career.MatchLineScore score, bool away)
        {
            var text = new StringBuilder(96);
            text.Append(away ? CurrentModel.AwayTeam.Name : CurrentModel.HomeTeam.Name).Append("   ");
            for (int inning = 1; inning <= score.InningCount; inning++)
            {
                int value = away ? score.GetAwayRuns(inning) : score.GetHomeRuns(inning);
                text.Append(value < 0 ? "–" : value.ToString()).Append("  ");
            }
            return text.ToString();
        }

        private static bool IsDecisionEvent(MatchEventType type) =>
            type is MatchEventType.PitcherEntered or MatchEventType.PinchHitterEntered or
                MatchEventType.PinchRunnerEntered or MatchEventType.DefensiveReplacement or
                MatchEventType.DefensiveAlignmentChanged or MatchEventType.IntentionalWalk or
                MatchEventType.BuntAttempted or MatchEventType.StealAttempted;

        private static string DescribeDecision(MatchEventType type) => type switch
        {
            MatchEventType.PitcherEntered => "투수 교체",
            MatchEventType.PinchHitterEntered => "대타 기용",
            MatchEventType.PinchRunnerEntered => "대주자 기용",
            MatchEventType.DefensiveReplacement => "수비 교체",
            MatchEventType.DefensiveAlignmentChanged => "수비 위치 조정",
            MatchEventType.IntentionalWalk => "고의4구",
            MatchEventType.BuntAttempted => "번트 시도",
            MatchEventType.StealAttempted => "도루 시도",
            _ => "감독 판단"
        };

        private static string DescribeReason(DecisionReasonCode reason) => reason switch
        {
            DecisionReasonCode.Fatigue => "피로 누적",
            DecisionReasonCode.PitchLimit => "투구 수 관리",
            DecisionReasonCode.TimesThroughOrder => "타순 반복 대면",
            DecisionReasonCode.Performance => "오늘의 투구 내용",
            DecisionReasonCode.HighLeverage => "승부처 대응",
            DecisionReasonCode.Matchup => "상대와의 대결 고려",
            DecisionReasonCode.Injury => "부상 대응",
            DecisionReasonCode.ScheduledUsage => "예정된 기용",
            DecisionReasonCode.DefensiveStrategy => "수비 전략",
            DecisionReasonCode.Emergency => "긴급 대응",
            DecisionReasonCode.ExpectedValue => "기대 득실점 고려",
            DecisionReasonCode.PlayerPolicy => "구단 방침 반영",
            DecisionReasonCode.ManagerProfile => "감독 운영 성향",
            _ => "경기 상황에 따른 판단"
        };

        private void RenderPlayDetail(in MatchEvent value)
        {
            if (!value.BallInPlayData.HasValue) return;
            var play = value.BallInPlayData;
            string kind = play.BattedBall.Type switch
            {
                BattedBallType.GroundBall => "땅볼",
                BattedBallType.LineDrive => "직선 타구",
                BattedBallType.FlyBall => "뜬공",
                BattedBallType.PopUp => "높은 뜬공",
                _ => "번트"
            };
            string defense = play.Fielding.HasValue
                ? " · " + _session.GetParticipantName(play.Fielding.FielderId) : "";
            _playDetail.text = kind + defense + "\n타구 위치는 수비 구역 기준으로 표시합니다.";
        }
    }
}
