using System.Collections.Generic;
using Baseball.Simulation.Career;
using Baseball.Simulation.Random;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Simulation.Career
{
    /// <summary>
    /// Rookie League 일정의 경기 수·라운드 공정성·결정론을 검증한다.
    /// </summary>
    public sealed class SeasonScheduleGeneratorTests
    {
        [Test]
        public void Generate_8구단모두정확히80경기를치른다()
        {
            int[] teamIds = { 1, 2, 3, 4, 5, 6, 7, 8 };
            ScheduledGameDefinition[] games = new SeasonScheduleGenerator(new Pcg32Random(77UL))
                .Generate(teamIds, 80);
            var counts = new Dictionary<int, int>();

            for (int index = 0; index < games.Length; index++)
            {
                ScheduledGameDefinition game = games[index];
                Assert.That(game.AwayTeamId, Is.Not.EqualTo(game.HomeTeamId));
                counts[game.AwayTeamId] = counts.GetValueOrDefault(game.AwayTeamId) + 1;
                counts[game.HomeTeamId] = counts.GetValueOrDefault(game.HomeTeamId) + 1;
            }

            Assert.That(games, Has.Length.EqualTo(320));
            for (int teamId = 1; teamId <= 8; teamId++)
                Assert.That(counts[teamId], Is.EqualTo(80), $"Team {teamId}");
        }

        [Test]
        public void Generate_매라운드모든구단이한번씩경기한다()
        {
            int[] teamIds = { 1, 2, 3, 4, 5, 6, 7, 8 };
            ScheduledGameDefinition[] games = new SeasonScheduleGenerator(new Pcg32Random(88UL))
                .Generate(teamIds, 80);

            for (int round = 1; round <= 80; round++)
            {
                var seen = new HashSet<int>();
                int gameCount = 0;
                for (int index = 0; index < games.Length; index++)
                {
                    if (games[index].Round != round)
                        continue;
                    gameCount++;
                    Assert.That(seen.Add(games[index].AwayTeamId), Is.True);
                    Assert.That(seen.Add(games[index].HomeTeamId), Is.True);
                }
                Assert.That(gameCount, Is.EqualTo(4));
                Assert.That(seen.Count, Is.EqualTo(8));
            }
        }

        [Test]
        public void Generate_같은Seed는같은대진을만든다()
        {
            int[] teamIds = { 1, 2, 3, 4, 5, 6, 7, 8 };
            ScheduledGameDefinition[] first = new SeasonScheduleGenerator(new Pcg32Random(99UL))
                .Generate(teamIds, 80);
            ScheduledGameDefinition[] second = new SeasonScheduleGenerator(new Pcg32Random(99UL))
                .Generate(teamIds, 80);

            for (int index = 0; index < first.Length; index++)
            {
                Assert.That(second[index].AwayTeamId, Is.EqualTo(first[index].AwayTeamId));
                Assert.That(second[index].HomeTeamId, Is.EqualTo(first[index].HomeTeamId));
            }
        }
    }
}
