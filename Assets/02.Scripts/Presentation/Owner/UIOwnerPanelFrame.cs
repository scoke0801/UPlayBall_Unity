using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>구단 운영 패널의 금속 외곽·샴페인 골드 코너를 해상도 독립적으로 그린다.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UIOwnerPanelFrame : MaskableGraphic
    {
        [SerializeField] private Color _rim = new Color32(10, 17, 24, 255);
        [SerializeField] private Color _metal = new Color32(105, 122, 132, 210);
        [SerializeField] private Color _gold = new Color32(204, 177, 120, 235);
        [SerializeField, Min(1)] private float _rimWidth = 4;
        [SerializeField, Min(8)] private float _cornerLength = 28;
        private bool _isHero;

        /// <summary>내용·클릭 영역을 바꾸지 않고 공통 장식 프레임을 한 번만 연결한다.</summary>
        public static void Attach(RectTransform panel, bool isHero = false)
        {
            var existing = panel.Find("OwnerPanelFrame");
            var frame = existing != null ? existing.GetComponent<UIOwnerPanelFrame>() : null;
            if (frame == null)
            {
                var root = new GameObject("OwnerPanelFrame", typeof(RectTransform));
                root.transform.SetParent(panel, false);
                frame = root.AddComponent<UIOwnerPanelFrame>();
                frame.raycastTarget = false;
                root.AddComponent<LayoutElement>().ignoreLayout = true;
                root.AddComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.DataImage);
                frame.rectTransform.anchorMin = Vector2.zero;
                frame.rectTransform.anchorMax = Vector2.one;
                frame.rectTransform.offsetMin = frame.rectTransform.offsetMax = Vector2.zero;
            }
            frame._isHero = isHero;
            frame.SetVerticesDirty();
            frame.transform.SetAsLastSibling();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect r = rectTransform.rect;
            if (r.width < 16 || r.height < 16) return;
            float rim = Mathf.Min(_rimWidth, Mathf.Min(r.width, r.height) / 4);
            Ring(mesh, r, rim, _rim);
            Rect inset = Rect.MinMaxRect(r.xMin + rim, r.yMin + rim, r.xMax - rim, r.yMax - rim);
            Ring(mesh, inset, 1, _metal);
            Color gold = _gold;
            if (!_isHero) gold.a *= .72f;
            float length = Mathf.Min(_cornerLength, Mathf.Min(r.width, r.height) / 4);
            // 금속 코너는 패널 가장자리 6px 안에만 둬 본문 안전 여백을 보존한다.
            for (int x = 0; x < 2; x++) for (int y = 0; y < 2; y++)
            {
                float left = x == 0 ? r.xMin + 2 : r.xMax - length - 2;
                float bottom = y == 0 ? r.yMin + 2 : r.yMax - 4;
                Quad(mesh, left, bottom, length, 2, gold);
                left = x == 0 ? r.xMin + 2 : r.xMax - 4;
                bottom = y == 0 ? r.yMin + 2 : r.yMax - length - 2;
                Quad(mesh, left, bottom, 2, length, gold);
            }
            Quad(mesh, r.center.x - 22, r.yMax - 4, 44, 2, gold);
            Quad(mesh, r.center.x - 9, r.yMin + 2, 18, 1, _metal);
        }

        private static void Ring(VertexHelper mesh, Rect r, float width, Color tint)
        {
            Quad(mesh, r.xMin, r.yMin, r.width, width, tint);
            Quad(mesh, r.xMin, r.yMax - width, r.width, width, tint);
            Quad(mesh, r.xMin, r.yMin + width, width, r.height - width * 2, tint);
            Quad(mesh, r.xMax - width, r.yMin + width, width, r.height - width * 2, tint);
        }

        private static void Quad(VertexHelper mesh, float x, float y, float width, float height, Color tint)
        {
            int index = mesh.currentVertCount;
            mesh.AddVert(new Vector3(x, y), tint, Vector2.zero);
            mesh.AddVert(new Vector3(x, y + height), tint, Vector2.zero);
            mesh.AddVert(new Vector3(x + width, y + height), tint, Vector2.zero);
            mesh.AddVert(new Vector3(x + width, y), tint, Vector2.zero);
            mesh.AddTriangle(index, index + 1, index + 2);
            mesh.AddTriangle(index + 2, index + 3, index);
        }
    }
}
