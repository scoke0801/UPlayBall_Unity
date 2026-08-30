using System;
using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Simulation.PlateAppearance;
using Baseball.Simulation.Random;

namespace Baseball.Simulation.Match
{
    /// <summary>투구 의도를 능력치와 결정론적 제구 오차가 반영된 실제 궤적으로 바꾼다.</summary>
    public sealed class PitchExecutionResolver
    {
        private readonly MiniGameBalance _balance;
        private readonly IRandomSource _random;

        public PitchExecutionResolver(BalanceTable balance, IRandomSource random)
        {
            _balance = balance?.MiniGame ?? throw new ArgumentNullException(nameof(balance));
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        /// <summary>현재 투수가 선택할 수 있는 구종과 예상 제구 범위를 만든다.</summary>
        public PitchOption[] BuildPitchOptions(in PlateAppearanceMatchup matchup)
        {
            Player pitcher = matchup.Pitcher;
            int count = pitcher.PitchRepertoire.Count;
            if (count == 0)
                return BuildDerivedPitchOptions(matchup);

            var result = new PitchOption[count];
            for (int index = 0; index < count; index++)
            {
                PitchRepertoireEntry entry = pitcher.PitchRepertoire[index];
                result[index] = BuildPitchOption(matchup, entry.PitchType, entry.Proficiency, entry.IsPrimary);
            }
            return result;
        }

        /// <summary>선택한 목표점과 실제 홈플레이트 통과점을 분리해 고정한다.</summary>
        public PitchFlightDescriptor Resolve(
            in PlateAppearanceMatchup matchup,
            in PitchSelectionCommand command)
        {
            if (Math.Abs(command.TargetPoint.X) > _balance.TargetHorizontalLimit ||
                Math.Abs(command.TargetPoint.Y) > _balance.TargetVerticalLimit)
            {
                throw new ArgumentOutOfRangeException(nameof(command), "투구 목표가 선택 가능 영역을 벗어났습니다.");
            }

            PitchTypeProfile profile = PitchTypeProfile.Get(command.PitchType);
            int proficiency = GetProficiency(matchup.Pitcher, command.PitchType);
            CommandEllipse ellipse = CalculateCommandEllipse(matchup, profile, proficiency);
            double angle = ellipse.RotationDegrees * Math.PI / 180d;
            double localX = NextGaussian() * ellipse.RadiusX;
            double localY = NextGaussian() * ellipse.RadiusY;
            double errorX = localX * Math.Cos(angle) - localY * Math.Sin(angle);
            double errorY = localX * Math.Sin(angle) + localY * Math.Cos(angle);
            PlatePoint actual = new PlatePoint(
                Clamp(command.TargetPoint.X + errorX, -1.8d, 1.8d),
                Clamp(command.TargetPoint.Y + errorY, -1.7d, 1.7d));

            double velocity = Clamp(
                profile.BaseVelocityMph +
                (matchup.EffectiveVelocity - 50d) * 0.105d +
                (proficiency - 50d) * 0.025d +
                (_random.NextDouble() - 0.5d) * 2.4d,
                68d,
                104d);
            double breakingScale = Clamp(
                0.72d + matchup.EffectiveBreaking / 170d + (proficiency - 50d) * 0.003d,
                0.55d,
                1.45d);
            double handDirection = matchup.Pitcher.ThrowingHand == Handedness.Left ? -1d : 1d;
            double horizontalBreak = profile.HorizontalBreak * breakingScale * handDirection;
            double verticalBreak = profile.VerticalBreak * breakingScale;
            double arrivalMilliseconds = 41250d / velocity;
            double quality = Clamp(
                matchup.EffectiveStuff * 0.55d +
                proficiency * 0.30d +
                matchup.EffectiveBreaking * 0.15d -
                Math.Sqrt(errorX * errorX + errorY * errorY) * 24d,
                0d,
                100d);
            double releaseX = matchup.Pitcher.ThrowingHand == Handedness.Left ? -0.42d : 0.42d;
            bool isHitByPitch = IsHitByPitch(matchup.Batter, actual);
            return new PitchFlightDescriptor(
                command.PitchType,
                new PlatePoint(releaseX, 1.22d),
                command.TargetPoint,
                actual,
                velocity,
                horizontalBreak,
                verticalBreak,
                profile.BreakStartTime01,
                arrivalMilliseconds,
                quality,
                isHitByPitch);
        }

        public CommandEllipse CalculateCommandEllipse(
            in PlateAppearanceMatchup matchup,
            PitchType pitchType)
        {
            return CalculateCommandEllipse(
                matchup,
                PitchTypeProfile.Get(pitchType),
                GetProficiency(matchup.Pitcher, pitchType));
        }

        private PitchOption[] BuildDerivedPitchOptions(in PlateAppearanceMatchup matchup)
        {
            bool favorsBreaking = matchup.Pitcher.PitcherAttributes.Breaking >= 55;
            PitchType secondary = favorsBreaking ? PitchType.Slider : PitchType.TwoSeamFastball;
            PitchType third = favorsBreaking ? PitchType.Curveball : PitchType.Changeup;
            PitchType fourth = matchup.Pitcher.PitcherAttributes.Stuff >= 60
                ? PitchType.Splitter
                : PitchType.Sinker;
            return new[]
            {
                BuildPitchOption(matchup, PitchType.FourSeamFastball, 55, true),
                BuildPitchOption(matchup, secondary, 50, false),
                BuildPitchOption(matchup, third, 46, false),
                BuildPitchOption(matchup, fourth, 42, false)
            };
        }

        private PitchOption BuildPitchOption(
            in PlateAppearanceMatchup matchup,
            PitchType pitchType,
            int proficiency,
            bool isPrimary)
        {
            PitchTypeProfile profile = PitchTypeProfile.Get(pitchType);
            double centerVelocity = Clamp(
                profile.BaseVelocityMph +
                (matchup.EffectiveVelocity - 50d) * 0.105d +
                (proficiency - 50d) * 0.025d,
                68d,
                104d);
            double breakScale = Clamp(0.72d + matchup.EffectiveBreaking / 170d, 0.55d, 1.35d);
            double handDirection = matchup.Pitcher.ThrowingHand == Handedness.Left ? -1d : 1d;
            return new PitchOption(
                pitchType,
                proficiency,
                isPrimary,
                centerVelocity - 1.5d,
                centerVelocity + 1.5d,
                profile.HorizontalBreak * breakScale * handDirection,
                profile.VerticalBreak * breakScale,
                profile.FatigueCost,
                CalculateCommandEllipse(matchup, profile, proficiency));
        }

        private CommandEllipse CalculateCommandEllipse(
            in PlateAppearanceMatchup matchup,
            in PitchTypeProfile profile,
            int proficiency)
        {
            double deviation = _balance.BaseCommandDeviation -
                               (matchup.EffectiveControl - 50d) * _balance.ControlDeviationWeight -
                               (proficiency - 50d) * 0.0008d +
                               profile.CommandDifficulty;
            deviation = Clamp(
                deviation,
                _balance.MinimumCommandDeviation,
                _balance.MaximumCommandDeviation);
            return new CommandEllipse(
                deviation * profile.HorizontalErrorScale,
                deviation * profile.VerticalErrorScale,
                profile.ErrorRotationDegrees);
        }

        private int GetProficiency(Player pitcher, PitchType pitchType)
        {
            for (int index = 0; index < pitcher.PitchRepertoire.Count; index++)
            {
                PitchRepertoireEntry entry = pitcher.PitchRepertoire[index];
                if (entry.PitchType == pitchType)
                    return entry.Proficiency;
            }
            return pitchType == PitchType.FourSeamFastball ? 55 : 46;
        }

        private bool IsHitByPitch(Player batter, PlatePoint actual)
        {
            if (actual.Y < -1.05d || actual.Y > 1.05d)
                return false;
            double batterSide = batter.BattingHand == Handedness.Left ? -1d : 1d;
            if (batter.BattingHand == Handedness.Switch)
                batterSide = batter.ThrowingHand == Handedness.Left ? 1d : -1d;
            if (actual.X * batterSide < 1.34d)
                return false;
            return _random.NextDouble() < 0.72d;
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

        private readonly struct PitchTypeProfile
        {
            private PitchTypeProfile(
                double baseVelocityMph,
                double horizontalBreak,
                double verticalBreak,
                double breakStartTime01,
                double commandDifficulty,
                double horizontalErrorScale,
                double verticalErrorScale,
                double errorRotationDegrees,
                double fatigueCost)
            {
                BaseVelocityMph = baseVelocityMph;
                HorizontalBreak = horizontalBreak;
                VerticalBreak = verticalBreak;
                BreakStartTime01 = breakStartTime01;
                CommandDifficulty = commandDifficulty;
                HorizontalErrorScale = horizontalErrorScale;
                VerticalErrorScale = verticalErrorScale;
                ErrorRotationDegrees = errorRotationDegrees;
                FatigueCost = fatigueCost;
            }

            public double BaseVelocityMph { get; }
            public double HorizontalBreak { get; }
            public double VerticalBreak { get; }
            public double BreakStartTime01 { get; }
            public double CommandDifficulty { get; }
            public double HorizontalErrorScale { get; }
            public double VerticalErrorScale { get; }
            public double ErrorRotationDegrees { get; }
            public double FatigueCost { get; }

            public static PitchTypeProfile Get(PitchType pitchType)
            {
                return pitchType switch
                {
                    PitchType.FourSeamFastball => new PitchTypeProfile(
                        91d, 0.03d, 0.10d, 0.72d, -0.012d, 0.92d, 1.05d, 0d, 1d),
                    PitchType.TwoSeamFastball => new PitchTypeProfile(
                        89d, 0.16d, -0.04d, 0.62d, 0d, 1.15d, 0.95d, 18d, 0.95d),
                    PitchType.Cutter => new PitchTypeProfile(
                        87d, -0.13d, 0.01d, 0.68d, 0.012d, 1.10d, 0.94d, -15d, 1.02d),
                    PitchType.Slider => new PitchTypeProfile(
                        83d, -0.31d, -0.15d, 0.60d, 0.025d, 1.22d, 1.02d, -22d, 1.05d),
                    PitchType.Curveball => new PitchTypeProfile(
                        76d, -0.14d, -0.42d, 0.52d, 0.035d, 1.02d, 1.28d, 8d, 1.08d),
                    PitchType.Changeup => new PitchTypeProfile(
                        82d, 0.11d, -0.17d, 0.64d, 0.018d, 1.12d, 1.06d, 14d, 0.90d),
                    PitchType.Splitter => new PitchTypeProfile(
                        84d, 0.04d, -0.36d, 0.66d, 0.032d, 1.03d, 1.30d, 4d, 1.10d),
                    PitchType.Sinker => new PitchTypeProfile(
                        88d, 0.18d, -0.23d, 0.61d, 0.018d, 1.18d, 1.10d, 20d, 1.03d),
                    _ => throw new ArgumentOutOfRangeException(nameof(pitchType))
                };
            }
        }
    }
}
