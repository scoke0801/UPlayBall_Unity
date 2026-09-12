using System.Collections.Generic;
using Baseball.Game.Career;
using Baseball.Game.Guide;
using Baseball.Game.Historical;
using Baseball.Game.Manager;
using Baseball.Presentation.Career;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.Shop;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Guide
{
    /// <summary>여섯 PresentationType을 한 Queue에서 소비하는 프런트 매니저 공통 Presenter다.</summary>
    public sealed class UI_System_FrontManagerGuide : UIBase
    {
        private static readonly Color PanelColor = CareerUiTheme.ReferencePanel;
        private static readonly Color AccentColor = CareerUiTheme.PrimaryBright;
        private static readonly Color TextColor = CareerUiTheme.TextOnLight;
        private static readonly Vector2 DialogueSize = new(1080f, 360f);
        [SerializeField, Range(0f, 1f)] private float _dialogueBottomAnchor = 0.25f;
        [SerializeField, Range(0f, 0.1f)] private float _dialogueRightMargin = 0.05f;
        [SerializeField, Range(0f, 1f)] private float _backgroundDimAlpha = 0.72f;
        [SerializeField, Range(1f, 1.25f)] private float _dialogueScale = 1.25f;
        private readonly FrontManagerGuideCtaRouter _router = new();
        private readonly List<string> _suppressionContexts = new(2);
        private GuideManager _manager;
        private UI_Scene_CareerDashboard _careerDashboard;
        private UI_Scene_Shop _shop;
        private GuideMessage _message;
        private Image _overlay;
        private RectTransform _panel;
        private Image _portrait;
        private Text _expressionFallback;
        private Text _managerLabel;
        private Text _messageText;
        private Button _ctaButton;
        private Text _ctaLabel;
        private Button _dismissButton;
        private Text _dismissLabel;
        private float _remainingAutoDismiss;
        private bool _wasHomeVisible;
        private int _homeEntrySequence;

        public override UILayer Layer => UILayer.System;
        public override bool BlocksLowerInput =>
            _message != null && (_message.RequiresAcknowledgement ||
                                 _message.PresentationType is GuidePresentationType.FullDialogue or
                                     GuidePresentationType.ModalCelebration);
        public override bool CanCloseWithCancel => _message != null && !_message.RequiresAcknowledgement;

        public static UI_System_FrontManagerGuide CreateRuntime(Transform parent)
        {
            var uiObject = new GameObject(
                nameof(UI_System_FrontManagerGuide),
                typeof(RectTransform),
                typeof(CanvasGroup));
            uiObject.transform.SetParent(parent, false);
            UI_System_FrontManagerGuide ui = uiObject.AddComponent<UI_System_FrontManagerGuide>();
            Stretch(uiObject.GetComponent<RectTransform>());
            return ui;
        }

        protected override void OnInitialize()
        {
            _manager = GameManager.EnsureExists().EnsureManager<GuideManager>("GuideManager");
            BuildHierarchy();
        }

        protected override void OnShow()
        {
            if (_message != null)
                Render(_message);
        }

        public override void Close()
        {
            if (_message != null && _message.RequiresAcknowledgement)
                return;
            CompleteCurrent();
        }

        private void Update()
        {
            // 구단주 안내는 셸 예약 공간에서 소비한다. 선수 커리어의 기존 표현은 유지한다.
            if (UiGameModeSession.IsSelected(UiGameMode.OwnerCareer)) { Hide(); return; }
            if (PauseForShopFlow())
                return;
            if (_message != null)
            {
                if (!IsVisible)
                {
                    float remaining = _remainingAutoDismiss;
                    Show();
                    _remainingAutoDismiss = remaining;
                }
                if (_remainingAutoDismiss > 0f)
                {
                    _remainingAutoDismiss -= Time.unscaledDeltaTime;
                    if (_remainingAutoDismiss <= 0f)
                        CompleteCurrent();
                }
                return;
            }
            if (_manager == null || !_manager.IsAvailable)
                return;
            UpdateHomeEntryState();
            if (_manager.QueuedCount == 0)
                return;

            GuideDisplayContext context = BuildDisplayContext();
            if (_manager.TryDequeue(context, out GuideMessage next))
            {
                _message = next;
                Show();
                Render(next);
            }
        }

        private GuideDisplayContext BuildDisplayContext()
        {
            _suppressionContexts.Clear();
            if (UI_CareerPresentation.IsPlaying)
                _suppressionContexts.Add("BlockingCinematic");

            CareerMatchSession match = CareerManager.Instance?.ActiveMatch;
            bool isMatchInProgress = match != null && match.Phase == CareerMatchPhase.Playing;
            bool isPlayerInput = isMatchInProgress &&
                                 (match.PendingDecision.HasValue ||
                                  match.PendingPitchingDecision.HasValue ||
                                  match.PendingPitchSelection.HasValue ||
                                  match.PendingSwingExecution.HasValue);
            if (isPlayerInput)
                _suppressionContexts.Add("PlayerMinigameInput");

            string homeEntryId = _wasHomeVisible ? "career-home:" + _homeEntrySequence : string.Empty;
            return new GuideDisplayContext(
                _suppressionContexts,
                isMatchInProgress,
                isSafePoint: !isPlayerInput && !UI_CareerPresentation.IsPlaying,
                homeEntryId, GuideModeScope.Career);
        }

        private void UpdateHomeEntryState()
        {
            if (_careerDashboard == null)
            {
                _careerDashboard = FindFirstObjectByType<UI_Scene_CareerDashboard>(
                    FindObjectsInactive.Include);
            }
            bool isHomeVisible = _careerDashboard != null && _careerDashboard.IsVisible;
            if (isHomeVisible && !_wasHomeVisible)
                _homeEntrySequence++;
            _wasHomeVisible = isHomeVisible;
        }

        private void Render(GuideMessage message)
        {
            // 알림 유형과 무관하게 배경을 낮추고, 뒤에 생성한 프레임과 초상화는 선명하게 유지한다.
            _overlay.color = new Color(0f, 0f, 0f, _backgroundDimAlpha);
            _overlay.raycastTarget = BlocksLowerInput;
            _messageText.text = message.Text;

            OwnerModeManager ownerManager = OwnerModeManager.Instance;
            Sprite sprite = ownerManager != null && ownerManager.HasActiveRuntime
                ? FrontManagerPortraitSprites.LoadForManager(
                    ownerManager.Runtime.OwnerProfile.FrontManagerId, message.ExpressionAssetKey)
                : FrontManagerPortraitSprites.Load(message.ExpressionAssetKey);
            _portrait.sprite = sprite;
            _portrait.color = sprite != null ? Color.white : GetExpressionColor(message.Expression);
            _expressionFallback.gameObject.SetActive(sprite == null);
            _expressionFallback.text = GetExpressionLabel(message.Expression);

            bool canRoute = message.Cta.HasValue && _router.CanRoute(message);
            _ctaButton.gameObject.SetActive(canRoute);
            if (canRoute)
                _ctaLabel.text = message.Cta.Value.Label;

            _dismissButton.gameObject.SetActive(true);
            _dismissLabel.text = message.RequiresAcknowledgement ? "확인" : "×";
            ConfigureLayout();
            _remainingAutoDismiss = message.RequiresAcknowledgement ? 0f : message.AutoDismissSeconds;
        }

        private bool PauseForShopFlow()
        {
            if (_shop == null)
                _shop = FindFirstObjectByType<UI_Scene_Shop>(FindObjectsInactive.Include);
            if (_shop == null || !_shop.IsGuideSuppressed)
                return false;

            // 이미 표시한 안내도 보관한 채 숨긴다. 숨긴 동안 자동 닫힘 시간과 대기열을 소비하지 않는다.
            Hide();
            return true;
        }

        private void HandleCta()
        {
            if (_message != null && _router.TryRoute(_message))
                CompleteCurrent();
        }

        private void CompleteCurrent()
        {
            _message = null;
            _remainingAutoDismiss = 0f;
            Hide();
        }

        private void BuildHierarchy()
        {
            RectTransform root = (RectTransform)transform;
            Stretch(root);
            _overlay = CreateImage("Overlay", root, Color.clear, Vector2.zero, Vector2.zero, stretch: true);
            Image frame = CreateImage("Panel", root, PanelColor, new Vector2(900f, 300f), Vector2.zero);
            frame.sprite = Resources.Load<Sprite>("FrontManager/FM_DialogueFrame_V1");
            frame.color = frame.sprite != null ? Color.white : PanelColor;
            frame.raycastTarget = true;
            _panel = frame.rectTransform;

            _portrait = CreateImage("Portrait", _panel, Color.white,
                new Vector2(172f, 172f), new Vector2(-298f, 0f));
            _portrait.preserveAspect = true;
            _expressionFallback = CreateText("ExpressionFallback", _portrait.transform, "FM", 28,
                FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(160f, 160f), Vector2.zero, TextColor);
            _managerLabel = CreateText("ManagerLabel", _panel, "매니저", 17,
                FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(180f, 28f), Vector2.zero, TextColor);
            _messageText = CreateText("Message", _panel, string.Empty, 20,
                FontStyle.Normal, TextAnchor.UpperLeft, new Vector2(570f, 108f), new Vector2(96f, 4f), TextColor);
            _messageText.lineSpacing = 1.25f;
            _messageText.resizeTextForBestFit = true;
            _messageText.resizeTextMinSize = 15;
            _messageText.resizeTextMaxSize = 20;
            // 글꼴의 기준선 여백 대신 실제 글자 영역을 탭 중앙에 맞춘다.
            _managerLabel.alignByGeometry = true;

            _ctaButton = CreateButton("CTA", _panel, string.Empty, new Vector2(210f, 46f),
                new Vector2(184f, -75f), AccentColor, out _ctaLabel);
            _ctaButton.onClick.AddListener(HandleCta);
            _dismissButton = CreateButton("Dismiss", _panel, "닫기", new Vector2(120f, 46f),
                new Vector2(354f, -75f), CareerUiTheme.SecondaryAction, out _dismissLabel);
            _dismissButton.onClick.AddListener(CompleteCurrent);
            gameObject.AddComponent<CareerUiPreserveTextColor>();
        }

        private void ConfigureLayout()
        {
            Vector2 size = DialogueSize;
            // 모든 화면과 안내 유형이 같은 외곽 프레임 계약을 사용한다.
            Vector2 anchor = new Vector2(1f - _dialogueRightMargin, _dialogueBottomAnchor);
            _panel.anchorMin = _panel.anchorMax = anchor;
            _panel.pivot = new Vector2(1f, 0f);
            _panel.sizeDelta = size;
            // 프레임·캐릭터·글자·버튼을 같은 배율로 키우고 화면 가장자리 기준점은 유지한다.
            _panel.localScale = Vector3.one * _dialogueScale;
            _panel.anchoredPosition = Vector2.zero;
            // 프레임과 대사의 비율을 함께 바꿔 모든 안내 유형에서 오른쪽 초상화 영역을 비운다.
            // 원본 프레임(2048×682)의 탭 내부 경계다. 늘어난 프레임과 같은 비율로 정렬한다.
            _managerLabel.rectTransform.anchorMin = new Vector2(32f / 2048f, 1f - 66f / 682f);
            _managerLabel.rectTransform.anchorMax = new Vector2(408f / 2048f, 1f - 22f / 682f);
            _managerLabel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _managerLabel.rectTransform.offsetMin = Vector2.zero;
            _managerLabel.rectTransform.offsetMax = Vector2.zero;
            _managerLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            _managerLabel.verticalOverflow = VerticalWrapMode.Overflow;
            SetContentRect(_messageText.rectTransform, new Vector2(0f, 1f),
                new Vector2(36f, -56f), new Vector2(size.x * 0.64f, size.y - 136f));
            SetContentRect(_portrait.rectTransform, new Vector2(1f, 0f),
                new Vector2(-22f, 18f), new Vector2(size.x * 0.27f, size.y + 12f));
            SetContentRect((RectTransform)_ctaButton.transform, Vector2.zero,
                new Vector2(36f, 32f), new Vector2(210f, 38f));
            bool requiresAcknowledgement = _message != null && _message.RequiresAcknowledgement;
            SetContentRect((RectTransform)_dismissButton.transform,
                requiresAcknowledgement ? Vector2.zero : Vector2.one,
                requiresAcknowledgement ? new Vector2(260f, 32f) : new Vector2(-22f, -32f),
                requiresAcknowledgement ? new Vector2(100f, 38f) : new Vector2(32f, 32f));
            _dismissLabel.fontSize = requiresAcknowledgement ? 15 : 26;
            Stretch(_ctaLabel.rectTransform);
            Stretch(_dismissLabel.rectTransform);
        }

        private void LateUpdate()
        {
            // 구매 버튼·코루틴이 Update 이후 연출을 열어도 렌더링 전에 안내와 입력 차단을 숨긴다.
            if (PauseForShopFlow())
                return;
            if (_message == null || !IsVisible)
                return;

            // 해상도가 바뀌어도 같은 정규화 위치와 기준 크기를 다시 적용한다.
            ConfigureLayout();
            _overlay.color = new Color(0f, 0f, 0f, _backgroundDimAlpha);
        }

        private static void SetContentRect(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static string GetExpressionLabel(GuideExpression expression) => expression switch
        {
                GuideExpression.Welcome => "환영",
                GuideExpression.Analysis => "분석",
                GuideExpression.Concerned => "확인",
                GuideExpression.Warning => "주의",
                GuideExpression.Celebrate => "승리",
                GuideExpression.Surprised => "소식",
                GuideExpression.Calm => "침착",
            _ => "FM"
        };

        private static Color GetExpressionColor(GuideExpression expression) => expression switch
        {
            GuideExpression.Warning => CareerUiTheme.Error,
            GuideExpression.Concerned => CareerUiTheme.Warning,
            GuideExpression.Celebrate => CareerUiTheme.Success,
            GuideExpression.Surprised => CareerUiTheme.AccentGold,
            GuideExpression.Welcome => CareerUiTheme.PrimaryBright,
            _ => CareerUiTheme.Primary
        };

        private static Image CreateImage(
            string name,
            Transform parent,
            Color color,
            Vector2 size,
            Vector2 position,
            bool stretch = false)
        {
            RectTransform rect = CreateRect(name, parent, size, position);
            if (stretch)
                Stretch(rect);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text CreateText(
            string name,
            Transform parent,
            string value,
            int fontSize,
            FontStyle style,
            TextAnchor alignment,
            Vector2 size,
            Vector2 position,
            Color color)
        {
            RectTransform rect = CreateRect(name, parent, size, position);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = Baseball.Presentation.UI.UIProjectFonts.Default;
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(
            string name,
            Transform parent,
            string label,
            Vector2 size,
            Vector2 position,
            Color color,
            out Text text)
        {
            Image image = CreateImage(name, parent, color, size, position);
            image.raycastTarget = true;
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.14f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
            button.colors = colors;
            text = CreateText("Label", image.transform, label, 15, FontStyle.Bold,
                TextAnchor.MiddleCenter, size - new Vector2(12f, 8f), Vector2.zero, TextColor);
            return button;
        }

        private static RectTransform CreateRect(
            string name,
            Transform parent,
            Vector2 size,
            Vector2 position)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
