using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>유학 지도에 이미지 아이콘을 받치는 원형 테두리를 그린다.</summary>
    [RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
    public sealed class UIStudyDestinationPin : MaskableGraphic
    {
        private bool _isUnlocked;
        private bool _isSelected;

        /// <summary>해금 여부와 선택 상태를 원형 핀에 반영한다.</summary>
        public void Configure(bool isUnlocked, bool isSelected)
        {
            _isUnlocked = isUnlocked;
            _isSelected = isSelected;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Vector2 center = rectTransform.rect.center;
            float radius = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .5f;
            AddDisc(mesh, center, radius, new Color32(26, 61, 104, 255));
            AddDisc(mesh, center, radius - 1, _isSelected ? new Color32(255, 201, 65, 255) : Color.white);
            AddDisc(mesh, center, radius - 3, _isUnlocked
                ? new Color32(53, 114, 194, 255) : new Color32(95, 112, 128, 255));
        }

        private void AddDisc(VertexHelper mesh, Vector2 center, float radius, Color tint)
        {
            const int segments = 48;
            int start = mesh.currentVertCount;
            mesh.AddVert(center, tint * color, Vector2.zero);
            for (int index = 0; index < segments; index++)
            {
                float angle = index * Mathf.PI * 2 / segments;
                mesh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius,
                    tint * color, Vector2.zero);
            }
            for (int index = 0; index < segments; index++)
                mesh.AddTriangle(start, start + 1 + index, start + 1 + (index + 1) % segments);
        }
    }
}
