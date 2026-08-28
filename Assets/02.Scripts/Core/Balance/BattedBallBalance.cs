namespace Baseball.Core.Balance
{
    /// <summary>
    /// 인플레이 타구가 안타와 장타로 변환되는 계수를 보관한다.
    /// </summary>
    public readonly struct BattedBallBalance
    {
        /// <summary>
        /// 타구 결과 확률 모델의 계수를 생성한다.
        /// </summary>
        public BattedBallBalance(
            double homeRunProbability,
            double powerHomeRunWeight,
            double breakingHomeRunWeight,
            double nonHomeRunHitProbability,
            double contactHitWeight,
            double breakingHitWeight,
            double defenseHitWeight,
            double doubleShare,
            double powerDoubleWeight,
            double breakingDoubleWeight,
            double tripleShare,
            double speedTripleWeight,
            double groundOutShare,
            double breakingGroundOutWeight,
            double powerGroundOutWeight)
        {
            HomeRunProbability = homeRunProbability;
            PowerHomeRunWeight = powerHomeRunWeight;
            BreakingHomeRunWeight = breakingHomeRunWeight;
            NonHomeRunHitProbability = nonHomeRunHitProbability;
            ContactHitWeight = contactHitWeight;
            BreakingHitWeight = breakingHitWeight;
            DefenseHitWeight = defenseHitWeight;
            DoubleShare = doubleShare;
            PowerDoubleWeight = powerDoubleWeight;
            BreakingDoubleWeight = breakingDoubleWeight;
            TripleShare = tripleShare;
            SpeedTripleWeight = speedTripleWeight;
            GroundOutShare = groundOutShare;
            BreakingGroundOutWeight = breakingGroundOutWeight;
            PowerGroundOutWeight = powerGroundOutWeight;
        }

        public double HomeRunProbability { get; }
        public double PowerHomeRunWeight { get; }
        public double BreakingHomeRunWeight { get; }
        public double NonHomeRunHitProbability { get; }
        public double ContactHitWeight { get; }
        public double BreakingHitWeight { get; }
        public double DefenseHitWeight { get; }
        public double DoubleShare { get; }
        public double PowerDoubleWeight { get; }
        public double BreakingDoubleWeight { get; }
        public double TripleShare { get; }
        public double SpeedTripleWeight { get; }
        public double GroundOutShare { get; }
        public double BreakingGroundOutWeight { get; }
        public double PowerGroundOutWeight { get; }
    }
}
