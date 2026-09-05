using System;
using System.Collections.Generic;
using System.Linq;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Historical;
using Baseball.Simulation.Random;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Simulation
{
    /// <summary>World History, 기록 기반 Award, 특수 합성팀의 순서와 결정론 계약을 검증한다.</summary>
    public sealed class HistoricalWorldAwardTests
    {
        private const int TestYear = 2035;

        [Test]
        public void LegacyInitializer_OriginalHistory는Simulation을호출하지않는다()
        {
            SeasonStatistics statistic = CreateStatistic("P001", PlayerPosition.Catcher, 10);
            var originalSeasons = new[] { new OriginalSeasonRecordDefinition(statistic) };
            var originalAwards = new[]
            {
                new OriginalAwardRecordDefinition(
                    new WorldAwardEntry(TestYear, WorldAwardType.AllStar, "P001", PlayerPosition.Catcher))
            };
            var simulation = new CountingHistoricalSimulation(new[] { statistic });
            var awardResolver = new CountingAwardResolver();
            var initializer = new WorldHistoryInitializer(
                simulation,
                awardResolver,
                new OriginalHistoryLoader());

            WorldHistorySnapshot snapshot = initializer.Initialize(
                new WorldHistoryInitializationRequest(
                    WorldRecordMode.OriginalHistory,
                    77UL,
                    originalSeasonRecords: originalSeasons,
                    originalAwardRecords: originalAwards));

            Assert.That(snapshot.RecordMode, Is.EqualTo(WorldRecordMode.OriginalHistory));
            Assert.That(snapshot.Statistics.Count, Is.EqualTo(1));
            Assert.That(snapshot.Awards.Entries.Count, Is.EqualTo(1));
            Assert.That(simulation.CallCount, Is.Zero);
            Assert.That(awardResolver.CallCount, Is.Zero);
        }

        [Test]
        public void Initialize_저장된SimulatedHistory는과거를재실행하지않는다()
        {
            var existing = new WorldHistorySnapshot(
                WorldRecordMode.SimulatedHistory,
                1234UL,
                Array.Empty<SeasonStatistics>(),
                new WorldAwardRecord(Array.Empty<WorldAwardEntry>()));
            var simulation = new CountingHistoricalSimulation(Array.Empty<SeasonStatistics>());
            var awardResolver = new CountingAwardResolver();
            var initializer = new WorldHistoryInitializer(
                simulation,
                awardResolver,
                new OriginalHistoryLoader());

            WorldHistorySnapshot result = initializer.Initialize(
                new WorldHistoryInitializationRequest(
                    WorldRecordMode.SimulatedHistory,
                    1234UL,
                    existingSnapshot: existing));

            Assert.That(result, Is.SameAs(existing));
            Assert.That(simulation.CallCount, Is.Zero);
            Assert.That(awardResolver.CallCount, Is.Zero);
        }

        [Test]
        public void Initialize_SimulatedHistory는해당연도정규구단만한번실행한다()
        {
            TeamSeasonDefinition[] teams = CreateRegularTeams();
            SeasonStatistics statistic = CreateStatistic(
                "SIM-P001",
                PlayerPosition.Catcher,
                15,
                teamSeasonKey: teams[0].TeamSeasonKey);
            var simulation = new CountingHistoricalSimulation(new[] { statistic });
            var awardResolver = new CountingAwardResolver();
            var initializer = new WorldHistoryInitializer(
                simulation,
                awardResolver,
                new OriginalHistoryLoader());

            WorldHistorySnapshot snapshot = initializer.Initialize(
                new WorldHistoryInitializationRequest(
                    WorldRecordMode.SimulatedHistory,
                    9876UL,
                    regularFranchiseTeams: teams));

            Assert.That(snapshot.RecordMode, Is.EqualTo(WorldRecordMode.SimulatedHistory));
            Assert.That(simulation.CallCount, Is.EqualTo(1));
            Assert.That(simulation.LastTeamCount, Is.EqualTo(LeagueInstance.MaximumRegularFranchiseTeamCount));
            Assert.That(awardResolver.CallCount, Is.EqualTo(1));
        }

        [Test]
        public void Initialize_SimulatedHistory는6구단연도를수용한다()
        {
            TeamSeasonDefinition[] teams = CreateRegularTeams(6);
            var simulation = new CountingHistoricalSimulation(new[]
            {
                CreateStatistic(
                    "SIM-SIX-P001",
                    PlayerPosition.Catcher,
                    15,
                    teamSeasonKey: teams[0].TeamSeasonKey)
            });
            var initializer = new WorldHistoryInitializer(
                simulation,
                new CountingAwardResolver(),
                new OriginalHistoryLoader());

            WorldHistorySnapshot snapshot = initializer.Initialize(
                new WorldHistoryInitializationRequest(
                    WorldRecordMode.SimulatedHistory,
                    9877UL,
                    regularFranchiseTeams: teams));

            Assert.That(snapshot.TeamStatistics.Count, Is.EqualTo(6));
            Assert.That(snapshot.Standings.Count, Is.EqualTo(6));
            Assert.That(snapshot.PostseasonResults[0].QualifiedTeamSeasonKeys.Count, Is.EqualTo(4));
        }

        [Test]
        public void Resolve_실제기록으로AllStar25명과GoldenGlove10명쿼터를결정론적으로선정한다()
        {
            List<SeasonStatistics> statistics = CreateAwardStatistics();
            var resolver = new WorldAwardResolver(AwardScoringPolicy.CreateDefault());

            WorldAwardRecord first = resolver.Resolve(statistics);
            statistics.Reverse();
            WorldAwardRecord second = resolver.Resolve(statistics);

            AssertAwardRecordsEqual(first, second);
            WorldAwardEntry[] allStars = first.Entries
                .Where(entry => entry.AwardType == WorldAwardType.AllStar)
                .ToArray();
            Assert.That(allStars.Length, Is.EqualTo(25));
            foreach (PlayerPosition position in StartingHitterPositions())
                Assert.That(allStars.Count(entry => entry.Position == position), Is.GreaterThanOrEqualTo(1));
            Assert.That(allStars.Count(entry => entry.Position == PlayerPosition.StartingPitcher), Is.EqualTo(5));
            Assert.That(allStars.Count(entry => entry.Position == PlayerPosition.ReliefPitcher), Is.EqualTo(6));

            WorldAwardEntry[] goldenGloves = first.Entries
                .Where(entry => entry.AwardType == WorldAwardType.GoldenGlove)
                .ToArray();
            Assert.That(goldenGloves.Length, Is.EqualTo(10));
            Assert.That(goldenGloves.Count(entry => IsPitcher(entry.Position)), Is.EqualTo(1));
            Assert.That(goldenGloves.Count(entry => IsOutfielder(entry.Position)), Is.EqualTo(3));
            Assert.That(goldenGloves.Count(entry => entry.Position == PlayerPosition.Catcher), Is.EqualTo(1));
            Assert.That(goldenGloves.Count(entry => entry.Position == PlayerPosition.FirstBase), Is.EqualTo(1));
            Assert.That(goldenGloves.Count(entry => entry.Position == PlayerPosition.SecondBase), Is.EqualTo(1));
            Assert.That(goldenGloves.Count(entry => entry.Position == PlayerPosition.ThirdBase), Is.EqualTo(1));
            Assert.That(goldenGloves.Count(entry => entry.Position == PlayerPosition.Shortstop), Is.EqualTo(1));
            Assert.That(goldenGloves.Count(entry => entry.Position == PlayerPosition.DesignatedHitter), Is.EqualTo(1));
            Assert.That(first.Entries.Count(entry => entry.AwardType == WorldAwardType.RegularSeasonMvp), Is.EqualTo(1));
            Assert.That(first.Entries.Count(entry => entry.AwardType == WorldAwardType.AllStarGameMvp), Is.EqualTo(1));
            Assert.That(first.Entries.Count(entry => entry.AwardType == WorldAwardType.PostseasonMvp), Is.EqualTo(1));
        }

        [Test]
        public void Build_우선순위대로세팀을만들고PlayerSeason중복과원본변조가없다()
        {
            PlayerSeasonDefinition[] players = CreateCompositePool();
            WorldHistorySnapshot history = CreateCompositeHistory(players);
            WorldCardCatalog catalog = WorldCardCatalogBuilder.Build(
                players,
                history.Awards,
                CardEditionBalanceTable.CreateInitial());
            var builder = new SpecialCompositeTeamBuilder(AwardScoringPolicy.CreateDefault());

            SpecialCompositeTeamSet first = builder.Build(
                TestYear,
                players,
                history,
                catalog,
                new Pcg32Random(555UL));
            SpecialCompositeTeamSet second = builder.Build(
                TestYear,
                players.Reverse().ToArray(),
                history,
                catalog,
                new Pcg32Random(555UL));

            Assert.That(first.Teams.Count, Is.EqualTo(3));
            Assert.That(first.Get(SpecialCompositeTeamType.AllStarComposite).Roster.Count, Is.EqualTo(25));
            Assert.That(first.Get(SpecialCompositeTeamType.GoldenGloveComposite).Roster.Count, Is.EqualTo(25));
            Assert.That(first.Get(SpecialCompositeTeamType.YearSelectComposite).Roster.Count, Is.EqualTo(25));
            var uniquePlayerSeasons = new HashSet<string>(StringComparer.Ordinal);
            for (int teamIndex = 0; teamIndex < first.Teams.Count; teamIndex++)
            {
                SpecialCompositeTeamDefinition team = first.Teams[teamIndex];
                Assert.That(team.TeamSeasonKey, Does.Contain(TestYear.ToString()));
                for (int rosterIndex = 0; rosterIndex < team.Roster.Count; rosterIndex++)
                {
                    SpecialCompositeRosterEntry entry = team.Roster[rosterIndex];
                    Assert.That(uniquePlayerSeasons.Add(entry.PlayerSeasonId), Is.True);
                    Assert.That(catalog.TryGetCard(entry.CardId, out PlayerCardDefinition card), Is.True);
                    Assert.That(card.PlayerSeasonId, Is.EqualTo(entry.PlayerSeasonId));
                    PlayerCardEdition expectedEdition = GetExpectedCompositeEdition(
                        team.TeamType,
                        entry.PlayerSeasonId,
                        catalog);
                    Assert.That(card.Edition, Is.EqualTo(expectedEdition));
                }
            }

            AssertCompositeSetsEqual(first, second);
            Assert.That(players.All(player => player.OriginYear == TestYear), Is.True);
            Assert.That(players.All(player => player.OriginTeamSeasonKey.StartsWith("REGULAR:", StringComparison.Ordinal)), Is.True);
        }

        [Test]
        public void Build_Award가확정되지않았으면실패한다()
        {
            PlayerSeasonDefinition[] players = CreateCompositePool();
            var history = new WorldHistorySnapshot(
                WorldRecordMode.SimulatedHistory,
                1UL,
                Array.Empty<SeasonStatistics>(),
                new WorldAwardRecord(Array.Empty<WorldAwardEntry>()));
            var builder = new SpecialCompositeTeamBuilder(AwardScoringPolicy.CreateDefault());
            WorldCardCatalog catalog = WorldCardCatalogBuilder.Build(
                players,
                history.Awards,
                CardEditionBalanceTable.CreateInitial());

            Assert.Throws<InvalidOperationException>(() =>
                builder.Build(TestYear, players, history, catalog, new Pcg32Random(1UL)));
        }

        private static PlayerCardEdition GetExpectedCompositeEdition(
            SpecialCompositeTeamType teamType,
            string playerSeasonId,
            WorldCardCatalog catalog)
        {
            PlayerCardEdition preferred = teamType switch
            {
                SpecialCompositeTeamType.AllStarComposite => PlayerCardEdition.AllStar,
                SpecialCompositeTeamType.GoldenGloveComposite => PlayerCardEdition.GoldenGlove,
                SpecialCompositeTeamType.YearSelectComposite => PlayerCardEdition.Normal,
                _ => throw new ArgumentOutOfRangeException(nameof(teamType))
            };
            string preferredId = PlayerCardDefinition.CreateStableCardId(playerSeasonId, preferred);
            return catalog.TryGetCard(preferredId, out _) ? preferred : PlayerCardEdition.Normal;
        }

        private static List<SeasonStatistics> CreateAwardStatistics()
        {
            var result = new List<SeasonStatistics>();
            int sequence = 0;
            foreach (PlayerPosition position in StartingHitterPositions())
            {
                for (int candidate = 0; candidate < 3; candidate++)
                {
                    string id = "AW-H-" + sequence.ToString("D3");
                    int value = 80 - candidate;
                    result.Add(CreateStatistic(id, position, value, isFirstHalf: true));
                    result.Add(CreateStatistic(id, position, value + 20, defensiveValue: value));
                    sequence++;
                }
            }

            for (int candidate = 0; candidate < 7; candidate++)
            {
                string id = "AW-SP-" + candidate.ToString("D2");
                result.Add(CreatePitchingStatistic(id, PlayerPosition.StartingPitcher, 170 - candidate, isFirstHalf: true));
                result.Add(CreatePitchingStatistic(id, PlayerPosition.StartingPitcher, 500 - candidate, defensiveValue: 20 + candidate));
            }
            for (int candidate = 0; candidate < 8; candidate++)
            {
                string id = "AW-RP-" + candidate.ToString("D2");
                result.Add(CreatePitchingStatistic(id, PlayerPosition.ReliefPitcher, 120 - candidate, isFirstHalf: true));
                result.Add(CreatePitchingStatistic(id, PlayerPosition.ReliefPitcher, 250 - candidate, defensiveValue: 30 + candidate));
            }

            result.Add(CreateStatistic("ASG-WINNER", PlayerPosition.CenterField, 30, isAllStarGame: true));
            result.Add(CreateStatistic("ASG-RUNNER", PlayerPosition.Catcher, 1, isAllStarGame: true));
            result.Add(CreateStatistic("POST-WINNER", PlayerPosition.FirstBase, 40, isPostseason: true));
            result.Add(CreateStatistic("POST-RUNNER", PlayerPosition.SecondBase, 1, isPostseason: true));
            return result;
        }

        private static PlayerSeasonDefinition[] CreateCompositePool()
        {
            var result = new List<PlayerSeasonDefinition>();
            int sequence = 0;
            foreach (PlayerPosition position in StartingHitterPositions())
            {
                for (int candidate = 0; candidate < 8; candidate++)
                {
                    result.Add(CreatePlayerSeason(
                        "POOL-H-" + sequence.ToString("D3"),
                        position,
                        PlayerType.Batter));
                    sequence++;
                }
            }
            for (int candidate = 0; candidate < 20; candidate++)
            {
                result.Add(CreatePlayerSeason(
                    "POOL-SP-" + candidate.ToString("D3"),
                    PlayerPosition.StartingPitcher,
                    PlayerType.Pitcher,
                    PitcherRole.Starter));
            }
            for (int candidate = 0; candidate < 30; candidate++)
            {
                PitcherRole role = candidate % 3 == 0
                    ? PitcherRole.Setup
                    : candidate % 3 == 1 ? PitcherRole.Closer : PitcherRole.MiddleRelief;
                result.Add(CreatePlayerSeason(
                    "POOL-RP-" + candidate.ToString("D3"),
                    PlayerPosition.ReliefPitcher,
                    PlayerType.Pitcher,
                    role));
            }
            return result.ToArray();
        }

        private static WorldHistorySnapshot CreateCompositeHistory(PlayerSeasonDefinition[] players)
        {
            var statistics = new List<SeasonStatistics>(players.Length);
            for (int index = 0; index < players.Length; index++)
            {
                PlayerSeasonDefinition player = players[index];
                statistics.Add(IsPitcher(player.Position)
                    ? CreatePitchingStatistic(player.PlayerSeasonId, player.Position, 100 + index)
                    : CreateStatistic(player.PlayerSeasonId, player.Position, 40 + index % 30));
            }

            var awards = new List<WorldAwardEntry>();
            for (int index = 0; index < 25; index++)
            {
                PlayerSeasonDefinition player = players[index];
                awards.Add(new WorldAwardEntry(TestYear, WorldAwardType.AllStar, player.PlayerSeasonId, player.Position));
            }
            for (int index = 10; index < 20; index++)
            {
                PlayerSeasonDefinition player = players[index];
                awards.Add(new WorldAwardEntry(TestYear, WorldAwardType.GoldenGlove, player.PlayerSeasonId, player.Position));
            }
            return new WorldHistorySnapshot(
                WorldRecordMode.SimulatedHistory,
                555UL,
                statistics,
                new WorldAwardRecord(awards));
        }

        private static PlayerSeasonDefinition CreatePlayerSeason(
            string playerSeasonId,
            PlayerPosition position,
            PlayerType playerType,
            PitcherRole pitcherRole = PitcherRole.MiddleRelief)
        {
            return new PlayerSeasonDefinition(
                playerSeasonId,
                "PERSON:" + playerSeasonId,
                TestYear,
                "FRANCHISE:" + playerSeasonId,
                "REGULAR:" + playerSeasonId,
                position,
                pitcherRole,
                playerType,
                RegistrationType.Domestic,
                new AbilityRatings(50),
                5,
                new AbilityRatings(70));
        }

        private static TeamSeasonDefinition[] CreateRegularTeams(
            int teamCount = LeagueInstance.MaximumRegularFranchiseTeamCount)
        {
            var result = new TeamSeasonDefinition[teamCount];
            for (int team = 0; team < result.Length; team++)
            {
                var cardIds = new string[25];
                for (int card = 0; card < cardIds.Length; card++)
                    cardIds[card] = "TEAM-" + team + "-CARD-" + card;
                result[team] = new TeamSeasonDefinition(
                    "TEAM:" + team,
                    "FRANCHISE:" + team,
                    TestYear,
                    cardIds,
                    cardIds,
                    50d);
            }
            return result;
        }

        private static SeasonStatistics CreateStatistic(
            string playerSeasonId,
            PlayerPosition position,
            int value,
            int defensiveValue = 10,
            bool isFirstHalf = false,
            bool isPostseason = false,
            bool isAllStarGame = false,
            string teamSeasonKey = "TEAM:0")
        {
            return new SeasonStatistics(
                playerSeasonId,
                teamSeasonKey,
                TestYear,
                position,
                plateAppearances: Math.Max(1, value * 3),
                hits: value,
                homeRuns: value / 5,
                walks: value / 4,
                strikeouts: value / 3,
                stolenBases: value / 8,
                defensiveChances: Math.Max(1, defensiveValue * 10),
                defensiveOutsAboveAverage: defensiveValue,
                fieldingErrors: 1,
                isFirstHalf: isFirstHalf,
                isPostseason: isPostseason,
                isAllStarGame: isAllStarGame);
        }

        private static SeasonStatistics CreatePitchingStatistic(
            string playerSeasonId,
            PlayerPosition position,
            int pitchingOuts,
            int defensiveValue = 10,
            bool isFirstHalf = false)
        {
            return new SeasonStatistics(
                playerSeasonId,
                "TEAM:0",
                TestYear,
                position,
                pitchingOuts: pitchingOuts,
                earnedRuns: Math.Max(1, pitchingOuts / 30),
                pitchingStrikeouts: pitchingOuts / 3,
                defensiveChances: 20,
                defensiveOutsAboveAverage: defensiveValue,
                fieldingErrors: 1,
                isFirstHalf: isFirstHalf);
        }

        private static PlayerPosition[] StartingHitterPositions()
        {
            return new[]
            {
                PlayerPosition.Catcher,
                PlayerPosition.FirstBase,
                PlayerPosition.SecondBase,
                PlayerPosition.ThirdBase,
                PlayerPosition.Shortstop,
                PlayerPosition.LeftField,
                PlayerPosition.CenterField,
                PlayerPosition.RightField,
                PlayerPosition.DesignatedHitter
            };
        }

        private static bool IsPitcher(PlayerPosition position)
        {
            return position == PlayerPosition.StartingPitcher || position == PlayerPosition.ReliefPitcher;
        }

        private static bool IsOutfielder(PlayerPosition position)
        {
            return position == PlayerPosition.LeftField ||
                position == PlayerPosition.CenterField ||
                position == PlayerPosition.RightField;
        }

        private static void AssertAwardRecordsEqual(WorldAwardRecord expected, WorldAwardRecord actual)
        {
            Assert.That(actual.Entries.Count, Is.EqualTo(expected.Entries.Count));
            for (int index = 0; index < expected.Entries.Count; index++)
            {
                Assert.That(actual.Entries[index].SeasonYear, Is.EqualTo(expected.Entries[index].SeasonYear));
                Assert.That(actual.Entries[index].AwardType, Is.EqualTo(expected.Entries[index].AwardType));
                Assert.That(actual.Entries[index].PlayerSeasonId, Is.EqualTo(expected.Entries[index].PlayerSeasonId));
                Assert.That(actual.Entries[index].Position, Is.EqualTo(expected.Entries[index].Position));
            }
        }

        private static void AssertCompositeSetsEqual(
            SpecialCompositeTeamSet expected,
            SpecialCompositeTeamSet actual)
        {
            for (int teamIndex = 0; teamIndex < expected.Teams.Count; teamIndex++)
            {
                SpecialCompositeTeamDefinition expectedTeam = expected.Teams[teamIndex];
                SpecialCompositeTeamDefinition actualTeam = actual.Teams[teamIndex];
                Assert.That(actualTeam.TeamType, Is.EqualTo(expectedTeam.TeamType));
                for (int rosterIndex = 0; rosterIndex < expectedTeam.Roster.Count; rosterIndex++)
                {
                    Assert.That(
                        actualTeam.Roster[rosterIndex].PlayerSeasonId,
                        Is.EqualTo(expectedTeam.Roster[rosterIndex].PlayerSeasonId));
                    Assert.That(actualTeam.Roster[rosterIndex].Role, Is.EqualTo(expectedTeam.Roster[rosterIndex].Role));
                }
            }
        }

        private sealed class CountingHistoricalSimulation : IHistoricalSeasonSimulation
        {
            private readonly IReadOnlyList<SeasonStatistics> _result;

            public CountingHistoricalSimulation(IReadOnlyList<SeasonStatistics> result)
            {
                _result = result;
            }

            public int CallCount { get; private set; }
            public int LastTeamCount { get; private set; }

            public HistoricalSeasonSimulationResult Simulate(
                ulong worldHistorySeed,
                IReadOnlyList<TeamSeasonDefinition> regularFranchiseTeams)
            {
                CallCount++;
                LastTeamCount = regularFranchiseTeams.Count;
                var teamStatistics = new TeamSeasonStatistics[regularFranchiseTeams.Count];
                var standings = new HistoricalStandingEntry[regularFranchiseTeams.Count];
                for (int index = 0; index < regularFranchiseTeams.Count; index++)
                {
                    string teamSeasonKey = regularFranchiseTeams[index].TeamSeasonKey;
                    teamStatistics[index] = new TeamSeasonStatistics(
                        teamSeasonKey,
                        regularFranchiseTeams[index].OriginYear,
                        1,
                        index == 0 ? 1 : 0,
                        index == 0 ? 0 : 1,
                        0,
                        index == 0 ? 5 : 1,
                        index == 0 ? 1 : 5,
                        30,
                        8,
                        27,
                        index == 0 ? 1 : 5,
                        8,
                        2);
                    standings[index] = new HistoricalStandingEntry(
                        regularFranchiseTeams[index].OriginYear,
                        index + 1,
                        teamSeasonKey);
                }
                var qualifiers = new string[4];
                for (int index = 0; index < qualifiers.Length; index++)
                    qualifiers[index] = regularFranchiseTeams[index].TeamSeasonKey;
                return new HistoricalSeasonSimulationResult(
                    _result,
                    teamStatistics,
                    standings,
                    new HistoricalPostseasonResult(
                        regularFranchiseTeams[0].OriginYear,
                        qualifiers,
                        qualifiers[0]));
            }
        }

        private sealed class CountingAwardResolver : ISeasonAwardResolver
        {
            public int CallCount { get; private set; }

            public WorldAwardRecord Resolve(IReadOnlyList<SeasonStatistics> statistics)
            {
                CallCount++;
                return new WorldAwardRecord(Array.Empty<WorldAwardEntry>());
            }
        }
    }
}
