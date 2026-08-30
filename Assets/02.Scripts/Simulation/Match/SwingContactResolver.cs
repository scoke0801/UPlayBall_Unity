using System;
using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Simulation.PlateAppearance;

namespace Baseball.Simulation.Match
{
    /// <summary>스윙 위치와 시점을 선수 능력치가 반영된 컨택 프로필로 변환한다.</summary>
    public sealed class SwingContactResolver
    {
        private readonly MiniGameBalance _balance;

        public SwingContactResolver(BalanceTable balance)
        {
            _balance = balance?.MiniGame ?? throw new ArgumentNullException(nameof(balance));
        }

        public double GetIdealSwingTime01(in PitchFlightDescriptor pitch)
        {
            return Clamp(
                1d - _balance.SwingImpactLeadMilliseconds / pitch.PlateArrivalMilliseconds,
                0.75d,
                0.97d);
        }

        /// <summary>최종 안타가 아닌 볼·스트라이크·컨택 품질까지만 판정한다.</summary>
        public ContactProfile Resolve(
            in PlateAppearanceMatchup matchup,
            in PitchFlightDescriptor pitch,
            in SwingCommand command,
            int pitchNumber)
        {
            if (pitch.IsHitByPitch)
                return CreateNoContact(PitchResult.HitByPitch, command, pitch);
            if (!command.DidSwing)
                return CreateNoContact(pitch.IsStrike ? PitchResult.CalledStrike : PitchResult.Ball, command, pitch);

            BatterAttributes batter = matchup.Batter.BatterAttributes;
            double intentRadius = GetIntentRadiusMultiplier(command.Intent, command.IsBunt);
            double ratingRadius = 1d + (batter.Contact - 50d) * _balance.ContactRadiusWeight;
            double radiusX = _balance.BaseBatRadiusX * ratingRadius * intentRadius;
            double radiusY = _balance.BaseBatRadiusY * ratingRadius * intentRadius;
            if (command.IsBunt)
            {
                radiusX *= 1.65d;
                radiusY *= 0.78d;
            }

            double differenceX = command.BatPoint.X - pitch.PlatePoint.X;
            double differenceY = command.BatPoint.Y - pitch.PlatePoint.Y;
            double normalizedLocationError = Math.Sqrt(
                differenceX * differenceX / (radiusX * radiusX) +
                differenceY * differenceY / (radiusY * radiusY));
            double idealTime = GetIdealSwingTime01(pitch);
            double timingError = (command.SwingInputTime01 - idealTime) *
                                 pitch.PlateArrivalMilliseconds;
            double timingRating = (batter.Contact - 50d) * _balance.ContactTimingWeight +
                                  (batter.Mental - 50d) * _balance.ContactTimingWeight * 0.55d;
            double validTiming = Math.Max(42d, _balance.ValidTimingMilliseconds + timingRating);
            double foulTiming = Math.Max(validTiming + 25d, _balance.FoulTimingMilliseconds + timingRating * 0.75d);
            double absoluteTiming = Math.Abs(timingError);

            SwingTimingFeedback timingFeedback = GetTimingFeedback(
                timingError,
                _balance.PerfectTimingMilliseconds,
                validTiming);
            SwingLocationFeedback locationFeedback = GetLocationFeedback(
                differenceX,
                differenceY,
                normalizedLocationError,
                matchup.Batter.BattingHand);

            bool isFairContact = normalizedLocationError <= 1d && absoluteTiming <= validTiming;
            bool isFoulContact = normalizedLocationError <= 1.48d && absoluteTiming <= foulTiming;
            // 고정 난수나 완벽히 반복되는 입력에서도 2스트라이크 파울이 무한히 이어지지 않게 한다.
            // 안전 한도에서는 마지막 커트를 약한 인플레이로 보내 기존 수비 Resolver가 끝낸다.
            if (!isFairContact &&
                isFoulContact &&
                pitchNumber >= Baseball.Core.Rules.BaseballRules.MaximumPitchesPerPlateAppearance)
            {
                isFairContact = true;
            }
            if (!isFairContact)
            {
                PitchResult missResult = isFoulContact ? PitchResult.Foul : PitchResult.SwingingStrike;
                ContactGrade missGrade = isFoulContact ? ContactGrade.FoulTip : ContactGrade.None;
                return new ContactProfile(
                    missResult,
                    missGrade,
                    timingFeedback,
                    locationFeedback,
                    timingError,
                    normalizedLocationError,
                    0d,
                    0d,
                    0d,
                    0d,
                    0d);
            }

            double positionQuality = Clamp(1d - normalizedLocationError, 0d, 1d);
            double timingQuality = Clamp(1d - absoluteTiming / validTiming, 0d, 1d);
            double zoneDistance = Math.Max(Math.Abs(pitch.PlatePoint.X), Math.Abs(pitch.PlatePoint.Y));
            double zonePenalty = zoneDistance <= 1d
                ? 0d
                : (zoneDistance - 1d) * _balance.OutOfZoneQualityPenalty;
            double quality = Clamp(
                _balance.ContactQualityBase + positionQuality * 42d + timingQuality * 30d +
                (batter.Contact - 50d) * 0.10d -
                (pitch.Quality - 50d) * 0.08d -
                zonePenalty,
                0d,
                100d);
            if (command.IsBunt)
                quality = Clamp(26d + positionQuality * 30d + (batter.Bunt - 50d) * 0.20d, 5d, 70d);
            ContactGrade grade = ResolveGrade(quality, normalizedLocationError, absoluteTiming);
            double intentVelocity = command.Intent switch
            {
                BattingApproach.Contact => -_balance.ContactIntentExitVelocityPenalty,
                BattingApproach.Power => _balance.PowerIntentExitVelocityBonus,
                BattingApproach.Aggressive => _balance.PowerIntentExitVelocityBonus * 0.55d,
                _ => 0d
            };
            double exitVelocity = command.IsBunt
                ? Clamp(28d + quality * 0.28d, 22d, 50d)
                : Clamp(
                    _balance.BaseExitVelocity +
                    (batter.Power - 50d) * _balance.PowerExitVelocityWeight +
                    (quality - 50d) * 0.18d +
                    (pitch.VelocityMph - 88d) * 0.12d +
                    intentVelocity,
                    48d,
                    121d);
            double launchAngle = command.IsBunt
                ? -8d + differenceY * 15d
                : Clamp(
                    _balance.LaunchAngleBaseDegrees -
                    differenceY * _balance.LaunchAngleLocationScale,
                    -25d,
                    58d);
            double sprayAngle = Clamp(timingError / validTiming * 46d, -55d, 55d);
            double spinRate = Clamp(1450d + Math.Abs(launchAngle) * 31d + exitVelocity * 8d, 900d, 4200d);
            PitchResult result = PitchResult.InPlay;
            return new ContactProfile(
                result,
                grade,
                timingFeedback,
                locationFeedback,
                timingError,
                normalizedLocationError,
                quality,
                exitVelocity,
                launchAngle,
                sprayAngle,
                spinRate);
        }

        private static ContactProfile CreateNoContact(
            PitchResult result,
            in SwingCommand command,
            in PitchFlightDescriptor pitch)
        {
            return new ContactProfile(
                result,
                ContactGrade.None,
                SwingTimingFeedback.Perfect,
                command.DidSwing ? SwingLocationFeedback.Missed : SwingLocationFeedback.Center,
                0d,
                command.DidSwing ? double.MaxValue : 0d,
                0d,
                0d,
                0d,
                0d,
                0d);
        }

        private double GetIntentRadiusMultiplier(BattingApproach intent, bool isBunt)
        {
            if (isBunt) return 1d;
            return intent switch
            {
                BattingApproach.Contact => _balance.ContactIntentRadiusMultiplier,
                BattingApproach.Power => _balance.PowerIntentRadiusMultiplier,
                BattingApproach.Patient => 1.04d,
                _ => 1d
            };
        }

        private static ContactGrade ResolveGrade(
            double quality,
            double normalizedLocationError,
            double absoluteTiming)
        {
            if (quality >= 88d && normalizedLocationError <= 0.35d && absoluteTiming <= 35d)
                return ContactGrade.Barrel;
            if (quality >= 70d) return ContactGrade.Solid;
            if (quality >= 48d) return ContactGrade.Normal;
            return ContactGrade.Weak;
        }

        private static SwingTimingFeedback GetTimingFeedback(
            double timingError,
            double perfectTiming,
            double validTiming)
        {
            if (timingError < -validTiming) return SwingTimingFeedback.VeryEarly;
            if (timingError < -perfectTiming) return SwingTimingFeedback.Early;
            if (timingError <= perfectTiming) return SwingTimingFeedback.Perfect;
            if (timingError <= validTiming) return SwingTimingFeedback.Late;
            return SwingTimingFeedback.VeryLate;
        }

        private static SwingLocationFeedback GetLocationFeedback(
            double differenceX,
            double differenceY,
            double normalizedError,
            Handedness battingHand)
        {
            if (normalizedError > 1.48d) return SwingLocationFeedback.Missed;
            if (Math.Abs(differenceX) <= 0.06d && Math.Abs(differenceY) <= 0.06d)
                return SwingLocationFeedback.Center;
            if (Math.Abs(differenceY) >= Math.Abs(differenceX))
                return differenceY > 0d ? SwingLocationFeedback.High : SwingLocationFeedback.Low;
            double handedDirection = battingHand == Handedness.Left ? -1d : 1d;
            return differenceX * handedDirection > 0d
                ? SwingLocationFeedback.Inside
                : SwingLocationFeedback.Outside;
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            if (value < minimum) return minimum;
            if (value > maximum) return maximum;
            return value;
        }
    }
}
