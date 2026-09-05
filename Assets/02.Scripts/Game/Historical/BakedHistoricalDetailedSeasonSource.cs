using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Rules;
using Baseball.Core.Teams;
using Baseball.Simulation.Career;
using Baseball.Simulation.Historical;
using Baseball.Simulation.Match;
using Baseball.Simulation.Random;

namespace Baseball.Game.Historical
{
    /// <summary>한 연도 Historical Simulation의 실행량과 할당량을 기록한다.</summary>
    public sealed class HistoricalSeasonSimulationMetrics
    {
        private readonly string[] _regularTeamSeasonKeys;

        public HistoricalSeasonSimulationMetrics(
            int seasonYear,
            IReadOnlyList<string> regularTeamSeasonKeys,
            int regularSeasonGameCount,
            int allStarGameCount,
            int postseasonGameCount,
            long elapsedTicks,
            long allocatedBytes,
            bool usesExactAllocationCounter)
        {
            SeasonYear = seasonYear;
            _regularTeamSeasonKeys = new string[regularTeamSeasonKeys.Count];
            for (int index = 0; index < regularTeamSeasonKeys.Count; index++)
                _regularTeamSeasonKeys[index] = regularTeamSeasonKeys[index];
            RegularSeasonGameCount = regularSeasonGameCount;
            AllStarGameCount = allStarGameCount;
            PostseasonGameCount = postseasonGameCount;
            ElapsedTicks = elapsedTicks;
            AllocatedBytes = Math.Max(0L, allocatedBytes);
            UsesExactAllocationCounter = usesExactAllocationCounter;
        }

        public int SeasonYear { get; }
        public IReadOnlyList<string> RegularTeamSeasonKeys => _regularTeamSeasonKeys;
        public int RegularSeasonGameCount { get; }
        public int AllStarGameCount { get; }
        public int PostseasonGameCount { get; }
        public int TotalGameCount => RegularSeasonGameCount + AllStarGameCount + PostseasonGameCount;
        public long ElapsedTicks { get; }
        public long AllocatedBytes { get; }
        public bool UsesExactAllocationCounter { get; }
        public double ElapsedMilliseconds => ElapsedTicks * 1000d / Stopwatch.Frequency;
    }

    /// <summary>Baked Core25만으로 DetailedMatchEngine의 과거 정규시즌·올스타전·포스트시즌을 실행한다.</summary>
    public sealed class BakedHistoricalDetailedSeasonSource : IHistoricalDetailedSeasonSource
    {
        public const int RegularSeasonGamesPerTeam = 80;

        private const ulong ScheduleStream = 0x5343484544554C45UL;
        private const ulong RegularGameStream = 0x524547554C415200UL;
        private const ulong AllStarGameStream = 0x414C4C5354415200UL;
        private const ulong PostseasonGameStream = 0x504F535453454100UL;
        private const int AllStarTeamId = 20_001;
        private const int AllStarGameId = 500_001;
        private const int PostseasonGameIdBase = 900_000;

        private readonly HistoricalBakedContent _content;
        private readonly BalanceTable _balance;
        private readonly WorldIdentityRegistry _identityRegistry;
        private readonly AwardScoringPolicy _awardScoring;
        private readonly HistoricalMatchConfiguration _historicalConfiguration;

        public BakedHistoricalDetailedSeasonSource(
            HistoricalBakedContent content,
            BalanceTable balance,
            WorldIdentityRegistry identityRegistry,
            AwardScoringPolicy awardScoring = null)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            _identityRegistry = identityRegistry ?? throw new ArgumentNullException(nameof(identityRegistry));
            _awardScoring = awardScoring ?? AwardScoringPolicy.CreateDefault();
            _historicalConfiguration = new HistoricalMatchConfiguration(
                _balance.HistoricalAssignment.CreateRule());
        }

        public HistoricalSeasonSimulationMetrics LastRunMetrics { get; private set; }

        public HistoricalDetailedSeasonOutput RunSeason(
            ulong worldHistorySeed,
            IReadOnlyList<TeamSeasonDefinition> regularFranchiseTeams)
        {
            long allocationBefore = AllocationCounter.Read();
            long startedAt = Stopwatch.GetTimestamp();
            SeasonContext context = CreateContext(regularFranchiseTeams);
            ulong seasonSeed = DeterministicSeed.Derive(worldHistorySeed, unchecked((ulong)context.SeasonYear));
            int regularSeasonGamesPerTeam = ResolveRegularSeasonGamesPerTeam(context.Rosters.Count);
            int regularGameCapacity = regularSeasonGamesPerTeam * context.Rosters.Count / 2;
            var matches = new List<HistoricalDetailedMatchRecord>(regularGameCapacity + 16);
            var standings = new StandingsAccumulator(context.Rosters.Count, seasonSeed);
            var workloads = new PitchingWorkloadTracker();
            int[] teamIds = context.GetTeamIds();
            ScheduledGameDefinition[] schedule = new SeasonScheduleGenerator(
                    new Pcg32Random(DeterministicSeed.Derive(seasonSeed, ScheduleStream)))
                .Generate(teamIds, regularSeasonGamesPerTeam);
            int lastRegularSeasonRound = schedule[schedule.Length - 1].Round;
            int firstHalfRoundCount = lastRegularSeasonRound / 2;

            int scheduleIndex = 0;
            while (scheduleIndex < schedule.Length && schedule[scheduleIndex].Round <= firstHalfRoundCount)
            {
                SimulateRegularGame(
                    context,
                    schedule[scheduleIndex++],
                    HistoricalMatchStage.RegularSeasonFirstHalf,
                    seasonSeed,
                    workloads,
                    standings,
                    matches);
            }

            HistoricalDetailedSeasonOutput firstHalfOutput = new HistoricalDetailedSeasonOutput(
                context.SeasonYear,
                matches,
                context.Identities);
            IReadOnlyList<SeasonStatistics> firstHalfStatistics =
                DetailedMatchHistoricalSeasonAdapter.Aggregate(firstHalfOutput, context.TeamDefinitions);
            IReadOnlyList<WorldAwardEntry> allStars = new AllStarSelectionResolver(_awardScoring)
                .Resolve(firstHalfStatistics);
            string[] allStarGameEligibleIds = GetAllStarGameEligibleIds(allStars, context.SeasonYear);
            MatchResult allStarResult = SimulateAllStarGame(
                context,
                allStars,
                standings,
                seasonSeed,
                firstHalfRoundCount + 1,
                workloads);
            matches.Add(new HistoricalDetailedMatchRecord(HistoricalMatchStage.AllStarGame, allStarResult));

            while (scheduleIndex < schedule.Length)
            {
                SimulateRegularGame(
                    context,
                    schedule[scheduleIndex++],
                    HistoricalMatchStage.RegularSeasonSecondHalf,
                    seasonSeed,
                    workloads,
                    standings,
                    matches);
            }

            int[] orderedTeamIds = standings.GetOrderedTeamIds();
            int[] postseasonTeamIds = CopyFirst(
                orderedTeamIds,
                _balance.Postseason.PlayoffTeamCount);
            PostseasonSimulationRun postseasonRun = SimulatePostseason(
                context,
                postseasonTeamIds,
                seasonSeed,
                lastRegularSeasonRound + 2,
                workloads,
                matches);
            long elapsedTicks = Stopwatch.GetTimestamp() - startedAt;
            LastRunMetrics = new HistoricalSeasonSimulationMetrics(
                context.SeasonYear,
                context.GetTeamSeasonKeys(),
                schedule.Length,
                allStarGameCount: 1,
                postseasonRun.GameCount,
                elapsedTicks,
                AllocationCounter.Read() - allocationBefore,
                AllocationCounter.UsesExactCounter);
            return new HistoricalDetailedSeasonOutput(
                context.SeasonYear,
                matches,
                context.Identities,
                allStarGameEligibleIds,
                AllStarTeamId,
                standings.CreateTeamStatistics(context),
                CreateStandingHistory(context, orderedTeamIds),
                new HistoricalPostseasonResult(
                    context.SeasonYear,
                    GetTeamSeasonKeys(context, postseasonTeamIds),
                    context.GetTeamSeasonKey(postseasonRun.ChampionTeamId)));
        }

        private static int ResolveRegularSeasonGamesPerTeam(int teamCount)
        {
            if ((teamCount & 1) == 0)
                return RegularSeasonGamesPerTeam;

            int opponentCount = teamCount - 1;
            int compatibleGameCount = RegularSeasonGamesPerTeam - RegularSeasonGamesPerTeam % opponentCount;
            if (compatibleGameCount <= 0)
                throw new InvalidOperationException("홀수 구단 Historical Season의 균등 대진을 구성할 수 없습니다.");

            // 홀수 구단은 매 cycle마다 한 팀이 쉬므로 상대 수의 배수만 전 구단의 경기 수가 같아진다.
            return compatibleGameCount;
        }

        private static int[] CopyFirst(IReadOnlyList<int> source, int count)
        {
            if (count <= 0 || count > source.Count)
                throw new InvalidOperationException("Postseason 진출 구단 수가 정규 구단 수 범위를 벗어났습니다.");
            var result = new int[count];
            for (int index = 0; index < count; index++)
                result[index] = source[index];
            return result;
        }

        private static HistoricalStandingEntry[] CreateStandingHistory(
            SeasonContext context,
            IReadOnlyList<int> orderedTeamIds)
        {
            var result = new HistoricalStandingEntry[orderedTeamIds.Count];
            for (int index = 0; index < result.Length; index++)
            {
                result[index] = new HistoricalStandingEntry(
                    context.SeasonYear,
                    index + 1,
                    context.GetTeamSeasonKey(orderedTeamIds[index]));
            }
            return result;
        }

        private static string[] GetTeamSeasonKeys(SeasonContext context, IReadOnlyList<int> teamIds)
        {
            var result = new string[teamIds.Count];
            for (int index = 0; index < result.Length; index++)
                result[index] = context.GetTeamSeasonKey(teamIds[index]);
            return result;
        }

        private static string[] GetAllStarGameEligibleIds(
            IReadOnlyList<WorldAwardEntry> allStars,
            int seasonYear)
        {
            var result = new string[allStars.Count];
            for (int index = 0; index < allStars.Count; index++)
            {
                WorldAwardEntry award = allStars[index];
                if (award.SeasonYear != seasonYear || award.AwardType != WorldAwardType.AllStar)
                    throw new InvalidOperationException("All-Star Game 후보에는 해당 시즌 AllStar 수상만 들어갈 수 있습니다.");
                result[index] = award.PlayerSeasonId;
            }
            return result;
        }

        private void SimulateRegularGame(
            SeasonContext context,
            ScheduledGameDefinition game,
            HistoricalMatchStage stage,
            ulong seasonSeed,
            PitchingWorkloadTracker workloads,
            StandingsAccumulator standings,
            ICollection<HistoricalDetailedMatchRecord> matches)
        {
            ulong gameSeed = DeterministicSeed.Derive(seasonSeed, RegularGameStream + unchecked((ulong)game.GameId));
            MatchResult result = SimulateMatch(
                context,
                game.AwayTeamId,
                game.HomeTeamId,
                game.Round,
                game.GameId,
                gameSeed,
                requiresWinner: false,
                workloads);
            standings.Record(result);
            workloads.Record(game.Round, result.PitcherUsage);
            matches.Add(new HistoricalDetailedMatchRecord(stage, result));
        }

        private MatchResult SimulateAllStarGame(
            SeasonContext context,
            IReadOnlyList<WorldAwardEntry> allStars,
            StandingsAccumulator standings,
            ulong seasonSeed,
            int logicalDay,
            PitchingWorkloadTracker workloads)
        {
            SeasonRoster leadingTeam = context.GetRoster(standings.GetLeadingTeamId());
            MatchRosterSnapshot allStarRoster = BuildAllStarRoster(context, allStars, logicalDay, workloads);
            MatchRosterSnapshot opponentRoster = BuildMatchRoster(leadingTeam, logicalDay, logicalDay, workloads);
            ulong gameSeed = DeterministicSeed.Derive(seasonSeed, AllStarGameStream);
            var input = new MatchInput(
                context.SeasonYear,
                AllStarGameId,
                gameSeed,
                allStarRoster,
                opponentRoster,
                MatchRules.CreateDefault(requiresWinner: true),
                SimulationRulesVersion.DetailedV2,
                CreateVersionStamp(),
                _historicalConfiguration);
            MatchResult result = new MatchSimulator(_balance, MatchRandomStreams.Create(gameSeed))
                .Simulate(input, NullMatchEventSink.Instance, MatchExecutionProfile.DetailedBackground);
            workloads.Record(logicalDay, result.PitcherUsage);
            return result;
        }

        private PostseasonSimulationRun SimulatePostseason(
            SeasonContext context,
            int[] seeds,
            ulong seasonSeed,
            int postseasonStartDay,
            PitchingWorkloadTracker workloads,
            ICollection<HistoricalDetailedMatchRecord> matches)
        {
            if (seeds == null || seeds.Length != _balance.Postseason.PlayoffTeamCount)
                throw new ArgumentException("Postseason 시드가 설정된 진출 구단 수와 다릅니다.", nameof(seeds));
            int gameSequence = 0;
            int logicalDay = postseasonStartDay;
            int semifinalAWinner = SimulateSeries(
                context,
                seeds[PostseasonBracket.GetHigherSeedIndex(PostseasonSeriesId.SemifinalA)],
                seeds[PostseasonBracket.GetLowerSeedIndex(PostseasonSeriesId.SemifinalA)],
                PostseasonRound.Semifinal,
                _balance.Postseason.SemifinalSeriesGames,
                seasonSeed,
                workloads,
                matches,
                ref gameSequence,
                ref logicalDay);
            int semifinalBWinner = SimulateSeries(
                context,
                seeds[PostseasonBracket.GetHigherSeedIndex(PostseasonSeriesId.SemifinalB)],
                seeds[PostseasonBracket.GetLowerSeedIndex(PostseasonSeriesId.SemifinalB)],
                PostseasonRound.Semifinal,
                _balance.Postseason.SemifinalSeriesGames,
                seasonSeed,
                workloads,
                matches,
                ref gameSequence,
                ref logicalDay);
            int higherSeed = GetHigherSeed(seeds, semifinalAWinner, semifinalBWinner);
            int lowerSeed = higherSeed == semifinalAWinner ? semifinalBWinner : semifinalAWinner;
            int championTeamId = SimulateSeries(
                context,
                higherSeed,
                lowerSeed,
                PostseasonRound.ChampionshipSeries,
                _balance.Postseason.ChampionshipSeriesGames,
                seasonSeed,
                workloads,
                matches,
                ref gameSequence,
                ref logicalDay);
            return new PostseasonSimulationRun(gameSequence, championTeamId);
        }

        private readonly struct PostseasonSimulationRun
        {
            public PostseasonSimulationRun(int gameCount, int championTeamId)
            {
                GameCount = gameCount;
                ChampionTeamId = championTeamId;
            }

            public int GameCount { get; }
            public int ChampionTeamId { get; }
        }

        private int SimulateSeries(
            SeasonContext context,
            int higherSeedTeamId,
            int lowerSeedTeamId,
            PostseasonRound round,
            int maximumGames,
            ulong seasonSeed,
            PitchingWorkloadTracker workloads,
            ICollection<HistoricalDetailedMatchRecord> matches,
            ref int gameSequence,
            ref int logicalDay)
        {
            int winsRequired = PostseasonBracket.GetWinsRequired(maximumGames);
            int higherWins = 0;
            int lowerWins = 0;
            int gameNumber = 1;
            while (higherWins < winsRequired && lowerWins < winsRequired)
            {
                bool higherSeedHome = PostseasonBracket.IsHigherSeedHome(round, gameNumber);
                int awayTeamId = higherSeedHome ? lowerSeedTeamId : higherSeedTeamId;
                int homeTeamId = higherSeedHome ? higherSeedTeamId : lowerSeedTeamId;
                int gameId = PostseasonGameIdBase + ++gameSequence;
                ulong gameSeed = DeterministicSeed.Derive(
                    seasonSeed,
                    PostseasonGameStream + unchecked((ulong)gameSequence));
                MatchResult result = SimulateMatch(
                    context,
                    awayTeamId,
                    homeTeamId,
                    logicalDay,
                    gameId,
                    gameSeed,
                    requiresWinner: true,
                    workloads);
                workloads.Record(logicalDay, result.PitcherUsage);
                matches.Add(new HistoricalDetailedMatchRecord(HistoricalMatchStage.Postseason, result));
                if (result.WinnerTeamId == higherSeedTeamId)
                    higherWins++;
                else if (result.WinnerTeamId == lowerSeedTeamId)
                    lowerWins++;
                else
                    throw new InvalidOperationException("승자 필수 Postseason 경기가 무승부로 끝났습니다.");
                gameNumber++;
                logicalDay++;
            }
            return higherWins > lowerWins ? higherSeedTeamId : lowerSeedTeamId;
        }

        private MatchResult SimulateMatch(
            SeasonContext context,
            int awayTeamId,
            int homeTeamId,
            int rotationIndex,
            int gameId,
            ulong gameSeed,
            bool requiresWinner,
            PitchingWorkloadTracker workloads)
        {
            MatchRosterSnapshot away = BuildMatchRoster(
                context.GetRoster(awayTeamId),
                rotationIndex,
                rotationIndex,
                workloads);
            MatchRosterSnapshot home = BuildMatchRoster(
                context.GetRoster(homeTeamId),
                rotationIndex,
                rotationIndex,
                workloads);
            var input = new MatchInput(
                context.SeasonYear,
                gameId,
                gameSeed,
                away,
                home,
                MatchRules.CreateDefault(requiresWinner),
                SimulationRulesVersion.DetailedV2,
                CreateVersionStamp(),
                _historicalConfiguration);
            return new MatchSimulator(_balance, MatchRandomStreams.Create(gameSeed))
                .Simulate(input, NullMatchEventSink.Instance, MatchExecutionProfile.DetailedBackground);
        }

        private MatchRosterSnapshot BuildMatchRoster(
            SeasonRoster roster,
            int rotationIndex,
            int logicalDay,
            PitchingWorkloadTracker workloads)
        {
            var lineup = new LineupSlot[9];
            for (int index = 0; index < lineup.Length; index++)
                lineup[index] = new LineupSlot(roster.Players[index], (PlayerPosition)(index + 1));

            var bench = new Player[5];
            Array.Copy(roster.Players, 9, bench, 0, bench.Length);
            int starterIndex = 14 + PositiveModulo(rotationIndex - 1, 5);
            var bullpen = new PitcherRosterEntry[6];
            for (int index = 0; index < 4; index++)
            {
                int playerIndex = 19 + index;
                bullpen[index] = CreatePitcherEntry(
                    roster,
                    playerIndex,
                    PitcherRole.MiddleRelief,
                    (ActiveRosterRole)((int)ActiveRosterRole.Bullpen1 + index),
                    logicalDay,
                    workloads);
            }
            bullpen[4] = CreatePitcherEntry(
                roster,
                23,
                PitcherRole.Setup,
                ActiveRosterRole.Setup,
                logicalDay,
                workloads);
            bullpen[5] = CreatePitcherEntry(
                roster,
                24,
                PitcherRole.Closer,
                ActiveRosterRole.Closer,
                logicalDay,
                workloads);

            return new MatchRosterSnapshot(
                roster.TeamId,
                roster.Team.FranchiseId,
                new Lineup(lineup),
                new PitcherRosterEntry(
                    roster.Players[starterIndex],
                    PitcherRole.Starter,
                    recentWorkload: workloads.Get(roster.Players[starterIndex].PlayerId, logicalDay),
                    naturalRole: roster.Seasons[starterIndex].PitcherRole,
                    playerSeasonId: roster.Seasons[starterIndex].PlayerSeasonId,
                    naturalRoleConfidence: roster.Seasons[starterIndex].PitcherRoleConfidence),
                bullpen,
                bench,
                ManagerTacticalProfile.Balanced,
                RunningApproach.Balanced);
        }

        private static PitcherRosterEntry CreatePitcherEntry(
            SeasonRoster roster,
            int playerIndex,
            PitcherRole assignedRole,
            ActiveRosterRole activeRosterRole,
            int logicalDay,
            PitchingWorkloadTracker workloads)
        {
            Player player = roster.Players[playerIndex];
            PlayerSeasonDefinition season = roster.Seasons[playerIndex];
            return new PitcherRosterEntry(
                player,
                assignedRole,
                recentWorkload: workloads.Get(player.PlayerId, logicalDay),
                naturalRole: season.PitcherRole,
                activeRosterRole: activeRosterRole,
                playerSeasonId: season.PlayerSeasonId,
                naturalRoleConfidence: season.PitcherRoleConfidence);
        }

        private MatchRosterSnapshot BuildAllStarRoster(
            SeasonContext context,
            IReadOnlyList<WorldAwardEntry> awards,
            int logicalDay,
            PitchingWorkloadTracker workloads)
        {
            var hittersByPosition = new Player[9];
            var bench = new List<Player>(5);
            var starters = new List<PlayerSeasonPair>(5);
            var relievers = new List<PlayerSeasonPair>(6);
            for (int index = 0; index < awards.Count; index++)
            {
                WorldAwardEntry award = awards[index];
                if (award.SeasonYear != context.SeasonYear || award.AwardType != WorldAwardType.AllStar)
                    continue;
                PlayerSeasonPair pair = context.GetPlayer(award.PlayerSeasonId);
                if (award.Position == PlayerPosition.StartingPitcher)
                    starters.Add(pair);
                else if (award.Position == PlayerPosition.ReliefPitcher)
                    relievers.Add(pair);
                else
                {
                    int positionIndex = (int)award.Position - 1;
                    if (positionIndex < 0 || positionIndex >= hittersByPosition.Length)
                        throw new InvalidOperationException("All-Star 야수 포지션이 유효하지 않습니다.");
                    if (hittersByPosition[positionIndex] == null)
                        hittersByPosition[positionIndex] = pair.Player;
                    else
                        bench.Add(pair.Player);
                }
            }
            if (starters.Count != 5 || relievers.Count != 6 || bench.Count != 5)
                throw new InvalidOperationException("All-Star 25인 역할 구성이 9/5/5/6 쿼터와 다릅니다.");

            var lineup = new LineupSlot[9];
            for (int index = 0; index < lineup.Length; index++)
            {
                if (hittersByPosition[index] == null)
                    throw new InvalidOperationException("All-Star 주전 포지션이 비어 있습니다.");
                lineup[index] = new LineupSlot(hittersByPosition[index], (PlayerPosition)(index + 1));
            }
            var bullpen = new PitcherRosterEntry[6];
            for (int index = 0; index < relievers.Count; index++)
            {
                PlayerSeasonPair pair = relievers[index];
                ActiveRosterRole rosterRole = index < 4
                    ? (ActiveRosterRole)((int)ActiveRosterRole.Bullpen1 + index)
                    : index == 4 ? ActiveRosterRole.Setup : ActiveRosterRole.Closer;
                PitcherRole role = index < 4
                    ? PitcherRole.MiddleRelief
                    : index == 4 ? PitcherRole.Setup : PitcherRole.Closer;
                bullpen[index] = new PitcherRosterEntry(
                    pair.Player,
                    role,
                    recentWorkload: workloads.Get(pair.Player.PlayerId, logicalDay),
                    naturalRole: pair.Season.PitcherRole,
                    activeRosterRole: rosterRole,
                    playerSeasonId: pair.Season.PlayerSeasonId,
                    naturalRoleConfidence: pair.Season.PitcherRoleConfidence);
            }
            PlayerSeasonPair starting = starters[0];
            return new MatchRosterSnapshot(
                AllStarTeamId,
                context.SeasonYear + " 올스타",
                new Lineup(lineup),
                new PitcherRosterEntry(
                    starting.Player,
                    PitcherRole.Starter,
                    recentWorkload: workloads.Get(starting.Player.PlayerId, logicalDay),
                    naturalRole: starting.Season.PitcherRole,
                    playerSeasonId: starting.Season.PlayerSeasonId,
                    naturalRoleConfidence: starting.Season.PitcherRoleConfidence),
                bullpen,
                bench,
                ManagerTacticalProfile.Balanced,
                RunningApproach.Balanced);
        }

        private SeasonContext CreateContext(IReadOnlyList<TeamSeasonDefinition> inputTeams)
        {
            if (inputTeams == null || !LeagueInstance.IsSupportedRegularFranchiseTeamCount(inputTeams.Count))
                throw new ArgumentException("Historical Season에는 해당 연도의 정규 Franchise 6~10구단이 필요합니다.", nameof(inputTeams));
            int year = inputTeams[0]?.OriginYear ?? 0;
            HistoricalYearContentDefinition yearContent = _content.GetYear(year);
            if (!LeagueInstance.IsSupportedRegularFranchiseTeamCount(yearContent.TeamSeasons.Count))
                throw new InvalidOperationException($"{year} Baked Content의 정규 Franchise 구단 수가 6~10 범위가 아닙니다.");

            var teams = new TeamSeasonDefinition[inputTeams.Count];
            var keys = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < inputTeams.Count; index++)
            {
                TeamSeasonDefinition team = inputTeams[index]
                    ?? throw new ArgumentException("null TeamSeason이 있습니다.", nameof(inputTeams));
                if (team.OriginYear != year)
                    throw new ArgumentException("한 Historical Season에 서로 다른 OriginYear를 섞을 수 없습니다.", nameof(inputTeams));
                if (!keys.Add(team.TeamSeasonKey))
                    throw new ArgumentException("TeamSeasonKey는 중복될 수 없습니다.", nameof(inputTeams));
                if (!_content.TryGetTeamSeason(team.TeamSeasonKey, out TeamSeasonDefinition baked) || baked.OriginYear != year)
                    throw new ArgumentException("Runtime Baked Content에 없는 정규 TeamSeason입니다.", nameof(inputTeams));
                teams[index] = baked;
            }
            for (int index = 0; index < yearContent.TeamSeasons.Count; index++)
            {
                if (!keys.Contains(yearContent.TeamSeasons[index].TeamSeasonKey))
                    throw new ArgumentException("Historical Season 입력은 해당 연도의 Baked 정규 구단 전체여야 합니다.", nameof(inputTeams));
            }
            Array.Sort(teams, (left, right) => string.CompareOrdinal(left.TeamSeasonKey, right.TeamSeasonKey));
            return CreateContext(year, teams);
        }

        private SeasonContext CreateContext(int year, IReadOnlyList<TeamSeasonDefinition> teams)
        {
            var seasonIds = new List<string>(teams.Count * 25);
            var uniqueSeasonIds = new HashSet<string>(StringComparer.Ordinal);
            for (int teamIndex = 0; teamIndex < teams.Count; teamIndex++)
            {
                TeamSeasonDefinition team = teams[teamIndex];
                if (team.Core25CardIds.Count != ActiveRosterCompositionRule.ActiveRosterSize)
                    throw new InvalidOperationException($"{team.TeamSeasonKey} Core25가 정확히 25명이 아닙니다.");
                for (int rosterIndex = 0; rosterIndex < team.Core25CardIds.Count; rosterIndex++)
                {
                    PlayerSeasonDefinition season = ResolveCorePlayer(team, team.Core25CardIds[rosterIndex]);
                    if (!uniqueSeasonIds.Add(season.PlayerSeasonId))
                        throw new InvalidOperationException("정규 구단 Core25에 PlayerSeasonId가 중복되었습니다.");
                    seasonIds.Add(season.PlayerSeasonId);
                }
            }
            seasonIds.Sort(StringComparer.Ordinal);
            var playerIds = new Dictionary<string, int>(seasonIds.Count, StringComparer.Ordinal);
            for (int index = 0; index < seasonIds.Count; index++)
                playerIds.Add(seasonIds[index], index + 1);

            var rosters = new SeasonRoster[teams.Count];
            var identities = new List<HistoricalPlayerSeasonIdentity>(seasonIds.Count);
            var players = new Dictionary<string, PlayerSeasonPair>(seasonIds.Count, StringComparer.Ordinal);
            for (int teamIndex = 0; teamIndex < teams.Count; teamIndex++)
            {
                TeamSeasonDefinition team = teams[teamIndex];
                var rosterPlayers = new Player[25];
                var rosterSeasons = new PlayerSeasonDefinition[25];
                for (int rosterIndex = 0; rosterIndex < team.Core25CardIds.Count; rosterIndex++)
                {
                    PlayerSeasonDefinition season = ResolveCorePlayer(team, team.Core25CardIds[rosterIndex]);
                    PlayerPersonDefinition person = ResolvePerson(season);
                    AbilityRatings ratings = season.CreateBaseAttributes();
                    var player = new Player(
                        playerIds[season.PlayerSeasonId],
                        _identityRegistry.GetPlayerDisplayName(person.PlayerPersonId),
                        season.Position,
                        person.Bats,
                        person.Throws,
                        ratings.ToBatterAttributes(),
                        ratings.ToPitcherAttributes(),
                        nationality: season.RegistrationType == RegistrationType.Foreign ? "외국인" : string.Empty);
                    ValidateCoreRole(team, rosterIndex, season);
                    rosterPlayers[rosterIndex] = player;
                    rosterSeasons[rosterIndex] = season;
                    identities.Add(new HistoricalPlayerSeasonIdentity(
                        player.PlayerId,
                        season.PlayerSeasonId,
                        team.TeamSeasonKey,
                        ResolveAssignedSeasonPosition(rosterIndex, season.Position)));
                    players.Add(season.PlayerSeasonId, new PlayerSeasonPair(player, season));
                }
                rosters[teamIndex] = new SeasonRoster(teamIndex + 1, team, rosterPlayers, rosterSeasons);
            }
            identities.Sort((left, right) => left.PlayerId.CompareTo(right.PlayerId));
            return new SeasonContext(year, teams, rosters, identities, players);
        }

        private static PlayerPosition ResolveAssignedSeasonPosition(
            int rosterIndex,
            PlayerPosition naturalPosition)
        {
            if (rosterIndex >= 0 && rosterIndex < 9)
                return (PlayerPosition)(rosterIndex + 1);
            if (rosterIndex >= 14 && rosterIndex < 19)
                return PlayerPosition.StartingPitcher;
            if (rosterIndex >= 19 && rosterIndex < 25)
                return PlayerPosition.ReliefPitcher;
            return naturalPosition;
        }

        private PlayerSeasonDefinition ResolveCorePlayer(TeamSeasonDefinition team, string cardId)
        {
            if (!_content.TryGetNormalCard(cardId, out PlayerCardDefinition card) ||
                card.Edition != PlayerCardEdition.Normal ||
                !string.Equals(card.CardId, PlayerCardDefinition.CreateStableCardId(
                    card.PlayerSeasonId,
                    PlayerCardEdition.Normal), StringComparison.Ordinal) ||
                !_content.TryGetPlayerSeason(card.PlayerSeasonId, out PlayerSeasonDefinition season))
            {
                throw new InvalidOperationException($"{team.TeamSeasonKey} Core25 Card {cardId}의 Baked PlayerSeason을 찾을 수 없습니다.");
            }
            if (!string.Equals(season.OriginTeamSeasonKey, team.TeamSeasonKey, StringComparison.Ordinal))
                throw new InvalidOperationException("Core25 PlayerSeason의 OriginTeamSeasonKey가 구단과 다릅니다.");
            return season;
        }

        private PlayerPersonDefinition ResolvePerson(PlayerSeasonDefinition season)
        {
            if (!_content.TryGetPlayerPerson(season.PlayerPersonId, out PlayerPersonDefinition person))
                throw new InvalidOperationException($"PlayerPerson {season.PlayerPersonId}를 찾을 수 없습니다.");
            return person;
        }

        private static void ValidateCoreRole(
            TeamSeasonDefinition team,
            int rosterIndex,
            PlayerSeasonDefinition season)
        {
            bool isHitterSlot = rosterIndex < 14;
            if (isHitterSlot && season.PlayerType != PlayerType.Batter)
                throw new InvalidOperationException($"{team.TeamSeasonKey} Core25 {rosterIndex}번은 야수 슬롯이어야 합니다.");
            if (!isHitterSlot && season.PlayerType != PlayerType.Pitcher)
                throw new InvalidOperationException($"{team.TeamSeasonKey} Core25 {rosterIndex}번은 투수 슬롯이어야 합니다.");
        }

        private SimulationVersionStamp CreateVersionStamp()
        {
            return SimulationVersionStamp.CreateCurrent(
                _balance.Version,
                _content.Manifest.ContentHash,
                (int)SimulationRulesVersion.DetailedV2);
        }

        private static int GetHigherSeed(IReadOnlyList<int> seeds, int firstTeamId, int secondTeamId)
        {
            for (int index = 0; index < seeds.Count; index++)
            {
                if (seeds[index] == firstTeamId)
                    return firstTeamId;
                if (seeds[index] == secondTeamId)
                    return secondTeamId;
            }
            throw new InvalidOperationException("결승 진출팀의 정규시즌 Seed를 찾을 수 없습니다.");
        }

        private static int PositiveModulo(int value, int modulus)
        {
            int result = value % modulus;
            return result < 0 ? result + modulus : result;
        }

        private sealed class SeasonContext
        {
            private readonly Dictionary<int, SeasonRoster> _rostersById;
            private readonly IReadOnlyDictionary<string, PlayerSeasonPair> _players;

            public SeasonContext(
                int seasonYear,
                IReadOnlyList<TeamSeasonDefinition> teams,
                IReadOnlyList<SeasonRoster> rosters,
                IReadOnlyList<HistoricalPlayerSeasonIdentity> identities,
                IReadOnlyDictionary<string, PlayerSeasonPair> players)
            {
                SeasonYear = seasonYear;
                TeamDefinitions = teams;
                Rosters = rosters;
                Identities = identities;
                _players = players;
                _rostersById = new Dictionary<int, SeasonRoster>(rosters.Count);
                for (int index = 0; index < rosters.Count; index++)
                    _rostersById.Add(rosters[index].TeamId, rosters[index]);
            }

            public int SeasonYear { get; }
            public IReadOnlyList<TeamSeasonDefinition> TeamDefinitions { get; }
            public IReadOnlyList<SeasonRoster> Rosters { get; }
            public IReadOnlyList<HistoricalPlayerSeasonIdentity> Identities { get; }

            public SeasonRoster GetRoster(int teamId) => _rostersById[teamId];

            public string GetTeamSeasonKey(int teamId) => GetRoster(teamId).Team.TeamSeasonKey;

            public PlayerSeasonPair GetPlayer(string playerSeasonId)
            {
                if (!_players.TryGetValue(playerSeasonId, out PlayerSeasonPair pair))
                    throw new InvalidOperationException($"All-Star PlayerSeason {playerSeasonId}가 정규 Core25에 없습니다.");
                return pair;
            }

            public int[] GetTeamIds()
            {
                var result = new int[Rosters.Count];
                for (int index = 0; index < result.Length; index++)
                    result[index] = Rosters[index].TeamId;
                return result;
            }

            public string[] GetTeamSeasonKeys()
            {
                var result = new string[Rosters.Count];
                for (int index = 0; index < result.Length; index++)
                    result[index] = Rosters[index].Team.TeamSeasonKey;
                return result;
            }
        }

        private sealed class SeasonRoster
        {
            public SeasonRoster(
                int teamId,
                TeamSeasonDefinition team,
                Player[] players,
                PlayerSeasonDefinition[] seasons)
            {
                TeamId = teamId;
                Team = team;
                Players = players;
                Seasons = seasons;
            }

            public int TeamId { get; }
            public TeamSeasonDefinition Team { get; }
            public Player[] Players { get; }
            public PlayerSeasonDefinition[] Seasons { get; }
        }

        private readonly struct PlayerSeasonPair
        {
            public PlayerSeasonPair(Player player, PlayerSeasonDefinition season)
            {
                Player = player;
                Season = season;
            }

            public Player Player { get; }
            public PlayerSeasonDefinition Season { get; }
        }

        private sealed class StandingsAccumulator
        {
            private readonly TeamStanding[] _teams;

            public StandingsAccumulator(int teamCount, ulong seasonSeed)
            {
                _teams = new TeamStanding[teamCount];
                for (int index = 0; index < teamCount; index++)
                {
                    int teamId = index + 1;
                    _teams[index] = new TeamStanding(
                        teamId,
                        DeterministicSeed.Derive(seasonSeed, unchecked((ulong)teamId)),
                        teamCount);
                }
            }

            public void Record(MatchResult result)
            {
                TeamStanding away = _teams[result.AwayBoxScore.TeamId - 1];
                TeamStanding home = _teams[result.HomeBoxScore.TeamId - 1];
                away.Games++;
                home.Games++;
                AccumulateBoxScore(away, result.AwayBoxScore);
                AccumulateBoxScore(home, result.HomeBoxScore);
                away.RunsScored += result.AwayBoxScore.Runs;
                away.RunsAllowed += result.HomeBoxScore.Runs;
                home.RunsScored += result.HomeBoxScore.Runs;
                home.RunsAllowed += result.AwayBoxScore.Runs;
                if (result.IsTie)
                {
                    away.Ties++;
                    home.Ties++;
                    return;
                }
                TeamStanding winner = result.WinnerTeamId == away.TeamId ? away : home;
                TeamStanding loser = ReferenceEquals(winner, away) ? home : away;
                winner.Wins++;
                loser.Losses++;
                winner.HeadToHeadWins[loser.TeamId - 1]++;
                loser.HeadToHeadLosses[winner.TeamId - 1]++;
            }

            public int GetLeadingTeamId()
            {
                TeamStandingEntry[] entries = CreateEntries();
                return PostseasonBracket.SelectSeeds(entries, 1)[0];
            }

            public int[] GetOrderedTeamIds()
            {
                return PostseasonBracket.SelectSeeds(CreateEntries(), _teams.Length);
            }

            public TeamSeasonStatistics[] CreateTeamStatistics(SeasonContext context)
            {
                var result = new TeamSeasonStatistics[_teams.Length];
                for (int index = 0; index < _teams.Length; index++)
                {
                    TeamStanding team = _teams[index];
                    result[index] = new TeamSeasonStatistics(
                        context.GetTeamSeasonKey(team.TeamId),
                        context.SeasonYear,
                        team.Games,
                        team.Wins,
                        team.Losses,
                        team.Ties,
                        team.RunsScored,
                        team.RunsAllowed,
                        team.AtBats,
                        team.Hits,
                        team.PitchingOuts,
                        team.EarnedRuns,
                        team.HitsAllowed,
                        team.WalksAllowed);
                }
                return result;
            }

            private static void AccumulateBoxScore(TeamStanding team, TeamBoxScore boxScore)
            {
                for (int index = 0; index < boxScore.BattingLines.Count; index++)
                {
                    team.AtBats += boxScore.BattingLines[index].AtBats;
                    team.Hits += boxScore.BattingLines[index].Hits;
                }
                for (int index = 0; index < boxScore.PitchingLines.Count; index++)
                {
                    PlayerPitchingLine line = boxScore.PitchingLines[index];
                    team.PitchingOuts += line.OutsRecorded;
                    team.EarnedRuns += line.EarnedRuns;
                    team.HitsAllowed += line.HitsAllowed;
                    team.WalksAllowed += line.WalksAllowed;
                }
            }

            public TeamStandingEntry[] CreateEntries()
            {
                var result = new TeamStandingEntry[_teams.Length];
                for (int index = 0; index < _teams.Length; index++)
                {
                    TeamStanding team = _teams[index];
                    var headToHead = new HeadToHeadEntry[_teams.Length - 1];
                    int target = 0;
                    for (int opponent = 0; opponent < _teams.Length; opponent++)
                    {
                        if (opponent == index)
                            continue;
                        headToHead[target++] = new HeadToHeadEntry(
                            opponent + 1,
                            team.HeadToHeadWins[opponent],
                            team.HeadToHeadLosses[opponent]);
                    }
                    result[index] = new TeamStandingEntry(
                        team.TeamId,
                        team.Wins,
                        team.Losses,
                        team.RunsScored,
                        team.RunsAllowed,
                        team.FixedTiebreaker,
                        headToHead);
                }
                return result;
            }

            private sealed class TeamStanding
            {
                public TeamStanding(int teamId, ulong fixedTiebreaker, int teamCount)
                {
                    TeamId = teamId;
                    FixedTiebreaker = fixedTiebreaker;
                    HeadToHeadWins = new int[teamCount];
                    HeadToHeadLosses = new int[teamCount];
                }

                public int TeamId { get; }
                public ulong FixedTiebreaker { get; }
                public int Games { get; set; }
                public int Wins { get; set; }
                public int Losses { get; set; }
                public int Ties { get; set; }
                public int RunsScored { get; set; }
                public int RunsAllowed { get; set; }
                public int AtBats { get; set; }
                public int Hits { get; set; }
                public int PitchingOuts { get; set; }
                public int EarnedRuns { get; set; }
                public int HitsAllowed { get; set; }
                public int WalksAllowed { get; set; }
                public int[] HeadToHeadWins { get; }
                public int[] HeadToHeadLosses { get; }
            }
        }

        private sealed class PitchingWorkloadTracker
        {
            private readonly Dictionary<int, PitchingDays> _byPlayerId = new Dictionary<int, PitchingDays>();

            public RecentPitchingWorkload Get(int playerId, int logicalDay)
            {
                if (!_byPlayerId.TryGetValue(playerId, out PitchingDays days))
                    return default;
                return new RecentPitchingWorkload(
                    days.Get(logicalDay - 1),
                    days.Get(logicalDay - 2),
                    days.Get(logicalDay - 3));
            }

            public void Record(int logicalDay, IReadOnlyList<PitcherUsageReport> usage)
            {
                for (int index = 0; index < usage.Count; index++)
                {
                    PitcherUsageReport entry = usage[index];
                    if (entry.PitchCount <= 0)
                        continue;
                    if (!_byPlayerId.TryGetValue(entry.PlayerId, out PitchingDays days))
                    {
                        days = new PitchingDays();
                        _byPlayerId.Add(entry.PlayerId, days);
                    }
                    days.Set(logicalDay, entry.PitchCount);
                }
            }

            private sealed class PitchingDays
            {
                private readonly int[] _days = new int[4];
                private readonly int[] _pitches = new int[4];

                public int Get(int day)
                {
                    if (day <= 0)
                        return 0;
                    int slot = day & 3;
                    return _days[slot] == day ? _pitches[slot] : 0;
                }

                public void Set(int day, int pitches)
                {
                    int slot = day & 3;
                    _days[slot] = day;
                    _pitches[slot] = pitches;
                }
            }
        }

        private static class AllocationCounter
        {
            private static readonly Func<long> Reader = CreateReader();

            public static bool UsesExactCounter { get; private set; }

            public static long Read() => Reader();

            private static Func<long> CreateReader()
            {
                MethodInfo method = typeof(GC).GetMethod(
                    "GetAllocatedBytesForCurrentThread",
                    BindingFlags.Public | BindingFlags.Static,
                    binder: null,
                    types: Type.EmptyTypes,
                    modifiers: null);
                if (method != null && method.ReturnType == typeof(long))
                {
                    try
                    {
                        UsesExactCounter = true;
                        return (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), method);
                    }
                    catch (Exception)
                    {
                        UsesExactCounter = false;
                    }
                }
                return () => GC.GetTotalMemory(forceFullCollection: false);
            }
        }
    }
}
