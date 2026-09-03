using System;
using System.Linq;
using Baseball.Editor.HistoricalDatabase;
using Baseball.Game.Data;
using Baseball.Game.Historical;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Baseball.Tests.EditMode.Editor
{
    /// <summary>Runtime 역사 Catalog가 Production Resources 정의를 통해 Player dependency가 되는지 검증한다.</summary>
    public sealed class HistoricalRuntimeContentDependencyTests
    {
        private HistoricalRuntimeContentCatalog _catalog;

        [OneTimeSetUp]
        public void LoadExportedRuntimeContent()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<HistoricalRuntimeContentCatalog>(
                HistoricalRuntimeContentExporter.CatalogAssetPath);
            Assert.That(
                _catalog,
                Is.Not.Null,
                "Historical Runtime exporter를 먼저 실행해야 합니다.");
        }

        [Test]
        public void Exporter_BindsCatalogToProductionNewGameDefinition()
        {
            NewGameDefinition definition = AssetDatabase.LoadAssetAtPath<NewGameDefinition>(
                HistoricalRuntimeContentExporter.NewGameDefinitionAssetPath);

            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.HistoricalContentCatalog, Is.SameAs(_catalog));
        }

        [Test]
        public void NewGameDefinition_LoadHistoricalContentProvider_LoadsAllYears()
        {
            IHistoricalContentProvider provider = NewGameDefinition.LoadHistoricalContentProvider();
            HistoricalBakedContent content = provider.Load();

            Assert.That(provider, Is.TypeOf<UnityHistoricalContentProvider>());
            Assert.That(content.Years.Count, Is.EqualTo(44));
            Assert.That(content.Manifest.NameDataPolicy, Is.EqualTo("runtime-fictional-only-v2"));
        }

        [Test]
        public void NewGameDefinition_DependencyGraphIncludesCatalogAndAllPayloads()
        {
            string[] dependencies = AssetDatabase.GetDependencies(
                HistoricalRuntimeContentExporter.NewGameDefinitionAssetPath,
                recursive: true);

            Assert.That(dependencies, Does.Contain(HistoricalRuntimeContentExporter.CatalogAssetPath));
            int runtimeJsonCount = dependencies.Count(path =>
                path.StartsWith(HistoricalRuntimeContentExporter.RuntimeRoot + "/", StringComparison.Ordinal) &&
                path.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
            Assert.That(runtimeJsonCount, Is.EqualTo(46));
        }

        [Test]
        public void NewGameDefinition_MissingCatalogFailsWithoutSyntheticFallback()
        {
            NewGameDefinition definition = ScriptableObject.CreateInstance<NewGameDefinition>();
            try
            {
                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                    () => definition.CreateHistoricalContentProvider());

                Assert.That(exception.Message, Does.Contain("HistoricalRuntimeContentCatalog"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }
    }
}
