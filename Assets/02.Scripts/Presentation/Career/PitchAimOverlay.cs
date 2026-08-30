using System;
using Baseball.Simulation.Match;
using UnityEngine;

namespace Baseball.Presentation.Career
{
    /// <summary>투수에게만 목표점·예상 제구 범위·실제 통과점 차이를 표시한다.</summary>
    public sealed class PitchAimOverlay
    {
        private readonly RectTransform _target;
        private readonly RectTransform _commandEllipse;
        private readonly RectTransform _actual;
        private readonly RectTransform _connector;

        public PitchAimOverlay(
            RectTransform target,
            RectTransform commandEllipse,
            RectTransform actual,
            RectTransform connector)
        {
            _target = target != null ? target : throw new ArgumentNullException(nameof(target));
            _commandEllipse = commandEllipse != null
                ? commandEllipse
                : throw new ArgumentNullException(nameof(commandEllipse));
            _actual = actual != null ? actual : throw new ArgumentNullException(nameof(actual));
            _connector = connector != null ? connector : throw new ArgumentNullException(nameof(connector));
            Hide();
        }

        public void ShowAim(in PlatePoint targetPoint, in CommandEllipse ellipse, float pulse01)
        {
            Vector2 position = PitchTrajectoryPresenter.ToPlateScreenPosition(targetPoint);
            _target.gameObject.SetActive(true);
            _target.anchoredPosition = position;
            float pulseScale = Mathf.Lerp(0.94f, 1.08f, pulse01);
            _target.localScale = new Vector3(pulseScale, pulseScale, 1f);

            _commandEllipse.gameObject.SetActive(true);
            _commandEllipse.anchoredPosition = position;
            _commandEllipse.sizeDelta = new Vector2(
                Mathf.Max(20f, (float)ellipse.RadiusX * 105f * 4f),
                Mathf.Max(20f, (float)ellipse.RadiusY * 85f * 4f));
            _commandEllipse.localRotation = Quaternion.Euler(0f, 0f, (float)ellipse.RotationDegrees);
            _actual.gameObject.SetActive(false);
            _connector.gameObject.SetActive(false);
        }

        public void ShowResult(in PlatePoint targetPoint, in PlatePoint actualPoint)
        {
            Vector2 target = PitchTrajectoryPresenter.ToPlateScreenPosition(targetPoint);
            Vector2 actual = PitchTrajectoryPresenter.ToPlateScreenPosition(actualPoint);
            _target.gameObject.SetActive(true);
            _target.anchoredPosition = target;
            _target.localScale = Vector3.one;
            _commandEllipse.gameObject.SetActive(false);
            _actual.gameObject.SetActive(true);
            _actual.anchoredPosition = actual;

            Vector2 difference = actual - target;
            _connector.gameObject.SetActive(true);
            _connector.anchoredPosition = (target + actual) * 0.5f;
            _connector.sizeDelta = new Vector2(difference.magnitude, 2f);
            _connector.localRotation = Quaternion.Euler(
                0f,
                0f,
                Mathf.Atan2(difference.y, difference.x) * Mathf.Rad2Deg);
        }

        public void Hide()
        {
            _target.gameObject.SetActive(false);
            _commandEllipse.gameObject.SetActive(false);
            _actual.gameObject.SetActive(false);
            _connector.gameObject.SetActive(false);
        }
    }
}
