using System;
using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Random;

namespace Baseball.Simulation.Career
{
    /// <summary>
    /// PlayerValue × PositionNeed × TeamBudget × TeamPreference × ScoutVariance 공식으로
    /// 구단이 새 게임 선수에게 오퍼를 낼지와 조건을 계산한다.
    /// </summary>
    public sealed class ContractOfferEvaluator
    {
        private readonly ContractOfferBalance _balance;
        private readonly PlayerValueEvaluator _playerValueEvaluator;
        private readonly IRandomSource _random;

        /// <summary>
        /// 밸런스 계수와 결정론적 RNG를 주입받아 평가기를 구성한다.
        /// </summary>
        public ContractOfferEvaluator(ContractOfferBalance balance, IRandomSource random)
            : this(balance, PlayerEvaluationBalance.CreateDefault(), random)
        {
        }

        /// <summary>
        /// 계약 계수와 포지션별 선수 가치 계수를 함께 주입받아 평가기를 구성한다.
        /// </summary>
        public ContractOfferEvaluator(
            ContractOfferBalance balance,
            PlayerEvaluationBalance playerEvaluationBalance,
            IRandomSource random)
        {
            _balance = balance;
            _playerValueEvaluator = new PlayerValueEvaluator(playerEvaluationBalance);
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        /// <summary>
        /// 한 구단이 오퍼를 내면 그 내용을, 오퍼를 내지 않으면 null을 반환한다.
        /// </summary>
        public ContractOffer? Evaluate(GeneratedTeam team, int overallValue, PlayerPosition position)
        {
            ContractOffer candidate = CreateCandidate(team, overallValue, position, preferenceFactor: 1d);
            return candidate.OfferScore >= _balance.OfferScoreThreshold ? candidate : null;
        }

        /// <summary>
        /// 포지션 적합 배분과 구단 성향까지 반영해 오퍼 여부를 평가한다.
        /// </summary>
        public ContractOffer? Evaluate(GeneratedTeam team, Player player)
        {
            ContractOffer candidate = CreateCandidate(team, player);
            return candidate.OfferScore >= _balance.OfferScoreThreshold ? candidate : null;
        }

        /// <summary>
        /// 최소 오퍼 수 보정에서 사용할 수 있도록 기준점 미달을 포함한 평가 결과를 만든다.
        /// </summary>
        public ContractOffer CreateCandidate(GeneratedTeam team, Player player)
        {
            if (player == null)
                throw new ArgumentNullException(nameof(player));

            int playerValue = _playerValueEvaluator.CalculatePositionValue(player);
            double buildPreference = _playerValueEvaluator.CalculateTeamPreferenceFactor(
                player,
                team.Archetype.Archetype);
            double preferenceFactor = IsPreferredPosition(team.Archetype.Archetype, player.PrimaryPosition)
                ? 1d + _balance.PreferredPositionBonus
                : 1d;
            return CreateCandidate(
                team,
                playerValue,
                player.PrimaryPosition,
                preferenceFactor * buildPreference);
        }

        private ContractOffer CreateCandidate(
            GeneratedTeam team,
            int overallValue,
            PlayerPosition position,
            double preferenceFactor)
        {
            double playerValueFactor = overallValue / _balance.RatingBaseline;
            double positionNeedFactor = team.GetPositionNeed(position) / _balance.RatingBaseline;
            double budgetFactor = team.Archetype.Budget / _balance.RatingBaseline;
            double scoutVarianceRange = _balance.ScoutVarianceMaximum - _balance.ScoutVarianceMinimum;
            double scoutVariance = _balance.ScoutVarianceMinimum + _random.NextDouble() * scoutVarianceRange;

            double score = playerValueFactor * positionNeedFactor * budgetFactor * preferenceFactor * scoutVariance;
            long signingBonus = (long)(_balance.BaseSigningBonus * score);
            long annualSalary = (long)(_balance.BaseSalary * score);
            ExpectedRole expectedRole = ResolveExpectedRole(team.GetPositionNeed(position));

            return new ContractOffer(
                team,
                signingBonus,
                annualSalary,
                expectedRole,
                score,
                _balance.ContractYears);
        }

        private ExpectedRole ResolveExpectedRole(int positionNeed)
        {
            if (positionNeed >= _balance.StartingCompetitionNeed)
                return ExpectedRole.StartingCompetition;
            if (positionNeed >= _balance.RosterCompetitionNeed)
                return ExpectedRole.RosterCompetition;
            return ExpectedRole.BenchCompetition;
        }

        private static bool IsPreferredPosition(TeamArchetype archetype, PlayerPosition position)
        {
            bool isPitchingPosition = position is PlayerPosition.StartingPitcher or PlayerPosition.ReliefPitcher;
            return archetype switch
            {
                TeamArchetype.OffenseFocused => !isPitchingPosition,
                TeamArchetype.PitchingFocused => isPitchingPosition,
                _ => false
            };
        }
    }
}
