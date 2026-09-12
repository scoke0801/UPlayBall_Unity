using Baseball.Presentation.UI;
using Baseball.Presentation.SharedUI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>현재 컨디션을 역할별 막대 그래프로 표시한다.</summary>
    [RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
    internal sealed class UIRosterConditionPlot : MaskableGraphic
    {
        private float[] _values;
        private bool[] _valid;
        private int[] _levels;

        /// <summary>슬롯 순서대로 컨디션과 선수 배치 여부를 반영한다.</summary>
        public void Bind(float[] values, bool[] valid, int[] levels)
        {
            _values = values;
            _valid = valid;
            _levels = levels;
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            if (r.width <= 0f || r.height <= 0f) return;
            Quad(vh, r, CareerUiTheme.RosterBoard);
            DrawGuide(vh, r, 0f);
            DrawGuide(vh, r, 0.5f);
            DrawGuide(vh, r, 1f);
            if (_values == null || _values.Length == 0) return;
            float step = r.width / _values.Length;
            for (int i = 0; i < _values.Length; i++)
            {
                float x = r.x + step * (i + 0.5f);
                float y = r.y + r.height * Mathf.Clamp01(_values[i] / 100f);
                if (!_valid[i]) continue;
                float width = Mathf.Min(24f, step * 0.44f);
                Color accent = PlayerCardConditionSprites.GetColor(_levels[i]);
                Color body = Color.Lerp(CareerUiTheme.RosterSurface, accent, 0.65f);
                Quad(vh, new Rect(x - width / 2f, r.y, width, r.height), CareerUiTheme.RosterSurface);
                Quad(vh, new Rect(x - width / 2f, r.y, width, y - r.y), body);
                float capHeight = Mathf.Min(3f, y - r.y);
                if (capHeight > 0f)
                    Quad(vh, new Rect(x - width / 2f, y - capHeight, width, capHeight), accent);
            }
        }

        private static void DrawGuide(VertexHelper vh, Rect rect, float ratio)
        {
            Quad(vh, new Rect(rect.x, rect.y + (rect.height - 1f) * ratio, rect.width, 1f),
                CareerUiTheme.RosterDivider);
        }

        private static void Quad(VertexHelper vh, Rect r, Color color)
        {
            int start = vh.currentVertCount;
            vh.AddVert(new Vector3(r.xMin, r.yMin), color, Vector2.zero);
            vh.AddVert(new Vector3(r.xMin, r.yMax), color, Vector2.zero);
            vh.AddVert(new Vector3(r.xMax, r.yMax), color, Vector2.zero);
            vh.AddVert(new Vector3(r.xMax, r.yMin), color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
