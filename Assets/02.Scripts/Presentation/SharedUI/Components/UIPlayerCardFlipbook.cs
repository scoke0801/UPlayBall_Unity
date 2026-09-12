#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.SharedUI
{
    /// <summary>선수 카드 디자인 갤러리에서만 고정 원화 광채 시트를 왕복 재생한다.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RawImage))]
    public sealed class UIPlayerCardFlipbook : MonoBehaviour
    {
        private const string ResourcePath = "UI/PlayerCards/PlayerCardFX_GoldHolographic_Flipbook_v2";
        private static Texture2D _sharedAtlas;
        [SerializeField] private Texture2D _atlas;
        [SerializeField, Min(1)] private int _columns = 4;
        [SerializeField, Min(1)] private int _rows = 4;
        [SerializeField, Min(.1f)] private float _framesPerSecond = 12f;
        private RawImage _image;
        private float _elapsed;
        private int _currentFrame = -1;

        /// <summary>초상과 같은 부모의 사진 영역에 재사용 가능한 FX를 배치한다.</summary>
        public static void Bind(RectTransform parent, Vector2 min, Vector2 max, Transform portrait)
        {
            Transform existing = parent.Find("PortraitFlipbook");
            UIPlayerCardFlipbook view;
            if (existing == null)
            {
                var item = new GameObject("PortraitFlipbook", typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(RawImage), typeof(UIPlayerCardFlipbook));
                item.transform.SetParent(parent, false);
                view = item.GetComponent<UIPlayerCardFlipbook>();
            }
            else view = existing.GetComponent<UIPlayerCardFlipbook>();

            RectTransform rect = (RectTransform)view.transform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            // 재바인딩에서도 초상 바로 아래로 옮겨 프레임 배경에 가려지지 않게 한다.
            rect.SetAsLastSibling();
            rect.SetSiblingIndex(portrait.GetSiblingIndex());
            view.gameObject.SetActive(true);
        }

        private void OnEnable()
        {
            _image = GetComponent<RawImage>();
            _image.raycastTarget = false;
            if (_atlas == null)
            {
                if (_sharedAtlas == null) _sharedAtlas = Resources.Load<Texture2D>(ResourcePath);
                _atlas = _sharedAtlas;
            }
            _image.texture = _atlas;
            _image.enabled = _atlas != null;
            _elapsed = 0;
            _currentFrame = -1;
            RefreshFrame();
        }

        private void Update()
        {
            if (_atlas == null || _image.canvasRenderer.cull) return;
            // 관전 속도·일시정지와 무관한 표현 전용 시간이며 숨겨진 카드는 갱신하지 않는다.
            int frameCount = Mathf.Max(1, _columns) * Mathf.Max(1, _rows);
            float duration = Mathf.Max(1, (frameCount - 1) * 2) / Mathf.Max(.1f, _framesPerSecond);
            _elapsed = Mathf.Repeat(_elapsed + Time.unscaledDeltaTime, duration);
            RefreshFrame();
        }

        private void RefreshFrame()
        {
            if (_atlas == null) return;
            int columns = Mathf.Max(1, _columns);
            int rows = Mathf.Max(1, _rows);
            int count = columns * rows;
            int step = Mathf.FloorToInt(_elapsed * Mathf.Max(.1f, _framesPerSecond));
            int frame = count == 1 ? 0 : step < count ? step : (count - 1) * 2 - step;
            if (frame == _currentFrame) return;
            _currentFrame = frame;
            // 제작 시트는 좌상단부터 읽고 Unity UV는 좌하단 기준이다.
            // 반 texel을 안쪽으로 잡아 Bilinear가 이웃 프레임을 섞지 않게 한다.
            float insetX = .5f / _atlas.width;
            float insetY = .5f / _atlas.height;
            _image.uvRect = new Rect(frame % columns / (float)columns + insetX,
                1f - (frame / columns + 1f) / rows + insetY,
                1f / columns - 2f * insetX, 1f / rows - 2f * insetY);
        }
    }
}
#endif
