using System;
using Baseball.Core.Balance;
using Baseball.Core.Teams;
using Baseball.Game.Career.News;
using Baseball.Simulation.Career;
using Baseball.Simulation.Match;

namespace Baseball.Game.Career
{
    /// <summary>
    /// 다음 경기 결과와 내 선수의 해당 경기 기록을 Presentation에 전달한다.
    /// </summary>
    public readonly struct CareerGameAdvanceResult
    {
        public CareerGameAdvanceResult(
            int gameId,
            int round,
            int opponentTeamId,
            bool isHome,
            int teamRuns,
            int opponentRuns,
            PlayerGameRole role,
            int atBats,
            int hits,
            int homeRuns,
            int runsBattedIn,
            int walks,
            int hitByPitches,
            int outsRecorded,
            int earnedRuns,
            int strikeouts,
            int walksAllowed,
            int hitBatters)
        {
            GameId = gameId;
            Round = round;
            OpponentTeamId = opponentTeamId;
            IsHome = isHome;
            TeamRuns = teamRuns;
            OpponentRuns = opponentRuns;
            Role = role;
            AtBats = atBats;
            Hits = hits;
            HomeRuns = homeRuns;
            RunsBattedIn = runsBattedIn;
            Walks = walks;
            HitByPitches = hitByPitches;
            OutsRecorded = outsRecorded;
            EarnedRuns = earnedRuns;
            Strikeouts = strikeouts;
            WalksAllowed = walksAllowed;
            HitBatters = hitBatters;
        }

        public int GameId { get; }
        public int Round { get; }
        public int OpponentTeamId { get; }
        public bool IsHome { get; }
        public int TeamRuns { get; }
        public int OpponentRuns { get; }
        public PlayerGameRole Role { get; }
        public int AtBats { get; }
        public int Hits { get; }
        public int HomeRuns { get; }
        public int RunsBattedIn { get; }
        /// <summary>타자로서 얻은 볼넷이다.</summary>
        public int Walks { get; }
        /// <summary>타자로서 맞은 사구다.</summary>
        public int HitByPitches { get; }
        public int OutsRecorded { get; }
        public int EarnedRuns { get; }
        public int Strikeouts { get; }
        /// <summary>투수로서 허용한 볼넷이다.</summary>
        public int WalksAllowed { get; }
        /// <summary>투수로서 맞힌 사구다.</summary>
        public int HitBatters { get; }
    }

    /// <summary>
    /// CareerState를 입력으로 감독 기용, 리그 한 라운드, 기록 누적을 결정론적으로 진행한다.
    /// </summary>
    public sealed class CareerSeasonService
    {
        private readonly CareerState _career;
        private readonly BalanceTable _balance;
        private readonly CareerGameRunner _gameRunner;
        private readonly CareerNewsService _newsService;

        public CareerSeasonService(CareerState career, BalanceTable balance)
            : this(career, balance, null)
        {
        }

        /// <summary>
        /// Unity Resources 없이도 동일한 시즌 진행을 검증할 수 있도록 뉴스 설정을 주입받는다.
        /// </summary>
        public CareerSeasonService(
            CareerState career,
            BalanceTable balance,
            CareerNewsConfiguration newsConfiguration)
        {
            _career = career ?? throw new ArgumentNullException(nameof(career));
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            if (career.CurrentLeague.CurrentSeason?.Phase != SeasonPhase.RegularSeason)
                throw new InvalidOperationException("정규 시즌 상태의 커리어가 필요합니다.");
            _gameRunner = new CareerGameRunner(career, balance);
            _newsService = new CareerNewsService(career, newsConfiguration);
        }

        public ScheduledGameState NextPlayerGame =>
            _career.CurrentLeague.CurrentSeason.Schedule.GetNextGameForTeam(_career.MyPlayer.CurrentTeamId);

        /// <summary>
        /// 대시보드에서 보일 다음 경기 역할을 실제 시뮬레이션 전에 한 번만 확정한다.
        /// </summary>
        public void EnsureNextGamePlan()
        {
            _gameRunner.EnsurePlayerRolePlan(NextPlayerGame);
        }

        /// <summary>
        /// 다음 경기의 역할과 입력을 잠그고 아직 기록에는 반영하지 않은 진행 세션을 만든다.
        /// </summary>
        public CareerMatchSession PrepareNextGame()
        {
            EnsureNextGamePlan();
            ScheduledGameState game = NextPlayerGame;
            if (game == null)
                throw new InvalidOperationException("준비할 정규 시즌 경기가 없습니다.");

            SeasonState season = _career.CurrentLeague.CurrentSeason;
            MatchInput input = _gameRunner.CreateMatchInput(
                game,
                game.PlannedPlayerRole,
                season.SeasonId);
            return new CareerMatchSession(
                game,
                input,
                GetGameDate(season.Year, game.Round),
                _career.MyPlayer.PlayerId,
                game.PlannedPlayerRole,
                CompetitionScope.RegularSeason,
                _balance,
                _career.MyPlayer.Condition,
                _career.MyPlayer.ManagerEvaluation);
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

        /// <summary>
        /// 내 선수의 다음 경기가 속한 라운드 전체를 진행해 순위와 개인 기록을 함께 갱신한다.
        /// 마지막 라운드가 끝나면 정규 시즌 순위로 포스트시즌 대진을 만들어 단계를 넘긴다.
        /// </summary>
        public CareerGameAdvanceResult AdvanceNextRound()
        {
            EnsureNextGamePlan();
            ScheduledGameState playerGame = NextPlayerGame;
            if (playerGame == null)
                throw new InvalidOperationException("진행할 정규 시즌 경기가 없습니다.");

            MatchResult playerMatchResult = _gameRunner.SimulateGame(
                playerGame,
                playerGame.PlannedPlayerRole,
                _career.CurrentLeague.CurrentSeason.SeasonId);
            return CompleteNextRound(playerGame, playerMatchResult);
        }

        /// <summary>
        /// 화면에서 완료한 내 선수 경기와 같은 라운드의 나머지 경기를 한 번만 기록한다.
        /// </summary>
        public CareerGameAdvanceResult CompletePreparedGame(CareerMatchSession session)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (!session.IsComplete || session.MatchResult == null)
                throw new InvalidOperationException("완료된 경기 세션이 필요합니다.");

            ScheduledGameState playerGame = NextPlayerGame;
            if (playerGame == null ||
                playerGame.GameId != session.ScheduledGame.GameId ||
                session.MatchResult.Input.RandomSeed != playerGame.RandomSeed)
            {
                throw new InvalidOperationException("현재 일정과 일치하지 않는 경기 결과입니다.");
            }

            return CompleteNextRound(playerGame, session.MatchResult);
        }

        private CareerGameAdvanceResult CompleteNextRound(
            ScheduledGameState playerGame,
            MatchResult preparedPlayerResult)
        {
            if (playerGame.IsCompleted)
                throw new InvalidOperationException("이미 기록한 경기입니다.");

            SeasonState season = _career.CurrentLeague.CurrentSeason;
            var worldSeasonService = new WorldSeasonService(_career, _balance);
            worldSeasonService.AdvanceBackgroundLeaguesBefore(
                _career.CurrentLeague.LeagueId,
                playerGame.Round);
            CareerGameAdvanceResult playerResult = default;
            bool hasPlayerResult = false;
            var statisticsService = new LeagueStatisticsService(season.LeagueStatistics);
            var games = season.Schedule.Games;
            for (int index = 0; index < games.Count; index++)
            {
                ScheduledGameState game = games[index];
                if (game.Round != playerGame.Round || game.IsCompleted)
                    continue;

                PlayerGameRole role = game.GameId == playerGame.GameId
                    ? playerGame.PlannedPlayerRole
                    : PlayerGameRole.Inactive;
                MatchResult matchResult = game.GameId == playerGame.GameId
                    ? preparedPlayerResult
                    : _gameRunner.SimulateGame(game, role, season.SeasonId);
                game.Complete(matchResult.AwayBoxScore.Runs, matchResult.HomeBoxScore.Runs);
                statisticsService.RecordMatch(
                    matchResult,
                    CompetitionScope.RegularSeason,
                    game.Round,
                    isChampionship: false,
                    isSeriesClinching: false);
                RecordTeamResults(matchResult);

                if (game.GameId == playerGame.GameId)
                {
                    playerResult = _gameRunner.RecordPlayerResult(
                        game,
                        role,
                        matchResult,
                        season.PlayerStatistics);
                    hasPlayerResult = true;
                }
            }

            if (!hasPlayerResult)
                throw new InvalidOperationException("내 선수 경기 결과를 찾지 못했습니다.");

            worldSeasonService.AdvanceBackgroundLeaguesAfter(
                _career.CurrentLeague.LeagueId,
                playerGame.Round);
            _career.World.Calendar.AdvanceTo(GetGameDate(season.Year, playerGame.Round));

            _newsService.PublishRegularSeasonRound(
                playerResult,
                GetGameDate(season.Year, playerGame.Round));
            if (NextPlayerGame == null)
                BeginPostseason(season);
            else
            {
                new TradeMarketService(_career, _balance).ProcessAfterScheduleDate();
                EnsureNextGamePlan();
            }
            return playerResult;
        }

        /// <summary>
        /// 정규 시즌 순위 상위 팀으로 계단식 대진을 만들어 시즌을 포스트시즌 단계로 넘긴다.
        /// </summary>
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
                new PlayerSeasonStatisticsState(),
                _career.MyPlayer);
            _career.MyPlayer.ApplyGameFeedback(
                _balance.CareerSeason.RestingConditionRecovery,
                managerEvaluationDelta: 0,
                _balance.CareerSeason.MinimumCondition);
        }

        private void RecordTeamResults(MatchResult result)
        {
            SeasonState season = _career.CurrentLeague.CurrentSeason;
            TeamSeasonRecordState away = season.GetTeamRecord(result.AwayBoxScore.TeamId);
            TeamSeasonRecordState home = season.GetTeamRecord(result.HomeBoxScore.TeamId);
            away.RecordGame(home.TeamId, result.AwayBoxScore.Runs, result.HomeBoxScore.Runs);
            home.RecordGame(away.TeamId, result.HomeBoxScore.Runs, result.AwayBoxScore.Runs);
        }
    }
}
