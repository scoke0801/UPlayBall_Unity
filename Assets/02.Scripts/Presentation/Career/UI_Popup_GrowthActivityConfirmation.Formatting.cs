using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Game.Career;
using UnityEngine;

namespace Baseball.Presentation.Career
{
    public sealed partial class UI_Popup_GrowthActivityConfirmation
    {
        private static string FormatMoney(long amount)
        {
            return amount >= 100_000_000L
                ? $"{amount / 100_000_000d:0.##}억원"
                : $"{amount / 10_000d:N0}만원";
        }

        private static string GetProgramDescription(GrowthProgramView selected)
        {
            if (selected.ActivityType == OffseasonActivityType.Study)
            {
                return $"훈련 적합도 {GetFitLabel(selected.Fit)} · 나이와 Potential 여유를 반영한 " +
                       "장기 프로그램입니다.";
            }
            if (selected.ActivityType == OffseasonActivityType.Rehabilitation)
                return "성장보다 다음 활동을 이어 갈 컨디션과 오프시즌 시간을 확보하는 프로그램입니다.";
            if (selected.ActivityType == OffseasonActivityType.TrainingPartner)
                return "높은 비용으로 코치·파트너의 전문성과 Potential 성장 기회를 확보합니다.";
            if (selected.ActivityType == OffseasonActivityType.Rest)
                return "비용 없이 한 주를 사용해 컨디션을 회복합니다.";
            return selected.Intensity switch
            {
                TrainingIntensity.Safe => "기간을 더 쓰는 대신 비용과 컨디션 부담을 낮춘 안정 훈련입니다.",
                TrainingIntensity.Intensive => "기간을 압축하는 대신 비용·컨디션·부상 위험을 감수하는 집중 훈련입니다.",
                _ => "기간·비용·컨디션 부담이 기준값인 표준 훈련입니다."
            };
        }

        private static string GetProgramLabel(string programId)
        {
            return GrowthProgramNameFormatter.GetLabel(programId);
        }

        private static string GetAbilityLabel(PlayerAbility ability)
        {
            return ability switch
            {
                PlayerAbility.Contact => "컨택",
                PlayerAbility.Power => "파워",
                PlayerAbility.Speed => "주루",
                PlayerAbility.Bunt => "번트",
                PlayerAbility.Defense => "수비",
                PlayerAbility.BatterMental => "선구",
                PlayerAbility.Stamina => "체력",
                PlayerAbility.Velocity => "구속",
                PlayerAbility.Stuff => "구위",
                PlayerAbility.Breaking => "변화구",
                PlayerAbility.Control => "제구",
                PlayerAbility.PitcherMental => "위기관리",
                _ => ability.ToString()
            };
        }

        private static string GetPositionLabel(PlayerPosition position)
        {
            return position switch
            {
                PlayerPosition.StartingPitcher => "선발투수",
                PlayerPosition.ReliefPitcher => "구원투수",
                PlayerPosition.Catcher => "포수",
                PlayerPosition.FirstBase => "1루수",
                PlayerPosition.SecondBase => "2루수",
                PlayerPosition.ThirdBase => "3루수",
                PlayerPosition.Shortstop => "유격수",
                PlayerPosition.LeftField => "좌익수",
                PlayerPosition.CenterField => "중견수",
                PlayerPosition.RightField => "우익수",
                PlayerPosition.DesignatedHitter => "지명타자",
                _ => position.ToString()
            };
        }

        private static string GetIntensityLabel(TrainingIntensity intensity)
        {
            return intensity switch
            {
                TrainingIntensity.Safe => "안정",
                TrainingIntensity.Standard => "표준",
                TrainingIntensity.Intensive => "집중",
                _ => intensity.ToString()
            };
        }

        private static string GetFitLabel(TrainingFitGrade fit)
        {
            return fit switch
            {
                TrainingFitGrade.Low => "낮음",
                TrainingFitGrade.Normal => "보통",
                TrainingFitGrade.High => "높음",
                TrainingFitGrade.VeryHigh => "매우 높음",
                _ => fit.ToString()
            };
        }

        private static Color GetFitColor(TrainingFitGrade fit)
        {
            return fit switch
            {
                TrainingFitGrade.Low => WarningColor,
                TrainingFitGrade.Normal => SecondaryTextColor,
                TrainingFitGrade.High => CyanColor,
                TrainingFitGrade.VeryHigh => AccentColor,
                _ => SecondaryTextColor
            };
        }

        private static string GetRiskLabel(double risk)
        {
            if (risk <= 0d) return "없음";
            if (risk < 0.015d) return "낮음";
            if (risk < 0.03d) return "보통";
            return "높음";
        }

        private static Color GetRiskColor(double risk)
        {
            if (risk < 0.015d) return CyanColor;
            if (risk < 0.03d) return WarningColor;
            return ErrorColor;
        }

        private static Color GetConditionColor(GrowthProgramView selected)
        {
            if (selected.IsConditionDanger) return ErrorColor;
            if (selected.IsConditionWarning) return WarningColor;
            return CyanColor;
        }

        private static string GetTimelineActivityLabel(OffseasonActivityType type)
        {
            return type == OffseasonActivityType.Study ? "유학" : "훈련";
        }

        private static string FormatSigned(int value)
        {
            return value > 0 ? $"+{value}" : value.ToString();
        }
    }
}
