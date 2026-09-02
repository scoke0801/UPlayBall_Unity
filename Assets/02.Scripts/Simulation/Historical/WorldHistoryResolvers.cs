using System;
using System.Collections.Generic;
using Baseball.Core.Historical;

namespace Baseball.Simulation.Historical
{
    /// <summary>기존 DetailedMatchEngine 기반 과거 시즌 실행 결과를 Historical 초기화에 공급한다.</summary>
    public interface IHistoricalSeasonSimulation
    {
        IReadOnlyList<SeasonStatistics> Simulate(
            ulong worldHistorySeed,
            IReadOnlyList<TeamSeasonDefinition> regularFranchiseTeams);
    }

    /// <summary>Statistics만 받아 World Award를 확정하는 공통 Resolver 계약이다.</summary>
    public interface ISeasonAwardResolver
    {
        WorldAwardRecord Resolve(IReadOnlyList<SeasonStatistics> statistics);
    }

    /// <summary>월드 기록 초기화에 필요한 두 경로의 입력을 한 번의 요청으로 전달한다.</summary>
    public sealed class WorldHistoryInitializationRequest
    {
        public WorldHistoryInitializationRequest(
            WorldRecordMode recordMode,
            ulong worldHistorySeed,
            WorldHistorySnapshot existingSnapshot = null,
            IReadOnlyList<TeamSeasonDefinition> regularFranchiseTeams = null,
            IReadOnlyList<OriginalSeasonRecordDefinition> originalSeasonRecords = null,
            IReadOnlyList<OriginalAwardRecordDefinition> originalAwardRecords = null)
        {
            RecordMode = recordMode;
            WorldHistorySeed = worldHistorySeed;
            ExistingSnapshot = existingSnapshot;
            RegularFranchiseTeams = regularFranchiseTeams;
            OriginalSeasonRecords = originalSeasonRecords;
            OriginalAwardRecords = originalAwardRecords;
        }

        public WorldRecordMode RecordMode { get; }
        public ulong WorldHistorySeed { get; }
        public WorldHistorySnapshot ExistingSnapshot { get; }
        public IReadOnlyList<TeamSeasonDefinition> RegularFranchiseTeams { get; }
        public IReadOnlyList<OriginalSeasonRecordDefinition> OriginalSeasonRecords { get; }
        public IReadOnlyList<OriginalAwardRecordDefinition> OriginalAwardRecords { get; }
    }

    /// <summary>Runtime-safe 고유 기록을 공통 WorldHistorySnapshot으로 복사한다.</summary>
    public sealed class OriginalHistoryLoader
    {
        public WorldHistorySnapshot Load(
            ulong worldHistorySeed,
            IReadOnlyList<OriginalSeasonRecordDefinition> seasonRecords,
            IReadOnlyList<OriginalAwardRecordDefinition> awardRecords)
        {
            if (seasonRecords == null)
                throw new ArgumentNullException(nameof(seasonRecords));
            if (awardRecords == null)
                throw new ArgumentNullException(nameof(awardRecords));

            var statistics = new SeasonStatistics[seasonRecords.Count];
            for (int index = 0; index < seasonRecords.Count; index++)
            {
                OriginalSeasonRecordDefinition record = seasonRecords[index]
                    ?? throw new ArgumentException("null 고유 시즌 기록이 있습니다.", nameof(seasonRecords));
                statistics[index] = record.Statistics;
            }

            var awards = new WorldAwardEntry[awardRecords.Count];
            for (int index = 0; index < awardRecords.Count; index++)
            {
                OriginalAwardRecordDefinition record = awardRecords[index]
                    ?? throw new ArgumentException("null 고유 수상 기록이 있습니다.", nameof(awardRecords));
                awards[index] = record.Award;
            }

            return new WorldHistorySnapshot(
                WorldRecordMode.OriginalHistory,
                worldHistorySeed,
                statistics,
                new WorldAwardRecord(awards));
        }
    }

    /// <summary>한 모드의 Provider만 실행하고 저장된 Snapshot이 있으면 과거를 재실행하지 않는다.</summary>
    public sealed class WorldHistoryInitializer
    {
        private readonly IHistoricalSeasonSimulation _historicalSimulation;
        private readonly ISeasonAwardResolver _awardResolver;
        private readonly OriginalHistoryLoader _originalHistoryLoader;

        public WorldHistoryInitializer(
            IHistoricalSeasonSimulation historicalSimulation,
            ISeasonAwardResolver awardResolver,
            OriginalHistoryLoader originalHistoryLoader)
        {
            _historicalSimulation = historicalSimulation
                ?? throw new ArgumentNullException(nameof(historicalSimulation));
            _awardResolver = awardResolver ?? throw new ArgumentNullException(nameof(awardResolver));
            _originalHistoryLoader = originalHistoryLoader
                ?? throw new ArgumentNullException(nameof(originalHistoryLoader));
        }

        public WorldHistorySnapshot Initialize(WorldHistoryInitializationRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (request.ExistingSnapshot != null)
            {
                if (request.ExistingSnapshot.RecordMode != request.RecordMode)
                    throw new InvalidOperationException("저장된 WorldRecordMode와 초기화 요청이 다릅니다.");
                if (request.ExistingSnapshot.WorldHistorySeed != request.WorldHistorySeed)
                    throw new InvalidOperationException("저장된 WorldHistorySeed와 초기화 요청이 다릅니다.");
                return request.ExistingSnapshot;
            }

            switch (request.RecordMode)
            {
                case WorldRecordMode.OriginalHistory:
                    return _originalHistoryLoader.Load(
                        request.WorldHistorySeed,
                        request.OriginalSeasonRecords,
                        request.OriginalAwardRecords);
                case WorldRecordMode.SimulatedHistory:
                    return InitializeSimulatedHistory(request);
                default:
                    throw new ArgumentOutOfRangeException(nameof(request.RecordMode));
            }
        }

        private WorldHistorySnapshot InitializeSimulatedHistory(WorldHistoryInitializationRequest request)
        {
            ValidateRegularFranchiseTeams(request.RegularFranchiseTeams);
            IReadOnlyList<SeasonStatistics> statistics = _historicalSimulation.Simulate(
                request.WorldHistorySeed,
                request.RegularFranchiseTeams);
            if (statistics == null || statistics.Count == 0)
                throw new InvalidOperationException("Historical Simulation이 시즌 기록을 만들지 않았습니다.");
            ValidateStatisticsTeams(statistics, request.RegularFranchiseTeams);

            WorldAwardRecord awards = _awardResolver.Resolve(statistics);
            return new WorldHistorySnapshot(
                WorldRecordMode.SimulatedHistory,
                request.WorldHistorySeed,
                statistics,
                awards);
        }

        private void ValidateRegularFranchiseTeams(IReadOnlyList<TeamSeasonDefinition> teams)
        {
            if (teams == null)
                throw new ArgumentNullException(nameof(teams));
            if (teams.Count != LeagueInstance.RequiredRegularFranchiseTeamCount)
                throw new ArgumentException("Historical Simulation에는 정규 Franchise 구단만 정확히 전달해야 합니다.", nameof(teams));

            var teamSeasonKeys = new HashSet<string>(StringComparer.Ordinal);
            var franchiseIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < teams.Count; index++)
            {
                TeamSeasonDefinition team = teams[index]
                    ?? throw new ArgumentException("null TeamSeason이 있습니다.", nameof(teams));
                if (!teamSeasonKeys.Add(team.TeamSeasonKey) || !franchiseIds.Add(team.FranchiseId))
                    throw new ArgumentException("Historical Simulation의 정규 Franchise 구단은 고유해야 합니다.", nameof(teams));
            }
        }

        private static void ValidateStatisticsTeams(
            IReadOnlyList<SeasonStatistics> statistics,
            IReadOnlyList<TeamSeasonDefinition> teams)
        {
            var regularTeamKeys = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < teams.Count; index++)
                regularTeamKeys.Add(teams[index].TeamSeasonKey);

            for (int index = 0; index < statistics.Count; index++)
            {
                SeasonStatistics row = statistics[index]
                    ?? throw new InvalidOperationException("Historical Simulation에 null 기록이 있습니다.");
                if (!regularTeamKeys.Contains(row.TeamSeasonKey))
                    throw new InvalidOperationException("정규 Franchise 외 구단의 기록이 Historical Simulation 결과에 포함되었습니다.");
            }
        }
    }
}
