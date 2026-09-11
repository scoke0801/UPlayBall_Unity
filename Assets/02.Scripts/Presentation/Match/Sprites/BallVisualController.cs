using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Match.Sprites
{
    /// <summary>선수 모션과 별개인 한 개의 공과 지면 그림자를 재사용한다.</summary>
    public sealed class BallVisualController
    {
        private readonly RectTransform _parent;
        private readonly RectTransform _ball;
        private readonly RectTransform _shadow;
        private readonly FieldProjection _projection;
        public bool IsVisible => _ball.gameObject.activeSelf;

        /// <summary>독립 공과 그림자를 한 번 생성한다.</summary>
        public BallVisualController(RectTransform parent, Sprite sprite, FieldProjection projection = null)
        {
            _parent = parent;
            _projection = projection;
            _shadow = SpriteActor.CreateImage(parent, "BallShadow", true).rectTransform;
            _shadow.GetComponent<Image>().color = new Color(0, 0, 0, 0.3f);
            _shadow.sizeDelta = new Vector2(12, 5);
            Image image = SpriteActor.CreateImage(parent, "BallVisual", sprite == null);
            image.sprite = sprite;
            _ball = image.rectTransform;
            _ball.sizeDelta = new Vector2(12, 12);
            _ball.anchorMin = _ball.anchorMax = _shadow.anchorMin = _shadow.anchorMax = new Vector2(0, 1);
            Hide();
        }

        /// <summary>동일 입력의 경로는 시간 분할이나 프레임률에 의존하지 않는다.</summary>
        public static Vector3 Evaluate(Vector2 start, Vector2 end, float progress, float peakHeight)
        {
            float t = Mathf.Clamp01(progress);
            Vector2 ground = Vector2.Lerp(start, end, t);
            return new Vector3(ground.x, ground.y, 4f * t * (1f - t) * peakHeight);
        }

        /// <summary>경로 위 공과 그 수직 아래 지면 그림자를 표시한다.</summary>
        public void Render(Vector2 start, Vector2 end, float progress, float peakHeight)
        {
            Vector3 point = Evaluate(start, end, progress, peakHeight);
            if (_projection != null)
            {
                float diameter = Mathf.Max(_projection.Layout.minimumBallDiameter,
                    _projection.Layout.ballDiameter * _projection.DepthScale(point.y)) * _parent.rect.height / 552f;
                _ball.sizeDelta = new Vector2(diameter, diameter);
                _shadow.sizeDelta = new Vector2(diameter, diameter * 0.4f);
            }
            _ball.gameObject.SetActive(true);
            _shadow.gameObject.SetActive(true);
            _ball.anchoredPosition = FieldProjection.ToScreen(new Vector2(point.x, point.y), _parent.rect.size, point.z);
            _shadow.anchoredPosition = FieldProjection.ToScreen(new Vector2(point.x, point.y), _parent.rect.size);
            _ball.SetAsLastSibling();
        }

        /// <summary>시트에 그려진 공으로 소유권이 넘어가면 외부 표시를 숨긴다.</summary>
        public void Hide()
        {
            _ball.gameObject.SetActive(false);
            _shadow.gameObject.SetActive(false);
        }
    }
}
