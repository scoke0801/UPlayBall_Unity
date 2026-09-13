using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>동일한 축과 상한으로 두 능력치 집합을 비교하고 결측 집합은 도형을 그리지 않는다.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UIOpponentRadar : MaskableGraphic
    {
        private float[] _own;
        private float[] _opponent;
        private int _axisCount = 5;
        private float _maximum = 100f;
        private Color _ownColor = new Color32(35, 115, 187, 255);
        private Color _opponentColor = new Color32(186, 51, 62, 255);
        private Color _gridColor = new Color32(185, 184, 173, 255);

        /// <summary>비교할 두 구단과 배경에 맞는 표현 색상을 지정한다.</summary>
        public void SetPalette(Color own, Color opponent, Color grid)
        {
            _ownColor = own;
            _opponentColor = opponent;
            _gridColor = grid;
            SetVerticesDirty();
        }

        /// <summary>기본 5축 팀 비교 또는 지정한 축 수·표시 상한으로 두 집합을 복사한다.</summary>
        public void Bind(float[] own, float[] opponent, int axisCount = 5, float maximum = 100f)
        {
            _axisCount = Mathf.Max(3, axisCount);
            _maximum = Mathf.Max(1f, maximum);
            raycastTarget = false;
            _own = own == null ? null : (float[])own.Clone();
            _opponent = opponent == null ? null : (float[])opponent.Clone();
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Vector2 center = rectTransform.rect.center;
            float radius = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .46f;
            Color grid = _gridColor;
            // 축소된 Canvas에서도 수직·수평 변이 픽셀 사이로 사라지지 않도록 화면 기준 두께를 보장한다.
            float scale = canvas != null ? Mathf.Max(canvas.scaleFactor, .01f) : 1f;
            float gridWidth = Mathf.Max(.6f, 1.25f / scale);
            float outlineWidth = Mathf.Max(1.1f, 1.75f / scale);
            for (int ring = 1; ring <= 4; ring++)
                for (int axis = 0; axis < _axisCount; axis++)
                    Line(vh, Point(center, radius * ring / 4, axis), Point(center, radius * ring / 4, axis + 1), grid, gridWidth);
            for (int axis = 0; axis < _axisCount; axis++) Line(vh, center, Point(center, radius, axis), grid, gridWidth);
            Polygon(vh, center, radius, _own, _ownColor, outlineWidth);
            Polygon(vh, center, radius, _opponent, _opponentColor, outlineWidth);
        }

        private void Polygon(VertexHelper vh, Vector2 center, float radius, float[] values, Color color, float outlineWidth)
        {
            if (values == null || values.Length != _axisCount) return;
            for (int index = 0; index < _axisCount; index++)
                if (float.IsNaN(values[index]) || float.IsInfinity(values[index])) return;
            Color fill = color;
            fill.a = .22f;
            for (int axis = 0; axis < _axisCount; axis++)
            {
                Vector2 a = Point(center, radius * Mathf.Clamp01(values[axis] / _maximum), axis);
                Vector2 b = Point(center, radius * Mathf.Clamp01(values[(axis + 1) % _axisCount] / _maximum), axis + 1);
                int start = vh.currentVertCount;
                vh.AddVert(center, fill, Vector2.zero);
                vh.AddVert(a, fill, Vector2.zero);
                vh.AddVert(b, fill, Vector2.zero);
                vh.AddTriangle(start, start + 1, start + 2);
                Line(vh, a, b, color, outlineWidth);
            }
        }

        private Vector2 Point(Vector2 center, float radius, int axis)
        {
            float angle = (90 - (axis % _axisCount) * (360f / _axisCount)) * Mathf.Deg2Rad;
            return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        private static void Line(VertexHelper vh, Vector2 a, Vector2 b, Color color, float width)
        {
            Vector2 delta = b - a;
            Vector2 direction = delta.normalized;
            Vector2 normal = new Vector2(-direction.y, direction.x) * width * .5f;
            // 선분 끝을 반 두께만큼 겹쳐 다각형 꼭짓점의 틈을 막는다.
            a -= direction * width * .5f;
            b += direction * width * .5f;
            int start = vh.currentVertCount;
            vh.AddVert(a - normal, color, Vector2.zero);
            vh.AddVert(a + normal, color, Vector2.zero);
            vh.AddVert(b + normal, color, Vector2.zero);
            vh.AddVert(b - normal, color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
