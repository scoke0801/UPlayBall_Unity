using System;
using Baseball.Simulation.Match;
using UnityEngine;

namespace Baseball.Presentation.Career
{
    /// <summary>미리 생성된 잔상 Image를 재사용해 투구마다 GameObject를 만들지 않는다.</summary>
    public sealed class PitchTrailPresenter
    {
        private readonly RectTransform[] _trail;

        public PitchTrailPresenter(RectTransform[] trail)
        {
            _trail = trail ?? throw new ArgumentNullException(nameof(trail));
            Hide();
        }

        public int Capacity => _trail.Length;

        public void Update(
            double progress01,
            int visibleCount,
            float spacing01,
            in PitchFlightDescriptor pitch,
            float breakEmphasis,
            float releaseSize,
            float arrivalSize)
        {
            int count = Mathf.Clamp(visibleCount, 0, _trail.Length);
            for (int index = 0; index < _trail.Length; index++)
            {
                RectTransform item = _trail[index];
                double sampleTime = progress01 - (index + 1) * spacing01;
                bool isVisible = index < count && sampleTime > 0d;
                item.gameObject.SetActive(isVisible);
                if (!isVisible)
                    continue;

                float sample = Mathf.Clamp01((float)sampleTime);
                item.anchoredPosition = PitchTrajectoryPresenter.EvaluateScreenPosition(
                    pitch,
                    sampleTime,
                    breakEmphasis);
                float size = Mathf.Lerp(releaseSize, arrivalSize, sample);
                float ageScale = Mathf.Lerp(0.82f, 0.36f, index / (float)Mathf.Max(1, count - 1));
                item.sizeDelta = new Vector2(size, size) * ageScale;
            }
        }

        public void Hide()
        {
            for (int index = 0; index < _trail.Length; index++)
                _trail[index].gameObject.SetActive(false);
        }
    }
}
