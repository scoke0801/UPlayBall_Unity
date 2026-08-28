using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Game.Manager;
using Baseball.Simulation.Career;

namespace Baseball.Game.Career
{
    /// <summary>
    /// 새 게임 이후의 영속 CareerState와 정규 시즌 진행을 소유한다.
    /// </summary>
    public sealed partial class CareerManager : ManagerBehaviour<CareerManager>
    {
        private CareerSeasonService _seasonService;
        private CareerSeasonTransitionService _seasonTransitionService;
        private BalanceTable _balance;
        private CareerGameAdvanceResult? _lastGame;
        private CareerMatchSession _activeMatch;
        private CareerSeasonAutoCompletionResult? _lastSeasonAutoCompletion;

        public override int InitializationOrder => -20;
        public bool HasActiveCareer => CurrentCareer != null;
        public CareerState CurrentCareer { get; private set; }
        public string LastError { get; private set; } = string.Empty;
        public CareerDashboardView Dashboard => BuildDashboard();
        public CareerContractView Contract => BuildContractView();
        public CareerMatchSession ActiveMatch => _activeMatch;
        public bool HasActiveMatch => _activeMatch != null;
        public LeagueHubView LeagueHub => BuildLeagueHub();
        public TeamOverviewView TeamOverview => BuildTeamOverview();

        public event Action CareerChanged;

        /// <summary>
        /// 저장되거나 새로 시작한 커리어를 현재 시즌 단계 그대로 인수한다.
        /// </summary>
        public void BeginCareer(CareerState career, BalanceTable balance)
        {
            CurrentCareer = career ?? throw new ArgumentNullException(nameof(career));
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            _seasonService = career.League.CurrentSeason.Phase == SeasonPhase.RegularSeason
                ? new CareerSeasonService(career, balance)
                : null;
            _seasonTransitionService = null;
            _seasonService?.EnsureNextGamePlan();
            _lastGame = null;
            _activeMatch = null;
            _lastSeasonAutoCompletion = null;
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
            if (_activeMatch != null)
                return Fail("준비하거나 진행 중인 경기를 먼저 마쳐야 합니다.");

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
        /// 남은 정규시즌과 포스트시즌을 자동 진행하고 선택이 필요한 시즌 결산에서 멈춘다.
        /// </summary>
        public bool AutoCompleteCurrentSeason()
        {
            if (CurrentCareer == null || _balance == null)
                return Fail("진행 중인 커리어가 없습니다.");
            if (_activeMatch != null)
                return Fail("준비하거나 진행 중인 경기를 먼저 마쳐야 합니다.");

            try
            {
                _lastSeasonAutoCompletion = new CareerSeasonAutoCompletionService(CurrentCareer, _balance)
                    .CompleteToSeasonReview();
                _seasonService = null;
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

        /// <summary>
        /// 다음 경기를 기록 변경 없이 준비 화면 상태로 연다.
        /// </summary>
        public bool PrepareNextGame()
        {
            if (_seasonService == null)
                return Fail("진행 중인 정규 시즌이 없습니다.");
            if (_activeMatch != null)
                return Fail("이미 준비하거나 진행 중인 경기가 있습니다.");

            try
            {
                _activeMatch = _seasonService.PrepareNextGame();
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
        /// 준비된 경기를 선택한 관전 방식으로 시작한다.
        /// </summary>
        public bool StartPreparedGame(CareerMatchMode mode)
        {
            return MutateActiveMatch(match => match.Start(mode));
        }

        /// <summary>
        /// 현재 투구에 선택한 타격 방식을 적용한다.
        /// </summary>
        public bool SubmitBattingApproach(BattingApproach approach)
        {
            return MutateActiveMatch(match => match.SubmitBattingApproach(approach));
        }

        /// <summary>
        /// 현재 타석의 남은 투구를 균형 타격으로 자동 진행한다.
        /// </summary>
        public bool AutoCompleteCurrentPlateAppearance()
        {
            return MutateActiveMatch(match => match.AutoCompleteCurrentPlateAppearance());
        }

        /// <summary>
        /// 이미 내린 선택은 유지하고 남은 경기를 자동 진행한다.
        /// </summary>
        public bool AutoCompleteActiveMatch()
        {
            return MutateActiveMatch(match => match.AutoCompleteMatch());
        }

        /// <summary>
        /// 시작 전 경기 준비를 닫고 홈으로 돌아간다.
        /// </summary>
        public bool CancelPreparedGame()
        {
            if (_activeMatch == null || _activeMatch.Phase != CareerMatchPhase.Preparation)
                return Fail("닫을 수 있는 경기 준비 화면이 없습니다.");

            _activeMatch = null;
            LastError = string.Empty;
            CareerChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// 기록 반영이 끝난 결과 화면을 닫고 갱신된 홈으로 돌아간다.
        /// </summary>
        public bool ReturnHomeFromCompletedMatch()
        {
            if (_activeMatch == null || !_activeMatch.IsCommitted)
                return Fail("홈으로 돌아갈 수 있는 경기 결과가 없습니다.");

            _activeMatch = null;
            LastError = string.Empty;
            CareerChanged?.Invoke();
            return true;
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
                _seasonTransitionService ??= new CareerSeasonTransitionService(CurrentCareer, _balance);
                if (_seasonTransitionService.Step == SeasonTransitionStep.NotStarted)
                {
                    SeasonTransitionStep step = _seasonTransitionService.BeginTransition();
                    if (step == SeasonTransitionStep.ContractOffers)
                    {
                        LastError = string.Empty;
                        CareerChanged?.Invoke();
                        return true;
                    }
                }

                if (_seasonTransitionService.Step == SeasonTransitionStep.ContractOffers)
                {
                    LastError = string.Empty;
                    CareerChanged?.Invoke();
                    return true;
                }

                CompleteSeasonTransition();
                return true;
            }
            catch (InvalidOperationException exception)
            {
                return Fail(exception.Message);
            }
        }

        /// <summary>
        /// 만료 계약의 다음 시즌 오퍼를 생성하고 플레이어 선택을 기다린다.
        /// </summary>
        public bool BeginContractNegotiation()
        {
            if (CurrentCareer == null || _balance == null)
                return Fail("진행 중인 커리어가 없습니다.");

            SeasonState season = CurrentCareer.League.CurrentSeason;
            if (season.Phase != SeasonPhase.Offseason)
                return Fail("시즌 결산과 오프시즌이 시작된 뒤 계약 오퍼를 확인할 수 있습니다.");
            if (CurrentCareer.CurrentContract.EndYear > season.Year)
                return Fail($"현재 계약은 {CurrentCareer.CurrentContract.EndYear} 시즌까지 유효합니다.");

            try
            {
                _seasonTransitionService ??= new CareerSeasonTransitionService(CurrentCareer, _balance);
                _seasonTransitionService.BeginTransition();
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
        /// 만료 후 제시된 오퍼 중 다음 계약 후보를 선택한다.
        /// </summary>
        public bool SelectContractOffer(int teamId)
        {
            if (_seasonTransitionService?.Step != SeasonTransitionStep.ContractOffers)
                return Fail("선택할 수 있는 계약 오퍼가 없습니다.");
            try
            {
                _seasonTransitionService.SelectRenewalOffer(teamId);
                LastError = string.Empty;
                CareerChanged?.Invoke();
                return true;
            }
            catch (ArgumentException exception)
            {
                return Fail(exception.Message);
            }
            catch (InvalidOperationException exception)
            {
                return Fail(exception.Message);
            }
        }

        /// <summary>
        /// 선택한 오퍼에 서명하고 소속·계약 이력·다음 시즌을 한 번에 확정한다.
        /// </summary>
        public bool SignSelectedContractOffer()
        {
            if (_seasonTransitionService?.Step != SeasonTransitionStep.ContractOffers)
                return Fail("서명할 계약 오퍼가 없습니다.");
            try
            {
                _seasonTransitionService.SignSelectedOffer();
                CompleteSeasonTransition();
                return true;
            }
            catch (InvalidOperationException exception)
            {
                return Fail(exception.Message);
            }
        }

        protected override void OnShutdown()
        {
            ResetGrowthRuntime();
            CareerChanged = null;
            CurrentCareer = null;
            _seasonService = null;
            _seasonTransitionService = null;
            _balance = null;
            _lastGame = null;
            _activeMatch = null;
            _lastSeasonAutoCompletion = null;
        }

        private bool Fail(string message)
        {
            LastError = message;
            CareerChanged?.Invoke();
            return false;
        }

        private void CompleteSeasonTransition()
        {
            _seasonService = new CareerSeasonService(CurrentCareer, _balance);
            _seasonService.EnsureNextGamePlan();
            _seasonTransitionService = null;
            _lastGame = null;
            _lastSeasonAutoCompletion = null;
            LastError = string.Empty;
            CareerChanged?.Invoke();
        }

        private CareerContractView BuildContractView()
        {
            if (CurrentCareer == null || _balance == null)
                return null;
            return new CareerContractViewBuilder(CurrentCareer, _balance)
                .Build(_seasonTransitionService, LastError);
        }

        private bool MutateActiveMatch(Action<CareerMatchSession> mutation)
        {
            if (_activeMatch == null)
                return Fail("준비하거나 진행 중인 경기가 없습니다.");

            try
            {
                mutation(_activeMatch);
                if (_activeMatch.IsComplete && !_activeMatch.IsCommitted)
                {
                    _lastGame = _seasonService.CompletePreparedGame(_activeMatch);
                    _activeMatch.MarkCommitted(
                        _lastGame.Value,
                        CurrentCareer.MyPlayer.Condition,
                        CurrentCareer.MyPlayer.ManagerEvaluation);
                }

                LastError = string.Empty;
                CareerChanged?.Invoke();
                return true;
            }
            catch (InvalidOperationException exception)
            {
                return Fail(exception.Message);
            }
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
            Player currentPlayer = BuildStablePlayer();
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
                ExpectedRole = CurrentCareer.CurrentContract.ExpectedRole,
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
                LastGame = _lastGame,
                RemainingRegularSeasonGames = CountRemainingRegularSeasonGames(player.CurrentTeamId),
                LastSeasonAutoCompletion = _lastSeasonAutoCompletion,
                SeasonProgress = BuildSeasonProgressView(season)
            };
        }

        private CareerSeasonProgressView BuildSeasonProgressView(SeasonState season)
        {
            PostseasonState postseason = season.Postseason;
            bool isQualified = false;
            int postseasonGames = 0;
            if (postseason != null)
            {
                for (int index = 0; index < postseason.SeedTeamIds.Count; index++)
                    isQualified |= postseason.SeedTeamIds[index] == CurrentCareer.MyPlayer.CurrentTeamId;
                for (int index = 0; index < postseason.Series.Count; index++)
                    postseasonGames += postseason.Series[index].Games.Count;
            }

            string championTeamName = postseason?.ChampionTeamId > 0
                ? GetTeam(postseason.ChampionTeamId).Name
                : string.Empty;
            int playerAwardCount = 0;
            if (season.Awards != null)
            {
                for (int index = 0; index < season.Awards.Results.Count; index++)
                {
                    if (season.Awards.Results[index].IncludesWinner(CurrentCareer.MyPlayer.PlayerId))
                        playerAwardCount++;
                }
            }

            int remainingWeeks = 0;
            if (CurrentCareer.CurrentOffseason != null && !CurrentCareer.CurrentOffseason.IsCompleted)
            {
                remainingWeeks = CurrentCareer.CurrentOffseason.TotalWeeks -
                                 CurrentCareer.CurrentOffseason.CurrentWeek + 1;
            }

            return new CareerSeasonProgressView(
                isQualified,
                championTeamName,
                postseason?.PlayerTeamResult ?? PlayerTeamPostseasonResult.DidNotQualify,
                postseasonGames,
                playerAwardCount,
                season.Settlement.SalaryIncome,
                season.Settlement.BonusIncome,
                remainingWeeks,
                season.Phase == SeasonPhase.Offseason &&
                CurrentCareer.CurrentContract.EndYear <= season.Year);
        }

        private LeagueHubView BuildLeagueHub()
        {
            if (CurrentCareer == null || _balance == null)
                return null;
            return new LeagueHubService(CurrentCareer, _balance).Build();
        }

        private TeamOverviewView BuildTeamOverview()
        {
            if (CurrentCareer == null || _balance == null)
                return null;
            return new TeamOverviewBuilder(_balance.PlayerEvaluation).Build(CurrentCareer);
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
                evaluator.CalculatePositionValue(BuildStablePlayer()),
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

        private int CountRemainingRegularSeasonGames(int playerTeamId)
        {
            IReadOnlyList<ScheduledGameState> games = CurrentCareer.League.CurrentSeason.Schedule?.Games;
            if (games == null)
                return 0;

            int count = 0;
            for (int index = 0; index < games.Count; index++)
            {
                if (!games[index].IsCompleted && games[index].IncludesTeam(playerTeamId))
                    count++;
            }
            return count;
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
