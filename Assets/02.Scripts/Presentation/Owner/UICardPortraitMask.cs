using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>은색 명찰의 V자 상단 윤곽에 맞춰 선수 초상의 하단을 자른다.</summary>
    [RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
    public sealed class UICardPortraitMask : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = rectTransform.rect;
            Vector2[] points = { new Vector2(0, .10f), new Vector2(.5f, 0),
                new Vector2(1, .10f), Vector2.one, Vector2.up };
            for (int i = 0; i < points.Length; i++)
                mesh.AddVert(new Vector3(rect.xMin + points[i].x * rect.width,
                    rect.yMin + points[i].y * rect.height), Color.white, points[i]);
            mesh.AddTriangle(0, 1, 4);
            mesh.AddTriangle(1, 3, 4);
            mesh.AddTriangle(1, 2, 3);
        }
    }
}
