namespace Baseball.Core.Rules
{
    /// <summary>
    /// 경기 시뮬레이션이 공유하는 야구 규칙 계약을 정의한다.
    /// </summary>
    public static class BaseballRules
    {
        public const int BattingOrderSize = 9;
        public const int OutsPerHalfInning = 3;
        public const int BallsForWalk = 4;
        public const int StrikesForStrikeout = 3;
        public const int RegulationInnings = 9;
        public const int MaximumInnings = 12;
        public const int MaximumPitchesPerPlateAppearance = 32;
    }
}
