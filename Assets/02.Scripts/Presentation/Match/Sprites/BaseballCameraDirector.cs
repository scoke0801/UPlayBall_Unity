using UnityEngine;

namespace Baseball.Presentation.Match.Sprites
{
    public enum BaseballCameraShot { Duel, Field, Highlight }

    /// <summary>배경과 독립 선수·공에 같은 이동과 확대를 적용하는 2D 카메라다.</summary>
    public sealed class BaseballCameraDirector
    {
        private readonly RectTransform _content;
        private readonly FieldLayoutDefinition _layout;
        /// <summary>선수·공·배경을 담은 같은 화면 공간을 제어한다.</summary>
        public BaseballCameraDirector(RectTransform content, FieldLayoutDefinition layout) { _content = content; _layout = layout; }

        /// <summary>절대 진행률을 사용해 배속과 관계없이 같은 구도를 재현한다.</summary>
        public void Render(BaseballCameraShot shot, Vector2 focus, float progress)
        {
            float weight = Mathf.SmoothStep(0, 1, Mathf.Clamp01(progress));
            float targetZoom = shot switch
            {
                BaseballCameraShot.Highlight => _layout.highlightZoom,
                BaseballCameraShot.Field => _layout.fieldZoom,
                _ => _layout.duelZoom
            };
            float zoom = Mathf.Lerp(_layout.duelZoom, targetZoom, weight);
            _content.localScale = Vector3.one * zoom;
            Vector2 shift = new Vector2((0.5f - focus.x) * _content.rect.width, (focus.y - 0.5f) * _content.rect.height);
            _content.anchoredPosition = shift * (zoom - 1f);
        }
    }
}
