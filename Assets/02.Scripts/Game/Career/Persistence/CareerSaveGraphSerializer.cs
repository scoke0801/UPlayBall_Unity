using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace Baseball.Game.Career.Persistence
{
    /// <summary>
    /// 순수 C# 커리어 Aggregate를 참조 보존 그래프 DTO로 변환한다.
    /// 저장 가능한 타입을 Baseball 상태와 기본 컬렉션으로 제한해 런타임 서비스 유입을 차단한다.
    /// </summary>
    internal sealed class CareerSaveGraphSerializer
    {
        private const int CurrentFormatVersion = 1;

        private enum ValueKind
        {
            Null,
            Scalar,
            Reference
        }

        private enum NodeKind
        {
            Object,
            Array,
            List,
            Dictionary,
            HashSet
        }

        public CareerSaveGraphData Capture(object root)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));

            var context = new CaptureContext();
            CareerSaveGraphValueData rootValue = CaptureValue(root, context);
            if (rootValue.kind != (int)ValueKind.Reference)
                throw new InvalidOperationException("커리어 세이브 루트는 참조 타입이어야 합니다.");

            return new CareerSaveGraphData
            {
                formatVersion = CurrentFormatVersion,
                rootId = rootValue.referenceId,
                nodes = context.Nodes.ToArray()
            };
        }

        public T Restore<T>(CareerSaveGraphData graph) where T : class
        {
            if (graph == null)
                throw new InvalidOperationException("커리어 세이브 Payload가 없습니다.");
            if (graph.formatVersion != CurrentFormatVersion)
                throw new CareerSaveCompatibilityException(
                    $"세이브 그래프 버전 {graph.formatVersion}은 현재 버전과 호환되지 않습니다.");
            if (graph.nodes == null || graph.nodes.Length == 0)
                throw new InvalidOperationException("커리어 세이브 Payload가 비어 있습니다.");

            var context = new RestoreContext(graph.nodes);
            for (int index = 0; index < graph.nodes.Length; index++)
            {
                CareerSaveGraphNodeData node = RequireNode(graph.nodes[index], index + 1);
                Type type = ResolveAllowedType(node.typeName);
                context.Objects[node.id] = CreateNodeInstance(node, type);
                context.Types[node.id] = type;
            }

            for (int index = 0; index < graph.nodes.Length; index++)
                PopulateNode(graph.nodes[index], context);

            if (graph.rootId <= 0 || graph.rootId >= context.Objects.Length)
                throw new InvalidOperationException("커리어 세이브 루트 참조가 잘못되었습니다.");
            return context.Objects[graph.rootId] as T ??
                   throw new InvalidOperationException("커리어 세이브 루트 타입이 잘못되었습니다.");
        }

        private static CareerSaveGraphValueData CaptureValue(object value, CaptureContext context)
        {
            if (value == null)
                return new CareerSaveGraphValueData { kind = (int)ValueKind.Null };

            Type type = value.GetType();
            if (TryFormatScalar(value, type, out string scalar))
            {
                return new CareerSaveGraphValueData
                {
                    kind = (int)ValueKind.Scalar,
                    typeName = GetTypeName(type),
                    scalar = scalar
                };
            }

            EnsureAllowedType(type);
            if (!type.IsValueType && context.ReferenceIds.TryGetValue(value, out int existingId))
                return CreateReference(existingId);

            int id = context.Nodes.Count + 1;
            if (!type.IsValueType)
                context.ReferenceIds.Add(value, id);

            var node = new CareerSaveGraphNodeData
            {
                id = id,
                typeName = GetTypeName(type),
                kind = (int)GetNodeKind(type)
            };
            context.Nodes.Add(node);
            PopulateCapturedNode(node, value, type, context);
            return CreateReference(id);
        }

        private static void PopulateCapturedNode(
            CareerSaveGraphNodeData node,
            object value,
            Type type,
            CaptureContext context)
        {
            NodeKind kind = (NodeKind)node.kind;
            if (kind is NodeKind.Array or NodeKind.List or NodeKind.HashSet)
            {
                var items = new List<CareerSaveGraphValueData>();
                foreach (object item in (IEnumerable)value)
                    items.Add(CaptureValue(item, context));
                node.items = items.ToArray();
                return;
            }

            if (kind == NodeKind.Dictionary)
            {
                var entries = new List<CareerSaveGraphEntryData>();
                foreach (DictionaryEntry entry in (IDictionary)value)
                {
                    entries.Add(new CareerSaveGraphEntryData
                    {
                        key = CaptureValue(entry.Key, context),
                        value = CaptureValue(entry.Value, context)
                    });
                }
                node.entries = entries.ToArray();
                return;
            }

            FieldInfo[] fields = GetSerializableFields(type);
            var result = new CareerSaveGraphFieldData[fields.Length];
            for (int index = 0; index < fields.Length; index++)
            {
                FieldInfo field = fields[index];
                result[index] = new CareerSaveGraphFieldData
                {
                    declaringTypeName = GetTypeName(field.DeclaringType),
                    fieldName = field.Name,
                    value = CaptureValue(field.GetValue(value), context)
                };
            }
            node.fields = result;
        }

        private static object CreateNodeInstance(CareerSaveGraphNodeData node, Type type)
        {
            NodeKind kind = RequireNodeKind(node.kind);
            int count = kind == NodeKind.Dictionary
                ? node.entries?.Length ?? 0
                : node.items?.Length ?? 0;

            return kind switch
            {
                NodeKind.Array => Array.CreateInstance(type.GetElementType() ??
                    throw new InvalidOperationException("배열 원소 타입이 없습니다."), count),
                NodeKind.List or NodeKind.Dictionary or NodeKind.HashSet =>
                    Activator.CreateInstance(type) ??
                    throw new InvalidOperationException($"{type.FullName} 컬렉션을 생성하지 못했습니다."),
                NodeKind.Object when type.IsValueType => Activator.CreateInstance(type),
                NodeKind.Object => FormatterServices.GetUninitializedObject(type),
                _ => throw new InvalidOperationException("지원하지 않는 세이브 노드입니다.")
            };
        }

        private static void PopulateNode(CareerSaveGraphNodeData node, RestoreContext context)
        {
            if (context.IsPopulated[node.id])
                return;
            if (context.IsPopulating[node.id])
            {
                if (context.Types[node.id].IsValueType)
                    throw new InvalidOperationException("값 형식 세이브 노드에 순환 참조가 있습니다.");
                return;
            }

            context.IsPopulating[node.id] = true;
            PopulateNodeCore(node, context);
            context.IsPopulating[node.id] = false;
            context.IsPopulated[node.id] = true;
        }

        private static void PopulateNodeCore(CareerSaveGraphNodeData node, RestoreContext context)
        {
            object target = context.Objects[node.id];
            Type type = context.Types[node.id];
            NodeKind kind = RequireNodeKind(node.kind);
            if (kind == NodeKind.Array)
            {
                Array array = (Array)target;
                Type elementType = type.GetElementType();
                CareerSaveGraphValueData[] items = node.items ?? Array.Empty<CareerSaveGraphValueData>();
                for (int index = 0; index < items.Length; index++)
                    array.SetValue(RestoreValue(items[index], elementType, context), index);
                return;
            }

            if (kind == NodeKind.List)
            {
                IList list = (IList)target;
                Type elementType = type.GetGenericArguments()[0];
                CareerSaveGraphValueData[] items = node.items ?? Array.Empty<CareerSaveGraphValueData>();
                for (int index = 0; index < items.Length; index++)
                    list.Add(RestoreValue(items[index], elementType, context));
                return;
            }

            if (kind == NodeKind.Dictionary)
            {
                IDictionary dictionary = (IDictionary)target;
                Type[] arguments = type.GetGenericArguments();
                CareerSaveGraphEntryData[] entries = node.entries ?? Array.Empty<CareerSaveGraphEntryData>();
                for (int index = 0; index < entries.Length; index++)
                {
                    dictionary.Add(
                        RestoreValue(entries[index].key, arguments[0], context),
                        RestoreValue(entries[index].value, arguments[1], context));
                }
                return;
            }

            if (kind == NodeKind.HashSet)
            {
                Type elementType = type.GetGenericArguments()[0];
                MethodInfo add = type.GetMethod("Add", new[] { elementType }) ??
                                 throw new InvalidOperationException("HashSet.Add를 찾지 못했습니다.");
                CareerSaveGraphValueData[] items = node.items ?? Array.Empty<CareerSaveGraphValueData>();
                for (int index = 0; index < items.Length; index++)
                    add.Invoke(target, new[] { RestoreValue(items[index], elementType, context) });
                return;
            }

            CareerSaveGraphFieldData[] fields = node.fields ?? Array.Empty<CareerSaveGraphFieldData>();
            for (int index = 0; index < fields.Length; index++)
            {
                CareerSaveGraphFieldData fieldData = fields[index] ??
                    throw new InvalidOperationException("세이브 필드가 null입니다.");
                Type declaringType = ResolveAllowedType(fieldData.declaringTypeName);
                if (!declaringType.IsAssignableFrom(type))
                    throw new InvalidOperationException("세이브 필드 선언 타입이 노드 타입과 맞지 않습니다.");
                FieldInfo field = declaringType.GetField(
                    fieldData.fieldName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly) ??
                    throw new CareerSaveCompatibilityException(
                        $"현재 코드에서 세이브 필드 {declaringType.FullName}.{fieldData.fieldName}을 찾지 못했습니다.");
                if (field.IsStatic || field.IsNotSerialized ||
                    field.GetCustomAttribute<CareerSaveIgnoreAttribute>() != null)
                    throw new InvalidOperationException("저장 제외 필드가 Payload에 포함되어 있습니다.");
                field.SetValue(target, RestoreValue(fieldData.value, field.FieldType, context));
            }
        }

        private static object RestoreValue(
            CareerSaveGraphValueData value,
            Type expectedType,
            RestoreContext context)
        {
            if (value == null)
                throw new InvalidOperationException("세이브 값이 null DTO입니다.");
            ValueKind kind = (ValueKind)value.kind;
            if (kind == ValueKind.Null)
            {
                if (expectedType.IsValueType && Nullable.GetUnderlyingType(expectedType) == null)
                    throw new InvalidOperationException($"{expectedType.FullName} 값은 null일 수 없습니다.");
                return null;
            }

            object restored;
            if (kind == ValueKind.Scalar)
            {
                Type actualType = ResolveAllowedType(value.typeName);
                restored = ParseScalar(value.scalar, actualType);
            }
            else if (kind == ValueKind.Reference)
            {
                if (value.referenceId <= 0 || value.referenceId >= context.Objects.Length)
                    throw new InvalidOperationException("세이브 참조 ID가 범위를 벗어났습니다.");
                if (context.Types[value.referenceId].IsValueType)
                    PopulateNode(context.Nodes[value.referenceId - 1], context);
                restored = context.Objects[value.referenceId];
            }
            else
            {
                throw new InvalidOperationException("세이브 값 종류가 잘못되었습니다.");
            }

            Type nullableType = Nullable.GetUnderlyingType(expectedType);
            if (nullableType != null)
            {
                if (restored == null)
                    return null;
                if (!nullableType.IsInstanceOfType(restored))
                    throw new InvalidOperationException("Nullable 세이브 값 타입이 맞지 않습니다.");
                return Activator.CreateInstance(expectedType, restored);
            }
            if (restored != null && !expectedType.IsInstanceOfType(restored))
                throw new InvalidOperationException(
                    $"세이브 값 {restored.GetType().FullName}을 {expectedType.FullName}에 넣을 수 없습니다.");
            return restored;
        }

        private static FieldInfo[] GetSerializableFields(Type type)
        {
            var fields = new List<FieldInfo>();
            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo[] declared = current.GetFields(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly);
                for (int index = 0; index < declared.Length; index++)
                {
                    FieldInfo field = declared[index];
                    if (field.IsStatic || field.IsNotSerialized ||
                        field.GetCustomAttribute<CareerSaveIgnoreAttribute>() != null)
                        continue;
                    fields.Add(field);
                }
            }
            fields.Sort((left, right) =>
            {
                int typeOrder = string.CompareOrdinal(
                    left.DeclaringType?.FullName,
                    right.DeclaringType?.FullName);
                return typeOrder != 0 ? typeOrder : string.CompareOrdinal(left.Name, right.Name);
            });
            return fields.ToArray();
        }

        private static NodeKind GetNodeKind(Type type)
        {
            if (type.IsArray)
                return NodeKind.Array;
            if (type.IsGenericType)
            {
                Type definition = type.GetGenericTypeDefinition();
                if (definition == typeof(List<>)) return NodeKind.List;
                if (definition == typeof(Dictionary<,>)) return NodeKind.Dictionary;
                if (definition == typeof(HashSet<>)) return NodeKind.HashSet;
            }
            return NodeKind.Object;
        }

        private static NodeKind RequireNodeKind(int value)
        {
            if (!Enum.IsDefined(typeof(NodeKind), value))
                throw new InvalidOperationException("세이브 노드 종류가 잘못되었습니다.");
            return (NodeKind)value;
        }

        private static CareerSaveGraphNodeData RequireNode(CareerSaveGraphNodeData node, int expectedId)
        {
            if (node == null || node.id != expectedId)
                throw new InvalidOperationException("세이브 노드 ID 순서가 잘못되었습니다.");
            RequireNodeKind(node.kind);
            return node;
        }

        private static CareerSaveGraphValueData CreateReference(int id) => new()
        {
            kind = (int)ValueKind.Reference,
            referenceId = id
        };

        private static string GetTypeName(Type type) =>
            type?.AssemblyQualifiedName ?? throw new InvalidOperationException("세이브 타입 이름이 없습니다.");

        private static Type ResolveAllowedType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                throw new InvalidOperationException("세이브 타입 이름이 비어 있습니다.");
            Type type = Type.GetType(typeName, throwOnError: false) ??
                        throw new CareerSaveCompatibilityException(
                            $"현재 코드에서 세이브 타입 {typeName}을 찾지 못했습니다.");
            EnsureAllowedType(type);
            return type;
        }

        private static void EnsureAllowedType(Type type)
        {
            if (IsScalarType(type) || type.IsArray)
                return;
            if (type.IsGenericType)
            {
                Type definition = type.GetGenericTypeDefinition();
                if (definition == typeof(List<>) || definition == typeof(Dictionary<,>) ||
                    definition == typeof(HashSet<>))
                    return;
            }
            string typeNamespace = type.Namespace ?? string.Empty;
            if (typeNamespace == "Baseball" || typeNamespace.StartsWith("Baseball.", StringComparison.Ordinal))
                return;
            throw new InvalidOperationException($"{type.FullName} 타입은 커리어 세이브에 포함할 수 없습니다.");
        }

        private static bool IsScalarType(Type type) =>
            type.IsEnum || type == typeof(string) || type == typeof(bool) || type == typeof(char) ||
            type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) ||
            type == typeof(ushort) || type == typeof(int) || type == typeof(uint) ||
            type == typeof(long) || type == typeof(ulong) || type == typeof(float) ||
            type == typeof(double) || type == typeof(decimal) || type == typeof(DateTime) ||
            type == typeof(DateTimeOffset) || type == typeof(TimeSpan) || type == typeof(Guid);

        private static bool TryFormatScalar(object value, Type type, out string scalar)
        {
            scalar = null;
            if (!IsScalarType(type))
                return false;
            if (type == typeof(string)) scalar = (string)value;
            else if (type == typeof(bool)) scalar = (bool)value ? "1" : "0";
            else if (type == typeof(char)) scalar = ((int)(char)value).ToString(CultureInfo.InvariantCulture);
            else if (type.IsEnum)
            {
                Type underlying = Enum.GetUnderlyingType(type);
                bool isUnsigned = underlying == typeof(byte) || underlying == typeof(ushort) ||
                                  underlying == typeof(uint) || underlying == typeof(ulong);
                scalar = isUnsigned
                    ? "u:" + Convert.ToUInt64(value, CultureInfo.InvariantCulture)
                        .ToString(CultureInfo.InvariantCulture)
                    : "s:" + Convert.ToInt64(value, CultureInfo.InvariantCulture)
                        .ToString(CultureInfo.InvariantCulture);
            }
            else if (type == typeof(float)) scalar = BitConverter.ToInt32(BitConverter.GetBytes((float)value), 0)
                .ToString(CultureInfo.InvariantCulture);
            else if (type == typeof(double)) scalar = BitConverter.DoubleToInt64Bits((double)value)
                .ToString(CultureInfo.InvariantCulture);
            else if (type == typeof(decimal)) scalar = string.Join(",", decimal.GetBits((decimal)value));
            else if (type == typeof(DateTime))
            {
                DateTime date = (DateTime)value;
                scalar = date.Ticks.ToString(CultureInfo.InvariantCulture) + "," +
                         ((int)date.Kind).ToString(CultureInfo.InvariantCulture);
            }
            else if (type == typeof(DateTimeOffset))
            {
                DateTimeOffset date = (DateTimeOffset)value;
                scalar = date.Ticks.ToString(CultureInfo.InvariantCulture) + "," +
                         date.Offset.Ticks.ToString(CultureInfo.InvariantCulture);
            }
            else if (type == typeof(TimeSpan)) scalar = ((TimeSpan)value).Ticks.ToString(CultureInfo.InvariantCulture);
            else if (type == typeof(Guid)) scalar = ((Guid)value).ToString("N");
            else scalar = Convert.ToString(value, CultureInfo.InvariantCulture);
            return true;
        }

        private static object ParseScalar(string scalar, Type type)
        {
            scalar ??= string.Empty;
            if (type == typeof(string)) return scalar;
            if (type == typeof(bool)) return scalar == "1";
            if (type == typeof(char)) return (char)int.Parse(scalar, CultureInfo.InvariantCulture);
            if (type.IsEnum)
            {
                if (scalar.StartsWith("u:", StringComparison.Ordinal))
                    return Enum.ToObject(type, ulong.Parse(scalar.Substring(2), CultureInfo.InvariantCulture));
                if (scalar.StartsWith("s:", StringComparison.Ordinal))
                    return Enum.ToObject(type, long.Parse(scalar.Substring(2), CultureInfo.InvariantCulture));
                throw new InvalidOperationException("enum 세이브 값이 잘못되었습니다.");
            }
            if (type == typeof(byte)) return byte.Parse(scalar, CultureInfo.InvariantCulture);
            if (type == typeof(sbyte)) return sbyte.Parse(scalar, CultureInfo.InvariantCulture);
            if (type == typeof(short)) return short.Parse(scalar, CultureInfo.InvariantCulture);
            if (type == typeof(ushort)) return ushort.Parse(scalar, CultureInfo.InvariantCulture);
            if (type == typeof(int)) return int.Parse(scalar, CultureInfo.InvariantCulture);
            if (type == typeof(uint)) return uint.Parse(scalar, CultureInfo.InvariantCulture);
            if (type == typeof(long)) return long.Parse(scalar, CultureInfo.InvariantCulture);
            if (type == typeof(ulong)) return ulong.Parse(scalar, CultureInfo.InvariantCulture);
            if (type == typeof(float)) return BitConverter.ToSingle(
                BitConverter.GetBytes(int.Parse(scalar, CultureInfo.InvariantCulture)), 0);
            if (type == typeof(double)) return BitConverter.Int64BitsToDouble(
                long.Parse(scalar, CultureInfo.InvariantCulture));
            if (type == typeof(decimal))
            {
                string[] parts = scalar.Split(',');
                if (parts.Length != 4)
                    throw new InvalidOperationException("decimal 세이브 값이 잘못되었습니다.");
                return new decimal(new[]
                {
                    int.Parse(parts[0], CultureInfo.InvariantCulture),
                    int.Parse(parts[1], CultureInfo.InvariantCulture),
                    int.Parse(parts[2], CultureInfo.InvariantCulture),
                    int.Parse(parts[3], CultureInfo.InvariantCulture)
                });
            }
            if (type == typeof(DateTime))
            {
                string[] parts = scalar.Split(',');
                return new DateTime(long.Parse(parts[0], CultureInfo.InvariantCulture),
                    (DateTimeKind)int.Parse(parts[1], CultureInfo.InvariantCulture));
            }
            if (type == typeof(DateTimeOffset))
            {
                string[] parts = scalar.Split(',');
                return new DateTimeOffset(
                    long.Parse(parts[0], CultureInfo.InvariantCulture),
                    new TimeSpan(long.Parse(parts[1], CultureInfo.InvariantCulture)));
            }
            if (type == typeof(TimeSpan)) return new TimeSpan(long.Parse(scalar, CultureInfo.InvariantCulture));
            if (type == typeof(Guid)) return Guid.ParseExact(scalar, "N");
            throw new InvalidOperationException($"{type.FullName} scalar 복원을 지원하지 않습니다.");
        }

        private sealed class CaptureContext
        {
            public readonly List<CareerSaveGraphNodeData> Nodes = new();
            public readonly Dictionary<object, int> ReferenceIds = new(ReferenceEqualityComparer.Instance);
        }

        private sealed class RestoreContext
        {
            public RestoreContext(CareerSaveGraphNodeData[] nodes)
            {
                Nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
                Objects = new object[nodes.Length + 1];
                Types = new Type[nodes.Length + 1];
                IsPopulated = new bool[nodes.Length + 1];
                IsPopulating = new bool[nodes.Length + 1];
            }

            public CareerSaveGraphNodeData[] Nodes { get; }
            public object[] Objects { get; }
            public Type[] Types { get; }
            public bool[] IsPopulated { get; }
            public bool[] IsPopulating { get; }
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new();
            public new bool Equals(object left, object right) => ReferenceEquals(left, right);
            public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
        }
    }
}
