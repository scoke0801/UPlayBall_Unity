using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Baseball.Editor.HistoricalDatabase
{
    public sealed partial class HistoricalDatabaseValidationService
    {
        private const int ExpectedContentSchemaVersion = 4;
        private const int ExpectedNormalizedSchemaVersion = 3;
        private const string ExpectedReferenceDataVersion = "kbo-normalized-v3";
        private const string ExpectedNormalizedImporterVersion = "1.2.0";
        private const string ExpectedAbilityFormulaVersion = "historical-ability-v3";
        private const string ExpectedPositionRoleClassifierVersion = "season-position-role-v4";
        private const string ExpectedRosterBuilderVersion = "position-first-core25-v2";
        private const string ExpectedCostFormulaVersion = "historical-role-composite-v3";
        private const string ExpectedDerivationBalanceVersion = "historical-derivation-balance-v4";

        private static void ValidateManifestAndFiles(
            HistoricalArchiveData archive,
            ValidationCollector collector)
        {
            HistoricalArchiveManifest manifest = archive.Manifest;
            if (manifest == null)
            {
                collector.Add(
                    HistoricalValidationSeverity.Error,
                    "Manifest",
                    null,
                    "manifest.json",
                    "manifest.json을 역직렬화하지 못했습니다.",
                    HistoricalNavigationKind.File,
                    "manifest.json");
                return;
            }

            collector.Check(
                manifest.AssetFormatVersion == ExpectedAssetFormatVersion,
                "Manifest",
                null,
                "assetFormatVersion",
                $"지원하는 Asset Format {ExpectedAssetFormatVersion}입니다.",
                $"지원하지 않는 Asset Format입니다. expected={ExpectedAssetFormatVersion}, actual={manifest.AssetFormatVersion}",
                HistoricalNavigationKind.File,
                "manifest.json");
            collector.Check(
                manifest.ContentSchemaVersion == ExpectedContentSchemaVersion,
                "Manifest",
                null,
                "contentSchemaVersion",
                $"Content Schema Version {manifest.ContentSchemaVersion}을 확인했습니다.",
                $"DERIVED_CACHE_VERSION_MISMATCH: expected={ExpectedContentSchemaVersion}, actual={manifest.ContentSchemaVersion}",
                HistoricalNavigationKind.File,
                "manifest.json");

            ValidateSourceManifest(manifest.SourceManifest, collector);
            ValidateManifestCounts(archive, manifest, collector);
            ValidateSourceFiles(archive, manifest, collector);
        }

        private static void ValidateSourceManifest(
            HistoricalSourceManifest sourceManifest,
            ValidationCollector collector)
        {
            if (sourceManifest == null)
            {
                collector.Add(
                    HistoricalValidationSeverity.Error,
                    "Manifest",
                    null,
                    "sourceManifest",
                    "Source Manifest가 없습니다.",
                    HistoricalNavigationKind.File,
                    "manifest.json");
                return;
            }

            ValidateRequiredManifestText("referenceDataVersion", sourceManifest.ReferenceDataVersion, collector);
            ValidateRequiredManifestText("generatorVersion", sourceManifest.GeneratorVersion, collector);
            ValidateRequiredManifestText("balanceVersion", sourceManifest.BalanceVersion, collector);
            ValidateRequiredManifestText("sourceIdentityPolicyVersion", sourceManifest.SourceIdentityPolicyVersion, collector);
            ValidateRequiredManifestText("sourceAllocationPolicyVersion", sourceManifest.SourceAllocationPolicyVersion, collector);
            ValidateRequiredManifestText("replacementGeneratorVersion", sourceManifest.ReplacementGeneratorVersion, collector);
            ValidateRequiredManifestText("replacementPopulationPolicyVersion", sourceManifest.ReplacementPopulationPolicyVersion, collector);
            ValidateExpectedManifestText(
                "referenceDataVersion",
                ExpectedReferenceDataVersion,
                sourceManifest.ReferenceDataVersion,
                collector);
            ValidateExpectedManifestNumber(
                "normalizedSchemaVersion",
                ExpectedNormalizedSchemaVersion,
                sourceManifest.NormalizedSchemaVersion,
                collector);
            ValidateExpectedManifestText(
                "normalizedImporterVersion",
                ExpectedNormalizedImporterVersion,
                sourceManifest.NormalizedImporterVersion,
                collector);
            ValidateExpectedManifestText(
                "abilityFormulaVersion",
                ExpectedAbilityFormulaVersion,
                sourceManifest.AbilityFormulaVersion,
                collector);
            ValidateExpectedManifestText(
                "positionRoleClassifierVersion",
                ExpectedPositionRoleClassifierVersion,
                sourceManifest.PositionRoleClassifierVersion,
                collector);
            ValidateExpectedManifestText(
                "rosterBuilderVersion",
                ExpectedRosterBuilderVersion,
                sourceManifest.RosterBuilderVersion,
                collector);
            ValidateExpectedManifestText(
                "costFormulaVersion",
                ExpectedCostFormulaVersion,
                sourceManifest.CostFormulaVersion,
                collector);
            ValidateExpectedManifestText(
                "derivationBalanceVersion",
                ExpectedDerivationBalanceVersion,
                sourceManifest.DerivationBalanceVersion,
                collector);
            ValidateManifestHash("rawDataVersion", sourceManifest.RawDataVersion, collector);
            ValidateManifestHash("normalizedContentHash", sourceManifest.NormalizedContentHash, collector);
            collector.Check(
                sourceManifest.GenerationSeed >= 0,
                "Manifest",
                null,
                "generationSeed",
                $"Generation Seed {sourceManifest.GenerationSeed}을 확인했습니다.",
                "Generation Seed는 음수일 수 없습니다.",
                HistoricalNavigationKind.File,
                "manifest.json");

            if (IsSha256(sourceManifest.ContentHash))
            {
                collector.Add(
                    HistoricalValidationSeverity.Warning,
                    "Hash",
                    null,
                    "contentHash",
                    "ContentHash 형식은 유효하지만 canonical 단일 JSON 재조립을 보존하지 않으므로 이 도구에서는 값 자체를 재계산하지 않습니다.",
                    HistoricalNavigationKind.File,
                    "manifest.json");
            }
            else
            {
                collector.Add(
                    HistoricalValidationSeverity.Error,
                    "Hash",
                    null,
                    "contentHash",
                    "ContentHash가 64자리 SHA-256 형식이 아닙니다.",
                    HistoricalNavigationKind.File,
                    "manifest.json");
            }
        }

        private static void ValidateRequiredManifestText(
            string entityId,
            string value,
            ValidationCollector collector)
        {
            collector.Check(
                !string.IsNullOrWhiteSpace(value),
                "Manifest",
                null,
                entityId,
                $"{entityId} 값이 존재합니다.",
                $"{entityId} 값이 비어 있습니다.",
                HistoricalNavigationKind.File,
                "manifest.json");
        }

        private static void ValidateExpectedManifestText(
            string fieldName,
            string expected,
            string actual,
            ValidationCollector collector)
        {
            collector.Check(
                string.Equals(actual, expected, StringComparison.Ordinal),
                "Manifest",
                null,
                fieldName,
                $"{fieldName}={actual} 버전을 확인했습니다.",
                $"DERIVED_CACHE_VERSION_MISMATCH: {fieldName} expected={expected}, actual={actual}",
                HistoricalNavigationKind.File,
                "manifest.json");
        }

        private static void ValidateExpectedManifestNumber(
            string fieldName,
            int expected,
            int actual,
            ValidationCollector collector)
        {
            collector.Check(
                actual == expected,
                "Manifest",
                null,
                fieldName,
                $"{fieldName}={actual} 버전을 확인했습니다.",
                $"DERIVED_CACHE_VERSION_MISMATCH: {fieldName} expected={expected}, actual={actual}",
                HistoricalNavigationKind.File,
                "manifest.json");
        }

        private static void ValidateManifestHash(
            string fieldName,
            string value,
            ValidationCollector collector)
        {
            collector.Check(
                IsSha256(value),
                "Manifest",
                null,
                fieldName,
                $"{fieldName} SHA-256을 확인했습니다.",
                $"DERIVED_CACHE_VERSION_MISMATCH: {fieldName}이 유효한 SHA-256이 아닙니다.",
                HistoricalNavigationKind.File,
                "manifest.json");
        }

        private static void ValidateManifestCounts(
            HistoricalArchiveData archive,
            HistoricalArchiveManifest manifest,
            ValidationCollector collector)
        {
            HistoricalArchiveSummary summary = manifest.Summary;
            if (summary == null)
            {
                collector.Add(
                    HistoricalValidationSeverity.Error,
                    "Manifest Count",
                    null,
                    "summary",
                    "Manifest Summary가 없습니다.",
                    HistoricalNavigationKind.File,
                    "manifest.json");
                return;
            }

            int manifestYearCount = manifest.Years?.Count ?? 0;
            CheckCount(summary.YearCount, manifestYearCount, "yearCount(manifest)", collector);
            CheckCount(summary.PlayerPersonCount, archive.Persons?.Count ?? 0, "playerPersonCount", collector);
            CheckCount(summary.PlayerSeasonCount, archive.PlayerRows?.Count ?? 0, "playerSeasonCount", collector);
            CheckCount(summary.TeamSeasonCount, archive.Teams?.Count ?? 0, "teamSeasonCount", collector);
            CheckCount(summary.NormalCardCount, archive.Cards?.Count ?? 0, "normalCardCount", collector);
            CheckCount(summary.OriginalRecordCount, archive.Records?.Count ?? 0, "originalRecordCount", collector);
            CheckCount(summary.OriginalAwardCount, archive.Awards?.Count ?? 0, "originalAwardCount", collector);
            CheckCount(
                summary.SourceBackedPlayerPersonCount + summary.ReplacementGeneratedPlayerPersonCount,
                archive.Persons?.Count ?? 0,
                "provenance.playerPersonCount",
                collector);
            CheckCount(
                summary.SourceBackedPlayerSeasonCount + summary.ReplacementGeneratedPlayerSeasonCount,
                archive.PlayerRows?.Count ?? 0,
                "provenance.playerSeasonCount",
                collector);

            if (manifest.PlayerPersons != null)
                CheckCount(manifest.PlayerPersons.Count, archive.Persons?.Count ?? 0, "playerPersons.file.count", collector);
            else
            {
                collector.Add(
                    HistoricalValidationSeverity.Error,
                    "Manifest Count",
                    null,
                    "playerPersons",
                    "PlayerPerson 파일 Manifest 항목이 없습니다.",
                    HistoricalNavigationKind.File,
                    "manifest.json");
            }

            var uniqueYears = new HashSet<int>();
            var uniquePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (manifest.Years == null)
                return;

            for (int index = 0; index < manifest.Years.Count; index++)
            {
                HistoricalArchiveYearEntry entry = manifest.Years[index];
                if (entry == null)
                {
                    collector.Add(HistoricalValidationSeverity.Error, "Manifest", null, "years", "null Year Manifest 항목이 있습니다.");
                    continue;
                }

                collector.Check(
                    uniqueYears.Add(entry.Year),
                    "Stable ID",
                    entry.Year,
                    entry.Year.ToString(),
                    "Manifest Year가 고유합니다.",
                    "Manifest Year가 중복되었습니다.",
                    HistoricalNavigationKind.File,
                    entry.Path);
                string normalizedPath = NormalizePath(entry.Path);
                collector.Check(
                    !string.IsNullOrEmpty(normalizedPath) && uniquePaths.Add(normalizedPath),
                    "Stable ID",
                    entry.Year,
                    normalizedPath,
                    "Year 파일 경로가 고유합니다.",
                    "Year 파일 경로가 비어 있거나 중복되었습니다.",
                    HistoricalNavigationKind.File,
                    normalizedPath);

                CheckYearEntryCounts(archive, entry, collector);
            }
        }

        private static void CheckYearEntryCounts(
            HistoricalArchiveData archive,
            HistoricalArchiveYearEntry entry,
            ValidationCollector collector)
        {
            int playerSeasonCount = CountPlayersByYear(archive.PlayerRows, entry.Year);
            int teamSeasonCount = CountTeamsByYear(archive.Teams, entry.Year);
            int originalRecordCount = CountRecordsByYear(archive.Records, entry.Year);
            int normalCardCount = CountCardsByYear(archive, entry.Year);
            int allStarCount = CountAwardsByYearAndType(archive.Awards, entry.Year, "AllStar");
            int goldenGloveCount = CountAwardsByYearAndType(archive.Awards, entry.Year, "GoldenGlove");

            CheckYearCount(entry.PlayerSeasonCount, playerSeasonCount, entry.Year, "playerSeasonCount", collector);
            CheckYearCount(entry.TeamSeasonCount, teamSeasonCount, entry.Year, "teamSeasonCount", collector);
            CheckYearCount(entry.OriginalRecordCount, originalRecordCount, entry.Year, "originalRecordCount", collector);
            CheckYearCount(entry.NormalCardCount, normalCardCount, entry.Year, "normalCardCount", collector);
            CheckYearCount(entry.AllStarCount, allStarCount, entry.Year, "allStarCount", collector);
            CheckYearCount(entry.GoldenGloveCount, goldenGloveCount, entry.Year, "goldenGloveCount", collector);
        }

        private static void ValidateSourceFiles(
            HistoricalArchiveData archive,
            HistoricalArchiveManifest manifest,
            ValidationCollector collector)
        {
            var filesByPath = new Dictionary<string, HistoricalSourceFileInfo>(StringComparer.OrdinalIgnoreCase);
            if (archive.SourceFiles != null)
            {
                for (int index = 0; index < archive.SourceFiles.Count; index++)
                {
                    HistoricalSourceFileInfo file = archive.SourceFiles[index];
                    if (file == null)
                        continue;
                    string path = NormalizePath(file.RelativePath);
                    if (!filesByPath.ContainsKey(path))
                        filesByPath.Add(path, file);
                }
            }

            var archiveEntries = new List<KeyValuePair<string, string>>();
            bool canValidateArchiveHash = true;
            if (manifest.PlayerPersons == null ||
                !ValidateSourceFile(manifest.PlayerPersons.Path, manifest.PlayerPersons.Sha256,
                    manifest.PlayerPersons.ByteLength, null, filesByPath, collector, out string personHash))
            {
                canValidateArchiveHash = false;
            }
            else
            {
                archiveEntries.Add(new KeyValuePair<string, string>(manifest.PlayerPersons.Path, personHash));
            }

            if (manifest.Years != null)
            {
                for (int index = 0; index < manifest.Years.Count; index++)
                {
                    HistoricalArchiveYearEntry entry = manifest.Years[index];
                    if (entry == null || !ValidateSourceFile(
                            entry.Path,
                            entry.Sha256,
                            entry.ByteLength,
                            entry.Year,
                            filesByPath,
                            collector,
                            out string actualHash))
                    {
                        canValidateArchiveHash = false;
                        continue;
                    }
                    archiveEntries.Add(new KeyValuePair<string, string>(entry.Path, actualHash));
                }
            }

            if (!canValidateArchiveHash)
            {
                collector.Add(
                    HistoricalValidationSeverity.Warning,
                    "Hash",
                    null,
                    "assetArchiveHash",
                    "일부 Source 파일이 없거나 손상되어 Asset Archive Hash를 계산하지 않았습니다.",
                    HistoricalNavigationKind.File,
                    "manifest.json");
                return;
            }

            string actualArchiveHash = ComputeArchiveHash(archiveEntries);
            collector.Check(
                string.Equals(actualArchiveHash, manifest.AssetArchiveHash, StringComparison.OrdinalIgnoreCase),
                "Hash",
                null,
                "assetArchiveHash",
                "Asset Archive Hash가 Manifest와 일치합니다.",
                $"Asset Archive Hash가 다릅니다. expected={manifest.AssetArchiveHash}, actual={actualArchiveHash}",
                HistoricalNavigationKind.File,
                "manifest.json");
        }

        private static bool ValidateSourceFile(
            string expectedPath,
            string expectedHash,
            long expectedByteLength,
            int? year,
            IReadOnlyDictionary<string, HistoricalSourceFileInfo> filesByPath,
            ValidationCollector collector,
            out string actualHash)
        {
            string path = NormalizePath(expectedPath);
            actualHash = string.Empty;
            if (!filesByPath.TryGetValue(path, out HistoricalSourceFileInfo file))
            {
                collector.Add(
                    HistoricalValidationSeverity.Error,
                    "File",
                    year,
                    path,
                    "Manifest가 참조하는 Source 파일을 찾을 수 없습니다.",
                    HistoricalNavigationKind.File,
                    path);
                return false;
            }

            bool byteLengthMatches = file.ActualByteLength == expectedByteLength && file.IsByteLengthMatch;
            collector.Check(
                byteLengthMatches,
                "File",
                year,
                path,
                $"byteLength {file.ActualByteLength}가 일치합니다.",
                $"byteLength가 다릅니다. expected={expectedByteLength}, actual={file.ActualByteLength}",
                HistoricalNavigationKind.File,
                path);

            actualHash = file.ActualSha256 ?? string.Empty;
            bool hashMatches = file.IsHashMatch &&
                               string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);
            collector.Check(
                hashMatches,
                "Hash",
                year,
                path,
                "파일 SHA-256이 Manifest와 일치합니다.",
                $"파일 SHA-256이 다릅니다. expected={expectedHash}, actual={actualHash}",
                HistoricalNavigationKind.File,
                path);
            return byteLengthMatches && hashMatches;
        }

        private static string ComputeArchiveHash(IReadOnlyList<KeyValuePair<string, string>> entries)
        {
            var source = new StringBuilder(entries.Count * 100);
            source.Append('[');
            for (int index = 0; index < entries.Count; index++)
            {
                if (index > 0)
                    source.Append(',');
                source.Append("[\"");
                // Generator는 manifest에 기록한 상대 경로 문자열 자체를 Archive Hash에 포함한다.
                AppendJsonEscaped(source, entries[index].Key);
                source.Append("\",\"");
                AppendJsonEscaped(source, entries[index].Value);
                source.Append("\"]");
            }
            source.Append(']');
            return ComputeSha256Hex(Encoding.UTF8.GetBytes(source.ToString()));
        }

        private static void AppendJsonEscaped(StringBuilder builder, string value)
        {
            if (value == null)
                return;
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                switch (character)
                {
                    case '\\': builder.Append("\\\\"); break;
                    case '"': builder.Append("\\\""); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (character < 0x20)
                            builder.Append("\\u").Append(((int)character).ToString("x4"));
                        else
                            builder.Append(character);
                        break;
                }
            }
        }

        private static string ComputeSha256Hex(byte[] payload)
        {
            using SHA256 sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(payload);
            var text = new StringBuilder(hash.Length * 2);
            for (int index = 0; index < hash.Length; index++)
                text.Append(hash[index].ToString("x2"));
            return text.ToString();
        }

        private static bool IsSha256(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 64)
                return false;
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                bool isHex = character >= '0' && character <= '9' ||
                             character >= 'a' && character <= 'f' ||
                             character >= 'A' && character <= 'F';
                if (!isHex)
                    return false;
            }
            return true;
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : path.Trim().Replace('\\', '/');
        }

        private static void CheckCount(int expected, int actual, string name, ValidationCollector collector)
        {
            collector.Check(
                expected == actual,
                "Manifest Count",
                null,
                name,
                $"{name}: {actual}",
                $"{name}가 다릅니다. expected={expected}, actual={actual}",
                HistoricalNavigationKind.File,
                "manifest.json");
        }

        private static void CheckYearCount(
            int expected,
            int actual,
            int year,
            string name,
            ValidationCollector collector)
        {
            collector.Check(
                expected == actual,
                "Manifest Count",
                year,
                name,
                $"{name}: {actual}",
                $"{name}가 다릅니다. expected={expected}, actual={actual}",
                HistoricalNavigationKind.File,
                $"Years/{year}.json");
        }

        private static int CountPlayersByYear(IReadOnlyList<HistoricalPlayerRow> rows, int year)
        {
            int count = 0;
            if (rows == null) return count;
            for (int index = 0; index < rows.Count; index++)
                if (rows[index]?.Season?.OriginYear == year) count++;
            return count;
        }

        private static int CountTeamsByYear(IReadOnlyList<HistoricalTeamSeason> teams, int year)
        {
            int count = 0;
            if (teams == null) return count;
            for (int index = 0; index < teams.Count; index++)
                if (teams[index]?.OriginYear == year) count++;
            return count;
        }

        private static int CountRecordsByYear(IReadOnlyList<HistoricalSeasonRecord> records, int year)
        {
            int count = 0;
            if (records == null) return count;
            for (int index = 0; index < records.Count; index++)
                if (records[index]?.SeasonYear == year) count++;
            return count;
        }

        private static int CountCardsByYear(HistoricalArchiveData archive, int year)
        {
            int count = 0;
            if (archive.Cards == null) return count;
            for (int index = 0; index < archive.Cards.Count; index++)
            {
                HistoricalCard card = archive.Cards[index];
                if (card != null && archive.PlayersBySeasonId.TryGetValue(card.PlayerSeasonId, out HistoricalPlayerRow row) &&
                    row.Season.OriginYear == year)
                {
                    count++;
                }
            }
            return count;
        }

        private static int CountAwardsByYearAndType(
            IReadOnlyList<HistoricalAwardRecord> awards,
            int year,
            string awardType)
        {
            int count = 0;
            if (awards == null) return count;
            for (int index = 0; index < awards.Count; index++)
            {
                HistoricalAwardRecord award = awards[index];
                if (award != null && award.SeasonYear == year &&
                    string.Equals(award.AwardType, awardType, StringComparison.Ordinal))
                {
                    count++;
                }
            }
            return count;
        }
    }
}
