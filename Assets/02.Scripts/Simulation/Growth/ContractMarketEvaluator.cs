using System;
using Baseball.Core.Balance;
using Baseball.Core.Growth;

namespace Baseball.Simulation.Growth
{
    /// <summary>
    /// 현재 장착 꼼수보다 최근 성과와 안정 전력을 함께 반영해 0~100 시장 가치를 계산한다.
    /// </summary>
    public sealed class ContractMarketEvaluator
    {
        private readonly ContractMarketBalanceTable _balance;

        public ContractMarketEvaluator(ContractMarketBalanceTable balance)
        {
            _balance = balance;
        }

        public double Evaluate(ContractMarketInput input)
        {
            return EvaluateDetailed(input).Score;
        }

        public ContractMarketEvaluationResult EvaluateDetailed(ContractMarketInput input)
        {
            var factors = new[]
            {
                CreateFactor(DecisionReasonCode.RecentPerformance, input.RecentPerformance, _balance.RecentPerformance, 1),
                CreateFactor(DecisionReasonCode.StableAbility, input.StableAbility, _balance.StableAbility, 2),
                CreateFactor(DecisionReasonCode.GrowthOutlook, input.AgeAndGrowthOutlook, _balance.AgeAndOutlook, 3),
                CreateFactor(DecisionReasonCode.Durability, input.Durability, _balance.Durability, 4),
                CreateFactor(DecisionReasonCode.PositionScarcity, input.PositionScarcity, _balance.PositionScarcity, 5),
                CreateFactor(DecisionReasonCode.TeamNeed, input.TeamNeed, _balance.TeamNeed, 6)
            };
            double score = 0d;
            int strongest = 0;
            for (int index = 0; index < factors.Length; index++)
            {
                score += factors[index].Contribution;
                if (factors[index].Contribution > factors[strongest].Contribution)
                    strongest = index;
            }
            return new ContractMarketEvaluationResult(
                score,
                new DecisionExplanation(
                    DecisionType.Contract,
                    factors[strongest].ReasonCode,
                    factors,
                    Array.Empty<double>(),
                    new[] { RecommendedActionCode.ImproveCoreAbility },
                    rulesVersion: 1));
        }

        private static DecisionFactor CreateFactor(
            DecisionReasonCode code,
            double value,
            double weight,
            int priority)
        {
            return new DecisionFactor(
                code,
                value,
                value,
                weight,
                value * weight,
                DecisionDirection.Positive,
                priority);
        }
    }
}
