using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Simulation.Random;

namespace Baseball.Simulation.Growth
{
    /// <summary>
    /// 훈련 프로그램의 기대 성장값을 결정론적 영구 능력치 변화로 변환한다.
    /// </summary>
    public sealed class GrowthResolver
    {
        private readonly GrowthBalanceTable _balance;

        public GrowthResolver(GrowthBalanceTable balance)
        {
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
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
            IRandomSource random)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (program == null) throw new ArgumentNullException(nameof(program));
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (!program.CanUse(player.PlayerType))
                throw new InvalidOperationException("선수 유형에 맞지 않는 프로그램입니다.");
            if (player.Condition < program.MinimumCondition)
                throw new InvalidOperationException("현재 컨디션으로 시작할 수 없는 프로그램입니다.");

            var abilityChanges = new List<AbilityChange>(program.TargetAbilityWeights.Length);
            var potentialChanges = new List<AbilityChange>(1);
            int totalGain = ResolveAbilityGrowth(
                player,
                program,
                priorSelections,
                trainingFit,
                random,
                abilityChanges);

            if (totalGain < program.MinimumGuaranteedGain)
            {
                totalGain += ApplyMinimumGuarantee(
                    player,
                    program,
                    program.MinimumGuaranteedGain - totalGain,
                    abilityChanges);
            }

            ResolvePotentialBreakthrough(player, program, random, potentialChanges);

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
                injuryResult);
            player.RecordGrowth(record);
            return record;
        }

        private int ResolveAbilityGrowth(
            PlayerGrowthState player,
            TrainingProgramDefinition program,
            int priorSelections,
            TrainingFitGrade trainingFit,
            IRandomSource random,
            List<AbilityChange> changes)
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
            if (random.NextDouble() >= _balance.PotentialBreakthroughProbability)
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
}
