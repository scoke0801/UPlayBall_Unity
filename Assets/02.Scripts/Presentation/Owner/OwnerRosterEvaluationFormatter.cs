using System.Globalization;
using Baseball.Simulation.Historical;

namespace Baseball.Presentation.Owner
{
    /// <summary>Game에서 계산한 기본 전력과 편성 비용을 서로 다른 의미로 표시한다.</summary>
    public static class OwnerRosterEvaluationFormatter
    {
        /// <summary>시즌 기본 능력 평균의 대상 인원과 수치를 표시한다.</summary>
        public static string FormatStrength(RosterStrengthBreakdown strength)
        {
            return strength?.Overall == null
                ? "기본 전력 미평가"
                : $"기본 전력 {FormatRating(strength.Overall)} · {strength.PlayerCount}인 평균";
        }

        /// <summary>타자·투수의 별도 능력 평균을 표시한다.</summary>
        public static string FormatUnits(RosterStrengthBreakdown strength)
        {
            return $"야수 {FormatRating(strength?.HitterStrength)} · 투수 {FormatRating(strength?.PitcherStrength)}";
        }

        /// <summary>벤치를 제외한 편성 비용은 전력 수치와 합치지 않는다.</summary>
        public static string FormatCost(RosterCostBreakdown? cost)
        {
            return cost.HasValue ? $"편성 비용 {cost.Value.TotalCost}" : "편성 비용 미평가";
        }

        private static string FormatRating(double? value) => value?.ToString("0.0", CultureInfo.InvariantCulture) ?? "—";
    }
}
