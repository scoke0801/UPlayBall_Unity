using System;
using System.Collections.Generic;
using Baseball.Core.Players;
using Baseball.Simulation.PlateAppearance;

namespace Baseball.Simulation.Match
{
    public enum MiniGameInterventionScope
    {
        AllInvolvement = 0,
        KeyMoments = 1,
        ManualIntervention = 2
    }

    public enum PitchPlayState
    {
        AwaitingPitchDecision = 0,
        PitchPrepared = 1,
        PitchInFlight = 2,
        AwaitingBatterAction = 3,
        ResolvingPitch = 4,
        ResolvingBallInPlay = 5,
        PitchCompleted = 6,
        PlateAppearanceCompleted = 7
    }

    /// <summary>Unity 좌표와 무관한 홈플레이트 정규화 좌표다.</summary>
    public readonly struct PlatePoint : IEquatable<PlatePoint>
    {
        public PlatePoint(double x, double y)
        {
            if (double.IsNaN(x) || double.IsInfinity(x))
                throw new ArgumentOutOfRangeException(nameof(x));
            if (double.IsNaN(y) || double.IsInfinity(y))
                throw new ArgumentOutOfRangeException(nameof(y));
            X = x;
            Y = y;
        }

        public double X { get; }
        public double Y { get; }
        public bool Equals(PlatePoint other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object obj) => obj is PlatePoint other && Equals(other);
        public override int GetHashCode() => unchecked(X.GetHashCode() * 397 ^ Y.GetHashCode());
    }

    /// <summary>현재 구종에서 플레이어가 판단할 제구 예상 타원을 표현한다.</summary>
    public readonly struct CommandEllipse
    {
        public CommandEllipse(double radiusX, double radiusY, double rotationDegrees)
        {
            RadiusX = radiusX;
            RadiusY = radiusY;
            RotationDegrees = rotationDegrees;
        }

        public double RadiusX { get; }
        public double RadiusY { get; }
        public double RotationDegrees { get; }
    }

    /// <summary>구종 선택 UI와 AI가 함께 소비하는 현재 구종 정보다.</summary>
    public readonly struct PitchOption
    {
        public PitchOption(
            PitchType pitchType,
            int proficiency,
            bool isPrimary,
            double minimumVelocityMph,
            double maximumVelocityMph,
            double horizontalBreak,
            double verticalBreak,
            double fatigueCost,
            CommandEllipse commandEllipse)
        {
            PitchType = pitchType;
            Proficiency = proficiency;
            IsPrimary = isPrimary;
            MinimumVelocityMph = minimumVelocityMph;
            MaximumVelocityMph = maximumVelocityMph;
            HorizontalBreak = horizontalBreak;
            VerticalBreak = verticalBreak;
            FatigueCost = fatigueCost;
            CommandEllipse = commandEllipse;
        }

        public PitchType PitchType { get; }
        public int Proficiency { get; }
        public bool IsPrimary { get; }
        public double MinimumVelocityMph { get; }
        public double MaximumVelocityMph { get; }
        public double HorizontalBreak { get; }
        public double VerticalBreak { get; }
        public double FatigueCost { get; }
        public CommandEllipse CommandEllipse { get; }
    }

    /// <summary>투수가 제출하는 구종·목표 위치·승부 방침 명령이다.</summary>
    public readonly struct PitchSelectionCommand
    {
        public PitchSelectionCommand(
            int requestId,
            PitchType pitchType,
            PlatePoint targetPoint,
            PitchingApproach approach = PitchingApproach.Balanced)
        {
            if (requestId < 0) throw new ArgumentOutOfRangeException(nameof(requestId));
            if (!Enum.IsDefined(typeof(PitchType), pitchType))
                throw new ArgumentOutOfRangeException(nameof(pitchType));
            if (!Enum.IsDefined(typeof(PitchingApproach), approach))
                throw new ArgumentOutOfRangeException(nameof(approach));
            RequestId = requestId;
            PitchType = pitchType;
            TargetPoint = targetPoint;
            Approach = approach;
        }

        public int RequestId { get; }
        public PitchType PitchType { get; }
        public PlatePoint TargetPoint { get; }
        public PitchingApproach Approach { get; }
    }

    /// <summary>투구 시작 시 고정되어 화면과 판정기가 함께 쓰는 실제 궤적 데이터다.</summary>
    public readonly struct PitchFlightDescriptor
    {
        public PitchFlightDescriptor(
            PitchType pitchType,
            PlatePoint releasePoint,
            PlatePoint targetPoint,
            PlatePoint platePoint,
            double velocityMph,
            double horizontalBreak,
            double verticalBreak,
            double breakStartTime01,
            double plateArrivalMilliseconds,
            double quality,
            bool isHitByPitch)
        {
            PitchType = pitchType;
            ReleasePoint = releasePoint;
            TargetPoint = targetPoint;
            PlatePoint = platePoint;
            VelocityMph = velocityMph;
            HorizontalBreak = horizontalBreak;
            VerticalBreak = verticalBreak;
            BreakStartTime01 = breakStartTime01;
            PlateArrivalMilliseconds = plateArrivalMilliseconds;
            Quality = quality;
            IsHitByPitch = isHitByPitch;
        }

        public PitchType PitchType { get; }
        public PlatePoint ReleasePoint { get; }
        public PlatePoint TargetPoint { get; }
        public PlatePoint PlatePoint { get; }
        public double VelocityMph { get; }
        public double HorizontalBreak { get; }
        public double VerticalBreak { get; }
        public double BreakStartTime01 { get; }
        public double PlateArrivalMilliseconds { get; }
        public double Quality { get; }
        public bool IsHitByPitch { get; }
        public bool IsStrike => Math.Abs(PlatePoint.X) <= 1d && Math.Abs(PlatePoint.Y) <= 1d;
    }

    /// <summary>한 투구의 구종과 목표 지점을 요청하는 순수 시뮬레이션 데이터다.</summary>
    public readonly struct PitchSelectionRequest
    {
        private readonly PitchOption[] _availablePitches;
        private readonly PitchType[] _recentPitchSequence;

        public PitchSelectionRequest(
            int requestId,
            int plateAppearanceIndex,
            int matchId,
            int inning,
            InningHalf half,
            int batterId,
            int pitcherId,
            int pitchNumber,
            int balls,
            int strikes,
            int outs,
            int awayScore,
            int homeScore,
            BaseStateSnapshot bases,
            double currentFatigue,
            LeverageTier leverage,
            PitchOption[] availablePitches,
            PitchType[] recentPitchSequence,
            PitchSelectionCommand suggestedPitch)
        {
            RequestId = requestId;
            PlateAppearanceIndex = plateAppearanceIndex;
            MatchId = matchId;
            Inning = inning;
            Half = half;
            BatterId = batterId;
            PitcherId = pitcherId;
            PitchNumber = pitchNumber;
            Balls = balls;
            Strikes = strikes;
            Outs = outs;
            AwayScore = awayScore;
            HomeScore = homeScore;
            Bases = bases;
            CurrentFatigue = currentFatigue;
            Leverage = leverage;
            _availablePitches = availablePitches == null
                ? Array.Empty<PitchOption>()
                : (PitchOption[])availablePitches.Clone();
            _recentPitchSequence = recentPitchSequence == null
                ? Array.Empty<PitchType>()
                : (PitchType[])recentPitchSequence.Clone();
            SuggestedPitch = suggestedPitch;
        }

        public int RequestId { get; }
        public int PlateAppearanceIndex { get; }
        public int MatchId { get; }
        public int Inning { get; }
        public InningHalf Half { get; }
        public int BatterId { get; }
        public int PitcherId { get; }
        public int PitchNumber { get; }
        public int Balls { get; }
        public int Strikes { get; }
        public int Outs { get; }
        public int AwayScore { get; }
        public int HomeScore { get; }
        public BaseStateSnapshot Bases { get; }
        public double CurrentFatigue { get; }
        public LeverageTier Leverage { get; }
        public IReadOnlyList<PitchOption> AvailablePitches => _availablePitches;
        public IReadOnlyList<PitchType> RecentPitchSequence => _recentPitchSequence;
        public PitchSelectionCommand SuggestedPitch { get; }
    }

    /// <summary>난이도와 별개로 화면이 제공할 입력 보조 수준을 고정한다.</summary>
    public readonly struct MiniGameAssistRule
    {
        public MiniGameAssistRule(
            bool showsTrail,
            bool showsLateArrivalGuide,
            double aimCorrection,
            double timeScale)
        {
            ShowsTrail = showsTrail;
            ShowsLateArrivalGuide = showsLateArrivalGuide;
            AimCorrection = aimCorrection;
            TimeScale = timeScale;
        }

        public bool ShowsTrail { get; }
        public bool ShowsLateArrivalGuide { get; }
        public double AimCorrection { get; }
        public double TimeScale { get; }
        public static MiniGameAssistRule Standard => new MiniGameAssistRule(true, false, 0.08d, 1d);
    }

    /// <summary>타자가 제출하는 스윙 여부·배트 위치·입력 시점 명령이다.</summary>
    public readonly struct SwingCommand
    {
        public SwingCommand(
            int requestId,
            bool didSwing,
            PlatePoint batPoint,
            double swingInputTime01,
            BattingApproach intent,
            bool isBunt = false)
        {
            if (requestId < 0) throw new ArgumentOutOfRangeException(nameof(requestId));
            if (swingInputTime01 < 0d || swingInputTime01 > 1d)
                throw new ArgumentOutOfRangeException(nameof(swingInputTime01));
            RequestId = requestId;
            DidSwing = didSwing;
            BatPoint = batPoint;
            SwingInputTime01 = swingInputTime01;
            Intent = intent;
            IsBunt = isBunt;
        }

        public int RequestId { get; }
        public bool DidSwing { get; }
        public PlatePoint BatPoint { get; }
        public double SwingInputTime01 { get; }
        public BattingApproach Intent { get; }
        public bool IsBunt { get; }
    }

    /// <summary>투구 궤적을 재생한 뒤 타자의 행동 입력을 요청한다.</summary>
    public readonly struct BatterMiniGameRequest
    {
        public BatterMiniGameRequest(
            int requestId,
            int plateAppearanceIndex,
            int matchId,
            int inning,
            InningHalf half,
            int batterId,
            int pitcherId,
            int pitchNumber,
            int balls,
            int strikes,
            int outs,
            int awayScore,
            int homeScore,
            BaseStateSnapshot bases,
            PitchFlightDescriptor pitch,
            int consecutivePitchTypeUses,
            double idealSwingTime01,
            BattingApproach defaultIntent,
            MiniGameAssistRule assistRule,
            SwingCommand suggestedSwing)
        {
            RequestId = requestId;
            PlateAppearanceIndex = plateAppearanceIndex;
            MatchId = matchId;
            Inning = inning;
            Half = half;
            BatterId = batterId;
            PitcherId = pitcherId;
            PitchNumber = pitchNumber;
            Balls = balls;
            Strikes = strikes;
            Outs = outs;
            AwayScore = awayScore;
            HomeScore = homeScore;
            Bases = bases;
            Pitch = pitch;
            ConsecutivePitchTypeUses = Math.Max(1, consecutivePitchTypeUses);
            IdealSwingTime01 = idealSwingTime01;
            DefaultIntent = defaultIntent;
            AssistRule = assistRule;
            SuggestedSwing = suggestedSwing;
        }

        public int RequestId { get; }
        public int PlateAppearanceIndex { get; }
        public int MatchId { get; }
        public int Inning { get; }
        public InningHalf Half { get; }
        public int BatterId { get; }
        public int PitcherId { get; }
        public int PitchNumber { get; }
        public int Balls { get; }
        public int Strikes { get; }
        public int Outs { get; }
        public int AwayScore { get; }
        public int HomeScore { get; }
        public BaseStateSnapshot Bases { get; }
        public PitchFlightDescriptor Pitch { get; }
        public int ConsecutivePitchTypeUses { get; }
        public double IdealSwingTime01 { get; }
        public BattingApproach DefaultIntent { get; }
        public MiniGameAssistRule AssistRule { get; }
        public SwingCommand SuggestedSwing { get; }
    }

    public enum ContactGrade
    {
        None = 0,
        FoulTip = 1,
        Weak = 2,
        Normal = 3,
        Solid = 4,
        Barrel = 5
    }

    public enum SwingTimingFeedback
    {
        VeryEarly = 0,
        Early = 1,
        Perfect = 2,
        Late = 3,
        VeryLate = 4
    }

    public enum SwingLocationFeedback
    {
        Center = 0,
        High = 1,
        Low = 2,
        Inside = 3,
        Outside = 4,
        Missed = 5
    }

    /// <summary>스윙 실행을 공식 투구 결과와 타구 생성 입력으로 변환한 값이다.</summary>
    public readonly struct ContactProfile
    {
        public ContactProfile(
            PitchResult pitchResult,
            ContactGrade grade,
            SwingTimingFeedback timingFeedback,
            SwingLocationFeedback locationFeedback,
            double timingErrorMilliseconds,
            double normalizedLocationError,
            double quality,
            double exitVelocityMph,
            double launchAngleDegrees,
            double sprayAngleDegrees,
            double spinRateRpm)
        {
            PitchResult = pitchResult;
            Grade = grade;
            TimingFeedback = timingFeedback;
            LocationFeedback = locationFeedback;
            TimingErrorMilliseconds = timingErrorMilliseconds;
            NormalizedLocationError = normalizedLocationError;
            Quality = quality;
            ExitVelocityMph = exitVelocityMph;
            LaunchAngleDegrees = launchAngleDegrees;
            SprayAngleDegrees = sprayAngleDegrees;
            SpinRateRpm = spinRateRpm;
        }

        public PitchResult PitchResult { get; }
        public ContactGrade Grade { get; }
        public SwingTimingFeedback TimingFeedback { get; }
        public SwingLocationFeedback LocationFeedback { get; }
        public double TimingErrorMilliseconds { get; }
        public double NormalizedLocationError { get; }
        public double Quality { get; }
        public double ExitVelocityMph { get; }
        public double LaunchAngleDegrees { get; }
        public double SprayAngleDegrees { get; }
        public double SpinRateRpm { get; }
        public bool IsBallInPlay => PitchResult == PitchResult.InPlay;
    }

    /// <summary>리플레이와 분석에 필요한 한 투구의 의도·실행·컨택 세부 기록이다.</summary>
    public readonly struct PitchPlayData : IEquatable<PitchPlayData>
    {
        public PitchPlayData(
            PitchSelectionCommand pitchSelection,
            PitchFlightDescriptor pitch,
            SwingCommand swing,
            ContactProfile contact)
        {
            HasValue = true;
            PitchSelection = pitchSelection;
            Pitch = pitch;
            Swing = swing;
            Contact = contact;
        }

        public bool HasValue { get; }
        public PitchSelectionCommand PitchSelection { get; }
        public PitchFlightDescriptor Pitch { get; }
        public SwingCommand Swing { get; }
        public ContactProfile Contact { get; }

        public bool Equals(PitchPlayData other)
        {
            return HasValue == other.HasValue &&
                   PitchSelection.RequestId == other.PitchSelection.RequestId &&
                   PitchSelection.PitchType == other.PitchSelection.PitchType &&
                   PitchSelection.TargetPoint.Equals(other.PitchSelection.TargetPoint) &&
                   Pitch.PlatePoint.Equals(other.Pitch.PlatePoint) &&
                   Pitch.VelocityMph.Equals(other.Pitch.VelocityMph) &&
                   Swing.RequestId == other.Swing.RequestId &&
                   Swing.DidSwing == other.Swing.DidSwing &&
                   Swing.BatPoint.Equals(other.Swing.BatPoint) &&
                   Swing.SwingInputTime01.Equals(other.Swing.SwingInputTime01) &&
                   Contact.PitchResult == other.Contact.PitchResult &&
                   Contact.Quality.Equals(other.Contact.Quality);
        }

        public override bool Equals(object obj) => obj is PitchPlayData other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = HasValue ? 1 : 0;
                hash = hash * 397 ^ PitchSelection.RequestId;
                hash = hash * 397 ^ (int)PitchSelection.PitchType;
                hash = hash * 397 ^ PitchSelection.TargetPoint.GetHashCode();
                hash = hash * 397 ^ Pitch.PlatePoint.GetHashCode();
                hash = hash * 397 ^ Pitch.VelocityMph.GetHashCode();
                hash = hash * 397 ^ Swing.RequestId;
                hash = hash * 397 ^ Swing.BatPoint.GetHashCode();
                hash = hash * 397 ^ Contact.Quality.GetHashCode();
                return hash;
            }
        }
    }

    public interface IPitchSelectionDecisionSource
    {
        bool RequiresPitchSelection(in PitchSelectionRequest request);
        bool TryGetPitchSelection(in PitchSelectionRequest request, out PitchSelectionCommand command);
    }

    public interface ISwingExecutionDecisionSource
    {
        bool RequiresSwingExecution(in BatterMiniGameRequest request);
        bool TryGetSwingExecution(in BatterMiniGameRequest request, out SwingCommand command);
    }

    /// <summary>저장된 직접 투구 선택을 순서대로 재생하고 다음 입력에서 정지한다.</summary>
    public sealed class RecordedPitchSelectionDecisionSource : IPitchSelectionDecisionSource
    {
        private readonly int _controlledPlayerId;
        private readonly IReadOnlyList<PitchSelectionCommand> _commands;
        private readonly MiniGameInterventionScope _scope;
        private readonly int _manualPlateAppearanceIndex;

        public RecordedPitchSelectionDecisionSource(
            int controlledPlayerId,
            IReadOnlyList<PitchSelectionCommand> commands,
            MiniGameInterventionScope scope,
            int manualPlateAppearanceIndex = -1)
        {
            _controlledPlayerId = controlledPlayerId;
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
            _scope = scope;
            _manualPlateAppearanceIndex = manualPlateAppearanceIndex;
        }

        public bool RequiresPitchSelection(in PitchSelectionRequest request)
        {
            return request.PitcherId == _controlledPlayerId &&
                   MiniGameParticipationPolicy.ShouldControl(
                       _scope,
                       request.PlateAppearanceIndex,
                       request.Inning,
                       request.AwayScore,
                       request.HomeScore,
                       request.Bases,
                       request.Leverage,
                       _manualPlateAppearanceIndex);
        }

        public bool TryGetPitchSelection(
            in PitchSelectionRequest request,
            out PitchSelectionCommand command)
        {
            if (request.RequestId < _commands.Count)
            {
                command = _commands[request.RequestId];
                if (command.RequestId != request.RequestId)
                    throw new InvalidOperationException("투구 선택 RequestId 순서가 일치하지 않습니다.");
                return true;
            }
            command = default;
            return false;
        }
    }

    /// <summary>저장된 직접 스윙 실행을 순서대로 재생하고 다음 입력에서 정지한다.</summary>
    public sealed class RecordedSwingExecutionDecisionSource : ISwingExecutionDecisionSource
    {
        private readonly int _controlledPlayerId;
        private readonly IReadOnlyList<SwingCommand> _commands;
        private readonly MiniGameInterventionScope _scope;
        private readonly int _manualPlateAppearanceIndex;

        public RecordedSwingExecutionDecisionSource(
            int controlledPlayerId,
            IReadOnlyList<SwingCommand> commands,
            MiniGameInterventionScope scope,
            int manualPlateAppearanceIndex = -1)
        {
            _controlledPlayerId = controlledPlayerId;
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
            _scope = scope;
            _manualPlateAppearanceIndex = manualPlateAppearanceIndex;
        }

        public bool RequiresSwingExecution(in BatterMiniGameRequest request)
        {
            return request.BatterId == _controlledPlayerId &&
                   MiniGameParticipationPolicy.ShouldControl(
                       _scope,
                       request.PlateAppearanceIndex,
                       request.Inning,
                       request.AwayScore,
                       request.HomeScore,
                       request.Bases,
                       LeverageTier.Medium,
                       _manualPlateAppearanceIndex);
        }

        public bool TryGetSwingExecution(
            in BatterMiniGameRequest request,
            out SwingCommand command)
        {
            if (request.RequestId < _commands.Count)
            {
                command = _commands[request.RequestId];
                if (command.RequestId != request.RequestId)
                    throw new InvalidOperationException("스윙 실행 RequestId 순서가 일치하지 않습니다.");
                return true;
            }
            command = default;
            return false;
        }
    }

    internal static class MiniGameParticipationPolicy
    {
        public static bool ShouldControl(
            MiniGameInterventionScope scope,
            int plateAppearanceIndex,
            int inning,
            int awayScore,
            int homeScore,
            BaseStateSnapshot bases,
            LeverageTier leverage,
            int manualPlateAppearanceIndex)
        {
            if (scope == MiniGameInterventionScope.AllInvolvement)
                return true;
            if (scope == MiniGameInterventionScope.ManualIntervention)
            {
                // 별도 타석을 지정하지 않은 수동 개입은 매 타석 경계에서 멈춘다.
                // Presentation은 이 경계에서 "이번 타석 자동" 또는 직접 진행을 선택한다.
                return manualPlateAppearanceIndex < 0 ||
                       plateAppearanceIndex == manualPlateAppearanceIndex;
            }

            int scoreDifference = Math.Abs(awayScore - homeScore);
            return leverage >= LeverageTier.High ||
                   bases.HasRunnerOnSecond ||
                   bases.HasRunnerOnThird ||
                   inning >= 7 && scoreDifference <= 2;
        }
    }
}
