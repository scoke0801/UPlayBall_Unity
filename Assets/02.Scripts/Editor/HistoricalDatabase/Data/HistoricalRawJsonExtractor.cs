using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Baseball.Editor.HistoricalDatabase
{
    /// <summary>원본 파일을 수정하지 않고 선택 Entity의 정확한 JSON object 범위를 찾아 반환한다.</summary>
    public static class HistoricalRawJsonExtractor
    {
        /// <summary>top-level 배열 또는 지정 Collection에서 한 문자열 ID가 일치하는 object를 찾는다.</summary>
        public static bool TryExtractObject(
            string filePath,
            string collectionProperty,
            string idProperty,
            string idValue,
            out string rawJson,
            out string error)
        {
            if (string.IsNullOrWhiteSpace(idProperty))
            {
                rawJson = string.Empty;
                error = "Entity ID Property가 비어 있습니다.";
                return false;
            }
            return TryExtractObject(
                filePath,
                collectionProperty,
                new Dictionary<string, string> { { idProperty, idValue } },
                out rawJson,
                out error);
        }

        /// <summary>지정 Collection에서 여러 문자열 Property가 모두 일치하는 object를 찾는다.</summary>
        public static bool TryExtractObject(
            string filePath,
            string collectionProperty,
            IReadOnlyDictionary<string, string> requiredProperties,
            out string rawJson,
            out string error)
        {
            rawJson = string.Empty;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(filePath))
            {
                error = "원본 JSON 경로가 비어 있습니다.";
                return false;
            }
            if (!File.Exists(filePath))
            {
                error = $"원본 JSON 파일을 찾을 수 없습니다: {filePath}";
                return false;
            }

            try
            {
                return TryExtractObjectFromJson(
                    File.ReadAllText(filePath),
                    collectionProperty,
                    requiredProperties,
                    out rawJson,
                    out error);
            }
            catch (Exception exception)
            {
                error = $"원본 JSON을 읽지 못했습니다: {exception.Message}";
                return false;
            }
        }

        /// <summary>파일 I/O 없이 JSON 문자열에서 object를 추출해 Parser 단위 테스트를 지원한다.</summary>
        public static bool TryExtractObjectFromJson(
            string json,
            string collectionProperty,
            IReadOnlyDictionary<string, string> requiredProperties,
            out string rawJson,
            out string error)
        {
            rawJson = string.Empty;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "JSON이 비어 있습니다.";
                return false;
            }
            if (requiredProperties == null || requiredProperties.Count == 0)
            {
                error = "Entity 식별 Property가 필요합니다.";
                return false;
            }

            int arrayStart = FindArrayStart(json, collectionProperty);
            if (arrayStart < 0)
            {
                error = string.IsNullOrWhiteSpace(collectionProperty)
                    ? "JSON Root 배열을 찾을 수 없습니다."
                    : $"JSON Collection을 찾을 수 없습니다: {collectionProperty}";
                return false;
            }

            int arrayDepth = 1;
            int objectDepth = 0;
            int objectStart = -1;
            for (int index = arrayStart + 1; index < json.Length; index++)
            {
                char character = json[index];
                if (character == '"')
                {
                    if (!TrySkipJsonString(json, index, out int stringEnd))
                    {
                        error = "닫히지 않은 JSON 문자열이 있습니다.";
                        return false;
                    }
                    index = stringEnd;
                    continue;
                }

                if (character == '[')
                {
                    arrayDepth++;
                    continue;
                }
                if (character == ']')
                {
                    arrayDepth--;
                    if (arrayDepth == 0)
                        break;
                    continue;
                }
                if (character == '{')
                {
                    if (arrayDepth == 1 && objectDepth == 0)
                        objectStart = index;
                    objectDepth++;
                    continue;
                }
                if (character != '}' || objectDepth <= 0)
                    continue;

                objectDepth--;
                if (objectDepth != 0 || objectStart < 0)
                    continue;

                int objectLength = index - objectStart + 1;
                string candidate = json.Substring(objectStart, objectLength);
                if (MatchesRequiredProperties(candidate, requiredProperties))
                {
                    rawJson = candidate;
                    return true;
                }
                objectStart = -1;
            }

            error = "조건에 맞는 JSON object를 찾을 수 없습니다.";
            return false;
        }

        private static int FindArrayStart(string json, string collectionProperty)
        {
            if (string.IsNullOrWhiteSpace(collectionProperty))
            {
                int root = SkipWhitespace(json, 0);
                return root < json.Length && json[root] == '[' ? root : -1;
            }

            for (int index = 0; index < json.Length; index++)
            {
                if (json[index] != '"')
                    continue;
                if (!TryReadJsonString(json, index, out string token, out int stringEnd))
                    return -1;
                index = stringEnd;
                if (!string.Equals(token, collectionProperty, StringComparison.Ordinal))
                    continue;

                int colon = SkipWhitespace(json, stringEnd + 1);
                if (colon >= json.Length || json[colon] != ':')
                    continue;
                int value = SkipWhitespace(json, colon + 1);
                if (value < json.Length && json[value] == '[')
                    return value;
            }
            return -1;
        }

        private static bool MatchesRequiredProperties(
            string jsonObject,
            IReadOnlyDictionary<string, string> requiredProperties)
        {
            foreach (KeyValuePair<string, string> required in requiredProperties)
            {
                if (!TryGetTopLevelStringProperty(jsonObject, required.Key, out string value)
                    || !string.Equals(value, required.Value, StringComparison.Ordinal))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool TryGetTopLevelStringProperty(
            string jsonObject,
            string propertyName,
            out string value)
        {
            value = string.Empty;
            int objectDepth = 0;
            int arrayDepth = 0;
            for (int index = 0; index < jsonObject.Length; index++)
            {
                char character = jsonObject[index];
                if (character == '{')
                {
                    objectDepth++;
                    continue;
                }
                if (character == '}')
                {
                    objectDepth--;
                    continue;
                }
                if (character == '[')
                {
                    arrayDepth++;
                    continue;
                }
                if (character == ']')
                {
                    arrayDepth--;
                    continue;
                }
                if (character != '"')
                    continue;

                if (!TryReadJsonString(jsonObject, index, out string token, out int stringEnd))
                    return false;
                index = stringEnd;
                if (objectDepth != 1 || arrayDepth != 0 || !string.Equals(token, propertyName, StringComparison.Ordinal))
                    continue;

                int colon = SkipWhitespace(jsonObject, stringEnd + 1);
                if (colon >= jsonObject.Length || jsonObject[colon] != ':')
                    continue;
                int valueStart = SkipWhitespace(jsonObject, colon + 1);
                if (valueStart >= jsonObject.Length || jsonObject[valueStart] != '"')
                    return false;
                return TryReadJsonString(jsonObject, valueStart, out value, out _);
            }
            return false;
        }

        private static bool TrySkipJsonString(string json, int quoteStart, out int quoteEnd)
        {
            return TryReadJsonString(json, quoteStart, out _, out quoteEnd);
        }

        private static bool TryReadJsonString(
            string json,
            int quoteStart,
            out string value,
            out int quoteEnd)
        {
            value = string.Empty;
            quoteEnd = -1;
            if (quoteStart < 0 || quoteStart >= json.Length || json[quoteStart] != '"')
                return false;

            var builder = new StringBuilder();
            for (int index = quoteStart + 1; index < json.Length; index++)
            {
                char character = json[index];
                if (character == '"')
                {
                    value = builder.ToString();
                    quoteEnd = index;
                    return true;
                }
                if (character != '\\')
                {
                    builder.Append(character);
                    continue;
                }

                if (++index >= json.Length)
                    return false;
                char escaped = json[index];
                switch (escaped)
                {
                    case '"': builder.Append('"'); break;
                    case '\\': builder.Append('\\'); break;
                    case '/': builder.Append('/'); break;
                    case 'b': builder.Append('\b'); break;
                    case 'f': builder.Append('\f'); break;
                    case 'n': builder.Append('\n'); break;
                    case 'r': builder.Append('\r'); break;
                    case 't': builder.Append('\t'); break;
                    case 'u':
                        if (index + 4 >= json.Length
                            || !ushort.TryParse(
                                json.Substring(index + 1, 4),
                                NumberStyles.HexNumber,
                                CultureInfo.InvariantCulture,
                                out ushort unicode))
                        {
                            return false;
                        }
                        builder.Append((char)unicode);
                        index += 4;
                        break;
                    default:
                        return false;
                }
            }
            return false;
        }

        private static int SkipWhitespace(string value, int index)
        {
            while (index < value.Length && char.IsWhiteSpace(value[index]))
                index++;
            return index;
        }
    }
}
