using System;
using Baseball.Core.Historical;
using Baseball.Core.Players;

namespace Baseball.Game.Historical
{
    /// <summary>공통 World History Snapshot과 저장 DTO를 결정론적 순서로 변환한다.</summary>
    public sealed class WorldHistorySaveMapper
    {
        public WorldHistorySaveData CreateSaveData(WorldHistorySnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            var statistics = new SeasonStatisticsSaveData[snapshot.Statistics.Count];
            for (int index = 0; index < snapshot.Statistics.Count; index++)
            {
                SeasonStatistics row = snapshot.Statistics[index];
                statistics[index] = new SeasonStatisticsSaveData
                {
                    playerSeasonId = row.PlayerSeasonId,
                    teamSeasonKey = row.TeamSeasonKey,
                    seasonYear = row.SeasonYear,
                    position = (int)row.Position,
                    plateAppearances = row.PlateAppearances,
                    hits = row.Hits,
                    homeRuns = row.HomeRuns,
                    walks = row.Walks,
                    strikeouts = row.Strikeouts,
                    stolenBases = row.StolenBases,
                    pitchingOuts = row.PitchingOuts,
                    earnedRuns = row.EarnedRuns,
                    pitchingStrikeouts = row.PitchingStrikeouts,
                    defensiveChances = row.DefensiveChances,
                    defensiveOutsAboveAverage = row.DefensiveOutsAboveAverage,
                    fieldingErrors = row.FieldingErrors,
                    isFirstHalf = row.IsFirstHalf,
                    isPostseason = row.IsPostseason,
                    isAllStarGame = row.IsAllStarGame
                };
            }
            Array.Sort(statistics, CompareStatistics);

            var awards = new WorldAwardEntrySaveData[snapshot.Awards.Entries.Count];
            for (int index = 0; index < snapshot.Awards.Entries.Count; index++)
            {
                WorldAwardEntry award = snapshot.Awards.Entries[index];
                awards[index] = new WorldAwardEntrySaveData
                {
                    seasonYear = award.SeasonYear,
                    awardType = (int)award.AwardType,
                    playerSeasonId = award.PlayerSeasonId,
                    position = (int)award.Position
                };
            }
            Array.Sort(awards, CompareAwards);

            return new WorldHistorySaveData
            {
                recordMode = (int)snapshot.RecordMode,
                worldHistorySeed = snapshot.WorldHistorySeed,
                statistics = statistics,
                awards = awards
            };
        }

        public WorldHistorySnapshot Restore(WorldHistorySaveData source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            ValidateEnum<WorldRecordMode>(source.recordMode, nameof(source.recordMode));
            if (source.statistics == null)
                throw new ArgumentException("World History 통계가 없습니다.", nameof(source));
            if (source.awards == null)
                throw new ArgumentException("World Award 기록이 없습니다.", nameof(source));

            var statistics = new SeasonStatistics[source.statistics.Length];
            for (int index = 0; index < statistics.Length; index++)
            {
                SeasonStatisticsSaveData row = source.statistics[index]
                    ?? throw new ArgumentException("World History에 null 통계가 있습니다.", nameof(source));
                ValidateEnum<PlayerPosition>(row.position, nameof(row.position));
                statistics[index] = new SeasonStatistics(
                    row.playerSeasonId,
                    row.teamSeasonKey,
                    row.seasonYear,
                    (PlayerPosition)row.position,
                    row.plateAppearances,
                    row.hits,
                    row.homeRuns,
                    row.walks,
                    row.strikeouts,
                    row.stolenBases,
                    row.pitchingOuts,
                    row.earnedRuns,
                    row.pitchingStrikeouts,
                    row.defensiveChances,
                    row.defensiveOutsAboveAverage,
                    row.fieldingErrors,
                    row.isFirstHalf,
                    row.isPostseason,
                    row.isAllStarGame);
            }

            var awards = new WorldAwardEntry[source.awards.Length];
            for (int index = 0; index < awards.Length; index++)
            {
                WorldAwardEntrySaveData award = source.awards[index]
                    ?? throw new ArgumentException("World History에 null Award가 있습니다.", nameof(source));
                ValidateEnum<WorldAwardType>(award.awardType, nameof(award.awardType));
                ValidateEnum<PlayerPosition>(award.position, nameof(award.position));
                awards[index] = new WorldAwardEntry(
                    award.seasonYear,
                    (WorldAwardType)award.awardType,
                    award.playerSeasonId,
                    (PlayerPosition)award.position);
            }

            return new WorldHistorySnapshot(
                (WorldRecordMode)source.recordMode,
                source.worldHistorySeed,
                statistics,
                new WorldAwardRecord(awards));
        }

        private static int CompareStatistics(SeasonStatisticsSaveData left, SeasonStatisticsSaveData right)
        {
            int comparison = left.seasonYear.CompareTo(right.seasonYear);
            if (comparison != 0) return comparison;
            comparison = StringComparer.Ordinal.Compare(left.playerSeasonId, right.playerSeasonId);
            if (comparison != 0) return comparison;
            comparison = left.isPostseason.CompareTo(right.isPostseason);
            if (comparison != 0) return comparison;
            comparison = left.isAllStarGame.CompareTo(right.isAllStarGame);
            if (comparison != 0) return comparison;
            return left.isFirstHalf.CompareTo(right.isFirstHalf);
        }

        private static int CompareAwards(WorldAwardEntrySaveData left, WorldAwardEntrySaveData right)
        {
            int comparison = left.seasonYear.CompareTo(right.seasonYear);
            if (comparison != 0) return comparison;
            comparison = left.awardType.CompareTo(right.awardType);
            if (comparison != 0) return comparison;
            comparison = left.position.CompareTo(right.position);
            return comparison != 0
                ? comparison
                : StringComparer.Ordinal.Compare(left.playerSeasonId, right.playerSeasonId);
        }

        private static void ValidateEnum<T>(int value, string parameterName) where T : struct
        {
            if (!Enum.IsDefined(typeof(T), value))
                throw new ArgumentOutOfRangeException(parameterName, value, "저장된 enum 값이 유효하지 않습니다.");
        }
    }
}
