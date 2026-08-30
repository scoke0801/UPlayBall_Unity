using System;
using Baseball.Simulation.Match;
using UnityEngine;

namespace Baseball.Presentation.Career
{
    /// <summary>PitchFlightDescriptor를 2D 공 위치·크기·잔상으로만 투영한다.</summary>
    public sealed class PitchTrajectoryPresenter
    {
        private const float PlateScaleX = 105f;
        private const float PlateScaleY = 85f;
        private const float PlateCenterY = -55f;
        private const float ReleaseDepthOffsetY = 66f;

        private readonly RectTransform _root;
        private readonly RectTransform _ball;
        private readonly RectTransform[] _previewDots;
        private readonly PitchTrailPresenter _trail;
        private PitchTrajectoryPresentationConfig _config;
        private PitchFlightDescriptor _pitch;
        private bool _hasPitch;

        public PitchTrajectoryPresenter(
            RectTransform root,
            RectTransform ball,
            RectTransform[] trail,
            RectTransform[] previewDots,
            PitchTrajectoryPresentationConfig config)
        {
            _root = root != null ? root : throw new ArgumentNullException(nameof(root));
            _ball = ball != null ? ball : throw new ArgumentNullException(nameof(ball));
            _previewDots = previewDots ?? throw new ArgumentNullException(nameof(previewDots));
            _trail = new PitchTrailPresenter(trail);
            _config = config ?? throw new ArgumentNullException(nameof(config));
            Hide();
        }

        public void SetRootVisible(bool isVisible) => _root.gameObject.SetActive(isVisible);

        /// <summary>선택 구종의 대표 변화만 점선으로 보여주고 이번 투구의 난수 결과는 사용하지 않는다.</summary>
        public void ShowPreview(in PitchOption option, in PlatePoint targetPoint)
        {
            _root.gameObject.SetActive(true);
            _hasPitch = false;
            _ball.gameObject.SetActive(false);
            _trail.Hide();
            PitchTypeProfile profile = PitchTypeProfileCatalog.Get(option.PitchType);
            var preview = new PitchFlightDescriptor(
                option.PitchType,
                new PlatePoint(0d, 1.22d),
                targetPoint,
                targetPoint,
                (option.MinimumVelocityMph + option.MaximumVelocityMph) * 0.5d,
                option.HorizontalBreak,
                option.VerticalBreak,
                profile.BreakStartTime01,
                500d,
                50d,
                false);

            for (int index = 0; index < _previewDots.Length; index++)
            {
                float progress = (index + 1f) / (_previewDots.Length + 1f);
                RectTransform dot = _previewDots[index];
                dot.gameObject.SetActive(true);
                dot.anchoredPosition = EvaluateScreenPosition(preview, progress, _config.BreakEmphasis);
                float size = Mathf.Lerp(5f, 10f, progress);
                dot.sizeDelta = new Vector2(size, size);
            }
        }

        public void BeginActual(in PitchFlightDescriptor pitch)
        {
            _root.gameObject.SetActive(true);
            _pitch = pitch;
            _hasPitch = true;
            HidePreview();
            _trail.Hide();
            _ball.gameObject.SetActive(false);
        }

        public void SetActualProgress(double progress01)
        {
            if (!_hasPitch)
                return;

            double progress = Math.Max(0d, Math.Min(1d, progress01));
            _ball.gameObject.SetActive(true);
            _ball.anchoredPosition = EvaluateScreenPosition(_pitch, progress, _config.BreakEmphasis);
            float size = Mathf.Lerp(
                _config.ReleaseBallSize,
                _config.ArrivalBallSize,
                (float)progress);
            _ball.sizeDelta = new Vector2(size, size);
            _trail.Update(
                progress,
                _config.TrailCount,
                _config.TrailSpacing01,
                _pitch,
                _config.BreakEmphasis,
                _config.ReleaseBallSize,
                _config.ArrivalBallSize);
        }

        public void HoldAtPlate()
        {
            SetActualProgress(1d);
            _trail.Hide();
        }

        public void Hide()
        {
            _hasPitch = false;
            _ball.gameObject.SetActive(false);
            _trail.Hide();
            HidePreview();
            _root.gameObject.SetActive(false);
        }

        public static Vector2 ToPlateScreenPosition(in PlatePoint point)
        {
            return new Vector2(
                (float)point.X * PlateScaleX,
                PlateCenterY + (float)point.Y * PlateScaleY);
        }

        public static PlatePoint FromPlateScreenPosition(in Vector2 position)
        {
            return new PlatePoint(
                position.x / PlateScaleX,
                (position.y - PlateCenterY) / PlateScaleY);
        }

        internal static Vector2 EvaluateScreenPosition(
            in PitchFlightDescriptor pitch,
            double time01,
            float breakEmphasis)
        {
            float time = Mathf.Clamp01((float)time01);
            PitchTrajectoryPoint point = pitch.Evaluate(time);
            double linearX = pitch.ReleasePoint.X + (pitch.PlatePoint.X - pitch.ReleasePoint.X) * time;
            double linearY = pitch.ReleasePoint.Y + (pitch.PlatePoint.Y - pitch.ReleasePoint.Y) * time;
            double emphasizedX = linearX + (point.X - linearX) * breakEmphasis;
            double emphasizedY = linearY + (point.Y - linearY) * breakEmphasis;
            return new Vector2(
                (float)emphasizedX * PlateScaleX,
                PlateCenterY + (float)emphasizedY * PlateScaleY +
                Mathf.Lerp(ReleaseDepthOffsetY, 0f, time));
        }

        private void HidePreview()
        {
            for (int index = 0; index < _previewDots.Length; index++)
                _previewDots[index].gameObject.SetActive(false);
        }
    }
}
