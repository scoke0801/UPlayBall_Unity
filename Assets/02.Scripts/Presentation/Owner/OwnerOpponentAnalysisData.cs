using System.Collections.Generic;
using System.Globalization;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Game.Historical;

namespace Baseball.Presentation.Owner
{
    /// <summary>공개된 현재 로스터와 완료 대진을 비교 화면의 표시값으로 복사한다.</summary>
    internal static class OwnerOpponentAnalysisData
    {
        internal static readonly PlayerAbility[] Axes =
            { PlayerAbility.Contact, PlayerAbility.Power, PlayerAbility.Speed, PlayerAbility.Defense, PlayerAbility.Control };

        internal static void Populate(OwnerModeManager manager, ManagerPregamePreparation preparation,
            IDictionary<string, string> output)
        {
            var runtime = manager.Runtime;
            var live = runtime.ManagerMode.LiveSeason;
            var game = preparation.ScheduledGame;
            int opponentId = game.HomeTeamId == live.PlayerTeamId ? game.AwayTeamId : game.HomeTeamId;
            output["analysis.own.name"] = manager.GetTeamDisplayName(runtime.PlayerTeamSeasonKey);
            output["analysis.own.side"] = game.HomeTeamId == live.PlayerTeamId ? "H" : "A";
            output["analysis.opponent.side"] = game.HomeTeamId == opponentId ? "H" : "A";
            output["analysis.league"] = runtime.League.Grade + " 리그 · " + live.OriginYear + " 시즌";
            var rotation = preparation.PlanSnapshot?.StarterRotationCardIds;
            if (rotation != null && rotation.Count > 0)
            {
                string cardId = rotation[(game.Round - 1) % rotation.Count];
                if (runtime.WorldCardCatalog.TryGetCard(cardId, out var card))
                    output["analysis.own.starter"] = runtime.IdentityRegistry.GetPlayerDisplayName(
                        runtime.WorldCardCatalog.GetPlayerSeason(card).PlayerPersonId) + " · 예정 선발";
            }
            PopulateTeam(manager, runtime.PlayerTeamSeasonKey, live.PlayerTeamId, "own", output);
            PopulateTeam(manager, preparation.OpponentTeamSeasonKey, opponentId, "opponent", output);
        }

        private static void PopulateTeam(OwnerModeManager manager, string teamKey, int teamId,
            string side, IDictionary<string, string> output)
        {
            var runtime = manager.Runtime;
            var roster = runtime.GetRoster(teamKey);
            var totals = new int[Axes.Length];
            var counts = new int[Axes.Length];
            var pitchers = new List<string>();
            foreach (var entry in roster.Entries)
            {
                if (!runtime.WorldCardCatalog.TryGetCard(entry.CardId, out var card)) continue;
                var season = runtime.WorldCardCatalog.GetPlayerSeason(card);
                var ratings = season.CreateBaseAttributes();
                bool isBatter = season.PlayerType == PlayerType.Batter;
                for (int axis = 0; axis < Axes.Length; axis++)
                {
                    if (isBatter != (axis < 4)) continue;
                    totals[axis] += ratings.Get(Axes[axis]);
                    counts[axis]++;
                }
                if (!isBatter)
                    pitchers.Add(runtime.IdentityRegistry.GetPlayerDisplayName(season.PlayerPersonId));
            }
            for (int axis = 0; axis < Axes.Length; axis++)
                output[$"analysis.{side}.axis{axis}"] = counts[axis] == 0 ? "—" :
                    ((double)totals[axis] / counts[axis]).ToString("0.0", CultureInfo.InvariantCulture);
            output[$"analysis.{side}.pitchers"] = string.Join("\n", pitchers);

            // 최근 전적은 Schedule의 완료 경기만 사용한다. 역사 시즌 기록과 현재 시즌을 섞지 않는다.
            var games = new List<Baseball.Game.Career.ScheduledGameState>();
            foreach (var match in runtime.ManagerMode.LiveSeason.Schedule.Games)
                if (match.IsCompleted && (match.HomeTeamId == teamId || match.AwayTeamId == teamId)) games.Add(match);
            games.Sort((a, b) => { int order = a.Round.CompareTo(b.Round); return order != 0 ? order : a.GameId.CompareTo(b.GameId); });
            int wins = 0, losses = 0, ties = 0, recentWins = 0, recentLosses = 0, recentTies = 0;
            for (int index = 0; index < games.Count; index++)
            {
                var match = games[index];
                int difference = match.HomeTeamId == teamId ? match.HomeRuns - match.AwayRuns : match.AwayRuns - match.HomeRuns;
                if (difference > 0) wins++; else if (difference < 0) losses++; else ties++;
                if (index < games.Count - 10) continue;
                if (difference > 0) recentWins++; else if (difference < 0) recentLosses++; else recentTies++;
            }
            output[$"analysis.{side}.record"] = $"{games.Count}|{wins}|{losses}|{ties}|" +
                (wins + losses == 0 ? "—" : ((double)wins / (wins + losses)).ToString("0.000", CultureInfo.InvariantCulture));
            output[$"analysis.{side}.recent"] = $"최근 {System.Math.Min(10, games.Count)}경기  {recentWins}승 {recentLosses}패 {recentTies}무";
        }
    }
}
