using System;
using System.Collections.Generic;
using System.Diagnostics;
using Baseball.Core.Balance;
using Baseball.Core.Historical;
using Baseball.Simulation.Historical;
using Baseball.Simulation.Random;

namespace Baseball.Game.Historical
{
    /// <summary>장기 World 실행이 멈춘 정확한 OriginYear를 보존한다.</summary>
    public sealed class HistoricalWorldSeasonSimulationException : Exception
    {
        public HistoricalWorldSeasonSimulationException(int seasonYear, Exception innerException)
            : base($"{seasonYear} Historical Season Simulation에 실패했습니다.", innerException)
        {
            SeasonYear = seasonYear;
        }

        public int SeasonYear { get; }
    }

    /// <summary>한 Historical World 생성에서 수행한 연도별 경기와 비용을 합산한다.</summary>
    public sealed class HistoricalWorldBuildMetrics
    {
        private readonly HistoricalSeasonSimulationMetrics[] _seasons;

        public HistoricalWorldBuildMetrics(
            IReadOnlyList<HistoricalSeasonSimulationMetrics> seasons,
            long totalElapsedTicks)
        {
            if (seasons == null)
                throw new ArgumentNullException(nameof(seasons));
            _seasons = new HistoricalSeasonSimulationMetrics[seasons.Count];
            int totalGames = 0;
            long simulationTicks = 0L;
            long allocatedBytes = 0L;
            bool usesExactCounter = seasons.Count > 0;
            for (int index = 0; index < seasons.Count; index++)
            {
                HistoricalSeasonSimulationMetrics season = seasons[index]
                    ?? throw new ArgumentException("null 시즌 측정값이 있습니다.", nameof(seasons));
                _seasons[index] = season;
                totalGames = checked(totalGames + season.TotalGameCount);
                simulationTicks = checked(simulationTicks + season.ElapsedTicks);
                allocatedBytes = checked(allocatedBytes + season.AllocatedBytes);
                usesExactCounter &= season.UsesExactAllocationCounter;
            }
            TotalGameCount = totalGames;
            HistoricalSimulationElapsedTicks = simulationTicks;
            TotalElapsedTicks = totalElapsedTicks;
            AllocatedBytes = allocatedBytes;
            UsesExactAllocationCounter = usesExactCounter;
        }

        public IReadOnlyList<HistoricalSeasonSimulationMetrics> Seasons => _seasons;
        public int TotalGameCount { get; }
        public long HistoricalSimulationElapsedTicks { get; }
        public long TotalElapsedTicks { get; }
        public long AllocatedBytes { get; }
        public bool UsesExactAllocationCounter { get; }
        public double HistoricalSimulationMilliseconds => ToMilliseconds(HistoricalSimulationElapsedTicks);
        public double TotalElapsedMilliseconds => ToMilliseconds(TotalElapsedTicks);
        public double MillisecondsPerGame => TotalGameCount == 0
            ? 0d
            : HistoricalSimulationMilliseconds / TotalGameCount;

        private static double ToMilliseconds(long ticks) => ticks * 1000d / Stopwatch.Frequency;
    }

    /// <summary>공통 World Record, 카드, Award 확정 뒤 합성팀을 함께 반환하는 불변 Runtime 결과다.</summary>
    public sealed class HistoricalWorldRuntimeContent
    {
        private readonly SpecialCompositeTeamSet[] _specialCompositeTeams;

        public HistoricalWorldRuntimeContent(
            HistoricalContentReference contentReference,
            WorldIdentityRegistry identityRegistry,
            WorldHistorySnapshot worldHistory,
            WorldCardCatalog worldCardCatalog,
            IReadOnlyList<SpecialCompositeTeamSet> specialCompositeTeams,
            HistoricalWorldBuildMetrics metrics)
        {
            ContentReference = contentReference ?? throw new ArgumentNullException(nameof(contentReference));
            IdentityRegistry = identityRegistry ?? throw new ArgumentNullException(nameof(identityRegistry));
            WorldHistory = worldHistory ?? throw new ArgumentNullException(nameof(worldHistory));
            WorldCardCatalog = worldCardCatalog ?? throw new ArgumentNullException(nameof(worldCardCatalog));
            Metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            if (specialCompositeTeams == null)
                throw new ArgumentNullException(nameof(specialCompositeTeams));
            _specialCompositeTeams = new SpecialCompositeTeamSet[specialCompositeTeams.Count];
            for (int index = 0; index < specialCompositeTeams.Count; index++)
                _specialCompositeTeams[index] = specialCompositeTeams[index]
                    ?? throw new ArgumentException("null 특수 합성팀 묶음이 있습니다.", nameof(specialCompositeTeams));
        }

        public HistoricalContentReference ContentReference { get; }
        public WorldIdentityRegistry IdentityRegistry { get; }
        public WorldHistorySnapshot WorldHistory { get; }
        public WorldAwardRecord WorldAwardRecord => WorldHistory.Awards;
        public WorldCardCatalog WorldCardCatalog { get; }
        public IReadOnlyList<SpecialCompositeTeamSet> SpecialCompositeTeams => _specialCompositeTeams;
        public HistoricalWorldBuildMetrics Metrics { get; }
    }

    /// <summary>연도 순서대로 World History를 확정한 뒤 카드와 특수 합성팀을 만드는 단일 조립점이다.</summary>
    public sealed class HistoricalWorldRuntimeBuilder
    {
        private const ulong CompositeTeamStream = 0x434F4D504F534954UL;

        private readonly BalanceTable _balance;
        private readonly AwardScoringPolicy _awardScoring;
        private readonly CardEditionBalanceTable _cardEditionBalance;
        private readonly IHistoricalSeasonSimulation _simulationOverride;

        public HistoricalWorldRuntimeBuilder(
            BalanceTable balance,
            AwardScoringPolicy awardScoring = null,
            CardEditionBalanceTable cardEditionBalance = null,
            IHistoricalSeasonSimulation simulationOverride = null)
        {
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            _awardScoring = awardScoring ?? AwardScoringPolicy.CreateDefault();
            _cardEditionBalance = cardEditionBalance ?? CardEditionBalanceTable.CreateInitial();
            _simulationOverride = simulationOverride;
        }

        public HistoricalWorldRuntimeContent Build(
            HistoricalBakedContent content,
            WorldRecordMode recordMode,
            ulong worldHistorySeed)
        {
            if (content == null)
                throw new ArgumentNullException(nameof(content));
            WorldIdentityRegistry identityRegistry = new WorldIdentityGenerator().Generate(
                content.PlayerPersons,
                content.TeamSeasons,
                content.IdentityNameCatalog,
                worldHistorySeed);
            return BuildCore(content, recordMode, worldHistorySeed, identityRegistry);
        }

        /// <summary>표시 Identity만 교체해 Simulation 입력 독립성을 검증하는 내부 검증 경로다.</summary>
        internal HistoricalWorldRuntimeContent BuildForValidation(
            HistoricalBakedContent content,
            WorldRecordMode recordMode,
            ulong worldHistorySeed,
            WorldIdentityRegistry identityRegistry)
        {
            if (content == null)
                throw new ArgumentNullException(nameof(content));
            if (identityRegistry == null)
                throw new ArgumentNullException(nameof(identityRegistry));
            return BuildCore(content, recordMode, worldHistorySeed, identityRegistry);
        }

        private HistoricalWorldRuntimeContent BuildCore(
            HistoricalBakedContent content,
            WorldRecordMode recordMode,
            ulong worldHistorySeed,
            WorldIdentityRegistry identityRegistry)
        {
            long startedAt = Stopwatch.GetTimestamp();
            var seasonMetrics = new List<HistoricalSeasonSimulationMetrics>(content.Years.Count);
            WorldHistorySnapshot history;
            switch (recordMode)
            {
                case WorldRecordMode.OriginalHistory:
                    history = BuildOriginalHistory(content, worldHistorySeed);
                    break;
                case WorldRecordMode.SimulatedHistory:
                    history = BuildSimulatedHistory(content, identityRegistry, worldHistorySeed, seasonMetrics);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(recordMode));
            }
            WorldCardCatalog catalog = WorldCardCatalogBuilder.Build(
                content.PlayerSeasons,
                history.Awards,
                _cardEditionBalance);
            SpecialCompositeTeamSet[] specialTeams = BuildSpecialCompositeTeams(
                content,
                history,
                catalog,
                worldHistorySeed);
            return new HistoricalWorldRuntimeContent(
                HistoricalContentReference.FromManifest(content.Manifest),
                identityRegistry,
                history,
                catalog,
                specialTeams,
                new HistoricalWorldBuildMetrics(
                    seasonMetrics,
                    Stopwatch.GetTimestamp() - startedAt));
        }

        private static WorldHistorySnapshot BuildOriginalHistory(
            HistoricalBakedContent content,
            ulong worldHistorySeed)
        {
            return new OriginalHistoryLoader().Load(
                worldHistorySeed,
                content.OriginalSeasonRecords,
                content.OriginalAwardRecords);
        }

        private WorldHistorySnapshot BuildSimulatedHistory(
            HistoricalBakedContent content,
            WorldIdentityRegistry identityRegistry,
            ulong worldHistorySeed,
            ICollection<HistoricalSeasonSimulationMetrics> metrics)
        {
            BakedHistoricalDetailedSeasonSource bakedSource = null;
            IHistoricalSeasonSimulation simulation = _simulationOverride;
            if (simulation == null)
            {
                bakedSource = new BakedHistoricalDetailedSeasonSource(
                    content,
                    _balance,
                    identityRegistry,
                    _awardScoring);
                simulation = new DetailedMatchHistoricalSeasonAdapter(bakedSource);
            }
            var initializer = new WorldHistoryInitializer(
                simulation,
                new WorldAwardResolver(_awardScoring),
                new OriginalHistoryLoader());
            var statistics = new List<SeasonStatistics>(content.PlayerSeasons.Count * 4);
            var teamStatistics = new List<TeamSeasonStatistics>(content.TeamSeasons.Count);
            var standings = new List<HistoricalStandingEntry>(content.TeamSeasons.Count);
            var postseasonResults = new List<HistoricalPostseasonResult>(content.Years.Count);
            var awards = new List<WorldAwardEntry>(content.Years.Count * 38);
            for (int yearIndex = 0; yearIndex < content.Years.Count; yearIndex++)
            {
                HistoricalYearContentDefinition year = content.Years[yearIndex];
                WorldHistorySnapshot season;
                try
                {
                    season = initializer.Initialize(
                        new WorldHistoryInitializationRequest(
                            WorldRecordMode.SimulatedHistory,
                            worldHistorySeed,
                            regularFranchiseTeams: year.TeamSeasons));
                }
                catch (Exception exception)
                {
                    throw new HistoricalWorldSeasonSimulationException(year.Year, exception);
                }
                Append(statistics, season.Statistics);
                Append(teamStatistics, season.TeamStatistics);
                Append(standings, season.Standings);
                Append(postseasonResults, season.PostseasonResults);
                Append(awards, season.Awards.Entries);
                if (bakedSource?.LastRunMetrics != null)
                    metrics.Add(bakedSource.LastRunMetrics);
            }
            return new WorldHistorySnapshot(
                WorldRecordMode.SimulatedHistory,
                worldHistorySeed,
                statistics,
                teamStatistics,
                standings,
                postseasonResults,
                new WorldAwardRecord(awards));
        }

        private SpecialCompositeTeamSet[] BuildSpecialCompositeTeams(
            HistoricalBakedContent content,
            WorldHistorySnapshot history,
            WorldCardCatalog cardCatalog,
            ulong worldHistorySeed)
        {
            var builder = new SpecialCompositeTeamBuilder(_awardScoring);
            var result = new SpecialCompositeTeamSet[content.Years.Count];
            for (int index = 0; index < content.Years.Count; index++)
            {
                HistoricalYearContentDefinition year = content.Years[index];
                ulong randomSeed = DeterministicSeed.Derive(
                    worldHistorySeed,
                    CompositeTeamStream + unchecked((ulong)year.Year));
                result[index] = builder.Build(
                    year.Year,
                    year.PlayerSeasons,
                    history,
                    cardCatalog,
                    new Pcg32Random(randomSeed));
            }
            return result;
        }

        private static void Append<T>(ICollection<T> target, IReadOnlyList<T> source)
        {
            for (int index = 0; index < source.Count; index++)
                target.Add(source[index]);
        }
    }

    /// <summary>Full Historical World를 실제로 반복 실행하고 결정론 비교에 쓸 Hash를 반환한다.</summary>
    public static class HistoricalWorldLongValidationHarness
    {
        public static HistoricalWorldValidationReport Run(
            HistoricalBakedContent content,
            BalanceTable balance,
            IReadOnlyList<ulong> worldHistorySeeds)
        {
            if (content == null)
                throw new ArgumentNullException(nameof(content));
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));
            if (worldHistorySeeds == null || worldHistorySeeds.Count == 0)
                throw new ArgumentException("하나 이상의 WorldHistorySeed가 필요합니다.", nameof(worldHistorySeeds));

            var seeds = new HistoricalWorldValidationSeed[worldHistorySeeds.Count];
            for (int index = 0; index < seeds.Length; index++)
                seeds[index] = new HistoricalWorldValidationSeed(worldHistorySeeds[index], worldHistorySeeds[index]);
            return Run(content, balance, seeds);
        }

        public static HistoricalWorldValidationReport Run(
            HistoricalBakedContent content,
            BalanceTable balance,
            IReadOnlyList<HistoricalWorldValidationSeed> seeds)
        {
            if (content == null)
                throw new ArgumentNullException(nameof(content));
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));
            if (seeds == null || seeds.Count == 0)
                throw new ArgumentException("하나 이상의 검증 Seed가 필요합니다.", nameof(seeds));

            var runs = new HistoricalWorldValidationRun[seeds.Count];
            for (int index = 0; index < runs.Length; index++)
            {
                HistoricalWorldValidationSeed seed = seeds[index];
                WorldIdentityRegistry identityRegistry = new WorldIdentityGenerator().Generate(
                    content.PlayerPersons,
                    content.TeamSeasons,
                    content.IdentityNameCatalog,
                    seed.IdentitySeed);
                HistoricalWorldRuntimeContent world = new HistoricalWorldRuntimeBuilder(balance)
                    .BuildForValidation(
                        content,
                        WorldRecordMode.SimulatedHistory,
                        seed.WorldHistorySeed,
                        identityRegistry);
                HistoricalWorldValidationFingerprints fingerprints =
                    HistoricalWorldResultHasher.ComputeFingerprints(world);
                var saveMapper = new WorldHistorySaveMapper();
                WorldHistorySnapshot restoredHistory = saveMapper.Restore(
                    saveMapper.CreateSaveData(world.WorldHistory));
                string restoredHistoryHash = HistoricalWorldResultHasher.ComputeHistory(restoredHistory);
                runs[index] = new HistoricalWorldValidationRun(
                    seed.WorldHistorySeed,
                    seed.IdentitySeed,
                    HistoricalWorldResultHasher.Compute(world),
                    fingerprints,
                    restoredHistoryHash,
                    world.WorldHistory.Statistics.Count,
                    world.WorldHistory.TeamStatistics.Count,
                    world.WorldHistory.Standings.Count,
                    world.WorldHistory.PostseasonResults.Count,
                    world.WorldAwardRecord.Entries.Count,
                    MeasureReplacementAwards(content, world.WorldAwardRecord),
                    world.Metrics);
            }
            return new HistoricalWorldValidationReport(runs);
        }

        private static HistoricalReplacementAwardMetrics MeasureReplacementAwards(
            HistoricalBakedContent content,
            WorldAwardRecord awards)
        {
            int replacementPlayerSeasons = 0;
            for (int index = 0; index < content.PlayerSeasons.Count; index++)
            {
                if (content.PlayerSeasons[index].DataProvenance == PlayerDataProvenance.ReplacementGenerated)
                    replacementPlayerSeasons++;
            }

            int allStarCount = 0;
            int replacementAllStarCount = 0;
            int goldenGloveCount = 0;
            int replacementGoldenGloveCount = 0;
            int mvpCount = 0;
            int replacementMvpCount = 0;
            for (int index = 0; index < awards.Entries.Count; index++)
            {
                WorldAwardEntry award = awards.Entries[index];
                if (!content.TryGetPlayerSeason(award.PlayerSeasonId, out PlayerSeasonDefinition season))
                    throw new InvalidOperationException($"Award PlayerSeason을 찾을 수 없습니다: {award.PlayerSeasonId}");
                bool isReplacement = season.DataProvenance == PlayerDataProvenance.ReplacementGenerated;
                switch (award.AwardType)
                {
                    case WorldAwardType.AllStar:
                        allStarCount++;
                        if (isReplacement) replacementAllStarCount++;
                        break;
                    case WorldAwardType.GoldenGlove:
                        goldenGloveCount++;
                        if (isReplacement) replacementGoldenGloveCount++;
                        break;
                    case WorldAwardType.RegularSeasonMvp:
                    case WorldAwardType.AllStarGameMvp:
                    case WorldAwardType.PostseasonMvp:
                        mvpCount++;
                        if (isReplacement) replacementMvpCount++;
                        break;
                }
            }

            return new HistoricalReplacementAwardMetrics(
                content.PlayerSeasons.Count,
                replacementPlayerSeasons,
                allStarCount,
                replacementAllStarCount,
                goldenGloveCount,
                replacementGoldenGloveCount,
                mvpCount,
                replacementMvpCount);
        }
    }

    public sealed class HistoricalWorldValidationReport
    {
        private readonly HistoricalWorldValidationRun[] _runs;

        public HistoricalWorldValidationReport(IReadOnlyList<HistoricalWorldValidationRun> runs)
        {
            _runs = new HistoricalWorldValidationRun[runs.Count];
            for (int index = 0; index < runs.Count; index++)
                _runs[index] = runs[index];
        }

        public IReadOnlyList<HistoricalWorldValidationRun> Runs => _runs;
    }

    /// <summary>Simulation Seed와 표시 Identity Seed를 분리해 독립성을 검증한다.</summary>
    public readonly struct HistoricalWorldValidationSeed
    {
        public HistoricalWorldValidationSeed(ulong worldHistorySeed, ulong identitySeed)
        {
            WorldHistorySeed = worldHistorySeed;
            IdentitySeed = identitySeed;
        }

        public ulong WorldHistorySeed { get; }
        public ulong IdentitySeed { get; }
    }

    public readonly struct HistoricalWorldValidationRun
    {
        public HistoricalWorldValidationRun(
            ulong worldHistorySeed,
            ulong identitySeed,
            string resultHash,
            HistoricalWorldValidationFingerprints fingerprints,
            string restoredHistoryHash,
            int statisticsCount,
            int teamStatisticsCount,
            int standingsCount,
            int postseasonResultCount,
            int awardCount,
            HistoricalReplacementAwardMetrics replacementAwards,
            HistoricalWorldBuildMetrics metrics)
        {
            WorldHistorySeed = worldHistorySeed;
            IdentitySeed = identitySeed;
            ResultHash = resultHash;
            Fingerprints = fingerprints;
            RestoredHistoryHash = restoredHistoryHash;
            StatisticsCount = statisticsCount;
            TeamStatisticsCount = teamStatisticsCount;
            StandingsCount = standingsCount;
            PostseasonResultCount = postseasonResultCount;
            AwardCount = awardCount;
            ReplacementAwards = replacementAwards;
            Metrics = metrics;
        }

        public ulong WorldHistorySeed { get; }
        public ulong IdentitySeed { get; }
        public string ResultHash { get; }
        public HistoricalWorldValidationFingerprints Fingerprints { get; }
        public string RestoredHistoryHash { get; }
        public bool IsSaveRoundTripStable => string.Equals(
            Fingerprints.HistoryHash,
            RestoredHistoryHash,
            StringComparison.Ordinal);
        public int StatisticsCount { get; }
        public int TeamStatisticsCount { get; }
        public int StandingsCount { get; }
        public int PostseasonResultCount { get; }
        public int AwardCount { get; }
        public HistoricalReplacementAwardMetrics ReplacementAwards { get; }
        public HistoricalWorldBuildMetrics Metrics { get; }
    }

    /// <summary>Seed 자체를 제외하고 World 결과 영역별 변화를 검증하는 Stable Hash 묶음이다.</summary>
    public readonly struct HistoricalWorldValidationFingerprints
    {
        public HistoricalWorldValidationFingerprints(
            string playerStatisticsHash,
            string teamStatisticsHash,
            string standingsHash,
            string awardsHash,
            string identityHash,
            string historyHash)
        {
            PlayerStatisticsHash = playerStatisticsHash;
            TeamStatisticsHash = teamStatisticsHash;
            StandingsHash = standingsHash;
            AwardsHash = awardsHash;
            IdentityHash = identityHash;
            HistoryHash = historyHash;
        }

        public string PlayerStatisticsHash { get; }
        public string TeamStatisticsHash { get; }
        public string StandingsHash { get; }
        public string AwardsHash { get; }
        public string IdentityHash { get; }
        public string HistoryHash { get; }
    }

    /// <summary>ReplacementGenerated의 선수풀 비중과 Simulation 수상 점유율을 비교한다.</summary>
    public readonly struct HistoricalReplacementAwardMetrics
    {
        public HistoricalReplacementAwardMetrics(
            int playerSeasonCount,
            int replacementPlayerSeasonCount,
            int allStarCount,
            int replacementAllStarCount,
            int goldenGloveCount,
            int replacementGoldenGloveCount,
            int mvpCount,
            int replacementMvpCount)
        {
            PlayerSeasonCount = playerSeasonCount;
            ReplacementPlayerSeasonCount = replacementPlayerSeasonCount;
            AllStarCount = allStarCount;
            ReplacementAllStarCount = replacementAllStarCount;
            GoldenGloveCount = goldenGloveCount;
            ReplacementGoldenGloveCount = replacementGoldenGloveCount;
            MvpCount = mvpCount;
            ReplacementMvpCount = replacementMvpCount;
        }

        public int PlayerSeasonCount { get; }
        public int ReplacementPlayerSeasonCount { get; }
        public int AllStarCount { get; }
        public int ReplacementAllStarCount { get; }
        public int GoldenGloveCount { get; }
        public int ReplacementGoldenGloveCount { get; }
        public int MvpCount { get; }
        public int ReplacementMvpCount { get; }
        public double ReplacementPlayerSeasonShare => Divide(ReplacementPlayerSeasonCount, PlayerSeasonCount);
        public double ReplacementAllStarShare => Divide(ReplacementAllStarCount, AllStarCount);
        public double ReplacementGoldenGloveShare => Divide(ReplacementGoldenGloveCount, GoldenGloveCount);
        public double ReplacementMvpShare => Divide(ReplacementMvpCount, MvpCount);

        private static double Divide(int numerator, int denominator)
        {
            return denominator == 0 ? 0d : (double)numerator / denominator;
        }
    }

    /// <summary>Statistics, Award, Composite roster의 순서까지 포함한 Stable FNV-1a Hash를 만든다.</summary>
    public static class HistoricalWorldResultHasher
    {
        private const ulong FnvOffset = 14695981039346656037UL;

        public static string Compute(HistoricalWorldRuntimeContent world)
        {
            if (world == null)
                throw new ArgumentNullException(nameof(world));
            ulong hash = FnvOffset;
            Add(ref hash, (int)world.WorldHistory.RecordMode);
            Add(ref hash, world.WorldHistory.WorldHistorySeed);
            for (int index = 0; index < world.WorldHistory.Statistics.Count; index++)
                Add(ref hash, world.WorldHistory.Statistics[index]);
            for (int index = 0; index < world.WorldHistory.TeamStatistics.Count; index++)
                Add(ref hash, world.WorldHistory.TeamStatistics[index]);
            for (int index = 0; index < world.WorldHistory.Standings.Count; index++)
                Add(ref hash, world.WorldHistory.Standings[index]);
            for (int index = 0; index < world.WorldHistory.PostseasonResults.Count; index++)
                Add(ref hash, world.WorldHistory.PostseasonResults[index]);
            for (int index = 0; index < world.WorldAwardRecord.Entries.Count; index++)
                Add(ref hash, world.WorldAwardRecord.Entries[index]);
            for (int setIndex = 0; setIndex < world.SpecialCompositeTeams.Count; setIndex++)
            {
                SpecialCompositeTeamSet set = world.SpecialCompositeTeams[setIndex];
                for (int teamIndex = 0; teamIndex < set.Teams.Count; teamIndex++)
                {
                    SpecialCompositeTeamDefinition team = set.Teams[teamIndex];
                    Add(ref hash, (int)team.TeamType);
                    Add(ref hash, team.OriginYear);
                    for (int rosterIndex = 0; rosterIndex < team.Roster.Count; rosterIndex++)
                    {
                        Add(ref hash, team.Roster[rosterIndex].CardId);
                        Add(ref hash, team.Roster[rosterIndex].PlayerSeasonId);
                        Add(ref hash, (int)team.Roster[rosterIndex].Role);
                    }
                }
            }
            return hash.ToString("x16");
        }

        public static HistoricalWorldValidationFingerprints ComputeFingerprints(
            HistoricalWorldRuntimeContent world)
        {
            if (world == null)
                throw new ArgumentNullException(nameof(world));
            return new HistoricalWorldValidationFingerprints(
                ComputePlayerStatistics(world.WorldHistory),
                ComputeTeamStatistics(world.WorldHistory),
                ComputeStandings(world.WorldHistory),
                ComputeAwards(world.WorldAwardRecord),
                ComputeIdentity(world.IdentityRegistry),
                ComputeHistory(world.WorldHistory));
        }

        /// <summary>World Seed를 포함하지 않아 실제 Simulation 산출물 변화만 비교한다.</summary>
        public static string ComputeHistory(WorldHistorySnapshot history)
        {
            if (history == null)
                throw new ArgumentNullException(nameof(history));
            ulong hash = Start("history");
            Add(ref hash, (int)history.RecordMode);
            Add(ref hash, ComputePlayerStatistics(history));
            Add(ref hash, ComputeTeamStatistics(history));
            Add(ref hash, ComputeStandings(history));
            Add(ref hash, ComputeAwards(history.Awards));
            return hash.ToString("x16");
        }

        public static string ComputePlayerStatistics(WorldHistorySnapshot history)
        {
            if (history == null)
                throw new ArgumentNullException(nameof(history));
            ulong hash = Start("player-statistics");
            Add(ref hash, history.Statistics.Count);
            var rows = new SeasonStatistics[history.Statistics.Count];
            for (int index = 0; index < rows.Length; index++)
                rows[index] = history.Statistics[index];
            Array.Sort(rows, CompareStatistics);
            for (int index = 0; index < rows.Length; index++)
                Add(ref hash, rows[index]);
            return hash.ToString("x16");
        }

        public static string ComputeTeamStatistics(WorldHistorySnapshot history)
        {
            if (history == null)
                throw new ArgumentNullException(nameof(history));
            ulong hash = Start("team-statistics");
            Add(ref hash, history.TeamStatistics.Count);
            var rows = new TeamSeasonStatistics[history.TeamStatistics.Count];
            for (int index = 0; index < rows.Length; index++)
                rows[index] = history.TeamStatistics[index];
            Array.Sort(rows, CompareTeamStatistics);
            for (int index = 0; index < rows.Length; index++)
                Add(ref hash, rows[index]);
            return hash.ToString("x16");
        }

        public static string ComputeStandings(WorldHistorySnapshot history)
        {
            if (history == null)
                throw new ArgumentNullException(nameof(history));
            ulong hash = Start("standings-postseason");
            Add(ref hash, history.Standings.Count);
            var standings = new HistoricalStandingEntry[history.Standings.Count];
            for (int index = 0; index < standings.Length; index++)
                standings[index] = history.Standings[index];
            Array.Sort(standings, CompareStandings);
            for (int index = 0; index < standings.Length; index++)
                Add(ref hash, standings[index]);
            Add(ref hash, history.PostseasonResults.Count);
            var postseason = new HistoricalPostseasonResult[history.PostseasonResults.Count];
            for (int index = 0; index < postseason.Length; index++)
                postseason[index] = history.PostseasonResults[index];
            Array.Sort(postseason, (left, right) => left.SeasonYear.CompareTo(right.SeasonYear));
            for (int index = 0; index < postseason.Length; index++)
                Add(ref hash, postseason[index]);
            return hash.ToString("x16");
        }

        public static string ComputeAwards(WorldAwardRecord awards)
        {
            if (awards == null)
                throw new ArgumentNullException(nameof(awards));
            ulong hash = Start("awards");
            Add(ref hash, awards.Entries.Count);
            var rows = new WorldAwardEntry[awards.Entries.Count];
            for (int index = 0; index < rows.Length; index++)
                rows[index] = awards.Entries[index];
            Array.Sort(rows, CompareAwards);
            for (int index = 0; index < rows.Length; index++)
                Add(ref hash, rows[index]);
            return hash.ToString("x16");
        }

        public static string ComputeIdentity(WorldIdentityRegistry identities)
        {
            if (identities == null)
                throw new ArgumentNullException(nameof(identities));
            ulong hash = Start("identity");
            Add(ref hash, identities.IdentityGeneratorVersion);
            Add(ref hash, identities.PlayerIdentities.Count);
            for (int index = 0; index < identities.PlayerIdentities.Count; index++)
            {
                WorldPlayerIdentity identity = identities.PlayerIdentities[index];
                Add(ref hash, identity.PlayerPersonId);
                Add(ref hash, identity.DisplayName);
            }
            Add(ref hash, identities.FranchiseIdentities.Count);
            for (int index = 0; index < identities.FranchiseIdentities.Count; index++)
            {
                WorldFranchiseIdentity identity = identities.FranchiseIdentities[index];
                Add(ref hash, identity.FranchiseId);
                Add(ref hash, identity.DisplayName);
            }
            return hash.ToString("x16");
        }

        private static ulong Start(string domain)
        {
            ulong hash = FnvOffset;
            Add(ref hash, domain);
            return hash;
        }

        private static int CompareStatistics(SeasonStatistics left, SeasonStatistics right)
        {
            int comparison = left.SeasonYear.CompareTo(right.SeasonYear);
            if (comparison != 0) return comparison;
            comparison = StringComparer.Ordinal.Compare(left.PlayerSeasonId, right.PlayerSeasonId);
            if (comparison != 0) return comparison;
            comparison = left.IsPostseason.CompareTo(right.IsPostseason);
            if (comparison != 0) return comparison;
            comparison = left.IsAllStarGame.CompareTo(right.IsAllStarGame);
            if (comparison != 0) return comparison;
            return left.IsFirstHalf.CompareTo(right.IsFirstHalf);
        }

        private static int CompareTeamStatistics(TeamSeasonStatistics left, TeamSeasonStatistics right)
        {
            int comparison = left.SeasonYear.CompareTo(right.SeasonYear);
            return comparison != 0
                ? comparison
                : StringComparer.Ordinal.Compare(left.TeamSeasonKey, right.TeamSeasonKey);
        }

        private static int CompareStandings(HistoricalStandingEntry left, HistoricalStandingEntry right)
        {
            int comparison = left.SeasonYear.CompareTo(right.SeasonYear);
            if (comparison != 0) return comparison;
            comparison = left.Rank.CompareTo(right.Rank);
            return comparison != 0
                ? comparison
                : StringComparer.Ordinal.Compare(left.TeamSeasonKey, right.TeamSeasonKey);
        }

        private static int CompareAwards(WorldAwardEntry left, WorldAwardEntry right)
        {
            int comparison = left.SeasonYear.CompareTo(right.SeasonYear);
            if (comparison != 0) return comparison;
            comparison = left.AwardType.CompareTo(right.AwardType);
            if (comparison != 0) return comparison;
            comparison = left.Position.CompareTo(right.Position);
            return comparison != 0
                ? comparison
                : StringComparer.Ordinal.Compare(left.PlayerSeasonId, right.PlayerSeasonId);
        }

        private static void Add(ref ulong hash, SeasonStatistics value)
        {
            Add(ref hash, value.PlayerSeasonId);
            Add(ref hash, value.TeamSeasonKey);
            Add(ref hash, value.SeasonYear);
            Add(ref hash, (int)value.Position);
            Add(ref hash, value.PlateAppearances);
            Add(ref hash, value.Hits);
            Add(ref hash, value.HomeRuns);
            Add(ref hash, value.Walks);
            Add(ref hash, value.Strikeouts);
            Add(ref hash, value.StolenBases);
            Add(ref hash, value.PitchingOuts);
            Add(ref hash, value.EarnedRuns);
            Add(ref hash, value.PitchingStrikeouts);
            Add(ref hash, value.DefensiveChances);
            Add(ref hash, value.DefensiveOutsAboveAverage);
            Add(ref hash, value.FieldingErrors);
            Add(ref hash, value.IsFirstHalf ? 1 : 0);
            Add(ref hash, value.IsPostseason ? 1 : 0);
            Add(ref hash, value.IsAllStarGame ? 1 : 0);
        }

        private static void Add(ref ulong hash, WorldAwardEntry value)
        {
            Add(ref hash, value.SeasonYear);
            Add(ref hash, (int)value.AwardType);
            Add(ref hash, value.PlayerSeasonId);
            Add(ref hash, (int)value.Position);
        }

        private static void Add(ref ulong hash, TeamSeasonStatistics value)
        {
            Add(ref hash, value.TeamSeasonKey);
            Add(ref hash, value.SeasonYear);
            Add(ref hash, value.Games);
            Add(ref hash, value.Wins);
            Add(ref hash, value.Losses);
            Add(ref hash, value.Ties);
            Add(ref hash, value.RunsScored);
            Add(ref hash, value.RunsAllowed);
            Add(ref hash, value.AtBats);
            Add(ref hash, value.Hits);
            Add(ref hash, value.PitchingOuts);
            Add(ref hash, value.EarnedRuns);
            Add(ref hash, value.HitsAllowed);
            Add(ref hash, value.WalksAllowed);
        }

        private static void Add(ref ulong hash, HistoricalStandingEntry value)
        {
            Add(ref hash, value.SeasonYear);
            Add(ref hash, value.Rank);
            Add(ref hash, value.TeamSeasonKey);
        }

        private static void Add(ref ulong hash, HistoricalPostseasonResult value)
        {
            Add(ref hash, value.SeasonYear);
            for (int index = 0; index < value.QualifiedTeamSeasonKeys.Count; index++)
                Add(ref hash, value.QualifiedTeamSeasonKeys[index]);
            Add(ref hash, value.ChampionTeamSeasonKey);
        }

        private static void Add(ref ulong hash, string value)
        {
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                hash ^= (byte)character;
                hash *= 1099511628211UL;
                hash ^= (byte)(character >> 8);
                hash *= 1099511628211UL;
            }
            hash ^= 0xFF;
            hash *= 1099511628211UL;
        }

        private static void Add(ref ulong hash, int value) => Add(ref hash, unchecked((ulong)(uint)value));

        private static void Add(ref ulong hash, ulong value)
        {
            for (int shift = 0; shift < 64; shift += 8)
            {
                hash ^= (byte)(value >> shift);
                hash *= 1099511628211UL;
            }
        }
    }
}
