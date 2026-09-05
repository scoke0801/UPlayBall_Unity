using System;
using System.Collections.Generic;
using Baseball.Core.Players;

namespace Baseball.Core.Historical
{
    /// <summary>정식 Simulation History와 회귀 검증용 Legacy 원기록 경로를 구분한다.</summary>
    public enum WorldRecordMode
    {
        SimulatedHistory,
        OriginalHistory
    }

    /// <summary>World Award와 Edition 활성화가 공유하는 수상 종류다.</summary>
    public enum WorldAwardType
    {
        AllStar,
        GoldenGlove,
        RegularSeasonMvp,
        AllStarGameMvp,
        PostseasonMvp
    }

    /// <summary>Award Resolver가 BaseAttributes 대신 소비하는 한 선수 시즌의 실제 누적 기록이다.</summary>
    public sealed class SeasonStatistics
    {
        public SeasonStatistics(
            string playerSeasonId,
            string teamSeasonKey,
            int seasonYear,
            PlayerPosition position,
            int plateAppearances = 0,
            int hits = 0,
            int homeRuns = 0,
            int walks = 0,
            int strikeouts = 0,
            int stolenBases = 0,
            int pitchingOuts = 0,
            int earnedRuns = 0,
            int pitchingStrikeouts = 0,
            int defensiveChances = 0,
            int defensiveOutsAboveAverage = 0,
            int fieldingErrors = 0,
            bool isFirstHalf = false,
            bool isPostseason = false,
            bool isAllStarGame = false)
        {
            PlayerSeasonId = RequireId(playerSeasonId, nameof(playerSeasonId));
            TeamSeasonKey = RequireId(teamSeasonKey, nameof(teamSeasonKey));
            if (seasonYear <= 0)
                throw new ArgumentOutOfRangeException(nameof(seasonYear));
            if (position == PlayerPosition.Unknown)
                throw new ArgumentException("기록 포지션이 필요합니다.", nameof(position));
            int[] counts =
            {
                plateAppearances, hits, homeRuns, walks, strikeouts, stolenBases,
                pitchingOuts, earnedRuns, pitchingStrikeouts, defensiveChances, fieldingErrors
            };
            for (int index = 0; index < counts.Length; index++)
                if (counts[index] < 0) throw new ArgumentOutOfRangeException(nameof(plateAppearances));

            SeasonYear = seasonYear;
            Position = position;
            PlateAppearances = plateAppearances;
            Hits = hits;
            HomeRuns = homeRuns;
            Walks = walks;
            Strikeouts = strikeouts;
            StolenBases = stolenBases;
            PitchingOuts = pitchingOuts;
            EarnedRuns = earnedRuns;
            PitchingStrikeouts = pitchingStrikeouts;
            DefensiveChances = defensiveChances;
            DefensiveOutsAboveAverage = defensiveOutsAboveAverage;
            FieldingErrors = fieldingErrors;
            IsFirstHalf = isFirstHalf;
            IsPostseason = isPostseason;
            IsAllStarGame = isAllStarGame;
        }

        public string PlayerSeasonId { get; }
        public string TeamSeasonKey { get; }
        public int SeasonYear { get; }
        public PlayerPosition Position { get; }
        public int PlateAppearances { get; }
        public int Hits { get; }
        public int HomeRuns { get; }
        public int Walks { get; }
        public int Strikeouts { get; }
        public int StolenBases { get; }
        public int PitchingOuts { get; }
        public int EarnedRuns { get; }
        public int PitchingStrikeouts { get; }
        public int DefensiveChances { get; }
        public int DefensiveOutsAboveAverage { get; }
        public int FieldingErrors { get; }
        public bool IsFirstHalf { get; }
        public bool IsPostseason { get; }
        public bool IsAllStarGame { get; }

        public double BattingAverage => PlateAppearances == 0 ? 0d : (double)Hits / PlateAppearances;
        public double EarnedRunAverage => PitchingOuts == 0 ? 0d : EarnedRuns * 27d / PitchingOuts;

        private static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("식별자는 비어 있을 수 없습니다.", parameterName);
            return value.Trim();
        }
    }

    /// <summary>실제 정규 시즌 Match BoxScore에서 누적한 한 Canonical TeamSeason의 성적이다.</summary>
    public sealed class TeamSeasonStatistics
    {
        public TeamSeasonStatistics(
            string teamSeasonKey,
            int seasonYear,
            int games,
            int wins,
            int losses,
            int ties,
            int runsScored,
            int runsAllowed,
            int atBats,
            int hits,
            int pitchingOuts,
            int earnedRuns,
            int hitsAllowed,
            int walksAllowed)
        {
            TeamSeasonKey = RequireId(teamSeasonKey, nameof(teamSeasonKey));
            if (seasonYear <= 0)
                throw new ArgumentOutOfRangeException(nameof(seasonYear));
            int[] counts =
            {
                games, wins, losses, ties, runsScored, runsAllowed, atBats, hits,
                pitchingOuts, earnedRuns, hitsAllowed, walksAllowed
            };
            for (int index = 0; index < counts.Length; index++)
                if (counts[index] < 0) throw new ArgumentOutOfRangeException(nameof(games));
            if (games != wins + losses + ties)
                throw new ArgumentException("경기 수는 승·패·무의 합과 같아야 합니다.", nameof(games));
            if (hits > atBats)
                throw new ArgumentException("안타 수는 타수보다 많을 수 없습니다.", nameof(hits));

            SeasonYear = seasonYear;
            Games = games;
            Wins = wins;
            Losses = losses;
            Ties = ties;
            RunsScored = runsScored;
            RunsAllowed = runsAllowed;
            AtBats = atBats;
            Hits = hits;
            PitchingOuts = pitchingOuts;
            EarnedRuns = earnedRuns;
            HitsAllowed = hitsAllowed;
            WalksAllowed = walksAllowed;
        }

        public string TeamSeasonKey { get; }
        public int SeasonYear { get; }
        public int Games { get; }
        public int Wins { get; }
        public int Losses { get; }
        public int Ties { get; }
        public int RunsScored { get; }
        public int RunsAllowed { get; }
        public int AtBats { get; }
        public int Hits { get; }
        public int PitchingOuts { get; }
        public int EarnedRuns { get; }
        public int HitsAllowed { get; }
        public int WalksAllowed { get; }
        public double WinningPercentage => Wins + Losses == 0 ? 0d : (double)Wins / (Wins + Losses);
        public double BattingAverage => AtBats == 0 ? 0d : (double)Hits / AtBats;
        public double EarnedRunAverage => PitchingOuts == 0 ? 0d : EarnedRuns * 27d / PitchingOuts;
        public double Whip => PitchingOuts == 0 ? 0d : (HitsAllowed + WalksAllowed) * 3d / PitchingOuts;

        private static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("식별자는 비어 있을 수 없습니다.", parameterName);
            return value.Trim();
        }
    }

    /// <summary>실제 정규 시즌 Match 결과로 확정한 한 TeamSeason의 최종 순위다.</summary>
    public sealed class HistoricalStandingEntry
    {
        public HistoricalStandingEntry(int seasonYear, int rank, string teamSeasonKey)
        {
            if (seasonYear <= 0)
                throw new ArgumentOutOfRangeException(nameof(seasonYear));
            if (rank <= 0)
                throw new ArgumentOutOfRangeException(nameof(rank));
            if (string.IsNullOrWhiteSpace(teamSeasonKey))
                throw new ArgumentException("TeamSeasonKey는 비어 있을 수 없습니다.", nameof(teamSeasonKey));
            SeasonYear = seasonYear;
            Rank = rank;
            TeamSeasonKey = teamSeasonKey.Trim();
        }

        public int SeasonYear { get; }
        public int Rank { get; }
        public string TeamSeasonKey { get; }
    }

    /// <summary>정규 시즌 순위에서 진출한 구단과 실제 Postseason Match의 Champion을 보관한다.</summary>
    public sealed class HistoricalPostseasonResult
    {
        private readonly string[] _qualifiedTeamSeasonKeys;

        public HistoricalPostseasonResult(
            int seasonYear,
            IReadOnlyList<string> qualifiedTeamSeasonKeys,
            string championTeamSeasonKey)
        {
            if (seasonYear <= 0)
                throw new ArgumentOutOfRangeException(nameof(seasonYear));
            if (qualifiedTeamSeasonKeys == null || qualifiedTeamSeasonKeys.Count == 0)
                throw new ArgumentException("Postseason 진출 구단이 필요합니다.", nameof(qualifiedTeamSeasonKeys));
            if (string.IsNullOrWhiteSpace(championTeamSeasonKey))
                throw new ArgumentException("Champion TeamSeasonKey가 필요합니다.", nameof(championTeamSeasonKey));

            SeasonYear = seasonYear;
            ChampionTeamSeasonKey = championTeamSeasonKey.Trim();
            _qualifiedTeamSeasonKeys = new string[qualifiedTeamSeasonKeys.Count];
            var unique = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < _qualifiedTeamSeasonKeys.Length; index++)
            {
                string key = qualifiedTeamSeasonKeys[index]?.Trim();
                if (string.IsNullOrEmpty(key) || !unique.Add(key))
                    throw new ArgumentException("Postseason 진출 TeamSeasonKey는 비어 있거나 중복될 수 없습니다.", nameof(qualifiedTeamSeasonKeys));
                _qualifiedTeamSeasonKeys[index] = key;
            }
            if (!unique.Contains(ChampionTeamSeasonKey))
                throw new ArgumentException("Champion은 Postseason 진출 구단이어야 합니다.", nameof(championTeamSeasonKey));
        }

        public int SeasonYear { get; }
        public IReadOnlyList<string> QualifiedTeamSeasonKeys => _qualifiedTeamSeasonKeys;
        public string ChampionTeamSeasonKey { get; }
    }

    /// <summary>한 Historical Season Simulation이 만든 개인·팀·순위·Postseason 결과다.</summary>
    public sealed class HistoricalSeasonSimulationResult
    {
        private readonly SeasonStatistics[] _statistics;
        private readonly TeamSeasonStatistics[] _teamStatistics;
        private readonly HistoricalStandingEntry[] _standings;

        public HistoricalSeasonSimulationResult(
            IReadOnlyList<SeasonStatistics> statistics,
            IReadOnlyList<TeamSeasonStatistics> teamStatistics,
            IReadOnlyList<HistoricalStandingEntry> standings,
            HistoricalPostseasonResult postseason)
        {
            _statistics = Copy(statistics, nameof(statistics));
            _teamStatistics = Copy(teamStatistics, nameof(teamStatistics));
            _standings = Copy(standings, nameof(standings));
            Postseason = postseason ?? throw new ArgumentNullException(nameof(postseason));
        }

        public IReadOnlyList<SeasonStatistics> Statistics => _statistics;
        public IReadOnlyList<TeamSeasonStatistics> TeamStatistics => _teamStatistics;
        public IReadOnlyList<HistoricalStandingEntry> Standings => _standings;
        public HistoricalPostseasonResult Postseason { get; }

        private static T[] Copy<T>(IReadOnlyList<T> source, string parameterName) where T : class
        {
            if (source == null)
                throw new ArgumentNullException(parameterName);
            var result = new T[source.Count];
            for (int index = 0; index < result.Length; index++)
                result[index] = source[index] ?? throw new ArgumentException("null 결과가 있습니다.", parameterName);
            return result;
        }
    }

    /// <summary>한 World에서 확정된 수상자와 그 수상 포지션을 보관한다.</summary>
    public readonly struct WorldAwardEntry
    {
        public WorldAwardEntry(int seasonYear, WorldAwardType awardType, string playerSeasonId, PlayerPosition position)
        {
            if (seasonYear <= 0)
                throw new ArgumentOutOfRangeException(nameof(seasonYear));
            if (string.IsNullOrWhiteSpace(playerSeasonId))
                throw new ArgumentException("PlayerSeasonId는 비어 있을 수 없습니다.", nameof(playerSeasonId));
            SeasonYear = seasonYear;
            AwardType = awardType;
            PlayerSeasonId = playerSeasonId.Trim();
            Position = position;
        }

        public int SeasonYear { get; }
        public WorldAwardType AwardType { get; }
        public string PlayerSeasonId { get; }
        public PlayerPosition Position { get; }
    }

    /// <summary>정식 Simulation과 Legacy 검증 경로가 공통 Consumer에 제공하는 수상 기록이다.</summary>
    public sealed class WorldAwardRecord
    {
        private readonly WorldAwardEntry[] _entries;

        /// <summary>
        /// PlayerSeasonId → 그 선수 시즌이 받은 수상 종류 비트마스크다.
        /// 카드 카탈로그는 선수 시즌 1만 7천여 건마다 수상 여부를 5번 물어보므로,
        /// 선형 탐색이면 1억 회를 넘는 문자열 비교가 되어 새 게임 시작에서 초 단위를 먹는다.
        /// 조회 전용이며 순회하지 않으므로 Dictionary를 써도 결정론 계약을 깨지 않는다.
        /// </summary>
        private readonly Dictionary<string, int> _awardMaskByPlayerSeason;

        public WorldAwardRecord(IReadOnlyList<WorldAwardEntry> entries)
        {
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));
            _entries = new WorldAwardEntry[entries.Count];
            _awardMaskByPlayerSeason = new Dictionary<string, int>(entries.Count, StringComparer.Ordinal);
            var unique = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < entries.Count; index++)
            {
                WorldAwardEntry entry = entries[index];
                string key = entry.SeasonYear + ":" + entry.AwardType + ":" + entry.Position + ":" + entry.PlayerSeasonId;
                if (!unique.Add(key))
                    throw new ArgumentException("같은 World Award를 중복 저장할 수 없습니다.", nameof(entries));
                _entries[index] = entry;
                _awardMaskByPlayerSeason.TryGetValue(entry.PlayerSeasonId, out int mask);
                _awardMaskByPlayerSeason[entry.PlayerSeasonId] = mask | ToMask(entry.AwardType);
            }
        }

        public IReadOnlyList<WorldAwardEntry> Entries => _entries;

        public bool HasAward(string playerSeasonId, WorldAwardType awardType)
        {
            if (playerSeasonId == null)
                return false;
            return _awardMaskByPlayerSeason.TryGetValue(playerSeasonId, out int mask) &&
                   (mask & ToMask(awardType)) != 0;
        }

        private static int ToMask(WorldAwardType awardType) => 1 << (int)awardType;
    }

    /// <summary>두 기록 초기화 경로가 수렴하며 Save에 한 번만 저장하는 World 역사 Snapshot이다.</summary>
    public sealed class WorldHistorySnapshot
    {
        private readonly SeasonStatistics[] _statistics;
        private readonly TeamSeasonStatistics[] _teamStatistics;
        private readonly HistoricalStandingEntry[] _standings;
        private readonly HistoricalPostseasonResult[] _postseasonResults;

        public WorldHistorySnapshot(
            WorldRecordMode recordMode,
            ulong worldHistorySeed,
            IReadOnlyList<SeasonStatistics> statistics,
            WorldAwardRecord awards)
            : this(
                recordMode,
                worldHistorySeed,
                statistics,
                Array.Empty<TeamSeasonStatistics>(),
                Array.Empty<HistoricalStandingEntry>(),
                Array.Empty<HistoricalPostseasonResult>(),
                awards)
        {
        }

        public WorldHistorySnapshot(
            WorldRecordMode recordMode,
            ulong worldHistorySeed,
            IReadOnlyList<SeasonStatistics> statistics,
            IReadOnlyList<TeamSeasonStatistics> teamStatistics,
            IReadOnlyList<HistoricalStandingEntry> standings,
            IReadOnlyList<HistoricalPostseasonResult> postseasonResults,
            WorldAwardRecord awards)
        {
            if (statistics == null)
                throw new ArgumentNullException(nameof(statistics));
            _statistics = new SeasonStatistics[statistics.Count];
            for (int index = 0; index < statistics.Count; index++)
                _statistics[index] = statistics[index] ?? throw new ArgumentException("null 기록이 있습니다.", nameof(statistics));
            _teamStatistics = Copy(teamStatistics, nameof(teamStatistics));
            _standings = Copy(standings, nameof(standings));
            _postseasonResults = Copy(postseasonResults, nameof(postseasonResults));
            RecordMode = recordMode;
            WorldHistorySeed = worldHistorySeed;
            Awards = awards ?? throw new ArgumentNullException(nameof(awards));
        }

        public WorldRecordMode RecordMode { get; }
        public ulong WorldHistorySeed { get; }
        public IReadOnlyList<SeasonStatistics> Statistics => _statistics;
        public IReadOnlyList<TeamSeasonStatistics> TeamStatistics => _teamStatistics;
        public IReadOnlyList<HistoricalStandingEntry> Standings => _standings;
        public IReadOnlyList<HistoricalPostseasonResult> PostseasonResults => _postseasonResults;
        public WorldAwardRecord Awards { get; }

        private static T[] Copy<T>(IReadOnlyList<T> source, string parameterName) where T : class
        {
            if (source == null)
                throw new ArgumentNullException(parameterName);
            var result = new T[source.Count];
            for (int index = 0; index < result.Length; index++)
                result[index] = source[index] ?? throw new ArgumentException("null 기록이 있습니다.", parameterName);
            return result;
        }
    }

    /// <summary>Legacy/Debug OriginalHistory 회귀 검증용 선수 시즌 기록이다.</summary>
    public sealed class OriginalSeasonRecordDefinition
    {
        public OriginalSeasonRecordDefinition(SeasonStatistics statistics)
        {
            Statistics = statistics ?? throw new ArgumentNullException(nameof(statistics));
        }

        public SeasonStatistics Statistics { get; }
    }

    /// <summary>Legacy/Debug OriginalHistory 회귀 검증용 수상 기록이다.</summary>
    public sealed class OriginalAwardRecordDefinition
    {
        public OriginalAwardRecordDefinition(WorldAwardEntry award)
        {
            Award = award;
        }

        public WorldAwardEntry Award { get; }
    }
}
