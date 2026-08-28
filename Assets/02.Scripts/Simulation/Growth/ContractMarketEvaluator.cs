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
            return input.RecentPerformance * _balance.RecentPerformance +
                   input.StableAbility * _balance.StableAbility +
                   input.AgeAndGrowthOutlook * _balance.AgeAndOutlook +
                   input.Durability * _balance.Durability +
                   input.PositionScarcity * _balance.PositionScarcity +
                   input.TeamNeed * _balance.TeamNeed;
        }
    }
}
