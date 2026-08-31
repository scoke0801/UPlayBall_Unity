using Baseball.Core.Players;
using Baseball.Simulation.Match;
using Baseball.Simulation.PlateAppearance;

namespace Baseball.Presentation.Career
{
    /// <summary>2D 경기장 안의 위치를 Unity 타입 없이 정규화해 표현한다.</summary>
    public readonly struct NormalizedFieldPoint
    {
        public NormalizedFieldPoint(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; }
        public double Y { get; }
    }

    /// <summary>하나의 플레이를 구성하는 화면 사건 종류다.</summary>
    public enum PlayResolutionCueType
    {
        BatterTake,
        BatterSwing,
        SwingAndMiss,
        Contact,
        FoulBall,
        PlateCall,
        FieldTransition,
        BattedBallFlight,
        FielderMove,
        Catch,
        BallPickup,
        FieldingError,
        Throw,
        RunnerMove,
        OutCall,
        SafeCall,
        ScoreCall,
        HomeRunCall,
        FinalResult,
        ResultHold
    }

    /// <summary>절대 시작 시각을 가져 서로 겹쳐 재생할 수 있는 단일 표현 사건이다.</summary>
    public readonly struct PlayResolutionCue
    {
        public PlayResolutionCue(
            PlayResolutionCueType type,
            double startSeconds,
            double durationSeconds,
            NormalizedFieldPoint startPoint = default,
            NormalizedFieldPoint endPoint = default,
            int playerId = 0,
            int fromBase = 0,
            int toBase = 0,
            PlayerPosition fielderPosition = PlayerPosition.DesignatedHitter,
            int revealThroughEventIndex = -1)
        {
            Type = type;
            StartSeconds = startSeconds;
            DurationSeconds = durationSeconds;
            StartPoint = startPoint;
            EndPoint = endPoint;
            PlayerId = playerId;
            FromBase = fromBase;
            ToBase = toBase;
            FielderPosition = fielderPosition;
            RevealThroughEventIndex = revealThroughEventIndex;
        }

        public PlayResolutionCueType Type { get; }
        public double StartSeconds { get; }
        public double DurationSeconds { get; }
        public double EndSeconds => StartSeconds + DurationSeconds;
        public NormalizedFieldPoint StartPoint { get; }
        public NormalizedFieldPoint EndPoint { get; }
        public int PlayerId { get; }
        public int FromBase { get; }
        public int ToBase { get; }
        public PlayerPosition FielderPosition { get; }
        public int RevealThroughEventIndex { get; }
    }

    /// <summary>한 투구의 Plate View와 Field View를 잇는 불변 Cue 묶음이다.</summary>
    public sealed class PlayResolutionSequence
    {
        public PlayResolutionSequence(
            PlayResolutionCue[] cues,
            int firstEventIndex,
            int pitchEventIndex,
            int lastEventIndex,
            int batterId,
            int pitcherId,
            int fielderId,
            PitchPlayData pitchPlay,
            BallInPlayEventData ballInPlay,
            PlateAppearanceResult finalResult,
            int outsOnPlay,
            int runsOnPlay,
            double fieldTransitionSeconds,
            double durationSeconds)
        {
            Cues = cues;
            FirstEventIndex = firstEventIndex;
            PitchEventIndex = pitchEventIndex;
            LastEventIndex = lastEventIndex;
            BatterId = batterId;
            PitcherId = pitcherId;
            FielderId = fielderId;
            PitchPlay = pitchPlay;
            BallInPlay = ballInPlay;
            FinalResult = finalResult;
            OutsOnPlay = outsOnPlay;
            RunsOnPlay = runsOnPlay;
            FieldTransitionSeconds = fieldTransitionSeconds;
            DurationSeconds = durationSeconds;
        }

        public PlayResolutionCue[] Cues { get; }
        public int FirstEventIndex { get; }
        public int PitchEventIndex { get; }
        public int LastEventIndex { get; }
        public int BatterId { get; }
        public int PitcherId { get; }
        public int FielderId { get; }
        public PitchPlayData PitchPlay { get; }
        public BallInPlayEventData BallInPlay { get; }
        public PlateAppearanceResult FinalResult { get; }
        public int OutsOnPlay { get; }
        public int RunsOnPlay { get; }
        public double FieldTransitionSeconds { get; }
        public double DurationSeconds { get; }
        public bool IsBallInPlay => BallInPlay.HasValue;
    }

    /// <summary>SequenceBuilder가 사용하는 Unity 비의존 연출 시간 묶음이다.</summary>
    public readonly struct PlayResolutionTiming
    {
        public PlayResolutionTiming(
            double batterResponse,
            double impactHold,
            double plateCallHold,
            double fieldTransition,
            double groundBallFlight,
            double lineDriveFlight,
            double flyBallFlight,
            double fielderMove,
            double pickupHold,
            double throwFlight,
            double callHold,
            double resultHold)
        {
            BatterResponse = batterResponse;
            ImpactHold = impactHold;
            PlateCallHold = plateCallHold;
            FieldTransition = fieldTransition;
            GroundBallFlight = groundBallFlight;
            LineDriveFlight = lineDriveFlight;
            FlyBallFlight = flyBallFlight;
            FielderMove = fielderMove;
            PickupHold = pickupHold;
            ThrowFlight = throwFlight;
            CallHold = callHold;
            ResultHold = resultHold;
        }

        public double BatterResponse { get; }
        public double ImpactHold { get; }
        public double PlateCallHold { get; }
        public double FieldTransition { get; }
        public double GroundBallFlight { get; }
        public double LineDriveFlight { get; }
        public double FlyBallFlight { get; }
        public double FielderMove { get; }
        public double PickupHold { get; }
        public double ThrowFlight { get; }
        public double CallHold { get; }
        public double ResultHold { get; }
    }
}
