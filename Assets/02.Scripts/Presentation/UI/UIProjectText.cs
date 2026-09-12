using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.UI
{
    /// <summary>확대된 패널에서도 화면 픽셀 밀도로 동적 폰트를 생성하는 공용 Text다.</summary>
    [AddComponentMenu("")]
    public sealed class UIProjectText : Text
    {
        private readonly UIVertex[] _quad = new UIVertex[4];
        private float _lastRasterDensity;

        /// <summary>부모 패널 확대에 맞춰 글자 생성 밀도를 높일지 지정한다.</summary>
        public bool UsePanelRasterDensity { get; set; } = true;

        /// <summary>Canvas 배율과 부모 패널 확대를 함께 반영한 글자 생성 밀도다.</summary>
        public float RasterPixelsPerUnit
        {
            get
            {
                if (!UsePanelRasterDensity || canvas == null || font == null || !font.dynamic)
                    return pixelsPerUnit;
                Vector3 canvasScale = canvas.transform.lossyScale;
                Vector3 textScale = transform.lossyScale;
                float horizontal = Mathf.Abs(textScale.x / Mathf.Max(.0001f, Mathf.Abs(canvasScale.x)));
                float vertical = Mathf.Abs(textScale.y / Mathf.Max(.0001f, Mathf.Abs(canvasScale.y)));
                // 축소 애니메이션에서는 기존 해상도를 유지해 프레임마다 저해상도 글자를 재생성하지 않는다.
                return pixelsPerUnit * Mathf.Max(1f, Mathf.Max(horizontal, vertical));
            }
        }

        private void LateUpdate()
        {
            float density = RasterPixelsPerUnit;
            if (Mathf.Approximately(_lastRasterDensity, density)) return;
            _lastRasterDensity = density;
            SetVerticesDirty();
            SetLayoutDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            if (font == null)
            {
                mesh.Clear();
                return;
            }
            m_DisableFontTextureRebuiltCallback = true;
            try
            {
                float density = RasterPixelsPerUnit;
                var settings = GetGenerationSettings(rectTransform.rect.size);
                settings.scaleFactor = density;
                cachedTextGenerator.PopulateWithErrors(text, settings, gameObject);
                // 최초 글리프 생성 중 폰트 텍스처가 갱신되면 다른 Text의 UpdateGeometry가
                // 같은 VertexHelper를 사용한다. 중첩 갱신이 끝난 뒤 비워야 다른 글자가 섞이지 않는다.
                mesh.Clear();
                var vertices = cachedTextGenerator.verts;
                if (vertices.Count == 0) return;
                Vector2 origin = (Vector2)vertices[0].position / density;
                Vector2 offset = PixelAdjustPoint(origin) - origin;
                for (int i = 0; i < vertices.Count; i++)
                {
                    int corner = i & 3;
                    _quad[corner] = vertices[i];
                    _quad[corner].position /= density;
                    _quad[corner].position += (Vector3)offset;
                    if (corner == 3) mesh.AddUIVertexQuad(_quad);
                }
            }
            finally
            {
                m_DisableFontTextureRebuiltCallback = false;
            }
        }

        public override float preferredWidth
        {
            get
            {
                var settings = GetGenerationSettings(Vector2.zero);
                settings.scaleFactor = RasterPixelsPerUnit;
                return cachedTextGeneratorForLayout.GetPreferredWidth(text, settings) / settings.scaleFactor;
            }
        }

        public override float preferredHeight
        {
            get
            {
                var settings = GetGenerationSettings(new Vector2(GetPixelAdjustedRect().width, 0f));
                settings.scaleFactor = RasterPixelsPerUnit;
                return cachedTextGeneratorForLayout.GetPreferredHeight(text, settings) / settings.scaleFactor;
            }
        }
    }
}
