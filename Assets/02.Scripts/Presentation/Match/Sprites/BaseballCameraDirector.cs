using UnityEngine;

namespace Baseball.Presentation.Match.Sprites
{
    public enum BaseballCameraShot { Duel, Field, Highlight, Contact }

    /// <summary>배경과 독립 선수·공에 같은 이동과 확대를 적용하는 2D 카메라다.</summary>
    public sealed class BaseballCameraDirector
    {
        private readonly RectTransform _content;
        private readonly FieldLayoutDefinition _layout;
        private float _returnZoom;
        private Vector2 _returnPosition;
        /// <summary>선수·공·배경을 담은 같은 화면 공간을 제어한다.</summary>
        public BaseballCameraDirector(RectTransform content, FieldLayoutDefinition layout) { _content = content; _layout = layout; }

        /// <summary>절대 진행률을 사용해 배속과 관계없이 같은 구도를 재현한다.</summary>
        public void Render(BaseballCameraShot shot, Vector2 focus, float progress)
        {
            RenderTransition(BaseballCameraShot.Duel, new Vector2(0.5f, 0.5f), shot, focus, progress);
        }

        /// <summary>두 구도의 위치와 배율을 함께 보간해 장면 경계의 확대·이동 점프를 막는다.</summary>
        public void RenderTransition(BaseballCameraShot from, Vector2 fromFocus, BaseballCameraShot to, Vector2 toFocus, float progress)
        {
            float weight = Mathf.SmoothStep(0, 1, Mathf.Clamp01(progress));
            float startZoom = GetZoom(from), endZoom = GetZoom(to);
            _content.localScale = Vector3.one * Mathf.Lerp(startZoom, endZoom, weight);
            _content.anchoredPosition = Vector2.Lerp(GetShift(fromFocus, startZoom), GetShift(toFocus, endZoom), weight);
        }

        /// <summary>판정 표시 시작 구도를 저장해 다음 타석 복귀의 출발점으로 쓴다.</summary>
        public void BeginReturnToDuel()
        {
            _returnZoom = _content.localScale.x;
            _returnPosition = _content.anchoredPosition;
        }

        /// <summary>결과 표시 시간에 기본 구도로 돌아간다.</summary>
        public void RenderReturnToDuel(float progress)
        {
            float weight = Mathf.SmoothStep(0, 1, Mathf.Clamp01(progress));
            _content.localScale = Vector3.one * Mathf.Lerp(_returnZoom, _layout.duelZoom, weight);
            _content.anchoredPosition = Vector2.Lerp(_returnPosition, Vector2.zero, weight);
        }

        private float GetZoom(BaseballCameraShot shot) => shot switch
            {
                BaseballCameraShot.Contact => _layout.contactZoom,
                BaseballCameraShot.Highlight => _layout.highlightZoom,
                BaseballCameraShot.Field => _layout.fieldZoom,
                _ => _layout.duelZoom
            };

        private Vector2 GetShift(Vector2 focus, float zoom)
        {
            Vector2 shift = new Vector2((0.5f - focus.x) * _content.rect.width, (focus.y - 0.5f) * _content.rect.height);
            return shift * (zoom - 1f);
        }
    }
}
