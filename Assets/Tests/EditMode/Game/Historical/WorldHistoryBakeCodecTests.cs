using System;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Game.Historical;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game.Historical
{
    /// <summary>Bake 산출물이 World History를 값 손실 없이 왕복하는지 검증한다.</summary>
    public sealed class WorldHistoryBakeCodecTests
    {
        [Test]
        public void Encode_Decode_WorldHistory를_그대로_복원한다()
        {
            BakedWorldHistoryPayload source = CreatePayload();

            byte[] bytes = WorldHistoryBakeCodec.Encode(source);
            BakedWorldHistoryPayload restored = WorldHistoryBakeCodec.Decode(bytes);

            Assert.That(restored.Key, Is.EqualTo(source.Key));
            AssertHistoryEquals(source.History, restored.History);
        }

        [Test]
        public void Encode_같은_입력이면_같은_바이트를_만든다()
        {
            BakedWorldHistoryPayload payload = CreatePayload();

            byte[] first = WorldHistoryBakeCodec.Encode(payload);
            byte[] second = WorldHistoryBakeCodec.Encode(payload);

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void 복원한_SaveData는_Snapshot으로_되돌릴_수_있다()
        {
            BakedWorldHistoryPayload payload = CreatePayload();

            BakedWorldHistoryPayload restored = WorldHistoryBakeCodec.Decode(
                WorldHistoryBakeCodec.Encode(payload));
            WorldHistorySnapshot snapshot = new WorldHistorySaveMapper().Restore(restored.History);

            Assert.That(snapshot.RecordMode, Is.EqualTo(WorldRecordMode.SimulatedHistory));
            Assert.That(snapshot.WorldHistorySeed, Is.EqualTo(payload.Key.WorldHistorySeed));
            Assert.That(snapshot.Statistics.Count, Is.EqualTo(payload.History.statistics.Length));
            Assert.That(snapshot.Awards.Entries.Count, Is.EqualTo(payload.History.awards.Length));
        }

        [Test]
        public void TryPeekKey는_본문을_읽지_않고_Key를_돌려준다()
        {
            BakedWorldHistoryPayload payload = CreatePayload();

            bool found = WorldHistoryBakeCodec.TryPeekKey(
                WorldHistoryBakeCodec.Encode(payload),
                out BakedWorldHistoryKey key);

            Assert.That(found, Is.True);
            Assert.That(key, Is.EqualTo(payload.Key));
        }

        [Test]
        public void TryPeekKey는_Bake가_아닌_바이트를_거부한다()
        {
            bool found = WorldHistoryBakeCodec.TryPeekKey(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, out _);

            Assert.That(found, Is.False);
        }

        [Test]
        public void Encode는_Key와_Seed가_다르면_거부한다()
        {
            BakedWorldHistoryPayload payload = CreatePayload();
            payload.History.worldHistorySeed = payload.Key.WorldHistorySeed + 1UL;

            Assert.Throws<WorldHistoryBakeFormatException>(() => WorldHistoryBakeCodec.Encode(payload));
        }

        [Test]
        public void Decode는_손상된_바이트를_형식_예외로_알린다()
        {
            byte[] bytes = WorldHistoryBakeCodec.Encode(CreatePayload());
            bytes[bytes.Length - 1] ^= 0xff;
            bytes[bytes.Length - 2] ^= 0xff;

            Assert.Throws<WorldHistoryBakeFormatException>(() => WorldHistoryBakeCodec.Decode(bytes));
        }

        private static BakedWorldHistoryPayload CreatePayload()
        {
            const ulong seed = 8_261_021UL;
            var history = new WorldHistorySaveData
            {
                recordMode = (int)WorldRecordMode.SimulatedHistory,
                worldHistorySeed = seed,
                statistics = new[]
                {
                    CreateStatistics("PS-1982-0001", "TS-1982-A", 1982, PlayerPosition.Catcher, 1),
                    // 수비 OAA는 음수가 될 수 있어 varint zigzag 경로를 함께 덮는다.
                    CreateStatistics("PS-1982-0002", "TS-1982-B", 1982, PlayerPosition.StartingPitcher, -7),
                    CreateStatistics("PS-2025-0003", "TS-2025-A", 2025, PlayerPosition.CenterField, 0)
                },
                teamStatistics = new[]
                {
                    new TeamSeasonStatisticsSaveData
                    {
                        teamSeasonKey = "TS-1982-A",
                        seasonYear = 1982,
                        games = 80,
                        wins = 44,
                        losses = 35,
                        ties = 1,
                        runsScored = 401,
                        runsAllowed = 366,
                        atBats = 2711,
                        hits = 733,
                        pitchingOuts = 2148,
                        earnedRuns = 331,
                        hitsAllowed = 701,
                        walksAllowed = 254
                    }
                },
                standings = new[]
                {
                    new HistoricalStandingEntrySaveData { seasonYear = 1982, rank = 1, teamSeasonKey = "TS-1982-A" },
                    new HistoricalStandingEntrySaveData { seasonYear = 1982, rank = 2, teamSeasonKey = "TS-1982-B" }
                },
                postseasonResults = new[]
                {
                    new HistoricalPostseasonResultSaveData
                    {
                        seasonYear = 1982,
                        qualifiedTeamSeasonKeys = new[] { "TS-1982-A", "TS-1982-B" },
                        championTeamSeasonKey = "TS-1982-A"
                    }
                },
                awards = new[]
                {
                    new WorldAwardEntrySaveData
                    {
                        seasonYear = 1982,
                        awardType = (int)WorldAwardType.RegularSeasonMvp,
                        playerSeasonId = "PS-1982-0001",
                        position = (int)PlayerPosition.Catcher
                    }
                }
            };
            var key = new BakedWorldHistoryKey(
                WorldRecordMode.SimulatedHistory,
                seed,
                "content-hash-abc",
                balanceVersion: 3,
                balanceContentHash: "balance-hash-xyz");
            return new BakedWorldHistoryPayload(key, history);
        }

        private static SeasonStatisticsSaveData CreateStatistics(
            string playerSeasonId,
            string teamSeasonKey,
            int seasonYear,
            PlayerPosition position,
            int defensiveOutsAboveAverage)
        {
            return new SeasonStatisticsSaveData
            {
                playerSeasonId = playerSeasonId,
                teamSeasonKey = teamSeasonKey,
                seasonYear = seasonYear,
                position = (int)position,
                plateAppearances = 512,
                hits = 141,
                homeRuns = 22,
                walks = 51,
                strikeouts = 88,
                stolenBases = 7,
                pitchingOuts = 0,
                earnedRuns = 0,
                pitchingStrikeouts = 0,
                defensiveChances = 640,
                defensiveOutsAboveAverage = defensiveOutsAboveAverage,
                fieldingErrors = 9,
                isFirstHalf = seasonYear % 2 == 0,
                isPostseason = false,
                isAllStarGame = position == PlayerPosition.Catcher
            };
        }

        private static void AssertHistoryEquals(WorldHistorySaveData expected, WorldHistorySaveData actual)
        {
            Assert.That(actual.recordMode, Is.EqualTo(expected.recordMode));
            Assert.That(actual.worldHistorySeed, Is.EqualTo(expected.worldHistorySeed));
            Assert.That(actual.statistics.Length, Is.EqualTo(expected.statistics.Length));
            for (int index = 0; index < expected.statistics.Length; index++)
            {
                SeasonStatisticsSaveData left = expected.statistics[index];
                SeasonStatisticsSaveData right = actual.statistics[index];
                Assert.That(right.playerSeasonId, Is.EqualTo(left.playerSeasonId));
                Assert.That(right.teamSeasonKey, Is.EqualTo(left.teamSeasonKey));
                Assert.That(right.seasonYear, Is.EqualTo(left.seasonYear));
                Assert.That(right.position, Is.EqualTo(left.position));
                Assert.That(right.plateAppearances, Is.EqualTo(left.plateAppearances));
                Assert.That(right.hits, Is.EqualTo(left.hits));
                Assert.That(right.homeRuns, Is.EqualTo(left.homeRuns));
                Assert.That(right.walks, Is.EqualTo(left.walks));
                Assert.That(right.strikeouts, Is.EqualTo(left.strikeouts));
                Assert.That(right.stolenBases, Is.EqualTo(left.stolenBases));
                Assert.That(right.pitchingOuts, Is.EqualTo(left.pitchingOuts));
                Assert.That(right.earnedRuns, Is.EqualTo(left.earnedRuns));
                Assert.That(right.pitchingStrikeouts, Is.EqualTo(left.pitchingStrikeouts));
                Assert.That(right.defensiveChances, Is.EqualTo(left.defensiveChances));
                Assert.That(right.defensiveOutsAboveAverage, Is.EqualTo(left.defensiveOutsAboveAverage));
                Assert.That(right.fieldingErrors, Is.EqualTo(left.fieldingErrors));
                Assert.That(right.isFirstHalf, Is.EqualTo(left.isFirstHalf));
                Assert.That(right.isPostseason, Is.EqualTo(left.isPostseason));
                Assert.That(right.isAllStarGame, Is.EqualTo(left.isAllStarGame));
            }

            Assert.That(actual.teamStatistics.Length, Is.EqualTo(expected.teamStatistics.Length));
            for (int index = 0; index < expected.teamStatistics.Length; index++)
            {
                TeamSeasonStatisticsSaveData left = expected.teamStatistics[index];
                TeamSeasonStatisticsSaveData right = actual.teamStatistics[index];
                Assert.That(right.teamSeasonKey, Is.EqualTo(left.teamSeasonKey));
                Assert.That(right.seasonYear, Is.EqualTo(left.seasonYear));
                Assert.That(right.games, Is.EqualTo(left.games));
                Assert.That(right.wins, Is.EqualTo(left.wins));
                Assert.That(right.losses, Is.EqualTo(left.losses));
                Assert.That(right.ties, Is.EqualTo(left.ties));
                Assert.That(right.runsScored, Is.EqualTo(left.runsScored));
                Assert.That(right.runsAllowed, Is.EqualTo(left.runsAllowed));
                Assert.That(right.atBats, Is.EqualTo(left.atBats));
                Assert.That(right.hits, Is.EqualTo(left.hits));
                Assert.That(right.pitchingOuts, Is.EqualTo(left.pitchingOuts));
                Assert.That(right.earnedRuns, Is.EqualTo(left.earnedRuns));
                Assert.That(right.hitsAllowed, Is.EqualTo(left.hitsAllowed));
                Assert.That(right.walksAllowed, Is.EqualTo(left.walksAllowed));
            }

            Assert.That(actual.standings.Length, Is.EqualTo(expected.standings.Length));
            for (int index = 0; index < expected.standings.Length; index++)
            {
                Assert.That(actual.standings[index].seasonYear, Is.EqualTo(expected.standings[index].seasonYear));
                Assert.That(actual.standings[index].rank, Is.EqualTo(expected.standings[index].rank));
                Assert.That(
                    actual.standings[index].teamSeasonKey,
                    Is.EqualTo(expected.standings[index].teamSeasonKey));
            }

            Assert.That(actual.postseasonResults.Length, Is.EqualTo(expected.postseasonResults.Length));
            for (int index = 0; index < expected.postseasonResults.Length; index++)
            {
                HistoricalPostseasonResultSaveData left = expected.postseasonResults[index];
                HistoricalPostseasonResultSaveData right = actual.postseasonResults[index];
                Assert.That(right.seasonYear, Is.EqualTo(left.seasonYear));
                Assert.That(right.championTeamSeasonKey, Is.EqualTo(left.championTeamSeasonKey));
                Assert.That(right.qualifiedTeamSeasonKeys, Is.EqualTo(left.qualifiedTeamSeasonKeys));
            }

            Assert.That(actual.awards.Length, Is.EqualTo(expected.awards.Length));
            for (int index = 0; index < expected.awards.Length; index++)
            {
                WorldAwardEntrySaveData left = expected.awards[index];
                WorldAwardEntrySaveData right = actual.awards[index];
                Assert.That(right.seasonYear, Is.EqualTo(left.seasonYear));
                Assert.That(right.awardType, Is.EqualTo(left.awardType));
                Assert.That(right.playerSeasonId, Is.EqualTo(left.playerSeasonId));
                Assert.That(right.position, Is.EqualTo(left.position));
            }
        }
    }
}
