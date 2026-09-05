using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using UnityEngine;

namespace Baseball.Game.Historical
{
    /// <summary>Runtime Historical payload의 손상 위치를 논리 파일과 연도까지 보존한다.</summary>
    public sealed class HistoricalContentLoadException : InvalidOperationException
    {
        public HistoricalContentLoadException(
            string message,
            string relativePath = null,
            int? year = null,
            Exception innerException = null)
            : base(BuildMessage(message, relativePath, year), innerException)
        {
            RelativePath = relativePath ?? string.Empty;
            Year = year;
        }

        public string RelativePath { get; }
        public int? Year { get; }

        private static string BuildMessage(string message, string relativePath, int? year)
        {
            var result = new StringBuilder(message ?? "Historical Content Load에 실패했습니다.");
            if (!string.IsNullOrWhiteSpace(relativePath))
                result.Append(" file=").Append(relativePath);
            if (year.HasValue)
                result.Append(" year=").Append(year.Value.ToString(CultureInfo.InvariantCulture));
            return result.ToString();
        }
    }

    /// <summary>Runtime Historical payload를 얼마나 검증할지 정한다.</summary>
    public enum HistoricalContentVerificationMode
    {
        /// <summary>
        /// 스키마·버전·연도별 개수만 확인하고 44개 파일 전체 SHA-256과 Archive Hash는 건너뛴다.
        /// 저작 산출물의 무결성은 데이터 저작·CI가 책임지므로 게임 시작에서 다시 볼 이유가 없다.
        /// </summary>
        Fast = 0,

        /// <summary>파일별 SHA-256, Archive Hash, Runtime 안전 필드까지 전부 확인한다. 저작 도구와 검증 테스트용이다.</summary>
        Full = 1
    }

    /// <summary>Player Build TextAsset을 검증하고 고정 Definition으로 한 번만 역직렬화한다.</summary>
    public sealed class UnityHistoricalContentProvider : IHistoricalContentProvider
    {
        public const int SupportedAssetFormatVersion = 1;
        public const int MinimumSupportedContentSchemaVersion = 3;
        public const int SupportedContentSchemaVersion = 5;
        public const string SupportedReferenceDataVersion = "kbo-normalized-v3";
        public const int SupportedNormalizedSchemaVersion = 3;
        public const string SupportedNormalizedImporterVersion = "1.2.0";
        public const string SupportedAbilityFormulaVersion = "historical-ability-v5";
        public const string SupportedPositionRoleClassifierVersion = "season-position-role-v5";
        public const string SupportedRosterBuilderVersion = "ability-fit-core25-v3";
        public const string SupportedCostFormulaVersion = "historical-role-composite-v7";
        public const string SupportedDerivationBalanceVersion = "historical-derivation-balance-v10";
        public const string SupportedGeneratorVersion = "source-backed-runtime-bake-v2";
        public const string SupportedBalanceVersion = "historical-source-backed-v2";
        public const string SupportedNamePolicyVersion = "world-identity-name-pool-v1";
        public const string SupportedNameDataPolicy = "runtime-world-identity-pool-v3";

        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private readonly HistoricalRuntimeContentCatalog _catalog;
        private readonly HistoricalContentVerificationMode _verificationMode;
        private readonly object _loadLock = new object();
        private readonly AssetByteReader _byteReader = new AssetByteReader();
        private HistoricalBakedContent _cached;

        public UnityHistoricalContentProvider(
            HistoricalRuntimeContentCatalog catalog,
            HistoricalContentVerificationMode verificationMode = HistoricalContentVerificationMode.Fast)
        {
            _catalog = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));
            _verificationMode = verificationMode;
        }

        public HistoricalContentVerificationMode VerificationMode => _verificationMode;
        public int MaterializationCount { get; private set; }

        /// <summary>
        /// TextAsset 접근은 메인 스레드에서만 가능하다. 파싱을 워커 스레드로 옮기려면
        /// 원본 바이트를 먼저 이 메서드로 확보해야 한다. 반드시 메인 스레드에서 호출한다.
        /// </summary>
        public void CacheAssetBytesOnMainThread()
        {
            _byteReader.Cache(_catalog.Manifest);
            if (_catalog.PlayerPersons != null)
                _byteReader.Cache(_catalog.PlayerPersons.Content);
            IReadOnlyList<HistoricalRuntimeYearContentFile> years = _catalog.Years;
            for (int index = 0; index < years.Count; index++)
                _byteReader.Cache(years[index]?.File?.Content);
        }

        /// <summary>파싱이 끝난 뒤 원본 바이트 사본을 놓아준다.</summary>
        public void ReleaseAssetByteCache()
        {
            _byteReader.Clear();
        }

        public HistoricalBakedContent Load()
        {
            HistoricalBakedContent cached = _cached;
            if (cached != null)
                return cached;

            lock (_loadLock)
            {
                if (_cached != null)
                    return _cached;
                _cached = Materialize();
                MaterializationCount++;
                return _cached;
            }
        }

        private HistoricalBakedContent Materialize()
        {
            TextAsset manifestAsset = _catalog.Manifest;
            if (manifestAsset == null)
                throw new HistoricalContentLoadException("Runtime catalog에 manifest TextAsset이 없습니다.", "manifest.json");

            string manifestText = Decode(_byteReader.Read(manifestAsset), "manifest.json", null);
            HistoricalRuntimeManifestDto manifestDto = ParseJson<HistoricalRuntimeManifestDto>(
                manifestText,
                "manifest.json",
                null);
            ValidateSchemaManifestFieldPresence(manifestText, manifestDto.ContentSchemaVersion);
            HistoricalContentManifest manifest = BuildManifest(manifestDto);
            ValidateCatalogShape(manifestDto);

            VerifiedPayload personsPayload = VerifyPayload(
                _catalog.PlayerPersons,
                manifestDto.PlayerPersons,
                null,
                _verificationMode,
                _byteReader);
            HistoricalRuntimePlayerPersonArrayDto personPayload = ParsePersonArray(personsPayload);
            HistoricalRuntimePlayerPersonDto[] personDtos = personPayload.Items;
            ValidateCount(
                "PlayerPerson",
                manifestDto.PlayerPersons.Count,
                personDtos.Length,
                personsPayload.RelativePath,
                null);

            var persons = new PlayerPersonDefinition[personDtos.Length];
            for (int index = 0; index < personDtos.Length; index++)
                persons[index] = MapPerson(personDtos[index], personsPayload.RelativePath);

            WorldIdentityNameCatalog identityNames = CreateIdentityNames(
                manifestDto,
                personPayload,
                personsPayload);
            Dictionary<int, HistoricalRuntimeYearContentFile> catalogYears = IndexCatalogYears();

            if (_verificationMode == HistoricalContentVerificationMode.Full)
            {
                HistoricalYearContentDefinition[] years = MaterializeAllYearsVerified(
                    manifestDto,
                    catalogYears,
                    personsPayload,
                    persons.Length,
                    _byteReader);
                return new HistoricalBakedContent(manifest, persons, years, identityNames);
            }

            // 연도 하나가 평균 500KB이고 44개가 전부 필요한 화면은 역대 기록뿐이다.
            // 새 게임 시작은 한두 해만 읽으므로 나머지는 실제로 요구될 때 파싱한다.
            var yearSource = new LazyRuntimeYearContentSource(manifestDto, catalogYears, _byteReader);
            return new HistoricalBakedContent(manifest, persons, yearSource, identityNames);
        }

        private static HistoricalYearContentDefinition[] MaterializeAllYearsVerified(
            HistoricalRuntimeManifestDto manifestDto,
            Dictionary<int, HistoricalRuntimeYearContentFile> catalogYears,
            VerifiedPayload personsPayload,
            int personCount,
            AssetByteReader byteReader)
        {
            using var contentHashVerifier = new RuntimeContentHashVerifier(manifestDto);
            var archiveEntries = new List<KeyValuePair<string, string>>(manifestDto.Years.Length + 1);
            archiveEntries.Add(new KeyValuePair<string, string>(
                manifestDto.PlayerPersons.Path,
                personsPayload.Sha256));
            contentHashVerifier.AppendPlayerPersons(personsPayload.Bytes);

            var years = new HistoricalYearContentDefinition[manifestDto.Years.Length];
            int totalPlayerSeasons = 0;
            int totalTeamSeasons = 0;
            int totalCards = 0;
            int totalRecords = 0;
            int totalAwards = 0;
            for (int index = 0; index < manifestDto.Years.Length; index++)
            {
                HistoricalRuntimeYearEntryDto entry = ResolveYearEntry(manifestDto, index);
                HistoricalRuntimeYearContentFile catalogYear = ResolveCatalogYear(catalogYears, entry);
                VerifiedPayload payload = VerifyPayload(
                    catalogYear.File,
                    entry,
                    entry.Year,
                    HistoricalContentVerificationMode.Full,
                    byteReader);
                archiveEntries.Add(new KeyValuePair<string, string>(entry.Path, payload.Sha256));
                contentHashVerifier.AppendYear(payload.Bytes, index);
                HistoricalRuntimeYearContentDto yearDto = ParseJson<HistoricalRuntimeYearContentDto>(
                    payload.Text,
                    payload.RelativePath,
                    entry.Year);
                years[index] = MapYear(yearDto, entry, payload.RelativePath);
                totalPlayerSeasons = checked(totalPlayerSeasons + yearDto.PlayerSeasons.Length);
                totalTeamSeasons = checked(totalTeamSeasons + yearDto.TeamSeasons.Length);
                totalCards = checked(totalCards + yearDto.NormalCards.Length);
                totalRecords = checked(totalRecords + yearDto.OriginalSeasonRecords.Length);
                totalAwards = checked(totalAwards + yearDto.OriginalAwardRecords.Length);
            }

            contentHashVerifier.Validate(sourceManifestHash: manifestDto.SourceManifest.ContentHash);
            ValidateArchiveHash(manifestDto.AssetArchiveHash, archiveEntries);
            ValidateSummary(
                manifestDto,
                years.Length,
                personCount,
                totalPlayerSeasons,
                totalTeamSeasons,
                totalCards,
                totalRecords,
                totalAwards);
            return years;
        }

        private static WorldIdentityNameCatalog CreateIdentityNames(
            HistoricalRuntimeManifestDto manifestDto,
            HistoricalRuntimePlayerPersonArrayDto personPayload,
            VerifiedPayload personsPayload)
        {
            HistoricalRuntimeWorldIdentityNamePoolDto identityPool = personPayload.WorldIdentityNamePool;
            if (manifestDto.ContentSchemaVersion >= 5 && identityPool == null)
            {
                throw new HistoricalContentLoadException(
                    "Content Schema v5에는 worldIdentityNamePool이 필요합니다.",
                    personsPayload.RelativePath);
            }
            if (identityPool == null)
                return null;
            ValidateVersion(
                "worldIdentityNamePool.version",
                SupportedNamePolicyVersion,
                identityPool.Version);
            return new WorldIdentityNameCatalog(
                identityPool.DomesticPlayerNames,
                identityPool.ForeignPlayerNames,
                identityPool.FranchiseNames);
        }

        private static HistoricalRuntimeYearEntryDto ResolveYearEntry(
            HistoricalRuntimeManifestDto manifestDto,
            int index)
        {
            return manifestDto.Years[index]
                ?? throw new HistoricalContentLoadException("Manifest에 null 연도 항목이 있습니다.", "manifest.json");
        }

        private static HistoricalRuntimeYearContentFile ResolveCatalogYear(
            Dictionary<int, HistoricalRuntimeYearContentFile> catalogYears,
            HistoricalRuntimeYearEntryDto entry)
        {
            if (!catalogYears.TryGetValue(entry.Year, out HistoricalRuntimeYearContentFile catalogYear))
            {
                throw new HistoricalContentLoadException(
                    "Runtime catalog에 manifest 연도 파일이 없습니다.",
                    entry.Path,
                    entry.Year);
            }
            return catalogYear;
        }

        /// <summary>
        /// 요청받은 연도 하나만 TextAsset에서 읽어 파싱한다.
        /// 연도별 개수 검증(manifest entry 대조)은 그대로 수행하므로 그 연도의 정합성은 여전히 보장된다.
        /// </summary>
        private sealed class LazyRuntimeYearContentSource : IHistoricalYearContentSource
        {
            private readonly Dictionary<int, HistoricalRuntimeYearEntryDto> _entriesByYear;
            private readonly Dictionary<int, HistoricalRuntimeYearContentFile> _catalogYears;
            private readonly AssetByteReader _byteReader;
            private readonly int[] _years;

            public LazyRuntimeYearContentSource(
                HistoricalRuntimeManifestDto manifestDto,
                Dictionary<int, HistoricalRuntimeYearContentFile> catalogYears,
                AssetByteReader byteReader)
            {
                _catalogYears = catalogYears;
                _byteReader = byteReader;
                _entriesByYear = new Dictionary<int, HistoricalRuntimeYearEntryDto>(manifestDto.Years.Length);
                for (int index = 0; index < manifestDto.Years.Length; index++)
                {
                    HistoricalRuntimeYearEntryDto entry = ResolveYearEntry(manifestDto, index);
                    if (!_entriesByYear.TryAdd(entry.Year, entry))
                    {
                        throw new HistoricalContentLoadException(
                            "Manifest에 같은 연도가 중복되었습니다.",
                            entry.Path,
                            entry.Year);
                    }
                }
                _years = new int[_entriesByYear.Count];
                _entriesByYear.Keys.CopyTo(_years, 0);
                Array.Sort(_years);
            }

            public IReadOnlyList<int> Years => _years;

            public HistoricalYearContentDefinition Load(int year)
            {
                if (!_entriesByYear.TryGetValue(year, out HistoricalRuntimeYearEntryDto entry))
                    throw new HistoricalContentLoadException("Manifest에 없는 연도입니다.", null, year);
                HistoricalRuntimeYearContentFile catalogYear = ResolveCatalogYear(_catalogYears, entry);
                VerifiedPayload payload = VerifyPayload(
                    catalogYear.File,
                    entry,
                    entry.Year,
                    HistoricalContentVerificationMode.Fast,
                    _byteReader);
                HistoricalRuntimeYearContentDto yearDto = ParseJson<HistoricalRuntimeYearContentDto>(
                    payload.Text,
                    payload.RelativePath,
                    entry.Year);
                return MapYear(yearDto, entry, payload.RelativePath);
            }
        }

        private static HistoricalContentManifest BuildManifest(HistoricalRuntimeManifestDto source)
        {
            if (source.AssetFormatVersion != SupportedAssetFormatVersion)
            {
                throw new HistoricalContentLoadException(
                    $"지원하지 않는 Asset Format입니다. expected={SupportedAssetFormatVersion}, actual={source.AssetFormatVersion}",
                    "manifest.json");
            }
            if (source.ContentSchemaVersion < MinimumSupportedContentSchemaVersion ||
                source.ContentSchemaVersion > SupportedContentSchemaVersion)
            {
                throw new HistoricalContentLoadException(
                    $"지원하지 않는 Content Schema입니다. supported={MinimumSupportedContentSchemaVersion}~{SupportedContentSchemaVersion}, actual={source.ContentSchemaVersion}",
                    "manifest.json");
            }
            if (!IsSha256(source.AssetArchiveHash))
                throw new HistoricalContentLoadException("assetArchiveHash가 64자리 SHA-256이 아닙니다.", "manifest.json");

            HistoricalRuntimeSourceManifestDto sourceManifest = source.SourceManifest;
            if (sourceManifest == null)
                throw new HistoricalContentLoadException("sourceManifest가 없습니다.", "manifest.json");
            ValidateVersion(
                "referenceDataVersion",
                SupportedReferenceDataVersion,
                sourceManifest.ReferenceDataVersion);
            ValidateVersion(
                "normalizedImporterVersion",
                SupportedNormalizedImporterVersion,
                sourceManifest.NormalizedImporterVersion);
            ValidateVersion(
                "abilityFormulaVersion",
                SupportedAbilityFormulaVersion,
                sourceManifest.AbilityFormulaVersion);
            ValidateVersion(
                "positionRoleClassifierVersion",
                SupportedPositionRoleClassifierVersion,
                sourceManifest.PositionRoleClassifierVersion);
            ValidateVersion(
                "rosterBuilderVersion",
                SupportedRosterBuilderVersion,
                sourceManifest.RosterBuilderVersion);
            ValidateVersion(
                "costFormulaVersion",
                SupportedCostFormulaVersion,
                sourceManifest.CostFormulaVersion);
            ValidateVersion(
                "derivationBalanceVersion",
                SupportedDerivationBalanceVersion,
                sourceManifest.DerivationBalanceVersion);
            if (sourceManifest.NormalizedSchemaVersion != SupportedNormalizedSchemaVersion)
            {
                throw new HistoricalContentLoadException(
                    $"DERIVED_CACHE_VERSION_MISMATCH: normalizedSchemaVersion expected={SupportedNormalizedSchemaVersion}, actual={sourceManifest.NormalizedSchemaVersion}",
                    "manifest.json");
            }
            if (!IsSha256(sourceManifest.RawDataVersion))
                throw new HistoricalContentLoadException("DERIVED_CACHE_VERSION_MISMATCH: rawDataVersion이 SHA-256이 아닙니다.", "manifest.json");
            if (!IsSha256(sourceManifest.NormalizedContentHash))
                throw new HistoricalContentLoadException("DERIVED_CACHE_VERSION_MISMATCH: normalizedContentHash가 SHA-256이 아닙니다.", "manifest.json");
            ValidateVersion(
                "generatorVersion",
                SupportedGeneratorVersion,
                sourceManifest.GeneratorVersion);
            ValidateVersion(
                "balanceVersion",
                SupportedBalanceVersion,
                sourceManifest.BalanceVersion);
            ValidateVersion(
                "namePolicyVersion",
                SupportedNamePolicyVersion,
                sourceManifest.NamePolicyVersion);
            ValidateVersion(
                "nameDataPolicy",
                SupportedNameDataPolicy,
                sourceManifest.NameDataPolicy);
            if (source.ContentSchemaVersion >= 4)
            {
                ValidateRequiredVersion(
                    "sourceIdentityPolicyVersion",
                    sourceManifest.SourceIdentityPolicyVersion);
                ValidateRequiredVersion(
                    "sourceAllocationPolicyVersion",
                    sourceManifest.SourceAllocationPolicyVersion);
                ValidateRequiredVersion(
                    "replacementGeneratorVersion",
                    sourceManifest.ReplacementGeneratorVersion);
                ValidateRequiredVersion(
                    "replacementPopulationPolicyVersion",
                    sourceManifest.ReplacementPopulationPolicyVersion);
                ValidateNonNegativeManifestCount(
                    "sourceBackedPlayerPersonCount",
                    sourceManifest.SourceBackedPlayerPersonCount);
                ValidateNonNegativeManifestCount(
                    "sourceBackedPlayerSeasonCount",
                    sourceManifest.SourceBackedPlayerSeasonCount);
                ValidateNonNegativeManifestCount(
                    "replacementGeneratedPlayerPersonCount",
                    sourceManifest.ReplacementGeneratedPlayerPersonCount);
                ValidateNonNegativeManifestCount(
                    "replacementGeneratedPlayerSeasonCount",
                    sourceManifest.ReplacementGeneratedPlayerSeasonCount);
            }
            if (source.ContentSchemaVersion >= 5)
            {
                ValidateRequiredVersion(
                    "sourceFranchiseIdentityPolicyVersion",
                    sourceManifest.SourceFranchiseIdentityPolicyVersion);
                ValidateRequiredVersion(
                    "sourceTeamSeasonIdentityPolicyVersion",
                    sourceManifest.SourceTeamSeasonIdentityPolicyVersion);
                if (sourceManifest.GenerationSeedAffectsCanonicalBake)
                {
                    throw new HistoricalContentLoadException(
                        "Canonical Bake는 generationSeed에 의존할 수 없습니다.",
                        "manifest.json");
                }
            }
            if (sourceManifest.GenerationSeed < 0)
                throw new HistoricalContentLoadException("generationSeed는 음수일 수 없습니다.", "manifest.json");
            if (!IsSha256(sourceManifest.ContentHash))
                throw new HistoricalContentLoadException("contentHash가 64자리 SHA-256이 아닙니다.", "manifest.json");

            var runtimeSource = new HistoricalSourceContentManifest(
                sourceManifest.ReferenceDataVersion,
                sourceManifest.GeneratorVersion,
                sourceManifest.BalanceVersion,
                (ulong)sourceManifest.GenerationSeed,
                sourceManifest.ContentHash,
                sourceManifest.SourceIdentityPolicyVersion,
                sourceManifest.SourceAllocationPolicyVersion,
                sourceManifest.ReplacementGeneratorVersion,
                sourceManifest.ReplacementPopulationPolicyVersion,
                sourceManifest.SourceBackedPlayerPersonCount,
                sourceManifest.SourceBackedPlayerSeasonCount,
                sourceManifest.ReplacementGeneratedPlayerPersonCount,
                sourceManifest.ReplacementGeneratedPlayerSeasonCount,
                sourceManifest.GenerationSeedAffectsCanonicalBake,
                sourceManifest.SourceFranchiseIdentityPolicyVersion,
                sourceManifest.SourceTeamSeasonIdentityPolicyVersion);
            return new HistoricalContentManifest(
                source.AssetFormatVersion,
                source.ContentSchemaVersion,
                source.AssetArchiveHash,
                runtimeSource,
                sourceManifest.NamePolicyVersion,
                sourceManifest.NameDataPolicy,
                sourceManifest.RawDataVersion,
                sourceManifest.NormalizedSchemaVersion,
                sourceManifest.NormalizedImporterVersion,
                sourceManifest.NormalizedContentHash,
                sourceManifest.AbilityFormulaVersion,
                sourceManifest.PositionRoleClassifierVersion,
                sourceManifest.RosterBuilderVersion,
                sourceManifest.CostFormulaVersion,
                sourceManifest.DerivationBalanceVersion);
        }

        private void ValidateCatalogShape(HistoricalRuntimeManifestDto manifest)
        {
            if (manifest.PlayerPersons == null)
                throw new HistoricalContentLoadException("Manifest에 playerPersons 항목이 없습니다.", "manifest.json");
            if (manifest.Summary == null)
                throw new HistoricalContentLoadException("Manifest에 summary 항목이 없습니다.", "manifest.json");
            if (manifest.Years.Length == 0)
                throw new HistoricalContentLoadException("Manifest에 연도 항목이 없습니다.", "manifest.json");
            if (_catalog.PlayerPersons == null)
                throw new HistoricalContentLoadException("Runtime catalog에 PlayerPerson payload가 없습니다.", manifest.PlayerPersons.Path);
            if (_catalog.Years.Count != manifest.Years.Length)
            {
                throw new HistoricalContentLoadException(
                    $"Runtime catalog 연도 파일 수가 manifest와 다릅니다. expected={manifest.Years.Length}, actual={_catalog.Years.Count}",
                    "manifest.json");
            }
        }

        private Dictionary<int, HistoricalRuntimeYearContentFile> IndexCatalogYears()
        {
            var result = new Dictionary<int, HistoricalRuntimeYearContentFile>(_catalog.Years.Count);
            for (int index = 0; index < _catalog.Years.Count; index++)
            {
                HistoricalRuntimeYearContentFile item = _catalog.Years[index];
                if (item == null || item.File == null)
                    throw new HistoricalContentLoadException("Runtime catalog에 null 연도 payload가 있습니다.");
                if (!result.TryAdd(item.Year, item))
                {
                    throw new HistoricalContentLoadException(
                        "Runtime catalog에 같은 연도가 중복되었습니다.",
                        item.File.RelativePath,
                        item.Year);
                }
            }
            return result;
        }

        private static VerifiedPayload VerifyPayload(
            HistoricalRuntimeContentFile catalogFile,
            HistoricalRuntimeFileEntryDto manifestEntry,
            int? year,
            HistoricalContentVerificationMode verificationMode,
            AssetByteReader byteReader)
        {
            if (catalogFile == null)
                throw new HistoricalContentLoadException("Runtime catalog 파일 참조가 없습니다.", manifestEntry?.Path, year);
            return VerifyPayload(
                catalogFile,
                manifestEntry?.Path,
                manifestEntry?.Sha256,
                manifestEntry?.ByteLength ?? -1,
                year,
                verificationMode,
                byteReader);
        }

        private static VerifiedPayload VerifyPayload(
            HistoricalRuntimeContentFile catalogFile,
            HistoricalRuntimeYearEntryDto manifestEntry,
            int year,
            HistoricalContentVerificationMode verificationMode,
            AssetByteReader byteReader)
        {
            if (catalogFile == null)
                throw new HistoricalContentLoadException("Runtime catalog 연도 파일 참조가 없습니다.", manifestEntry?.Path, year);
            return VerifyPayload(
                catalogFile,
                manifestEntry?.Path,
                manifestEntry?.Sha256,
                manifestEntry?.ByteLength ?? -1,
                year,
                verificationMode,
                byteReader);
        }

        private static VerifiedPayload VerifyPayload(
            HistoricalRuntimeContentFile catalogFile,
            string expectedPath,
            string expectedHash,
            long expectedByteLength,
            int? year,
            HistoricalContentVerificationMode verificationMode,
            AssetByteReader byteReader)
        {
            string path = expectedPath ?? string.Empty;
            ValidateLogicalPath(path, year);
            if (!string.Equals(catalogFile.RelativePath, path, StringComparison.Ordinal))
            {
                throw new HistoricalContentLoadException(
                    $"Runtime catalog 논리 경로가 manifest와 다릅니다. catalog={catalogFile.RelativePath}",
                    path,
                    year);
            }
            if (catalogFile.Content == null)
                throw new HistoricalContentLoadException("Runtime TextAsset이 없습니다.", path, year);

            byte[] bytes = byteReader.Read(catalogFile.Content);
            if (bytes.LongLength != expectedByteLength)
            {
                throw new HistoricalContentLoadException(
                    $"파일 크기가 다릅니다. expected={expectedByteLength}, actual={bytes.LongLength}",
                    path,
                    year);
            }

            // Fast에서는 23MB에 대한 SHA-256 재계산과 23MB 문자열 스캔을 건너뛴다.
            // 둘 다 Bake 산출물의 저작 무결성 검사이며, Full 모드 검증 테스트가 계속 수행한다.
            string actualHash = string.Empty;
            if (verificationMode == HistoricalContentVerificationMode.Full)
            {
                actualHash = ComputeSha256Hex(bytes);
                if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                {
                    throw new HistoricalContentLoadException(
                        $"파일 SHA-256이 다릅니다. expected={expectedHash}, actual={actualHash}",
                        path,
                        year);
                }
            }

            string text = Decode(bytes, path, year);
            if (verificationMode == HistoricalContentVerificationMode.Full)
                ValidateRuntimeSafePayloadText(text, path, year);
            return new VerifiedPayload(path, actualHash, text, bytes);
        }

        private static void ValidateRuntimeSafePayloadText(string text, string relativePath, int? year)
        {
            if (text.IndexOf("\"originalName\"", StringComparison.Ordinal) >= 0 ||
                text.IndexOf("\"sourceReferenceNames\"", StringComparison.Ordinal) >= 0)
            {
                throw new HistoricalContentLoadException(
                    "Editor 전용 원본 이름 필드가 Runtime payload에 포함되어 있습니다.",
                    relativePath,
                    year);
            }
        }

        private static HistoricalRuntimePlayerPersonArrayDto ParsePersonArray(VerifiedPayload payload)
        {
            string trimmed = payload.Text.TrimStart();
            if (trimmed.Length == 0 || (trimmed[0] != '[' && trimmed[0] != '{'))
                throw new HistoricalContentLoadException("PlayerPerson JSON Root 형식이 올바르지 않습니다.", payload.RelativePath);
            string json = trimmed[0] == '[' ? "{\"items\":" + payload.Text + "}" : payload.Text;
            return ParseJson<HistoricalRuntimePlayerPersonArrayDto>(
                json,
                payload.RelativePath,
                null);
        }

        private static HistoricalYearContentDefinition MapYear(
            HistoricalRuntimeYearContentDto source,
            HistoricalRuntimeYearEntryDto manifestEntry,
            string relativePath)
        {
            int year = manifestEntry.Year;
            if (source.Year != year)
            {
                throw new HistoricalContentLoadException(
                    $"연도 JSON 값이 manifest와 다릅니다. expected={year}, actual={source.Year}",
                    relativePath,
                    year);
            }

            ValidateCount("PlayerSeason", manifestEntry.PlayerSeasonCount, source.PlayerSeasons.Length, relativePath, year);
            ValidateCount("TeamSeason", manifestEntry.TeamSeasonCount, source.TeamSeasons.Length, relativePath, year);
            ValidateCount("Normal Card", manifestEntry.NormalCardCount, source.NormalCards.Length, relativePath, year);
            ValidateCount("Original Record", manifestEntry.OriginalRecordCount, source.OriginalSeasonRecords.Length, relativePath, year);
            ValidateAwardCount(source.OriginalAwardRecords, manifestEntry, relativePath);

            var seasons = new PlayerSeasonDefinition[source.PlayerSeasons.Length];
            var seasonsById = new Dictionary<string, PlayerSeasonDefinition>(
                source.PlayerSeasons.Length,
                StringComparer.Ordinal);
            for (int index = 0; index < seasons.Length; index++)
            {
                seasons[index] = MapPlayerSeason(source.PlayerSeasons[index], relativePath, year);
                if (!seasonsById.TryAdd(seasons[index].PlayerSeasonId, seasons[index]))
                {
                    throw new HistoricalContentLoadException(
                        $"중복 PlayerSeasonId입니다. id={seasons[index].PlayerSeasonId}",
                        relativePath,
                        year);
                }
            }
            var cards = new PlayerCardDefinition[source.NormalCards.Length];
            for (int index = 0; index < cards.Length; index++)
                cards[index] = MapCard(source.NormalCards[index], relativePath, year);
            var teams = new TeamSeasonDefinition[source.TeamSeasons.Length];
            for (int index = 0; index < teams.Length; index++)
                teams[index] = MapTeam(source.TeamSeasons[index], relativePath, year);
            var records = new OriginalSeasonRecordDefinition[source.OriginalSeasonRecords.Length];
            for (int index = 0; index < records.Length; index++)
                records[index] = MapRecord(source.OriginalSeasonRecords[index], relativePath, year);
            var awards = new OriginalAwardRecordDefinition[source.OriginalAwardRecords.Length];
            for (int index = 0; index < awards.Length; index++)
                awards[index] = MapAward(source.OriginalAwardRecords[index], seasonsById, relativePath, year);
            return new HistoricalYearContentDefinition(year, seasons, cards, teams, records, awards);
        }

        private static PlayerPersonDefinition MapPerson(
            HistoricalRuntimePlayerPersonDto source,
            string relativePath)
        {
            if (source == null)
                throw new HistoricalContentLoadException("null PlayerPerson 항목이 있습니다.", relativePath);
            try
            {
                return new PlayerPersonDefinition(
                    source.PlayerPersonId,
                    source.BirthYear,
                    ParseHandedness(source.Bats, "bats"),
                    ParseHandedness(source.Throws, "throws"),
                    ParsePosition(source.PrimaryPosition, PitcherRole.Starter),
                    ParseRegistrationType(source.RegistrationType),
                    source.CareerStartYear,
                    source.CareerEndYear,
                    new PersonPotentialTrait(source.PersonPotentialTrait));
            }
            catch (Exception exception)
            {
                throw new HistoricalContentLoadException(
                    $"PlayerPerson {source.PlayerPersonId}를 Definition으로 변환하지 못했습니다.",
                    relativePath,
                    null,
                    exception);
            }
        }

        private static PlayerSeasonDefinition MapPlayerSeason(
            HistoricalRuntimePlayerSeasonDto source,
            string relativePath,
            int year)
        {
            if (source == null)
                throw new HistoricalContentLoadException("null PlayerSeason 항목이 있습니다.", relativePath, year);
            try
            {
                PlayerType playerType = ParsePlayerType(source.PlayerType);
                PitcherRole pitcherRole = ParsePitcherRole(source.PitcherRole, playerType);
                return new PlayerSeasonDefinition(
                    source.PlayerSeasonId,
                    source.PlayerPersonId,
                    source.OriginYear,
                    source.OriginFranchiseId,
                    source.OriginTeamSeasonKey,
                    ParsePosition(source.Position, pitcherRole),
                    pitcherRole,
                    playerType,
                    ParseRegistrationType(source.RegistrationType),
                    new AbilityRatings(source.BaseAttributes),
                    source.Cost,
                    new AbilityRatings(source.TrainingCeiling),
                    ParsePlayerDataProvenance(source.DataProvenance),
                    ParsePitcherRoleConfidence(source.PitcherRoleConfidence));
            }
            catch (Exception exception)
            {
                throw new HistoricalContentLoadException(
                    $"PlayerSeason {source.PlayerSeasonId}를 Definition으로 변환하지 못했습니다.",
                    relativePath,
                    year,
                    exception);
            }
        }

        private static PlayerCardDefinition MapCard(
            HistoricalRuntimeCardDto source,
            string relativePath,
            int year)
        {
            if (source == null)
                throw new HistoricalContentLoadException("null Normal Card 항목이 있습니다.", relativePath, year);
            if (!string.Equals(source.Edition, "Normal", StringComparison.Ordinal))
            {
                throw new HistoricalContentLoadException(
                    $"Offline 기본 카드의 Edition은 Normal이어야 합니다. actual={source.Edition}",
                    relativePath,
                    year);
            }
            try
            {
                return new PlayerCardDefinition(
                    source.CardId,
                    source.PlayerSeasonId,
                    PlayerCardEdition.Normal,
                    source.EditionStatModifiers);
            }
            catch (Exception exception)
            {
                throw new HistoricalContentLoadException(
                    $"Normal Card {source.CardId}를 Definition으로 변환하지 못했습니다.",
                    relativePath,
                    year,
                    exception);
            }
        }

        private static TeamSeasonDefinition MapTeam(
            HistoricalRuntimeTeamSeasonDto source,
            string relativePath,
            int year)
        {
            if (source == null)
                throw new HistoricalContentLoadException("null TeamSeason 항목이 있습니다.", relativePath, year);
            try
            {
                return new TeamSeasonDefinition(
                    source.TeamSeasonKey,
                    source.FranchiseId,
                    source.OriginYear,
                    source.AllNormalCardIds,
                    source.Core25CardIds,
                    source.ReferenceStrength);
            }
            catch (Exception exception)
            {
                throw new HistoricalContentLoadException(
                    $"TeamSeason {source.TeamSeasonKey}를 Definition으로 변환하지 못했습니다.",
                    relativePath,
                    year,
                    exception);
            }
        }

        private static OriginalSeasonRecordDefinition MapRecord(
            HistoricalRuntimeSeasonRecordDto source,
            string relativePath,
            int year)
        {
            if (source == null)
                throw new HistoricalContentLoadException("null Original Record 항목이 있습니다.", relativePath, year);
            try
            {
                return new OriginalSeasonRecordDefinition(new SeasonStatistics(
                    source.PlayerSeasonId,
                    source.TeamSeasonKey,
                    source.SeasonYear,
                    ParsePosition(source.Position, PitcherRole.Starter),
                    source.PlateAppearances,
                    source.Hits,
                    source.HomeRuns,
                    source.Walks,
                    source.Strikeouts,
                    stolenBases: 0,
                    source.PitchingOuts,
                    source.EarnedRuns,
                    source.PitchingStrikeouts,
                    source.DefensiveChances,
                    defensiveOutsAboveAverage: 0,
                    source.FieldingErrors));
            }
            catch (Exception exception)
            {
                throw new HistoricalContentLoadException(
                    $"Original Record {source.PlayerSeasonId}를 Definition으로 변환하지 못했습니다.",
                    relativePath,
                    year,
                    exception);
            }
        }

        private static OriginalAwardRecordDefinition MapAward(
            HistoricalRuntimeAwardDto source,
            IReadOnlyDictionary<string, PlayerSeasonDefinition> seasonsById,
            string relativePath,
            int year)
        {
            if (source == null)
                throw new HistoricalContentLoadException("null Original Award 항목이 있습니다.", relativePath, year);
            try
            {
                if (!seasonsById.TryGetValue(source.PlayerSeasonId, out PlayerSeasonDefinition playerSeason))
                {
                    throw new HistoricalContentLoadException(
                        $"Original Award가 알 수 없는 PlayerSeason을 참조합니다. id={source.PlayerSeasonId}",
                        relativePath,
                        year);
                }
                return new OriginalAwardRecordDefinition(new WorldAwardEntry(
                    source.SeasonYear,
                    ParseAwardType(source.AwardType),
                    source.PlayerSeasonId,
                    ResolveAwardPosition(source.Position, playerSeason)));
            }
            catch (Exception exception)
            {
                throw new HistoricalContentLoadException(
                    $"Original Award {source.PlayerSeasonId}/{source.AwardType}를 Definition으로 변환하지 못했습니다.",
                    relativePath,
                    year,
                    exception);
            }
        }

        private static PlayerPosition ResolveAwardPosition(
            string awardPosition,
            PlayerSeasonDefinition playerSeason)
        {
            bool isPitcherAward = string.Equals(awardPosition, "P", StringComparison.Ordinal);
            bool isPitcherSeason = playerSeason.PlayerType == PlayerType.Pitcher;
            if (isPitcherAward != isPitcherSeason)
            {
                throw new HistoricalContentLoadException(
                    $"Award Position의 선수 유형이 PlayerSeason과 호환되지 않습니다. " +
                    $"playerSeasonId={playerSeason.PlayerSeasonId}, award={awardPosition}, " +
                    $"playerType={playerSeason.PlayerType}");
            }

            if (isPitcherAward)
                return playerSeason.Position;

            // 구시대 수비 기록 결측과 멀티포지션 시즌에서는 수상 부문의 포지션이
            // NaturalPosition보다 직접적인 독립 증거다. OF만 세부 슬롯이 없으므로
            // 자연 외야 포지션을 보존하고, 그마저 없을 때는 중립 대표 슬롯 CF로 둔다.
            if (string.Equals(awardPosition, "OF", StringComparison.Ordinal))
            {
                return playerSeason.Position switch
                {
                    PlayerPosition.LeftField => PlayerPosition.LeftField,
                    PlayerPosition.CenterField => PlayerPosition.CenterField,
                    PlayerPosition.RightField => PlayerPosition.RightField,
                    _ => PlayerPosition.CenterField
                };
            }
            return ParsePosition(awardPosition, playerSeason.PitcherRole);
        }

        private static void ValidateAwardCount(
            IReadOnlyList<HistoricalRuntimeAwardDto> awards,
            HistoricalRuntimeYearEntryDto entry,
            string relativePath)
        {
            int allStarCount = 0;
            int goldenGloveCount = 0;
            for (int index = 0; index < awards.Count; index++)
            {
                HistoricalRuntimeAwardDto award = awards[index];
                if (award == null)
                    throw new HistoricalContentLoadException("null Original Award 항목이 있습니다.", relativePath, entry.Year);
                if (string.Equals(award.AwardType, "AllStar", StringComparison.Ordinal))
                    allStarCount++;
                else if (string.Equals(award.AwardType, "GoldenGlove", StringComparison.Ordinal))
                    goldenGloveCount++;
            }
            ValidateCount("AllStar Award", entry.AllStarCount, allStarCount, relativePath, entry.Year);
            ValidateCount("GoldenGlove Award", entry.GoldenGloveCount, goldenGloveCount, relativePath, entry.Year);
        }

        private static void ValidateSummary(
            HistoricalRuntimeManifestDto manifest,
            int years,
            int persons,
            int seasons,
            int teams,
            int cards,
            int records,
            int awards)
        {
            HistoricalRuntimeSummaryDto summary = manifest.Summary;
            if (summary == null)
                throw new HistoricalContentLoadException("Manifest summary가 없습니다.", "manifest.json");
            ValidateCount("Year summary", summary.YearCount, years, "manifest.json", null);
            ValidateCount("PlayerPerson summary", summary.PlayerPersonCount, persons, "manifest.json", null);
            ValidateCount("PlayerSeason summary", summary.PlayerSeasonCount, seasons, "manifest.json", null);
            ValidateCount("TeamSeason summary", summary.TeamSeasonCount, teams, "manifest.json", null);
            ValidateCount("Normal Card summary", summary.NormalCardCount, cards, "manifest.json", null);
            ValidateCount("Original Record summary", summary.OriginalRecordCount, records, "manifest.json", null);
            ValidateCount("Original Award summary", summary.OriginalAwardCount, awards, "manifest.json", null);
            if (manifest.ContentSchemaVersion >= 4)
            {
                HistoricalRuntimeSourceManifestDto source = manifest.SourceManifest;
                ValidateCount(
                    "Source/Replacement PlayerPerson manifest",
                    checked(source.SourceBackedPlayerPersonCount + source.ReplacementGeneratedPlayerPersonCount),
                    persons,
                    "manifest.json",
                    null);
                ValidateCount(
                    "Source/Replacement PlayerSeason manifest",
                    checked(source.SourceBackedPlayerSeasonCount + source.ReplacementGeneratedPlayerSeasonCount),
                    seasons,
                    "manifest.json",
                    null);
            }
        }

        private static void ValidateArchiveHash(
            string expectedHash,
            IReadOnlyList<KeyValuePair<string, string>> entries)
        {
            var source = new StringBuilder(entries.Count * 100);
            source.Append('[');
            for (int index = 0; index < entries.Count; index++)
            {
                if (index > 0)
                    source.Append(',');
                source.Append("[\"");
                AppendJsonEscaped(source, entries[index].Key);
                source.Append("\",\"");
                AppendJsonEscaped(source, entries[index].Value);
                source.Append("\"]");
            }
            source.Append(']');
            string actualHash = ComputeSha256Hex(Encoding.UTF8.GetBytes(source.ToString()));
            if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new HistoricalContentLoadException(
                    $"Asset Archive Hash가 다릅니다. expected={expectedHash}, actual={actualHash}",
                    "manifest.json");
            }
        }

        private static void AppendJsonEscaped(StringBuilder builder, string value)
        {
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
                            builder.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            builder.Append(character);
                        break;
                }
            }
        }

        private static void ValidateCount(
            string entity,
            int expected,
            int actual,
            string relativePath,
            int? year)
        {
            if (expected != actual)
            {
                throw new HistoricalContentLoadException(
                    $"{entity} 수가 manifest와 다릅니다. expected={expected}, actual={actual}",
                    relativePath,
                    year);
            }
        }

        private static void ValidateVersion(string field, string expected, string actual)
        {
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
            {
                throw new HistoricalContentLoadException(
                    $"지원하지 않는 {field}입니다. expected={expected}, actual={actual}",
                    "manifest.json");
            }
        }

        private static void ValidateRequiredVersion(string field, string actual)
        {
            if (string.IsNullOrWhiteSpace(actual))
            {
                throw new HistoricalContentLoadException(
                    $"Content Schema v4의 {field}은 비어 있을 수 없습니다.",
                    "manifest.json");
            }
        }

        private static void ValidateNonNegativeManifestCount(string field, int count)
        {
            if (count < 0)
            {
                throw new HistoricalContentLoadException(
                    $"Content Schema v4의 {field}은 음수일 수 없습니다. actual={count}",
                    "manifest.json");
            }
        }

        private static void ValidateSchemaManifestFieldPresence(string json, int contentSchemaVersion)
        {
            if (contentSchemaVersion < 4 || contentSchemaVersion > SupportedContentSchemaVersion)
                return;

            const string sourceManifestProperty = "\"sourceManifest\"";
            int propertyIndex = json.IndexOf(sourceManifestProperty, StringComparison.Ordinal);
            int objectStart = propertyIndex < 0 ? -1 : json.IndexOf('{', propertyIndex);
            int objectEnd = FindJsonObjectEnd(json, objectStart);
            if (objectStart < 0 || objectEnd <= objectStart)
            {
                throw new HistoricalContentLoadException(
                    "Content Schema v4의 sourceManifest JSON을 찾을 수 없습니다.",
                    "manifest.json");
            }

            string[] requiredFields =
            {
                "sourceIdentityPolicyVersion",
                "sourceAllocationPolicyVersion",
                "replacementGeneratorVersion",
                "replacementPopulationPolicyVersion",
                "sourceBackedPlayerPersonCount",
                "sourceBackedPlayerSeasonCount",
                "replacementGeneratedPlayerPersonCount",
                "replacementGeneratedPlayerSeasonCount"
            };
            int length = objectEnd - objectStart;
            for (int index = 0; index < requiredFields.Length; index++)
            {
                string property = $"\"{requiredFields[index]}\"";
                if (json.IndexOf(property, objectStart, length, StringComparison.Ordinal) < 0)
                {
                    throw new HistoricalContentLoadException(
                        $"Content Schema v4의 sourceManifest에 {requiredFields[index]} 필드가 없습니다.",
                        "manifest.json");
                }
            }
            if (contentSchemaVersion >= 5)
            {
                string[] canonicalFields =
                {
                    "generationSeedAffectsCanonicalBake",
                    "sourceFranchiseIdentityPolicyVersion",
                    "sourceTeamSeasonIdentityPolicyVersion"
                };
                for (int index = 0; index < canonicalFields.Length; index++)
                {
                    string property = $"\"{canonicalFields[index]}\"";
                    if (json.IndexOf(property, objectStart, length, StringComparison.Ordinal) < 0)
                    {
                        throw new HistoricalContentLoadException(
                            $"Content Schema v5의 sourceManifest에 {canonicalFields[index]} 필드가 없습니다.",
                            "manifest.json");
                    }
                }
            }
        }

        private static int FindJsonObjectEnd(string json, int objectStart)
        {
            if (objectStart < 0 || objectStart >= json.Length || json[objectStart] != '{')
                return -1;

            int depth = 0;
            bool isString = false;
            bool isEscaped = false;
            for (int index = objectStart; index < json.Length; index++)
            {
                char character = json[index];
                if (isString)
                {
                    if (isEscaped)
                        isEscaped = false;
                    else if (character == '\\')
                        isEscaped = true;
                    else if (character == '"')
                        isString = false;
                    continue;
                }

                if (character == '"')
                {
                    isString = true;
                    continue;
                }
                if (character == '{')
                    depth++;
                else if (character == '}' && --depth == 0)
                    return index;
            }
            return -1;
        }

        private static void ValidateLogicalPath(string path, int? year)
        {
            if (string.IsNullOrWhiteSpace(path) || path.StartsWith("/", StringComparison.Ordinal) ||
                path.Contains("\\") || path.Contains(":") || path.Contains("//"))
            {
                throw new HistoricalContentLoadException("안전하지 않은 Runtime Content 논리 경로입니다.", path, year);
            }

            string[] segments = path.Split('/');
            for (int index = 0; index < segments.Length; index++)
            {
                if (segments[index].Length == 0 || segments[index] == "." || segments[index] == "..")
                    throw new HistoricalContentLoadException("안전하지 않은 Runtime Content 논리 경로입니다.", path, year);
            }
        }

        private static string Decode(byte[] bytes, string relativePath, int? year)
        {
            try
            {
                int offset = bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf
                    ? 3
                    : 0;
                return StrictUtf8.GetString(bytes, offset, bytes.Length - offset);
            }
            catch (DecoderFallbackException exception)
            {
                throw new HistoricalContentLoadException("UTF-8 JSON으로 읽을 수 없습니다.", relativePath, year, exception);
            }
        }

        private static T ParseJson<T>(string json, string relativePath, int? year) where T : class
        {
            try
            {
                T result = JsonUtility.FromJson<T>(json);
                return result ?? throw new InvalidDataException("JSON Root를 읽을 수 없습니다.");
            }
            catch (Exception exception) when (!(exception is HistoricalContentLoadException))
            {
                throw new HistoricalContentLoadException("JSON을 파싱하지 못했습니다.", relativePath, year, exception);
            }
        }

        private static string ComputeSha256Hex(byte[] bytes)
        {
            using SHA256 sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(bytes);
            var result = new StringBuilder(hash.Length * 2);
            for (int index = 0; index < hash.Length; index++)
                result.Append(hash[index].ToString("x2", CultureInfo.InvariantCulture));
            return result.ToString();
        }

        private static bool IsSha256(string value)
        {
            if (value == null || value.Length != 64)
                return false;
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                bool isHex = character is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
                if (!isHex)
                    return false;
            }
            return true;
        }

        private static Handedness ParseHandedness(string value, string field)
        {
            return value switch
            {
                "Right" => Handedness.Right,
                "Left" => Handedness.Left,
                "Switch" => Handedness.Switch,
                _ => throw new HistoricalContentLoadException($"알 수 없는 {field} 값입니다. value={value}")
            };
        }

        private static RegistrationType ParseRegistrationType(string value)
        {
            return value switch
            {
                "Domestic" => RegistrationType.Domestic,
                "Foreign" => RegistrationType.Foreign,
                _ => throw new HistoricalContentLoadException($"알 수 없는 registrationType입니다. value={value}")
            };
        }

        private static PlayerDataProvenance ParsePlayerDataProvenance(string value)
        {
            return value switch
            {
                "" => PlayerDataProvenance.SourceBacked,
                "SourceBacked" => PlayerDataProvenance.SourceBacked,
                "ReplacementGenerated" => PlayerDataProvenance.ReplacementGenerated,
                _ => throw new HistoricalContentLoadException($"알 수 없는 dataProvenance입니다. value={value}")
            };
        }

        private static PitcherRoleConfidence ParsePitcherRoleConfidence(string value)
        {
            return value switch
            {
                "" => PitcherRoleConfidence.High,
                "Low" => PitcherRoleConfidence.Low,
                "Medium" => PitcherRoleConfidence.Medium,
                "High" => PitcherRoleConfidence.High,
                _ => throw new HistoricalContentLoadException(
                    $"알 수 없는 pitcherRoleConfidence입니다. value={value}")
            };
        }

        private static PitcherRole ParsePitcherRole(string value)
        {
            return value switch
            {
                "Starter" => PitcherRole.Starter,
                "Swingman" => PitcherRole.Swingman,
                "LongRelief" => PitcherRole.LongRelief,
                "MiddleRelief" => PitcherRole.MiddleRelief,
                "Setup" => PitcherRole.Setup,
                "Closer" => PitcherRole.Closer,
                _ => throw new HistoricalContentLoadException($"알 수 없는 pitcherRole입니다. value={value}")
            };
        }

        private static PitcherRole ParsePitcherRole(string value, PlayerType playerType)
        {
            if (playerType == PlayerType.Batter && string.IsNullOrEmpty(value))
                return PitcherRole.Starter;
            return ParsePitcherRole(value);
        }

        private static PlayerType ParsePlayerType(string value)
        {
            return value switch
            {
                "Hitter" => PlayerType.Batter,
                "Batter" => PlayerType.Batter,
                "Pitcher" => PlayerType.Pitcher,
                _ => throw new HistoricalContentLoadException($"알 수 없는 playerType입니다. value={value}")
            };
        }

        private static PlayerPosition ParsePosition(string value, PitcherRole pitcherRole)
        {
            return value switch
            {
                "C" => PlayerPosition.Catcher,
                "1B" => PlayerPosition.FirstBase,
                "2B" => PlayerPosition.SecondBase,
                "3B" => PlayerPosition.ThirdBase,
                "SS" => PlayerPosition.Shortstop,
                "LF" => PlayerPosition.LeftField,
                "CF" => PlayerPosition.CenterField,
                "RF" => PlayerPosition.RightField,
                "DH" => PlayerPosition.DesignatedHitter,
                "P" => pitcherRole == PitcherRole.Starter
                    ? PlayerPosition.StartingPitcher
                    : PlayerPosition.ReliefPitcher,
                _ => throw new HistoricalContentLoadException($"알 수 없는 position입니다. value={value}")
            };
        }

        private static WorldAwardType ParseAwardType(string value)
        {
            return value switch
            {
                "AllStar" => WorldAwardType.AllStar,
                "GoldenGlove" => WorldAwardType.GoldenGlove,
                "RegularSeasonMvp" => WorldAwardType.RegularSeasonMvp,
                "AllStarGameMvp" => WorldAwardType.AllStarGameMvp,
                "KoreanSeriesMvp" => WorldAwardType.PostseasonMvp,
                "PostseasonMvp" => WorldAwardType.PostseasonMvp,
                _ => throw new HistoricalContentLoadException($"알 수 없는 awardType입니다. value={value}")
            };
        }

        /// <summary>분할 JSON을 원래 Runtime content의 canonical 순서로 이어 붙여 contentHash를 검증한다.</summary>
        /// <summary>
        /// TextAsset.bytes는 메인 스레드에서만 읽을 수 있고 호출마다 사본을 만든다.
        /// 미리 확보해 두면 파싱을 워커 스레드에서 돌릴 수 있고, 중복 사본도 생기지 않는다.
        /// </summary>
        private sealed class AssetByteReader
        {
            private readonly Dictionary<TextAsset, byte[]> _bytesByAsset =
                new Dictionary<TextAsset, byte[]>();

            public void Cache(TextAsset asset)
            {
                if (asset == null)
                    return;
                lock (_bytesByAsset)
                {
                    if (!_bytesByAsset.ContainsKey(asset))
                        _bytesByAsset.Add(asset, asset.bytes);
                }
            }

            public void Clear()
            {
                lock (_bytesByAsset)
                    _bytesByAsset.Clear();
            }

            public byte[] Read(TextAsset asset)
            {
                if (asset == null)
                    throw new HistoricalContentLoadException("Runtime TextAsset이 없습니다.");
                lock (_bytesByAsset)
                {
                    if (_bytesByAsset.TryGetValue(asset, out byte[] cached))
                        return cached;
                }

                // 미리 확보하지 않았다면 메인 스레드에서 호출된 것으로 보고 직접 읽는다.
                return asset.bytes;
            }
        }

        private sealed class RuntimeContentHashVerifier : IDisposable
        {
            private readonly SHA256 _sha256;
            private readonly int _contentSchemaVersion;
            private int _yearCount;
            private bool _isCompleted;

            public RuntimeContentHashVerifier(HistoricalRuntimeManifestDto manifest)
            {
                _sha256 = SHA256.Create();
                _contentSchemaVersion = manifest.ContentSchemaVersion;
                AppendUtf8("{\"manifest\":");
                AppendSourceManifest(manifest.SourceManifest, _contentSchemaVersion);
                AppendUtf8(",\"playerPersons\":");
            }

            public void AppendPlayerPersons(byte[] payload)
            {
                if (_contentSchemaVersion >= 5)
                {
                    string json = DecodeCanonicalPayload(payload);
                    AppendUtf8(ExtractCanonicalPropertyValue(json, "items"));
                    AppendUtf8(",\"schemaVersion\":");
                    AppendUtf8(_contentSchemaVersion.ToString(CultureInfo.InvariantCulture));
                    AppendUtf8(",\"worldIdentityNamePool\":");
                    AppendUtf8(ExtractCanonicalPropertyValue(json, "worldIdentityNamePool"));
                    AppendUtf8(",\"years\":[");
                    return;
                }

                AppendCanonicalPayload(payload);
                AppendUtf8(",\"schemaVersion\":");
                AppendUtf8(_contentSchemaVersion.ToString(CultureInfo.InvariantCulture));
                AppendUtf8(",\"years\":[");
            }

            public void AppendYear(byte[] payload, int index)
            {
                if (index != _yearCount)
                    throw new InvalidOperationException("Runtime 연도 contentHash 순서가 연속적이지 않습니다.");
                if (_yearCount > 0)
                    AppendUtf8(",");
                AppendCanonicalPayload(payload);
                _yearCount++;
            }

            public void Validate(string sourceManifestHash)
            {
                if (_isCompleted)
                    throw new InvalidOperationException("Runtime contentHash 검증은 한 번만 완료할 수 있습니다.");

                AppendUtf8("]}");
                _sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                _isCompleted = true;
                string actualHash = FormatHex(_sha256.Hash);
                if (!string.Equals(actualHash, sourceManifestHash, StringComparison.OrdinalIgnoreCase))
                {
                    throw new HistoricalContentLoadException(
                        $"Runtime Content Hash가 다릅니다. expected={sourceManifestHash}, actual={actualHash}",
                        "manifest.json");
                }
            }

            public void Dispose()
            {
                _sha256.Dispose();
            }

            private void AppendSourceManifest(
                HistoricalRuntimeSourceManifestDto source,
                int contentSchemaVersion)
            {
                var builder = new StringBuilder(768);
                builder.Append("{\"abilityFormulaVersion\":\"");
                AppendJsonEscaped(builder, source.AbilityFormulaVersion);
                builder.Append("\",\"balanceVersion\":\"");
                AppendJsonEscaped(builder, source.BalanceVersion);
                builder.Append("\",\"contentHash\":\"\",\"costFormulaVersion\":\"");
                AppendJsonEscaped(builder, source.CostFormulaVersion);
                builder.Append("\",\"derivationBalanceVersion\":\"");
                AppendJsonEscaped(builder, source.DerivationBalanceVersion);
                builder.Append("\",\"generationSeed\":")
                    .Append(source.GenerationSeed.ToString(CultureInfo.InvariantCulture));
                if (contentSchemaVersion >= 5)
                {
                    builder.Append(",\"generationSeedAffectsCanonicalBake\":")
                        .Append(source.GenerationSeedAffectsCanonicalBake ? "true" : "false");
                }
                builder.Append(",\"generatorVersion\":\"");
                AppendJsonEscaped(builder, source.GeneratorVersion);
                builder.Append("\",\"nameDataPolicy\":\"");
                AppendJsonEscaped(builder, source.NameDataPolicy);
                builder.Append("\",\"namePolicyVersion\":\"");
                AppendJsonEscaped(builder, source.NamePolicyVersion);
                builder.Append("\",\"normalizedContentHash\":\"");
                AppendJsonEscaped(builder, source.NormalizedContentHash);
                builder.Append("\",\"normalizedImporterVersion\":\"");
                AppendJsonEscaped(builder, source.NormalizedImporterVersion);
                builder.Append("\",\"normalizedSchemaVersion\":")
                    .Append(source.NormalizedSchemaVersion.ToString(CultureInfo.InvariantCulture))
                    .Append(",\"positionRoleClassifierVersion\":\"");
                AppendJsonEscaped(builder, source.PositionRoleClassifierVersion);
                builder.Append("\",\"rawDataVersion\":\"");
                AppendJsonEscaped(builder, source.RawDataVersion);
                builder.Append("\",\"referenceDataVersion\":\"");
                AppendJsonEscaped(builder, source.ReferenceDataVersion);
                if (contentSchemaVersion >= 4)
                {
                    builder.Append("\",\"replacementGeneratedPlayerPersonCount\":")
                        .Append(source.ReplacementGeneratedPlayerPersonCount.ToString(CultureInfo.InvariantCulture))
                        .Append(",\"replacementGeneratedPlayerSeasonCount\":")
                        .Append(source.ReplacementGeneratedPlayerSeasonCount.ToString(CultureInfo.InvariantCulture))
                        .Append(",\"replacementGeneratorVersion\":\"");
                    AppendJsonEscaped(builder, source.ReplacementGeneratorVersion);
                    builder.Append("\",\"replacementPopulationPolicyVersion\":\"");
                    AppendJsonEscaped(builder, source.ReplacementPopulationPolicyVersion);
                }
                builder.Append("\",\"rosterBuilderVersion\":\"");
                AppendJsonEscaped(builder, source.RosterBuilderVersion);
                if (contentSchemaVersion >= 4)
                {
                    builder.Append("\",\"sourceAllocationPolicyVersion\":\"");
                    AppendJsonEscaped(builder, source.SourceAllocationPolicyVersion);
                    builder.Append("\",\"sourceBackedPlayerPersonCount\":")
                        .Append(source.SourceBackedPlayerPersonCount.ToString(CultureInfo.InvariantCulture))
                        .Append(",\"sourceBackedPlayerSeasonCount\":")
                        .Append(source.SourceBackedPlayerSeasonCount.ToString(CultureInfo.InvariantCulture));
                    if (contentSchemaVersion >= 5)
                    {
                        builder.Append(",\"sourceFranchiseIdentityPolicyVersion\":\"");
                        AppendJsonEscaped(builder, source.SourceFranchiseIdentityPolicyVersion);
                        builder.Append("\",\"sourceIdentityPolicyVersion\":\"");
                    }
                    else
                    {
                        builder.Append(",\"sourceIdentityPolicyVersion\":\"");
                    }
                    AppendJsonEscaped(builder, source.SourceIdentityPolicyVersion);
                    if (contentSchemaVersion >= 5)
                    {
                        builder.Append("\",\"sourceTeamSeasonIdentityPolicyVersion\":\"");
                        AppendJsonEscaped(builder, source.SourceTeamSeasonIdentityPolicyVersion);
                    }
                }
                builder.Append("\"}");
                AppendUtf8(builder.ToString());
            }

            private void AppendCanonicalPayload(byte[] payload)
            {
                if (payload == null || payload.Length == 0)
                    throw new HistoricalContentLoadException("contentHash 대상 payload가 비어 있습니다.");

                int count = payload.Length;
                if (count > 0 && payload[count - 1] == (byte)'\n')
                    count--;
                if (count > 0 && payload[count - 1] == (byte)'\r')
                    count--;
                if (count == 0)
                    throw new HistoricalContentLoadException("contentHash 대상 JSON이 비어 있습니다.");
                AppendBytes(payload, 0, count);
            }

            private static string DecodeCanonicalPayload(byte[] payload)
            {
                if (payload == null || payload.Length == 0)
                    throw new HistoricalContentLoadException("contentHash 대상 payload가 비어 있습니다.");
                string json = Encoding.UTF8.GetString(payload);
                if (json.Length > 0 && json[0] == '\uFEFF')
                    json = json.Substring(1);
                return json.TrimEnd('\r', '\n');
            }

            private static string ExtractCanonicalPropertyValue(string json, string propertyName)
            {
                string token = "\"" + propertyName + "\":";
                int propertyIndex = json.IndexOf(token, StringComparison.Ordinal);
                if (propertyIndex < 0)
                {
                    throw new HistoricalContentLoadException(
                        $"contentHash 대상 PlayerPerson payload에 {propertyName} 값이 없습니다.");
                }

                int start = propertyIndex + token.Length;
                while (start < json.Length && char.IsWhiteSpace(json[start]))
                    start++;
                if (start >= json.Length || (json[start] != '[' && json[start] != '{'))
                {
                    throw new HistoricalContentLoadException(
                        $"contentHash 대상 {propertyName} JSON 값이 배열 또는 객체가 아닙니다.");
                }

                int depth = 0;
                bool isInsideString = false;
                bool isEscaped = false;
                for (int index = start; index < json.Length; index++)
                {
                    char character = json[index];
                    if (isInsideString)
                    {
                        if (isEscaped)
                        {
                            isEscaped = false;
                            continue;
                        }
                        if (character == '\\')
                        {
                            isEscaped = true;
                            continue;
                        }
                        if (character == '"')
                            isInsideString = false;
                        continue;
                    }

                    if (character == '"')
                    {
                        isInsideString = true;
                        continue;
                    }
                    if (character == '[' || character == '{')
                    {
                        depth++;
                        continue;
                    }
                    if (character != ']' && character != '}')
                        continue;
                    depth--;
                    if (depth == 0)
                        return json.Substring(start, index - start + 1);
                }

                throw new HistoricalContentLoadException(
                    $"contentHash 대상 {propertyName} JSON 값이 닫히지 않았습니다.");
            }

            private void AppendUtf8(string value)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(value);
                AppendBytes(bytes, 0, bytes.Length);
            }

            private void AppendBytes(byte[] bytes, int offset, int count)
            {
                if (_isCompleted)
                    throw new InvalidOperationException("완료된 Runtime contentHash에는 데이터를 추가할 수 없습니다.");
                if (count == 0)
                    return;
                _sha256.TransformBlock(bytes, offset, count, bytes, offset);
            }
        }

        private static string FormatHex(byte[] bytes)
        {
            var result = new StringBuilder(bytes.Length * 2);
            for (int index = 0; index < bytes.Length; index++)
                result.Append(bytes[index].ToString("x2", CultureInfo.InvariantCulture));
            return result.ToString();
        }

        private readonly struct VerifiedPayload
        {
            public VerifiedPayload(string relativePath, string sha256, string text, byte[] bytes)
            {
                RelativePath = relativePath;
                Sha256 = sha256;
                Text = text;
                Bytes = bytes;
            }

            public string RelativePath { get; }
            public string Sha256 { get; }
            public string Text { get; }
            public byte[] Bytes { get; }
        }

    }
}
