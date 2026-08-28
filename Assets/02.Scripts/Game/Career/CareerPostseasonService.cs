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
        /// 다음 포스트시즌 경기를 하나 진행한다. 내 선수 구단의 경기면 기용 판단과 기록까지 반영한다.
        /// </summary>
        public CareerPostseasonGameResult AdvanceNextGame()
        {
            PostseasonSeriesState series = EnsureCurrentSeries() ??
                                           throw new InvalidOperationException("이미 끝난 포스트시즌입니다.");

            int gameId = PostseasonGameIdBase + CountPlayedGames() + 1;
            ulong gameSeed = DeriveGameSeed(gameId);
            ScheduledGameState game = series.AppendNextGame(gameId, gameSeed);

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

            bool isPostseasonCompleted = false;
            if (series.IsCompleted && series.Round == PostseasonRound.ChampionshipSeries)
            {
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
                isPostseasonCompleted = true;
            }

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

        private ulong DeriveGameSeed(int gameId)
        {
            ulong stream = PostseasonStream ^ ((ulong)(uint)Season.SeasonId << 32) ^ (uint)gameId;
            return DeterministicSeed.Derive(_career.League.RandomSeed, stream);
        }
    }
}
