using System.IO;
using Baseball.Editor.Tools;
using Baseball.Game.SceneFlow;
using Baseball.Presentation.SceneFlow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Baseball.Editor.SceneFlow
{
    /// <summary>
    /// 표준 Scene 네 개와 Build Settings 순서를 동일한 규칙으로 생성한다.
    /// </summary>
    public static class SceneFlowSceneGenerator
    {
        public const string SceneRoot = "Assets/01.Scenes";
        public const string BootScenePath = SceneRoot + "/Boot.unity";
        public const string LoadingScenePath = SceneRoot + "/Loading.unity";
        public const string ManagementScenePath = SceneRoot + "/Management.unity";
        public const string MatchScenePath = SceneRoot + "/Match.unity";

        [BaseballEditorTool(
            "프로젝트 기반",
            "표준 Scene Flow 생성",
            "Boot, Loading, Management, Match Scene을 다시 만들고 Build Settings 순서를 연결합니다.",
            order: 10,
            impact: ToolImpact.BulkWrite)]
        public static void GenerateAll()
        {
            if (!Application.isBatchMode &&
                !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Directory.CreateDirectory(SceneRoot);
            AssetDatabase.Refresh();

            CreateScene(
                SceneId.Boot,
                BootScenePath,
                new Color(0.015f, 0.025f, 0.045f),
                root => root.AddComponent<BootSceneController>());
            CreateScene(
                SceneId.Loading,
                LoadingScenePath,
                new Color(0.025f, 0.04f, 0.065f),
                root => root.AddComponent<LoadingSceneController>());
            CreateScene(
                SceneId.Management,
                ManagementScenePath,
                new Color(0.035f, 0.08f, 0.11f),
                additionalSetup: null);
            CreateScene(
                SceneId.Match,
                MatchScenePath,
                new Color(0.035f, 0.12f, 0.07f),
                additionalSetup: null);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootScenePath, enabled: true),
                new EditorBuildSettingsScene(LoadingScenePath, enabled: true),
                new EditorBuildSettingsScene(ManagementScenePath, enabled: true),
                new EditorBuildSettingsScene(MatchScenePath, enabled: true)
            };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Debug.Log("[SceneFlowSceneGenerator] 표준 Scene 네 개와 Build Settings 연결을 완료했습니다.");
        }

        private static void CreateScene(
            SceneId sceneId,
            string scenePath,
            Color backgroundColor,
            System.Action<GameObject> additionalSetup)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var contextObject = new GameObject($"{sceneId}SceneContext");
            SceneContext context = contextObject.AddComponent<SceneContext>();
            var serializedContext = new SerializedObject(context);
            serializedContext.FindProperty("_sceneId").enumValueIndex = (int)sceneId;
            serializedContext.ApplyModifiedPropertiesWithoutUndo();
            additionalSetup?.Invoke(contextObject);

            CreateCamera(backgroundColor);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, scenePath))
                throw new IOException($"Scene 저장에 실패했습니다: {scenePath}");
        }

        private static void CreateCamera(Color backgroundColor)
        {
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = backgroundColor;
            camera.orthographic = true;
            camera.orthographicSize = 5f;
        }
    }
}
