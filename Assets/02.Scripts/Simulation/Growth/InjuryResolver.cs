using System;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Simulation.Random;

namespace Baseball.Simulation.Growth
{
    /// <summary>
    /// 피로·나이·출장량·훈련 강도·내구도를 설명 가능한 부상 결과로 변환한다.
    /// </summary>
    public sealed class InjuryResolver
    {
        private readonly InjuryBalanceTable _balance;

        public InjuryResolver(InjuryBalanceTable balance)
        {
            _balance = balance;
        }

        public double CalculateRisk(InjuryRiskInput input)
        {
            return EvaluateRisk(input).Risk;
        }

        public InjuryRiskEvaluationResult EvaluateRisk(InjuryRiskInput input)
        {
            double fatigueRisk = input.Fatigue / 100d * _balance.FatigueRiskAtMaximum;
            double ageRisk = Math.Max(0, input.Age - _balance.AgeRiskStart) * _balance.AgeRiskPerYear;
            double workloadRisk = Math.Max(0d, input.RecentWorkloadRatio - 1d) * _balance.WorkloadRiskAtDouble;
            double trainingRisk = input.TrainingIntensity * _balance.TrainingRiskAtMaximum;
            double existingRisk = input.HasExistingInjury ? _balance.ExistingInjuryRisk : 0d;
            double durabilityReduction = input.Durability / 100d * _balance.DurabilityReductionAtMaximum;
            double risk = ClampProbability(
                _balance.BaseRisk + fatigueRisk + ageRisk + workloadRisk + trainingRisk + existingRisk - durabilityReduction);
            var factors = new[]
            {
                CreateFactor(DecisionReasonCode.BaseRisk, _balance.BaseRisk, _balance.BaseRisk, 1),
                CreateFactor(DecisionReasonCode.Fatigue, input.Fatigue, fatigueRisk, 2),
                CreateFactor(DecisionReasonCode.AgeCurve, input.Age, ageRisk, 3),
                CreateFactor(DecisionReasonCode.Workload, input.RecentWorkloadRatio, workloadRisk, 4),
                CreateFactor(DecisionReasonCode.TrainingIntensity, input.TrainingIntensity, trainingRisk, 5),
                CreateFactor(DecisionReasonCode.ExistingInjury, input.HasExistingInjury ? 1d : 0d, existingRisk, 6),
                new DecisionFactor(
                    DecisionReasonCode.Durability,
                    input.Durability,
                    input.Durability,
                    _balance.DurabilityReductionAtMaximum,
                    -durabilityReduction,
                    durabilityReduction > 0d ? DecisionDirection.Negative : DecisionDirection.Neutral,
                    7)
            };
            DecisionReasonCode summary = DecisionReasonCode.BaseRisk;
            double strongest = _balance.BaseRisk;
            for (int index = 1; index < factors.Length - 1; index++)
            {
                if (factors[index].Contribution <= strongest)
                    continue;
                strongest = factors[index].Contribution;
                summary = factors[index].ReasonCode;
            }
            var actions = input.HasExistingInjury
                ? new[] { RecommendedActionCode.SeekTreatment, RecommendedActionCode.ChooseRecovery }
                : input.Fatigue >= 70 || input.RecentWorkloadRatio > 1.2d
                    ? new[] { RecommendedActionCode.ReduceWorkload, RecommendedActionCode.ChooseRecovery }
                    : input.TrainingIntensity >= 0.7d
                        ? new[] { RecommendedActionCode.ChooseRecovery }
                        : Array.Empty<RecommendedActionCode>();
            return new InjuryRiskEvaluationResult(
                risk,
                new DecisionExplanation(
                    DecisionType.Injury,
                    summary,
                    factors,
                    new[] { risk },
                    actions,
                    rulesVersion: 1));
        }

        public InjuryRecord Resolve(
            PlayerGrowthState player,
            InjuryRiskInput input,
            int seasonYear,
            string sourceId,
            ulong randomSeed,
            IRandomSource random)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (random == null) throw new ArgumentNullException(nameof(random));
            InjuryRiskEvaluationResult evaluation = EvaluateRisk(input);
            double recoveryReduction = player.SeasonInjuryRiskReduction;
            double risk = evaluation.Risk * (1d - recoveryReduction);
            DecisionExplanation explanation = ApplyRecoveryProtection(
                evaluation.Explanation,
                evaluation.Risk,
                risk,
                recoveryReduction);
            if (random.NextDouble() >= risk)
                return null;

            InjurySeverity severity = RollSeverity(random.NextDouble());
            GetAbsenceRange(severity, out int minimumDays, out int maximumDays);
            var record = new InjuryRecord(
                seasonYear,
                sourceId,
                severity,
                minimumDays,
                maximumDays,
                risk,
                randomSeed,
                explanation);
            player.RecordInjury(record);
            return record;
        }

        private static DecisionExplanation ApplyRecoveryProtection(
            DecisionExplanation source,
            double originalRisk,
            double adjustedRisk,
            double reduction)
        {
            if (reduction <= 0d)
                return source;
            var factors = new DecisionFactor[source.Factors.Length + 1];
            Array.Copy(source.Factors, factors, source.Factors.Length);
            factors[^1] = new DecisionFactor(
                DecisionReasonCode.RecoveryProtection,
                reduction,
                reduction,
                1d,
                adjustedRisk - originalRisk,
                DecisionDirection.Negative,
                source.Factors.Length + 1);
            return new DecisionExplanation(
                source.DecisionType,
                source.SummaryReasonCode,
                factors,
                new[] { adjustedRisk },
                source.RecommendedActions,
                source.RulesVersion);
        }

        public void ChooseTreatment(
            InjuryRecord injury,
            InjuryTreatmentChoice choice,
            CareerEconomyState economy,
            int seasonYear)
        {
            if (injury == null) throw new ArgumentNullException(nameof(injury));
            if (economy == null) throw new ArgumentNullException(nameof(economy));
            if (choice == InjuryTreatmentChoice.SpecialistTreatment)
            {
                economy.Spend(
                    seasonYear,
                    MoneyTransactionType.TreatmentExpense,
                    injury.SourceId,
                    _balance.SpecialistTreatmentCost);
            }
            injury.ChooseTreatment(choice);
        }

        private InjurySeverity RollSeverity(double roll)
        {
            if (roll < _balance.DiscomfortShare) return InjurySeverity.Discomfort;
            roll -= _balance.DiscomfortShare;
            if (roll < _balance.MinorShare) return InjurySeverity.Minor;
            roll -= _balance.MinorShare;
            return roll < _balance.SeriousShare ? InjurySeverity.Serious : InjurySeverity.Major;
        }

        private static void GetAbsenceRange(InjurySeverity severity, out int minimumDays, out int maximumDays)
        {
            switch (severity)
            {
                case InjurySeverity.Discomfort: minimumDays = 0; maximumDays = 3; break;
                case InjurySeverity.Minor: minimumDays = 5; maximumDays = 14; break;
                case InjurySeverity.Serious: minimumDays = 21; maximumDays = 60; break;
                default: minimumDays = 90; maximumDays = 240; break;
            }
        }

        private static double ClampProbability(double value)
        {
            if (value < 0d) return 0d;
            return value > 0.50d ? 0.50d : value;
        }

        private static DecisionFactor CreateFactor(
            DecisionReasonCode code,
            double rawValue,
            double contribution,
            int priority)
        {
            return new DecisionFactor(
                code,
                rawValue,
                rawValue,
                1d,
                contribution,
                contribution > 0d ? DecisionDirection.Positive : DecisionDirection.Neutral,
                priority);
        }
    }
}
