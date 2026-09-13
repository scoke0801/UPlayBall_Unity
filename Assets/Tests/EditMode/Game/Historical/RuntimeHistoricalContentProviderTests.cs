using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Baseball.Game.Historical;
using NUnit.Framework;
using UnityEngine;

namespace Baseball.Tests.EditMode.Game
{
    /// <summary>실제 1982~2025 Bake를 Runtime TextAsset 경계로 읽고 무결성·캐시 계약을 검증한다.</summary>
    public sealed class RuntimeHistoricalContentProviderTests
    {
        private const string SourceRoot =
            "Assets/10.Datas/HistoricalSimulation/1982-2025";

        private readonly List<UnityEngine.Object> _createdObjects = new List<UnityEngine.Object>();
        private HistoricalRuntimeContentCatalog _catalog;
        private UnityHistoricalContentProvider _provider;

        [OneTimeSetUp]
        public void CreateCatalog()
        {
            _catalog = CreateCatalogFromBake();
            _provider = new UnityHistoricalContentProvider(_catalog, HistoricalContentVerificationMode.Full);
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
            Assert.That(content.Manifest.ContentSchemaVersion, Is.EqualTo(6));
            Assert.That(
                content.Manifest.AssetArchiveHash,
                Does.Match("^[a-f0-9]{64}$"));
            Assert.That(content.Manifest.ReferenceDataVersion, Is.EqualTo("kbo-normalized-v3"));
            Assert.That(content.Manifest.GeneratorVersion, Is.EqualTo("source-backed-runtime-bake-v2"));
            Assert.That(content.Manifest.BalanceVersion, Is.EqualTo("historical-source-backed-v2"));
            Assert.That(content.Manifest.NamePolicyVersion, Is.EqualTo("world-identity-name-pool-v2"));
            Assert.That(content.Manifest.NameDataPolicy, Is.EqualTo("runtime-world-identity-pool-v3"));
            Assert.That(content.Manifest.GenerationSeed, Is.Zero);
            Assert.That(content.Manifest.SourceManifest.GenerationSeedAffectsCanonicalBake, Is.False);
            Assert.That(content.Manifest.SourceManifest.SourceIdentityPolicyVersion, Is.EqualTo("source-backed-identity-v1"));
            Assert.That(content.Manifest.SourceManifest.SourceFranchiseIdentityPolicyVersion, Is.EqualTo("source-franchise-identity-v1"));
            Assert.That(content.Manifest.SourceManifest.SourceTeamSeasonIdentityPolicyVersion, Is.EqualTo("source-team-season-identity-v1"));
            Assert.That(content.Manifest.SourceManifest.SourceAllocationPolicyVersion, Is.EqualTo("source-team-season-one-to-one-v2"));
            Assert.That(content.Manifest.SourceManifest.ReplacementGeneratorVersion, Is.EqualTo("quota-fallback-percentile-v2"));
            Assert.That(content.Manifest.SourceManifest.ReplacementPopulationPolicyVersion, Is.EqualTo("quota-fallback-aggregate-percentile-v2"));
            Assert.That(content.Manifest.SourceManifest.SourceBackedPlayerPersonCount, Is.EqualTo(3511));
            Assert.That(content.Manifest.SourceManifest.SourceBackedPlayerSeasonCount, Is.EqualTo(18272));
            Assert.That(content.Manifest.SourceManifest.ReplacementGeneratedPlayerPersonCount, Is.EqualTo(54));
            Assert.That(content.Manifest.SourceManifest.ReplacementGeneratedPlayerSeasonCount, Is.EqualTo(54));
            Assert.That(content.Manifest.SourceManifest.PitchBalanceVersion, Is.EqualTo("pitch-arsenal-v2"));
            Assert.That(content.Manifest.SourceManifest.PitchGenerationSeed, Is.EqualTo(20260906UL));
            Assert.That(
                content.Manifest.ContentHash,
                Does.Match("^[a-f0-9]{64}$"));
        }

        [TestCase("birthYearResearchedCount")]
        [TestCase("handednessResearchedCount")]
        [TestCase("personCount")]
        [TestCase("researchVersion")]
        public void RuntimeContentProvider_RejectsChangedPersonIdentityResearch(string field)
        {
            // 조사 메타데이터만 바뀌어도 원본 ContentHash 검증에서 감지해야 한다.
            string original = _catalog.Manifest.text;
            var researchPattern = new System.Text.RegularExpressions.Regex(
                "\"personIdentityResearch\":\\{[^}]*\\}");
            var fieldPattern = new System.Text.RegularExpressions.Regex(
                "\"" + field + "\":(?:\"[^\"]*\"|[0-9]+)");
            string replacement = "\"" + field + "\":" +
                (field == "researchVersion" ? "\"tampered\"" : "999999");
            string invalidManifest = researchPattern.Replace(original,
                match => fieldPattern.Replace(match.Value, replacement));
            Assert.That(invalidManifest, Is.Not.EqualTo(original), "조사 메타데이터가 있는 원본이 필요합니다.");
            HistoricalRuntimeContentCatalog catalog = CreateCatalog(
                CreateTextAsset(invalidManifest), _catalog.PlayerPersons, _catalog.Years);

            HistoricalContentLoadException exception = Assert.Throws<HistoricalContentLoadException>(
                () => new UnityHistoricalContentProvider(catalog, HistoricalContentVerificationMode.Full).Load());

            Assert.That(exception.Message, Does.Contain("Content Hash"));
            Assert.That(exception.RelativePath, Is.EqualTo("manifest.json"));
        }

        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void RuntimeContentProvider_RejectsLegacyArmSchema(int schemaVersion)
        {
            TextAsset manifest = CreateTextAsset(_catalog.Manifest.text.Replace(
                "\"contentSchemaVersion\":6", "\"contentSchemaVersion\":" + schemaVersion));
            HistoricalRuntimeContentCatalog catalog = CreateCatalog(
                manifest,
                _catalog.PlayerPersons,
                _catalog.Years);

            HistoricalContentLoadException exception = Assert.Throws<HistoricalContentLoadException>(
                () => new UnityHistoricalContentProvider(catalog).Load());
            Assert.That(exception.Message, Does.Contain("Content Schema"));
        }

        [Test]
        public void RuntimeContentProvider_RejectsSchemaV6WithoutSourceContractFields()
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
        public void RuntimeContentProvider_RejectsSchemaV6EmptySourceContractVersion()
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
            Assert.That(content.PlayerPersons.Count, Is.EqualTo(3565));
            Assert.That(content.PlayerSeasons.Count, Is.EqualTo(18326));
            Assert.That(content.NormalCards.Count, Is.EqualTo(18326));
            Assert.That(content.TeamSeasons.Count, Is.EqualTo(363));
            Assert.That(content.OriginalSeasonRecords.Count, Is.EqualTo(17387));
            Assert.That(content.OriginalAwardRecords.Count, Is.EqualTo(569));
            for (int index = 0; index < content.Years.Count; index++)
            {
                int teamCount = content.Years[index].TeamSeasons.Count;
                Assert.That(teamCount, Is.InRange(6, 10));
                Assert.That(content.Years[index].PlayerSeasons.Count, Is.GreaterThanOrEqualTo(teamCount * 25));
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
                () => new UnityHistoricalContentProvider(damagedCatalog, HistoricalContentVerificationMode.Full).Load());

            Assert.That(exception.Message, Does.Contain("SHA-256"));
            Assert.That(exception.RelativePath, Is.EqualTo("Years/1982.json"));
            Assert.That(exception.Year, Is.EqualTo(1982));
        }

        [Test]
        public void RuntimeContentProvider_RejectsInvalidContentHash()
        {
            string contentHash = _provider.Load().Manifest.ContentHash;
            string invalidManifest = _catalog.Manifest.text.Replace(
                contentHash,
                new string('0', 64));
            TextAsset manifest = CreateTextAsset(invalidManifest);
            HistoricalRuntimeContentCatalog invalidCatalog = CreateCatalog(
                manifest,
                _catalog.PlayerPersons,
                _catalog.Years);

            HistoricalContentLoadException exception = Assert.Throws<HistoricalContentLoadException>(
                () => new UnityHistoricalContentProvider(invalidCatalog, HistoricalContentVerificationMode.Full).Load());

            Assert.That(exception.Message, Does.Contain("Content Hash"));
            Assert.That(exception.RelativePath, Is.EqualTo("manifest.json"));
        }

        [Test]
        public void RuntimeContentProvider_RejectsInvalidVersion()
        {
            string invalidManifest = _catalog.Manifest.text.Replace(
                "\"contentSchemaVersion\":6",
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
                "runtime-world-identity-pool-v3",
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


        private static void AssertRuntimeSafePayload(string text, string relativePath)
        {
            Assert.That(text, Does.Not.Contain("\"originalName\""), relativePath);
            Assert.That(text, Does.Not.Contain("\"sourceReferenceNames\""), relativePath);
        }
    }
}
