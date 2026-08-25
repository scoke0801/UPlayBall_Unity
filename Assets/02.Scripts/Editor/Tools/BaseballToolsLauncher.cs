using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Baseball.Editor.Tools
{
    /// <summary>
    /// 프로젝트 에디터 도구를 검색하고 한곳에서 실행하는 통합 창이다.
    /// </summary>
    public sealed class BaseballToolsLauncher : EditorWindow
    {
        private readonly List<ToolDescriptor> _tools = new();
        private ToolbarSearchField _searchField;
        private ScrollView _toolList;

        [MenuItem("Baseball/툴 런처", priority = 0)]
        public static void Open()
        {
            var window = GetWindow<BaseballToolsLauncher>("Baseball Tools");
            window.minSize = new Vector2(620f, 420f);
            window.Show();
        }

        private void OnEnable()
        {
            DiscoverTools();
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 10f;
            root.style.paddingRight = 10f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;

            var title = new Label("Baseball Tool Launcher");
            title.style.fontSize = 18f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 4f;
            root.Add(title);

            var guide = new Label("밸런스, 데이터, UI 저작 도구를 이 창에 모읍니다.");
            guide.style.color = EditorGUIUtility.isProSkin
                ? new Color(0.72f, 0.72f, 0.72f)
                : new Color(0.3f, 0.3f, 0.3f);
            guide.style.marginBottom = 8f;
            root.Add(guide);

            var toolbar = new Toolbar();
            _searchField = new ToolbarSearchField();
            _searchField.style.flexGrow = 1f;
            _searchField.RegisterValueChangedCallback(_ => RebuildToolList());
            toolbar.Add(_searchField);

            var refreshButton = new ToolbarButton(() =>
            {
                DiscoverTools();
                RebuildToolList();
            })
            {
                text = "새로고침"
            };
            toolbar.Add(refreshButton);
            root.Add(toolbar);

            _toolList = new ScrollView();
            _toolList.style.flexGrow = 1f;
            _toolList.style.marginTop = 8f;
            root.Add(_toolList);

            RebuildToolList();
        }

        private void DiscoverTools()
        {
            _tools.Clear();

            foreach (MethodInfo method in TypeCache.GetMethodsWithAttribute<BaseballEditorToolAttribute>())
            {
                BaseballEditorToolAttribute metadata = method.GetCustomAttribute<BaseballEditorToolAttribute>();
                if (metadata == null)
                    continue;

                if (!method.IsStatic || method.ReturnType != typeof(void) || method.GetParameters().Length != 0)
                {
                    Debug.LogError(
                        $"[BaseballToolsLauncher] {method.DeclaringType?.FullName}.{method.Name}은 " +
                        "static void 무인자 메서드여야 합니다.");
                    continue;
                }

                _tools.Add(new ToolDescriptor(method, metadata));
            }

            _tools.Sort(ToolDescriptor.Compare);
        }

        private void RebuildToolList()
        {
            if (_toolList == null)
                return;

            _toolList.Clear();
            string query = _searchField?.value?.Trim() ?? string.Empty;
            List<ToolDescriptor> visibleTools = _tools
                .Where(tool => tool.Matches(query))
                .ToList();

            if (visibleTools.Count == 0)
            {
                _toolList.Add(new HelpBox("조건에 맞는 도구가 없습니다.", HelpBoxMessageType.Info));
                return;
            }

            foreach (IGrouping<string, ToolDescriptor> category in visibleTools.GroupBy(tool => tool.Category))
            {
                var categoryLabel = new Label(category.Key);
                categoryLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                categoryLabel.style.fontSize = 14f;
                categoryLabel.style.marginTop = 10f;
                categoryLabel.style.marginBottom = 4f;
                _toolList.Add(categoryLabel);

                foreach (ToolDescriptor tool in category)
                    _toolList.Add(CreateToolRow(tool));
            }
        }

        private VisualElement CreateToolRow(ToolDescriptor tool)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 4f;
            row.style.paddingLeft = 8f;
            row.style.paddingRight = 8f;
            row.style.paddingTop = 6f;
            row.style.paddingBottom = 6f;
            row.style.backgroundColor = EditorGUIUtility.isProSkin
                ? new Color(0.22f, 0.22f, 0.22f)
                : new Color(0.87f, 0.87f, 0.87f);

            var textContainer = new VisualElement();
            textContainer.style.flexGrow = 1f;

            var nameLabel = new Label(tool.DisplayName);
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            textContainer.Add(nameLabel);

            var descriptionLabel = new Label(tool.Description);
            descriptionLabel.style.whiteSpace = WhiteSpace.Normal;
            descriptionLabel.style.fontSize = 11f;
            textContainer.Add(descriptionLabel);
            row.Add(textContainer);

            var impactLabel = new Label(GetImpactLabel(tool.Impact));
            impactLabel.style.minWidth = 64f;
            impactLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            impactLabel.style.color = GetImpactColor(tool.Impact);
            row.Add(impactLabel);

            var executeButton = new Button(() => Execute(tool))
            {
                text = "실행"
            };
            executeButton.style.width = 70f;
            row.Add(executeButton);
            return row;
        }

        private static void Execute(ToolDescriptor tool)
        {
            if (tool.Impact == ToolImpact.Destructive)
            {
                bool confirmed = EditorUtility.DisplayDialog(
                    "파괴적 도구 실행",
                    $"{tool.DisplayName}\n\n되돌리기 어려운 변경이 생길 수 있습니다. 계속할까요?",
                    "실행",
                    "취소");
                if (!confirmed)
                    return;
            }

            try
            {
                tool.Method.Invoke(null, null);
            }
            catch (TargetInvocationException exception)
            {
                Debug.LogException(exception.InnerException ?? exception);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static string GetImpactLabel(ToolImpact impact)
        {
            return impact switch
            {
                ToolImpact.ReadOnly => "검사",
                ToolImpact.DataWrite => "에셋 변경",
                ToolImpact.BulkWrite => "대량 변경",
                ToolImpact.Destructive => "주의",
                _ => string.Empty
            };
        }

        private static Color GetImpactColor(ToolImpact impact)
        {
            return impact switch
            {
                ToolImpact.DataWrite => new Color(0.35f, 0.72f, 1f),
                ToolImpact.BulkWrite => new Color(1f, 0.65f, 0.2f),
                ToolImpact.Destructive => new Color(1f, 0.35f, 0.3f),
                _ => EditorGUIUtility.isProSkin ? Color.white : Color.black
            };
        }

        private sealed class ToolDescriptor
        {
            public ToolDescriptor(MethodInfo method, BaseballEditorToolAttribute metadata)
            {
                Method = method;
                Category = metadata.Category;
                DisplayName = metadata.DisplayName;
                Description = metadata.Description;
                Order = metadata.Order;
                Impact = metadata.Impact;
            }

            public MethodInfo Method { get; }
            public string Category { get; }
            public string DisplayName { get; }
            public string Description { get; }
            public int Order { get; }
            public ToolImpact Impact { get; }

            public bool Matches(string query)
            {
                if (string.IsNullOrEmpty(query))
                    return true;

                return Category.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || Description.Contains(query, StringComparison.OrdinalIgnoreCase);
            }

            public static int Compare(ToolDescriptor left, ToolDescriptor right)
            {
                int categoryResult = string.Compare(left.Category, right.Category, StringComparison.Ordinal);
                if (categoryResult != 0)
                    return categoryResult;

                int orderResult = left.Order.CompareTo(right.Order);
                return orderResult != 0
                    ? orderResult
                    : string.Compare(left.DisplayName, right.DisplayName, StringComparison.Ordinal);
            }
        }
    }
}
