using System.Collections.Generic;
using System.IO;
using System.Text;
using Baseball.Editor.Tools;
using Baseball.Presentation.UI;
using UnityEditor;
using UnityEngine;

namespace Baseball.Editor.UI
{
    /// <summary>
    /// UI 기반 프리팹 생성과 정적 검증을 통합 런처에 제공한다.
    /// </summary>
    public static class UIEditorTools
    {
        private const string UiPrefabRoot = "Assets/03.Prefabs/UI";
        private const string UiRootPrefabPath = "Assets/Resources/UI/UI_System_Root.prefab";

        [BaseballEditorTool(
            "UI",
            "UI 스크립트 생성기",
            "프로젝트 네이밍과 namespace 규칙에 맞는 UI 스크립트를 생성합니다.",
            order: 0,
            impact: ToolImpact.DataWrite)]
        public static void OpenScriptGenerator()
        {
            UIScriptGeneratorWindow.Open();
        }

        [BaseballEditorTool(
            "UI",
            "UI Root 프리팹 생성/갱신",
            "HUD, Scene, Popup, System Canvas를 가진 Resources 프리팹을 비파괴적으로 갱신합니다.",
            order: 10,
            impact: ToolImpact.DataWrite)]
        public static void CreateOrUpdateUiRootPrefab()
        {
            EnsureDirectory(Path.GetDirectoryName(UiRootPrefabPath));

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiRootPrefabPath);
            GameObject rootObject = prefab != null
                ? PrefabUtility.LoadPrefabContents(UiRootPrefabPath)
                : new GameObject("UI_System_Root", typeof(RectTransform), typeof(UIRoot));

            try
            {
                UIRoot root = rootObject.GetComponent<UIRoot>();
                if (root == null)
                    root = rootObject.AddComponent<UIRoot>();

                root.BuildMissingLayers();
                PrefabUtility.SaveAsPrefabAsset(rootObject, UiRootPrefabPath);
            }
            finally
            {
                if (prefab != null)
                    PrefabUtility.UnloadPrefabContents(rootObject);
                else
                    Object.DestroyImmediate(rootObject);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[UIEditorTools] UI Root 준비 완료: {UiRootPrefabPath}");
        }

        [BaseballEditorTool(
            "UI",
            "선택 UI를 프리팹으로 저장",
            "선택한 UIBase 오브젝트를 레이어별 표준 폴더에 프리팹으로 저장합니다.",
            order: 20,
            impact: ToolImpact.DataWrite)]
        public static void SaveSelectedUiAsPrefab()
        {
            GameObject selected = Selection.activeGameObject;
            UIBase ui = selected != null ? selected.GetComponent<UIBase>() : null;
            if (ui == null)
            {
                EditorUtility.DisplayDialog("UI 프리팹 저장", "UIBase가 붙은 루트 GameObject를 선택하세요.", "확인");
                return;
            }

            string expectedPrefix = GetExpectedPrefix(ui.Layer);
            if (!selected.name.StartsWith(expectedPrefix, System.StringComparison.Ordinal))
            {
                EditorUtility.DisplayDialog(
                    "이름 규칙 불일치",
                    $"{ui.Layer} UI 이름은 {expectedPrefix} 접두사로 시작해야 합니다.",
                    "확인");
                return;
            }

            string folder = $"{UiPrefabRoot}/{ui.Layer}";
            EnsureDirectory(folder);
            string prefabPath = $"{folder}/{selected.name}.prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(selected, prefabPath, InteractionMode.UserAction);
            AssetDatabase.SaveAssets();
            Debug.Log($"[UIEditorTools] UI 프리팹 저장 완료: {prefabPath}");
        }

        [BaseballEditorTool(
            "UI",
            "UI 프리팹 구조 검증",
            "UI 이름, 레이어, 필수 컴포넌트와 중복 UI 타입을 검사합니다.",
            order: 100,
            impact: ToolImpact.ReadOnly)]
        public static void ValidateUiPrefabs()
        {
            if (!Directory.Exists(UiPrefabRoot))
            {
                Debug.Log("[UIEditorTools] UI 프리팹 폴더가 아직 없습니다.");
                return;
            }

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { UiPrefabRoot });
            var usedTypes = new Dictionary<System.Type, string>();
            var report = new StringBuilder();
            int issueCount = 0;
            int uiCount = 0;

            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                UIBase[] uiComponents = prefab.GetComponentsInChildren<UIBase>(true);
                for (int i = 0; i < uiComponents.Length; i++)
                {
                    UIBase ui = uiComponents[i];
                    uiCount++;

                    string expectedPrefix = GetExpectedPrefix(ui.Layer);
                    if (!ui.name.StartsWith(expectedPrefix, System.StringComparison.Ordinal))
                    {
                        AppendIssue(report, path, $"{ui.name}: {expectedPrefix} 접두사가 필요합니다.");
                        issueCount++;
                    }

                    if (ui.GetComponent<RectTransform>() == null || ui.GetComponent<CanvasGroup>() == null)
                    {
                        AppendIssue(report, path, $"{ui.name}: RectTransform 또는 CanvasGroup이 없습니다.");
                        issueCount++;
                    }

                    System.Type type = ui.GetType();
                    if (usedTypes.TryGetValue(type, out string previousPath))
                    {
                        AppendIssue(report, path, $"{type.Name} 타입이 {previousPath}에도 등록되어 있습니다.");
                        issueCount++;
                    }
                    else
                    {
                        usedTypes.Add(type, path);
                    }
                }
            }

            if (issueCount == 0)
                Debug.Log($"[UIEditorTools] 검증 완료: UI {uiCount}개, 문제 없음");
            else
                Debug.LogError($"[UIEditorTools] 검증 완료: UI {uiCount}개, 문제 {issueCount}개\n{report}");
        }

        internal static string GetExpectedPrefix(UILayer layer)
        {
            return layer switch
            {
                UILayer.HUD => "UI_HUD_",
                UILayer.Scene => "UI_Scene_",
                UILayer.Popup => "UI_Popup_",
                UILayer.System => "UI_System_",
                _ => "UI_"
            };
        }

        private static void AppendIssue(StringBuilder report, string path, string message)
        {
            report.Append("- ");
            report.Append(path);
            report.Append(": ");
            report.AppendLine(message);
        }

        private static void EnsureDirectory(string path)
        {
            if (string.IsNullOrEmpty(path) || Directory.Exists(path))
                return;

            Directory.CreateDirectory(path);
            AssetDatabase.Refresh();
        }
    }
}
