using System.Collections.Generic;
using Baseball.Simulation.Historical;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Simulation
{
    public sealed class OwnerPostseasonBracketTests
    {
        [Test]
        public void SelectSeeds_승률득실차영속Key순으로상위4팀을고정한다()
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

            Assert.That(seeds, Is.EqualTo(new[] { 1, 3, 2, 4 }));
            OwnerPostseasonBracket.GetSemifinalPair(seeds, 0, out int firstHigh, out int firstLow);
            OwnerPostseasonBracket.GetSemifinalPair(seeds, 1, out int secondHigh, out int secondLow);
            Assert.That((firstHigh, firstLow), Is.EqualTo((1, 4)));
            Assert.That((secondHigh, secondLow), Is.EqualTo((3, 2)));
        }

        [Test]
        public void SelectSeeds_3팀조는상위2팀만결승에진출한다()
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

            Assert.That(OwnerPostseasonBracket.SelectSeeds(standings, ids), Is.EqualTo(new[] { 1, 2 }));
        }
    }
}
