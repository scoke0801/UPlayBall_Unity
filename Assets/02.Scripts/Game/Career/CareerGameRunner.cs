using System;
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
            Player player = _career.MyPlayer.ToPlayer(_skillBoardService);
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
            bool requiresWinner = false)
        {
            MatchInput input = CreateMatchInput(game, playerRole, seasonId, requiresWinner);
            return new MatchSimulator(_balance, new Pcg32Random(game.RandomSeed))
                .Simulate(input, NullMatchEventSink.Instance);
        }

        /// <summary>
        /// 화면 진행과 즉시 시뮬레이션이 동일한 잠금 입력을 사용하도록 경기 입력을 만든다.
        /// </summary>
        public MatchInput CreateMatchInput(
            ScheduledGameState game,
            PlayerGameRole playerRole,
            int seasonId,
            bool requiresWinner = false)
        {
            if (game == null)
                throw new ArgumentNullException(nameof(game));

            Team awayTeam = BuildMatchTeam(game.AwayTeamId, game.Round, playerRole, game.RandomSeed);
            Team homeTeam = BuildMatchTeam(game.HomeTeamId, game.Round, playerRole, game.RandomSeed);
            return new MatchInput(
                seasonId,
                game.GameId,
                game.RandomSeed,
                awayTeam,
                homeTeam,
                requiresWinner);
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
            int atBats = battingLine?.AtBats ?? 0;
            int hits = battingLine?.Hits ?? 0;
            int homeRuns = battingLine?.HomeRuns ?? 0;
            int runsBattedIn = battingLine?.RunsBattedIn ?? 0;
            int walks = battingLine?.Walks ?? 0;
            int hitByPitches = battingLine?.HitByPitches ?? 0;
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
                hitBatters);
        }

        private Team BuildMatchTeam(int teamId, int round, PlayerGameRole playerRole, ulong gameSeed)
        {
            TeamState team = GetTeam(teamId);
            bool isPlayerTeam = _league.LeagueId == _career.MyPlayer.CurrentLeagueId &&
                                teamId == _career.MyPlayer.CurrentTeamId;
            Player myPlayer = isPlayerTeam ? _career.MyPlayer.ToPlayer(_skillBoardService) : null;
            var slots = new LineupSlot[9];
            for (int index = 0; index < slots.Length; index++)
            {
                var position = (PlayerPosition)(index + 1);
                Player batter = isPlayerTeam &&
                                playerRole == PlayerGameRole.StartingBatter &&
                                myPlayer.PrimaryPosition == position
                    ? myPlayer
                    : CreateRosterPlayer(team.GetStrongestCompetitor(position));
                slots[index] = new LineupSlot(batter, position);
            }

            Player startingPitcher = isPlayerTeam && playerRole == PlayerGameRole.StartingPitcher
                ? myPlayer
                : CreateRosterPlayer(team.GetCompetitor(PlayerPosition.StartingPitcher, round % 2));
            Player reliefPitcher = isPlayerTeam && playerRole == PlayerGameRole.ReliefPitcher
                ? myPlayer
                : CreateRosterPlayer(team.GetCompetitor(PlayerPosition.ReliefPitcher, (round + 1) % 2));
            PositionPlayerSubstitutionPlan substitution = isPlayerTeam
                ? CreateBenchSubstitutionPlan(myPlayer, playerRole, gameSeed)
                : null;
            return new Team(
                team.TeamId,
                team.Name,
                new Lineup(slots),
                startingPitcher,
                reliefPitcher,
                _balance.CareerSeason.ReliefStartInning,
                substitution);
        }

        private PositionPlayerSubstitutionPlan CreateBenchSubstitutionPlan(
            Player player,
            PlayerGameRole playerRole,
            ulong gameSeed)
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
                (int)player.PrimaryPosition - 1,
                balance.BenchSubstitutionEarliestInning,
                balance.BenchSubstitutionMaximumScoreDifference);
        }

        private static Player CreateRosterPlayer(RosterCompetitorState competitor)
        {
            bool isPitcher = competitor.Position is PlayerPosition.StartingPitcher or PlayerPosition.ReliefPitcher;
            int batterRating = isPitcher ? 20 : competitor.Overall;
            int pitcherRating = isPitcher ? competitor.Overall : 20;
            Handedness battingHand = competitor.PlayerId % 3 == 0
                ? Handedness.Switch
                : competitor.PlayerId % 2 == 0 ? Handedness.Left : Handedness.Right;
            Handedness throwingHand = competitor.PlayerId % 4 == 0 ? Handedness.Left : Handedness.Right;
            return new Player(
                competitor.PlayerId,
                competitor.Name,
                competitor.Position,
                battingHand,
                throwingHand,
                new BatterAttributes(
                    batterRating,
                    batterRating,
                    batterRating,
                    batterRating,
                    batterRating,
                    batterRating),
                new PitcherAttributes(
                    pitcherRating,
                    pitcherRating,
                    pitcherRating,
                    pitcherRating,
                    pitcherRating,
                    pitcherRating));
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
