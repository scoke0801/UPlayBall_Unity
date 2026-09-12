using System;

namespace Baseball.Core.Balance
{
    /// <summary>다른 조의 타석 결과·추정 투구 수를 상세 엔진 분포에 맞추는 독립 보정표다.</summary>
    public sealed class AggregateMatchBalance
    {
        // 고능력 로스터에서도 삼진이 사라지지 않도록 상세/간이 각 28,000경기로 민감도를 대조했다.
        // 근거: docs/reports/pitch-batting-balance-20260912.md.
        public AggregateMatchBalance(double walkRate = 0.09, double strikeoutRate = 0.18,
            double hitByPitchRate = 0.008, double controlWalkWeight = 0.022,
            double mentalWalkWeight = 0.01, double contactStrikeoutWeight = 0.035,
            double stuffStrikeoutWeight = 0.025, double velocityStrikeoutWeight = 0.007,
            double ballQualityAdjustment = 15, double homeRunMultiplier = 0.75,
            double inPlayPitchMean = 2.65, double strikeoutPitchMean = 4.8,
            double walkPitchMean = 5.6, double controlHitByPitchWeight = 0.17,
            double maximumHitByPitchRate = 0.04)
        {
            double[] values = { walkRate, strikeoutRate, hitByPitchRate, controlWalkWeight,
                mentalWalkWeight, contactStrikeoutWeight, stuffStrikeoutWeight, velocityStrikeoutWeight,
                ballQualityAdjustment, homeRunMultiplier, inPlayPitchMean, strikeoutPitchMean, walkPitchMean,
                controlHitByPitchWeight, maximumHitByPitchRate };
            foreach (double value in values)
                if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(values));
            if (walkRate <= 0 || strikeoutRate <= 0 || hitByPitchRate < 0 ||
                walkRate + strikeoutRate + hitByPitchRate >= 1 || homeRunMultiplier <= 0 ||
                homeRunMultiplier > 5 || Math.Abs(ballQualityAdjustment) > 100 ||
                inPlayPitchMean < 1 || strikeoutPitchMean < 3 || walkPitchMean < 4 ||
                inPlayPitchMean > 10 || strikeoutPitchMean > 10 || walkPitchMean > 10 ||
                maximumHitByPitchRate <= 0 || maximumHitByPitchRate >= 1)
                throw new ArgumentOutOfRangeException(nameof(walkRate));
            double[] weights = { controlWalkWeight, mentalWalkWeight, contactStrikeoutWeight,
                stuffStrikeoutWeight, velocityStrikeoutWeight, controlHitByPitchWeight };
            foreach (double weight in weights)
                if (weight < 0 || weight > 1) throw new ArgumentOutOfRangeException(nameof(weights));
            WalkRate = walkRate;
            StrikeoutRate = strikeoutRate;
            HitByPitchRate = hitByPitchRate;
            ControlWalkWeight = controlWalkWeight;
            MentalWalkWeight = mentalWalkWeight;
            ContactStrikeoutWeight = contactStrikeoutWeight;
            StuffStrikeoutWeight = stuffStrikeoutWeight;
            VelocityStrikeoutWeight = velocityStrikeoutWeight;
            BallQualityAdjustment = ballQualityAdjustment;
            HomeRunMultiplier = homeRunMultiplier;
            InPlayPitchMean = inPlayPitchMean;
            StrikeoutPitchMean = strikeoutPitchMean;
            WalkPitchMean = walkPitchMean;
            ControlHitByPitchWeight = controlHitByPitchWeight;
            MaximumHitByPitchRate = maximumHitByPitchRate;
        }

        public double WalkRate { get; }
        public double StrikeoutRate { get; }
        public double HitByPitchRate { get; }
        public double ControlWalkWeight { get; }
        public double MentalWalkWeight { get; }
        public double ContactStrikeoutWeight { get; }
        public double StuffStrikeoutWeight { get; }
        public double VelocityStrikeoutWeight { get; }
        public double BallQualityAdjustment { get; }
        public double HomeRunMultiplier { get; }
        public double InPlayPitchMean { get; }
        public double StrikeoutPitchMean { get; }
        public double WalkPitchMean { get; }
        public double ControlHitByPitchWeight { get; }
        public double MaximumHitByPitchRate { get; }

        /// <summary>상세 엔진 비교 진단의 기준값. 실제 구단주 게임은 저작 JSON으로 주입한다.</summary>
        public static AggregateMatchBalance CreateDefault() => new AggregateMatchBalance();
    }
}
