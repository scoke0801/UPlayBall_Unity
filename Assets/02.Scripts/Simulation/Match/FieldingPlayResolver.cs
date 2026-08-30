using System;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Simulation.PlateAppearance;
using Baseball.Simulation.Random;

namespace Baseball.Simulation.Match
{
    public enum FieldingFailureType
    {
        None = 0,
        Reach = 1,
        FieldingError = 2,
        ThrowingError = 3
    }

    /// <summary>
    /// Defense에서 Range·Hands를, Arm에서 송구 강점을 파생해 각 수비 역할을 분리한다.
    /// </summary>
    public readonly struct FieldingProfile
    {
        public FieldingProfile(int range, int hands, int arm, int positionProficiency)
        {
            Range = range;
            Hands = hands;
            Arm = arm;
            PositionProficiency = positionProficiency;
        }

        public int Range { get; }
        public int Hands { get; }
        public int Arm { get; }
        public int PositionProficiency { get; }

        public static FieldingProfile Derive(Player player, PlayerPosition position, int abilityBonus = 0)
        {
            uint hash = StableHash(player.PlayerId);
            int defense = ClampRating(player.BatterAttributes.Defense + abilityBonus);
            int range = ClampRating(defense + (int)(hash % 7U) - 3);
            int hands = ClampRating(defense + (int)((hash >> 5) % 7U) - 3);
            int arm = ClampRating(player.BatterAttributes.Arm + (int)((hash >> 11) % 7U) - 3);
            return new FieldingProfile(range, hands, arm, player.GetPositionProficiency(position));
        }

        private static uint StableHash(int playerId)
        {
            uint value = unchecked((uint)playerId) ^ 0xA511E9B3U;
            value ^= value >> 15;
            value *= 0x2C1B3C6DU;
            value ^= value >> 12;
            return value;
        }

        private static int ClampRating(int value)
        {
            if (value < 0) return 0;
            if (value > 100) return 100;
            return value;
        }
    }

    /// <summary>
    /// 수비 도달·포구·송구가 끝난 뒤 공식 기록기가 소비할 단일 플레이 결과다.
    /// </summary>
    public readonly struct FieldingPlayOutcome
    {
        public FieldingPlayOutcome(
            PlateAppearanceResult result,
            PlayerPosition fielderPosition,
            int fielderId,
            FieldingFailureType failureType,
            bool wasRoutine,
            bool isDoublePlay,
            double reachChance)
        {
            Result = result;
            FielderPosition = fielderPosition;
            FielderId = fielderId;
            FailureType = failureType;
            WasRoutine = wasRoutine;
            IsDoublePlay = isDoublePlay;
            ReachChance = reachChance;
        }

        public PlateAppearanceResult Result { get; }
        public PlayerPosition FielderPosition { get; }
        public int FielderId { get; }
        public FieldingFailureType FailureType { get; }
        public bool WasRoutine { get; }
        public bool IsDoublePlay { get; }
        public double ReachChance { get; }
    }

    /// <summary>
    /// 타구 결과를 먼저 정하지 않고 수비수 도달·포구·송구 순서로 안타와 실책을 판정한다.
    /// </summary>
    public sealed class FieldingPlayResolver
    {
        private readonly DetailedFieldingBalance _balance;
        private readonly SkillTraitBalance _skillTraits;
        private readonly IRandomSource _random;

        public FieldingPlayResolver(
            DetailedFieldingBalance balance,
            IRandomSource random,
            SkillTraitBalance? skillTraits = null)
        {
            _balance = balance;
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _skillTraits = skillTraits ?? SkillTraitBalance.CreateDefault();
        }

        public FieldingPlayOutcome Resolve(
            in BattedBallDescriptor ball,
            Player fielder,
            PlayerPosition position,
            DefensiveAlignment alignment,
            int batterSpeed,
            int leadRunnerSpeed,
            bool canAttemptDoublePlay)
        {
            if (fielder == null) throw new ArgumentNullException(nameof(fielder));
            int traitBonus = fielder.HasTrait(SkillTraitIds.DefensiveFocus)
                ? _skillTraits.DefensiveFocusAbilityBonus
                : 0;
            FieldingProfile profile = FieldingProfile.Derive(fielder, position, traitBonus);
            double reachChance = CalculateReachChance(ball, profile, alignment);
            bool routine = reachChance >= 0.68d && ball.Quality <= 62d;
            if (_random.NextDouble() >= reachChance)
            {
                return new FieldingPlayOutcome(
                    ResolveHit(ball, batterSpeed),
                    position,
                    fielder.PlayerId,
                    FieldingFailureType.Reach,
                    routine,
                    false,
                    reachChance);
            }

            double handleFailure = CalculateHandleFailure(ball, profile, routine);
            if (_random.NextDouble() < handleFailure)
            {
                PlateAppearanceResult result = routine
                    ? PlateAppearanceResult.ReachedOnError
                    : ResolveHit(ball, batterSpeed);
                return new FieldingPlayOutcome(
                    result,
                    position,
                    fielder.PlayerId,
                    routine ? FieldingFailureType.FieldingError : FieldingFailureType.Reach,
                    routine,
                    false,
                    reachChance);
            }

            if (ball.Type is BattedBallType.GroundBall or BattedBallType.Bunt)
            {
                bool difficultThrow = ball.Quality >= 67d || profile.PositionProficiency < 70;
                double throwFailure = (difficultThrow
                    ? _balance.DifficultThrowFailure
                    : _balance.NormalThrowFailure) * GetErrorMultiplier(profile.Hands);
                if (_random.NextDouble() < throwFailure)
                {
                    return new FieldingPlayOutcome(
                        PlateAppearanceResult.ReachedOnError,
                        position,
                        fielder.PlayerId,
                        FieldingFailureType.ThrowingError,
                        routine,
                        false,
                        reachChance);
                }

                bool doublePlay = canAttemptDoublePlay &&
                                  _random.NextDouble() < CalculateDoublePlayChance(
                                      ball,
                                      profile,
                                      batterSpeed,
                                      leadRunnerSpeed,
                                      alignment);
                PlateAppearanceResult groundResult = ball.Type == BattedBallType.Bunt
                    ? PlateAppearanceResult.SacrificeBunt
                    : canAttemptDoublePlay && !doublePlay
                        ? PlateAppearanceResult.FieldersChoice
                        : PlateAppearanceResult.GroundOut;
                return new FieldingPlayOutcome(
                    groundResult,
                    position,
                    fielder.PlayerId,
                    FieldingFailureType.None,
                    routine,
                    doublePlay,
                    reachChance);
            }

            return new FieldingPlayOutcome(
                PlateAppearanceResult.FlyOut,
                position,
                fielder.PlayerId,
                FieldingFailureType.None,
                routine,
                false,
                reachChance);
        }

        private double CalculateReachChance(
            in BattedBallDescriptor ball,
            in FieldingProfile profile,
            DefensiveAlignment alignment)
        {
            double baseChance = ball.Type switch
            {
                BattedBallType.GroundBall => _balance.GroundBallReachBase,
                BattedBallType.LineDrive => _balance.LineDriveReachBase,
                BattedBallType.FlyBall => _balance.FlyBallReachBase,
                BattedBallType.PopUp => _balance.PopUpReachBase,
                _ => 0.78d
            };
            double rangeAdjustment = Clamp(
                (profile.Range - 50d) * _balance.RangeProbabilityWeight,
                -_balance.MaximumRangeAdjustment,
                _balance.MaximumRangeAdjustment);
            double proficiencyAdjustment = (profile.PositionProficiency - 100d) *
                                           _balance.PositionProficiencyWeight;
            double qualityPenalty = (ball.Quality - 50d) * _balance.QualityReachPenalty;
            return Clamp(
                baseChance + rangeAdjustment + proficiencyAdjustment - qualityPenalty +
                GetAlignmentReachAdjustment(ball, alignment),
                0.05d,
                0.99d);
        }

        private double CalculateHandleFailure(
            in BattedBallDescriptor ball,
            in FieldingProfile profile,
            bool routine)
        {
            double baseFailure = ball.Type is BattedBallType.GroundBall or BattedBallType.Bunt
                ? _balance.NormalGroundHandleFailure
                : _balance.NormalFlyHandleFailure;
            double difficultyMultiplier = routine ? 1d : 1.8d;
            return Clamp(baseFailure * GetErrorMultiplier(profile.Hands) * difficultyMultiplier, 0.001d, 0.08d);
        }

        private double GetErrorMultiplier(int hands)
        {
            return Clamp(1d - (hands - 50d) * _balance.HandsErrorWeight, 0.35d, 1.65d);
        }

        private static double CalculateDoublePlayChance(
            in BattedBallDescriptor ball,
            in FieldingProfile profile,
            int batterSpeed,
            int runnerSpeed,
            DefensiveAlignment alignment)
        {
            double pace = ball.Pace == BallPaceBand.Fast ? 0.12d : ball.Pace == BallPaceBand.Slow ? -0.10d : 0d;
            double alignmentBonus = alignment == DefensiveAlignment.DoublePlayDepth ? 0.09d : 0d;
            return Clamp(
                0.26d + pace + (profile.Hands - 50d) * 0.0015d +
                (profile.Arm - 50d) * 0.0020d -
                (batterSpeed - 50d) * 0.0025d -
                (runnerSpeed - 50d) * 0.0010d + alignmentBonus,
                0.08d,
                0.78d);
        }

        private static PlateAppearanceResult ResolveHit(in BattedBallDescriptor ball, int batterSpeed)
        {
            if (ball.Type == BattedBallType.Bunt)
                return PlateAppearanceResult.BuntSingle;
            if (ball.Type == BattedBallType.GroundBall)
                return PlateAppearanceResult.Single;
            if (ball.Type == BattedBallType.LineDrive && ball.Quality < 58d)
                return PlateAppearanceResult.Single;
            if (ball.Quality >= 78d && batterSpeed >= 72)
                return PlateAppearanceResult.Triple;
            return PlateAppearanceResult.Double;
        }

        private static double GetAlignmentReachAdjustment(
            in BattedBallDescriptor ball,
            DefensiveAlignment alignment)
        {
            return alignment switch
            {
                DefensiveAlignment.PullShift when ball.Direction == BattedBallDirection.Pull &&
                                                    ball.Type == BattedBallType.GroundBall => 0.035d,
                DefensiveAlignment.PullShift when ball.Direction == BattedBallDirection.Opposite &&
                                                    ball.Type == BattedBallType.GroundBall => -0.045d,
                DefensiveAlignment.InfieldIn when ball.Type == BattedBallType.GroundBall => -0.035d,
                DefensiveAlignment.GuardLines when ball.FieldZone is FieldZone.LeftFieldLine or FieldZone.RightFieldLine => 0.04d,
                DefensiveAlignment.DoublePlayDepth when ball.Type == BattedBallType.GroundBall => -0.012d,
                _ => 0d
            };
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            if (value < minimum) return minimum;
            if (value > maximum) return maximum;
            return value;
        }
    }
}
