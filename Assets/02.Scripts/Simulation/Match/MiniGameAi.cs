using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Simulation.PlateAppearance;
using Baseball.Simulation.Random;

namespace Baseball.Simulation.Match
{
    internal readonly struct PitchSelectionAiContext
    {
        public PitchSelectionAiContext(
            int requestId,
            int batterId,
            int pitchNumber,
            int balls,
            int strikes,
            IReadOnlyList<PitchOption> availablePitches,
            IReadOnlyList<PitchType> recentPitches,
            int recentPitchCount)
        {
            RequestId = requestId;
            BatterId = batterId;
            PitchNumber = pitchNumber;
            Balls = balls;
            Strikes = strikes;
            AvailablePitches = availablePitches ?? throw new ArgumentNullException(nameof(availablePitches));
            RecentPitches = recentPitches ?? throw new ArgumentNullException(nameof(recentPitches));
            if (recentPitchCount < 0 || recentPitchCount > recentPitches.Count)
                throw new ArgumentOutOfRangeException(nameof(recentPitchCount));
            RecentPitchCount = recentPitchCount;
        }

        public int RequestId { get; }
        public int BatterId { get; }
        public int PitchNumber { get; }
        public int Balls { get; }
        public int Strikes { get; }
        public IReadOnlyList<PitchOption> AvailablePitches { get; }
        public IReadOnlyList<PitchType> RecentPitches { get; }
        public int RecentPitchCount { get; }
    }

    internal readonly struct SwingExecutionAiContext
    {
        public SwingExecutionAiContext(
            int requestId,
            PitchFlightDescriptor pitch,
            int consecutivePitchTypeUses,
            double idealSwingTime01,
            BattingApproach defaultIntent)
        {
            RequestId = requestId;
            Pitch = pitch;
            ConsecutivePitchTypeUses = consecutivePitchTypeUses;
            IdealSwingTime01 = idealSwingTime01;
            DefaultIntent = defaultIntent;
        }

        public int RequestId { get; }
        public PitchFlightDescriptor Pitch { get; }
        public int ConsecutivePitchTypeUses { get; }
        public double IdealSwingTime01 { get; }
        public BattingApproach DefaultIntent { get; }
    }

    /// <summary>AI 투수도 플레이어와 같은 구종·목표 위치 명령을 생성한다.</summary>
    public sealed class PitchSelectionAi
    {
        private readonly MiniGameBalance _balance;
        private readonly IRandomSource _random;

        public PitchSelectionAi(BalanceTable balance, IRandomSource random)
        {
            _balance = balance?.MiniGame ?? throw new ArgumentNullException(nameof(balance));
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public PitchSelectionCommand Select(
            in PitchSelectionRequest request,
            PitchingApproach approach)
        {
            var context = new PitchSelectionAiContext(
                request.RequestId,
                request.BatterId,
                request.PitchNumber,
                request.Balls,
                request.Strikes,
                request.AvailablePitches,
                request.RecentPitchSequence,
                request.RecentPitchSequence.Count);
            return Select(context, approach);
        }

        internal PitchSelectionCommand Select(
            in PitchSelectionAiContext request,
            PitchingApproach approach)
        {
            if (request.AvailablePitches.Count == 0)
                throw new InvalidOperationException("선택 가능한 구종이 없습니다.");

            int selectedIndex = SelectPitchIndex(request, approach);
            PitchType pitchType = request.AvailablePitches[selectedIndex].PitchType;
            PlatePoint target = SelectTarget(request, pitchType, approach);
            return new PitchSelectionCommand(request.RequestId, pitchType, target, approach);
        }

        private static int SelectPitchIndex(
            in PitchSelectionAiContext request,
            PitchingApproach approach)
        {
            int count = request.AvailablePitches.Count;
            if (count == 1)
                return 0;

            bool strikeoutCount = request.Strikes == 2 && request.Balls < 3;
            if (strikeoutCount || approach is PitchingApproach.Strikeout or PitchingApproach.Nibble)
            {
                for (int index = 0; index < count; index++)
                {
                    PitchType type = request.AvailablePitches[index].PitchType;
                    if (type is PitchType.Slider or PitchType.Curveball or
                        PitchType.Changeup or PitchType.Splitter)
                    {
                        if (!WasRepeatedTooOften(request, type))
                            return index;
                    }
                }
            }

            int deterministicIndex = Math.Abs(
                request.BatterId * 17 + request.PitchNumber * 7 + request.Balls * 3 + request.Strikes) % count;
            if (WasRepeatedTooOften(request, request.AvailablePitches[deterministicIndex].PitchType))
                deterministicIndex = (deterministicIndex + 1) % count;
            return deterministicIndex;
        }

        private PlatePoint SelectTarget(
            in PitchSelectionAiContext request,
            PitchType pitchType,
            PitchingApproach approach)
        {
            double choice = _random.NextDouble();
            double side = _random.NextDouble() < 0.5d ? -1d : 1d;
            bool breakingPitch = pitchType is PitchType.Slider or PitchType.Curveball or
                                 PitchType.Changeup or PitchType.Splitter or PitchType.Sinker;
            if (approach == PitchingApproach.PitchAround)
                return CreateWasteTarget(side, breakingPitch);
            if (approach == PitchingApproach.Nibble)
                return new PlatePoint(side * 0.96d, breakingPitch ? -0.72d : 0.58d);
            if (approach == PitchingApproach.Strikeout)
                return breakingPitch
                    ? new PlatePoint(side * 0.64d, -1.04d)
                    : new PlatePoint(side * 0.48d, 0.82d);
            if (approach == PitchingApproach.GroundBall)
                return new PlatePoint(side * 0.52d, -0.73d);
            if (approach == PitchingApproach.AttackZone)
                return new PlatePoint(side * 0.55d, choice < 0.5d ? -0.48d : 0.48d);
            if (request.Balls == 3)
            {
                return choice < _balance.AiThreeBallChallengeProbability
                    ? new PlatePoint(side * 0.28d, -0.18d)
                    : CreateWasteTarget(side, breakingPitch);
            }
            if (request.Strikes == 2 && choice < _balance.AiTwoStrikeWasteProbability)
                return CreateWasteTarget(side, breakingPitch);
            if (choice < _balance.AiWastePitchProbability)
                return CreateWasteTarget(side, breakingPitch);
            return new PlatePoint(side * 0.72d, breakingPitch ? -0.55d : 0.42d);
        }

        private PlatePoint CreateWasteTarget(double side, bool breakingPitch)
        {
            if (breakingPitch)
                return new PlatePoint(side * 0.62d, -_balance.AiWastePitchDistance);
            if (_random.NextDouble() < _balance.AiInsideWasteProbability)
                return new PlatePoint(side * _balance.AiWastePitchDistance, 0.32d);
            return new PlatePoint(side * 0.56d, _balance.AiWastePitchDistance);
        }

        private static bool WasRepeatedTooOften(in PitchSelectionAiContext request, PitchType pitchType)
        {
            int count = request.RecentPitchCount;
            return count >= 2 &&
                   request.RecentPitches[count - 1] == pitchType &&
                   request.RecentPitches[count - 2] == pitchType;
        }
    }

    /// <summary>AI 타자도 플레이어와 같은 스윙 여부·위치·시점 명령을 생성한다.</summary>
    public sealed class SwingExecutionAi
    {
        private readonly BalanceTable _balance;
        private readonly IRandomSource _random;

        public SwingExecutionAi(BalanceTable balance, IRandomSource random)
        {
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public SwingCommand Select(
            in BatterMiniGameRequest request,
            in PlateAppearanceMatchup matchup)
        {
            var context = new SwingExecutionAiContext(
                request.RequestId,
                request.Pitch,
                request.ConsecutivePitchTypeUses,
                request.IdealSwingTime01,
                request.DefaultIntent);
            return Select(context, matchup);
        }

        internal SwingCommand Select(
            in SwingExecutionAiContext request,
            in PlateAppearanceMatchup matchup)
        {
            BatterAttributes batter = matchup.Batter.BatterAttributes;
            BattingApproachModifier approach = _balance.BattingApproach.GetModifier(request.DefaultIntent);
            double swingProbability;
            if (request.Pitch.IsStrike)
            {
                swingProbability = _balance.PlateDiscipline.StrikeSwingProbability +
                                   (batter.Mental - 50d) * _balance.PlateDiscipline.MentalStrikeSwingWeight +
                                   approach.StrikeSwingAdjustment;
            }
            else
            {
                swingProbability = _balance.PlateDiscipline.ChaseProbability -
                                   (batter.Mental - 50d) * _balance.PlateDiscipline.MentalChaseWeight +
                                   (matchup.EffectiveStuff - 50d) * _balance.PlateDiscipline.StuffChaseWeight +
                                   (matchup.EffectiveVelocity - 50d) * _balance.PlateDiscipline.VelocityChaseWeight +
                                   approach.ChaseAdjustment;
            }

            double repeatRecognition = CalculateRepeatRecognition(request, batter.Mental);
            if (!request.Pitch.IsStrike)
                swingProbability -= repeatRecognition * _balance.MiniGame.RepeatChaseReduction;

            bool didSwing = _random.NextDouble() < Clamp(swingProbability, 0.02d, 0.96d);
            if (!didSwing)
            {
                return new SwingCommand(
                    request.RequestId,
                    false,
                    default,
                    request.IdealSwingTime01,
                    request.DefaultIntent,
                    request.DefaultIntent == BattingApproach.Bunt);
            }

            double pitchDifficulty = (request.Pitch.Quality - 50d) * 0.0030d +
                                     (request.Pitch.VelocityMph - 88d) * 0.006d;
            double recognition = (batter.Contact - 50d) * 0.0060d +
                                 (batter.Mental - 50d) * 0.0035d;
            double locationScale = Clamp(
                _balance.MiniGame.AiLocationErrorScale + pitchDifficulty - recognition,
                0.38d,
                1.35d);
            locationScale *= 1d -
                             repeatRecognition * _balance.MiniGame.RepeatExecutionErrorReduction;
            double horizontalError = NextGaussian() * _balance.MiniGame.BaseBatRadiusX * locationScale;
            double verticalError = NextGaussian() * _balance.MiniGame.BaseBatRadiusY * locationScale;
            double timingDeviation = _balance.MiniGame.AiTimingErrorMilliseconds +
                                     (matchup.EffectiveVelocity - 50d) * 0.24d +
                                     (matchup.EffectiveStuff - 50d) * 0.16d -
                                     (batter.Contact - 50d) * 0.30d -
                                     (batter.Mental - 50d) * 0.22d;
            timingDeviation *= 1d -
                               repeatRecognition * _balance.MiniGame.RepeatExecutionErrorReduction;
            double timingError = NextGaussian() * Clamp(timingDeviation, 28d, 94d);
            double swingTime = Clamp(
                request.IdealSwingTime01 + timingError / request.Pitch.PlateArrivalMilliseconds,
                0d,
                1d);
            return new SwingCommand(
                request.RequestId,
                true,
                new PlatePoint(
                    request.Pitch.PlatePoint.X + horizontalError,
                    request.Pitch.PlatePoint.Y + verticalError),
                swingTime,
                request.DefaultIntent,
                request.DefaultIntent == BattingApproach.Bunt);
        }

        private double CalculateRepeatRecognition(
            in SwingExecutionAiContext request,
            int mental)
        {
            int repeatedUses = Math.Max(0, request.ConsecutivePitchTypeUses - 1);
            double recognitionPerUse = _balance.MiniGame.RepeatRecognitionBase +
                                       (mental - 50d) * _balance.MiniGame.RepeatRecognitionMentalWeight;
            return Clamp(repeatedUses * recognitionPerUse, 0d, 0.65d);
        }

        private double NextGaussian()
        {
            double first = Math.Max(0.0000001d, _random.NextDouble());
            double second = _random.NextDouble();
            return Math.Sqrt(-2d * Math.Log(first)) * Math.Cos(2d * Math.PI * second);
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            if (value < minimum) return minimum;
            if (value > maximum) return maximum;
            return value;
        }
    }
}
