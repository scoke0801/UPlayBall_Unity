using System;

namespace Baseball.Core.Growth
{
    public enum InjurySeverity
    {
        Discomfort,
        Minor,
        Serious,
        Major
    }

    public enum InjuryTreatmentChoice
    {
        ContinuePlaying,
        StandardTreatment,
        SpecialistTreatment
    }

    /// <summary>
    /// 발생 이유와 예상 결장 범위를 보존하는 한 번의 부상 기록이다.
    /// </summary>
    public sealed class InjuryRecord
    {
        public InjuryRecord(
            int seasonYear,
            string sourceId,
            InjurySeverity severity,
            int minimumAbsenceDays,
            int maximumAbsenceDays,
            double calculatedRisk,
            ulong randomSeed)
        {
            if (minimumAbsenceDays < 0 || maximumAbsenceDays < minimumAbsenceDays)
                throw new ArgumentOutOfRangeException(nameof(minimumAbsenceDays));
            SeasonYear = seasonYear;
            SourceId = sourceId ?? string.Empty;
            Severity = severity;
            MinimumAbsenceDays = minimumAbsenceDays;
            MaximumAbsenceDays = maximumAbsenceDays;
            CalculatedRisk = calculatedRisk;
            RandomSeed = randomSeed;
        }

        public int SeasonYear { get; }
        public string SourceId { get; }
        public InjurySeverity Severity { get; }
        public int MinimumAbsenceDays { get; }
        public int MaximumAbsenceDays { get; }
        public double CalculatedRisk { get; }
        public ulong RandomSeed { get; }
        public InjuryTreatmentChoice? TreatmentChoice { get; private set; }

        public void ChooseTreatment(InjuryTreatmentChoice choice)
        {
            if (TreatmentChoice.HasValue)
                throw new InvalidOperationException("부상 치료 방식은 한 번만 결정할 수 있습니다.");
            if (choice == InjuryTreatmentChoice.ContinuePlaying && Severity >= InjurySeverity.Serious)
                throw new InvalidOperationException("중상 이상은 계속 출전할 수 없습니다.");
            TreatmentChoice = choice;
        }
    }

    /// <summary>
    /// 부상 확률을 구성하는 모든 설명 가능한 입력값이다.
    /// </summary>
    public readonly struct InjuryRiskInput
    {
        public InjuryRiskInput(
            int age,
            int fatigue,
            double recentWorkloadRatio,
            double trainingIntensity,
            bool hasExistingInjury,
            int durability)
        {
            if (fatigue < 0 || fatigue > 100 || durability < 0 || durability > 100)
                throw new ArgumentOutOfRangeException(nameof(fatigue));
            if (recentWorkloadRatio < 0d || trainingIntensity < 0d || trainingIntensity > 1d)
                throw new ArgumentOutOfRangeException(nameof(recentWorkloadRatio));
            Age = age;
            Fatigue = fatigue;
            RecentWorkloadRatio = recentWorkloadRatio;
            TrainingIntensity = trainingIntensity;
            HasExistingInjury = hasExistingInjury;
            Durability = durability;
        }

        public int Age { get; }
        public int Fatigue { get; }
        public double RecentWorkloadRatio { get; }
        public double TrainingIntensity { get; }
        public bool HasExistingInjury { get; }
        public int Durability { get; }
    }
}
