using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Unity.Plastic.Newtonsoft.Json.Linq;

namespace Baseball.Editor.Guide
{
    /// <summary>Front Manager Schema가 사용하는 Draft 2020-12 keyword만 결정론적으로 검증한다.</summary>
    public static class JsonSchemaSubsetValidator
    {
        public static string[] Validate(string instanceJson, string schemaJson)
        {
            if (string.IsNullOrWhiteSpace(instanceJson))
                return new[] { "$: JSON 원문이 비어 있습니다." };
            if (string.IsNullOrWhiteSpace(schemaJson))
                return new[] { "$: JSON Schema가 비어 있습니다." };

            JToken instance;
            JObject schema;
            try
            {
                instance = JToken.Parse(instanceJson);
                schema = JObject.Parse(schemaJson);
            }
            catch (Exception exception)
            {
                return new[] { $"$: JSON 구문 오류: {exception.Message}" };
            }

            var errors = new List<string>();
            ValidateToken(instance, schema, schema, "$", errors);
            return errors.ToArray();
        }

        private static void ValidateToken(
            JToken instance,
            JObject schema,
            JObject rootSchema,
            string path,
            ICollection<string> errors)
        {
            if (schema["$ref"]?.Type == JTokenType.String)
            {
                JObject referenced = ResolveReference(rootSchema, schema["$ref"].Value<string>());
                if (referenced == null)
                {
                    errors.Add($"{path}: 해석할 수 없는 $ref입니다: {schema["$ref"]}");
                    return;
                }
                ValidateToken(instance, referenced, rootSchema, path, errors);
                return;
            }

            if (!MatchesType(instance, schema["type"]))
            {
                errors.Add($"{path}: type이 Schema와 다릅니다. 실제={instance.Type}, 기대={schema["type"]}");
                return;
            }

            JToken constant = schema["const"];
            if (constant != null && !JToken.DeepEquals(instance, constant))
                errors.Add($"{path}: 값이 const {constant}와 다릅니다.");

            if (schema["enum"] is JArray allowed && !ContainsDeepEqual(allowed, instance))
                errors.Add($"{path}: enum에 없는 값입니다: {instance}");

            switch (instance.Type)
            {
                case JTokenType.Object:
                    ValidateObject((JObject)instance, schema, rootSchema, path, errors);
                    break;
                case JTokenType.Array:
                    ValidateArray((JArray)instance, schema, rootSchema, path, errors);
                    break;
                case JTokenType.String:
                    ValidateString(instance.Value<string>(), schema, path, errors);
                    break;
                case JTokenType.Integer:
                case JTokenType.Float:
                    ValidateNumber(instance.Value<double>(), schema, path, errors);
                    break;
            }
        }

        private static void ValidateObject(
            JObject instance,
            JObject schema,
            JObject rootSchema,
            string path,
            ICollection<string> errors)
        {
            var propertySchemas = schema["properties"] as JObject;
            if (schema["required"] is JArray required)
            {
                for (int index = 0; index < required.Count; index++)
                {
                    string name = required[index].Value<string>();
                    if (instance.Property(name, StringComparison.Ordinal) == null)
                        errors.Add($"{path}: 필수 property '{name}'이 없습니다.");
                }
            }

            foreach (JProperty property in instance.Properties())
            {
                if (propertySchemas?[property.Name] is JObject propertySchema)
                {
                    ValidateToken(property.Value, propertySchema, rootSchema,
                        path + "." + property.Name, errors);
                }
                else if (schema["additionalProperties"]?.Type == JTokenType.Boolean &&
                         !schema["additionalProperties"].Value<bool>())
                {
                    errors.Add($"{path}: 허용되지 않은 property '{property.Name}'입니다.");
                }
            }
        }

        private static void ValidateArray(
            JArray instance,
            JObject schema,
            JObject rootSchema,
            string path,
            ICollection<string> errors)
        {
            int minimum = schema["minItems"]?.Value<int>() ?? 0;
            int maximum = schema["maxItems"]?.Value<int>() ?? int.MaxValue;
            if (instance.Count < minimum || instance.Count > maximum)
                errors.Add($"{path}: 항목 수 {instance.Count}가 허용 범위 {minimum}..{maximum} 밖입니다.");

            if (schema["uniqueItems"]?.Value<bool>() == true)
            {
                for (int left = 0; left < instance.Count; left++)
                    for (int right = left + 1; right < instance.Count; right++)
                        if (JToken.DeepEquals(instance[left], instance[right]))
                            errors.Add($"{path}: [{left}]와 [{right}] 항목이 중복됐습니다.");
            }

            if (schema["items"] is not JObject itemSchema)
                return;
            for (int index = 0; index < instance.Count; index++)
                ValidateToken(instance[index], itemSchema, rootSchema, $"{path}[{index}]", errors);
        }

        private static void ValidateString(
            string instance,
            JObject schema,
            string path,
            ICollection<string> errors)
        {
            int minimumLength = schema["minLength"]?.Value<int>() ?? 0;
            if ((instance?.Length ?? 0) < minimumLength)
                errors.Add($"{path}: 문자열 길이가 minLength {minimumLength}보다 짧습니다.");
            string pattern = schema["pattern"]?.Value<string>();
            if (!string.IsNullOrEmpty(pattern) && !Regex.IsMatch(instance ?? string.Empty, pattern))
                errors.Add($"{path}: pattern '{pattern}'과 일치하지 않습니다.");
        }

        private static void ValidateNumber(
            double instance,
            JObject schema,
            string path,
            ICollection<string> errors)
        {
            if (schema["minimum"] != null && instance < schema["minimum"].Value<double>())
                errors.Add($"{path}: {instance.ToString(CultureInfo.InvariantCulture)}가 minimum보다 작습니다.");
            if (schema["maximum"] != null && instance > schema["maximum"].Value<double>())
                errors.Add($"{path}: {instance.ToString(CultureInfo.InvariantCulture)}가 maximum보다 큽니다.");
            if (schema["exclusiveMinimum"] != null && instance <= schema["exclusiveMinimum"].Value<double>())
                errors.Add($"{path}: 값이 exclusiveMinimum보다 커야 합니다.");
        }

        private static bool MatchesType(JToken instance, JToken typeToken)
        {
            if (typeToken == null)
                return true;
            if (typeToken.Type == JTokenType.Array)
            {
                foreach (JToken item in (JArray)typeToken)
                    if (MatchesTypeName(instance, item.Value<string>()))
                        return true;
                return false;
            }
            return MatchesTypeName(instance, typeToken.Value<string>());
        }

        private static bool MatchesTypeName(JToken instance, string type)
        {
            return type switch
            {
                "object" => instance.Type == JTokenType.Object,
                "array" => instance.Type == JTokenType.Array,
                "string" => instance.Type == JTokenType.String,
                "number" => instance.Type is JTokenType.Integer or JTokenType.Float,
                "integer" => instance.Type == JTokenType.Integer,
                "boolean" => instance.Type == JTokenType.Boolean,
                "null" => instance.Type == JTokenType.Null,
                _ => false
            };
        }

        private static JObject ResolveReference(JObject root, string reference)
        {
            if (string.IsNullOrWhiteSpace(reference) || !reference.StartsWith("#/", StringComparison.Ordinal))
                return null;
            JToken current = root;
            string[] segments = reference.Substring(2).Split('/');
            for (int index = 0; index < segments.Length; index++)
            {
                string segment = segments[index].Replace("~1", "/").Replace("~0", "~");
                current = current?[segment];
                if (current == null)
                    return null;
            }
            return current as JObject;
        }

        private static bool ContainsDeepEqual(JArray values, JToken target)
        {
            for (int index = 0; index < values.Count; index++)
                if (JToken.DeepEquals(values[index], target))
                    return true;
            return false;
        }
    }
}
