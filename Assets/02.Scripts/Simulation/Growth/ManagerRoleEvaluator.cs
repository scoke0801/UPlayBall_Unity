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
            return new ManagerRoleEvaluationResult(
                playerScore,
                strongestCompetitor,
                role,
                BuildExplanation(player, style, strongestCompetitor, margin));
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
                   input.GrowthOutlook * growthWeight +
                   input.IncumbentBonus;
        }

        private OpportunityRole ResolveRole(bool isPitcher, double margin)
        {
            if (margin >= _balance.StarterMargin)
                return isPitcher ? OpportunityRole.StartingRotation : OpportunityRole.Starter;
            if (margin >= _balance.CompetitionMargin)
                return isPitcher ? OpportunityRole.HighLeverageRelief : OpportunityRole.Platoon;
            if (margin > _balance.BackupMargin)
                return isPitcher ? OpportunityRole.LowLeverageRelief : OpportunityRole.Backup;
            return OpportunityRole.MinorLeague;
        }

        private DecisionExplanation BuildExplanation(
            ManagerRoleEvaluationInput input,
            ManagerDevelopmentStyle style,
            double strongestCompetitor,
            double margin)
        {
            double[] weights = GetAdjustedWeights(style);
            var factors = new[]
            {
                CreateFactor(DecisionReasonCode.CurrentAbility, input.CurrentAbility, weights[0], 1),
                CreateFactor(DecisionReasonCode.PositionFit, input.RoleFit, weights[4], 2),
                CreateFactor(DecisionReasonCode.RecentPerformance, input.LastSeasonPerformance, weights[1], 3),
                CreateFactor(DecisionReasonCode.Condition, input.Condition, weights[2], 4),
                CreateFactor(DecisionReasonCode.ManagerTrust, input.ManagerTrust, weights[3], 5),
                CreateFactor(DecisionReasonCode.GrowthOutlook, input.GrowthOutlook, weights[5], 6),
                new DecisionFactor(
                    DecisionReasonCode.IncumbentBonus,
                    input.IncumbentBonus,
                    input.IncumbentBonus,
                    1d,
                    input.IncumbentBonus,
                    input.IncumbentBonus > 0d ? DecisionDirection.Positive : DecisionDirection.Neutral,
                    7),
                new DecisionFactor(
                    DecisionReasonCode.CompetitorScore,
                    strongestCompetitor,
                    strongestCompetitor,
                    1d,
                    -strongestCompetitor,
                    DecisionDirection.Negative,
                    8)
            };
            var actions = margin >= _balance.StarterMargin
                ? Array.Empty<RecommendedActionCode>()
                : input.Condition < 60d
                    ? new[] { RecommendedActionCode.RestoreCondition, RecommendedActionCode.ImproveCoreAbility }
                    : new[] { RecommendedActionCode.ImproveCoreAbility, RecommendedActionCode.ImprovePositionFit };
            DecisionReasonCode summary = margin >= _balance.StarterMargin
                ? DecisionReasonCode.CurrentAbility
                : strongestCompetitor > input.CurrentAbility
                    ? DecisionReasonCode.CompetitorScore
                    : DecisionReasonCode.PositionFit;
            return new DecisionExplanation(
                DecisionType.ManagerRole,
                summary,
                factors,
                new[] { _balance.StarterMargin, _balance.CompetitionMargin, _balance.BackupMargin },
                actions,
                rulesVersion: 1);
        }

        private double[] GetAdjustedWeights(ManagerDevelopmentStyle style)
        {
            double current = _balance.CurrentAbility;
            double performance = _balance.LastSeasonPerformance;
            double condition = _balance.Condition;
            double trust = _balance.ManagerTrust;
            double fit = _balance.RoleFit;
            double growth = _balance.GrowthOutlook;
            switch (style)
            {
                case ManagerDevelopmentStyle.VeteranPreference:
                    performance += 0.05d; trust += 0.03d; growth -= 0.05d; current -= 0.03d; break;
                case ManagerDevelopmentStyle.Development:
                    growth += 0.08d; current -= 0.05d; performance -= 0.03d; break;
                case ManagerDevelopmentStyle.DataDriven:
                    performance += 0.04d; condition += 0.03d; trust -= 0.04d; growth -= 0.03d; break;
                case ManagerDevelopmentStyle.DefenseFirst:
                    fit += 0.07d; performance -= 0.04d; current -= 0.03d; break;
            }
            return new[] { current, performance, condition, trust, fit, growth };
        }

        private static DecisionFactor CreateFactor(
            DecisionReasonCode code,
            double value,
            double weight,
            int priority)
        {
            double contribution = value * weight;
            return new DecisionFactor(
                code,
                value,
                value,
                weight,
                contribution,
                contribution > 0d ? DecisionDirection.Positive : DecisionDirection.Neutral,
                priority);
        }
    }
}
