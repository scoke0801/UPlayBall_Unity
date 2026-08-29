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
        private const int RaceEventMinimumGames = 60;

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
            int activeIndex = -1;
            for (int index = 0; index < leagues.Count; index++)
            {
                if (leagues[index].LeagueId == leagueId)
                {
                    activeIndex = index;
                    break;
                }
            }
            if (activeIndex < 0)
                throw new InvalidOperationException($"{leagueId}를 월드에서 찾을 수 없습니다.");
            for (int index = 0; index < leagues.Count; index++)
            {
                LeagueState league = leagues[index];
                if (index == activeIndex || processBefore != (index < activeIndex))
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
                DateTime gameDate = SeasonDateCalculator.GetGameDate(
                    season.Year,
                    game.Round,
                    _balance.CareerSeason);
                MatchResult result = gameRunner.SimulateGame(
                    game,
                    PlayerGameRole.Inactive,
                    season.SeasonId,
                    gameDate: gameDate);
                game.Complete(result.AwayBoxScore.Runs, result.HomeBoxScore.Runs);
                statisticsService.RecordMatch(
                    result,
                    CompetitionScope.RegularSeason,
                    game.Round,
                    isChampionship: false,
                    isSeriesClinching: false);
                RecordTeamResults(season, result);
                gameRunner.RecordPitcherUsage(result, gameDate);
            }

            if (!HasIncompleteGame(season.Schedule))
                BeginPostseason(season);
            else
                RecordLeagueRaceEvents(league, SeasonDateCalculator.GetGameDate(
                    season.Year,
                    targetRound,
                    _balance.CareerSeason));
        }

        /// <summary>
        /// 시즌 막판 순위가 실제 경쟁 구역에 들어온 순간을 팀·시즌별 한 번만 확정한다.
        /// </summary>
        public void RecordLeagueRaceEvents(LeagueState league, DateTime worldDate)
        {
            if (league == null) throw new ArgumentNullException(nameof(league));
            SeasonState season = league.CurrentSeason;
            if (season?.Phase != SeasonPhase.RegularSeason || season.TeamRecords.Count == 0)
                return;
            if (season.TeamRecords[0].GamesPlayed < RaceEventMinimumGames)
                return;

            var entries = new TeamStandingEntry[season.TeamRecords.Count];
            for (int index = 0; index < entries.Length; index++)
            {
                TeamSeasonRecordState record = season.TeamRecords[index];
                entries[index] = new TeamStandingEntry(
                    record.TeamId,
                    record.Wins,
                    record.Losses,
                    record.RunsScored,
                    record.RunsAllowed,
                    record.FixedTiebreaker,
                    record.GetHeadToHeadEntries());
            }

            int[] orderedTeamIds = PostseasonBracket.SelectSeeds(entries, entries.Length);
            LeagueDefinition definition = WorldGenerationConfiguration.GetDefaultDefinition(league.LeagueLevel);
            for (int index = 0; index < orderedTeamIds.Length; index++)
            {
                int rank = index + 1;
                if (definition.PromotionSlots > 0 && rank <= definition.PromotionSlots)
                {
                    AppendRaceEvent(
                        season,
                        orderedTeamIds[index],
                        rank,
                        "PromotionRaceEntered",
                        worldDate);
                }
                else if (definition.RelegationSlots > 0 &&
                         rank > orderedTeamIds.Length - definition.RelegationSlots)
                {
                    AppendRaceEvent(
                        season,
                        orderedTeamIds[index],
                        rank,
                        "RelegationRiskEntered",
                        worldDate);
                }
            }
        }

        private void AppendRaceEvent(
            SeasonState season,
            int teamId,
            int rank,
            string eventType,
            DateTime worldDate)
        {
            string eventId = $"league-race:{season.SeasonId}:{teamId}:{eventType}";
            if (_career.World.DomainEvents.Contains(eventId))
                return;
            _career.World.DomainEvents.Append(new WorldDomainEvent(
                eventId,
                eventType,
                worldDate,
                teamId,
                rank));
        }

        private void BeginPostseason(SeasonState season)
        {
            LeagueState league = FindLeague(season);
            int[] finalStandings = new LeagueMovementPlanner(_career, _balance)
                .ResolveFinalStandings(league, out LeagueTiebreakGameState[] tiebreakGames);
            season.FinalizeStandings(finalStandings, tiebreakGames);
            int[] seeds = CopyPostseasonSeeds(finalStandings);
            season.BeginPostseason(
                new PostseasonState(_career.SaveVersion, seeds),
                new PlayerSeasonStatisticsState());
        }

        private LeagueState FindLeague(SeasonState season)
        {
            for (int index = 0; index < _career.World.Leagues.Count; index++)
            {
                if (ReferenceEquals(_career.World.Leagues[index].CurrentSeason, season))
                    return _career.World.Leagues[index];
            }
            throw new InvalidOperationException("현재 시즌을 소유한 리그를 찾을 수 없습니다.");
        }

        private int[] CopyPostseasonSeeds(int[] finalStandings)
        {
            int count = _balance.Postseason.PlayoffTeamCount;
            var result = new int[count];
            Array.Copy(finalStandings, result, count);
            return result;
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
