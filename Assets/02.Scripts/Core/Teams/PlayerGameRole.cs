namespace Baseball.Core.Teams
{
    /// <summary>
    /// 감독 AI가 한 경기에서 내 선수에게 부여한 실제 기용 역할을 구분한다.
    /// </summary>
    public enum PlayerGameRole
    {
        Inactive,
        Bench,
        StartingBatter,
        StartingPitcher,
        ReliefPitcher,
        PitcherRest
    }
}
