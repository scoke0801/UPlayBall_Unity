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
            Assert.That(content.Manifest.ContentSchemaVersion, Is.EqualTo(3));
            Assert.That(
                content.Manifest.AssetArchiveHash,
                Is.EqualTo("400993aeeef23ec348df8cc078334e7ef0f7adac9bbfb8f03d32abb61d5676b4"));
            Assert.That(content.Manifest.ReferenceDataVersion, Is.EqualTo("kbo-normalized-v3"));
            Assert.That(content.Manifest.GeneratorVersion, Is.EqualTo("synthetic-bake-v2"));
            Assert.That(content.Manifest.BalanceVersion, Is.EqualTo("historical-normal-v1"));
            Assert.That(content.Manifest.NamePolicyVersion, Is.EqualTo("korean-source-component-v2"));
            Assert.That(content.Manifest.NameDataPolicy, Is.EqualTo("runtime-fictional-only-v2"));
            Assert.That(content.Manifest.GenerationSeed, Is.EqualTo(20260901UL));
            Assert.That(
                content.Manifest.ContentHash,
                Is.EqualTo("df7d4eab7596057b86cf6dfd822677026146aa097f4d1ccc7003f88ce3d37d09"));
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
            Assert.That(content.PlayerPersons.Count, Is.EqualTo(1757));
            Assert.That(content.PlayerSeasons.Count, Is.EqualTo(13200));
            Assert.That(content.NormalCards.Count, Is.EqualTo(13200));
            Assert.That(content.TeamSeasons.Count, Is.EqualTo(440));
            Assert.That(content.OriginalSeasonRecords.Count, Is.EqualTo(13200));
            Assert.That(content.OriginalAwardRecords.Count, Is.EqualTo(1672));
            for (int index = 0; index < content.Years.Count; index++)
            {
                Assert.That(content.Years[index].TeamSeasons.Count, Is.EqualTo(10));
                Assert.That(content.Years[index].PlayerSeasons.Count, Is.EqualTo(300));
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
                "df7d4eab7596057b86cf6dfd822677026146aa097f4d1ccc7003f88ce3d37d09",
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
                "\"contentSchemaVersion\":3",
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

        private static void AssertRuntimeSafePayload(string text, string relativePath)
        {
            Assert.That(text, Does.Not.Contain("\"originalName\""), relativePath);
            Assert.That(text, Does.Not.Contain("\"sourceReferenceNames\""), relativePath);
        }
    }
}
