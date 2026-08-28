using System;
using System.Collections.Generic;
using Baseball.Core.Balance;

namespace Baseball.Game.Career
{
    /// <summary>세 리그의 결산·오프시즌·다음 개막 연도를 하나의 월드 경계로 맞춘다.</summary>
    public sealed class WorldSeasonLifecycleService
    {
        private readonly CareerState _career;
        private readonly BalanceTable _balance;

        public WorldSeasonLifecycleService(CareerState career, BalanceTable balance)
        {
            _career = career ?? throw new ArgumentNullException(nameof(career));
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
        }

        /// <summary>플레이어 성장 결산과 함께 배경 리그도 오프시즌 경계에 진입시킨다.</summary>
        public void BeginBackgroundOffseasons(LeagueId activeLeagueId)
        {
            LeagueState activeLeague = _career.World.GetLeague(activeLeagueId);
            bool usesTestShortcut = activeLeague.CurrentSeason.Postseason == null;
            IReadOnlyList<LeagueState> leagues = _career.World.Leagues;
            for (int index = 0; index < leagues.Count; index++)
            {
                LeagueState league = leagues[index];
                if (league.LeagueId == activeLeagueId ||
                    league.CurrentSeason.Phase != SeasonPhase.RegularSeason)
                {
                    continue;
                }
                if (!usesTestShortcut)
                    throw new InvalidOperationException($"{league.LeagueId} 정규시즌이 월드 포스트시즌보다 늦게 남았습니다.");
                league.CurrentSeason.CompleteRegularSeason();
            }

            new WorldPostseasonService(_career, _balance)
                .CompleteAllBackgroundLeagues(activeLeagueId);
            for (int index = 0; index < leagues.Count; index++)
            {
                LeagueState league = leagues[index];
                if (league.LeagueId == activeLeagueId)
                    continue;
                SeasonState season = league.CurrentSeason;
                if (season.Phase == SeasonPhase.SeasonReview)
                    season.BeginOffseason();
                else if (season.Phase != SeasonPhase.Offseason)
                    throw new InvalidOperationException($"{league.LeagueId}가 오프시즌 경계에 도달하지 못했습니다.");
            }
        }

        /// <summary>플레이어 리그 전환 뒤 나머지 리그도 같은 연도의 정규시즌으로 교체한다.</summary>
        public void AdvanceBackgroundLeaguesToNextSeason(
            LeagueId activeLeagueId,
            int nextYear)
        {
            var previousLeagues = new LeagueState[_career.World.Leagues.Count];
            for (int index = 0; index < previousLeagues.Length; index++)
                previousLeagues[index] = _career.World.Leagues[index];

            var rollover = new LeagueSeasonRolloverService(_balance);
            for (int index = 0; index < previousLeagues.Length; index++)
            {
                LeagueState league = previousLeagues[index];
                if (league.LeagueId == activeLeagueId)
                    continue;
                SeasonState completedSeason = league.CurrentSeason;
                if (completedSeason.Phase != SeasonPhase.Offseason)
                    throw new InvalidOperationException($"{league.LeagueId}가 다음 시즌 전환 가능한 상태가 아닙니다.");
                if (completedSeason.Year + 1 != nextYear)
                    throw new InvalidOperationException($"{league.LeagueId}의 다음 시즌 연도가 월드와 다릅니다.");

                completedSeason.CompleteArchive();
                int nextSeasonId = completedSeason.SeasonId + 1;
                TeamState[] nextTeams = rollover.AdvanceRosters(
                    league,
                    _career.World,
                    nextSeasonId);
                SeasonState nextSeason = rollover.BuildNextRegularSeason(
                    league,
                    nextTeams,
                    nextSeasonId,
                    nextYear);
                _career.World.ReplaceLeague(league.CreateNextSeason(
                    NewGameFlow.CurrentSaveVersion,
                    nextYear,
                    nextTeams,
                    nextSeason));
            }
        }

        /// <summary>시장 계획의 로스터를 사용해 활성 리그를 제외한 다음 시즌 리그 상태를 만든다.</summary>
        public LeagueState[] BuildBackgroundLeaguesToNextSeason(
            LeagueId activeLeagueId,
            int nextYear,
            WorldOffseasonMarketPlan marketPlan)
        {
            if (marketPlan == null) throw new ArgumentNullException(nameof(marketPlan));
            var result = new List<LeagueState>(_career.World.Leagues.Count - 1);
            var rollover = new LeagueSeasonRolloverService(_balance);
            for (int index = 0; index < _career.World.Leagues.Count; index++)
            {
                LeagueState league = _career.World.Leagues[index];
                if (league.LeagueId == activeLeagueId)
                    continue;
                SeasonState completedSeason = league.CurrentSeason;
                if (completedSeason.Phase != SeasonPhase.Offseason)
                    throw new InvalidOperationException($"{league.LeagueId}가 다음 시즌 전환 가능한 상태가 아닙니다.");
                if (completedSeason.Year + 1 != nextYear)
                    throw new InvalidOperationException($"{league.LeagueId}의 다음 시즌 연도가 월드와 다릅니다.");

                completedSeason.CompleteArchive();
                int nextSeasonId = completedSeason.SeasonId + 1;
                TeamState[] nextTeams = marketPlan.GetTeams(league.LeagueId);
                SeasonState nextSeason = rollover.BuildNextRegularSeason(
                    league,
                    nextTeams,
                    nextSeasonId,
                    nextYear);
                result.Add(league.CreateNextSeason(
                    NewGameFlow.CurrentSaveVersion,
                    nextYear,
                    nextTeams,
                    nextSeason));
            }
            return result.ToArray();
        }

        public void CompleteWorldTransition(int nextYear)
        {
            IReadOnlyList<LeagueState> leagues = _career.World.Leagues;
            for (int index = 0; index < leagues.Count; index++)
            {
                LeagueState league = leagues[index];
                if (league.LeagueYear != nextYear ||
                    league.CurrentSeason.Year != nextYear ||
                    league.CurrentSeason.Phase != SeasonPhase.RegularSeason)
                {
                    throw new InvalidOperationException($"{league.LeagueId}의 시즌 전환이 월드와 동기화되지 않았습니다.");
                }
            }

            DateTime openingDay = new DateTime(
                nextYear,
                _balance.CareerSeason.SeasonOpeningMonth,
                _balance.CareerSeason.SeasonOpeningDay);
            if (openingDay > _career.World.Calendar.CurrentDate)
                _career.World.Calendar.AdvanceTo(openingDay);
            _career.World.DomainEvents.Append(new WorldDomainEvent(
                $"world-season-start:{nextYear}",
                "WorldSeasonStarted",
                openingDay,
                nextYear,
                leagues.Count));
            _career.World.ValidateInvariants();
        }
    }
}
