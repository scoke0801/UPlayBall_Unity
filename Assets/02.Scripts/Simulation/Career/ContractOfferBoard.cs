using System;
using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Core.Teams;

namespace Baseball.Simulation.Career
{
    /// <summary>
    /// 구단별 오퍼 후보를 점수 순으로 정렬하고 시장 종류별 오퍼 수 규칙을 적용한다.
    /// </summary>
    /// <remarks>
    /// 새 게임은 진입 실패를 막기 위해 최소 오퍼 수를 보장하지만, 계약 만료 뒤 공개 시장은
    /// 실제 평가 기준을 넘은 구단만 남긴다. 두 경로를 섞으면 기존 구단이 언제나 재계약하거나
    /// 외부 정식 오퍼가 반드시 생겨 커리어 위기가 사라지므로 선정 정책을 명시적으로 분리한다.
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
        /// 현재 구단을 제외하고 평가 기준을 넘은 외부 구단만 공개 시장 오퍼로 선정한다.
        /// </summary>
        public static ContractOffer[] SelectOpenMarketOffers(
            ContractOfferBalance balance,
            ContractOfferEvaluator evaluator,
            Player player,
            GeneratedTeam[] teams,
            int currentTeamId,
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
                if (teams[index].TeamId == currentTeamId)
                    continue;

                ContractOffer candidate = evaluator.CreateCandidate(
                        teams[index],
                        player,
                        contractEvaluationBonus)
                    .WithChannel(ContractOfferChannel.OpenMarket);
                if (candidate.OfferScore < balance.OfferScoreThreshold)
                    continue;
                candidates[eligibleCount++] = candidate;
            }

            Array.Sort(candidates, 0, eligibleCount, Comparer.Instance);
            int offerCount = Math.Min(eligibleCount, balance.MaximumOfferCount);
            var offers = new ContractOffer[offerCount];
            Array.Copy(candidates, offers, offerCount);
            return offers;
        }

        /// <summary>
        /// 정식 오퍼가 없을 때 커리어가 막히지 않도록 가장 관심이 높은 외부 구단의 육성 계약을 만든다.
        /// </summary>
        public static ContractOffer SelectDevelopmentFallback(
            ContractOfferEvaluator evaluator,
            Player player,
            GeneratedTeam[] teams,
            int currentTeamId,
            int contractEvaluationBonus = 0)
        {
            ContractOffer? best = null;
            for (int index = 0; index < teams.Length; index++)
            {
                if (teams[index].TeamId == currentTeamId)
                    continue;
                ContractOffer candidate = evaluator.CreateCandidate(
                    teams[index],
                    player,
                    contractEvaluationBonus);
                if (!best.HasValue || CompareOffers(candidate, best.Value) < 0)
                    best = candidate;
            }

            if (!best.HasValue)
                throw new InvalidOperationException("육성 계약을 제시할 외부 구단이 없습니다.");

            ContractOffer source = best.Value;
            return new ContractOffer(
                source.Team,
                signingBonus: 0L,
                annualSalary: Math.Max(1L, source.AnnualSalary / 2L),
                ExpectedRole.BenchCompetition,
                source.OfferScore,
                contractYears: 1,
                ContractOfferChannel.DevelopmentFallback,
                estimatedPlayingTime: Math.Min(source.EstimatedPlayingTime, 0.20d),
                hasTradeProtection: false);
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

        private sealed class Comparer : System.Collections.Generic.IComparer<ContractOffer>
        {
            public static readonly Comparer Instance = new Comparer();

            public int Compare(ContractOffer left, ContractOffer right) => CompareOffers(left, right);
        }
    }
}
