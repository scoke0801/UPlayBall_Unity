using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>카드 뒷면의 부채꼴 외야·내야를 해상도에 맞춰 그린다.</summary>
    [RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
    public sealed class UICardDefenseField : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Color chalk = new Color32(224, 214, 211, 255);
            Color dirt = new Color32(110, 66, 76, 255);
            Sector(mesh, new Vector2(.5f, .10f), .88f, 45, 135, chalk);
            Sector(mesh, new Vector2(.5f, .10f), .60f, 45, 135, dirt);
            Triangle(mesh, new Vector2(.5f, .10f), new Vector2(.23f, .37f), new Vector2(.5f, .64f), chalk);
            Triangle(mesh, new Vector2(.5f, .10f), new Vector2(.5f, .64f), new Vector2(.77f, .37f), chalk);
            Sector(mesh, new Vector2(.5f, .37f), .035f, 0, 360, dirt);
        }

        private void Sector(VertexHelper mesh, Vector2 center, float radius, float from, float to, Color color)
        {
            const int segments = 64;
            for (int i = 0; i < segments; i++)
            {
                float a = Mathf.Lerp(from, to, i / (float)segments) * Mathf.Deg2Rad;
                float b = Mathf.Lerp(from, to, (i + 1) / (float)segments) * Mathf.Deg2Rad;
                Triangle(mesh, center, center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius,
                    center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, color);
            }
        }

        private void Triangle(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            Rect rect = rectTransform.rect;
            // 부채꼴 양끝이 정규화 사각형을 넘으므로 외곽선까지 안전 영역 안으로 축소한다.
            a = FitPoint(a);
            b = FitPoint(b);
            c = FitPoint(c);
            int start = mesh.currentVertCount;
            mesh.AddVert(new Vector3(rect.xMin + a.x * rect.width, rect.yMin + a.y * rect.height), color, Vector2.zero);
            mesh.AddVert(new Vector3(rect.xMin + b.x * rect.width, rect.yMin + b.y * rect.height), color, Vector2.zero);
            mesh.AddVert(new Vector3(rect.xMin + c.x * rect.width, rect.yMin + c.y * rect.height), color, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
        }

        /// <summary>도식과 한국어 라벨에 같은 안전 영역 변환을 적용한다.</summary>
        public static Vector2 FitPoint(Vector2 point) => new Vector2(.11f, .11f) + point * .78f;
    }
}
