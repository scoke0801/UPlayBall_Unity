using System.Collections.Generic;
using Baseball.Simulation.Historical;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Simulation
{
    public sealed class OwnerPostseasonBracketTests
    {
        [Test]
        public void SelectSeeds_승률득실차영속Key순으로상위5팀을고정한다()
        {
            var standings = new[]
            {
                new OwnerLeagueStanding("TEAM-D", 7, 3, 8),
                new OwnerLeagueStanding("TEAM-C", 8, 2, 2),
                new OwnerLeagueStanding("TEAM-B", 7, 3, 8),
                new OwnerLeagueStanding("TEAM-E", 5, 5, 20),
                new OwnerLeagueStanding("TEAM-A", 9, 1, -2)
            };
            var ids = new Dictionary<string, int>
            {
                { "TEAM-A", 1 }, { "TEAM-B", 2 }, { "TEAM-C", 3 }, { "TEAM-D", 4 }, { "TEAM-E", 5 }
            };

            int[] seeds = OwnerPostseasonBracket.SelectSeeds(standings, ids);

            Assert.That(seeds, Is.EqualTo(new[] { 1, 3, 2, 4, 5 }));

        }

        [Test]
        public void SelectSeeds_3팀조는2위와3위의플레이오프부터시작한다()
        {
            var standings = new[]
            {
                new OwnerLeagueStanding("TEAM-C", 4, 6, 0),
                new OwnerLeagueStanding("TEAM-A", 7, 3, 0),
                new OwnerLeagueStanding("TEAM-B", 6, 4, 0)
            };
            var ids = new Dictionary<string, int>
            {
                { "TEAM-A", 1 }, { "TEAM-B", 2 }, { "TEAM-C", 3 }
            };

            Assert.That(OwnerPostseasonBracket.SelectSeeds(standings, ids), Is.EqualTo(new[] { 1, 2, 3 }));
        }
    }
}
