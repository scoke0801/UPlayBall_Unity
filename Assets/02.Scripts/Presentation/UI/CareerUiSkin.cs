using System;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.UI
{
    /// <summary>선수 커리어 UI의 공통 프레임, 버튼, 슬라이더와 강조 연출을 한 테마로 적용한다.</summary>
    public static class CareerUiSkin
    {
        private const string UniversalPanelPath = "UI/Skin/ui_panel_universal_v2";
        private const string HeroPanelPath = "UI/Skin/ui_panel_hero_v2";
        private const string ButtonStatesPath = "UI/Skin/ui_button_states";
        private const string SelectedPointPath = "UI/Skin/ui_selected_point";
        private const string SliderPartsPath = "UI/Skin/ui_slider_parts";
        private const string EffectsPath = "UI/Skin/ui_fx_atlas";
        private const string SelectedBadgeName = "SkinSelectedBadge";

        private const float MinimumLabelPadding = 20f;
        private const float MaximumLabelPadding = 52f;
        private const float CompactButtonMaximumWidth = 180f;
        private const float CompactButtonMaximumHeight = 46f;
        private const float CardButtonMinimumHeight = 200f;

        private static readonly Vector4 PanelBorder = new(72f, 62f, 72f, 62f);
        private static readonly Vector4 HeroPanelBorder = new(64f, 64f, 64f, 64f);
        private static readonly Vector4 ButtonBorder = new(96f, 44f, 54f, 44f);
        private static readonly Vector4 SliderBorder = new(68f, 46f, 68f, 46f);
        private static readonly Color FlatPanelColor = new(0.014f, 0.048f, 0.078f, 0.98f);
        private static readonly Color FlatSurfaceColor = new(0.009f, 0.032f, 0.052f, 0.98f);
        private static readonly Color FlatBorderColor = new(0.17f, 0.28f, 0.36f, 0.92f);
        private static readonly Color CompactButtonColor = new(0.018f, 0.062f, 0.098f, 1f);
        private static readonly Color CompactButtonSelectedColor = new(0.035f, 0.25f, 0.42f, 1f);
        private static readonly Color CompactButtonBorderColor = new(0.34f, 0.45f, 0.53f, 0.9f);
        private static readonly Color CompactButtonSelectedBorderColor = new(0.94f, 0.86f, 0.66f, 1f);

        private static Sprite _universalPanel;
        private static Sprite _heroPanel;
        private static Sprite _buttonNormal;
        private static Sprite _buttonFocused;
        private static Sprite _buttonPressed;
        private static Sprite _buttonSelectedBadge;
        private static Sprite _sliderTrack;
        private static Sprite _sliderFill;
        private static Sprite _sliderHandle;
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

            bool hasSelectedBadge = button.transform.Find(SelectedBadgeName) != null;
            bool isSemanticActive = hasSelectedBadge
                || image.sprite == _buttonFocused
                || image.sprite == _heroPanel
                || IsSemanticActive(image.color);

            if (IsCardButton(image.rectTransform))
            {
                ApplyCardButton(button, image, isSemanticActive);
                return;
            }

            if (IsCompactButton(image.rectTransform))
            {
                ApplyCompactButton(button, image, isSemanticActive);
                return;
            }

            if (IsButtonStyled(image))
            {
                EnsureButtonLabelLayout(button, hasSelectedBadge);
                EnsurePrimaryActionShine(button);
                return;
            }

            Color sourceTint = image.color;
            image.sprite = isSemanticActive ? _buttonFocused : ResolveButtonSprite(sourceTint);
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
            image.color = ResolveTextureTint(sourceTint, 0.24f);

            if (isSemanticActive)
            {
                EnsureSelectedBadge(button);
                ConfigurePersistentSelectedTransition(button);
            }
            else
            {
                ConfigureSpriteSwapTransition(button);
            }

            if (button.GetComponent<RectMask2D>() == null)
                button.gameObject.AddComponent<RectMask2D>();

            EnsureButtonLabelLayout(button, isSemanticActive);
            EnsurePrimaryActionShine(button);
        }

        /// <summary>Slider의 Track, Fill, Handle를 공통 이미지 파츠로 교체한다.</summary>
        public static void ApplySlider(Slider slider)
        {
            if (slider == null || !EnsureLoaded())
                return;

            Image track = slider.GetComponent<Image>();
            if (track != null)
            {
                if (track.sprite != _sliderTrack)
                    track.color = ResolveTextureTint(track.color, 0.35f);
                track.sprite = _sliderTrack;
                track.type = Image.Type.Sliced;
            }

            Image fill = slider.fillRect != null ? slider.fillRect.GetComponent<Image>() : null;
            if (fill != null)
            {
                fill.sprite = _sliderFill;
                fill.type = Image.Type.Sliced;
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

        /// <summary>정보 패널에 범용 또는 핵심 CTA용 9-slice 프레임을 적용한다.</summary>
        public static void ApplyPanel(Image image, bool isHero)
        {
            if (image == null || !EnsureLoaded())
                return;

            float sourceAlpha = image.color.a;
            image.sprite = isHero ? _heroPanel : _universalPanel;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
            image.color = new Color(1f, 1f, 1f, sourceAlpha);
        }

        private static bool EnsureLoaded()
        {
            if (_isLoaded)
                return _universalPanel != null && _buttonNormal != null && _sliderTrack != null;

            _isLoaded = true;
            Texture2D universal = Resources.Load<Texture2D>(UniversalPanelPath);
            Texture2D hero = Resources.Load<Texture2D>(HeroPanelPath);
            Texture2D buttons = Resources.Load<Texture2D>(ButtonStatesPath);
            Texture2D selectedPoint = Resources.Load<Texture2D>(SelectedPointPath);
            Texture2D sliders = Resources.Load<Texture2D>(SliderPartsPath);
            Texture2D effects = Resources.Load<Texture2D>(EffectsPath);

            // ImageGen 투명화 과정에서 프레임 바깥 최상단에 남은 고립 픽셀은 Sprite 영역에서 제외한다.
            _universalPanel = CreateSprite(universal, new Rect(13f, 0f, 1659f, 933f), PanelBorder);
            _heroPanel = CreateSprite(hero, new Rect(16f, 0f, 1505f, 980f), HeroPanelBorder);
            _buttonNormal = CreateSprite(buttons, new Rect(35f, 1180f, 955f, 172f), ButtonBorder);
            _buttonFocused = CreateSprite(buttons, new Rect(35f, 867f, 955f, 174f), ButtonBorder);
            _buttonPressed = CreateSprite(buttons, new Rect(35f, 558f, 955f, 172f), ButtonBorder);
            _buttonSelectedBadge = CreateSprite(selectedPoint, new Rect(177f, 138f, 900f, 904f), Vector4.zero);
            _sliderTrack = CreateSprite(sliders, new Rect(52f, 708f, 1432f, 124f), SliderBorder);
            _sliderFill = CreateSprite(sliders, new Rect(52f, 504f, 1432f, 120f), SliderBorder);
            _sliderHandle = CreateSprite(sliders, new Rect(632f, 159f, 270f, 267f), Vector4.zero);
            _horizontalSweep = CreateSprite(effects, new Rect(107f, 114f, 1283f, 201f), Vector4.zero);

            return _universalPanel != null && _buttonNormal != null && _sliderTrack != null;
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

            if (isSemanticActive)
                EnsureSelectedBadge(button);
            ConfigureCompactTransition(button);
            EnsureButtonLabelLayout(button, isSemanticActive);
        }

        private static void ApplyCardButton(Button button, Image image, bool isSemanticActive)
        {
            float sourceAlpha = image.color.a;
            image.sprite = isSemanticActive ? _heroPanel : _universalPanel;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
            image.color = new Color(1f, 1f, 1f, sourceAlpha);

            // 대형 카드 프레임 자체가 선택 위계를 표현한다. 작은 Point를 겹치면 프레임 장식과 충돌한다.
            RemoveSelectedBadge(button);
            ConfigureCardTransition(button, isSemanticActive);
            EnsureButtonLabelLayout(button, isSemanticActive);
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

        private static void EnsureSelectedBadge(Button button)
        {
            if (_buttonSelectedBadge == null || button.transform.Find(SelectedBadgeName) != null)
                return;

            var badgeObject = new GameObject(
                SelectedBadgeName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            RectTransform rect = (RectTransform)badgeObject.transform;
            rect.SetParent(button.transform, false);
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);

            float buttonHeight = GetButtonHeight(button);
            float buttonWidth = GetButtonWidth(button);
            bool isCompact = IsCompactButton(buttonWidth, buttonHeight);
            float badgeHeight = isCompact
                ? Mathf.Clamp(buttonHeight * 0.44f, 14f, 18f)
                : Mathf.Clamp(buttonHeight * 0.42f, 24f, 36f);
            rect.sizeDelta = new Vector2(badgeHeight * 0.85f, badgeHeight);
            float badgeX = isCompact ? 8f : Mathf.Clamp(buttonHeight * 0.7f, 28f, 48f);
            rect.anchoredPosition = new Vector2(badgeX, 0f);
            rect.SetAsFirstSibling();

            Image badge = badgeObject.GetComponent<Image>();
            badge.sprite = _buttonSelectedBadge;
            badge.type = Image.Type.Simple;
            badge.preserveAspect = true;
            badge.raycastTarget = false;
            badge.color = Color.white;
        }

        private static void RemoveSelectedBadge(Button button)
        {
            Transform badge = button.transform.Find(SelectedBadgeName);
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

        private static void EnsureButtonLabelLayout(Button button, bool hasSelectedBadge)
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
                if (hasSelectedBadge)
                {
                    RectTransform badge = button.transform.Find(SelectedBadgeName) as RectTransform;
                    if (badge != null)
                        horizontalPadding = Mathf.Max(horizontalPadding, badge.anchoredPosition.x + badge.rect.width + 8f);
                }
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
}
