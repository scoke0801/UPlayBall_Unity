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
            AppendTeam(players, result.Input.AwayTeam, result.AwayBoxScore);
            AppendTeam(players, result.Input.HomeTeam, result.HomeBoxScore);
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

        private static void AppendTeam(List<PlayerGameStatistics> target, Team team, TeamBoxScore box)
        {
            for (int index = 0; index < team.Lineup.Count; index++)
            {
                LineupSlot slot = team.Lineup[index];
                PlayerBattingLine line = box.BattingLines[index];
                var player = new PlayerGameStatistics(
                    line.PlayerId,
                    slot.Player.Name,
                    team.TeamId,
                    slot.FieldingPosition)
                {
                    HasBattingLine = true,
                    StartedBatting = true,
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
                    SacrificeFlies = line.SacrificeFlies,
                    GroundedIntoDoublePlays = line.GroundedIntoDoublePlays
                };
                player.FieldingLine = FindFieldingLine(box, player.PlayerId);
                target.Add(player);
            }

            AppendPositionPlayerSubstitute(target, team, box);

            for (int index = 0; index < box.PitchingLines.Count; index++)
            {
                PlayerPitchingLine line = box.PitchingLines[index];
                Player source = index == 0 ? team.StartingPitcher : team.ReliefPitcher;
                if (source == null) continue;
                var player = new PlayerGameStatistics(
                    line.PlayerId,
                    source.Name,
                    team.TeamId,
                    source.PrimaryPosition)
                {
                    HasPitchingLine = line.BattersFaced > 0,
                    StartedPitching = index == 0,
                    OutsRecorded = line.OutsRecorded,
                    HitsAllowed = line.HitsAllowed,
                    HomeRunsAllowed = line.HomeRunsAllowed,
                    WalksAllowed = line.WalksAllowed,
                    HitBatters = line.HitBatters,
                    PitchingStrikeouts = line.Strikeouts,
                    RunsAllowed = line.RunsAllowed,
                    EarnedRuns = line.EarnedRuns,
                    BattersFaced = line.BattersFaced,
                    QualityStarts = index == 0 && line.OutsRecorded >= 18 && line.EarnedRuns <= 3 ? 1 : 0
                };
                player.FieldingLine = FindFieldingLine(box, player.PlayerId);
                target.Add(player);
            }
        }

        private static void AppendPositionPlayerSubstitute(
            List<PlayerGameStatistics> target,
            Team team,
            TeamBoxScore box)
        {
            PositionPlayerSubstitutionPlan substitution = team.PositionPlayerSubstitution;
            if (substitution == null)
                return;

            PlayerBattingLine line = box.BattingLines[team.Lineup.Count];
            PlayerFieldingLine fieldingLine = FindFieldingLine(box, line.PlayerId);
            bool didAppear = line.PlateAppearances > 0 || fieldingLine?.DefensiveOuts > 0;
            if (!didAppear)
                return;

            var player = new PlayerGameStatistics(
                line.PlayerId,
                substitution.Player.Name,
                team.TeamId,
                substitution.Player.PrimaryPosition)
            {
                HasBattingLine = line.PlateAppearances > 0,
                StartedBatting = false,
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
                SacrificeFlies = line.SacrificeFlies,
                GroundedIntoDoublePlays = line.GroundedIntoDoublePlays,
                FieldingLine = fieldingLine
            };
            target.Add(player);
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

            int runMargin = Math.Abs(result.HomeBoxScore.Runs - result.AwayBoxScore.Runs);
            PlayerGameStatistics reliever = FindReliever(players, winnerTeamId);
            if (reliever != null && reliever.OutsRecorded >= 3 && runMargin <= 3)
                reliever.Saves = 1;
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

        private static PlayerGameStatistics FindReliever(List<PlayerGameStatistics> players, int teamId)
        {
            for (int index = 0; index < players.Count; index++)
            {
                PlayerGameStatistics player = players[index];
                if (player.TeamId == teamId && player.HasPitchingLine && !player.StartedPitching)
                    return player;
            }
            return null;
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
