using System;

namespace Baseball.Game.Career.News
{
    /// <summary>이름 끝 글자의 받침을 기준으로 한국어 조사를 선택한다.</summary>
    public static class KoreanPostpositionFormatter
    {
        /// <summary>받침형과 무받침형 조사를 선택해 명사 뒤에 붙인다.</summary>
        public static string Apply(string noun, string consonantForm, string vowelForm)
        {
            noun ??= string.Empty;
            KoreanFinalConsonantType finalType = GetFinalConsonantType(noun);
            if (consonantForm == "으로" && vowelForm == "로" && finalType == KoreanFinalConsonantType.Rieul)
                return noun + vowelForm;
            return noun + (finalType == KoreanFinalConsonantType.None ? vowelForm : consonantForm);
        }

        internal static KoreanFinalConsonantType GetFinalConsonantType(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return KoreanFinalConsonantType.None;

            char last = value[value.Length - 1];
            if (last >= 0xAC00 && last <= 0xD7A3)
            {
                int jongseong = (last - 0xAC00) % 28;
                if (jongseong == 0) return KoreanFinalConsonantType.None;
                if (jongseong == 8) return KoreanFinalConsonantType.Rieul;
                return KoreanFinalConsonantType.Other;
            }

            if (char.IsDigit(last))
            {
                return last switch
                {
                    '2' or '4' or '5' or '9' => KoreanFinalConsonantType.None,
                    '1' or '7' or '8' => KoreanFinalConsonantType.Rieul,
                    _ => KoreanFinalConsonantType.Other
                };
            }

            // 외국어 이름의 발음을 추측해 틀리는 것보다 한국어 기사에서 흔한 받침형을 기본으로 쓴다.
            return KoreanFinalConsonantType.Other;
        }
    }
}
