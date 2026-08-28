using System;
using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Random;

namespace Baseball.Simulation.Career
{
    /// <summary>
    /// 계약 역할·경쟁자·컨디션·감독 평가를 바탕으로 한 경기의 선수 기용을 결정한다.
    /// </summary>
    public sealed class ManagerUsageAi
    {
        private readonly CareerSeasonBalance _balance;
        private readonly PlayerValueEvaluator _playerValueEvaluator;

        public ManagerUsageAi(CareerSeasonBalance balance, PlayerEvaluationBalance playerEvaluationBalance)
        {
            _balance = balance;
            _playerValueEvaluator = new PlayerValueEvaluator(playerEvaluationBalance);
        }

        /// <summary>
        /// 같은 입력과 RNG Seed에서 항상 같은 실제 경기 역할을 반환한다.
        /// </summary>
        public PlayerGameRole DecideRole(
            Player player,
            ExpectedRole expectedRole,
            int strongestCompetitorOverall,
            int condition,
            int managerEvaluation,
            int teamGameNumber,
            IRandomSource random)
        {
            return DecideRole(
                player,
                expectedRole,
                strongestCompetitorOverall,
                condition,
                managerEvaluation,
                teamGameNumber,
                allowEvaluationOpportunity: true,
                random);
        }

        /// <summary>
        /// 포스트시즌처럼 평가 목적의 강제 기회를 허용하지 않는 경기까지 구분해 역할을 결정한다.
        /// </summary>
        public PlayerGameRole DecideRole(
            Player player,
            ExpectedRole expectedRole,
            int strongestCompetitorOverall,
            int condition,
            int managerEvaluation,
            int teamGameNumber,
            bool allowEvaluationOpportunity,
            IRandomSource random)
        {
            if (player == null)
                throw new ArgumentNullException(nameof(player));
            if (random == null)
                throw new ArgumentNullException(nameof(random));
            if (teamGameNumber <= 0)
                throw new ArgumentOutOfRangeException(nameof(teamGameNumber));

            double variance = (random.NextDouble() * 2d - 1d) * _balance.ManagerDecisionVariance;
            double conditionAdjustment = (condition - _balance.InitialCondition) *
                                         _balance.ConditionDecisionWeight;
            double evaluationAdjustment = (managerEvaluation - _balance.InitialManagerEvaluation) *
                                          _balance.ManagerEvaluationDecisionWeight;
            double decisionScore = _playerValueEvaluator.CalculatePositionValue(player) +
                                   GetContractRoleBonus(expectedRole) +
                                   conditionAdjustment +
                                   evaluationAdjustment +
                                   variance;
            bool isEvaluationOpportunity = allowEvaluationOpportunity && IsEvaluationOpportunity(
                player,
                expectedRole,
                condition,
                teamGameNumber);

            if (player.PrimaryPosition == PlayerPosition.StartingPitcher)
            {
                int rotationSlot = player.PlayerId % _balance.StartingRotationSize;
                bool isRotationTurn = (teamGameNumber - 1) % _balance.StartingRotationSize == rotationSlot;
                return isRotationTurn &&
                       (isEvaluationOpportunity ||
                        decisionScore >= strongestCompetitorOverall - _balance.ReliefOpportunityMargin)
                    ? PlayerGameRole.StartingPitcher
                    : PlayerGameRole.Bench;
            }

            if (player.PrimaryPosition == PlayerPosition.ReliefPitcher)
            {
                return isEvaluationOpportunity ||
                       decisionScore >= strongestCompetitorOverall - _balance.ReliefOpportunityMargin
                    ? PlayerGameRole.ReliefPitcher
                    : PlayerGameRole.Bench;
            }

            return isEvaluationOpportunity || decisionScore >= strongestCompetitorOverall
                ? PlayerGameRole.StartingBatter
                : PlayerGameRole.Bench;
        }

        private bool IsEvaluationOpportunity(
            Player player,
            ExpectedRole expectedRole,
            int condition,
            int teamGameNumber)
        {
            if (condition < _balance.EvaluationOpportunityMinimumCondition)
                return false;

            int evaluationInterval = GetEvaluationInterval(expectedRole);
            int gameIndex = teamGameNumber - 1;
            int rotationSize = _balance.StartingRotationSize;
            int rotationSlot = player.PlayerId % rotationSize;
            if (player.PrimaryPosition == PlayerPosition.StartingPitcher)
            {
                if (gameIndex % rotationSize != rotationSlot)
                    return false;

                int rotationOpportunityIndex = (gameIndex - rotationSlot) / rotationSize;
                return rotationOpportunityIndex % evaluationInterval == 0;
            }

            // 2군과 대타가 없는 MVP에서도 평가 표본을 만들기 위해 로테이션 크기를 한 평가 주기로 쓴다.
            int gameInterval = evaluationInterval * rotationSize;
            int evaluationSlot = player.PlayerId % gameInterval;
            return gameIndex % gameInterval == evaluationSlot;
        }

        private int GetEvaluationInterval(ExpectedRole role)
        {
            return role switch
            {
                ExpectedRole.StartingCompetition => _balance.StartingCompetitionEvaluationInterval,
                ExpectedRole.RosterCompetition => _balance.RosterCompetitionEvaluationInterval,
                _ => _balance.BenchCompetitionEvaluationInterval
            };
        }

        private int GetContractRoleBonus(ExpectedRole role)
        {
            return role switch
            {
                ExpectedRole.StartingCompetition => _balance.StartingCompetitionBonus,
                ExpectedRole.RosterCompetition => _balance.RosterCompetitionBonus,
                _ => _balance.BenchCompetitionBonus
            };
        }
    }
}
