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

        public void Bind(float[] values, bool[] valid)
        {
            _values = values;
            _valid = valid;
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            Quad(vh, new Rect(r.x, r.y, r.width, r.height * 0.6f), new Color(0.83f, 0.84f, 0.85f));
            Quad(vh, new Rect(r.x, r.y + r.height * 0.6f, r.width, r.height * 0.2f), new Color(0.97f, 0.93f, 0.73f));
            Quad(vh, new Rect(r.x, r.y + r.height * 0.8f, r.width, r.height * 0.2f), new Color(0.94f, 0.82f, 0.82f));
            for (int i = 0; i <= 10; i++)
                Quad(vh, new Rect(r.x, r.y + r.height * i / 10f, r.width, 1), new Color(0.65f, 0.66f, 0.67f, 0.5f));
            if (_values == null || _values.Length == 0) return;
            float step = r.width / _values.Length;
            for (int i = 0; i < _values.Length; i++)
            {
                float x = r.x + step * (i + 0.5f);
                float y = r.y + r.height * Mathf.Clamp01(_values[i] / 100f);
                Quad(vh, new Rect(r.x + step * i, r.y, 1, r.height), new Color(0.65f, 0.66f, 0.67f, 0.4f));
                if (!_valid[i]) continue;
                Quad(vh, new Rect(x - step * 0.24f, r.y, step * 0.48f, y - r.y),
                    _values[i] >= 80 ? new Color(0.78f, 0.12f, 0.16f) : new Color(0.97f, 0.75f, 0.08f));
            }
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
