using System;
using System.Collections.Generic;
using Baseball.Core.Balance;

namespace Baseball.Game.Career
{
    /// <summary>현재 리그 포스트시즌 진행량에 맞춰 배경 리그 대회와 시상을 결정론적으로 확정한다.</summary>
    public sealed class WorldPostseasonService
    {
        private readonly CareerState _career;
        private readonly BalanceTable _balance;

        public WorldPostseasonService(CareerState career, BalanceTable balance)
        {
            _career = career ?? throw new ArgumentNullException(nameof(career));
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
        }

        public void AdvanceBackgroundLeaguesBefore(
            LeagueId activeLeagueId,
            int targetCompletedGameCount)
        {
            AdvanceBackgroundLeagues(activeLeagueId, targetCompletedGameCount, processBefore: true);
        }

        public void AdvanceBackgroundLeaguesAfter(
            LeagueId activeLeagueId,
            int targetCompletedGameCount)
        {
            AdvanceBackgroundLeagues(activeLeagueId, targetCompletedGameCount, processBefore: false);
        }

        /// <summary>현재 리그 우승 확정 시 공통 종료 주간까지 나머지 리그도 끝낸다.</summary>
        public void CompleteAllBackgroundLeagues(LeagueId activeLeagueId)
        {
            IReadOnlyList<LeagueState> leagues = _career.World.Leagues;
            for (int index = 0; index < leagues.Count; index++)
            {
                LeagueState league = leagues[index];
                if (league.LeagueId == activeLeagueId)
                    continue;
                CompleteLeague(league);
            }

            DateTime postseasonEnd = GetSharedPostseasonEndDate(_career.World.GetLeague(activeLeagueId));
            if (postseasonEnd > _career.World.Calendar.CurrentDate)
                _career.World.Calendar.AdvanceTo(postseasonEnd);
            RecordCompletedLeagueEvents();
        }

        private void AdvanceBackgroundLeagues(
            LeagueId activeLeagueId,
            int targetCompletedGameCount,
            bool processBefore)
        {
            IReadOnlyList<LeagueState> leagues = _career.World.Leagues;
            for (int index = 0; index < leagues.Count; index++)
            {
                LeagueState league = leagues[index];
                int comparison = league.LeagueId.CompareTo(activeLeagueId);
                if (comparison == 0 || processBefore != (comparison < 0))
                    continue;
                AdvanceLeagueThroughGameCount(league, targetCompletedGameCount);
            }
        }

        private void AdvanceLeagueThroughGameCount(
            LeagueState league,
            int targetCompletedGameCount)
        {
            if (league.CurrentSeason?.Phase != SeasonPhase.Postseason)
                return;

            var service = new CareerPostseasonService(
                _career,
                _balance,
                league,
                newsConfiguration: null,
                synchronizeWorld: false);
            while (league.CurrentSeason.Phase == SeasonPhase.Postseason &&
                   service.CountCompletedGames() < targetCompletedGameCount)
            {
                service.AdvanceNextGame();
            }
        }

        private void CompleteLeague(LeagueState league)
        {
            if (league.CurrentSeason?.Phase != SeasonPhase.Postseason)
                return;
            var service = new CareerPostseasonService(
                _career,
                _balance,
                league,
                newsConfiguration: null,
                synchronizeWorld: false);
            while (league.CurrentSeason.Phase == SeasonPhase.Postseason)
                service.AdvanceNextGame();
        }

        private void RecordCompletedLeagueEvents()
        {
            IReadOnlyList<LeagueState> leagues = _career.World.Leagues;
            for (int index = 0; index < leagues.Count; index++)
            {
                LeagueState league = leagues[index];
                PostseasonState postseason = league.CurrentSeason?.Postseason;
                if (postseason?.IsCompleted != true)
                    continue;
                string eventId = $"champion:{league.LeagueId}:{league.CurrentSeason.Year}";
                if (_career.World.DomainEvents.Contains(eventId))
                    continue;
                _career.World.DomainEvents.Append(new WorldDomainEvent(
                    eventId,
                    "LeagueChampion",
                    _career.World.Calendar.CurrentDate,
                    postseason.ChampionTeamId,
                    (int)league.LeagueLevel));
            }
        }

        private DateTime GetSharedPostseasonEndDate(LeagueState activeLeague)
        {
            int finalRegularSeasonRound = 0;
            IReadOnlyList<ScheduledGameState> games = activeLeague.CurrentSeason.Schedule.Games;
            for (int index = 0; index < games.Count; index++)
            {
                if (games[index].Round > finalRegularSeasonRound)
                    finalRegularSeasonRound = games[index].Round;
            }
            DateTime regularSeasonEnd = SeasonDateCalculator.GetGameDate(
                activeLeague.CurrentSeason.Year,
                finalRegularSeasonRound,
                _balance.CareerSeason);
            int sharedPostseasonDays = _balance.Postseason.SemifinalSeriesGames +
                                       _balance.Postseason.ChampionshipSeriesGames + 4;
            return regularSeasonEnd.AddDays(sharedPostseasonDays);
        }
    }
}
