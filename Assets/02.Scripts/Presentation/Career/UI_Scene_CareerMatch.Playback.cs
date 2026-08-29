using System.Collections.Generic;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Career;
using Baseball.Simulation.Match;
using Baseball.Simulation.PlateAppearance;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Career
{
    public sealed partial class UI_Scene_CareerMatch
    {
        private const int MaximumStepAdvances = 64;

        private Text _stopConditionText;
        private string _controlSignature;

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
            _isPaused = false;
            _isCallUpAcknowledged = false;
            _nextAutomaticPlayAt = Time.unscaledTime + GetAutomaticPlayIntervalSeconds();
        }

        private void ResetPlayback()
        {
            _playback.Reset();
            _playbackSession = null;
            ClearControlledResult();
            _isPlaybackInitialized = false;
            _isPaused = false;
            _isCallUpAcknowledged = false;
            _nextAutomaticPlayAt = 0f;
        }

        private bool UpdateAutomaticPlayback(CareerMatchSession session)
        {
            if (!IsAutomaticPlaybackActive(session))
                return false;
            if (_isPaused || Time.unscaledTime < _nextAutomaticPlayAt)
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

            if (AdvanceOneStep(session))
                Render();

            return true;
        }

        /// <summary>
        /// 다음 타석 결과까지 공개하고, 공수 교대는 조금 더 오래 머무르게 대기 시간을 잡는다.
        /// </summary>
        private bool AdvanceOneStep(CareerMatchSession session)
        {
            int firstRevealedEventIndex = _playback.VisibleEventCount;
            bool pauseBeforeControlledPlayer = session.Mode == CareerMatchMode.InterveneOnPlayer;
            if (!_playback.AdvanceAutomatic(
                    session.Events,
                    session.ControlledPlayerId,
                    pauseBeforeControlledPlayer))
            {
                return false;
            }

            if (!pauseBeforeControlledPlayer &&
                _playback.TryGetControlledPlateAppearanceSummary(
                    session.Events,
                    firstRevealedEventIndex,
                    session.ControlledPlayerId,
                    out CareerPlateAppearanceSummary summary))
            {
                _controlledResult = summary;
                _hasControlledResult = true;
            }

            bool isControlledPlayerStep = _hasControlledResult ||
                                          ContainsControlledPitcherEvent(
                                              session.Events,
                                              firstRevealedEventIndex,
                                              _playback.VisibleEventCount,
                                              session.ControlledPlayerId);
            _isControlledPlayerPlaybackStep = isControlledPlayerStep;

            _isCallUpAcknowledged = false;
            _nextAutomaticPlayAt = Time.unscaledTime + (isControlledPlayerStep
                ? GetControlledResultHoldSeconds()
                : IsLatestVisibleEventHalfInningEnd(session)
                    ? GetSideChangeHoldSeconds()
                    : GetAutomaticPlayIntervalSeconds());
            return true;
        }

        private bool IsLatestVisibleEventHalfInningEnd(CareerMatchSession session)
        {
            int visibleEventCount = _playback.VisibleEventCount;
            return visibleEventCount > 0 &&
                   session.Events[visibleEventCount - 1].EventType == MatchEventType.HalfInningEnded;
        }

        private bool IsAutomaticPlaybackActive(CareerMatchSession session)
        {
            return session != null &&
                   session.Phase != CareerMatchPhase.Preparation &&
                   session.Mode != CareerMatchMode.ResultsOnly &&
                   ReferenceEquals(_playbackSession, session) &&
                   _isPlaybackInitialized &&
                   (_hasControlledResult || _playback.HasPendingEvents(session.Events));
        }

        private bool IsDecisionInputReady(CareerMatchSession session)
        {
            return session != null &&
                   session.Mode == CareerMatchMode.InterveneOnPlayer &&
                   session.Phase == CareerMatchPhase.Playing &&
                   session.PendingDecision.HasValue &&
                   !_hasControlledResult &&
                   !_playback.HasPendingEvents(session.Events);
        }

        private bool IsPitchingDecisionInputReady(CareerMatchSession session)
        {
            return session != null &&
                   session.Mode == CareerMatchMode.InterveneOnPlayer &&
                   session.Phase == CareerMatchPhase.Playing &&
                   session.PendingPitchingDecision.HasValue &&
                   !_hasControlledResult &&
                   !_playback.HasPendingEvents(session.Events);
        }

        /// <summary>
        /// 화면 전체가 함께 쓰는 표시 상태를 만든다. 각 패널이 이닝·아웃을 따로 계산하지 않게 한다.
        /// </summary>
        private MatchProgressViewState BuildViewState(
            CareerMatchSession session,
            CareerMatchPlaybackSnapshot snapshot,
            bool isDecisionInputReady)
        {
            var player = new MatchProgressPlayerContext
            {
                ControlledPlayerId = session.ControlledPlayerId,
                Role = session.PlayerRole,
                Position = _manager.CurrentCareer.MyPlayer.PrimaryPosition,
                IsPlayerTeamHome =
                    session.Input.HomeTeam.TeamId == _manager.CurrentCareer.MyPlayer.CurrentTeamId,
                CanReceiveBattingDecisions = session.CanReceiveBattingDecisions
            };
            var flow = new MatchProgressFlowContext
            {
                Phase = session.Phase,
                IsDecisionInputReady = isDecisionInputReady,
                HasControlledResult = _hasControlledResult,
                IsAutomaticPlaybackActive = IsAutomaticPlaybackActive(session),
                IsPaused = _isPaused,
                IsCallUpAcknowledged = _isCallUpAcknowledged
            };
            return MatchProgressViewState.Create(
                session.Events,
                _playback.VisibleEventCount,
                snapshot,
                player,
                flow);
        }

        private bool IsPendingCallUpAcknowledgement(CareerMatchSession session)
        {
            if (session.Phase == CareerMatchPhase.Preparation ||
                session.Mode != CareerMatchMode.InterveneOnPlayer ||
                _isCallUpAcknowledged)
                return false;

            bool isDecisionInputReady = IsDecisionInputReady(session);
            CareerMatchPlaybackSnapshot snapshot = _playback.BuildSnapshot(
                session.Events,
                isDecisionInputReady ? session.PendingDecision : null);
            return BuildViewState(session, snapshot, isDecisionInputReady).Flow ==
                   MatchFlowState.PlayerCallUp;
        }

        private void AcknowledgeCallUp()
        {
            _isCallUpAcknowledged = true;
            Render();
        }

        private void TogglePause()
        {
            _isPaused = !_isPaused;
            if (!_isPaused)
                _nextAutomaticPlayAt = Time.unscaledTime + GetAutomaticPlayIntervalSeconds();
            Render();
        }

        private void ResumePlayback()
        {
            if (!_isPaused)
                return;
            TogglePause();
        }

        /// <summary>
        /// 일시 정지 상태에서 다음 타석 하나만 공개한다.
        /// </summary>
        private void StepOnePlateAppearance()
        {
            CareerMatchSession session = _manager?.ActiveMatch;
            if (session == null || !IsAutomaticPlaybackActive(session))
                return;

            _isPaused = true;
            AdvanceOneStep(session);
            Render();
        }

        /// <summary>
        /// 일시 정지 상태에서 현재 이닝이 끝날 때까지만 공개한다.
        /// </summary>
        private void StepToInningEnd()
        {
            CareerMatchSession session = _manager?.ActiveMatch;
            if (session == null || !IsAutomaticPlaybackActive(session))
                return;

            _isPaused = true;
            for (int step = 0; step < MaximumStepAdvances; step++)
            {
                if (!AdvanceOneStep(session))
                    break;
                if (IsLatestVisibleEventHalfInningEnd(session))
                    break;
            }
            Render();
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
            _isPaused = false;
            Render();
        }

        private void RevealControlledPlayAndRender()
        {
            CareerMatchSession session = _manager.ActiveMatch;
            EnsurePlayback(session);
            int firstRevealedEventIndex = _playback.VisibleEventCount;
            _playback.RevealControlledPlay(session.Events, session.ControlledPlayerId);
            _isCallUpAcknowledged = false;
            _isPaused = false;
            if (_playback.TryGetControlledPlateAppearanceSummary(
                    session.Events,
                    firstRevealedEventIndex,
                    session.ControlledPlayerId,
                    out CareerPlateAppearanceSummary summary))
            {
                _controlledResult = summary;
                _hasControlledResult = true;
                _isControlledPlayerPlaybackStep = true;
                _nextAutomaticPlayAt = Time.unscaledTime + GetControlledResultHoldSeconds();
            }
            else
            {
                _nextAutomaticPlayAt = Time.unscaledTime + GetAutomaticPlayIntervalSeconds();
            }
            Render();
        }

        /// <summary>
        /// 오른쪽 패널을 자동 진행 제어와 라인 스코어, 다음 타순 세 가지로만 정리한다.
        /// </summary>
        private void RenderControlPanel(
            RectTransform panel,
            CareerMatchSession session,
            CareerMatchPlaybackSnapshot snapshot,
            MatchProgressViewState view)
        {
            if (IsPitchingDecisionInputReady(session))
            {
                ClearPersistentControls();
                RenderPitchingDecisionPanel(panel, session);
                return;
            }

            RenderPersistentControls(session, view);

            if (view.IsPlaybackControlHidden)
            {
                CreateStatusPill(
                    panel,
                    view.Flow == MatchFlowState.PlayerAtBat ? "내 타석 진행 중" : "감독 호출",
                    new Vector2(460f, 52f),
                    new Vector2(0f, 412f));
                CreateText(
                    "HiddenGuide", panel, "타석이 끝나면 자동 진행 제어가 다시 열립니다.", 14,
                    FontStyle.Normal, TextAnchor.MiddleCenter,
                    new Vector2(460f, 26f), new Vector2(0f, 372f), SecondaryTextColor);
                RenderLineScore(panel, session, new Vector2(0f, 236f));
                RenderBattingOrder(panel, session, snapshot, new Vector2(0f, -110f));
                return;
            }

            RenderLineScore(panel, session, new Vector2(0f, 10f));
            RenderBattingOrder(panel, session, snapshot, new Vector2(0f, -262f));

            if (!string.IsNullOrEmpty(_manager.LastError))
            {
                CreateText(
                    "Error", panel, _manager.LastError, 13, FontStyle.Normal, TextAnchor.MiddleCenter,
                    new Vector2(460f, 22f), new Vector2(0f, -440f), DangerColor);
            }
        }

        private void RenderPitchingDecisionPanel(RectTransform panel, CareerMatchSession session)
        {
            MatchPitchingDecisionRequest request = session.PendingPitchingDecision.Value;
            CreateStatusPill(panel, $"{request.Inning}회 · 투구 방침 확인",
                new Vector2(450f, 50f), new Vector2(0f, 396f));
            CreateText("PitchingTitle", panel, "이번 이닝 투구 방침", 25, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(440f, 42f), new Vector2(0f, 330f), PrimaryTextColor);
            CreateText("PitchingSituation", panel,
                $"아웃 {request.Outs} · 주자 {GetPitchingRunnerLabel(request)} · " +
                $"스코어 {request.AwayScore}:{request.HomeScore}",
                15, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(440f, 30f), new Vector2(0f, 290f), SecondaryTextColor);

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
                bool selected = approach == _selectedPitchingApproach;
                Button button = CreateButton(
                    "PitchingApproach_" + approach,
                    panel,
                    $"{index + 1}  {GetPitchingApproachLabel(approach)}",
                    new Vector2(410f, 52f),
                    new Vector2(0f, 220f - index * 61f),
                    selected ? new Color(0.025f, 0.32f, 0.52f, 1f) : PanelDarkColor,
                    selected ? PrimaryTextColor : SecondaryTextColor);
                button.onClick.AddListener(() => SelectPitchingApproach(approach));
            }

            Button start = CreateButton(
                "StartPitchingInning", panel, "이닝 투구 시작   SPACE",
                new Vector2(430f, 64f), new Vector2(0f, -140f),
                new Color(0.02f, 0.38f, 0.7f, 1f), PrimaryTextColor);
            start.onClick.AddListener(StartSelectedPitchingInning);
            CreateText("PitchingGuide", panel,
                "현재 방침으로 이닝 종료까지 진행하고 다음 이닝 시작 전에 다시 멈춥니다.",
                14, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(430f, 48f), new Vector2(0f, -208f), SecondaryTextColor);
        }

        private void StartSelectedPitchingInning()
        {
            CareerMatchSession session = _manager?.ActiveMatch;
            if (!IsPitchingDecisionInputReady(session))
                return;
            _manager.AutoCompleteCurrentPitchingInning(_selectedPitchingApproach);
        }

        private static string GetPitchingApproachLabel(PitchingApproach approach)
        {
            return approach switch
            {
                PitchingApproach.FullPower => "전력 투구",
                PitchingApproach.ControlFirst => "제구 우선",
                PitchingApproach.InduceChase => "유인구 승부",
                PitchingApproach.QuickAttack => "빠른 승부",
                _ => "균형 투구"
            };
        }

        private static string GetPitchingRunnerLabel(MatchPitchingDecisionRequest request)
        {
            if (!request.HasRunnerOnFirst && !request.HasRunnerOnSecond && !request.HasRunnerOnThird)
                return "없음";
            string result = request.HasRunnerOnFirst ? "1루" : string.Empty;
            if (request.HasRunnerOnSecond) result += string.IsNullOrEmpty(result) ? "2루" : "·2루";
            if (request.HasRunnerOnThird) result += string.IsNullOrEmpty(result) ? "3루" : "·3루";
            return result;
        }

        /// <summary>
        /// 자동 진행 중에는 화면이 매 스텝 다시 그려지므로, 버튼을 그때마다 파괴하면
        /// 누르는 도중에 대상이 사라져 클릭이 성립하지 않는다. 그래서 조작부는 별도 계층에 두고
        /// 표시 내용이 실제로 달라졌을 때만 다시 만든다.
        /// </summary>
        private void RenderPersistentControls(CareerMatchSession session, MatchProgressViewState view)
        {
            string signature = BuildControlSignature(view);
            if (signature != _controlSignature)
            {
                ClearChildren(_controlHost);
                _stopConditionText = null;
                _controlSignature = signature;
                if (!view.IsPlaybackControlHidden)
                {
                    RenderAutomaticProgressCard(_controlHost, session, view, new Vector2(0f, 349f));

                    Button primary = CreateButton(
                        "PrimaryAction", _controlHost, GetPrimaryActionLabel(view.PrimaryAction),
                        new Vector2(460f, 66f), new Vector2(0f, 200f),
                        new Color(0.02f, 0.38f, 0.7f, 1f), PrimaryTextColor);
                    primary.onClick.AddListener(() => InvokePrimaryAction(view.PrimaryAction));

                    RenderSecondaryActions(_controlHost, session, view, 136f);
                }
            }

            if (_stopConditionText != null)
                _stopConditionText.text = GetStopConditionLabel(session, view);
        }

        /// <summary>
        /// 조작부의 구성을 결정하는 값만 모은다. 여기 없는 값(다음 정지 문구 등)은
        /// 다시 만들지 않고 기존 오브젝트의 내용만 갱신한다.
        /// </summary>
        private string BuildControlSignature(MatchProgressViewState view)
        {
            if (view.IsPlaybackControlHidden)
                return "hidden";
            return string.Concat(
                ((int)view.Flow).ToString(),
                "/",
                ((int)view.PrimaryAction).ToString(),
                "/",
                _playbackSpeedStepIndex.ToString());
        }

        private void ClearPersistentControls()
        {
            if (_controlHost == null)
                return;
            ClearChildren(_controlHost);
            _stopConditionText = null;
            _controlSignature = null;
        }

        private void RenderAutomaticProgressCard(
            RectTransform panel,
            CareerMatchSession session,
            MatchProgressViewState view,
            Vector2 position)
        {
            RectTransform card = CreateImage(
                "AutoProgress", panel, PanelDarkColor, new Vector2(460f, 186f), position);
            bool isRunning = view.Flow is MatchFlowState.AutoRunning or MatchFlowState.SideChange;
            CreateText(
                "Title", card, isRunning ? "자동 진행 중" : "경기 일시 정지", 21, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Vector2(360f, 30f), new Vector2(-32f, 58f),
                isRunning ? PrimaryTextColor : SecondaryTextColor);
            CreateImage(
                "RunningDot", card, isRunning ? RoleColor : MutedTextColor,
                new Vector2(14f, 14f), new Vector2(208f, 58f));
            CreateImage("Divider", card, new Color(0.1f, 0.24f, 0.34f, 1f),
                new Vector2(420f, 1f), new Vector2(0f, 34f));

            CreateText(
                "StopLabel", card, "다음 정지", 12, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(200f, 22f), new Vector2(-112f, 12f), MutedTextColor);
            _stopConditionText = CreateText(
                "StopValue", card, GetStopConditionLabel(session, view), 16, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Vector2(420f, 26f), new Vector2(-12f, -14f), GoldColor);

            for (int index = 0; index < PlaybackSpeedRates.Length; index++)
                CreateSpeedButton(card, index, new Vector2(-168f + index * 112f, -58f));
        }

        private void CreateSpeedButton(RectTransform parent, int stepIndex, Vector2 position)
        {
            bool isSelected = stepIndex == _playbackSpeedStepIndex;
            Button button = CreateButton(
                "Speed_" + stepIndex, parent, FormatPlaybackSpeedRate(PlaybackSpeedRates[stepIndex]),
                new Vector2(104f, 44f), position,
                isSelected ? new Color(0.035f, 0.24f, 0.39f, 1f) : new Color(0.06f, 0.13f, 0.17f, 1f),
                isSelected ? PrimaryTextColor : SecondaryTextColor);
            button.onClick.AddListener(() => SelectPlaybackSpeed(stepIndex));
        }

        private void RenderSecondaryActions(
            RectTransform panel,
            CareerMatchSession session,
            MatchProgressViewState view,
            float y)
        {
            // 타석 결과를 확인하는 동안 부분 진행 버튼을 열어 두면 결과 화면과 진행 상태가 어긋난다.
            bool isRunning = view.Flow is MatchFlowState.AutoRunning or MatchFlowState.SideChange or
                MatchFlowState.PlayerAtBatResult;
            if (isRunning || view.PrimaryAction == MatchPrimaryAction.FinishMatch)
            {
                Button finish = CreateButton(
                    "FinishMatch", panel, "경기 종료까지 진행", new Vector2(460f, 44f), new Vector2(0f, y),
                    new Color(0.07f, 0.16f, 0.21f, 1f), SecondaryTextColor);
                finish.onClick.AddListener(AutoCompleteMatch);
                return;
            }

            Button nextBatter = CreateButton(
                "StepPlateAppearance", panel, "다음 타자 진행", new Vector2(224f, 44f), new Vector2(-118f, y),
                new Color(0.07f, 0.16f, 0.21f, 1f), SecondaryTextColor);
            nextBatter.onClick.AddListener(StepOnePlateAppearance);
            Button inningEnd = CreateButton(
                "StepInningEnd", panel, "이닝 종료까지", new Vector2(224f, 44f), new Vector2(118f, y),
                new Color(0.07f, 0.16f, 0.21f, 1f), SecondaryTextColor);
            inningEnd.onClick.AddListener(StepToInningEnd);
        }

        private void InvokePrimaryAction(MatchPrimaryAction action)
        {
            switch (action)
            {
                case MatchPrimaryAction.Pause:
                    TogglePause();
                    return;
                case MatchPrimaryAction.AdvanceToPlayerEntry:
                case MatchPrimaryAction.AdvanceToPlayerAtBat:
                case MatchPrimaryAction.ContinueMatch:
                    ResumePlayback();
                    return;
                case MatchPrimaryAction.EnterPlateAppearance:
                    AcknowledgeCallUp();
                    return;
                case MatchPrimaryAction.NextPitch:
                    SubmitSelectedApproach();
                    return;
                case MatchPrimaryAction.FinishMatch:
                    AutoCompleteMatch();
                    return;
                default:
                    _manager.ReturnHomeFromCompletedMatch();
                    return;
            }
        }

        /// <summary>
        /// 총 타석 수가 정해져 있지 않은 야구 경기에서는 진행률 막대 대신 라인 스코어가 진행 정도를 알려 준다.
        /// </summary>
        private void RenderLineScore(RectTransform panel, CareerMatchSession session, Vector2 position)
        {
            RectTransform card = CreateImage(
                "LineScore", panel, PanelDarkColor, new Vector2(460f, 176f), position);
            CreateText(
                "Label", card, "라인 스코어", 12, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(200f, 22f), new Vector2(-112f, 66f), MutedTextColor);

            MatchLineScore lineScore = MatchLineScore.Create(session.Events, _playback.VisibleEventCount);
            float cellWidth = Mathf.Min(34f, 310f / lineScore.InningCount);
            float firstCellX = -140f;

            for (int inning = 1; inning <= lineScore.InningCount; inning++)
            {
                float x = firstCellX + (inning - 1) * cellWidth;
                bool isCurrentInning = inning == lineScore.CurrentInning;
                CreateText(
                    "Header" + inning, card, inning.ToString(), 13, FontStyle.Bold, TextAnchor.MiddleCenter,
                    new Vector2(cellWidth, 22f), new Vector2(x, 34f),
                    isCurrentInning ? GoldColor : MutedTextColor);
                CreateLineScoreCell(card, "Away" + inning, lineScore.GetAwayRuns(inning), x, 0f, cellWidth);
                CreateLineScoreCell(card, "Home" + inning, lineScore.GetHomeRuns(inning), x, -34f, cellWidth);
            }

            CreateText(
                "AwayName", card, session.Input.AwayTeam.Name, 13, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(130f, 22f), new Vector2(-160f, 0f), SecondaryTextColor);
            CreateText(
                "HomeName", card, session.Input.HomeTeam.Name, 13, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(130f, 22f), new Vector2(-160f, -34f), SecondaryTextColor);
            CreateText(
                "RunHeader", card, "R", 13, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(34f, 22f), new Vector2(200f, 34f), MutedTextColor);
            CreateText(
                "AwayTotal", card, lineScore.AwayTotal.ToString(), 17, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(34f, 24f), new Vector2(200f, 0f), PrimaryTextColor);
            CreateText(
                "HomeTotal", card, lineScore.HomeTotal.ToString(), 17, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(34f, 24f), new Vector2(200f, -34f), PrimaryTextColor);
            CreateText(
                "Progress", card, $"{lineScore.CurrentInning}회 진행 중", 13, FontStyle.Normal,
                TextAnchor.MiddleCenter, new Vector2(440f, 22f), new Vector2(0f, -66f), MutedTextColor);
        }

        private static void CreateLineScoreCell(
            RectTransform card,
            string name,
            int runs,
            float x,
            float y,
            float cellWidth)
        {
            bool isPlayed = runs != MatchLineScore.NotPlayed;
            CreateText(
                name, card, isPlayed ? runs.ToString() : "·", 15, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(cellWidth, 24f), new Vector2(x, y),
                !isPlayed ? MutedTextColor : runs > 0 ? GoldColor : PrimaryTextColor);
        }

        /// <summary>
        /// 현재 타순과 뒤따르는 타순을 보여 준다. 내 선수가 타순에 있으면 몇 번째 뒤인지 함께 표시한다.
        /// </summary>
        private void RenderBattingOrder(
            RectTransform panel,
            CareerMatchSession session,
            CareerMatchPlaybackSnapshot snapshot,
            Vector2 position)
        {
            RectTransform card = CreateImage(
                "BattingOrder", panel, PanelDarkColor, new Vector2(460f, 340f), position);
            CreateText(
                "Label", card, "현재 및 다음 타순", 12, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(220f, 22f), new Vector2(-112f, 148f), MutedTextColor);

            Team battingTeam = GetBattingTeam(session, snapshot.Half);
            int currentIndex = FindBattingOrderIndex(battingTeam, snapshot.BatterId);
            if (currentIndex < 0)
                currentIndex = 0;

            for (int offset = 0; offset < BattingOrderPreviewCount; offset++)
            {
                int slotIndex = (currentIndex + offset) % battingTeam.Lineup.Count;
                RenderBattingOrderRow(card, session, battingTeam, slotIndex, offset, 106f - offset * 40f);
            }

            CreateImage("Divider", card, new Color(0.1f, 0.24f, 0.34f, 1f),
                new Vector2(420f, 1f), new Vector2(0f, -76f));
            CreateText(
                "Pitcher", card,
                $"상대 투수 · {FindPlayerName(session.Input, snapshot.PitcherId)}",
                15, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(440f, 26f), new Vector2(0f, -104f), SecondaryTextColor);

            if (FindBattingOrderIndex(battingTeam, session.ControlledPlayerId) < 0)
            {
                CreateText(
                    "BenchNotice", card,
                    $"내 선수 · 벤치 대기 · {GetSubstitutionPriorityLabel(session)}",
                    14, FontStyle.Bold, TextAnchor.MiddleCenter,
                    new Vector2(440f, 24f), new Vector2(0f, -138f), RoleColor);
            }
        }

        private void RenderBattingOrderRow(
            RectTransform card,
            CareerMatchSession session,
            Team battingTeam,
            int slotIndex,
            int offset,
            float y)
        {
            int playerId = battingTeam.Lineup[slotIndex].Player.PlayerId;
            bool isControlled = playerId == session.ControlledPlayerId;
            if (isControlled)
            {
                CreateImage(
                    "ControlledRow", card, new Color(0.03f, 0.14f, 0.1f, 1f),
                    new Vector2(420f, 34f), new Vector2(0f, y));
            }

            CreateText(
                $"Order{slotIndex}", card, (slotIndex + 1).ToString(), 14, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(32f, 24f), new Vector2(-194f, y),
                offset == 0 ? GoldColor : MutedTextColor);
            CreateText(
                $"Name{slotIndex}", card, battingTeam.Lineup[slotIndex].Player.Name, 16,
                offset == 0 ? FontStyle.Bold : FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(220f, 26f), new Vector2(-56f, y),
                isControlled ? RoleColor : offset == 0 ? PrimaryTextColor : SecondaryTextColor);
            CreateText(
                $"Status{slotIndex}", card,
                isControlled
                    ? offset == 0 ? "내 선수 · 타석 중" : $"내 선수 · {offset}명 뒤"
                    : offset == 0 ? "타석 진행 중" : offset == 1 ? "대기" : string.Empty,
                13, FontStyle.Bold, TextAnchor.MiddleRight,
                new Vector2(148f, 24f), new Vector2(128f, y),
                isControlled ? RoleColor : MutedTextColor);
        }

        private void SelectPlaybackSpeed(int stepIndex)
        {
            int clamped = Mathf.Clamp(stepIndex, 0, PlaybackSpeedRates.Length - 1);
            if (clamped == _playbackSpeedStepIndex)
                return;

            _playbackSpeedStepIndex = clamped;

            CareerGameSettings settings = _manager.CurrentCareer.GameSettings;
            _manager.UpdateGameSettings(
                settings.BattingApproach,
                settings.PitchingApproach,
                settings.MatchProgressMode,
                (int)PlaybackSpeedRates[clamped],
                settings.AutoSlowOnPlayerEvent);

            // 남은 대기 시간이 이전 배속으로 잡혀 있으므로 새 배속 기준으로 다시 잡는다.
            _nextAutomaticPlayAt = Time.unscaledTime + (_hasControlledResult
                ? GetControlledResultHoldSeconds()
                : GetAutomaticPlayIntervalSeconds());
            Render();
        }

        private float GetAutomaticPlayIntervalSeconds()
        {
            return automaticPlayIntervalSeconds / GetEffectivePlaybackSpeedRate(false);
        }

        private float GetControlledResultHoldSeconds()
        {
            float speedRate = GetEffectivePlaybackSpeedRate(true);
            return Mathf.Max(
                minimumControlledResultHoldSeconds,
                controlledResultHoldSeconds / speedRate);
        }

        private float GetSideChangeHoldSeconds()
        {
            return Mathf.Max(GetAutomaticPlayIntervalSeconds(), sideChangeHoldSeconds / GetPlaybackSpeedRate());
        }

        private float GetPlaybackSpeedRate()
        {
            return PlaybackSpeedRates[
                Mathf.Clamp(_playbackSpeedStepIndex, 0, PlaybackSpeedRates.Length - 1)];
        }

        private float GetEffectivePlaybackSpeedRate(bool isControlledPlayerEvent)
        {
            CareerGameSettings settings = _manager?.CurrentCareer?.GameSettings;
            CareerMatchMode mode = _playbackSession?.Mode ?? CareerMatchMode.FullGameWatch;
            if (settings == null)
                return GetPlaybackSpeedRate();
            return CareerMatchPlaybackSpeedPolicy.Resolve(
                (int)GetPlaybackSpeedRate(),
                mode,
                settings.AutoSlowOnPlayerEvent,
                isControlledPlayerEvent);
        }

        private void ClearControlledResult()
        {
            _controlledResult = default;
            _hasControlledResult = false;
            _isControlledPlayerPlaybackStep = false;
        }

        private static bool ContainsControlledPitcherEvent(
            IReadOnlyList<MatchEvent> events,
            int firstEventIndex,
            int visibleEventCount,
            int controlledPlayerId)
        {
            for (int index = firstEventIndex; index < visibleEventCount; index++)
            {
                if (events[index].PitcherId == controlledPlayerId)
                    return true;
            }
            return false;
        }

        private static bool IsControlledPlayerOnBase(
            CareerMatchPlaybackSnapshot snapshot,
            int controlledPlayerId)
        {
            return snapshot.FirstRunnerId == controlledPlayerId ||
                   snapshot.SecondRunnerId == controlledPlayerId ||
                   snapshot.ThirdRunnerId == controlledPlayerId;
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
                MatchEventType.PitcherEntered or MatchEventType.PitcherRemoved => true,
                MatchEventType.PinchHitterEntered or MatchEventType.PinchRunnerEntered => true,
                MatchEventType.DefensiveReplacement => true,
                MatchEventType.StealSucceeded or MatchEventType.CaughtStealing => true,
                MatchEventType.IntentionalWalk or MatchEventType.BuntResolved => true,
                MatchEventType.FieldingError or MatchEventType.ThrowingError => true,
                MatchEventType.DoublePlay or MatchEventType.FieldersChoice => true,
                MatchEventType.HalfInningEnded => true,
                _ => false
            };
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
            return FormatPlaybackSpeedRate(
                GetEffectivePlaybackSpeedRate(_isControlledPlayerPlaybackStep));
        }

        private string GetConfiguredPlaybackSpeedLabel()
        {
            return FormatPlaybackSpeedRate(GetPlaybackSpeedRate());
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
