using System;
using System.Collections.Generic;

namespace Baseball.Editor.HistoricalDatabase
{
    public sealed partial class HistoricalDatabaseValidationService
    {
        /// <summary>TeamSeason 한 건의 Core25 구성과 모든 참조를 검증한다.</summary>
        public HistoricalTeamValidationResult ValidateTeam(
            HistoricalArchiveData archive,
            HistoricalTeamSeason team)
        {
            if (archive == null)
                throw new ArgumentNullException(nameof(archive));
            if (team == null)
                throw new ArgumentNullException(nameof(team));

            var collector = new ValidationCollector();
            TeamMetrics metrics;
            if (IsOriginalSourceArchive(archive))
            {
                ValidatePoolCardReferences(archive, team, team.AllNormalCardIds, collector);
                metrics = CollectCoreMetrics(archive, team, team.Core25CardIds, collector);
                if (CanSourcePoolSatisfyCoreComposition(archive, team))
                    ValidateCoreMetrics(team, metrics, collector);
                ValidateRosterTrace(team, collector);
            }
            else
            {
                metrics = ValidateTeamCore(archive, team, collector);
            }
            return new HistoricalTeamValidationResult(
                team,
                metrics.TotalCount,
                metrics.HitterCount,
                metrics.PitcherCount,
                metrics.StartingHitterCount,
                metrics.BenchHitterCount,
                metrics.StartingPitcherCount,
                metrics.BullpenPitcherCount,
                metrics.SetupPitcherCount,
                metrics.CloserPitcherCount,
                metrics.ForeignPlayerCount,
                metrics.DuplicatePersonCount,
                collector.Issues);
        }

        private static void ValidateTeams(HistoricalArchiveData archive, ValidationCollector collector)
        {
            var teamKeys = new HashSet<string>(StringComparer.Ordinal);
            if (archive.Teams != null)
            {
                for (int index = 0; index < archive.Teams.Count; index++)
                {
                    HistoricalTeamSeason team = archive.Teams[index];
                    if (team == null)
                    {
                        collector.Add(HistoricalValidationSeverity.Error, "TeamSeason", null, string.Empty, "null TeamSeason이 있습니다.");
                        continue;
                    }

                    collector.Check(
                        !string.IsNullOrWhiteSpace(team.TeamSeasonKey) && teamKeys.Add(team.TeamSeasonKey),
                        "Stable ID",
                        team.OriginYear,
                        team.TeamSeasonKey,
                        "TeamSeasonKey가 고유합니다.",
                        "TeamSeasonKey가 비어 있거나 중복되었습니다.",
                        HistoricalNavigationKind.Team,
                        team.TeamSeasonKey);
                    HistoricalTeamValidationResult result = new HistoricalDatabaseValidationService().ValidateTeam(archive, team);
                    for (int issueIndex = 0; issueIndex < result.Issues.Count; issueIndex++)
                    {
                        HistoricalValidationIssue issue = result.Issues[issueIndex];
                        collector.Add(
                            issue.Severity,
                            issue.Category,
                            issue.Year,
                            issue.EntityId,
                            issue.Message,
                            issue.NavigationKind,
                            issue.NavigationId);
                    }
                }
            }

            ValidateNormalCardPoolBackReferences(archive, collector);

            if (archive.Manifest?.Years == null)
                return;
            for (int index = 0; index < archive.Manifest.Years.Count; index++)
            {
                int year = archive.Manifest.Years[index].Year;
                int count = CountTeamsByYear(archive.Teams, year);
                collector.Check(
                    count == RequiredTeamCountPerYear,
                    "TeamSeason",
                    year,
                    year.ToString(),
                    $"정규 Franchise TeamSeason이 {RequiredTeamCountPerYear}개입니다.",
                    $"정규 Franchise TeamSeason은 연도마다 {RequiredTeamCountPerYear}개여야 합니다. actual={count}",
                    HistoricalNavigationKind.File,
                    $"Years/{year}.json");
            }
        }

        private static void ValidateNormalCardPoolBackReferences(
            HistoricalArchiveData archive,
            ValidationCollector collector)
        {
            var poolIdsByTeam = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            for (int index = 0; index < archive.Teams.Count; index++)
            {
                HistoricalTeamSeason team = archive.Teams[index];
                if (team == null || string.IsNullOrWhiteSpace(team.TeamSeasonKey))
                    continue;
                poolIdsByTeam[team.TeamSeasonKey] = new HashSet<string>(team.AllNormalCardIds, StringComparer.Ordinal);
            }

            for (int index = 0; index < archive.Cards.Count; index++)
            {
                HistoricalCard card = archive.Cards[index];
                if (card == null || !string.Equals(card.Edition, "Normal", StringComparison.Ordinal))
                    continue;
                if (!archive.PlayersBySeasonId.TryGetValue(card.PlayerSeasonId, out HistoricalPlayerRow player))
                    continue;

                string teamKey = player.OriginTeamSeasonKey;
                bool isIncluded = poolIdsByTeam.TryGetValue(teamKey, out HashSet<string> poolIds) &&
                                  poolIds.Contains(card.CardId);
                collector.Check(
                    isIncluded,
                    "Roster",
                    player.OriginYear,
                    card.CardId,
                    "Normal Card가 Origin TeamSeason의 전체 Pool에 포함됩니다.",
                    "Normal Card가 Origin TeamSeason의 전체 Pool에서 누락되었습니다.",
                    HistoricalNavigationKind.Team,
                    teamKey);
            }
        }

        private static TeamMetrics ValidateTeamCore(
            HistoricalArchiveData archive,
            HistoricalTeamSeason team,
            ValidationCollector collector)
        {
            string teamKey = team.TeamSeasonKey ?? string.Empty;
            int year = team.OriginYear;
            collector.Check(
                year > 0 && !string.IsNullOrWhiteSpace(team.FranchiseId),
                "Origin",
                year,
                teamKey,
                $"TeamSeason Origin {team.FranchiseId}/{year}가 유효합니다.",
                "TeamSeason의 OriginYear 또는 FranchiseId가 유효하지 않습니다.",
                HistoricalNavigationKind.Team,
                teamKey);
            string expectedTeamKey = (team.FranchiseId ?? string.Empty) + "_" + year;
            collector.Check(
                string.Equals(teamKey, expectedTeamKey, StringComparison.Ordinal),
                "Stable ID",
                year,
                teamKey,
                "TeamSeasonKey가 FranchiseId_Year 규칙과 일치합니다.",
                $"TeamSeasonKey가 Stable 규칙과 다릅니다. expected={expectedTeamKey}",
                HistoricalNavigationKind.Team,
                teamKey);

            IReadOnlyList<string> allCards = team.AllNormalCardIds ?? Array.Empty<string>();
            IReadOnlyList<string> coreCards = team.Core25CardIds ?? Array.Empty<string>();
            var allUnique = new HashSet<string>(StringComparer.Ordinal);
            bool allIdsUnique = AddAllIds(allCards, allUnique);
            collector.Check(
                allCards.Count >= 28 && allCards.Count <= 40,
                "Roster",
                year,
                teamKey,
                $"전체 Normal Pool이 권장 범위입니다: {allCards.Count}",
                $"전체 Normal Pool은 28~40장이어야 합니다. actual={allCards.Count}",
                HistoricalNavigationKind.Team,
                teamKey);
            collector.Check(
                allIdsUnique,
                "Roster",
                year,
                teamKey,
                "전체 Normal Pool CardId가 중복되지 않습니다.",
                "전체 Normal Pool에 비어 있거나 중복된 CardId가 있습니다.",
                HistoricalNavigationKind.Team,
                teamKey);

            var coreUnique = new HashSet<string>(StringComparer.Ordinal);
            bool coreIdsUnique = AddAllIds(coreCards, coreUnique);
            collector.Check(
                coreCards.Count == 25,
                "Roster",
                year,
                teamKey,
                "Core25가 정확히 25장입니다.",
                $"Core25는 정확히 25장이어야 합니다. actual={coreCards.Count}",
                HistoricalNavigationKind.Team,
                teamKey);
            collector.Check(
                coreIdsUnique,
                "Roster",
                year,
                teamKey,
                "Core25 CardId가 중복되지 않습니다.",
                "Core25에 비어 있거나 중복된 CardId가 있습니다.",
                HistoricalNavigationKind.Team,
                teamKey);
            bool coreIsSubset = true;
            for (int index = 0; index < coreCards.Count; index++)
                if (!allUnique.Contains(coreCards[index] ?? string.Empty)) coreIsSubset = false;
            collector.Check(
                coreIsSubset,
                "Roster",
                year,
                teamKey,
                "Core25가 전체 Normal Pool의 부분집합입니다.",
                "Core25에 전체 Normal Pool에 없는 CardId가 있습니다.",
                HistoricalNavigationKind.Team,
                teamKey);

            ValidatePoolCardReferences(archive, team, allCards, collector);
            TeamMetrics metrics = CollectCoreMetrics(archive, team, coreCards, collector);
            ValidateCoreMetrics(team, metrics, collector);
            return metrics;
        }

        private static void ValidatePoolCardReferences(
            HistoricalArchiveData archive,
            HistoricalTeamSeason team,
            IReadOnlyList<string> cardIds,
            ValidationCollector collector)
        {
            for (int index = 0; index < cardIds.Count; index++)
            {
                string cardId = cardIds[index] ?? string.Empty;
                bool cardExists = archive.CardsById.TryGetValue(cardId, out HistoricalCard card);
                collector.Check(
                    cardExists,
                    "Join",
                    team.OriginYear,
                    team.TeamSeasonKey,
                    $"Pool Card 참조가 유효합니다: {cardId}",
                    $"Pool Card를 찾을 수 없습니다: {cardId}",
                    HistoricalNavigationKind.Team,
                    team.TeamSeasonKey);
                if (!cardExists)
                    continue;

                bool seasonExists = archive.PlayersBySeasonId.TryGetValue(card.PlayerSeasonId, out HistoricalPlayerRow row);
                bool originMatches = seasonExists &&
                                     string.Equals(row.Season.OriginTeamSeasonKey, team.TeamSeasonKey, StringComparison.Ordinal) &&
                                     row.Season.OriginYear == team.OriginYear &&
                                     string.Equals(row.Season.OriginFranchiseId, team.FranchiseId, StringComparison.Ordinal);
                collector.Check(
                    string.Equals(card.Edition, "Normal", StringComparison.Ordinal) && originMatches,
                    "Origin",
                    team.OriginYear,
                    cardId,
                    "Pool Card가 이 TeamSeason의 Normal 원본입니다.",
                    "Pool Card의 Edition 또는 Origin이 TeamSeason과 다릅니다.",
                    HistoricalNavigationKind.Team,
                    team.TeamSeasonKey);
            }
        }

        private static bool CanSourcePoolSatisfyCoreComposition(
            HistoricalArchiveData archive,
            HistoricalTeamSeason team)
        {
            int hitterCount = 0;
            int pitcherCount = 0;
            IReadOnlyList<string> cardIds = team.AllNormalCardIds ?? Array.Empty<string>();
            for (int index = 0; index < cardIds.Count; index++)
            {
                if (!archive.CardsById.TryGetValue(cardIds[index], out HistoricalCard card) ||
                    !archive.PlayersBySeasonId.TryGetValue(card.PlayerSeasonId, out HistoricalPlayerRow row))
                {
                    continue;
                }

                if (string.Equals(row.Season.PlayerType, "Hitter", StringComparison.Ordinal)) hitterCount++;
                if (string.Equals(row.Season.PlayerType, "Pitcher", StringComparison.Ordinal)) pitcherCount++;
            }
            return cardIds.Count >= 25 && hitterCount >= 14 && pitcherCount >= 11;
        }

        private static TeamMetrics CollectCoreMetrics(
            HistoricalArchiveData archive,
            HistoricalTeamSeason team,
            IReadOnlyList<string> coreCards,
            ValidationCollector collector)
        {
            var metrics = new TeamMetrics { TotalCount = coreCards.Count };
            var personIds = new HashSet<string>(StringComparer.Ordinal);
            var fixedRoles = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int index = 0; index < coreCards.Count; index++)
            {
                string cardId = coreCards[index] ?? string.Empty;
                if (!archive.CardsById.TryGetValue(cardId, out HistoricalCard card) ||
                    !archive.PlayersBySeasonId.TryGetValue(card.PlayerSeasonId, out HistoricalPlayerRow row) ||
                    row.Season == null)
                {
                    continue;
                }

                HistoricalPlayerSeason season = row.Season;
                if (string.Equals(season.PlayerType, "Hitter", StringComparison.Ordinal)) metrics.HitterCount++;
                if (string.Equals(season.PlayerType, "Pitcher", StringComparison.Ordinal)) metrics.PitcherCount++;
                if (season.RosterRole != null && season.RosterRole.StartsWith("StartingHitter:", StringComparison.Ordinal))
                {
                    metrics.StartingHitterCount++;
                    Increment(fixedRoles, season.RosterRole);
                }
                else if (season.RosterRole != null && season.RosterRole.StartsWith("BenchHitter:", StringComparison.Ordinal))
                {
                    metrics.BenchHitterCount++;
                }
                else if (season.RosterRole != null && season.RosterRole.StartsWith("StartingPitcher:", StringComparison.Ordinal))
                {
                    metrics.StartingPitcherCount++;
                    Increment(fixedRoles, season.RosterRole);
                }
                else if (season.RosterRole != null && season.RosterRole.StartsWith("Bullpen", StringComparison.Ordinal))
                {
                    metrics.BullpenPitcherCount++;
                    Increment(fixedRoles, season.RosterRole);
                }
                else if (string.Equals(season.RosterRole, "Setup", StringComparison.Ordinal))
                {
                    metrics.SetupPitcherCount++;
                    Increment(fixedRoles, season.RosterRole);
                }
                else if (string.Equals(season.RosterRole, "Closer", StringComparison.Ordinal))
                {
                    metrics.CloserPitcherCount++;
                    Increment(fixedRoles, season.RosterRole);
                }

                if (string.Equals(season.RegistrationType, "Foreign", StringComparison.Ordinal))
                    metrics.ForeignPlayerCount++;
                if (!personIds.Add(season.PlayerPersonId ?? string.Empty))
                    metrics.DuplicatePersonCount++;
            }
            metrics.FixedRoles = fixedRoles;
            return metrics;
        }

        private static void ValidateCoreMetrics(
            HistoricalTeamSeason team,
            TeamMetrics metrics,
            ValidationCollector collector)
        {
            CheckRosterCount(team, "Total", 25, metrics.TotalCount, collector);
            CheckRosterCount(team, "Hitters", 14, metrics.HitterCount, collector);
            CheckRosterCount(team, "Pitchers", 11, metrics.PitcherCount, collector);
            CheckRosterCount(team, "StartingHitters", 9, metrics.StartingHitterCount, collector);
            CheckRosterCount(team, "BenchHitters", 5, metrics.BenchHitterCount, collector);
            CheckRosterCount(team, "StartingPitchers", 5, metrics.StartingPitcherCount, collector);
            CheckRosterCount(team, "BullpenPitchers", 4, metrics.BullpenPitcherCount, collector);
            CheckRosterCount(team, "Setup", 1, metrics.SetupPitcherCount, collector);
            CheckRosterCount(team, "Closer", 1, metrics.CloserPitcherCount, collector);
            collector.Check(
                metrics.ForeignPlayerCount <= 3,
                "Roster",
                team.OriginYear,
                team.TeamSeasonKey,
                $"Foreign 등록 선수가 상한 이내입니다: {metrics.ForeignPlayerCount}/3",
                $"Foreign 등록 선수는 최대 3명입니다. actual={metrics.ForeignPlayerCount}",
                HistoricalNavigationKind.Team,
                team.TeamSeasonKey);
            collector.Check(
                metrics.DuplicatePersonCount == 0,
                "Roster",
                team.OriginYear,
                team.TeamSeasonKey,
                "Core25에 중복 PlayerPerson이 없습니다.",
                $"Core25에 중복 PlayerPerson이 있습니다. duplicates={metrics.DuplicatePersonCount}",
                HistoricalNavigationKind.Team,
                team.TeamSeasonKey);

            string[] fixedRoles =
            {
                "StartingHitter:C", "StartingHitter:1B", "StartingHitter:2B", "StartingHitter:3B",
                "StartingHitter:SS", "StartingHitter:LF", "StartingHitter:CF", "StartingHitter:RF",
                "StartingHitter:DH", "StartingPitcher:1", "StartingPitcher:2", "StartingPitcher:3",
                "StartingPitcher:4", "StartingPitcher:5", "Bullpen1", "Bullpen2", "Bullpen3",
                "Bullpen4", "Setup", "Closer"
            };
            for (int index = 0; index < fixedRoles.Length; index++)
            {
                metrics.FixedRoles.TryGetValue(fixedRoles[index], out int count);
                collector.Check(
                    count == 1,
                    "Roster Role",
                    team.OriginYear,
                    team.TeamSeasonKey,
                    $"{fixedRoles[index]} 슬롯이 정확히 1명입니다.",
                    $"{fixedRoles[index]} 슬롯은 정확히 1명이어야 합니다. actual={count}",
                    HistoricalNavigationKind.Team,
                    team.TeamSeasonKey);
            }
        }

        private static void ValidateRosterTrace(
            HistoricalTeamSeason team,
            ValidationCollector collector)
        {
            HistoricalRosterSelectionTrace trace = team.RosterSelectionTrace;
            if (trace == null)
            {
                collector.Add(
                    HistoricalValidationSeverity.Warning,
                    "DERIVED_CACHE_VERSION_MISMATCH",
                    team.OriginYear,
                    team.TeamSeasonKey,
                    "RosterSelectionTrace가 없어 현재 RosterBuilder 산출 근거를 확인할 수 없습니다.",
                    HistoricalNavigationKind.Team,
                    team.TeamSeasonKey);
                return;
            }

            for (int index = 0; index < trace.ValidationWarnings.Count; index++)
            {
                HistoricalDerivationWarningTrace warning = trace.ValidationWarnings[index];
                collector.Add(
                    HistoricalValidationSeverity.Warning,
                    warning.Code,
                    team.OriginYear,
                    string.IsNullOrWhiteSpace(warning.PlayerSeasonId)
                        ? team.TeamSeasonKey
                        : warning.PlayerSeasonId,
                    warning.Message,
                    HistoricalNavigationKind.Team,
                    team.TeamSeasonKey);
            }

            var assignedSlots = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < trace.StartingSlots.Count; index++)
            {
                HistoricalStartingSlotTrace slot = trace.StartingSlots[index];
                if (slot != null && !assignedSlots.Add(slot.Slot))
                {
                    collector.Add(
                        HistoricalValidationSeverity.Warning,
                        "ROSTER_DUPLICATE_STARTER_POSITION",
                        team.OriginYear,
                        team.TeamSeasonKey,
                        $"수비 Starter slot {slot.Slot}이 중복되었습니다.",
                        HistoricalNavigationKind.Team,
                        team.TeamSeasonKey);
                }
            }
        }

        private static void CheckRosterCount(
            HistoricalTeamSeason team,
            string label,
            int expected,
            int actual,
            ValidationCollector collector)
        {
            collector.Check(
                actual == expected,
                "Roster",
                team.OriginYear,
                team.TeamSeasonKey,
                $"{label}: {actual}",
                $"{label} 인원이 다릅니다. expected={expected}, actual={actual}",
                HistoricalNavigationKind.Team,
                team.TeamSeasonKey);
        }

        private static bool AddAllIds(IReadOnlyList<string> source, ISet<string> target)
        {
            bool isValid = true;
            for (int index = 0; index < source.Count; index++)
            {
                string id = source[index];
                if (string.IsNullOrWhiteSpace(id) || !target.Add(id))
                    isValid = false;
            }
            return isValid;
        }

        private static void Increment(IDictionary<string, int> counts, string key)
        {
            counts.TryGetValue(key, out int count);
            counts[key] = count + 1;
        }

        private sealed class TeamMetrics
        {
            public int TotalCount;
            public int HitterCount;
            public int PitcherCount;
            public int StartingHitterCount;
            public int BenchHitterCount;
            public int StartingPitcherCount;
            public int BullpenPitcherCount;
            public int SetupPitcherCount;
            public int CloserPitcherCount;
            public int ForeignPlayerCount;
            public int DuplicatePersonCount;
            public IReadOnlyDictionary<string, int> FixedRoles = new Dictionary<string, int>();
        }
    }
}
