namespace Baseball.Simulation.PlateAppearance
{
    /// <summary>
    /// MatchSimulator가 투구와 인플레이 결과를 분리해 소비하는 타석 계약이다.
    /// </summary>
    public interface IPlateAppearanceSimulator
    {
        /// <summary>
        /// 현재 Count에서 다음 투구 결과를 계산한다.
        /// </summary>
        PitchResult SimulatePitch(
            in PlateAppearanceMatchup matchup,
            int balls,
            int strikes,
            int pitchNumber);

        /// <summary>
        /// 공정 타구가 된 Contact의 최종 결과를 계산한다.
        /// </summary>
        PlateAppearanceResult ResolveBallInPlay(in PlateAppearanceMatchup matchup);
    }
}
