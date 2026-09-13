using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>확정 값과 예상 값을 같은 척도에 표시하는 비입력 게이지다.</summary>
    public sealed class UIOwnerMetricBar : MonoBehaviour
    {
        private RectTransform _current;
        private RectTransform _expected;

        /// <summary>장식 프레임을 반복하지 않고 현재 막대와 예상 증가 구간을 만든다.</summary>
        public static UIOwnerMetricBar Create(Transform parent, string name)
        {
            var track = OwnerRuntimeUiFactory.CreateImage(name, parent, OwnerDashboardStyle.Line);
            OwnerDashboardStyle.SetDataSurface(track, OwnerDashboardStyle.Line);
            var bar = track.gameObject.AddComponent<UIOwnerMetricBar>();
            bar._expected = CreateFill(track.transform, "Expected", OwnerDashboardStyle.Gold);
            bar._current = CreateFill(track.transform, "Current", OwnerDashboardStyle.Info);
            return bar;
        }

        /// <summary>도메인이 계산한 값만 받아 시각 비율로 변환한다.</summary>
        public void Bind(float current, float expected, float maximum)
        {
            float denominator = Mathf.Max(1, maximum);
            _current.anchorMax = new Vector2(Mathf.Clamp01(current / denominator), 1);
            _expected.anchorMax = new Vector2(Mathf.Clamp01(expected / denominator), 1);
        }

        private static RectTransform CreateFill(Transform parent, string name, Color color)
        {
            var image = OwnerRuntimeUiFactory.CreateImage(name, parent, color);
            OwnerDashboardStyle.SetDataSurface(image, color);
            OwnerRuntimeUiFactory.Stretch(image.rectTransform);
            return image.rectTransform;
        }
    }
}
