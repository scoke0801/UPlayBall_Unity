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
            double fatigueRisk = input.Fatigue / 100d * _balance.FatigueRiskAtMaximum;
            double ageRisk = Math.Max(0, input.Age - _balance.AgeRiskStart) * _balance.AgeRiskPerYear;
            double workloadRisk = Math.Max(0d, input.RecentWorkloadRatio - 1d) * _balance.WorkloadRiskAtDouble;
            double trainingRisk = input.TrainingIntensity * _balance.TrainingRiskAtMaximum;
            double existingRisk = input.HasExistingInjury ? _balance.ExistingInjuryRisk : 0d;
            double durabilityReduction = input.Durability / 100d * _balance.DurabilityReductionAtMaximum;
            return ClampProbability(
                _balance.BaseRisk + fatigueRisk + ageRisk + workloadRisk + trainingRisk + existingRisk - durabilityReduction);
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
            double risk = CalculateRisk(input);
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
                randomSeed);
            player.RecordInjury(record);
            return record;
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
    }
}
