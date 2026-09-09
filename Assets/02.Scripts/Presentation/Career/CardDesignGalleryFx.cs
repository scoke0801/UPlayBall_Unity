#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Career
{
    /// <summary>개발용 카드 디자인 갤러리에서만 등급별 테두리 광택을 미리 보여 준다.</summary>
    [DisallowMultipleComponent]
    public sealed class CardDesignGalleryFx : MonoBehaviour
    {
        private const float CycleDuration = 4.8f;
        private const float SweepDuration = 1.15f;
        private static Sprite _glintSprite;
        private static Sprite _verticalGlintSprite;

        private readonly Image[] _edgeGlow = new Image[4];
        private RectTransform _root;
        private Image _topGlint;
        private Image _rightGlint;
        private Image _sparkleA;
        private Image _sparkleB;
        private Image _sparkleCrossA;
        private Image _sparkleCrossB;
        private Color _tint;
        private float _strength;
        private float _phase;
        private float _elapsed;
        private float _glintLength;
        private bool _isInitialized;

        /// <summary>카드 등급과 표시 크기에 맞춰 Pulse, Glint와 Sparkle 계층을 생성한다.</summary>
        public void Initialize(string variant, bool isCompact, bool isEmphasized)
        {
            if (_isInitialized)
                return;

            _root = transform as RectTransform;
            if (_root == null)
                return;

            _isInitialized = true;
            _tint = ResolveTint(variant);
            _strength = ResolveStrength(variant) * (isEmphasized ? 1f : .72f);
            _phase = ResolvePhase(variant);

            float thickness = isCompact ? 2f : 3.5f;
            _glintLength = isCompact ? 28f : 52f;
            CreateEdgeGlow(thickness);
            CreateGlints(thickness);
            _sparkleA = CreateSparkle("CornerSparkleA", new Vector2(.92f, .94f), isCompact ? 10f : 18f, out _sparkleCrossA);
            _sparkleB = CreateSparkle("CornerSparkleB", new Vector2(.08f, .08f), isCompact ? 8f : 14f, out _sparkleCrossB);
            RefreshVisuals(0f);
        }

        private void OnEnable()
        {
            _elapsed = 0f;
            if (_isInitialized)
                RefreshVisuals(_elapsed);
        }

        private void Update()
        {
            if (!_isInitialized)
                return;

            _elapsed += Time.unscaledDeltaTime;
            RefreshVisuals(_elapsed);
        }

        private void CreateEdgeGlow(float thickness)
        {
            _edgeGlow[0] = CreateImage("TopGlow", new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -thickness * .5f), new Vector2(0f, thickness));
            _edgeGlow[1] = CreateImage("BottomGlow", Vector2.zero, new Vector2(1f, 0f),
                new Vector2(0f, thickness * .5f), new Vector2(0f, thickness));
            _edgeGlow[2] = CreateImage("LeftGlow", Vector2.zero, new Vector2(0f, 1f),
                new Vector2(thickness * .5f, 0f), new Vector2(thickness, 0f));
            _edgeGlow[3] = CreateImage("RightGlow", new Vector2(1f, 0f), Vector2.one,
                new Vector2(-thickness * .5f, 0f), new Vector2(thickness, 0f));
        }

        private void CreateGlints(float thickness)
        {
            _topGlint = CreateImage("TopGlint", new Vector2(0f, 1f), new Vector2(0f, 1f),
                Vector2.zero, new Vector2(_glintLength, thickness * 2.6f));
            _topGlint.rectTransform.pivot = new Vector2(.5f, 1f);
            _topGlint.sprite = GetGlintSprite(false);

            _rightGlint = CreateImage("RightGlint", new Vector2(1f, 0f), new Vector2(1f, 0f),
                Vector2.zero, new Vector2(thickness * 2.6f, _glintLength));
            _rightGlint.rectTransform.pivot = new Vector2(1f, .5f);
            _rightGlint.sprite = GetGlintSprite(true);
        }

        private Image CreateSparkle(string name, Vector2 anchor, float size, out Image cross)
        {
            var sparkleObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = (RectTransform)sparkleObject.transform;
            rect.SetParent(_root, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = new Vector2(size, Mathf.Max(2f, size * .16f));
            rect.localRotation = Quaternion.Euler(0f, 0f, 45f);

            Image image = sparkleObject.GetComponent<Image>();
            image.raycastTarget = false;
            image.color = WithAlpha(_tint, 0f);

            var crossObject = new GameObject("Cross", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform crossRect = (RectTransform)crossObject.transform;
            crossRect.SetParent(rect, false);
            crossRect.anchorMin = crossRect.anchorMax = new Vector2(.5f, .5f);
            crossRect.sizeDelta = new Vector2(size, Mathf.Max(2f, size * .16f));
            crossRect.localRotation = Quaternion.Euler(0f, 0f, 90f);
            cross = crossObject.GetComponent<Image>();
            cross.raycastTarget = false;
            cross.color = WithAlpha(_tint, 0f);
            return image;
        }

        private Image CreateImage(
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            var imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = (RectTransform)imageObject.transform;
            rect.SetParent(_root, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            Image image = imageObject.GetComponent<Image>();
            image.raycastTarget = false;
            image.color = WithAlpha(_tint, 0f);
            return image;
        }

        private void RefreshVisuals(float elapsed)
        {
            float pulse = .72f + Mathf.Sin((elapsed + _phase) * 1.7f) * .16f;
            float edgeAlpha = Mathf.Clamp01(.26f * _strength * pulse);
            for (int index = 0; index < _edgeGlow.Length; index++)
                SetImageAlpha(_edgeGlow[index], edgeAlpha);

            float width = Mathf.Max(1f, _root.rect.width);
            float height = Mathf.Max(1f, _root.rect.height);
            float cycle = Mathf.Repeat(elapsed + _phase, CycleDuration);
            float sweepProgress = Mathf.Clamp01(cycle / SweepDuration);
            float sweepAlpha = cycle <= SweepDuration
                ? Mathf.Sin(sweepProgress * Mathf.PI) * .82f * _strength
                : 0f;

            // 광택 전체가 카드 안에 머물도록 반 길이만큼 여백을 둔다. 카드 내용에 마스크를 추가하지 않는다.
            float horizontalLength = Mathf.Min(_glintLength, width);
            float verticalLength = Mathf.Min(_glintLength, height);
            _topGlint.rectTransform.sizeDelta = new Vector2(horizontalLength, _topGlint.rectTransform.sizeDelta.y);
            _topGlint.rectTransform.anchoredPosition = new Vector2(
                Mathf.Lerp(horizontalLength * .5f, width - horizontalLength * .5f, SmoothStep(sweepProgress)), 0f);
            _rightGlint.rectTransform.sizeDelta = new Vector2(_rightGlint.rectTransform.sizeDelta.x, verticalLength);
            _rightGlint.rectTransform.anchoredPosition = new Vector2(
                0f, Mathf.Lerp(height - verticalLength * .5f, verticalLength * .5f, SmoothStep(sweepProgress)));
            SetImageAlpha(_topGlint, sweepAlpha);
            SetImageAlpha(_rightGlint, sweepAlpha * .78f);

            float sparklePulseA = Mathf.Pow(Mathf.Max(0f, Mathf.Sin((elapsed + _phase) * 2.1f)), 10f);
            float sparklePulseB = Mathf.Pow(Mathf.Max(0f, Mathf.Sin((elapsed + _phase + 1.9f) * 1.8f)), 12f);
            SetSparkle(_sparkleA, _sparkleCrossA, sparklePulseA);
            SetSparkle(_sparkleB, _sparkleCrossB, sparklePulseB);
        }

        private void SetSparkle(Image sparkle, Image cross, float pulse)
        {
            float alpha = pulse * _strength;
            SetImageAlpha(sparkle, alpha);
            SetImageAlpha(cross, alpha * .82f);
            sparkle.rectTransform.localScale = Vector3.one * Mathf.Lerp(.55f, 1.25f, pulse);
        }

        private static void SetImageAlpha(Image image, float alpha)
        {
            if (image == null)
                return;
            Color color = image.color;
            color.a = Mathf.Clamp01(alpha);
            image.color = color;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private static float SmoothStep(float value)
        {
            return value * value * (3f - 2f * value);
        }

        private static Sprite GetGlintSprite(bool isVertical)
        {
            Sprite cached = isVertical ? _verticalGlintSprite : _glintSprite;
            if (cached != null)
                return cached;

            const int length = 64;
            int width = isVertical ? 1 : length;
            int height = isVertical ? length : 1;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = isVertical ? "CardGalleryVerticalGlintTexture" : "CardGalleryGlintTexture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[length];
            for (int index = 0; index < pixels.Length; index++)
            {
                float normalized = index / (length - 1f);
                byte alpha = (byte)(Mathf.Sin(normalized * Mathf.PI) * 255f);
                pixels[index] = new Color32(255, 255, 255, alpha);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(.5f, .5f), 1f);
            sprite.name = isVertical ? "CardGalleryVerticalGlint" : "CardGalleryGlint";
            if (isVertical) _verticalGlintSprite = sprite;
            else _glintSprite = sprite;
            return sprite;
        }

        private static Color ResolveTint(string variant)
        {
            return variant switch
            {
                "AllStar" => new Color32(255, 112, 179, 255),
                "GoldenGlove" => new Color32(255, 198, 46, 255),
                "MVP" => new Color32(255, 220, 116, 255),
                "Rare" => new Color32(76, 183, 255, 255),
                "Ex" => new Color32(143, 222, 255, 255),
                "Legend" => new Color32(255, 187, 45, 255),
                "CareerHigh" => new Color32(255, 137, 45, 255),
                _ => new Color32(196, 220, 238, 255)
            };
        }

        private static float ResolveStrength(string variant)
        {
            return variant switch
            {
                "Normal" => .38f,
                "AllStar" => .58f,
                "GoldenGlove" => .7f,
                "MVP" => .78f,
                "Rare" => .56f,
                "Ex" => .72f,
                "Legend" => 1f,
                "CareerHigh" => .84f,
                _ => .5f
            };
        }

        private static float ResolvePhase(string variant)
        {
            return variant switch
            {
                "AllStar" => .45f,
                "GoldenGlove" => .9f,
                "MVP" => 1.35f,
                "Rare" => 1.8f,
                "Ex" => 2.25f,
                "Legend" => 2.7f,
                "CareerHigh" => 3.15f,
                _ => 0f
            };
        }
    }
}
#endif
