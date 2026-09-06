using System;
using System.Collections.Generic;
using Baseball.Core.Players;
using Baseball.Game.Career;

namespace Baseball.Game.Historical
{
    /// <summary>
    /// 구단주 세이브가 현재 시즌 개인 기록을 경기 재시뮬레이션 없이 복원하도록 변환한다.
    /// </summary>
    /// <remarks>
    /// 저장 대상은 타격·투구·수비 시즌 누적뿐이다. <c>GameContributions</c>와 <c>TeamSplits</c>는
    /// 선수 커리어 모드의 감독 평가 가중치·수상 가중치·시즌 중 트레이드 분할 전용이고 구단주 모드에
    /// 소비자가 없다. 경기별 기여도는 시즌당 수만 건이라 저장 비용도 정당화되지 않는다.
    /// 따라서 구단주 모드에서 Source of Truth는 여기 저장된 시즌 누적이고, 기여도·구단 분할은
    /// 커리어 모드 런타임에서만 존재하는 파생 상태다.
    /// </remarks>
    public static class LeagueSeasonStatisticsSaveMapper
    {
        public static LeagueSeasonStatisticsSaveData CreateSaveData(LeagueSeasonStatisticsState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            return new LeagueSeasonStatisticsSaveData
            {
                schemaVersion = state.StatisticsSchemaVersion,
                regularSeason = CreatePlayers(state.RegularSeason)
            };
        }

        public static LeagueSeasonStatisticsState Restore(LeagueSeasonStatisticsSaveData source)
        {
            var state = new LeagueSeasonStatisticsState();
            if (source?.regularSeason == null)
                return state;

            for (int index = 0; index < source.regularSeason.Length; index++)
            {
                PlayerSeasonRecordSaveData saved = source.regularSeason[index]
                    ?? throw new InvalidOperationException("null 선수 기록이 있습니다.");
                PlayerCompetitionStatisticsState player = state.RegularSeason.GetOrCreate(
                    saved.playerId,
                    saved.playerName ?? string.Empty,
                    saved.teamId,
                    (PlayerPosition)saved.primaryPosition);
                player.TeamGames = saved.teamGames;
                RestoreBatting(player.Batting, saved.batting);
                RestorePitching(player.Pitching, saved.pitching);
                RestoreFielding(player, saved.fielding);
            }
            return state;
        }

        private static PlayerSeasonRecordSaveData[] CreatePlayers(CompetitionStatisticsState competition)
        {
            var players = new List<PlayerCompetitionStatisticsState>(competition.Players.Count);
            foreach (PlayerCompetitionStatisticsState player in competition.Players.Values)
                players.Add(player);
            // Dictionary 순회 순서가 세이브 파일에 남지 않도록 PlayerId로 고정한다.
            players.Sort((left, right) => left.PlayerId.CompareTo(right.PlayerId));

            var result = new PlayerSeasonRecordSaveData[players.Count];
            for (int index = 0; index < players.Count; index++)
            {
                PlayerCompetitionStatisticsState player = players[index];
                result[index] = new PlayerSeasonRecordSaveData
                {
                    playerId = player.PlayerId,
                    playerName = player.PlayerName,
                    teamId = player.TeamId,
                    primaryPosition = (int)player.PrimaryPosition,
                    teamGames = player.TeamGames,
                    batting = CreateBatting(player.Batting),
                    pitching = CreatePitching(player.Pitching),
                    fielding = CreateFielding(player)
                };
            }
            return result;
        }

        private static BattingRecordSaveData CreateBatting(BattingStatisticsState source)
        {
            return new BattingRecordSaveData
            {
                games = source.Games,
                gamesStarted = source.GamesStarted,
                plateAppearances = source.PlateAppearances,
                atBats = source.AtBats,
                runs = source.Runs,
                hits = source.Hits,
                doubles = source.Doubles,
                triples = source.Triples,
                homeRuns = source.HomeRuns,
                runsBattedIn = source.RunsBattedIn,
                walks = source.Walks,
                hitByPitches = source.HitByPitches,
                strikeouts = source.Strikeouts,
                stolenBases = source.StolenBases,
                caughtStealing = source.CaughtStealing,
                sacrificeBunts = source.SacrificeBunts,
                sacrificeFlies = source.SacrificeFlies,
                intentionalWalks = source.IntentionalWalks,
                reachedOnErrors = source.ReachedOnErrors,
                groundedIntoDoublePlays = source.GroundedIntoDoublePlays
            };
        }

        private static void RestoreBatting(BattingStatisticsState target, BattingRecordSaveData source)
        {
            if (source == null) return;
            target.Games = source.games;
            target.GamesStarted = source.gamesStarted;
            target.PlateAppearances = source.plateAppearances;
            target.AtBats = source.atBats;
            target.Runs = source.runs;
            target.Hits = source.hits;
            target.Doubles = source.doubles;
            target.Triples = source.triples;
            target.HomeRuns = source.homeRuns;
            target.RunsBattedIn = source.runsBattedIn;
            target.Walks = source.walks;
            target.HitByPitches = source.hitByPitches;
            target.Strikeouts = source.strikeouts;
            target.StolenBases = source.stolenBases;
            target.CaughtStealing = source.caughtStealing;
            target.SacrificeBunts = source.sacrificeBunts;
            target.SacrificeFlies = source.sacrificeFlies;
            target.IntentionalWalks = source.intentionalWalks;
            target.ReachedOnErrors = source.reachedOnErrors;
            target.GroundedIntoDoublePlays = source.groundedIntoDoublePlays;
        }

        private static PitchingRecordSaveData CreatePitching(PitchingStatisticsState source)
        {
            return new PitchingRecordSaveData
            {
                appearances = source.Appearances,
                starts = source.Starts,
                outsRecorded = source.OutsRecorded,
                pitchesThrown = source.PitchesThrown,
                wins = source.Wins,
                losses = source.Losses,
                saves = source.Saves,
                holds = source.Holds,
                blownSaves = source.BlownSaves,
                hitsAllowed = source.HitsAllowed,
                homeRunsAllowed = source.HomeRunsAllowed,
                walksAllowed = source.WalksAllowed,
                hitBatters = source.HitBatters,
                strikeouts = source.Strikeouts,
                runsAllowed = source.RunsAllowed,
                earnedRuns = source.EarnedRuns,
                battersFaced = source.BattersFaced,
                inheritedRunners = source.InheritedRunners,
                inheritedRunnersScored = source.InheritedRunnersScored,
                qualityStarts = source.QualityStarts
            };
        }

        private static void RestorePitching(PitchingStatisticsState target, PitchingRecordSaveData source)
        {
            if (source == null) return;
            target.Appearances = source.appearances;
            target.Starts = source.starts;
            target.OutsRecorded = source.outsRecorded;
            target.PitchesThrown = source.pitchesThrown;
            target.Wins = source.wins;
            target.Losses = source.losses;
            target.Saves = source.saves;
            target.Holds = source.holds;
            target.BlownSaves = source.blownSaves;
            target.HitsAllowed = source.hitsAllowed;
            target.HomeRunsAllowed = source.homeRunsAllowed;
            target.WalksAllowed = source.walksAllowed;
            target.HitBatters = source.hitBatters;
            target.Strikeouts = source.strikeouts;
            target.RunsAllowed = source.runsAllowed;
            target.EarnedRuns = source.earnedRuns;
            target.BattersFaced = source.battersFaced;
            target.InheritedRunners = source.inheritedRunners;
            target.InheritedRunnersScored = source.inheritedRunnersScored;
            target.QualityStarts = source.qualityStarts;
        }

        private static FieldingRecordSaveData[] CreateFielding(PlayerCompetitionStatisticsState player)
        {
            var result = new List<FieldingRecordSaveData>(2);
            for (int position = (int)PlayerPosition.Catcher;
                 position <= (int)PlayerPosition.ReliefPitcher;
                 position++)
            {
                FieldingStatisticsState fielding = player.GetFielding((PlayerPosition)position);
                if (fielding == null) continue;
                result.Add(new FieldingRecordSaveData
                {
                    position = position,
                    defensiveOuts = fielding.DefensiveOuts,
                    opportunities = fielding.Opportunities,
                    successfulPlays = fielding.SuccessfulPlays,
                    putouts = fielding.Putouts,
                    assists = fielding.Assists,
                    errors = fielding.Errors,
                    doublePlays = fielding.DoublePlays,
                    difficultPlayAttempts = fielding.DifficultPlayAttempts,
                    difficultPlaysMade = fielding.DifficultPlaysMade,
                    expectedOuts = fielding.ExpectedOuts,
                    estimatedRunsSaved = fielding.EstimatedRunsSaved
                });
            }
            return result.ToArray();
        }

        private static void RestoreFielding(
            PlayerCompetitionStatisticsState player,
            FieldingRecordSaveData[] source)
        {
            if (source == null) return;
            for (int index = 0; index < source.Length; index++)
            {
                FieldingRecordSaveData saved = source[index];
                if (saved == null) continue;
                FieldingStatisticsState target = player.GetOrCreateFielding((PlayerPosition)saved.position);
                target.DefensiveOuts = saved.defensiveOuts;
                target.Opportunities = saved.opportunities;
                target.SuccessfulPlays = saved.successfulPlays;
                target.Putouts = saved.putouts;
                target.Assists = saved.assists;
                target.Errors = saved.errors;
                target.DoublePlays = saved.doublePlays;
                target.DifficultPlayAttempts = saved.difficultPlayAttempts;
                target.DifficultPlaysMade = saved.difficultPlaysMade;
                target.ExpectedOuts = saved.expectedOuts;
                target.EstimatedRunsSaved = saved.estimatedRunsSaved;
            }
        }
    }
}
