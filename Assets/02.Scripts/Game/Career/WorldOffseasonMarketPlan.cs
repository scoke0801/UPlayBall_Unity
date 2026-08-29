using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Teams;
using Baseball.Simulation.Career;
using Baseball.Simulation.Match;
using Baseball.Simulation.Random;

namespace Baseball.Game.Career
{
    /// <summary>계약 선택 전에는 월드를 바꾸지 않고 다음 시즌 세 리그 로스터 결정을 보관한다.</summary>
    public sealed class WorldOffseasonMarketPlan
    {
        private readonly LeagueRosterPlan[] _leagueRosters;
        private readonly AiMarketDecision[] _decisions;
        private readonly PlayerState[] _newPlayers;

        public WorldOffseasonMarketPlan(
            LeagueRosterPlan[] leagueRosters,
            AiMarketDecision[] decisions,
            PlayerState[] newPlayers,
            LeagueMovementPlan leagueMovementPlan = null)
        {
            _leagueRosters = leagueRosters ?? throw new ArgumentNullException(nameof(leagueRosters));
            _decisions = decisions ?? throw new ArgumentNullException(nameof(decisions));
            _newPlayers = newPlayers ?? throw new ArgumentNullException(nameof(newPlayers));
            LeagueMovementPlan = leagueMovementPlan ?? LeagueMovementPlan.Empty;
            Array.Sort(_leagueRosters, (left, right) => left.LeagueId.CompareTo(right.LeagueId));
            Array.Sort(_decisions, (left, right) => left.PlayerId.CompareTo(right.PlayerId));
            Array.Sort(_newPlayers, (left, right) => left.PlayerId.CompareTo(right.PlayerId));
        }

        public IReadOnlyList<LeagueRosterPlan> LeagueRosters => _leagueRosters;
        public IReadOnlyList<AiMarketDecision> Decisions => _decisions;
        public IReadOnlyList<PlayerState> NewPlayers => _newPlayers;
        public LeagueMovementPlan LeagueMovementPlan { get; }

        public TeamState[] GetTeams(LeagueId leagueId)
        {
            for (int index = 0; index < _leagueRosters.Length; index++)
            {
                if (_leagueRosters[index].LeagueId == leagueId)
                    return _leagueRosters[index].CopyTeams();
            }
            throw new InvalidOperationException($"{leagueId}의 시장 로스터 계획이 없습니다.");
        }

        public LeagueId GetLeagueIdForTeam(int teamId)
        {
            for (int leagueIndex = 0; leagueIndex < _leagueRosters.Length; leagueIndex++)
            {
                IReadOnlyList<TeamState> teams = _leagueRosters[leagueIndex].Teams;
                for (int teamIndex = 0; teamIndex < teams.Count; teamIndex++)
                {
                    if (teams[teamIndex].TeamId == teamId)
                        return _leagueRosters[leagueIndex].LeagueId;
                }
            }
            throw new InvalidOperationException($"TeamId {teamId}의 다음 시즌 리그가 없습니다.");
        }

        public WorldOffseasonMarketPlan WithTeams(LeagueId leagueId, TeamState[] teams)
        {
            var rosters = new LeagueRosterPlan[_leagueRosters.Length];
            bool replaced = false;
            for (int index = 0; index < rosters.Length; index++)
            {
                LeagueRosterPlan source = _leagueRosters[index];
                if (source.LeagueId == leagueId)
                {
                    rosters[index] = new LeagueRosterPlan(leagueId, teams);
                    replaced = true;
                }
                else
                {
                    rosters[index] = new LeagueRosterPlan(source.LeagueId, source.CopyTeams());
                }
            }
            if (!replaced)
                throw new InvalidOperationException($"{leagueId}의 시장 로스터 계획이 없습니다.");
            return new WorldOffseasonMarketPlan(
                rosters,
                (AiMarketDecision[])_decisions.Clone(),
                (PlayerState[])_newPlayers.Clone(),
                LeagueMovementPlan);
        }

        /// <summary>내 선수의 계약 이동으로 자리를 바꾼 AI 선수의 시장 결정을 교체한다.</summary>
        public WorldOffseasonMarketPlan WithDecision(AiMarketDecision decision)
        {
            var decisions = new List<AiMarketDecision>(_decisions.Length + 1);
            bool replaced = false;
            for (int index = 0; index < _decisions.Length; index++)
            {
                if (_decisions[index].PlayerId == decision.PlayerId)
                {
                    decisions.Add(decision);
                    replaced = true;
                }
                else
                {
                    decisions.Add(_decisions[index]);
                }
            }
            if (!replaced)
                decisions.Add(decision);

            var rosters = new LeagueRosterPlan[_leagueRosters.Length];
            for (int index = 0; index < rosters.Length; index++)
            {
                LeagueRosterPlan source = _leagueRosters[index];
                rosters[index] = new LeagueRosterPlan(source.LeagueId, source.CopyTeams());
            }
            return new WorldOffseasonMarketPlan(
                rosters,
                decisions.ToArray(),
                (PlayerState[])_newPlayers.Clone(),
                LeagueMovementPlan);
        }
    }

    /// <summary>한 리그의 다음 시즌 구단 로스터 스냅샷을 보관한다.</summary>
    public sealed class LeagueRosterPlan
    {
        private readonly TeamState[] _teams;

        public LeagueRosterPlan(LeagueId leagueId, TeamState[] teams)
        {
            if (!leagueId.IsAssigned) throw new ArgumentException("유효한 LeagueId가 필요합니다.", nameof(leagueId));
            LeagueId = leagueId;
            _teams = teams == null ? throw new ArgumentNullException(nameof(teams)) : (TeamState[])teams.Clone();
            Array.Sort(_teams, (left, right) => left.TeamId.CompareTo(right.TeamId));
        }

        public LeagueId LeagueId { get; }
        public IReadOnlyList<TeamState> Teams => _teams;
        public TeamState[] CopyTeams() => (TeamState[])_teams.Clone();
    }

    /// <summary>AI 선수 한 명의 오프시즌 계약 또는 은퇴 결정을 보관한다.</summary>
    public readonly struct AiMarketDecision
    {
        public AiMarketDecision(
            int playerId,
            PlayerMovementType movementType,
            LeagueId previousLeagueId,
            int previousTeamId,
            LeagueId targetLeagueId,
            int targetTeamId,
            ExpectedRole expectedRole,
            int contractYears,
            long annualSalary,
            string reason,
            bool preservesContract = false)
        {
            PlayerId = playerId;
            MovementType = movementType;
            PreviousLeagueId = previousLeagueId;
            PreviousTeamId = previousTeamId;
            TargetLeagueId = targetLeagueId;
            TargetTeamId = targetTeamId;
            ExpectedRole = expectedRole;
            ContractYears = contractYears;
            AnnualSalary = annualSalary;
            Reason = reason ?? string.Empty;
            PreservesContract = preservesContract;
        }

        public int PlayerId { get; }
        public PlayerMovementType MovementType { get; }
        public LeagueId PreviousLeagueId { get; }
        public int PreviousTeamId { get; }
        public LeagueId TargetLeagueId { get; }
        public int TargetTeamId { get; }
        public ExpectedRole ExpectedRole { get; }
        public int ContractYears { get; }
        public long AnnualSalary { get; }
        public string Reason { get; }
        public bool PreservesContract { get; }
        public bool IsRetirement => MovementType == PlayerMovementType.Retirement;
    }

    public enum TeamLeagueMovementType
    {
        Promotion,
        Relegation
    }

    /// <summary>정규시즌 순위로 확정된 구단의 한 단계 이동을 과거 리그와 함께 보관한다.</summary>
    public readonly struct TeamLeagueMovementRecord
    {
        public TeamLeagueMovementRecord(
            int seasonId,
            int year,
            int teamId,
            int regularSeasonRank,
            TeamLeagueMovementType movementType,
            LeagueId previousLeagueId,
            LeagueLevel previousTier,
            LeagueId targetLeagueId,
            LeagueLevel targetTier)
        {
            SeasonId = seasonId;
            Year = year;
            TeamId = teamId;
            RegularSeasonRank = regularSeasonRank;
            MovementType = movementType;
            PreviousLeagueId = previousLeagueId;
            PreviousTier = previousTier;
            TargetLeagueId = targetLeagueId;
            TargetTier = targetTier;
        }

        public int SeasonId { get; }
        public int Year { get; }
        public int TeamId { get; }
        public int RegularSeasonRank { get; }
        public TeamLeagueMovementType MovementType { get; }
        public LeagueId PreviousLeagueId { get; }
        public LeagueLevel PreviousTier { get; }
        public LeagueId TargetLeagueId { get; }
        public LeagueLevel TargetTier { get; }
    }

    /// <summary>모든 경계의 승격·강등을 월드 변경 전에 함께 검증하는 불변 계획이다.</summary>
    public sealed class LeagueMovementPlan
    {
        public static readonly LeagueMovementPlan Empty =
            new LeagueMovementPlan(
                Array.Empty<TeamLeagueMovementRecord>(),
                Array.Empty<LeagueTiebreakGameState>());

        private readonly TeamLeagueMovementRecord[] _records;
        private readonly LeagueTiebreakGameState[] _tiebreakGames;

        public LeagueMovementPlan(
            TeamLeagueMovementRecord[] records,
            LeagueTiebreakGameState[] tiebreakGames = null)
        {
            _records = records ?? throw new ArgumentNullException(nameof(records));
            _tiebreakGames = tiebreakGames ?? Array.Empty<LeagueTiebreakGameState>();
            Array.Sort(_records, CompareRecords);
            Array.Sort(_tiebreakGames, (left, right) => left.GameId.CompareTo(right.GameId));
        }

        public IReadOnlyList<TeamLeagueMovementRecord> Records => _records;
        public IReadOnlyList<LeagueTiebreakGameState> TiebreakGames => _tiebreakGames;

        public LeagueId GetTargetLeagueId(int teamId, LeagueId currentLeagueId)
        {
            for (int index = 0; index < _records.Length; index++)
            {
                if (_records[index].TeamId == teamId)
                    return _records[index].TargetLeagueId;
            }
            return currentLeagueId;
        }

        public TeamLeagueMovementRecord? Find(int teamId)
        {
            for (int index = 0; index < _records.Length; index++)
            {
                if (_records[index].TeamId == teamId)
                    return _records[index];
            }
            return null;
        }

        private static int CompareRecords(TeamLeagueMovementRecord left, TeamLeagueMovementRecord right)
        {
            int tier = left.PreviousTier.CompareTo(right.PreviousTier);
            return tier != 0 ? tier : left.TeamId.CompareTo(right.TeamId);
        }
    }

    /// <summary>리그별 최종 순위를 모두 읽은 뒤 경계별 두 구단 교환 계획을 만든다.</summary>
    public sealed class LeagueMovementPlanner
    {
        private const ulong TiebreakerStream = 0x544945425245414BUL;

        private readonly CareerState _career;
        private readonly BalanceTable _balance;

        public LeagueMovementPlanner()
        {
        }

        public LeagueMovementPlanner(CareerState career, BalanceTable balance)
        {
            _career = career ?? throw new ArgumentNullException(nameof(career));
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
        }

        public LeagueMovementPlan CreatePlan(WorldState world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (world.Leagues.Count == 1)
                return LeagueMovementPlan.Empty;
            if (world.Leagues.Count != LeagueLevelRules.Count)
                throw new InvalidOperationException($"승강 월드는 {LeagueLevelRules.Count}개 리그가 모두 필요합니다.");

            var records = new List<TeamLeagueMovementRecord>((world.Leagues.Count - 1) * 4);
            var tiebreakGames = new List<LeagueTiebreakGameState>();
            var standingsByLeague = new int[world.Leagues.Count][];
            for (int leagueIndex = 0; leagueIndex < world.Leagues.Count; leagueIndex++)
            {
                standingsByLeague[leagueIndex] = GetOrderedTeamIds(
                    world.Leagues[leagueIndex],
                    tiebreakGames);
            }
            for (int lowerIndex = 0; lowerIndex < world.Leagues.Count - 1; lowerIndex++)
            {
                LeagueState lower = world.Leagues[lowerIndex];
                LeagueState upper = world.Leagues[lowerIndex + 1];
                if ((int)upper.LeagueLevel != (int)lower.LeagueLevel + 1)
                    throw new InvalidOperationException("승강 대상 리그 단계가 인접하지 않습니다.");

                int[] lowerStandings = standingsByLeague[lowerIndex];
                int[] upperStandings = standingsByLeague[lowerIndex + 1];
                LeagueDefinition lowerDefinition = WorldGenerationConfiguration.GetDefaultDefinition(lower.LeagueLevel);
                LeagueDefinition upperDefinition = WorldGenerationConfiguration.GetDefaultDefinition(upper.LeagueLevel);
                int slots = Math.Min(lowerDefinition.PromotionSlots, upperDefinition.RelegationSlots);
                if (slots <= 0 || slots * 2 > lowerStandings.Length || slots * 2 > upperStandings.Length)
                    throw new InvalidOperationException("승강 슬롯 수가 리그 구단 수와 맞지 않습니다.");

                for (int slot = 0; slot < slots; slot++)
                {
                    records.Add(new TeamLeagueMovementRecord(
                        lower.CurrentSeason.SeasonId,
                        lower.CurrentSeason.Year,
                        lowerStandings[slot],
                        slot + 1,
                        TeamLeagueMovementType.Promotion,
                        lower.LeagueId,
                        lower.LeagueLevel,
                        upper.LeagueId,
                        upper.LeagueLevel));

                    int relegationIndex = upperStandings.Length - slots + slot;
                    records.Add(new TeamLeagueMovementRecord(
                        upper.CurrentSeason.SeasonId,
                        upper.CurrentSeason.Year,
                        upperStandings[relegationIndex],
                        relegationIndex + 1,
                        TeamLeagueMovementType.Relegation,
                        upper.LeagueId,
                        upper.LeagueLevel,
                        lower.LeagueId,
                        lower.LeagueLevel));
                }
            }
            return new LeagueMovementPlan(records.ToArray(), tiebreakGames.ToArray());
        }

        private int[] GetOrderedTeamIds(
            LeagueState league,
            List<LeagueTiebreakGameState> tiebreakGames)
        {
            SeasonState season = league.CurrentSeason ??
                throw new InvalidOperationException($"{league.LeagueId}의 현재 시즌이 없습니다.");
            if (season.FinalStandingTeamIds.Count > 0)
            {
                for (int gameIndex = 0; gameIndex < season.TiebreakGames.Count; gameIndex++)
                    tiebreakGames.Add(season.TiebreakGames[gameIndex]);
                var finalized = new int[season.FinalStandingTeamIds.Count];
                for (int index = 0; index < finalized.Length; index++)
                    finalized[index] = season.FinalStandingTeamIds[index];
                return finalized;
            }
            if (season.TeamRecords == null || season.TeamRecords.Count != league.Teams.Count)
                throw new InvalidOperationException($"{league.LeagueId}의 최종 순위 원본이 없습니다.");

            var entries = new TeamStandingEntry[season.TeamRecords.Count];
            for (int index = 0; index < entries.Length; index++)
            {
                TeamSeasonRecordState record = season.TeamRecords[index];
                entries[index] = new TeamStandingEntry(
                    record.TeamId,
                    record.Wins,
                    record.Losses,
                    record.RunsScored,
                    record.RunsAllowed,
                    record.FixedTiebreaker,
                    record.GetHeadToHeadEntries());
            }
            int[] ordered = PostseasonBracket.SelectSeeds(entries, entries.Length);
            ResolveBoundaryTie(league, entries, ordered, boundaryRank: 2, tiebreakGames);
            ResolveBoundaryTie(league, entries, ordered, boundaryRank: 4, tiebreakGames);
            ResolveBoundaryTie(league, entries, ordered, boundaryRank: 6, tiebreakGames);
            return ordered;
        }

        /// <summary>정규시즌 종료 즉시 세 승강 경계의 완전 동률을 해결해 최종 순서를 반환한다.</summary>
        public int[] ResolveFinalStandings(
            LeagueState league,
            out LeagueTiebreakGameState[] tiebreakGames)
        {
            var games = new List<LeagueTiebreakGameState>();
            int[] result = GetOrderedTeamIds(league, games);
            tiebreakGames = games.ToArray();
            return result;
        }

        private void ResolveBoundaryTie(
            LeagueState league,
            TeamStandingEntry[] entries,
            int[] orderedTeamIds,
            int boundaryRank,
            List<LeagueTiebreakGameState> tiebreakGames)
        {
            TeamStandingEntry upper = FindEntry(entries, orderedTeamIds[boundaryRank - 1]);
            TeamStandingEntry lower = FindEntry(entries, orderedTeamIds[boundaryRank]);
            if (!IsExactBoundaryTie(upper, lower))
                return;

            int gameId = 900_000 + (int)league.LeagueLevel * 10 + boundaryRank;
            ulong seed = DeterministicSeed.Derive(
                league.RandomSeed,
                TiebreakerStream ^
                ((ulong)(uint)league.CurrentSeason.SeasonId << 32) ^
                (uint)gameId);
            int winnerTeamId;
            int loserTeamId;
            int awayRuns;
            int homeRuns;
            if (_career != null && _balance != null)
            {
                var game = new ScheduledGameState(
                    gameId,
                    100 + boundaryRank,
                    seed,
                    lower.TeamId,
                    upper.TeamId);
                MatchResult result = new CareerGameRunner(_career, _balance, league)
                    .SimulateGame(
                        game,
                        PlayerGameRole.Inactive,
                        league.CurrentSeason.SeasonId,
                        requiresWinner: true);
                awayRuns = result.AwayBoxScore.Runs;
                homeRuns = result.HomeBoxScore.Runs;
                winnerTeamId = awayRuns > homeRuns ? lower.TeamId : upper.TeamId;
                loserTeamId = winnerTeamId == lower.TeamId ? upper.TeamId : lower.TeamId;
            }
            else
            {
                bool lowerWins = new Pcg32Random(seed).NextDouble() < 0.5d;
                winnerTeamId = lowerWins ? lower.TeamId : upper.TeamId;
                loserTeamId = lowerWins ? upper.TeamId : lower.TeamId;
                awayRuns = lowerWins ? 1 : 0;
                homeRuns = lowerWins ? 0 : 1;
            }

            if (winnerTeamId == lower.TeamId)
            {
                orderedTeamIds[boundaryRank - 1] = lower.TeamId;
                orderedTeamIds[boundaryRank] = upper.TeamId;
            }
            tiebreakGames.Add(new LeagueTiebreakGameState(
                gameId,
                league.CurrentSeason.SeasonId,
                league.LeagueId,
                boundaryRank,
                lower.TeamId,
                upper.TeamId,
                awayRuns,
                homeRuns,
                winnerTeamId,
                loserTeamId,
                seed));
        }

        private static TeamStandingEntry FindEntry(TeamStandingEntry[] entries, int teamId)
        {
            for (int index = 0; index < entries.Length; index++)
            {
                if (entries[index].TeamId == teamId)
                    return entries[index];
            }
            throw new InvalidOperationException($"TeamId {teamId}의 순위 원본이 없습니다.");
        }

        private static bool IsExactBoundaryTie(TeamStandingEntry left, TeamStandingEntry right)
        {
            return Math.Abs(left.WinningPercentage - right.WinningPercentage) < 0.0000001d &&
                   Math.Abs(left.GetHeadToHeadWinningPercentage(right.TeamId) -
                            right.GetHeadToHeadWinningPercentage(left.TeamId)) < 0.0000001d &&
                   left.RunDifferential == right.RunDifferential;
        }
    }

    /// <summary>정규시즌 기록과 분리된 승격·포스트시즌·잔류 경계 결정전 결과다.</summary>
    public readonly struct LeagueTiebreakGameState
    {
        public LeagueTiebreakGameState(
            int gameId,
            int seasonId,
            LeagueId leagueId,
            int boundaryRank,
            int awayTeamId,
            int homeTeamId,
            int awayRuns,
            int homeRuns,
            int winnerTeamId,
            int loserTeamId,
            ulong randomSeed)
        {
            GameId = gameId;
            SeasonId = seasonId;
            LeagueId = leagueId;
            BoundaryRank = boundaryRank;
            AwayTeamId = awayTeamId;
            HomeTeamId = homeTeamId;
            AwayRuns = awayRuns;
            HomeRuns = homeRuns;
            WinnerTeamId = winnerTeamId;
            LoserTeamId = loserTeamId;
            RandomSeed = randomSeed;
        }

        public int GameId { get; }
        public int SeasonId { get; }
        public LeagueId LeagueId { get; }
        public int BoundaryRank { get; }
        public int AwayTeamId { get; }
        public int HomeTeamId { get; }
        public int AwayRuns { get; }
        public int HomeRuns { get; }
        public int WinnerTeamId { get; }
        public int LoserTeamId { get; }
        public ulong RandomSeed { get; }
    }

    /// <summary>월드가 확정한 구단 승강 이력을 날짜 역행 없이 누적한다.</summary>
    public sealed class TeamLeagueMovementLedger
    {
        private readonly List<TeamLeagueMovementRecord> _records = new();
        public IReadOnlyList<TeamLeagueMovementRecord> Records => _records;

        public void Record(TeamLeagueMovementRecord record) => _records.Add(record);
    }
}
