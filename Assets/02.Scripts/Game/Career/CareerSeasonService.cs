using System;
using Baseball.Core.Balance;
using Baseball.Core.Teams;
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
            int outsRecorded,
            int earnedRuns,
            int strikeouts)
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
            OutsRecorded = outsRecorded;
            EarnedRuns = earnedRuns;
            Strikeouts = strikeouts;
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
        public int OutsRecorded { get; }
        public int EarnedRuns { get; }
        public int Strikeouts { get; }
    }

    /// <summary>
    /// CareerState를 입력으로 감독 기용, 리그 한 라운드, 기록 누적을 결정론적으로 진행한다.
    /// </summary>
    public sealed class CareerSeasonService
    {
        private readonly CareerState _career;
        private readonly BalanceTable _balance;
        private readonly CareerGameRunner _gameRunner;

        public CareerSeasonService(CareerState career, BalanceTable balance)
        {
            _career = career ?? throw new ArgumentNullException(nameof(career));
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            if (career.League.CurrentSeason?.Phase != SeasonPhase.RegularSeason)
                throw new InvalidOperationException("정규 시즌 상태의 커리어가 필요합니다.");
            _gameRunner = new CareerGameRunner(career, balance);
        }

        public ScheduledGameState NextPlayerGame =>
            _career.League.CurrentSeason.Schedule.GetNextGameForTeam(_career.MyPlayer.CurrentTeamId);

        /// <summary>
        /// 대시보드에서 보일 다음 경기 역할을 실제 시뮬레이션 전에 한 번만 확정한다.
        /// </summary>
        public void EnsureNextGamePlan()
        {
            _gameRunner.EnsurePlayerRolePlan(NextPlayerGame);
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

            SeasonState season = _career.League.CurrentSeason;
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
                MatchResult matchResult = _gameRunner.SimulateGame(game, role, season.SeasonId);
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

            if (NextPlayerGame == null)
                BeginPostseason(season);
            else
                EnsureNextGamePlan();
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
        }

        private void RecordTeamResults(MatchResult result)
        {
            SeasonState season = _career.League.CurrentSeason;
            TeamSeasonRecordState away = season.GetTeamRecord(result.AwayBoxScore.TeamId);
            TeamSeasonRecordState home = season.GetTeamRecord(result.HomeBoxScore.TeamId);
            away.RecordGame(home.TeamId, result.AwayBoxScore.Runs, result.HomeBoxScore.Runs);
            home.RecordGame(away.TeamId, result.HomeBoxScore.Runs, result.AwayBoxScore.Runs);
        }
    }
}
