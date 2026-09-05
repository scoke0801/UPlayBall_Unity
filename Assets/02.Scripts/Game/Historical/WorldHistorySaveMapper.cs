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

            var teamStatistics = new TeamSeasonStatisticsSaveData[snapshot.TeamStatistics.Count];
            for (int index = 0; index < teamStatistics.Length; index++)
            {
                TeamSeasonStatistics row = snapshot.TeamStatistics[index];
                teamStatistics[index] = new TeamSeasonStatisticsSaveData
                {
                    teamSeasonKey = row.TeamSeasonKey,
                    seasonYear = row.SeasonYear,
                    games = row.Games,
                    wins = row.Wins,
                    losses = row.Losses,
                    ties = row.Ties,
                    runsScored = row.RunsScored,
                    runsAllowed = row.RunsAllowed,
                    atBats = row.AtBats,
                    hits = row.Hits,
                    pitchingOuts = row.PitchingOuts,
                    earnedRuns = row.EarnedRuns,
                    hitsAllowed = row.HitsAllowed,
                    walksAllowed = row.WalksAllowed
                };
            }

            var standings = new HistoricalStandingEntrySaveData[snapshot.Standings.Count];
            for (int index = 0; index < standings.Length; index++)
            {
                HistoricalStandingEntry row = snapshot.Standings[index];
                standings[index] = new HistoricalStandingEntrySaveData
                {
                    seasonYear = row.SeasonYear,
                    rank = row.Rank,
                    teamSeasonKey = row.TeamSeasonKey
                };
            }

            var postseasonResults = new HistoricalPostseasonResultSaveData[snapshot.PostseasonResults.Count];
            for (int index = 0; index < postseasonResults.Length; index++)
            {
                HistoricalPostseasonResult row = snapshot.PostseasonResults[index];
                var qualifiers = new string[row.QualifiedTeamSeasonKeys.Count];
                for (int qualifierIndex = 0; qualifierIndex < qualifiers.Length; qualifierIndex++)
                    qualifiers[qualifierIndex] = row.QualifiedTeamSeasonKeys[qualifierIndex];
                postseasonResults[index] = new HistoricalPostseasonResultSaveData
                {
                    seasonYear = row.SeasonYear,
                    qualifiedTeamSeasonKeys = qualifiers,
                    championTeamSeasonKey = row.ChampionTeamSeasonKey
                };
            }

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

            return new WorldHistorySaveData
            {
                recordMode = (int)snapshot.RecordMode,
                worldHistorySeed = snapshot.WorldHistorySeed,
                statistics = statistics,
                teamStatistics = teamStatistics,
                standings = standings,
                postseasonResults = postseasonResults,
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

            TeamSeasonStatisticsSaveData[] teamStatisticsData =
                source.teamStatistics ?? Array.Empty<TeamSeasonStatisticsSaveData>();
            var teamStatistics = new TeamSeasonStatistics[teamStatisticsData.Length];
            for (int index = 0; index < teamStatistics.Length; index++)
            {
                TeamSeasonStatisticsSaveData row = teamStatisticsData[index]
                    ?? throw new ArgumentException("World History에 null 팀 통계가 있습니다.", nameof(source));
                teamStatistics[index] = new TeamSeasonStatistics(
                    row.teamSeasonKey,
                    row.seasonYear,
                    row.games,
                    row.wins,
                    row.losses,
                    row.ties,
                    row.runsScored,
                    row.runsAllowed,
                    row.atBats,
                    row.hits,
                    row.pitchingOuts,
                    row.earnedRuns,
                    row.hitsAllowed,
                    row.walksAllowed);
            }

            HistoricalStandingEntrySaveData[] standingsData =
                source.standings ?? Array.Empty<HistoricalStandingEntrySaveData>();
            var standings = new HistoricalStandingEntry[standingsData.Length];
            for (int index = 0; index < standings.Length; index++)
            {
                HistoricalStandingEntrySaveData row = standingsData[index]
                    ?? throw new ArgumentException("World History에 null 순위가 있습니다.", nameof(source));
                standings[index] = new HistoricalStandingEntry(row.seasonYear, row.rank, row.teamSeasonKey);
            }

            HistoricalPostseasonResultSaveData[] postseasonData =
                source.postseasonResults ?? Array.Empty<HistoricalPostseasonResultSaveData>();
            var postseasonResults = new HistoricalPostseasonResult[postseasonData.Length];
            for (int index = 0; index < postseasonResults.Length; index++)
            {
                HistoricalPostseasonResultSaveData row = postseasonData[index]
                    ?? throw new ArgumentException("World History에 null Postseason 결과가 있습니다.", nameof(source));
                postseasonResults[index] = new HistoricalPostseasonResult(
                    row.seasonYear,
                    row.qualifiedTeamSeasonKeys
                        ?? throw new ArgumentException("Postseason 진출 구단이 없습니다.", nameof(source)),
                    row.championTeamSeasonKey);
            }

            return new WorldHistorySnapshot(
                (WorldRecordMode)source.recordMode,
                source.worldHistorySeed,
                statistics,
                teamStatistics,
                standings,
                postseasonResults,
                new WorldAwardRecord(awards));
        }

        private static void ValidateEnum<T>(int value, string parameterName) where T : struct
        {
            if (!Enum.IsDefined(typeof(T), value))
                throw new ArgumentOutOfRangeException(parameterName, value, "저장된 enum 값이 유효하지 않습니다.");
        }
    }
}
