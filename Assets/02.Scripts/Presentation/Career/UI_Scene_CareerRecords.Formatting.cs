using System;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Career;

namespace Baseball.Presentation.Career
{
    public sealed partial class UI_Scene_CareerRecords
    {
        private static string GetCategoryLabel(CareerRecordCategory category)
        {
            return category switch
            {
                CareerRecordCategory.Batting => "타자",
                CareerRecordCategory.Pitching => "투수",
                CareerRecordCategory.Fielding => "수비",
                _ => "주루"
            };
        }

        private static string GetMetricLabel(CareerRecordMetric metric, bool longLabel)
        {
            if (!longLabel)
            {
                return metric switch
                {
                    CareerRecordMetric.Games => "G",
                    CareerRecordMetric.AtBats => "AB",
                    CareerRecordMetric.Runs => "R",
                    CareerRecordMetric.Hits => "H",
                    CareerRecordMetric.Doubles => "2B",
                    CareerRecordMetric.Triples => "3B",
                    CareerRecordMetric.HomeRuns => "HR",
                    CareerRecordMetric.RunsBattedIn => "RBI",
                    CareerRecordMetric.Walks => "BB",
                    CareerRecordMetric.BattingStrikeouts => "SO",
                    CareerRecordMetric.BattingAverage => "AVG",
                    CareerRecordMetric.OnBasePercentage => "OBP",
                    CareerRecordMetric.SluggingPercentage => "SLG",
                    CareerRecordMetric.OnBasePlusSlugging => "OPS",
                    CareerRecordMetric.PitchingAppearances => "G",
                    CareerRecordMetric.PitchingStarts => "GS",
                    CareerRecordMetric.OutsRecorded => "IP",
                    CareerRecordMetric.Wins => "W",
                    CareerRecordMetric.Losses => "L",
                    CareerRecordMetric.Saves => "SV",
                    CareerRecordMetric.Holds => "HLD",
                    CareerRecordMetric.HitsAllowed => "H",
                    CareerRecordMetric.EarnedRuns => "ER",
                    CareerRecordMetric.WalksAllowed => "BB",
                    CareerRecordMetric.PitchingStrikeouts => "SO",
                    CareerRecordMetric.EarnedRunAverage => "ERA",
                    CareerRecordMetric.WalksHitsPerInningPitched => "WHIP",
                    CareerRecordMetric.FieldingOpportunities => "TC",
                    CareerRecordMetric.SuccessfulFieldingPlays => "PLAY",
                    CareerRecordMetric.Putouts => "PO",
                    CareerRecordMetric.Assists => "A",
                    CareerRecordMetric.Errors => "E",
                    CareerRecordMetric.DoublePlays => "DP",
                    CareerRecordMetric.EstimatedRunsSaved => "ERS",
                    CareerRecordMetric.FieldingSuccessRate => "FLD%",
                    CareerRecordMetric.StolenBases => "SB",
                    CareerRecordMetric.CaughtStealing => "CS",
                    CareerRecordMetric.StolenBasePercentage => "SB%",
                    _ => "-"
                };
            }
            return metric switch
            {
                CareerRecordMetric.Games => "경기",
                CareerRecordMetric.AtBats => "타수",
                CareerRecordMetric.Runs => "득점",
                CareerRecordMetric.Hits => "안타",
                CareerRecordMetric.Doubles => "2루타",
                CareerRecordMetric.Triples => "3루타",
                CareerRecordMetric.HomeRuns => "홈런",
                CareerRecordMetric.RunsBattedIn => "타점",
                CareerRecordMetric.Walks => "볼넷",
                CareerRecordMetric.BattingStrikeouts => "삼진",
                CareerRecordMetric.BattingAverage => "타율",
                CareerRecordMetric.OnBasePercentage => "출루율",
                CareerRecordMetric.SluggingPercentage => "장타율",
                CareerRecordMetric.OnBasePlusSlugging => "OPS",
                CareerRecordMetric.PitchingAppearances => "등판",
                CareerRecordMetric.PitchingStarts => "선발",
                CareerRecordMetric.OutsRecorded => "이닝",
                CareerRecordMetric.Wins => "승",
                CareerRecordMetric.Losses => "패",
                CareerRecordMetric.Saves => "세이브",
                CareerRecordMetric.Holds => "홀드",
                CareerRecordMetric.HitsAllowed => "피안타",
                CareerRecordMetric.EarnedRuns => "자책점",
                CareerRecordMetric.WalksAllowed => "볼넷 허용",
                CareerRecordMetric.PitchingStrikeouts => "탈삼진",
                CareerRecordMetric.EarnedRunAverage => "평균자책점",
                CareerRecordMetric.WalksHitsPerInningPitched => "WHIP",
                CareerRecordMetric.FieldingOpportunities => "수비 기회",
                CareerRecordMetric.SuccessfulFieldingPlays => "처리 성공",
                CareerRecordMetric.Putouts => "자살",
                CareerRecordMetric.Assists => "보살",
                CareerRecordMetric.Errors => "실책",
                CareerRecordMetric.DoublePlays => "병살",
                CareerRecordMetric.EstimatedRunsSaved => "실점 억제",
                CareerRecordMetric.FieldingSuccessRate => "수비 성공률",
                CareerRecordMetric.StolenBases => "도루",
                CareerRecordMetric.CaughtStealing => "도루 실패",
                CareerRecordMetric.StolenBasePercentage => "도루 성공률",
                _ => "-"
            };
        }

        private static string FormatMetric(CareerRecordMetric metric, double value)
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
                CareerRecordMetric.EstimatedRunsSaved => value.ToString("0.00"),
                CareerRecordMetric.OutsRecorded => FormatInnings((int)Math.Round(value)),
                _ => Math.Round(value).ToString("0")
            };
        }

        private static string FormatRate(double value)
        {
            string formatted = value.ToString("0.000");
            return formatted.StartsWith("0.", StringComparison.Ordinal) ? formatted.Substring(1) : formatted;
        }

        private static string GetAwardLabel(AwardCategory category)
        {
            return category switch
            {
                AwardCategory.RegularSeasonMvp => "정규시즌 MVP",
                AwardCategory.PostseasonMvp => "포스트시즌 MVP",
                AwardCategory.RookieOfYear => "신인왕",
                AwardCategory.BattingAverage => "타격왕",
                AwardCategory.HomeRun => "홈런왕",
                AwardCategory.RunsBattedIn => "타점왕",
                AwardCategory.StolenBase => "도루왕",
                AwardCategory.EarnedRunAverage => "평균자책점 1위",
                AwardCategory.Win => "다승왕",
                AwardCategory.Strikeout => "탈삼진왕",
                AwardCategory.Save => "세이브왕",
                AwardCategory.GoldGlove => "골든글러브",
                _ => category.ToString()
            };
        }

        private static string GetPositionLabel(PlayerPosition position)
        {
            return position switch
            {
                PlayerPosition.Catcher => "포수",
                PlayerPosition.FirstBase => "1루수",
                PlayerPosition.SecondBase => "2루수",
                PlayerPosition.ThirdBase => "3루수",
                PlayerPosition.Shortstop => "유격수",
                PlayerPosition.LeftField => "좌익수",
                PlayerPosition.CenterField => "중견수",
                PlayerPosition.RightField => "우익수",
                PlayerPosition.DesignatedHitter => "지명타자",
                PlayerPosition.StartingPitcher => "선발투수",
                PlayerPosition.ReliefPitcher => "구원투수",
                _ => "전체"
            };
        }

        private static string GetRoleLabel(PlayerGameRole role, PlayerPosition position)
        {
            if (CareerGameRoleFormatter.IsPitcherRest(role, position))
                return CareerGameRoleFormatter.GetPitcherRestLabel(position);

            return role switch
            {
                PlayerGameRole.StartingBatter => "선발 타자",
                PlayerGameRole.StartingPitcher => "선발 투수",
                PlayerGameRole.ReliefPitcher => "구원 투수",
                PlayerGameRole.Bench => "벤치",
                _ => "미출장"
            };
        }

        private static string GetExpectedRoleLabel(ExpectedRole role)
        {
            return role switch
            {
                ExpectedRole.StartingCompetition => "주전 경쟁",
                ExpectedRole.RosterCompetition => "로스터 경쟁",
                _ => "벤치 경쟁"
            };
        }

        private static string GetLeagueLabel(LeagueLevel level)
        {
            return level switch
            {
                LeagueLevel.Rookie => "Rookie",
                LeagueLevel.Minor => "Minor",
                LeagueLevel.Major => "Major",
                _ => "Rookie"
            };
        }

        private static string GetTeamShortName(string teamName)
        {
            if (string.IsNullOrWhiteSpace(teamName))
                return "-";
            int separator = teamName.IndexOf(' ');
            string shortName = separator > 0 ? teamName.Substring(0, separator) : teamName;
            return shortName.Length > 4 ? shortName.Substring(0, 4) : shortName;
        }

        private static string FormatMoney(long amount)
        {
            return amount >= 100_000_000L
                ? $"{amount / 100_000_000d:0.##}억원"
                : $"{amount / 10_000d:N0}만원";
        }

        private static string FormatInnings(int outs) => $"{outs / 3}.{outs % 3}";
    }
}
