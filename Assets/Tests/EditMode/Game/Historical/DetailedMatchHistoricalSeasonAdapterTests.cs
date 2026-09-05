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

            HistoricalSeasonSimulationResult simulation = adapter.Simulate(99123UL, regularTeams);
            IReadOnlyList<SeasonStatistics> result = simulation.Statistics;

            SeasonStatistics firstHalf = Find(result, "PS-101", isFirstHalf: true);
            SeasonStatistics regular = Find(result, "PS-101", isFirstHalf: false);
            Assert.That(firstHalf.PlateAppearances, Is.GreaterThan(0));
            Assert.That(regular.PlateAppearances, Is.EqualTo(firstHalf.PlateAppearances));
            Assert.That(regular.Hits, Is.EqualTo(firstHalf.Hits));
            Assert.That(regular.IsPostseason, Is.False);
            Assert.That(regular.IsAllStarGame, Is.False);
            Assert.That(simulation.TeamStatistics.Count, Is.EqualTo(10));
            Assert.That(simulation.Standings.Count, Is.EqualTo(10));
            Assert.That(simulation.Postseason.ChampionTeamSeasonKey, Is.EqualTo("TEAM-00"));
        }

        [Test]
        public void Simulate_AllStarGameStatisticsExcludeNonSelectedOpponentPlayers()
        {
            IReadOnlyList<TeamSeasonDefinition> regularTeams = CreateRegularTeams();
            var adapter = new DetailedMatchHistoricalSeasonAdapter(
                new OneDetailedMatchSeasonSource(
                    HistoricalMatchStage.AllStarGame,
                    new[] { "PS-101" }));

            IReadOnlyList<SeasonStatistics> result = adapter.Simulate(99124UL, regularTeams).Statistics;

            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].PlayerSeasonId, Is.EqualTo("PS-101"));
            Assert.That(result[0].IsAllStarGame, Is.True);
        }

        [Test]
        public void Simulate_AllStarGameSamePlayerOnBothSides_AccumulatesSelectedTeamSideOnly()
        {
            IReadOnlyList<TeamSeasonDefinition> regularTeams = CreateRegularTeams();
            var source = new OneDetailedMatchSeasonSource(
                HistoricalMatchStage.AllStarGame,
                new[] { "PS-101" },
                allStarGameStatisticsTeamId: 1,
                duplicateEligiblePlayerOnOpponent: true);
            var adapter = new DetailedMatchHistoricalSeasonAdapter(source);

            IReadOnlyList<SeasonStatistics> result = adapter.Simulate(99125UL, regularTeams).Statistics;

            Assert.That(source.SelectedSidePlateAppearances, Is.GreaterThan(0));
            Assert.That(source.OpponentSidePlateAppearances, Is.GreaterThan(0));
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].PlayerSeasonId, Is.EqualTo("PS-101"));
            Assert.That(result[0].PlateAppearances, Is.EqualTo(source.SelectedSidePlateAppearances));
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
            private readonly HistoricalMatchStage _stage;
            private readonly IReadOnlyList<string> _allStarGameEligiblePlayerSeasonIds;
            private readonly int? _allStarGameStatisticsTeamId;
            private readonly bool _duplicateEligiblePlayerOnOpponent;

            public OneDetailedMatchSeasonSource()
                : this(HistoricalMatchStage.RegularSeasonFirstHalf, null, null, false)
            {
            }

            public OneDetailedMatchSeasonSource(
                HistoricalMatchStage stage,
                IReadOnlyList<string> allStarGameEligiblePlayerSeasonIds,
                int? allStarGameStatisticsTeamId = 1,
                bool duplicateEligiblePlayerOnOpponent = false)
            {
                _stage = stage;
                _allStarGameEligiblePlayerSeasonIds = allStarGameEligiblePlayerSeasonIds;
                _allStarGameStatisticsTeamId = allStarGameStatisticsTeamId;
                _duplicateEligiblePlayerOnOpponent = duplicateEligiblePlayerOnOpponent;
            }

            public int SelectedSidePlateAppearances { get; private set; }
            public int OpponentSidePlateAppearances { get; private set; }

            public HistoricalDetailedSeasonOutput RunSeason(
                ulong worldHistorySeed,
                IReadOnlyList<TeamSeasonDefinition> regularFranchiseTeams)
            {
                Team away = CreateTeam(1);
                Player sharedPlayer = _duplicateEligiblePlayerOnOpponent
                    ? away.Lineup[0].Player
                    : null;
                Team home = CreateTeam(2, sharedPlayer);
                var input = new MatchInput(2024, 1, worldHistorySeed, away, home);
                MatchResult match = new MatchSimulator(
                        BalanceTable.CreateDefault(),
                        MatchRandomStreams.Create(worldHistorySeed))
                    .Simulate(input, NullMatchEventSink.Instance);
                if (_duplicateEligiblePlayerOnOpponent)
                {
                    SelectedSidePlateAppearances = GetPlateAppearances(match.AwayBoxScore, sharedPlayer.PlayerId);
                    OpponentSidePlateAppearances = GetPlateAppearances(match.HomeBoxScore, sharedPlayer.PlayerId);
                }

                var identities = new List<HistoricalPlayerSeasonIdentity>(20);
                AddIdentities(identities, 1, regularFranchiseTeams[0].TeamSeasonKey);
                AddIdentities(
                    identities,
                    2,
                    regularFranchiseTeams[1].TeamSeasonKey,
                    skipFirstHitter: _duplicateEligiblePlayerOnOpponent);
                var matches = new[] { new HistoricalDetailedMatchRecord(_stage, match) };
                var teamStatistics = new TeamSeasonStatistics[regularFranchiseTeams.Count];
                var standings = new HistoricalStandingEntry[regularFranchiseTeams.Count];
                for (int index = 0; index < regularFranchiseTeams.Count; index++)
                {
                    string teamSeasonKey = regularFranchiseTeams[index].TeamSeasonKey;
                    teamStatistics[index] = new TeamSeasonStatistics(
                        teamSeasonKey,
                        2024,
                        0,
                        0,
                        0,
                        0,
                        0,
                        0,
                        0,
                        0,
                        0,
                        0,
                        0,
                        0);
                    standings[index] = new HistoricalStandingEntry(2024, index + 1, teamSeasonKey);
                }
                var qualifiers = new string[4];
                for (int index = 0; index < qualifiers.Length; index++)
                    qualifiers[index] = regularFranchiseTeams[index].TeamSeasonKey;
                return new HistoricalDetailedSeasonOutput(
                    2024,
                    matches,
                    identities,
                    _allStarGameEligiblePlayerSeasonIds,
                    _allStarGameStatisticsTeamId,
                    teamStatistics,
                    standings,
                    new HistoricalPostseasonResult(2024, qualifiers, qualifiers[0]));
            }

            private static Team CreateTeam(int teamId, Player sharedFirstHitter = null)
            {
                var slots = new LineupSlot[9];
                for (int index = 0; index < slots.Length; index++)
                {
                    PlayerPosition position = (PlayerPosition)(index + 1);
                    Player player = index == 0 && sharedFirstHitter != null
                        ? sharedFirstHitter
                        : new Player(
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
                string teamSeasonKey,
                bool skipFirstHitter = false)
            {
                for (int index = 0; index < 9; index++)
                {
                    if (skipFirstHitter && index == 0)
                        continue;
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

            private static int GetPlateAppearances(TeamBoxScore boxScore, int playerId)
            {
                for (int index = 0; index < boxScore.BattingLines.Count; index++)
                {
                    PlayerBattingLine line = boxScore.BattingLines[index];
                    if (line.PlayerId == playerId)
                        return line.PlateAppearances;
                }
                throw new AssertionException("대상 선수의 타격 기록을 찾지 못했습니다.");
            }
        }
    }
}
