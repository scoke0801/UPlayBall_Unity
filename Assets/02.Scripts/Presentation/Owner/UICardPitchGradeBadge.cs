using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>구종 등급을 금속 테두리와 에나멜 채색의 원형 배지로 그린다.</summary>
    [RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
    public sealed class UICardPitchGradeBadge : MaskableGraphic
    {
        private Color _top;
        private Color _bottom;

        /// <summary>S 계열은 로즈, 나머지 등급은 앰버 배지로 표시한다.</summary>
        public void SetGrade(string grade)
        {
            bool special = grade.StartsWith("S", System.StringComparison.Ordinal);
            _top = special ? new Color32(239, 134, 162, 255) : new Color32(236, 185, 89, 255);
            _bottom = special ? new Color32(132, 38, 77, 255) : new Color32(137, 85, 28, 255);
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Disc(mesh, 1f, new Color32(67, 58, 48, 255), new Color32(36, 34, 35, 255));
            Disc(mesh, .94f, new Color32(255, 246, 210, 255), new Color32(157, 131, 84, 255));
            Disc(mesh, .78f, _top, _bottom);
        }

        private void Disc(VertexHelper mesh, float scale, Color top, Color bottom)
        {
            Rect rect = rectTransform.rect;
            float radius = Mathf.Min(rect.width, rect.height) * .5f * scale;
            int center = mesh.currentVertCount;
            mesh.AddVert(rect.center, Color.Lerp(bottom, top, .5f), Vector2.zero);
            const int segments = 64;
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector2 point = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                mesh.AddVert(rect.center + point * radius, Color.Lerp(bottom, top, (point.y + 1f) * .5f), Vector2.zero);
                if (i > 0) mesh.AddTriangle(center, center + i, center + i + 1);
            }
        }
    }
}
