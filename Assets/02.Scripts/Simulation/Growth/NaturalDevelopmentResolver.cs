using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Rules;
using Baseball.Simulation.Random;

namespace Baseball.Simulation.Growth
{
    /// <summary>
    /// 한 시즌의 활용 역할을 소폭의 자연 성장 예산으로 변환한다.
    /// </summary>
    public sealed class NaturalDevelopmentResolver
    {
        private readonly GrowthBalanceTable _balance;
        private readonly SimulationVersionStamp _versionStamp;

        public NaturalDevelopmentResolver(
            GrowthBalanceTable balance,
            SimulationVersionStamp? versionStamp = null)
        {
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            _versionStamp = versionStamp ?? SimulationVersionStamp.CreateCurrent(balanceVersion: 0);
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
                if (usage.IsCatchUpTarget(target.Ability))
                    expected *= usage.GetCatchUpMultiplier(player.Age);
                int progress = (int)Math.Round(expected * 1_000d, MidpointRounding.AwayFromZero);
                int applied = player.AddDevelopmentProgress(target.Ability, Math.Max(0, progress));
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
                0,
                versionStamp: _versionStamp,
                explanation: BuildExplanation(player, usage));
            player.RecordGrowth(record);
            return record;
        }

        private DecisionExplanation BuildExplanation(PlayerGrowthState player, SeasonUsageSummary usage)
        {
            double exposure = _balance.NaturalGrowth.GetUsageMultiplier(usage.UsageRatio);
            double catchUp = usage.GetCatchUpMultiplier(player.Age);
            return new DecisionExplanation(
                DecisionType.Growth,
                usage.UsageRatio < 0.35d
                    ? DecisionReasonCode.UsageExposure
                    : DecisionReasonCode.PotentialGap,
                new[]
                {
                    new DecisionFactor(DecisionReasonCode.AgeCurve, player.Age,
                        _balance.NaturalGrowth.GetAgeBudget(player.Age), 1d,
                        _balance.NaturalGrowth.GetAgeBudget(player.Age), DecisionDirection.Positive, 1),
                    new DecisionFactor(DecisionReasonCode.UsageExposure, usage.UsageRatio,
                        exposure, 1d, exposure, DecisionDirection.Positive, 2),
                    new DecisionFactor(DecisionReasonCode.CatchUpSupport, usage.CompetitorGap,
                        catchUp, 1d, catchUp,
                        catchUp > 1d ? DecisionDirection.Positive : DecisionDirection.Neutral, 3),
                    new DecisionFactor(DecisionReasonCode.WorkEthic, (int)player.WorkEthic,
                        _balance.WorkEthic.GetMultiplier(player.WorkEthic), 1d,
                        _balance.WorkEthic.GetMultiplier(player.WorkEthic), DecisionDirection.Positive, 4)
                },
                Array.Empty<double>(),
                usage.UsageRatio < 0.35d
                    ? new[] { RecommendedActionCode.EarnPlayingTime }
                    : Array.Empty<RecommendedActionCode>(),
                rulesVersion: 1);
        }

    }
}
