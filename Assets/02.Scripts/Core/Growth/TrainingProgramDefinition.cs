using System;
using Baseball.Core.Players;

namespace Baseball.Core.Growth
{
    public enum OffseasonActivityType
    {
        Rest,
        Rehabilitation,
        PersonalTraining,
        TrainingPartner,
        Study
    }

    public enum TrainingIntensity
    {
        Safe,
        Standard,
        Intensive
    }

    /// <summary>
    /// 커리어 진행 단계에 따라 해금되는 훈련 프로그램의 접근 등급이다.
    /// </summary>
    public enum TrainingAccessTier
    {
        Foundation,
        Advanced,
        Elite
    }

    /// <summary>
    /// 한 프로그램의 성장력을 능력치별로 나누는 고정 가중치다.
    /// </summary>
    public readonly struct AbilityWeight
    {
        public AbilityWeight(PlayerAbility ability, double weight)
        {
            if (ability < 0 || ability >= PlayerAbility.Count)
                throw new ArgumentOutOfRangeException(nameof(ability));
            if (weight <= 0d)
                throw new ArgumentOutOfRangeException(nameof(weight));
            Ability = ability;
            Weight = weight;
        }

        public PlayerAbility Ability { get; }
        public double Weight { get; }
    }

    /// <summary>
    /// 오프시즌 활동의 기간·비용·성장 채널을 데이터로 정의한다.
    /// </summary>
    public sealed class TrainingProgramDefinition
    {
        public TrainingProgramDefinition(
            string programId,
            OffseasonActivityType activityType,
            TrainingCategory category,
            PlayerType? targetPlayerType,
            int durationWeeks,
            long moneyCost,
            double programPower,
            AbilityWeight[] targetAbilityWeights,
            int minimumCondition,
            double injuryRisk,
            int maxTotalGain,
            int maxGainPerAbility,
            int conditionChange,
            int minimumGuaranteedGain = 0,
            string partnerId = "",
            bool canRaisePotential = false,
            TrainingIntensity intensity = TrainingIntensity.Standard,
            TrainingAccessTier minimumAccessTier = TrainingAccessTier.Foundation,
            double potentialBreakthroughChanceMultiplier = 1d,
            int minimumPotentialBreakthroughsWhenCapped = 0)
        {
            if (string.IsNullOrWhiteSpace(programId))
                throw new ArgumentException("ProgramId는 비어 있을 수 없습니다.", nameof(programId));
            if (category < 0 || category >= TrainingCategory.Count)
                throw new ArgumentOutOfRangeException(nameof(category));
            if (durationWeeks < 0 || durationWeeks > 12)
                throw new ArgumentOutOfRangeException(nameof(durationWeeks));
            if (moneyCost < 0L || programPower < 0d)
                throw new ArgumentOutOfRangeException(nameof(moneyCost));
            if (minimumCondition < 0 || minimumCondition > 100)
                throw new ArgumentOutOfRangeException(nameof(minimumCondition));
            if (injuryRisk < 0d || injuryRisk > 1d)
                throw new ArgumentOutOfRangeException(nameof(injuryRisk));
            if (maxTotalGain < 0 || maxGainPerAbility < 0 || minimumGuaranteedGain < 0)
                throw new ArgumentOutOfRangeException(nameof(maxTotalGain));
            if (minimumGuaranteedGain > maxTotalGain)
                throw new ArgumentOutOfRangeException(nameof(minimumGuaranteedGain));
            if (activityType == OffseasonActivityType.TrainingPartner && string.IsNullOrWhiteSpace(partnerId))
                throw new ArgumentException("훈련 파트너 활동에는 PartnerId가 필요합니다.", nameof(partnerId));
            if (intensity < TrainingIntensity.Safe || intensity > TrainingIntensity.Intensive)
                throw new ArgumentOutOfRangeException(nameof(intensity));
            if (minimumAccessTier < TrainingAccessTier.Foundation ||
                minimumAccessTier > TrainingAccessTier.Elite)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumAccessTier));
            }
            if (potentialBreakthroughChanceMultiplier < 0d)
                throw new ArgumentOutOfRangeException(nameof(potentialBreakthroughChanceMultiplier));
            if (minimumPotentialBreakthroughsWhenCapped < 0)
                throw new ArgumentOutOfRangeException(nameof(minimumPotentialBreakthroughsWhenCapped));
            if (!canRaisePotential && minimumPotentialBreakthroughsWhenCapped > 0)
            {
                throw new ArgumentException(
                    "Potential 돌파를 보장하는 프로그램은 CanRaisePotential이어야 합니다.",
                    nameof(minimumPotentialBreakthroughsWhenCapped));
            }

            ProgramId = programId.Trim();
            ActivityType = activityType;
            Category = category;
            TargetPlayerType = targetPlayerType;
            DurationWeeks = durationWeeks;
            MoneyCost = moneyCost;
            ProgramPower = programPower;
            TargetAbilityWeights = CopyWeights(targetAbilityWeights, programPower);
            MinimumCondition = minimumCondition;
            InjuryRisk = injuryRisk;
            MaxTotalGain = maxTotalGain;
            MaxGainPerAbility = maxGainPerAbility;
            ConditionChange = conditionChange;
            MinimumGuaranteedGain = minimumGuaranteedGain;
            PartnerId = partnerId?.Trim() ?? string.Empty;
            CanRaisePotential = canRaisePotential;
            Intensity = intensity;
            MinimumAccessTier = minimumAccessTier;
            PotentialBreakthroughChanceMultiplier = potentialBreakthroughChanceMultiplier;
            MinimumPotentialBreakthroughsWhenCapped = minimumPotentialBreakthroughsWhenCapped;
        }

        public string ProgramId { get; }
        public OffseasonActivityType ActivityType { get; }
        public TrainingCategory Category { get; }
        public PlayerType? TargetPlayerType { get; }
        public int DurationWeeks { get; }
        public long MoneyCost { get; }
        public double ProgramPower { get; }
        public AbilityWeight[] TargetAbilityWeights { get; }
        public int MinimumCondition { get; }
        public double InjuryRisk { get; }
        public int MaxTotalGain { get; }
        public int MaxGainPerAbility { get; }
        public int ConditionChange { get; }
        public int MinimumGuaranteedGain { get; }
        public string PartnerId { get; }
        public bool CanRaisePotential { get; }
        public TrainingIntensity Intensity { get; }
        public TrainingAccessTier MinimumAccessTier { get; }
        public double PotentialBreakthroughChanceMultiplier { get; }
        public int MinimumPotentialBreakthroughsWhenCapped { get; }
        public bool IsStudy => ActivityType == OffseasonActivityType.Study;
        public bool SupportsIntensity => ActivityType == OffseasonActivityType.PersonalTraining;

        public bool CanUse(PlayerType playerType)
        {
            return !TargetPlayerType.HasValue || TargetPlayerType.Value == playerType;
        }

        public bool CanAccess(TrainingAccessTier accessTier)
        {
            return accessTier >= MinimumAccessTier;
        }

        private static AbilityWeight[] CopyWeights(AbilityWeight[] source, double programPower)
        {
            if (source == null || source.Length == 0)
            {
                if (programPower > 0d)
                    throw new ArgumentException("성장력이 있는 프로그램에는 능력치 가중치가 필요합니다.", nameof(source));
                return Array.Empty<AbilityWeight>();
            }

            var result = new AbilityWeight[source.Length];
            double totalWeight = 0d;
            for (int index = 0; index < source.Length; index++)
            {
                for (int previous = 0; previous < index; previous++)
                {
                    if (source[previous].Ability == source[index].Ability)
                        throw new ArgumentException("프로그램의 대상 능력치는 중복될 수 없습니다.", nameof(source));
                }
                result[index] = source[index];
                totalWeight += source[index].Weight;
            }

            if (Math.Abs(totalWeight - 1d) > 0.000001d)
                throw new ArgumentException("능력치 가중치 합은 1이어야 합니다.", nameof(source));
            return result;
        }
    }
}
