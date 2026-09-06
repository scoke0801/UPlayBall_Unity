using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.SharedUI
{
    /// <summary>선수 카드의 얇은 금속 프레임과 명찰을 해상도에 독립적인 UI 메시로 그린다.</summary>
    public sealed class PlayerCardSurface : MaskableGraphic
    {
        [SerializeField] private Color _top = new Color32(103, 111, 122, 255);
        [SerializeField] private Color _bottom = new Color32(20, 24, 31, 255);

        /// <summary>표면의 위아래 색을 지정한다.</summary>
        public void SetColors(Color top, Color bottom)
        {
            _top = top;
            _bottom = bottom;
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            Rect rect = GetPixelAdjustedRect();
            helper.AddVert(new Vector3(rect.xMin, rect.yMin), _bottom, Vector2.zero);
            helper.AddVert(new Vector3(rect.xMin, rect.yMax), _top, Vector2.up);
            helper.AddVert(new Vector3(rect.xMax, rect.yMax), _top, Vector2.one);
            helper.AddVert(new Vector3(rect.xMax, rect.yMin), _bottom, Vector2.right);
            helper.AddTriangle(0, 1, 2);
            helper.AddTriangle(2, 3, 0);
        }
    }
}
