using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>동일한 0~100 능력 축으로 양 팀을 비교하고 결측 팀은 도형을 그리지 않는다.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UIOpponentRadar : MaskableGraphic
    {
        private float[] _own;
        private float[] _opponent;

        /// <summary>정확·장타·주력·수비·제구 순서의 로스터 평균을 복사한다.</summary>
        public void Bind(float[] own, float[] opponent)
        {
            _own = own == null ? null : (float[])own.Clone();
            _opponent = opponent == null ? null : (float[])opponent.Clone();
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Vector2 center = rectTransform.rect.center;
            float radius = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .46f;
            Color grid = new Color32(185, 184, 173, 255);
            for (int ring = 1; ring <= 4; ring++)
                for (int axis = 0; axis < 5; axis++)
                    Line(vh, Point(center, radius * ring / 4, axis), Point(center, radius * ring / 4, axis + 1), grid, .6f);
            for (int axis = 0; axis < 5; axis++) Line(vh, center, Point(center, radius, axis), grid, .6f);
            Polygon(vh, center, radius, _own, new Color32(35, 115, 187, 255));
            Polygon(vh, center, radius, _opponent, new Color32(186, 51, 62, 255));
        }

        private static void Polygon(VertexHelper vh, Vector2 center, float radius, float[] values, Color color)
        {
            if (values == null || values.Length != 5) return;
            for (int index = 0; index < 5; index++)
                if (float.IsNaN(values[index]) || float.IsInfinity(values[index])) return;
            Color fill = color;
            fill.a = .22f;
            for (int axis = 0; axis < 5; axis++)
            {
                Vector2 a = Point(center, radius * Mathf.Clamp01(values[axis] / 100), axis);
                Vector2 b = Point(center, radius * Mathf.Clamp01(values[(axis + 1) % 5] / 100), axis + 1);
                int start = vh.currentVertCount;
                vh.AddVert(center, fill, Vector2.zero);
                vh.AddVert(a, fill, Vector2.zero);
                vh.AddVert(b, fill, Vector2.zero);
                vh.AddTriangle(start, start + 1, start + 2);
                Line(vh, a, b, color, 1.1f);
            }
        }

        private static Vector2 Point(Vector2 center, float radius, int axis)
        {
            float angle = (90 - axis * 72) * Mathf.Deg2Rad;
            return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        private static void Line(VertexHelper vh, Vector2 a, Vector2 b, Color color, float width)
        {
            Vector2 delta = b - a;
            Vector2 normal = new Vector2(-delta.y, delta.x).normalized * width * .5f;
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
