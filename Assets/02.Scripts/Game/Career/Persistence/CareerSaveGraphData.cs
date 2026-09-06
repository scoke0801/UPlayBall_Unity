using System;

namespace Baseball.Game.Career.Persistence
{
    [Serializable]
    public sealed class CareerSaveGraphData
    {
        public int formatVersion;
        public int rootId;
        public CareerSaveGraphNodeData[] nodes;
    }

    [Serializable]
    public sealed class CareerSaveGraphNodeData
    {
        public int id;
        public string typeName;
        public int kind;
        public CareerSaveGraphFieldData[] fields;
        public CareerSaveGraphValueData[] items;
        public CareerSaveGraphEntryData[] entries;
    }

    [Serializable]
    public sealed class CareerSaveGraphFieldData
    {
        public string declaringTypeName;
        public string fieldName;
        public CareerSaveGraphValueData value;
    }

    [Serializable]
    public sealed class CareerSaveGraphEntryData
    {
        public CareerSaveGraphValueData key;
        public CareerSaveGraphValueData value;
    }

    [Serializable]
    public sealed class CareerSaveGraphValueData
    {
        public int kind;
        public string typeName;
        public string scalar;
        public int referenceId;
    }

    /// <summary>현재 Balance처럼 로드 때 다시 연결할 필드를 그래프 Payload에서 제외한다.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class CareerSaveIgnoreAttribute : Attribute
    {
    }
}
