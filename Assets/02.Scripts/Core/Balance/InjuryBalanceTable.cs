using System;

namespace Baseball.Core.Balance
{
    /// <summary>
    /// 경기·훈련 부상 위험 요인과 심각도 분포를 보관한다.
    /// </summary>
    public readonly struct InjuryBalanceTable
    {
        public InjuryBalanceTable(
            double baseRisk,
            double fatigueRiskAtMaximum,
            int ageRiskStart,
            double ageRiskPerYear,
            double workloadRiskAtDouble,
            double trainingRiskAtMaximum,
            double existingInjuryRisk,
            double durabilityReductionAtMaximum,
            double discomfortShare,
            double minorShare,
            double seriousShare,
            int specialistTreatmentCost)
        {
            double severitySum = discomfortShare + minorShare + seriousShare;
            if (baseRisk < 0d || severitySum < 0d || severitySum > 1d)
                throw new ArgumentOutOfRangeException(nameof(baseRisk));
            if (ageRiskStart < 0 || specialistTreatmentCost < 0)
                throw new ArgumentOutOfRangeException(nameof(ageRiskStart));
            BaseRisk = baseRisk;
            FatigueRiskAtMaximum = fatigueRiskAtMaximum;
            AgeRiskStart = ageRiskStart;
            AgeRiskPerYear = ageRiskPerYear;
            WorkloadRiskAtDouble = workloadRiskAtDouble;
            TrainingRiskAtMaximum = trainingRiskAtMaximum;
            ExistingInjuryRisk = existingInjuryRisk;
            DurabilityReductionAtMaximum = durabilityReductionAtMaximum;
            DiscomfortShare = discomfortShare;
            MinorShare = minorShare;
            SeriousShare = seriousShare;
            SpecialistTreatmentCost = specialistTreatmentCost;
        }

        public double BaseRisk { get; }
        public double FatigueRiskAtMaximum { get; }
        public int AgeRiskStart { get; }
        public double AgeRiskPerYear { get; }
        public double WorkloadRiskAtDouble { get; }
        public double TrainingRiskAtMaximum { get; }
        public double ExistingInjuryRisk { get; }
        public double DurabilityReductionAtMaximum { get; }
        public double DiscomfortShare { get; }
        public double MinorShare { get; }
        public double SeriousShare { get; }
        public int SpecialistTreatmentCost { get; }

        public static InjuryBalanceTable CreateDefault()
        {
            // 단일 경기의 중상은 드물게 유지하되 피로·과부하·기존 부상이 겹치면 위험 이유가 드러나게 한다.
            return new InjuryBalanceTable(
                baseRisk: 0.0020d,
                fatigueRiskAtMaximum: 0.0100d,
                ageRiskStart: 30,
                ageRiskPerYear: 0.0005d,
                workloadRiskAtDouble: 0.0080d,
                trainingRiskAtMaximum: 0.0150d,
                existingInjuryRisk: 0.0100d,
                durabilityReductionAtMaximum: 0.0030d,
                discomfortShare: 0.55d,
                minorShare: 0.30d,
                seriousShare: 0.13d,
                specialistTreatmentCost: 500);
        }
    }
}
