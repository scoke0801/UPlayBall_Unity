using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace Baseball.Game.Career.Diagnostics
{
    /// <summary>
    /// 공개 세이브 상태를 고정 순서로 직렬화해 자동완료 전후 비교용 checksum을 만든다.
    /// </summary>
    public static class CareerStateChecksum
    {
        public const int FormatVersion = 1;

        /// <summary>
        /// 같은 공개 상태와 참조 구조가 같은 SHA-256 checksum을 만들도록 계산한다.
        /// </summary>
        public static string Calculate(CareerState career)
        {
            if (career == null)
                throw new ArgumentNullException(nameof(career));

            var payload = new StringBuilder(1_048_576);
            payload.Append("career-state-checksum-v").Append(FormatVersion).Append('|');
            var visited = new Dictionary<object, int>(ReferenceEqualityComparer.Instance);
            AppendValue(career, payload, visited);

            byte[] bytes = Encoding.UTF8.GetBytes(payload.ToString());
            using SHA256 sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(bytes);
            var result = new StringBuilder(hash.Length * 2);
            for (int index = 0; index < hash.Length; index++)
                result.Append(hash[index].ToString("x2", CultureInfo.InvariantCulture));
            return result.ToString();
        }

        private static void AppendValue(
            object value,
            StringBuilder output,
            Dictionary<object, int> visited)
        {
            if (value == null)
            {
                output.Append("null;");
                return;
            }

            Type type = value.GetType();
            if (TryAppendScalar(value, type, output))
                return;

            bool tracksReference = !type.IsValueType;
            if (tracksReference)
            {
                if (visited.TryGetValue(value, out int referenceId))
                {
                    output.Append("ref:").Append(referenceId).Append(';');
                    return;
                }

                visited.Add(value, visited.Count + 1);
            }

            if (value is IDictionary dictionary)
            {
                AppendDictionary(type, dictionary, output);
                return;
            }

            if (value is IEnumerable enumerable)
            {
                AppendEnumerable(type, enumerable, output, visited);
                return;
            }

            output.Append("object:").Append(type.FullName).Append('{');
            PropertyInfo[] properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            Array.Sort(properties, CompareProperties);
            for (int index = 0; index < properties.Length; index++)
            {
                PropertyInfo property = properties[index];
                if (!property.CanRead || property.GetIndexParameters().Length != 0)
                    continue;

                output.Append(property.Name).Append('=');
                AppendValue(property.GetValue(value), output, visited);
            }
            output.Append("};");
        }

        private static bool TryAppendScalar(object value, Type type, StringBuilder output)
        {
            if (value is string text)
            {
                output.Append("string:").Append(text.Length).Append(':').Append(text).Append(';');
                return true;
            }
            if (value is bool boolean)
            {
                output.Append(boolean ? "bool:1;" : "bool:0;");
                return true;
            }
            if (value is char character)
            {
                output.Append("char:").Append((int)character).Append(';');
                return true;
            }
            if (type.IsEnum)
            {
                output.Append("enum:").Append(type.FullName).Append(':')
                    .Append(Convert.ToInt64(value, CultureInfo.InvariantCulture)).Append(';');
                return true;
            }
            if (value is DateTime dateTime)
            {
                output.Append("datetime:").Append(dateTime.Ticks).Append(':')
                    .Append((int)dateTime.Kind).Append(';');
                return true;
            }
            if (value is DateTimeOffset dateTimeOffset)
            {
                output.Append("datetimeoffset:").Append(dateTimeOffset.Ticks).Append(':')
                    .Append(dateTimeOffset.Offset.Ticks).Append(';');
                return true;
            }
            if (value is Guid guid)
            {
                output.Append("guid:").Append(guid.ToString("N")).Append(';');
                return true;
            }
            if (value is double doubleValue)
            {
                output.Append("double:").Append(BitConverter.DoubleToInt64Bits(doubleValue)).Append(';');
                return true;
            }
            if (value is float floatValue)
            {
                output.Append("float:").Append(BitConverter.ToInt32(BitConverter.GetBytes(floatValue), 0))
                    .Append(';');
                return true;
            }
            if (value is decimal decimalValue)
            {
                int[] bits = decimal.GetBits(decimalValue);
                output.Append("decimal:");
                for (int index = 0; index < bits.Length; index++)
                    output.Append(bits[index]).Append(',');
                output.Append(';');
                return true;
            }
            if (value is byte or sbyte or short or ushort or int or uint or long or ulong)
            {
                output.Append("number:")
                    .Append(Convert.ToString(value, CultureInfo.InvariantCulture))
                    .Append(';');
                return true;
            }

            return false;
        }

        private static void AppendDictionary(Type type, IDictionary dictionary, StringBuilder output)
        {
            var entries = new List<string>(dictionary.Count);
            foreach (DictionaryEntry entry in dictionary)
            {
                var entryOutput = new StringBuilder();
                var entryVisited = new Dictionary<object, int>(ReferenceEqualityComparer.Instance);
                AppendValue(entry.Key, entryOutput, entryVisited);
                entryOutput.Append('=');
                AppendValue(entry.Value, entryOutput, entryVisited);
                entries.Add(entryOutput.ToString());
            }
            entries.Sort(StringComparer.Ordinal);
            output.Append("dictionary:").Append(type.FullName).Append('[');
            for (int index = 0; index < entries.Count; index++)
                output.Append(entries[index]);
            output.Append("]; ");
        }

        private static void AppendEnumerable(
            Type type,
            IEnumerable enumerable,
            StringBuilder output,
            Dictionary<object, int> visited)
        {
            output.Append("sequence:").Append(type.FullName).Append('[');
            foreach (object item in enumerable)
                AppendValue(item, output, visited);
            output.Append("]; ");
        }

        private static int CompareProperties(PropertyInfo left, PropertyInfo right)
        {
            return string.CompareOrdinal(left.Name, right.Name);
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

            public new bool Equals(object left, object right) => ReferenceEquals(left, right);

            public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
        }
    }
}
