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
        [TestCase(false)]
        [TestCase(true)]
        public void 슬로건선택여부와관계없이_UnityJson을거쳐실제진행을복원한다(bool hasSlogan)
        {
            var fixture = typeof(ManagerHistoricalSaveTests).GetNestedType("Fixture", System.Reflection.BindingFlags.NonPublic)
                .GetMethod("Create").Invoke(null, new object[] { Baseball.Core.Historical.WorldRecordMode.SimulatedHistory, false });
            var adapter = (ManagerHistoricalSaveAdapter)fixture.GetType().GetMethod("CreateAdapter").Invoke(fixture, null);
            var runtime = (ManagerHistoricalRuntimeState)fixture.GetType().GetProperty("State").GetValue(fixture);
            runtime = adapter.Restore(adapter.CreateSaveData(runtime));
            var source = adapter.CreateSaveData(runtime);
            // 간소화 Fixture에도 실제 새 게임과 같은 25장 지급 이력을 채워 JSON 전체 복원을 검증한다.
            source.newGameReceipt = new OwnerNewGameReceiptSaveData {
                mainCardIds = Array.ConvertAll(source.ownedCards, card => card.cardId), fillerCardIds = Array.Empty<string>() };
            if (hasSlogan)
            {
                source.playerGrowth.slogan = new Baseball.Core.Historical.OwnerSloganDefinition {
                    id = "contact", name = "정교한 야구", minimumAbility = 1,
                    cardsRequired = new[] { 1, 2, 3, 4, 5, 6 }, bonusByLevel = new[] { 1, 1, 2, 2, 3, 4 },
                    penaltyByLevel = new int[6] };
                source.playerGrowth.sloganLevel = 1;
                source.playerGrowth.sloganRevision = 1;
            }

            var deserialized = ManagerHistoricalSaveJsonStore.Deserialize(ManagerHistoricalSaveJsonStore.Serialize(source));
            var restored = adapter.Restore(deserialized);

            Assert.That(restored.PlayerGrowth.Slogan != null, Is.EqualTo(hasSlogan));
            if (hasSlogan)
                Assert.That(restored.PlayerGrowth.Slogan.Definition.GetMinimumAbility(1), Is.EqualTo(1));
            Assert.That(restored.OwnedCards.Count, Is.EqualTo(runtime.OwnedCards.Count));
            Assert.That(restored.Economy.Money, Is.EqualTo(runtime.Economy.Money));
        }

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
        public void 슬롯요약은기존저장필드를읽고전체기록은복원하지않는다()
        {
            string directory = Path.Combine(Path.GetTempPath(), "UPlayBall", Guid.NewGuid().ToString("N"));
            try
            {
                var store = new ManagerHistoricalSaveJsonStore(Path.Combine(directory, "owner.json"));
                var source = CreateSaveData();
                source.ownerProfile = new OwnerProfileSaveData { clubName = "테스트 구단", nickname = "구단주" };
                store.Save(source);
                var preview = store.LoadPreview();
                Assert.That(preview.saveVersion, Is.EqualTo(source.saveVersion));
                Assert.That(preview.playerTeamSeasonKey, Is.EqualTo(source.playerTeamSeasonKey));
                Assert.That(preview.ownerProfile.clubName, Is.EqualTo("테스트 구단"));
                Assert.That(preview.identityRegistry.franchises[0].displayName, Is.EqualTo("서울 코멧츠"));
                Assert.That(JsonUtility.ToJson(preview), Does.Not.Contain("worldHistory"));
                Assert.That(JsonUtility.ToJson(preview.identityRegistry), Does.Not.Contain("players"));
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
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

        [Test]
        public void Delete_RemovesExistingOwnerSave_AndIsIdempotent()
        {
            string directory = Path.Combine(Path.GetTempPath(), "UPlayBall", Guid.NewGuid().ToString("N"));
            string path = Path.Combine(directory, "manager_historical.json");
            try
            {
                var store = new ManagerHistoricalSaveJsonStore(path);
                store.Save(CreateSaveData());

                store.Delete();
                Assert.That(store.Exists, Is.False);

                Assert.DoesNotThrow(store.Delete);
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
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
