using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace Baseball.Editor.Tools
{
    /// <summary>
    /// 선수 아카이브 → Runtime 콘텐츠 → World History의 세 단계를 한 창에서 순서대로 실행한다.
    /// 개별 도구가 이미 있는데 창을 따로 두는 이유는 실행 순서와 최신 여부가 이 작업의 본질이기 때문이다.
    /// 앞 단계를 다시 만들고 뒤를 두면 뒤 산출물이 조용히 무효가 되고, 게임은 느려지기만 한다.
    /// </summary>
    public sealed class HistoricalContentPipelineWindow : EditorWindow
    {
        private const string UvPathPreferenceKey = "Baseball.HistoricalPipeline.UvPath";
        private const string YearsPreferenceKey = "Baseball.HistoricalPipeline.Years";
        private const string SeedPreferenceKey = "Baseball.HistoricalPipeline.Seed";

        private readonly HistoricalContentPipelineRunner _runner = new HistoricalContentPipelineRunner();
        private readonly HistoricalCanonicalBakeOptions _options = new HistoricalCanonicalBakeOptions();
        private readonly HashSet<HistoricalContentPipelineStepId> _selected =
            new HashSet<HistoricalContentPipelineStepId>();

        private HistoricalContentPipelineStatusReport _status;
        private Vector2 _scroll;
        private Vector2 _logScroll;
        private bool _showOptions = true;
        private bool _wasRunning;

        [BaseballEditorTool(
            "데이터",
            "역사 콘텐츠 파이프라인",
            "선수 아카이브 굽기 → Runtime 콘텐츠 내보내기 → World History 굽기를 순서대로 실행하고 최신 여부를 확인합니다.",
            order: 0,
            impact: ToolImpact.BulkWrite)]
        public static void Open()
        {
            var window = GetWindow<HistoricalContentPipelineWindow>("역사 콘텐츠 파이프라인");
            window.minSize = new Vector2(680f, 560f);
            window.Show();
        }

        private void OnEnable()
        {
            _options.UvExecutablePath = EditorPrefs.GetString(UvPathPreferenceKey, "uv");
            _options.Years = EditorPrefs.GetString(YearsPreferenceKey, "1982-2025");
            _options.GenerationSeed = EditorPrefs.GetInt(SeedPreferenceKey, 20260901);
            _runner.Changed += HandleRunnerChanged;
        }

        private void OnDisable()
        {
            _runner.Changed -= HandleRunnerChanged;
            EditorPrefs.SetString(UvPathPreferenceKey, _options.UvExecutablePath);
            EditorPrefs.SetString(YearsPreferenceKey, _options.Years);
            EditorPrefs.SetInt(SeedPreferenceKey, _options.GenerationSeed);
        }

        /// <summary>실행이 끝난 직후의 상태가 사용자가 가장 알고 싶어 하는 값이므로 그때 자동으로 다시 검사한다.</summary>
        private void HandleRunnerChanged()
        {
            if (_wasRunning && !_runner.IsRunning)
                RefreshStatus();
            _wasRunning = _runner.IsRunning;
            Repaint();
        }

        private void RefreshStatus()
        {
            try
            {
                EditorUtility.DisplayProgressBar("역사 콘텐츠 파이프라인", "산출물 최신 여부를 확인하는 중…", 0.5f);
                _status = HistoricalContentPipelineStatus.Inspect();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            // 낡았거나 없는 단계만 기본 선택한다. 멀쩡한 단계를 다시 굽는 데 몇 분을 쓸 이유가 없다.
            _selected.Clear();
            IReadOnlyList<HistoricalContentPipelineStep> steps = _runner.Steps;
            for (int index = 0; index < steps.Count; index++)
            {
                HistoricalContentFreshness freshness = _status.GetFreshness(steps[index].Id);
                if (freshness == HistoricalContentFreshness.Stale ||
                    freshness == HistoricalContentFreshness.Missing)
                {
                    _selected.Add(steps[index].Id);
                }
            }
            Repaint();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawHeader();
            DrawCanonicalBakeOptions();
            DrawSteps();
            DrawBakeKeys();
            DrawControls();
            EditorGUILayout.EndScrollView();
            DrawLog();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("역사 콘텐츠 파이프라인", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "뒤 단계는 앞 단계 결과의 해시를 Key로 쓴다. 앞만 다시 만들면 뒤 산출물은 조용히 무효가 되고,\n" +
                "게임은 정상 동작하면서 새 게임 시작만 수십 배 느려진다.",
                EditorStyles.wordWrappedMiniLabel);
            if (_status == null)
            {
                EditorGUILayout.HelpBox(
                    "산출물 최신 여부를 아직 확인하지 않았습니다. 콘텐츠를 읽어야 하므로 몇 초 걸립니다.",
                    MessageType.None);
                using (new EditorGUI.DisabledScope(_runner.IsRunning))
                {
                    if (GUILayout.Button("상태 확인", GUILayout.Height(24f)))
                        RefreshStatus();
                }
            }
            else if (!string.IsNullOrEmpty(_status.ContentHash))
            {
                EditorGUILayout.LabelField("현재 ContentHash", _status.ContentHash);
            }
            EditorGUILayout.Space(4f);
        }

        private void DrawCanonicalBakeOptions()
        {
            using (new EditorGUI.DisabledScope(_runner.IsRunning))
            {
                _showOptions = EditorGUILayout.Foldout(_showOptions, "1단계 실행 인자", true);
                if (!_showOptions)
                    return;
                EditorGUI.indentLevel++;
                _options.UvExecutablePath = EditorGUILayout.TextField(
                    new GUIContent("uv 실행 파일", "PATH에 없으면 절대 경로를 넣습니다."),
                    _options.UvExecutablePath);
                _options.Years = EditorGUILayout.TextField("연도 범위", _options.Years);
                _options.GenerationSeed = EditorGUILayout.IntField(
                    new GUIContent("Generation Seed", "Archive 정렬·검증 호환용이며 능력치를 무작위로 바꾸지 않습니다."),
                    _options.GenerationSeed);
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(4f);
            }
        }

        private void DrawSteps()
        {
            IReadOnlyList<HistoricalContentPipelineStep> steps = _runner.Steps;
            for (int index = 0; index < steps.Count; index++)
                DrawStep(steps[index]);
        }

        private void DrawStep(HistoricalContentPipelineStep step)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool isSelected = _selected.Contains(step.Id);
                    using (new EditorGUI.DisabledScope(_runner.IsRunning))
                    {
                        bool nextSelected = EditorGUILayout.ToggleLeft(step.Title, isSelected, EditorStyles.boldLabel);
                        if (nextSelected != isSelected)
                        {
                            if (nextSelected) _selected.Add(step.Id);
                            else _selected.Remove(step.Id);
                        }
                    }
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField(
                        DescribeState(step), GUILayout.Width(160f));
                }

                EditorGUILayout.LabelField(step.Summary, EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.LabelField(step.InputPath + "  →  " + step.OutputPath, EditorStyles.miniLabel);

                if (_status != null)
                {
                    HistoricalContentFreshness freshness = _status.GetFreshness(step.Id);
                    EditorGUILayout.HelpBox(
                        DescribeFreshness(freshness) + "  " + _status.GetDetail(step.Id),
                        ToMessageType(freshness));
                }
                if (!string.IsNullOrEmpty(step.Message))
                    EditorGUILayout.HelpBox(step.Message, MessageType.Error);

                using (new EditorGUI.DisabledScope(_runner.IsRunning))
                {
                    if (GUILayout.Button("이 단계만 실행", GUILayout.Width(120f)))
                        _runner.Start(new[] { step.Id }, _options);
                }
            }
        }

        private void DrawBakeKeys()
        {
            if (_status == null || _status.BakeKeyLines.Count == 0)
                return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Bake Key 대조", EditorStyles.boldLabel);
                for (int index = 0; index < _status.BakeKeyLines.Count; index++)
                    EditorGUILayout.LabelField(_status.BakeKeyLines[index], EditorStyles.miniLabel);
            }
        }

        private void DrawControls()
        {
            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_runner.IsRunning || _selected.Count == 0))
                {
                    if (GUILayout.Button("선택 단계 순서대로 실행", GUILayout.Height(28f)))
                        _runner.Start(SortSelected(), _options);
                }
                using (new EditorGUI.DisabledScope(!_runner.IsRunning))
                {
                    if (GUILayout.Button("취소", GUILayout.Height(28f), GUILayout.Width(80f)))
                        _runner.Cancel();
                }
                using (new EditorGUI.DisabledScope(_runner.IsRunning))
                {
                    if (GUILayout.Button("상태 새로고침", GUILayout.Height(28f), GUILayout.Width(110f)))
                        RefreshStatus();
                }
            }

            if (_runner.IsRunning)
            {
                EditorGUILayout.HelpBox(
                    "실행 중입니다. 2·3단계는 에디터를 잠시 멈춥니다. 취소는 파이썬 단계에서만 즉시 반영됩니다.",
                    MessageType.Info);
            }
        }

        /// <summary>선택 순서와 무관하게 파이프라인 정의 순서로 실행한다.</summary>
        private List<HistoricalContentPipelineStepId> SortSelected()
        {
            var ordered = new List<HistoricalContentPipelineStepId>(_selected.Count);
            IReadOnlyList<HistoricalContentPipelineStep> steps = _runner.Steps;
            for (int index = 0; index < steps.Count; index++)
                if (_selected.Contains(steps[index].Id))
                    ordered.Add(steps[index].Id);
            return ordered;
        }

        private void DrawLog()
        {
            EditorGUILayout.Space(2f);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("실행 로그", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("지우기", GUILayout.Width(60f)))
                    _runner.ClearLog();
            }
            _logScroll = EditorGUILayout.BeginScrollView(_logScroll, GUILayout.Height(180f));
            EditorGUILayout.TextArea(_runner.Log, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private static string DescribeState(HistoricalContentPipelineStep step)
        {
            switch (step.State)
            {
                case HistoricalContentPipelineStepState.Running:
                    return "실행 중…";
                case HistoricalContentPipelineStepState.Succeeded:
                    return "완료 " + step.ElapsedSeconds.ToString("F1", CultureInfo.InvariantCulture) + "초";
                case HistoricalContentPipelineStepState.Failed:
                    return "실패";
                case HistoricalContentPipelineStepState.Canceled:
                    return "취소됨";
                default:
                    return string.Empty;
            }
        }

        private static string DescribeFreshness(HistoricalContentFreshness freshness)
        {
            switch (freshness)
            {
                case HistoricalContentFreshness.UpToDate: return "최신";
                case HistoricalContentFreshness.Stale: return "낡음 — 다시 만들어야 합니다.";
                case HistoricalContentFreshness.Missing: return "산출물 없음";
                default: return "확인 불가";
            }
        }

        private static MessageType ToMessageType(HistoricalContentFreshness freshness)
        {
            switch (freshness)
            {
                case HistoricalContentFreshness.UpToDate: return MessageType.Info;
                case HistoricalContentFreshness.Stale: return MessageType.Warning;
                case HistoricalContentFreshness.Missing: return MessageType.Warning;
                default: return MessageType.None;
            }
        }
    }
}
