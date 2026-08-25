using System;

namespace Baseball.Editor.Tools
{
    /// <summary>
    /// Baseball Tool Launcher가 정적 에디터 도구를 자동 발견하기 위한 메타데이터다.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class BaseballEditorToolAttribute : Attribute
    {
        public BaseballEditorToolAttribute(
            string category,
            string displayName,
            string description,
            int order = 0,
            ToolImpact impact = ToolImpact.ReadOnly)
        {
            Category = category;
            DisplayName = displayName;
            Description = description;
            Order = order;
            Impact = impact;
        }

        public string Category { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public int Order { get; }
        public ToolImpact Impact { get; }
    }

    /// <summary>
    /// 실행 전 사용자가 알아야 할 에셋 변경 범위를 표시한다.
    /// </summary>
    public enum ToolImpact
    {
        ReadOnly,
        DataWrite,
        BulkWrite,
        Destructive
    }
}
