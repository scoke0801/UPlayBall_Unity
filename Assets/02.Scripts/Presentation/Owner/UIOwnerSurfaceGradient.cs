using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>패널의 네이티브 정점 색상에 약한 세로 음영을 더한다.</summary>
    [DisallowMultipleComponent]
    public sealed class UIOwnerSurfaceGradient : BaseMeshEffect
    {
        /// <summary>배경 그림과 별개로 해상도에 독립적인 패널 깊이를 표현한다.</summary>
        public override void ModifyMesh(VertexHelper vertices)
        {
            if (!IsActive()) return;
            Rect rect = graphic.rectTransform.rect;
            UIVertex vertex = default;
            for (int i = 0; i < vertices.currentVertCount; i++)
            {
                vertices.PopulateUIVertex(ref vertex, i);
                Color color = vertex.color;
                float shade = Mathf.Lerp(.78f, 1.08f, Mathf.InverseLerp(rect.yMin, rect.yMax, vertex.position.y));
                vertex.color = new Color(color.r * shade, color.g * shade, color.b * shade, color.a);
                vertices.SetUIVertex(vertex, i);
            }
        }
    }
}
