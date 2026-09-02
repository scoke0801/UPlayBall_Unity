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
            ManagerHistoricalRuntimeState original = Fixture.Create(WorldRecordMode.SimulatedHistory);
            var adapter = new ManagerHistoricalSaveAdapter();

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
            ManagerHistoricalRuntimeState original = Fixture.Create(mode);
            var simulation = new CountingHistoricalSimulation();
            var historyInitializer = new WorldHistoryInitializer(
                simulation,
                new CountingAwardResolver(),
                new OriginalHistoryLoader());
            var adapter = new ManagerHistoricalSaveAdapter();
            var service = new ManagerHistoricalLoadService(adapter, historyInitializer);

            ManagerHistoricalRuntimeState restored = service.Restore(adapter.CreateSaveData(original));

            Assert.That(simulation.CallCount, Is.Zero);
            Assert.That(restored.WorldHistory.RecordMode, Is.EqualTo(mode));
            Assert.That(restored.WorldHistory.WorldHistorySeed, Is.EqualTo(77123UL));
        }

        [Test]
        public void RuntimeState_ExposesOwnedEconomyOnlyForPlayerFranchise()
        {
            ManagerHistoricalRuntimeState state = Fixture.Create(WorldRecordMode.OriginalHistory);

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
            var adapter = new ManagerHistoricalSaveAdapter();
            ManagerHistoricalSaveData save = adapter.CreateSaveData(Fixture.Create(WorldRecordMode.OriginalHistory));
            save.saveVersion++;

            Assert.Throws<InvalidOperationException>(() => adapter.Restore(save));
        }

        private sealed class CountingHistoricalSimulation : IHistoricalSeasonSimulation
        {
            public int CallCount { get; private set; }

            public IReadOnlyList<SeasonStatistics> Simulate(
                ulong worldHistorySeed,
                IReadOnlyList<TeamSeasonDefinition> regularFranchiseTeams)
            {
                CallCount++;
                throw new AssertionException("저장된 Snapshot 로드 중 Historical Simulation이 호출되었습니다.");
            }
        }

        private sealed class CountingAwardResolver : ISeasonAwardResolver
        {
            public WorldAwardRecord Resolve(IReadOnlyList<SeasonStatistics> statistics)
            {
                throw new AssertionException("저장된 Snapshot 로드 중 Award Resolver가 호출되었습니다.");
            }
        }

        private static class Fixture
        {
            public static ManagerHistoricalRuntimeState Create(WorldRecordMode mode)
            {
                var seasons = new List<PlayerSeasonDefinition>(250);
                var cards = new List<PlayerCardDefinition>(250);
                var rosters = new List<CurrentRosterState>(10);
                var owned = new List<OwnedPlayerCardState>(25);
                var teamKeys = new string[10];
                var zeroModifiers = new int[PlayerAbilityCatalog.AbilityCount];

                for (int teamIndex = 0; teamIndex < 10; teamIndex++)
                {
                    string teamKey = $"TEAM-{teamIndex:00}";
                    teamKeys[teamIndex] = teamKey;
                    var entries = new List<ActiveRosterEntry>(25);
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
                    rosters.Add(new CurrentRosterState(teamKey, entries));
                }

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
                    new WorldAwardRecord(Array.Empty<WorldAwardEntry>()));

                return new ManagerHistoricalRuntimeState(
                    "TEAM-00",
                    history,
                    new WorldCardCatalog(seasons, cards),
                    new LeagueInstance("LEAGUE-01", LeagueGrade.Rookie, teamKeys),
                    rosters,
                    owned,
                    new ManagerEconomyState(125000L, 80, 30, 40));
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
