using System;
using System.Text;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Game.Career;
using Baseball.Presentation.SharedScreens;
using UnityEngine;

namespace Baseball.Presentation.Career
{
    public sealed partial class UI_Scene_Player
    {
        private static Color GetSkillCategoryColor(SkillBlockCategory category)
        {
            return Baseball.Presentation.UI.SkillBlockVisual.GetCategoryColor(category);
        }

        private static string GetSkillCategoryLabel(SkillBlockCategory category)
        {
            return category switch
            {
                SkillBlockCategory.Contact => "교타력",
                SkillBlockCategory.Power => "장타력",
                SkillBlockCategory.Baserunning => "주력",
                SkillBlockCategory.Defense => "수비력",
                SkillBlockCategory.BatterMental => "정신력",
                SkillBlockCategory.Velocity => "구속",
                SkillBlockCategory.Control => "제구력",
                SkillBlockCategory.Breaking => "변화구",
                SkillBlockCategory.PitcherPhysical => "체력",
                SkillBlockCategory.Bunt => "번트",
                SkillBlockCategory.Stuff => "구위",
                _ => "정신력"
            };
        }

        private static string GetRarityCode(SkillBlockRarity rarity) => SkillBlockGradeCatalog.GetLabel(rarity);

        private static string GetRarityLabel(SkillBlockRarity rarity) => SkillBlockGradeCatalog.GetLabel(rarity);

        private static Color GetRarityColor(SkillBlockRarity rarity) => Baseball.Presentation.UI.SkillBlockVisual.GetRarityColor(rarity);

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
                return $"최근 {games}경기  평균자책 {era:0.00}  ·  {FormatInnings(outs)}이닝  ·  탈삼진 {strikeouts}";
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
            return $"최근 {games}경기  타율 {average:.000}  ·  홈런 {homeRuns}  ·  타점 {runsBattedIn}";
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
                CareerRecordMetric.OnBasePlusSlugging => "출루율+장타율",
                CareerRecordMetric.PitchingAppearances => "등판",
                CareerRecordMetric.OutsRecorded => "이닝",
                CareerRecordMetric.Wins => "승",
                CareerRecordMetric.Saves => "세이브",
                CareerRecordMetric.PitchingStrikeouts => "탈삼진",
                CareerRecordMetric.EarnedRunAverage => "평균자책점",
                CareerRecordMetric.WalksHitsPerInningPitched => "이닝당 출루허용률",
                _ => CareerSharedSnapshotFormatters.FormatMetricLabel(metric)
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
                ? $"{FormatInnings(game.OutsRecorded)}이닝  ·  자책 {game.EarnedRuns}  ·  탈삼진 {game.Strikeouts}" +
                  $"  ·  볼넷 {game.WalksAllowed}"
                : $"{game.AtBats}타수 {game.Hits}안타  ·  {game.HomeRuns}홈런  ·  {game.RunsBattedIn}타점" +
                  $"  ·  {game.Walks}볼넷";
        }

        private static string FormatInnings(int outs)
        {
            return $"{outs / 3}.{outs % 3}";
        }
    }
}
