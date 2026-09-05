using System;
using System.Globalization;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Career;

namespace Baseball.Presentation.SharedScreens
{
    /// <summary>
    /// Career 도메인 값을 공용 Snapshot의 한국어 표시 문자열로 변환한다.
    /// </summary>
    public static class CareerSharedSnapshotFormatters
    {
        public static string FormatId(int value) => value.ToString(CultureInfo.InvariantCulture);

        public static string FormatLeague(LeagueLevel level) =>
            WorldGenerationConfiguration.GetDefaultDefinition(level).UiDisplayName;

        public static string FormatTeamColor(TeamColor color) =>
            $"#{color.Red:X2}{color.Green:X2}{color.Blue:X2}";

        public static string FormatTeamEmblemKey(int emblemId) =>
            emblemId > 0 ? $"TeamEmblem/{emblemId.ToString(CultureInfo.InvariantCulture)}" : string.Empty;

        public static string FormatRate(double value) =>
            value.ToString(".000", CultureInfo.InvariantCulture);

        public static string FormatDecimal(double value) =>
            value.ToString("0.00", CultureInfo.InvariantCulture);

        public static string FormatInnings(int outs) =>
            $"{outs / 3}.{outs % 3}";

        public static string FormatPosition(PlayerPosition position)
        {
            return position switch
            {
                PlayerPosition.Catcher => "C",
                PlayerPosition.FirstBase => "1B",
                PlayerPosition.SecondBase => "2B",
                PlayerPosition.ThirdBase => "3B",
                PlayerPosition.Shortstop => "SS",
                PlayerPosition.LeftField => "LF",
                PlayerPosition.CenterField => "CF",
                PlayerPosition.RightField => "RF",
                PlayerPosition.DesignatedHitter => "DH",
                PlayerPosition.StartingPitcher => "SP",
                PlayerPosition.ReliefPitcher => "RP",
                _ => "-"
            };
        }

        public static string FormatRosterRole(TeamRosterRole role)
        {
            return role switch
            {
                TeamRosterRole.Starting => "주전",
                TeamRosterRole.Rotation => "선발진",
                TeamRosterRole.Bullpen => "불펜",
                TeamRosterRole.Competition => "경쟁",
                _ => "백업"
            };
        }

        public static string FormatGameRole(PlayerGameRole role)
        {
            return role switch
            {
                PlayerGameRole.StartingBatter => "선발 타자",
                PlayerGameRole.StartingPitcher => "선발 투수",
                PlayerGameRole.ReliefPitcher => "구원 투수",
                PlayerGameRole.Bench => "벤치",
                PlayerGameRole.PitcherRest => "투수 휴식",
                _ => "미출장"
            };
        }

        public static string FormatExpectedRole(ExpectedRole role)
        {
            return role switch
            {
                ExpectedRole.StartingCompetition => "주전 경쟁",
                ExpectedRole.RosterCompetition => "로스터 경쟁",
                _ => "벤치 경쟁"
            };
        }

        public static string FormatAbility(PlayerAbility ability)
        {
            return ability switch
            {
                PlayerAbility.Contact => "컨택",
                PlayerAbility.Power => "장타",
                PlayerAbility.Speed => "주루",
                PlayerAbility.Arm => "송구",
                PlayerAbility.Defense => "수비",
                PlayerAbility.BatterMental => "정신력",
                PlayerAbility.Stamina => "체력",
                PlayerAbility.Velocity => "구속",
                PlayerAbility.Stuff => "구위",
                PlayerAbility.Breaking => "변화구",
                PlayerAbility.Control => "제구",
                PlayerAbility.PitcherMental => "위기관리",
                _ => "-"
            };
        }

        public static string FormatWorkEthic(WorkEthicGrade grade)
        {
            return grade switch
            {
                WorkEthicGrade.VeryDiligent => "매우 성실",
                WorkEthicGrade.Diligent => "성실",
                WorkEthicGrade.Inconsistent => "기복 있음",
                _ => "보통"
            };
        }

        public static string FormatPlayerType(PlayerType playerType) =>
            playerType == PlayerType.Pitcher ? "투수" : "타자";

        public static string FormatHandedness(Handedness handedness)
        {
            return handedness switch
            {
                Handedness.Left => "좌",
                Handedness.Switch => "양",
                _ => "우"
            };
        }

        public static string FormatCareerPhase(CareerPhase phase)
        {
            return phase switch
            {
                CareerPhase.Growth => "성장기",
                CareerPhase.Prime => "전성기",
                CareerPhase.Skilled => "숙련기",
                CareerPhase.Decline => "하락기",
                _ => "커리어 후반"
            };
        }

        public static string FormatRecordCategory(CareerRecordCategory category)
        {
            return category switch
            {
                CareerRecordCategory.Batting => "타자",
                CareerRecordCategory.Pitching => "투수",
                CareerRecordCategory.Fielding => "수비",
                _ => "주루"
            };
        }

        public static string FormatMetricLabel(CareerRecordMetric metric)
        {
            return metric switch
            {
                CareerRecordMetric.Games => "경기",
                CareerRecordMetric.GamesStarted => "선발",
                CareerRecordMetric.PlateAppearances => "타석",
                CareerRecordMetric.AtBats => "타수",
                CareerRecordMetric.Runs => "득점",
                CareerRecordMetric.Hits => "안타",
                CareerRecordMetric.Singles => "단타",
                CareerRecordMetric.Doubles => "2루타",
                CareerRecordMetric.Triples => "3루타",
                CareerRecordMetric.HomeRuns => "홈런",
                CareerRecordMetric.RunsBattedIn => "타점",
                CareerRecordMetric.Walks => "볼넷",
                CareerRecordMetric.HitByPitches => "사구",
                CareerRecordMetric.BattingStrikeouts => "삼진",
                CareerRecordMetric.SacrificeFlies => "희생플라이",
                CareerRecordMetric.GroundedIntoDoublePlays => "병살타",
                CareerRecordMetric.TotalBases => "루타",
                CareerRecordMetric.BattingAverage => "타율",
                CareerRecordMetric.OnBasePercentage => "출루율",
                CareerRecordMetric.SluggingPercentage => "장타율",
                CareerRecordMetric.OnBasePlusSlugging => "출루+장타",
                CareerRecordMetric.WalkStrikeoutRatio => "볼넷/삼진",
                CareerRecordMetric.PitchingAppearances => "등판",
                CareerRecordMetric.PitchingStarts => "선발",
                CareerRecordMetric.OutsRecorded => "이닝",
                CareerRecordMetric.Wins => "승",
                CareerRecordMetric.Losses => "패",
                CareerRecordMetric.Saves => "세이브",
                CareerRecordMetric.Holds => "홀드",
                CareerRecordMetric.BlownSaves => "블론",
                CareerRecordMetric.HitsAllowed => "피안타",
                CareerRecordMetric.HomeRunsAllowed => "피홈런",
                CareerRecordMetric.RunsAllowed => "실점",
                CareerRecordMetric.EarnedRuns => "자책",
                CareerRecordMetric.WalksAllowed => "볼넷 허용",
                CareerRecordMetric.HitBatters => "사구 허용",
                CareerRecordMetric.PitchingStrikeouts => "탈삼진",
                CareerRecordMetric.BattersFaced => "상대타자",
                CareerRecordMetric.QualityStarts => "퀄리티스타트",
                CareerRecordMetric.EarnedRunAverage => "평균자책",
                CareerRecordMetric.WalksHitsPerInningPitched => "이닝당출루",
                CareerRecordMetric.StrikeoutWalkRatio => "삼진/볼넷",
                CareerRecordMetric.HomeRunsPerNineInnings => "피홈런/9",
                CareerRecordMetric.DefensiveOuts => "수비이닝",
                CareerRecordMetric.FieldingOpportunities => "수비기회",
                CareerRecordMetric.SuccessfulFieldingPlays => "처리성공",
                CareerRecordMetric.Putouts => "자살",
                CareerRecordMetric.Assists => "보살",
                CareerRecordMetric.Errors => "실책",
                CareerRecordMetric.DoublePlays => "병살",
                CareerRecordMetric.DifficultPlayAttempts => "어려운타구",
                CareerRecordMetric.DifficultPlaysMade => "호수비",
                CareerRecordMetric.ExpectedOuts => "기대아웃",
                CareerRecordMetric.EstimatedRunsSaved => "실점억제",
                CareerRecordMetric.FieldingSuccessRate => "수비율",
                CareerRecordMetric.StolenBases => "도루",
                CareerRecordMetric.CaughtStealing => "도루실패",
                CareerRecordMetric.StolenBasePercentage => "도루성공률",
                _ => "-"
            };
        }

        public static string FormatMetricValue(CareerRecordMetric metric, double value)
        {
            return metric switch
            {
                CareerRecordMetric.BattingAverage or
                CareerRecordMetric.OnBasePercentage or
                CareerRecordMetric.SluggingPercentage or
                CareerRecordMetric.OnBasePlusSlugging or
                CareerRecordMetric.FieldingSuccessRate or
                CareerRecordMetric.StolenBasePercentage => FormatRate(value),
                CareerRecordMetric.EarnedRunAverage or
                CareerRecordMetric.WalksHitsPerInningPitched or
                CareerRecordMetric.WalkStrikeoutRatio or
                CareerRecordMetric.StrikeoutWalkRatio or
                CareerRecordMetric.HomeRunsPerNineInnings or
                CareerRecordMetric.ExpectedOuts or
                CareerRecordMetric.EstimatedRunsSaved => FormatDecimal(value),
                CareerRecordMetric.OutsRecorded or CareerRecordMetric.DefensiveOuts =>
                    FormatInnings((int)Math.Round(value)),
                _ => Math.Round(value).ToString("0", CultureInfo.InvariantCulture)
            };
        }
    }
}
