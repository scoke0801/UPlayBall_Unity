using System;
using UnityEngine;

namespace Baseball.Presentation.Owner
{
    /// <summary>포스트시즌 표현 시간과 아트 경로를 시뮬레이션 밖 JSON에서 읽는다.</summary>
    [Serializable]
    public sealed class OwnerPostseasonPresentationData
    {
        public float bracketStagger = 0.12f;
        public float bracketFade = 0.3f;
        public float bracketSlide = 16f;
        public float resultHold = 0.8f;
        public float titleDelay = 0.25f;
        public float teamDelay = 0.65f;
        public float scoreDelay = 1.05f;
        public float revealFade = 0.4f;
        public float seriesDuration = 2.2f;
        public float championshipDuration = 3.4f;
        public float confettiDelay = 1.25f;
        public int confettiCount = 30;
        public string seriesArt = "UI/OwnerPostseason/series-victory-v1";
        public string championshipArt = "UI/OwnerPostseason/championship-v1";

        private static OwnerPostseasonPresentationData _cached;

        /// <summary>설정이 누락되어도 동일한 최종 상태에 도달하는 기본값을 제공한다.</summary>
        public static OwnerPostseasonPresentationData Load()
        {
            if (_cached != null) return _cached;
            TextAsset asset = Resources.Load<TextAsset>("UI/OwnerPostseason/Presentation");
            _cached = asset == null ? new OwnerPostseasonPresentationData() :
                JsonUtility.FromJson<OwnerPostseasonPresentationData>(asset.text) ?? new OwnerPostseasonPresentationData();
            return _cached;
        }
    }
}
