using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Baseball.Game.Historical;
using NUnit.Framework;
using UnityEngine;

namespace Baseball.Tests.EditMode.Game
{
    /// <summary>실제 1982~2025 Bake를 Runtime TextAsset 경계로 읽고 무결성·캐시 계약을 검증한다.</summary>
    public sealed class RuntimeHistoricalContentProviderTests
    {
        private const string SourceRoot =
            "Assets/Editor Default Resources/HistoricalSimulation/1982-2025/Runtime";

        private readonly List<UnityEngine.Object> _createdObjects = new List<UnityEngine.Object>();
        private HistoricalRuntimeContentCatalog _catalog;
        private UnityHistoricalContentProvider _provider;

        [OneTimeSetUp]
        public void CreateCatalog()
        {
            _catalog = CreateCatalogFromBake();
            _provider = new UnityHistoricalContentProvider(_catalog);
        }

        [OneTimeTearDown]
        public void DestroyCatalog()
        {
            for (int index = _createdObjects.Count - 1; index >= 0; index--)
                UnityEngine.Object.DestroyImmediate(_createdObjects[index]);
            _createdObjects.Clear();
        }

        [Test]
        public void RuntimeContentProvider_LoadsManifest()
        {
            HistoricalBakedContent content = _provider.Load();

            Assert.That(content.Manifest.AssetFormatVersion, Is.EqualTo(1));
            Assert.That(content.Manifest.ContentSchemaVersion, Is.EqualTo(4));
            Assert.That(
                content.Manifest.AssetArchiveHash,
                Is.EqualTo("d995ba952985a0a2e2c1622cc877db7e1293440249b853910a7e35ef8d224d12"));
            Assert.That(content.Manifest.ReferenceDataVersion, Is.EqualTo("kbo-normalized-v3"));
            Assert.That(content.Manifest.GeneratorVersion, Is.EqualTo("source-backed-runtime-bake-v1"));
            Assert.That(content.Manifest.BalanceVersion, Is.EqualTo("historical-source-backed-v1"));
            Assert.That(content.Manifest.NamePolicyVersion, Is.EqualTo("source-backed-fictional-name-v1"));
            Assert.That(content.Manifest.NameDataPolicy, Is.EqualTo("runtime-fictional-only-v2"));
            Assert.That(content.Manifest.GenerationSeed, Is.EqualTo(20260901UL));
            Assert.That(content.Manifest.SourceManifest.SourceIdentityPolicyVersion, Is.EqualTo("source-backed-identity-v1"));
            Assert.That(content.Manifest.SourceManifest.SourceAllocationPolicyVersion, Is.EqualTo("source-backed-franchise-allocation-v1"));
            Assert.That(content.Manifest.SourceManifest.ReplacementGeneratorVersion, Is.EqualTo("replacement-generation-v1"));
            Assert.That(content.Manifest.SourceManifest.ReplacementPopulationPolicyVersion, Is.EqualTo("origin-year-position-role-source-only-v1"));
            Assert.That(content.Manifest.SourceManifest.SourceBackedPlayerPersonCount, Is.EqualTo(3510));
            Assert.That(content.Manifest.SourceManifest.SourceBackedPlayerSeasonCount, Is.EqualTo(17333));
            Assert.That(content.Manifest.SourceManifest.ReplacementGeneratedPlayerPersonCount, Is.EqualTo(355));
            Assert.That(content.Manifest.SourceManifest.ReplacementGeneratedPlayerSeasonCount, Is.EqualTo(355));
            Assert.That(
                content.Manifest.ContentHash,
                Is.EqualTo("f52ff738c10520285e9ecaf9486d602a6cd382d04e20f1077c339296a0815c2c"));
        }

        [Test]
        public void RuntimeContentProvider_LoadsSchemaV3WithoutSourceContractFields()
        {
            TextAsset manifest = CreateTextAsset(BuildSchemaV3ManifestText());
            HistoricalRuntimeContentCatalog catalog = CreateCatalog(
                manifest,
                _catalog.PlayerPersons,
                _catalog.Years);

            HistoricalContentManifest loaded = new UnityHistoricalContentProvider(catalog).Load().Manifest;

            Assert.That(loaded.ContentSchemaVersion, Is.EqualTo(3));
            Assert.That(loaded.SourceManifest.SourceIdentityPolicyVersion, Is.Empty);
            Assert.That(loaded.SourceManifest.SourceAllocationPolicyVersion, Is.Empty);
            Assert.That(loaded.SourceManifest.ReplacementGeneratorVersion, Is.Empty);
            Assert.That(loaded.SourceManifest.ReplacementPopulationPolicyVersion, Is.Empty);
            Assert.That(loaded.SourceManifest.SourceBackedPlayerPersonCount, Is.Zero);
            Assert.That(loaded.SourceManifest.SourceBackedPlayerSeasonCount, Is.Zero);
            Assert.That(loaded.SourceManifest.ReplacementGeneratedPlayerPersonCount, Is.Zero);
            Assert.That(loaded.SourceManifest.ReplacementGeneratedPlayerSeasonCount, Is.Zero);
        }

        [Test]
        public void RuntimeContentProvider_RejectsSchemaV4WithoutSourceContractFields()
        {
            string invalidManifest = _catalog.Manifest.text.Replace(
                "\"sourceIdentityPolicyVersion\":\"source-backed-identity-v1\"",
                "\"legacySourceIdentityPolicyVersion\":\"source-backed-identity-v1\"");
            TextAsset manifest = CreateTextAsset(invalidManifest);
            HistoricalRuntimeContentCatalog invalidCatalog = CreateCatalog(
                manifest,
                _catalog.PlayerPersons,
                _catalog.Years);

            HistoricalContentLoadException exception = Assert.Throws<HistoricalContentLoadException>(
                () => new UnityHistoricalContentProvider(invalidCatalog).Load());

            Assert.That(exception.Message, Does.Contain("sourceIdentityPolicyVersion"));
            Assert.That(exception.RelativePath, Is.EqualTo("manifest.json"));
        }

        [Test]
        public void RuntimeContentProvider_RejectsSchemaV4EmptySourceContractVersion()
        {
            string invalidManifest = _catalog.Manifest.text.Replace(
                "\"sourceIdentityPolicyVersion\":\"source-backed-identity-v1\"",
                "\"sourceIdentityPolicyVersion\":\"\"");
            TextAsset manifest = CreateTextAsset(invalidManifest);
            HistoricalRuntimeContentCatalog invalidCatalog = CreateCatalog(
                manifest,
                _catalog.PlayerPersons,
                _catalog.Years);

            HistoricalContentLoadException exception = Assert.Throws<HistoricalContentLoadException>(
                () => new UnityHistoricalContentProvider(invalidCatalog).Load());

            Assert.That(exception.Message, Does.Contain("sourceIdentityPolicyVersion"));
            Assert.That(exception.RelativePath, Is.EqualTo("manifest.json"));
        }

        [Test]
        public void RuntimePayload_DoesNotContainEditorOriginalNames()
        {
            AssertRuntimeSafePayload(_catalog.Manifest.text, "manifest.json");
            AssertRuntimeSafePayload(_catalog.PlayerPersons.Content.text, "player_persons.json");
            for (int index = 0; index < _catalog.Years.Count; index++)
            {
                AssertRuntimeSafePayload(
                    _catalog.Years[index].File.Content.text,
                    _catalog.Years[index].File.RelativePath);
            }
        }

        [Test]
        public void RuntimeContentProvider_LoadsAllYears()
        {
            HistoricalBakedContent content = _provider.Load();

            Assert.That(content.Years.Count, Is.EqualTo(44));
            Assert.That(content.Years[0].Year, Is.EqualTo(1982));
            Assert.That(content.Years[43].Year, Is.EqualTo(2025));
            Assert.That(content.PlayerPersons.Count, Is.EqualTo(3865));
            Assert.That(content.PlayerSeasons.Count, Is.EqualTo(17688));
            Assert.That(content.NormalCards.Count, Is.EqualTo(17688));
            Assert.That(content.TeamSeasons.Count, Is.EqualTo(440));
            Assert.That(content.OriginalSeasonRecords.Count, Is.EqualTo(17688));
            Assert.That(content.OriginalAwardRecords.Count, Is.EqualTo(555));
            for (int index = 0; index < content.Years.Count; index++)
            {
                Assert.That(content.Years[index].TeamSeasons.Count, Is.EqualTo(10));
                Assert.That(content.Years[index].PlayerSeasons.Count, Is.GreaterThanOrEqualTo(250));
            }
        }

        [Test]
        public void RuntimeContentProvider_ReusesMaterializedCache()
        {
            HistoricalBakedContent first = _provider.Load();
            HistoricalBakedContent second = _provider.Load();

            Assert.That(second, Is.SameAs(first));
            Assert.That(_provider.MaterializationCount, Is.EqualTo(1));
        }

        [Test]
        public void RuntimeContentProvider_RejectsInvalidHash()
        {
            HistoricalRuntimeYearContentFile first = _catalog.Years[0];
            string original = first.File.Content.text;
            char replacement = original[0] == '{' ? '[' : '{';
            TextAsset damaged = CreateTextAsset(replacement + original.Substring(1));
            var years = new HistoricalRuntimeYearContentFile[_catalog.Years.Count];
            years[0] = new HistoricalRuntimeYearContentFile(
                first.Year,
                new HistoricalRuntimeContentFile(first.File.RelativePath, damaged));
            for (int index = 1; index < years.Length; index++)
                years[index] = _catalog.Years[index];
            HistoricalRuntimeContentCatalog damagedCatalog = CreateCatalog(
                _catalog.Manifest,
                _catalog.PlayerPersons,
                years);

            HistoricalContentLoadException exception = Assert.Throws<HistoricalContentLoadException>(
                () => new UnityHistoricalContentProvider(damagedCatalog).Load());

            Assert.That(exception.Message, Does.Contain("SHA-256"));
            Assert.That(exception.RelativePath, Is.EqualTo("Years/1982.json"));
            Assert.That(exception.Year, Is.EqualTo(1982));
        }

        [Test]
        public void RuntimeContentProvider_RejectsInvalidContentHash()
        {
            string invalidManifest = _catalog.Manifest.text.Replace(
                "f52ff738c10520285e9ecaf9486d602a6cd382d04e20f1077c339296a0815c2c",
                new string('0', 64));
            TextAsset manifest = CreateTextAsset(invalidManifest);
            HistoricalRuntimeContentCatalog invalidCatalog = CreateCatalog(
                manifest,
                _catalog.PlayerPersons,
                _catalog.Years);

            HistoricalContentLoadException exception = Assert.Throws<HistoricalContentLoadException>(
                () => new UnityHistoricalContentProvider(invalidCatalog).Load());

            Assert.That(exception.Message, Does.Contain("Content Hash"));
            Assert.That(exception.RelativePath, Is.EqualTo("manifest.json"));
        }

        [Test]
        public void RuntimeContentProvider_RejectsInvalidVersion()
        {
            string invalidManifest = _catalog.Manifest.text.Replace(
                "\"contentSchemaVersion\":4",
                "\"contentSchemaVersion\":999");
            TextAsset manifest = CreateTextAsset(invalidManifest);
            HistoricalRuntimeContentCatalog invalidCatalog = CreateCatalog(
                manifest,
                _catalog.PlayerPersons,
                _catalog.Years);

            HistoricalContentLoadException exception = Assert.Throws<HistoricalContentLoadException>(
                () => new UnityHistoricalContentProvider(invalidCatalog).Load());

            Assert.That(exception.Message, Does.Contain("Content Schema"));
            Assert.That(exception.RelativePath, Is.EqualTo("manifest.json"));
        }

        [Test]
        public void RuntimeContentProvider_RejectsEditorNamePolicy()
        {
            string invalidManifest = _catalog.Manifest.text.Replace(
                "runtime-fictional-only-v2",
                "editor-original-reference-v1");
            TextAsset manifest = CreateTextAsset(invalidManifest);
            HistoricalRuntimeContentCatalog invalidCatalog = CreateCatalog(
                manifest,
                _catalog.PlayerPersons,
                _catalog.Years);

            HistoricalContentLoadException exception = Assert.Throws<HistoricalContentLoadException>(
                () => new UnityHistoricalContentProvider(invalidCatalog).Load());

            Assert.That(exception.Message, Does.Contain("nameDataPolicy"));
            Assert.That(exception.RelativePath, Is.EqualTo("manifest.json"));
        }

        [Test]
        public void RuntimePath_DoesNotReferenceUnityEditor()
        {
            Assembly runtimeAssembly = typeof(UnityHistoricalContentProvider).Assembly;
            string[] references = runtimeAssembly.GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            Assert.That(references, Does.Not.Contain("UnityEditor"));
            Assert.That(references.Any(name => name.StartsWith("UnityEditor.", StringComparison.Ordinal)), Is.False);
        }

        private HistoricalRuntimeContentCatalog CreateCatalogFromBake()
        {
            TextAsset manifest = LoadTextAsset(SourceRoot + "/manifest.json");
            var persons = new HistoricalRuntimeContentFile(
                "player_persons.json",
                LoadTextAsset(SourceRoot + "/player_persons.json"));
            var years = new HistoricalRuntimeYearContentFile[44];
            for (int index = 0; index < years.Length; index++)
            {
                int year = 1982 + index;
                string path = $"Years/{year}.json";
                years[index] = new HistoricalRuntimeYearContentFile(
                    year,
                    new HistoricalRuntimeContentFile(path, LoadTextAsset(SourceRoot + "/" + path)));
            }
            return CreateCatalog(manifest, persons, years);
        }

        private HistoricalRuntimeContentCatalog CreateCatalog(
            TextAsset manifest,
            HistoricalRuntimeContentFile persons,
            IReadOnlyList<HistoricalRuntimeYearContentFile> years)
        {
            HistoricalRuntimeContentCatalog result =
                ScriptableObject.CreateInstance<HistoricalRuntimeContentCatalog>();
            _createdObjects.Add(result);
            result.Configure(manifest, persons, years);
            return result;
        }

        private TextAsset LoadTextAsset(string relativePath)
        {
            return CreateTextAsset(File.ReadAllText(Path.GetFullPath(relativePath)));
        }

        private TextAsset CreateTextAsset(string value)
        {
            var result = new TextAsset(value);
            _createdObjects.Add(result);
            return result;
        }

        private string BuildSchemaV3ManifestText()
        {
            const string currentContentHash =
                "f52ff738c10520285e9ecaf9486d602a6cd382d04e20f1077c339296a0815c2c";
            const string schemaV4Tail =
                "\"referenceDataVersion\":\"kbo-normalized-v3\"," +
                "\"replacementGeneratedPlayerPersonCount\":355," +
                "\"replacementGeneratedPlayerSeasonCount\":355," +
                "\"replacementGeneratorVersion\":\"replacement-generation-v1\"," +
                "\"replacementPopulationPolicyVersion\":\"origin-year-position-role-source-only-v1\"," +
                "\"rosterBuilderVersion\":\"position-first-core25-v2\"," +
                "\"sourceAllocationPolicyVersion\":\"source-backed-franchise-allocation-v1\"," +
                "\"sourceBackedPlayerPersonCount\":3510," +
                "\"sourceBackedPlayerSeasonCount\":17333," +
                "\"sourceIdentityPolicyVersion\":\"source-backed-identity-v1\"}";
            const string schemaV3Tail =
                "\"referenceDataVersion\":\"kbo-normalized-v3\"," +
                "\"rosterBuilderVersion\":\"position-first-core25-v2\"}";

            string manifest = _catalog.Manifest.text
                .Replace("\"contentSchemaVersion\":4", "\"contentSchemaVersion\":3")
                .Replace(schemaV4Tail, schemaV3Tail);
            string sourceManifest = ExtractSourceManifest(manifest).Replace(
                $"\"contentHash\":\"{currentContentHash}\"",
                "\"contentHash\":\"\"");
            var canonical = new StringBuilder();
            canonical.Append("{\"manifest\":")
                .Append(sourceManifest)
                .Append(",\"playerPersons\":")
                .Append(TrimTrailingNewline(_catalog.PlayerPersons.Content.text))
                .Append(",\"schemaVersion\":3,\"years\":[");
            for (int index = 0; index < _catalog.Years.Count; index++)
            {
                if (index > 0)
                    canonical.Append(',');
                canonical.Append(TrimTrailingNewline(_catalog.Years[index].File.Content.text));
            }
            canonical.Append("]}");
            string v3ContentHash = ComputeSha256(canonical.ToString());
            return manifest.Replace(currentContentHash, v3ContentHash);
        }

        private static string ExtractSourceManifest(string manifest)
        {
            const string property = "\"sourceManifest\":";
            int propertyIndex = manifest.IndexOf(property, StringComparison.Ordinal);
            int start = manifest.IndexOf('{', propertyIndex + property.Length);
            int end = manifest.IndexOf("},\"summary\"", start, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0));
            Assert.That(end, Is.GreaterThan(start));
            return manifest.Substring(start, end - start + 1);
        }

        private static string TrimTrailingNewline(string value)
        {
            return value.TrimEnd('\r', '\n');
        }

        private static string ComputeSha256(string value)
        {
            using SHA256 sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(value));
            var result = new StringBuilder(hash.Length * 2);
            for (int index = 0; index < hash.Length; index++)
                result.Append(hash[index].ToString("x2"));
            return result.ToString();
        }

        private static void AssertRuntimeSafePayload(string text, string relativePath)
        {
            Assert.That(text, Does.Not.Contain("\"originalName\""), relativePath);
            Assert.That(text, Does.Not.Contain("\"sourceReferenceNames\""), relativePath);
        }
    }
}
