using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.UI
{
    /// <summary>
    /// HUD, Scene, Popup, System Canvas를 고정된 정렬 순서로 보유한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIRoot : MonoBehaviour
    {
        [SerializeField] private RectTransform _hudLayer;
        [SerializeField] private RectTransform _sceneLayer;
        [SerializeField] private RectTransform _popupLayer;
        [SerializeField] private RectTransform _systemLayer;

        /// <summary>
        /// 지정한 UI 레이어의 부모 RectTransform을 반환한다.
        /// </summary>
        public RectTransform GetLayerRoot(UILayer layer)
        {
            return layer switch
            {
                UILayer.HUD => _hudLayer,
                UILayer.Scene => _sceneLayer,
                UILayer.Popup => _popupLayer,
                UILayer.System => _systemLayer,
                _ => _sceneLayer
            };
        }

        /// <summary>
        /// 프리팹이 없을 때 같은 구조의 런타임 UI Root를 생성한다.
        /// </summary>
        public static UIRoot CreateRuntime(Transform parent)
        {
            var rootObject = new GameObject("UI_System_Root", typeof(RectTransform), typeof(UIRoot));
            rootObject.transform.SetParent(parent, false);
            var root = rootObject.GetComponent<UIRoot>();
            root.BuildMissingLayers();
            return root;
        }

        /// <summary>
        /// 누락된 Canvas 레이어만 생성해 기존 UI 작업물을 보존한다.
        /// </summary>
        public void BuildMissingLayers()
        {
            _hudLayer = EnsureLayer(_hudLayer, UILayer.HUD, 0);
            _sceneLayer = EnsureLayer(_sceneLayer, UILayer.Scene, 100);
            _popupLayer = EnsureLayer(_popupLayer, UILayer.Popup, 200);
            _systemLayer = EnsureLayer(_systemLayer, UILayer.System, 300);
        }

        private RectTransform EnsureLayer(RectTransform current, UILayer layer, int sortingOrder)
        {
            if (current == null)
            {
                Transform existing = transform.Find(layer + "Layer");
                current = existing as RectTransform;
            }

            if (current == null)
            {
                var layerObject = new GameObject(
                    layer + "Layer",
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler),
                    typeof(GraphicRaycaster));
                current = layerObject.GetComponent<RectTransform>();
                current.SetParent(transform, false);
            }

            current.anchorMin = Vector2.zero;
            current.anchorMax = Vector2.one;
            current.offsetMin = Vector2.zero;
            current.offsetMax = Vector2.zero;

            Canvas canvas = current.GetComponent<Canvas>();
            if (canvas == null)
                canvas = current.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = current.GetComponent<CanvasScaler>();
            if (scaler == null)
                scaler = current.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (current.GetComponent<GraphicRaycaster>() == null)
                current.gameObject.AddComponent<GraphicRaycaster>();

            return current;
        }
    }
}
