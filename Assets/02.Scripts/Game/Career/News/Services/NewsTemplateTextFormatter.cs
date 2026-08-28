using System.Text;

namespace Baseball.Game.Career.News
{
    /// <summary>Fact 토큰과 이름 뒤 조사 토큰을 완전한 한국어 문장 템플릿에 적용한다.</summary>
    internal static class NewsTemplateTextFormatter
    {
        public static string Format(string template, NewsFactSet facts)
        {
            if (string.IsNullOrEmpty(template))
                return string.Empty;

            var result = new StringBuilder(template.Length + 32);
            int cursor = 0;
            while (cursor < template.Length)
            {
                int open = template.IndexOf('{', cursor);
                if (open < 0)
                {
                    result.Append(template, cursor, template.Length - cursor);
                    break;
                }

                result.Append(template, cursor, open - cursor);
                int close = template.IndexOf('}', open + 1);
                if (close < 0)
                {
                    result.Append(template, open, template.Length - open);
                    break;
                }

                string token = template.Substring(open + 1, close - open - 1);
                result.Append(ResolveToken(token, facts));
                cursor = close + 1;
            }
            return result.ToString();
        }

        private static string ResolveToken(string token, NewsFactSet facts)
        {
            string[] parts = token.Split('|');
            if (!System.Enum.TryParse(parts[0], out NewsFactKey key))
                return "{" + token + "}";
            string value = facts.GetText(key);
            if (parts.Length == 3)
                return KoreanPostpositionFormatter.Apply(value, parts[1], parts[2]);
            return value;
        }
    }
}
