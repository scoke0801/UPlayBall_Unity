using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.UI
{
    /// <summary>야구공 아래의 등급 문자를 칸 크기와 기존 RawImage 표시 상태에 맞춘다.</summary>
    internal sealed class UISkillTileGradeLabel : Text
    {
        private RawImage _tile;

        /// <summary>재사용되는 보드 칸의 등급을 갱신하며 입력은 원래 칸에 통과시킨다.</summary>
        public void Initialize(RawImage tile, string grade)
        {
            _tile = tile;
            font = UIProjectFonts.Default;
            text = grade;
            alignment = TextAnchor.MiddleCenter;
            horizontalOverflow = HorizontalWrapMode.Overflow;
            verticalOverflow = VerticalWrapMode.Overflow;
            raycastTarget = false;
            var outline = GetComponent<Outline>();
            if (outline == null) outline = gameObject.AddComponent<Outline>();
            outline.effectColor = new Color32(10, 24, 42, 255);
            outline.effectDistance = new Vector2(.6f, -.6f);
            Synchronize();
        }

        private void LateUpdate() => Synchronize();

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            Synchronize();
        }

        private void Synchronize()
        {
            if (_tile == null) return;
            // 빈 칸으로 재사용되거나 미리보기 Tint가 변해도 문자만 남지 않게 한다.
            color = new Color(1f, 1f, 1f, _tile.enabled && _tile.texture != null
                ? _tile.color.a * _tile.canvasRenderer.GetAlpha() : 0f);
            fontSize = Mathf.Max(6, Mathf.RoundToInt(Mathf.Min(
                _tile.rectTransform.rect.width, _tile.rectTransform.rect.height) * .25f));
        }
    }
}
