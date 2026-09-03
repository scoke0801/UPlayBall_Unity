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
                Assert.That(restored.contentReference.contentHash, Is.EqualTo("test-content-hash"));
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
