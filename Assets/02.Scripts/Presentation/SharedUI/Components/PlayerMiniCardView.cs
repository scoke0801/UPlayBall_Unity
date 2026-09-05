using System;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.SharedUI
{
    /// <summary>
    /// 선수 목록과 Roster 슬롯에서 공유하는 선택 가능한 읽기 전용 Mini Card 표면이다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(Image), typeof(Button))]
    public sealed class PlayerMiniCardView : MonoBehaviour
    {
        /// <summary>
        /// Compact Card의 기준 너비다.
        /// </summary>
        public const float PreferredWidth = 156f;

        /// <summary>
        /// Compact Card의 기준 높이다.
        /// </summary>
        public const float PreferredHeight = 212f;

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

        /// <summary>
        /// 사용자가 상세 보기 대상으로 카드를 선택했을 때 현재 모델을 전달한다.
        /// </summary>
        public event Action<PlayerMiniCardModel> Selected;

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
            text.resizeTextMinSize = Math.Max(9, fontSize - 4);
            text.resizeTextMaxSize = fontSize;
            text.raycastTarget = false;
            return text;
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
