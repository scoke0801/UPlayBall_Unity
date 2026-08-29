using System;
using Baseball.Core.Balance;
using Baseball.Core.Teams;
using Baseball.Game.Career.News;
using Baseball.Game.Career.Narrative;
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
            int plateAppearances,
            int atBats,
            int runs,
            int hits,
            int doubles,
            int triples,
            int homeRuns,
            int runsBattedIn,
            int walks,
            int hitByPitches,
            int sacrificeFlies,
            int groundedIntoDoublePlays,
            int outsRecorded,
            int earnedRuns,
            int strikeouts,
            int walksAllowed,
            int hitBatters,
            int conditionBefore,
            int conditionAfter,
            int managerEvaluationBefore,
            int managerEvaluationAfter,
            int stolenBases = 0,
            int caughtStealing = 0,
            int sacrificeBunts = 0,
            int intentionalWalks = 0,
            int reachedOnErrors = 0,
            int pitchesThrown = 0,
            int inheritedRunners = 0,
            int inheritedRunnersScored = 0)
        {
            GameId = gameId;
            Round = round;
            OpponentTeamId = opponentTeamId;
            IsHome = isHome;
            TeamRuns = teamRuns;
            OpponentRuns = opponentRuns;
            Role = role;
            PlateAppearances = plateAppearances;
            AtBats = atBats;
            Runs = runs;
            Hits = hits;
            Doubles = doubles;
            Triples = triples;
            HomeRuns = homeRuns;
            RunsBattedIn = runsBattedIn;
            Walks = walks;
            HitByPitches = hitByPitches;
            SacrificeFlies = sacrificeFlies;
            GroundedIntoDoublePlays = groundedIntoDoublePlays;
            OutsRecorded = outsRecorded;
            EarnedRuns = earnedRuns;
            Strikeouts = strikeouts;
            WalksAllowed = walksAllowed;
            HitBatters = hitBatters;
            ConditionBefore = conditionBefore;
            ConditionAfter = conditionAfter;
            ManagerEvaluationBefore = managerEvaluationBefore;
            ManagerEvaluationAfter = managerEvaluationAfter;
            StolenBases = stolenBases;
            CaughtStealing = caughtStealing;
            SacrificeBunts = sacrificeBunts;
            IntentionalWalks = intentionalWalks;
            ReachedOnErrors = reachedOnErrors;
            PitchesThrown = pitchesThrown;
            InheritedRunners = inheritedRunners;
            InheritedRunnersScored = inheritedRunnersScored;
        }

        public int GameId { get; }
        public int Round { get; }
        public int OpponentTeamId { get; }
        public bool IsHome { get; }
        public int TeamRuns { get; }
        public int OpponentRuns { get; }
        public PlayerGameRole Role { get; }
        public int PlateAppearances { get; }
        public int AtBats { get; }
        public int Runs { get; }
        public int Hits { get; }
        public int Doubles { get; }
        public int Triples { get; }
        public int HomeRuns { get; }
        public int RunsBattedIn { get; }
        /// <summary>타자로서 얻은 볼넷이다.</summary>
        public int Walks { get; }
        /// <summary>타자로서 맞은 사구다.</summary>
        public int HitByPitches { get; }
        public int SacrificeFlies { get; }
        public int GroundedIntoDoublePlays { get; }
        public int OutsRecorded { get; }
        public int EarnedRuns { get; }
        public int Strikeouts { get; }
        /// <summary>투수로서 허용한 볼넷이다.</summary>
        public int WalksAllowed { get; }
        /// <summary>투수로서 맞힌 사구다.</summary>
        public int HitBatters { get; }
        public int ConditionBefore { get; }
        public int ConditionAfter { get; }
        public int ManagerEvaluationBefore { get; }
        public int ManagerEvaluationAfter { get; }
        public int StolenBases { get; }
        public int CaughtStealing { get; }
        public int SacrificeBunts { get; }
        public int IntentionalWalks { get; }
        public int ReachedOnErrors { get; }
        public int PitchesThrown { get; }
        public int InheritedRunners { get; }
        public int InheritedRunnersScored { get; }
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
            DateTime gameDate = GetGameDate(season.Year, game.Round);
            MatchInput input = _gameRunner.CreateMatchInput(
                game,
                game.PlannedPlayerRole,
                season.SeasonId,
                gameDate: gameDate);
            return new CareerMatchSession(
                game,
                input,
                gameDate,
                _career.MyPlayer.PlayerId,
                game.PlannedPlayerRole,
                CompetitionScope.RegularSeason,
                _balance,
                _career.MyPlayer.Condition,
                _career.MyPlayer.ManagerEvaluation,
                MatchNarrativeService.CaptureBaseline(
                    _career,
                    game,
                    game.PlannedPlayerRole,
                    CompetitionScope.RegularSeason),
                _career.GameSettings);
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

            MatchNarrativeBaseline narrativeBaseline = MatchNarrativeService.CaptureBaseline(
                _career,
                playerGame,
                playerGame.PlannedPlayerRole,
                CompetitionScope.RegularSeason);
            MatchResult playerMatchResult = _gameRunner.SimulateGame(
                playerGame,
                playerGame.PlannedPlayerRole,
                _career.CurrentLeague.CurrentSeason.SeasonId,
                gameDate: GetGameDate(
                    _career.CurrentLeague.CurrentSeason.Year,
                    playerGame.Round));
            return CompleteNextRound(playerGame, playerMatchResult, narrativeBaseline);
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

            return CompleteNextRound(playerGame, session.MatchResult, session.NarrativeBaseline);
        }

        private CareerGameAdvanceResult CompleteNextRound(
            ScheduledGameState playerGame,
            MatchResult preparedPlayerResult,
            MatchNarrativeBaseline narrativeBaseline)
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
            DateTime gameDate = GetGameDate(season.Year, playerGame.Round);
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
                    : _gameRunner.SimulateGame(
                        game,
                        role,
                        season.SeasonId,
                        gameDate: gameDate);
                game.Complete(matchResult.AwayBoxScore.Runs, matchResult.HomeBoxScore.Runs);
                statisticsService.RecordMatch(
                    matchResult,
                    CompetitionScope.RegularSeason,
                    game.Round,
                    isChampionship: false,
                    isSeriesClinching: false);
                RecordTeamResults(matchResult);
                _gameRunner.RecordPitcherUsage(matchResult, gameDate);

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
            _career.World.Calendar.AdvanceTo(gameDate);

            MatchNarrativeSnapshot narrative = MatchNarrativeService.CreateSnapshot(
                _career,
                narrativeBaseline,
                playerResult);
            season.RecordMatchNarrative(narrative);
            var reactionService = new CareerReactionService(_career);
            bool hasNextGame = NextPlayerGame != null;
            if (hasNextGame)
            {
                TradeInterestRecord[] previousInterests = CopyTradeInterests(_career.TradeState.Interests);
                TradeExecutionResult? trade = new TradeMarketService(_career, _balance)
                    .ProcessAfterScheduleDate();
                var occurredAt = new CareerDate(
                    new NewsCycleKey(season.SeasonId, SeasonPhase.RegularSeason, playerResult.Round),
                    gameDate);
                var tradeEvents = new TradeNarrativeNewsEvaluator().Evaluate(
                    _career,
                    occurredAt,
                    previousInterests,
                    trade);
                for (int index = 0; index < tradeEvents.Count; index++)
                {
                    _newsService.Collect(tradeEvents[index]);
                    if (tradeEvents[index].EventType is NewsEventType.TradeRumorReported or
                        NewsEventType.TradeNegotiationReported)
                    {
                        reactionService.TryCreateTradeDevelopment(
                            season.SeasonId,
                            playerResult.Round,
                            playerResult.GameId,
                            tradeEvents[index].FactSet.GetText(NewsFactKey.InterestedTeamName),
                            tradeEvents[index].EventType == NewsEventType.TradeNegotiationReported
                                ? TradeInterestStage.Negotiating
                                : TradeInterestStage.Rumor);
                    }
                }

                ContractOffer? extension = new ContractRenewalService(_career, _balance)
                    .BuildExtensionOffer();
                if (extension.HasValue)
                {
                    _newsService.Collect(new ContractNarrativeNewsEvaluator().EvaluateOffer(
                        _career,
                        occurredAt,
                        extension.Value));
                    reactionService.TryCreateContractOffer(
                        season.SeasonId,
                        playerResult.Round,
                        playerResult.GameId,
                        extension.Value.Team.Name);
                }
            }
            reactionService.TryCreateAfterMatch(narrative);
            _newsService.PublishRegularSeasonRound(
                playerResult,
                gameDate,
                narrative);
            if (!hasNextGame)
                BeginPostseason(season);
            else
            {
                EnsureNextGamePlan();
            }
            return playerResult;
        }

        private static TradeInterestRecord[] CopyTradeInterests(
            System.Collections.Generic.IReadOnlyList<TradeInterestRecord> source)
        {
            var copy = new TradeInterestRecord[source.Count];
            for (int index = 0; index < source.Count; index++)
                copy[index] = source[index];
            return copy;
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
            season.AttachReviewSnapshot(SeasonReviewSnapshot.CaptureRegularSeason(_career));
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
