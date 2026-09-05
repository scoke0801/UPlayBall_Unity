using System;
using System.Collections.Generic;
using System.Text;

namespace Baseball.Game.Guide
{
    /// <summary>중괄호 placeholder를 추출하고 Fact 값으로 치환하는 단일 템플릿 구현이다.</summary>
    public static class GuideTemplate
    {
        public static string[] ExtractTokens(string template)
        {
            if (string.IsNullOrEmpty(template))
                return Array.Empty<string>();

            var tokens = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            int cursor = 0;
            while (cursor < template.Length)
            {
                int opening = template.IndexOf('{', cursor);
                if (opening < 0)
                    break;
                int closing = template.IndexOf('}', opening + 1);
                if (closing < 0)
                    break;

                string token = template.Substring(opening + 1, closing - opening - 1);
                if (token.Length > 0 && seen.Add(token))
                    tokens.Add(token);
                cursor = closing + 1;
            }
            return tokens.ToArray();
        }

        public static bool TryRender(
            string template,
            Func<string, string> valueResolver,
            out string rendered,
            out string missingToken)
        {
            if (template == null)
            {
                rendered = string.Empty;
                missingToken = string.Empty;
                return false;
            }
            if (valueResolver == null)
                throw new ArgumentNullException(nameof(valueResolver));

            var builder = new StringBuilder(template.Length + 32);
            int cursor = 0;
            while (cursor < template.Length)
            {
                int opening = template.IndexOf('{', cursor);
                if (opening < 0)
                {
                    builder.Append(template, cursor, template.Length - cursor);
                    break;
                }

                builder.Append(template, cursor, opening - cursor);
                int closing = template.IndexOf('}', opening + 1);
                if (closing < 0)
                {
                    rendered = string.Empty;
                    missingToken = template.Substring(opening);
                    return false;
                }

                string token = template.Substring(opening + 1, closing - opening - 1);
                string value = valueResolver(token);
                if (value == null)
                {
                    rendered = string.Empty;
                    missingToken = token;
                    return false;
                }
                builder.Append(value);
                cursor = closing + 1;
            }

            rendered = builder.ToString();
            missingToken = string.Empty;
            return true;
        }
    }
}
