using System;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.UI
{
    /// <summary>공용 야구 관리 UI의 프레임, 버튼, 슬라이더와 강조 연출을 한 테마로 적용한다.</summary>
    public static class CareerUiSkin
    {
        private const string UniversalPanelPath = "UI/Skin/ui_panel_universal_v2";
        private const string HeroPanelPath = "UI/Skin/ui_panel_hero_v2";
        private const string ButtonStatesPath = "UI/Skin/ui_button_states";
        private const string SliderPartsPath = "UI/Skin/ui_slider_parts";
        private const string ProgressPartsPath = "UI/Skin/ui_progress_pairs_v3";
        private const string EffectsPath = "UI/Skin/ui_fx_atlas";
        private const string LegacySelectedBadgeName = "SkinSelectedBadge";

        private const float MinimumLabelPadding = 20f;
        private const float MaximumLabelPadding = 52f;
        private const float CompactButtonMaximumWidth = 180f;
        private const float CompactButtonMaximumHeight = 46f;
        private const float CompactFramedPixelsPerUnitMultiplier = 3f;
        private const float CardButtonMinimumHeight = 260f;
        private const float MaximumFlatSurfaceAlpha = 0.78f;
        private const float LargeProgressMinimumWidth = 180f;
        private const float LargeProgressMinimumHeight = 12f;
        private const float LargeProgressFillHeightRatio = 0.68f;
        private const float CompactProgressFillHeightRatio = 0.70f;

        private static readonly Vector4 PanelBorder = new(150f, 72f, 210f, 128f);
        private static readonly Vector4 HeroPanelBorder = new(230f, 110f, 180f, 160f);
        private static readonly Vector4 ButtonBorder = new(96f, 44f, 54f, 44f);
        private static readonly Vector4 SliderBorder = new(68f, 46f, 68f, 46f);
        private static readonly Vector4 LargeProgressTrackBorder = new(56f, 56f, 56f, 56f);
        private static readonly Vector4 CompactProgressTrackBorder = new(41f, 40f, 41f, 40f);
        private static Color FlatPanelColor => CareerUiTheme.ReferencePanel;
        private static Color FlatSurfaceColor => CareerUiTheme.ReferencePanelHeader;
        private static Color FlatBorderColor => CareerUiTheme.ReferenceBorder;
        private static Color CompactButtonColor => CareerUiTheme.ReferenceButton;
        private static Color CompactButtonSelectedColor => new(0.76f, 0.84f, 0.91f, 1f);
        private static Color CompactButtonBorderColor => CareerUiTheme.ReferenceBorder;
        private static Color CompactButtonSelectedBorderColor => CareerUiTheme.ReferenceAccent;

        private static Sprite _universalPanel;
        private static Sprite _heroPanel;
        private static Sprite _buttonNormal;
        private static Sprite _buttonFocused;
        private static Sprite _buttonPressed;
        private static Sprite _sliderTrack;
        private static Sprite _sliderFill;
        private static Sprite _sliderHandle;
        private static Sprite _largeProgressTrack;
        private static Sprite _largeProgressHolder;
        private static Sprite _compactProgressTrack;
        private static Sprite _compactProgressHolder;
        private static Sprite _horizontalSweep;
        private static bool _isLoaded;

        /// <summary>지정한 UI 계층의 패널, 버튼과 슬라이더에 공통 스킨을 적용한다.</summary>
        public static void Apply(Transform root)
        {
            if (root == null || !EnsureLoaded())
                return;

            EnsureObserver(root);

            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
                ApplyButton(buttons[i]);

            Slider[] sliders = root.GetComponentsInChildren<Slider>(true);
            for (int i = 0; i < sliders.Length; i++)
                ApplySlider(sliders[i]);

            ApplyPanelHierarchy(root.GetComponentsInChildren<Image>(true));
            ApplyReferenceTextHierarchy(root.GetComponentsInChildren<Text>(true));

            RectTransform[] containers = root.GetComponentsInChildren<RectTransform>(true);
            for (int i = 0; i < containers.Length; i++)
            {
                RectTransform container = containers[i];
                if (container.name.EndsWith("Content", StringComparison.Ordinal)
                    || container.name.Equals("Body", StringComparison.Ordinal))
                    EnsureObserver(container);
            }
        }

        /// <summary>버튼의 상태와 라벨 안전 영역을 공통 야구 중계 스킨으로 교체한다.</summary>
        public static void ApplyButton(Button button)
        {
            if (button == null || !EnsureLoaded())
                return;

            Image image = button.targetGraphic as Image;
            if (image == null)
                image = button.GetComponent<Image>();
            if (image == null)
                return;

            CareerUiVisualElement visual = image.GetComponent<CareerUiVisualElement>();
            if (visual != null && visual.Role == CareerUiVisualRole.TexturedAction)
            {
                // Layout 계산 전 너비와 관계없이 명시한 버튼 스킨을 유지한다.
                image.sprite = visual.IsHeroFrame ? _buttonFocused : _buttonNormal;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = CompactFramedPixelsPerUnitMultiplier;
                image.color = Color.white;
                Outline outline = image.GetComponent<Outline>();
                if (outline != null) outline.enabled = false;
                ConfigurePersistentSelectedTransition(button);
                EnsureButtonLabelLayout(button);
                return;
            }
            if (visual != null && visual.Role == CareerUiVisualRole.FlatSurface)
            {
                ApplyFlatInteractiveControl(button, image);
                return;
            }

            RemoveLegacySelectedBadge(button);
            bool isSemanticActive = image.sprite == _buttonFocused
                || image.sprite == _heroPanel
                || IsSemanticActive(image.color);

            if (visual != null
                && visual.Role == CareerUiVisualRole.FramedControl
                && IsCompactFramedControl(image.rectTransform))
            {
                ApplyCompactFramedControl(button, image, isSemanticActive);
                return;
            }

            if (IsCardButton(image.rectTransform))
            {
                ApplyCardButton(button, image, isSemanticActive);
                return;
            }

            ApplyCompactButton(button, image, isSemanticActive);
            EnsurePrimaryActionShine(button);
        }

        /// <summary>Slider의 Track, Fill, Handle를 공통 이미지 파츠로 교체한다.</summary>
        public static void ApplySlider(Slider slider)
        {
            if (slider == null || !EnsureLoaded())
                return;

            Image track = slider.GetComponent<Image>();
            Image fill = slider.fillRect != null ? slider.fillRect.GetComponent<Image>() : null;
            if (track != null)
            {
                if (track.sprite != _sliderTrack)
                    track.color = ResolveTextureTint(track.color, 0.35f);
                track.sprite = _sliderTrack;
                track.type = Image.Type.Sliced;
                track.pixelsPerUnitMultiplier = 1f;
            }

            if (fill != null)
            {
                fill.sprite = _sliderFill;
                fill.type = Image.Type.Sliced;
                fill.pixelsPerUnitMultiplier = 1f;
                fill.color = Color.white;
            }

            Image handle = slider.handleRect != null ? slider.handleRect.GetComponent<Image>() : null;
            if (handle != null)
            {
                handle.sprite = _sliderHandle;
                handle.type = Image.Type.Simple;
                handle.preserveAspect = true;
                handle.color = Color.white;
            }
        }

        /// <summary>표시 전용 진행 바의 크기에 맞는 Track과 Holder 쌍을 적용한다.</summary>
        public static void ApplyProgressBar(Image track, Image fill, float? normalizedValue = null)
        {
            if (!EnsureLoaded())
                return;

            float trackWidth = track != null ? Mathf.Abs(track.rectTransform.rect.width) : 0f;
            float trackHeight = track != null ? Mathf.Abs(track.rectTransform.rect.height) : 0f;
            bool usesLargeVariant = trackWidth >= LargeProgressMinimumWidth
                && trackHeight >= LargeProgressMinimumHeight;
            Sprite trackSprite = usesLargeVariant ? _largeProgressTrack : _compactProgressTrack;
            Sprite holderSprite = usesLargeVariant ? _largeProgressHolder : _compactProgressHolder;

            if (track != null)
            {
                track.sprite = trackSprite;
                track.type = Image.Type.Sliced;
                track.pixelsPerUnitMultiplier = ResolveProgressPixelsPerUnit(
                    trackHeight,
                    usesLargeVariant);
                track.color = Color.white;
            }

            if (fill != null)
            {
                Color sourceFillColor = fill.color;
                if (normalizedValue.HasValue && track != null)
                {
                    float ratio = Mathf.Clamp01(normalizedValue.Value);
                    RectTransform fillRect = fill.rectTransform;
                    float heightRatio = usesLargeVariant
                        ? LargeProgressFillHeightRatio
                        : CompactProgressFillHeightRatio;
                    float fillHeight = Mathf.Max(2f, trackHeight * heightRatio);
                    float horizontalInset = (trackHeight - fillHeight) * 0.5f;
                    float fullWidth = Mathf.Max(0f, track.rectTransform.rect.width - horizontalInset * 2f);
                    fillRect.anchorMin = fillRect.anchorMax = new Vector2(0f, 0.5f);
                    fillRect.pivot = new Vector2(0f, 0.5f);
                    fillRect.anchoredPosition = new Vector2(horizontalInset, 0f);
                    fillRect.sizeDelta = new Vector2(fullWidth * ratio, fillHeight);
                    fill.enabled = ratio > 0f;
                }

                fill.sprite = holderSprite;
                bool usesFillAmount = !normalizedValue.HasValue && fill.type == Image.Type.Filled;
                fill.type = normalizedValue.HasValue ? Image.Type.Simple
                    : usesFillAmount ? Image.Type.Filled : Image.Type.Simple;
                fill.pixelsPerUnitMultiplier = 1f;
                if (usesFillAmount)
                {
                    fill.fillMethod = Image.FillMethod.Horizontal;
                    fill.fillOrigin = (int)Image.OriginHorizontal.Left;
                }

                CareerUiProgressGradient gradient = fill.GetComponent<CareerUiProgressGradient>();
                if (normalizedValue.HasValue && usesLargeVariant)
                {
                    if (gradient == null)
                        gradient = fill.gameObject.AddComponent<CareerUiProgressGradient>();
                    gradient.enabled = true;
                    fill.color = Color.white;
                    gradient.SetValue(normalizedValue.Value);
                }
                else
                {
                    if (gradient != null)
                        gradient.enabled = false;
                    fill.color = normalizedValue.HasValue
                        ? CareerUiProgressGradient.EvaluateColor(normalizedValue.Value)
                        : sourceFillColor;
                }
            }
        }

        private static float ResolveProgressPixelsPerUnit(float trackHeight, bool usesLargeVariant)
        {
            float sourceRadius = usesLargeVariant
                ? LargeProgressTrackBorder.x
                : CompactProgressTrackBorder.x;
            float targetRadius = Mathf.Max(1f, trackHeight * 0.5f);
            return Mathf.Max(1f, sourceRadius / targetRadius);
        }

        /// <summary>정보 패널에 범용 또는 핵심 CTA용 9-slice 프레임을 적용한다.</summary>
        public static void ApplyPanel(Image image, bool isHero)
        {
            if (image == null || !EnsureLoaded())
                return;

            float sourceAlpha = image.color.a;
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.pixelsPerUnitMultiplier = 1f;
            Color surface = isHero
                ? Color.Lerp(CareerUiTheme.ReferencePanel, new Color(0.80f, 0.87f, 0.93f, 1f), 0.28f)
                : CareerUiTheme.ReferencePanel;
            image.color = new Color(surface.r, surface.g, surface.b, sourceAlpha);
            ConfigureReferenceOutline(image, isHero);
        }

        /// <summary>명시된 시각 역할을 가진 동적 Image에 공통 스킨을 즉시 적용한다.</summary>
        public static void ApplyVisualElement(Image image)
        {
            if (image == null || !EnsureLoaded())
                return;

            TryApplyExplicitVisualRole(image);
        }

        private static bool EnsureLoaded()
        {
            if (_isLoaded)
                return _universalPanel != null && _buttonNormal != null
                    && _sliderTrack != null && _largeProgressTrack != null
                    && _compactProgressTrack != null;

            _isLoaded = true;
            Texture2D universal = Resources.Load<Texture2D>(UniversalPanelPath);
            Texture2D hero = Resources.Load<Texture2D>(HeroPanelPath);
            Texture2D buttons = Resources.Load<Texture2D>(ButtonStatesPath);
            Texture2D sliders = Resources.Load<Texture2D>(SliderPartsPath);
            Texture2D progress = Resources.Load<Texture2D>(ProgressPartsPath);
            Texture2D effects = Resources.Load<Texture2D>(EffectsPath);

            // ImageGen 투명화 과정에서 프레임 바깥 최상단에 남은 고립 픽셀은 Sprite 영역에서 제외한다.
            _universalPanel = CreateSprite(universal, new Rect(13f, 0f, 1659f, 933f), PanelBorder);
            _heroPanel = CreateSprite(hero, new Rect(16f, 0f, 1505f, 980f), HeroPanelBorder);
            _buttonNormal = CreateSprite(buttons, new Rect(35f, 1180f, 955f, 172f), ButtonBorder);
            _buttonFocused = CreateSprite(buttons, new Rect(35f, 867f, 955f, 174f), ButtonBorder);
            _buttonPressed = CreateSprite(buttons, new Rect(35f, 558f, 955f, 172f), ButtonBorder);
            _sliderTrack = CreateSprite(sliders, new Rect(52f, 708f, 1432f, 124f), SliderBorder);
            _sliderFill = CreateSprite(sliders, new Rect(52f, 504f, 1432f, 120f), SliderBorder);
            _sliderHandle = CreateSprite(sliders, new Rect(632f, 159f, 270f, 267f), Vector4.zero);
            _largeProgressTrack = CreateSprite(
                progress, new Rect(89f, 735f, 1358f, 113f), LargeProgressTrackBorder);
            _largeProgressHolder = CreateSprite(
                progress, new Rect(127f, 608f, 1108f, 76f), Vector4.zero);
            _compactProgressTrack = CreateSprite(
                progress, new Rect(107f, 329f, 728f, 82f), CompactProgressTrackBorder);
            _compactProgressHolder = CreateSprite(
                progress, new Rect(126f, 237f, 661f, 57f), Vector4.zero);
            _horizontalSweep = CreateSprite(effects, new Rect(107f, 114f, 1283f, 201f), Vector4.zero);

            return _universalPanel != null && _buttonNormal != null
                && _sliderTrack != null && _largeProgressTrack != null
                && _compactProgressTrack != null;
        }

        private static Sprite CreateSprite(Texture2D texture, Rect rect, Vector4 border)
        {
            if (texture == null)
                return null;

            return Sprite.Create(
                texture,
                rect,
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                border);
        }

        private static void ApplyPanelHierarchy(Image[] images)
        {
            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];
                if (TryApplyExplicitVisualRole(image))
                    continue;
                if (image != null && image.GetComponentInParent<CareerUiFrame>() != null)
                    continue;
                if (!IsPanelCandidate(image))
                    continue;

                if (TryGetPrimaryPanelContainer(image, out Image container))
                {
                    NeutralizeLegacyContainer(container);
                    ApplyPanel(image, IsHeroPanel(container.name));
                    continue;
                }

                if (IsStandalonePrimaryPanel(image))
                {
                    ApplyPanel(image, IsHeroPanel(image.name));
                    continue;
                }

                ApplyFlatPanel(image);
            }
        }

        private static bool TryApplyExplicitVisualRole(Image image)
        {
            if (image == null)
                return false;

            CareerUiVisualElement visual = image.GetComponent<CareerUiVisualElement>();
            if (visual == null)
                return false;

            switch (visual.Role)
            {
                case CareerUiVisualRole.TexturedPanel:
                    image.sprite = visual.IsHeroFrame ? _heroPanel : _universalPanel;
                    image.type = Image.Type.Sliced;
                    // 작은 홈 패널에서도 모서리 장식이 본문을 침범하지 않도록 축소한다.
                    image.pixelsPerUnitMultiplier = 5f;
                    image.color = Color.white;
                    image.raycastTarget = false;
                    break;
                case CareerUiVisualRole.DecorativeFrame:
                    ApplyPanel(image, visual.IsHeroFrame);
                    image.raycastTarget = false;
                    break;
                case CareerUiVisualRole.FlatSurface:
                    ApplyExplicitFlatSurface(image);
                    break;
                case CareerUiVisualRole.FramedSurface:
                    ApplyFramedSurface(image);
                    break;
                case CareerUiVisualRole.FramedCard:
                    ApplyFramedCard(image);
                    break;
            }

            return true;
        }

        private static void ApplyExplicitFlatSurface(Image image)
        {
            image.sprite = null;
            image.type = Image.Type.Simple;
            Color source = image.color;
            image.color = new Color(source.r, source.g, source.b, Mathf.Min(source.a, MaximumFlatSurfaceAlpha));
            Outline outline = image.GetComponent<Outline>();
            if (outline != null)
                outline.enabled = false;
        }

        private static void ApplyFlatInteractiveControl(Button button, Image image)
        {
            image.sprite = null;
            image.type = Image.Type.Simple;
            Outline outline = image.GetComponent<Outline>();
            if (outline != null)
                outline.enabled = false;
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
        }

        private static void ApplyFramedSurface(Image image)
        {
            ApplyFramedBackplate(image, CompactFramedPixelsPerUnitMultiplier);
        }

        // 카드형 표면은 옆에 놓이는 Button 카드와 프레임 두께가 같아야 한 벌로 읽히므로
        // 정보 표면(FramedSurface)의 축소 배율 대신 원본 9-slice 두께를 그대로 쓴다.
        private static void ApplyFramedCard(Image image)
        {
            ApplyFramedBackplate(image, 1f);
        }

        private static void ApplyFramedBackplate(Image image, float pixelsPerUnitMultiplier)
        {
            Color sourceTint = image.color;
            bool isSemanticActive = image.sprite == _buttonFocused || IsSemanticActive(sourceTint);
            bool isAlreadyStyled = IsButtonStyled(image);
            image.sprite = isSemanticActive ? _buttonFocused : _buttonNormal;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = pixelsPerUnitMultiplier;
            if (!isAlreadyStyled)
                image.color = ResolveTextureTint(sourceTint, 0.24f);
            image.raycastTarget = false;

            Outline outline = image.GetComponent<Outline>();
            if (outline != null)
                outline.enabled = false;
        }

        private static void ApplyCompactFramedControl(
            Button button,
            Image image,
            bool isSemanticActive)
        {
            if (!IsButtonStyled(image))
            {
                Color sourceTint = image.color;
                image.sprite = isSemanticActive ? _buttonFocused : ResolveButtonSprite(sourceTint);
                image.color = ResolveTextureTint(sourceTint, 0.24f);
                if (isSemanticActive)
                    ConfigurePersistentSelectedTransition(button);
                else
                    ConfigureSpriteSwapTransition(button);
            }

            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = CompactFramedPixelsPerUnitMultiplier;
            if (button.GetComponent<RectMask2D>() == null)
                button.gameObject.AddComponent<RectMask2D>();
            EnsureButtonLabelLayout(button);
        }

        private static bool IsPanelCandidate(Image image)
        {
            if (image == null || image.GetComponentInParent<Button>() != null)
                return false;
            if (image.GetComponentInParent<Slider>() != null || image.GetComponentInParent<Scrollbar>() != null)
                return false;
            if (image.sprite != null && image.sprite != _universalPanel && image.sprite != _heroPanel)
                return false;

            Rect rect = image.rectTransform.rect;
            if (Mathf.Abs(rect.width) < 120f || Mathf.Abs(rect.height) < 60f)
                return false;

            string name = image.name;
            return name.EndsWith("Panel", StringComparison.Ordinal)
                || name.EndsWith("Card", StringComparison.Ordinal)
                || name.EndsWith("Surface", StringComparison.Ordinal)
                || name.EndsWith("Frame", StringComparison.Ordinal)
                || name.EndsWith("Modal", StringComparison.Ordinal)
                || name.Equals("Content", StringComparison.Ordinal)
                || name.Equals("Surface", StringComparison.Ordinal);
        }

        private static bool TryGetPrimaryPanelContainer(Image image, out Image container)
        {
            container = null;
            if (!image.name.Equals("Surface", StringComparison.Ordinal))
                return false;

            Transform parent = image.transform.parent;
            if (parent == null || !IsPrimaryPanelName(parent.name))
                return false;

            container = parent.GetComponent<Image>();
            return container != null
                && !HasPrimaryPanelAncestor(parent.parent)
                && Mathf.Abs(image.rectTransform.rect.width) >= 260f
                && Mathf.Abs(image.rectTransform.rect.height) >= 140f;
        }

        private static bool IsStandalonePrimaryPanel(Image image)
        {
            string name = image.name;
            if (!IsPrimaryPanelName(name)
                && !name.Equals("Content", StringComparison.Ordinal)
                && !(name.EndsWith("Frame", StringComparison.Ordinal)
                    && Mathf.Abs(image.rectTransform.rect.width) >= 800f
                    && Mathf.Abs(image.rectTransform.rect.height) >= 500f))
                return false;

            if (FindDirectSurface(image.transform) != null || HasPrimaryPanelAncestor(image.transform.parent))
                return false;

            Rect rect = image.rectTransform.rect;
            return Mathf.Abs(rect.width) >= 260f && Mathf.Abs(rect.height) >= 140f;
        }

        private static bool IsPrimaryPanelName(string name)
        {
            return name.EndsWith("Panel", StringComparison.Ordinal)
                || name.EndsWith("Modal", StringComparison.Ordinal);
        }

        private static bool HasPrimaryPanelAncestor(Transform current)
        {
            while (current != null)
            {
                if (IsPrimaryPanelName(current.name) && current.GetComponent<Image>() != null)
                    return true;
                current = current.parent;
            }

            return false;
        }

        private static Image FindDirectSurface(Transform container)
        {
            for (int i = 0; i < container.childCount; i++)
            {
                Transform child = container.GetChild(i);
                if (child.name.Equals("Surface", StringComparison.Ordinal))
                    return child.GetComponent<Image>();
            }

            return null;
        }

        private static void NeutralizeLegacyContainer(Image container)
        {
            container.sprite = null;
            container.type = Image.Type.Simple;
            container.color = Color.clear;
            Outline outline = container.GetComponent<Outline>();
            if (outline != null)
                outline.enabled = false;
        }

        private static void ApplyFlatPanel(Image image)
        {
            bool isSurface = image.name.Equals("Surface", StringComparison.Ordinal);
            bool hasSurface = FindDirectSurface(image.transform) != null;
            float alpha = image.color.a;
            image.sprite = null;
            image.type = Image.Type.Simple;
            Color baseColor = isSurface ? FlatSurfaceColor : hasSurface ? FlatBorderColor : FlatPanelColor;
            image.color = new Color(baseColor.r, baseColor.g, baseColor.b, Mathf.Min(alpha, baseColor.a));

            if (isSurface || hasSurface || image.name.Equals("Content", StringComparison.Ordinal))
                return;

            Outline outline = image.GetComponent<Outline>();
            if (outline == null)
                outline = image.gameObject.AddComponent<Outline>();
            outline.enabled = true;
            outline.effectColor = FlatBorderColor;
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;
        }

        private static void ApplyReferenceTextHierarchy(Text[] texts)
        {
            for (int i = 0; i < texts.Length; i++)
            {
                Text text = texts[i];
                if (text == null || text.GetComponentInParent<CareerUiPreserveTextColor>() != null ||
                    !HasLightSurface(text.transform))
                    continue;

                Color source = text.color;
                if (IsNear(source, CareerUiTheme.AccentGold) || IsNear(source, CareerUiTheme.Number))
                    text.color = WithAlpha(CareerUiTheme.ReferenceAccent, source.a);
                else if (IsNear(source, CareerUiTheme.TextSecondary) || IsNear(source, CareerUiTheme.TextMuted))
                    text.color = WithAlpha(CareerUiTheme.ReferenceTextSecondary, source.a);
                else if (IsNear(source, CareerUiTheme.TextPrimary) || IsBrightNeutral(source))
                    text.color = WithAlpha(CareerUiTheme.ReferenceText, source.a);
            }
        }

        private static bool HasLightSurface(Transform target)
        {
            Transform current = target.parent;
            while (current != null)
            {
                Image image = current.GetComponent<Image>();
                if (image != null && image.color.a > 0.15f)
                {
                    Color color = image.color;
                    float luminance = color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f;
                    return luminance >= 0.58f;
                }
                current = current.parent;
            }
            return false;
        }

        private static bool IsNear(Color first, Color second)
        {
            const float tolerance = 0.035f;
            return Mathf.Abs(first.r - second.r) <= tolerance &&
                Mathf.Abs(first.g - second.g) <= tolerance &&
                Mathf.Abs(first.b - second.b) <= tolerance;
        }

        private static bool IsBrightNeutral(Color color)
        {
            float maximum = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
            float minimum = Mathf.Min(color.r, Mathf.Min(color.g, color.b));
            return maximum >= 0.78f && maximum - minimum <= 0.14f;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private static bool IsHeroPanel(string name)
        {
            return name.IndexOf("NextGame", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Primary", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Result", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsPrimaryAction(string name)
        {
            if (name.IndexOf("Cancel", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Close", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;

            return name.IndexOf("Progress", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Start", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Continue", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Advance", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Confirm", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Execute", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Match", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void EnsurePrimaryActionShine(Button button)
        {
            if (!IsPrimaryAction(button.name) || button.GetComponent<CareerUiShine>() != null)
                return;

            CareerUiShine shine = button.gameObject.AddComponent<CareerUiShine>();
            shine.Initialize(_horizontalSweep);
        }

        private static void EnsureObserver(Transform target)
        {
            if (target.GetComponent<CareerUiSkinObserver>() == null)
                target.gameObject.AddComponent<CareerUiSkinObserver>();
        }

        private static bool IsCompactButton(RectTransform rect)
        {
            if (rect == null)
                return false;

            Rect bounds = rect.rect;
            return IsCompactButton(Mathf.Abs(bounds.width), Mathf.Abs(bounds.height));
        }

        private static bool IsCompactFramedControl(RectTransform rect)
        {
            return rect != null
                && Mathf.Abs(rect.rect.height) <= CompactButtonMaximumHeight;
        }

        private static bool IsCompactButton(float width, float height)
        {
            return width <= CompactButtonMaximumWidth
                && height <= CompactButtonMaximumHeight;
        }

        private static bool IsCardButton(RectTransform rect)
        {
            return rect != null && Mathf.Abs(rect.rect.height) >= CardButtonMinimumHeight;
        }

        private static bool IsButtonStyled(Image image)
        {
            return image.sprite == _buttonNormal
                || image.sprite == _buttonFocused
                || image.sprite == _buttonPressed;
        }

        private static void ApplyCompactButton(Button button, Image image, bool isSemanticActive)
        {
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.color = isSemanticActive ? CompactButtonSelectedColor : CompactButtonColor;

            Outline outline = image.GetComponent<Outline>();
            if (outline == null)
                outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = isSemanticActive
                ? CompactButtonSelectedBorderColor
                : CompactButtonBorderColor;
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;

            ConfigureCompactTransition(button);
            EnsureButtonLabelLayout(button);
        }

        private static void ApplyCardButton(Button button, Image image, bool isSemanticActive)
        {
            float sourceAlpha = image.color.a;
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.pixelsPerUnitMultiplier = 1f;
            Color surface = isSemanticActive
                ? CompactButtonSelectedColor
                : CareerUiTheme.ReferencePanel;
            image.color = new Color(surface.r, surface.g, surface.b, sourceAlpha);
            ConfigureReferenceOutline(image, isSemanticActive);

            ConfigureCardTransition(button, isSemanticActive);
            EnsureButtonLabelLayout(button);
        }

        private static void ConfigureSpriteSwapTransition(Button button)
        {
            SpriteState states = button.spriteState;
            states.highlightedSprite = _buttonFocused;
            states.selectedSprite = _buttonFocused;
            states.pressedSprite = _buttonPressed;
            states.disabledSprite = _buttonPressed;
            button.spriteState = states;
            button.transition = Selectable.Transition.SpriteSwap;
        }

        private static void ConfigurePersistentSelectedTransition(Button button)
        {
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.96f, 0.84f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.82f, 0.84f, 0.88f, 1f);
            colors.disabledColor = new Color(0.5f, 0.52f, 0.56f, 0.55f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.transition = Selectable.Transition.ColorTint;
        }

        private static void ConfigureCompactTransition(Button button)
        {
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.14f, 1.14f, 1.14f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.78f, 0.82f, 0.86f, 1f);
            colors.disabledColor = new Color(0.5f, 0.52f, 0.56f, 0.48f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.transition = Selectable.Transition.ColorTint;
        }

        private static void ConfigureCardTransition(Button button, bool isSemanticActive)
        {
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = isSemanticActive
                ? new Color(1f, 0.97f, 0.88f, 1f)
                : new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.8f, 0.82f, 0.84f, 1f);
            colors.disabledColor = new Color(0.48f, 0.5f, 0.54f, 0.5f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.transition = Selectable.Transition.ColorTint;
        }

        private static void ConfigureReferenceOutline(Image image, bool isSelected)
        {
            Outline outline = image.GetComponent<Outline>();
            if (outline == null)
                outline = image.gameObject.AddComponent<Outline>();
            outline.enabled = true;
            outline.effectColor = isSelected
                ? CareerUiTheme.ReferenceAccent
                : CareerUiTheme.ReferenceBorder;
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = false;
        }

        private static void RemoveLegacySelectedBadge(Button button)
        {
            Transform badge = button.transform.Find(LegacySelectedBadgeName);
            if (badge == null)
                return;

            if (Application.isPlaying)
            {
                badge.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(badge.gameObject);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(badge.gameObject);
            }
        }

        private static void EnsureButtonLabelLayout(Button button)
        {
            if (button.GetComponent<RectMask2D>() == null)
                button.gameObject.AddComponent<RectMask2D>();

            Text[] labels = button.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                Text label = labels[i];
                RectTransform rect = label.rectTransform;
                if (!IsStretchLabel(rect, button.transform))
                    continue;

                int maximumFontSize = Mathf.Max(1, label.fontSize);
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = Mathf.Max(10, Mathf.FloorToInt(maximumFontSize * 0.62f));
                label.resizeTextMaxSize = maximumFontSize;

                float buttonHeight = GetButtonHeight(button);
                float buttonWidth = GetButtonWidth(button);
                bool isCompact = IsCompactButton(buttonWidth, buttonHeight);
                float horizontalPadding = isCompact
                    ? 10f
                    : Mathf.Clamp(buttonHeight * 0.5f, MinimumLabelPadding, MaximumLabelPadding);
                Vector2 offsetMin = rect.offsetMin;
                Vector2 offsetMax = rect.offsetMax;
                offsetMin.x = Mathf.Max(offsetMin.x, horizontalPadding);
                offsetMin.y = Mathf.Max(offsetMin.y, 5f);
                offsetMax.x = Mathf.Min(offsetMax.x, -horizontalPadding);
                offsetMax.y = Mathf.Min(offsetMax.y, -5f);
                rect.offsetMin = offsetMin;
                rect.offsetMax = offsetMax;
            }
        }

        private static float GetButtonHeight(Button button)
        {
            RectTransform rect = button.transform as RectTransform;
            return rect != null ? Mathf.Abs(rect.rect.height) : 0f;
        }

        private static float GetButtonWidth(Button button)
        {
            RectTransform rect = button.transform as RectTransform;
            return rect != null ? Mathf.Abs(rect.rect.width) : 0f;
        }

        private static bool IsStretchLabel(RectTransform rect, Transform buttonTransform)
        {
            if (rect == null || rect.parent != buttonTransform)
                return false;

            const float tolerance = 0.001f;
            return Mathf.Abs(rect.anchorMin.x) <= tolerance
                && Mathf.Abs(rect.anchorMin.y) <= tolerance
                && Mathf.Abs(rect.anchorMax.x - 1f) <= tolerance
                && Mathf.Abs(rect.anchorMax.y - 1f) <= tolerance;
        }

        private static bool IsSemanticActive(Color color)
        {
            Color.RGBToHSV(color, out _, out float saturation, out float value);
            return value >= 0.28f && value <= 0.56f && saturation >= 0.42f;
        }

        private static Sprite ResolveButtonSprite(Color color)
        {
            Color.RGBToHSV(color, out _, out float saturation, out float value);
            return value >= 0.72f && saturation >= 0.42f ? _buttonFocused : _buttonNormal;
        }

        private static Color ResolveTextureTint(Color source, float sourceWeight)
        {
            Color tint = Color.Lerp(Color.white, new Color(source.r, source.g, source.b, 1f), sourceWeight);
            tint.a = source.a;
            return tint;
        }
    }

    /// <summary>밝은 Workspace 안의 독립적인 어두운 위젯이 자체 Text 팔레트를 유지하게 한다.</summary>
    [DisallowMultipleComponent]
    internal sealed class CareerUiPreserveTextColor : MonoBehaviour
    {
    }
}
