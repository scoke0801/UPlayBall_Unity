using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Growth;

namespace Baseball.Simulation.Growth
{
    public readonly struct AiOffseasonPlanItem
    {
        public AiOffseasonPlanItem(string programId, int startWeek, int endWeek)
        {
            ProgramId = programId;
            StartWeek = startWeek;
            EndWeek = endWeek;
        }

        public string ProgramId { get; }
        public int StartWeek { get; }
        public int EndWeek { get; }
    }

    /// <summary>
    /// AI 선수도 같은 기간·비용·성장 공식을 사용하도록 결정론적 오프시즌 계획을 만든다.
    /// </summary>
    public sealed class AiOffseasonPlanner
    {
        private readonly GrowthBalanceTable _balance;

        public AiOffseasonPlanner(GrowthBalanceTable balance)
        {
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
        }

        public AiOffseasonPlanItem[] Plan(PlayerGrowthState player, long developmentBudget, bool requiresRehabilitation)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (developmentBudget < 0L) throw new ArgumentOutOfRangeException(nameof(developmentBudget));
            var result = new List<AiOffseasonPlanItem>();
            int nextWeek = 1;
            long remainingMoney = developmentBudget;
            bool studyUsed = false;
            var selectedPartners = new List<string>();
            var categoryCounts = new int[(int)TrainingCategory.Count];

            if (requiresRehabilitation)
            {
                TrainingProgramDefinition rehabilitation = _balance.FindProgram("rehab_general");
                Add(rehabilitation, result, ref nextWeek, ref remainingMoney);
                categoryCounts[(int)TrainingCategory.Rehabilitation]++;
            }

            while (nextWeek <= _balance.OffseasonWeeks)
            {
                TrainingProgramDefinition best = FindBestProgram(
                    player,
                    remainingMoney,
                    _balance.OffseasonWeeks - nextWeek + 1,
                    studyUsed,
                    selectedPartners,
                    categoryCounts);
                if (best == null)
                    break;
                Add(best, result, ref nextWeek, ref remainingMoney);
                if (best.IsStudy)
                    studyUsed = true;
                if (best.ActivityType == OffseasonActivityType.TrainingPartner)
                    selectedPartners.Add(best.PartnerId);
                categoryCounts[(int)best.Category]++;
            }

            return result.ToArray();
        }

        private TrainingProgramDefinition FindBestProgram(
            PlayerGrowthState player,
            long money,
            int remainingWeeks,
            bool studyUsed,
            List<string> selectedPartners,
            int[] categoryCounts)
        {
            TrainingProgramDefinition best = null;
            double bestScore = 0d;
            for (int index = 0; index < _balance.Programs.Length; index++)
            {
                TrainingProgramDefinition candidate = _balance.Programs[index];
                if (candidate.ActivityType is OffseasonActivityType.Rest or OffseasonActivityType.Rehabilitation)
                    continue;
                if (!candidate.CanUse(player.PlayerType) || candidate.MoneyCost > money ||
                    candidate.DurationWeeks > remainingWeeks || (candidate.IsStudy && studyUsed))
                    continue;
                if (candidate.ActivityType == OffseasonActivityType.TrainingPartner &&
                    Contains(selectedPartners, candidate.PartnerId))
                    continue;

                double potentialFactor = 0d;
                for (int target = 0; target < candidate.TargetAbilityWeights.Length; target++)
                {
                    AbilityWeight weight = candidate.TargetAbilityWeights[target];
                    potentialFactor += weight.Weight * _balance.PotentialGap.GetMultiplier(
                        player.BaseAbilities.Get(weight.Ability),
                        player.PotentialByAbility.Get(weight.Ability));
                }
                double repetition = _balance.Repetition.GetMultiplier(
                    categoryCounts[(int)candidate.Category],
                    candidate.IsStudy);
                double score = candidate.ProgramPower * potentialFactor * repetition /
                               Math.Max(1, candidate.DurationWeeks);
                if (score > bestScore)
                {
                    best = candidate;
                    bestScore = score;
                }
            }
            return best;
        }

        private static bool Contains(List<string> values, string target)
        {
            for (int index = 0; index < values.Count; index++)
            {
                if (string.Equals(values[index], target, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static void Add(
            TrainingProgramDefinition program,
            List<AiOffseasonPlanItem> result,
            ref int nextWeek,
            ref long remainingMoney)
        {
            if (program == null)
                return;
            int endWeek = nextWeek + program.DurationWeeks - 1;
            result.Add(new AiOffseasonPlanItem(program.ProgramId, nextWeek, endWeek));
            nextWeek = endWeek + 1;
            remainingMoney -= program.MoneyCost;
        }
    }
}
