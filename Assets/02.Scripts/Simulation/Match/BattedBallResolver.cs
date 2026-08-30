using System;
using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Simulation.PlateAppearance;
using Baseball.Simulation.Random;

namespace Baseball.Simulation.Match
{
    /// <summary>
    /// 공정 컨택을 홈런 또는 수비가 처리할 범주형 타구로 변환한다.
    /// </summary>
    public sealed class BattedBallResolver
    {
        private readonly BattedBallBalance _balance;
        private readonly MiniGameBalance _miniGame;
        private readonly TacticalMatchBalance _tactical;
        private readonly IRandomSource _random;

        public BattedBallResolver(BalanceTable balance, IRandomSource random)
        {
            if (balance == null) throw new ArgumentNullException(nameof(balance));
            _balance = balance.BattedBall;
            _miniGame = balance.MiniGame;
            _tactical = balance.Match.Tactical;
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public BattedBallDescriptor Resolve(
            in PlateAppearanceMatchup matchup,
            BattingApproach battingApproach)
        {
            BatterAttributes batter = matchup.Batter.BatterAttributes;
            if (battingApproach == BattingApproach.Bunt)
                return ResolveBunt(matchup.Batter);

            double qualityMean = 48d +
                                 (batter.Contact - matchup.EffectiveStuff) * 0.20d +
                                 (batter.Power - matchup.EffectiveBreaking) * 0.22d +
                                 matchup.HardHitAdjustment * 170d +
                                 GetApproachQualityAdjustment(battingApproach);
            double quality = Clamp(qualityMean + (_random.NextDouble() - 0.5d) * 46d, 0d, 100d);
            double homeRunProbability = Clamp(
                _balance.HomeRunProbability +
                (batter.Power - 50d) * _balance.PowerHomeRunWeight -
                (matchup.EffectiveBreaking - 50d) * _balance.BreakingHomeRunWeight +
                (quality - 50d) * 0.0010d,
                0.002d,
                0.18d);
            bool isHomeRun = _random.NextDouble() < homeRunProbability;
            BattedBallType type = ResolveType(matchup, quality);
            BattedBallDirection direction = ResolveDirection(matchup.Batter);
            FieldZone zone = ResolveZone(matchup.Batter, type, direction);
            return new BattedBallDescriptor(
                type,
                direction,
                zone,
                quality,
                ResolveHangTime(type, quality),
                ResolvePace(type, quality),
                isHomeRun);
        }

        /// <summary>직접/AI 스윙이 만든 공통 컨택 데이터를 기존 수비 판정용 타구로 변환한다.</summary>
        public BattedBallDescriptor Resolve(
            in PlateAppearanceMatchup matchup,
            BattingApproach battingApproach,
            in ContactProfile contact)
        {
            if (!contact.IsBallInPlay)
                throw new ArgumentException("인플레이 컨택만 타구로 변환할 수 있습니다.", nameof(contact));

            if (battingApproach == BattingApproach.Bunt)
            {
                BattedBallDirection buntDirection = contact.SprayAngleDegrees < 0d
                    ? BattedBallDirection.Pull
                    : BattedBallDirection.Opposite;
                FieldZone buntZone = ResolveZone(matchup.Batter, BattedBallType.Bunt, buntDirection);
                return new BattedBallDescriptor(
                    BattedBallType.Bunt,
                    buntDirection,
                    buntZone,
                    contact.Quality,
                    BallFlightBand.Short,
                    BallPaceBand.Slow,
                    false,
                    contact.ExitVelocityMph,
                    contact.LaunchAngleDegrees,
                    contact.SprayAngleDegrees,
                    contact.SpinRateRpm);
            }

            BatterAttributes batter = matchup.Batter.BatterAttributes;
            double quality = Clamp(
                contact.Quality + matchup.HardHitAdjustment * 170d +
                (_random.NextDouble() - 0.5d) * 10d,
                0d,
                100d);
            bool hasHomeRunLaunch =
                contact.LaunchAngleDegrees >= _miniGame.HomeRunMinimumLaunchAngle &&
                contact.LaunchAngleDegrees <= _miniGame.HomeRunMaximumLaunchAngle &&
                contact.ExitVelocityMph >= _miniGame.HomeRunMinimumExitVelocity;
            double homeRunProbability = Clamp(
                (_balance.HomeRunProbability +
                 (batter.Power - 50d) * _balance.PowerHomeRunWeight -
                 (matchup.EffectiveBreaking - 50d) * _balance.BreakingHomeRunWeight +
                 (quality - 50d) * 0.0012d) * _miniGame.HomeRunProbabilityMultiplier,
                0.002d,
                0.32d);
            bool isHomeRun = hasHomeRunLaunch && _random.NextDouble() < homeRunProbability;
            BattedBallType type = ResolveType(contact.LaunchAngleDegrees, quality);
            BattedBallDirection direction = ResolveDirection(contact.SprayAngleDegrees);
            FieldZone zone = ResolveZone(matchup.Batter, type, direction);
            return new BattedBallDescriptor(
                type,
                direction,
                zone,
                quality,
                ResolveHangTime(type, quality),
                ResolvePace(type, quality),
                isHomeRun,
                contact.ExitVelocityMph,
                contact.LaunchAngleDegrees,
                contact.SprayAngleDegrees,
                contact.SpinRateRpm);
        }

        private BattedBallDescriptor ResolveBunt(Player batter)
        {
            double fairChance = Clamp(
                _tactical.FairBuntBase +
                (batter.BatterAttributes.Bunt - 50d) * _tactical.BuntAbilityWeight +
                (batter.BatterAttributes.Mental - 50d) * _tactical.BuntMentalWeight,
                0.20d,
                0.90d);
            double quality = _random.NextDouble() < fairChance
                ? 35d + _random.NextDouble() * 25d
                : 8d + _random.NextDouble() * 18d;
            FieldZone zone = _random.NextDouble() < 0.5d ? FieldZone.Pitcher : FieldZone.ThirdBase;
            return new BattedBallDescriptor(
                BattedBallType.Bunt,
                BattedBallDirection.Opposite,
                zone,
                quality,
                BallFlightBand.Short,
                BallPaceBand.Slow,
                false);
        }

        private BattedBallType ResolveType(in PlateAppearanceMatchup matchup, double quality)
        {
            double groundShare = Clamp(
                _balance.GroundOutShare +
                (matchup.EffectiveBreaking - 50d) * _balance.BreakingGroundOutWeight -
                (matchup.Batter.BatterAttributes.Power - 50d) * _balance.PowerGroundOutWeight +
                (matchup.PitchingApproach == PitchingApproach.GroundBall ? 0.10d : 0d),
                0.28d,
                0.70d);
            double roll = _random.NextDouble();
            if (roll < groundShare)
                return BattedBallType.GroundBall;
            if (roll < groundShare + 0.23d)
                return BattedBallType.FlyBall;
            if (roll < groundShare + 0.39d || quality >= 60d)
                return BattedBallType.LineDrive;
            return BattedBallType.PopUp;
        }

        private static BattedBallType ResolveType(double launchAngleDegrees, double quality)
        {
            if (launchAngleDegrees < 8d)
                return BattedBallType.GroundBall;
            if (launchAngleDegrees <= 18d || quality >= 76d && launchAngleDegrees <= 24d)
                return BattedBallType.LineDrive;
            if (launchAngleDegrees <= 42d)
                return BattedBallType.FlyBall;
            return BattedBallType.PopUp;
        }

        private BattedBallDirection ResolveDirection(Player batter)
        {
            BattingTendencyProfile tendency = BattingTendencyProfile.Derive(batter);
            double roll = _random.NextDouble();
            if (roll < tendency.PullTendency)
                return BattedBallDirection.Pull;
            if (roll < tendency.PullTendency + 0.34d)
                return BattedBallDirection.Center;
            return BattedBallDirection.Opposite;
        }

        private static BattedBallDirection ResolveDirection(double sprayAngleDegrees)
        {
            if (sprayAngleDegrees < -10d) return BattedBallDirection.Pull;
            if (sprayAngleDegrees > 10d) return BattedBallDirection.Opposite;
            return BattedBallDirection.Center;
        }

        private FieldZone ResolveZone(Player batter, BattedBallType type, BattedBallDirection direction)
        {
            bool batsLeft = batter.BattingHand == Handedness.Left ||
                            batter.BattingHand == Handedness.Switch && _random.NextDouble() < 0.5d;
            if (type == BattedBallType.GroundBall || type == BattedBallType.Bunt)
            {
                if (type == BattedBallType.Bunt)
                    return direction == BattedBallDirection.Pull ? FieldZone.FirstBase : FieldZone.ThirdBase;
                if (direction == BattedBallDirection.Center)
                    return _random.NextDouble() < 0.5d ? FieldZone.SecondBase : FieldZone.Shortstop;
                bool leftSide = direction == BattedBallDirection.Pull ? !batsLeft : batsLeft;
                return leftSide
                    ? (_random.NextDouble() < 0.55d ? FieldZone.ThirdBase : FieldZone.Shortstop)
                    : (_random.NextDouble() < 0.55d ? FieldZone.FirstBase : FieldZone.SecondBase);
            }

            if (direction == BattedBallDirection.Center)
                return FieldZone.CenterField;
            bool leftField = direction == BattedBallDirection.Pull ? !batsLeft : batsLeft;
            if (_random.NextDouble() < 0.12d)
                return leftField ? FieldZone.LeftFieldLine : FieldZone.RightFieldLine;
            return leftField ? FieldZone.LeftField : FieldZone.RightField;
        }

        private static BallFlightBand ResolveHangTime(BattedBallType type, double quality)
        {
            if (type is BattedBallType.GroundBall or BattedBallType.Bunt or BattedBallType.LineDrive)
                return BallFlightBand.Short;
            if (type == BattedBallType.PopUp || quality < 40d)
                return BallFlightBand.Long;
            return BallFlightBand.Medium;
        }

        private static BallPaceBand ResolvePace(BattedBallType type, double quality)
        {
            if (type == BattedBallType.Bunt || quality < 32d) return BallPaceBand.Slow;
            if (quality >= 65d) return BallPaceBand.Fast;
            return BallPaceBand.Medium;
        }

        private static double GetApproachQualityAdjustment(BattingApproach approach)
        {
            return approach switch
            {
                BattingApproach.Contact => -5d,
                BattingApproach.Power => 7d,
                BattingApproach.Aggressive => 4d,
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
