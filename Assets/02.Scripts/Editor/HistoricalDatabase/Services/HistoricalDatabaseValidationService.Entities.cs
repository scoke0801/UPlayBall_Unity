using System;
using System.Collections.Generic;

namespace Baseball.Editor.HistoricalDatabase
{
    public sealed partial class HistoricalDatabaseValidationService
    {
        private static readonly HashSet<string> ValidPositions = new HashSet<string>(
            new[] { "P", "C", "1B", "2B", "3B", "SS", "LF", "CF", "RF", "DH" },
            StringComparer.Ordinal);

        private static readonly HashSet<string> ValidPitcherRoles = new HashSet<string>(
            new[] { "Starter", "Swingman", "LongRelief", "MiddleRelief", "Setup", "Closer" },
            StringComparer.Ordinal);

        private static readonly HashSet<string> ValidAwardTypes = new HashSet<string>(
            new[] { "AllStar", "GoldenGlove", "RegularSeasonMvp", "AllStarGameMvp", "KoreanSeriesMvp", "PostseasonMvp" },
            StringComparer.Ordinal);

        private const string SourceBackedProvenance = "SourceBacked";
        private const string ReplacementGeneratedProvenance = "ReplacementGenerated";

        private static void ValidatePersonsAndPlayers(
            HistoricalArchiveData archive,
            ValidationCollector collector)
        {
            var personIds = new HashSet<string>(StringComparer.Ordinal);
            bool requiresOriginalNames = string.Equals(
                archive.Manifest.SourceManifest?.NameDataPolicy,
                "editor-original-reference-v1",
                StringComparison.Ordinal);
            var minimumYearByPerson = new Dictionary<string, int>(StringComparer.Ordinal);
            var maximumYearByPerson = new Dictionary<string, int>(StringComparer.Ordinal);
            var provenanceByPerson = new Dictionary<string, string>(StringComparer.Ordinal);
            if (archive.PlayerRows != null)
            {
                for (int index = 0; index < archive.PlayerRows.Count; index++)
                {
                    HistoricalPlayerRow row = archive.PlayerRows[index];
                    if (row?.Season == null || string.IsNullOrWhiteSpace(row.Season.PlayerPersonId))
                        continue;
                    string personId = row.Season.PlayerPersonId;
                    if (!minimumYearByPerson.TryGetValue(personId, out int minimumYear) || row.Season.OriginYear < minimumYear)
                        minimumYearByPerson[personId] = row.Season.OriginYear;
                    if (!maximumYearByPerson.TryGetValue(personId, out int maximumYear) || row.Season.OriginYear > maximumYear)
                        maximumYearByPerson[personId] = row.Season.OriginYear;
                    if (provenanceByPerson.TryGetValue(personId, out string existingProvenance))
                    {
                        collector.Check(
                            string.Equals(existingProvenance, row.Season.DataProvenance, StringComparison.Ordinal),
                            "Data Provenance",
                            row.Season.OriginYear,
                            personId,
                            "한 PlayerPerson의 모든 시즌 DataProvenance가 일치합니다.",
                            $"한 PlayerPerson에 서로 다른 DataProvenance가 섞였습니다: {existingProvenance}/{row.Season.DataProvenance}",
                            HistoricalNavigationKind.Player,
                            personId);
                    }
                    else
                    {
                        provenanceByPerson[personId] = row.Season.DataProvenance;
                    }
                }
            }

            if (archive.Persons != null)
            {
                for (int index = 0; index < archive.Persons.Count; index++)
                {
                    HistoricalPlayerPerson person = archive.Persons[index];
                    if (person == null)
                    {
                        collector.Add(HistoricalValidationSeverity.Error, "PlayerPerson", null, string.Empty, "null PlayerPerson이 있습니다.");
                        continue;
                    }
                    provenanceByPerson.TryGetValue(person.PlayerPersonId, out string provenance);
                    ValidatePerson(person, provenance, personIds, minimumYearByPerson, maximumYearByPerson, collector);
                    if (requiresOriginalNames)
                    {
                        collector.Check(
                            !string.IsNullOrWhiteSpace(person.OriginalName),
                            "Editor Source",
                            null,
                            person.PlayerPersonId,
                            "대표 원본 이름이 존재합니다.",
                            "Editor 원본명 Archive에 대표 원본 이름이 없습니다.",
                            HistoricalNavigationKind.Player,
                            person.PlayerPersonId);
                    }
                }
            }

            var seasonIds = new HashSet<string>(StringComparer.Ordinal);
            if (archive.PlayerRows == null)
                return;
            for (int index = 0; index < archive.PlayerRows.Count; index++)
            {
                HistoricalPlayerRow row = archive.PlayerRows[index];
                if (row?.Season == null)
                {
                    collector.Add(HistoricalValidationSeverity.Error, "PlayerSeason", null, string.Empty, "null PlayerSeason Row가 있습니다.");
                    continue;
                }
                ValidatePlayer(row, archive, seasonIds, collector);
                if (requiresOriginalNames)
                {
                    collector.Check(
                        row.SourceReferenceNames.Count > 0,
                        "Editor Source",
                        row.OriginYear,
                        row.PlayerSeasonId,
                        "1:1 Source Reference 이름이 존재합니다.",
                        "Editor 원본명 Archive에 1:1 Source Reference 이름이 없습니다.",
                        HistoricalNavigationKind.Player,
                        row.PlayerSeasonId);
                }
            }
        }

        private static void ValidatePerson(
            HistoricalPlayerPerson person,
            string dataProvenance,
            ISet<string> personIds,
            IReadOnlyDictionary<string, int> minimumYearByPerson,
            IReadOnlyDictionary<string, int> maximumYearByPerson,
            ValidationCollector collector)
        {
            string id = person.PlayerPersonId ?? string.Empty;
            collector.Check(
                !string.IsNullOrWhiteSpace(id) && personIds.Add(id),
                "Stable ID",
                null,
                id,
                "PlayerPersonId가 고유합니다.",
                "PlayerPersonId가 비어 있거나 중복되었습니다.",
                HistoricalNavigationKind.Player,
                id);
            collector.Check(
                IsStablePersonId(id, dataProvenance),
                "Stable ID",
                null,
                id,
                $"{dataProvenance} PlayerPersonId 형식이 유효합니다.",
                $"PlayerPersonId 형식이 DataProvenance와 맞지 않습니다: provenance={dataProvenance}, id={id}",
                HistoricalNavigationKind.Player,
                id);
            collector.Check(
                person.BirthYear > 0 && person.CareerStartYear > 0 && person.CareerEndYear >= person.CareerStartYear,
                "Origin",
                person.CareerStartYear,
                id,
                $"Career Span {person.CareerStartYear}-{person.CareerEndYear}가 유효합니다.",
                "BirthYear 또는 Career Span이 유효하지 않습니다.",
                HistoricalNavigationKind.Player,
                id);
            collector.Check(
                ValidPositions.Contains(person.PrimaryPosition),
                "Position",
                null,
                id,
                $"PrimaryPosition {person.PrimaryPosition}을 확인했습니다.",
                $"지원하지 않는 PrimaryPosition입니다: {person.PrimaryPosition}",
                HistoricalNavigationKind.Player,
                id);
            collector.Check(
                IsRegistrationType(person.RegistrationType),
                "Registration",
                null,
                id,
                $"RegistrationType {person.RegistrationType}을 확인했습니다.",
                $"지원하지 않는 RegistrationType입니다: {person.RegistrationType}",
                HistoricalNavigationKind.Player,
                id);
            collector.Check(
                IsBats(person.Bats) && IsThrows(person.Throws),
                "PlayerPerson",
                null,
                id,
                $"Bats/Throws {person.Bats}/{person.Throws}를 확인했습니다.",
                $"지원하지 않는 Bats/Throws입니다: {person.Bats}/{person.Throws}",
                HistoricalNavigationKind.Player,
                id);

            bool potentialLengthValid = person.PotentialTrait != null &&
                                        person.PotentialTrait.Length == ExpectedAbilityCount;
            collector.Check(
                potentialLengthValid,
                "Ability",
                null,
                id,
                "PersonPotentialTrait가 12개 능력치를 가집니다.",
                $"PersonPotentialTrait 길이는 12여야 합니다. actual={person.PotentialTrait?.Length ?? 0}",
                HistoricalNavigationKind.Player,
                id);
            if (potentialLengthValid)
            {
                bool valuesValid = true;
                for (int abilityIndex = 0; abilityIndex < person.PotentialTrait.Length; abilityIndex++)
                {
                    int value = person.PotentialTrait[abilityIndex];
                    if (value < 0 || value > 100)
                    {
                        valuesValid = false;
                        break;
                    }
                }
                collector.Check(
                    valuesValid,
                    "Ability",
                    null,
                    id,
                    "PersonPotentialTrait가 0~100 범위입니다.",
                    "PersonPotentialTrait에 0~100 범위 밖 값이 있습니다.",
                    HistoricalNavigationKind.Player,
                    id);
            }

            int minimumYear = 0;
            int maximumYear = 0;
            bool hasSeason = minimumYearByPerson.TryGetValue(id, out minimumYear) &&
                             maximumYearByPerson.TryGetValue(id, out maximumYear);
            collector.Check(
                hasSeason,
                "Join",
                null,
                id,
                "PlayerPerson에 연결된 PlayerSeason이 있습니다.",
                "PlayerPerson에 연결된 PlayerSeason이 없습니다.",
                HistoricalNavigationKind.Player,
                id);
            if (hasSeason)
            {
                collector.Check(
                    person.CareerStartYear == minimumYear && person.CareerEndYear == maximumYear,
                    "Origin",
                    minimumYear,
                    id,
                    "Career Span이 연결된 시즌 범위와 일치합니다.",
                    $"Career Span이 시즌 범위와 다릅니다. person={person.CareerStartYear}-{person.CareerEndYear}, seasons={minimumYear}-{maximumYear}",
                    HistoricalNavigationKind.Player,
                    id);
            }
        }

        private static void ValidatePlayer(
            HistoricalPlayerRow row,
            HistoricalArchiveData archive,
            ISet<string> seasonIds,
            ValidationCollector collector)
        {
            HistoricalPlayerSeason season = row.Season;
            string id = season.PlayerSeasonId ?? string.Empty;
            int year = season.OriginYear;
            collector.Check(
                !string.IsNullOrWhiteSpace(id) && seasonIds.Add(id),
                "Stable ID",
                year,
                id,
                "PlayerSeasonId가 고유합니다.",
                "PlayerSeasonId가 비어 있거나 중복되었습니다.",
                HistoricalNavigationKind.Player,
                id);
            collector.Check(
                IsStableSeasonId(id, season.DataProvenance),
                "Stable ID",
                year,
                id,
                $"{season.DataProvenance} PlayerSeasonId 형식이 유효합니다.",
                $"PlayerSeasonId 형식이 DataProvenance와 맞지 않습니다: provenance={season.DataProvenance}, id={id}",
                HistoricalNavigationKind.Player,
                id);
            collector.Check(
                IsSupportedDataProvenance(season.DataProvenance),
                "Data Provenance",
                year,
                id,
                $"DataProvenance {season.DataProvenance}을 확인했습니다.",
                $"지원하지 않는 DataProvenance입니다: {season.DataProvenance}",
                HistoricalNavigationKind.Player,
                id);

            HistoricalPlayerPerson person = null;
            bool personExists = !string.IsNullOrWhiteSpace(season.PlayerPersonId) &&
                                archive.PersonsById.TryGetValue(season.PlayerPersonId, out person);
            collector.Check(
                personExists,
                "Join",
                year,
                id,
                "PlayerSeason → PlayerPerson 참조가 유효합니다.",
                $"PlayerPerson을 찾을 수 없습니다: {season.PlayerPersonId}",
                HistoricalNavigationKind.Player,
                id);
            if (personExists)
            {
                collector.Check(
                    string.Equals(season.RegistrationType, person.RegistrationType, StringComparison.Ordinal),
                    "Origin",
                    year,
                    id,
                    "Person/Season RegistrationType이 일치합니다.",
                    $"Person/Season RegistrationType이 다릅니다: {person.RegistrationType}/{season.RegistrationType}",
                    HistoricalNavigationKind.Player,
                    id);
                collector.Check(
                    year >= person.CareerStartYear && year <= person.CareerEndYear,
                    "Origin",
                    year,
                    id,
                    "OriginYear가 Person Career Span 안에 있습니다.",
                    $"OriginYear {year}가 Career Span {person.CareerStartYear}-{person.CareerEndYear} 밖입니다.",
                    HistoricalNavigationKind.Player,
                    id);
            }

            HistoricalTeamSeason team = null;
            bool teamExists = !string.IsNullOrWhiteSpace(season.OriginTeamSeasonKey) &&
                              archive.TeamsByKey.TryGetValue(season.OriginTeamSeasonKey, out team);
            collector.Check(
                teamExists,
                "Join",
                year,
                id,
                "PlayerSeason → Origin TeamSeason 참조가 유효합니다.",
                $"Origin TeamSeason을 찾을 수 없습니다: {season.OriginTeamSeasonKey}",
                HistoricalNavigationKind.Player,
                id);
            if (teamExists)
            {
                collector.Check(
                    team.OriginYear == year && string.Equals(team.FranchiseId, season.OriginFranchiseId, StringComparison.Ordinal),
                    "Origin",
                    year,
                    id,
                    "PlayerSeason Origin이 TeamSeason과 일치합니다.",
                    $"PlayerSeason Origin과 TeamSeason이 다릅니다: {season.OriginFranchiseId}/{year}, {team.FranchiseId}/{team.OriginYear}",
                    HistoricalNavigationKind.Team,
                    team.TeamSeasonKey);
            }

            collector.Check(
                season.Cost >= 1 && season.Cost <= 10,
                "Cost",
                year,
                id,
                $"Cost {season.Cost}가 유효합니다.",
                $"Cost는 1~10이어야 합니다. actual={season.Cost}",
                HistoricalNavigationKind.Player,
                id);
            ValidateRatingArrays(season, collector);
            collector.Check(
                ValidPositions.Contains(season.Position) && IsPlayerTypePositionCompatible(season.PlayerType, season.Position),
                "Position",
                year,
                id,
                $"PlayerType/Position {season.PlayerType}/{season.Position}이 유효합니다.",
                $"PlayerType/Position 조합이 유효하지 않습니다: {season.PlayerType}/{season.Position}",
                HistoricalNavigationKind.Player,
                id);
            collector.Check(
                IsPitcherRoleCompatible(season.PlayerType, season.PitcherRole),
                "PitcherRole",
                year,
                id,
                $"PitcherRole {season.PitcherRole}을 확인했습니다.",
                $"지원하지 않는 PitcherRole입니다: {season.PitcherRole}",
                HistoricalNavigationKind.Player,
                id);
            collector.Check(
                IsRegistrationType(season.RegistrationType),
                "Registration",
                year,
                id,
                $"RegistrationType {season.RegistrationType}을 확인했습니다.",
                $"지원하지 않는 RegistrationType입니다: {season.RegistrationType}",
                HistoricalNavigationKind.Player,
                id);
            collector.Check(
                IsRosterRole(season.RosterRole),
                "Roster",
                year,
                id,
                $"RosterRole {season.RosterRole}을 확인했습니다.",
                $"지원하지 않는 RosterRole입니다: {season.RosterRole}",
                HistoricalNavigationKind.Player,
                id);
        }

        private static void ValidateRatingArrays(
            HistoricalPlayerSeason season,
            ValidationCollector collector)
        {
            string id = season.PlayerSeasonId ?? string.Empty;
            bool lengthValid = season.BaseAttributes != null && season.BaseAttributes.Length == ExpectedAbilityCount &&
                               season.TrainingCeiling != null && season.TrainingCeiling.Length == ExpectedAbilityCount;
            collector.Check(
                lengthValid,
                "Ability",
                season.OriginYear,
                id,
                "BaseAttributes와 TrainingCeiling이 각각 12개 능력치를 가집니다.",
                $"능력치 배열 길이는 각각 12여야 합니다. base={season.BaseAttributes?.Length ?? 0}, ceiling={season.TrainingCeiling?.Length ?? 0}",
                HistoricalNavigationKind.Player,
                id);
            if (!lengthValid)
                return;

            bool rangeValid = true;
            bool ceilingValid = true;
            for (int abilityIndex = 0; abilityIndex < ExpectedAbilityCount; abilityIndex++)
            {
                int baseValue = season.BaseAttributes[abilityIndex];
                int ceilingValue = season.TrainingCeiling[abilityIndex];
                if (baseValue < Baseball.Core.Growth.AbilityRatings.Minimum ||
                    baseValue > Baseball.Core.Growth.AbilityRatings.Maximum ||
                    ceilingValue < Baseball.Core.Growth.AbilityRatings.Minimum ||
                    ceilingValue > Baseball.Core.Growth.AbilityRatings.Maximum)
                    rangeValid = false;
                if (ceilingValue < baseValue)
                    ceilingValid = false;
            }
            collector.Check(
                rangeValid,
                "Ability",
                season.OriginYear,
                id,
                "BaseAttributes와 TrainingCeiling이 1~100 범위입니다.",
                "BaseAttributes 또는 TrainingCeiling에 1~100 범위 밖 값이 있습니다.",
                HistoricalNavigationKind.Player,
                id);
            collector.Check(
                ceilingValid,
                "Ability",
                season.OriginYear,
                id,
                "모든 TrainingCeiling이 BaseAttributes 이상입니다.",
                "BaseAttributes보다 낮은 TrainingCeiling이 있습니다.",
                HistoricalNavigationKind.Player,
                id);
        }

        private static void ValidateRecordsAndAwards(
            HistoricalArchiveData archive,
            ValidationCollector collector)
        {
            var recordCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            if (archive.Records != null)
            {
                for (int index = 0; index < archive.Records.Count; index++)
                {
                    HistoricalSeasonRecord record = archive.Records[index];
                    if (record == null)
                    {
                        collector.Add(HistoricalValidationSeverity.Error, "Original Record", null, string.Empty, "null Original Record가 있습니다.");
                        continue;
                    }
                    recordCounts.TryGetValue(record.PlayerSeasonId ?? string.Empty, out int count);
                    recordCounts[record.PlayerSeasonId ?? string.Empty] = count + 1;
                    ValidateRecord(record, archive, collector);
                }
            }

            if (archive.PlayerRows != null)
            {
                for (int index = 0; index < archive.PlayerRows.Count; index++)
                {
                    HistoricalPlayerRow row = archive.PlayerRows[index];
                    if (row?.Season == null) continue;
                    recordCounts.TryGetValue(row.Season.PlayerSeasonId, out int count);
                    collector.Check(
                        count == 1,
                        "Join",
                        row.Season.OriginYear,
                        row.Season.PlayerSeasonId,
                        "PlayerSeason에 Original Record가 정확히 하나 연결됩니다.",
                        $"PlayerSeason의 Original Record 수는 1이어야 합니다. actual={count}",
                        HistoricalNavigationKind.Player,
                        row.Season.PlayerSeasonId);
                }
            }

            ValidateAwards(archive, collector);
        }

        private static void ValidateRecord(
            HistoricalSeasonRecord record,
            HistoricalArchiveData archive,
            ValidationCollector collector)
        {
            string id = record.PlayerSeasonId ?? string.Empty;
            bool playerExists = archive.PlayersBySeasonId.TryGetValue(id, out HistoricalPlayerRow row);
            collector.Check(
                playerExists,
                "Join",
                record.SeasonYear,
                id,
                "Original Record → PlayerSeason 참조가 유효합니다.",
                "Original Record가 존재하지 않는 PlayerSeason을 참조합니다.",
                HistoricalNavigationKind.Player,
                id);
            if (playerExists)
            {
                HistoricalPlayerSeason season = row.Season;
                collector.Check(
                    record.SeasonYear == season.OriginYear &&
                    string.Equals(record.TeamSeasonKey, season.OriginTeamSeasonKey, StringComparison.Ordinal) &&
                    string.Equals(record.Position, season.Position, StringComparison.Ordinal),
                    "Join",
                    record.SeasonYear,
                    id,
                    "Original Record의 Year/Team/Position이 PlayerSeason과 일치합니다.",
                    "Original Record의 Year/Team/Position이 PlayerSeason과 다릅니다.",
                    HistoricalNavigationKind.Player,
                    id);
            }

            bool countsValid = record.PlateAppearances >= 0 && record.Hits >= 0 && record.HomeRuns >= 0 &&
                               record.Walks >= 0 && record.Strikeouts >= 0 && record.DefensiveChances >= 0 &&
                               record.FieldingErrors >= 0 && record.PitchingOuts >= 0 && record.EarnedRuns >= 0 &&
                               record.PitchingStrikeouts >= 0;
            collector.Check(
                countsValid,
                "Original Record",
                record.SeasonYear,
                id,
                "Original Record 집계값이 음수가 아닙니다.",
                "Original Record에 음수 집계값이 있습니다.",
                HistoricalNavigationKind.Player,
                id);
        }

        private static void ValidateAwards(HistoricalArchiveData archive, ValidationCollector collector)
        {
            var unique = new HashSet<string>(StringComparer.Ordinal);
            if (archive.Awards != null)
            {
                for (int index = 0; index < archive.Awards.Count; index++)
                {
                    HistoricalAwardRecord award = archive.Awards[index];
                    if (award == null)
                    {
                        collector.Add(HistoricalValidationSeverity.Error, "Award", null, string.Empty, "null Award Record가 있습니다.");
                        continue;
                    }
                    string stableKey = award.SeasonYear + ":" + award.AwardType + ":" + award.Position + ":" + award.PlayerSeasonId;
                    collector.Check(
                        unique.Add(stableKey),
                        "Stable ID",
                        award.SeasonYear,
                        stableKey,
                        "Award Record key가 고유합니다.",
                        "같은 Award Record가 중복되었습니다.",
                        HistoricalNavigationKind.Award,
                        award.PlayerSeasonId);
                    collector.Check(
                        ValidAwardTypes.Contains(award.AwardType),
                        "Award",
                        award.SeasonYear,
                        stableKey,
                        $"AwardType {award.AwardType}을 확인했습니다.",
                        $"지원하지 않는 AwardType입니다: {award.AwardType}",
                        HistoricalNavigationKind.Award,
                        award.PlayerSeasonId);

                    bool playerExists = archive.PlayersBySeasonId.TryGetValue(award.PlayerSeasonId ?? string.Empty, out HistoricalPlayerRow row);
                    collector.Check(
                        playerExists,
                        "Join",
                        award.SeasonYear,
                        stableKey,
                        "Award → PlayerSeason 참조가 유효합니다.",
                        $"Award가 존재하지 않는 PlayerSeason을 참조합니다: {award.PlayerSeasonId}",
                        HistoricalNavigationKind.Player,
                        award.PlayerSeasonId);
                    if (playerExists)
                    {
                        collector.Check(
                            award.SeasonYear == row.Season.OriginYear &&
                            IsAwardPositionCompatible(award.Position, row.Season.PlayerType),
                            "Join",
                            award.SeasonYear,
                            stableKey,
                            "Award의 Year/Position이 PlayerSeason과 호환됩니다.",
                            $"Award의 Year/Position이 PlayerSeason과 다릅니다: award={award.SeasonYear}/{award.Position}, season={row.Season.OriginYear}/{row.Season.Position}",
                            HistoricalNavigationKind.Player,
                            award.PlayerSeasonId);
                    }
                }
            }
        }

        private static void ValidateCards(HistoricalArchiveData archive, ValidationCollector collector)
        {
            var cardIds = new HashSet<string>(StringComparer.Ordinal);
            var normalCardsBySeason = new Dictionary<string, int>(StringComparer.Ordinal);
            if (archive.Cards != null)
            {
                for (int index = 0; index < archive.Cards.Count; index++)
                {
                    HistoricalCard card = archive.Cards[index];
                    if (card == null)
                    {
                        collector.Add(HistoricalValidationSeverity.Error, "Card", null, string.Empty, "null Card가 있습니다.");
                        continue;
                    }
                    int? year = archive.PlayersBySeasonId.TryGetValue(card.PlayerSeasonId ?? string.Empty, out HistoricalPlayerRow row)
                        ? row.Season.OriginYear
                        : null;
                    collector.Check(
                        !string.IsNullOrWhiteSpace(card.CardId) && cardIds.Add(card.CardId),
                        "Stable ID",
                        year,
                        card.CardId,
                        "CardId가 고유합니다.",
                        "CardId가 비어 있거나 중복되었습니다.",
                        HistoricalNavigationKind.Player,
                        card.PlayerSeasonId);
                    bool playerExists = row != null;
                    collector.Check(
                        playerExists,
                        "Join",
                        year,
                        card.CardId,
                        "Card → PlayerSeason 참조가 유효합니다.",
                        $"Card가 존재하지 않는 PlayerSeason을 참조합니다: {card.PlayerSeasonId}",
                        HistoricalNavigationKind.Player,
                        card.PlayerSeasonId);
                    string expectedCardId = (card.PlayerSeasonId ?? string.Empty) + ":" + (card.Edition ?? string.Empty);
                    collector.Check(
                        string.Equals(card.CardId, expectedCardId, StringComparison.Ordinal),
                        "Stable ID",
                        year,
                        card.CardId,
                        "CardId가 PlayerSeasonId:Edition 규칙과 일치합니다.",
                        $"CardId가 Stable CardId와 다릅니다. expected={expectedCardId}",
                        HistoricalNavigationKind.Player,
                        card.PlayerSeasonId);
                    bool modifiersValid = card.EditionStatModifiers != null &&
                                          card.EditionStatModifiers.Length == ExpectedAbilityCount;
                    collector.Check(
                        modifiersValid,
                        "Card",
                        year,
                        card.CardId,
                        "Edition modifier가 12개 능력치를 가집니다.",
                        $"Edition modifier 길이는 12여야 합니다. actual={card.EditionStatModifiers?.Length ?? 0}",
                        HistoricalNavigationKind.Player,
                        card.PlayerSeasonId);

                    if (string.Equals(card.Edition, "Normal", StringComparison.Ordinal))
                    {
                        normalCardsBySeason.TryGetValue(card.PlayerSeasonId ?? string.Empty, out int count);
                        normalCardsBySeason[card.PlayerSeasonId ?? string.Empty] = count + 1;
                        if (modifiersValid)
                        {
                            bool allZero = true;
                            for (int abilityIndex = 0; abilityIndex < card.EditionStatModifiers.Length; abilityIndex++)
                                if (card.EditionStatModifiers[abilityIndex] != 0) allZero = false;
                            collector.Check(
                                allZero,
                                "Card",
                                year,
                                card.CardId,
                                "Normal Card modifier가 모두 0입니다.",
                                "Normal Card에 0이 아닌 Edition modifier가 있습니다.",
                                HistoricalNavigationKind.Player,
                                card.PlayerSeasonId);
                        }
                    }
                }
            }

            if (archive.PlayerRows == null)
                return;
            for (int index = 0; index < archive.PlayerRows.Count; index++)
            {
                HistoricalPlayerRow row = archive.PlayerRows[index];
                if (row?.Season == null) continue;
                normalCardsBySeason.TryGetValue(row.Season.PlayerSeasonId, out int count);
                collector.Check(
                    count == 1,
                    "Join",
                    row.Season.OriginYear,
                    row.Season.PlayerSeasonId,
                    "PlayerSeason에 Normal Card가 정확히 하나 연결됩니다.",
                    $"PlayerSeason의 Normal Card 수는 1이어야 합니다. actual={count}",
                    HistoricalNavigationKind.Player,
                    row.Season.PlayerSeasonId);
            }
        }

        private static bool IsStablePersonId(string value, string dataProvenance)
        {
            return string.Equals(dataProvenance, SourceBackedProvenance, StringComparison.Ordinal)
                ? IsStableHexId(value, "PERSON_", 20)
                : string.Equals(dataProvenance, ReplacementGeneratedProvenance, StringComparison.Ordinal) &&
                  IsStableHexId(value, "REPL-PERSON-", 24);
        }

        private static bool IsStableSeasonId(string value, string dataProvenance)
        {
            return string.Equals(dataProvenance, SourceBackedProvenance, StringComparison.Ordinal)
                ? IsStableHexId(value, "SEASON_", 20)
                : string.Equals(dataProvenance, ReplacementGeneratedProvenance, StringComparison.Ordinal) &&
                  IsStableHexId(value, "REPL-SEASON-", 24);
        }

        private static bool IsSupportedDataProvenance(string value)
        {
            return string.Equals(value, SourceBackedProvenance, StringComparison.Ordinal) ||
                   string.Equals(value, ReplacementGeneratedProvenance, StringComparison.Ordinal);
        }

        private static bool IsStableHexId(string value, string prefix)
        {
            return IsStableHexId(value, prefix, 20);
        }

        private static bool IsStableHexId(string value, string prefix, int digestLength)
        {
            if (string.IsNullOrEmpty(value) || !value.StartsWith(prefix, StringComparison.Ordinal) ||
                value.Length != prefix.Length + digestLength)
                return false;
            for (int index = prefix.Length; index < value.Length; index++)
            {
                char character = value[index];
                bool isHex = character >= '0' && character <= '9' ||
                             character >= 'a' && character <= 'f' ||
                             character >= 'A' && character <= 'F';
                if (!isHex) return false;
            }
            return true;
        }

        private static bool IsPlayerTypePositionCompatible(string playerType, string position)
        {
            return string.Equals(playerType, "Hitter", StringComparison.Ordinal)
                ? !string.Equals(position, "P", StringComparison.Ordinal)
                : string.Equals(playerType, "Pitcher", StringComparison.Ordinal) &&
                  string.Equals(position, "P", StringComparison.Ordinal);
        }

        private static bool IsPitcherRoleCompatible(string playerType, string pitcherRole)
        {
            if (string.Equals(playerType, "Pitcher", StringComparison.Ordinal))
                return ValidPitcherRoles.Contains(pitcherRole);
            return string.Equals(playerType, "Hitter", StringComparison.Ordinal) &&
                   string.IsNullOrEmpty(pitcherRole);
        }

        private static bool IsRosterRole(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            if (value.StartsWith("StartingHitter:", StringComparison.Ordinal))
                return ValidPositions.Contains(value.Substring("StartingHitter:".Length)) &&
                       !value.EndsWith(":P", StringComparison.Ordinal);
            if (value.StartsWith("BenchHitter:", StringComparison.Ordinal))
                return HasNumericSuffix(value, "BenchHitter:", 1, 5);
            if (value.StartsWith("ReserveHitter:", StringComparison.Ordinal))
                return HasNumericSuffix(value, "ReserveHitter:", 1, int.MaxValue);
            if (value.StartsWith("StartingPitcher:", StringComparison.Ordinal))
                return HasNumericSuffix(value, "StartingPitcher:", 1, 5);
            if (value.StartsWith("ReservePitcher:", StringComparison.Ordinal))
                return HasNumericSuffix(value, "ReservePitcher:", 1, int.MaxValue);
            if (value.StartsWith("Bullpen", StringComparison.Ordinal))
                return HasNumericSuffix(value, "Bullpen", 1, 4);
            return value == "Setup" || value == "Closer";
        }

        private static bool HasNumericSuffix(string value, string prefix, int minimum, int maximum)
        {
            return int.TryParse(value.Substring(prefix.Length), out int number) &&
                   number >= minimum && number <= maximum;
        }

        private static bool IsRegistrationType(string value)
        {
            return value == "Domestic" || value == "Foreign";
        }

        private static bool IsBats(string value)
        {
            return value == "Right" || value == "Left" || value == "Switch";
        }

        private static bool IsThrows(string value)
        {
            return value == "Right" || value == "Left";
        }

        private static bool IsAwardPositionCompatible(string awardPosition, string playerType)
        {
            bool isPitcherAward = string.Equals(awardPosition, "P", StringComparison.Ordinal);
            return isPitcherAward
                ? string.Equals(playerType, "Pitcher", StringComparison.Ordinal)
                : string.Equals(playerType, "Hitter", StringComparison.Ordinal);
        }
    }
}
