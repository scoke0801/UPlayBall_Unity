using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.UI
{
    /// <summary>핵심 CTA 위로 낮은 빈도의 광택을 흘려 주 행동의 시각적 우선순위를 유지한다.</summary>
    [DisallowMultipleComponent]
    public sealed class CareerUiShine : MonoBehaviour
    {
        private const float CycleDuration = 5.4f;
        private const float SweepDuration = 0.95f;

        private RectTransform _root;
        private Image _shine;
        private float _elapsed;

        /// <summary>ImageGen 광택 Sprite로 연출 계층을 초기화한다.</summary>
        public void Initialize(Sprite sprite)
        {
            if (sprite == null || _shine != null)
                return;

            _root = transform as RectTransform;
            var shineObject = new GameObject("SkinShine", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = (RectTransform)shineObject.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(220f, 92f);
            rect.SetAsFirstSibling();

            _shine = shineObject.GetComponent<Image>();
            _shine.sprite = sprite;
            _shine.color = new Color(1f, 0.91f, 0.74f, 0f);
            _shine.preserveAspect = false;
            _shine.raycastTarget = false;

            if (GetComponent<RectMask2D>() == null)
                gameObject.AddComponent<RectMask2D>();
        }

        private void Awake()
        {
            _root = transform as RectTransform;
        }

        private void OnEnable()
        {
            _elapsed = 0f;
        }

        private void Update()
        {
            if (_root == null || _shine == null)
                return;

            _elapsed += Time.unscaledDeltaTime;
            float cycle = _elapsed % CycleDuration;
            if (cycle > SweepDuration)
            {
                SetAlpha(0f);
                return;
            }

            float progress = cycle / SweepDuration;
            float width = Mathf.Max(1f, _root.rect.width);
            float x = Mathf.Lerp(-width * 0.65f, width * 1.65f, SmoothStep(progress));
            _shine.rectTransform.anchoredPosition = new Vector2(x, 0f);
            SetAlpha(Mathf.Sin(progress * Mathf.PI) * 0.1f);
        }

        private void SetAlpha(float alpha)
        {
            Color color = _shine.color;
            color.a = alpha;
            _shine.color = color;
        }

        private static float SmoothStep(float value)
        {
            return value * value * (3f - (2f * value));
        }
    }
}
