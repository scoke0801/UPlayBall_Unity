using System;
using System.Text;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Career;
using UnityEngine;

namespace Baseball.Presentation.Career
{
    public sealed partial class UI_Scene_CareerGrowth
    {
        private static string GetSeasonPhaseLabel(SeasonPhase phase)
        {
            return phase switch
            {
                SeasonPhase.Preseason => "PRE-SEASON",
                SeasonPhase.RegularSeason => "REGULAR SEASON",
                SeasonPhase.Postseason => "POST-SEASON",
                SeasonPhase.SeasonReview => "SEASON REVIEW",
                SeasonPhase.Offseason => "OFF-SEASON",
                SeasonPhase.Completed => "COMPLETED",
                _ => phase.ToString().ToUpperInvariant()
            };
        }

        private static string FormatMoney(long amount)
        {
            return amount >= 100_000_000L
                ? $"{amount / 100_000_000d:0.##}억원"
                : $"{amount / 10_000d:N0}만원";
        }

        private static string GetInitial(string name)
        {
            return string.IsNullOrWhiteSpace(name) ? "P" : name.Substring(0, 1);
        }

        private static string GetPositionCode(PlayerPosition position)
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

        private static string GetHandsLabel(CareerDashboardView view)
        {
            string throwing = view.ThrowingHand == Handedness.Left ? "좌투" : "우투";
            string batting = view.BattingHand switch
            {
                Handedness.Left => "좌타",
                Handedness.Switch => "양타",
                _ => "우타"
            };
            return throwing + "/" + batting;
        }

        private static string GetCurrentRoleLabel(CareerDashboardView view)
        {
            if (!view.NextGame.HasValue)
                return view.SeasonPhase == SeasonPhase.Offseason ? "오프시즌" : "시즌 일정 종료";
            PlayerGameRole role = view.NextGame.Value.PlannedRole;
            if (CareerGameRoleFormatter.IsPitcherRest(role, view.Position))
                return CareerGameRoleFormatter.GetPitcherRestLabel(view.Position);

            return role switch
            {
                PlayerGameRole.StartingBatter => "선발 " + GetPositionCode(view.Position),
                PlayerGameRole.StartingPitcher => "선발 투수",
                PlayerGameRole.ReliefPitcher => "구원 대기",
                PlayerGameRole.Bench => "벤치",
                _ => "감독 판단 대기"
            };
        }

        private static PlayerAbility[] GetVisibleAbilities(PlayerType playerType)
        {
            return playerType == PlayerType.Batter
                ? new[]
                {
                    PlayerAbility.Contact,
                    PlayerAbility.Power,
                    PlayerAbility.Speed,
                    PlayerAbility.Bunt,
                    PlayerAbility.Defense,
                    PlayerAbility.BatterMental
                }
                : new[]
                {
                    PlayerAbility.Stamina,
                    PlayerAbility.Velocity,
                    PlayerAbility.Stuff,
                    PlayerAbility.Breaking,
                    PlayerAbility.Control,
                    PlayerAbility.PitcherMental
                };
        }

        private static string GetAbilityLabel(PlayerAbility ability)
        {
            return ability switch
            {
                PlayerAbility.Contact => "교타력",
                PlayerAbility.Power => "장타력",
                PlayerAbility.Speed => "주력",
                PlayerAbility.Bunt => "번트",
                PlayerAbility.Defense => "수비력",
                PlayerAbility.BatterMental => "정신력",
                PlayerAbility.Stamina => "체력",
                PlayerAbility.Velocity => "구속",
                PlayerAbility.Stuff => "구위",
                PlayerAbility.Breaking => "변화구",
                PlayerAbility.Control => "제구력",
                PlayerAbility.PitcherMental => "위기관리",
                _ => ability.ToString()
            };
        }

        private static string GetGrowthSourceLabel(GrowthResultRecord record)
        {
            return record.SourceType switch
            {
                GrowthSourceType.NaturalDevelopment => "자연 성장",
                GrowthSourceType.PersonalTraining => record.Intensity switch
                {
                    TrainingIntensity.Safe => "안정 훈련",
                    TrainingIntensity.Intensive => "집중 훈련",
                    _ => "표준 훈련"
                },
                GrowthSourceType.TrainingPartner => "훈련 파트너",
                GrowthSourceType.Study => "유학",
                GrowthSourceType.Aging => "노쇠",
                GrowthSourceType.Injury => "부상 영향",
                _ => record.SourceId
            };
        }

        private static bool HasPositiveChange(GrowthResultRecord record)
        {
            for (int index = 0; index < record.AbilityChanges.Length; index++)
            {
                if (record.AbilityChanges[index].Amount > 0)
                    return true;
            }
            return false;
        }

        private static string FormatGrowthChanges(GrowthResultRecord record)
        {
            var builder = new StringBuilder();
            AppendAbilityChanges(builder, record.AbilityChanges, potential: false);
            AppendAbilityChanges(builder, record.PotentialChanges, potential: true);
            if (record.ConditionChange != 0)
            {
                AppendSeparator(builder);
                builder.Append("컨디션 ");
                AppendSigned(builder, record.ConditionChange);
            }
            return builder.Length == 0 ? "변화 없음" : builder.ToString();
        }

        private static void AppendAbilityChanges(
            StringBuilder builder,
            AbilityChange[] changes,
            bool potential)
        {
            for (int index = 0; index < changes.Length; index++)
            {
                AppendSeparator(builder);
                builder.Append(GetAbilityLabel(changes[index].Ability));
                if (potential)
                    builder.Append(" 잠재");
                builder.Append(' ');
                AppendSigned(builder, changes[index].Amount);
            }
        }

        private static void AppendSeparator(StringBuilder builder)
        {
            if (builder.Length > 0)
                builder.Append(" · ");
        }

        private static void AppendSigned(StringBuilder builder, int value)
        {
            if (value > 0)
                builder.Append('+');
            builder.Append(value);
        }

        private static string FormatBoardBonuses(CareerGrowthView growth)
        {
            var builder = new StringBuilder();
            PlayerAbility[] visible = GetVisibleAbilities(growth.PlayerType);
            for (int index = 0; index < visible.Length; index++)
            {
                int bonus = growth.BoardBonuses[(int)visible[index]];
                if (bonus <= 0)
                    continue;
                AppendSeparator(builder);
                builder.Append(GetAbilityLabel(visible[index]));
                builder.Append(" +");
                builder.Append(bonus);
            }
            return builder.Length == 0 ? "장착 보너스 없음" : builder.ToString();
        }

        private static Color GetDominantBonusColor(CareerGrowthView growth)
        {
            PlayerAbility[] visible = GetVisibleAbilities(growth.PlayerType);
            for (int index = 0; index < visible.Length; index++)
            {
                if (growth.BoardBonuses[(int)visible[index]] > 0)
                    return GetAbilityColor(visible[index]);
            }
            return MutedColor;
        }

        private static string GetCategoryLabel(SkillBlockCategory category)
        {
            return category switch
            {
                SkillBlockCategory.Contact => "교타",
                SkillBlockCategory.Power => "장타",
                SkillBlockCategory.Baserunning => "주루",
                SkillBlockCategory.Defense => "수비",
                SkillBlockCategory.BatterMental => "타격 정신",
                SkillBlockCategory.Velocity => "구속",
                SkillBlockCategory.Control => "제구",
                SkillBlockCategory.Breaking => "변화구",
                SkillBlockCategory.PitcherPhysical => "투수 체력",
                SkillBlockCategory.PitcherMental => "투수 정신",
                _ => category.ToString()
            };
        }

        private static string GetCategoryShortLabel(SkillBlockCategory category)
        {
            return category switch
            {
                SkillBlockCategory.BatterMental => "정신",
                SkillBlockCategory.PitcherMental => "정신",
                SkillBlockCategory.PitcherPhysical => "체력",
                SkillBlockCategory.Baserunning => "주루",
                SkillBlockCategory.Breaking => "변화",
                _ => GetCategoryLabel(category)
            };
        }

        private static string GetCategoryEffectLabel(SkillBlockCategory category)
        {
            return category switch
            {
                SkillBlockCategory.Contact => "교타력 보너스",
                SkillBlockCategory.Power => "장타력 보너스",
                SkillBlockCategory.Defense => "수비력 보너스",
                SkillBlockCategory.BatterMental => "정신력 보너스",
                SkillBlockCategory.Velocity => "구속 보너스",
                SkillBlockCategory.Control => "제구력 보너스",
                SkillBlockCategory.Breaking => "변화구 보너스",
                SkillBlockCategory.PitcherMental => "위기관리 보너스",
                _ => "능력치 보너스"
            };
        }

        private static Color GetCategoryColor(SkillBlockCategory category)
        {
            return category switch
            {
                SkillBlockCategory.Contact => new Color(0.08f, 0.43f, 0.78f, 1f),
                SkillBlockCategory.Power => new Color(0.57f, 0.18f, 0.75f, 1f),
                SkillBlockCategory.Defense => new Color(0.16f, 0.55f, 0.17f, 1f),
                SkillBlockCategory.BatterMental => new Color(0.76f, 0.52f, 0.08f, 1f),
                SkillBlockCategory.Velocity => new Color(0.72f, 0.20f, 0.18f, 1f),
                SkillBlockCategory.Control => new Color(0.08f, 0.47f, 0.73f, 1f),
                SkillBlockCategory.Breaking => new Color(0.48f, 0.22f, 0.72f, 1f),
                SkillBlockCategory.PitcherMental => new Color(0.76f, 0.52f, 0.08f, 1f),
                _ => AccentColor
            };
        }

        private static Color GetAbilityColor(PlayerAbility ability)
        {
            return ability switch
            {
                PlayerAbility.Contact => GetCategoryColor(SkillBlockCategory.Contact),
                PlayerAbility.Power => GetCategoryColor(SkillBlockCategory.Power),
                PlayerAbility.Defense => GetCategoryColor(SkillBlockCategory.Defense),
                PlayerAbility.BatterMental => GetCategoryColor(SkillBlockCategory.BatterMental),
                PlayerAbility.Velocity => GetCategoryColor(SkillBlockCategory.Velocity),
                PlayerAbility.Control => GetCategoryColor(SkillBlockCategory.Control),
                PlayerAbility.Breaking => GetCategoryColor(SkillBlockCategory.Breaking),
                PlayerAbility.PitcherMental => GetCategoryColor(SkillBlockCategory.PitcherMental),
                _ => GreenColor
            };
        }

        private static string GetRarityCode(SkillBlockRarity rarity)
        {
            return rarity switch
            {
                SkillBlockRarity.Normal => "N",
                SkillBlockRarity.Rare => "R",
                SkillBlockRarity.Elite => "E",
                SkillBlockRarity.Unique => "U",
                SkillBlockRarity.Legendary => "L",
                _ => "?"
            };
        }

        private static GrowthGachaOfferView FindGachaOffer(
            CareerGrowthView growth,
            SkillGachaPurchaseTier tier)
        {
            for (int index = 0; index < growth.GachaOffers.Length; index++)
            {
                if (growth.GachaOffers[index].Tier == tier)
                    return growth.GachaOffers[index];
            }
            return default;
        }

        private static string FormatGachaProbability(GrowthGachaOfferView offer)
        {
            return $"N {offer.NormalProbability:P0} · R {offer.RareProbability:P0} · " +
                   $"E {offer.EliteProbability:P0} · U {offer.UniqueProbability:P0} · " +
                   $"L {offer.LegendaryProbability:P0}";
        }

        private static string GetRarityLabel(SkillBlockRarity rarity)
        {
            return rarity switch
            {
                SkillBlockRarity.Normal => "Normal",
                SkillBlockRarity.Rare => "Rare",
                SkillBlockRarity.Elite => "Elite",
                SkillBlockRarity.Unique => "Unique",
                SkillBlockRarity.Legendary => "Legendary",
                _ => rarity.ToString()
            };
        }

        private static string FormatAbilityChanges(AbilityChange[] changes)
        {
            if (changes == null || changes.Length == 0)
                return "보너스 없음";
            var builder = new StringBuilder();
            for (int index = 0; index < changes.Length; index++)
            {
                AppendSeparator(builder);
                builder.Append(GetAbilityLabel(changes[index].Ability));
                builder.Append(' ');
                AppendSigned(builder, changes[index].Amount);
            }
            return builder.ToString();
        }

        private static GrowthSkillBlockView FindOwnedBlock(CareerGrowthView growth, int instanceId)
        {
            for (int index = 0; index < growth.OwnedBlocks.Length; index++)
            {
                if (growth.OwnedBlocks[index].InstanceId == instanceId)
                    return growth.OwnedBlocks[index];
            }
            return default;
        }

        private static bool ContainsPlacedBlock(CareerGrowthView growth, int instanceId)
        {
            for (int index = 0; index < growth.BoardCells.Length; index++)
            {
                if (growth.BoardCells[index].InstanceId == instanceId)
                    return true;
            }
            return false;
        }

        private static GrowthBoardCellView FindPlacedCell(CareerGrowthView growth, int instanceId)
        {
            for (int index = 0; index < growth.BoardCells.Length; index++)
            {
                if (growth.BoardCells[index].InstanceId == instanceId)
                    return growth.BoardCells[index];
            }
            return default;
        }

        private static GrowthSkillBlockView FindPlacedBlock(CareerGrowthView growth, int instanceId)
        {
            for (int index = 0; index < growth.PlacedBlocks.Length; index++)
            {
                if (growth.PlacedBlocks[index].InstanceId == instanceId)
                    return growth.PlacedBlocks[index];
            }
            return default;
        }

        private static GrowthProgramView FindSelectedProgram(CareerGrowthView growth)
        {
            for (int index = 0; index < growth.Programs.Length; index++)
            {
                if (string.Equals(
                        growth.Programs[index].ProgramId,
                        growth.SelectedProgramId,
                        StringComparison.Ordinal))
                {
                    return growth.Programs[index];
                }
            }
            return default;
        }

        private static string BuildProgramPreview(GrowthProgramView program)
        {
            string growthRange = program.MaxTotalGain > 0
                ? $"예상 총 성장 +{program.MinimumGuaranteedGain}~{program.MaxTotalGain}"
                : program.ConditionChange > 0 ? $"컨디션 +{program.ConditionChange}" : "회복 활동";
            string risk = program.InjuryRisk <= 0d ? "부상 위험 없음" : $"불편감 위험 {program.InjuryRisk:P1}";
            return $"{GetProgramLabel(program.ProgramId)} · {program.DurationWeeks}주 · " +
                $"{FormatMoney(program.MoneyCost)} · 남은 {program.RemainingWeeksBefore}→" +
                   $"{program.RemainingWeeksAfter}주 · 컨디션 {program.ConditionBefore}→" +
                   $"{program.ConditionAfter} · {growthRange} · {risk}";
        }

        private static Color GetProgramColor(OffseasonActivityType type)
        {
            return type switch
            {
                OffseasonActivityType.PersonalTraining => new Color(0.025f, 0.24f, 0.43f, 1f),
                OffseasonActivityType.TrainingPartner => new Color(0.12f, 0.22f, 0.30f, 1f),
                OffseasonActivityType.Study => new Color(0.18f, 0.17f, 0.29f, 1f),
                OffseasonActivityType.Rehabilitation => new Color(0.11f, 0.24f, 0.22f, 1f),
                _ => CardColor
            };
        }

        private static string GetActivityShortLabel(OffseasonActivityType type)
        {
            return type switch
            {
                OffseasonActivityType.PersonalTraining => "PERSONAL",
                OffseasonActivityType.TrainingPartner => "PARTNER",
                OffseasonActivityType.Study => "STUDY",
                OffseasonActivityType.Rehabilitation => "RECOVERY",
                OffseasonActivityType.Rest => "REST",
                _ => type.ToString().ToUpperInvariant()
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
                "rehab_general" => "재활·회복",
                "sports_science_recovery" => "스포츠 사이언스 회복",
                "rest" => "휴식",
                _ => programId
            };
        }

        private static string FormatProgramAbilities(GrowthProgramView program)
        {
            if (program.AbilityWeights == null || program.AbilityWeights.Length == 0)
                return program.ConditionChange > 0 ? $"컨디션 +{program.ConditionChange}" : "회복";
            var builder = new StringBuilder();
            int count = Math.Min(2, program.AbilityWeights.Length);
            for (int index = 0; index < count; index++)
            {
                if (index > 0)
                    builder.Append('\n');
                builder.Append(GetAbilityLabel(program.AbilityWeights[index].Ability));
                builder.Append(' ');
                int arrows = Math.Max(1, (int)Math.Round(program.AbilityWeights[index].Weight * 4d));
                for (int arrow = 0; arrow < arrows; arrow++)
                    builder.Append('▲');
            }
            return builder.ToString();
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
                TrainingFitGrade.High => GreenColor,
                TrainingFitGrade.VeryHigh => BrightAccentColor,
                _ => SecondaryTextColor
            };
        }

        private static Color GetRatingColor(int rating)
        {
            if (rating >= 80)
                return GreenColor;
            if (rating >= 65)
                return AccentColor;
            if (rating >= 50)
                return new Color(0.38f, 0.67f, 0.86f, 1f);
            return WarningColor;
        }
    }
}
