using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Career;
using Baseball.Simulation.Growth;
using Baseball.Simulation.Match;
using Baseball.Simulation.Random;

namespace Baseball.Game.Career
{
    /// <summary>
    /// 한 경기의 감독 기용 판단·시뮬레이션·내 선수 기록 반영을 소유한다.
    /// 정규 시즌과 포스트시즌이 같은 판단과 같은 집계 경로를 쓰도록 한곳에 모았다.
    /// </summary>
    public sealed class CareerGameRunner
    {
        private readonly CareerState _career;
        private readonly LeagueState _league;
        private readonly BalanceTable _balance;
        private readonly ManagerUsageAi _managerUsageAi;
        private readonly ManagerLineupAi _managerLineupAi;
        private readonly SkillBoardService _skillBoardService;

        public CareerGameRunner(CareerState career, BalanceTable balance)
            : this(career, balance, career?.CurrentLeague)
        {
        }

        /// <summary>
        /// 배경 리그도 같은 경기 생성 경로를 사용하도록 대상 리그를 명시한다.
        /// </summary>
        public CareerGameRunner(CareerState career, BalanceTable balance, LeagueState league)
        {
            _career = career ?? throw new ArgumentNullException(nameof(career));
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            _league = league ?? throw new ArgumentNullException(nameof(league));
            _managerUsageAi = new ManagerUsageAi(balance.CareerSeason, balance.PlayerEvaluation);
            _managerLineupAi = new ManagerLineupAi(balance.ManagerLineup);
            _skillBoardService = new SkillBoardService(
                balance.Growth.SkillBoard,
                balance.Growth.SkillBlocks);
        }

        /// <summary>
        /// 화면 표시와 실제 경기 입력이 같은 판단을 쓰도록 기용 결정을 경기 상태에 한 번만 고정한다.
        /// </summary>
        public void EnsurePlayerRolePlan(ScheduledGameState game)
        {
            EnsurePlayerRolePlan(game, allowEvaluationOpportunity: true);
        }

        /// <summary>
        /// 정규시즌 평가 기회 허용 여부까지 포함해 한 경기의 기용 결정을 고정한다.
        /// </summary>
        public void EnsurePlayerRolePlan(ScheduledGameState game, bool allowEvaluationOpportunity)
        {
            if (game == null || game.HasPlayerRolePlan)
                return;
            if (_league.LeagueId != _career.MyPlayer.CurrentLeagueId)
                throw new InvalidOperationException("배경 리그에는 내 선수 기용 계획을 만들 수 없습니다.");

            TeamState team = GetTeam(_career.MyPlayer.CurrentTeamId);
            Player player = CreateMyPlayer();
            ulong decisionSeed = DeterministicSeed.Derive(game.RandomSeed, (ulong)player.PlayerId);
            PlayerGameRole role = _managerUsageAi.DecideRole(
                player,
                _career.CurrentExpectedRole,
                team.GetStrongestCompetitorOverall(player.PrimaryPosition),
                _career.MyPlayer.Condition,
                _career.MyPlayer.ManagerEvaluation,
                game.Round,
                allowEvaluationOpportunity,
                new Pcg32Random(decisionSeed));
            game.PlanPlayerRole(role);
        }

        /// <summary>
        /// 경기 Seed만으로 재현되는 한 경기를 시뮬레이션한다.
        /// </summary>
        public MatchResult SimulateGame(
            ScheduledGameState game,
            PlayerGameRole playerRole,
            int seasonId,
            bool requiresWinner = false,
            DateTime? gameDate = null)
        {
            MatchInput input = CreateMatchInput(game, playerRole, seasonId, requiresWinner, gameDate);
            return new MatchSimulator(_balance, MatchRandomStreams.Create(game.RandomSeed))
                .Simulate(input, NullMatchEventSink.Instance);
        }

        /// <summary>
        /// 화면 진행과 즉시 시뮬레이션이 동일한 잠금 입력을 사용하도록 경기 입력을 만든다.
        /// </summary>
        public MatchInput CreateMatchInput(
            ScheduledGameState game,
            PlayerGameRole playerRole,
            int seasonId,
            bool requiresWinner = false,
            DateTime? gameDate = null)
        {
            if (game == null)
                throw new ArgumentNullException(nameof(game));

            MatchRosterSnapshot awayRoster = BuildMatchRoster(
                game.AwayTeamId,
                game.Round,
                playerRole,
                game.RandomSeed,
                gameDate ?? ResolveGameDate(game.Round));
            MatchRosterSnapshot homeRoster = BuildMatchRoster(
                game.HomeTeamId,
                game.Round,
                playerRole,
                game.RandomSeed,
                gameDate ?? ResolveGameDate(game.Round));
            return new MatchInput(
                seasonId,
                game.GameId,
                game.RandomSeed,
                awayRoster,
                homeRoster,
                Baseball.Core.Rules.MatchRules.CreateDefault(requiresWinner));
        }

        private MatchRosterSnapshot BuildMatchRoster(
            int teamId,
            int round,
            PlayerGameRole playerRole,
            ulong gameSeed,
            DateTime gameDate)
        {
            Team compatibility = BuildMatchTeam(teamId, round, playerRole, gameSeed);
            TeamState team = GetTeam(teamId);
            bool isPlayerTeam = _league.LeagueId == _career.MyPlayer.CurrentLeagueId &&
                                teamId == _career.MyPlayer.CurrentTeamId;
            Player myPlayer = isPlayerTeam ? CreateMyPlayer() : null;
            var bullpen = new List<PitcherRosterEntry>(5);
            int startingSelection = round % 2;
            if (!(isPlayerTeam && playerRole == PlayerGameRole.StartingPitcher))
            {
                AddPitcherIfUnique(
                    bullpen,
                    CareerLineupPlan.CreateRosterPlayer(
                        _career.World,
                        team.GetCompetitor(PlayerPosition.StartingPitcher, 1 - startingSelection)),
                    PitcherRole.Swingman,
                    myPlayer,
                    gameDate);
            }
            else
            {
                AddPitcherIfUnique(
                    bullpen,
                    CareerLineupPlan.CreateRosterPlayer(
                        _career.World,
                        team.GetCompetitor(PlayerPosition.StartingPitcher, 0)),
                    PitcherRole.LongRelief,
                    myPlayer,
                    gameDate);
                AddPitcherIfUnique(
                    bullpen,
                    CareerLineupPlan.CreateRosterPlayer(
                        _career.World,
                        team.GetCompetitor(PlayerPosition.StartingPitcher, 1)),
                    PitcherRole.Swingman,
                    myPlayer,
                    gameDate);
            }

            AddPitcherIfUnique(
                bullpen,
                CareerLineupPlan.CreateRosterPlayer(
                    _career.World,
                    team.GetCompetitor(PlayerPosition.ReliefPitcher, 0)),
                PitcherRole.Setup,
                myPlayer,
                gameDate);
            AddPitcherIfUnique(
                bullpen,
                CareerLineupPlan.CreateRosterPlayer(
                    _career.World,
                    team.GetCompetitor(PlayerPosition.ReliefPitcher, 1)),
                PitcherRole.Closer,
                myPlayer,
                gameDate);
            if (isPlayerTeam && playerRole == PlayerGameRole.ReliefPitcher)
                AddPitcherIfUnique(bullpen, myPlayer, GetMyPlayerReliefRole(), null, gameDate);

            var bench = new List<Player>(10);
            if (isPlayerTeam && playerRole == PlayerGameRole.Bench)
                bench.Add(myPlayer);
            for (PlayerPosition position = PlayerPosition.Catcher;
                 position <= PlayerPosition.DesignatedHitter;
                 position++)
            {
                Player candidate = CareerLineupPlan.CreateRosterPlayer(
                    _career.World,
                    team.GetCompetitor(position, 1));
                if (!ContainsPlayer(compatibility.Lineup, candidate.PlayerId) &&
                    !ContainsPlayer(bench, candidate.PlayerId))
                {
                    bench.Add(candidate);
                }
            }

            PitcherRole starterRole = PitcherRole.Starter;
            int starterCondition = compatibility.StartingPitcher.PlayerId == _career.MyPlayer.PlayerId
                ? _career.MyPlayer.Condition
                : 100;
            return new MatchRosterSnapshot(
                team.TeamId,
                team.Name,
                compatibility.Lineup,
                CreatePitcherEntry(compatibility.StartingPitcher, starterRole, gameDate, starterCondition),
                bullpen,
                bench,
                CreateManagerTacticalProfile(team),
                RunningApproach.Balanced,
                isPlayerTeam ? _career.MyPlayer.PlayerId : 0);
        }

        private void AddPitcherIfUnique(
            List<PitcherRosterEntry> bullpen,
            Player pitcher,
            PitcherRole role,
            Player excluded,
            DateTime gameDate)
        {
            if (pitcher == null || pitcher.PlayerId == excluded?.PlayerId)
                return;
            for (int index = 0; index < bullpen.Count; index++)
            {
                if (bullpen[index].Player.PlayerId == pitcher.PlayerId)
                    return;
            }
            bullpen.Add(CreatePitcherEntry(pitcher, role, gameDate));
        }

        private PitcherRole GetMyPlayerReliefRole()
        {
            PitcherRole preferredRole = _career.CreationProfile.PreferredPitcherRole;
            return preferredRole is PitcherRole.LongRelief or
                PitcherRole.MiddleRelief or
                PitcherRole.Setup or
                PitcherRole.Closer
                ? preferredRole
                : PitcherRole.MiddleRelief;
        }

        /// <summary>경기 종료 후 모든 등판 투수의 당일 부하를 월드 상태에 반영한다.</summary>
        public void RecordPitcherUsage(MatchResult result, DateTime gameDate)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            for (int index = 0; index < result.PitcherUsage.Count; index++)
            {
                PitcherUsageReport usage = result.PitcherUsage[index];
                if (usage.PitchCount <= 0)
                    continue;
                _career.World.GetPlayer(usage.PlayerId)
                    .RecordPitchingUsage(gameDate, usage.PitchCount);
            }
        }

        private PitcherRosterEntry CreatePitcherEntry(
            Player pitcher,
            PitcherRole role,
            DateTime gameDate,
            int conditionOverride = -1)
        {
            PlayerState state = _career.World.GetPlayer(pitcher.PlayerId);
            int condition = conditionOverride >= 0
                ? conditionOverride
                : state.Condition > 0 ? state.Condition : 100;
            return new PitcherRosterEntry(
                pitcher,
                role,
                condition,
                state.GetRecentPitchingWorkload(gameDate));
        }

        private DateTime ResolveGameDate(int round)
        {
            SeasonState season = _league.CurrentSeason;
            if (season != null && season.Phase == SeasonPhase.RegularSeason)
            {
                int playedDays = round - 1;
                int restDays = playedDays / _balance.CareerSeason.GamesBetweenRestDays;
                return new DateTime(
                        season.Year,
                        _balance.CareerSeason.SeasonOpeningMonth,
                        _balance.CareerSeason.SeasonOpeningDay)
                    .AddDays(playedDays + restDays);
            }
            return _career.World.Calendar.CurrentDate.Date.AddDays(1);
        }

        private static bool ContainsPlayer(Lineup lineup, int playerId)
        {
            for (int index = 0; index < lineup.Count; index++)
            {
                if (lineup[index].Player.PlayerId == playerId)
                    return true;
            }
            return false;
        }

        private static bool ContainsPlayer(List<Player> players, int playerId)
        {
            for (int index = 0; index < players.Count; index++)
            {
                if (players[index].PlayerId == playerId)
                    return true;
            }
            return false;
        }

        private Player CreateMyPlayer()
        {
            Player player = _career.MyPlayer.ToPlayer(_skillBoardService);
            PitchRepertoireEntry[] repertoire = _career.CreationProfile?.PitchRepertoire;
            return repertoire == null || repertoire.Length == 0
                ? player
                : player.WithPitchRepertoire(repertoire);
        }

        private static ManagerTacticalProfile CreateManagerTacticalProfile(TeamState team)
        {
            TeamArchetypeProfile profile = team.Archetype;
            return new ManagerTacticalProfile(
                hookSpeed: profile.Scouting,
                bullpenAggression: (profile.RosterDepth + profile.Scouting) / 2,
                bullpenRoleRigidity: 45 + profile.RosterDepth / 5,
                smallBallPreference: 100 - profile.Budget,
                runningAggression: (profile.Development + profile.Scouting) / 2,
                matchupPreference: profile.Scouting,
                defensiveAggression: profile.RosterDepth,
                starTrust: profile.Budget);
        }

        /// <summary>
        /// 경기 결과를 지정된 기록 누적기에 반영한다. 정규 시즌과 포스트시즌은
        /// 서로 다른 누적기를 넘겨 기록이 절대 합산되지 않게 한다.
        /// </summary>
        public CareerGameAdvanceResult RecordPlayerResult(
            ScheduledGameState game,
            PlayerGameRole role,
            MatchResult result,
            PlayerSeasonStatisticsState statistics)
        {
            if (statistics == null)
                throw new ArgumentNullException(nameof(statistics));

            int playerTeamId = _career.MyPlayer.CurrentTeamId;
            bool isHome = game.HomeTeamId == playerTeamId;
            TeamBoxScore playerBox = isHome ? result.HomeBoxScore : result.AwayBoxScore;
            int teamRuns = playerBox.Runs;
            int opponentRuns = isHome ? result.AwayBoxScore.Runs : result.HomeBoxScore.Runs;
            int opponentTeamId = isHome ? game.AwayTeamId : game.HomeTeamId;
            PlayerBattingLine battingLine = FindBattingLine(playerBox, _career.MyPlayer.PlayerId);
            PlayerPitchingLine pitchingLine = FindPitchingLine(playerBox, _career.MyPlayer.PlayerId);
            int conditionBefore = _career.MyPlayer.Condition;
            int managerEvaluationBefore = _career.MyPlayer.ManagerEvaluation;
            int plateAppearances = battingLine?.PlateAppearances ?? 0;
            int atBats = battingLine?.AtBats ?? 0;
            int runs = battingLine?.Runs ?? 0;
            int hits = battingLine?.Hits ?? 0;
            int doubles = battingLine?.Doubles ?? 0;
            int triples = battingLine?.Triples ?? 0;
            int homeRuns = battingLine?.HomeRuns ?? 0;
            int runsBattedIn = battingLine?.RunsBattedIn ?? 0;
            int walks = battingLine?.Walks ?? 0;
            int hitByPitches = battingLine?.HitByPitches ?? 0;
            int sacrificeFlies = battingLine?.SacrificeFlies ?? 0;
            int groundedIntoDoublePlays = battingLine?.GroundedIntoDoublePlays ?? 0;
            int outsRecorded = pitchingLine?.OutsRecorded ?? 0;
            int earnedRuns = pitchingLine?.EarnedRuns ?? 0;
            int strikeouts = pitchingLine?.Strikeouts ?? battingLine?.Strikeouts ?? 0;
            int walksAllowed = pitchingLine?.WalksAllowed ?? 0;
            int hitBatters = pitchingLine?.HitBatters ?? 0;
            bool didAppear = battingLine?.PlateAppearances > 0 || pitchingLine?.BattersFaced > 0;
            ApplyPlayerFeedback(didAppear, battingLine, pitchingLine);

            statistics.AddGameLog(new PlayerGameLogState(
                game.GameId,
                opponentTeamId,
                isHome,
                teamRuns > opponentRuns,
                teamRuns,
                opponentRuns,
                role,
                atBats,
                hits,
                homeRuns,
                runsBattedIn,
                walks,
                hitByPitches,
                outsRecorded,
                earnedRuns,
                strikeouts,
                walksAllowed,
                hitBatters));
            return new CareerGameAdvanceResult(
                game.GameId,
                game.Round,
                opponentTeamId,
                isHome,
                teamRuns,
                opponentRuns,
                role,
                plateAppearances,
                atBats,
                runs,
                hits,
                doubles,
                triples,
                homeRuns,
                runsBattedIn,
                walks,
                hitByPitches,
                sacrificeFlies,
                groundedIntoDoublePlays,
                outsRecorded,
                earnedRuns,
                strikeouts,
                walksAllowed,
                hitBatters,
                conditionBefore,
                _career.MyPlayer.Condition,
                managerEvaluationBefore,
                _career.MyPlayer.ManagerEvaluation,
                battingLine?.StolenBases ?? 0,
                battingLine?.CaughtStealing ?? 0,
                battingLine?.SacrificeBunts ?? 0,
                battingLine?.IntentionalWalks ?? 0,
                battingLine?.ReachedOnErrors ?? 0,
                pitchingLine?.PitchesThrown ?? 0,
                pitchingLine?.InheritedRunners ?? 0,
                pitchingLine?.InheritedRunnersScored ?? 0);
        }

        private Team BuildMatchTeam(int teamId, int round, PlayerGameRole playerRole, ulong gameSeed)
        {
            TeamState team = GetTeam(teamId);
            bool isPlayerTeam = _league.LeagueId == _career.MyPlayer.CurrentLeagueId &&
                                teamId == _career.MyPlayer.CurrentTeamId;
            Player myPlayer = isPlayerTeam ? CreateMyPlayer() : null;
            Lineup lineup = CareerLineupPlan.BuildStartingLineup(
                team,
                _career.World,
                myPlayer,
                playerRole,
                _managerLineupAi);

            Player startingPitcher = isPlayerTeam && playerRole == PlayerGameRole.StartingPitcher
                ? myPlayer
                : CareerLineupPlan.CreateRosterPlayer(
                    _career.World,
                    team.GetCompetitor(PlayerPosition.StartingPitcher, round % 2));
            Player reliefPitcher = isPlayerTeam && playerRole == PlayerGameRole.ReliefPitcher
                ? myPlayer
                : CareerLineupPlan.CreateRosterPlayer(
                    _career.World,
                    team.GetCompetitor(PlayerPosition.ReliefPitcher, (round + 1) % 2));
            PositionPlayerSubstitutionPlan substitution = isPlayerTeam
                ? CreateBenchSubstitutionPlan(myPlayer, playerRole, gameSeed, lineup)
                : null;
            return new Team(
                team.TeamId,
                team.Name,
                lineup,
                startingPitcher,
                reliefPitcher,
                _balance.CareerSeason.ReliefStartInning,
                substitution);
        }

        private PositionPlayerSubstitutionPlan CreateBenchSubstitutionPlan(
            Player player,
            PlayerGameRole playerRole,
            ulong gameSeed,
            Lineup lineup)
        {
            if (playerRole != PlayerGameRole.Bench ||
                player.PrimaryPosition < PlayerPosition.Catcher ||
                player.PrimaryPosition > PlayerPosition.DesignatedHitter)
            {
                return null;
            }

            ulong decisionSeed = DeterministicSeed.Derive(gameSeed, unchecked((ulong)player.PlayerId) ^ 0x50484D47UL);
            var random = new Pcg32Random(decisionSeed);
            CareerSeasonBalance balance = _balance.CareerSeason;
            if (random.NextDouble() >= balance.BenchSubstitutionOpportunityProbability)
                return null;

            return new PositionPlayerSubstitutionPlan(
                player,
                CareerLineupPlan.GetBattingOrderIndex(lineup, player.PrimaryPosition),
                balance.BenchSubstitutionEarliestInning,
                balance.BenchSubstitutionMaximumScoreDifference);
        }

        private void ApplyPlayerFeedback(
            bool didAppear,
            PlayerBattingLine battingLine,
            PlayerPitchingLine pitchingLine)
        {
            CareerSeasonBalance balance = _balance.CareerSeason;
            int conditionDelta = didAppear
                ? -balance.PlayingConditionCost
                : balance.RestingConditionRecovery;
            int evaluationDelta = 0;
            if (battingLine != null)
            {
                if (battingLine.HomeRuns > 0 || battingLine.Hits >= balance.ProductiveBattingHits)
                {
                    evaluationDelta = battingLine.HomeRuns > 0 &&
                                      battingLine.Hits >= balance.ExcellentBattingHits
                        ? balance.ExcellentEvaluationChange
                        : balance.PositiveEvaluationChange;
                }
                else if (battingLine.AtBats >= balance.PoorBattingAtBats && battingLine.Hits == 0)
                {
                    evaluationDelta = balance.PoorEvaluationChange;
                }
            }
            else if (pitchingLine != null)
            {
                if (pitchingLine.EarnedRuns <= balance.QualityPitchingMaximumEarnedRuns)
                    evaluationDelta = balance.PositiveEvaluationChange;
                else if (pitchingLine.EarnedRuns >= balance.PoorPitchingMinimumEarnedRuns)
                    evaluationDelta = balance.VeryPoorEvaluationChange;
            }

            int maxChange = balance.MaximumManagerEvaluationChange;
            if (evaluationDelta > maxChange) evaluationDelta = maxChange;
            if (evaluationDelta < -maxChange) evaluationDelta = -maxChange;
            _career.MyPlayer.ApplyGameFeedback(
                conditionDelta,
                evaluationDelta,
                balance.MinimumCondition);
        }

        private TeamState GetTeam(int teamId)
        {
            for (int index = 0; index < _league.Teams.Count; index++)
            {
                TeamState team = _league.Teams[index];
                if (team.TeamId == teamId)
                    return team;
            }
            throw new InvalidOperationException($"TeamId {teamId}를 찾을 수 없습니다.");
        }

        private static PlayerBattingLine FindBattingLine(TeamBoxScore boxScore, int playerId)
        {
            for (int index = 0; index < boxScore.BattingLines.Count; index++)
            {
                if (boxScore.BattingLines[index].PlayerId == playerId)
                    return boxScore.BattingLines[index];
            }
            return null;
        }

        private static PlayerPitchingLine FindPitchingLine(TeamBoxScore boxScore, int playerId)
        {
            for (int index = 0; index < boxScore.PitchingLines.Count; index++)
            {
                if (boxScore.PitchingLines[index].PlayerId == playerId)
                    return boxScore.PitchingLines[index];
            }
            return null;
        }
    }
}
