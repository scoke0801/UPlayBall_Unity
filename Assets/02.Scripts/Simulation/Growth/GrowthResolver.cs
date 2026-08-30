using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Rules;
using Baseball.Simulation.Random;

namespace Baseball.Simulation.Growth
{
    /// <summary>
    /// 훈련 프로그램의 기대 성장값을 결정론적 영구 능력치 변화로 변환한다.
    /// </summary>
    public sealed class GrowthResolver
    {
        private readonly GrowthBalanceTable _balance;
        private readonly SimulationVersionStamp _versionStamp;

        public GrowthResolver(GrowthBalanceTable balance, SimulationVersionStamp? versionStamp = null)
        {
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            _versionStamp = versionStamp ?? SimulationVersionStamp.CreateCurrent(balanceVersion: 0);
        }

        /// <summary>
        /// 주입된 RNG만 사용해 한 활동의 성장·컨디션·경미한 부상 결과를 확정한다.
        /// </summary>
        public GrowthResultRecord Resolve(
            PlayerGrowthState player,
            TrainingProgramDefinition program,
            int seasonYear,
            int priorSelections,
            TrainingFitGrade trainingFit,
            ulong randomSeed,
            IRandomSource random,
            PlayerAbility? focusAbility = null)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (program == null) throw new ArgumentNullException(nameof(program));
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (!program.CanUse(player.PlayerType))
                throw new InvalidOperationException("선수 유형에 맞지 않는 프로그램입니다.");
            if (player.Condition < program.MinimumCondition)
                throw new InvalidOperationException("현재 컨디션으로 시작할 수 없는 프로그램입니다.");

            DecisionExplanation explanation = BuildExplanation(
                player,
                program,
                priorSelections,
                trainingFit);
            var abilityChanges = new List<AbilityChange>(program.TargetAbilityWeights.Length);
            var potentialChanges = new List<AbilityChange>(1);
            var peakChanges = new List<AbilityChange>(1);
            // 고가 프로그램의 개발 한계 돌파가 같은 활동의 성장에도 영향을 주어야 한다.
            // 성장 계산 뒤 Potential을 올리면 큰돈을 쓰고도 그해 결과가 전부 +0인 공백이 생긴다.
            ResolvePotentialBreakthrough(player, program, random, potentialChanges);
            int totalGain = ResolveAbilityGrowth(
                player,
                program,
                priorSelections,
                trainingFit,
                random,
                abilityChanges,
                peakChanges);

            if (totalGain < program.MinimumGuaranteedGain)
            {
                totalGain += ApplyMinimumGuarantee(
                    player,
                    program,
                    program.MinimumGuaranteedGain - totalGain,
                    abilityChanges);
            }
            if (focusAbility.HasValue && totalGain >= 4)
            {
                ApplyFocusRedistribution(
                    player,
                    focusAbility.Value,
                    totalGain / 4,
                    abilityChanges);
            }

            GrowthInjuryResult injuryResult = GrowthInjuryResult.None;
            int conditionChange = program.ConditionChange;
            if (program.InjuryRisk > 0d && random.NextDouble() < program.InjuryRisk)
            {
                injuryResult = GrowthInjuryResult.Discomfort;
                conditionChange -= _balance.TrainingInjuryConditionPenalty;
            }
            int appliedConditionChange = player.ChangeCondition(conditionChange);

            var snapshot = new GrowthInputSnapshot(
                player.Age,
                player.Condition - appliedConditionChange,
                player.WorkEthic,
                trainingFit,
                priorSelections);
            var record = new GrowthResultRecord(
                player.PlayerId,
                seasonYear,
                GetSourceType(program.ActivityType),
                program.ProgramId,
                snapshot,
                randomSeed,
                abilityChanges.ToArray(),
                potentialChanges.ToArray(),
                appliedConditionChange,
                program.MoneyCost,
                program.DurationWeeks,
                injuryResult,
                program.Intensity,
                peakChanges.ToArray(),
                _versionStamp,
                explanation);
            player.RecordGrowth(record);
            return record;
        }

        private DecisionExplanation BuildExplanation(
            PlayerGrowthState player,
            TrainingProgramDefinition program,
            int priorSelections,
            TrainingFitGrade trainingFit)
        {
            double age = _balance.AgeGrowth.GetMultiplier(player.Age);
            double workEthic = _balance.WorkEthic.GetMultiplier(player.WorkEthic);
            double fit = _balance.TrainingFit.GetMultiplier(trainingFit);
            double condition = _balance.Condition.GetMultiplier(player.Condition);
            double repetition = _balance.Repetition.GetMultiplier(priorSelections, program.IsStudy);
            double potential = CalculatePotentialGapMultiplier(player, program);
            DecisionReasonCode summary = DecisionReasonCode.PotentialGap;
            double lowest = potential;
            if (repetition < lowest)
            {
                lowest = repetition;
                summary = DecisionReasonCode.RepetitionPenalty;
            }
            if (condition < lowest)
            {
                lowest = condition;
                summary = DecisionReasonCode.Condition;
            }
            if (age < lowest)
                summary = DecisionReasonCode.AgeCurve;

            var actions = new List<RecommendedActionCode>(2);
            if (priorSelections > 0)
                actions.Add(RecommendedActionCode.ReduceTrainingRepetition);
            if (player.Condition < _balance.Condition.NormalMinimum)
                actions.Add(RecommendedActionCode.RestoreCondition);
            return new DecisionExplanation(
                DecisionType.Growth,
                summary,
                new[]
                {
                    CreateMultiplierFactor(DecisionReasonCode.AgeCurve, player.Age, age, 1),
                    CreateMultiplierFactor(DecisionReasonCode.PotentialGap, potential, potential, 2),
                    CreateMultiplierFactor(DecisionReasonCode.WorkEthic, (int)player.WorkEthic, workEthic, 3),
                    CreateMultiplierFactor(DecisionReasonCode.TrainingFit, (int)trainingFit, fit, 4),
                    CreateMultiplierFactor(DecisionReasonCode.RepetitionPenalty, priorSelections, repetition, 5),
                    CreateMultiplierFactor(DecisionReasonCode.Condition, player.Condition, condition, 6)
                },
                new[] { 1d },
                actions.ToArray(),
                _versionStamp.RulesVersion);
        }

        private double CalculatePotentialGapMultiplier(
            PlayerGrowthState player,
            TrainingProgramDefinition program)
        {
            if (program.TargetAbilityWeights.Length == 0)
                return 1d;
            double result = 0d;
            for (int index = 0; index < program.TargetAbilityWeights.Length; index++)
            {
                AbilityWeight target = program.TargetAbilityWeights[index];
                result += _balance.PotentialGap.GetMultiplier(
                    player.BaseAbilities.Get(target.Ability),
                    player.PotentialByAbility.Get(target.Ability)) * target.Weight;
            }
            return result;
        }

        private static DecisionFactor CreateMultiplierFactor(
            DecisionReasonCode reasonCode,
            double rawValue,
            double multiplier,
            int priority)
        {
            return new DecisionFactor(
                reasonCode,
                rawValue,
                multiplier,
                1d,
                multiplier - 1d,
                multiplier >= 1d ? DecisionDirection.Positive : DecisionDirection.Negative,
                priority);
        }

        private static void ApplyFocusRedistribution(
            PlayerGrowthState player,
            PlayerAbility focusAbility,
            int requested,
            List<AbilityChange> changes)
        {
            if (requested <= 0 ||
                player.BaseAbilities.Get(focusAbility) >= player.PotentialByAbility.Get(focusAbility))
            {
                return;
            }

            for (int point = 0; point < requested; point++)
            {
                int donorIndex = -1;
                for (int index = changes.Count - 1; index >= 0; index--)
                {
                    if (changes[index].Ability != focusAbility && changes[index].Amount > 0)
                    {
                        donorIndex = index;
                        break;
                    }
                }
                if (donorIndex < 0)
                    return;

                AbilityChange donor = changes[donorIndex];
                int removed = player.ApplyBaseAbilityChange(donor.Ability, -1);
                if (removed >= 0)
                    return;
                int applied = player.ApplyBaseAbilityChange(focusAbility, 1);
                if (applied <= 0)
                {
                    player.ApplyBaseAbilityChange(donor.Ability, 1);
                    return;
                }
                if (donor.Amount == 1)
                    changes.RemoveAt(donorIndex);
                else
                    changes[donorIndex] = new AbilityChange(donor.Ability, donor.Amount - 1);
                AddOrAccumulate(changes, focusAbility, applied);
            }
        }

        private int ResolveAbilityGrowth(
            PlayerGrowthState player,
            TrainingProgramDefinition program,
            int priorSelections,
            TrainingFitGrade trainingFit,
            IRandomSource random,
            List<AbilityChange> changes,
            List<AbilityChange> peakChanges)
        {
            if (program.ProgramPower <= 0d || program.MaxTotalGain == 0)
                return 0;

            double qualityRoll = _balance.MinimumQualityRoll +
                                 (_balance.MaximumQualityRoll - _balance.MinimumQualityRoll) * random.NextDouble();
            double commonMultiplier = _balance.AgeGrowth.GetMultiplier(player.Age) *
                                      _balance.WorkEthic.GetMultiplier(player.WorkEthic) *
                                      _balance.TrainingFit.GetMultiplier(trainingFit) *
                                      _balance.Condition.GetMultiplier(player.Condition) *
                                      _balance.Repetition.GetMultiplier(priorSelections, program.IsStudy) *
                                      qualityRoll;
            int totalGain = 0;

            for (int index = 0; index < program.TargetAbilityWeights.Length; index++)
            {
                if (totalGain >= program.MaxTotalGain)
                    break;

                AbilityWeight target = program.TargetAbilityWeights[index];
                int baseAbility = player.BaseAbilities.Get(target.Ability);
                int potential = player.PotentialByAbility.Get(target.Ability);
                double expectedGrowth = program.ProgramPower * target.Weight * commonMultiplier *
                                        _balance.PotentialGap.GetMultiplier(baseAbility, potential);
                if (baseAbility >= potential &&
                    program.MinimumAccessTier >= TrainingAccessTier.Elite &&
                    program.ProgramPower >= 1.4d &&
                    qualityRoll >= 1.05d)
                {
                    int peakApplied = player.ApplyPeakBonusChange(target.Ability, 1);
                    if (peakApplied > 0)
                        peakChanges.Add(new AbilityChange(target.Ability, peakApplied));
                }
                int rolledGain = StochasticRound(expectedGrowth, random);
                rolledGain = Math.Min(rolledGain, program.MaxGainPerAbility);
                rolledGain = Math.Min(rolledGain, program.MaxTotalGain - totalGain);
                int applied = player.ApplyBaseAbilityChange(target.Ability, rolledGain);
                if (applied <= 0)
                    continue;
                changes.Add(new AbilityChange(target.Ability, applied));
                totalGain += applied;
            }
            return totalGain;
        }

        private static int ApplyMinimumGuarantee(
            PlayerGrowthState player,
            TrainingProgramDefinition program,
            int requested,
            List<AbilityChange> changes)
        {
            int appliedTotal = 0;
            for (int pass = 0; pass < requested; pass++)
            {
                bool appliedThisPass = false;
                for (int index = 0; index < program.TargetAbilityWeights.Length; index++)
                {
                    PlayerAbility ability = program.TargetAbilityWeights[index].Ability;
                    int applied = player.ApplyBaseAbilityChange(ability, 1);
                    if (applied <= 0)
                        continue;
                    AddOrAccumulate(changes, ability, applied);
                    appliedTotal += applied;
                    appliedThisPass = true;
                    break;
                }
                if (!appliedThisPass)
                    break;
            }
            return appliedTotal;
        }

        private void ResolvePotentialBreakthrough(
            PlayerGrowthState player,
            TrainingProgramDefinition program,
            IRandomSource random,
            List<AbilityChange> changes)
        {
            if (!program.CanRaisePotential || program.TargetAbilityWeights.Length == 0)
                return;

            if (IsAtDevelopmentLimit(player, program))
            {
                ApplyGuaranteedPotentialBreakthroughs(
                    player,
                    program,
                    program.MinimumPotentialBreakthroughsWhenCapped,
                    changes);
            }

            double probability = Math.Min(
                1d,
                _balance.PotentialBreakthroughProbability *
                program.PotentialBreakthroughChanceMultiplier);
            if (probability <= 0d || random.NextDouble() >= probability)
                return;

            double selection = random.NextDouble();
            double cumulative = 0d;
            for (int index = 0; index < program.TargetAbilityWeights.Length; index++)
            {
                AbilityWeight target = program.TargetAbilityWeights[index];
                cumulative += target.Weight;
                if (selection > cumulative && index < program.TargetAbilityWeights.Length - 1)
                    continue;
                int applied = player.ApplyPotentialChange(target.Ability, 1);
                if (applied > 0)
                    changes.Add(new AbilityChange(target.Ability, applied));
                return;
            }
        }

        private static bool IsAtDevelopmentLimit(
            PlayerGrowthState player,
            TrainingProgramDefinition program)
        {
            bool hasBreakthroughTarget = false;
            for (int index = 0; index < program.TargetAbilityWeights.Length; index++)
            {
                PlayerAbility ability = program.TargetAbilityWeights[index].Ability;
                int current = player.BaseAbilities.Get(ability);
                int potential = player.PotentialByAbility.Get(ability);
                if (current < potential)
                    return false;
                if (current < AbilityRatings.Maximum && potential < AbilityRatings.Maximum)
                    hasBreakthroughTarget = true;
            }
            return hasBreakthroughTarget;
        }

        private static void ApplyGuaranteedPotentialBreakthroughs(
            PlayerGrowthState player,
            TrainingProgramDefinition program,
            int requested,
            List<AbilityChange> changes)
        {
            for (int pass = 0; pass < requested; pass++)
            {
                for (int index = 0; index < program.TargetAbilityWeights.Length; index++)
                {
                    PlayerAbility ability = program.TargetAbilityWeights[index].Ability;
                    if (player.BaseAbilities.Get(ability) >= AbilityRatings.Maximum)
                        continue;
                    int applied = player.ApplyPotentialChange(ability, 1);
                    if (applied <= 0)
                        continue;
                    AddOrAccumulate(changes, ability, applied);
                    break;
                }
            }
        }

        private static void AddOrAccumulate(List<AbilityChange> changes, PlayerAbility ability, int amount)
        {
            for (int index = 0; index < changes.Count; index++)
            {
                if (changes[index].Ability != ability)
                    continue;
                changes[index] = new AbilityChange(ability, changes[index].Amount + amount);
                return;
            }
            changes.Add(new AbilityChange(ability, amount));
        }

        private static int StochasticRound(double expectedValue, IRandomSource random)
        {
            if (expectedValue <= 0d)
                return 0;
            int guaranteed = (int)Math.Floor(expectedValue);
            double remainder = expectedValue - guaranteed;
            return guaranteed + (random.NextDouble() < remainder ? 1 : 0);
        }

        private static GrowthSourceType GetSourceType(OffseasonActivityType activityType)
        {
            return activityType switch
            {
                OffseasonActivityType.TrainingPartner => GrowthSourceType.TrainingPartner,
                OffseasonActivityType.Study => GrowthSourceType.Study,
                OffseasonActivityType.Rehabilitation => GrowthSourceType.Injury,
                _ => GrowthSourceType.PersonalTraining
            };
        }
    }

    /// <summary>
    /// 확정 전에 표시할 한 능력치의 보수적 최소·최대 성장 범위다.
    /// </summary>
    public readonly struct AbilityGrowthRange
    {
        public AbilityGrowthRange(PlayerAbility ability, int currentValue, int minimumGain, int maximumGain)
        {
            Ability = ability;
            CurrentValue = currentValue;
            MinimumGain = minimumGain;
            MaximumGain = maximumGain;
        }

        public PlayerAbility Ability { get; }
        public int CurrentValue { get; }
        public int MinimumGain { get; }
        public int MaximumGain { get; }
        public int MinimumValue => CurrentValue + MinimumGain;
        public int MaximumValue => CurrentValue + MaximumGain;
    }

    /// <summary>
    /// 현재 선수 상태에서 프로그램을 실행했을 때 UI가 설명할 기간·비용·성장·부담의 스냅샷이다.
    /// </summary>
    public readonly struct GrowthProgramPreview
    {
        public GrowthProgramPreview(
            TrainingProgramDefinition program,
            AbilityGrowthRange[] abilityRanges,
            int conditionBefore,
            int conditionAfter,
            int conditionAfterWithDiscomfort,
            int priorSelections,
            double repetitionMultiplier)
        {
            Program = program;
            AbilityRanges = abilityRanges ?? Array.Empty<AbilityGrowthRange>();
            ConditionBefore = conditionBefore;
            ConditionAfter = conditionAfter;
            ConditionAfterWithDiscomfort = conditionAfterWithDiscomfort;
            PriorSelections = priorSelections;
            RepetitionMultiplier = repetitionMultiplier;
        }

        public TrainingProgramDefinition Program { get; }
        public AbilityGrowthRange[] AbilityRanges { get; }
        public int ConditionBefore { get; }
        public int ConditionAfter { get; }
        public int ConditionAfterWithDiscomfort { get; }
        public int PriorSelections { get; }
        public double RepetitionMultiplier { get; }
    }

    /// <summary>
    /// 실제 성장 공식의 최솟값·최댓값으로 확정 전 예상 범위를 계산한다.
    /// </summary>
    public sealed class GrowthPreviewCalculator
    {
        private readonly GrowthBalanceTable _balance;

        public GrowthPreviewCalculator(GrowthBalanceTable balance)
        {
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
        }

        public GrowthProgramPreview Build(
            PlayerGrowthState player,
            TrainingProgramDefinition baseProgram,
            TrainingIntensity intensity,
            int priorSelections,
            TrainingFitGrade trainingFit)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            return Build(
                player,
                baseProgram,
                intensity,
                priorSelections,
                trainingFit,
                player.Condition);
        }

        /// <summary>
        /// 앞선 계획 활동의 예상 컨디션을 반영해 다음 활동의 성장 범위를 계산한다.
        /// </summary>
        public GrowthProgramPreview Build(
            PlayerGrowthState player,
            TrainingProgramDefinition baseProgram,
            TrainingIntensity intensity,
            int priorSelections,
            TrainingFitGrade trainingFit,
            int conditionBefore)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (baseProgram == null) throw new ArgumentNullException(nameof(baseProgram));
            if (priorSelections < 0) throw new ArgumentOutOfRangeException(nameof(priorSelections));
            if (conditionBefore < 0 || conditionBefore > 100)
                throw new ArgumentOutOfRangeException(nameof(conditionBefore));

            TrainingProgramDefinition program = _balance.TrainingIntensities.Apply(
                baseProgram,
                intensity);
            AbilityGrowthRange[] ranges = BuildAbilityRanges(
                player,
                program,
                priorSelections,
                trainingFit,
                conditionBefore);
            int conditionAfter = Clamp(conditionBefore + program.ConditionChange, 0, 100);
            int conditionAfterWithDiscomfort = program.InjuryRisk > 0d
                ? Clamp(
                    conditionBefore + program.ConditionChange -
                    _balance.TrainingInjuryConditionPenalty,
                    0,
                    100)
                : conditionAfter;
            return new GrowthProgramPreview(
                program,
                ranges,
                conditionBefore,
                conditionAfter,
                conditionAfterWithDiscomfort,
                priorSelections,
                _balance.Repetition.GetMultiplier(priorSelections, program.IsStudy));
        }

        private AbilityGrowthRange[] BuildAbilityRanges(
            PlayerGrowthState player,
            TrainingProgramDefinition program,
            int priorSelections,
            TrainingFitGrade trainingFit,
            int condition)
        {
            int count = program.TargetAbilityWeights.Length;
            var minimumGains = new int[count];
            var maximumGains = new int[count];
            var capacities = new int[count];
            int[] guaranteedPotentialGains = BuildGuaranteedPotentialGains(player, program);
            double commonMultiplier = _balance.AgeGrowth.GetMultiplier(player.Age) *
                                      _balance.WorkEthic.GetMultiplier(player.WorkEthic) *
                                      _balance.TrainingFit.GetMultiplier(trainingFit) *
                                      _balance.Condition.GetMultiplier(condition) *
                                      _balance.Repetition.GetMultiplier(priorSelections, program.IsStudy);
            int minimumTotal = 0;

            for (int index = 0; index < count; index++)
            {
                AbilityWeight target = program.TargetAbilityWeights[index];
                int current = player.BaseAbilities.Get(target.Ability);
                int potential = Math.Min(
                    AbilityRatings.Maximum,
                    player.PotentialByAbility.Get(target.Ability) +
                    guaranteedPotentialGains[index]);
                int capacity = Math.Max(0, potential - current);
                double potentialMultiplier = _balance.PotentialGap.GetMultiplier(
                    current,
                    potential);
                double expected = program.ProgramPower * target.Weight *
                                  commonMultiplier * potentialMultiplier;

                int minimum = (int)Math.Floor(expected * _balance.MinimumQualityRoll);
                int maximum = (int)Math.Ceiling(expected * _balance.MaximumQualityRoll);
                minimum = Math.Min(minimum, program.MaxGainPerAbility);
                maximum = Math.Min(maximum, program.MaxGainPerAbility);
                minimum = Math.Min(minimum, capacity);
                maximum = Math.Min(maximum, capacity);
                minimum = Math.Min(minimum, Math.Max(0, program.MaxTotalGain - minimumTotal));
                // 능력치별 최대치는 서로 동시에 달성된다는 뜻이 아니라, 해당 능력치가 얻을 수 있는
                // 개별 범위다. 앞 능력치가 낮게 굴러가면 뒤 능력치가 MaxTotalGain 여유를 쓸 수 있다.
                maximum = Math.Min(maximum, program.MaxTotalGain);
                minimumGains[index] = Math.Max(0, minimum);
                maximumGains[index] = Math.Max(minimumGains[index], maximum);
                capacities[index] = capacity;
                minimumTotal += minimumGains[index];
            }

            ApplyMinimumGuarantee(
                program,
                capacities,
                minimumGains,
                maximumGains,
                minimumTotal);

            var result = new AbilityGrowthRange[count];
            for (int index = 0; index < count; index++)
            {
                PlayerAbility ability = program.TargetAbilityWeights[index].Ability;
                result[index] = new AbilityGrowthRange(
                    ability,
                    player.BaseAbilities.Get(ability),
                    minimumGains[index],
                    maximumGains[index]);
            }
            return result;
        }

        private static int[] BuildGuaranteedPotentialGains(
            PlayerGrowthState player,
            TrainingProgramDefinition program)
        {
            int count = program.TargetAbilityWeights.Length;
            var result = new int[count];
            if (program.MinimumPotentialBreakthroughsWhenCapped <= 0)
                return result;

            bool hasBreakthroughTarget = false;
            for (int index = 0; index < count; index++)
            {
                PlayerAbility ability = program.TargetAbilityWeights[index].Ability;
                int current = player.BaseAbilities.Get(ability);
                int potential = player.PotentialByAbility.Get(ability);
                if (current < potential)
                    return result;
                if (current < AbilityRatings.Maximum && potential < AbilityRatings.Maximum)
                    hasBreakthroughTarget = true;
            }
            if (!hasBreakthroughTarget)
                return result;

            for (int pass = 0; pass < program.MinimumPotentialBreakthroughsWhenCapped; pass++)
            {
                for (int index = 0; index < count; index++)
                {
                    PlayerAbility ability = program.TargetAbilityWeights[index].Ability;
                    int current = player.BaseAbilities.Get(ability);
                    int potential = player.PotentialByAbility.Get(ability) + result[index];
                    if (current >= AbilityRatings.Maximum || potential >= AbilityRatings.Maximum)
                        continue;
                    result[index]++;
                    break;
                }
            }
            return result;
        }

        private static void ApplyMinimumGuarantee(
            TrainingProgramDefinition program,
            int[] capacities,
            int[] minimumGains,
            int[] maximumGains,
            int currentMinimumTotal)
        {
            int required = Math.Min(
                program.MinimumGuaranteedGain,
                program.MaxTotalGain) - currentMinimumTotal;
            for (int pass = 0; pass < required; pass++)
            {
                for (int index = 0; index < minimumGains.Length; index++)
                {
                    if (minimumGains[index] >= capacities[index])
                        continue;
                    minimumGains[index]++;
                    if (maximumGains[index] < minimumGains[index])
                        maximumGains[index] = minimumGains[index];
                    break;
                }
            }
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum) return minimum;
            return value > maximum ? maximum : value;
        }
    }
}
