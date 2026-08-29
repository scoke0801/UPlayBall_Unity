namespace Baseball.Game.Career
{
    /// <summary>성장 화면·연출·뉴스가 같은 오프시즌 활동 명칭을 사용하게 한다.</summary>
    public static class GrowthProgramNameFormatter
    {
        public static string GetLabel(string programId)
        {
            return programId switch
            {
                "weight_batter" => "타격 웨이트 트레이닝",
                "weight_pitcher" => "투수 웨이트 트레이닝",
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
                _ => string.IsNullOrWhiteSpace(programId) ? "오프시즌 활동" : programId
            };
        }
    }
}
