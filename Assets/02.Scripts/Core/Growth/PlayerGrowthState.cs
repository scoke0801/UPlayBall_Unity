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
        private int[] _peakBonuses;
        private int[] _developmentProgress;
        private readonly List<GrowthResultRecord> _growthHistory;
        private readonly List<InjuryRecord> _injuryHistory;
        private List<string> _legacyTraitIds;

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
            _peakBonuses = new int[PlayerAbilityCatalog.AbilityCount];
            _developmentProgress = new int[PlayerAbilityCatalog.AbilityCount];
            for (int index = 0; index < _trainingAffinities.Length; index++)
                _trainingAffinities[index] = TrainingFitGrade.Normal;
            MigratePotentialOverflowToPeakWithoutAbilityLoss();
            _growthHistory = new List<GrowthResultRecord>();
            _injuryHistory = new List<InjuryRecord>();
            _legacyTraitIds = new List<string>();
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
        public IReadOnlyList<string> LegacyTraitIds
        {
            get
            {
                _legacyTraitIds ??= new List<string>();
                return _legacyTraitIds;
            }
        }
        public double SeasonInjuryRiskReduction { get; private set; }
        public int PhysicalDeclineProtectionPoints { get; private set; }

        public int GetPeakBonus(PlayerAbility ability)
        {
            EnsureVersion14State();
            ValidateAbility(ability);
            return _peakBonuses[(int)ability];
        }

        public int GetDevelopmentProgress(PlayerAbility ability)
        {
            EnsureVersion14State();
            ValidateAbility(ability);
            return _developmentProgress[(int)ability];
        }

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
            int maximum = PotentialByAbility.Get(ability);
            return BaseAbilities.AddClamped(ability, delta, AbilityRatings.Minimum, maximum);
        }

        /// <summary>영구 Base와 분리된 시즌형 Peak를 능력치별 0~3 범위에서 변경한다.</summary>
        public int ApplyPeakBonusChange(PlayerAbility ability, int delta)
        {
            EnsureVersion14State();
            ValidateAbility(ability);
            int index = (int)ability;
            int before = _peakBonuses[index];
            _peakBonuses[index] = Clamp(before + delta, 0, 3);
            return _peakBonuses[index] - before;
        }

        /// <summary>1,000 단위 고정소수점 진행도를 영구 성장으로 전환하고 나머지를 이월한다.</summary>
        public int AddDevelopmentProgress(PlayerAbility ability, int progress)
        {
            EnsureVersion14State();
            ValidateAbility(ability);
            if (progress < 0)
                throw new ArgumentOutOfRangeException(nameof(progress));

            int index = (int)ability;
            if (BaseAbilities.Get(ability) >= PotentialByAbility.Get(ability))
            {
                _developmentProgress[index] = 0;
                return 0;
            }

            _developmentProgress[index] += progress;
            int requestedGain = _developmentProgress[index] / 1_000;
            if (requestedGain <= 0)
                return 0;

            int applied = ApplyBaseAbilityChange(ability, requestedGain);
            _developmentProgress[index] -= applied * 1_000;
            if (BaseAbilities.Get(ability) >= PotentialByAbility.Get(ability))
                _developmentProgress[index] = 0;
            return applied;
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
            DecayPeakBonuses(Age >= 35 ? 2 : 1);
            Age++;
        }

        public void DecayPeakBonuses(int amount)
        {
            EnsureVersion14State();
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            for (int index = 0; index < _peakBonuses.Length; index++)
                _peakBonuses[index] = Math.Max(0, _peakBonuses[index] - amount);
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

        public void ApplyOffseasonRecoveryBenefits(
            double injuryRiskReduction,
            int physicalDeclineProtectionPoints)
        {
            SeasonInjuryRiskReduction = Math.Max(0d, Math.Min(0.50d, injuryRiskReduction));
            PhysicalDeclineProtectionPoints = Math.Max(0, physicalDeclineProtectionPoints);
        }

        public int ConsumePhysicalDeclineProtection(int requested)
        {
            int applied = Math.Min(Math.Max(0, requested), PhysicalDeclineProtectionPoints);
            PhysicalDeclineProtectionPoints -= applied;
            return applied;
        }

        public bool UnlockLegacyTrait(string traitId)
        {
            if (string.IsNullOrWhiteSpace(traitId))
                throw new ArgumentException("Legacy Trait ID는 비어 있을 수 없습니다.", nameof(traitId));
            _legacyTraitIds ??= new List<string>();
            for (int index = 0; index < _legacyTraitIds.Count; index++)
                if (string.Equals(_legacyTraitIds[index], traitId, StringComparison.Ordinal)) return false;
            _legacyTraitIds.Add(traitId);
            _legacyTraitIds.Sort(StringComparer.Ordinal);
            return true;
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

        private static void ValidateAbility(PlayerAbility ability)
        {
            if (ability < 0 || ability >= PlayerAbility.Count)
                throw new ArgumentOutOfRangeException(nameof(ability));
        }

        private void MigratePotentialOverflowToPeakWithoutAbilityLoss()
        {
            EnsureVersion14State();
            for (int index = 0; index < PlayerAbilityCatalog.AbilityCount; index++)
            {
                var ability = (PlayerAbility)index;
                int current = BaseAbilities.Get(ability);
                int potential = PotentialByAbility.Get(ability);
                if (current <= potential)
                    continue;

                int requiredPotential = Math.Max(potential, current - 3);
                if (requiredPotential > potential)
                    PotentialByAbility.AddClamped(ability, requiredPotential - potential);
                potential = PotentialByAbility.Get(ability);
                _peakBonuses[index] = current - potential;
                BaseAbilities.AddClamped(ability, potential - current);
            }
        }

        /// <summary>v13의 Potential 초과분을 능력 손실 없이 Peak로 옮기고 신규 진행도 저장소를 복원한다.</summary>
        public void MigrateVersion14State()
        {
            EnsureVersion14State();
            MigratePotentialOverflowToPeakWithoutAbilityLoss();
        }

        private void EnsureVersion14State()
        {
            _peakBonuses ??= new int[PlayerAbilityCatalog.AbilityCount];
            _developmentProgress ??= new int[PlayerAbilityCatalog.AbilityCount];
            _legacyTraitIds ??= new List<string>();
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum) return minimum;
            return value > maximum ? maximum : value;
        }
    }
}
