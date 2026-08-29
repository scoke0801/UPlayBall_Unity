using System.Collections.Generic;
using Baseball.Game.Input;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.UI
{
    /// <summary>
    /// 포인터 클릭 위치에 짧은 링 확산 연출을 띄워 입력이 접수됐음을 알린다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIClickFeedback : MonoBehaviour
    {
        /// <summary>모든 UI 위에 그리기 위해 System 레이어보다 높은 정렬 순서를 사용한다.</summary>
        private const int SortingOrder = 500;

        /// <summary>UI 지침 5.2의 포커스 링 토큰(#8CC2FF). 화면을 가리지 않도록 알파를 낮게 잡는다.</summary>
        [SerializeField] private Color _ringColor = new(0.549f, 0.761f, 1f, 0.38f);

        /// <summary>UI 지침 5.2의 주요 블루 토큰(#438FF5).</summary>
        [SerializeField] private Color _coreColor = new(0.263f, 0.561f, 0.961f, 0.22f);

        /// <summary>UI 지침 10.1의 Hover·Pressed 대역(80–120ms) 상단. 눈에 남지 않고 스쳐 지나가는 길이.</summary>
        [SerializeField] private float _duration = 0.16f;
        [SerializeField] private float _startDiameter = 14f;
        [SerializeField] private float _endDiameter = 42f;
        [SerializeField] private float _coreDiameter = 8f;
        [SerializeField] private int _maxConcurrentRipples = 8;

        private readonly List<Ripple> _ripples = new();
        private RectTransform _rect;
        private Canvas _canvas;
        private Sprite _ringSprite;
        private Sprite _discSprite;
        private int _activeCount;

        /// <summary>지정한 UI 레이어 위에 클릭 연출 전용 오버레이를 생성한다.</summary>
        public static UIClickFeedback CreateRuntime(RectTransform parent)
        {
            var feedbackObject = new GameObject("UI_System_ClickFeedback", typeof(RectTransform));
            var rect = (RectTransform)feedbackObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);

            // 매 클릭마다 갱신되는 연출이므로 정적 UI와 Canvas를 분리해 배치 재생성 범위를 좁힌다.
            Canvas canvas = feedbackObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = SortingOrder;

            return feedbackObject.AddComponent<UIClickFeedback>();
        }

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _canvas = GetComponent<Canvas>();
            // 얇은 테두리로 만들어 클릭 지점을 가리지 않고 윤곽만 스치게 한다.
            _ringSprite = CreateRadialSprite(0.86f, 0.99f);
            _discSprite = CreateRadialSprite(-1f, 0.94f);
        }

        private void OnEnable()
        {
            if (InputManager.Instance != null)
                InputManager.Instance.PointerClicked += HandlePointerClicked;
        }

        private void OnDisable()
        {
            if (InputManager.Instance != null)
                InputManager.Instance.PointerClicked -= HandlePointerClicked;
        }

        private void OnDestroy()
        {
            DestroySprite(_ringSprite);
            DestroySprite(_discSprite);
            _ringSprite = null;
            _discSprite = null;
        }

        private void Update()
        {
            if (_activeCount == 0)
                return;

            // 일시정지(timeScale 0) 중에도 입력 피드백은 멈추면 안 되므로 unscaled 시간을 쓴다.
            float deltaTime = Time.unscaledDeltaTime;
            for (int i = 0; i < _ripples.Count; i++)
            {
                Ripple ripple = _ripples[i];
                if (!ripple.IsActive)
                    continue;

                ripple.Elapsed += deltaTime;
                float progress = ripple.Elapsed / _duration;
                if (progress >= 1f)
                {
                    ripple.IsActive = false;
                    ripple.Root.gameObject.SetActive(false);
                    _activeCount--;
                    continue;
                }

                ApplyRippleProgress(ripple, progress);
            }
        }

        private void HandlePointerClicked(Vector2 screenPosition)
        {
            if (_rect == null)
                return;

            Camera eventCamera = _canvas != null && _canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _canvas.rootCanvas.worldCamera
                : null;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _rect,
                    screenPosition,
                    eventCamera,
                    out Vector2 localPosition))
                return;

            Ripple ripple = RentRipple();
            ripple.Root.anchoredPosition = localPosition;
            ripple.Elapsed = 0f;
            ApplyRippleProgress(ripple, 0f);
            ripple.Root.gameObject.SetActive(true);
        }

        private void ApplyRippleProgress(Ripple ripple, float progress)
        {
            // 빠르게 퍼지다 감속하는 ease-out. 손끝에서 튕겨나가는 인상을 준다.
            float inverse = 1f - progress;
            float eased = 1f - (inverse * inverse * inverse);

            float diameter = Mathf.Lerp(_startDiameter, _endDiameter, eased);
            ripple.Ring.rectTransform.sizeDelta = new Vector2(diameter, diameter);
            SetAlpha(ripple.Ring, _ringColor, inverse * inverse);

            // 중심 점은 링보다 먼저 사라져 "눌린 지점"만 짧게 찍히도록 한다.
            float coreProgress = Mathf.Clamp01(progress / 0.45f);
            float coreDiameter = _coreDiameter * Mathf.Lerp(1f, 1.2f, coreProgress);
            ripple.Core.rectTransform.sizeDelta = new Vector2(coreDiameter, coreDiameter);
            SetAlpha(ripple.Core, _coreColor, 1f - coreProgress);
        }

        private Ripple RentRipple()
        {
            for (int i = 0; i < _ripples.Count; i++)
            {
                if (!_ripples[i].IsActive)
                {
                    _ripples[i].IsActive = true;
                    _activeCount++;
                    return _ripples[i];
                }
            }

            if (_ripples.Count < _maxConcurrentRipples)
            {
                Ripple created = CreateRipple(_ripples.Count);
                created.IsActive = true;
                _ripples.Add(created);
                _activeCount++;
                return created;
            }

            return RecycleOldestRipple();
        }

        private Ripple RecycleOldestRipple()
        {
            Ripple oldest = _ripples[0];
            for (int i = 1; i < _ripples.Count; i++)
            {
                if (_ripples[i].Elapsed > oldest.Elapsed)
                    oldest = _ripples[i];
            }

            oldest.IsActive = true;
            return oldest;
        }

        private Ripple CreateRipple(int index)
        {
            var rootObject = new GameObject("Ripple" + index, typeof(RectTransform));
            var root = (RectTransform)rootObject.transform;
            root.SetParent(_rect, false);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = Vector2.zero;
            rootObject.SetActive(false);

            Image ring = CreateImage("Ring", root, _ringSprite, _ringColor);
            Image core = CreateImage("Core", root, _discSprite, _coreColor);
            return new Ripple(root, ring, core);
        }

        private static Image CreateImage(string name, RectTransform parent, Sprite sprite, Color color)
        {
            var imageObject = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)imageObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;

            Image image = imageObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            // 연출이 클릭 자체를 가로채면 안 되므로 레이캐스트 대상에서 제외한다.
            image.raycastTarget = false;
            return image;
        }

        private static void SetAlpha(Image image, Color baseColor, float alphaScale)
        {
            Color color = baseColor;
            color.a = baseColor.a * Mathf.Clamp01(alphaScale);
            image.color = color;
        }

        /// <summary>
        /// 중심에서의 거리 비율이 [inner, outer] 구간일 때 불투명한 흰색 원형 스프라이트를 만든다.
        /// inner를 음수로 주면 채워진 원이 되어 링과 중심 점을 같은 코드로 생성한다.
        /// </summary>
        private static Sprite CreateRadialSprite(float inner, float outer)
        {
            const int Resolution = 128;
            const float Feather = 0.035f;

            var texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color32[Resolution * Resolution];
            float center = (Resolution - 1) * 0.5f;
            for (int y = 0; y < Resolution; y++)
            {
                for (int x = 0; x < Resolution; x++)
                {
                    float dx = (x - center) / center;
                    float dy = (y - center) / center;
                    float distance = Mathf.Sqrt((dx * dx) + (dy * dy));
                    float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(inner - Feather, inner + Feather, distance))
                        * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(outer - Feather, outer + Feather, distance)));
                    pixels[(y * Resolution) + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, Resolution, Resolution),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static void DestroySprite(Sprite sprite)
        {
            if (sprite == null)
                return;

            Texture texture = sprite.texture;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(sprite);
                if (texture != null)
                    DestroyImmediate(texture);
                return;
            }
#endif
            Destroy(sprite);
            if (texture != null)
                Destroy(texture);
        }

        private sealed class Ripple
        {
            public Ripple(RectTransform root, Image ring, Image core)
            {
                Root = root;
                Ring = ring;
                Core = core;
            }

            public RectTransform Root { get; }
            public Image Ring { get; }
            public Image Core { get; }
            public float Elapsed { get; set; }
            public bool IsActive { get; set; }
        }
    }
}
