using System;
using System.Text;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Game.Career;
using UnityEngine;

namespace Baseball.Presentation.Career
{
    public sealed partial class UI_Scene_Player
    {
        private static Color GetSkillCategoryColor(SkillBlockCategory category)
        {
            return category switch
            {
                SkillBlockCategory.Contact => new Color(0.15f, 0.48f, 0.82f, 1f),
                SkillBlockCategory.Power => new Color(0.82f, 0.48f, 0.12f, 1f),
                SkillBlockCategory.Baserunning => new Color(0.24f, 0.66f, 0.31f, 1f),
                SkillBlockCategory.Defense => new Color(0.18f, 0.56f, 0.58f, 1f),
                SkillBlockCategory.BatterMental => new Color(0.49f, 0.38f, 0.77f, 1f),
                SkillBlockCategory.Velocity => new Color(0.78f, 0.26f, 0.22f, 1f),
                SkillBlockCategory.Control => new Color(0.16f, 0.46f, 0.76f, 1f),
                SkillBlockCategory.Breaking => new Color(0.45f, 0.34f, 0.73f, 1f),
                SkillBlockCategory.PitcherPhysical => new Color(0.64f, 0.45f, 0.16f, 1f),
                _ => new Color(0.24f, 0.58f, 0.52f, 1f)
            };
        }

        private static string GetSkillCategoryLabel(SkillBlockCategory category)
        {
            return category switch
            {
                SkillBlockCategory.Contact => "컨택",
                SkillBlockCategory.Power => "장타",
                SkillBlockCategory.Baserunning => "주루",
                SkillBlockCategory.Defense => "수비",
                SkillBlockCategory.BatterMental => "타자 정신력",
                SkillBlockCategory.Velocity => "구속",
                SkillBlockCategory.Control => "제구",
                SkillBlockCategory.Breaking => "변화구",
                SkillBlockCategory.PitcherPhysical => "투수 체력",
                _ => "투수 정신력"
            };
        }

        private static string GetRarityCode(SkillBlockRarity rarity)
        {
            return rarity switch
            {
                SkillBlockRarity.Epic => "E",
                SkillBlockRarity.Rare => "R",
                SkillBlockRarity.Uncommon => "U",
                _ => "C"
            };
        }

        private static string GetRarityLabel(SkillBlockRarity rarity)
        {
            return rarity switch
            {
                SkillBlockRarity.Epic => "에픽",
                SkillBlockRarity.Rare => "레어",
                SkillBlockRarity.Uncommon => "언커먼",
                _ => "커먼"
            };
        }

        private static Color GetRarityColor(SkillBlockRarity rarity)
        {
            return rarity switch
            {
                SkillBlockRarity.Epic => new Color(0.77f, 0.39f, 0.95f, 1f),
                SkillBlockRarity.Rare => GoldColor,
                SkillBlockRarity.Uncommon => RoleColor,
                _ => SecondaryTextColor
            };
        }

        private static string FormatAbilityBonuses(AbilityChange[] bonuses)
        {
            if (bonuses == null || bonuses.Length == 0)
                return "안정 능력치 보너스 없음";

            var builder = new StringBuilder(48);
            for (int index = 0; index < bonuses.Length; index++)
            {
                if (index > 0)
                    builder.Append("  ·  ");
                builder.Append(GetAbilityLabel(bonuses[index].Ability));
                builder.Append(' ');
                if (bonuses[index].Amount > 0)
                    builder.Append('+');
                builder.Append(bonuses[index].Amount);
            }
            return builder.ToString();
        }

        private static string BuildRecentFormText(PlayerProfileView view)
        {
            if (view.RecentGames.Length == 0)
                return "최근 경기 기록 없음";

            int games = Math.Min(5, view.RecentGames.Length);
            if (view.PlayerType == PlayerType.Pitcher)
            {
                int outs = 0;
                int earnedRuns = 0;
                int strikeouts = 0;
                for (int index = 0; index < games; index++)
                {
                    outs += view.RecentGames[index].OutsRecorded;
                    earnedRuns += view.RecentGames[index].EarnedRuns;
                    strikeouts += view.RecentGames[index].Strikeouts;
                }
                double era = outs == 0 ? 0d : earnedRuns * 27d / outs;
                return $"최근 {games}경기  ERA {era:0.00}  ·  {FormatInnings(outs)} IP  ·  {strikeouts} SO";
            }

            int atBats = 0;
            int hits = 0;
            int homeRuns = 0;
            int runsBattedIn = 0;
            for (int index = 0; index < games; index++)
            {
                atBats += view.RecentGames[index].AtBats;
                hits += view.RecentGames[index].Hits;
                homeRuns += view.RecentGames[index].HomeRuns;
                runsBattedIn += view.RecentGames[index].RunsBattedIn;
            }
            double average = atBats == 0 ? 0d : hits / (double)atBats;
            return $"최근 {games}경기  AVG {average:.000}  ·  {homeRuns} HR  ·  {runsBattedIn} RBI";
        }

        private static string BuildPlayerNote(PlayerProfileView view)
        {
            if (view.Condition < 50)
                return "컨디션 저하가 경기력과 기용에 영향을 줄 수 있습니다.";
            if (view.Fatigue >= 70)
                return "피로 누적이 큽니다. 다음 성장 활동에서 회복 선택을 검토하세요.";
            if (view.ManagerEvaluation < 50)
                return "감독 평가가 낮아 다음 경기 역할이 불안정할 수 있습니다.";
            if (view.ManagerEvaluation >= 75)
                return "감독 신뢰가 높습니다. 현재 역할을 지킬 가능성이 큽니다.";
            return "현재 상태는 안정적이지만 포지션 경쟁 결과에 따라 역할이 바뀔 수 있습니다.";
        }

        private static string GetMetricLabel(CareerRecordMetric metric)
        {
            return metric switch
            {
                CareerRecordMetric.Games => "경기",
                CareerRecordMetric.Hits => "안타",
                CareerRecordMetric.HomeRuns => "홈런",
                CareerRecordMetric.RunsBattedIn => "타점",
                CareerRecordMetric.BattingAverage => "타율",
                CareerRecordMetric.OnBasePercentage => "출루율",
                CareerRecordMetric.SluggingPercentage => "장타율",
                CareerRecordMetric.OnBasePlusSlugging => "OPS",
                CareerRecordMetric.PitchingAppearances => "등판",
                CareerRecordMetric.OutsRecorded => "이닝",
                CareerRecordMetric.Wins => "승",
                CareerRecordMetric.Saves => "세이브",
                CareerRecordMetric.PitchingStrikeouts => "탈삼진",
                CareerRecordMetric.EarnedRunAverage => "ERA",
                CareerRecordMetric.WalksHitsPerInningPitched => "WHIP",
                _ => metric.ToString()
            };
        }

        private static string FormatMetricValue(CareerRecordMetricValue metric)
        {
            return metric.Metric switch
            {
                CareerRecordMetric.BattingAverage or
                    CareerRecordMetric.OnBasePercentage or
                    CareerRecordMetric.SluggingPercentage or
                    CareerRecordMetric.OnBasePlusSlugging => metric.Value.ToString(".000"),
                CareerRecordMetric.EarnedRunAverage or
                    CareerRecordMetric.WalksHitsPerInningPitched => metric.Value.ToString("0.00"),
                CareerRecordMetric.OutsRecorded => FormatInnings((int)Math.Round(metric.Value)),
                _ => Math.Round(metric.Value).ToString("0")
            };
        }

        private static string FormatGameLine(PlayerGameLogState game, PlayerType playerType)
        {
            return playerType == PlayerType.Pitcher
                ? $"{FormatInnings(game.OutsRecorded)} IP  ·  {game.EarnedRuns} ER  ·  {game.Strikeouts} SO"
                : $"{game.AtBats}타수 {game.Hits}안타  ·  {game.HomeRuns}홈런  ·  {game.RunsBattedIn}타점";
        }

        private static string FormatInnings(int outs)
        {
            return $"{outs / 3}.{outs % 3}";
        }
    }
}
