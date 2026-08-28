using System;

namespace Baseball.Core.Balance
{
    /// <summary>
    /// Rookie League 구단 변주와 포지션 경쟁자 생성에 쓰이는 계수를 보관한다.
    /// </summary>
    public readonly struct TeamGenerationBalance
    {
        /// <summary>
        /// 구단 등급 변주, 포지션 필요도, 경쟁자 수와 능력 범위를 생성한다.
        /// </summary>
        public TeamGenerationBalance(
            int archetypeVariation,
            double positionNeedBase,
            double rosterDepthNeedWeight,
            double positionNeedVariance,
            int minimumPositionNeed,
            int maximumPositionNeed,
            int competitorsPerPosition,
            double competitorOverallBase,
            double positionNeedCompetitorWeight,
            double competitorOverallVariance,
            int minimumCompetitorOverall,
            int maximumCompetitorOverall)
        {
            if (archetypeVariation < 0)
                throw new ArgumentOutOfRangeException(nameof(archetypeVariation));
            if (minimumPositionNeed < 0 || maximumPositionNeed > 100 || maximumPositionNeed < minimumPositionNeed)
                throw new ArgumentOutOfRangeException(nameof(minimumPositionNeed));
            if (competitorsPerPosition <= 0)
                throw new ArgumentOutOfRangeException(nameof(competitorsPerPosition));
            if (minimumCompetitorOverall < 0 || maximumCompetitorOverall > 100 || maximumCompetitorOverall < minimumCompetitorOverall)
                throw new ArgumentOutOfRangeException(nameof(minimumCompetitorOverall));

            ArchetypeVariation = archetypeVariation;
            PositionNeedBase = positionNeedBase;
            RosterDepthNeedWeight = rosterDepthNeedWeight;
            PositionNeedVariance = positionNeedVariance;
            MinimumPositionNeed = minimumPositionNeed;
            MaximumPositionNeed = maximumPositionNeed;
            CompetitorsPerPosition = competitorsPerPosition;
            CompetitorOverallBase = competitorOverallBase;
            PositionNeedCompetitorWeight = positionNeedCompetitorWeight;
            CompetitorOverallVariance = competitorOverallVariance;
            MinimumCompetitorOverall = minimumCompetitorOverall;
            MaximumCompetitorOverall = maximumCompetitorOverall;
        }

        public int ArchetypeVariation { get; }
        public double PositionNeedBase { get; }
        public double RosterDepthNeedWeight { get; }
        public double PositionNeedVariance { get; }
        public int MinimumPositionNeed { get; }
        public int MaximumPositionNeed { get; }
        public int CompetitorsPerPosition { get; }
        public double CompetitorOverallBase { get; }
        public double PositionNeedCompetitorWeight { get; }
        public double CompetitorOverallVariance { get; }
        public int MinimumCompetitorOverall { get; }
        public int MaximumCompetitorOverall { get; }

        /// <summary>
        /// 특징은 유지하면서 새 게임마다 체감 차이를 주는 최초 검증용 값을 만든다.
        /// </summary>
        public static TeamGenerationBalance CreateDefault()
        {
            return new TeamGenerationBalance(
                archetypeVariation: 8,
                positionNeedBase: 70d,
                rosterDepthNeedWeight: 0.5d,
                positionNeedVariance: 30d,
                minimumPositionNeed: 5,
                maximumPositionNeed: 95,
                competitorsPerPosition: 2,
                competitorOverallBase: 70d,
                positionNeedCompetitorWeight: 0.22d,
                competitorOverallVariance: 10d,
                minimumCompetitorOverall: 38,
                maximumCompetitorOverall: 72);
        }
    }
}
