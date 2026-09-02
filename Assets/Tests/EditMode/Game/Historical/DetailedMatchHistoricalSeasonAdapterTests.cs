using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Historical;
using Baseball.Simulation.Match;
using Baseball.Simulation.Random;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game.Historical
{
    public sealed class DetailedMatchHistoricalSeasonAdapterTests
    {
        [Test]
        public void Simulate_AggregatesActualDetailedBoxScoreIntoFirstHalfAndRegularStatistics()
        {
            IReadOnlyList<TeamSeasonDefinition> regularTeams = CreateRegularTeams();
            var adapter = new DetailedMatchHistoricalSeasonAdapter(new OneDetailedMatchSeasonSource());

            IReadOnlyList<SeasonStatistics> result = adapter.Simulate(99123UL, regularTeams);

            SeasonStatistics firstHalf = Find(result, "PS-101", isFirstHalf: true);
            SeasonStatistics regular = Find(result, "PS-101", isFirstHalf: false);
            Assert.That(firstHalf.PlateAppearances, Is.GreaterThan(0));
            Assert.That(regular.PlateAppearances, Is.EqualTo(firstHalf.PlateAppearances));
            Assert.That(regular.Hits, Is.EqualTo(firstHalf.Hits));
            Assert.That(regular.IsPostseason, Is.False);
            Assert.That(regular.IsAllStarGame, Is.False);
        }

        private static SeasonStatistics Find(
            IReadOnlyList<SeasonStatistics> rows,
            string playerSeasonId,
            bool isFirstHalf)
        {
            for (int index = 0; index < rows.Count; index++)
            {
                SeasonStatistics row = rows[index];
                if (row.PlayerSeasonId == playerSeasonId && row.IsFirstHalf == isFirstHalf &&
                    !row.IsPostseason && !row.IsAllStarGame)
                    return row;
            }
            throw new AssertionException("집계된 SeasonStatistics를 찾지 못했습니다.");
        }

        private static IReadOnlyList<TeamSeasonDefinition> CreateRegularTeams()
        {
            var result = new TeamSeasonDefinition[10];
            for (int teamIndex = 0; teamIndex < result.Length; teamIndex++)
            {
                var cardIds = new string[25];
                for (int cardIndex = 0; cardIndex < cardIds.Length; cardIndex++)
                    cardIds[cardIndex] = $"TEAM-{teamIndex:00}-CARD-{cardIndex:00}";
                result[teamIndex] = new TeamSeasonDefinition(
                    $"TEAM-{teamIndex:00}",
                    $"FRANCHISE-{teamIndex:00}",
                    2024,
                    cardIds,
                    cardIds,
                    50d);
            }
            return result;
        }

        private sealed class OneDetailedMatchSeasonSource : IHistoricalDetailedSeasonSource
        {
            public HistoricalDetailedSeasonOutput RunSeason(
                ulong worldHistorySeed,
                IReadOnlyList<TeamSeasonDefinition> regularFranchiseTeams)
            {
                Team away = CreateTeam(1);
                Team home = CreateTeam(2);
                var input = new MatchInput(2024, 1, worldHistorySeed, away, home);
                MatchResult match = new MatchSimulator(
                        BalanceTable.CreateDefault(),
                        MatchRandomStreams.Create(worldHistorySeed))
                    .Simulate(input, NullMatchEventSink.Instance);

                var identities = new List<HistoricalPlayerSeasonIdentity>(20);
                AddIdentities(identities, 1, regularFranchiseTeams[0].TeamSeasonKey);
                AddIdentities(identities, 2, regularFranchiseTeams[1].TeamSeasonKey);
                return new HistoricalDetailedSeasonOutput(
                    2024,
                    new[] { new HistoricalDetailedMatchRecord(HistoricalMatchStage.RegularSeasonFirstHalf, match) },
                    identities);
            }

            private static Team CreateTeam(int teamId)
            {
                var slots = new LineupSlot[9];
                for (int index = 0; index < slots.Length; index++)
                {
                    PlayerPosition position = (PlayerPosition)(index + 1);
                    var player = new Player(
                        teamId * 100 + index + 1,
                        $"{teamId}팀 타자 {index + 1}",
                        position,
                        Handedness.Right,
                        Handedness.Right,
                        new BatterAttributes(50, 50, 50, 50, 50, 50),
                        new PitcherAttributes(20, 20, 20, 20, 20, 20));
                    slots[index] = new LineupSlot(player, position);
                }
                var pitcher = new Player(
                    teamId * 100 + 99,
                    $"{teamId}팀 투수",
                    PlayerPosition.StartingPitcher,
                    Handedness.Right,
                    Handedness.Right,
                    new BatterAttributes(20, 20, 20, 20, 30, 20),
                    new PitcherAttributes(50, 50, 50, 50, 50, 50));
                return new Team(teamId, $"테스트 {teamId}팀", new Lineup(slots), pitcher);
            }

            private static void AddIdentities(
                ICollection<HistoricalPlayerSeasonIdentity> target,
                int teamId,
                string teamSeasonKey)
            {
                for (int index = 0; index < 9; index++)
                {
                    int playerId = teamId * 100 + index + 1;
                    target.Add(new HistoricalPlayerSeasonIdentity(
                        playerId,
                        $"PS-{playerId}",
                        teamSeasonKey,
                        (PlayerPosition)(index + 1)));
                }
                int pitcherId = teamId * 100 + 99;
                target.Add(new HistoricalPlayerSeasonIdentity(
                    pitcherId,
                    $"PS-{pitcherId}",
                    teamSeasonKey,
                    PlayerPosition.StartingPitcher));
            }
        }
    }
}
