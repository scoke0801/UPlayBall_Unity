using System;
using System.Collections.Generic;
using Baseball.Game.Historical;
using Baseball.Simulation.Match;
using Baseball.Simulation.PlateAppearance;
using BaseballPlayer = Baseball.Core.Players.Player;

namespace Baseball.Presentation.Match
{
    /// <summary>중요 순간 관전이 한 번에 재생할 공식 이벤트 구간이다.</summary>
    public readonly struct OwnerMatchHighlightSegment
    {
        public OwnerMatchHighlightSegment(int startEventIndex, int endEventIndex)
        {
            if (startEventIndex < 0) throw new ArgumentOutOfRangeException(nameof(startEventIndex));
            if (endEventIndex < startEventIndex) throw new ArgumentOutOfRangeException(nameof(endEventIndex));
            StartEventIndex = startEventIndex;
            EndEventIndex = endEventIndex;
        }

        public int StartEventIndex { get; }
        public int EndEventIndex { get; }
    }

    /// <summary>확정 이벤트에서 승리 기대값 변화와 경기 맥락으로 중요 순간을 선별한다.</summary>
    public static class OwnerMatchHighlightSelector
    {
        private const float DefaultWinExpectancySwing = 0.10f;
        private const float DefaultLateWinExpectancySwing = 0.055f;

        public static OwnerMatchHighlightSegment[] Select(
            MatchEvent[] events,
            MatchGameCastConfig config)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (events.Length == 0) return Array.Empty<OwnerMatchHighlightSegment>();

            int lateInning = Math.Max(1, config.highlightLateInning);
            int closeRunMargin = Math.Max(0, config.highlightCloseRunMargin);
            int multiRunThreshold = Math.Max(1, config.highlightMultiRunThreshold);
            double standardSwing = config.highlightWinExpectancySwing > 0f
                ? config.highlightWinExpectancySwing
                : DefaultWinExpectancySwing;
            double lateSwing = config.highlightLateWinExpectancySwing > 0f
                ? config.highlightLateWinExpectancySwing
                : DefaultLateWinExpectancySwing;

            var result = new List<OwnerMatchHighlightSegment>(16);
            var winExpectancy = new WinExpectancyModel(RunExpectancy24.CreateDefault());
            int awayScore = 0;
            int homeScore = 0;
            int outs = 0;
            int firstRunnerId = 0;
            int secondRunnerId = 0;
            int thirdRunnerId = 0;
            int blockStartIndex = 0;
            int firstContextIndex = -1;
            int firstPitchIndex = -1;
            int preludeIndex = -1;
            int beforeAwayScore = 0;
            int beforeHomeScore = 0;
            int beforeOuts = 0;
            int beforeOccupancyMask = 0;
            bool hasBeforeState = false;
            bool hasPivotalDecision = false;
            bool hasPivotalDefense = false;
            OwnerMatchHighlightSegment lastPlateAppearance = default;
            bool hasLastPlateAppearance = false;
            int matchEndIndex = -1;

            for (int index = 0; index < events.Length; index++)
            {
                MatchEvent current = events[index];
                if (IsPrelude(current.EventType))
                {
                    if (preludeIndex < 0) preludeIndex = index;
                    hasPivotalDecision |= IsManagerDecision(current.EventType);
                }
                hasPivotalDefense |= IsPivotalDefense(current.EventType);

                if (!hasBeforeState && IsPlateAppearanceContext(current.EventType))
                {
                    hasBeforeState = true;
                    firstContextIndex = index;
                    // 투구·작전 이벤트의 점수는 타석 결과 적용 전 상태이므로 재개 경기에도 안전하다.
                    beforeAwayScore = current.AwayScore;
                    beforeHomeScore = current.HomeScore;
                    beforeOuts = outs;
                    beforeOccupancyMask = CreateOccupancyMask(
                        firstRunnerId,
                        secondRunnerId,
                        thirdRunnerId);
                }
                if (current.EventType == MatchEventType.Pitch && firstPitchIndex < 0)
                    firstPitchIndex = index;

                ApplyRunnerState(
                    current,
                    ref firstRunnerId,
                    ref secondRunnerId,
                    ref thirdRunnerId);
                awayScore = current.AwayScore;
                homeScore = current.HomeScore;
                if (CarriesOutState(current.EventType)) outs = current.Outs;

                if (current.EventType == MatchEventType.HalfInningEnded)
                {
                    firstRunnerId = secondRunnerId = thirdRunnerId = 0;
                    outs = 0;
                    if (!hasBeforeState) blockStartIndex = index + 1;
                }

                if (current.EventType == MatchEventType.PlateAppearanceEnded)
                {
                    if (!hasBeforeState)
                    {
                        beforeAwayScore = awayScore;
                        beforeHomeScore = homeScore;
                        beforeOuts = Math.Max(0, current.Outs - (IsOut(current.PlateAppearanceResult) ? 1 : 0));
                        beforeOccupancyMask = 0;
                    }

                    int startIndex = preludeIndex >= blockStartIndex
                        ? preludeIndex
                        : firstPitchIndex >= blockStartIndex
                            ? firstPitchIndex
                            : firstContextIndex >= blockStartIndex
                                ? firstContextIndex
                                : blockStartIndex;
                    var segment = new OwnerMatchHighlightSegment(startIndex, index);
                    lastPlateAppearance = segment;
                    hasLastPlateAppearance = true;
                    int afterOccupancyMask = CreateOccupancyMask(
                        firstRunnerId,
                        secondRunnerId,
                        thirdRunnerId);
                    if (ShouldHighlight(
                            current,
                            beforeAwayScore,
                            beforeHomeScore,
                            beforeOuts,
                            beforeOccupancyMask,
                            afterOccupancyMask,
                            hasPivotalDecision,
                            hasPivotalDefense,
                            lateInning,
                            closeRunMargin,
                            multiRunThreshold,
                            standardSwing,
                            lateSwing,
                            winExpectancy))
                    {
                        AddSegment(result, segment);
                    }

                    blockStartIndex = index + 1;
                    firstContextIndex = -1;
                    firstPitchIndex = -1;
                    preludeIndex = -1;
                    hasBeforeState = false;
                    hasPivotalDecision = false;
                    hasPivotalDefense = false;
                }
                else if (current.EventType is MatchEventType.MatchEnded or MatchEventType.MatchEndedAsDraw)
                {
                    matchEndIndex = index;
                }
            }

            // 점수 차와 무관하게 마지막 타석은 경기의 결말이므로 반드시 직접 보여 준다.
            if (hasLastPlateAppearance) AddSegment(result, lastPlateAppearance);
            if (matchEndIndex >= 0)
                AddSegment(result, new OwnerMatchHighlightSegment(matchEndIndex, matchEndIndex));
            return result.ToArray();
        }

        private static bool ShouldHighlight(
            in MatchEvent plateAppearance,
            int beforeAwayScore,
            int beforeHomeScore,
            int beforeOuts,
            int beforeOccupancyMask,
            int afterOccupancyMask,
            bool hasPivotalDecision,
            bool hasPivotalDefense,
            int lateInning,
            int closeRunMargin,
            int multiRunThreshold,
            double standardSwing,
            double lateSwing,
            WinExpectancyModel winExpectancy)
        {
            int beforeGameDifference = beforeAwayScore - beforeHomeScore;
            int afterGameDifference = plateAppearance.AwayScore - plateAppearance.HomeScore;
            int runsScored = Math.Abs(
                plateAppearance.AwayScore + plateAppearance.HomeScore -
                beforeAwayScore - beforeHomeScore);
            bool isLate = plateAppearance.Inning >= lateInning;
            bool isClose = Math.Min(Math.Abs(beforeGameDifference), Math.Abs(afterGameDifference)) <= closeRunMargin;
            bool tiedGame = beforeGameDifference != 0 && afterGameDifference == 0;
            bool leadReversed = beforeGameDifference != 0 && afterGameDifference != 0 &&
                                Math.Sign(beforeGameDifference) != Math.Sign(afterGameDifference);
            bool lateLeadTaken = isLate && beforeGameDifference == 0 && afterGameDifference != 0;
            bool signaturePlay = plateAppearance.PlateAppearanceResult is
                PlateAppearanceResult.HomeRun or PlateAppearanceResult.Triple;

            int beforeOffenseDifference = plateAppearance.Half == InningHalf.Top
                ? beforeGameDifference
                : -beforeGameDifference;
            int afterOffenseDifference = plateAppearance.Half == InningHalf.Top
                ? afterGameDifference
                : -afterGameDifference;
            double before = GetOffenseWinExpectancy(
                winExpectancy,
                plateAppearance.Inning,
                plateAppearance.Half,
                beforeOffenseDifference,
                Math.Min(beforeOuts, 2),
                beforeOccupancyMask);
            double after = GetOffenseWinExpectancy(
                winExpectancy,
                plateAppearance.Inning,
                plateAppearance.Half,
                afterOffenseDifference,
                plateAppearance.Outs,
                afterOccupancyMask);
            double swing = Math.Abs(after - before);

            if (signaturePlay || tiedGame || leadReversed || lateLeadTaken || runsScored >= multiRunThreshold)
                return true;
            if (swing >= standardSwing)
                return true;
            return isLate && isClose &&
                   (swing >= lateSwing || runsScored > 0 || hasPivotalDecision || hasPivotalDefense);
        }

        private static double GetOffenseWinExpectancy(
            WinExpectancyModel model,
            int inning,
            InningHalf half,
            int offenseScoreDifference,
            int outs,
            int occupancyMask)
        {
            if (outs < 3)
                return model.GetWinExpectancy(inning, half, offenseScoreDifference, outs, occupancyMask);

            int nextInning = half == InningHalf.Bottom ? inning + 1 : inning;
            InningHalf nextHalf = half == InningHalf.Top ? InningHalf.Bottom : InningHalf.Top;
            return 1d - model.GetWinExpectancy(nextInning, nextHalf, -offenseScoreDifference, 0, 0);
        }

        private static void AddSegment(List<OwnerMatchHighlightSegment> segments, OwnerMatchHighlightSegment segment)
        {
            if (segments.Count > 0 && segments[segments.Count - 1].EndEventIndex == segment.EndEventIndex)
                return;
            segments.Add(segment);
        }

        private static bool IsPlateAppearanceContext(MatchEventType type) =>
            type is MatchEventType.Pitch or MatchEventType.BattingApproachSelected or
                MatchEventType.PitchingApproachSelected or MatchEventType.IntentionalWalk or
                MatchEventType.BuntAttempted;

        private static bool IsPrelude(MatchEventType type) =>
            type == MatchEventType.HighLeverageSituationStarted || IsManagerDecision(type);

        private static bool IsManagerDecision(MatchEventType type) =>
            type is MatchEventType.PitcherEntered or MatchEventType.PinchHitterEntered or
                MatchEventType.PinchRunnerEntered or MatchEventType.DefensiveReplacement or
                MatchEventType.DefensiveAlignmentChanged or MatchEventType.IntentionalWalk or
                MatchEventType.BuntAttempted or MatchEventType.StealAttempted;

        private static bool IsPivotalDefense(MatchEventType type) =>
            type is MatchEventType.FieldingError or MatchEventType.ThrowingError or
                MatchEventType.DoublePlay or MatchEventType.RunnerThrownOut or
                MatchEventType.CaughtStealing;

        private static bool CarriesOutState(MatchEventType type) =>
            type is MatchEventType.Pitch or MatchEventType.Out or MatchEventType.RunnerAdvance or
                MatchEventType.RunnerThrownOut or MatchEventType.Score or
                MatchEventType.PlateAppearanceEnded;

        private static bool IsOut(PlateAppearanceResult result) =>
            result is PlateAppearanceResult.Strikeout or PlateAppearanceResult.GroundOut or
                PlateAppearanceResult.FlyOut or PlateAppearanceResult.BuntPopOut;

        private static int CreateOccupancyMask(int firstRunnerId, int secondRunnerId, int thirdRunnerId)
        {
            return (firstRunnerId != 0 ? 1 : 0) |
                   (secondRunnerId != 0 ? 2 : 0) |
                   (thirdRunnerId != 0 ? 4 : 0);
        }

        private static void ApplyRunnerState(
            in MatchEvent current,
            ref int firstRunnerId,
            ref int secondRunnerId,
            ref int thirdRunnerId)
        {
            if (current.EventType == MatchEventType.RunnerAdvance)
            {
                ClearRunner(current.PlayerId, current.FromBase,
                    ref firstRunnerId, ref secondRunnerId, ref thirdRunnerId);
                if (current.ToBase == 1) firstRunnerId = current.PlayerId;
                else if (current.ToBase == 2) secondRunnerId = current.PlayerId;
                else if (current.ToBase == 3) thirdRunnerId = current.PlayerId;
            }
            else if (current.EventType is MatchEventType.Out or MatchEventType.RunnerThrownOut or
                     MatchEventType.CaughtStealing)
            {
                ClearRunnerById(current.PlayerId,
                    ref firstRunnerId, ref secondRunnerId, ref thirdRunnerId);
            }
        }

        private static void ClearRunner(
            int playerId,
            int fromBase,
            ref int firstRunnerId,
            ref int secondRunnerId,
            ref int thirdRunnerId)
        {
            if (fromBase == 1) firstRunnerId = 0;
            else if (fromBase == 2) secondRunnerId = 0;
            else if (fromBase == 3) thirdRunnerId = 0;
            else ClearRunnerById(playerId, ref firstRunnerId, ref secondRunnerId, ref thirdRunnerId);
        }

        private static void ClearRunnerById(
            int playerId,
            ref int firstRunnerId,
            ref int secondRunnerId,
            ref int thirdRunnerId)
        {
            if (firstRunnerId == playerId) firstRunnerId = 0;
            if (secondRunnerId == playerId) secondRunnerId = 0;
            if (thirdRunnerId == playerId) thirdRunnerId = 0;
        }
    }

    /// <summary>구단주 경기를 감독 AI로 확정하고 그 이벤트를 관전용으로 재생한다.</summary>
    public sealed class OwnerMatchSpectatorSession : IOwnerMatchOverlay
    {
        private const string SpectatorPermissionMessage =
            "경기 운영은 감독 AI가 담당합니다. 구단주는 관전 속도만 조절할 수 있습니다.";

        private readonly MatchEvent[] _events;
        private readonly MatchHudPresentationModelBuilder _hudBuilder = new MatchHudPresentationModelBuilder();
        private readonly IMatchHudView _hudView;
        private readonly int _playerTeamId;
        private readonly Func<int, string> _teamNameResolver;
        private readonly IReadOnlyDictionary<int, string> _participantNames;
        private readonly OwnerMatchHighlightSegment[] _highlightSegments;
        private int _visibleEventCount;
        private bool _isPaused;
        private OwnerMatchPlaybackSpeed _speed = OwnerMatchPlaybackSpeed.Normal;
        private OwnerMatchViewingMode _viewingMode = OwnerMatchViewingMode.EveryMoment;

        private OwnerMatchSpectatorSession(
            ManagerModeMatchResult result,
            MatchEvent[] events,
            IMatchHudView hudView,
            int playerTeamId)
            : this(result, events, hudView, playerTeamId, null, null) { }

        private OwnerMatchSpectatorSession(
            ManagerModeMatchResult result, MatchEvent[] events, IMatchHudView hudView,
            int playerTeamId, Func<int, string> teamNameResolver, IReadOnlyDictionary<int, string> participantNames)
        {
            Result = result ?? throw new ArgumentNullException(nameof(result));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            if (_events.Length == 0)
                throw new InvalidOperationException("구단주 관전 경기의 이벤트 스트림이 비어 있습니다.");

            _hudView = hudView;
            _playerTeamId = playerTeamId;
            _teamNameResolver = teamNameResolver;
            _participantNames = participantNames;
            _highlightSegments = OwnerMatchHighlightSelector.Select(_events, MatchGameCastConfig.Load());
            CurrentHud = BuildHud();
            _hudView?.Present(CurrentHud);
        }

        public ManagerModeMatchResult Result { get; }
        public MatchHudPresentationModel CurrentHud { get; private set; }
        /// <summary>관전 화면은 이 경계 안의 이벤트만 읽어 최종 결과가 먼저 노출되지 않게 한다.</summary>
        public MatchEvent GetVisibleEvent(int index)
        {
            if (index < 0 || index >= _visibleEventCount)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _events[index];
        }

        /// <summary>현재 공개 구간에서만 이닝별 득점을 복원한다.</summary>
        public Baseball.Game.Career.MatchLineScore CreateVisibleLineScore()
        {
            return Baseball.Game.Career.MatchLineScore.Create(_events, _visibleEventCount);
        }

        /// <summary>경기에 등록된 선수의 표시 이름을 조회한다.</summary>
        public string GetParticipantName(int playerId)
        {
            return CreateParticipant(Result.Match.Input, playerId).Name;
        }

        /// <summary>판단 Trace의 Actor가 플레이어 구단 경기 로스터에 속하는지 확인한다.</summary>
        public bool IsPlayerTeamParticipant(int playerId)
        {
            MatchInput input = Result.Match.Input;
            MatchRosterSnapshot roster = input.AwayRoster.TeamId == _playerTeamId
                ? input.AwayRoster
                : input.HomeRoster;
            return FindPlayer(roster, playerId) != null;
        }

        /// <summary>현재 투수와 타자의 실제 투타 방향을 연출용으로 해석한다.</summary>
        public OwnerMatchHandedness GetHandedness(int pitcherId, int batterId)
        {
            MatchInput input = Result.Match.Input;
            BaseballPlayer pitcher = FindPlayer(input.AwayRoster, pitcherId) ?? FindPlayer(input.HomeRoster, pitcherId);
            BaseballPlayer batter = FindPlayer(input.AwayRoster, batterId) ?? FindPlayer(input.HomeRoster, batterId);
            var throwingHand = pitcher?.ThrowingHand ?? Baseball.Core.Players.Handedness.Right;
            var battingHand = batter?.BattingHand ?? Baseball.Core.Players.Handedness.Right;
            if (battingHand == Baseball.Core.Players.Handedness.Switch)
            {
                battingHand = throwingHand == Baseball.Core.Players.Handedness.Left
                    ? Baseball.Core.Players.Handedness.Right
                    : Baseball.Core.Players.Handedness.Left;
            }
            return new OwnerMatchHandedness(throwingHand, battingHand);
        }
        public OwnerMatchOverlayState State => new OwnerMatchOverlayState(
            _visibleEventCount,
            _events.Length,
            _isPaused,
            _speed,
            SpectatorPermissionMessage,
            _viewingMode);

        /// <summary>실시간 명령 없이 감독 AI가 경기 전체를 확정하고 Full 이벤트를 관전 세션에 연결한다.</summary>
        public static OwnerMatchSpectatorSession PlayNextGame(
            OwnerModeManager manager,
            IMatchHudView hudView = null)
        {
            if (manager == null)
                throw new ArgumentNullException(nameof(manager));

            int playerTeamId = manager.Runtime?.ManagerMode.LiveSeason.PlayerTeamId ?? 0;
            var eventBuffer = new MatchEventBuffer();
            ManagerModeMatchResult result = manager.PlayNextGame(
                eventBuffer,
                CreateSpectatorExecutionProfile());
            var season = manager.Runtime.ManagerMode.LiveSeason;
            return new OwnerMatchSpectatorSession(result, eventBuffer.ToArray(), hudView, playerTeamId,
                teamId => manager.GetClubDisplayName(season.GetTeamSeasonKey(teamId)),
                manager.CreateMatchParticipantNames(result.Match.Input));
        }

        public bool TryTogglePause()
        {
            if (!State.CanTogglePause)
                return false;

            _isPaused = !_isPaused;
            return true;
        }

        public bool TrySetPlaybackSpeed(OwnerMatchPlaybackSpeed speed)
        {
            if (!State.CanChangeSpeed || !Enum.IsDefined(typeof(OwnerMatchPlaybackSpeed), speed))
                return false;

            _speed = speed;
            return true;
        }

        public bool TrySetViewingMode(OwnerMatchViewingMode mode)
        {
            if (!State.CanChangeViewingMode || !Enum.IsDefined(typeof(OwnerMatchViewingMode), mode))
                return false;

            _viewingMode = mode;
            _isPaused = false;
            if (mode == OwnerMatchViewingMode.ResultOnly)
                return TryRevealAll();

            return true;
        }

        /// <summary>선택한 관전 밀도의 다음 경계까지 이미 확정된 이벤트만 공개한다.</summary>
        public bool TryAdvance()
        {
            if (!State.CanAdvance)
                return false;

            if (_viewingMode == OwnerMatchViewingMode.KeyMoments)
            {
                _visibleEventCount = TryFindNextHighlight(
                    _visibleEventCount,
                    out OwnerMatchHighlightSegment highlight)
                    ? Math.Min(_events.Length, highlight.EndEventIndex + 1)
                    : _events.Length;
            }
            else
            {
                while (_visibleEventCount < _events.Length)
                {
                    int eventIndex = _visibleEventCount++;
                    if (IsEveryMomentBoundary(eventIndex)) break;
                }
            }

            PresentCurrentHud();
            return true;
        }

        /// <summary>다음 관전 구간을 준비하되 마지막 사건의 점수와 판정은 아직 공개하지 않는다.</summary>
        public bool TryPreparePlayback(out int lastEventIndex)
        {
            lastEventIndex = _visibleEventCount;
            if (!State.CanAdvance || _isPaused) return false;

            if (_viewingMode == OwnerMatchViewingMode.KeyMoments)
            {
                if (!TryFindNextHighlight(_visibleEventCount, out OwnerMatchHighlightSegment highlight))
                    return false;
                lastEventIndex = highlight.EndEventIndex;
                if (_visibleEventCount < highlight.StartEventIndex)
                {
                    _visibleEventCount = highlight.StartEventIndex;
                    PresentCurrentHud();
                }
                return true;
            }

            while (lastEventIndex < _events.Length - 1 && !IsEveryMomentBoundary(lastEventIndex))
                lastEventIndex++;
            return true;
        }

        /// <summary>연출 준비에만 사용할 다음 사건이다. HUD와 기록은 GetVisibleEvent 경계를 따른다.</summary>
        public MatchEvent PeekPlaybackEvent()
        {
            if (!State.CanAdvance) throw new InvalidOperationException("남은 경기 사건이 없습니다.");
            return _events[_visibleEventCount];
        }

        /// <summary>송구 원인과 아웃 결과를 HUD 공개 없이 함께 준비한다.</summary>
        public OwnerMatchPlaybackGroup PeekPlaybackGroup()
        {
            MatchEvent current = PeekPlaybackEvent();
            MatchEvent next = _visibleEventCount + 1 < _events.Length ? _events[_visibleEventCount + 1] : default;
            return OwnerMatchPlaybackGroup.Resolve(current, next);
        }

        /// <summary>연출이 끝난 묶음만 원래 기록 순서 그대로 한 번에 공개한다.</summary>
        public bool TryRevealPlaybackGroup(int eventCount)
        {
            if (!State.CanAdvance || _isPaused || PeekPlaybackGroup().EventCount != eventCount) return false;
            _visibleEventCount += eventCount;
            PresentCurrentHud();
            return true;
        }

        /// <summary>타구 연출에 필요한 공식 담당 야수만 같은 투구의 결과에서 읽는다.</summary>
        public BallInPlayEventData PeekBallInPlay()
        {
            BallInPlayEventData data = default;
            for (int index = _visibleEventCount; index < _events.Length; index++)
            {
                MatchEvent current = _events[index];
                if (index > _visibleEventCount && current.EventType == MatchEventType.Pitch) break;
                if (current.BallInPlayData.HasValue) data = current.BallInPlayData;
                if (current.EventType == MatchEventType.PlateAppearanceEnded) break;
            }
            return data;
        }

        /// <summary>HUD를 공개하지 않고 같은 타구의 기존 주자 이동만 연출 버퍼에 복사한다.</summary>
        public int CopyUpcomingRunnerRoutes(OwnerMatchRunnerRoute[] destination)
        {
            MatchHudBaseStateModel bases = CurrentHud.Bases;
            return OwnerMatchRunnerRoute.Collect(_events, _visibleEventCount, CurrentHud.Batter.PlayerId,
                bases.First.PlayerId, bases.Second.PlayerId, bases.Third.PlayerId, destination);
        }

        /// <summary>연출이 끝난 사건 한 건만 공개한다. 일시정지 중에는 공개하지 않는다.</summary>
        public bool TryRevealPlaybackEvent()
        {
            if (!State.CanAdvance || _isPaused) return false;
            _visibleEventCount++;
            PresentCurrentHud();
            return true;
        }

        private bool IsEveryMomentBoundary(int eventIndex)
        {
            MatchEvent matchEvent = _events[eventIndex];
            if (matchEvent.EventType is MatchEventType.HalfInningEnded or MatchEventType.MatchEnded or
                MatchEventType.MatchEndedAsDraw)
                return true;
            return matchEvent.EventType is MatchEventType.Pitch or MatchEventType.PlateAppearanceEnded;
        }

        private bool TryFindNextHighlight(int visibleEventCount, out OwnerMatchHighlightSegment highlight)
        {
            for (int index = 0; index < _highlightSegments.Length; index++)
            {
                OwnerMatchHighlightSegment candidate = _highlightSegments[index];
                if (candidate.EndEventIndex < visibleEventCount) continue;
                highlight = candidate;
                return true;
            }
            highlight = default;
            return false;
        }

        public bool TryRevealAll()
        {
            if (!State.CanAdvance)
                return false;

            _visibleEventCount = _events.Length;
            _isPaused = false;
            PresentCurrentHud();
            return true;
        }

        private static MatchExecutionProfile CreateSpectatorExecutionProfile()
        {
            return new MatchExecutionProfile(
                SimulationEngineKind.Detailed,
                MatchDecisionMode.InternalAiOnly,
                MatchEventMode.Full,
                MatchDecisionTraceMode.Full,
                MatchStatisticsMode.FullBoxScore);
        }

        private void PresentCurrentHud()
        {
            CurrentHud = BuildHud();
            _hudView?.Present(CurrentHud);
        }

        private MatchHudPresentationModel BuildHud()
        {
            MatchEvent latest = _events[_visibleEventCount == 0 ? 0 : _visibleEventCount - 1];
            int firstRunnerId = 0;
            int secondRunnerId = 0;
            int thirdRunnerId = 0;
            for (int index = 0; index < _visibleEventCount; index++)
            {
                MatchEvent matchEvent = _events[index];
                if (matchEvent.EventType == MatchEventType.RunnerAdvance)
                {
                    ClearRunner(
                        matchEvent.PlayerId,
                        matchEvent.FromBase,
                        ref firstRunnerId,
                        ref secondRunnerId,
                        ref thirdRunnerId);
                    PlaceRunner(
                        matchEvent.PlayerId,
                        matchEvent.ToBase,
                        ref firstRunnerId,
                        ref secondRunnerId,
                        ref thirdRunnerId);
                }
                else if (matchEvent.EventType == MatchEventType.Out)
                {
                    ClearRunnerById(
                        matchEvent.PlayerId,
                        ref firstRunnerId,
                        ref secondRunnerId,
                        ref thirdRunnerId);
                }
                else if (matchEvent.EventType == MatchEventType.HalfInningEnded)
                {
                    firstRunnerId = 0;
                    secondRunnerId = 0;
                    thirdRunnerId = 0;
                }
            }

            MatchInput input = Result.Match.Input;
            bool isTop = latest.Half == InningHalf.Top;
            bool isBetweenInnings = _visibleEventCount > 0 &&
                                    latest.EventType == MatchEventType.HalfInningEnded;
            return _hudBuilder.Build(
                Math.Max(1, latest.Inning),
                isTop ? MatchHudHalf.Top : MatchHudHalf.Bottom,
                new MatchHudTeamModel(
                    FormatTeamDisplayName(_teamNameResolver?.Invoke(input.AwayRoster.TeamId) ?? input.AwayRoster.TeamName, input.AwayRoster.TeamId == _playerTeamId),
                    latest.AwayScore,
                    isTop),
                new MatchHudTeamModel(
                    FormatTeamDisplayName(_teamNameResolver?.Invoke(input.HomeRoster.TeamId) ?? input.HomeRoster.TeamName, input.HomeRoster.TeamId == _playerTeamId),
                    latest.HomeScore,
                    !isTop),
                new MatchHudCountModel(latest.Balls, latest.Strikes, latest.Outs),
                new MatchHudBaseStateModel(
                    CreateParticipant(input, firstRunnerId),
                    CreateParticipant(input, secondRunnerId),
                    CreateParticipant(input, thirdRunnerId)),
                CreateParticipant(input, latest.BatterId),
                CreateParticipant(input, latest.PitcherId),
                isBetweenInnings);
        }

        /// <summary>구단명이 비어 있을 때만 역할 기반 이름으로 대체한다.</summary>
        public static string FormatTeamDisplayName(string teamName, bool isPlayerTeam)
        {
            if (!string.IsNullOrWhiteSpace(teamName))
                return teamName.Trim();

            return isPlayerTeam ? "우리 구단" : "상대 구단";
        }

        private MatchHudParticipantModel CreateParticipant(MatchInput input, int playerId)
        {
            if (playerId <= 0)
                return MatchHudParticipantModel.Empty;

            if (_participantNames != null && _participantNames.TryGetValue(playerId, out string name))
                return new MatchHudParticipantModel(playerId, name);

            BaseballPlayer player = FindPlayer(input.AwayRoster, playerId) ?? FindPlayer(input.HomeRoster, playerId);
            return new MatchHudParticipantModel(playerId, player?.Name ?? string.Empty);
        }

        private static BaseballPlayer FindPlayer(MatchRosterSnapshot roster, int playerId)
        {
            for (int index = 0; index < roster.StartingLineup.Count; index++)
            {
                BaseballPlayer player = roster.StartingLineup[index].Player;
                if (player.PlayerId == playerId)
                    return player;
            }
            if (roster.StartingPitcher.Player.PlayerId == playerId)
                return roster.StartingPitcher.Player;
            for (int index = 0; index < roster.Bullpen.Count; index++)
            {
                BaseballPlayer player = roster.Bullpen[index].Player;
                if (player.PlayerId == playerId)
                    return player;
            }
            for (int index = 0; index < roster.Bench.Count; index++)
            {
                BaseballPlayer player = roster.Bench[index];
                if (player.PlayerId == playerId)
                    return player;
            }
            return null;
        }

        private static void ClearRunner(
            int playerId,
            int fromBase,
            ref int firstRunnerId,
            ref int secondRunnerId,
            ref int thirdRunnerId)
        {
            if (fromBase == 1) firstRunnerId = 0;
            else if (fromBase == 2) secondRunnerId = 0;
            else if (fromBase == 3) thirdRunnerId = 0;
            else ClearRunnerById(playerId, ref firstRunnerId, ref secondRunnerId, ref thirdRunnerId);
        }

        private static void PlaceRunner(
            int playerId,
            int toBase,
            ref int firstRunnerId,
            ref int secondRunnerId,
            ref int thirdRunnerId)
        {
            if (toBase == 1) firstRunnerId = playerId;
            else if (toBase == 2) secondRunnerId = playerId;
            else if (toBase == 3) thirdRunnerId = playerId;
        }

        private static void ClearRunnerById(
            int playerId,
            ref int firstRunnerId,
            ref int secondRunnerId,
            ref int thirdRunnerId)
        {
            if (firstRunnerId == playerId) firstRunnerId = 0;
            if (secondRunnerId == playerId) secondRunnerId = 0;
            if (thirdRunnerId == playerId) thirdRunnerId = 0;
        }
    }
}
