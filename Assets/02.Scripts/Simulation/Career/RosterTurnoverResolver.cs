using System;
using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Simulation.Random;

namespace Baseball.Simulation.Career
{
    /// <summary>
    /// 시즌 전환 시 다른 구단 로스터의 Overall을 포지션 필요도 기준선으로 회귀시키고
    /// 결정론적 편차를 더해 다음 시즌 로스터를 만든다.
    /// </summary>
    public sealed class RosterTurnoverResolver
    {
        private readonly TeamGenerationBalance _teamGenerationBalance;
        private readonly RosterTurnoverBalance _turnoverBalance;
        private readonly IRandomSource _random;

        public RosterTurnoverResolver(
            TeamGenerationBalance teamGenerationBalance,
            RosterTurnoverBalance turnoverBalance,
            IRandomSource random)
        {
            _teamGenerationBalance = teamGenerationBalance;
            _turnoverBalance = turnoverBalance;
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        /// <summary>
        /// TeamGenerator가 새 게임에서 쓰는 것과 같은 기준선(CompetitorOverallBase - 필요도 가중치)으로
        /// 기존 Overall을 MeanReversionWeight만큼 되돌리고, SeasonDriftVariance 범위의 편차를 더한다.
        /// </summary>
        public RosterCompetitor[] AdvanceSeason(RosterCompetitor[] currentRoster, int[] positionNeedRatings)
        {
            if (currentRoster == null)
                throw new ArgumentNullException(nameof(currentRoster));
            if (positionNeedRatings == null)
                throw new ArgumentNullException(nameof(positionNeedRatings));

            var result = new RosterCompetitor[currentRoster.Length];
            for (int index = 0; index < currentRoster.Length; index++)
            {
                RosterCompetitor competitor = currentRoster[index];
                double baseline = _teamGenerationBalance.CompetitorOverallBase -
                                  positionNeedRatings[(int)competitor.Position] *
                                  _teamGenerationBalance.PositionNeedCompetitorWeight;
                double reverted = competitor.Overall +
                                  (baseline - competitor.Overall) * _turnoverBalance.MeanReversionWeight;
                double drift = (_random.NextDouble() - 0.5d) * _turnoverBalance.SeasonDriftVariance;
                int overall = (int)Clamp(
                    reverted + drift,
                    _teamGenerationBalance.MinimumCompetitorOverall,
                    _teamGenerationBalance.MaximumCompetitorOverall);
                result[index] = new RosterCompetitor(
                    competitor.PlayerId,
                    competitor.Name,
                    competitor.Position,
                    overall);
            }
            return result;
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            if (value < minimum)
                return minimum;
            if (value > maximum)
                return maximum;
            return value;
        }
    }
}
