using System;
using System.Collections.Generic;
using Baseball.Core.Teams;

namespace Baseball.Game.Career
{
    /// <summary>일정 화면에서 표시할 경기 범위를 구분한다.</summary>
    public enum CareerScheduleScope
    {
        MyTeam,
        EntireLeague
    }

    /// <summary>내 구단 관점의 완료 경기 결과를 구분한다.</summary>
    public enum CareerScheduleOutcome
    {
        Pending,
        Win,
        Loss,
        Tie
    }

    /// <summary>달력·목록·우측 요약이 공유하는 한 경기의 읽기 전용 표시 값이다.</summary>
    public readonly struct CareerScheduleGameView
    {
        public CareerScheduleGameView(
            int gameId,
            int round,
            DateTime date,
            int awayTeamId,
            string awayTeamName,
            TeamColor awayTeamColor,
            int homeTeamId,
            string homeTeamName,
            TeamColor homeTeamColor,
            bool isCompleted,
            int awayRuns,
            int homeRuns,
            int playerTeamId,
            int awayTeamEmblemId = 0,
            int homeTeamEmblemId = 0)
        {
            GameId = gameId;
            Round = round;
            Date = date.Date;
            AwayTeamId = awayTeamId;
            AwayTeamName = awayTeamName;
            AwayTeamColor = awayTeamColor;
            HomeTeamId = homeTeamId;
            HomeTeamName = homeTeamName;
            HomeTeamColor = homeTeamColor;
            AwayTeamEmblemId = awayTeamEmblemId;
            HomeTeamEmblemId = homeTeamEmblemId;
            IsCompleted = isCompleted;
            AwayRuns = awayRuns;
            HomeRuns = homeRuns;
            IsPlayerGame = awayTeamId == playerTeamId || homeTeamId == playerTeamId;
            IsPlayerHome = homeTeamId == playerTeamId;
            OpponentTeamId = !IsPlayerGame ? 0 : IsPlayerHome ? awayTeamId : homeTeamId;
            OpponentName = !IsPlayerGame ? string.Empty : IsPlayerHome ? awayTeamName : homeTeamName;
            OpponentColor = !IsPlayerGame
                ? new TeamColor(90, 110, 130)
                : IsPlayerHome ? awayTeamColor : homeTeamColor;
            OpponentEmblemId = !IsPlayerGame
                ? 0
                : IsPlayerHome ? awayTeamEmblemId : homeTeamEmblemId;
            PlayerTeamRuns = !IsPlayerGame ? 0 : IsPlayerHome ? homeRuns : awayRuns;
            OpponentRuns = !IsPlayerGame ? 0 : IsPlayerHome ? awayRuns : homeRuns;
            Outcome = GetOutcome(IsPlayerGame, isCompleted, PlayerTeamRuns, OpponentRuns);
        }

        public int GameId { get; }
        public int Round { get; }
        public DateTime Date { get; }
        public int AwayTeamId { get; }
        public string AwayTeamName { get; }
        public TeamColor AwayTeamColor { get; }
        public int AwayTeamEmblemId { get; }
        public int HomeTeamId { get; }
        public string HomeTeamName { get; }
        public TeamColor HomeTeamColor { get; }
        public int HomeTeamEmblemId { get; }
        public bool IsCompleted { get; }
        public int AwayRuns { get; }
        public int HomeRuns { get; }
        public bool IsPlayerGame { get; }
        public bool IsPlayerHome { get; }
        public int OpponentTeamId { get; }
        public string OpponentName { get; }
        public TeamColor OpponentColor { get; }
        public int OpponentEmblemId { get; }
        public int PlayerTeamRuns { get; }
        public int OpponentRuns { get; }
        public CareerScheduleOutcome Outcome { get; }

        private static CareerScheduleOutcome GetOutcome(
            bool isPlayerGame,
            bool isCompleted,
            int playerRuns,
            int opponentRuns)
        {
            if (!isPlayerGame || !isCompleted)
                return CareerScheduleOutcome.Pending;
            if (playerRuns > opponentRuns)
                return CareerScheduleOutcome.Win;
            if (playerRuns < opponentRuns)
                return CareerScheduleOutcome.Loss;
            return CareerScheduleOutcome.Tie;
        }
    }

    /// <summary>42칸 달력에서 한 날짜가 표시할 경기와 휴식 상태다.</summary>
    public readonly struct CareerScheduleDayView
    {
        public CareerScheduleDayView(
            DateTime date,
            bool isVisibleMonth,
            bool isCurrentDate,
            bool isRestDay,
            CareerScheduleGameView[] games)
        {
            Date = date.Date;
            IsVisibleMonth = isVisibleMonth;
            IsCurrentDate = isCurrentDate;
            IsRestDay = isRestDay;
            Games = games ?? Array.Empty<CareerScheduleGameView>();
        }

        public DateTime Date { get; }
        public bool IsVisibleMonth { get; }
        public bool IsCurrentDate { get; }
        public bool IsRestDay { get; }
        public IReadOnlyList<CareerScheduleGameView> Games { get; }
    }

    /// <summary>한 달의 내 구단 승패와 홈·원정 분할 기록이다.</summary>
    public readonly struct CareerScheduleMonthSummaryView
    {
        public CareerScheduleMonthSummaryView(
            int completedGames,
            int wins,
            int losses,
            int ties,
            int homeGames,
            int homeWins,
            int homeLosses,
            int awayGames,
            int awayWins,
            int awayLosses)
        {
            CompletedGames = completedGames;
            Wins = wins;
            Losses = losses;
            Ties = ties;
            HomeGames = homeGames;
            HomeWins = homeWins;
            HomeLosses = homeLosses;
            AwayGames = awayGames;
            AwayWins = awayWins;
            AwayLosses = awayLosses;
        }

        public int CompletedGames { get; }
        public int Wins { get; }
        public int Losses { get; }
        public int Ties { get; }
        public int HomeGames { get; }
        public int HomeWins { get; }
        public int HomeLosses { get; }
        public int AwayGames { get; }
        public int AwayWins { get; }
        public int AwayLosses { get; }
        public double WinningPercentage => Wins + Losses == 0 ? 0d : Wins / (double)(Wins + Losses);
        public double HomeWinningPercentage => HomeWins + HomeLosses == 0
            ? 0d
            : HomeWins / (double)(HomeWins + HomeLosses);
        public double AwayWinningPercentage => AwayWins + AwayLosses == 0
            ? 0d
            : AwayWins / (double)(AwayWins + AwayLosses);
    }

    /// <summary>선택한 월과 범위에 맞게 완성된 일정 화면 한 장의 읽기 모델이다.</summary>
    public sealed class CareerScheduleMonthView
    {
        internal CareerScheduleMonthView(
            DateTime month,
            CareerScheduleScope scope,
            CareerScheduleDayView[] days,
            CareerScheduleGameView[] displayedGames,
            CareerScheduleMonthSummaryView summary)
        {
            Month = month;
            Scope = scope;
            Days = days;
            DisplayedGames = displayedGames;
            Summary = summary;
        }

        public DateTime Month { get; }
        public CareerScheduleScope Scope { get; }
        public IReadOnlyList<CareerScheduleDayView> Days { get; }
        public IReadOnlyList<CareerScheduleGameView> DisplayedGames { get; }
        public CareerScheduleMonthSummaryView Summary { get; }
    }

    /// <summary>한 시즌의 전체 경기와 일정 화면 우측 요약에 필요한 값을 보관한다.</summary>
    public sealed class CareerScheduleView
    {
        private readonly CareerScheduleGameView[] _games;

        internal CareerScheduleView(
            int seasonYear,
            LeagueLevel leagueLevel,
            SeasonPhase seasonPhase,
            long availableMoney,
            int playerTeamId,
            string playerTeamName,
            TeamColor playerTeamColor,
            int teamRank,
            int teamWins,
            int teamLosses,
            int teamTies,
            DateTime seasonStartDate,
            DateTime seasonEndDate,
            DateTime currentDate,
            CareerScheduleGameView[] games,
            CareerScheduleGameView[] recentGames,
            CareerScheduleGameView[] upcomingGames,
            int playerTeamEmblemId = 0)
        {
            SeasonYear = seasonYear;
            LeagueLevel = leagueLevel;
            SeasonPhase = seasonPhase;
            AvailableMoney = availableMoney;
            PlayerTeamId = playerTeamId;
            PlayerTeamName = playerTeamName;
            PlayerTeamColor = playerTeamColor;
            PlayerTeamEmblemId = playerTeamEmblemId;
            TeamRank = teamRank;
            TeamWins = teamWins;
            TeamLosses = teamLosses;
            TeamTies = teamTies;
            SeasonStartDate = seasonStartDate.Date;
            SeasonEndDate = seasonEndDate.Date;
            CurrentDate = currentDate.Date;
            _games = games;
            RecentGames = recentGames;
            UpcomingGames = upcomingGames;
        }

        public int SeasonYear { get; }
        public LeagueLevel LeagueLevel { get; }
        public SeasonPhase SeasonPhase { get; }
        public long AvailableMoney { get; }
        public int PlayerTeamId { get; }
        public string PlayerTeamName { get; }
        public TeamColor PlayerTeamColor { get; }
        public int PlayerTeamEmblemId { get; }
        public int TeamRank { get; }
        public int TeamWins { get; }
        public int TeamLosses { get; }
        public int TeamTies { get; }
        public DateTime SeasonStartDate { get; }
        public DateTime SeasonEndDate { get; }
        public DateTime CurrentDate { get; }
        public IReadOnlyList<CareerScheduleGameView> Games => _games;
        public IReadOnlyList<CareerScheduleGameView> RecentGames { get; }
        public IReadOnlyList<CareerScheduleGameView> UpcomingGames { get; }
        public CareerScheduleGameView? NextGame => UpcomingGames.Count == 0 ? null : UpcomingGames[0];

        /// <summary>선택한 월을 항상 6주·42칸 달력과 목록용 경기 배열로 구성한다.</summary>
        public CareerScheduleMonthView BuildMonth(int year, int month, CareerScheduleScope scope)
        {
            var firstOfMonth = new DateTime(year, month, 1);
            DateTime gridStart = firstOfMonth.AddDays(-(int)firstOfMonth.DayOfWeek);
            var days = new CareerScheduleDayView[42];
            for (int dayIndex = 0; dayIndex < days.Length; dayIndex++)
            {
                DateTime date = gridStart.AddDays(dayIndex);
                CareerScheduleGameView[] games = GetGamesOnDate(date, scope);
                bool hasPlayerGame = false;
                for (int gameIndex = 0; gameIndex < games.Length; gameIndex++)
                    hasPlayerGame |= games[gameIndex].IsPlayerGame;
                bool isRestDay = date >= SeasonStartDate && date <= SeasonEndDate && !hasPlayerGame;
                days[dayIndex] = new CareerScheduleDayView(
                    date,
                    date.Month == month,
                    date == CurrentDate,
                    isRestDay,
                    games);
            }

            CareerScheduleGameView[] displayedGames = GetGamesInMonth(year, month, scope);
            CareerScheduleMonthSummaryView summary = BuildSummary(year, month);
            return new CareerScheduleMonthView(firstOfMonth, scope, days, displayedGames, summary);
        }

        private CareerScheduleGameView[] GetGamesOnDate(DateTime date, CareerScheduleScope scope)
        {
            int count = 0;
            for (int index = 0; index < _games.Length; index++)
            {
                if (_games[index].Date == date.Date && IsInScope(_games[index], scope))
                    count++;
            }

            var result = new CareerScheduleGameView[count];
            int resultIndex = 0;
            for (int index = 0; index < _games.Length; index++)
            {
                if (_games[index].Date == date.Date && IsInScope(_games[index], scope))
                    result[resultIndex++] = _games[index];
            }
            return result;
        }

        private CareerScheduleGameView[] GetGamesInMonth(int year, int month, CareerScheduleScope scope)
        {
            int count = 0;
            for (int index = 0; index < _games.Length; index++)
            {
                CareerScheduleGameView game = _games[index];
                if (game.Date.Year == year && game.Date.Month == month && IsInScope(game, scope))
                    count++;
            }

            var result = new CareerScheduleGameView[count];
            int resultIndex = 0;
            for (int index = 0; index < _games.Length; index++)
            {
                CareerScheduleGameView game = _games[index];
                if (game.Date.Year == year && game.Date.Month == month && IsInScope(game, scope))
                    result[resultIndex++] = game;
            }
            return result;
        }

        private CareerScheduleMonthSummaryView BuildSummary(int year, int month)
        {
            int completed = 0;
            int wins = 0;
            int losses = 0;
            int ties = 0;
            int homeGames = 0;
            int homeWins = 0;
            int homeLosses = 0;
            int awayGames = 0;
            int awayWins = 0;
            int awayLosses = 0;
            for (int index = 0; index < _games.Length; index++)
            {
                CareerScheduleGameView game = _games[index];
                if (!game.IsPlayerGame || !game.IsCompleted || game.Date.Year != year || game.Date.Month != month)
                    continue;

                completed++;
                if (game.IsPlayerHome)
                {
                    homeGames++;
                    if (game.Outcome == CareerScheduleOutcome.Win)
                        homeWins++;
                    else if (game.Outcome == CareerScheduleOutcome.Loss)
                        homeLosses++;
                }
                else
                {
                    awayGames++;
                    if (game.Outcome == CareerScheduleOutcome.Win)
                        awayWins++;
                    else if (game.Outcome == CareerScheduleOutcome.Loss)
                        awayLosses++;
                }

                if (game.Outcome == CareerScheduleOutcome.Win) wins++;
                else if (game.Outcome == CareerScheduleOutcome.Loss) losses++;
                else if (game.Outcome == CareerScheduleOutcome.Tie) ties++;
            }

            return new CareerScheduleMonthSummaryView(
                completed,
                wins,
                losses,
                ties,
                homeGames,
                homeWins,
                homeLosses,
                awayGames,
                awayWins,
                awayLosses);
        }

        private static bool IsInScope(CareerScheduleGameView game, CareerScheduleScope scope)
        {
            return scope == CareerScheduleScope.EntireLeague || game.IsPlayerGame;
        }
    }
}
