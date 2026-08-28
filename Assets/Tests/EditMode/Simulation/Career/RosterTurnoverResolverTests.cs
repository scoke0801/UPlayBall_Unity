using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Simulation.Career;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Simulation.Career
{
    /// <summary>시즌 전환 로스터 회귀가 리그 단계별 전력 기준선을 보존하는지 검증한다.</summary>
    public sealed class RosterTurnoverResolverTests
    {
        [Test]
        public void AdvanceSeason_리그전력보정을기준선과상한에반영한다()
        {
            var teamGeneration = new TeamGenerationBalance(
                archetypeVariation: 0,
                positionNeedBase: 0d,
                rosterDepthNeedWeight: 0d,
                positionNeedVariance: 0d,
                minimumPositionNeed: 0,
                maximumPositionNeed: 100,
                competitorsPerPosition: 1,
                competitorOverallBase: 60d,
                positionNeedCompetitorWeight: 0d,
                competitorOverallVariance: 0d,
                minimumCompetitorOverall: 40,
                maximumCompetitorOverall: 72);
            var turnover = new RosterTurnoverBalance(
                meanReversionWeight: 1d,
                seasonDriftVariance: 0d);
            var roster = new[]
            {
                new RosterCompetitor(1, "테스트 포수", PlayerPosition.Catcher, 50)
            };
            var positionNeeds = new int[(int)PlayerPosition.ReliefPitcher + 1];

            var rookieResolver = new RosterTurnoverResolver(
                teamGeneration,
                turnover,
                new SequenceRandom(0.5d));
            var majorResolver = new RosterTurnoverResolver(
                teamGeneration,
                turnover,
                new SequenceRandom(0.5d));

            RosterCompetitor[] rookieRoster = rookieResolver.AdvanceSeason(
                roster,
                positionNeeds,
                overallAdjustment: 0);
            RosterCompetitor[] majorRoster = majorResolver.AdvanceSeason(
                roster,
                positionNeeds,
                overallAdjustment: 20);

            Assert.That(rookieRoster[0].Overall, Is.EqualTo(60));
            Assert.That(majorRoster[0].Overall, Is.EqualTo(80));
        }

        [Test]
        public void AdvanceSeason_음수리그전력보정을거부한다()
        {
            BalanceTable balance = BalanceTable.CreateDefault();
            var resolver = new RosterTurnoverResolver(
                balance.TeamGeneration,
                balance.RosterTurnover,
                new SequenceRandom(0.5d));
            var roster = new[]
            {
                new RosterCompetitor(1, "테스트 포수", PlayerPosition.Catcher, 50)
            };
            var positionNeeds = new int[(int)PlayerPosition.ReliefPitcher + 1];

            Assert.That(
                () => resolver.AdvanceSeason(roster, positionNeeds, overallAdjustment: -1),
                Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }
    }
}
