namespace Baseball.Core.Historical
{
    /// <summary>원본 선수 시즌 평가로 고정되는 선호 타순 구간이다. 투수는 None이다.</summary>
    public enum PreferredBattingOrder
    {
        None,
        Upper,
        Cleanup,
        Lower
    }

    /// <summary>실제 선발 타순과 선호 구간의 일치 여부다. 벤치·투수는 적용하지 않는다.</summary>
    public enum BattingOrderFit { NotApplicable, Preferred, Mismatch }

    /// <summary>1부터 시작하는 실제 타순을 선호 구간으로 변환한다.</summary>
    public static class PreferredBattingOrderRule
    {
        /// <summary>벤치와 유효하지 않은 타순은 적용 대상에서 제외한다.</summary>
        public static PreferredBattingOrder GetGroup(int battingOrder)
        {
            if (battingOrder < 1 || battingOrder > 9) return PreferredBattingOrder.None;
            if (battingOrder <= 2) return PreferredBattingOrder.Upper;
            return battingOrder <= 5 ? PreferredBattingOrder.Cleanup : PreferredBattingOrder.Lower;
        }

        /// <summary>선호 구간에 실제로 배치된 타자인지 확인한다.</summary>
        public static bool IsMatch(PreferredBattingOrder preference, int battingOrder) =>
            preference != PreferredBattingOrder.None && preference == GetGroup(battingOrder);

        /// <summary>컨디션에 적용할 타순 적합도를 구한다.</summary>
        public static BattingOrderFit GetFit(PreferredBattingOrder preference, int battingOrder) =>
            preference == PreferredBattingOrder.None || GetGroup(battingOrder) == PreferredBattingOrder.None
                ? BattingOrderFit.NotApplicable : IsMatch(preference, battingOrder)
                ? BattingOrderFit.Preferred : BattingOrderFit.Mismatch;
    }
}
