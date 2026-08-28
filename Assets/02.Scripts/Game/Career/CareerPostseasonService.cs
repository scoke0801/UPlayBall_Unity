using System;
using Baseball.Core.Balance;
using Baseball.Core.Teams;
using Baseball.Simulation.Career;
using Baseball.Simulation.Match;
using Baseball.Simulation.Random;

namespace Baseball.Game.Career
{
    /// <summary>
    /// 포스트시즌 한 경기의 결과를 Presentation에 전달한다. 내 선수가 뛰지 않은 경기면
    /// PlayerResult가 null이다.
    /// </summary>
    public readonly struct CareerPostseasonGameResult
    {
        public CareerPostseasonGameResult(
            PostseasonRound round,
            int gameNumber,
            int higherSeedTeamId,
            int lowerSeedTeamId,
            int higherSeedWins,
            int lowerSeedWins,
            int winnerTeamId,
            bool isSeriesCompleted,
            bool isPostseasonCompleted,
            int championTeamId,
            CareerGameAdvanceResult? playerResult)
        {
            Round = round;
            GameNumber = gameNumber;
            HigherSeedTeamId = higherSeedTeamId;
            LowerSeedTeamId = lowerSeedTeamId;
            HigherSeedWins = higherSeedWins;
            LowerSeedWins = lowerSeedWins;
            WinnerTeamId = winnerTeamId;
            IsSeriesCompleted = isSeriesCompleted;
            IsPostseasonCompleted = isPostseasonCompleted;
            ChampionTeamId = championTeamId;
            PlayerResult = playerResult;
        }

        public PostseasonRound Round { get; }
        public int GameNumber { get; }
        public int HigherSeedTeamId { get; }
        public int LowerSeedTeamId { get; }
        public int HigherSeedWins { get; }
        public int LowerSeedWins { get; }

        /// <summary>
        /// 이 경기의 승자다. 12이닝까지 승부가 나지 않은 무승부면 0이다.
        /// </summary>
        public int WinnerTeamId { get; }

        public bool IsSeriesCompleted { get; }
        public bool IsPostseasonCompleted { get; }
        public int ChampionTeamId { get; }
        public CareerGameAdvanceResult? PlayerResult { get; }
    }

    /// <summary>
    /// 정규 시즌 순위로 확정된 상위 4팀 토너먼트를 경기 단위로 진행한다.
    /// 정규 시즌과 같은 감독 기용 판단과 같은 집계 경로를 쓰되, 기록은 별도 누적기에 쌓는다.
    /// </summary>
    public sealed class CareerPostseasonService
    {
        private const ulong PostseasonStream = 0x504F535453454153UL;
        private const int PostseasonGameIdBase = 900_000;

        private readonly CareerState _career;
        private readonly BalanceTable _balance;
        private readonly CareerGameRunner _gameRunner;

        public CareerPostseasonService(CareerState career, BalanceTable balance)
        {
            _career = career ?? throw new ArgumentNullException(nameof(career));
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            if (career.League.CurrentSeason?.Phase != SeasonPhase.Postseason)
                throw new InvalidOperationException("포스트시즌 상태의 커리어가 필요합니다.");
            _gameRunner = new CareerGameRunner(career, balance);
        }

        private SeasonState Season => _career.League.CurrentSeason;
        private PostseasonState Postseason => Season.Postseason;

        public bool IsCompleted => Postseason.IsCompleted;
        public int ChampionTeamId => Postseason.ChampionTeamId;

        /// <summary>
        /// 내 선수의 구단이 포스트시즌에 진출했는지 여부다.
        /// </summary>
        public bool IsPlayerTeamQualified
        {
            get
            {
                int playerTeamId = _career.MyPlayer.CurrentTeamId;
                for (int index = 0; index < Postseason.SeedTeamIds.Count; index++)
                {
                    if (Postseason.SeedTeamIds[index] == playerTeamId)
                        return true;
                }
                return false;
            }
        }

        /// <summary>
        /// 진행 중인 시리즈를 반환한다. 아직 만들어지지 않았으면 다음 라운드를 연다.
        /// </summary>
        public PostseasonSeriesState EnsureCurrentSeries()
        {
            if (Postseason.IsCompleted)
                return null;

            if (Postseason.Series.Count == 0)
                InitializeSemifinals();

            PostseasonSeriesState current = Postseason.CurrentSeries;
            if (current != null)
                return current;

            if (Postseason.Series.Count == 2)
                return InitializeChampionship();

            return null;
        }

        /// <summary>
        /// 다른 구단 경기만 필요한 만큼 먼저 진행한 뒤 내 구단의 다음 경기를 준비한다.
        /// 경기 결과는 완료 세션을 다시 전달할 때까지 기록에 반영하지 않는다.
        /// </summary>
        public CareerMatchSession PrepareNextPlayerGame()
        {
            int playerTeamId = _career.MyPlayer.CurrentTeamId;
            if (!Postseason.CanTeamPlayNextGame(playerTeamId))
                throw new InvalidOperationException("내 구단이 치를 포스트시즌 경기가 없습니다.");

            PostseasonSeriesState series = AdvanceUntilPlayerSeries(playerTeamId);
            ScheduledGameState game = GetOrAppendNextGame(series);
            _gameRunner.EnsurePlayerRolePlan(game, allowEvaluationOpportunity: false);
            MatchInput input = _gameRunner.CreateMatchInput(
                game,
                game.PlannedPlayerRole,
                Season.SeasonId,
                requiresWinner: true);
            return new CareerMatchSession(
                game,
                input,
                GetGameDate(series),
                _career.MyPlayer.PlayerId,
                game.PlannedPlayerRole,
                CompetitionScope.Postseason,
                _balance,
                _career.MyPlayer.Condition,
                _career.MyPlayer.ManagerEvaluation);
        }

        /// <summary>
        /// 화면에서 완료한 내 구단 포스트시즌 경기를 시리즈와 별도 기록에 한 번만 반영한다.
        /// </summary>
        public CareerGameAdvanceResult CompletePreparedGame(CareerMatchSession session)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (session.CompetitionScope != CompetitionScope.Postseason)
                throw new InvalidOperationException("포스트시즌 경기 세션이 필요합니다.");
            if (!session.IsComplete || session.MatchResult == null)
                throw new InvalidOperationException("완료된 경기 세션이 필요합니다.");

            PostseasonSeriesState series = FindSeries(session.ScheduledGame.GameId);
            ScheduledGameState game = FindGame(series, session.ScheduledGame.GameId);
            if (game == null || game.IsCompleted ||
                session.MatchResult.Input.RandomSeed != game.RandomSeed)
            {
                throw new InvalidOperationException("현재 포스트시즌 일정과 일치하지 않는 경기 결과입니다.");
            }

            CareerPostseasonGameResult result = CompleteGame(
                series,
                game,
                session.PlayerRole,
                session.MatchResult,
                isPlayerGame: true);
            return result.PlayerResult ??
                   throw new InvalidOperationException("내 선수의 포스트시즌 경기 결과가 없습니다.");
        }

        /// <summary>
        /// 다음 포스트시즌 경기를 하나 진행한다. 내 선수 구단의 경기면 기용 판단과 기록까지 반영한다.
        /// </summary>
        public CareerPostseasonGameResult AdvanceNextGame()
        {
            PostseasonSeriesState series = EnsureCurrentSeries() ??
                                           throw new InvalidOperationException("이미 끝난 포스트시즌입니다.");

            ScheduledGameState game = GetOrAppendNextGame(series);

            bool isPlayerGame = game.IncludesTeam(_career.MyPlayer.CurrentTeamId);
            PlayerGameRole role = PlayerGameRole.Inactive;
            if (isPlayerGame)
            {
                _gameRunner.EnsurePlayerRolePlan(game, allowEvaluationOpportunity: false);
                role = game.PlannedPlayerRole;
            }

            MatchResult matchResult = _gameRunner.SimulateGame(
                game,
                role,
                Season.SeasonId,
                requiresWinner: true);
            return CompleteGame(series, game, role, matchResult, isPlayerGame);
        }

        /// <summary>
        /// 한국시리즈 우승이 확정될 때까지 남은 모든 경기를 진행한다.
        /// </summary>
        public CareerPostseasonGameResult AdvanceToChampion()
        {
            if (Postseason.IsCompleted)
                throw new InvalidOperationException("이미 끝난 포스트시즌입니다.");

            CareerPostseasonGameResult result = AdvanceNextGame();
            while (!result.IsPostseasonCompleted)
                result = AdvanceNextGame();
            return result;
        }

        private PostseasonSeriesState AdvanceUntilPlayerSeries(int playerTeamId)
        {
            PostseasonSeriesState series = EnsureCurrentSeries();
            while (series != null && !series.IncludesTeam(playerTeamId))
            {
                AdvanceNextGame();
                series = EnsureCurrentSeries();
            }

            return series ??
                   throw new InvalidOperationException("내 구단의 다음 포스트시즌 대진을 만들 수 없습니다.");
        }

        private CareerPostseasonGameResult CompleteGame(
            PostseasonSeriesState series,
            ScheduledGameState game,
            PlayerGameRole role,
            MatchResult matchResult,
            bool isPlayerGame)
        {
            if (game.IsCompleted)
                throw new InvalidOperationException("이미 기록한 포스트시즌 경기입니다.");
            if (matchResult.Input.GameId != game.GameId ||
                matchResult.Input.RandomSeed != game.RandomSeed)
            {
                throw new InvalidOperationException("포스트시즌 일정과 경기 결과가 일치하지 않습니다.");
            }

            game.Complete(matchResult.AwayBoxScore.Runs, matchResult.HomeBoxScore.Runs);
            int winnerTeamId = matchResult.WinnerTeamId;
            bool isSeriesClinching = winnerTeamId == series.HigherSeedTeamId
                ? series.HigherSeedWins + 1 >= series.WinsRequired
                : series.LowerSeedWins + 1 >= series.WinsRequired;
            new LeagueStatisticsService(Season.LeagueStatistics).RecordMatch(
                matchResult,
                CompetitionScope.Postseason,
                series.Round == PostseasonRound.ChampionshipSeries ? 1 : 0,
                series.Round == PostseasonRound.ChampionshipSeries,
                isSeriesClinching);

            CareerGameAdvanceResult? playerResult = null;
            if (isPlayerGame)
            {
                playerResult = _gameRunner.RecordPlayerResult(
                    game,
                    role,
                    matchResult,
                    Season.PostseasonPlayerStatistics);
            }

            series.RecordGameResult(winnerTeamId);
            bool isPostseasonCompleted = CompletePostseasonIfNeeded(series);
            return new CareerPostseasonGameResult(
                series.Round,
                game.Round,
                series.HigherSeedTeamId,
                series.LowerSeedTeamId,
                series.HigherSeedWins,
                series.LowerSeedWins,
                winnerTeamId,
                series.IsCompleted,
                isPostseasonCompleted,
                Postseason.ChampionTeamId,
                playerResult);
        }

        private bool CompletePostseasonIfNeeded(PostseasonSeriesState series)
        {
            if (!series.IsCompleted || series.Round != PostseasonRound.ChampionshipSeries)
                return false;

            int runnerUpTeamId = series.WinnerTeamId == series.HigherSeedTeamId
                ? series.LowerSeedTeamId
                : series.HigherSeedTeamId;
            Postseason.CompleteWithChampion(
                series.WinnerTeamId,
                runnerUpTeamId,
                _career.MyPlayer.CurrentTeamId);
            SeasonAwardsState awards = new SeasonAwardService(_balance.SeasonAwards)
                .Evaluate(Season, series.WinnerTeamId);
            Season.CompletePostseason(awards);
            return true;
        }

        private ScheduledGameState GetOrAppendNextGame(PostseasonSeriesState series)
        {
            if (series.Games.Count > 0)
            {
                ScheduledGameState pending = series.Games[series.Games.Count - 1];
                if (!pending.IsCompleted)
                    return pending;
            }

            int gameId = PostseasonGameIdBase + CountPlayedGames() + 1;
            return series.AppendNextGame(gameId, DeriveGameSeed(gameId));
        }

        private PostseasonSeriesState FindSeries(int gameId)
        {
            for (int seriesIndex = 0; seriesIndex < Postseason.Series.Count; seriesIndex++)
            {
                PostseasonSeriesState series = Postseason.Series[seriesIndex];
                if (FindGame(series, gameId) != null)
                    return series;
            }
            throw new InvalidOperationException("포스트시즌 경기의 시리즈를 찾을 수 없습니다.");
        }

        private static ScheduledGameState FindGame(PostseasonSeriesState series, int gameId)
        {
            if (series == null)
                return null;
            for (int gameIndex = 0; gameIndex < series.Games.Count; gameIndex++)
            {
                if (series.Games[gameIndex].GameId == gameId)
                    return series.Games[gameIndex];
            }
            return null;
        }

        private void InitializeSemifinals()
        {
            AddSemifinal(PostseasonSeriesId.SemifinalA);
            AddSemifinal(PostseasonSeriesId.SemifinalB);
        }

        private void AddSemifinal(PostseasonSeriesId seriesId)
        {
            int higherSeedIndex = PostseasonBracket.GetHigherSeedIndex(seriesId);
            int lowerSeedIndex = PostseasonBracket.GetLowerSeedIndex(seriesId);
            Postseason.AddSeries(new PostseasonSeriesState(
                seriesId,
                PostseasonRound.Semifinal,
                Postseason.GetSeedTeamId(higherSeedIndex),
                Postseason.GetSeedTeamId(lowerSeedIndex),
                _balance.Postseason.SemifinalSeriesGames));
        }

        private PostseasonSeriesState InitializeChampionship()
        {
            PostseasonSeriesState semifinalA = Postseason.GetSeries(PostseasonSeriesId.SemifinalA);
            PostseasonSeriesState semifinalB = Postseason.GetSeries(PostseasonSeriesId.SemifinalB);
            if (semifinalA?.IsCompleted != true || semifinalB?.IsCompleted != true)
                throw new InvalidOperationException("두 준결승이 끝나야 결승을 만들 수 있습니다.");

            int firstWinner = semifinalA.WinnerTeamId;
            int secondWinner = semifinalB.WinnerTeamId;
            int firstSeed = GetSeedIndex(firstWinner);
            int secondSeed = GetSeedIndex(secondWinner);
            int higherSeedTeamId = firstSeed < secondSeed ? firstWinner : secondWinner;
            int lowerSeedTeamId = higherSeedTeamId == firstWinner ? secondWinner : firstWinner;
            var championship = new PostseasonSeriesState(
                PostseasonSeriesId.Championship,
                PostseasonRound.ChampionshipSeries,
                higherSeedTeamId,
                lowerSeedTeamId,
                _balance.Postseason.ChampionshipSeriesGames);
            Postseason.AddSeries(championship);
            _career.MyPlayer.ApplyGameFeedback(
                _balance.CareerSeason.RestingConditionRecovery,
                managerEvaluationDelta: 0,
                _balance.CareerSeason.MinimumCondition);
            return championship;
        }

        private int GetSeedIndex(int teamId)
        {
            for (int index = 0; index < Postseason.SeedTeamIds.Count; index++)
            {
                if (Postseason.SeedTeamIds[index] == teamId)
                    return index;
            }
            throw new InvalidOperationException("시드에 없는 구단입니다.");
        }

        private int CountPlayedGames()
        {
            int total = 0;
            for (int index = 0; index < Postseason.Series.Count; index++)
                total += Postseason.Series[index].Games.Count;
            return total;
        }

        private DateTime GetGameDate(PostseasonSeriesState series)
        {
            int finalRegularSeasonRound = 0;
            for (int index = 0; index < Season.Schedule.Games.Count; index++)
            {
                if (Season.Schedule.Games[index].Round > finalRegularSeasonRound)
                    finalRegularSeasonRound = Season.Schedule.Games[index].Round;
            }

            DateTime regularSeasonEnd = SeasonDateCalculator.GetGameDate(
                Season.Year,
                finalRegularSeasonRound,
                _balance.CareerSeason);
            if (series.Round == PostseasonRound.Semifinal)
                return regularSeasonEnd.AddDays(series.Games.Count + 1);

            int semifinalDays = 0;
            PostseasonSeriesState semifinalA = Postseason.GetSeries(PostseasonSeriesId.SemifinalA);
            PostseasonSeriesState semifinalB = Postseason.GetSeries(PostseasonSeriesId.SemifinalB);
            if (semifinalA != null)
                semifinalDays = semifinalA.Games.Count;
            if (semifinalB != null && semifinalB.Games.Count > semifinalDays)
                semifinalDays = semifinalB.Games.Count;
            return regularSeasonEnd.AddDays(semifinalDays + series.Games.Count + 2);
        }

        private ulong DeriveGameSeed(int gameId)
        {
            ulong stream = PostseasonStream ^ ((ulong)(uint)Season.SeasonId << 32) ^ (uint)gameId;
            return DeterministicSeed.Derive(_career.League.RandomSeed, stream);
        }
    }
}
