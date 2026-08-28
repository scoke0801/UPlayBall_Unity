using System;
using Baseball.Core.Balance;
using Baseball.Core.Growth;

namespace Baseball.Simulation.Growth
{
    /// <summary>
    /// 현재 전력과 실제 경쟁자 차이로 스프링캠프 역할을 결정한다.
    /// </summary>
    public sealed class ManagerRoleEvaluator
    {
        private readonly ManagerEvaluationWeightTable _balance;

        public ManagerRoleEvaluator(ManagerEvaluationWeightTable balance)
        {
            _balance = balance;
        }

        public ManagerRoleEvaluationResult Evaluate(
            ManagerRoleEvaluationInput player,
            ManagerRoleEvaluationInput[] competitors,
            ManagerDevelopmentStyle style)
        {
            if (competitors == null)
                throw new ArgumentNullException(nameof(competitors));
            double playerScore = CalculateScore(player, style);
            double strongestCompetitor = 0d;
            for (int index = 0; index < competitors.Length; index++)
                strongestCompetitor = Math.Max(strongestCompetitor, CalculateScore(competitors[index], style));
            double margin = playerScore - strongestCompetitor;
            OpportunityRole role = ResolveRole(player.IsPitcher, margin);
            return new ManagerRoleEvaluationResult(playerScore, strongestCompetitor, role);
        }

        public double CalculateScore(ManagerRoleEvaluationInput input, ManagerDevelopmentStyle style)
        {
            double currentWeight = _balance.CurrentAbility;
            double performanceWeight = _balance.LastSeasonPerformance;
            double conditionWeight = _balance.Condition;
            double trustWeight = _balance.ManagerTrust;
            double fitWeight = _balance.RoleFit;
            double growthWeight = _balance.GrowthOutlook;

            switch (style)
            {
                case ManagerDevelopmentStyle.VeteranPreference:
                    performanceWeight += 0.05d;
                    trustWeight += 0.03d;
                    growthWeight -= 0.05d;
                    currentWeight -= 0.03d;
                    break;
                case ManagerDevelopmentStyle.Development:
                    growthWeight += 0.08d;
                    currentWeight -= 0.05d;
                    performanceWeight -= 0.03d;
                    break;
                case ManagerDevelopmentStyle.DataDriven:
                    performanceWeight += 0.04d;
                    conditionWeight += 0.03d;
                    trustWeight -= 0.04d;
                    growthWeight -= 0.03d;
                    break;
                case ManagerDevelopmentStyle.DefenseFirst:
                    fitWeight += 0.07d;
                    performanceWeight -= 0.04d;
                    currentWeight -= 0.03d;
                    break;
            }

            return input.CurrentAbility * currentWeight +
                   input.LastSeasonPerformance * performanceWeight +
                   input.Condition * conditionWeight +
                   input.ManagerTrust * trustWeight +
                   input.RoleFit * fitWeight +
                   input.GrowthOutlook * growthWeight;
        }

        private OpportunityRole ResolveRole(bool isPitcher, double margin)
        {
            if (margin >= _balance.StarterMargin)
                return isPitcher ? OpportunityRole.StartingRotation : OpportunityRole.Starter;
            if (margin >= _balance.CompetitionMargin)
                return isPitcher ? OpportunityRole.HighLeverageRelief : OpportunityRole.Platoon;
            if (margin >= _balance.BackupMargin)
                return isPitcher ? OpportunityRole.LowLeverageRelief : OpportunityRole.Backup;
            return OpportunityRole.MinorLeague;
        }
    }
}
