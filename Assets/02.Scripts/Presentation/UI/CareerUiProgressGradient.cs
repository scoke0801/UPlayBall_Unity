using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.UI
{
    /// <summary>진행 바의 절대 구간에 따라 저평가색부터 최고평가색까지 연속 색상을 입힌다.</summary>
    [DisallowMultipleComponent]
    public sealed class CareerUiProgressGradient : BaseMeshEffect
    {
        private static readonly Color LowColor = new(0.86f, 0.22f, 0.24f, 1f);
        private static readonly Color AverageColor = new(0.96f, 0.67f, 0.18f, 1f);
        private static readonly Color GoodColor = new(0.24f, 0.78f, 0.46f, 1f);
        private static readonly Color EliteColor = new(0.12f, 0.68f, 1f, 1f);
        private float _normalizedValue = 1f;

        /// <summary>표시 중인 진행률을 색상 구간의 끝값으로 지정한다.</summary>
        public void SetValue(float normalizedValue)
        {
            _normalizedValue = Mathf.Clamp01(normalizedValue);
            Refresh();
        }

        /// <summary>Skin이나 진행률이 바뀐 뒤 그라데이션 Mesh를 다시 생성한다.</summary>
        public void Refresh()
        {
            if (graphic != null)
                graphic.SetVerticesDirty();
        }

        /// <summary>Fill Mesh의 절대 가로 위치를 평가 구간 색상으로 변환한다.</summary>
        public override void ModifyMesh(VertexHelper vertexHelper)
        {
            if (!IsActive() || vertexHelper.currentVertCount < 4 || graphic == null)
                return;

            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            float minU = float.MaxValue;
            float maxU = float.MinValue;
            float minV = float.MaxValue;
            float maxV = float.MinValue;
            byte sourceAlpha = 0;
            UIVertex vertex = default;
            for (int index = 0; index < vertexHelper.currentVertCount; index++)
            {
                vertexHelper.PopulateUIVertex(ref vertex, index);
                minX = Mathf.Min(minX, vertex.position.x);
                maxX = Mathf.Max(maxX, vertex.position.x);
                minY = Mathf.Min(minY, vertex.position.y);
                maxY = Mathf.Max(maxY, vertex.position.y);
                minU = Mathf.Min(minU, vertex.uv0.x);
                maxU = Mathf.Max(maxU, vertex.uv0.x);
                minV = Mathf.Min(minV, vertex.uv0.y);
                maxV = Mathf.Max(maxV, vertex.uv0.y);
                if (vertex.color.a > sourceAlpha)
                    sourceAlpha = vertex.color.a;
            }

            if (maxX - minX <= 0.0001f || maxY - minY <= 0.0001f)
                return;

            const int segmentCount = 16;
            vertexHelper.Clear();
            for (int segment = 0; segment < segmentCount; segment++)
            {
                float leftRatio = segment / (float)segmentCount;
                float rightRatio = (segment + 1) / (float)segmentCount;
                float leftX = Mathf.Lerp(minX, maxX, leftRatio);
                float rightX = Mathf.Lerp(minX, maxX, rightRatio);
                float leftU = Mathf.Lerp(minU, maxU, leftRatio);
                float rightU = Mathf.Lerp(minU, maxU, rightRatio);
                Color leftColor = EvaluateColor(leftRatio * _normalizedValue);
                Color rightColor = EvaluateColor(rightRatio * _normalizedValue);
                leftColor.a *= sourceAlpha / 255f;
                rightColor.a *= sourceAlpha / 255f;

                int vertexStart = vertexHelper.currentVertCount;
                vertexHelper.AddVert(CreateVertex(leftX, minY, leftU, minV, leftColor));
                vertexHelper.AddVert(CreateVertex(leftX, maxY, leftU, maxV, leftColor));
                vertexHelper.AddVert(CreateVertex(rightX, maxY, rightU, maxV, rightColor));
                vertexHelper.AddVert(CreateVertex(rightX, minY, rightU, minV, rightColor));
                vertexHelper.AddTriangle(vertexStart, vertexStart + 1, vertexStart + 2);
                vertexHelper.AddTriangle(vertexStart + 2, vertexStart + 3, vertexStart);
            }
        }

        private static UIVertex CreateVertex(float x, float y, float u, float v, Color color)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = new Vector3(x, y);
            vertex.uv0 = new Vector2(u, v);
            vertex.color = color;
            return vertex;
        }

        /// <summary>진행률을 저평가색·평균색·우수색·최고평가색 사이의 연속 색상으로 변환한다.</summary>
        public static Color EvaluateColor(float value)
        {
            value = Mathf.Clamp01(value);
            if (value < 0.5f)
                return Color.Lerp(LowColor, AverageColor, value / 0.5f);
            if (value < 0.75f)
                return Color.Lerp(AverageColor, GoodColor, (value - 0.5f) / 0.25f);
            return Color.Lerp(GoodColor, EliteColor, (value - 0.75f) / 0.25f);
        }
    }
}
