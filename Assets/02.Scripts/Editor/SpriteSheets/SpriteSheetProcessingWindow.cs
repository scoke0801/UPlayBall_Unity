using System;
using System.IO;
using Baseball.Editor.Tools;
using Baseball.Presentation.Match.Sprites;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Baseball.Editor.SpriteSheets
{
    /// <summary>검수 카탈로그의 순서·접지·사건을 미리 보고 승인된 모션만 배포한다.</summary>
    public sealed class SpriteSheetProcessingWindow : EditorWindow
    {
        private string _processedRoot = "output/sprite-sheet-ingame/Processed";
        private SpriteAnimationCatalog _catalog;
        private SerializedObject _serialized;
        private ReorderableList _frames;
        private int _clipIndex;
        private int _speedIndex;
        private bool _playing;
        private double _start;
        private Vector2 _scroll;
        private Texture2D _source;
        private string _sourcePath;
        private int _rows = 3;
        private int _cols = 4;
        private int _inspection;
        private Texture2D _inspectionTexture;
        private Sprite _inspectionSprite;
        private int _inspectionMode = -1;
        private SpriteAnimationEvent _anchorMarker = SpriteAnimationEvent.BallRelease;
        private Texture2D _eventSource;
        private string _eventSourcePath;
        private SpriteSheetBatchImporter.ProcessedClip _clipMetadata;
        private string _metadataPath;
        private DateTime _metadataWriteTime;

        [BaseballEditorTool("경기 표현", "스프라이트 시트 처리", "처리 결과를 가져오고 프레임 순서·접지·사건·손잡이를 검수합니다.", 30, ToolImpact.DataWrite)]
        public static void Open() => GetWindow<SpriteSheetProcessingWindow>("스프라이트 시트 처리").Show();

        private void OnEnable()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<SpriteAnimationCatalog>(SpriteSheetBatchImporter.ReviewPath);
            EditorApplication.update += Tick;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            if (_source != null) DestroyImmediate(_source);
            if (_inspectionTexture != null) DestroyImmediate(_inspectionTexture);
            if (_eventSource != null) DestroyImmediate(_eventSource);
        }

        private void Tick() { if (_playing) Repaint(); }

        private void OnGUI()
        {
            _processedRoot = EditorGUILayout.TextField("처리 결과 폴더", _processedRoot);
            if (GUILayout.Button("처리 결과 가져오기"))
            {
                try { _catalog = SpriteSheetBatchImporter.Import(_processedRoot); _serialized = null; }
                catch (Exception exception) { Debug.LogException(exception); ShowNotification(new GUIContent(exception.Message)); }
            }
            EditorGUI.BeginChangeCheck();
            _catalog = (SpriteAnimationCatalog)EditorGUILayout.ObjectField("검수 카탈로그", _catalog, typeof(SpriteAnimationCatalog), false);
            if (EditorGUI.EndChangeCheck()) _serialized = null;
            if (_catalog == null || _catalog.clips == null || _catalog.clips.Length == 0) return;
            var names = new string[_catalog.clips.Length];
            for (int i = 0; i < names.Length; i++) names[i] = _catalog.clips[i].clipId;
            int selected = EditorGUILayout.Popup("모션", Mathf.Clamp(_clipIndex, 0, names.Length - 1), names);
            if (_serialized == null || selected != _clipIndex)
            {
                _clipIndex = selected;
                BindFrames();
            }
            _serialized.Update();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            SerializedProperty clip = _serialized.FindProperty("clips").GetArrayElementAtIndex(_clipIndex);
            EditorGUILayout.PropertyField(clip.FindPropertyRelative("handedness"), new GUIContent("손잡이"));
            EditorGUILayout.PropertyField(clip.FindPropertyRelative("approved"), new GUIContent("시각 검수 승인"));
            EditorGUILayout.PropertyField(clip.FindPropertyRelative("loop"), new GUIContent("반복"));
            EditorGUILayout.LabelField("경기 등록 상태", _catalog.clips[_clipIndex].IsProductionReady ? "승인 조건 충족" : "NeedsReview · 손잡이/프레임/접점 확인 필요");
            EditorGUILayout.HelpBox("NeedsReview는 승인 체크와 관계없이 경기 카탈로그에서 차단됩니다. 여기서 변경한 순서·사건은 카탈로그 편집이며, 처리 결과를 재생성해 가져오면 원본 메타데이터가 다시 적용됩니다.", MessageType.Info);
            _frames.DoLayoutList();
            _serialized.ApplyModifiedProperties();
            DrawPreview(_catalog.clips[_clipIndex]);
            DrawEventAnchorCalibration(_catalog.clips[_clipIndex]);
            DrawSource();
            if (GUILayout.Button("검수 저장 · 승인 모션만 경기 카탈로그에 반영"))
            {
                try { SpriteSheetBatchImporter.Publish(_catalog); AssetDatabase.SaveAssets(); }
                catch (Exception exception) { Debug.LogException(exception); ShowNotification(new GUIContent(exception.Message)); }
            }
            EditorGUILayout.EndScrollView();
        }

        private void BindFrames()
        {
            _serialized = new SerializedObject(_catalog);
            SerializedProperty frames = _serialized.FindProperty("clips").GetArrayElementAtIndex(_clipIndex).FindPropertyRelative("frames");
            _frames = new ReorderableList(_serialized, frames, true, true, false, false);
            _frames.drawHeaderCallback = rect => GUI.Label(rect, "프레임 순서 (드래그) · 시간(ms) · 사건");
            _frames.elementHeightCallback = index => EditorGUI.GetPropertyHeight(frames.GetArrayElementAtIndex(index), true) + 6;
            _frames.drawElementCallback = (rect, index, active, focused) =>
            {
                rect.y += 2; rect.height -= 4;
                EditorGUI.PropertyField(rect, frames.GetArrayElementAtIndex(index), new GUIContent($"프레임 {index}"), true);
            };
            _frames.onReorderCallback = list =>
            {
                _serialized.ApplyModifiedProperties();
                SpriteClipDefinition clip = _catalog.clips[_clipIndex];
                foreach (SpriteEventAnchorDefinition anchor in clip.eventAnchors ?? Array.Empty<SpriteEventAnchorDefinition>())
                    if (anchor != null) anchor.frameIndex = FindEventFrame(clip, anchor.marker);
                EditorUtility.SetDirty(_catalog);
                _serialized.Update();
            };
        }

        private void DrawEventAnchorCalibration(SpriteClipDefinition clip)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("사건 접점 보정", EditorStyles.boldLabel);
            _anchorMarker = (SpriteAnimationEvent)EditorGUILayout.EnumPopup("사건", _anchorMarker);
            int frameIndex = FindEventFrame(clip, _anchorMarker);
            if (frameIndex < 0) { EditorGUILayout.HelpBox("이 모션에는 선택한 사건이 없습니다. 프레임 사건을 먼저 지정하세요.", MessageType.Info); return; }
            string metadataPath = Path.Combine(_processedRoot, clip.clipId, clip.clipId + ".json");
            if (!File.Exists(metadataPath)) { EditorGUILayout.HelpBox("원본 셀을 찾으려면 이 모션의 처리 결과 폴더를 지정하세요.", MessageType.Info); return; }
            DateTime modified = File.GetLastWriteTimeUtc(metadataPath);
            if (_metadataPath != metadataPath || _metadataWriteTime != modified)
            {
                _clipMetadata = JsonUtility.FromJson<SpriteSheetBatchImporter.ProcessedClip>(File.ReadAllText(metadataPath));
                _metadataPath = metadataPath; _metadataWriteTime = modified;
            }
            if (_clipMetadata == null || _clipMetadata.frames == null || !File.Exists(_clipMetadata.sourceFile)) return;
            // 카탈로그 재정렬 뒤에도 원래 처리 이미지 번호로 원본 셀을 찾는다.
            string filename = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(clip.frames[frameIndex].sprite));
            if (!filename.StartsWith("frame_", StringComparison.Ordinal) || !int.TryParse(filename.Substring(6), out int originalFrame) ||
                originalFrame < 0 || originalFrame >= _clipMetadata.frames.Length) return;
            SpriteSheetBatchImporter.ProcessedFrame sourceFrame = _clipMetadata.frames[originalFrame];
            SpriteSheetBatchImporter.CellRect cell = sourceFrame.sourceCellRect;
            if (cell == null || cell.width <= 0 || cell.height <= 0) return;
            if (_eventSourcePath != _clipMetadata.sourceFile)
            {
                if (_eventSource != null) DestroyImmediate(_eventSource);
                _eventSource = new Texture2D(2, 2); _eventSource.LoadImage(File.ReadAllBytes(_clipMetadata.sourceFile));
                _eventSourcePath = _clipMetadata.sourceFile;
            }
            SpriteEventAnchorDefinition anchor = FindAnchor(clip, _anchorMarker);
            EditorGUILayout.LabelField($"재생 프레임 {frameIndex} · 원본 셀 {sourceFrame.cellIndex}");
            EditorGUILayout.HelpBox("원본 셀의 손끝·배트·글러브를 클릭하면 해당 사건의 접점이 저장됩니다. 접점 보정은 모션 승인 상태를 바꾸지 않습니다.", MessageType.Info);
            if (anchor != null)
            {
                EditorGUI.BeginChangeCheck();
                Vector2 position = EditorGUILayout.Vector2Field("셀 좌상단 기준 접점", anchor.sourcePositionNormalized);
                if (EditorGUI.EndChangeCheck()) SetEventAnchor(clip, frameIndex, sourceFrame, position);
                if (GUILayout.Button("접점 보정 제거 · 기본 위치 사용"))
                {
                    Undo.RecordObject(_catalog, "사건 접점 제거");
                    var remaining = new System.Collections.Generic.List<SpriteEventAnchorDefinition>();
                    foreach (SpriteEventAnchorDefinition existing in clip.eventAnchors)
                        if (existing != null && existing.marker != _anchorMarker) remaining.Add(existing);
                    clip.eventAnchors = remaining.ToArray(); EditorUtility.SetDirty(_catalog); anchor = null;
                }
            }
            Rect image = FieldAnchorCalibrationWindow.Fit(GUILayoutUtility.GetRect(100, 230, GUILayout.ExpandWidth(true)), (int)cell.width, (int)cell.height);
            GUI.DrawTextureWithTexCoords(image, _eventSource, new Rect(cell.x / _eventSource.width,
                1f - (cell.y + cell.height) / _eventSource.height, cell.width / _eventSource.width, cell.height / _eventSource.height));
            Vector2 currentPivot = clip.frames[frameIndex].sprite.pivot / clip.frames[frameIndex].sprite.rect.size;
            float ground = image.y + (1f - currentPivot.y) * image.height;
            EditorGUI.DrawRect(new Rect(image.x, ground, image.width, 1), Color.yellow);
            if (anchor != null)
            {
                Vector2 point = image.position + Vector2.Scale(image.size, anchor.sourcePositionNormalized);
                EditorGUI.DrawRect(new Rect(point.x - 6, point.y, 13, 1), Color.red);
                EditorGUI.DrawRect(new Rect(point.x, point.y - 6, 1, 13), Color.red);
            }
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && image.Contains(Event.current.mousePosition))
            {
                Vector2 point = Event.current.mousePosition - image.position;
                SetEventAnchor(clip, frameIndex, sourceFrame, new Vector2(point.x / image.width, point.y / image.height));
                Event.current.Use();
            }
        }

        private void SetEventAnchor(SpriteClipDefinition clip, int frameIndex, SpriteSheetBatchImporter.ProcessedFrame source, Vector2 position)
        {
            Undo.RecordObject(_catalog, "사건 접점 보정");
            SpriteEventAnchorDefinition anchor = FindAnchor(clip, _anchorMarker);
            if (anchor == null)
            {
                var anchors = clip.eventAnchors ?? Array.Empty<SpriteEventAnchorDefinition>();
                Array.Resize(ref anchors, anchors.Length + 1);
                anchor = new SpriteEventAnchorDefinition { marker = _anchorMarker }; anchors[anchors.Length - 1] = anchor;
                clip.eventAnchors = anchors;
            }
            anchor.frameIndex = frameIndex;
            anchor.sourcePositionNormalized = new Vector2(Mathf.Clamp01(position.x), Mathf.Clamp01(position.y));
            anchor.sourceCellSize = new Vector2(source.sourceCellRect.width, source.sourceCellRect.height);
            Vector2 pivot = clip.frames[frameIndex].sprite.pivot / clip.frames[frameIndex].sprite.rect.size;
            anchor.sourceRootNormalized = new Vector2(pivot.x, 1f - pivot.y);
            EditorUtility.SetDirty(_catalog);
        }

        private static int FindEventFrame(SpriteClipDefinition clip, SpriteAnimationEvent marker)
        {
            for (int i = 0; i < clip.frames.Length; i++)
                if (clip.frames[i] != null && Array.IndexOf(clip.frames[i].events ?? Array.Empty<SpriteAnimationEvent>(), marker) >= 0) return i;
            return -1;
        }

        private static SpriteEventAnchorDefinition FindAnchor(SpriteClipDefinition clip, SpriteAnimationEvent marker)
        {
            foreach (SpriteEventAnchorDefinition anchor in clip.eventAnchors ?? Array.Empty<SpriteEventAnchorDefinition>())
                if (anchor != null && anchor.marker == marker) return anchor;
            return null;
        }

        private void DrawPreview(SpriteClipDefinition clip)
        {
            _speedIndex = GUILayout.Toolbar(_speedIndex, new[] { "1x", "2x", "4x" });
            bool play = GUILayout.Toggle(_playing, "미리보기 재생", "Button");
            if (play != _playing) { _playing = play; _start = EditorApplication.timeSinceStartup; }
            if (clip.frames == null || clip.frames.Length == 0) return;
            int index = Mathf.Clamp(_frames.index, 0, clip.frames.Length - 1);
            if (_playing)
            {
                double duration = clip.DurationSeconds;
                if (duration > 0)
                {
                    double time = (EditorApplication.timeSinceStartup - _start) * (1 << _speedIndex) % duration;
                    index = 0;
                    while (index < clip.frames.Length - 1 && time >= clip.frames[index].durationMs / 1000f)
                        time -= clip.frames[index++].durationMs / 1000f;
                }
            }
            SpriteFrameDefinition frame = clip.frames[index];
            if (frame.sprite == null) return;
            _inspection = GUILayout.Toolbar(_inspection, new[] { "투명 결과", "알파 경계", "녹색 잔여" });
            Rect area = GUILayoutUtility.GetRect(100, 260, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(area, new Color(0.17f, 0.17f, 0.19f));
            Rect image = FieldAnchorCalibrationWindow.Fit(area, (int)frame.sprite.rect.width, (int)frame.sprite.rect.height);
            Rect textureRect = frame.sprite.textureRect;
            if (_inspection == 0)
                GUI.DrawTextureWithTexCoords(image, frame.sprite.texture, new Rect(textureRect.x / frame.sprite.texture.width,
                    textureRect.y / frame.sprite.texture.height, textureRect.width / frame.sprite.texture.width,
                    textureRect.height / frame.sprite.texture.height));
            else DrawInspection(image, frame.sprite);
            Vector2 pivot = frame.sprite.pivot / frame.sprite.rect.size;
            float ground = image.yMax - pivot.y * image.height;
            EditorGUI.DrawRect(new Rect(image.x, ground, image.width, 1), Color.yellow);
            EditorGUI.DrawRect(new Rect(image.x + pivot.x * image.width, ground - 7, 1, 14), Color.cyan);
            EditorGUILayout.LabelField($"프레임 {index} · {string.Join(", ", frame.events ?? Array.Empty<SpriteAnimationEvent>())}");
            EditorGUI.BeginChangeCheck();
            Vector2 adjusted = EditorGUILayout.Vector2Field("Sprite pivot (좌하단)", pivot);
            if (EditorGUI.EndChangeCheck())
            {
                var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(frame.sprite)) as TextureImporter;
                if (importer != null)
                {
                    importer.spritePivot = new Vector2(Mathf.Clamp01(adjusted.x), Mathf.Clamp01(adjusted.y));
                    TextureImporterSettings settings = new TextureImporterSettings();
                    importer.ReadTextureSettings(settings); settings.spriteAlignment = (int)SpriteAlignment.Custom;
                    importer.SetTextureSettings(settings); importer.SaveAndReimport();
                    Undo.RecordObject(_catalog, "사건 접지 위치 보정");
                    foreach (SpriteEventAnchorDefinition anchor in clip.eventAnchors ?? Array.Empty<SpriteEventAnchorDefinition>())
                        if (anchor != null && anchor.frameIndex == index)
                            anchor.sourceRootNormalized = new Vector2(importer.spritePivot.x, 1f - importer.spritePivot.y);
                    EditorUtility.SetDirty(_catalog);
                }
            }
        }

        private void DrawInspection(Rect image, Sprite sprite)
        {
            if (_inspectionSprite != sprite || _inspectionMode != _inspection)
            {
                if (_inspectionTexture != null) DestroyImmediate(_inspectionTexture);
                _inspectionTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                _inspectionTexture.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite)));
                Color[] colors = _inspectionTexture.GetPixels();
                for (int i = 0; i < colors.Length; i++)
                {
                    Color color = colors[i];
                    float value = _inspection == 1 ? color.a : Mathf.Clamp01((color.g - Mathf.Max(color.r, color.b)) * 4) * color.a;
                    colors[i] = _inspection == 1 ? new Color(value, value, value, 1) : new Color(value, 0, 0, 1);
                }
                _inspectionTexture.SetPixels(colors); _inspectionTexture.Apply();
                _inspectionSprite = sprite; _inspectionMode = _inspection;
            }
            GUI.DrawTexture(image, _inspectionTexture);
        }

        private void DrawSource()
        {
            if (GUILayout.Button("원본 시트 비교 열기"))
            {
                string path = EditorUtility.OpenFilePanel("원본 시트", "docs/design/sprite_sheet_ingame", "png");
                if (!string.IsNullOrEmpty(path))
                {
                    if (_source != null) DestroyImmediate(_source);
                    _source = new Texture2D(2, 2); _source.LoadImage(File.ReadAllBytes(path)); _sourcePath = path;
                }
            }
            if (_source == null) return;
            EditorGUILayout.LabelField(Path.GetFileName(_sourcePath));
            _rows = Mathf.Max(1, EditorGUILayout.IntField("행", _rows));
            _cols = Mathf.Max(1, EditorGUILayout.IntField("열", _cols));
            Rect image = FieldAnchorCalibrationWindow.Fit(GUILayoutUtility.GetRect(100, 280, GUILayout.ExpandWidth(true)), _source.width, _source.height);
            GUI.DrawTexture(image, _source);
            for (int row = 0; row < _rows; row++)
                for (int col = 0; col < _cols; col++)
                {
                    Rect cell = new Rect(image.x + col * image.width / _cols, image.y + row * image.height / _rows, image.width / _cols, image.height / _rows);
                    EditorGUI.DrawRect(new Rect(cell.x, cell.y, cell.width, 1), Color.yellow);
                    EditorGUI.DrawRect(new Rect(cell.x, cell.y, 1, cell.height), Color.yellow);
                    GUI.Label(cell, (row * _cols + col).ToString());
                }
        }
    }
}
