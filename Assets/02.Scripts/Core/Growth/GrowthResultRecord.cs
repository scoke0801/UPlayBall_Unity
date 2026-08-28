using System;

namespace Baseball.Core.Growth
{
    /// <summary>
    /// 한 성장 처리에서 발생한 능력치 변화를 표현한다.
    /// </summary>
    public readonly struct AbilityChange
    {
        public AbilityChange(PlayerAbility ability, int amount)
        {
            if (ability < 0 || ability >= PlayerAbility.Count)
                throw new ArgumentOutOfRangeException(nameof(ability));
            Ability = ability;
            Amount = amount;
        }

        public PlayerAbility Ability { get; }
        public int Amount { get; }
    }

    /// <summary>
    /// 버그 재현과 밸런스 분석에 필요한 성장 계산 입력의 핵심 스냅샷이다.
    /// </summary>
    public readonly struct GrowthInputSnapshot
    {
        public GrowthInputSnapshot(
            int age,
            int condition,
            WorkEthicGrade workEthic,
            TrainingFitGrade trainingFit,
            int repetitionCount)
        {
            Age = age;
            Condition = condition;
            WorkEthic = workEthic;
            TrainingFit = trainingFit;
            RepetitionCount = repetitionCount;
        }

        public int Age { get; }
        public int Condition { get; }
        public WorkEthicGrade WorkEthic { get; }
        public TrainingFitGrade TrainingFit { get; }
        public int RepetitionCount { get; }
    }

    /// <summary>
    /// Seed와 원인을 포함해 세이브에 누적할 수 있는 한 번의 성장 결과다.
    /// </summary>
    public sealed class GrowthResultRecord
    {
        public GrowthResultRecord(
            int playerId,
            int seasonYear,
            GrowthSourceType sourceType,
            string sourceId,
            GrowthInputSnapshot inputSnapshot,
            ulong randomSeed,
            AbilityChange[] abilityChanges,
            AbilityChange[] potentialChanges,
            int conditionChange,
            long moneySpent,
            int weeksSpent,
            GrowthInjuryResult injuryResult = GrowthInjuryResult.None,
            TrainingIntensity intensity = TrainingIntensity.Standard)
        {
            if (playerId <= 0)
                throw new ArgumentOutOfRangeException(nameof(playerId));
            PlayerId = playerId;
            SeasonYear = seasonYear;
            SourceType = sourceType;
            SourceId = sourceId ?? string.Empty;
            InputSnapshot = inputSnapshot;
            RandomSeed = randomSeed;
            AbilityChanges = abilityChanges ?? Array.Empty<AbilityChange>();
            PotentialChanges = potentialChanges ?? Array.Empty<AbilityChange>();
            ConditionChange = conditionChange;
            MoneySpent = moneySpent;
            WeeksSpent = weeksSpent;
            InjuryResult = injuryResult;
            Intensity = intensity;
        }

        public int PlayerId { get; }
        public int SeasonYear { get; }
        public GrowthSourceType SourceType { get; }
        public string SourceId { get; }
        public GrowthInputSnapshot InputSnapshot { get; }
        public ulong RandomSeed { get; }
        public AbilityChange[] AbilityChanges { get; }
        public AbilityChange[] PotentialChanges { get; }
        public int ConditionChange { get; }
        public long MoneySpent { get; }
        public int WeeksSpent { get; }
        public GrowthInjuryResult InjuryResult { get; }
        public TrainingIntensity Intensity { get; }
    }
}
