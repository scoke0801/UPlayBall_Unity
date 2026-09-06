using System;
using System.Collections.Generic;
using System.Text;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Simulation.Growth;
using Baseball.Simulation.Match;
using Baseball.Simulation.Random;

namespace Baseball.Tools.SimulationDiagnostics
{
    /// <summary>실제 자연 성장·노쇠·훈련 공식을 반복해 주무기 등급 상승 속도를 비교한다.</summary>
    internal static class PitchGradeCareerDiagnostics
    {
        private const string FocusProgramId = "pitch_breaking_training";

        public static PitchGradeCareerReport Run(int careerCount, int maximumSeasons)
        {
            if (careerCount <= 0) throw new ArgumentOutOfRangeException(nameof(careerCount));
            if (maximumSeasons <= 0) throw new ArgumentOutOfRangeException(nameof(maximumSeasons));

            BalanceTable balance = BalanceTable.CreateDefault();
            TrainingProgramDefinition focusProgram = FindProgram(balance.Growth.Programs, FocusProgramId);
            AbilityWeight[] usageWeights = BuildStartingPitcherUsageWeights(balance.PlayerEvaluation);
            var control = new PitchGradeCohortAggregate(maximumSeasons);
            var regular = new PitchGradeCohortAggregate(maximumSeasons);
            var focused = new PitchGradeCohortAggregate(maximumSeasons);

            for (int careerIndex = 0; careerIndex < careerCount; careerIndex++)
            {
                PitchRepertoireEntry[] repertoire = CreateRepertoire(careerIndex);
                PlayerGrowthState controlPlayer = CreateGrowthState(careerIndex);
                PlayerGrowthState regularPlayer = CreateGrowthState(careerIndex);
                PlayerGrowthState focusedPlayer = CreateGrowthState(careerIndex);
                PitcherAttributes baseline = controlPlayer.BaseAbilities.ToPitcherAttributes();
                double nextGradeThreshold = balance.PitchArsenal.Grade
                    .GetProgress(CalculateStableQuality(controlPlayer, baseline, repertoire, balance.PitchArsenal))
                    .NextThreshold;

                SimulateCareer(
                    controlPlayer,
                    baseline,
                    repertoire,
                    usageWeights,
                    focusProgram: null,
                    programSelectionsPerOffseason: 0,
                    careerIndex,
                    maximumSeasons,
                    nextGradeThreshold,
                    balance,
                    control);
                SimulateCareer(
                    regularPlayer,
                    baseline,
                    repertoire,
                    usageWeights,
                    focusProgram,
                    programSelectionsPerOffseason: 1,
                    careerIndex,
                    maximumSeasons,
                    nextGradeThreshold,
                    balance,
                    regular);
                SimulateCareer(
                    focusedPlayer,
                    baseline,
                    repertoire,
                    usageWeights,
                    focusProgram,
                    programSelectionsPerOffseason: 3,
                    careerIndex,
                    maximumSeasons,
                    nextGradeThreshold,
                    balance,
                    focused);
            }

            return new PitchGradeCareerReport(
                careerCount,
                maximumSeasons,
                FocusProgramId,
                control.CreateSummary(careerCount),
                regular.CreateSummary(careerCount),
                focused.CreateSummary(careerCount));
        }

        private static void SimulateCareer(
            PlayerGrowthState player,
            PitcherAttributes baseline,
            PitchRepertoireEntry[] repertoire,
            AbilityWeight[] usageWeights,
            TrainingProgramDefinition focusProgram,
            int programSelectionsPerOffseason,
            int careerIndex,
            int maximumSeasons,
            double nextGradeThreshold,
            BalanceTable balance,
            PitchGradeCohortAggregate aggregate)
        {
            var natural = new NaturalDevelopmentResolver(balance.Growth);
            var aging = new AgingResolver(balance.Growth);
            var training = new GrowthResolver(balance.Growth);
            double initialQuality = CalculateStableQuality(player, baseline, repertoire, balance.PitchArsenal);
            int firstPromotionSeason = 0;
            double usageRatio = 0.45d + careerIndex % 56 / 100d;
            bool isStarter = usageRatio >= 0.75d;

            for (int season = 1; season <= maximumSeasons; season++)
            {
                ulong seasonSeed = DeterministicSeed.Derive(
                    0x504954434847524FUL,
                    (ulong)(careerIndex * maximumSeasons + season));
                natural.Resolve(
                    player,
                    new SeasonUsageSummary(usageRatio, usageWeights, isStarter),
                    2028 + season - 1,
                    seasonSeed,
                    new Pcg32Random(seasonSeed));
                aging.Resolve(
                    player,
                    2028 + season - 1,
                    seasonSeed + 1UL,
                    new Pcg32Random(seasonSeed + 1UL));

                if (focusProgram != null && programSelectionsPerOffseason > 0)
                {
                    player.ChangeCondition(100 - player.Condition);
                    for (int selection = 0; selection < programSelectionsPerOffseason; selection++)
                    {
                        if (player.Condition < focusProgram.MinimumCondition)
                            break;
                        ulong trainingSeed = seasonSeed + 2UL + (ulong)selection;
                        training.Resolve(
                            player,
                            focusProgram,
                            2028 + season - 1,
                            selection,
                            player.GetTrainingFit(TrainingCategory.Pitching),
                            trainingSeed,
                            new Pcg32Random(trainingSeed));
                        aggregate.RecordTrainingSelection();
                    }
                }

                double quality = CalculateStableQuality(player, baseline, repertoire, balance.PitchArsenal);
                if (firstPromotionSeason == 0 && quality >= nextGradeThreshold)
                    firstPromotionSeason = season;
                aggregate.RecordSeason(season, quality >= nextGradeThreshold);
                player.AdvanceAge();
            }

            double finalQuality = CalculateStableQuality(player, baseline, repertoire, balance.PitchArsenal);
            aggregate.RecordCareer(
                initialQuality,
                finalQuality,
                player.BaseAbilities.Get(PlayerAbility.Breaking),
                firstPromotionSeason,
                balance.PitchArsenal.Grade.GetGrade(finalQuality));
        }

        private static PlayerGrowthState CreateGrowthState(int careerIndex)
        {
            int[] baseValues = new AbilityRatings(50).ToArray();
            baseValues[(int)PlayerAbility.Stamina] = 48 + careerIndex % 13;
            baseValues[(int)PlayerAbility.Velocity] = 47 + careerIndex * 3 % 15;
            baseValues[(int)PlayerAbility.Stuff] = 46 + careerIndex * 5 % 17;
            baseValues[(int)PlayerAbility.Breaking] = 44 + careerIndex * 7 % 18;
            baseValues[(int)PlayerAbility.Control] = 46 + careerIndex * 11 % 17;
            baseValues[(int)PlayerAbility.PitcherMental] = 48 + careerIndex * 13 % 15;

            int[] potentialValues = (int[])baseValues.Clone();
            for (int index = (int)PlayerAbility.Stamina;
                 index <= (int)PlayerAbility.PitcherMental;
                 index++)
            {
                potentialValues[index] = Math.Min(100, baseValues[index] + 14 + (careerIndex + index) % 13);
            }

            return new PlayerGrowthState(
                careerIndex + 1,
                18,
                PlayerType.Pitcher,
                new AbilityRatings(baseValues),
                new AbilityRatings(potentialValues),
                (WorkEthicGrade)(careerIndex % 4),
                100,
                0,
                65 + careerIndex % 26);
        }

        private static PitchRepertoireEntry[] CreateRepertoire(int careerIndex)
        {
            PitchType primary = (careerIndex % 3) switch
            {
                0 => PitchType.Slider,
                1 => PitchType.Curveball,
                _ => PitchType.Changeup
            };
            PitchType third = (careerIndex % 3) switch
            {
                0 => PitchType.Changeup,
                1 => PitchType.Slider,
                _ => PitchType.Curveball
            };
            return new[]
            {
                new PitchRepertoireEntry(primary, 55, true),
                new PitchRepertoireEntry(PitchType.FourSeamFastball, 45, false),
                new PitchRepertoireEntry(third, 45, false)
            };
        }

        private static double CalculateStableQuality(
            PlayerGrowthState player,
            PitcherAttributes baseline,
            PitchRepertoireEntry[] repertoire,
            PitchArsenalBalance balance)
        {
            PitchRepertoireEntry primary = repertoire[0];
            return PitchEffectivenessResolver.ResolveStableQuality(
                primary,
                player.BaseAbilities.ToPitcherAttributes(),
                balance,
                baseline,
                repertoire.Length,
                priorityIndex: 0);
        }

        private static AbilityWeight[] BuildStartingPitcherUsageWeights(PlayerEvaluationBalance evaluation)
        {
            double key = evaluation.KeyAttributeWeight;
            double support = evaluation.SupportingAttributeWeight;
            double total = key * 2d + support * 4d;
            return new[]
            {
                new AbilityWeight(PlayerAbility.Stamina, key / total),
                new AbilityWeight(PlayerAbility.Velocity, support / total),
                new AbilityWeight(PlayerAbility.Stuff, support / total),
                new AbilityWeight(PlayerAbility.Breaking, support / total),
                new AbilityWeight(PlayerAbility.Control, key / total),
                new AbilityWeight(PlayerAbility.PitcherMental, support / total)
            };
        }

        private static TrainingProgramDefinition FindProgram(
            TrainingProgramDefinition[] programs,
            string programId)
        {
            for (int index = 0; index < programs.Length; index++)
            {
                if (string.Equals(programs[index].ProgramId, programId, StringComparison.Ordinal))
                    return programs[index];
            }
            throw new InvalidOperationException($"성장 프로그램을 찾지 못했습니다: {programId}");
        }
    }

    internal sealed class PitchGradeCohortAggregate
    {
        private readonly int[] _promotedBySeason;
        private readonly List<int> _firstPromotionSeasons = new List<int>();
        private readonly Dictionary<string, int> _finalGrades = new Dictionary<string, int>(StringComparer.Ordinal);
        private double _initialQualityTotal;
        private double _finalQualityTotal;
        private long _finalBreakingTotal;
        private long _trainingSelections;

        public PitchGradeCohortAggregate(int maximumSeasons)
        {
            _promotedBySeason = new int[maximumSeasons];
        }

        public void RecordSeason(int season, bool hasPromoted)
        {
            if (hasPromoted)
                _promotedBySeason[season - 1]++;
        }

        public void RecordTrainingSelection()
        {
            _trainingSelections++;
        }

        public void RecordCareer(
            double initialQuality,
            double finalQuality,
            int finalBreaking,
            int firstPromotionSeason,
            string finalGrade)
        {
            _initialQualityTotal += initialQuality;
            _finalQualityTotal += finalQuality;
            _finalBreakingTotal += finalBreaking;
            if (firstPromotionSeason > 0)
                _firstPromotionSeasons.Add(firstPromotionSeason);
            _finalGrades.TryGetValue(finalGrade, out int gradeCount);
            _finalGrades[finalGrade] = gradeCount + 1;
        }

        public PitchGradeCohortSummary CreateSummary(int careerCount)
        {
            _firstPromotionSeasons.Sort();
            var rates = new double[_promotedBySeason.Length];
            for (int index = 0; index < rates.Length; index++)
                rates[index] = _promotedBySeason[index] / (double)careerCount;
            return new PitchGradeCohortSummary(
                _initialQualityTotal / careerCount,
                _finalQualityTotal / careerCount,
                _finalBreakingTotal / (double)careerCount,
                _trainingSelections / (double)(careerCount * _promotedBySeason.Length),
                _firstPromotionSeasons.Count / (double)careerCount,
                Percentile(_firstPromotionSeasons, 0.5d),
                rates,
                _finalGrades);
        }

        private static double Percentile(List<int> sorted, double percentile)
        {
            if (sorted.Count == 0) return 0d;
            double position = (sorted.Count - 1) * percentile;
            int lower = (int)Math.Floor(position);
            int upper = (int)Math.Ceiling(position);
            if (lower == upper) return sorted[lower];
            return sorted[lower] + (sorted[upper] - sorted[lower]) * (position - lower);
        }
    }

    internal readonly struct PitchGradeCohortSummary
    {
        private readonly Dictionary<string, int> _finalGrades;

        public PitchGradeCohortSummary(
            double initialQuality,
            double finalQuality,
            double finalBreaking,
            double averageTrainingSelections,
            double promotionRate,
            double medianPromotionSeason,
            double[] promotedBySeason,
            Dictionary<string, int> finalGrades)
        {
            InitialQuality = initialQuality;
            FinalQuality = finalQuality;
            FinalBreaking = finalBreaking;
            AverageTrainingSelections = averageTrainingSelections;
            PromotionRate = promotionRate;
            MedianPromotionSeason = medianPromotionSeason;
            PromotedBySeason = promotedBySeason;
            _finalGrades = finalGrades;
        }

        public double InitialQuality { get; }
        public double FinalQuality { get; }
        public double FinalBreaking { get; }
        public double AverageTrainingSelections { get; }
        public double PromotionRate { get; }
        public double MedianPromotionSeason { get; }
        public double[] PromotedBySeason { get; }

        public string FormatGradeDistribution(int careerCount)
        {
            string[] order = { "D", "C", "B", "A", "S", "SS" };
            var builder = new StringBuilder();
            for (int index = 0; index < order.Length; index++)
            {
                _finalGrades.TryGetValue(order[index], out int count);
                if (count == 0) continue;
                if (builder.Length > 0) builder.Append(" / ");
                builder.Append(order[index]);
                builder.Append(' ');
                builder.Append((count / (double)careerCount).ToString("P1"));
            }
            return builder.ToString();
        }
    }

    internal readonly struct PitchGradeCareerReport
    {
        public PitchGradeCareerReport(
            int careerCount,
            int maximumSeasons,
            string focusProgramId,
            PitchGradeCohortSummary control,
            PitchGradeCohortSummary regular,
            PitchGradeCohortSummary focused)
        {
            CareerCount = careerCount;
            MaximumSeasons = maximumSeasons;
            FocusProgramId = focusProgramId;
            Control = control;
            Regular = regular;
            Focused = focused;
        }

        public int CareerCount { get; }
        public int MaximumSeasons { get; }
        public string FocusProgramId { get; }
        public PitchGradeCohortSummary Control { get; }
        public PitchGradeCohortSummary Regular { get; }
        public PitchGradeCohortSummary Focused { get; }

        public string Format()
        {
            var builder = new StringBuilder();
            builder.AppendLine($"구종 성장 코호트: {CareerCount:N0}명 × {MaximumSeasons}시즌");
            builder.AppendLine("대상: 생성 시 B 등급 주무기(슬라이더/커브/체인지업), 선발 활용량 45~100%");
            AppendCohort(builder, "자연 성장만", Control);
            AppendCohort(builder, $"매년 {FocusProgramId} 1회(3주)", Regular);
            AppendCohort(builder, $"매년 {FocusProgramId} 3회(9주)", Focused);
            builder.AppendLine($"9주 집중 훈련 효과: 최종 품질 +{Focused.FinalQuality - Control.FinalQuality:F2}, " +
                               $"다음 등급 도달률 +{Focused.PromotionRate - Control.PromotionRate:P1}");
            builder.Append("주의: 성장·노쇠·훈련 공식의 paired cohort이며 계약·부상·자금 제약은 포함하지 않습니다.");
            return builder.ToString();
        }

        public void Validate()
        {
            if (Regular.FinalQuality <= Control.FinalQuality || Focused.FinalQuality <= Regular.FinalQuality)
                throw new InvalidOperationException("훈련 횟수 증가가 구종 품질 증가로 이어지지 않았습니다.");
            if (Regular.PromotionRate <= Control.PromotionRate || Focused.PromotionRate <= Regular.PromotionRate)
                throw new InvalidOperationException("훈련 횟수 증가가 다음 등급 도달률을 높이지 못했습니다.");
            if (Focused.PromotionRate > 0d && Focused.MedianPromotionSeason <= 0d)
                throw new InvalidOperationException("등급 도달 시점 집계가 올바르지 않습니다.");
        }

        private void AppendCohort(
            StringBuilder builder,
            string label,
            PitchGradeCohortSummary summary)
        {
            int season3 = Math.Min(3, MaximumSeasons) - 1;
            int season5 = Math.Min(5, MaximumSeasons) - 1;
            int season10 = Math.Min(10, MaximumSeasons) - 1;
            builder.AppendLine($"[{label}]");
            builder.AppendLine($"  평균 품질 {summary.InitialQuality:F2} → {summary.FinalQuality:F2}, " +
                               $"최종 Breaking {summary.FinalBreaking:F2}, " +
                               $"시즌당 훈련 {summary.AverageTrainingSelections:F2}회");
            builder.AppendLine($"  다음 등급 도달 {summary.PromotionRate:P1}, " +
                               $"도달자 중앙값 {summary.MedianPromotionSeason:F1}시즌");
            builder.AppendLine($"  누적 도달률: 3시즌 {summary.PromotedBySeason[season3]:P1}, " +
                               $"5시즌 {summary.PromotedBySeason[season5]:P1}, " +
                               $"10시즌 {summary.PromotedBySeason[season10]:P1}");
            builder.AppendLine($"  최종 등급: {summary.FormatGradeDistribution(CareerCount)}");
        }
    }
}
