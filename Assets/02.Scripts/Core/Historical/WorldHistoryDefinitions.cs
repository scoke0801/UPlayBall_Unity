using System;
using System.Collections.Generic;
using Baseball.Core.Players;

namespace Baseball.Core.Historical
{
    /// <summary>새 게임 이전 기록을 Simulation 또는 Baked 원기록으로 초기화하는 방식을 구분한다.</summary>
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

    /// <summary>OriginalHistory와 SimulatedHistory가 공통 Consumer에 제공하는 수상 기록이다.</summary>
    public sealed class WorldAwardRecord
    {
        private readonly WorldAwardEntry[] _entries;

        public WorldAwardRecord(IReadOnlyList<WorldAwardEntry> entries)
        {
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));
            _entries = new WorldAwardEntry[entries.Count];
            var unique = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < entries.Count; index++)
            {
                WorldAwardEntry entry = entries[index];
                string key = entry.SeasonYear + ":" + entry.AwardType + ":" + entry.Position + ":" + entry.PlayerSeasonId;
                if (!unique.Add(key))
                    throw new ArgumentException("같은 World Award를 중복 저장할 수 없습니다.", nameof(entries));
                _entries[index] = entry;
            }
        }

        public IReadOnlyList<WorldAwardEntry> Entries => _entries;

        public bool HasAward(string playerSeasonId, WorldAwardType awardType)
        {
            for (int index = 0; index < _entries.Length; index++)
            {
                if (_entries[index].AwardType == awardType &&
                    string.Equals(_entries[index].PlayerSeasonId, playerSeasonId, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }
    }

    /// <summary>두 기록 초기화 경로가 수렴하며 Save에 한 번만 저장하는 World 역사 Snapshot이다.</summary>
    public sealed class WorldHistorySnapshot
    {
        private readonly SeasonStatistics[] _statistics;

        public WorldHistorySnapshot(
            WorldRecordMode recordMode,
            ulong worldHistorySeed,
            IReadOnlyList<SeasonStatistics> statistics,
            WorldAwardRecord awards)
        {
            if (statistics == null)
                throw new ArgumentNullException(nameof(statistics));
            _statistics = new SeasonStatistics[statistics.Count];
            for (int index = 0; index < statistics.Count; index++)
                _statistics[index] = statistics[index] ?? throw new ArgumentException("null 기록이 있습니다.", nameof(statistics));
            RecordMode = recordMode;
            WorldHistorySeed = worldHistorySeed;
            Awards = awards ?? throw new ArgumentNullException(nameof(awards));
        }

        public WorldRecordMode RecordMode { get; }
        public ulong WorldHistorySeed { get; }
        public IReadOnlyList<SeasonStatistics> Statistics => _statistics;
        public WorldAwardRecord Awards { get; }
    }

    /// <summary>OriginalHistory용 Runtime-safe 선수 시즌 기록이다.</summary>
    public sealed class OriginalSeasonRecordDefinition
    {
        public OriginalSeasonRecordDefinition(SeasonStatistics statistics)
        {
            Statistics = statistics ?? throw new ArgumentNullException(nameof(statistics));
        }

        public SeasonStatistics Statistics { get; }
    }

    /// <summary>OriginalHistory용 Runtime-safe 수상 기록이다.</summary>
    public sealed class OriginalAwardRecordDefinition
    {
        public OriginalAwardRecordDefinition(WorldAwardEntry award)
        {
            Award = award;
        }

        public WorldAwardEntry Award { get; }
    }
}
