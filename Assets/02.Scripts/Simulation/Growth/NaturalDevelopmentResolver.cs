using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Simulation.Random;

namespace Baseball.Simulation.Growth
{
    /// <summary>
    /// 한 시즌의 활용 역할을 소폭의 자연 성장 예산으로 변환한다.
    /// </summary>
    public sealed class NaturalDevelopmentResolver
    {
        private readonly GrowthBalanceTable _balance;

        public NaturalDevelopmentResolver(GrowthBalanceTable balance)
        {
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
        }

        public GrowthResultRecord Resolve(
            PlayerGrowthState player,
            SeasonUsageSummary usage,
            int seasonYear,
            ulong randomSeed,
            IRandomSource random)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (usage == null) throw new ArgumentNullException(nameof(usage));
            if (random == null) throw new ArgumentNullException(nameof(random));

            double qualityRoll = _balance.MinimumQualityRoll +
                                 (_balance.MaximumQualityRoll - _balance.MinimumQualityRoll) * random.NextDouble();
            double budget = _balance.NaturalGrowth.GetAgeBudget(player.Age) *
                            _balance.NaturalGrowth.GetUsageMultiplier(usage.UsageRatio) *
                            _balance.WorkEthic.GetMultiplier(player.WorkEthic) *
                            qualityRoll;
            var changes = new List<AbilityChange>(2);

            for (int index = 0; index < usage.DevelopmentWeights.Length; index++)
            {
                AbilityWeight target = usage.DevelopmentWeights[index];
                int current = player.BaseAbilities.Get(target.Ability);
                int potential = player.PotentialByAbility.Get(target.Ability);
                double expected = budget * target.Weight *
                                  _balance.PotentialGap.GetMultiplier(current, potential);
                int gain = StochasticRound(expected, random);
                int applied = player.ApplyBaseAbilityChange(target.Ability, gain);
                if (applied > 0)
                    changes.Add(new AbilityChange(target.Ability, applied));
            }

            var record = new GrowthResultRecord(
                player.PlayerId,
                seasonYear,
                GrowthSourceType.NaturalDevelopment,
                "season_usage",
                new GrowthInputSnapshot(player.Age, player.Condition, player.WorkEthic, TrainingFitGrade.Normal, 0),
                randomSeed,
                changes.ToArray(),
                Array.Empty<AbilityChange>(),
                0,
                0L,
                0);
            player.RecordGrowth(record);
            return record;
        }

        private static int StochasticRound(double expectedValue, IRandomSource random)
        {
            if (expectedValue <= 0d)
                return 0;
            int guaranteed = (int)Math.Floor(expectedValue);
            return guaranteed + (random.NextDouble() < expectedValue - guaranteed ? 1 : 0);
        }
    }
}
