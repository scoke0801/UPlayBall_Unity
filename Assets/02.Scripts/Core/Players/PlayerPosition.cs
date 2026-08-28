namespace Baseball.Core.Players
{
    /// <summary>
    /// 새 게임에서 먼저 선택하는 선수의 경기 역할을 구분한다.
    /// </summary>
    public enum PlayerType
    {
        Batter,
        Pitcher
    }

    /// <summary>
    /// MVP 로스터에서 사용하는 수비 포지션과 투수 역할을 정의한다.
    /// </summary>
    public enum PlayerPosition
    {
        Unknown = 0,
        Catcher = 1,
        FirstBase = 2,
        SecondBase = 3,
        ThirdBase = 4,
        Shortstop = 5,
        LeftField = 6,
        CenterField = 7,
        RightField = 8,
        DesignatedHitter = 9,
        StartingPitcher = 10,
        ReliefPitcher = 11
    }

    /// <summary>
    /// 선수의 타격 또는 투구 손을 나타낸다.
    /// </summary>
    public enum Handedness
    {
        Right = 0,
        Left = 1,
        Switch = 2
    }
}
