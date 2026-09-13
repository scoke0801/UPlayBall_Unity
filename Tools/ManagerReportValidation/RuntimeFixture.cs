using System;
using System.Collections.Generic;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Career;
using Baseball.Game.Historical;
using Baseball.Simulation.Historical;


namespace Baseball.Tools.ManagerReportValidation
{
    internal static class RuntimeFixture
    {
        internal sealed class FixtureData
        {
            public FixtureData(ManagerHistoricalRuntimeState state, RecordingHistoricalContentProvider provider)
            {
                State = state;
                Provider = provider;
            }

            public ManagerHistoricalRuntimeState State { get; }
            public RecordingHistoricalContentProvider Provider { get; }

            public ManagerHistoricalSaveAdapter CreateAdapter()
            {
                return new ManagerHistoricalSaveAdapter(
                    Provider,
                    CardEditionBalanceTable.CreateInitial());
            }
        }

        internal sealed class RecordingHistoricalContentProvider : IHistoricalContentProvider
        {
            private readonly HistoricalBakedContent _content;

            public RecordingHistoricalContentProvider(HistoricalBakedContent content)
            {
                _content = content;
            }

            public int LoadCount { get; private set; }

            public HistoricalBakedContent Load()
            {
                LoadCount++;
                return _content;
            }
        }

        internal static class Fixture
        {
            public static FixtureData Create(
                WorldRecordMode mode)
            {
                var persons = new List<PlayerPersonDefinition>(250);
                var seasons = new List<PlayerSeasonDefinition>(250);
                var cards = new List<PlayerCardDefinition>(250);
                var teamSeasons = new List<TeamSeasonDefinition>(10);
                var rosters = new List<CurrentRosterState>(10);
                var owned = new List<OwnedPlayerCardState>(25);
                var teamKeys = new string[10];
                var zeroModifiers = new int[PlayerAbilityCatalog.AbilityCount];

                for (int teamIndex = 0; teamIndex < 10; teamIndex++)
                {
                    string teamKey = $"TEAM-{teamIndex:00}";
                    teamKeys[teamIndex] = teamKey;
                    var entries = new List<ActiveRosterEntry>(25);
                    var teamCardIds = new string[25];
                    for (int rosterIndex = 0; rosterIndex < 25; rosterIndex++)
                    {
                        int playerIndex = teamIndex * 25 + rosterIndex;
                        string playerSeasonId = $"PS-{playerIndex:000}";
                        string playerPersonId = $"PP-{playerIndex:000}";
                        string cardId = PlayerCardDefinition.CreateStableCardId(
                            playerSeasonId,
                            PlayerCardEdition.Normal);
                        ActiveRosterRole role = GetRole(rosterIndex);
                        PlayerPosition position = GetPosition(role);
                        bool isPitcher = ActiveRosterCompositionRule.Standard.IsPitcherRole(role);
                        PitcherRole pitcherRole = isPitcher
                            ? ActiveRosterCompositionRule.Standard.GetAssignedPitcherRole(role)
                            : PitcherRole.Starter;
                        persons.Add(new PlayerPersonDefinition(
                            playerPersonId,
                            1998,
                            Handedness.Right,
                            Handedness.Right,
                            position,
                            RegistrationType.Domestic,
                            2020,
                            2035,
                            new PersonPotentialTrait(new int[PlayerAbilityCatalog.AbilityCount])));
                        seasons.Add(new PlayerSeasonDefinition(
                            playerSeasonId,
                            playerPersonId,
                            2024,
                            $"FRANCHISE-{teamIndex:00}",
                            teamKey,
                            position,
                            pitcherRole,
                            isPitcher ? PlayerType.Pitcher : PlayerType.Batter,
                            RegistrationType.Domestic,
                            new AbilityRatings(50),
                            5,
                            new AbilityRatings(60)));
                        cards.Add(new PlayerCardDefinition(
                            cardId,
                            playerSeasonId,
                            PlayerCardEdition.Normal,
                            zeroModifiers));
                        entries.Add(new ActiveRosterEntry(
                            cardId,
                            playerSeasonId,
                            playerPersonId,
                            RegistrationType.Domestic,
                            role));
                        teamCardIds[rosterIndex] = cardId;

                        if (teamIndex == 0)
                        {
                            if (rosterIndex == 0)
                            {
                                var training = new int[PlayerAbilityCatalog.AbilityCount];
                                var study = new int[PlayerAbilityCatalog.AbilityCount];
                                training[(int)PlayerAbility.Contact] = 2;
                                study[(int)PlayerAbility.Contact] = 1;
                                owned.Add(new OwnedPlayerCardState(
                                    cardId, 3, 2, true, true, new CardTrainingState(training, study)));
                            }
                            else
                            {
                                owned.Add(new OwnedPlayerCardState(cardId));
                            }
                        }
                    }
                    teamSeasons.Add(new TeamSeasonDefinition(
                        teamKey,
                        $"FRANCHISE-{teamIndex:00}",
                        2024,
                        teamCardIds,
                        teamCardIds,
                        50d));
                    rosters.Add(new CurrentRosterState(teamKey, entries));
                }

                var awards = new List<WorldAwardEntry>(50);
                var specialTeams = Array.Empty<SpecialCompositeTeamRegistration>();
                string[] leagueRegularTeamKeys = teamKeys;

                var historyStatistics = new[]
                {
                    new SeasonStatistics(
                        "PS-000",
                        "TEAM-00",
                        2024,
                        PlayerPosition.Catcher,
                        plateAppearances: 500,
                        atBats: 460,
                        hits: 150,
                        homeRuns: 20,
                        walks: 40,
                        strikeouts: 80,
                        defensiveChances: 600,
                        defensiveOutsAboveAverage: 4,
                        fieldingErrors: 3)
                };
                WorldHistorySnapshot history;
                if (mode == WorldRecordMode.SimulatedHistory)
                {
                    var teamStatistics = new TeamSeasonStatistics[teamKeys.Length];
                    var standings = new HistoricalStandingEntry[teamKeys.Length];
                    for (int index = 0; index < teamKeys.Length; index++)
                    {
                        teamStatistics[index] = new TeamSeasonStatistics(
                            teamKeys[index],
                            2024,
                            10,
                            10 - index,
                            index,
                            0,
                            50 - index,
                            30 + index,
                            300,
                            80 - index,
                            270,
                            20 + index,
                            70 + index,
                            20 + index);
                        standings[index] = new HistoricalStandingEntry(2024, index + 1, teamKeys[index]);
                    }
                    history = new WorldHistorySnapshot(
                        mode,
                        77123UL,
                        historyStatistics,
                        teamStatistics,
                        standings,
                        new[]
                        {
                            new HistoricalPostseasonResult(
                                2024,
                                new[] { teamKeys[0], teamKeys[1], teamKeys[2], teamKeys[3] },
                                teamKeys[0])
                        },
                        new WorldAwardRecord(awards));
                }
                else
                {
                    history = new WorldHistorySnapshot(
                        mode,
                        77123UL,
                        historyStatistics,
                        new WorldAwardRecord(awards));
                }

                var manifest = new HistoricalContentManifest(
                    1,
                    1,
                    "test-archive-hash",
                new HistoricalSourceContentManifest(
                        "test-reference",
                        "test-generator",
                        "test-balance",
                        20260901UL,
                        "test-content-hash"));
                var year = new HistoricalYearContentDefinition(
                    2024,
                    seasons,
                    cards,
                    teamSeasons,
                    Array.Empty<OriginalSeasonRecordDefinition>(),
                    Array.Empty<OriginalAwardRecordDefinition>());
                var bakedContent = new HistoricalBakedContent(manifest, persons, new[] { year });
                // 합성 Fixture의 FRANCHISE-00 계열은 실제 구단 연고지 매핑 대상이 아니다.
                var fixtureNames = new WorldIdentityNameCatalog(
                    bakedContent.IdentityNameCatalog.DomesticPlayerNames,
                    bakedContent.IdentityNameCatalog.ForeignPlayerNames,
                    bakedContent.IdentityNameCatalog.FranchiseNames);
                bakedContent = new HistoricalBakedContent(manifest, persons, new[] { year }, fixtureNames);
                var provider = new RecordingHistoricalContentProvider(bakedContent);
                WorldIdentityRegistry identities = new WorldIdentityGenerator().Generate(
                    bakedContent.PlayerPersons,
                    bakedContent.TeamSeasons,
                    bakedContent.IdentityNameCatalog,
                    77123UL);
                WorldCardCatalog catalog = WorldCardCatalogBuilder.Build(
                    seasons,
                    history.Awards,
                    CardEditionBalanceTable.CreateInitial());
                var state = new ManagerHistoricalRuntimeState(
                    "TEAM-00",
                    HistoricalContentReference.FromManifest(manifest),
                    identities,
                    history,
                    catalog,
                    new LeagueInstance("LEAGUE-01", LeagueGrade.Rookie, leagueRegularTeamKeys, specialTeams),
                    rosters,
                    owned,
                    new ManagerEconomyState(125000L, 80, 30, 40));
                return new FixtureData(state, provider);
            }

            private static ActiveRosterRole GetRole(int rosterIndex)
            {
                if (rosterIndex < 9)
                    return (ActiveRosterRole)rosterIndex;
                if (rosterIndex < 14)
                    return ActiveRosterRole.BenchHitter;
                return (ActiveRosterRole)(rosterIndex - 4);
            }

            private static PlayerPosition GetPosition(ActiveRosterRole role)
            {
                if (ActiveRosterCompositionRule.Standard.IsStartingHitterRole(role))
                    return ActiveRosterCompositionRule.Standard.GetAssignedPosition(role);
                if (role == ActiveRosterRole.BenchHitter)
                    return PlayerPosition.Catcher;
                return ActiveRosterCompositionRule.Standard.IsStartingPitcherRole(role)
                    ? PlayerPosition.StartingPitcher
                    : PlayerPosition.ReliefPitcher;
            }
        }
    }
}

