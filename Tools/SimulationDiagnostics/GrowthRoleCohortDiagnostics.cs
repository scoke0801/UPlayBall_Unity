using System;
using System.Collections.Generic;
using System.Text;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Simulation.Growth;
using Baseball.Simulation.Random;

namespace Baseball.Tools.SimulationDiagnostics
{
    /// <summary>
    /// 실제 성장·노쇠·역할 평가기를 20시즌 반복해 백업 복귀와 프로그램 선택 집중도를 빠르게 점검한다.
    /// 리그 월드·계약·경제를 생략하므로 전체 커리어 코호트가 아니라 성장/역할 전용 선행 진단이다.
    /// </summary>
    internal static class GrowthRoleCohortDiagnostics
    {
        private static readonly AbilityWeight[] BatterUsageWeights =
        {
            new AbilityWeight(PlayerAbility.Contact, 0.35d),
            new AbilityWeight(PlayerAbility.Power, 0.15d),
            new AbilityWeight(PlayerAbility.Speed, 0.10d),
            new AbilityWeight(PlayerAbility.Defense, 0.25d),
            new AbilityWeight(PlayerAbility.Arm, 0.05d),
            new AbilityWeight(PlayerAbility.BatterMental, 0.10d)
        };

        public static GrowthRoleCohortReport Run(int careerCount, int maximumSeasons)
        {
            if (careerCount <= 0) throw new ArgumentOutOfRangeException(nameof(careerCount));
            if (maximumSeasons <= 0) throw new ArgumentOutOfRangeException(nameof(maximumSeasons));

            BalanceTable balance = BalanceTable.CreateDefault();
            var natural = new NaturalDevelopmentResolver(balance.Growth);
            var growth = new GrowthResolver(balance.Growth);
            var aging = new AgingResolver(balance.Growth);
            var role = new ManagerRoleEvaluator(balance.ManagerRoleEvaluation);
            var factory = new PlayerGrowthFactory(balance.Growth);
            var programSelections = new Dictionary<string, int>(StringComparer.Ordinal);
            int backupEpisodes = 0;
            int recoveredWithinTwoSeasons = 0;
            int potentialReachedAbilities = 0;
            int evaluatedAbilities = 0;
            double finalAbilityTotal = 0d;

            for (int careerIndex = 0; careerIndex < careerCount; careerIndex++)
            {
                PlayerGrowthState initialized = factory.Create(
                    CreatePlayer(careerIndex),
                    age: 18,
                    initialCondition: 90);
                var player = new PlayerGrowthState(
                    initialized.PlayerId,
                    initialized.Age,
                    initialized.PlayerType,
                    initialized.BaseAbilities,
                    initialized.PotentialByAbility,
                    (WorkEthicGrade)(careerIndex % 4),
                    initialized.Condition,
                    initialized.Fatigue,
                    initialized.Durability);
                int backupStartSeason = -1;
                var repetition = new Dictionary<TrainingCategory, int>();

                for (int season = 0; season < maximumSeasons; season++)
                {
                    double currentAbility = CalculateBatterAverage(player);
                    // 역할 복귀 계약은 "격차 5 안팎의 고정 경쟁자"를 대상으로 측정한다.
                    // 실제 월드 경쟁자 성장 분포는 별도 전체 커리어 코호트의 책임이다.
                    const double competitorAbility = 65d;
                    ManagerRoleEvaluationResult evaluation = role.Evaluate(
                        CreateRoleInput(player, currentAbility, isIncumbent: false),
                        new[] { CreateCompetitorInput(competitorAbility) },
                        ManagerDevelopmentStyle.Development);
                    double usage = ResolveUsage(evaluation.Role);

                    if (evaluation.Role is OpportunityRole.Backup or OpportunityRole.MinorLeague)
                    {
                        if (backupStartSeason < 0)
                        {
                            backupStartSeason = season;
                            backupEpisodes++;
                        }
                    }
                    else if (backupStartSeason >= 0)
                    {
                        if (season - backupStartSeason <= 2)
                            recoveredWithinTwoSeasons++;
                        backupStartSeason = -1;
                    }

                    ulong seasonSeed = 0x434F484F52540000UL +
                                       (ulong)(careerIndex * maximumSeasons + season);
                    bool isStarter = evaluation.Role is OpportunityRole.KeyStarter or OpportunityRole.Starter;
                    natural.Resolve(
                        player,
                        new SeasonUsageSummary(
                            usage,
                            BatterUsageWeights,
                            isStarter,
                            Math.Max(0d, competitorAbility - currentAbility)),
                        2028 + season,
                        seasonSeed,
                        new Pcg32Random(seasonSeed));
                    aging.Resolve(
                        player,
                        2028 + season,
                        seasonSeed + 1UL,
                        new Pcg32Random(seasonSeed + 1UL));

                    player.ChangeCondition(100);
                    TrainingAccessTier tier = ResolveTier(season);
                    TrainingProgramDefinition program = SelectProgram(
                        balance.Growth.Programs,
                        tier,
                        careerIndex % 3);
                    repetition.TryGetValue(program.Category, out int priorSelections);
                    ulong growthSeed = seasonSeed + 2UL;
                    growth.Resolve(
                        player,
                        program,
                        2028 + season,
                        priorSelections,
                        TrainingFitGrade.Normal,
                        growthSeed,
                        new Pcg32Random(growthSeed));
                    repetition[program.Category] = priorSelections + 1;
                    programSelections.TryGetValue(program.ProgramId, out int selectedCount);
                    programSelections[program.ProgramId] = selectedCount + 1;
                    player.AdvanceAge();
                }

                finalAbilityTotal += CalculateBatterAverage(player);
                for (int abilityIndex = (int)PlayerAbility.Contact;
                     abilityIndex <= (int)PlayerAbility.BatterMental;
                     abilityIndex++)
                {
                    var ability = (PlayerAbility)abilityIndex;
                    if (player.BaseAbilities.Get(ability) >= player.PotentialByAbility.Get(ability))
                        potentialReachedAbilities++;
                    evaluatedAbilities++;
                }
            }

            string mostSelectedProgram = string.Empty;
            int mostSelectedCount = 0;
            foreach (KeyValuePair<string, int> pair in programSelections)
            {
                if (pair.Value <= mostSelectedCount) continue;
                mostSelectedProgram = pair.Key;
                mostSelectedCount = pair.Value;
            }

            int totalSelections = careerCount * maximumSeasons;
            return new GrowthRoleCohortReport(
                careerCount,
                maximumSeasons,
                finalAbilityTotal / careerCount,
                backupEpisodes,
                recoveredWithinTwoSeasons,
                potentialReachedAbilities / (double)evaluatedAbilities,
                mostSelectedProgram,
                mostSelectedCount / (double)totalSelections,
                programSelections.Count);
        }

        private static Player CreatePlayer(int careerIndex)
        {
            int build = careerIndex % 4;
            BatterAttributes attributes = build switch
            {
                0 => new BatterAttributes(60, 60, 60, 60, 60, 60),
                1 => new BatterAttributes(72, 66, 56, 52, 58, 56),
                2 => new BatterAttributes(56, 72, 62, 52, 58, 60),
                _ => new BatterAttributes(58, 54, 62, 72, 68, 46)
            };
            return new Player(
                careerIndex + 1,
                $"코호트 {careerIndex + 1}",
                PlayerPosition.Shortstop,
                Handedness.Right,
                Handedness.Right,
                attributes,
                default);
        }

        private static ManagerRoleEvaluationInput CreateRoleInput(
            PlayerGrowthState player,
            double currentAbility,
            bool isIncumbent)
        {
            double outlook = Math.Min(100d, currentAbility + CalculateAveragePotentialGap(player) * 2d);
            return new ManagerRoleEvaluationInput(
                currentAbility,
                lastSeasonPerformance: currentAbility,
                player.Condition,
                managerTrust: 50d,
                roleFit: 75d,
                growthOutlook: outlook,
                isPitcher: false,
                incumbentBonus: isIncumbent ? 2d : 0d);
        }

        private static ManagerRoleEvaluationInput CreateCompetitorInput(double ability)
        {
            return new ManagerRoleEvaluationInput(
                ability,
                ability,
                condition: 80d,
                managerTrust: 50d,
                roleFit: 75d,
                growthOutlook: ability,
                isPitcher: false,
                incumbentBonus: 2d);
        }

        private static double ResolveUsage(OpportunityRole role)
        {
            return role switch
            {
                OpportunityRole.KeyStarter or OpportunityRole.Starter => 1d,
                OpportunityRole.Platoon => 0.65d,
                OpportunityRole.Backup or OpportunityRole.PinchHitter or OpportunityRole.PinchRunner => 0.25d,
                _ => 0.05d
            };
        }

        private static TrainingAccessTier ResolveTier(int season)
        {
            int tier = Math.Min((int)TrainingAccessTier.Legacy, season / 3);
            return (TrainingAccessTier)tier;
        }

        private static TrainingProgramDefinition SelectProgram(
            TrainingProgramDefinition[] programs,
            TrainingAccessTier tier,
            int strategy)
        {
            TrainingProgramDefinition selected = null;
            double selectedScore = double.MinValue;
            for (int index = 0; index < programs.Length; index++)
            {
                TrainingProgramDefinition candidate = programs[index];
                if (!candidate.CanUse(PlayerType.Batter) || !candidate.CanAccess(tier) ||
                    candidate.ProgramPower <= 0d || candidate.DurationWeeks <= 0)
                {
                    continue;
                }

                double score = strategy switch
                {
                    0 => candidate.ProgramPower / candidate.DurationWeeks,
                    1 => candidate.MoneyCost == 0L
                        ? candidate.ProgramPower * 10d
                        : candidate.ProgramPower * 10_000_000d / candidate.MoneyCost,
                    _ => candidate.MinimumGuaranteedGain * 10d + candidate.MaxTotalGain
                };
                if (score <= selectedScore) continue;
                selected = candidate;
                selectedScore = score;
            }
            return selected ?? throw new InvalidOperationException($"{tier}에서 타자 성장 프로그램을 찾지 못했습니다.");
        }

        private static double CalculateBatterAverage(PlayerGrowthState player)
        {
            int total = 0;
            for (int index = (int)PlayerAbility.Contact; index <= (int)PlayerAbility.BatterMental; index++)
                total += player.BaseAbilities.Get((PlayerAbility)index) + player.GetPeakBonus((PlayerAbility)index);
            return total / 6d;
        }

        private static double CalculateAveragePotentialGap(PlayerGrowthState player)
        {
            int total = 0;
            for (int index = (int)PlayerAbility.Contact; index <= (int)PlayerAbility.BatterMental; index++)
            {
                var ability = (PlayerAbility)index;
                total += Math.Max(0, player.PotentialByAbility.Get(ability) - player.BaseAbilities.Get(ability));
            }
            return total / 6d;
        }
    }

    internal readonly struct GrowthRoleCohortReport
    {
        public GrowthRoleCohortReport(
            int careerCount,
            int maximumSeasons,
            double averageFinalAbility,
            int backupEpisodes,
            int recoveredWithinTwoSeasons,
            double potentialReachRate,
            string mostSelectedProgram,
            double mostSelectedProgramRate,
            int selectedProgramCount)
        {
            CareerCount = careerCount;
            MaximumSeasons = maximumSeasons;
            AverageFinalAbility = averageFinalAbility;
            BackupEpisodes = backupEpisodes;
            RecoveredWithinTwoSeasons = recoveredWithinTwoSeasons;
            PotentialReachRate = potentialReachRate;
            MostSelectedProgram = mostSelectedProgram;
            MostSelectedProgramRate = mostSelectedProgramRate;
            SelectedProgramCount = selectedProgramCount;
        }

        public int CareerCount { get; }
        public int MaximumSeasons { get; }
        public double AverageFinalAbility { get; }
        public int BackupEpisodes { get; }
        public int RecoveredWithinTwoSeasons { get; }
        public double PotentialReachRate { get; }
        public string MostSelectedProgram { get; }
        public double MostSelectedProgramRate { get; }
        public int SelectedProgramCount { get; }
        public double RecoveryRate => BackupEpisodes == 0
            ? 0d
            : RecoveredWithinTwoSeasons / (double)BackupEpisodes;

        public string Format()
        {
            var builder = new StringBuilder();
            builder.AppendLine($"성장·역할 코호트: {CareerCount:N0}명 × {MaximumSeasons}시즌");
            builder.AppendLine($"최종 평균 기량: {AverageFinalAbility:F2}");
            builder.AppendLine(
                $"백업 에피소드 2시즌 내 복귀: {RecoveredWithinTwoSeasons:N0}/{BackupEpisodes:N0} " +
                $"({RecoveryRate:P1})");
            builder.AppendLine($"Potential 도달 능력 비율: {PotentialReachRate:P1}");
            builder.AppendLine(
                $"최다 선택 프로그램: {MostSelectedProgram} ({MostSelectedProgramRate:P1}), " +
                $"선택된 프로그램 종류 {SelectedProgramCount}");
            builder.Append("주의: 이 모드는 성장·노쇠·역할 평가 전용이며 리그 월드·계약·경제 분포를 증명하지 않습니다.");
            return builder.ToString();
        }

        public void Validate()
        {
            if (RecoveryRate < 0.30d)
                throw new InvalidOperationException($"백업 2시즌 내 복귀율이 기준 미달입니다: {RecoveryRate:P1}");
            if (MostSelectedProgramRate >= 0.45d)
            {
                throw new InvalidOperationException(
                    $"단일 프로그램 선택률이 지배 전략 기준을 넘었습니다: {MostSelectedProgramRate:P1}");
            }
            if (SelectedProgramCount < 3)
                throw new InvalidOperationException("성장 전략에서 선택된 프로그램 종류가 너무 적습니다.");
            if (AverageFinalAbility < 50d || AverageFinalAbility > 80d)
                throw new InvalidOperationException($"20시즌 최종 평균 기량이 비정상 범위입니다: {AverageFinalAbility:F2}");
        }
    }
}
