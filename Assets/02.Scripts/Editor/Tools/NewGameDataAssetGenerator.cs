using System.IO;
using Baseball.Game.Data;
using UnityEditor;
using UnityEngine;

namespace Baseball.Editor.Tools
{
    /// <summary>
    /// 새 게임 기본 정적 정의를 표준 데이터 경로에 한 번만 생성한다.
    /// </summary>
    public static class NewGameDataAssetGenerator
    {
        public const string AssetPath = "Assets/10.Datas/Resources/NewGame/NewGameDefinition.asset";

        [BaseballEditorTool(
            "프로젝트 기반",
            "새 게임 기본 데이터 생성",
            "구단 후보와 생성 밸런스를 보관할 NewGameDefinition Asset이 없을 때 생성합니다.",
            order: 20,
            impact: ToolImpact.BulkWrite)]
        public static void EnsureDefaultAsset()
        {
            if (AssetDatabase.LoadAssetAtPath<NewGameDefinition>(AssetPath) != null)
            {
                Debug.Log($"[NewGameDataAssetGenerator] 기존 Asset을 유지합니다: {AssetPath}");
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
            var definition = ScriptableObject.CreateInstance<NewGameDefinition>();
            AssetDatabase.CreateAsset(definition, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[NewGameDataAssetGenerator] 기본 Asset을 생성했습니다: {AssetPath}");
        }
    }
}
