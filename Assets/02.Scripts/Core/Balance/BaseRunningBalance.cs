namespace Baseball.Core.Balance
{
    /// <summary>
    /// 안타와 타구 아웃 때 주자의 추가 진루 확률 계수를 보관한다.
    /// </summary>
    public readonly struct BaseRunningBalance
    {
        /// <summary>
        /// 주루 판단 확률 모델의 계수를 생성한다.
        /// </summary>
        public BaseRunningBalance(
            double singleFromSecondScoreProbability,
            double singleFromFirstToThirdProbability,
            double doubleFromFirstScoreProbability,
            double sacrificeFlyProbability,
            double groundOutFromThirdScoreProbability,
            double groundOutAdvanceProbability,
            double doublePlayProbability,
            double runnerSpeedWeight,
            double defenseWeight,
            double doublePlayRunnerSpeedWeight,
            double doublePlayDefenseWeight)
        {
            SingleFromSecondScoreProbability = singleFromSecondScoreProbability;
            SingleFromFirstToThirdProbability = singleFromFirstToThirdProbability;
            DoubleFromFirstScoreProbability = doubleFromFirstScoreProbability;
            SacrificeFlyProbability = sacrificeFlyProbability;
            GroundOutFromThirdScoreProbability = groundOutFromThirdScoreProbability;
            GroundOutAdvanceProbability = groundOutAdvanceProbability;
            DoublePlayProbability = doublePlayProbability;
            RunnerSpeedWeight = runnerSpeedWeight;
            DefenseWeight = defenseWeight;
            DoublePlayRunnerSpeedWeight = doublePlayRunnerSpeedWeight;
            DoublePlayDefenseWeight = doublePlayDefenseWeight;
        }

        public double SingleFromSecondScoreProbability { get; }
        public double SingleFromFirstToThirdProbability { get; }
        public double DoubleFromFirstScoreProbability { get; }
        public double SacrificeFlyProbability { get; }
        public double GroundOutFromThirdScoreProbability { get; }
        public double GroundOutAdvanceProbability { get; }
        public double DoublePlayProbability { get; }
        public double RunnerSpeedWeight { get; }
        public double DefenseWeight { get; }
        public double DoublePlayRunnerSpeedWeight { get; }
        public double DoublePlayDefenseWeight { get; }
    }
}
