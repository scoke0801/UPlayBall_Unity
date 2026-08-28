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
            return programId switch
            {
                "personal_batting" => "기초 타격 훈련",
                "personal_pitching" => "기초 투구 밸런스",
                "bat_balance_training" => "기초 밸런스 훈련",
                "bat_power_camp" => "파워 집중 캠프",
                "bat_contact_training" => "컨택 안정화 훈련",
                "bat_speed_defense_camp" => "주루·수비 강화 캠프",
                "bat_elite_hitting_lab" => "엘리트 타격 랩",
                "pitch_velocity_camp" => "구속 집중 캠프",
                "pitch_control_training" => "제구 안정화 훈련",
                "pitch_stamina_camp" => "체력 강화 캠프",
                "pitch_breaking_training" => "변화구 집중 훈련",
                "pitch_elite_biomechanics" => "엘리트 바이오메카닉스 랩",
                "partner_batter_default" => "베테랑 타자 합동 훈련",
                "partner_pitcher_default" => "베테랑 투수 합동 훈련",
                "private_batting_coach" => "전담 타격 코치",
                "private_pitching_coach" => "전담 피칭 코치",
                "japan_batting_camp" => "동아시아 컨택 캠프",
                "japan_pitch_design" => "동아시아 제구 캠프",
                "usa_power_center" => "북미 파워 아카데미",
                "usa_velocity_center" => "북미 파워 아카데미",
                "usa_elite_batting_academy" => "북미 엘리트 타격 아카데미",
                "usa_elite_pitching_academy" => "북미 엘리트 피칭 아카데미",
                "caribbean_batting_league" => "카리브 실전 리그",
                "caribbean_pitch_league" => "카리브 실전 리그",
                "europe_batting_balance" => "유럽 밸런스 프로그램",
                "europe_pitch_balance" => "유럽 밸런스 프로그램",
                "rehab_general" => "재활·컨디션 관리",
                "sports_science_recovery" => "스포츠 사이언스 회복",
                "rest" => "휴식",
                _ => programId
            };
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
