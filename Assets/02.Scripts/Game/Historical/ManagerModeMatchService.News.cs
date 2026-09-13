using System;
using System.Collections.Generic;
using Baseball.Game.Career;
using Baseball.Game.Guide;

namespace Baseball.Game.Historical
{
    public sealed partial class ManagerModeMatchService
    {
        /// <summary>통계에 반영된 정규시즌 플레이어 경기만 보고 입력으로 투영한다.</summary>
        private static void RecordManagerNews(ManagerHistoricalRuntimeState runtime, PlayerIdMap ids, CareerGameResult game)
        {
            var season = runtime.ManagerMode.LiveSeason;
            var cards = new Dictionary<int, string>();
            foreach (var entry in runtime.GetRoster(runtime.PlayerTeamSeasonKey).Entries)
            {
                if (!runtime.WorldCardCatalog.TryGetCard(entry.CardId, out var card)) return;
                var definition = runtime.WorldCardCatalog.GetPlayerSeason(card);
                if (ids.TryGet(runtime.PlayerTeamSeasonKey, definition.PlayerSeasonId, out int id)) cards[id] = entry.CardId;
            }
            var players = new List<ManagerNewsPlayerLine>();
            foreach (var player in game.PlayerStatistics)
            {
                if (player.TeamId != season.PlayerTeamId || !cards.TryGetValue(player.PlayerId, out string cardId)) continue;
                var total = season.Statistics.RegularSeason.GetPlayer(player.PlayerId);
                players.Add(new ManagerNewsPlayerLine
                {
                    CardId = cardId, HasPitchingLine = player.HasPitchingLine, StartedPitching = player.StartedPitching,
                    PlateAppearances = player.PlateAppearances, Hits = player.Hits, HomeRuns = player.HomeRuns,
                    SeasonHomeRuns = total.Batting.HomeRuns, Outs = player.OutsRecorded, EarnedRuns = player.EarnedRuns,
                    Walks = player.WalksAllowed, Strikeouts = player.PitchingStrikeouts, SeasonStrikeouts = total.Pitching.Strikeouts
                });
            }
            players.Sort((left, right) => string.CompareOrdinal(left.CardId, right.CardId));
            bool home = game.HomeTeamId == season.PlayerTeamId;
            runtime.GuideProgress.RecordOfficialGame(season.SeasonNumber, season.GetCompletedGameCount(season.PlayerTeamId),
                ManagerLiveSeasonState.GamesPerOperationWeek, season.NextPlayerGame == null, game.Scope,
                home ? game.HomeScore : game.AwayScore, home ? game.AwayScore : game.HomeScore, players);
        }
    }
}
