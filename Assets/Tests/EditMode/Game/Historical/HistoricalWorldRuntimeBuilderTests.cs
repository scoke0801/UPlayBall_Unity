using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Historical;
using Baseball.Simulation.Historical;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game.Historical
{
    public sealed class HistoricalWorldRuntimeBuilderTests
    {
        [Test]
        public void LegacyBuilder_OriginalHistory는Simulation을실행하지않는다()
        {
            HistoricalBakedContent content = Fixture.CreateContent();
            var simulation = new RecordingSeasonSimulation();

            HistoricalWorldRuntimeContent result = CreateBuilder(simulation).Build(
                content,
                WorldRecordMode.OriginalHistory,
                701UL);

            Assert.That(simulation.CallCount, Is.Zero);
            Assert.That(result.WorldHistory.RecordMode, Is.EqualTo(WorldRecordMode.OriginalHistory));
            Assert.That(result.WorldHistory.Statistics.Count, Is.EqualTo(content.OriginalSeasonRecords.Count));
            Assert.That(result.WorldAwardRecord.Entries.Count, Is.EqualTo(content.OriginalAwardRecords.Count));
        }

        [Test]
        public void ProductionNewGame_OriginalHistory를거부한다()
        {
            HistoricalBakedContent content = Fixture.CreateContent();
            var provider = new RecordingContentProvider(content);
            var simulation = new RecordingSeasonSimulation();
            var service = new ManagerHistoricalNewGameService(
                provider,
                CreateBuilder(simulation));
            string playerTeamSeasonKey = content.Years[0].TeamSeasons[0].TeamSeasonKey;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => service.Create(
                new ManagerHistoricalNewGameRequest(
                    WorldRecordMode.OriginalHistory,
                    700UL,
                    content.Years[0].Year,
                    "HISTORICAL-2024-ROOKIE",
                    playerTeamSeasonKey,
                    new ManagerEconomyState(1_000_000L, 100, 50))));

            Assert.That(simulation.CallCount, Is.Zero);
            Assert.That(provider.LoadCount, Is.Zero);
            Assert.That(exception.Message, Does.Contain("Legacy"));
        }

        [Test]
        public void SimulatedHistory_NewGame은한번시뮬레이션하고Load는저장결과만복원한다()
        {
            HistoricalBakedContent content = Fixture.CreateContent();
            var provider = new RecordingContentProvider(content);
            var simulation = new RecordingSeasonSimulation();
            var service = new ManagerHistoricalNewGameService(
                provider,
                CreateBuilder(simulation));
            string playerTeamSeasonKey = content.Years[0].TeamSeasons[0].TeamSeasonKey;

            ManagerHistoricalRuntimeState created = service.Create(
                new ManagerHistoricalNewGameRequest(
                    WorldRecordMode.SimulatedHistory,
                    709UL,
                    content.Years[0].Year,
                    "HISTORICAL-2024-ROOKIE",
                    playerTeamSeasonKey,
                    new ManagerEconomyState()));

            Assert.That(simulation.CallCount, Is.EqualTo(1));
            var adapter = new ManagerHistoricalSaveAdapter(
                provider,
                CardEditionBalanceTable.CreateInitial());
            ManagerHistoricalSaveData saveData = adapter.CreateSaveData(created);
            ManagerHistoricalRuntimeState restored = new ManagerHistoricalLoadService(adapter)
                .Restore(saveData);

            Assert.That(simulation.CallCount, Is.EqualTo(1));
            Assert.That(restored.WorldHistory.RecordMode, Is.EqualTo(WorldRecordMode.SimulatedHistory));
            Assert.That(restored.WorldHistory.WorldHistorySeed, Is.EqualTo(709UL));
            Assert.That(restored.WorldHistory.Statistics.Count, Is.EqualTo(created.WorldHistory.Statistics.Count));
            Assert.That(restored.WorldHistory.TeamStatistics.Count, Is.EqualTo(created.WorldHistory.TeamStatistics.Count));
            Assert.That(restored.WorldHistory.Standings.Count, Is.EqualTo(created.WorldHistory.Standings.Count));
            Assert.That(restored.WorldHistory.PostseasonResults.Count, Is.EqualTo(created.WorldHistory.PostseasonResults.Count));
            Assert.That(
                restored.WorldHistory.PostseasonResults[0].ChampionTeamSeasonKey,
                Is.EqualTo(created.WorldHistory.PostseasonResults[0].ChampionTeamSeasonKey));
            Assert.That(restored.WorldAwardRecord.Entries.Count, Is.EqualTo(created.WorldAwardRecord.Entries.Count));
        }

        [Test]
        public void SimulatedHistory_UsesBakedRegularTeamsOnly()
        {
            HistoricalBakedContent content = Fixture.CreateContent();
            var simulation = new RecordingSeasonSimulation();

            HistoricalWorldRuntimeContent result = CreateBuilder(simulation).Build(
                content,
                WorldRecordMode.SimulatedHistory,
                702UL);

            Assert.That(simulation.CallCount, Is.EqualTo(content.Years.Count));
            Assert.That(simulation.ReceivedTeamKeys.Count, Is.EqualTo(10));
            for (int index = 0; index < content.Years[0].TeamSeasons.Count; index++)
            {
                Assert.That(
                    simulation.ReceivedTeamKeys,
                    Does.Contain(content.Years[0].TeamSeasons[index].TeamSeasonKey));
            }
            Assert.That(result.WorldHistory.RecordMode, Is.EqualTo(WorldRecordMode.SimulatedHistory));
            Assert.That(result.WorldHistory.TeamStatistics.Count, Is.EqualTo(10));
            Assert.That(result.WorldHistory.Standings.Count, Is.EqualTo(10));
            Assert.That(result.WorldHistory.PostseasonResults.Count, Is.EqualTo(1));
        }

        [Test]
        public void SimulatedHistory_DoesNotCopyOriginalAwards()
        {
            HistoricalBakedContent content = Fixture.CreateContent();
            string originalOnlyPlayer = content.PlayerSeasons[content.PlayerSeasons.Count - 1].PlayerSeasonId;

            HistoricalWorldRuntimeContent result = CreateBuilder(new RecordingSeasonSimulation()).Build(
                content,
                WorldRecordMode.SimulatedHistory,
                703UL);

            Assert.That(
                ContainsAward(result.WorldAwardRecord, WorldAwardType.PostseasonMvp, originalOnlyPlayer),
                Is.False);
            Assert.That(
                ContainsOriginalAward(content, WorldAwardType.PostseasonMvp, originalOnlyPlayer),
                Is.True);
        }

        [Test]
        public void SimulatedHistory_SameSeed_IsDeterministic()
        {
            HistoricalBakedContent content = Fixture.CreateContent();
            HistoricalWorldRuntimeContent first = CreateBuilder(new RecordingSeasonSimulation()).Build(
                content,
                WorldRecordMode.SimulatedHistory,
                704UL);
            HistoricalWorldRuntimeContent second = CreateBuilder(new RecordingSeasonSimulation()).Build(
                content,
                WorldRecordMode.SimulatedHistory,
                704UL);

            Assert.That(
                HistoricalWorldResultHasher.Compute(second),
                Is.EqualTo(HistoricalWorldResultHasher.Compute(first)));
            Assert.That(
                HistoricalWorldResultHasher.ComputeFingerprints(second).HistoryHash,
                Is.EqualTo(HistoricalWorldResultHasher.ComputeFingerprints(first).HistoryHash));
        }

        [Test]
        public void ResultHasher_Seed만다르고산출물이같으면HistoryHash는같다()
        {
            HistoricalBakedContent content = Fixture.CreateContent();
            var builder = CreateBuilder(new SeedIgnoringSeasonSimulation());
            HistoricalWorldRuntimeContent first = builder.Build(
                content,
                WorldRecordMode.SimulatedHistory,
                704UL);
            HistoricalWorldRuntimeContent second = builder.Build(
                content,
                WorldRecordMode.SimulatedHistory,
                705UL);

            Assert.That(HistoricalWorldResultHasher.Compute(second), Is.Not.EqualTo(
                HistoricalWorldResultHasher.Compute(first)), "전체 Hash는 Seed 자체를 포함한다.");
            Assert.That(
                HistoricalWorldResultHasher.ComputeFingerprints(second).HistoryHash,
                Is.EqualTo(HistoricalWorldResultHasher.ComputeFingerprints(first).HistoryHash),
                "Seed 제외 History Hash는 실제 산출물만 비교해야 한다.");
        }

        [Test]
        public void SimulatedHistory_DifferentSeed_PreservesBakedIdentityAndChangesDerivedStatistics()
        {
            HistoricalBakedContent content = Fixture.CreateContent();
            PlayerSeasonDefinition before = content.PlayerSeasons[0];
            HistoricalWorldRuntimeContent first = CreateBuilder(new RecordingSeasonSimulation()).Build(
                content,
                WorldRecordMode.SimulatedHistory,
                705UL);
            HistoricalWorldRuntimeContent second = CreateBuilder(new RecordingSeasonSimulation()).Build(
                content,
                WorldRecordMode.SimulatedHistory,
                706UL);

            PlayerSeasonDefinition after = content.PlayerSeasons[0];
            Assert.That(after, Is.SameAs(before));
            Assert.That(after.PlayerSeasonId, Is.EqualTo(before.PlayerSeasonId));
            Assert.That(after.PlayerPersonId, Is.EqualTo(before.PlayerPersonId));
            Assert.That(after.OriginFranchiseId, Is.EqualTo(before.OriginFranchiseId));
            Assert.That(after.OriginTeamSeasonKey, Is.EqualTo(before.OriginTeamSeasonKey));
            Assert.That(after.Cost, Is.EqualTo(before.Cost));
            Assert.That(
                first.WorldHistory.Statistics[0].Hits,
                Is.Not.EqualTo(second.WorldHistory.Statistics[0].Hits));
            Assert.That(
                first.WorldHistory.PostseasonResults[0].ChampionTeamSeasonKey,
                Is.Not.EqualTo(second.WorldHistory.PostseasonResults[0].ChampionTeamSeasonKey));
        }

        [Test]
        public void SpecialComposite_IsCreatedAfterAwardsWithoutOverlapOrOriginMutation()
        {
            HistoricalBakedContent content = Fixture.CreateContent();
            string[] originKeys = CopyOriginKeys(content.PlayerSeasons);

            HistoricalWorldRuntimeContent result = CreateBuilder(new RecordingSeasonSimulation()).Build(
                content,
                WorldRecordMode.SimulatedHistory,
                707UL);

            Assert.That(result.WorldAwardRecord.Entries.Count, Is.GreaterThan(0));
            Assert.That(result.SpecialCompositeTeams.Count, Is.EqualTo(content.Years.Count));
            var assigned = new HashSet<string>(StringComparer.Ordinal);
            int allStarEditionCount = 0;
            int goldenGloveEditionCount = 0;
            for (int setIndex = 0; setIndex < result.SpecialCompositeTeams.Count; setIndex++)
            {
                SpecialCompositeTeamSet set = result.SpecialCompositeTeams[setIndex];
                Assert.That(set.Teams.Count, Is.EqualTo(3));
                for (int teamIndex = 0; teamIndex < set.Teams.Count; teamIndex++)
                {
                    SpecialCompositeTeamDefinition team = set.Teams[teamIndex];
                    Assert.That(team.Roster.Count, Is.EqualTo(25));
                    for (int rosterIndex = 0; rosterIndex < team.Roster.Count; rosterIndex++)
                    {
                        SpecialCompositeRosterEntry entry = team.Roster[rosterIndex];
                        Assert.That(assigned.Add(entry.PlayerSeasonId), Is.True);
                        Assert.That(
                            result.WorldCardCatalog.TryGetCard(entry.CardId, out PlayerCardDefinition card),
                            Is.True);
                        Assert.That(card.PlayerSeasonId, Is.EqualTo(entry.PlayerSeasonId));
                        PlayerCardEdition expectedEdition = GetExpectedCompositeEdition(
                            team.TeamType,
                            entry.PlayerSeasonId,
                            result.WorldCardCatalog);
                        Assert.That(card.Edition, Is.EqualTo(expectedEdition));
                        if (card.Edition == PlayerCardEdition.AllStar) allStarEditionCount++;
                        if (card.Edition == PlayerCardEdition.GoldenGlove) goldenGloveEditionCount++;
                    }
                }
            }
            Assert.That(allStarEditionCount, Is.GreaterThan(0));
            Assert.That(goldenGloveEditionCount, Is.GreaterThan(0));
            for (int index = 0; index < content.PlayerSeasons.Count; index++)
                Assert.That(content.PlayerSeasons[index].OriginTeamSeasonKey, Is.EqualTo(originKeys[index]));
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
            string cardId = PlayerCardDefinition.CreateStableCardId(playerSeasonId, preferred);
            return catalog.TryGetCard(cardId, out _) ? preferred : PlayerCardEdition.Normal;
        }

        [Test]
        [Timeout(120000)]
        public void LongValidationHarness_OneYearOneSeed_CompletesDetailedWorld()
        {
            HistoricalWorldValidationReport report = HistoricalWorldLongValidationHarness.Run(
                Fixture.CreateContent(),
                BalanceTable.CreateDefault(),
                new[]
                {
                    new HistoricalWorldValidationSeed(708UL, 708UL),
                    new HistoricalWorldValidationSeed(708UL, 708UL),
                    new HistoricalWorldValidationSeed(708UL, 709UL)
                });

            Assert.That(report.Runs.Count, Is.EqualTo(3));
            HistoricalWorldValidationRun run = report.Runs[0];
            Assert.That(report.Runs[1].ResultHash, Is.EqualTo(run.ResultHash));
            Assert.That(report.Runs[1].Fingerprints.HistoryHash, Is.EqualTo(run.Fingerprints.HistoryHash));
            Assert.That(report.Runs[2].Fingerprints.IdentityHash, Is.Not.EqualTo(run.Fingerprints.IdentityHash));
            Assert.That(report.Runs[2].Fingerprints.HistoryHash, Is.EqualTo(run.Fingerprints.HistoryHash));
            Assert.That(report.Runs[0].IsSaveRoundTripStable, Is.True);
            Assert.That(report.Runs[1].IsSaveRoundTripStable, Is.True);
            Assert.That(report.Runs[2].IsSaveRoundTripStable, Is.True);
            Assert.That(run.ResultHash, Has.Length.EqualTo(16));
            Assert.That(run.Metrics.Seasons.Count, Is.EqualTo(1));
            Assert.That(
                run.Metrics.Seasons[0].RegularSeasonGameCount,
                Is.EqualTo(BakedHistoricalDetailedSeasonSource.RegularSeasonGamesPerTeam * 5));
            Assert.That(run.Metrics.Seasons[0].AllStarGameCount, Is.EqualTo(1));
            Assert.That(run.Metrics.Seasons[0].PostseasonGameCount, Is.GreaterThan(0));
            Assert.That(run.Metrics.TotalElapsedTicks, Is.GreaterThan(0L));
            Assert.That(run.ReplacementAwards.ReplacementPlayerSeasonCount, Is.Zero);
            Assert.That(run.ReplacementAwards.AllStarCount, Is.EqualTo(25));
            Assert.That(run.ReplacementAwards.GoldenGloveCount, Is.EqualTo(10));
            Assert.That(run.ReplacementAwards.MvpCount, Is.EqualTo(3));
            Console.WriteLine(
                $"HistoricalWorldSmoke Year=2024 Games={run.Metrics.TotalGameCount} " +
                $"ElapsedMs={run.Metrics.TotalElapsedMilliseconds:F1} " +
                $"MsPerGame={run.Metrics.MillisecondsPerGame:F3} " +
                $"AllocatedBytes={run.Metrics.AllocatedBytes} Hash={run.ResultHash} " +
                $"RepeatHash={report.Runs[1].ResultHash}");
        }

        [Test]
        [Timeout(120000)]
        public void DetailedSimulation_DifferentWorldSeed_ChangesSeasonStatistics()
        {
            HistoricalBakedContent content = Fixture.CreateContent();
            var builder = new HistoricalWorldRuntimeBuilder(BalanceTable.CreateDefault());
            HistoricalWorldRuntimeContent first = builder.Build(
                content,
                WorldRecordMode.SimulatedHistory,
                810UL);
            HistoricalWorldRuntimeContent second = builder.Build(
                content,
                WorldRecordMode.SimulatedHistory,
                811UL);

            bool hasDifferentStatistics = false;
            for (int index = 0; index < first.WorldHistory.Statistics.Count; index++)
            {
                SeasonStatistics left = first.WorldHistory.Statistics[index];
                SeasonStatistics right = second.WorldHistory.Statistics[index];
                if (left.Hits != right.Hits ||
                    left.HomeRuns != right.HomeRuns ||
                    left.Walks != right.Walks ||
                    left.Strikeouts != right.Strikeouts ||
                    left.EarnedRuns != right.EarnedRuns)
                {
                    hasDifferentStatistics = true;
                    break;
                }
            }

            Assert.That(hasDifferentStatistics, Is.True);
        }

        [Test]
        public void BakedHistory_실제시뮬레이션과같은결과를복원하고시뮬레이션을생략한다()
        {
            HistoricalBakedContent content = Fixture.CreateContent();
            const ulong seed = 4_242UL;
            HistoricalWorldRuntimeContent simulated = CreateBuilder(new RecordingSeasonSimulation()).Build(
                content,
                WorldRecordMode.SimulatedHistory,
                seed);
            var source = new StubBakedWorldHistorySource(CreateBakedBytes(content, simulated.WorldHistory, seed));
            var replaySimulation = new RecordingSeasonSimulation();

            HistoricalWorldRuntimeContent restored = new HistoricalWorldRuntimeBuilder(
                BalanceTable.CreateDefault(),
                simulationOverride: replaySimulation,
                bakedHistorySource: source).Build(content, WorldRecordMode.SimulatedHistory, seed);

            Assert.That(replaySimulation.CallCount, Is.Zero, "Bake가 적중하면 시즌을 다시 돌리지 않아야 한다.");
            Assert.That(restored.Metrics.IsHistoryRestoredFromBake, Is.True);
            AssertSameHistory(simulated.WorldHistory, restored.WorldHistory);
        }

        [Test]
        public void BakedHistory_Seed가다르면Bake를무시하고실제로시뮬레이션한다()
        {
            HistoricalBakedContent content = Fixture.CreateContent();
            const ulong bakedSeed = 4_242UL;
            HistoricalWorldRuntimeContent baked = CreateBuilder(new RecordingSeasonSimulation()).Build(
                content,
                WorldRecordMode.SimulatedHistory,
                bakedSeed);
            var source = new StubBakedWorldHistorySource(
                CreateBakedBytes(content, baked.WorldHistory, bakedSeed));
            var replaySimulation = new RecordingSeasonSimulation();

            HistoricalWorldRuntimeContent other = new HistoricalWorldRuntimeBuilder(
                BalanceTable.CreateDefault(),
                simulationOverride: replaySimulation,
                bakedHistorySource: source).Build(content, WorldRecordMode.SimulatedHistory, bakedSeed + 1UL);

            Assert.That(replaySimulation.CallCount, Is.EqualTo(content.Years.Count));
            Assert.That(other.Metrics.IsHistoryRestoredFromBake, Is.False);
        }

        [Test]
        public void BakedHistory_Balance가바뀌면Bake를무시한다()
        {
            HistoricalBakedContent content = Fixture.CreateContent();
            const ulong seed = 4_242UL;
            HistoricalWorldRuntimeContent baked = CreateBuilder(new RecordingSeasonSimulation()).Build(
                content,
                WorldRecordMode.SimulatedHistory,
                seed);
            var source = new StubBakedWorldHistorySource(CreateBakedBytes(content, baked.WorldHistory, seed));
            var replaySimulation = new RecordingSeasonSimulation();

            HistoricalWorldRuntimeContent result = new HistoricalWorldRuntimeBuilder(
                CreateBalanceWithDifferentContentHash(),
                simulationOverride: replaySimulation,
                bakedHistorySource: source).Build(content, WorldRecordMode.SimulatedHistory, seed);

            Assert.That(replaySimulation.CallCount, Is.EqualTo(content.Years.Count));
            Assert.That(result.Metrics.IsHistoryRestoredFromBake, Is.False);
        }

        /// <summary>Bake Key에 들어가는 Balance 식별자만 바꾼 표를 만든다.</summary>
        private static BalanceTable CreateBalanceWithDifferentContentHash()
        {
            BalanceTable source = BalanceTable.CreateDefault();
            return new BalanceTable(
                source.Version + 1,
                source.PlateDiscipline,
                source.BattedBall,
                source.BaseRunning,
                source.ContractOffer,
                source.TeamGeneration,
                source.PlayerEvaluation,
                source.CareerSeason,
                contentHash: "changed-balance-content");
        }

        [Test]
        public void GetOrBuild_같은Content와Seed면World를다시만들지않는다()
        {
            HistoricalBakedContent content = Fixture.CreateContent();
            var simulation = new RecordingSeasonSimulation();
            HistoricalWorldRuntimeBuilder builder = CreateBuilder(simulation);

            HistoricalWorldRuntimeContent first = builder.GetOrBuild(
                content,
                WorldRecordMode.SimulatedHistory,
                777UL);
            HistoricalWorldRuntimeContent second = builder.GetOrBuild(
                content,
                WorldRecordMode.SimulatedHistory,
                777UL);

            Assert.That(second, Is.SameAs(first));
            Assert.That(simulation.CallCount, Is.EqualTo(content.Years.Count));
        }

        [Test]
        public void GetOrBuild_Seed가다르면새World를만든다()
        {
            HistoricalBakedContent content = Fixture.CreateContent();
            var simulation = new RecordingSeasonSimulation();
            HistoricalWorldRuntimeBuilder builder = CreateBuilder(simulation);

            HistoricalWorldRuntimeContent first = builder.GetOrBuild(
                content,
                WorldRecordMode.SimulatedHistory,
                777UL);
            HistoricalWorldRuntimeContent second = builder.GetOrBuild(
                content,
                WorldRecordMode.SimulatedHistory,
                778UL);

            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(simulation.CallCount, Is.EqualTo(content.Years.Count * 2));
        }

        private static byte[] CreateBakedBytes(
            HistoricalBakedContent content,
            WorldHistorySnapshot history,
            ulong worldHistorySeed)
        {
            BakedWorldHistoryKey key = CreateBuilder(new RecordingSeasonSimulation())
                .CreateBakeKey(content, worldHistorySeed);
            return WorldHistoryBakeCodec.Encode(new BakedWorldHistoryPayload(
                key,
                new WorldHistorySaveMapper().CreateSaveData(history)));
        }

        private static void AssertSameHistory(WorldHistorySnapshot expected, WorldHistorySnapshot actual)
        {
            Assert.That(actual.RecordMode, Is.EqualTo(expected.RecordMode));
            Assert.That(actual.WorldHistorySeed, Is.EqualTo(expected.WorldHistorySeed));
            Assert.That(actual.Statistics.Count, Is.EqualTo(expected.Statistics.Count));
            for (int index = 0; index < expected.Statistics.Count; index++)
            {
                SeasonStatistics left = expected.Statistics[index];
                SeasonStatistics right = actual.Statistics[index];
                Assert.That(right.PlayerSeasonId, Is.EqualTo(left.PlayerSeasonId));
                Assert.That(right.TeamSeasonKey, Is.EqualTo(left.TeamSeasonKey));
                Assert.That(right.SeasonYear, Is.EqualTo(left.SeasonYear));
                Assert.That(right.PlateAppearances, Is.EqualTo(left.PlateAppearances));
                Assert.That(right.Hits, Is.EqualTo(left.Hits));
                Assert.That(right.HomeRuns, Is.EqualTo(left.HomeRuns));
                Assert.That(right.EarnedRuns, Is.EqualTo(left.EarnedRuns));
                Assert.That(right.PitchingOuts, Is.EqualTo(left.PitchingOuts));
            }

            Assert.That(actual.TeamStatistics.Count, Is.EqualTo(expected.TeamStatistics.Count));
            for (int index = 0; index < expected.TeamStatistics.Count; index++)
            {
                Assert.That(actual.TeamStatistics[index].Wins, Is.EqualTo(expected.TeamStatistics[index].Wins));
                Assert.That(actual.TeamStatistics[index].Losses, Is.EqualTo(expected.TeamStatistics[index].Losses));
            }

            Assert.That(actual.Standings.Count, Is.EqualTo(expected.Standings.Count));
            for (int index = 0; index < expected.Standings.Count; index++)
            {
                Assert.That(actual.Standings[index].Rank, Is.EqualTo(expected.Standings[index].Rank));
                Assert.That(
                    actual.Standings[index].TeamSeasonKey,
                    Is.EqualTo(expected.Standings[index].TeamSeasonKey));
            }

            Assert.That(actual.PostseasonResults.Count, Is.EqualTo(expected.PostseasonResults.Count));
            for (int index = 0; index < expected.PostseasonResults.Count; index++)
            {
                Assert.That(
                    actual.PostseasonResults[index].ChampionTeamSeasonKey,
                    Is.EqualTo(expected.PostseasonResults[index].ChampionTeamSeasonKey));
            }

            Assert.That(actual.Awards.Entries.Count, Is.EqualTo(expected.Awards.Entries.Count));
            for (int index = 0; index < expected.Awards.Entries.Count; index++)
            {
                Assert.That(
                    actual.Awards.Entries[index].PlayerSeasonId,
                    Is.EqualTo(expected.Awards.Entries[index].PlayerSeasonId));
                Assert.That(
                    actual.Awards.Entries[index].AwardType,
                    Is.EqualTo(expected.Awards.Entries[index].AwardType));
            }
        }

        /// <summary>Key가 맞을 때만 구운 결과를 내주는 최소 구현이다.</summary>
        private sealed class StubBakedWorldHistorySource : IBakedWorldHistorySource
        {
            private readonly byte[] _bytes;

            public StubBakedWorldHistorySource(byte[] bytes)
            {
                _bytes = bytes;
            }

            public bool TryLoad(BakedWorldHistoryKey key, out WorldHistorySnapshot snapshot)
            {
                snapshot = null;
                if (!WorldHistoryBakeCodec.TryPeekKey(_bytes, out BakedWorldHistoryKey candidate) ||
                    !candidate.Equals(key))
                {
                    return false;
                }
                snapshot = new WorldHistorySaveMapper().Restore(WorldHistoryBakeCodec.Decode(_bytes).History);
                return true;
            }
        }

        private static HistoricalWorldRuntimeBuilder CreateBuilder(IHistoricalSeasonSimulation simulation)
        {
            return new HistoricalWorldRuntimeBuilder(
                BalanceTable.CreateDefault(),
                simulationOverride: simulation);
        }

        private static bool ContainsAward(
            WorldAwardRecord awards,
            WorldAwardType awardType,
            string playerSeasonId)
        {
            for (int index = 0; index < awards.Entries.Count; index++)
            {
                WorldAwardEntry award = awards.Entries[index];
                if (award.AwardType == awardType && award.PlayerSeasonId == playerSeasonId)
                    return true;
            }
            return false;
        }

        private static bool ContainsOriginalAward(
            HistoricalBakedContent content,
            WorldAwardType awardType,
            string playerSeasonId)
        {
            for (int index = 0; index < content.OriginalAwardRecords.Count; index++)
            {
                WorldAwardEntry award = content.OriginalAwardRecords[index].Award;
                if (award.AwardType == awardType && award.PlayerSeasonId == playerSeasonId)
                    return true;
            }
            return false;
        }

        private static string[] CopyOriginKeys(IReadOnlyList<PlayerSeasonDefinition> seasons)
        {
            var result = new string[seasons.Count];
            for (int index = 0; index < result.Length; index++)
                result[index] = seasons[index].OriginTeamSeasonKey;
            return result;
        }

        private sealed class RecordingSeasonSimulation : IHistoricalSeasonSimulation
        {
            public int CallCount { get; private set; }
            public IReadOnlyList<string> ReceivedTeamKeys { get; private set; } = Array.Empty<string>();

            public HistoricalSeasonSimulationResult Simulate(
                ulong worldHistorySeed,
                IReadOnlyList<TeamSeasonDefinition> regularFranchiseTeams)
            {
                CallCount++;
                var teamKeys = new string[regularFranchiseTeams.Count];
                for (int index = 0; index < regularFranchiseTeams.Count; index++)
                {
                    string key = regularFranchiseTeams[index].TeamSeasonKey;
                    if (key.IndexOf("COMPOSITE", StringComparison.OrdinalIgnoreCase) >= 0)
                        throw new AssertionException("특수 합성팀이 최초 Historical Simulation에 들어왔습니다.");
                    teamKeys[index] = key;
                }
                ReceivedTeamKeys = teamKeys;
                return Fixture.CreateSimulationResult(regularFranchiseTeams, worldHistorySeed);
            }
        }

        private sealed class SeedIgnoringSeasonSimulation : IHistoricalSeasonSimulation
        {
            public HistoricalSeasonSimulationResult Simulate(
                ulong worldHistorySeed,
                IReadOnlyList<TeamSeasonDefinition> regularFranchiseTeams)
            {
                return Fixture.CreateSimulationResult(regularFranchiseTeams, 999UL);
            }
        }

        private sealed class RecordingContentProvider : IHistoricalContentProvider
        {
            private readonly HistoricalBakedContent _content;

            public RecordingContentProvider(HistoricalBakedContent content)
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
            private const int Year = 2024;

            public static HistoricalBakedContent CreateContent()
            {
                var persons = new List<PlayerPersonDefinition>(250);
                var seasons = new List<PlayerSeasonDefinition>(250);
                var cards = new List<PlayerCardDefinition>(250);
                var teams = new List<TeamSeasonDefinition>(10);
                var originalRecords = new List<OriginalSeasonRecordDefinition>(250);
                var originalAwards = new List<OriginalAwardRecordDefinition>(38);
                int[] noModifiers = new int[PlayerAbilityCatalog.AbilityCount];

                for (int teamIndex = 0; teamIndex < 10; teamIndex++)
                {
                    string teamKey = GetTeamKey(teamIndex);
                    string franchiseId = GetFranchiseId(teamIndex);
                    var core25 = new string[25];
                    for (int rosterIndex = 0; rosterIndex < 25; rosterIndex++)
                    {
                        int playerIndex = teamIndex * 25 + rosterIndex;
                        ActiveRosterRole role = GetRole(rosterIndex);
                        PlayerPosition position = GetPosition(role);
                        bool isPitcher = ActiveRosterCompositionRule.Standard.IsPitcherRole(role);
                        PitcherRole pitcherRole = isPitcher
                            ? ActiveRosterCompositionRule.Standard.GetAssignedPitcherRole(role)
                            : PitcherRole.Starter;
                        string personId = $"PP-{playerIndex:000}";
                        string seasonId = $"PS-{playerIndex:000}";
                        string cardId = PlayerCardDefinition.CreateStableCardId(
                            seasonId,
                            PlayerCardEdition.Normal);
                        persons.Add(new PlayerPersonDefinition(
                            personId,
                            1998,
                            Handedness.Right,
                            Handedness.Right,
                            position,
                            RegistrationType.Domestic,
                            2020,
                            2035,
                            new PersonPotentialTrait(new int[PlayerAbilityCatalog.AbilityCount])));
                        seasons.Add(new PlayerSeasonDefinition(
                            seasonId,
                            personId,
                            Year,
                            franchiseId,
                            teamKey,
                            position,
                            pitcherRole,
                            isPitcher ? PlayerType.Pitcher : PlayerType.Batter,
                            RegistrationType.Domestic,
                            new AbilityRatings(48 + playerIndex % 8),
                            5,
                            new AbilityRatings(70)));
                        cards.Add(new PlayerCardDefinition(
                            cardId,
                            seasonId,
                            PlayerCardEdition.Normal,
                            noModifiers));
                        core25[rosterIndex] = cardId;
                        originalRecords.Add(new OriginalSeasonRecordDefinition(
                            CreateRegularStatistics(seasonId, teamKey, position, playerIndex, 0)));
                    }
                    teams.Add(new TeamSeasonDefinition(
                        teamKey,
                        franchiseId,
                        Year,
                        core25,
                        core25,
                        50d));
                }

                AddOriginalAwards(seasons, originalAwards);
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
                    Year,
                    seasons,
                    cards,
                    teams,
                    originalRecords,
                    originalAwards);
                return new HistoricalBakedContent(manifest, persons, new[] { year });
            }

            public static IReadOnlyList<SeasonStatistics> CreateSimulationStatistics(
                IReadOnlyList<TeamSeasonDefinition> teams,
                ulong worldHistorySeed)
            {
                int seedModifier = (int)(worldHistorySeed % 7UL);
                var result = new List<SeasonStatistics>(520);
                for (int teamIndex = 0; teamIndex < teams.Count; teamIndex++)
                {
                    string teamKey = teams[teamIndex].TeamSeasonKey;
                    for (int rosterIndex = 0; rosterIndex < 25; rosterIndex++)
                    {
                        int playerIndex = teamIndex * 25 + rosterIndex;
                        ActiveRosterRole role = GetRole(rosterIndex);
                        PlayerPosition position = GetPosition(role);
                        string seasonId = $"PS-{playerIndex:000}";
                        result.Add(CreateScopedStatistics(
                            seasonId,
                            teamKey,
                            position,
                            playerIndex,
                            seedModifier,
                            isFirstHalf: true));
                        result.Add(CreateRegularStatistics(
                            seasonId,
                            teamKey,
                            position,
                            playerIndex,
                            seedModifier));
                        if (playerIndex < 25)
                        {
                            result.Add(CreateScopedStatistics(
                                seasonId,
                                teamKey,
                                position,
                                playerIndex,
                                seedModifier,
                                isAllStarGame: true));
                            result.Add(CreateScopedStatistics(
                                seasonId,
                                teamKey,
                                position,
                                playerIndex,
                                seedModifier,
                                isPostseason: true));
                        }
                    }
                }
                return result;
            }

            public static HistoricalSeasonSimulationResult CreateSimulationResult(
                IReadOnlyList<TeamSeasonDefinition> teams,
                ulong worldHistorySeed)
            {
                var teamStatistics = new TeamSeasonStatistics[teams.Count];
                var standings = new HistoricalStandingEntry[teams.Count];
                for (int index = 0; index < teams.Count; index++)
                {
                    int wins = teams.Count - index;
                    int losses = index;
                    teamStatistics[index] = new TeamSeasonStatistics(
                        teams[index].TeamSeasonKey,
                        teams[index].OriginYear,
                        wins + losses,
                        wins,
                        losses,
                        0,
                        100 - index,
                        80 + index,
                        300,
                        80 - index,
                        270,
                        25 + index,
                        70 + index,
                        20 + index);
                    standings[index] = new HistoricalStandingEntry(
                        teams[index].OriginYear,
                        index + 1,
                        teams[index].TeamSeasonKey);
                }
                var qualifiers = new string[4];
                for (int index = 0; index < qualifiers.Length; index++)
                    qualifiers[index] = teams[index].TeamSeasonKey;
                return new HistoricalSeasonSimulationResult(
                    CreateSimulationStatistics(teams, worldHistorySeed),
                    teamStatistics,
                    standings,
                    new HistoricalPostseasonResult(
                        teams[0].OriginYear,
                        qualifiers,
                        qualifiers[(int)(worldHistorySeed % (ulong)qualifiers.Length)]));
            }

            private static SeasonStatistics CreateRegularStatistics(
                string seasonId,
                string teamKey,
                PlayerPosition position,
                int playerIndex,
                int seedModifier)
            {
                bool isPitcher = position == PlayerPosition.StartingPitcher ||
                    position == PlayerPosition.ReliefPitcher;
                return new SeasonStatistics(
                    seasonId,
                    teamKey,
                    Year,
                    position,
                    plateAppearances: isPitcher ? 0 : 300,
                    hits: isPitcher ? 0 : 75 + playerIndex % 20 + seedModifier,
                    homeRuns: isPitcher ? 0 : 5 + playerIndex % 12,
                    walks: isPitcher ? 0 : 25,
                    strikeouts: isPitcher ? 0 : 50,
                    stolenBases: isPitcher ? 0 : playerIndex % 10,
                    pitchingOuts: isPitcher ? 180 + playerIndex % 40 : 0,
                    earnedRuns: isPitcher ? 18 + playerIndex % 8 : 0,
                    pitchingStrikeouts: isPitcher ? 50 + playerIndex % 30 : 0,
                    defensiveChances: 120 + playerIndex % 30,
                    defensiveOutsAboveAverage: playerIndex % 8,
                    fieldingErrors: playerIndex % 4);
            }

            private static SeasonStatistics CreateScopedStatistics(
                string seasonId,
                string teamKey,
                PlayerPosition position,
                int playerIndex,
                int seedModifier,
                bool isFirstHalf = false,
                bool isPostseason = false,
                bool isAllStarGame = false)
            {
                bool isPitcher = position == PlayerPosition.StartingPitcher ||
                    position == PlayerPosition.ReliefPitcher;
                return new SeasonStatistics(
                    seasonId,
                    teamKey,
                    Year,
                    position,
                    plateAppearances: isPitcher ? 0 : 30,
                    hits: isPitcher ? 0 : 8 + playerIndex % 8 + seedModifier,
                    homeRuns: isPitcher ? 0 : 1 + playerIndex % 3,
                    walks: isPitcher ? 0 : 3,
                    strikeouts: isPitcher ? 0 : 4,
                    pitchingOuts: isPitcher ? 30 + playerIndex % 12 : 0,
                    earnedRuns: isPitcher ? 2 + playerIndex % 3 : 0,
                    pitchingStrikeouts: isPitcher ? 8 + playerIndex % 6 : 0,
                    defensiveChances: 20,
                    defensiveOutsAboveAverage: playerIndex % 4,
                    fieldingErrors: playerIndex % 2,
                    isFirstHalf: isFirstHalf,
                    isPostseason: isPostseason,
                    isAllStarGame: isAllStarGame);
            }

            private static void AddOriginalAwards(
                IReadOnlyList<PlayerSeasonDefinition> seasons,
                ICollection<OriginalAwardRecordDefinition> output)
            {
                for (int index = 0; index < 25; index++)
                    output.Add(new OriginalAwardRecordDefinition(new WorldAwardEntry(
                        Year,
                        WorldAwardType.AllStar,
                        seasons[index].PlayerSeasonId,
                        seasons[index].Position)));
                for (int index = 0; index < 10; index++)
                    output.Add(new OriginalAwardRecordDefinition(new WorldAwardEntry(
                        Year,
                        WorldAwardType.GoldenGlove,
                        seasons[index].PlayerSeasonId,
                        seasons[index].Position)));
                output.Add(new OriginalAwardRecordDefinition(new WorldAwardEntry(
                    Year,
                    WorldAwardType.RegularSeasonMvp,
                    seasons[0].PlayerSeasonId,
                    seasons[0].Position)));
                output.Add(new OriginalAwardRecordDefinition(new WorldAwardEntry(
                    Year,
                    WorldAwardType.AllStarGameMvp,
                    seasons[1].PlayerSeasonId,
                    seasons[1].Position)));
                PlayerSeasonDefinition originalOnly = seasons[seasons.Count - 1];
                output.Add(new OriginalAwardRecordDefinition(new WorldAwardEntry(
                    Year,
                    WorldAwardType.PostseasonMvp,
                    originalOnly.PlayerSeasonId,
                    originalOnly.Position)));
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

            private static string GetTeamKey(int teamIndex) => $"TEAM-{teamIndex:00}";
            private static string GetFranchiseId(int teamIndex) => $"FRANCHISE-{teamIndex:00}";
        }
    }
}
