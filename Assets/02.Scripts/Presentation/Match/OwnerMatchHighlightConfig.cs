using System;
using UnityEngine;

namespace Baseball.Presentation.Match
{
    /// <summary>삽입 컷 한 장의 리소스와 플레이어용 설명을 묶는다.</summary>
    [Serializable]
    public sealed class OwnerMatchHighlightImage
    {
        public OwnerMatchHighlightKind kind;
        public string resourcePath;
        public string caption;
    }

    /// <summary>판정과 독립적인 삽입 컷 목록·표시 시간을 저작한다.</summary>
    [Serializable]
    public sealed class OwnerMatchHighlightConfig
    {
        public float durationSeconds = 0.9f;
        public float minimumDurationSeconds = 0.5f;
        public float fadeSeconds = 0.10f;
        public string heading = "하이라이트";
        public OwnerMatchHighlightImage[] images = Array.Empty<OwnerMatchHighlightImage>();

        /// <summary>리소스가 없으면 삽입 컷을 생략하고 기본 중계를 유지한다.</summary>
        public static OwnerMatchHighlightConfig Load()
        {
            TextAsset json = Resources.Load<TextAsset>("UI/OwnerMatch/Highlights/HighlightPresentation");
            return json == null ? new OwnerMatchHighlightConfig() : JsonUtility.FromJson<OwnerMatchHighlightConfig>(json.text);
        }

        /// <summary>고배속에서도 이미지를 읽을 최소 표시 시간을 보장한다.</summary>
        public float GetDuration(OwnerMatchPlaybackSpeed speed) =>
            Mathf.Max(0.1f, minimumDurationSeconds, durationSeconds / Mathf.Max(1, (int)speed));
    }
}
