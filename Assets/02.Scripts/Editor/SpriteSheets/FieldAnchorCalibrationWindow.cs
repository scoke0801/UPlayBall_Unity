using System;
using Baseball.Editor.Tools;
using Baseball.Presentation.Match.Sprites;
using UnityEditor;
using UnityEngine;

namespace Baseball.Editor.SpriteSheets
{
    /// <summary>빈 경기장 위에서 좌상단 기준 정규화 앵커를 보정한다.</summary>
    public sealed class FieldAnchorCalibrationWindow : EditorWindow
    {
        private FieldLayoutDefinition _layout;
        private Texture2D _reference;
        private FieldAnchor _selected;

        [BaseballEditorTool("경기 표현", "경기장 앵커 보정", "배경 위를 클릭해 수비 위치와 투타 구도를 보정합니다.", 31, ToolImpact.DataWrite)]
        public static void Open() => GetWindow<FieldAnchorCalibrationWindow>("경기장 앵커").Show();

        private void OnEnable() => _layout = AssetDatabase.LoadAssetAtPath<FieldLayoutDefinition>(SpriteSheetBatchImporter.LayoutPath);

        private void OnGUI()
        {
            _layout = (FieldLayoutDefinition)EditorGUILayout.ObjectField("경기장 정의", _layout, typeof(FieldLayoutDefinition), false);
            _reference = (Texture2D)EditorGUILayout.ObjectField("구도 비교 시안", _reference, typeof(Texture2D), false);
            if (_layout == null) { EditorGUILayout.HelpBox("스프라이트 처리 도구에서 먼저 가져오기를 실행하세요.", MessageType.Info); return; }
            _selected = (FieldAnchor)EditorGUILayout.EnumPopup("선택 앵커", _selected);
            EditorGUI.BeginChangeCheck();
            Vector2 point = EditorGUILayout.Vector2Field("좌상단 기준 위치", _layout.GetAnchor(_selected));
            float height = EditorGUILayout.FloatField("홈 선수 높이", _layout.foregroundHeight);
            float scale = EditorGUILayout.Slider("외야 크기 비율", _layout.depthScale, 0.05f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_layout, "경기장 앵커 보정");
                SetAnchor(point);
                _layout.foregroundHeight = Mathf.Max(1, height);
                _layout.depthScale = scale;
                EditorUtility.SetDirty(_layout);
            }
            if (GUILayout.Button("보정 저장")) AssetDatabase.SaveAssets();
            EditorGUILayout.HelpBox("배경 클릭: 선택 앵커 이동. 시안은 비교 전용입니다.", MessageType.Info);
            if (_layout.background == null) return;
            Rect area = GUILayoutUtility.GetRect(100, Mathf.Max(180, position.height - 230), GUILayout.ExpandWidth(true));
            if (_reference != null)
            {
                GUI.DrawTexture(new Rect(area.x + area.width * 0.5f, area.y, area.width * 0.5f, area.height), _reference, ScaleMode.ScaleToFit);
                area.width *= 0.5f;
            }
            Rect image = Fit(area, _layout.background.width, _layout.background.height);
            GUI.DrawTexture(image, _layout.background, ScaleMode.StretchToFill);
            foreach (FieldAnchor id in Enum.GetValues(typeof(FieldAnchor)))
            {
                Vector2 screen = image.position + Vector2.Scale(image.size, _layout.GetAnchor(id));
                EditorGUI.DrawRect(new Rect(screen.x - 3, screen.y - 3, 6, 6), id == _selected ? Color.yellow : Color.cyan);
                GUI.Label(new Rect(screen.x + 4, screen.y - 10, 130, 20), id.ToString());
            }
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && image.Contains(Event.current.mousePosition))
            {
                Undo.RecordObject(_layout, "경기장 앵커 이동");
                Vector2 local = Event.current.mousePosition - image.position;
                SetAnchor(new Vector2(local.x / image.width, local.y / image.height));
                EditorUtility.SetDirty(_layout);
                Event.current.Use();
            }
        }

        private void SetAnchor(Vector2 point)
        {
            point = new Vector2(Mathf.Clamp01(point.x), Mathf.Clamp01(point.y));
            var anchors = _layout.anchors ?? Array.Empty<FieldAnchorDefinition>();
            foreach (FieldAnchorDefinition anchor in anchors)
                if (anchor != null && anchor.id == _selected) { anchor.normalizedPosition = point; return; }
            Array.Resize(ref anchors, anchors.Length + 1);
            anchors[anchors.Length - 1] = new FieldAnchorDefinition { id = _selected, normalizedPosition = point };
            _layout.anchors = anchors;
        }

        internal static Rect Fit(Rect area, int width, int height)
        {
            float scale = Mathf.Min(area.width / width, area.height / height);
            Vector2 size = new Vector2(width * scale, height * scale);
            return new Rect(area.center - size * 0.5f, size);
        }
    }
}
