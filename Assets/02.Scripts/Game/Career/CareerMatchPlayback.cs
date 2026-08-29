using System;
using System.Collections.Generic;
using Baseball.Simulation.Match;
using Baseball.Simulation.PlateAppearance;

namespace Baseball.Game.Career
{
    /// <summary>
    /// 계산이 끝난 경기 이벤트를 타석 단위로 공개해 자동 중계와 플레이어 입력 시점을 분리한다.
    /// </summary>
    public sealed class CareerMatchPlayback
    {
        public int VisibleEventCount { get; private set; }

        /// <summary>
        /// 아직 화면에 공개하지 않은 경기 이벤트가 있는지 반환한다.
        /// </summary>
        public bool HasPendingEvents(IReadOnlyList<MatchEvent> events)
        {
            ValidateEvents(events);
            return VisibleEventCount < events.Count;
        }

        /// <summary>
        /// 다음 타자의 타석 결과까지 이미 계산된 이벤트를 공개한다.
        /// </summary>
        public bool AdvanceAutomatic(IReadOnlyList<MatchEvent> events)
        {
            ValidateEvents(events);
            if (VisibleEventCount >= events.Count)
                return false;

            while (VisibleEventCount < events.Count)
            {
                MatchEvent matchEvent = events[VisibleEventCount++];
                if (matchEvent.EventType == MatchEventType.PlateAppearanceEnded)
                {
                    RevealFollowingHalfInningEnd(events);
                    return true;
                }

                if (matchEvent.EventType is MatchEventType.HalfInningEnded or MatchEventType.MatchEnded)
                    return true;
            }

            return true;
        }

        /// <summary>
        /// 버튼 입력으로 확정된 내 선수의 한 투구 또는 타석 결과를 즉시 공개한다.
        /// </summary>
        public bool RevealControlledPlay(
            IReadOnlyList<MatchEvent> events,
            int controlledPlayerId)
        {
            ValidateEvents(events);
            if (controlledPlayerId <= 0)
                throw new ArgumentOutOfRangeException(nameof(controlledPlayerId));

            if (VisibleEventCount >= events.Count || events[VisibleEventCount].BatterId != controlledPlayerId)
                return false;

            bool didReveal = false;
            while (VisibleEventCount < events.Count)
            {
                MatchEvent matchEvent = events[VisibleEventCount++];
                didReveal = true;
                if (matchEvent.EventType == MatchEventType.PlateAppearanceEnded &&
                    matchEvent.BatterId == controlledPlayerId)
                {
                    RevealFollowingHalfInningEnd(events);
                    return true;
                }

                if (matchEvent.EventType is MatchEventType.HalfInningEnded or MatchEventType.MatchEnded)
                    return true;
            }

            return didReveal;
        }

        /// <summary>
        /// 방금 공개한 구간에서 내 선수의 타석 종료 결과와 플레이 아웃·타점을 요약한다.
        /// </summary>
        public bool TryGetControlledPlateAppearanceSummary(
            IReadOnlyList<MatchEvent> events,
            int firstEventIndex,
            int controlledPlayerId,
            out CareerPlateAppearanceSummary summary)
        {
            ValidateEvents(events);
            if (firstEventIndex < 0 || firstEventIndex > VisibleEventCount)
                throw new ArgumentOutOfRangeException(nameof(firstEventIndex));
            if (controlledPlayerId <= 0)
                throw new ArgumentOutOfRangeException(nameof(controlledPlayerId));

            int outsOnPlay = 0;
            int runsBattedIn = 0;
            for (int index = firstEventIndex; index < VisibleEventCount; index++)
            {
                MatchEvent matchEvent = events[index];
                if (matchEvent.EventType == MatchEventType.Out)
                    outsOnPlay++;
                else if (matchEvent.EventType == MatchEventType.Score &&
                         matchEvent.BatterId == controlledPlayerId)
                    runsBattedIn++;

                if (matchEvent.EventType != MatchEventType.PlateAppearanceEnded ||
                    matchEvent.BatterId != controlledPlayerId)
                {
                    continue;
                }

                summary = new CareerPlateAppearanceSummary(
                    matchEvent.PlateAppearanceResult,
                    outsOnPlay,
                    runsBattedIn);
                return true;
            }

            summary = default;
            return false;
        }

        /// <summary>
        /// 남은 이벤트를 모두 공개해 즉시 결과 보기 상태로 전환한다.
        /// </summary>
        public void RevealAll(IReadOnlyList<MatchEvent> events)
        {
            ValidateEvents(events);
            VisibleEventCount = events.Count;
        }

        /// <summary>
        /// 현재까지 공개된 이벤트로 스코어, Count, 주자 상태를 복원한다.
        /// </summary>
        public CareerMatchPlaybackSnapshot BuildSnapshot(
            IReadOnlyList<MatchEvent> events,
            MatchDecisionRequest? pendingDecision = null)
        {
            ValidateEvents(events);
            return CareerMatchPlaybackSnapshot.Create(events, VisibleEventCount, pendingDecision);
        }

        /// <summary>
        /// 새 경기의 첫 이벤트부터 재생하도록 공개 위치를 초기화한다.
        /// </summary>
        public void Reset()
        {
            VisibleEventCount = 0;
        }

        private void RevealFollowingHalfInningEnd(IReadOnlyList<MatchEvent> events)
        {
            if (VisibleEventCount >= events.Count)
                return;
            if (events[VisibleEventCount].EventType == MatchEventType.HalfInningEnded)
                VisibleEventCount++;
        }

        private void ValidateEvents(IReadOnlyList<MatchEvent> events)
        {
            if (events == null)
                throw new ArgumentNullException(nameof(events));
            if (VisibleEventCount > events.Count)
                throw new InvalidOperationException("경기 이벤트가 이미 공개한 위치보다 짧아졌습니다.");
        }
    }

    /// <summary>선택 배속과 내 선수 자동 감속을 실제 중계 배속으로 해석한다.</summary>
    public static class CareerMatchPlaybackSpeedPolicy
    {
        /// <summary>현재 장면 소유자와 진행 방식에 맞는 실제 중계 배속을 반환한다.</summary>
        public static int Resolve(
            int configuredGameSpeed,
            CareerMatchMode mode,
            bool autoSlowOnPlayerEvent,
            bool isControlledPlayerEvent)
        {
            if (configuredGameSpeed is not (1 or 2 or 3 or 5))
                throw new ArgumentOutOfRangeException(nameof(configuredGameSpeed));

            if (isControlledPlayerEvent && autoSlowOnPlayerEvent)
                return 1;
            if (!isControlledPlayerEvent && mode == CareerMatchMode.PlayerFocusAutomatic)
                return 5;
            return configuredGameSpeed;
        }
    }

    /// <summary>
    /// 한 타석 결과를 병살·타점까지 구분해 Presentation에 전달한다.
    /// </summary>
    public readonly struct CareerPlateAppearanceSummary
    {
        public CareerPlateAppearanceSummary(
            PlateAppearanceResult result,
            int outsOnPlay,
            int runsBattedIn)
        {
            Result = result;
            OutsOnPlay = outsOnPlay;
            RunsBattedIn = runsBattedIn;
        }

        public PlateAppearanceResult Result { get; }
        public int OutsOnPlay { get; }
        public int RunsBattedIn { get; }
        public bool IsDoublePlay => Result == PlateAppearanceResult.GroundOut && OutsOnPlay >= 2;
        public bool IsSacrificeFly => Result == PlateAppearanceResult.FlyOut && RunsBattedIn > 0;
    }

    /// <summary>
    /// 공개된 이벤트만으로 복원한 한 시점의 경기 표시 상태다.
    /// </summary>
    public readonly struct CareerMatchPlaybackSnapshot
    {
        private CareerMatchPlaybackSnapshot(
            int inning,
            InningHalf half,
            int batterId,
            int pitcherId,
            int balls,
            int strikes,
            int outs,
            int awayScore,
            int homeScore,
            int firstRunnerId,
            int secondRunnerId,
            int thirdRunnerId,
            MatchEventType latestEventType,
            PlateAppearanceResult latestPlateAppearanceResult)
        {
            Inning = inning;
            Half = half;
            BatterId = batterId;
            PitcherId = pitcherId;
            Balls = balls;
            Strikes = strikes;
            Outs = outs;
            AwayScore = awayScore;
            HomeScore = homeScore;
            FirstRunnerId = firstRunnerId;
            SecondRunnerId = secondRunnerId;
            ThirdRunnerId = thirdRunnerId;
            LatestEventType = latestEventType;
            LatestPlateAppearanceResult = latestPlateAppearanceResult;
        }

        public int Inning { get; }
        public InningHalf Half { get; }
        public int BatterId { get; }
        public int PitcherId { get; }
        public int Balls { get; }
        public int Strikes { get; }
        public int Outs { get; }
        public int AwayScore { get; }
        public int HomeScore { get; }
        public int FirstRunnerId { get; }
        public int SecondRunnerId { get; }
        public int ThirdRunnerId { get; }
        public MatchEventType LatestEventType { get; }
        public PlateAppearanceResult LatestPlateAppearanceResult { get; }
        public bool HasRunnerOnFirst => FirstRunnerId != 0;
        public bool HasRunnerOnSecond => SecondRunnerId != 0;
        public bool HasRunnerOnThird => ThirdRunnerId != 0;

        /// <summary>
        /// 이벤트 스트림의 임의 공개 지점에서 표시 상태를 복원한다.
        /// </summary>
        public static CareerMatchPlaybackSnapshot Create(
            IReadOnlyList<MatchEvent> events,
            int visibleEventCount,
            MatchDecisionRequest? pendingDecision)
        {
            int inning = 1;
            InningHalf half = InningHalf.Top;
            int batterId = 0;
            int pitcherId = 0;
            int balls = 0;
            int strikes = 0;
            int outs = 0;
            int awayScore = 0;
            int homeScore = 0;
            int firstRunnerId = 0;
            int secondRunnerId = 0;
            int thirdRunnerId = 0;
            MatchEventType latestEventType = MatchEventType.Pitch;
            PlateAppearanceResult latestPlateAppearanceResult = PlateAppearanceResult.None;

            for (int index = 0; index < visibleEventCount; index++)
            {
                MatchEvent matchEvent = events[index];
                inning = matchEvent.Inning;
                half = matchEvent.Half;
                batterId = matchEvent.BatterId;
                pitcherId = matchEvent.PitcherId;
                balls = matchEvent.Balls;
                strikes = matchEvent.Strikes;
                outs = matchEvent.Outs;
                awayScore = matchEvent.AwayScore;
                homeScore = matchEvent.HomeScore;
                latestEventType = matchEvent.EventType;

                if (matchEvent.EventType == MatchEventType.PlateAppearanceEnded)
                    latestPlateAppearanceResult = matchEvent.PlateAppearanceResult;

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

            if (visibleEventCount == 0 && events.Count > 0)
            {
                MatchEvent firstEvent = events[0];
                inning = firstEvent.Inning;
                half = firstEvent.Half;
                batterId = firstEvent.BatterId;
                pitcherId = firstEvent.PitcherId;
            }

            if (pendingDecision.HasValue)
            {
                MatchDecisionRequest request = pendingDecision.Value;
                inning = request.Inning;
                half = request.Half;
                batterId = request.BatterId;
                pitcherId = request.PitcherId;
                balls = request.Balls;
                strikes = request.Strikes;
                outs = request.Outs;
                awayScore = request.AwayScore;
                homeScore = request.HomeScore;
                firstRunnerId = ResolvePendingRunner(firstRunnerId, request.HasRunnerOnFirst);
                secondRunnerId = ResolvePendingRunner(secondRunnerId, request.HasRunnerOnSecond);
                thirdRunnerId = ResolvePendingRunner(thirdRunnerId, request.HasRunnerOnThird);
            }

            return new CareerMatchPlaybackSnapshot(
                inning,
                half,
                batterId,
                pitcherId,
                balls,
                strikes,
                outs,
                awayScore,
                homeScore,
                firstRunnerId,
                secondRunnerId,
                thirdRunnerId,
                latestEventType,
                latestPlateAppearanceResult);
        }

        private static int ResolvePendingRunner(int runnerId, bool isOccupied)
        {
            if (!isOccupied)
                return 0;
            return runnerId == 0 ? -1 : runnerId;
        }

        private static void ClearRunner(
            int playerId,
            int fromBase,
            ref int firstRunnerId,
            ref int secondRunnerId,
            ref int thirdRunnerId)
        {
            switch (fromBase)
            {
                case 1:
                    firstRunnerId = 0;
                    break;
                case 2:
                    secondRunnerId = 0;
                    break;
                case 3:
                    thirdRunnerId = 0;
                    break;
                default:
                    ClearRunnerById(playerId, ref firstRunnerId, ref secondRunnerId, ref thirdRunnerId);
                    break;
            }
        }

        private static void PlaceRunner(
            int playerId,
            int toBase,
            ref int firstRunnerId,
            ref int secondRunnerId,
            ref int thirdRunnerId)
        {
            switch (toBase)
            {
                case 1:
                    firstRunnerId = playerId;
                    break;
                case 2:
                    secondRunnerId = playerId;
                    break;
                case 3:
                    thirdRunnerId = playerId;
                    break;
            }
        }

        private static void ClearRunnerById(
            int playerId,
            ref int firstRunnerId,
            ref int secondRunnerId,
            ref int thirdRunnerId)
        {
            if (firstRunnerId == playerId)
                firstRunnerId = 0;
            if (secondRunnerId == playerId)
                secondRunnerId = 0;
            if (thirdRunnerId == playerId)
                thirdRunnerId = 0;
        }
    }
}
