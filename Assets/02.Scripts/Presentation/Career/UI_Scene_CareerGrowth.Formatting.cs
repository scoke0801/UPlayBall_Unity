using System;
using System.Text;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Career;
using Baseball.Simulation.Growth;
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
                PlayerAbility.Arm,
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
                PlayerAbility.Arm => "송구",
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
            if (record.PeakChanges != null)
            {
                for (int index = 0; index < record.PeakChanges.Length; index++)
                {
                    AppendSeparator(builder);
                    builder.Append(GetAbilityLabel(record.PeakChanges[index].Ability));
                    builder.Append(" Peak ");
                    AppendSigned(builder, record.PeakChanges[index].Amount);
                }
            }
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
                int rawBonus = growth.RawBoardBonuses[(int)visible[index]];
                if (bonus <= 0)
                    continue;
                AppendSeparator(builder);
                builder.Append(GetAbilityLabel(visible[index]));
                builder.Append(" +");
                if (rawBonus > bonus)
                {
                    builder.Append(rawBonus);
                    builder.Append('→');
                }
                builder.Append(bonus);
            }
            if (builder.Length == 0)
                return "장착 보너스 없음";
            int total = 0;
            for (int index = 0; index < growth.BoardBonuses.Length; index++)
                total += growth.BoardBonuses[index];
            builder.Append(" · 전체 ");
            builder.Append(total);
            builder.Append('/');
            builder.Append(SkillBoardService.MaximumTotalAbilityBonus);
            if (growth.ActiveTraitIds != null && growth.ActiveTraitIds.Length > 0)
            {
                builder.Append("\nTrait ");
                for (int index = 0; index < growth.ActiveTraitIds.Length; index++)
                {
                    if (index > 0) builder.Append(" · ");
                    builder.Append(GetTraitLabel(growth.ActiveTraitIds[index]));
                }
            }
            return builder.ToString();
        }

        private static string GetTraitLabel(string traitId)
        {
            return traitId switch
            {
                SkillTraitIds.TwoStrikeContact => "2스트라이크 대응",
                SkillTraitIds.ScoringPositionPower => "득점권 장타",
                SkillTraitIds.AggressiveBaserunning => "공격 주루",
                SkillTraitIds.DefensiveFocus => "수비 집중",
                SkillTraitIds.ScoringPositionFocus => "득점권 집중",
                SkillTraitIds.LateInningStuff => "후반 구위",
                SkillTraitIds.CrisisManagement => "위기 관리",
                SkillTraitIds.LongOutingAdaptation => "긴 이닝 적응",
                _ => traitId
            };
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
                SkillBlockCategory.Arm => "송구",
                SkillBlockCategory.Stuff => "구위",
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
                SkillBlockCategory.Baserunning => "주루 보너스",
                SkillBlockCategory.Arm => "송구 보너스",
                SkillBlockCategory.PitcherPhysical => "투수 체력 보너스",
                SkillBlockCategory.Stuff => "구위 보너스",
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
                SkillBlockCategory.Baserunning => new Color(0.13f, 0.62f, 0.55f, 1f),
                SkillBlockCategory.Arm => new Color(0.70f, 0.42f, 0.16f, 1f),
                SkillBlockCategory.PitcherPhysical => new Color(0.64f, 0.45f, 0.16f, 1f),
                SkillBlockCategory.Stuff => new Color(0.60f, 0.24f, 0.24f, 1f),
                _ => AccentColor
            };
        }

        private static Color GetAbilityColor(PlayerAbility ability)
        {
            return ability switch
            {
                PlayerAbility.Contact => GetCategoryColor(SkillBlockCategory.Contact),
                PlayerAbility.Power => GetCategoryColor(SkillBlockCategory.Power),
                PlayerAbility.Speed => GetCategoryColor(SkillBlockCategory.Baserunning),
                PlayerAbility.Arm => GetCategoryColor(SkillBlockCategory.Arm),
                PlayerAbility.Defense => GetCategoryColor(SkillBlockCategory.Defense),
                PlayerAbility.BatterMental => GetCategoryColor(SkillBlockCategory.BatterMental),
                PlayerAbility.Velocity => GetCategoryColor(SkillBlockCategory.Velocity),
                PlayerAbility.Control => GetCategoryColor(SkillBlockCategory.Control),
                PlayerAbility.Breaking => GetCategoryColor(SkillBlockCategory.Breaking),
                PlayerAbility.PitcherMental => GetCategoryColor(SkillBlockCategory.PitcherMental),
                PlayerAbility.Stamina => GetCategoryColor(SkillBlockCategory.PitcherPhysical),
                PlayerAbility.Stuff => GetCategoryColor(SkillBlockCategory.Stuff),
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
            string breakthrough = program.CanRaisePotential
                ? program.MinimumPotentialBreakthroughsWhenCapped > 0
                    ? $" · Potential {program.PotentialBreakthroughProbability:P0} · 정체 시 돌파 보장"
                    : $" · Potential {program.PotentialBreakthroughProbability:P0}"
                : string.Empty;
            string repetition = program.RepetitionMultiplier < 0.999d
                ? $" · 반복 페널티 ×{program.RepetitionMultiplier:0.00}"
                : string.Empty;
            string roleImpact = program.EstimatedRoleScoreGain > 0d
                ? $" · 역할 점수 최대 +{program.EstimatedRoleScoreGain:0.0} 예상"
                : string.Empty;
            string locked = string.IsNullOrEmpty(program.UnavailableReason)
                ? string.Empty
                : $"\n잠금: {program.UnavailableReason}";
            return $"{GetProgramLabel(program.ProgramId)} · {program.DurationWeeks}주 · " +
                $"{FormatMoney(program.MoneyCost)} · 남은 {program.RemainingWeeksBefore}→" +
                   $"{program.RemainingWeeksAfter}주 · 컨디션 {program.ConditionBefore}→" +
                   $"{program.ConditionAfter} · {growthRange} · {risk}{breakthrough}" +
                   $"{repetition}{roleImpact}{locked}";
        }

        private static string GetExpectedRoleLabel(ExpectedRole role)
        {
            return role switch
            {
                ExpectedRole.StartingCompetition => "주전",
                ExpectedRole.RosterCompetition => "로테이션",
                _ => "백업"
            };
        }

        private static string FormatDecisionExplanation(DecisionExplanation explanation, int maximumFactors = 3)
        {
            if (explanation == null || explanation.Factors.Length == 0)
                return "아직 역할 평가 근거가 없습니다.";
            var factors = (DecisionFactor[])explanation.Factors.Clone();
            Array.Sort(factors, (left, right) =>
            {
                int priority = left.Priority.CompareTo(right.Priority);
                return priority != 0
                    ? priority
                    : Math.Abs(right.Contribution).CompareTo(Math.Abs(left.Contribution));
            });
            var builder = new StringBuilder();
            int count = Math.Min(maximumFactors, factors.Length);
            for (int index = 0; index < count; index++)
            {
                if (index > 0) builder.Append(" · ");
                builder.Append(GetDecisionReasonLabel(factors[index].ReasonCode));
                builder.Append(' ');
                builder.Append(factors[index].Contribution >= 0d ? '+' : '-');
                builder.Append(Math.Abs(factors[index].Contribution).ToString("0.0"));
            }
            if (explanation.RecommendedActions.Length > 0)
            {
                builder.Append("\n추천: ");
                builder.Append(GetRecommendedActionLabel(explanation.RecommendedActions[0]));
            }
            return builder.ToString();
        }

        private static string GetDecisionReasonLabel(DecisionReasonCode reason)
        {
            return reason switch
            {
                DecisionReasonCode.CurrentAbility => "현재 기량",
                DecisionReasonCode.PositionFit => "포지션 적합도",
                DecisionReasonCode.RecentPerformance => "최근 성과",
                DecisionReasonCode.Condition => "컨디션",
                DecisionReasonCode.ManagerTrust => "감독 신뢰",
                DecisionReasonCode.GrowthOutlook => "성장 전망",
                DecisionReasonCode.IncumbentBonus => "기존 주전 보정",
                DecisionReasonCode.CompetitorScore => "경쟁자 전력",
                DecisionReasonCode.AgeCurve => "나이 곡선",
                DecisionReasonCode.PotentialGap => "Potential 여유",
                DecisionReasonCode.WorkEthic => "Work Ethic",
                DecisionReasonCode.TrainingFit => "훈련 적합도",
                DecisionReasonCode.RepetitionPenalty => "반복 페널티",
                DecisionReasonCode.UsageExposure => "출장 경험",
                DecisionReasonCode.CatchUpSupport => "따라잡기 보정",
                DecisionReasonCode.RecoveryProtection => "휴식·재활 보호",
                _ => reason.ToString()
            };
        }

        private static string GetRecommendedActionLabel(RecommendedActionCode action)
        {
            return action switch
            {
                RecommendedActionCode.ImproveCoreAbility => "핵심 능력 보완",
                RecommendedActionCode.ImprovePositionFit => "포지션 적합도 강화",
                RecommendedActionCode.RestoreCondition => "컨디션 회복",
                RecommendedActionCode.EarnPlayingTime => "출장 기회 확보",
                RecommendedActionCode.ReduceTrainingRepetition => "훈련 종류 변경",
                RecommendedActionCode.ChooseRecovery => "휴식·재활 선택",
                _ => action.ToString()
            };
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
            return GrowthProgramNameFormatter.GetLabel(programId);
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
