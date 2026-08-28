namespace Baseball.Simulation.PlateAppearance
{
    /// <summary>
    /// 한 구의 판정 결과를 정의한다.
    /// </summary>
    public enum PitchResult
    {
        None = 0,
        Ball = 1,
        CalledStrike = 2,
        SwingingStrike = 3,
        Foul = 4,
        InPlay = 5
    }
}
