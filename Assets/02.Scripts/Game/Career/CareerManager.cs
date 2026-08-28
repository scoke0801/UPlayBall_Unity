using System;
using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Game.Manager;
using Baseball.Simulation.Career;

namespace Baseball.Game.Career
{
    /// <summary>
    /// 새 게임 이후의 영속 CareerState와 정규 시즌 진행을 소유한다.
    /// </summary>
    public sealed class CareerManager : ManagerBehaviour<CareerManager>
    {
        private CareerSeasonService _seasonService;
        private BalanceTable _balance;
        private CareerGameAdvanceResult? _lastGame;

        public override int InitializationOrder => -20;
        public bool HasActiveCareer => CurrentCareer != null;
        public CareerState CurrentCareer { get; private set; }
        public string LastError { get; private set; } = string.Empty;
        public CareerDashboardView Dashboard => BuildDashboard();

        public event Action CareerChanged;

        /// <summary>
        /// 계약을 마친 커리어의 정규 시즌 진행권을 인수한다.
        /// </summary>
        public void BeginCareer(CareerState career, BalanceTable balance)
        {
            CurrentCareer = career ?? throw new ArgumentNullException(nameof(career));
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            _seasonService = new CareerSeasonService(career, balance);
            _seasonService.EnsureNextGamePlan();
            _lastGame = null;
            LastError = string.Empty;
            CareerChanged?.Invoke();
        }

        /// <summary>
        /// 다음 경기 라운드를 즉시 시뮬레이션하고 대시보드를 갱신한다.
        /// </summary>
        public bool AdvanceNextGame()
        {
            if (_seasonService == null)
                return Fail("진행 중인 정규 시즌이 없습니다.");
            try
            {
                _lastGame = _seasonService.AdvanceNextRound();
                LastError = string.Empty;
                CareerChanged?.Invoke();
                return true;
            }
            catch (InvalidOperationException exception)
            {
                return Fail(exception.Message);
            }
        }

        /// <summary>
        /// 오프시즌을 마감하고 다음 시즌 정규 시즌으로 전환한 뒤 대시보드를 갱신한다.
        /// </summary>
        public bool CompleteOffseasonAndAdvanceToNextSeason()
        {
            if (CurrentCareer == null || _balance == null)
                return Fail("진행 중인 커리어가 없습니다.");
            try
            {
                new CareerSeasonTransitionService(CurrentCareer, _balance).AdvanceToNextSeason();
                _seasonService.EnsureNextGamePlan();
                _lastGame = null;
                LastError = string.Empty;
                CareerChanged?.Invoke();
                return true;
            }
            catch (InvalidOperationException exception)
            {
                return Fail(exception.Message);
            }
        }

        protected override void OnShutdown()
        {
            CareerChanged = null;
            CurrentCareer = null;
            _seasonService = null;
            _balance = null;
            _lastGame = null;
        }

        private bool Fail(string message)
        {
            LastError = message;
            CareerChanged?.Invoke();
            return false;
        }

        private CareerDashboardView BuildDashboard()
        {
            if (CurrentCareer == null || _balance == null)
                return null;

            PlayerState player = CurrentCareer.MyPlayer;
            SeasonState season = CurrentCareer.League.CurrentSeason;
            TeamState playerTeam = GetTeam(player.CurrentTeamId);
            TeamSeasonRecordState teamRecord = season.GetTeamRecord(player.CurrentTeamId);
            var evaluator = new PlayerValueEvaluator(_balance.PlayerEvaluation);
            Player currentPlayer = player.ToPlayer();
            return new CareerDashboardView
            {
                PlayerName = player.Name,
                Age = player.Age,
                Position = player.PrimaryPosition,
                BattingHand = player.BattingHand,
                ThrowingHand = player.ThrowingHand,
                BatterAttributes = currentPlayer.BatterAttributes,
                PitcherAttributes = currentPlayer.PitcherAttributes,
                Overall = evaluator.CalculatePositionValue(currentPlayer),
                Condition = player.Condition,
                ManagerEvaluation = player.ManagerEvaluation,
                TeamName = playerTeam.Name,
                SeasonYear = season.Year,
                LeagueLevel = season.LeagueLevel,
                SeasonPhase = season.Phase,
                AvailableMoney = CurrentCareer.AvailableMoney,
                TeamRank = CalculateRank(teamRecord),
                TeamWins = teamRecord?.Wins ?? 0,
                TeamLosses = teamRecord?.Losses ?? 0,
                TeamTies = teamRecord?.Ties ?? 0,
                NextGame = BuildNextGameView(),
                Statistics = new PlayerSeasonStatisticsView(
                    season.PlayerStatistics,
                    player.PrimaryPosition is PlayerPosition.StartingPitcher or PlayerPosition.ReliefPitcher),
                Competition = BuildCompetition(playerTeam, player, evaluator),
                UpcomingGames = BuildUpcomingGames(player.CurrentTeamId),
                RecentGames = BuildRecentGames(season.PlayerStatistics),
                LastGame = _lastGame
            };
        }

        private NextCareerGameView? BuildNextGameView()
        {
            ScheduledGameState game = _seasonService?.NextPlayerGame;
            if (game == null)
                return null;
            int playerTeamId = CurrentCareer.MyPlayer.CurrentTeamId;
            bool isHome = game.HomeTeamId == playerTeamId;
            int opponentTeamId = isHome ? game.AwayTeamId : game.HomeTeamId;
            return new NextCareerGameView(
                game.GameId,
                GetGameDate(CurrentCareer.League.CurrentSeason.Year, game.Round),
                GetTeam(game.AwayTeamId).Name,
                GetTeam(game.HomeTeamId).Name,
                GetTeam(opponentTeamId).Name,
                isHome,
                game.PlannedPlayerRole);
        }

        private PositionCompetitionView[] BuildCompetition(
            TeamState team,
            PlayerState player,
            PlayerValueEvaluator evaluator)
        {
            int count = 1;
            for (int index = 0; index < team.RosterCompetitors.Count; index++)
            {
                if (team.RosterCompetitors[index].Position == player.PrimaryPosition)
                    count++;
            }

            var result = new PositionCompetitionView[count];
            result[0] = new PositionCompetitionView(
                player.Name,
                evaluator.CalculatePositionValue(player.ToPlayer()),
                true);
            int resultIndex = 1;
            for (int index = 0; index < team.RosterCompetitors.Count; index++)
            {
                RosterCompetitorState competitor = team.RosterCompetitors[index];
                if (competitor.Position != player.PrimaryPosition)
                    continue;
                result[resultIndex++] = new PositionCompetitionView(
                    competitor.Name,
                    competitor.Overall,
                    false);
            }
            return result;
        }

        private UpcomingGameView[] BuildUpcomingGames(int playerTeamId)
        {
            var games = CurrentCareer.League.CurrentSeason.Schedule.Games;
            int count = 0;
            for (int index = 0; index < games.Count && count < 5; index++)
            {
                if (!games[index].IsCompleted && games[index].IncludesTeam(playerTeamId))
                    count++;
            }

            var result = new UpcomingGameView[count];
            int resultIndex = 0;
            for (int index = 0; index < games.Count && resultIndex < count; index++)
            {
                ScheduledGameState game = games[index];
                if (game.IsCompleted || !game.IncludesTeam(playerTeamId))
                    continue;
                bool isHome = game.HomeTeamId == playerTeamId;
                int opponentTeamId = isHome ? game.AwayTeamId : game.HomeTeamId;
                result[resultIndex] = new UpcomingGameView(
                    GetGameDate(CurrentCareer.League.CurrentSeason.Year, game.Round),
                    GetTeam(opponentTeamId).Name,
                    isHome,
                    resultIndex == 0);
                resultIndex++;
            }
            return result;
        }

        private static PlayerGameLogState[] BuildRecentGames(PlayerSeasonStatisticsState statistics)
        {
            var result = new PlayerGameLogState[statistics.RecentGames.Count];
            for (int index = 0; index < result.Length; index++)
                result[index] = statistics.RecentGames[result.Length - 1 - index];
            return result;
        }

        private int CalculateRank(TeamSeasonRecordState playerRecord)
        {
            if (playerRecord == null)
                return 1;
            int rank = 1;
            var records = CurrentCareer.League.CurrentSeason.TeamRecords;
            for (int index = 0; index < records.Count; index++)
            {
                TeamSeasonRecordState other = records[index];
                if (other.TeamId == playerRecord.TeamId)
                    continue;
                if (other.WinningPercentage > playerRecord.WinningPercentage ||
                    Math.Abs(other.WinningPercentage - playerRecord.WinningPercentage) < 0.000001d &&
                    other.Wins > playerRecord.Wins)
                {
                    rank++;
                }
            }
            return rank;
        }

        private TeamState GetTeam(int teamId)
        {
            for (int index = 0; index < CurrentCareer.League.Teams.Count; index++)
            {
                TeamState team = CurrentCareer.League.Teams[index];
                if (team.TeamId == teamId)
                    return team;
            }
            throw new InvalidOperationException($"TeamId {teamId}를 찾을 수 없습니다.");
        }

        private DateTime GetGameDate(int year, int round)
        {
            int playedDays = round - 1;
            int restDays = playedDays / _balance.CareerSeason.GamesBetweenRestDays;
            return new DateTime(
                    year,
                    _balance.CareerSeason.SeasonOpeningMonth,
                    _balance.CareerSeason.SeasonOpeningDay)
                .AddDays(playedDays + restDays);
        }
    }
}
