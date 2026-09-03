using System;
using System.Collections.Generic;

namespace Baseball.Game.Historical
{
    /// <summary>세이브가 참조하는 Runtime Historical Content의 불변 식별 정보다.</summary>
    public sealed class HistoricalContentReference
    {
        public HistoricalContentReference(
            int assetFormatVersion,
            int contentSchemaVersion,
            string assetArchiveHash,
            string referenceDataVersion,
            string generatorVersion,
            string balanceVersion,
            ulong generationSeed,
            string contentHash)
        {
            if (assetFormatVersion <= 0)
                throw new ArgumentOutOfRangeException(nameof(assetFormatVersion));
            if (contentSchemaVersion <= 0)
                throw new ArgumentOutOfRangeException(nameof(contentSchemaVersion));

            AssetFormatVersion = assetFormatVersion;
            ContentSchemaVersion = contentSchemaVersion;
            AssetArchiveHash = Require(assetArchiveHash, nameof(assetArchiveHash));
            ReferenceDataVersion = Require(referenceDataVersion, nameof(referenceDataVersion));
            GeneratorVersion = Require(generatorVersion, nameof(generatorVersion));
            BalanceVersion = Require(balanceVersion, nameof(balanceVersion));
            GenerationSeed = generationSeed;
            ContentHash = Require(contentHash, nameof(contentHash));
        }

        public int AssetFormatVersion { get; }
        public int ContentSchemaVersion { get; }
        public string AssetArchiveHash { get; }
        public string ReferenceDataVersion { get; }
        public string GeneratorVersion { get; }
        public string BalanceVersion { get; }
        public ulong GenerationSeed { get; }
        public string ContentHash { get; }

        public static HistoricalContentReference FromManifest(HistoricalContentManifest manifest)
        {
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));

            return new HistoricalContentReference(
                manifest.AssetFormatVersion,
                manifest.ContentSchemaVersion,
                manifest.AssetArchiveHash,
                manifest.ReferenceDataVersion,
                manifest.GeneratorVersion,
                manifest.BalanceVersion,
                manifest.GenerationSeed,
                manifest.ContentHash);
        }

        /// <summary>세이브가 생성된 콘텐츠와 현재 Player Build 콘텐츠가 완전히 같은지 검증한다.</summary>
        public void EnsureMatches(HistoricalContentManifest manifest)
        {
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));

            EnsureEqual(nameof(AssetFormatVersion), AssetFormatVersion, manifest.AssetFormatVersion);
            EnsureEqual(nameof(ContentSchemaVersion), ContentSchemaVersion, manifest.ContentSchemaVersion);
            EnsureEqual(nameof(AssetArchiveHash), AssetArchiveHash, manifest.AssetArchiveHash);
            EnsureEqual(nameof(ReferenceDataVersion), ReferenceDataVersion, manifest.ReferenceDataVersion);
            EnsureEqual(nameof(GeneratorVersion), GeneratorVersion, manifest.GeneratorVersion);
            EnsureEqual(nameof(BalanceVersion), BalanceVersion, manifest.BalanceVersion);
            EnsureEqual(nameof(GenerationSeed), GenerationSeed, manifest.GenerationSeed);
            EnsureEqual(nameof(ContentHash), ContentHash, manifest.ContentHash);
        }

        private static void EnsureEqual<T>(string fieldName, T saved, T runtime)
        {
            if (EqualityComparer<T>.Default.Equals(saved, runtime))
                return;
            throw new InvalidOperationException(
                $"Historical Content {fieldName} 불일치: Save='{saved}', Runtime='{runtime}'.");
        }

        private static string Require(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Historical Content 식별 값은 비어 있을 수 없습니다.", parameterName);
            return value.Trim();
        }
    }

    /// <summary>HistoricalContentReference를 Unity JSON에 저장하는 순수 DTO다.</summary>
    [Serializable]
    public sealed class HistoricalContentReferenceSaveData
    {
        public int assetFormatVersion;
        public int contentSchemaVersion;
        public string assetArchiveHash;
        public string referenceDataVersion;
        public string generatorVersion;
        public string balanceVersion;
        public ulong generationSeed;
        public string contentHash;
    }

    /// <summary>Historical Content 참조와 저장 DTO를 상호 변환한다.</summary>
    public static class HistoricalContentReferenceMapper
    {
        public static HistoricalContentReferenceSaveData CreateSaveData(HistoricalContentReference reference)
        {
            if (reference == null)
                throw new ArgumentNullException(nameof(reference));

            return new HistoricalContentReferenceSaveData
            {
                assetFormatVersion = reference.AssetFormatVersion,
                contentSchemaVersion = reference.ContentSchemaVersion,
                assetArchiveHash = reference.AssetArchiveHash,
                referenceDataVersion = reference.ReferenceDataVersion,
                generatorVersion = reference.GeneratorVersion,
                balanceVersion = reference.BalanceVersion,
                generationSeed = reference.GenerationSeed,
                contentHash = reference.ContentHash
            };
        }

        public static HistoricalContentReference Restore(HistoricalContentReferenceSaveData source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            return new HistoricalContentReference(
                source.assetFormatVersion,
                source.contentSchemaVersion,
                source.assetArchiveHash,
                source.referenceDataVersion,
                source.generatorVersion,
                source.balanceVersion,
                source.generationSeed,
                source.contentHash);
        }
    }
}
