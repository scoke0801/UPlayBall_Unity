using System;
using System.Collections.Generic;
using Baseball.Core.Players;
using Baseball.Simulation.Match;
using Baseball.Simulation.PlateAppearance;

namespace Baseball.Presentation.Career
{
    /// <summary>확정된 MatchEvent 구간을 결과를 스포일러하지 않는 Plate/Field Cue로 변환한다.</summary>
    public sealed class PlayResolutionSequenceBuilder
    {
        private readonly List<PlayResolutionCue> _cues = new(32);
        private readonly List<MatchEventIndex> _runnerAdvances = new(8);
        private readonly List<MatchEventIndex> _scores = new(4);
        private readonly List<MatchEventIndex> _outs = new(3);
        private readonly List<MatchEventIndex> _runnerOuts = new(2);
        private readonly Dictionary<int, double> _runnerArrivalTimes = new(8);

        public bool TryBuild(
            IReadOnlyList<MatchEvent> events,
            int firstEventIndex,
            PlayResolutionPresentationConfig config,
            out PlayResolutionSequence sequence)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (firstEventIndex < 0 || firstEventIndex > events.Count)
                throw new ArgumentOutOfRangeException(nameof(firstEventIndex));

            ResetBuffers();
            int pitchEventIndex = FindPitchEvent(events, firstEventIndex);
            if (pitchEventIndex < 0)
            {
                sequence = null;
                return false;
            }

            MatchEvent pitchEvent = events[pitchEventIndex];
            PitchPlayData pitchPlay = pitchEvent.PitchPlayData;
            int lastEventIndex = pitchEventIndex;
            int contactEventIndex = -1;
            int fieldingEventIndex = -1;
            int fielderId = 0;
            int outsOnPlay = 0;
            int runsOnPlay = 0;
            PlateAppearanceResult finalResult = PlateAppearanceResult.None;
            BallInPlayEventData ballInPlay = default;

            for (int index = pitchEventIndex + 1; index < events.Count; index++)
            {
                MatchEvent current = events[index];
                if (current.EventType == MatchEventType.Pitch)
                    break;

                lastEventIndex = index;
                if (current.BallInPlayData.HasValue)
                    ballInPlay = current.BallInPlayData;
                switch (current.EventType)
                {
                    case MatchEventType.Contact:
                        contactEventIndex = index;
                        break;
                    case MatchEventType.FieldingPlayStarted:
                        fieldingEventIndex = index;
                        fielderId = current.PlayerId;
                        break;
                    case MatchEventType.RunnerAdvance:
                        _runnerAdvances.Add(new MatchEventIndex(current, index));
                        break;
                    case MatchEventType.Score:
                        runsOnPlay++;
                        _scores.Add(new MatchEventIndex(current, index));
                        break;
                    case MatchEventType.Out:
                        outsOnPlay++;
                        _outs.Add(new MatchEventIndex(current, index));
                        break;
                    case MatchEventType.RunnerThrownOut:
                        _runnerOuts.Add(new MatchEventIndex(current, index));
                        break;
                    case MatchEventType.PlateAppearanceEnded:
                        finalResult = current.PlateAppearanceResult;
                        index = events.Count;
                        break;
                }

                if (finalResult != PlateAppearanceResult.None)
                    break;
            }

            if (ballInPlay.Fielding.HasValue)
                fielderId = ballInPlay.Fielding.FielderId;

            PlayResolutionTiming timing = config.CreateTiming();
            double fieldTransitionSeconds = double.PositiveInfinity;
            double durationSeconds;
            if (pitchEvent.PitchResult == PitchResult.InPlay && ballInPlay.HasValue)
            {
                durationSeconds = BuildBallInPlay(
                    pitchEvent,
                    pitchEventIndex,
                    contactEventIndex,
                    fieldingEventIndex,
                    lastEventIndex,
                    finalResult,
                    ballInPlay,
                    config,
                    timing,
                    out fieldTransitionSeconds);
            }
            else
            {
                durationSeconds = BuildPlateResult(
                    pitchEvent,
                    pitchEventIndex,
                    lastEventIndex,
                    finalResult,
                    timing);
            }

            sequence = new PlayResolutionSequence(
                _cues.ToArray(),
                firstEventIndex,
                pitchEventIndex,
                lastEventIndex,
                pitchEvent.BatterId,
                pitchEvent.PitcherId,
                fielderId,
                pitchPlay,
                ballInPlay,
                finalResult,
                outsOnPlay,
                runsOnPlay,
                fieldTransitionSeconds,
                durationSeconds);
            return true;
        }

        private double BuildPlateResult(
            in MatchEvent pitchEvent,
            int pitchEventIndex,
            int lastEventIndex,
            PlateAppearanceResult finalResult,
            in PlayResolutionTiming timing)
        {
            PitchPlayData play = pitchEvent.PitchPlayData;
            PlayResolutionCueType response = play.Swing.DidSwing
                ? PlayResolutionCueType.BatterSwing
                : PlayResolutionCueType.BatterTake;
            Add(response, 0d, timing.BatterResponse);

            double callStart = timing.BatterResponse;
            if (pitchEvent.PitchResult == PitchResult.SwingingStrike)
            {
                Add(PlayResolutionCueType.SwingAndMiss, timing.BatterResponse * 0.42d,
                    timing.BatterResponse * 0.9d);
                callStart += timing.BatterResponse * 0.35d;
            }
            else if (pitchEvent.PitchResult == PitchResult.Foul)
            {
                double contactStart = timing.BatterResponse * 0.48d;
                Add(PlayResolutionCueType.Contact, contactStart, timing.ImpactHold);
                Add(
                    PlayResolutionCueType.FoulBall,
                    contactStart + timing.ImpactHold * 0.45d,
                    timing.PlateCallHold,
                    PlayResolutionFieldLayout.Home,
                    new NormalizedFieldPoint(
                        play.Contact.SprayAngleDegrees < 0d ? 0.08d : 0.92d,
                        0.72d));
                callStart = contactStart + timing.ImpactHold + timing.PlateCallHold * 0.68d;
            }

            Add(
                PlayResolutionCueType.PlateCall,
                callStart,
                timing.PlateCallHold,
                revealThroughEventIndex: pitchEventIndex);
            double finalStart = callStart + timing.PlateCallHold;
            int finalRevealIndex = finalResult == PlateAppearanceResult.None
                ? pitchEventIndex
                : lastEventIndex;
            Add(
                PlayResolutionCueType.FinalResult,
                finalStart,
                timing.CallHold,
                revealThroughEventIndex: finalRevealIndex);
            Add(PlayResolutionCueType.ResultHold, finalStart + timing.CallHold, timing.ResultHold);
            return finalStart + timing.CallHold + timing.ResultHold;
        }

        private double BuildBallInPlay(
            in MatchEvent pitchEvent,
            int pitchEventIndex,
            int contactEventIndex,
            int fieldingEventIndex,
            int lastEventIndex,
            PlateAppearanceResult finalResult,
            in BallInPlayEventData ballInPlay,
            PlayResolutionPresentationConfig config,
            in PlayResolutionTiming timing,
            out double fieldTransitionSeconds)
        {
            Add(PlayResolutionCueType.BatterSwing, 0d, timing.BatterResponse);
            double contactStart = timing.BatterResponse * 0.5d;
            Add(
                PlayResolutionCueType.Contact,
                contactStart,
                timing.ImpactHold,
                revealThroughEventIndex: contactEventIndex >= 0 ? contactEventIndex : pitchEventIndex);

            fieldTransitionSeconds = contactStart + timing.ImpactHold;
            Add(
                PlayResolutionCueType.FieldTransition,
                fieldTransitionSeconds,
                timing.FieldTransition,
                revealThroughEventIndex: fieldingEventIndex >= 0
                    ? fieldingEventIndex
                    : contactEventIndex >= 0 ? contactEventIndex : pitchEventIndex);

            BattedBallDescriptor ball = ballInPlay.BattedBall;
            FieldingPlayOutcome fielding = ballInPlay.Fielding;
            NormalizedFieldPoint ballTarget = PlayResolutionFieldLayout.GetBattedBallTarget(ball);
            double flightStart = fieldTransitionSeconds + timing.FieldTransition;
            double flightSeconds = config.ResolveBattedBallFlightSeconds(ball);
            double flightEnd = flightStart + flightSeconds;
            Add(
                PlayResolutionCueType.BattedBallFlight,
                flightStart,
                flightSeconds,
                PlayResolutionFieldLayout.Home,
                ballTarget);

            if (!ball.IsHomeRun && fielding.HasValue)
            {
                double fielderDuration = Math.Min(timing.FielderMove, flightSeconds * 0.88d);
                Add(
                    PlayResolutionCueType.FielderMove,
                    flightStart + 0.04d,
                    fielderDuration,
                    PlayResolutionFieldLayout.GetFielderPoint(fielding.FielderPosition),
                    ballTarget,
                    fielderPosition: fielding.FielderPosition);
            }

            double defenseEnd = BuildFieldingResolution(
                finalResult,
                ballInPlay,
                ballTarget,
                flightEnd,
                timing);
            double runnerStart = IsCaughtOut(finalResult)
                ? defenseEnd - timing.CallHold
                : flightStart + 0.08d;
            double runnerEnd = BuildRunnerResolution(
                pitchEvent.BatterId,
                finalResult,
                ballInPlay,
                runnerStart,
                defenseEnd,
                timing);
            double scoreEnd = BuildScoreCalls(defenseEnd, timing);
            double finalStart = Math.Max(defenseEnd, Math.Max(runnerEnd, scoreEnd)) + 0.08d;
            Add(
                PlayResolutionCueType.FinalResult,
                finalStart,
                timing.CallHold,
                revealThroughEventIndex: lastEventIndex);
            Add(PlayResolutionCueType.ResultHold, finalStart + timing.CallHold, timing.ResultHold);
            return finalStart + timing.CallHold + timing.ResultHold;
        }

        private double BuildFieldingResolution(
            PlateAppearanceResult finalResult,
            in BallInPlayEventData play,
            NormalizedFieldPoint ballTarget,
            double flightEnd,
            in PlayResolutionTiming timing)
        {
            FieldingPlayOutcome fielding = play.Fielding;
            if (play.BattedBall.IsHomeRun || finalResult == PlateAppearanceResult.HomeRun)
            {
                Add(PlayResolutionCueType.HomeRunCall, flightEnd, timing.CallHold);
                return flightEnd + timing.CallHold;
            }

            if (fielding.FailureType is FieldingFailureType.FieldingError or FieldingFailureType.ThrowingError)
            {
                Add(
                    PlayResolutionCueType.FieldingError,
                    flightEnd,
                    timing.PickupHold,
                    ballTarget,
                    ballTarget,
                    fielderPosition: fielding.FielderPosition);
                return flightEnd + timing.PickupHold;
            }

            if (IsCaughtOut(finalResult))
            {
                Add(
                    PlayResolutionCueType.Catch,
                    flightEnd,
                    timing.PickupHold,
                    ballTarget,
                    ballTarget,
                    fielderPosition: fielding.FielderPosition);
                Add(
                    PlayResolutionCueType.OutCall,
                    flightEnd + timing.PickupHold,
                    timing.CallHold,
                    revealThroughEventIndex: GetOutEventIndex(0));
                return flightEnd + timing.PickupHold + timing.CallHold;
            }

            Add(
                PlayResolutionCueType.BallPickup,
                flightEnd,
                timing.PickupHold,
                ballTarget,
                ballTarget,
                fielderPosition: fielding.FielderPosition);
            double throwStart = flightEnd + timing.PickupHold;
            if (fielding.IsDoublePlay)
            {
                double firstThrowEnd = AddThrow(
                    ballTarget,
                    2,
                    throwStart,
                    timing.ThrowFlight,
                    timing,
                    GetOutEventIndex(0));
                double secondThrowStart = firstThrowEnd + timing.CallHold * 0.45d;
                double secondThrowEnd = AddThrow(
                    PlayResolutionFieldLayout.GetBasePoint(2),
                    1,
                    secondThrowStart,
                    timing.ThrowFlight,
                    timing,
                    GetOutEventIndex(1));
                return secondThrowEnd + timing.CallHold;
            }

            if (finalResult is PlateAppearanceResult.GroundOut or PlateAppearanceResult.SacrificeBunt)
            {
                double throwEnd = AddThrow(
                    ballTarget,
                    1,
                    throwStart,
                    timing.ThrowFlight,
                    timing,
                    GetOutEventIndex(0));
                return throwEnd + timing.CallHold;
            }

            if (finalResult == PlateAppearanceResult.FieldersChoice)
            {
                double throwEnd = AddThrow(
                    ballTarget,
                    2,
                    throwStart,
                    timing.ThrowFlight,
                    timing,
                    GetOutEventIndex(0));
                return throwEnd + timing.CallHold;
            }

            double latest = throwStart;
            for (int index = 0; index < _runnerOuts.Count; index++)
            {
                MatchEvent runnerOut = _runnerOuts[index].Event;
                latest = AddThrow(
                    ballTarget,
                    runnerOut.ToBase,
                    latest,
                    timing.ThrowFlight,
                    timing,
                    FindOutEventIndex(runnerOut.PlayerId)) + timing.CallHold;
            }
            return Math.Max(flightEnd + timing.PickupHold, latest);
        }

        private double BuildRunnerResolution(
            int batterId,
            PlateAppearanceResult finalResult,
            in BallInPlayEventData play,
            double runnerStart,
            double defenseEnd,
            in PlayResolutionTiming timing)
        {
            double latest = runnerStart;
            for (int index = 0; index < _runnerAdvances.Count; index++)
            {
                MatchEvent current = _runnerAdvances[index].Event;
                double duration = ResolveRunnerDuration(current.FromBase, current.ToBase);
                AddRunnerMove(_runnerAdvances[index], runnerStart, duration, timing.CallHold, false);
                latest = Math.Max(latest, runnerStart + duration);
            }

            for (int index = 0; index < _runnerOuts.Count; index++)
            {
                MatchEvent current = _runnerOuts[index].Event;
                if (_runnerArrivalTimes.ContainsKey(current.PlayerId))
                    continue;
                double duration = Math.Max(0.3d, defenseEnd - runnerStart - timing.CallHold * 0.5d);
                AddRunnerMove(_runnerOuts[index], runnerStart, duration, timing.CallHold, true);
                latest = Math.Max(latest, runnerStart + duration);
            }

            bool hasBatterRoute = _runnerArrivalTimes.ContainsKey(batterId);
            if (!hasBatterRoute &&
                finalResult is PlateAppearanceResult.GroundOut or PlateAppearanceResult.SacrificeBunt)
            {
                double duration = Math.Max(0.35d, defenseEnd - runnerStart - timing.CallHold * 0.35d);
                Add(
                    PlayResolutionCueType.RunnerMove,
                    runnerStart,
                    duration,
                    PlayResolutionFieldLayout.Home,
                    PlayResolutionFieldLayout.GetBasePoint(1),
                    batterId,
                    0,
                    1);
                _runnerArrivalTimes[batterId] = runnerStart + duration;
                latest = Math.Max(latest, runnerStart + duration);
            }

            if (play.Fielding.IsDoublePlay && _outs.Count >= 2)
            {
                int forcedRunnerId = _outs[0].Event.PlayerId;
                if (!_runnerArrivalTimes.ContainsKey(forcedRunnerId))
                {
                    double duration = Math.Max(0.3d, defenseEnd - runnerStart - timing.CallHold);
                    Add(
                        PlayResolutionCueType.RunnerMove,
                        runnerStart,
                        duration,
                        PlayResolutionFieldLayout.GetBasePoint(1),
                        PlayResolutionFieldLayout.GetBasePoint(2),
                        forcedRunnerId,
                        1,
                        2);
                    latest = Math.Max(latest, runnerStart + duration);
                }
                if (!_runnerArrivalTimes.ContainsKey(batterId))
                {
                    double duration = Math.Max(0.4d, defenseEnd - runnerStart - timing.CallHold * 0.35d);
                    Add(
                        PlayResolutionCueType.RunnerMove,
                        runnerStart,
                        duration,
                        PlayResolutionFieldLayout.Home,
                        PlayResolutionFieldLayout.GetBasePoint(1),
                        batterId,
                        0,
                        1);
                    latest = Math.Max(latest, runnerStart + duration);
                }
            }
            return latest;
        }

        private double BuildScoreCalls(double fallbackStart, in PlayResolutionTiming timing)
        {
            double latest = fallbackStart;
            for (int index = 0; index < _scores.Count; index++)
            {
                MatchEvent score = _scores[index].Event;
                double start = _runnerArrivalTimes.TryGetValue(score.PlayerId, out double arrival)
                    ? arrival
                    : fallbackStart;
                Add(
                    PlayResolutionCueType.ScoreCall,
                    start,
                    timing.CallHold,
                    playerId: score.PlayerId,
                    fromBase: score.FromBase,
                    toBase: 4,
                    revealThroughEventIndex: _scores[index].Index);
                latest = Math.Max(latest, start + timing.CallHold);
            }
            return latest;
        }

        private double AddThrow(
            NormalizedFieldPoint startPoint,
            int targetBase,
            double startSeconds,
            double durationSeconds,
            in PlayResolutionTiming timing,
            int revealThroughEventIndex)
        {
            NormalizedFieldPoint target = PlayResolutionFieldLayout.GetBasePoint(targetBase);
            Add(
                PlayResolutionCueType.Throw,
                startSeconds,
                durationSeconds,
                startPoint,
                target,
                toBase: targetBase);
            double throwEnd = startSeconds + durationSeconds;
            Add(
                PlayResolutionCueType.OutCall,
                throwEnd,
                timing.CallHold,
                toBase: targetBase,
                revealThroughEventIndex: revealThroughEventIndex);
            return throwEnd;
        }

        private void AddRunnerMove(
            in MatchEventIndex indexedEvent,
            double start,
            double duration,
            double callHoldSeconds,
            bool isOut)
        {
            MatchEvent current = indexedEvent.Event;
            Add(
                PlayResolutionCueType.RunnerMove,
                start,
                duration,
                PlayResolutionFieldLayout.GetBasePoint(current.FromBase),
                PlayResolutionFieldLayout.GetBasePoint(current.ToBase),
                current.PlayerId,
                current.FromBase,
                current.ToBase);
            _runnerArrivalTimes[current.PlayerId] = start + duration;
            if (!isOut && current.ToBase is >= 1 and <= 3)
            {
                Add(
                    PlayResolutionCueType.SafeCall,
                    start + duration,
                    callHoldSeconds * 0.66d,
                    playerId: current.PlayerId,
                    toBase: current.ToBase,
                    revealThroughEventIndex: indexedEvent.Index);
            }
        }

        private int GetOutEventIndex(int index)
        {
            return index >= 0 && index < _outs.Count ? _outs[index].Index : -1;
        }

        private int FindOutEventIndex(int playerId)
        {
            for (int index = 0; index < _outs.Count; index++)
            {
                if (_outs[index].Event.PlayerId == playerId)
                    return _outs[index].Index;
            }
            return -1;
        }

        private void Add(
            PlayResolutionCueType type,
            double start,
            double duration,
            NormalizedFieldPoint startPoint = default,
            NormalizedFieldPoint endPoint = default,
            int playerId = 0,
            int fromBase = 0,
            int toBase = 0,
            PlayerPosition fielderPosition = PlayerPosition.DesignatedHitter,
            int revealThroughEventIndex = -1)
        {
            _cues.Add(new PlayResolutionCue(
                type,
                start,
                duration,
                startPoint,
                endPoint,
                playerId,
                fromBase,
                toBase,
                fielderPosition,
                revealThroughEventIndex));
        }

        private void ResetBuffers()
        {
            _cues.Clear();
            _runnerAdvances.Clear();
            _scores.Clear();
            _outs.Clear();
            _runnerOuts.Clear();
            _runnerArrivalTimes.Clear();
        }

        private static int FindPitchEvent(IReadOnlyList<MatchEvent> events, int firstEventIndex)
        {
            for (int index = firstEventIndex; index < events.Count; index++)
            {
                MatchEvent current = events[index];
                if (current.EventType == MatchEventType.Pitch && current.PitchPlayData.HasValue)
                    return index;
            }
            return -1;
        }

        private static bool IsCaughtOut(PlateAppearanceResult result)
        {
            return result is PlateAppearanceResult.FlyOut or PlateAppearanceResult.BuntPopOut;
        }

        private static double ResolveRunnerDuration(int fromBase, int toBase)
        {
            if (toBase == 4)
                return fromBase == 0 ? 1.5d : 0.72d;
            int distance = Math.Max(1, toBase - fromBase);
            return 0.48d + (distance - 1) * 0.28d;
        }

        private readonly struct MatchEventIndex
        {
            public MatchEventIndex(in MatchEvent matchEvent, int index)
            {
                Event = matchEvent;
                Index = index;
            }

            public MatchEvent Event { get; }
            public int Index { get; }
        }
    }
}
