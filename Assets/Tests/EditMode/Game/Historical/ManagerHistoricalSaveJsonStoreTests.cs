using System;
using System.IO;
using Baseball.Game.Historical;
using Baseball.Game.Unity.Persistence;
using NUnit.Framework;
using UnityEngine;

namespace Baseball.Tests.EditMode.Game.Historical
{
    public sealed class ManagerHistoricalSaveJsonStoreTests
    {
        [Test]
        public void FileRoundTrip_PreservesWorldHistoryAndContentReferenceWithoutDefinitionCopy()
        {
            string directory = Path.Combine(Path.GetTempPath(), "UPlayBall", Guid.NewGuid().ToString("N"));
            string path = Path.Combine(directory, "manager_historical.json");
            try
            {
                var store = new ManagerHistoricalSaveJsonStore(path);
                ManagerHistoricalSaveData source = CreateSaveData();

                store.Save(source);
                ManagerHistoricalSaveData restored = store.Load();
                string json = File.ReadAllText(path);

                Assert.That(store.Exists, Is.True);
                Assert.That(restored.worldHistory.recordMode, Is.EqualTo(source.worldHistory.recordMode));
                Assert.That(restored.worldHistory.worldHistorySeed, Is.EqualTo(77123UL));
                Assert.That(restored.worldHistory.statistics.Length, Is.EqualTo(1));
                Assert.That(restored.worldHistory.teamStatistics[0].wins, Is.EqualTo(8));
                Assert.That(restored.worldHistory.standings[0].rank, Is.EqualTo(1));
                Assert.That(restored.worldHistory.postseasonResults[0].championTeamSeasonKey, Is.EqualTo("TEAM-00"));
                Assert.That(restored.contentReference.contentHash, Is.EqualTo("test-content-hash"));
                Assert.That(restored.identityRegistry.identityGeneratorVersion, Is.EqualTo("test-identity-v1"));
                Assert.That(restored.identityRegistry.identitySeed, Is.EqualTo(77123UL));
                Assert.That(restored.identityRegistry.players[0].displayName, Is.EqualTo("김도윤"));
                Assert.That(restored.identityRegistry.franchises[0].displayName, Is.EqualTo("서울 코멧츠"));
                Assert.That(json, Does.Contain("\"displayName\":\"김도윤\""));
                Assert.That(json, Does.Not.Contain("worldCardCatalog"));
                Assert.That(json, Does.Not.Contain("baseAttributes"));
                Assert.That(json, Does.Not.Contain("trainingCeiling"));
                Assert.That(json, Does.Not.Contain("editionStatModifiers"));
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        [Test]
        public void CommonWorldHistoryJson_DoesNotContainManagerCardEconomy()
        {
            string json = JsonUtility.ToJson(CreateSaveData().worldHistory);

            Assert.That(json, Does.Not.Contain("ownedCards"));
            Assert.That(json, Does.Not.Contain("enhancementLevel"));
            Assert.That(json, Does.Not.Contain("duplicateCount"));
            Assert.That(json, Does.Not.Contain("trainingBonuses"));
            Assert.That(json, Does.Not.Contain("pityGauge"));
        }

        private static ManagerHistoricalSaveData CreateSaveData()
        {
            return new ManagerHistoricalSaveData
            {
                saveVersion = ManagerHistoricalSaveAdapter.CurrentSaveVersion,
                contentReference = new HistoricalContentReferenceSaveData
                {
                    assetFormatVersion = 1,
                    contentSchemaVersion = 1,
                    assetArchiveHash = "test-archive-hash",
                    referenceDataVersion = "test-reference",
                    generatorVersion = "test-generator",
                    balanceVersion = "test-balance",
                    generationSeed = 20260901UL,
                    contentHash = "test-content-hash"
                },
                playerTeamSeasonKey = "TEAM-00",
                identityRegistry = new WorldIdentityRegistrySaveData
                {
                    identityGeneratorVersion = "test-identity-v1",
                    identitySeed = 77123UL,
                    players = new[]
                    {
                        new WorldPlayerIdentitySaveData
                        {
                            playerPersonId = "PP-000",
                            displayName = "김도윤"
                        }
                    },
                    franchises = new[]
                    {
                        new WorldFranchiseIdentitySaveData
                        {
                            franchiseId = "FRANCHISE-00",
                            displayName = "서울 코멧츠"
                        }
                    }
                },
                worldHistory = new WorldHistorySaveData
                {
                    recordMode = 1,
                    worldHistorySeed = 77123UL,
                    statistics = new[]
                    {
                        new SeasonStatisticsSaveData
                        {
                            playerSeasonId = "PS-000",
                            teamSeasonKey = "TEAM-00",
                            seasonYear = 2024,
                            position = 0,
                            plateAppearances = 500,
                            hits = 150
                        }
                    },
                    teamStatistics = new[]
                    {
                        new TeamSeasonStatisticsSaveData
                        {
                            teamSeasonKey = "TEAM-00",
                            seasonYear = 2024,
                            games = 10,
                            wins = 8,
                            losses = 2,
                            runsScored = 50,
                            runsAllowed = 30,
                            atBats = 300,
                            hits = 80,
                            pitchingOuts = 270,
                            earnedRuns = 20,
                            hitsAllowed = 70,
                            walksAllowed = 20
                        }
                    },
                    standings = new[]
                    {
                        new HistoricalStandingEntrySaveData
                        {
                            seasonYear = 2024,
                            rank = 1,
                            teamSeasonKey = "TEAM-00"
                        }
                    },
                    postseasonResults = new[]
                    {
                        new HistoricalPostseasonResultSaveData
                        {
                            seasonYear = 2024,
                            qualifiedTeamSeasonKeys = new[] { "TEAM-00" },
                            championTeamSeasonKey = "TEAM-00"
                        }
                    },
                    awards = Array.Empty<WorldAwardEntrySaveData>()
                },
                league = new LeagueInstanceSaveData
                {
                    leagueInstanceId = "LEAGUE-01",
                    grade = 0,
                    regularTeamSeasonKeys = new[] { "TEAM-00" },
                    specialCompositeTeams = Array.Empty<SpecialCompositeTeamRegistrationSaveData>()
                },
                rosters = Array.Empty<CurrentRosterSaveData>(),
                ownedCards = Array.Empty<OwnedPlayerCardSaveData>(),
                economy = new ManagerEconomySaveData()
            };
        }
    }
}
