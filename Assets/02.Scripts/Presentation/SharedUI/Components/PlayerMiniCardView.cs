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
        public const float PreferredWidth = 151f;

        /// <summary>
        /// Compact Card의 기준 높이다.
        /// </summary>
        public const float PreferredHeight = 212f;

        /// <summary>Roster 역할 슬롯에서 사용하는 세로형 카드 너비다.</summary>
        public const float LineupSlotWidth = 80f;

        /// <summary>Roster 역할 슬롯에서 사용하는 세로형 카드 높이다.</summary>
        public const float LineupSlotHeight = 120f;

        private static Color NeutralSurface => new Color32(9, 12, 20, 255);
        private static Color HighlightedSurface => new Color32(32, 44, 65, 255);
        private static Color SelectedSurface => new Color32(41, 58, 81, 255);
        private static Color WarningSurface => Color.Lerp(NeutralSurface, CareerUiTheme.Warning, 0.2f);
        private static Color PortraitSurface => new Color32(17, 25, 48, 255);
        private static Color DefaultAccent => CareerUiTheme.PrimaryBright;
        private static Color Warning => CareerUiTheme.Warning;
        private static Color TextPrimary => Color.white;
        private static Color TextSecondary => new Color32(205, 215, 229, 255);
        private static Color TextMuted => new Color32(173, 187, 207, 255);

        private static Font _defaultFont;

        private Image _surface;
        private Image _lineupFrame;
        private Image _accentStrip;
        private Image _portrait;
        private Image _portraitBacking;
        private RectTransform _nameBand;
        private RectTransform _costStars;
        private Outline _outline;
        private Button _button;
        private CanvasGroup _canvasGroup;
        private Text _nameText;
        private Text _positionText;
        private Text _yearText;
        private Text _costText;
        private Text _editionText;
        private Text _statusText;
        private Image _assignmentBadge;
        private Text _assignmentText;
        private Image _teamEmblem;
        private PlayerMiniCardModel _model;
        private bool _usesLineupSlotLayout;
        private bool _usesPrimaryClickForDetail;

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

        /// <summary>선택 기능이 없는 읽기 전용 카드에서 좌클릭도 상세 보기 입력으로 사용한다.</summary>
        public void SetPrimaryClickForDetail(bool enabled)
        {
            EnsureHierarchy();
            _usesPrimaryClickForDetail = enabled;
            UpdateButtonInteractable();
        }

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
            PlayerMiniCardView view = cardObject.AddComponent<PlayerMiniCardView>();
            view.EnsureHierarchy();
            return view;
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
            portrait = PlayerPortraitSprites.GetAssigned(model.PlayerId)
                ?? PlayerPortraitSprites.GetAssigned(model.PortraitAssetKey) ?? portrait;
            EnsureHierarchy();

            if (_costStars != null) _costStars.gameObject.SetActive(false);

            SetAssignmentBadge(null);
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
            if (_teamEmblem != null) _teamEmblem.gameObject.SetActive(false);
            UpdateButtonInteractable();
            _canvasGroup.alpha = model.VisualState == PlayerMiniCardVisualState.Disabled ? 0.48f : 1f;

            ApplyVisualState(model.VisualState, ParseAccent(model.TeamAccentHex));
        }

        /// <summary>
        /// 모델은 유지한 채 비동기로 로드된 초상화 Sprite만 교체한다.
        /// </summary>
        public void SetPortrait(Sprite portrait)
        {
            EnsureHierarchy();
            portrait = PlayerPortraitSprites.GetAssigned(_model?.PlayerId)
                ?? PlayerPortraitSprites.GetAssigned(_model?.PortraitAssetKey) ?? portrait;
            _portrait.sprite = portrait;
            _portrait.preserveAspect = true;
            _portrait.color = portrait == null
                ? (_model?.FrameEdition.HasValue == true ? Color.clear : PortraitSurface)
                : Color.white;
        }

        /// <summary>같은 선수 카드 정보를 Roster 역할표에 맞는 고밀도 세로 카드로 배치한다.</summary>
        public void UseLineupSlotLayout()
        {
            EnsureHierarchy();
            _usesLineupSlotLayout = true;
            CareerUiVisualElement visual = GetComponent<CareerUiVisualElement>();
            if (visual == null) visual = gameObject.AddComponent<CareerUiVisualElement>();
            visual.Initialize(CareerUiVisualRole.FlatSurface);
            RectTransform root = GetComponent<RectTransform>();
            root.sizeDelta = new Vector2(LineupSlotWidth, LineupSlotHeight);
            _lineupFrame.gameObject.SetActive(true);
            _portraitBacking.gameObject.SetActive(false);
            _nameBand.gameObject.SetActive(true);
            _nameBand.GetComponent<PlayerCardSurface>().SetColors(Color.black, Color.black);
            SetAnchors(_nameBand, new Vector2(.025f, .17f), new Vector2(.975f, .32f), Vector2.zero, Vector2.zero);
            SetAnchors(_lineupFrame.rectTransform, Vector2.zero, new Vector2(1f, .89f), Vector2.zero, Vector2.zero);
            _portrait.gameObject.SetActive(true);

            SetAnchors(_accentStrip.rectTransform, new Vector2(0.08f, 0.01f), new Vector2(0.92f, 0.025f),
                Vector2.zero, Vector2.zero);
            SetAnchors(_portrait.rectTransform, new Vector2(0.035f, 0.32f), new Vector2(0.965f, 0.88f),
                Vector2.zero, Vector2.zero);
            SetAnchors(_positionText.rectTransform, new Vector2(0.06f, 0.88f), new Vector2(0.94f, 0.99f),
                Vector2.zero, Vector2.zero);
            SetAnchors(_yearText.rectTransform, new Vector2(0.03f, 0.22f), new Vector2(0.24f, 0.32f),
                Vector2.zero, Vector2.zero);
            SetAnchors(_nameText.rectTransform, new Vector2(0.25f, 0.22f), new Vector2(0.97f, 0.32f),
                Vector2.zero, Vector2.zero);
            SetAnchors(_statusText.rectTransform, new Vector2(0.03f, 0.005f), new Vector2(0.97f, 0.11f),
                Vector2.zero, Vector2.zero);
            SetAnchors(_costText.rectTransform, new Vector2(0.47f, 0.115f), new Vector2(0.95f, 0.22f),
                Vector2.zero, Vector2.zero);
            SetAnchors(_editionText.rectTransform, new Vector2(0.035f, 0.115f), new Vector2(0.47f, 0.22f),
                Vector2.zero, Vector2.zero);
            _positionText.alignment = TextAnchor.MiddleCenter;
            _yearText.alignment = TextAnchor.MiddleCenter;
            _nameText.alignment = TextAnchor.MiddleCenter;
            _statusText.alignment = TextAnchor.MiddleCenter;
            _accentStrip.gameObject.SetActive(true);
            SetAnchors(_accentStrip.rectTransform, new Vector2(.025f, .115f), new Vector2(.975f, .22f), Vector2.zero, Vector2.zero);
            _costText.alignment = TextAnchor.MiddleCenter;
            _editionText.alignment = TextAnchor.MiddleCenter;
            _nameText.fontSize = 11;
            _positionText.fontSize = 9;
            _yearText.fontSize = 7;
            _costText.fontSize = 8;
            _editionText.fontSize = 7;
            _statusText.fontSize = 7;
            SetBestFitRange(_nameText, 10, 26);
            SetBestFitRange(_positionText, 9, 20);
            SetBestFitRange(_yearText, 9, 18);
            SetBestFitRange(_costText, 9, 24);
            SetBestFitRange(_editionText, 9, 20);
            SetBestFitRange(_statusText, 10, 22);
            ApplyVisualState(_model?.VisualState ?? PlayerMiniCardVisualState.Normal,
                ParseAccent(_model?.TeamAccentHex));
        }

        /// <summary>선택 교환 중에도 모델을 다시 만들지 않고 카드 강조 상태만 바꾼다.</summary>
        public void SetVisualState(PlayerMiniCardVisualState visualState)
        {
            EnsureHierarchy();
            ApplyVisualState(visualState, ParseAccent(_model?.TeamAccentHex));
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
            _lineupFrame.sprite = Resources.Load<Sprite>("UI/PlayerCards/PlayerCard_Mini_Reference");
            _lineupFrame.preserveAspect = false;
            SetAnchors(_lineupFrame.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _lineupFrame.gameObject.SetActive(false);

            _portraitBacking = CreateImage("PortraitBacking", root, PortraitSurface);
            SetAnchors(_portraitBacking.rectTransform, new Vector2(.025f, .38f), new Vector2(.975f, .975f), Vector2.zero, Vector2.zero);
            var nameBand = new GameObject(
                "NameBand",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(PlayerCardSurface));
            nameBand.transform.SetParent(root, false);
            _nameBand = (RectTransform)nameBand.transform;
            SetAnchors(_nameBand, new Vector2(.025f, .23f), new Vector2(.975f, .39f), Vector2.zero, Vector2.zero);
            nameBand.GetComponent<PlayerCardSurface>().SetColors(new Color32(74, 81, 94, 255), NeutralSurface);

            _accentStrip = CreateImage("TeamAccent", root, DefaultAccent);
            SetAnchors(_accentStrip.rectTransform, new Vector2(0f, 1f), Vector2.one,
                Vector2.zero, new Vector2(0f, 4f));

            _portrait = CreateImage("Portrait", root, PortraitSurface);
            SetAnchors(_portrait.rectTransform, new Vector2(0f, 0.38f), new Vector2(1f, 1f),
                new Vector2(8f, 5f), new Vector2(-8f, -10f));

            _yearText = CreateText("Year", root, 11, FontStyle.Bold, TextAnchor.MiddleCenter, TextPrimary);
            SetAnchors(_yearText.rectTransform, new Vector2(.76f, .23f), new Vector2(.97f, .39f), Vector2.zero, Vector2.zero);
            _costText = CreateText("Cost", root, 12, FontStyle.Bold, TextAnchor.MiddleLeft, TextPrimary);
            SetAnchors(_costText.rectTransform, new Vector2(.03f, .11f), new Vector2(.49f, .23f),
                new Vector2(5f, 0f), Vector2.zero);

            _nameText = CreateText("Name", root, 18, FontStyle.Bold, TextAnchor.MiddleCenter, TextPrimary);
            SetAnchors(_nameText.rectTransform, new Vector2(.03f, .23f), new Vector2(.75f, .39f),
                new Vector2(3f, 0f), Vector2.zero);
            _positionText = CreateText("Position", root, 14, FontStyle.Bold, TextAnchor.MiddleLeft, TextSecondary);
            SetAnchors(_positionText.rectTransform, new Vector2(.03f, .85f), new Vector2(.74f, .97f),
                new Vector2(5f, 0f), Vector2.zero);
            _editionText = CreateText("Edition", root, 12, FontStyle.Normal, TextAnchor.MiddleRight, TextMuted);
            SetAnchors(_editionText.rectTransform, new Vector2(0.50f, 0.11f), new Vector2(1f, 0.23f),
                Vector2.zero, new Vector2(-10f, 0f));
            _statusText = CreateText("Status", root, 11, FontStyle.Bold, TextAnchor.MiddleLeft, TextSecondary);
            SetAnchors(_statusText.rectTransform, Vector2.zero, new Vector2(1f, 0.12f),
                new Vector2(10f, 1f), new Vector2(-10f, 0f));
            _statusText.gameObject.SetActive(false);

        }

        private void ApplyVisualState(PlayerMiniCardVisualState visualState, Color accent)
        {
            _accentStrip.color = _usesLineupSlotLayout && string.IsNullOrWhiteSpace(_model?.TeamAccentHex)
                ? new Color32(20, 82, 142, 255) : accent;
            _positionText.color = TextSecondary;
            _statusText.color = visualState == PlayerMiniCardVisualState.Warning ? Warning : TextSecondary;

            if (_usesLineupSlotLayout)
            {
                ApplyLineupSlotVisualState(visualState, accent);
                ApplyEditionFrame();
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
            ApplyEditionFrame();
        }

        private void ApplyEditionFrame()
        {
            if (_model == null || !_model.FrameEdition.HasValue) return;
            Sprite frame = OwnerPlayerCardFrames.Get(_model.FrameEdition.Value, true);
            if (frame == null) return;
            _lineupFrame.sprite = frame;
            _lineupFrame.color = Color.white;
            _lineupFrame.gameObject.SetActive(true);
            _portraitBacking.gameObject.SetActive(false);
            _nameBand.gameObject.SetActive(false);
            _editionText.gameObject.SetActive(false);
            _accentStrip.gameObject.SetActive(false);
            _portrait.color = _portrait.sprite == null ? Color.clear : Color.white;
            float top = _usesLineupSlotLayout ? .89f : 1f;
            Rect name = OwnerPlayerCardFrames.GetNameRect(_model.FrameEdition.Value, true);
            SetAnchors(_lineupFrame.rectTransform, Vector2.zero, new Vector2(1f, top), Vector2.zero, Vector2.zero);
            SetAnchors(_portrait.rectTransform, new Vector2(.12f, top * OwnerPlayerCardFrames.GetPortraitBottom(_model.FrameEdition.Value, true)), new Vector2(.88f, top * .86f), Vector2.zero, Vector2.zero);
            OwnerPlayerCardFrames.SetDecoration((RectTransform)transform, frame, _model.FrameEdition.Value,
                true, top, _portrait.transform.GetSiblingIndex() + 1);
            SetAnchors(_nameText.rectTransform, new Vector2(name.xMin, top * name.yMin), new Vector2(name.xMax, top * name.yMax), Vector2.zero, Vector2.zero);
            SetAnchors(_yearText.rectTransform, new Vector2(.77f, top * name.yMin), new Vector2(.91f, top * name.yMax), Vector2.zero, Vector2.zero);
            SetAnchors(_costText.rectTransform, new Vector2(.06f, top * .085f), new Vector2(.94f, top * .165f), Vector2.zero, Vector2.zero);
            SetAnchors(_statusText.rectTransform, new Vector2(.04f, .01f), new Vector2(.96f, top * .085f), Vector2.zero, Vector2.zero);
            _nameText.color = _yearText.color = OwnerPlayerCardFrames.GetNameColor(_model.FrameEdition.Value);
            _costText.alignment = _statusText.alignment = TextAnchor.MiddleCenter;
            // 80px 슬롯의 실제 텍스트 영역 높이에 맞춰 수치와 한국어 이름의 잘림을 막는다.
            SetBestFitRange(_nameText, 6, _usesLineupSlotLayout ? 12 : 18);
            SetBestFitRange(_yearText, 5, _usesLineupSlotLayout ? 9 : 12);
            SetBestFitRange(_costText, 6, _usesLineupSlotLayout ? 10 : 14);
            SetBestFitRange(_statusText, 5, _usesLineupSlotLayout ? 8 : 11);
            if (_model.Cost.HasValue)
            {
                if (_costStars == null)
                {
                    var row = new GameObject("CostStars", typeof(RectTransform));
                    row.transform.SetParent(transform, false);
                    _costStars = (RectTransform)row.transform;
                }
                _costStars.gameObject.SetActive(true);
                SetAnchors(_costStars, new Vector2(.045f, top * .095f), new Vector2(.79f, top * .16f), Vector2.zero, Vector2.zero);
                OwnerPlayerCardFrames.SetCostStars(_costStars, _model.FrameEdition.Value, _model.Cost.Value);
                _costText.text = _model.Cost.Value.ToString();
                SetAnchors(_costText.rectTransform, new Vector2(.81f, top * .085f), new Vector2(.97f, top * .165f), Vector2.zero, Vector2.zero);
            }
        }

        private void ApplyLineupSlotVisualState(PlayerMiniCardVisualState visualState, Color accent)
        {
            bool isSelected = visualState == PlayerMiniCardVisualState.Selected;
            _lineupFrame.color = visualState == PlayerMiniCardVisualState.Warning
                ? new Color(1f, 0.88f, 0.62f, 1f)
                : isSelected ? new Color(0.72f, 0.86f, 1f, 1f) : Color.white;
            _surface.color = visualState == PlayerMiniCardVisualState.Warning
                ? new Color(0.45f, 0.28f, 0.08f, 1f)
                : isSelected ? SelectedSurface : new Color32(238, 238, 232, 255);
            _outline.effectColor = visualState == PlayerMiniCardVisualState.Warning
                ? CareerUiTheme.Warning
                : isSelected ? accent : new Color(0.65f, 0.71f, 0.75f, 1f);
            _outline.effectDistance = isSelected ? new Vector2(2f, -2f) : new Vector2(1f, -1f);

            _nameText.color = Color.white;
            _yearText.color = TextPrimary;
            _costText.color = TextPrimary;
            _editionText.color = TextPrimary;
            _positionText.color = isSelected || visualState == PlayerMiniCardVisualState.Warning
                ? Color.white : new Color32(44, 44, 44, 255);
            _statusText.color = visualState == PlayerMiniCardVisualState.Warning
                ? new Color(1f, 0.76f, 0.30f, 1f)
                : TextSecondary;
        }

        /// <summary>카드 중앙 배지로 현재 배치 역할을 구분한다.</summary>
        public void SetAssignmentBadge(string assignmentLabel)
        {
            bool isAssigned = !string.IsNullOrWhiteSpace(assignmentLabel);
            if (_positionText != null) _positionText.gameObject.SetActive(true);
            if (_assignmentBadge == null && !isAssigned) return;
            if (_assignmentBadge == null)
            {
                _assignmentBadge = CreateImage("AssignmentBadge", transform, new Color(0.04f, 0.36f, 0.65f, 0.97f));
                SetAnchors(_assignmentBadge.rectTransform, new Vector2(0.03f, 0.44f),
                    new Vector2(0.97f, 0.56f), Vector2.zero, Vector2.zero);
                _assignmentText = CreateText("AssignmentLabel", _assignmentBadge.transform, 12,
                    FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
                SetAnchors(_assignmentText.rectTransform, Vector2.zero, Vector2.one,
                    new Vector2(2f, 0f), new Vector2(-2f, 0f));
                SetBestFitRange(_assignmentText, 8, 12);
            }
            _assignmentBadge.gameObject.SetActive(isAssigned);
            _assignmentText.text = isAssigned ? "배치 중 · " + assignmentLabel : string.Empty;
            _assignmentBadge.transform.SetAsLastSibling();
        }

        /// <summary>원 소속 구단의 정본 엠블럼을 초상 왼쪽에 표시한다.</summary>
        public void SetTeamIdentity(string teamDisplayName)
        {
            if (_teamEmblem == null)
            {
                _teamEmblem = CreateImage("TeamEmblem", transform, Color.white);
                SetAnchors(_teamEmblem.rectTransform, new Vector2(.04f, .65f), new Vector2(.35f, .86f),
                    Vector2.zero, Vector2.zero);
            }
            bool shown = !string.IsNullOrWhiteSpace(teamDisplayName) &&
                TeamEmblemSprites.TryApply(_teamEmblem, 0, teamDisplayName);
            _teamEmblem.gameObject.SetActive(shown);
        }

        private void HandleSelected()
        {
            if (_model == null) return;
            if (_usesPrimaryClickForDetail)
            {
                DetailRequested?.Invoke(_model);
                return;
            }
            if (_model.IsInteractable) Selected?.Invoke(_model);
        }

        private void UpdateButtonInteractable()
        {
            if (_button != null)
                _button.interactable = _model != null && (_model.IsInteractable || _usesPrimaryClickForDetail);
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
