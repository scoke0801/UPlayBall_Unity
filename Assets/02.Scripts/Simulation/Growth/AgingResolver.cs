using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Rules;
using Baseball.Simulation.Random;

namespace Baseball.Simulation.Growth
{
    /// <summary>
    /// Physical·Technical·Mental 계통별 시즌 노쇠 예산을 영구 감소로 적용한다.
    /// </summary>
    public sealed class AgingResolver
    {
        private readonly GrowthBalanceTable _balance;
        private readonly SimulationVersionStamp _versionStamp;

        public AgingResolver(GrowthBalanceTable balance, SimulationVersionStamp? versionStamp = null)
        {
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            _versionStamp = versionStamp ?? SimulationVersionStamp.CreateCurrent(balanceVersion: 0);
        }

        public GrowthResultRecord Resolve(
            PlayerGrowthState player,
            int seasonYear,
            ulong randomSeed,
            IRandomSource random)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (random == null) throw new ArgumentNullException(nameof(random));

            var totals = new int[PlayerAbilityCatalog.AbilityCount];
            ApplyFamilyDecline(player, AbilityFamily.Physical, random, totals);
            ApplyFamilyDecline(player, AbilityFamily.Technical, random, totals);
            ApplyFamilyDecline(player, AbilityFamily.Mental, random, totals);

            var changes = new List<AbilityChange>();
            for (int index = 0; index < totals.Length; index++)
            {
                if (totals[index] != 0)
                    changes.Add(new AbilityChange((PlayerAbility)index, totals[index]));
            }

            var record = new GrowthResultRecord(
                player.PlayerId,
                seasonYear,
                GrowthSourceType.Aging,
                "season_aging",
                new GrowthInputSnapshot(player.Age, player.Condition, player.WorkEthic, TrainingFitGrade.Normal, 0),
                randomSeed,
                changes.ToArray(),
                Array.Empty<AbilityChange>(),
                0,
                0L,
                0,
                versionStamp: _versionStamp);
            player.RecordGrowth(record);
            return record;
        }

        private void ApplyFamilyDecline(
            PlayerGrowthState player,
            AbilityFamily family,
            IRandomSource random,
            int[] totals)
        {
            double budget = _balance.AgingDecline.GetBudget(player.Age, family);
            int declinePoints = (int)Math.Floor(budget);
            if (random.NextDouble() < budget - declinePoints)
                declinePoints++;
            if (family == AbilityFamily.Physical && declinePoints > 0)
                declinePoints -= player.ConsumePhysicalDeclineProtection(declinePoints);
            if (declinePoints == 0)
                return;

            var candidates = new PlayerAbility[PlayerAbilityCatalog.AbilityCount];
            int candidateCount = 0;
            for (int index = 0; index < PlayerAbilityCatalog.AbilityCount; index++)
            {
                PlayerAbility ability = (PlayerAbility)index;
                bool appliesToPlayer = player.PlayerType == Core.Players.PlayerType.Batter
                    ? PlayerAbilityCatalog.IsBatterAbility(ability)
                    : PlayerAbilityCatalog.IsPitcherAbility(ability);
                if (appliesToPlayer && PlayerAbilityCatalog.GetFamily(ability) == family &&
                    player.BaseAbilities.Get(ability) > AbilityRatings.Minimum)
                {
                    candidates[candidateCount++] = ability;
                }
            }

            for (int point = 0; point < declinePoints && candidateCount > 0; point++)
            {
                int selected = Math.Min((int)(random.NextDouble() * candidateCount), candidateCount - 1);
                PlayerAbility ability = candidates[selected];
                int applied = player.ApplyBaseAbilityChange(ability, -1);
                totals[(int)ability] += applied;
                if (player.BaseAbilities.Get(ability) == AbilityRatings.Minimum)
                {
                    candidates[selected] = candidates[candidateCount - 1];
                    candidateCount--;
                }
            }
        }
    }
}
