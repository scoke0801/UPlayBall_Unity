using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Baseball.Editor.Tools;
using Baseball.Game.Data;
using Baseball.Game.Historical;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Baseball.Editor.HistoricalDatabase
{
    /// <summary>Editor 원본명 Archive와 함께 생성된 Runtime 정제본만 Player Build payload로 내보낸다.</summary>
    public static class HistoricalRuntimeContentExporter
    {
        public const string SourceRoot =
            "Assets/Editor Default Resources/HistoricalSimulation/1982-2025/Runtime";
        public const string RuntimeRoot =
            "Assets/10.Datas/HistoricalSimulation/1982-2025";
        public const string CatalogAssetPath =
            "Assets/10.Datas/HistoricalSimulation/HistoricalRuntimeContentCatalog.asset";
        public const string NewGameDefinitionAssetPath =
            "Assets/10.Datas/Resources/NewGame/NewGameDefinition.asset";

        [BaseballEditorTool(
            "데이터",
            "Historical Runtime Content Export",
            "실명을 제거한 Runtime 정제본을 Player Build용 TextAsset과 Catalog로 내보냅니다.",
            order: 1,
            impact: ToolImpact.BulkWrite)]
        public static void ExportFromToolLauncher()
        {
            HistoricalRuntimeContentCatalog catalog = ExportRuntimePayload();
            HistoricalBakedContent content = new UnityHistoricalContentProvider(catalog).Load();
            Debug.Log(
                $"[HistoricalRuntimeContentExporter] Runtime payload export 완료: " +
                $"years={content.Years.Count}, persons={content.PlayerPersons.Count}, " +
                $"contentHash={content.Manifest.ContentHash}, path={RuntimeRoot}");
        }

        /// <summary>Runtime 정제본 검증 후 변경된 파일만 복사하고 Catalog 참조를 갱신한다.</summary>
        public static HistoricalRuntimeContentCatalog ExportRuntimePayload()
        {
            HistoricalArchiveData archive = new HistoricalArchiveRepository().Load(
                Path.GetFullPath(SourceRoot));
            HistoricalDatabaseValidationReport report =
                new HistoricalDatabaseValidationService().Validate(archive);
            if (!report.IsValid)
            {
                throw new InvalidDataException(
                    BuildValidationFailureMessage(report));
            }
            if (!string.Equals(
                    archive.Manifest.SourceManifest?.NameDataPolicy,
                    UnityHistoricalContentProvider.SupportedNameDataPolicy,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"원본 이름이 포함될 수 있는 Archive는 Runtime으로 내보낼 수 없습니다. " +
                    $"required={UnityHistoricalContentProvider.SupportedNameDataPolicy}, " +
                    $"actual={archive.Manifest.SourceManifest?.NameDataPolicy ?? "<missing>"}");
            }

            Directory.CreateDirectory(RuntimeRoot);
            Directory.CreateDirectory(RuntimeRoot + "/Years");
            CopyIfChanged(SourceRoot + "/manifest.json", RuntimeRoot + "/manifest.json");
            CopyIfChanged(SourceRoot + "/player_persons.json", RuntimeRoot + "/player_persons.json");
            for (int index = 0; index < archive.Manifest.Years.Count; index++)
            {
                HistoricalArchiveYearEntry year = archive.Manifest.Years[index];
                string relativePath = NormalizeAssetRelativePath(year.Path);
                CopyIfChanged(SourceRoot + "/" + relativePath, RuntimeRoot + "/" + relativePath);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            TextAsset manifest = LoadTextAsset(RuntimeRoot + "/manifest.json");
            TextAsset persons = LoadTextAsset(RuntimeRoot + "/player_persons.json");
            var years = new HistoricalRuntimeYearContentFile[archive.Manifest.Years.Count];
            for (int index = 0; index < archive.Manifest.Years.Count; index++)
            {
                HistoricalArchiveYearEntry year = archive.Manifest.Years[index];
                string relativePath = NormalizeAssetRelativePath(year.Path);
                years[index] = new HistoricalRuntimeYearContentFile(
                    year.Year,
                    new HistoricalRuntimeContentFile(
                        year.Path,
                        LoadTextAsset(RuntimeRoot + "/" + relativePath)));
            }

            HistoricalRuntimeContentCatalog catalog =
                AssetDatabase.LoadAssetAtPath<HistoricalRuntimeContentCatalog>(CatalogAssetPath);
            if (catalog == null)
            {
                string catalogDirectory = Path.GetDirectoryName(CatalogAssetPath);
                if (!string.IsNullOrEmpty(catalogDirectory))
                    Directory.CreateDirectory(catalogDirectory);
                catalog = ScriptableObject.CreateInstance<HistoricalRuntimeContentCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogAssetPath);
            }

            catalog.Configure(
                manifest,
                new HistoricalRuntimeContentFile(archive.Manifest.PlayerPersons.Path, persons),
                years);
            EditorUtility.SetDirty(catalog);
            BindCatalogToNewGameDefinition(catalog);
            AssetDatabase.SaveAssets();

            // Editor Repository와 다른 코드 경로로 최종 payload를 다시 읽어 Player에서의 실패를 앞당긴다.
            new UnityHistoricalContentProvider(catalog).Load();
            return catalog;
        }

        /// <summary>이미 검증 가능한 Runtime payload는 바꾸지 않고 Unity TextAsset 참조만 다시 묶는다.</summary>
        public static void RebindExistingRuntimeCatalog()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            TextAsset manifest = LoadTextAsset(RuntimeRoot + "/manifest.json");
            TextAsset persons = LoadTextAsset(RuntimeRoot + "/player_persons.json");
            string[] yearPaths = Directory.GetFiles(RuntimeRoot + "/Years", "*.json");
            Array.Sort(yearPaths, StringComparer.Ordinal);
            var years = new HistoricalRuntimeYearContentFile[yearPaths.Length];
            for (int index = 0; index < yearPaths.Length; index++)
            {
                string assetPath = yearPaths[index].Replace('\\', '/');
                if (!int.TryParse(Path.GetFileNameWithoutExtension(assetPath), out int year))
                    throw new InvalidDataException($"Runtime 연도 파일명이 숫자가 아닙니다: {assetPath}");
                years[index] = new HistoricalRuntimeYearContentFile(
                    year,
                    new HistoricalRuntimeContentFile(
                        "Years/" + Path.GetFileName(assetPath),
                        LoadTextAsset(assetPath)));
            }

            HistoricalRuntimeContentCatalog catalog =
                AssetDatabase.LoadAssetAtPath<HistoricalRuntimeContentCatalog>(CatalogAssetPath);
            if (catalog == null)
                throw new InvalidDataException($"Runtime Catalog를 찾을 수 없습니다: {CatalogAssetPath}");
            catalog.Configure(
                manifest,
                new HistoricalRuntimeContentFile("player_persons.json", persons),
                years);
            EditorUtility.SetDirty(catalog);
            BindCatalogToNewGameDefinition(catalog);
            AssetDatabase.SaveAssets();

            // Source authoring cache와 무관하게 manifest hash·파일 hash·schema·summary를 전부 다시 검증한다.
            HistoricalBakedContent content = new UnityHistoricalContentProvider(catalog).Load();
            Debug.Log(
                $"[HistoricalRuntimeContentExporter] 기존 Runtime Catalog 재바인딩 완료: " +
                $"years={content.Years.Count}, persons={content.PlayerPersons.Count}, " +
                $"contentHash={content.Manifest.ContentHash}");
        }

        private static void BindCatalogToNewGameDefinition(HistoricalRuntimeContentCatalog catalog)
        {
            NewGameDefinition definition =
                AssetDatabase.LoadAssetAtPath<NewGameDefinition>(NewGameDefinitionAssetPath);
            if (definition == null)
            {
                throw new InvalidDataException(
                    $"Production NewGameDefinition을 찾을 수 없습니다: {NewGameDefinitionAssetPath}");
            }

            var serializedDefinition = new SerializedObject(definition);
            SerializedProperty catalogProperty =
                serializedDefinition.FindProperty("_historicalContentCatalog");
            if (catalogProperty == null)
            {
                throw new InvalidDataException(
                    "NewGameDefinition의 Historical Runtime Catalog 직렬화 필드를 찾을 수 없습니다.");
            }

            catalogProperty.objectReferenceValue = catalog;
            serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        private static TextAsset LoadTextAsset(string assetPath)
        {
            TextAsset result = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            if (result == null)
                throw new InvalidDataException($"Runtime TextAsset Import에 실패했습니다: {assetPath}");
            return result;
        }

        private static string NormalizeAssetRelativePath(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Contains("\\") || value.StartsWith("/", StringComparison.Ordinal))
                throw new InvalidDataException($"지원하지 않는 manifest 상대 경로입니다: {value}");
            string[] segments = value.Split('/');
            for (int index = 0; index < segments.Length; index++)
            {
                if (segments[index].Length == 0 || segments[index] == "." || segments[index] == "..")
                    throw new InvalidDataException($"Archive 밖을 가리키는 manifest 상대 경로입니다: {value}");
            }
            return string.Join("/", segments);
        }

        private static void CopyIfChanged(string sourcePath, string destinationPath)
        {
            byte[] source = File.ReadAllBytes(sourcePath);
            if (File.Exists(destinationPath))
            {
                byte[] destination = File.ReadAllBytes(destinationPath);
                if (AreEqual(source, destination))
                    return;
            }

            string destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationDirectory))
                Directory.CreateDirectory(destinationDirectory);
            File.WriteAllBytes(destinationPath, source);
        }

        private static string BuildValidationFailureMessage(HistoricalDatabaseValidationReport report)
        {
            const int maximumDetails = 12;
            var builder = new StringBuilder(
                $"Historical Runtime 정제본 검증에 실패해 export를 중단했습니다. errors={report.ErrorCount}");
            int detailCount = 0;
            for (int index = 0; index < report.Issues.Count && detailCount < maximumDetails; index++)
            {
                HistoricalValidationIssue issue = report.Issues[index];
                if (issue.Severity != HistoricalValidationSeverity.Error)
                    continue;
                builder.AppendLine()
                    .Append("- ").Append(issue.Category)
                    .Append(" year=").Append(issue.Year?.ToString() ?? "-")
                    .Append(" entity=").Append(issue.EntityId)
                    .Append(": ").Append(issue.Message);
                detailCount++;
            }
            return builder.ToString();
        }

        private static bool AreEqual(IReadOnlyList<byte> left, IReadOnlyList<byte> right)
        {
            if (left.Count != right.Count)
                return false;
            for (int index = 0; index < left.Count; index++)
                if (left[index] != right[index]) return false;
            return true;
        }
    }

    /// <summary>Player Build 직전에 Editor 정본과 Runtime payload를 동기화하고 전체 Provider 경로를 검증한다.</summary>
    public sealed class HistoricalRuntimeContentPrebuildValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            try
            {
                HistoricalRuntimeContentExporter.ExportRuntimePayload();
            }
            catch (Exception exception)
            {
                throw new BuildFailedException(
                    $"Historical Runtime Content 준비에 실패했습니다: {exception.Message}");
            }
        }
    }
}
