using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>실제 보유 구종의 대표 변화 방향만 원형 도식으로 표시한다.</summary>
    [RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
    public sealed class UICardPitchCompass : MaskableGraphic
    {
        private int _directions;

        /// <summary>오른쪽부터 반시계 방향 8방위의 표시 비트를 받는다.</summary>
        public void SetDirections(int directions)
        {
            _directions = directions;
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Ring(mesh, .87f, .875f, new Color32(122, 139, 154, 85));
            Ring(mesh, .81f, .82f, new Color32(183, 158, 107, 110));
            Ring(mesh, .16f, .18f, new Color32(211, 190, 147, 210));
            for (int index = 0; index < 8; index++)
            {
                Vector2 direction = Direction(index * 45);
                Vector2 side = new Vector2(-direction.y, direction.x);
                Quad(mesh, direction * .89f - side * .006f, direction * .95f - side * .006f,
                    direction * .95f + side * .006f, direction * .89f + side * .006f,
                    new Color32(175, 185, 193, 110));
                if ((_directions & (1 << index)) == 0) continue;
                Color tint = new Color32(205, 194, 169, 230);
                Quad(mesh, direction * .23f - side * .024f, direction * .57f - side * .024f,
                    direction * .57f + side * .024f, direction * .23f + side * .024f, tint);
                Triangle(mesh, direction * .73f, direction * .53f + side * .085f,
                    direction * .53f - side * .085f, tint);
            }
        }

        private static Vector2 Direction(float degrees)
        {
            float angle = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        private void Ring(VertexHelper mesh, float inner, float outer, Color tint)
        {
            const int segments = 96;
            for (int i = 0; i < segments; i++)
            {
                Vector2 a = Direction(i * 360f / segments);
                Vector2 b = Direction((i + 1) * 360f / segments);
                Quad(mesh, a * inner, a * outer, b * outer, b * inner, tint);
            }
        }

        private void Quad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color tint)
        {
            Triangle(mesh, a, b, c, tint);
            Triangle(mesh, a, c, d, tint);
        }

        private void Triangle(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Color tint)
        {
            Rect rect = rectTransform.rect;
            float radius = Mathf.Min(rect.width, rect.height) * .5f;
            int first = mesh.currentVertCount;
            mesh.AddVert(rect.center + a * radius, tint, Vector2.zero);
            mesh.AddVert(rect.center + b * radius, tint, Vector2.zero);
            mesh.AddVert(rect.center + c * radius, tint, Vector2.zero);
            mesh.AddTriangle(first, first + 1, first + 2);
        }
    }
}
