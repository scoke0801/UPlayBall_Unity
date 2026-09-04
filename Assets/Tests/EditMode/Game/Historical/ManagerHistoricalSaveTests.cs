using System;
using System.Collections.Generic;
using System.Reflection;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Career;
using Baseball.Game.Historical;
using Baseball.Simulation.Historical;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game.Historical
{
    public sealed class ManagerHistoricalSaveTests
    {
        [Test]
        public void CreateSaveDataAndRestore_PreservesManagerHistoricalState()
        {
            FixtureData fixture = Fixture.Create(WorldRecordMode.SimulatedHistory);
            ManagerHistoricalRuntimeState original = fixture.State;
            ManagerHistoricalSaveAdapter adapter = fixture.CreateAdapter();

            ManagerHistoricalSaveData saveData = adapter.CreateSaveData(original);
            ManagerHistoricalRuntimeState restored = adapter.Restore(saveData);

            Assert.That(saveData.saveVersion, Is.EqualTo(ManagerHistoricalSaveAdapter.CurrentSaveVersion));
            Assert.That(restored.PlayerTeamSeasonKey, Is.EqualTo(original.PlayerTeamSeasonKey));
            Assert.That(restored.WorldHistory.RecordMode, Is.EqualTo(WorldRecordMode.SimulatedHistory));
            Assert.That(restored.WorldHistory.WorldHistorySeed, Is.EqualTo(77123UL));
            Assert.That(restored.League.RegularFranchiseTeamCount, Is.EqualTo(10));
            Assert.That(restored.Rosters.Count, Is.EqualTo(10));
            Assert.That(restored.OwnedCards.Count, Is.EqualTo(25));
            Assert.That(restored.Economy.Money, Is.EqualTo(125000L));
            Assert.That(restored.Economy.ScoutingPoints, Is.EqualTo(80));
            Assert.That(restored.Economy.DevelopmentPoints, Is.EqualTo(30));
            Assert.That(restored.Economy.PityGauge, Is.EqualTo(40));

            Assert.That(restored.TryGetOwnedCard("PS-000:Normal", out OwnedPlayerCardState owned), Is.True);
            Assert.That(owned.EnhancementLevel, Is.EqualTo(3));
            Assert.That(owned.DuplicateCount, Is.EqualTo(2));
            Assert.That(owned.IsLocked, Is.True);
            Assert.That(owned.Training.GetBonus(PlayerAbility.Contact), Is.EqualTo(2));
        }

        [TestCase(WorldRecordMode.OriginalHistory)]
        [TestCase(WorldRecordMode.SimulatedHistory)]
        public void Restore_WithSavedSnapshot_DoesNotRunHistoricalSimulation(WorldRecordMode mode)
        {
            FixtureData fixture = Fixture.Create(mode);
            ManagerHistoricalRuntimeState original = fixture.State;
            ManagerHistoricalSaveAdapter adapter = fixture.CreateAdapter();
            var service = new ManagerHistoricalLoadService(adapter);

            ManagerHistoricalRuntimeState restored = service.Restore(adapter.CreateSaveData(original));

            Assert.That(fixture.Provider.LoadCount, Is.EqualTo(1));
            Assert.That(restored.WorldHistory.RecordMode, Is.EqualTo(mode));
            Assert.That(restored.WorldHistory.WorldHistorySeed, Is.EqualTo(77123UL));
            ConstructorInfo constructor = typeof(ManagerHistoricalLoadService).GetConstructors()[0];
            Assert.That(constructor.GetParameters().Length, Is.EqualTo(1));
            Assert.That(constructor.GetParameters()[0].ParameterType, Is.EqualTo(typeof(ManagerHistoricalSaveAdapter)));
        }

        [Test]
        public void RuntimeState_ExposesOwnedEconomyOnlyForPlayerFranchise()
        {
            ManagerHistoricalRuntimeState state = Fixture.Create(WorldRecordMode.OriginalHistory).State;

            Assert.That(state.HasOwnedEconomy("TEAM-00"), Is.True);
            Assert.That(state.HasOwnedEconomy("TEAM-01"), Is.False);
        }

        [Test]
        public void CareerState_DoesNotOwnManagerCardEconomy()
        {
            PropertyInfo[] properties = typeof(CareerState).GetProperties(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            FieldInfo[] fields = typeof(CareerState).GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            for (int index = 0; index < properties.Length; index++)
            {
                Assert.That(properties[index].PropertyType, Is.Not.EqualTo(typeof(ManagerEconomyState)));
                Assert.That(properties[index].PropertyType, Is.Not.EqualTo(typeof(OwnedPlayerCardState)));
            }
            for (int index = 0; index < fields.Length; index++)
            {
                Assert.That(fields[index].FieldType, Is.Not.EqualTo(typeof(ManagerEconomyState)));
                Assert.That(fields[index].FieldType, Is.Not.EqualTo(typeof(OwnedPlayerCardState)));
            }
        }

        [Test]
        public void Restore_WithUnknownSaveVersion_IsRejected()
        {
            FixtureData fixture = Fixture.Create(WorldRecordMode.OriginalHistory);
            ManagerHistoricalSaveAdapter adapter = fixture.CreateAdapter();
            ManagerHistoricalSaveData save = adapter.CreateSaveData(fixture.State);
            save.saveVersion++;

            Assert.Throws<InvalidOperationException>(() => adapter.Restore(save));
        }

        [Test]
        public void Restore_WithDifferentContentHash_IsRejected()
        {
            FixtureData fixture = Fixture.Create(WorldRecordMode.OriginalHistory);
            ManagerHistoricalSaveAdapter adapter = fixture.CreateAdapter();
            ManagerHistoricalSaveData save = adapter.CreateSaveData(fixture.State);
            save.contentReference.contentHash = "damaged-content";

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => adapter.Restore(save));
            Assert.That(exception.Message, Does.Contain("ContentHash"));
        }

        [Test]
        public void Restore_WithUnknownHistoryPlayerSeason_IsRejected()
        {
            FixtureData fixture = Fixture.Create(WorldRecordMode.SimulatedHistory);
            ManagerHistoricalSaveAdapter adapter = fixture.CreateAdapter();
            ManagerHistoricalSaveData save = adapter.CreateSaveData(fixture.State);
            save.worldHistory.statistics[0].playerSeasonId = "PS-NOT-FOUND";

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => adapter.Restore(save));

            Assert.That(exception.Message, Does.Contain("World History"));
            Assert.That(exception.Message, Does.Contain("PS-NOT-FOUND"));
        }

        [Test]
        public void Restore_WithHistoryTeamDifferentFromPlayerOrigin_IsRejected()
        {
            FixtureData fixture = Fixture.Create(WorldRecordMode.SimulatedHistory);
            ManagerHistoricalSaveAdapter adapter = fixture.CreateAdapter();
            ManagerHistoricalSaveData save = adapter.CreateSaveData(fixture.State);
            save.worldHistory.statistics[0].teamSeasonKey = "TEAM-01";

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => adapter.Restore(save));

            Assert.That(exception.Message, Does.Contain("원소속"));
            Assert.That(exception.Message, Does.Contain("TEAM-01"));
        }

        [Test]
        public void Restore_WithUnknownHistoryTeamSeason_IsRejected()
        {
            FixtureData fixture = Fixture.Create(WorldRecordMode.SimulatedHistory);
            ManagerHistoricalSaveAdapter adapter = fixture.CreateAdapter();
            ManagerHistoricalSaveData save = adapter.CreateSaveData(fixture.State);
            save.worldHistory.statistics[0].teamSeasonKey = "TEAM-NOT-FOUND";

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => adapter.Restore(save));

            Assert.That(exception.Message, Does.Contain("TeamSeasonKey"));
            Assert.That(exception.Message, Does.Contain("TEAM-NOT-FOUND"));
        }

        [Test]
        public void Restore_WithHistoryYearDifferentFromBakedContent_IsRejected()
        {
            FixtureData fixture = Fixture.Create(WorldRecordMode.SimulatedHistory);
            ManagerHistoricalSaveAdapter adapter = fixture.CreateAdapter();
            ManagerHistoricalSaveData save = adapter.CreateSaveData(fixture.State);
            save.worldHistory.statistics[0].seasonYear = 2023;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => adapter.Restore(save));

            Assert.That(exception.Message, Does.Contain("SeasonYear"));
            Assert.That(exception.Message, Does.Contain("saved=2023"));
        }

        [Test]
        public void Restore_WithUnknownAwardPlayerSeason_IsRejected()
        {
            FixtureData fixture = Fixture.Create(WorldRecordMode.SimulatedHistory);
            ManagerHistoricalSaveAdapter adapter = fixture.CreateAdapter();
            ManagerHistoricalSaveData save = adapter.CreateSaveData(fixture.State);
            save.worldHistory.awards = new[]
            {
                new WorldAwardEntrySaveData
                {
                    seasonYear = 2024,
                    awardType = (int)WorldAwardType.AllStar,
                    playerSeasonId = "PS-NOT-FOUND",
                    position = (int)PlayerPosition.Catcher
                }
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => adapter.Restore(save));

            Assert.That(exception.Message, Does.Contain("World Award"));
            Assert.That(exception.Message, Does.Contain("PS-NOT-FOUND"));
        }

        [Test]
        public void Restore_WithAwardYearDifferentFromBakedPlayerSeason_IsRejected()
        {
            FixtureData fixture = Fixture.Create(WorldRecordMode.SimulatedHistory);
            ManagerHistoricalSaveAdapter adapter = fixture.CreateAdapter();
            ManagerHistoricalSaveData save = adapter.CreateSaveData(fixture.State);
            save.worldHistory.awards = new[]
            {
                new WorldAwardEntrySaveData
                {
                    seasonYear = 2023,
                    awardType = (int)WorldAwardType.AllStar,
                    playerSeasonId = "PS-000",
                    position = (int)PlayerPosition.Catcher
                }
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => adapter.Restore(save));

            Assert.That(exception.Message, Does.Contain("World Award"));
            Assert.That(exception.Message, Does.Contain("saved=2023"));
        }

        [Test]
        public void Restore_WithSpecialCompositeAssignments_PreservesRostersEditionsAndDoesNotResimulate()
        {
            FixtureData fixture = Fixture.Create(
                WorldRecordMode.SimulatedHistory,
                includeSpecialCompositeTeams: true);
            ManagerHistoricalSaveAdapter adapter = fixture.CreateAdapter();
            ManagerHistoricalSaveData save = adapter.CreateSaveData(fixture.State);
            var loadService = new ManagerHistoricalLoadService(adapter);

            ManagerHistoricalRuntimeState restored = loadService.Restore(save);

            Assert.That(fixture.Provider.LoadCount, Is.EqualTo(1));
            Assert.That(restored.League.RegularFranchiseTeamCount, Is.EqualTo(10));
            Assert.That(restored.League.SpecialCompositeTeams.Count, Is.EqualTo(3));
            Assert.That(restored.Rosters.Count, Is.EqualTo(13));
            var assignedPlayerSeasons = new HashSet<string>(StringComparer.Ordinal);
            for (int teamIndex = 0; teamIndex < restored.League.SpecialCompositeTeams.Count; teamIndex++)
            {
                SpecialCompositeTeamRegistration originalRegistration =
                    fixture.State.League.SpecialCompositeTeams[teamIndex];
                SpecialCompositeTeamRegistration registration = restored.League.SpecialCompositeTeams[teamIndex];
                CurrentRosterState originalRoster = fixture.State.GetRoster(registration.TeamSeasonKey);
                CurrentRosterState restoredRoster = restored.GetRoster(registration.TeamSeasonKey);
                PlayerCardEdition expectedEdition = GetCompositeEdition(registration.TeamType);

                Assert.That(registration.TeamType, Is.EqualTo(originalRegistration.TeamType));
                Assert.That(registration.TeamSeasonKey, Is.EqualTo(originalRegistration.TeamSeasonKey));
                Assert.That(registration.OriginYear, Is.EqualTo(originalRegistration.OriginYear));
                Assert.That(
                    registration.TeamSeasonKey,
                    Is.EqualTo(SpecialCompositeTeamDefinition.CreateStableTeamSeasonKey(
                        registration.OriginYear,
                        registration.TeamType)));
                Assert.That(restoredRoster.Entries.Count, Is.EqualTo(25));
                Assert.That(restoredRoster.Entries.Count, Is.EqualTo(originalRoster.Entries.Count));
                for (int rosterIndex = 0; rosterIndex < restoredRoster.Entries.Count; rosterIndex++)
                {
                    ActiveRosterEntry originalEntry = originalRoster.Entries[rosterIndex];
                    ActiveRosterEntry restoredEntry = restoredRoster.Entries[rosterIndex];
                    Assert.That(restoredEntry.PlayerSeasonId, Is.EqualTo(originalEntry.PlayerSeasonId));
                    Assert.That(restoredEntry.CardId, Is.EqualTo(originalEntry.CardId));
                    Assert.That(restoredEntry.Role, Is.EqualTo(originalEntry.Role));
                    Assert.That(assignedPlayerSeasons.Add(restoredEntry.PlayerSeasonId), Is.True);
                    Assert.That(restored.WorldCardCatalog.TryGetCard(restoredEntry.CardId, out PlayerCardDefinition card), Is.True);
                    Assert.That(card.Edition, Is.EqualTo(expectedEdition));
                }
            }
            Assert.That(assignedPlayerSeasons.Count, Is.EqualTo(75));
        }

        [Test]
        public void SaveData_DoesNotDuplicateBakedDefinitions()
        {
            FixtureData fixture = Fixture.Create(WorldRecordMode.SimulatedHistory);
            ManagerHistoricalSaveData save = fixture.CreateAdapter().CreateSaveData(fixture.State);

            Assert.That(typeof(ManagerHistoricalSaveData).GetField("worldCardCatalog"), Is.Null);
            Assert.That(typeof(ManagerHistoricalSaveData).GetField("playerSeasons"), Is.Null);
            Assert.That(typeof(ManagerHistoricalSaveData).GetField("playerCards"), Is.Null);
            Assert.That(save.contentReference.contentHash, Is.EqualTo("test-content-hash"));
        }

        [Test]
        public void CommonWorldHistorySaveData_DoesNotContainManagerCardEconomy()
        {
            Type[] forbiddenTypes =
            {
                typeof(OwnedPlayerCardState),
                typeof(ManagerEconomyState),
                typeof(CardTrainingState)
            };
            FieldInfo[] fields = typeof(WorldHistorySaveData).GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int fieldIndex = 0; fieldIndex < fields.Length; fieldIndex++)
            {
                for (int forbiddenIndex = 0; forbiddenIndex < forbiddenTypes.Length; forbiddenIndex++)
                    Assert.That(fields[fieldIndex].FieldType, Is.Not.EqualTo(forbiddenTypes[forbiddenIndex]));
            }
        }

        private static PlayerCardEdition GetCompositeEdition(SpecialCompositeTeamType teamType)
        {
            switch (teamType)
            {
                case SpecialCompositeTeamType.AllStarComposite:
                    return PlayerCardEdition.AllStar;
                case SpecialCompositeTeamType.GoldenGloveComposite:
                    return PlayerCardEdition.GoldenGlove;
                case SpecialCompositeTeamType.YearSelectComposite:
                    return PlayerCardEdition.Normal;
                default:
                    throw new ArgumentOutOfRangeException(nameof(teamType));
            }
        }

        private sealed class FixtureData
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

        private sealed class RecordingHistoricalContentProvider : IHistoricalContentProvider
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

        private static class Fixture
        {
            public static FixtureData Create(
                WorldRecordMode mode,
                bool includeSpecialCompositeTeams = false)
            {
                var persons = new List<PlayerPersonDefinition>(250);
                var seasons = new List<PlayerSeasonDefinition>(250);
                var cards = new List<PlayerCardDefinition>(250);
                var teamSeasons = new List<TeamSeasonDefinition>(10);
                var rosters = new List<CurrentRosterState>(13);
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
                            $"선수 {playerIndex:000}",
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
                                training[(int)PlayerAbility.Contact] = 2;
                                owned.Add(new OwnedPlayerCardState(cardId, 3, 2, true, true, new CardTrainingState(training)));
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
                SpecialCompositeTeamRegistration[] specialTeams = includeSpecialCompositeTeams
                    ? AddSpecialCompositeTeams(seasons, rosters, awards)
                    : Array.Empty<SpecialCompositeTeamRegistration>();

                var historyStatistics = new[]
                {
                    new SeasonStatistics(
                        "PS-000",
                        "TEAM-00",
                        2024,
                        PlayerPosition.Catcher,
                        plateAppearances: 500,
                        hits: 150,
                        homeRuns: 20,
                        walks: 40,
                        strikeouts: 80,
                        defensiveChances: 600,
                        defensiveOutsAboveAverage: 4,
                        fieldingErrors: 3)
                };
                var history = new WorldHistorySnapshot(
                    mode,
                    77123UL,
                    historyStatistics,
                    new WorldAwardRecord(awards));

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
                var provider = new RecordingHistoricalContentProvider(
                    new HistoricalBakedContent(manifest, persons, new[] { year }));
                WorldCardCatalog catalog = WorldCardCatalogBuilder.Build(
                    seasons,
                    history.Awards,
                    CardEditionBalanceTable.CreateInitial());
                var state = new ManagerHistoricalRuntimeState(
                    "TEAM-00",
                    HistoricalContentReference.FromManifest(manifest),
                    history,
                    catalog,
                    new LeagueInstance("LEAGUE-01", LeagueGrade.Rookie, teamKeys, specialTeams),
                    rosters,
                    owned,
                    new ManagerEconomyState(125000L, 80, 30, 40));
                return new FixtureData(state, provider);
            }

            private static SpecialCompositeTeamRegistration[] AddSpecialCompositeTeams(
                IReadOnlyList<PlayerSeasonDefinition> seasons,
                ICollection<CurrentRosterState> rosters,
                ICollection<WorldAwardEntry> awards)
            {
                SpecialCompositeTeamType[] teamTypes =
                {
                    SpecialCompositeTeamType.AllStarComposite,
                    SpecialCompositeTeamType.GoldenGloveComposite,
                    SpecialCompositeTeamType.YearSelectComposite
                };
                var definitions = new SpecialCompositeTeamDefinition[teamTypes.Length];
                for (int teamIndex = 0; teamIndex < teamTypes.Length; teamIndex++)
                {
                    SpecialCompositeTeamType teamType = teamTypes[teamIndex];
                    PlayerCardEdition edition = GetCompositeEdition(teamType);
                    var definitionEntries = new SpecialCompositeRosterEntry[25];
                    var rosterEntries = new ActiveRosterEntry[25];
                    int playerOffset = teamIndex * 25;
                    for (int rosterIndex = 0; rosterIndex < rosterEntries.Length; rosterIndex++)
                    {
                        PlayerSeasonDefinition season = seasons[playerOffset + rosterIndex];
                        ActiveRosterRole role = GetRole(rosterIndex);
                        string cardId = PlayerCardDefinition.CreateStableCardId(
                            season.PlayerSeasonId,
                            edition);
                        definitionEntries[rosterIndex] = new SpecialCompositeRosterEntry(
                            season.PlayerSeasonId,
                            cardId,
                            role);
                        rosterEntries[rosterIndex] = new ActiveRosterEntry(
                            cardId,
                            season.PlayerSeasonId,
                            season.PlayerPersonId,
                            season.RegistrationType,
                            role);

                        if (teamType == SpecialCompositeTeamType.AllStarComposite)
                        {
                            awards.Add(new WorldAwardEntry(
                                season.OriginYear,
                                WorldAwardType.AllStar,
                                season.PlayerSeasonId,
                                season.Position));
                        }
                        else if (teamType == SpecialCompositeTeamType.GoldenGloveComposite)
                        {
                            awards.Add(new WorldAwardEntry(
                                season.OriginYear,
                                WorldAwardType.GoldenGlove,
                                season.PlayerSeasonId,
                                season.Position));
                        }
                    }

                    definitions[teamIndex] = new SpecialCompositeTeamDefinition(
                        teamType,
                        2024,
                        definitionEntries);
                    rosters.Add(new CurrentRosterState(
                        definitions[teamIndex].TeamSeasonKey,
                        rosterEntries));
                }

                var set = new SpecialCompositeTeamSet(definitions);
                var registrations = new SpecialCompositeTeamRegistration[set.Teams.Count];
                for (int index = 0; index < registrations.Length; index++)
                {
                    SpecialCompositeTeamDefinition team = set.Teams[index];
                    registrations[index] = new SpecialCompositeTeamRegistration(
                        team.TeamSeasonKey,
                        team.OriginYear,
                        team.TeamType);
                }
                return registrations;
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
