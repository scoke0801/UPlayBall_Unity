namespace Baseball.Core.Teams
{
    /// <summary>
    /// 투수의 등판 금지 조건이 아니라 감독 AI가 기대하는 임무를 정의한다.
    /// </summary>
    public enum PitcherRole
    {
        Starter = 0,
        Swingman = 1,
        LongRelief = 2,
        MiddleRelief = 3,
        Setup = 4,
        Closer = 5
    }
}
