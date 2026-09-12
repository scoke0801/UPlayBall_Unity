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
        private const int DerivedPitchOptionCount = 4;
        private static readonly PitchRepertoireEntry[,] LegacyEntries = CreateLegacyEntries();
        private readonly MiniGameBalance _balance;
        private readonly PitchArsenalBalance _arsenal;
        private readonly IRandomSource _random;

        public PitchExecutionResolver(BalanceTable balance, IRandomSource random)
        {
            _balance = balance?.MiniGame ?? throw new ArgumentNullException(nameof(balance));
            _arsenal = balance.PitchArsenal;
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        /// <summary>현재 투수가 선택할 수 있는 구종과 예상 제구 범위를 만든다.</summary>
        public PitchOption[] BuildPitchOptions(in PlateAppearanceMatchup matchup)
        {
            int repertoireCount = matchup.Pitcher.PitchRepertoire.Count;
            var result = new PitchOption[
                repertoireCount == 0 ? DerivedPitchOptionCount : repertoireCount];
            FillPitchOptions(matchup, result);
            return result;
        }

        /// <summary>투수 한 명의 반복 투구에 재사용할 옵션 버퍼의 최소 길이를 반환한다.</summary>
        public static int GetRequiredPitchOptionCapacity(Player pitcher)
        {
            if (pitcher == null) throw new ArgumentNullException(nameof(pitcher));
            return Math.Max(DerivedPitchOptionCount, pitcher.PitchRepertoire.Count);
        }

        /// <summary>호출자가 소유한 버퍼에 현재 피로가 반영된 옵션을 채우고 유효 개수를 반환한다.</summary>
        public int FillPitchOptions(
            in PlateAppearanceMatchup matchup,
            PitchOption[] destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            Player pitcher = matchup.Pitcher;
            int count = pitcher.PitchRepertoire.Count;
            int required = count == 0 ? DerivedPitchOptionCount : count;
            if (destination.Length < required)
                throw new ArgumentException("투구 옵션 버퍼가 보유 구종 수보다 작습니다.", nameof(destination));
            if (count == 0)
                return FillDerivedPitchOptions(matchup, destination);

            for (int index = 0; index < count; index++)
            {
                PitchRepertoireEntry entry = pitcher.PitchRepertoire[index];
                destination[index] = BuildPitchOption(matchup, entry);
            }
            return count;
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

            PitchTypeProfile profile = PitchTypeProfileCatalog.Get(command.PitchType);
            PitchRepertoireEntry entry = GetEntry(matchup.Pitcher, command.PitchType);
            int proficiency = entry.Proficiency;
            CommandEllipse ellipse = CalculateCommandEllipse(matchup, profile, proficiency, command.PitchType);
            double angle = ellipse.RotationDegrees * Math.PI / 180d;
            double localX = NextGaussian() * ellipse.RadiusX;
            double localY = NextGaussian() * ellipse.RadiusY;
            double errorX = localX * Math.Cos(angle) - localY * Math.Sin(angle);
            double errorY = localX * Math.Sin(angle) + localY * Math.Cos(angle);
            PlatePoint actual = new PlatePoint(
                Clamp(command.TargetPoint.X + errorX, -1.8d, 1.8d),
                Clamp(command.TargetPoint.Y + errorY, -1.7d, 1.7d));

            double velocity = PitchEffectivenessResolver.ResolveVelocityKph(entry,
                GetPhysicalVelocityRating(matchup), _arsenal) / 1.609344d +
                (_random.NextDouble() - 0.5d) * 2.4d;
            double breakingScale = PitchEffectivenessResolver.ResolveMovementScale(entry,
                matchup.EffectiveBreaking, _arsenal);
            double handDirection = matchup.Pitcher.ThrowingHand == Handedness.Left ? -1d : 1d;
            double horizontalBreak = profile.HorizontalBreak * breakingScale * handDirection;
            double verticalBreak = profile.VerticalBreak * breakingScale;
            double arrivalMilliseconds = 41250d / velocity;
            double quality = Clamp(PitchEffectivenessResolver.ResolvePlayerQuality(entry, matchup.Pitcher,
                matchup.EffectiveStuff, matchup.EffectiveBreaking, matchup.EffectiveControl, _arsenal) -
                Math.Sqrt(errorX * errorX + errorY * errorY) * 24d, 0d, AttributeRating.Maximum);
            double releaseX = matchup.Pitcher.ThrowingHand == Handedness.Left ? -0.42d : 0.42d;
            bool isHitByPitch = IsHitByPitch(matchup.Batter, matchup.Pitcher.ThrowingHand, actual);
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
                PitchTypeProfileCatalog.Get(pitchType),
                GetEntry(matchup.Pitcher, pitchType).Proficiency, pitchType);
        }

        private int FillDerivedPitchOptions(
            in PlateAppearanceMatchup matchup,
            PitchOption[] destination)
        {
            for (int index = 0; index < DerivedPitchOptionCount; index++)
                destination[index] = BuildPitchOption(matchup, GetDerivedEntry(matchup.Pitcher, index));
            return DerivedPitchOptionCount;
        }

        private PitchOption BuildPitchOption(
            in PlateAppearanceMatchup matchup,
            in PitchRepertoireEntry entry)
        {
            PitchType pitchType = entry.PitchType;
            int proficiency = entry.Proficiency;
            PitchTypeProfile profile = PitchTypeProfileCatalog.Get(pitchType);
            double centerVelocity = PitchEffectivenessResolver.ResolveVelocityKph(entry,
                GetPhysicalVelocityRating(matchup), _arsenal) / 1.609344d;
            double breakScale = PitchEffectivenessResolver.ResolveMovementScale(entry,
                matchup.EffectiveBreaking, _arsenal);
            double handDirection = matchup.Pitcher.ThrowingHand == Handedness.Left ? -1d : 1d;
            return new PitchOption(
                pitchType,
                proficiency,
                entry.IsPrimary,
                centerVelocity - 1.5d,
                centerVelocity + 1.5d,
                profile.HorizontalBreak * breakScale * handDirection,
                profile.VerticalBreak * breakScale,
                profile.FatigueCost,
                CalculateCommandEllipse(matchup, profile, proficiency, pitchType),
                entry.UsagePreference,
                PitchEffectivenessResolver.ResolvePlayerQuality(entry, matchup.Pitcher, matchup.EffectiveStuff,
                    matchup.EffectiveBreaking, matchup.EffectiveControl, _arsenal),
                _arsenal.Grade.GetGrade(PitchEffectivenessResolver.ResolveStableQuality(entry,
                    matchup.Pitcher.PermanentPitcherAttributes, _arsenal, matchup.Pitcher.BakedPitcherAttributes,
                    Math.Max(1, matchup.Pitcher.PitchRepertoire.Count),
                    PitchEffectivenessResolver.GetPriority(entry, matchup.Pitcher))),
                1d + Math.Max(0d, profile.HorizontalBreak * handDirection *
                    (matchup.Batter.BattingHand == Handedness.Left ? -1d : 1d)));
        }

        private CommandEllipse CalculateCommandEllipse(
            in PlateAppearanceMatchup matchup,
            in PitchTypeProfile profile,
            int proficiency,
            PitchType pitchType)
        {
            double deviation = _balance.BaseCommandDeviation -
                               (matchup.EffectiveControl - 50d) * _balance.ControlDeviationWeight -
                               (proficiency - 50d) * 0.0008d +
                               _arsenal.Get(pitchType).ControlDifficulty;
            deviation = Clamp(
                deviation,
                _balance.MinimumCommandDeviation,
                _balance.MaximumCommandDeviation);
            return new CommandEllipse(
                deviation * profile.HorizontalErrorScale,
                deviation * profile.VerticalErrorScale,
                profile.ErrorRotationDegrees);
        }

        private PitchRepertoireEntry GetEntry(Player pitcher, PitchType pitchType)
        {
            for (int index = 0; index < pitcher.PitchRepertoire.Count; index++)
            {
                PitchRepertoireEntry entry = pitcher.PitchRepertoire[index];
                if (entry.PitchType == pitchType)
                    return entry;
            }
            if (pitcher.PitchRepertoire.Count > 0)
                throw new ArgumentException("보유하지 않은 구종은 선택할 수 없습니다.", nameof(pitchType));
            // 구종 없는 합성 로스터도 선택 화면의 숙련도와 실제 투구의 숙련도가 같아야 한다.
            for (int index = 0; index < DerivedPitchOptionCount; index++)
            {
                PitchRepertoireEntry entry = GetDerivedEntry(pitcher, index);
                if (entry.PitchType == pitchType) return entry;
            }
            return LegacyEntries[(int)pitchType, 2];
        }

        private static PitchRepertoireEntry GetDerivedEntry(Player pitcher, int index)
        {
            bool favorsBreaking = pitcher.PitcherAttributes.Breaking >= 55;
            bool favorsCommand = pitcher.PitcherAttributes.Control >= 58;
            PitchType pitchType = index switch
            {
                0 => PitchType.FourSeamFastball,
                1 => favorsBreaking ? (favorsCommand ? PitchType.Cutter : PitchType.Slider) : PitchType.TwoSeamFastball,
                2 => favorsBreaking ? PitchType.Curveball : PitchType.Changeup,
                _ => pitcher.PitcherAttributes.Stuff >= 60 ? PitchType.Splitter : PitchType.Sinker
            };
            return LegacyEntries[(int)pitchType, index];
        }

        private static double GetPhysicalVelocityRating(in PlateAppearanceMatchup matchup)
        {
            // 구속은 표시 단위이므로 분산 압축 전 값을 쓰되 현재 피로·컨디션 변화는 그대로 반영한다.
            return matchup.Pitcher.UncurvedPitcherAttributes.Velocity +
                matchup.EffectiveVelocity - matchup.Pitcher.PitcherAttributes.Velocity;
        }

        private static PitchRepertoireEntry[,] CreateLegacyEntries()
        {
            // 구종 없는 기존 테스트/세이브 경로도 반복 투구에서 enum 검증 박싱을 만들지 않는다.
            int count = Enum.GetValues(typeof(PitchType)).Length;
            var result = new PitchRepertoireEntry[count, DerivedPitchOptionCount];
            for (int index = 0; index < count; index++)
            {
                result[index, 0] = new PitchRepertoireEntry((PitchType)index, 55, true);
                result[index, 1] = new PitchRepertoireEntry((PitchType)index, 50, false);
                result[index, 2] = new PitchRepertoireEntry((PitchType)index, 46, false);
                result[index, 3] = new PitchRepertoireEntry((PitchType)index, 42, false);
            }
            return result;
        }

        private bool IsHitByPitch(Player batter, Handedness pitcherHand, PlatePoint actual)
        {
            if (Math.Abs(actual.Y) > _balance.HitByPitchMaximumHeight)
                return false;
            double batterSide = batter.BattingHand == Handedness.Left ? -1d : 1d;
            // 스위치 타자의 타석은 자신의 송구 손이 아니라 상대 투수의 손으로 결정된다.
            if (batter.BattingHand == Handedness.Switch)
                batterSide = pitcherHand == Handedness.Left ? 1d : -1d;
            if (actual.X * batterSide < _balance.HitByPitchMinimumInsideLocation)
                return false;
            return _random.NextDouble() < _balance.HitByPitchContactProbability;
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
