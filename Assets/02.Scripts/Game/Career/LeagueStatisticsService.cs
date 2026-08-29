using System;
using System.Collections.Generic;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Match;

namespace Baseball.Game.Career
{
    /// <summary>MatchResult를 공통 DTO로 바꾸고 리그 전체 선수 통계에 한 번만 누적한다.</summary>
    public sealed class LeagueStatisticsService
    {
        private readonly LeagueSeasonStatisticsState _state;

        public LeagueStatisticsService(LeagueSeasonStatisticsState state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        public void RegisterPlayer(
            CompetitionScope scope,
            int playerId,
            string playerName,
            int teamId,
            PlayerPosition position)
        {
            _state.Get(scope).GetOrCreate(playerId, playerName, teamId, position);
        }

        public CareerGameResult RecordMatch(
            MatchResult result,
            CompetitionScope scope,
            int roundIndex,
            bool isChampionship,
            bool isSeriesClinching)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));

            List<PlayerGameStatistics> players = BuildPlayerStatistics(
                result,
                roundIndex,
                isChampionship,
                isSeriesClinching);
            var game = new CareerGameResult(
                result.Input.GameId,
                result.HomeBoxScore.TeamId,
                result.AwayBoxScore.TeamId,
                result.HomeBoxScore.Runs,
                result.AwayBoxScore.Runs,
                scope,
                players);

            CompetitionStatisticsState competition = _state.Get(scope);
            for (int index = 0; index < players.Count; index++)
            {
                PlayerGameStatistics player = players[index];
                competition.GetOrCreate(
                    player.PlayerId,
                    player.PlayerName,
                    player.TeamId,
                    player.PrimaryPosition);
            }
            competition.RecordTeamGame(game.HomeTeamId);
            competition.RecordTeamGame(game.AwayTeamId);
            for (int index = 0; index < players.Count; index++)
                competition.Add(players[index]);
            return game;
        }

        private static List<PlayerGameStatistics> BuildPlayerStatistics(
            MatchResult result,
            int roundIndex,
            bool isChampionship,
            bool isSeriesClinching)
        {
            var players = new List<PlayerGameStatistics>(24);
            AppendTeam(players, result.Input.AwayRoster, result.AwayBoxScore);
            AppendTeam(players, result.Input.HomeRoster, result.HomeBoxScore);
            ApplyPitchingDecisions(players, result);
            for (int index = 0; index < players.Count; index++)
            {
                PlayerGameStatistics player = players[index];
                double raw = CalculateContribution(player);
                double roundWeight = isChampionship ? 1.25d : 1d;
                double clinchingWeight = isSeriesClinching ? 1.15d : 1d;
                player.Contribution = new PlayerGameContributionState(
                    result.Input.GameId,
                    roundIndex,
                    isChampionship,
                    isSeriesClinching,
                    raw,
                    raw * roundWeight * clinchingWeight);
            }
            return players;
        }

        private static void AppendTeam(
            List<PlayerGameStatistics> target,
            MatchRosterSnapshot roster,
            TeamBoxScore box)
        {
            for (int index = 0; index < box.BattingLines.Count; index++)
            {
                PlayerBattingLine line = box.BattingLines[index];
                Player playerDefinition = FindPositionPlayer(roster, line.PlayerId, out bool started);
                if (playerDefinition == null)
                    continue;
                PlayerFieldingLine fieldingLine = FindFieldingLine(box, line.PlayerId);
                bool appeared = line.PlateAppearances > 0 || fieldingLine?.DefensiveOuts > 0 || started;
                if (!appeared)
                    continue;
                var player = new PlayerGameStatistics(
                    line.PlayerId,
                    playerDefinition.Name,
                    roster.TeamId,
                    playerDefinition.PrimaryPosition)
                {
                    HasBattingLine = line.PlateAppearances > 0,
                    StartedBatting = started,
                    PlateAppearances = line.PlateAppearances,
                    AtBats = line.AtBats,
                    Runs = line.Runs,
                    Hits = line.Hits,
                    Doubles = line.Doubles,
                    Triples = line.Triples,
                    HomeRuns = line.HomeRuns,
                    RunsBattedIn = line.RunsBattedIn,
                    Walks = line.Walks,
                    HitByPitches = line.HitByPitches,
                    BattingStrikeouts = line.Strikeouts,
                    StolenBases = line.StolenBases,
                    CaughtStealing = line.CaughtStealing,
                    SacrificeBunts = line.SacrificeBunts,
                    SacrificeFlies = line.SacrificeFlies,
                    IntentionalWalks = line.IntentionalWalks,
                    ReachedOnErrors = line.ReachedOnErrors,
                    GroundedIntoDoublePlays = line.GroundedIntoDoublePlays,
                    AppearedAsPinchHitter = line.AppearedAsPinchHitter,
                    AppearedAsPinchRunner = line.AppearedAsPinchRunner
                };
                player.FieldingLine = fieldingLine;
                target.Add(player);
            }

            for (int index = 0; index < box.PitchingLines.Count; index++)
            {
                PlayerPitchingLine line = box.PitchingLines[index];
                Player source = FindPitcher(roster, line.PlayerId, out bool started);
                if (source == null) continue;
                var player = new PlayerGameStatistics(
                    line.PlayerId,
                    source.Name,
                    roster.TeamId,
                    source.PrimaryPosition)
                {
                    HasPitchingLine = line.BattersFaced > 0,
                    StartedPitching = started,
                    OutsRecorded = line.OutsRecorded,
                    PitchesThrown = line.PitchesThrown,
                    HitsAllowed = line.HitsAllowed,
                    HomeRunsAllowed = line.HomeRunsAllowed,
                    WalksAllowed = line.WalksAllowed,
                    HitBatters = line.HitBatters,
                    PitchingStrikeouts = line.Strikeouts,
                    RunsAllowed = line.RunsAllowed,
                    EarnedRuns = line.EarnedRuns,
                    BattersFaced = line.BattersFaced,
                    InheritedRunners = line.InheritedRunners,
                    InheritedRunnersScored = line.InheritedRunnersScored,
                    Saves = line.HasSave ? 1 : 0,
                    Holds = line.HasHold ? 1 : 0,
                    BlownSaves = line.HasBlownSave ? 1 : 0,
                    QualityStarts = started && line.OutsRecorded >= 18 && line.EarnedRuns <= 3 ? 1 : 0
                };
                player.FieldingLine = FindFieldingLine(box, player.PlayerId);
                target.Add(player);
            }
        }

        private static Player FindPositionPlayer(
            MatchRosterSnapshot roster,
            int playerId,
            out bool started)
        {
            for (int index = 0; index < roster.StartingLineup.Count; index++)
            {
                if (roster.StartingLineup[index].Player.PlayerId != playerId)
                    continue;
                started = true;
                return roster.StartingLineup[index].Player;
            }
            for (int index = 0; index < roster.Bench.Count; index++)
            {
                if (roster.Bench[index].PlayerId != playerId)
                    continue;
                started = false;
                return roster.Bench[index];
            }
            started = false;
            return null;
        }

        private static Player FindPitcher(
            MatchRosterSnapshot roster,
            int playerId,
            out bool started)
        {
            if (roster.StartingPitcher.Player.PlayerId == playerId)
            {
                started = true;
                return roster.StartingPitcher.Player;
            }
            for (int index = 0; index < roster.Bullpen.Count; index++)
            {
                if (roster.Bullpen[index].Player.PlayerId != playerId)
                    continue;
                started = false;
                return roster.Bullpen[index].Player;
            }
            started = false;
            return null;
        }

        private static PlayerFieldingLine FindFieldingLine(TeamBoxScore box, int playerId)
        {
            for (int index = 0; index < box.FieldingLines.Count; index++)
            {
                if (box.FieldingLines[index].PlayerId == playerId)
                    return box.FieldingLines[index];
            }
            return null;
        }

        private static void ApplyPitchingDecisions(List<PlayerGameStatistics> players, MatchResult result)
        {
            if (result.IsTie) return;

            int winnerTeamId = result.WinnerTeamId;
            int loserTeamId = winnerTeamId == result.HomeBoxScore.TeamId
                ? result.AwayBoxScore.TeamId
                : result.HomeBoxScore.TeamId;
            PlayerGameStatistics winningPitcher = SelectDecisionPitcher(players, winnerTeamId);
            PlayerGameStatistics losingPitcher = SelectDecisionPitcher(players, loserTeamId);
            if (winningPitcher != null) winningPitcher.Wins = 1;
            if (losingPitcher != null) losingPitcher.Losses = 1;
        }

        private static PlayerGameStatistics SelectDecisionPitcher(
            List<PlayerGameStatistics> players,
            int teamId)
        {
            PlayerGameStatistics starter = null;
            PlayerGameStatistics reliever = null;
            for (int index = 0; index < players.Count; index++)
            {
                PlayerGameStatistics player = players[index];
                if (player.TeamId != teamId || !player.HasPitchingLine) continue;
                if (player.StartedPitching) starter = player;
                else reliever = player;
            }
            return starter != null && starter.OutsRecorded >= 15 ? starter : reliever ?? starter;
        }

        private static double CalculateContribution(PlayerGameStatistics player)
        {
            int singles = player.Hits - player.Doubles - player.Triples - player.HomeRuns;
            double batting = singles + player.Doubles * 1.7d + player.Triples * 2.4d +
                             player.HomeRuns * 3.2d + (player.Walks + player.HitByPitches) * 0.7d +
                             player.RunsBattedIn * 0.5d + player.Runs * 0.4d +
                             player.StolenBases * 0.5d - player.CaughtStealing * 0.7d -
                             player.GroundedIntoDoublePlays * 0.6d - player.BattingStrikeouts * 0.1d;
            double pitching = player.OutsRecorded * 0.18d + player.PitchingStrikeouts * 0.25d -
                              player.HitsAllowed * 0.25d - (player.WalksAllowed + player.HitBatters) * 0.30d -
                              player.EarnedRuns * 1.40d - player.HomeRunsAllowed * 0.60d +
                              player.Wins * 0.70d + player.Saves * 1.20d + player.Holds * 0.60d -
                              player.BlownSaves * 1.20d;
            double fielding = player.FieldingLine?.EstimatedRunsSaved ?? 0d;
            return batting + pitching + fielding;
        }
    }
}
