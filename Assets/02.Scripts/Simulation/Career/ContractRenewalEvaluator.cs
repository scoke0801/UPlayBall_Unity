using System;
using Baseball.Core.Balance;
using Baseball.Core.Teams;

namespace Baseball.Simulation.Career
{
    /// <summary>
    /// 기존 구단이 선수를 다음 시즌 계획에 포함할지 설명 가능한 입력으로 전달한다.
    /// </summary>
    public readonly struct ContractRenewalEvaluationInput
    {
        public ContractRenewalEvaluationInput(
            GeneratedTeam team,
            double playerMarketValue,
            double currentRoleValue,
            double recentPerformance,
            double ageAndPotential,
            double costEfficiency,
            double managerRelationship,
            int strongestCompetitorOverall)
        {
            Team = team ?? throw new ArgumentNullException(nameof(team));
            PlayerMarketValue = ValidateRating(playerMarketValue, nameof(playerMarketValue));
            CurrentRoleValue = ValidateRating(currentRoleValue, nameof(currentRoleValue));
            RecentPerformance = ValidateRating(recentPerformance, nameof(recentPerformance));
            AgeAndPotential = ValidateRating(ageAndPotential, nameof(ageAndPotential));
            CostEfficiency = ValidateRating(costEfficiency, nameof(costEfficiency));
            ManagerRelationship = ValidateRating(managerRelationship, nameof(managerRelationship));
            StrongestCompetitorOverall = strongestCompetitorOverall;
        }

        public GeneratedTeam Team { get; }
        public double PlayerMarketValue { get; }
        public double CurrentRoleValue { get; }
        public double RecentPerformance { get; }
        public double AgeAndPotential { get; }
        public double CostEfficiency { get; }
        public double ManagerRelationship { get; }
        public int StrongestCompetitorOverall { get; }

        private static double ValidateRating(double value, string parameterName)
        {
            if (value < 0d || value > 100d)
                throw new ArgumentOutOfRangeException(parameterName);
            return value;
        }
    }

    /// <summary>
    /// 시장 가치·포지션 필요·팀 내 역할·최근 성과로 기존 구단 재계약 여부와 조건을 평가한다.
    /// </summary>
    public sealed class ContractRenewalEvaluator
    {
        private readonly ContractRenewalBalance _renewalBalance;
        private readonly ContractOfferBalance _offerBalance;

        public ContractRenewalEvaluator(
            ContractRenewalBalance renewalBalance,
            ContractOfferBalance offerBalance)
        {
            _renewalBalance = renewalBalance;
            _offerBalance = offerBalance;
        }

        /// <summary>
        /// 기준 미달이면 오퍼 없음, 기준 이상이면 구단 의향에 맞는 기간과 약속 역할을 반환한다.
        /// </summary>
        public ContractOffer? Evaluate(
            ContractRenewalEvaluationInput input,
            Baseball.Core.Players.PlayerPosition position,
            ContractOfferChannel channel)
        {
            double score = CalculateInterestScore(input, position);
            if (score < _renewalBalance.MinimumInterestScore)
                return null;

            ExpectedRole promisedRole = ResolvePromisedRole(score, input.CurrentRoleValue);
            int contractYears = ResolveContractYears(score, channel);
            double salaryFactor = Math.Max(0.55d, score / _renewalBalance.NormalOfferScore);
            if (channel == ContractOfferChannel.CurrentTeamExtension)
                salaryFactor *= 0.92d;
            long annualSalary = Math.Max(1L, (long)Math.Round(_offerBalance.BaseSalary * salaryFactor));
            long signingBonus = channel == ContractOfferChannel.CurrentTeamExtension
                ? (long)Math.Round(_offerBalance.BaseSigningBonus * score / 200d)
                : (long)Math.Round(_offerBalance.BaseSigningBonus * score / 100d);
            double estimatedPlayingTime = EstimatePlayingTime(
                promisedRole,
                input.Team.GetPositionNeed(position));

            return new ContractOffer(
                input.Team,
                signingBonus,
                annualSalary,
                promisedRole,
                score / 100d,
                contractYears,
                channel,
                estimatedPlayingTime,
                hasTradeProtection: false);
        }

        /// <summary>
        /// 재계약 의향의 각 항목을 100점 기준으로 합산하고 로스터·예산 위험을 감점한다.
        /// </summary>
        public double CalculateInterestScore(
            ContractRenewalEvaluationInput input,
            Baseball.Core.Players.PlayerPosition position)
        {
            double score = input.PlayerMarketValue * 0.25d +
                           input.Team.GetPositionNeed(position) * 0.20d +
                           input.CurrentRoleValue * 0.15d +
                           input.RecentPerformance * 0.15d +
                           input.AgeAndPotential * 0.10d +
                           input.CostEfficiency * 0.10d +
                           input.ManagerRelationship * 0.05d;
            double competitorPenalty = Math.Max(
                0d,
                input.StrongestCompetitorOverall - input.PlayerMarketValue) * 0.40d;
            double budgetPenalty = Math.Max(0d, 50d - input.Team.Archetype.Budget) * 0.15d;
            return Clamp(score - competitorPenalty - budgetPenalty, 0d, 100d);
        }

        private ExpectedRole ResolvePromisedRole(double score, double currentRoleValue)
        {
            if (score >= _renewalBalance.CoreOfferScore)
                return ExpectedRole.StartingCompetition;
            if (score >= _renewalBalance.NormalOfferScore || currentRoleValue >= 65d)
                return ExpectedRole.RosterCompetition;
            return ExpectedRole.BenchCompetition;
        }

        private int ResolveContractYears(double score, ContractOfferChannel channel)
        {
            if (channel == ContractOfferChannel.CurrentTeamExtension)
                return score >= _renewalBalance.CoreOfferScore ? 2 : 1;
            if (score >= _renewalBalance.CoreOfferScore)
                return 3;
            return score >= _renewalBalance.NormalOfferScore ? 2 : 1;
        }

        private static double EstimatePlayingTime(ExpectedRole role, int positionNeed)
        {
            double baseline = role switch
            {
                ExpectedRole.StartingCompetition => 0.68d,
                ExpectedRole.RosterCompetition => 0.44d,
                _ => 0.20d
            };
            return Clamp(baseline + (positionNeed - 50d) / 250d, 0.10d, 0.85d);
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            if (value < minimum) return minimum;
            if (value > maximum) return maximum;
            return value;
        }
    }
}
