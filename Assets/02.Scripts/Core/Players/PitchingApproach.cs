namespace Baseball.Core.Players
{
    /// <summary>
    /// 투수가 한 타석에서 선택하는 승부 방침을 정의한다.
    /// </summary>
    public enum PitchingApproach
    {
        Balanced = 0,
        AttackZone = 1,
        Nibble = 2,
        Strikeout = 3,
        PitchAround = 4,
        GroundBall = 5,

        // 신규 커리어 화면의 표현은 플레이어 의도를 설명하고, 현재 시뮬레이션 전술값을 재사용한다.
        ControlFirst = AttackZone,
        InduceChase = Nibble,
        FullPower = Strikeout,
        QuickAttack = GroundBall
    }

    /// <summary>
    /// 주자가 추가 베이스를 노릴 때 적용할 기본 위험 감수 수준을 정의한다.
    /// </summary>
    public enum RunningApproach
    {
        Conservative = 0,
        Balanced = 1,
        Aggressive = 2
    }
}
