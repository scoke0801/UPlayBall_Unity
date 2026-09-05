using System;
using System.Collections.Generic;
using Baseball.Editor.Tools;
using Baseball.Game.Data;
using Baseball.Game.Guide;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Baseball.Editor.Guide
{
    /// <summary>Front Manager JSON Schema와 런타임 상호 참조 검증을 Editor와 CI가 공유한다.</summary>
    public static class FrontManagerGuideDataValidation
    {
        public const string DatasetPath =
            "Assets/10.Datas/FrontManager/front_manager_guide_dataset_v1.json";
        public const string SchemaPath =
            "Assets/10.Datas/FrontManager/front_manager_guide_dataset_v1.schema.json";

        [BaseballEditorTool(
            "데이터",
            "Front Manager Guide 검증",
            "JSON Schema, FactType, CTA, expressionKey, payload와 dedupe placeholder를 검사합니다.",
            order: 10,
            impact: ToolImpact.ReadOnly)]
        public static void ValidateFromToolsLauncher()
        {
            string[] errors = Validate();
            if (errors.Length == 0)
            {
                Debug.Log("[FrontManagerGuideValidation] 오류 없음 — 100 Cue / 300 Variation");
                return;
            }
            for (int index = 0; index < errors.Length; index++)
                Debug.LogError("[FrontManagerGuideValidation] " + errors[index]);
        }

        /// <summary>Unity batchmode/CI가 검증 실패를 프로세스 오류로 받을 수 있는 진입점이다.</summary>
        public static void ValidateForCi()
        {
            string[] errors = Validate();
            if (errors.Length > 0)
                throw new InvalidOperationException(
                    "Front Manager Guide CI 검증 실패:\n" + string.Join("\n", errors));
            Debug.Log("[FrontManagerGuideValidation] CI 검증 통과 — 100 Cue / 300 Variation");
        }

        public static string[] Validate()
        {
            TextAsset dataset = AssetDatabase.LoadAssetAtPath<TextAsset>(DatasetPath);
            TextAsset schema = AssetDatabase.LoadAssetAtPath<TextAsset>(SchemaPath);
            var errors = new List<string>();
            if (dataset == null)
                errors.Add($"Dataset 파일이 없습니다: {DatasetPath}");
            if (schema == null)
                errors.Add($"Schema 파일이 없습니다: {SchemaPath}");
            if (errors.Count > 0)
                return errors.ToArray();

            errors.AddRange(JsonSchemaSubsetValidator.Validate(dataset.text, schema.text));
            GuideDatasetData data;
            try
            {
                data = JsonUtility.FromJson<GuideDatasetData>(dataset.text);
            }
            catch (ArgumentException exception)
            {
                errors.Add("Runtime JsonUtility 구문 오류: " + exception.Message);
                return errors.ToArray();
            }

            GuideValidationIssue[] semanticIssues = GuideDatasetValidator.Validate(data);
            for (int index = 0; index < semanticIssues.Length; index++)
                errors.Add(semanticIssues[index].ToString());

            FrontManagerGuideDatasetAsset reference =
                AssetDatabase.LoadAssetAtPath<FrontManagerGuideDatasetAsset>(
                    FrontManagerGuideDatasetAssetGenerator.AssetPath);
            if (reference == null)
            {
                errors.Add($"Runtime 참조 Asset이 없습니다: {FrontManagerGuideDatasetAssetGenerator.AssetPath}");
            }
            else
            {
                if (reference.Dataset != dataset)
                    errors.Add("Runtime 참조 Asset의 Dataset 연결이 정본과 다릅니다.");
                if (reference.Schema != schema)
                    errors.Add("Runtime 참조 Asset의 Schema 연결이 정본과 다릅니다.");
            }
            return errors.ToArray();
        }
    }

    /// <summary>Player Build가 잘못된 Guide 데이터로 진행되지 않도록 CI와 같은 검증을 실행한다.</summary>
    public sealed class FrontManagerGuideBuildValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            string[] errors = FrontManagerGuideDataValidation.Validate();
            if (errors.Length > 0)
                throw new BuildFailedException("Front Manager Guide 검증 실패:\n" + string.Join("\n", errors));
        }
    }

    /// <summary>Dataset 또는 Schema가 바뀐 직후 상호 참조 오류를 Console에 노출한다.</summary>
    public sealed class FrontManagerGuideAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (!Contains(importedAssets, FrontManagerGuideDataValidation.DatasetPath) &&
                !Contains(importedAssets, FrontManagerGuideDataValidation.SchemaPath))
                return;
            string[] errors = FrontManagerGuideDataValidation.Validate();
            for (int index = 0; index < errors.Length; index++)
                Debug.LogError("[FrontManagerGuideValidation] " + errors[index]);
        }

        private static bool Contains(string[] values, string expected)
        {
            for (int index = 0; index < values.Length; index++)
                if (string.Equals(values[index], expected, StringComparison.Ordinal))
                    return true;
            return false;
        }
    }

    /// <summary>원본 JSON을 옮기지 않고 Resources 참조 Asset으로 Player Build에 포함한다.</summary>
    public static class FrontManagerGuideDatasetAssetGenerator
    {
        public const string DirectoryPath = "Assets/10.Datas/Resources/FrontManager";
        public const string AssetPath = DirectoryPath + "/FrontManagerGuideDataset.asset";

        [BaseballEditorTool(
            "프로젝트 기반",
            "Front Manager Guide 참조 생성",
            "원본 JSON과 Schema를 Runtime Resources 참조 Asset에 연결합니다.",
            order: 22,
            impact: ToolImpact.DataWrite)]
        public static void EnsureAsset()
        {
            TextAsset dataset = AssetDatabase.LoadAssetAtPath<TextAsset>(FrontManagerGuideDataValidation.DatasetPath);
            TextAsset schema = AssetDatabase.LoadAssetAtPath<TextAsset>(FrontManagerGuideDataValidation.SchemaPath);
            if (dataset == null || schema == null)
                throw new InvalidOperationException("Front Manager Dataset 또는 Schema 파일이 없습니다.");

            System.IO.Directory.CreateDirectory(DirectoryPath);
            FrontManagerGuideDatasetAsset asset =
                AssetDatabase.LoadAssetAtPath<FrontManagerGuideDatasetAsset>(AssetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<FrontManagerGuideDatasetAsset>();
                AssetDatabase.CreateAsset(asset, AssetPath);
            }
            asset.Configure(dataset, schema);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[FrontManagerGuideDatasetAssetGenerator] 연결 완료: {AssetPath}");
        }
    }
}
