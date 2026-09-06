using System.Reflection;
using System.Text.Json;

// 콘솔 파일 입력만 대체한다. DTO 검증·Definition 매핑·경기와 시즌 실행은 프로젝트 원본을 사용한다.
// 이 어셈블리는 Unity에 포함하지 않으며 Unity JsonUtility 호환성 테스트를 대신하지 않는다.
namespace UnityEngine;

public sealed class SerializeField : Attribute { }
public class ScriptableObject { }

public sealed class TextAsset
{
    public TextAsset(string text) { bytes = System.Text.Encoding.UTF8.GetBytes(text); }
    public byte[] bytes { get; }
}

internal static class JsonUtility
{
    public static T FromJson<T>(string text)
    {
        using var document = JsonDocument.Parse(text);
        return (T)Read(document.RootElement, typeof(T));
    }

    private static object Read(JsonElement node, Type type)
    {
        if (node.ValueKind == JsonValueKind.Null) return null;
        if (type == typeof(string)) return node.GetString();
        if (type.IsPrimitive || type == typeof(decimal)) return JsonSerializer.Deserialize(node.GetRawText(), type);
        if (type.IsArray)
        {
            Type itemType = type.GetElementType();
            var result = Array.CreateInstance(itemType, node.GetArrayLength());
            int index = 0;
            foreach (var item in node.EnumerateArray()) result.SetValue(Read(item, itemType), index++);
            return result;
        }

        object value = Activator.CreateInstance(type, true);
        for (Type current = type; current != null; current = current.BaseType)
        {
            foreach (var field in current.GetFields(BindingFlags.Instance | BindingFlags.NonPublic |
                BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                if (node.TryGetProperty(field.Name, out var item)) field.SetValue(value, Read(item, field.FieldType));
            }
        }
        return value;
    }
}
