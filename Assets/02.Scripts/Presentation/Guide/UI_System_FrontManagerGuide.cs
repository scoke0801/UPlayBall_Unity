using System;
using System.Collections.Generic;
using Baseball.Game.Career;
using Baseball.Game.Guide;
using Baseball.Game.Manager;
using Baseball.Presentation.Career;
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
        private const float CompactGuideTopInset = 204f;
        [SerializeField, Range(1f, 1.25f)] private float _dialogueScale = 1.25f;
        private readonly FrontManagerGuideCtaRouter _router = new();
        private readonly List<string> _suppressionContexts = new(2);
        private GuideManager _manager;
        private UI_Scene_CareerDashboard _careerDashboard;
        private GuideMessage _message;
        private Image _overlay;
        private RectTransform _panel;
        private Image _portrait;
        private Text _expressionFallback;
        private Text _typeLabel;
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
            if (_message != null)
            {
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
                homeEntryId);
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
            _overlay.color = BlocksLowerInput ? new Color(0f, 0f, 0f, 0.56f) : Color.clear;
            _overlay.raycastTarget = BlocksLowerInput;
            _messageText.text = message.Text;
            _typeLabel.text = GetTypeLabel(message.PresentationType);
            _typeLabel.color = GetExpressionColor(message.Expression);

            Sprite sprite = Resources.Load<Sprite>("FrontManager/" + message.ExpressionAssetKey);
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
            ConfigureLayout(message.PresentationType);
            _remainingAutoDismiss = message.RequiresAcknowledgement ? 0f : message.AutoDismissSeconds;
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
            _typeLabel = CreateText("Type", _panel, string.Empty, 14,
                FontStyle.Bold, TextAnchor.MiddleLeft, new Vector2(410f, 28f), new Vector2(16f, 73f), AccentColor);
            _messageText = CreateText("Message", _panel, string.Empty, 20,
                FontStyle.Normal, TextAnchor.UpperLeft, new Vector2(570f, 108f), new Vector2(96f, 4f), TextColor);

            _ctaButton = CreateButton("CTA", _panel, string.Empty, new Vector2(210f, 46f),
                new Vector2(184f, -75f), AccentColor, out _ctaLabel);
            _ctaButton.onClick.AddListener(HandleCta);
            _dismissButton = CreateButton("Dismiss", _panel, "닫기", new Vector2(120f, 46f),
                new Vector2(354f, -75f), CareerUiTheme.SecondaryAction, out _dismissLabel);
            _dismissButton.onClick.AddListener(CompleteCurrent);
            gameObject.AddComponent<CareerUiPreserveTextColor>();
        }

        private void ConfigureLayout(GuidePresentationType type)
        {
            Vector2 size;
            Vector2 anchor;
            Vector2 position;
            switch (type)
            {
                case GuidePresentationType.Toast:
                    size = new Vector2(780f, 260f);
                    anchor = Vector2.one;
                    position = new Vector2(-32f, -CompactGuideTopInset);
                    break;
                case GuidePresentationType.NotificationCard:
                    size = new Vector2(900f, 300f);
                    anchor = Vector2.one;
                    position = new Vector2(-32f, -CompactGuideTopInset);
                    break;
                case GuidePresentationType.FullDialogue:
                case GuidePresentationType.ModalCelebration:
                    size = new Vector2(1080f, 360f);
                    anchor = new Vector2(0.5f, 0f);
                    position = new Vector2(0f, 84f);
                    break;
                case GuidePresentationType.Briefing:
                    size = new Vector2(960f, 320f);
                    anchor = new Vector2(0f, 1f);
                    position = new Vector2(32f, -CompactGuideTopInset);
                    break;
                default:
                    size = new Vector2(900f, 300f);
                    anchor = Vector2.zero;
                    position = new Vector2(32f, 84f);
                    break;
            }
            _panel.anchorMin = _panel.anchorMax = _panel.pivot = anchor;
            _panel.sizeDelta = size;
            // 프레임·캐릭터·글자·버튼을 같은 배율로 키우고 화면 가장자리 기준점은 유지한다.
            _panel.localScale = Vector3.one * _dialogueScale;
            _panel.anchoredPosition = position;
            // 프레임과 대사의 비율을 함께 바꿔 모든 안내 유형에서 오른쪽 초상화 영역을 비운다.
            SetContentRect(_typeLabel.rectTransform, new Vector2(0f, 1f),
                new Vector2(36f, -36f), new Vector2(size.x * 0.64f, 24f));
            SetContentRect(_messageText.rectTransform, new Vector2(0f, 1f),
                new Vector2(36f, -72f), new Vector2(size.x * 0.64f, size.y - 152f));
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

        private static void SetContentRect(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static string GetTypeLabel(GuidePresentationType type) => type switch
        {
            GuidePresentationType.FullDialogue => "FRONT MANAGER · 중요 안내",
            GuidePresentationType.Briefing => "FRONT MANAGER · 브리핑",
            GuidePresentationType.ContextBubble => "FRONT MANAGER · 분석",
            GuidePresentationType.Toast => "FRONT MANAGER",
            GuidePresentationType.NotificationCard => "FRONT MANAGER · 알림",
            GuidePresentationType.ModalCelebration => "FRONT MANAGER · 기록",
            _ => "FRONT MANAGER"
        };

        private static string GetExpressionLabel(GuideExpression expression) => expression switch
        {
            GuideExpression.Welcome => "WELCOME",
            GuideExpression.Analysis => "ANALYSIS",
            GuideExpression.Concerned => "CHECK",
            GuideExpression.Warning => "WARNING",
            GuideExpression.Celebrate => "WIN",
            GuideExpression.Surprised => "NEWS",
            GuideExpression.Calm => "CALM",
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
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
