using System;
using UnityEngine;

namespace Baseball.Presentation.Career
{
    /// <summary>판정과 분리된 투구 궤적의 화면 시간·크기·강조 설정이다.</summary>
    [Serializable]
    public sealed class PitchTrajectoryPresentationConfig
    {
        [SerializeField, Min(0.1f)] private float windupSeconds = 0.55f;
        [SerializeField, Min(0.01f)] private float releaseEmphasisSeconds = 0.08f;
        [SerializeField, Min(0.1f)] private float minimumFlightSeconds = 0.38f;
        [SerializeField, Min(0.1f)] private float maximumFlightSeconds = 0.62f;
        [SerializeField, Min(0f)] private float plateArrivalHoldSeconds = 0.12f;
        [SerializeField, Min(0f)] private float batterReactionSeconds = 0.16f;
        [SerializeField, Min(0.1f)] private float resultHoldSeconds = 0.48f;
        [SerializeField, Range(1f, 2f)] private float breakEmphasis = 1.3f;
        [SerializeField, Min(1f)] private float releaseBallSize = 9f;
        [SerializeField, Min(1f)] private float arrivalBallSize = 27f;
        [SerializeField, Range(5, 8)] private int trailCount = 6;
        [SerializeField, Range(0.01f, 0.12f)] private float trailSpacing01 = 0.055f;

        public float WindupSeconds => windupSeconds;
        public float ReleaseEmphasisSeconds => releaseEmphasisSeconds;
        public float MinimumFlightSeconds => minimumFlightSeconds;
        public float MaximumFlightSeconds => Mathf.Max(minimumFlightSeconds, maximumFlightSeconds);
        public float PlateArrivalHoldSeconds => plateArrivalHoldSeconds;
        public float BatterReactionSeconds => batterReactionSeconds;
        public float ResultHoldSeconds => resultHoldSeconds;
        public float BreakEmphasis => breakEmphasis;
        public float ReleaseBallSize => releaseBallSize;
        public float ArrivalBallSize => arrivalBallSize;
        public int TrailCount => trailCount;
        public float TrailSpacing01 => trailSpacing01;

        /// <summary>실제 도착 시간을 읽기 가능한 2D 연출 범위로 제한한다.</summary>
        public float ResolveFlightSeconds(double plateArrivalMilliseconds)
        {
            float seconds = (float)(plateArrivalMilliseconds / 1000d);
            return Mathf.Clamp(seconds, MinimumFlightSeconds, MaximumFlightSeconds);
        }
    }
}
