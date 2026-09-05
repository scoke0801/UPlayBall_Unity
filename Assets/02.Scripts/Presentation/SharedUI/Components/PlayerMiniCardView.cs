using System;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Baseball.Presentation.SharedUI
{
    /// <summary>
    /// 선수 목록과 Roster 슬롯에서 공유하는 선택 가능한 읽기 전용 Mini Card 표면이다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(Image), typeof(Button))]
    public sealed class PlayerMiniCardView : MonoBehaviour, IPointerClickHandler
    {
        /// <summary>
        /// Compact Card의 기준 너비다.
        /// </summary>
        public const float PreferredWidth = 156f;

        /// <summary>
        /// Compact Card의 기준 높이다.
        /// </summary>
        public const float PreferredHeight = 212f;

        /// <summary>Roster 역할 슬롯에서 사용하는 세로형 카드 너비다.</summary>
        public const float LineupSlotWidth = 80f;

        /// <summary>Roster 역할 슬롯에서 사용하는 세로형 카드 높이다.</summary>
        public const float LineupSlotHeight = 148f;

        private static Color NeutralSurface => CareerUiTheme.Surface;
        private static Color HighlightedSurface => CareerUiTheme.SurfaceSelected;
        private static Color SelectedSurface => CareerUiTheme.PrimaryAction;
        private static Color WarningSurface => Color.Lerp(CareerUiTheme.Surface, CareerUiTheme.Warning, 0.2f);
        private static Color PortraitSurface => CareerUiTheme.PanelDark;
        private static Color DefaultAccent => CareerUiTheme.PrimaryBright;
        private static Color Warning => CareerUiTheme.Warning;
        private static Color TextPrimary => CareerUiTheme.TextPrimary;
        private static Color TextSecondary => CareerUiTheme.TextSecondary;
        private static Color TextMuted => CareerUiTheme.TextMuted;

        private static Font _defaultFont;

        private Image _surface;
        private Image _lineupFrame;
        private Image _accentStrip;
        private Image _portrait;
        private Outline _outline;
        private Button _button;
        private CanvasGroup _canvasGroup;
        private Text _nameText;
        private Text _positionText;
        private Text _yearText;
        private Text _costText;
        private Text _editionText;
        private Text _statusText;
        private PlayerMiniCardModel _model;
        private bool _usesLineupSlotLayout;
        private bool _hasCompactSurfaces;

        /// <summary>
        /// 사용자가 상세 보기 대상으로 카드를 선택했을 때 현재 모델을 전달한다.
        /// </summary>
        public event Action<PlayerMiniCardModel> Selected;
        public event Action<PlayerMiniCardModel> DetailRequested;

        /// <summary>왼쪽 선택·교환과 독립적으로 우클릭 상세 보기를 요청한다.</summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right && _model != null)
                DetailRequested?.Invoke(_model);
        }

        /// <summary>
        /// 현재 카드에 바인딩된 순수 표시 모델이다.
        /// </summary>
        public PlayerMiniCardModel Model => _model;

        /// <summary>
        /// 프리팹 통합 전에도 부모 아래에 Compact Card 계층을 생성한다.
        /// </summary>
        public static PlayerMiniCardView CreateRuntime(Transform parent, string objectName = "PlayerMiniCard")
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));

            var cardObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            cardObject.transform.SetParent(parent, false);
            RectTransform rect = cardObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(PreferredWidth, PreferredHeight);
            return cardObject.AddComponent<PlayerMiniCardView>();
        }

        /// <summary>
        /// 카드 모델을 표시하고 기존 초상화는 유지한다.
        /// </summary>
        public void Bind(PlayerMiniCardModel model)
        {
            Bind(model, _portrait != null ? _portrait.sprite : null);
        }

        /// <summary>
        /// 카드 모델과 Asset Resolver가 찾은 초상화 Sprite를 함께 표시한다.
        /// </summary>
        public void Bind(PlayerMiniCardModel model, Sprite portrait)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            EnsureHierarchy();

            _nameText.text = model.DisplayName;
            _positionText.text = model.PositionLabel;
            _yearText.text = model.YearLabel;
            _costText.text = model.CostLabel;
            _editionText.text = model.EditionLabel;
            _statusText.text = model.StatusLabel;
            _statusText.gameObject.SetActive(!string.IsNullOrWhiteSpace(model.StatusLabel));
            _portrait.sprite = portrait;
            _portrait.preserveAspect = true;
            _portrait.color = portrait == null ? PortraitSurface : Color.white;
            _button.interactable = model.IsInteractable;
            _canvasGroup.alpha = model.VisualState == PlayerMiniCardVisualState.Disabled ? 0.48f : 1f;

            ApplyVisualState(model.VisualState, ParseAccent(model.TeamAccentHex));
        }

        /// <summary>
        /// 모델은 유지한 채 비동기로 로드된 초상화 Sprite만 교체한다.
        /// </summary>
        public void SetPortrait(Sprite portrait)
        {
            EnsureHierarchy();
            _portrait.sprite = portrait;
            _portrait.preserveAspect = true;
            _portrait.color = portrait == null ? PortraitSurface : Color.white;
        }

        /// <summary>같은 선수 카드 정보를 Roster 역할표에 맞는 고밀도 세로 카드로 배치한다.</summary>
        public void UseLineupSlotLayout()
        {
            EnsureHierarchy();
            _usesLineupSlotLayout = true;
            RectTransform root = GetComponent<RectTransform>();
            root.sizeDelta = new Vector2(LineupSlotWidth, LineupSlotHeight);
            _lineupFrame.gameObject.SetActive(false);
            if (!_hasCompactSurfaces)
            {
                // 얇은 프레임과 정보 행을 실제 UI 영역으로 분리해 작은 카드에서도 선명하게 표시한다.
                AddCompactSurface("PortraitBacking", new Color(0.87f, 0.90f, 0.92f), 0.33f, 0.88f);
                AddCompactSurface("NameBacking", new Color(0.055f, 0.13f, 0.22f), 0.235f, 0.335f);
                AddCompactSurface("DetailsBacking", new Color(0.98f, 0.98f, 0.96f), 0.12f, 0.235f);
                AddCompactSurface("RoleBacking", new Color(0.80f, 0.87f, 0.89f), 0.01f, 0.115f);
                _hasCompactSurfaces = true;
            }
            SetAnchors(_lineupFrame.rectTransform, new Vector2(0f, 0.10f), new Vector2(1f, 0.88f), Vector2.zero, Vector2.zero);
            _portrait.gameObject.SetActive(true);

            SetAnchors(_accentStrip.rectTransform, new Vector2(0.08f, 0.02f), new Vector2(0.92f, 0.04f),
                Vector2.zero, Vector2.zero);
            SetAnchors(_portrait.rectTransform, new Vector2(0.05f, 0.335f), new Vector2(0.95f, 0.88f),
                Vector2.zero, Vector2.zero);
            SetAnchors(_positionText.rectTransform, new Vector2(0.04f, 0.89f), new Vector2(0.96f, 1f),
                Vector2.zero, Vector2.zero);
            SetAnchors(_yearText.rectTransform, new Vector2(0.75f, 0.79f), new Vector2(0.94f, 0.87f),
                Vector2.zero, Vector2.zero);
            SetAnchors(_nameText.rectTransform, new Vector2(0.03f, 0.245f), new Vector2(0.97f, 0.335f),
                Vector2.zero, Vector2.zero);
            SetAnchors(_statusText.rectTransform, new Vector2(0.03f, 0.005f), new Vector2(0.97f, 0.09f),
                Vector2.zero, Vector2.zero);
            SetAnchors(_costText.rectTransform, new Vector2(0.46f, 0.12f), new Vector2(0.98f, 0.235f),
                Vector2.zero, Vector2.zero);
            SetAnchors(_editionText.rectTransform, new Vector2(0.02f, 0.12f), new Vector2(0.46f, 0.235f),
                Vector2.zero, Vector2.zero);
            _positionText.alignment = TextAnchor.MiddleCenter;
            _yearText.alignment = TextAnchor.MiddleCenter;
            _nameText.alignment = TextAnchor.MiddleCenter;
            _statusText.alignment = TextAnchor.MiddleCenter;
            _accentStrip.gameObject.SetActive(false);
            _costText.alignment = TextAnchor.MiddleCenter;
            _editionText.alignment = TextAnchor.MiddleCenter;
            _nameText.fontSize = 11;
            _positionText.fontSize = 9;
            _yearText.fontSize = 7;
            _costText.fontSize = 8;
            _editionText.fontSize = 7;
            _statusText.fontSize = 7;
            SetBestFitRange(_nameText, 10, 13);
            SetBestFitRange(_positionText, 9, 11);
            SetBestFitRange(_yearText, 6, 7);
            SetBestFitRange(_costText, 9, 11);
            SetBestFitRange(_editionText, 9, 11);
            SetBestFitRange(_statusText, 10, 12);
            ApplyVisualState(_model?.VisualState ?? PlayerMiniCardVisualState.Normal,
                ParseAccent(_model?.TeamAccentHex));
        }

        /// <summary>선택 교환 중에도 모델을 다시 만들지 않고 카드 강조 상태만 바꾼다.</summary>
        public void SetVisualState(PlayerMiniCardVisualState visualState)
        {
            EnsureHierarchy();
            ApplyVisualState(visualState, ParseAccent(_model?.TeamAccentHex));
        }

        private void AddCompactSurface(string name, Color color, float bottom, float top)
        {
            Image surface = CreateImage(name, transform, color);
            SetAnchors(surface.rectTransform, new Vector2(0.025f, bottom), new Vector2(0.975f, top), Vector2.zero, Vector2.zero);
            surface.transform.SetSiblingIndex(1);
        }

        private void Awake()
        {
            EnsureHierarchy();
        }

        private void OnDestroy()
        {
            if (_button != null)
                _button.onClick.RemoveListener(HandleSelected);
        }

        private void EnsureHierarchy()
        {
            if (_nameText != null)
                return;

            RectTransform root = GetComponent<RectTransform>();
            if (root.sizeDelta == Vector2.zero)
                root.sizeDelta = new Vector2(PreferredWidth, PreferredHeight);
            if (GetComponent<CareerUiPreserveTextColor>() == null)
                gameObject.AddComponent<CareerUiPreserveTextColor>();

            _surface = GetComponent<Image>();
            _surface.color = NeutralSurface;
            _button = GetComponent<Button>();
            _button.targetGraphic = _surface;
            _button.transition = Selectable.Transition.ColorTint;
            _button.colors = new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f),
                pressedColor = new Color(0.84f, 0.88f, 0.86f, 1f),
                selectedColor = Color.white,
                disabledColor = new Color(0.6f, 0.6f, 0.6f, 1f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };
            _button.onClick.AddListener(HandleSelected);
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _outline = GetComponent<Outline>();
            if (_outline == null)
                _outline = gameObject.AddComponent<Outline>();
            _outline.effectDistance = new Vector2(1f, -1f);
            _outline.effectColor = CareerUiTheme.Border;
            _outline.useGraphicAlpha = false;

            _lineupFrame = CreateImage("LineupSubFrame", root, Color.white);
            _lineupFrame.sprite = Resources.Load<Sprite>("UI/PlayerCards/PlayerCard_PortraitMiniFrame_V2");
            _lineupFrame.preserveAspect = false;
            SetAnchors(_lineupFrame.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _lineupFrame.gameObject.SetActive(false);

            _accentStrip = CreateImage("TeamAccent", root, DefaultAccent);
            SetAnchors(_accentStrip.rectTransform, new Vector2(0f, 1f), Vector2.one,
                Vector2.zero, new Vector2(0f, 4f));

            _portrait = CreateImage("Portrait", root, PortraitSurface);
            SetAnchors(_portrait.rectTransform, new Vector2(0f, 0.38f), new Vector2(1f, 1f),
                new Vector2(8f, 5f), new Vector2(-8f, -10f));

            _yearText = CreateText("Year", root, 12, FontStyle.Bold, TextAnchor.UpperLeft, TextSecondary);
            SetAnchors(_yearText.rectTransform, new Vector2(0f, 0.72f), new Vector2(0.55f, 0.96f),
                new Vector2(13f, 0f), Vector2.zero);
            _costText = CreateText("Cost", root, 12, FontStyle.Bold, TextAnchor.UpperRight, TextPrimary);
            SetAnchors(_costText.rectTransform, new Vector2(0.45f, 0.72f), new Vector2(1f, 0.96f),
                Vector2.zero, new Vector2(-13f, 0f));

            _nameText = CreateText("Name", root, 18, FontStyle.Bold, TextAnchor.MiddleLeft, TextPrimary);
            SetAnchors(_nameText.rectTransform, new Vector2(0f, 0.23f), new Vector2(1f, 0.39f),
                new Vector2(10f, 0f), new Vector2(-10f, 0f));
            _positionText = CreateText("Position", root, 14, FontStyle.Bold, TextAnchor.MiddleLeft, DefaultAccent);
            SetAnchors(_positionText.rectTransform, new Vector2(0f, 0.11f), new Vector2(0.35f, 0.24f),
                new Vector2(10f, 0f), Vector2.zero);
            _editionText = CreateText("Edition", root, 12, FontStyle.Normal, TextAnchor.MiddleRight, TextMuted);
            SetAnchors(_editionText.rectTransform, new Vector2(0.32f, 0.11f), new Vector2(1f, 0.24f),
                Vector2.zero, new Vector2(-10f, 0f));
            _statusText = CreateText("Status", root, 11, FontStyle.Bold, TextAnchor.MiddleLeft, TextSecondary);
            SetAnchors(_statusText.rectTransform, Vector2.zero, new Vector2(1f, 0.12f),
                new Vector2(10f, 1f), new Vector2(-10f, 0f));
            _statusText.gameObject.SetActive(false);
        }

        private void ApplyVisualState(PlayerMiniCardVisualState visualState, Color accent)
        {
            _accentStrip.color = accent;
            _positionText.color = accent;
            _statusText.color = visualState == PlayerMiniCardVisualState.Warning ? Warning : TextSecondary;

            if (_usesLineupSlotLayout)
            {
                ApplyLineupSlotVisualState(visualState, accent);
                return;
            }

            switch (visualState)
            {
                case PlayerMiniCardVisualState.Highlighted:
                    _surface.color = HighlightedSurface;
                    _outline.effectColor = accent;
                    _outline.effectDistance = new Vector2(2f, -2f);
                    break;
                case PlayerMiniCardVisualState.Selected:
                    _surface.color = SelectedSurface;
                    _outline.effectColor = accent;
                    _outline.effectDistance = new Vector2(3f, -3f);
                    break;
                case PlayerMiniCardVisualState.Warning:
                    _surface.color = WarningSurface;
                    _outline.effectColor = Warning;
                    _outline.effectDistance = new Vector2(2f, -2f);
                    break;
                default:
                    _surface.color = NeutralSurface;
                    _outline.effectColor = CareerUiTheme.Border;
                    _outline.effectDistance = new Vector2(1f, -1f);
                    break;
            }
        }

        private void ApplyLineupSlotVisualState(PlayerMiniCardVisualState visualState, Color accent)
        {
            bool isSelected = visualState == PlayerMiniCardVisualState.Selected;
            _lineupFrame.color = visualState == PlayerMiniCardVisualState.Warning
                ? new Color(1f, 0.88f, 0.62f, 1f)
                : isSelected ? new Color(0.72f, 0.86f, 1f, 1f) : Color.white;
            _surface.color = visualState == PlayerMiniCardVisualState.Warning
                ? new Color(0.45f, 0.28f, 0.08f, 0.42f)
                : isSelected ? new Color(0.65f, 0.81f, 0.92f, 1f) : new Color(0.96f, 0.97f, 0.97f, 1f);
            _outline.effectColor = visualState == PlayerMiniCardVisualState.Warning
                ? CareerUiTheme.Warning
                : isSelected ? accent : new Color(0.65f, 0.71f, 0.75f, 1f);
            _outline.effectDistance = isSelected ? new Vector2(2f, -2f) : new Vector2(1f, -1f);

            Color primary = Color.white;
            Color secondary = new Color(0.78f, 0.84f, 0.89f, 1f);
            _nameText.color = primary;
            _yearText.color = new Color(0.08f, 0.12f, 0.18f, 1f);
            _costText.color = new Color(0.08f, 0.12f, 0.18f, 1f);
            _editionText.color = new Color(0.08f, 0.12f, 0.18f, 1f);
            _positionText.color = new Color(0.08f, 0.12f, 0.18f, 1f);
            _statusText.color = visualState == PlayerMiniCardVisualState.Warning
                ? new Color(1f, 0.76f, 0.30f, 1f)
                : new Color(0.08f, 0.12f, 0.18f, 1f);
        }

        private void HandleSelected()
        {
            if (_model != null && _model.IsInteractable)
                Selected?.Invoke(_model);
        }

        private static Color ParseAccent(string htmlColor)
        {
            if (!string.IsNullOrWhiteSpace(htmlColor) && ColorUtility.TryParseHtmlString(htmlColor, out Color parsed))
                return parsed;
            return DefaultAccent;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            var imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text CreateText(
            string name,
            Transform parent,
            int fontSize,
            FontStyle fontStyle,
            TextAnchor alignment,
            Color color)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(parent, false);
            Text text = textObject.GetComponent<Text>();
            text.font = DefaultFont;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Math.Max(6, fontSize - 4);
            text.resizeTextMaxSize = fontSize;
            text.raycastTarget = false;
            return text;
        }

        private static void SetBestFitRange(Text text, int minimum, int maximum)
        {
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = minimum;
            text.resizeTextMaxSize = maximum;
        }

        private static Font DefaultFont =>
            _defaultFont ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        private static void SetAnchors(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
