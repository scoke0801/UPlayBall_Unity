namespace Baseball.Core.Players
{
    /// <summary>
    /// 타자가 한 투구에 적용할 타격 접근 방식을 정의한다.
    /// </summary>
    public enum BattingApproach
    {
        Balanced = 0,
        Contact = 1,
        Power = 2,
        Patient = 3,
        Aggressive = 4,
        Bunt = 5
    }
}
