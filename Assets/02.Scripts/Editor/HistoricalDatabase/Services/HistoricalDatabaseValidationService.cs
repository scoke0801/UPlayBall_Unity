using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Baseball.Editor.HistoricalDatabase
{
    /// <summary>Historical Archive 검증 결과의 심각도다.</summary>
    public enum HistoricalValidationSeverity
    {
        Pass,
        Warning,
        Error
    }

    /// <summary>검증 결과에서 바로 이동할 수 있는 대상 종류다.</summary>
    public enum HistoricalNavigationKind
    {
        None,
        Archive,
        Player,
        Team,
        Award,
        File
    }

    /// <summary>Archive 검증 한 건의 상태와 탐색 대상을 보관한다.</summary>
    public sealed class HistoricalValidationIssue
    {
        public HistoricalValidationIssue(
            HistoricalValidationSeverity severity,
            string category,
            int? year,
            string entityId,
            string message,
            HistoricalNavigationKind navigationKind = HistoricalNavigationKind.None,
            string navigationId = "")
        {
            Severity = severity;
            Category = category ?? string.Empty;
            Year = year;
            EntityId = entityId ?? string.Empty;
            Message = message ?? string.Empty;
            NavigationKind = navigationKind;
            NavigationId = navigationId ?? string.Empty;
        }

        public HistoricalValidationSeverity Severity { get; }
        public string Category { get; }
        public int? Year { get; }
        public string EntityId { get; }
        public string Entity => EntityId;
        public string Message { get; }
        public HistoricalNavigationKind NavigationKind { get; }
        public HistoricalNavigationKind EntityKind => NavigationKind;
        public string NavigationId { get; }
    }

    /// <summary>Archive 전체 검증의 집계와 안정된 순서의 결과 목록이다.</summary>
    public sealed class HistoricalDatabaseValidationReport
    {
        /// <summary>상세 결과 목록에서 심각도별 건수를 계산해 보고서를 만든다.</summary>
        public HistoricalDatabaseValidationReport(
            IReadOnlyList<HistoricalValidationIssue> issues,
            TimeSpan elapsed)
            : this(issues, Count(issues, HistoricalValidationSeverity.Pass),
                Count(issues, HistoricalValidationSeverity.Warning),
                Count(issues, HistoricalValidationSeverity.Error), elapsed)
        {
        }

        internal HistoricalDatabaseValidationReport(
            IReadOnlyList<HistoricalValidationIssue> issues,
            int passCount,
            int warningCount,
            int errorCount,
            TimeSpan elapsed)
        {
            if (issues == null)
                throw new ArgumentNullException(nameof(issues));

            var copy = new HistoricalValidationIssue[issues.Count];
            for (int index = 0; index < issues.Count; index++)
            {
                HistoricalValidationIssue issue = issues[index]
                    ?? throw new ArgumentException("null 검증 결과가 있습니다.", nameof(issues));
                copy[index] = issue;
            }

            Issues = copy;
            PassCount = passCount;
            WarningCount = warningCount;
            ErrorCount = errorCount;
            DetailedPassCount = Count(copy, HistoricalValidationSeverity.Pass);
            Elapsed = elapsed;
        }

        public IReadOnlyList<HistoricalValidationIssue> Issues { get; }
        public int PassCount { get; }
        public int WarningCount { get; }
        public int ErrorCount { get; }
        public int DetailedPassCount { get; }
        public bool ArePassDetailsTruncated => DetailedPassCount < PassCount;
        public bool IsValid => ErrorCount == 0;
        public TimeSpan Elapsed { get; }

        private static int Count(
            IReadOnlyList<HistoricalValidationIssue> issues,
            HistoricalValidationSeverity severity)
        {
            if (issues == null)
                throw new ArgumentNullException(nameof(issues));
            int count = 0;
            for (int index = 0; index < issues.Count; index++)
                if (issues[index]?.Severity == severity) count++;
            return count;
        }
    }

    /// <summary>TeamSeason 한 건의 로스터 지표와 검증 결과를 보관한다.</summary>
    public sealed class HistoricalTeamValidationResult
    {
        public HistoricalTeamValidationResult(
            HistoricalTeamSeason team,
            int totalCount,
            int hitterCount,
            int pitcherCount,
            int startingHitterCount,
            int benchHitterCount,
            int startingPitcherCount,
            int bullpenPitcherCount,
            int setupPitcherCount,
            int closerPitcherCount,
            int foreignPlayerCount,
            int duplicatePersonCount,
            IReadOnlyList<HistoricalValidationIssue> issues)
        {
            Team = team ?? throw new ArgumentNullException(nameof(team));
            TotalCount = totalCount;
            HitterCount = hitterCount;
            PitcherCount = pitcherCount;
            StartingHitterCount = startingHitterCount;
            BenchHitterCount = benchHitterCount;
            StartingPitcherCount = startingPitcherCount;
            BullpenPitcherCount = bullpenPitcherCount;
            SetupPitcherCount = setupPitcherCount;
            CloserPitcherCount = closerPitcherCount;
            ForeignPlayerCount = foreignPlayerCount;
            DuplicatePersonCount = duplicatePersonCount;
            Issues = issues ?? throw new ArgumentNullException(nameof(issues));
        }

        public HistoricalTeamSeason Team { get; }
        public int TotalCount { get; }
        public int HitterCount { get; }
        public int PitcherCount { get; }
        public int StartingHitterCount { get; }
        public int BenchHitterCount { get; }
        public int StartingPitcherCount { get; }
        public int BullpenPitcherCount { get; }
        public int SetupPitcherCount { get; }
        public int CloserPitcherCount { get; }
        public int ForeignPlayerCount { get; }
        public int DuplicatePersonCount { get; }
        public IReadOnlyList<HistoricalValidationIssue> Issues { get; }

        public bool IsValid
        {
            get
            {
                for (int index = 0; index < Issues.Count; index++)
                    if (Issues[index].Severity == HistoricalValidationSeverity.Error) return false;
                return true;
            }
        }
    }

    /// <summary>Historical Archive의 파일 무결성·참조·Baked 규칙을 읽기 전용으로 검증한다.</summary>
    public sealed partial class HistoricalDatabaseValidationService
    {
        private const int ExpectedAssetFormatVersion = 1;
        private const int ExpectedAbilityCount = 12;
        private const int RequiredTeamCountPerYear = 10;

        /// <summary>Archive 전체를 검증하고 UI 탐색 정보가 포함된 보고서를 반환한다.</summary>
        public HistoricalDatabaseValidationReport Validate(HistoricalArchiveData archive)
        {
            if (archive == null)
                throw new ArgumentNullException(nameof(archive));

            var stopwatch = Stopwatch.StartNew();
            var collector = new ValidationCollector();
            ValidateManifestAndFiles(archive, collector);
            if (IsOriginalSourceArchive(archive))
            {
                ValidateOriginalSourceArchive(archive, collector);
            }
            else
            {
                ValidatePersonsAndPlayers(archive, collector);
                ValidateRecordsAndAwards(archive, collector);
                ValidateCards(archive, collector);
                ValidateTeams(archive, collector);
            }
            stopwatch.Stop();
            return new HistoricalDatabaseValidationReport(
                collector.Issues,
                collector.PassCount,
                collector.WarningCount,
                collector.ErrorCount,
                stopwatch.Elapsed);
        }

        private static bool IsOriginalSourceArchive(HistoricalArchiveData archive)
        {
            return string.Equals(
                archive?.Manifest?.SourceManifest?.NameDataPolicy,
                "editor-original-source-v2",
                StringComparison.Ordinal);
        }

        private static void ValidateOriginalSourceArchive(
            HistoricalArchiveData archive,
            ValidationCollector collector)
        {
            var personIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < archive.Persons.Count; index++)
            {
                HistoricalPlayerPerson person = archive.Persons[index];
                string personId = person?.PlayerPersonId ?? string.Empty;
                collector.Check(
                    person != null && IsStableHexId(personId, "PERSON_") && personIds.Add(personId),
                    "원본 선수",
                    null,
                    personId,
                    "원본 선수 ID가 고유하고 유효합니다.",
                    "원본 선수 ID가 비어 있거나 중복되었습니다.",
                    HistoricalNavigationKind.Player,
                    personId);
                if (person == null)
                    continue;
                collector.Check(
                    !string.IsNullOrWhiteSpace(person.OriginalName) && string.IsNullOrWhiteSpace(person.FictionalName),
                    "이름 분리",
                    null,
                    personId,
                    "실제 이름만 존재하며 Runtime 가명이 없습니다.",
                    "Editor 원본 선수에 실제 이름이 없거나 Runtime 가명이 섞였습니다.",
                    HistoricalNavigationKind.Player,
                    personId);
                collector.Check(
                    person.CareerStartYear > 0 && person.CareerEndYear >= person.CareerStartYear,
                    "원본 선수",
                    person.CareerStartYear,
                    personId,
                    "원본 커리어 시즌 범위가 유효합니다.",
                    "원본 커리어 시즌 범위가 유효하지 않습니다.",
                    HistoricalNavigationKind.Player,
                    personId);
            }

            var seasonIds = new HashSet<string>(StringComparer.Ordinal);
            var recordIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < archive.Records.Count; index++)
            {
                HistoricalSeasonRecord record = archive.Records[index];
                if (record != null)
                    recordIds.Add(record.PlayerSeasonId);
            }
            for (int index = 0; index < archive.PlayerRows.Count; index++)
            {
                HistoricalPlayerRow row = archive.PlayerRows[index];
                string seasonId = row?.PlayerSeasonId ?? string.Empty;
                bool hasPerson = row?.Person != null;
                bool oneToOneName = hasPerson && row.SourceReferenceNames.Count == 1 &&
                                    string.Equals(row.SourceReferenceNames[0], row.Name, StringComparison.Ordinal);
                collector.Check(
                    row != null && IsStableHexId(seasonId, "SEASON_") && seasonIds.Add(seasonId),
                    "원본 시즌",
                    row?.OriginYear,
                    seasonId,
                    "원본 선수 시즌 ID가 고유하고 유효합니다.",
                    "원본 선수 시즌 ID가 비어 있거나 중복되었습니다.",
                    HistoricalNavigationKind.Player,
                    seasonId);
                if (row == null)
                    continue;
                collector.Check(
                    hasPerson && oneToOneName && recordIds.Contains(seasonId) && row.IsOriginalSource,
                    "원본 1:1 연결",
                    row.OriginYear,
                    seasonId,
                    "실제 선수명·시즌·기록이 1:1로 연결됩니다.",
                    "실제 선수명·시즌·기록의 1:1 연결이 깨졌습니다.",
                    HistoricalNavigationKind.Player,
                    seasonId);
                bool seasonRecordMatches = row.Record != null &&
                                           row.Record.SeasonYear == row.OriginYear &&
                                           string.Equals(row.Record.PlayerSeasonId, seasonId, StringComparison.Ordinal);
                collector.Check(
                    seasonRecordMatches,
                    "SEASON_RECORD_CROSS_YEAR_REFERENCE",
                    row.OriginYear,
                    seasonId,
                    "PlayerSeason과 Original Record의 SeasonYear가 일치합니다.",
                    $"PlayerSeason/Record 연도 또는 ID가 다릅니다: season={row.OriginYear}, record={row.Record?.SeasonYear}",
                    HistoricalNavigationKind.Player,
                    seasonId);
                collector.Check(
                    row.BaseAttributes.Length == ExpectedAbilityCount && row.TrainingCeiling.Length == 0,
                    "파생 데이터",
                    row.OriginYear,
                    seasonId,
                    "환산 능력치는 존재하고 원본에 없는 훈련 상한은 비어 있습니다.",
                    "환산 능력치 길이가 잘못되었거나 원본에 없는 훈련 상한이 생성되었습니다.",
                    HistoricalNavigationKind.Player,
                    seasonId);
                ValidateOriginalDerivationTrace(row, collector);
            }

            ValidateCards(archive, collector);
            ValidateOriginalSourceTeams(archive, collector);
            ValidateOriginalSourceAwards(archive, collector);
        }

        private static void ValidateOriginalDerivationTrace(
            HistoricalPlayerRow row,
            ValidationCollector collector)
        {
            if (row.Season.AbilityDerivationTrace.Count > 0)
            {
                bool abilityTraceMatches = true;
                for (int index = 0; index < row.Season.AbilityDerivationTrace.Count; index++)
                {
                    HistoricalAbilityDerivationTrace trace = row.Season.AbilityDerivationTrace[index];
                    if (trace == null || trace.PlayerSeasonId != row.PlayerSeasonId ||
                        trace.SeasonYear != row.OriginYear || double.IsNaN(trace.CombinedZ) ||
                        double.IsInfinity(trace.CombinedZ))
                    {
                        abilityTraceMatches = false;
                        break;
                    }
                }
                collector.Check(
                    abilityTraceMatches,
                    "AbilityDerivationTrace",
                    row.OriginYear,
                    row.PlayerSeasonId,
                    "능력치 Trace가 같은 PlayerSeason/SeasonYear를 참조합니다.",
                    "능력치 Trace가 다른 PlayerSeason/SeasonYear를 참조하거나 유한하지 않은 값이 있습니다.",
                    HistoricalNavigationKind.Player,
                    row.PlayerSeasonId);
            }

            HistoricalCostDerivationTrace costTrace = row.Season.CostDerivationTrace;
            if (costTrace != null)
            {
                collector.Check(
                    costTrace.OriginYear == row.OriginYear && costTrace.Cost == row.Cost &&
                    costTrace.PopulationCount > 0 && !double.IsNaN(costTrace.Composite) &&
                    !double.IsInfinity(costTrace.Composite),
                    "CostDerivationTrace",
                    row.OriginYear,
                    row.PlayerSeasonId,
                    "Cost Trace가 OriginYear 전체 모집단과 저장 Cost에 일치합니다.",
                    "Cost Trace의 OriginYear/Population/Cost/Composite가 저장값과 일치하지 않습니다.",
                    HistoricalNavigationKind.Player,
                    row.PlayerSeasonId);
                if (costTrace.PopulationCount < 20)
                {
                    collector.Add(
                        HistoricalValidationSeverity.Warning,
                        "COST_SMALL_POPULATION",
                        row.OriginYear,
                        row.PlayerSeasonId,
                        $"Cost 백분위 모집단이 작습니다: {costTrace.PopulationCount}",
                        HistoricalNavigationKind.Player,
                        row.PlayerSeasonId);
                }
            }

            HistoricalPositionRoleDerivationTrace roleTrace = row.Season.PositionRoleDerivationTrace;
            if (roleTrace == null)
                return;
            bool selectedRoleMatches = row.IsPitcher
                ? string.Equals(roleTrace.SelectedNaturalPitcherRole, row.PitcherRole, StringComparison.Ordinal)
                : string.Equals(roleTrace.SelectedNaturalPosition, row.Position, StringComparison.Ordinal);
            collector.Check(
                selectedRoleMatches,
                "PITCHER_ROLE_SOURCE_MISMATCH",
                row.OriginYear,
                row.PlayerSeasonId,
                "Natural Position/PitcherRole이 시즌 파생 Trace와 일치합니다.",
                "저장된 Natural Position/PitcherRole이 시즌 파생 Trace와 다릅니다.",
                HistoricalNavigationKind.Player,
                row.PlayerSeasonId);
            for (int index = 0; index < roleTrace.Warnings.Count; index++)
            {
                HistoricalDerivationWarningTrace warning = roleTrace.Warnings[index];
                collector.Add(
                    HistoricalValidationSeverity.Warning,
                    warning.Code,
                    row.OriginYear,
                    row.PlayerSeasonId,
                    warning.Message,
                    HistoricalNavigationKind.Player,
                    row.PlayerSeasonId);
            }
        }

        private static void ValidateOriginalSourceTeams(
            HistoricalArchiveData archive,
            ValidationCollector collector)
        {
            var teamKeys = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < archive.Teams.Count; index++)
            {
                HistoricalTeamSeason team = archive.Teams[index];
                string teamKey = team?.TeamSeasonKey ?? string.Empty;
                bool coreIsSubset = team != null &&
                                    new HashSet<string>(team.AllNormalCardIds, StringComparer.Ordinal)
                                        .IsSupersetOf(team.Core25CardIds);
                collector.Check(
                    team != null && !string.IsNullOrWhiteSpace(team.FranchiseId) && teamKeys.Add(teamKey) &&
                    team.Core25CardIds.Length <= 25 && coreIsSubset,
                    "원본 팀",
                    team?.OriginYear,
                    teamKey,
                    "원본 팀 Pool과 대표 25인 참조가 유효합니다.",
                    "원본 팀 Pool 또는 대표 25인 참조가 유효하지 않습니다.",
                    HistoricalNavigationKind.Team,
                    teamKey);
                if (team != null)
                {
                    if (CanSourcePoolSatisfyCoreComposition(archive, team))
                    {
                        TeamMetrics metrics = CollectCoreMetrics(
                            archive,
                            team,
                            team.Core25CardIds,
                            collector);
                        ValidateCoreMetrics(team, metrics, collector);
                    }
                    ValidateRosterTrace(team, collector);
                }
            }
            ValidateNormalCardPoolBackReferences(archive, collector);
        }

        private static void ValidateOriginalSourceAwards(
            HistoricalArchiveData archive,
            ValidationCollector collector)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < archive.Awards.Count; index++)
            {
                HistoricalAwardRecord award = archive.Awards[index];
                string key = award == null
                    ? string.Empty
                    : $"{award.SeasonYear}:{award.AwardType}:{award.Position}:{award.PlayerSeasonId}";
                bool valid = award != null && keys.Add(key) && ValidAwardTypes.Contains(award.AwardType) &&
                             archive.PlayersBySeasonId.ContainsKey(award.PlayerSeasonId);
                collector.Check(
                    valid,
                    "원본 수상",
                    award?.SeasonYear,
                    key,
                    "원본 수상 기록이 선수 시즌과 연결됩니다.",
                    "원본 수상 기록이 중복되었거나 선수 시즌과 연결되지 않습니다.",
                    HistoricalNavigationKind.Player,
                    award?.PlayerSeasonId ?? string.Empty);
            }
        }

        private sealed class ValidationCollector
        {
            private const int MaximumDetailedPassCount = 256;
            private readonly List<HistoricalValidationIssue> _issues = new List<HistoricalValidationIssue>();

            public IReadOnlyList<HistoricalValidationIssue> Issues => _issues;
            public int PassCount { get; private set; }
            public int WarningCount { get; private set; }
            public int ErrorCount { get; private set; }

            public void Add(
                HistoricalValidationSeverity severity,
                string category,
                int? year,
                string entityId,
                string message,
                HistoricalNavigationKind navigationKind = HistoricalNavigationKind.None,
                string navigationId = "")
            {
                switch (severity)
                {
                    case HistoricalValidationSeverity.Pass:
                        PassCount++;
                        break;
                    case HistoricalValidationSeverity.Warning:
                        WarningCount++;
                        break;
                    case HistoricalValidationSeverity.Error:
                        ErrorCount++;
                        break;
                }

                if (severity == HistoricalValidationSeverity.Pass && PassCount > MaximumDetailedPassCount)
                    return;
                _issues.Add(new HistoricalValidationIssue(
                    severity,
                    category,
                    year,
                    entityId,
                    message,
                    navigationKind,
                    navigationId));
            }

            public void Check(
                bool condition,
                string category,
                int? year,
                string entityId,
                string passMessage,
                string errorMessage,
                HistoricalNavigationKind navigationKind = HistoricalNavigationKind.None,
                string navigationId = "")
            {
                Add(
                    condition ? HistoricalValidationSeverity.Pass : HistoricalValidationSeverity.Error,
                    category,
                    year,
                    entityId,
                    condition ? passMessage : errorMessage,
                    navigationKind,
                    navigationId);
            }
        }
    }
}
