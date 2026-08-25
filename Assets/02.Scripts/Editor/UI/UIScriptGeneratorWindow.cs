using System.IO;
using System.Text;
using Baseball.Presentation.UI;
using UnityEditor;
using UnityEngine;

namespace Baseball.Editor.UI
{
    /// <summary>
    /// 반복되는 UI 클래스 이름과 폴더 구성을 안전하게 생성한다.
    /// </summary>
    public sealed class UIScriptGeneratorWindow : EditorWindow
    {
        private const string PresentationUiRoot = "Assets/02.Scripts/Presentation/UI";

        [SerializeField] private string _uiName = "Home";
        [SerializeField] private UILayer _layer = UILayer.Scene;

        public static void Open()
        {
            var window = GetWindow<UIScriptGeneratorWindow>("UI Script Generator");
            window.minSize = new Vector2(420f, 190f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("UI 스크립트 생성", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "코드 식별자만 입력하세요. 예: Home, PlayerDetail, MatchScoreboard",
                MessageType.Info);

            _uiName = EditorGUILayout.TextField("UI Name", _uiName);
            _layer = (UILayer)EditorGUILayout.EnumPopup("Layer", _layer);

            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(!IsValidIdentifier(_uiName)))
            {
                if (GUILayout.Button("스크립트 생성", GUILayout.Height(32f)))
                    CreateScript();
            }
        }

        private void CreateScript()
        {
            string prefix = UIEditorTools.GetExpectedPrefix(_layer);
            string className = prefix + _uiName;
            string category = _layer.ToString();
            string folder = $"{PresentationUiRoot}/{category}";
            string path = $"{folder}/{className}.cs";

            if (File.Exists(path))
            {
                EditorUtility.DisplayDialog("UI 스크립트 생성", $"이미 존재합니다.\n{path}", "확인");
                return;
            }

            Directory.CreateDirectory(folder);
            string baseType = _layer switch
            {
                UILayer.HUD => nameof(UIHudBase),
                UILayer.Scene => nameof(UISceneBase),
                UILayer.Popup => nameof(UIPopupBase),
                UILayer.System => nameof(UISystemBase),
                _ => nameof(UIBase)
            };
            string namespaceName = $"Baseball.Presentation.UI.{category}";
            string source = BuildSource(namespaceName, className, baseType);
            File.WriteAllText(path, source, new UTF8Encoding(false));
            AssetDatabase.Refresh();

            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            Selection.activeObject = script;
            EditorGUIUtility.PingObject(script);
            Debug.Log($"[UIScriptGenerator] 생성 완료: {path}");
        }

        private static string BuildSource(string namespaceName, string className, string baseType)
        {
            return
                $"using Baseball.Presentation.UI;\n\n" +
                $"namespace {namespaceName}\n" +
                "{\n" +
                "    /// <summary>\n" +
                $"    /// {className} 화면을 표시한다.\n" +
                "    /// </summary>\n" +
                $"    public sealed class {className} : {baseType}\n" +
                "    {\n" +
                "        protected override void OnInitialize()\n" +
                "        {\n" +
                "        }\n\n" +
                "        protected override void OnShow()\n" +
                "        {\n" +
                "        }\n\n" +
                "        protected override void OnHide()\n" +
                "        {\n" +
                "        }\n\n" +
                "        protected override void OnClose()\n" +
                "        {\n" +
                "        }\n" +
                "    }\n" +
                "}\n";
        }

        private static bool IsValidIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || (!char.IsLetter(value[0]) && value[0] != '_'))
                return false;

            for (int i = 1; i < value.Length; i++)
            {
                if (!char.IsLetterOrDigit(value[i]) && value[i] != '_')
                    return false;
            }

            return true;
        }
    }
}
