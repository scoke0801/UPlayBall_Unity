using System;
using System.Collections.Generic;
using Baseball.Core.Players;

namespace Baseball.Core.Growth
{
    /// <summary>
    /// Base Ability, Potential과 장기 성장 이력을 소유하는 선수별 런타임 상태다.
    /// </summary>
    public sealed class PlayerGrowthState
    {
        private readonly TrainingFitGrade[] _trainingAffinities;
        private readonly List<GrowthResultRecord> _growthHistory;
        private readonly List<InjuryRecord> _injuryHistory;

        public PlayerGrowthState(
            int playerId,
            int age,
            PlayerType playerType,
            AbilityRatings baseAbilities,
            AbilityRatings potentialByAbility,
            WorkEthicGrade workEthic,
            int condition,
            int fatigue,
            int durability)
        {
            if (playerId <= 0)
                throw new ArgumentOutOfRangeException(nameof(playerId));
            if (age < 16 || age > 60)
                throw new ArgumentOutOfRangeException(nameof(age));

            PlayerId = playerId;
            Age = age;
            PlayerType = playerType;
            BaseAbilities = baseAbilities ?? throw new ArgumentNullException(nameof(baseAbilities));
            PotentialByAbility = potentialByAbility ?? throw new ArgumentNullException(nameof(potentialByAbility));
            WorkEthic = workEthic;
            Condition = ValidatePercentage(condition, nameof(condition));
            Fatigue = ValidatePercentage(fatigue, nameof(fatigue));
            Durability = ValidatePercentage(durability, nameof(durability));
            _trainingAffinities = new TrainingFitGrade[(int)TrainingCategory.Count];
            for (int index = 0; index < _trainingAffinities.Length; index++)
                _trainingAffinities[index] = TrainingFitGrade.Normal;
            _growthHistory = new List<GrowthResultRecord>();
            _injuryHistory = new List<InjuryRecord>();
        }

        public int PlayerId { get; }
        public int Age { get; private set; }
        public PlayerType PlayerType { get; }
        public CareerPhase CareerPhase => GetCareerPhase(Age);
        public AbilityRatings BaseAbilities { get; }
        public AbilityRatings PotentialByAbility { get; }
        public WorkEthicGrade WorkEthic { get; }
        public int Condition { get; private set; }
        public int Fatigue { get; private set; }
        public int Durability { get; private set; }
        public IReadOnlyList<GrowthResultRecord> GrowthHistory => _growthHistory;
        public IReadOnlyList<InjuryRecord> InjuryHistory => _injuryHistory;

        public TrainingFitGrade GetTrainingFit(TrainingCategory category)
        {
            ValidateCategory(category);
            return _trainingAffinities[(int)category];
        }

        public void SetTrainingFit(TrainingCategory category, TrainingFitGrade fit)
        {
            ValidateCategory(category);
            _trainingAffinities[(int)category] = fit;
        }

        public int ApplyBaseAbilityChange(PlayerAbility ability, int delta)
        {
            int maximum = Math.Min(
                AbilityRatings.Maximum,
                PotentialByAbility.Get(ability) + 3);
            return BaseAbilities.AddClamped(ability, delta, AbilityRatings.Minimum, maximum);
        }

        public int ApplyPotentialChange(PlayerAbility ability, int delta)
        {
            return PotentialByAbility.AddClamped(ability, delta);
        }

        public int ChangeCondition(int delta)
        {
            int before = Condition;
            Condition = Clamp(Condition + delta, 0, 100);
            return Condition - before;
        }

        public void SetFatigue(int value)
        {
            Fatigue = ValidatePercentage(value, nameof(value));
        }

        public void AdvanceAge()
        {
            Age++;
        }

        public void RecordGrowth(GrowthResultRecord record)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));
            if (record.PlayerId != PlayerId)
                throw new InvalidOperationException("다른 선수의 성장 기록을 추가할 수 없습니다.");
            _growthHistory.Add(record);
        }

        public void RecordInjury(InjuryRecord record)
        {
            _injuryHistory.Add(record ?? throw new ArgumentNullException(nameof(record)));
        }

        public static CareerPhase GetCareerPhase(int age)
        {
            if (age <= 22) return CareerPhase.Growth;
            if (age <= 27) return CareerPhase.Prime;
            if (age <= 31) return CareerPhase.Skilled;
            if (age <= 34) return CareerPhase.Decline;
            return CareerPhase.LateCareer;
        }

        private static int ValidatePercentage(int value, string parameterName)
        {
            if (value < 0 || value > 100)
                throw new ArgumentOutOfRangeException(parameterName, value, "0~100 범위여야 합니다.");
            return value;
        }

        private static void ValidateCategory(TrainingCategory category)
        {
            if (category < 0 || category >= TrainingCategory.Count)
                throw new ArgumentOutOfRangeException(nameof(category));
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum) return minimum;
            return value > maximum ? maximum : value;
        }
    }
}
