namespace Baseball.Simulation.PlateAppearance
{
    /// <summary>
    /// 한 타석이 종료되는 공식 결과를 정의한다.
    /// </summary>
    public enum PlateAppearanceResult
    {
        None = 0,
        Walk = 1,
        Strikeout = 2,
        GroundOut = 3,
        FlyOut = 4,
        Single = 5,
        Double = 6,
        Triple = 7,
        HomeRun = 8,
        HitByPitch = 9
    }
}
