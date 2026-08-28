using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Teams;
using Baseball.Simulation.Career;
using Baseball.Simulation.Match;

namespace Baseball.Game.Career
{
    /// <summary>
    /// 플레이어가 진행한 일정 라운드까지 다른 리그의 경기를 같은 규칙으로 확정한다.
    /// </summary>
    public sealed class WorldSeasonService
    {
        private readonly CareerState _career;
        private readonly BalanceTable _balance;

        public WorldSeasonService(CareerState career, BalanceTable balance)
        {
            _career = career ?? throw new ArgumentNullException(nameof(career));
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
        }

        public void AdvanceBackgroundLeaguesBefore(LeagueId leagueId, int round)
        {
            AdvanceBackgroundLeagues(leagueId, round, processBefore: true);
        }

        public void AdvanceBackgroundLeaguesAfter(LeagueId leagueId, int round)
        {
            AdvanceBackgroundLeagues(leagueId, round, processBefore: false);
        }

        private void AdvanceBackgroundLeagues(LeagueId leagueId, int round, bool processBefore)
        {
            IReadOnlyList<LeagueState> leagues = _career.World.Leagues;
            for (int index = 0; index < leagues.Count; index++)
            {
                LeagueState league = leagues[index];
                int comparison = league.LeagueId.CompareTo(leagueId);
                if (comparison == 0 || processBefore != (comparison < 0))
                    continue;
                AdvanceLeagueThroughRound(league, round);
            }
        }

        private void AdvanceLeagueThroughRound(LeagueState league, int targetRound)
        {
            SeasonState season = league.CurrentSeason;
            if (season?.Phase != SeasonPhase.RegularSeason)
                return;

            var gameRunner = new CareerGameRunner(_career, _balance, league);
            var statisticsService = new LeagueStatisticsService(season.LeagueStatistics);
            IReadOnlyList<ScheduledGameState> games = season.Schedule.Games;
            for (int index = 0; index < games.Count; index++)
            {
                ScheduledGameState game = games[index];
                if (game.IsCompleted || game.Round > targetRound)
                    continue;
                MatchResult result = gameRunner.SimulateGame(
                    game,
                    PlayerGameRole.Inactive,
                    season.SeasonId);
                game.Complete(result.AwayBoxScore.Runs, result.HomeBoxScore.Runs);
                statisticsService.RecordMatch(
                    result,
                    CompetitionScope.RegularSeason,
                    game.Round,
                    isChampionship: false,
                    isSeriesClinching: false);
                RecordTeamResults(season, result);
            }

            if (!HasIncompleteGame(season.Schedule))
                BeginPostseason(season);
        }

        private void BeginPostseason(SeasonState season)
        {
            var standings = new TeamStandingEntry[season.TeamRecords.Count];
            for (int index = 0; index < standings.Length; index++)
            {
                TeamSeasonRecordState record = season.TeamRecords[index];
                standings[index] = new TeamStandingEntry(
                    record.TeamId,
                    record.Wins,
                    record.Losses,
                    record.RunsScored,
                    record.RunsAllowed,
                    record.FixedTiebreaker,
                    record.GetHeadToHeadEntries());
            }
            int[] seeds = PostseasonBracket.SelectSeeds(standings, _balance.Postseason.PlayoffTeamCount);
            season.BeginPostseason(
                new PostseasonState(_career.SaveVersion, seeds),
                new PlayerSeasonStatisticsState());
        }

        private static bool HasIncompleteGame(SeasonScheduleState schedule)
        {
            for (int index = 0; index < schedule.Games.Count; index++)
            {
                if (!schedule.Games[index].IsCompleted)
                    return true;
            }
            return false;
        }

        private static void RecordTeamResults(SeasonState season, MatchResult result)
        {
            TeamSeasonRecordState away = season.GetTeamRecord(result.AwayBoxScore.TeamId);
            TeamSeasonRecordState home = season.GetTeamRecord(result.HomeBoxScore.TeamId);
            away.RecordGame(home.TeamId, result.AwayBoxScore.Runs, result.HomeBoxScore.Runs);
            home.RecordGame(away.TeamId, result.HomeBoxScore.Runs, result.AwayBoxScore.Runs);
        }
    }
}
