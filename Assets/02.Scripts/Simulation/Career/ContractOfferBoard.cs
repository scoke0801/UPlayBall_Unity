using System;
using Baseball.Core.Balance;
using Baseball.Core.Players;

namespace Baseball.Simulation.Career
{
    /// <summary>
    /// 구단별 오퍼 후보를 점수 순으로 정렬하고 최소·최대 오퍼 수 규칙을 적용해
    /// 플레이어에게 실제로 제시할 계약 오퍼 목록을 만든다.
    /// </summary>
    /// <remarks>
    /// 새 게임 입단과 계약 만료 후 재계약이 완전히 같은 규칙을 쓰도록 선정 규칙을 한곳에 모았다.
    /// MinimumOfferCount 덕분에 기준점을 넘는 구단이 하나도 없어도 항상 선택지가 제시되므로,
    /// 선수가 무소속으로 남는 경우를 별도 규칙으로 막을 필요가 없다.
    /// </remarks>
    public static class ContractOfferBoard
    {
        /// <summary>
        /// teams를 배열 순서대로 평가하므로, 같은 RNG를 주입하면 항상 같은 목록이 나온다.
        /// </summary>
        public static ContractOffer[] SelectOffers(
            ContractOfferBalance balance,
            ContractOfferEvaluator evaluator,
            Player player,
            GeneratedTeam[] teams,
            int contractEvaluationBonus = 0)
        {
            if (evaluator == null)
                throw new ArgumentNullException(nameof(evaluator));
            if (player == null)
                throw new ArgumentNullException(nameof(player));
            if (teams == null || teams.Length == 0)
                throw new ArgumentException("오퍼를 평가할 구단이 필요합니다.", nameof(teams));

            var candidates = new ContractOffer[teams.Length];
            int eligibleCount = 0;
            for (int index = 0; index < teams.Length; index++)
            {
                ContractOffer candidate = evaluator.CreateCandidate(
                    teams[index],
                    player,
                    contractEvaluationBonus);
                candidates[index] = candidate;
                if (candidate.OfferScore >= balance.OfferScoreThreshold)
                    eligibleCount++;
            }

            Array.Sort(candidates, CompareOffers);
            int offerCount = eligibleCount;
            if (offerCount < balance.MinimumOfferCount)
                offerCount = balance.MinimumOfferCount;
            if (offerCount > balance.MaximumOfferCount)
                offerCount = balance.MaximumOfferCount;
            if (offerCount > candidates.Length)
                offerCount = candidates.Length;

            var offers = new ContractOffer[offerCount];
            Array.Copy(candidates, offers, offerCount);
            return offers;
        }

        /// <summary>
        /// 점수 내림차순, 동점이면 TeamId 오름차순으로 정렬해 제시 순서를 결정론적으로 고정한다.
        /// </summary>
        private static int CompareOffers(ContractOffer left, ContractOffer right)
        {
            int scoreComparison = right.OfferScore.CompareTo(left.OfferScore);
            return scoreComparison != 0
                ? scoreComparison
                : left.Team.TeamId.CompareTo(right.Team.TeamId);
        }
    }
}
