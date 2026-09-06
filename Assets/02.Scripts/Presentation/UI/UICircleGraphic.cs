using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.UI
{
    /// <summary>에디터 전용 내장 Sprite에 의존하지 않는 원형 UI 마커다.</summary>
    public sealed class UICircleGraphic : Image
    {
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            const int segments = 32;
            Rect rect = GetPixelAdjustedRect();
            Vector2 center = rect.center;
            Vector2 radius = rect.size * 0.5f;
            helper.AddVert(center, color, new Vector2(0.5f, 0.5f));
            for (int index = 0; index <= segments; index++)
            {
                float angle = index * Mathf.PI * 2 / segments;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                helper.AddVert(center + Vector2.Scale(direction, radius), color, direction * 0.5f + Vector2.one * 0.5f);
                if (index > 0) helper.AddTriangle(0, index, index + 1);
            }
        }
    }
}
