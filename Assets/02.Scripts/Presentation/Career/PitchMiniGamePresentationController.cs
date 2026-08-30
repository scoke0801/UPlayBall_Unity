using System;
using Baseball.Core.Players;
using Baseball.Simulation.Match;

namespace Baseball.Presentation.Career
{
    /// <summary>투수 미니게임의 입력과 결과 재생 상태를 화면 요소와 분리해 소유한다.</summary>
    public sealed class PitchMiniGamePresentationController
    {
        private double _phaseElapsedSeconds;
        private double _flightSeconds;

        public PitchMiniGamePresentationState State { get; private set; } =
            PitchMiniGamePresentationState.NextPitchReady;

        public PitchSelectionRequest Request { get; private set; }
        public PitchType SelectedPitch { get; private set; }
        public PlatePoint TargetPoint { get; private set; }
        public PitchingApproach Approach { get; private set; }
        public PitchPlayData ResolvedPlay { get; private set; }
        public int ResolvedEventIndex { get; private set; } = -1;
        public double FlightProgress01 { get; private set; }
        public bool HasRequest { get; private set; }
        public bool HasResolvedPlay => ResolvedPlay.HasValue;
        public bool IsInputUnlocked => State is PitchMiniGamePresentationState.PrePitchReady or
            PitchMiniGamePresentationState.PitchSelection or
            PitchMiniGamePresentationState.TargetAiming or
            PitchMiniGamePresentationState.StrategySelection;
        public bool IsPresentationActive => State is PitchMiniGamePresentationState.PitchConfirmed or
            PitchMiniGamePresentationState.Windup or
            PitchMiniGamePresentationState.BallInFlight or
            PitchMiniGamePresentationState.PlateArrival or
            PitchMiniGamePresentationState.BatterReaction or
            PitchMiniGamePresentationState.PitchResult;
        public bool IsStageVisible => HasRequest && State != PitchMiniGamePresentationState.NextPitchReady;

        /// <summary>새 투구 요청을 준비 상태로 열고 이전 투구의 표현 상태를 버린다.</summary>
        public bool EnsureRequest(in PitchSelectionRequest request)
        {
            if (HasRequest && Request.RequestId == request.RequestId)
                return false;

            Request = request;
            SelectedPitch = request.SuggestedPitch.PitchType;
            TargetPoint = request.SuggestedPitch.TargetPoint;
            Approach = request.SuggestedPitch.Approach;
            ResolvedPlay = default;
            ResolvedEventIndex = -1;
            FlightProgress01 = 0d;
            _phaseElapsedSeconds = 0d;
            _flightSeconds = 0d;
            HasRequest = true;
            State = PitchMiniGamePresentationState.PrePitchReady;
            return true;
        }

        public void BeginSelection()
        {
            if (State == PitchMiniGamePresentationState.PrePitchReady)
                SetState(PitchMiniGamePresentationState.PitchSelection);
        }

        public void SelectPitch(PitchType pitchType)
        {
            EnsureInputUnlocked();
            SelectedPitch = pitchType;
            SetState(PitchMiniGamePresentationState.PitchSelection);
        }

        public void SetTarget(PlatePoint targetPoint)
        {
            EnsureInputUnlocked();
            TargetPoint = targetPoint;
            SetState(PitchMiniGamePresentationState.TargetAiming);
        }

        public void SelectApproach(PitchingApproach approach)
        {
            EnsureInputUnlocked();
            Approach = approach;
            SetState(PitchMiniGamePresentationState.StrategySelection);
        }

        public void ReturnToReady()
        {
            EnsureInputUnlocked();
            SetState(PitchMiniGamePresentationState.PrePitchReady);
        }

        /// <summary>입력을 잠그며 같은 투구의 중복 제출을 막는다.</summary>
        public PitchSelectionCommand Confirm()
        {
            EnsureInputUnlocked();
            SetState(PitchMiniGamePresentationState.PitchConfirmed);
            return new PitchSelectionCommand(Request.RequestId, SelectedPitch, TargetPoint, Approach);
        }

        /// <summary>시뮬레이션이 확정한 Descriptor를 받아 와인드업부터 재생한다.</summary>
        public void BeginResolvedPitch(
            int eventIndex,
            in PitchPlayData play,
            PitchTrajectoryPresentationConfig config)
        {
            if (State != PitchMiniGamePresentationState.PitchConfirmed)
                throw new InvalidOperationException("투구 확정 상태에서만 실제 궤적을 시작할 수 있습니다.");
            if (!play.HasValue || play.PitchSelection.RequestId != Request.RequestId)
                throw new ArgumentException("현재 투구 요청과 일치하는 결과가 아닙니다.", nameof(play));
            if (eventIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(eventIndex));
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            ResolvedPlay = play;
            ResolvedEventIndex = eventIndex;
            _flightSeconds = config.ResolveFlightSeconds(play.Pitch.PlateArrivalMilliseconds);
            FlightProgress01 = 0d;
            SetState(PitchMiniGamePresentationState.Windup);
        }

        /// <summary>재생 시계가 공급한 시간만 소비해 일시정지 중 진행되지 않게 한다.</summary>
        public bool Tick(double deltaSeconds, PitchTrajectoryPresentationConfig config)
        {
            if (!IsPresentationActive || State == PitchMiniGamePresentationState.PitchConfirmed)
                return false;
            if (deltaSeconds < 0d || double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds))
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            _phaseElapsedSeconds += deltaSeconds;
            PitchMiniGamePresentationState previous = State;
            switch (State)
            {
                case PitchMiniGamePresentationState.Windup:
                    if (_phaseElapsedSeconds >= config.WindupSeconds)
                        SetState(PitchMiniGamePresentationState.BallInFlight);
                    break;
                case PitchMiniGamePresentationState.BallInFlight:
                    double flightElapsed = _phaseElapsedSeconds - config.ReleaseEmphasisSeconds;
                    FlightProgress01 = flightElapsed <= 0d
                        ? 0d
                        : Clamp01(flightElapsed / _flightSeconds);
                    if (FlightProgress01 >= 1d)
                        SetState(PitchMiniGamePresentationState.PlateArrival);
                    break;
                case PitchMiniGamePresentationState.PlateArrival:
                    if (_phaseElapsedSeconds >= config.PlateArrivalHoldSeconds)
                        SetState(PitchMiniGamePresentationState.BatterReaction);
                    break;
                case PitchMiniGamePresentationState.BatterReaction:
                    if (_phaseElapsedSeconds >= config.BatterReactionSeconds)
                        SetState(PitchMiniGamePresentationState.PitchResult);
                    break;
                case PitchMiniGamePresentationState.PitchResult:
                    if (_phaseElapsedSeconds >= config.ResultHoldSeconds)
                        SetState(PitchMiniGamePresentationState.NextPitchReady);
                    break;
            }

            return previous != State;
        }

        public double GetPhaseProgress(double durationSeconds)
        {
            if (durationSeconds <= 0d)
                return 1d;
            return Clamp01(_phaseElapsedSeconds / durationSeconds);
        }

        public void CancelConfirmedPitch()
        {
            if (State == PitchMiniGamePresentationState.PitchConfirmed)
                SetState(PitchMiniGamePresentationState.StrategySelection);
        }

        public void Complete()
        {
            HasRequest = false;
            ResolvedPlay = default;
            ResolvedEventIndex = -1;
            FlightProgress01 = 0d;
            _phaseElapsedSeconds = 0d;
            _flightSeconds = 0d;
            State = PitchMiniGamePresentationState.NextPitchReady;
        }

        private void SetState(PitchMiniGamePresentationState state)
        {
            State = state;
            _phaseElapsedSeconds = 0d;
            if (state == PitchMiniGamePresentationState.BallInFlight)
                FlightProgress01 = 0d;
            else if (state is PitchMiniGamePresentationState.PlateArrival or
                     PitchMiniGamePresentationState.BatterReaction or
                     PitchMiniGamePresentationState.PitchResult)
                FlightProgress01 = 1d;
        }

        private void EnsureInputUnlocked()
        {
            if (!HasRequest || !IsInputUnlocked)
                throw new InvalidOperationException("현재는 투구 입력을 변경할 수 없습니다.");
        }

        private static double Clamp01(double value)
        {
            if (value < 0d) return 0d;
            if (value > 1d) return 1d;
            return value;
        }
    }

    /// <summary>투수 미니게임에서 사용자 입력과 결과 표현의 명시적 진행 단계다.</summary>
    public enum PitchMiniGamePresentationState
    {
        PrePitchReady,
        PitchSelection,
        TargetAiming,
        StrategySelection,
        PitchConfirmed,
        Windup,
        BallInFlight,
        PlateArrival,
        BatterReaction,
        PitchResult,
        NextPitchReady
    }
}
