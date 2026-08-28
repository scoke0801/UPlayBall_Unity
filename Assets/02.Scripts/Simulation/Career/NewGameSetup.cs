using System;
using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Random;

namespace Baseball.Simulation.Career
{
    /// <summary>
    /// 캐릭터 생성이 끝난 선수를 입력으로 받아 Rookie League 구단을 생성하고
    /// 계약 오퍼 목록을 계산하는 새 게임 진입점이다.
    /// </summary>
    public sealed class NewGameSetup
    {
        private readonly TeamGenerator _teamGenerator;
        private readonly ContractOfferEvaluator _offerEvaluator;
        private readonly ContractOfferBalance _offerBalance;

        /// <summary>
        /// 하나의 결정론적 RNG로 구단 생성과 오퍼 평가를 함께 구성한다.
        /// 같은 RNG를 공유해도 두 단계는 고정된 순서로만 소비하므로 결정론이 깨지지 않는다.
        /// </summary>
        public NewGameSetup(ContractOfferBalance offerBalance, IRandomSource random)
            : this(
                offerBalance,
                TeamGenerationBalance.CreateDefault(),
                PlayerEvaluationBalance.CreateDefault(),
                random)
        {
        }

        /// <summary>
        /// 새 게임 관련 밸런스 묶음과 하나의 RNG로 전체 생성을 구성한다.
        /// </summary>
        public NewGameSetup(
            ContractOfferBalance offerBalance,
            TeamGenerationBalance teamGenerationBalance,
            PlayerEvaluationBalance playerEvaluationBalance,
            IRandomSource random)
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random));

            _offerBalance = offerBalance;
            _teamGenerator = new TeamGenerator(teamGenerationBalance, random);
            _offerEvaluator = new ContractOfferEvaluator(offerBalance, playerEvaluationBalance, random);
        }

        /// <summary>
        /// 구단 목록을 생성하고, 선수 포지션 기준으로 오퍼를 낸 구단만 골라 반환한다.
        /// </summary>
        public NewGameSetupResult GenerateLeagueAndOffers(
            Player player,
            int teamCount,
            TeamArchetypeProfile[] archetypePool,
            string[] namePool)
        {
            if (player == null)
                throw new ArgumentNullException(nameof(player));

            GeneratedTeam[] teams = _teamGenerator.GenerateLeague(teamCount, archetypePool, namePool);
            return SelectOffers(player, teams);
        }

        /// <summary>
        /// 대표색과 기존 포지션 경쟁자까지 생성하고 오퍼 수를 문서 범위로 제한한다.
        /// </summary>
        public NewGameSetupResult GenerateLeagueAndOffers(
            Player player,
            int teamCount,
            TeamArchetypeProfile[] archetypePool,
            TeamIdentityDefinition[] identityPool,
            string[] playerNamePool)
        {
            if (player == null)
                throw new ArgumentNullException(nameof(player));

            GeneratedTeam[] teams = _teamGenerator.GenerateLeague(
                teamCount,
                archetypePool,
                identityPool,
                playerNamePool);
            return SelectOffers(player, teams);
        }

        private NewGameSetupResult SelectOffers(Player player, GeneratedTeam[] teams)
        {
            ContractOffer[] offers = ContractOfferBoard.SelectOffers(
                _offerBalance,
                _offerEvaluator,
                player,
                teams);
            return new NewGameSetupResult(teams, offers);
        }
    }

    /// <summary>
    /// 새 게임에서 생성된 전체 구단 목록과, 그중 실제로 오퍼를 낸 구단 목록을 함께 보관한다.
    /// </summary>
    public sealed class NewGameSetupResult
    {
        public NewGameSetupResult(GeneratedTeam[] teams, ContractOffer[] offers)
        {
            Teams = teams;
            Offers = offers;
        }

        public GeneratedTeam[] Teams { get; }
        public ContractOffer[] Offers { get; }
    }
}
