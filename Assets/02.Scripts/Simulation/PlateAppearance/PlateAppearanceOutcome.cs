namespace Baseball.Simulation.PlateAppearance
{
    /// <summary>
    /// 타석의 최종 결과와 투구 수를 반환한다.
    /// </summary>
    public readonly struct PlateAppearanceOutcome
    {
        /// <summary>
        /// 완료된 타석 결과를 생성한다.
        /// </summary>
        public PlateAppearanceOutcome(
            PlateAppearanceResult result,
            int pitchCount,
            int finalBalls,
            int finalStrikes)
        {
            Result = result;
            PitchCount = pitchCount;
            FinalBalls = finalBalls;
            FinalStrikes = finalStrikes;
        }

        public PlateAppearanceResult Result { get; }
        public int PitchCount { get; }
        public int FinalBalls { get; }
        public int FinalStrikes { get; }
    }
}
